// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Contracts;

namespace Foundry.Application;

public enum TranscriptTokenState
{
    Confident,
    Uncertain,
    Resolved,
    Illegible,
}

public sealed record TranscriptToken(
    string RecognizedText,
    double Confidence,
    TranscriptTokenState State,
    string? ResolvedText = null)
{
    public int LineIndex { get; init; }

    public bool ConfidenceAvailable { get; init; } = true;

    public bool LayoutMetadataAvailable { get; init; }

    public string LeadingText { get; init; } = string.Empty;

    public string TrailingText { get; init; } = string.Empty;
}

/// <summary>
/// The OCR uncertainty workflow of Board to Brief (plan §10.2): every token below
/// the confidence threshold must be resolved by the teacher — accepted, retyped,
/// or marked illegible — before a verified transcript exists. Completion with an
/// unresolved uncertain token is impossible; nothing is ever guessed silently.
/// </summary>
public sealed class TranscriptSession
{
    public const string IllegibleMarker = "[illegible]";
    public const double DefaultConfidenceThreshold = 0.85;

    private readonly List<TranscriptToken> _tokens;

    public TranscriptSession(OcrResult recognition, double confidenceThreshold = DefaultConfidenceThreshold)
    {
        ArgumentNullException.ThrowIfNull(recognition);
        if (!double.IsFinite(confidenceThreshold) || confidenceThreshold is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(confidenceThreshold),
                "The confidence threshold must be a finite value from zero through one.");
        }

        var recognizedTokens = recognition.Tokens;
        if (recognizedTokens is null || recognizedTokens.Count == 0)
        {
            throw new ArgumentException("There is nothing to transcribe.", nameof(recognition));
        }

        var previousLineIndex = 0;
        for (var index = 0; index < recognizedTokens.Count; index++)
        {
            var token = recognizedTokens[index];
            if (token is null
                || string.IsNullOrWhiteSpace(token.Text)
                || !double.IsFinite(token.Confidence)
                || token.Confidence is < 0 or > 1)
            {
                throw new ArgumentException(
                    "Every OCR token must contain text and a finite confidence from zero through one.",
                    nameof(recognition));
            }

            if (token.LineIndex < 0 || (index > 0 && token.LineIndex < previousLineIndex))
            {
                throw new ArgumentException(
                    "OCR line indexes must be non-negative and non-decreasing in source order.",
                    nameof(recognition));
            }

            previousLineIndex = token.LineIndex;
        }

        ValidateLineLayouts(recognizedTokens, nameof(recognition));

