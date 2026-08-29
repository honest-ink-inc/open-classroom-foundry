using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Inference;
using Foundry.Inference.Synthetic;
using Foundry.Modules.BuiltIn.AllAboard;
using Xunit;

namespace Foundry.Tests.Contract;

/// <summary>
/// The optional-suggestion loop of Release 0.1: intent → Gate A preview →
/// confirmed egress → synthetic provider → strict parse → builder → review
/// session → teacher approval. A suggestion is never an approved artifact.
/// </summary>
public class SuggestionFlowTests
{
    private static readonly DateTimeOffset SomeInstant = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    private sealed class EmptyCatalog : IAssetCatalog
    {
        public IReadOnlyList<AssetProvenance> All => [];

        public AssetProvenance? Find(AssetId id) => null;

        public bool TryGetContent(AssetId id, out ReadOnlyMemory<byte> content, out string mimeType)
        {
            content = default;
            mimeType = string.Empty;
            return false;
        }
    }

    private static InferenceRequest IntentRequest() => new(
        RecipeId: "all-aboard.task-strip",
        RecipeVersion: "0.1.0",
        OutputSchemaId: "schema.all-aboard.v1",
        Parts: [new TextPart("Task: watering the class plants. Propose a title and 3-8 one-action steps.")],
        PayloadLane: DataLane.Green);

    private static JobStateMachine MachineAtReview()
    {
        var machine = new JobStateMachine();
        foreach (var state in new[]
        {
            JobState.Imported, JobState.Normalized, JobState.DataLaneConfirmed,
            JobState.OutboundPayloadPreviewed, JobState.DraftGenerated,
            JobState.SchemaValidated, JobState.InvariantsValidated, JobState.AwaitingTeacherReview,
        })
        {
            machine.Transition(state);
        }

        return machine;
    }

    [Fact]
    public async Task A_structured_suggestion_travels_the_whole_grammar_and_still_needs_the_teacher()
    {
        var suggestionJson = """{"title":"Watering the class plants","steps":["Pick up the can.","Fill to the line.","Water each plant once."]}""";
        var runner = new SuggestionRunner(new SyntheticInferenceProvider(null, SyntheticStep.Structured(suggestionJson)));

        var preview = await runner.PrepareAsync(IntentRequest(), CancellationToken.None);
        Assert.Contains(preview.Parts.OfType<OutboundTextPreview>(), p => p.ExactText.Contains("watering", StringComparison.Ordinal));

        var confirmed = EgressGate.Confirm(preview, "teacher@example.org", SomeInstant);
        var result = await runner.RunAsync(confirmed, CancellationToken.None);
        Assert.True(result.IsSuccess);

        var (suggestion, issues) = TaskStripSuggestionParser.Parse(result.StructuredJson!);
        Assert.Empty(issues);
        Assert.NotNull(suggestion);

        var document = AllAboardBuilders.TaskStrip(
            suggestion.Title, TaskStripSuggestionParser.ToSteps(suggestion), new EmptyCatalog());
        var session = new ReviewSession(
            DraftArtifact.New(document, DataLane.Green), MachineAtReview(),
            new DefaultArtifactValidator(), new DomainApprovalGate());

        // The proposal is editable and only the teacher's act approves it.
        session.ReplaceNode(1, new Paragraph("Model output is a draft the teacher owns."));
        var approved = session.Approve("teacher@example.org", SomeInstant);

        Assert.Equal(session.Draft.Revision.Number, approved.Receipt.RevisionNumber);
        Assert.Equal("teacher@example.org", approved.Receipt.ApprovedBy);
    }

    [Theory]
    [InlineData(InferenceOutcome.Refusal)]
    [InlineData(InferenceOutcome.ContentFiltered)]
    [InlineData(InferenceOutcome.ProviderError)]
    public async Task A_failed_run_yields_no_draft_and_no_parse(InferenceOutcome outcome)
    {
        var runner = new SuggestionRunner(new SyntheticInferenceProvider(null, SyntheticStep.Outcome(outcome)));

        var preview = await runner.PrepareAsync(IntentRequest(), CancellationToken.None);
        var result = await runner.RunAsync(EgressGate.Confirm(preview, "teacher@example.org", SomeInstant), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.StructuredJson);
    }

    [Fact]
    public void Suggestions_arrive_symbol_less_because_symbols_are_teacher_choices()
    {
        var (suggestion, _) = TaskStripSuggestionParser.Parse(
            """{"title":"T","steps":["a","b","c"]}""");

        Assert.All(TaskStripSuggestionParser.ToSteps(suggestion!), step => Assert.Null(step.Symbol));
    }
}
