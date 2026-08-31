using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Infrastructure.Windows;

namespace Foundry.Tests.Integration;

public class WindowsOcrServiceTests
{
    [Fact]
    public async Task Normalized_session_bytes_become_line_aware_mandatory_review_tokens()
    {
        byte[] normalizedBytes = [137, 80, 78, 71, 1, 2, 3, 4];
        var (store, source) = NormalizedSource(normalizedBytes);
        var platform = new RecordingRecognizer(new WindowsOcrPlatformRecognition(
            "en-US",
            [
                Line("x=2", "x", "2"),
                Line("Measure 25 mL.", "Measure", "25", "mL"),
            ]));
        var service = new WindowsOcrService(store, platform);

        var result = await service.RecognizeAsync(source, CancellationToken.None);

        Assert.Equal(normalizedBytes, platform.ObservedBytes);
        Assert.Equal(1, platform.Calls);
        Assert.Collection(
            result.Tokens,
            token => Assert.Equal(UncertainToken("x", 0), token),
            token => Assert.Equal(UncertainToken("2", 0, leadingText: "="), token),
            token => Assert.Equal(UncertainToken("Measure", 1), token),
            token => Assert.Equal(UncertainToken("25", 1, leadingText: " "), token),
            token => Assert.Equal(UncertainToken("mL", 1, leadingText: " ", trailingText: "."), token));
        Assert.Equal("en-US", result.RecognizerLanguage);
        Assert.All(platform.BorrowedBytes.ToArray(), value => Assert.Equal(0, value));

        var transcript = new TranscriptSession(result);
        Assert.Equal("en-US", transcript.RecognizerLanguage);
        Assert.Equal(5, transcript.UnresolvedCount);
        transcript.Resolve(0, "y");
        transcript.Resolve(1, "3");
        transcript.Resolve(2, "Measure");
        transcript.Resolve(3, "30");
        transcript.Resolve(4, "mL");
        Assert.Equal(["y=3", "Measure 30 mL."], transcript.VerifiedLines());
    }

    [Theory]
    [InlineData(false, "image/png", 1)]
    [InlineData(true, "image/jpeg", 1)]
    [InlineData(true, "image/png", 2)]
    public async Task Only_one_fresh_normalized_png_can_cross_the_platform_boundary(
        bool metadataStripped,
        string mimeType,
        int pageCount)
    {
        var (store, source) = NormalizedSource([1, 2, 3]);
        source = source with
        {
            MetadataStripped = metadataStripped,
            MimeType = mimeType,
            PageCount = pageCount,
        };
        var platform = new RecordingRecognizer(SuccessfulRecognition());
        var service = new WindowsOcrService(store, platform);

        var exception = await Record.ExceptionAsync(
            () => service.RecognizeAsync(source, CancellationToken.None));

        Assert.NotNull(exception);
        Assert.Equal(pageCount == 1 ? typeof(InvalidOperationException) : typeof(NotSupportedException), exception.GetType());
        Assert.Equal(0, platform.Calls);
    }

    [Fact]
    public async Task A_released_session_reference_fails_without_calling_the_platform()
    {
        var (store, source) = NormalizedSource([1, 2, 3]);
        store.Release(source.Bytes);
        var platform = new RecordingRecognizer(SuccessfulRecognition());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new WindowsOcrService(store, platform).RecognizeAsync(source, CancellationToken.None));

