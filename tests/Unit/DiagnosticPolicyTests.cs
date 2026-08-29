using Foundry.Application;
using Foundry.Domain;

namespace Foundry.Tests.Unit;

public class DiagnosticPolicyTests
{
    private static DiagnosticEvent SoundEvent() => new(
        EventCode: "job.state-changed",
        OutcomeCategory: "success",
        ModuleId: "deterministic-press",
        RecipeId: "blankforms.graph-paper",
        RecipeVersion: "1.0.0",
        ProviderId: "synthetic",
        FromState: JobState.Approved,
        ToState: JobState.Rendered,
        Duration: TimeSpan.FromMilliseconds(41),
        MediaClass: "pdf",
        InputTokens: null,
        OutputTokens: null);

    [Fact]
    public void A_sound_event_is_content_free()
    {
        Assert.True(DiagnosticPolicy.IsContentFree(SoundEvent()));
    }

    [Theory]
    [InlineData("Student wrote 7 instead of 9")]
    [InlineData("UPPERCASE.CODE")]
    [InlineData("code with spaces")]
    [InlineData("")]
    public void Prose_shaped_event_codes_are_rejected(string eventCode)
    {
        var diagnosticEvent = SoundEvent() with { EventCode = eventCode };

        Assert.False(DiagnosticPolicy.IsContentFree(diagnosticEvent));
    }

    [Fact]
    public void Identifiers_longer_than_sixty_four_characters_are_rejected()
    {
        var diagnosticEvent = SoundEvent() with { RecipeId = new string('a', 65) };

        Assert.False(DiagnosticPolicy.IsContentFree(diagnosticEvent));
    }

    [Fact]
    public void Outcome_categories_outside_the_allowlist_are_rejected()
    {
        var diagnosticEvent = SoundEvent() with { OutcomeCategory = "surprising" };

        Assert.False(DiagnosticPolicy.IsContentFree(diagnosticEvent));
    }

    [Fact]
    public void Media_classes_outside_the_broad_allowlist_are_rejected()
    {
        var diagnosticEvent = SoundEvent() with { MediaClass = "student-essay" };

        Assert.False(DiagnosticPolicy.IsContentFree(diagnosticEvent));
    }

    [Fact]
    public void The_sink_records_sound_events_and_throws_on_content()
    {
        var sink = new InMemoryDiagnosticsSink();

        sink.Record(SoundEvent());
        Assert.Single(sink.Events);

        var leaky = SoundEvent() with { EventCode = "The student said she was scared" };
        Assert.Throws<InvalidOperationException>(() => sink.Record(leaky));
        Assert.Single(sink.Events);
    }
}
