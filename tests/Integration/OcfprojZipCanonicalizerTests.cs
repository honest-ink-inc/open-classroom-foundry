using System.Buffers.Binary;
using System.IO.Compression;
using Foundry.Storage;

namespace Foundry.Tests.Integration;

public sealed class OcfprojZipCanonicalizerTests
{
    private static readonly DateTimeOffset Stamp = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Unix_host_bytes_are_the_only_bytes_the_post_close_pass_changes()
    {
        var canonical = CreateArchive();
        var unixLike = canonical.ToArray();
        foreach (var offset in OcfprojZipAssertions.CentralHeaderOffsets(unixLike))
        {
            unixLike[offset + 5] = 3;
        }

        using var stream = new MemoryStream(unixLike, writable: true);
        stream.Position = 7;
        OcfprojZipCanonicalizer.Canonicalize(stream);

        Assert.Equal(7, stream.Position);
        Assert.Equal(canonical, stream.ToArray());
        OcfprojZipAssertions.HasCanonicalMetadata(stream.ToArray());

        stream.Position = 3;
        OcfprojZipCanonicalizer.Canonicalize(stream);
        Assert.Equal(3, stream.Position);
        Assert.Equal(canonical, stream.ToArray());
    }

    [Fact]
    public void Noncanonical_external_attributes_are_refused_before_any_mutation()
    {
        var malformed = CreateArchive();
        var firstHeader = OcfprojZipAssertions.CentralHeaderOffsets(malformed)[0];
        BinaryPrimitives.WriteUInt32LittleEndian(malformed.AsSpan(firstHeader + 38), 1);
        var before = malformed.ToArray();

        using var stream = new MemoryStream(malformed, writable: true);
        stream.Position = 5;
        var exception = Assert.Throws<InvalidDataException>(() => OcfprojZipCanonicalizer.Canonicalize(stream));

        Assert.Contains("external attributes", exception.Message, StringComparison.Ordinal);
        Assert.Equal(5, stream.Position);
        Assert.Equal(before, stream.ToArray());
    }

    [Fact]
    public void An_unexpected_host_or_truncated_archive_is_refused_without_mutation()
    {
        var unexpectedHost = CreateArchive();
        var firstHeader = OcfprojZipAssertions.CentralHeaderOffsets(unexpectedHost)[0];
        unexpectedHost[firstHeader + 5] = 9;
        var before = unexpectedHost.ToArray();
        using (var stream = new MemoryStream(unexpectedHost, writable: true))
        {
            Assert.Throws<InvalidDataException>(() => OcfprojZipCanonicalizer.Canonicalize(stream));
            Assert.Equal(before, stream.ToArray());
        }

        using var truncated = new MemoryStream(new byte[21], writable: true);
        Assert.Throws<InvalidDataException>(() => OcfprojZipCanonicalizer.Canonicalize(truncated));
    }

    private static byte[] CreateArchive()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "manifest.json", "{\r\n  \"synthetic\": true\r\n}\r\n"u8);
            WriteEntry(archive, "snapshot.html", "<!doctype html><title>Synthetic</title>"u8);
        }

        return stream.ToArray();
    }

    private static void WriteEntry(ZipArchive archive, string name, ReadOnlySpan<byte> content)
    {
        var entry = OcfprojZipCanonicalizer.CreateDataEntry(archive, name, CompressionLevel.Optimal);
        entry.LastWriteTime = Stamp;
        using var output = entry.Open();
        output.Write(content);
    }
}