        Assert.Contains("no longer holds", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, platform.Calls);
    }

    [Fact]
    public async Task Missing_ocr_language_fails_honestly_even_if_a_platform_returns_words()
    {
        var (store, source) = NormalizedSource([1, 2, 3]);
        var platform = new RecordingRecognizer(new WindowsOcrPlatformRecognition(null, [Line("word", "word")]));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new WindowsOcrService(store, platform).RecognizeAsync(source, CancellationToken.None));

        Assert.Contains("language", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Empty_or_whitespace_only_recognition_fails_honestly()
    {
        var (store, source) = NormalizedSource([1, 2, 3]);
        var platform = new RecordingRecognizer(new WindowsOcrPlatformRecognition(
            "en-US",
            [
                Line(string.Empty),
                Line("  "),
            ]));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new WindowsOcrService(store, platform).RecognizeAsync(source, CancellationToken.None));

        Assert.Contains("did not recognize any text", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_platform_line_and_word_mismatch_refuses_ocr_for_manual_fallback()
    {
        var (store, source) = NormalizedSource([1, 2, 3]);
        var platform = new RecordingRecognizer(new WindowsOcrPlatformRecognition(
            "en-US",
            [Line("x=2", "x", "3")]));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new WindowsOcrService(store, platform).RecognizeAsync(source, CancellationToken.None));

        Assert.Contains("did not align exactly", exception.Message, StringComparison.Ordinal);
        Assert.Contains("manually", exception.Message, StringComparison.Ordinal);
        Assert.All(platform.BorrowedBytes.ToArray(), value => Assert.Equal(0, value));
    }

    [Fact]
    public async Task Precancelled_recognition_never_reads_through_the_platform()
    {
        var (store, source) = NormalizedSource([1, 2, 3]);
        var platform = new RecordingRecognizer(SuccessfulRecognition());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new WindowsOcrService(store, platform).RecognizeAsync(source, cancellation.Token));

        Assert.Equal(0, platform.Calls);
    }

    [Fact]
    public async Task Cancellation_during_platform_work_settles_and_zeroes_the_owned_input_copy()
    {
        var (store, source) = NormalizedSource([1, 2, 3]);
        var platform = new BlockingRecognizer();
        using var cancellation = new CancellationTokenSource();
        var pending = new WindowsOcrService(store, platform).RecognizeAsync(source, cancellation.Token);

        await platform.Started;
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.All(platform.BorrowedBytes.ToArray(), value => Assert.Equal(0, value));
    }

    [Fact]
    public void Platform_dimension_gate_honors_the_engine_maximum_at_the_boundary()
    {
        WindowsOcrPlatformRecognizer.ValidateImageDimensions(2_600, 2_600, 2_600);

        Assert.Throws<InvalidDataException>(
            () => WindowsOcrPlatformRecognizer.ValidateImageDimensions(2_601, 2_600, 2_600));
        Assert.Throws<InvalidDataException>(
            () => WindowsOcrPlatformRecognizer.ValidateImageDimensions(0, 2_600, 2_600));
    }

    private static WindowsOcrPlatformRecognition SuccessfulRecognition()
        => new("en-US", [Line("word", "word")]);

    private static WindowsOcrPlatformLine Line(string text, params string[] words)
        => new(text, words);

    private static OcrToken UncertainToken(
        string text,
        int lineIndex,
        string leadingText = "",
        string trailingText = "")
        => new(text, 0)
        {
            LineIndex = lineIndex,
            ConfidenceAvailable = false,
            LayoutMetadataAvailable = true,
            LeadingText = leadingText,
            TrailingText = trailingText,
        };

    private static (InMemorySessionByteStore Store, SourceEnvelope Source) NormalizedSource(byte[] bytes)
    {
        var store = new InMemorySessionByteStore();
        var reference = store.Put(bytes);
        var source = new SourceEnvelope(
            "board-photo",
            "image/png",
            1,
            DataLane.Amber,
            MetadataStripped: true,
            "Synthetic teacher-owned board fixture",
            reference);
        return (store, source);
    }

    private sealed class RecordingRecognizer(WindowsOcrPlatformRecognition recognition)
        : IWindowsOcrPlatformRecognizer
    {
        public int Calls { get; private set; }

        public byte[] ObservedBytes { get; private set; } = [];

        public ReadOnlyMemory<byte> BorrowedBytes { get; private set; }

        public Task<WindowsOcrPlatformRecognition> RecognizeAsync(
            ReadOnlyMemory<byte> normalizedImage,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            BorrowedBytes = normalizedImage;
            ObservedBytes = normalizedImage.ToArray();
            return Task.FromResult(recognition);
        }
    }

    private sealed class BlockingRecognizer : IWindowsOcrPlatformRecognizer
    {
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started => _started.Task;

        public ReadOnlyMemory<byte> BorrowedBytes { get; private set; }

        public async Task<WindowsOcrPlatformRecognition> RecognizeAsync(
            ReadOnlyMemory<byte> normalizedImage,
            CancellationToken cancellationToken)
        {
            BorrowedBytes = normalizedImage;
            _started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The cancellation-aware wait unexpectedly completed.");
        }
    }
}
