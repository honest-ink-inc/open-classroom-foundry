using System.Buffers.Binary;
using System.IO.Compression;

namespace Foundry.Tests.Integration;

internal static class OcfprojZipAssertions
{
    public static void HasCanonicalMetadata(string path)
        => HasCanonicalMetadata(File.ReadAllBytes(path));

    public static void HasCanonicalMetadata(byte[] package)
    {
        using (var stream = new MemoryStream(package, writable: false))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
        {
            Assert.NotEmpty(archive.Entries);
            Assert.All(archive.Entries, entry => Assert.Equal(0, entry.ExternalAttributes));
        }

        foreach (var offset in CentralHeaderOffsets(package))
        {
            Assert.Equal(0, package[offset + 5]);
            Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(package.AsSpan(offset + 38)));
        }
    }

    public static void HasCanonicalStorageJson(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var jsonEntries = archive.Entries
            .Where(entry => entry.FullName.EndsWith(".json", StringComparison.Ordinal))
            .ToList();
        Assert.NotEmpty(jsonEntries);
        foreach (var entry in jsonEntries)
        {
            using var reader = new StreamReader(entry.Open());
            var json = reader.ReadToEnd();
            Assert.Contains("\r\n", json, StringComparison.Ordinal);
            var withoutCanonicalNewlines = json.Replace("\r\n", string.Empty, StringComparison.Ordinal);
            Assert.DoesNotContain('\r', withoutCanonicalNewlines);
            Assert.DoesNotContain('\n', withoutCanonicalNewlines);
        }
    }

    public static int[] CentralHeaderOffsets(byte[] package)
    {
        var bytes = package.AsSpan();
        var endOffset = bytes.LastIndexOf("PK\u0005\u0006"u8);
        Assert.True(endOffset >= 0);
        Assert.True(endOffset <= bytes.Length - 22);

        var entryCount = BinaryPrimitives.ReadUInt16LittleEndian(bytes[(endOffset + 10)..]);
        var centralOffset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[(endOffset + 16)..]));
        var offsets = new int[entryCount];
        var cursor = centralOffset;
        for (var index = 0; index < entryCount; index++)
        {
            Assert.True(cursor <= endOffset - 46);
            Assert.Equal(0x02014B50u, BinaryPrimitives.ReadUInt32LittleEndian(bytes[cursor..]));
            offsets[index] = cursor;
            var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes[(cursor + 28)..]);
            var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes[(cursor + 30)..]);
            var commentLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes[(cursor + 32)..]);
            cursor = checked(cursor + 46 + nameLength + extraLength + commentLength);
        }

        Assert.Equal(endOffset, cursor);
        return offsets;
    }
}