        _tokens = [.. recognizedTokens.Select(t => new TranscriptToken(
            t.Text,
            t.Confidence,
            t.ConfidenceAvailable && t.Confidence >= confidenceThreshold
                ? TranscriptTokenState.Confident
                : TranscriptTokenState.Uncertain)
        {
            LineIndex = t.LineIndex,
            ConfidenceAvailable = t.ConfidenceAvailable,
            LayoutMetadataAvailable = t.LayoutMetadataAvailable,
            LeadingText = t.LeadingText,
            TrailingText = t.TrailingText,
        })];
        RecognizerLanguage = recognition.RecognizerLanguage;
    }

    public IReadOnlyList<TranscriptToken> Tokens => _tokens;

    public string RecognizerLanguage { get; }

    public int UnresolvedCount => _tokens.Count(t => t.State == TranscriptTokenState.Uncertain);

    public bool IsComplete => UnresolvedCount == 0;

    /// <summary>
    /// Finds the next unresolved token strictly after <paramref name="afterIndex"/>.
    /// The scan is bounded to the remaining tokens and never wraps; pass -1 (the
    /// default) to find the first unresolved token.
    /// </summary>
    public int? NextUnresolvedIndex(int afterIndex = -1)
    {
        if (afterIndex < -1 || afterIndex >= _tokens.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(afterIndex));
        }

        for (var index = afterIndex + 1; index < _tokens.Count; index++)
        {
            if (_tokens[index].State == TranscriptTokenState.Uncertain)
            {
                return index;
            }
        }

        return null;
    }

    /// <summary>Accept the recognized text as correct, or retype it — either way, a named human decided.</summary>
    public void Resolve(int index, string verifiedText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(verifiedText);
        var token = RequireUncertain(index);
        _tokens[index] = token with { State = TranscriptTokenState.Resolved, ResolvedText = verifiedText };
    }

    public void MarkIllegible(int index)
    {
        var token = RequireUncertain(index);
        _tokens[index] = token with { State = TranscriptTokenState.Illegible };
    }

    /// <summary>
    /// The verified transcript — available only when every uncertainty is resolved.
    /// Illegible tokens surface as an explicit marker, never as a plausible guess.
    /// </summary>
    public IReadOnlyList<string> VerifiedWords()
    {
        if (!IsComplete)
        {
            throw new InvalidOperationException(
                $"{UnresolvedCount} uncertain token(s) remain; the transcript is not verified. Fail closed - resolve or mark illegible.");
        }

        return [.. _tokens.Select(t => t.State switch
        {
            TranscriptTokenState.Resolved => t.ResolvedText!,
            TranscriptTokenState.Illegible => IllegibleMarker,
            _ => t.RecognizedText,
        })];
    }

    /// <summary>
    /// The verified transcript projected as source lines. Line boundaries are
    /// retained while edits and explicit illegible markers replace individual
    /// words. As with <see cref="VerifiedWords"/>, this fails closed until every
    /// uncertainty has been resolved.
    /// </summary>
    public IReadOnlyList<string> VerifiedLines()
    {
        _ = VerifiedWords();
        return ProjectLines(token => token.State switch
        {
            TranscriptTokenState.Resolved => token.ResolvedText!,
            TranscriptTokenState.Illegible => IllegibleMarker,
            _ => token.RecognizedText,
        });
    }

    /// <summary>
    /// Projects candidate or partially verified token text without losing exact
    /// recognizer separators. The WinForms comparison surface uses this internal
    /// seam so its before/after views obey the same line contract as completion.
    /// </summary>
    internal IReadOnlyList<string> ProjectLines(Func<TranscriptToken, string> projectToken)
    {
        ArgumentNullException.ThrowIfNull(projectToken);
        var lines = new List<string>();
        var currentLineIndex = _tokens[0].LineIndex;
        var lineTokens = new List<TranscriptToken>();

        foreach (var token in _tokens)
        {
            if (token.LineIndex != currentLineIndex)
            {
                lines.Add(ProjectLine(lineTokens, projectToken));
                lineTokens.Clear();
                currentLineIndex = token.LineIndex;
            }

            lineTokens.Add(token);
        }

        lines.Add(ProjectLine(lineTokens, projectToken));
        return lines;
    }

    public string VerifiedText() => string.Join(' ', VerifiedWords());

    private TranscriptToken RequireUncertain(int index)
    {
        var token = _tokens[index];
        return token.State == TranscriptTokenState.Uncertain
            ? token
            : throw new InvalidOperationException($"Token {index} is {token.State}; only uncertain tokens need the teacher.");
    }

    private static string ProjectLine(
        List<TranscriptToken> tokens,
        Func<TranscriptToken, string> projectToken)
    {
        if (!tokens[0].LayoutMetadataAvailable)
        {
            return string.Join(' ', tokens.Select(projectToken));
        }

        var line = new System.Text.StringBuilder();
        foreach (var token in tokens)
        {
            line.Append(token.LeadingText);
            line.Append(projectToken(token));
            line.Append(token.TrailingText);
        }

        return line.ToString();
    }

    private static void ValidateLineLayouts(
        IReadOnlyList<OcrToken> tokens,
        string parameterName)
    {
        var lineStart = 0;
        while (lineStart < tokens.Count)
        {
            var lineIndex = tokens[lineStart].LineIndex;
            var lineEnd = lineStart + 1;
            while (lineEnd < tokens.Count && tokens[lineEnd].LineIndex == lineIndex)
            {
                lineEnd++;
            }

            var layoutAvailable = tokens[lineStart].LayoutMetadataAvailable;
            for (var index = lineStart; index < lineEnd; index++)
            {
                var token = tokens[index];
                if (token.LeadingText is null || token.TrailingText is null)
                {
                    throw new ArgumentException(
                        "OCR layout text must be non-null.",
                        parameterName);
                }

                if (token.LayoutMetadataAvailable != layoutAvailable)
                {
                    throw new ArgumentException(
                        "Every OCR token on one source line must agree whether exact layout metadata is available.",
                        parameterName);
                }

                if (!layoutAvailable
                    && (token.LeadingText.Length != 0 || token.TrailingText.Length != 0))
                {
                    throw new ArgumentException(
                        "OCR layout text cannot be supplied when exact layout metadata is unavailable.",
                        parameterName);
                }

                if (layoutAvailable && index < lineEnd - 1 && token.TrailingText.Length != 0)
                {
                    throw new ArgumentException(
                        "Only the final OCR token on a source line may carry trailing layout text.",
                        parameterName);
                }
            }

            lineStart = lineEnd;
        }
    }
}
