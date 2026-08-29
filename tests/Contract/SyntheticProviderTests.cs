using Foundry.Domain;
using Foundry.Inference;
using Foundry.Inference.Synthetic;

namespace Foundry.Tests.Contract;

public class SyntheticProviderTests
{
    private static InferenceRequest SomeRequest() => new(
        RecipeId: "blankforms.graph-paper",
        RecipeVersion: "1.0.0",
        OutputSchemaId: "schema.blankforms.v1",
        Parts: [new TextPart("grid: 5mm")],
        PayloadLane: DataLane.Green);

    private static PreviewedRequest Previewed()
        => EgressGate.Confirm(
            EgressGate.Preview(SomeRequest(), SyntheticInferenceProvider.DefaultCapabilities),
            "teacher@example.org",
            new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task An_unscripted_provider_answers_with_a_benign_structured_object()
    {
        var provider = new SyntheticInferenceProvider();

        var result = await provider.CompleteAsync(Previewed(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SyntheticInferenceProvider.EmptyStructuredOutput, result.StructuredJson);
    }

    [Fact]
    public async Task Scripted_steps_play_back_in_order_then_fall_back_to_success()
    {
        var provider = new SyntheticInferenceProvider(
            capabilities: null,
            SyntheticStep.Outcome(InferenceOutcome.RateLimited),
            SyntheticStep.Structured("""{"steps":3}"""),
            SyntheticStep.Outcome(InferenceOutcome.Refusal));

        Assert.Equal(InferenceOutcome.RateLimited, (await provider.CompleteAsync(Previewed(), CancellationToken.None)).Outcome);
        Assert.Equal("""{"steps":3}""", (await provider.CompleteAsync(Previewed(), CancellationToken.None)).StructuredJson);
        Assert.Equal(InferenceOutcome.Refusal, (await provider.CompleteAsync(Previewed(), CancellationToken.None)).Outcome);
        Assert.True((await provider.CompleteAsync(Previewed(), CancellationToken.None)).IsSuccess);
    }

    [Theory]
    [InlineData(InferenceOutcome.Refusal)]
    [InlineData(InferenceOutcome.ContentFiltered)]
    [InlineData(InferenceOutcome.MalformedOutput)]
    [InlineData(InferenceOutcome.SchemaMismatch)]
    [InlineData(InferenceOutcome.Truncated)]
    [InlineData(InferenceOutcome.Timeout)]
    [InlineData(InferenceOutcome.Unauthorized)]
    [InlineData(InferenceOutcome.RateLimited)]
    [InlineData(InferenceOutcome.ProviderError)]
    [InlineData(InferenceOutcome.UnsupportedCapability)]
    public async Task Every_failure_case_of_the_fake_provider_suite_is_scriptable(InferenceOutcome outcome)
    {
        var provider = new SyntheticInferenceProvider(capabilities: null, SyntheticStep.Outcome(outcome));

        var result = await provider.CompleteAsync(Previewed(), CancellationToken.None);

        Assert.Equal(outcome, result.Outcome);
        Assert.False(result.IsSuccess);
        Assert.Null(result.StructuredJson);
    }

    [Fact]
    public async Task Cancellation_interrupts_a_slow_response()
    {
        var provider = new SyntheticInferenceProvider(
            capabilities: null,
            SyntheticStep.DelayedOutcome(InferenceOutcome.Timeout, TimeSpan.FromSeconds(30)));

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.CompleteAsync(Previewed(), cancellation.Token));
    }

    [Fact]
    public async Task Capabilities_are_reported_and_overridable()
    {
        var noStructured = SyntheticInferenceProvider.DefaultCapabilities with { SupportsStructuredOutput = false };
        var provider = new SyntheticInferenceProvider(noStructured);

        var capabilities = await provider.GetCapabilitiesAsync(CancellationToken.None);

        Assert.False(capabilities.SupportsStructuredOutput);
        Assert.Equal("synthetic", capabilities.ProviderId);
    }

    [Fact]
    public async Task Identical_scripts_produce_identical_sequences()
    {
        static SyntheticInferenceProvider Build()
        {
            return new(
            capabilities: null,
            SyntheticStep.Outcome(InferenceOutcome.ProviderError),
            SyntheticStep.Structured("""{"ok":true}"""));
        }

        var first = Build();
        var second = Build();

        for (var call = 0; call < 3; call++)
        {
            var a = await first.CompleteAsync(Previewed(), CancellationToken.None);
            var b = await second.CompleteAsync(Previewed(), CancellationToken.None);
            Assert.Equal(a, b);
        }
    }

    [Fact]
    public void A_success_cannot_be_scripted_as_a_failure_nor_a_failure_left_empty()
    {
        Assert.Throws<ArgumentException>(() => InferenceResult.Failure(InferenceOutcome.StructuredOutput));
        Assert.Throws<ArgumentException>(() => InferenceResult.Success("   "));
    }
}
