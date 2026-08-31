// SPDX-License-Identifier: GPL-3.0-or-later
using System.Security.Cryptography;
using Foundry.Contracts;
using Foundry.Domain;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using FoundryOcrResult = Foundry.Contracts.OcrResult;
using WindowsOcrEngine = Windows.Media.Ocr.OcrEngine;

namespace Foundry.Infrastructure.Windows;

/// <summary>
/// Local OCR for one normalized capture. The service receives only an opaque
/// session reference and reads its fresh metadata-stripped PNG from the session
/// byte store; it never accepts or reconstructs a filesystem path.
/// </summary>
public sealed class WindowsOcrService : IOcrService
{
    private readonly ISessionByteStore _store;
    private readonly IWindowsOcrPlatformRecognizer _recognizer;

    public WindowsOcrService(ISessionByteStore store)
        : this(store, new WindowsOcrPlatformRecognizer())
    {
    }

    internal WindowsOcrService(ISessionByteStore store, IWindowsOcrPlatformRecognizer recognizer)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _recognizer = recognizer ?? throw new ArgumentNullException(nameof(recognizer));
    }

    public async Task<FoundryOcrResult> RecognizeAsync(
        SourceEnvelope source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();

        if (!source.MetadataStripped
            || !string.Equals(source.MimeType, "image/png", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Local OCR accepts only the fresh metadata-stripped PNG retained by the capture session.");
        }

        if (source.PageCount != 1)
        {
            throw new NotSupportedException("Local Windows OCR accepts one normalized image at a time.");
        }

        if (!_store.TryGet(source.Bytes, out var normalizedBytes))
        {
            throw new InvalidOperationException(
                "The session no longer holds the normalized image bytes; re-capture is required.");
        }

        if (normalizedBytes.IsEmpty || normalizedBytes.Length > ImageNormalizer.MaxEncodedImageBytes)
        {
            throw new InvalidDataException("The normalized image exceeds the bounded OCR input contract.");
        }

        var ownedBytes = normalizedBytes.ToArray();
        WindowsOcrPlatformRecognition recognition;
        try
        {
            recognition = await _recognizer
                .RecognizeAsync(ownedBytes, cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(ownedBytes);
        }

        if (string.IsNullOrWhiteSpace(recognition.LanguageTag))
        {
            throw new InvalidOperationException(
                "No Windows OCR language is available for the current user; install a matching Windows OCR language and try again.");
        }

        var tokens = new List<OcrToken>();
        for (var lineIndex = 0; lineIndex < recognition.Lines.Count; lineIndex++)
        {
            tokens.AddRange(AlignLine(recognition.Lines[lineIndex], lineIndex));
        }

        if (tokens.Count == 0)
        {
            throw new InvalidOperationException(
                "Windows OCR did not recognize any text in the normalized image; type the board text manually or capture it again.");
        }

        return new FoundryOcrResult(tokens)
        {
            RecognizerLanguage = recognition.LanguageTag,
        };
    }

    /// <summary>
    /// Aligns Windows' word list to its own exact line text. Matching is ordinal
    /// and forward-only; any disagreement refuses OCR instead of silently
    /// changing formulae, units, punctuation, or reading order. LeadingText owns
    /// each gap before a word, while only the final token owns the line suffix.
    /// </summary>
    private static List<OcrToken> AlignLine(
        WindowsOcrPlatformLine line,
        int lineIndex)
    {
        if (line is null || line.Text is null || line.Words is null)
        {
            throw LineAlignmentFailure();
        }

        if (line.Words.Count == 0)
        {
            if (string.IsNullOrWhiteSpace(line.Text))
            {
                return [];
            }

            throw LineAlignmentFailure();
        }

        var tokens = new List<OcrToken>(line.Words.Count);
        var cursor = 0;
        for (var wordIndex = 0; wordIndex < line.Words.Count; wordIndex++)
        {
            var word = line.Words[wordIndex];
            if (string.IsNullOrWhiteSpace(word))
            {
                throw LineAlignmentFailure();
            }

            var matchIndex = line.Text.IndexOf(word, cursor, StringComparison.Ordinal);
            if (matchIndex < 0)
            {
                throw LineAlignmentFailure();
            }

            var leadingText = line.Text[cursor..matchIndex];
            cursor = checked(matchIndex + word.Length);
            var trailingText = wordIndex == line.Words.Count - 1
                ? line.Text[cursor..]
                : string.Empty;

            // Windows.Media.Ocr.OcrWord exposes Text and BoundingRect but no
            // confidence. Zero is only a numeric placeholder: the explicit
            // flag forces every platform word through teacher verification.
            tokens.Add(new OcrToken(word, Confidence: 0)
            {
                LineIndex = lineIndex,
                ConfidenceAvailable = false,
                LayoutMetadataAvailable = true,
                LeadingText = leadingText,
                TrailingText = trailingText,
            });
        }

        return tokens;
    }

    private static InvalidOperationException LineAlignmentFailure()
        => new(
            "Windows OCR line text did not align exactly with its recognized words; type the board text manually or capture it again.");
}

/// <summary>
/// Injectable boundary around Windows.Media.Ocr. Tests replace this interface so
/// language absence, line structure, and cancellation are deterministic without
/// depending on the current machine's installed recognition packs.
/// </summary>
internal interface IWindowsOcrPlatformRecognizer
{
    Task<WindowsOcrPlatformRecognition> RecognizeAsync(
        ReadOnlyMemory<byte> normalizedImage,
        CancellationToken cancellationToken);
}

internal sealed record WindowsOcrPlatformRecognition(
    string? LanguageTag,
    IReadOnlyList<WindowsOcrPlatformLine> Lines);

internal sealed record WindowsOcrPlatformLine(
    string Text,
    IReadOnlyList<string> Words);

internal sealed class WindowsOcrPlatformRecognizer : IWindowsOcrPlatformRecognizer
{
    public async Task<WindowsOcrPlatformRecognition> RecognizeAsync(
        ReadOnlyMemory<byte> normalizedImage,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Windows owns the capability boundary: AvailableRecognizerLanguages is
        // the installed set, while TryCreateFromUserProfileLanguages refuses when
        // none of those recognizers matches the current user's language profile.
        if (WindowsOcrEngine.AvailableRecognizerLanguages.Count == 0)
        {
            return new WindowsOcrPlatformRecognition(null, []);
        }

        var engine = WindowsOcrEngine.TryCreateFromUserProfileLanguages();
        if (engine is null)
        {
            return new WindowsOcrPlatformRecognition(null, []);
        }

        using var randomAccessStream = new InMemoryRandomAccessStream();
        using var writer = randomAccessStream.AsStreamForWrite();
        await writer.WriteAsync(normalizedImage, cancellationToken).ConfigureAwait(false);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        randomAccessStream.Seek(0);

        cancellationToken.ThrowIfCancellationRequested();
        var decoder = await BitmapDecoder
            .CreateAsync(randomAccessStream)
            .AsTask(cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        // The WinRT engine publishes its own maximum input dimension. Apply that
        // exact bound before allocating the decoded SoftwareBitmap.
        ValidateImageDimensions(decoder.PixelWidth, decoder.PixelHeight, WindowsOcrEngine.MaxImageDimension);

        using var bitmap = await decoder
            .GetSoftwareBitmapAsync(BitmapPixelFormat.Gray8, BitmapAlphaMode.Ignore)
            .AsTask(cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var recognized = await engine
            .RecognizeAsync(bitmap)
            .AsTask(cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<WindowsOcrPlatformLine> lines =
        [
            .. recognized.Lines.Select(line =>
                new WindowsOcrPlatformLine(
                    line.Text,
                    [.. line.Words.Select(word => word.Text)])),
        ];

        return new WindowsOcrPlatformRecognition(engine.RecognizerLanguage.LanguageTag, lines);
    }

    internal static void ValidateImageDimensions(uint width, uint height, uint maximum)
    {
        if (width == 0 || height == 0 || maximum == 0 || width > maximum || height > maximum)
        {
            throw new InvalidDataException(
                $"The normalized image exceeds the Windows OCR maximum dimension of {maximum} pixels.");
        }
    }
}
