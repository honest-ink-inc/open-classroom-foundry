using Foundry.Application;
using Foundry.Contracts;
using Xunit;

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
        Assert.Throws<InvalidOperationException>(() => session.VerifiedText());
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
}
