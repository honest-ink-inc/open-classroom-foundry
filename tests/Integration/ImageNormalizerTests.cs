using System.Buffers.Binary;
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
        GdiPlusImageEncoder.Save(bitmap, stream, ImageFormat.Png);
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
            GdiPlusImageEncoder.Save(bitmap, stream, ImageFormat.Jpeg);
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
    public async Task Asymmetric_crop_may_end_exactly_at_the_image_edges()
    {
        var source = MakePng(7, 5, bitmap => bitmap.SetPixel(6, 4, Color.Blue));
        var (normalizer, store, envelope) = Setup(source, "image/png");

        var normalized = await normalizer.NormalizeAsync(
            envelope, new NormalizationRequest(Crop: new CropRectangle(2, 1, 5, 4)), CancellationToken.None);

        using var image = Decode(store, normalized);
        Assert.Equal(5, image.Width);
        Assert.Equal(4, image.Height);
        Assert.Equal(Color.Blue.ToArgb(), image.GetPixel(4, 3).ToArgb());
    }

    [Fact]
    public async Task Crop_coordinates_are_validated_and_applied_after_rotation()
    {
        var source = MakePng(6, 3, bitmap => bitmap.SetPixel(5, 0, Color.Red));
        var (normalizer, store, envelope) = Setup(source, "image/png");

        var normalized = await normalizer.NormalizeAsync(
            envelope,
            new NormalizationRequest(
                Rotation: RotationDegrees.Rotate90,
                Crop: new CropRectangle(1, 3, 2, 3)),
            CancellationToken.None);

        using var image = Decode(store, normalized);
        Assert.Equal(2, image.Width);
        Assert.Equal(3, image.Height);
        Assert.Equal(Color.Red.ToArgb(), image.GetPixel(1, 2).ToArgb());
    }

    [Theory]
    [InlineData(-1, 0, 1, 1)]
    [InlineData(0, -1, 1, 1)]
    [InlineData(0, 0, 0, 1)]
    [InlineData(0, 0, 1, 0)]
    [InlineData(35, 0, 6, 1)]
    [InlineData(0, 15, 1, 6)]
    [InlineData(int.MaxValue - 4, 0, 10, 1)]
    [InlineData(0, int.MaxValue - 4, 1, 10)]
    [InlineData(1, 0, int.MaxValue, 1)]
    [InlineData(0, 1, 1, int.MaxValue)]
    public async Task Invalid_crop_is_refused_without_storing_a_generation_or_mutating_the_source(
        int x,
        int y,
        int width,
        int height)
    {
        var source = MakePng(40, 20, bitmap => bitmap.SetPixel(5, 5, Color.Red));
        var store = new PutRecordingStore();
        var sourceReference = store.Put(source);
        var envelope = new SourceEnvelope(
            "file-import",
            "image/png",
            1,
            DataLane.Amber,
            false,
            string.Empty,
            sourceReference);

        var failure = await Assert.ThrowsAsync<ArgumentException>(
            () => new ImageNormalizer(store).NormalizeAsync(
                envelope,
                new NormalizationRequest(Crop: new CropRectangle(x, y, width, height)),
                CancellationToken.None));

        Assert.Contains("wholly within", failure.Message, StringComparison.Ordinal);
        Assert.Equal(1, store.PutCount);
        Assert.Equal(0, store.ReleaseCount);
        Assert.Equal(1, store.Count);
        Assert.True(store.TryGet(sourceReference, out var retained));
        Assert.Equal(source, retained.ToArray());
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

    [Fact]
    public async Task Encoded_capture_bytes_are_bounded_before_decode()
    {
        var oversized = new byte[ImageNormalizer.MaxEncodedImageBytes + 1];
        var (normalizer, _, envelope) = Setup(oversized, "image/png");

        var failure = await Assert.ThrowsAsync<InvalidDataException>(
            () => normalizer.NormalizeAsync(envelope, new NormalizationRequest(), CancellationToken.None));

        Assert.Contains("encoded-image contract", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Declared_dimensions_are_bounded_before_canvas_allocation()
    {
        var forged = MakePng(1, 1);
        BinaryPrimitives.WriteUInt32BigEndian(
            forged.AsSpan(16, 4),
            (uint)ImageNormalizer.MaxImageDimension + 1);
        var (normalizer, _, envelope) = Setup(forged, "image/png");

        var failure = await Assert.ThrowsAsync<InvalidDataException>(
            () => normalizer.NormalizeAsync(envelope, new NormalizationRequest(), CancellationToken.None));

        Assert.Contains("decoded-image contract", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Normalization_returns_before_background_decode_and_encode_complete()
    {
        using var store = new ThreadRecordingStore(blockRead: true);
        using var stopFailsafe = new ManualResetEventSlim(initialState: false);
        var reference = store.Put(MakePng(40, 20));
        var envelope = new SourceEnvelope(
            "file-import",
            "image/png",
            1,
            DataLane.Amber,
            false,
            string.Empty,
            reference);
        var failsafe = new Thread(() =>
        {
            if (!stopFailsafe.Wait(TimeSpan.FromSeconds(30)))
            {
                store.AllowRead.Set();
            }
        })
        {
            IsBackground = true,
            Name = "image-normalizer-test-failsafe",
        };
        failsafe.Start();

        try
        {
            var normalization = new ImageNormalizer(store).NormalizeAsync(
                envelope,
                new NormalizationRequest(),
                CancellationToken.None);

            Assert.False(
                store.AllowRead.IsSet,
                "NormalizeAsync did not return until the blocking decode read was released.");
            Assert.False(normalization.IsCompleted, "Normalization completed while its source read remained blocked.");

            store.AllowRead.Set();
            var normalized = await normalization.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.True(store.ReadEntered.IsCompletedSuccessfully, "Normalization completed without reading its source.");
            Assert.True(normalized.MetadataStripped);
        }
        finally
        {
            store.AllowRead.Set();
            stopFailsafe.Set();
            failsafe.Join();
        }
    }

    [Fact]
    public async Task Cancellation_after_background_dispatch_stops_before_an_output_is_stored()
    {
        using var store = new ThreadRecordingStore(blockRead: true);
        var reference = store.Put(MakePng(40, 20));
        var envelope = new SourceEnvelope(
            "file-import",
            "image/png",
            1,
            DataLane.Amber,
            false,
            string.Empty,
            reference);
        using var cancellation = new CancellationTokenSource();

        var normalization = new ImageNormalizer(store).NormalizeAsync(
            envelope,
            new NormalizationRequest(),
            cancellation.Token);
        try
        {
            var readEntered = store.ReadEntered;
            var completed = await Task.WhenAny(readEntered, Task.Delay(TimeSpan.FromSeconds(30)));
            Assert.True(
                ReferenceEquals(readEntered, completed),
                "The background normalization did not reach its source read within the deadlock watchdog.");
            await readEntered;
            cancellation.Cancel();
        }
        finally
        {
            store.AllowRead.Set();
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => normalization);
        Assert.Equal(1, store.Count);
    }

    [Fact]
    public async Task Cancellation_after_output_storage_releases_and_zeroes_the_tentative_result()
    {
        using var store = new PostPutCancellationStore();
        var sourceReference = store.Put(MakePng(40, 20));
        var envelope = new SourceEnvelope(
            "file-import",
            "image/png",
            1,
            DataLane.Amber,
            false,
            string.Empty,
            sourceReference);
        using var cancellation = new CancellationTokenSource();

        var normalization = new ImageNormalizer(store).NormalizeAsync(
            envelope,
            new NormalizationRequest(),
            cancellation.Token);
        var outputStored = store.OutputStored;
        var completed = await Task.WhenAny(outputStored, Task.Delay(TimeSpan.FromSeconds(30)));
        Assert.True(
            ReferenceEquals(outputStored, completed),
            "The background normalization did not store its tentative output within the deadlock watchdog.");
        await outputStored;
        cancellation.Cancel();
        store.AllowOutputPutReturn.Set();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => normalization);
        Assert.Equal(1, store.Count);
        Assert.True(store.TryGet(sourceReference, out _));
        Assert.All(store.HeldOutput.ToArray(), value => Assert.Equal(0, value));
    }

    [Fact]
    public void Explicit_encoder_resolution_is_safe_during_simultaneous_first_saves()
    {
        Parallel.For(0, 64, index =>
        {
            using var bitmap = new Bitmap(8, 8, PixelFormat.Format24bppRgb);
            using var stream = new MemoryStream();
            var format = index % 2 == 0 ? ImageFormat.Png : ImageFormat.Jpeg;

            GdiPlusImageEncoder.Save(bitmap, stream, format);

            Assert.True(stream.Length > 0);
        });
    }

    private sealed class ThreadRecordingStore(bool blockRead) : ISessionByteStore, IDisposable
    {
        private readonly InMemorySessionByteStore _inner = new();
        private readonly TaskCompletionSource<bool> _readEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task ReadEntered => _readEntered.Task;

        public ManualResetEventSlim AllowRead { get; } = new(initialState: !blockRead);

        public int Count => _inner.Count;

        public SessionByteReference Put(ReadOnlyMemory<byte> content)
            => _inner.Put(content);

        public bool TryGet(SessionByteReference reference, out ReadOnlyMemory<byte> content)
        {
            _readEntered.TrySetResult(true);
            AllowRead.Wait();
            return _inner.TryGet(reference, out content);
        }

        public void Release(SessionByteReference reference)
            => _inner.Release(reference);

        public void PurgeAll()
            => _inner.PurgeAll();

        public void Dispose()
        {
            AllowRead.Set();
            AllowRead.Dispose();
        }
    }

    private sealed class PutRecordingStore : ISessionByteStore
    {
        private readonly InMemorySessionByteStore _inner = new();

        public int Count => _inner.Count;

        public int PutCount { get; private set; }

        public int ReleaseCount { get; private set; }

        public SessionByteReference Put(ReadOnlyMemory<byte> content)
        {
            PutCount++;
            return _inner.Put(content);
        }

        public bool TryGet(SessionByteReference reference, out ReadOnlyMemory<byte> content)
            => _inner.TryGet(reference, out content);

        public void Release(SessionByteReference reference)
        {
            ReleaseCount++;
            _inner.Release(reference);
        }

        public void PurgeAll()
            => _inner.PurgeAll();
    }

    private sealed class PostPutCancellationStore : ISessionByteStore, IDisposable
    {
        private readonly InMemorySessionByteStore _inner = new();
        private readonly TaskCompletionSource<bool> _outputStored = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _putCount;

        public Task OutputStored => _outputStored.Task;

        public ManualResetEventSlim AllowOutputPutReturn { get; } = new(initialState: false);

        public ReadOnlyMemory<byte> HeldOutput { get; private set; }

        public int Count => _inner.Count;

        public SessionByteReference Put(ReadOnlyMemory<byte> content)
        {
            var reference = _inner.Put(content);
            if (Interlocked.Increment(ref _putCount) == 2)
            {
                _inner.TryGet(reference, out var output);
                HeldOutput = output;
                _outputStored.TrySetResult(true);
                AllowOutputPutReturn.Wait();
            }

            return reference;
        }

        public bool TryGet(SessionByteReference reference, out ReadOnlyMemory<byte> content)
            => _inner.TryGet(reference, out content);

        public void Release(SessionByteReference reference) => _inner.Release(reference);

        public void PurgeAll() => _inner.PurgeAll();

        public void Dispose()
        {
            AllowOutputPutReturn.Set();
            AllowOutputPutReturn.Dispose();
        }
    }
}
