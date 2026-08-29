using Foundry.Contracts;

namespace Foundry.Application;

public enum TranscriptTokenState
{
    Confident,
    Uncertain,
    Resolved,
    Illegible,
}

public sealed record TranscriptToken(string RecognizedText, double Confidence, TranscriptTokenState State, string? ResolvedText = null);

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
        if (recognition.Tokens.Count == 0)
        {
            throw new ArgumentException("There is nothing to transcribe.", nameof(recognition));
        }

        _tokens = [.. recognition.Tokens.Select(t => new TranscriptToken(
            t.Text,
            t.Confidence,
            t.Confidence >= confidenceThreshold ? TranscriptTokenState.Confident : TranscriptTokenState.Uncertain))];
    }

    public IReadOnlyList<TranscriptToken> Tokens => _tokens;

    public int UnresolvedCount => _tokens.Count(t => t.State == TranscriptTokenState.Uncertain);

    public bool IsComplete => UnresolvedCount == 0;

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

    public string VerifiedText() => string.Join(' ', VerifiedWords());

    private TranscriptToken RequireUncertain(int index)
    {
        var token = _tokens[index];
        return token.State == TranscriptTokenState.Uncertain
            ? token
            : throw new InvalidOperationException($"Token {index} is {token.State}; only uncertain tokens need the teacher.");
    }
}
