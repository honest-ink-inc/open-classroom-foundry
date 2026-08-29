using System.Drawing.Imaging;
using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Infrastructure.Windows;

namespace Foundry.Tests.Integration;

public class ImageNormalizerTests
{
    private static byte[] MakePng(int width, int height, Action<Bitmap>? paint = null)
    {
        using var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.White);
        }

        paint?.Invoke(bitmap);

        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    /// <summary>A JPEG with a valid empty EXIF APP1 segment spliced in after SOI.</summary>
    private static byte[] MakeJpegWithExif()
    {
        byte[] plain;
        using (var bitmap = new Bitmap(8, 8, PixelFormat.Format24bppRgb))
        {
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.White);
            }

            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Jpeg);
            plain = stream.ToArray();
        }

        byte[] exifPayload =
        [
            (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0, 0,          // Exif header
            (byte)'I', (byte)'I', 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00,  // TIFF little-endian, IFD at 8
            0x00, 0x00,                                                 // zero IFD entries
            0x00, 0x00, 0x00, 0x00,                                     // no next IFD
        ];

        var segmentLength = exifPayload.Length + 2;
        var spliced = new byte[plain.Length + 4 + exifPayload.Length];
        spliced[0] = plain[0]; // FF
        spliced[1] = plain[1]; // D8 (SOI)
        spliced[2] = 0xFF;
        spliced[3] = 0xE1;     // APP1
        spliced[4] = (byte)(segmentLength >> 8);
        spliced[5] = (byte)(segmentLength & 0xFF);
        exifPayload.CopyTo(spliced, 6);
        Array.Copy(plain, 2, spliced, 6 + exifPayload.Length, plain.Length - 2);
        return spliced;
    }

    private static bool ContainsAscii(byte[] haystack, string needle)
    {
        var pattern = System.Text.Encoding.ASCII.GetBytes(needle);
        for (var i = 0; i <= haystack.Length - pattern.Length; i++)
        {
            if (haystack.AsSpan(i, pattern.Length).SequenceEqual(pattern))
            {
                return true;
            }
        }

        return false;
    }

    private static (ImageNormalizer Normalizer, InMemorySessionByteStore Store, SourceEnvelope Envelope) Setup(byte[] bytes, string mimeType)
    {
        var store = new InMemorySessionByteStore();
        var reference = store.Put(bytes);
        var envelope = new SourceEnvelope("file-import", mimeType, 1, DataLane.Amber, false, string.Empty, reference);
        return (new ImageNormalizer(store), store, envelope);
    }

    private static Bitmap Decode(InMemorySessionByteStore store, SourceEnvelope envelope)
    {
        Assert.True(store.TryGet(envelope.Bytes, out var bytes));
        return new Bitmap(new MemoryStream(bytes.ToArray()));
    }

    [Fact]
    public async Task Normalization_strips_metadata_by_reencoding()
    {
        var jpeg = MakeJpegWithExif();
        Assert.True(ContainsAscii(jpeg, "Exif"));

        var (normalizer, store, envelope) = Setup(jpeg, "image/jpeg");

        var normalized = await normalizer.NormalizeAsync(envelope, new NormalizationRequest(), CancellationToken.None);

        Assert.True(normalized.MetadataStripped);
        Assert.Equal("image/png", normalized.MimeType);
        Assert.True(store.TryGet(normalized.Bytes, out var output));
        Assert.False(ContainsAscii(output.ToArray(), "Exif"));
    }

    [Fact]
    public async Task Rotation_by_ninety_degrees_swaps_the_dimensions()
    {
        var (normalizer, store, envelope) = Setup(MakePng(40, 20), "image/png");

        var normalized = await normalizer.NormalizeAsync(
            envelope, new NormalizationRequest(Rotation: RotationDegrees.Rotate90), CancellationToken.None);

        using var image = Decode(store, normalized);
        Assert.Equal(20, image.Width);
        Assert.Equal(40, image.Height);
    }

    [Fact]
    public async Task Cropping_yields_the_exact_region()
    {
        var source = MakePng(40, 20, bitmap => bitmap.SetPixel(5, 5, Color.Red));
        var (normalizer, store, envelope) = Setup(source, "image/png");

        var normalized = await normalizer.NormalizeAsync(
            envelope, new NormalizationRequest(Crop: new CropRectangle(4, 4, 10, 10)), CancellationToken.None);

        using var image = Decode(store, normalized);
        Assert.Equal(10, image.Width);
        Assert.Equal(10, image.Height);
        Assert.Equal(Color.Red.ToArgb(), image.GetPixel(1, 1).ToArgb());
    }

    [Fact]
    public async Task A_burned_region_destroys_the_pixels_beneath_it()
    {
        var source = MakePng(40, 20, bitmap => bitmap.SetPixel(10, 10, Color.Red));
        var (normalizer, store, envelope) = Setup(source, "image/png");

        var normalized = await normalizer.NormalizeAsync(
            envelope,
            new NormalizationRequest(RedactionBurns: [new RedactionRegion(1, 8, 8, 6, 6, "name visible")]),
            CancellationToken.None);

        using var image = Decode(store, normalized);
        Assert.Equal(Color.Black.ToArgb(), image.GetPixel(10, 10).ToArgb());
        Assert.Equal(Color.White.ToArgb(), image.GetPixel(30, 10).ToArgb());
    }

    [Fact]
    public async Task Missing_session_bytes_fail_loudly()
    {
        var store = new InMemorySessionByteStore();
        var normalizer = new ImageNormalizer(store);
        var envelope = new SourceEnvelope("file-import", "image/png", 1, DataLane.Amber, false, string.Empty, SessionByteReference.NewReference());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => normalizer.NormalizeAsync(envelope, new NormalizationRequest(), CancellationToken.None));
    }
}
