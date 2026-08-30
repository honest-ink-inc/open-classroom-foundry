// SPDX-License-Identifier: GPL-3.0-or-later
using System.Buffers.Binary;
using System.IO.Compression;

namespace Foundry.Storage;

/// <summary>
/// Canonicalizes the two host-dependent fields emitted by <see cref="ZipArchive"/>
/// after a new <c>.ocfproj</c> archive has closed. This is deliberately not a
/// general ZIP repairer or validator; callers still validate the staged package.
/// </summary>
internal static class OcfprojZipCanonicalizer
{
    private const uint EndOfCentralDirectorySignature = 0x06054B50;
    private const uint CentralDirectoryHeaderSignature = 0x02014B50;
    private const ushort Zip64ExtraFieldId = 0x0001;
    private const int EndOfCentralDirectoryLength = 22;
    private const int CentralDirectoryHeaderLength = 46;
    private const int MaximumEndSearchLength = EndOfCentralDirectoryLength + ushort.MaxValue;

    /// <summary>
    /// Creates a data-only package entry with an explicit, platform-neutral
    /// external-attributes field. OCF project entries carry no executable or
    /// extraction permission semantics.
    /// </summary>
    internal static ZipArchiveEntry CreateDataEntry(
        ZipArchive archive,
        string name,
        CompressionLevel compressionLevel)
    {
        ArgumentNullException.ThrowIfNull(archive);
        var entry = archive.CreateEntry(name, compressionLevel);
        entry.ExternalAttributes = 0;
        return entry;
    }

    /// <summary>
    /// Changes only the central-directory host byte from .NET's Unix value to
    /// the canonical DOS/Windows value. The complete bounded structure is read
    /// and checked before the first byte is changed.
    /// </summary>
    internal static void Canonicalize(Stream package)
    {
        ArgumentNullException.ThrowIfNull(package);
        if (!package.CanRead || !package.CanWrite || !package.CanSeek)
        {
            throw Invalid("A readable, writable, seekable package stream is required.");
        }

        var originalPosition = package.Position;
        try
        {
            var packageLength = package.Length;
            if (packageLength is < EndOfCentralDirectoryLength
                or > OcfprojPackageValidator.MaxPackageBytes)
            {
                throw Invalid("The package size is outside the admitted bounds.");
            }

            var tailLength = checked((int)Math.Min(packageLength, MaximumEndSearchLength));
            var tailStart = packageLength - tailLength;
            var tail = new byte[tailLength];
            ReadExactlyAt(package, tailStart, tail);

            var candidates = new List<EndRecord>(capacity: 2);
            for (var index = tail.Length - EndOfCentralDirectoryLength; index >= 0; index--)
            {
                var candidate = tail.AsSpan(index, EndOfCentralDirectoryLength);
                if (BinaryPrimitives.ReadUInt32LittleEndian(candidate) != EndOfCentralDirectorySignature)
                {
                    continue;
                }

                var absoluteOffset = tailStart + index;
                if (TryDescribeEndRecord(candidate, absoluteOffset, packageLength, out var description))
                {
                    candidates.Add(description);
                    if (candidates.Count > 1)
                    {
                        throw Invalid("The package has an ambiguous end record.");
                    }
                }
            }

            if (candidates.Count != 1)
            {
                throw Invalid("The package has no unique bounded end record.");
            }

            var end = candidates[0];
            var centralEnd = end.Offset;
            var hostByteOffsets = new long[end.EntryCount];
            var cursor = end.CentralDirectoryOffset;
            Span<byte> header = stackalloc byte[CentralDirectoryHeaderLength];

            for (var entryIndex = 0; entryIndex < end.EntryCount; entryIndex++)
            {
                if (cursor > centralEnd - CentralDirectoryHeaderLength)
                {
                    throw Invalid("A central-directory record is truncated.");
                }

                ReadExactlyAt(package, cursor, header);
                if (BinaryPrimitives.ReadUInt32LittleEndian(header) != CentralDirectoryHeaderSignature)
                {
                    throw Invalid("A central-directory signature is invalid.");
                }

                var hostPlatform = header[5];
                if (hostPlatform is not (0 or 3))
                {
                    throw Invalid("An unexpected host-platform value was emitted.");
                }

                var compressedSize = BinaryPrimitives.ReadUInt32LittleEndian(header[20..]);
                var uncompressedSize = BinaryPrimitives.ReadUInt32LittleEndian(header[24..]);
                var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(header[28..]);
                var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(header[30..]);
                var commentLength = BinaryPrimitives.ReadUInt16LittleEndian(header[32..]);
                var diskStart = BinaryPrimitives.ReadUInt16LittleEndian(header[34..]);
                var externalAttributes = BinaryPrimitives.ReadUInt32LittleEndian(header[38..]);
                var localHeaderOffset = BinaryPrimitives.ReadUInt32LittleEndian(header[42..]);

                if (compressedSize == uint.MaxValue
                    || uncompressedSize == uint.MaxValue
                    || localHeaderOffset == uint.MaxValue)
                {
                    throw Invalid("A ZIP64 central entry is not admitted.");
                }

                if (diskStart != 0)
                {
                    throw Invalid("A multi-disk central entry is not admitted.");
                }

                if (externalAttributes != 0)
                {
                    throw Invalid("Portable external attributes were not set by the writer.");
                }

                if (localHeaderOffset >= end.CentralDirectoryOffset)
                {
                    throw Invalid("A local-header address is outside the payload region.");
                }

                var variableLength = (long)nameLength + extraLength + commentLength;
                var next = cursor + CentralDirectoryHeaderLength + variableLength;
                if (next > centralEnd)
                {
                    throw Invalid("A central-directory record exceeds its declared bounds.");
                }

                ValidateNonZip64ExtraFields(
                    package,
                    cursor + CentralDirectoryHeaderLength + nameLength,
                    extraLength);

                hostByteOffsets[entryIndex] = cursor + 5;
                cursor = next;
            }

            if (cursor != centralEnd)
            {
                throw Invalid("The central directory contains undeclared trailing records.");
            }

            if (package.Length != packageLength)
            {
                throw Invalid("The package changed during canonicalization.");
            }

            // Every structural check is complete. The staging file is still
            // unpublished and held FileShare.None, so no cancellation or
            // fallible parsing occurs after mutation begins.
            foreach (var offset in hostByteOffsets)
            {
                package.Position = offset;
                var current = package.ReadByte();
                if (current == 0)
                {
                    continue;
                }

                if (current != 3)
                {
                    throw Invalid("The package changed during canonicalization.");
                }

                package.Position = offset;
                package.WriteByte(0);
            }
        }
        finally
        {
            package.Position = originalPosition;
        }
    }

