using Foundry.Application;
using Foundry.Contracts;

namespace Foundry.Tests.Unit;

public class TranscriptSessionTests
{
    private static TranscriptSession BoardSession() => new(new OcrResult(
    [
        new OcrToken("Finish", 0.98),
        new OcrToken("chapter", 0.97),
        new OcrToken("9", 0.41),
        new OcrToken("by", 0.96),
        new OcrToken("Fri1ay", 0.52),
    ]));

    [Fact]
    public void Tokens_below_the_threshold_are_uncertain_and_block_completion()
    {
        var session = BoardSession();

        Assert.Equal(2, session.UnresolvedCount);
        Assert.False(session.IsComplete);
        Assert.Throws<InvalidOperationException>(session.VerifiedText);
    }

    [Fact]
    public void Resolving_every_uncertainty_yields_the_verified_transcript()
    {
        var session = BoardSession();

        session.Resolve(2, "9");
        session.Resolve(4, "Friday");

        Assert.True(session.IsComplete);
        Assert.Equal("Finish chapter 9 by Friday", session.VerifiedText());
    }

    [Fact]
    public void Illegible_is_an_explicit_marker_never_a_guess()
    {
        var session = BoardSession();

        session.Resolve(2, "9");
        session.MarkIllegible(4);

        Assert.Equal($"Finish chapter 9 by {TranscriptSession.IllegibleMarker}", session.VerifiedText());
    }

    [Fact]
    public void Confident_tokens_need_no_teacher_and_cannot_be_re_resolved()
    {
        var session = BoardSession();

        Assert.Throws<InvalidOperationException>(() => session.Resolve(0, "Something else"));
    }

    [Fact]
    public void A_blank_resolution_is_refused()
    {
        var session = BoardSession();

        Assert.Throws<ArgumentException>(() => session.Resolve(2, "  "));
    }

    [Fact]
    public void The_threshold_is_the_teachers_dial()
    {
        var strict = new TranscriptSession(new OcrResult([new OcrToken("word", 0.90)]), confidenceThreshold: 0.95);
        var lenient = new TranscriptSession(new OcrResult([new OcrToken("word", 0.90)]), confidenceThreshold: 0.80);

        Assert.Equal(1, strict.UnresolvedCount);
        Assert.Equal(0, lenient.UnresolvedCount);
    }

    [Fact]
    public void Missing_platform_confidence_forces_teacher_verification_even_with_a_high_placeholder()
    {
        var session = new TranscriptSession(new OcrResult(
        [
            new OcrToken("word", 1.0) { ConfidenceAvailable = false },
        ]));

        var token = Assert.Single(session.Tokens);
        Assert.False(token.ConfidenceAvailable);
        Assert.Equal(TranscriptTokenState.Uncertain, token.State);
    }

    [Fact]
    public void Next_unresolved_navigation_is_forward_only_bounded_and_does_not_wrap()
    {
        var session = BoardSession();

        Assert.Equal(2, session.NextUnresolvedIndex());
        Assert.Equal(4, session.NextUnresolvedIndex(2));
        Assert.Null(session.NextUnresolvedIndex(4));

        Assert.Throws<ArgumentOutOfRangeException>(() => session.NextUnresolvedIndex(-2));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.NextUnresolvedIndex(session.Tokens.Count));
    }

    [Fact]
    public void Verified_lines_preserve_recognizer_boundaries_and_edits()
    {
        var session = new TranscriptSession(new OcrResult(
        [
            new OcrToken("Finish", 0.98),
            new OcrToken("nine", 0.42),
            new OcrToken("Bring", 0.97) { LineIndex = 1 },
            new OcrToken("notes", 0.41) { LineIndex = 1 },
        ]));

        Assert.Throws<InvalidOperationException>(session.VerifiedLines);

        session.Resolve(1, "9");
        session.MarkIllegible(3);

        Assert.Equal(["Finish 9", $"Bring {TranscriptSession.IllegibleMarker}"], session.VerifiedLines());
        Assert.Equal(
            $"Finish 9 Bring {TranscriptSession.IllegibleMarker}",
            session.VerifiedText());
    }

    [Fact]
    public void Exact_layout_metadata_preserves_formula_units_punctuation_and_arrows()
    {
        var session = new TranscriptSession(new OcrResult(
        [
            LayoutToken("x", lineIndex: 0),
            LayoutToken("2", lineIndex: 0, leadingText: "="),
            LayoutToken("25", lineIndex: 1),
            LayoutToken("mL", lineIndex: 1, leadingText: " "),
            LayoutToken("Read", lineIndex: 2),
            LayoutToken("write", lineIndex: 2, leadingText: " → ", trailingText: "!"),
        ]));

        Assert.Equal(["x=2", "25 mL", "Read → write!"], session.VerifiedLines());
        Assert.All(session.Tokens, token => Assert.True(token.LayoutMetadataAvailable));
    }

    [Fact]
    public void Retyping_words_changes_only_words_and_keeps_exact_layout()
    {
        var session = new TranscriptSession(new OcrResult(
        [
            LayoutToken("x", lineIndex: 0, confidence: 0),
            LayoutToken("2", lineIndex: 0, leadingText: "=", confidence: 0),
            LayoutToken("25", lineIndex: 1, confidence: 0),
            LayoutToken("mL", lineIndex: 1, leadingText: " ", trailingText: ".", confidence: 0),
        ]));

        session.Resolve(0, "y");
        session.Resolve(1, "3");
        session.Resolve(2, "30");
        session.Resolve(3, "mL");

        Assert.Equal(["y=3", "30 mL."], session.VerifiedLines());
    }

    [Fact]
    public void Legacy_tokens_without_layout_metadata_keep_space_join_compatibility()
    {
        var session = new TranscriptSession(new OcrResult(
        [
            new OcrToken("x=2", 1),
            new OcrToken("25", 1) { LineIndex = 1 },
            new OcrToken("mL", 1) { LineIndex = 1 },
        ]));

        Assert.Equal(["x=2", "25 mL"], session.VerifiedLines());
        Assert.All(session.Tokens, token => Assert.False(token.LayoutMetadataAvailable));
    }

    [Fact]
    public void Inconsistent_layout_metadata_on_one_line_is_refused()
    {
        Assert.Throws<ArgumentException>(() => new TranscriptSession(new OcrResult(
        [
            LayoutToken("x", lineIndex: 0),
            new OcrToken("2", 1) { LineIndex = 0 },
        ])));

        Assert.Throws<ArgumentException>(() => new TranscriptSession(new OcrResult(
        [
            new OcrToken("x", 1) { LeadingText = " " },
        ])));

        Assert.Throws<ArgumentException>(() => new TranscriptSession(new OcrResult(
        [
            LayoutToken("x", lineIndex: 0, trailingText: "="),
            LayoutToken("2", lineIndex: 0),
        ])));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 1)]
    public void Invalid_or_reversed_line_indexes_are_refused(int secondLineIndex, int firstLineIndex)
    {
        var recognition = new OcrResult(
        [
            new OcrToken("first", 0.90) { LineIndex = firstLineIndex },
            new OcrToken("second", 0.90) { LineIndex = secondLineIndex },
        ]);

        Assert.Throws<ArgumentException>(() => new TranscriptSession(recognition));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Invalid_thresholds_are_refused(double threshold)
    {
        var recognition = new OcrResult([new OcrToken("word", 0.90)]);

        Assert.Throws<ArgumentOutOfRangeException>(() => new TranscriptSession(recognition, threshold));
    }

    [Theory]
    [InlineData(" ", 0.90)]
    [InlineData("word", double.NaN)]
    [InlineData("word", -0.01)]
    [InlineData("word", 1.01)]
    public void Invalid_recognizer_tokens_are_refused(string text, double confidence)
    {
        var recognition = new OcrResult([new OcrToken(text, confidence)]);

        Assert.Throws<ArgumentException>(() => new TranscriptSession(recognition));
    }

    [Fact]
    public void Legacy_record_constructors_and_deconstruction_remain_source_compatible()
    {
        var recognized = new OcrToken("word", 0.90) { LineIndex = 2 };
        var transcript = new TranscriptToken("word", 0.90, TranscriptTokenState.Confident);

        var (text, confidence) = recognized;
        var (transcriptText, transcriptConfidence, state, resolvedText) = transcript;

        Assert.Equal("word", text);
        Assert.Equal(0.90, confidence);
        Assert.Equal("word", transcriptText);
        Assert.Equal(0.90, transcriptConfidence);
        Assert.Equal(TranscriptTokenState.Confident, state);
        Assert.Null(resolvedText);
    }

    private static OcrToken LayoutToken(
        string text,
        int lineIndex,
        string leadingText = "",
        string trailingText = "",
        double confidence = 1)
        => new(text, confidence)
        {
            LineIndex = lineIndex,
            LayoutMetadataAvailable = true,
            LeadingText = leadingText,
            TrailingText = trailingText,
        };
}