    private static bool TryDescribeEndRecord(
        ReadOnlySpan<byte> record,
        long absoluteOffset,
        long packageLength,
        out EndRecord description)
    {
        description = default;
        var commentLength = BinaryPrimitives.ReadUInt16LittleEndian(record[20..]);
        if (absoluteOffset + EndOfCentralDirectoryLength + commentLength != packageLength)
        {
            return false;
        }

        var disk = BinaryPrimitives.ReadUInt16LittleEndian(record[4..]);
        var centralDisk = BinaryPrimitives.ReadUInt16LittleEndian(record[6..]);
        var entriesOnDisk = BinaryPrimitives.ReadUInt16LittleEndian(record[8..]);
        var entryCount = BinaryPrimitives.ReadUInt16LittleEndian(record[10..]);
        var centralDirectorySize = BinaryPrimitives.ReadUInt32LittleEndian(record[12..]);
        var centralDirectoryOffset = BinaryPrimitives.ReadUInt32LittleEndian(record[16..]);

        if (disk != 0
            || centralDisk != 0
            || entriesOnDisk != entryCount
            || entryCount is 0 or > OcfprojPackageValidator.MaxEntries
            || centralDirectorySize == uint.MaxValue
            || centralDirectoryOffset == uint.MaxValue)
        {
            return false;
        }

        var centralStart = (long)centralDirectoryOffset;
        var centralSize = (long)centralDirectorySize;
        var centralEnd = centralStart + centralSize;
        if (centralStart >= absoluteOffset
            || centralEnd != absoluteOffset
            || centralSize < (long)entryCount * CentralDirectoryHeaderLength)
        {
            return false;
        }

        description = new EndRecord(absoluteOffset, entryCount, centralStart);
        return true;
    }

    private static void ValidateNonZip64ExtraFields(Stream package, long offset, ushort length)
    {
        if (length == 0)
        {
            return;
        }

        var fields = new byte[length];
        ReadExactlyAt(package, offset, fields);
        var cursor = 0;
        while (cursor < fields.Length)
        {
            if (cursor > fields.Length - 4)
            {
                throw Invalid("A central extra field is truncated.");
            }

            var headerId = BinaryPrimitives.ReadUInt16LittleEndian(fields.AsSpan(cursor));
            var dataLength = BinaryPrimitives.ReadUInt16LittleEndian(fields.AsSpan(cursor + 2));
            var next = cursor + 4 + dataLength;
            if (next > fields.Length)
            {
                throw Invalid("A central extra field exceeds its declared bounds.");
            }

            if (headerId == Zip64ExtraFieldId)
            {
                throw Invalid("ZIP64 extra fields are not admitted.");
            }

            cursor = next;
        }
    }

    private static void ReadExactlyAt(Stream package, long offset, Span<byte> destination)
    {
        if (offset < 0 || offset > package.Length - destination.Length)
        {
            throw Invalid("A declared ZIP range is outside the package.");
        }

        package.Position = offset;
        while (!destination.IsEmpty)
        {
            var read = package.Read(destination);
            if (read == 0)
            {
                throw Invalid("A declared ZIP range ended unexpectedly.");
            }

            destination = destination[read..];
        }
    }

    private static InvalidDataException Invalid(string detail)
        => new($"A newly written .ocfproj ZIP could not be canonicalized: {detail}");

    private readonly record struct EndRecord(
        long Offset,
        int EntryCount,
        long CentralDirectoryOffset);
}
