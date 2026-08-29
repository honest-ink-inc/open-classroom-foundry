using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Inference;
using Foundry.Inference.Synthetic;
using Xunit;

namespace Foundry.Tests.Contract;

/// <summary>R2-1: district policy is law at the provider boundary — refused before any egress.</summary>
public class PolicyGateTests
{
    private static readonly DateTimeOffset SomeInstant = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    private sealed class FixedPolicy(DistrictPolicy policy) : IDistrictPolicyProvider
    {
        public DistrictPolicy Current { get; } = policy;
    }

    private static PreviewedRequest Previewed(DataLane lane)
    {
        var request = new InferenceRequest(
            "all-aboard.task-strip", "0.1.0", "schema.all-aboard.v1",
            [new TextPart("Task: watering the plants.")], lane);
        return EgressGate.Confirm(
            EgressGate.Preview(request, SyntheticInferenceProvider.DefaultCapabilities),
            "teacher@example.org", SomeInstant);
    }

    private static DistrictPolicy Enabled(DataLane maximumLane) => new(
        ["https://district.example/openai"], "synthetic", "synthetic-1", maximumLane, CloudInferenceEnabled: true);

    [Fact]
    public async Task Disabled_cloud_inference_refuses_before_any_egress()
    {
        // The inner script would answer happily; the gate must never let it.
        var inner = new SyntheticInferenceProvider(null, SyntheticStep.Structured("""{"steps":3}"""));
        var gate = new PolicyGatedInferenceProvider(inner, new FixedPolicy(DistrictPolicy.Offline));

        var result = await gate.CompleteAsync(Previewed(DataLane.Green), CancellationToken.None);

        Assert.Equal(InferenceOutcome.PolicyRefused, result.Outcome);

        // The scripted answer is still queued: the inner provider was never called.
        var direct = await inner.CompleteAsync(Previewed(DataLane.Green), CancellationToken.None);
        Assert.True(direct.IsSuccess);
    }

    [Fact]
    public async Task A_payload_above_the_district_lane_ceiling_is_refused()
    {
        var gate = new PolicyGatedInferenceProvider(
            new SyntheticInferenceProvider(), new FixedPolicy(Enabled(DataLane.Green)));

        var result = await gate.CompleteAsync(Previewed(DataLane.Amber), CancellationToken.None);

        Assert.Equal(InferenceOutcome.PolicyRefused, result.Outcome);
    }

    [Fact]
    public async Task Within_policy_the_gate_is_transparent()
    {
        var gate = new PolicyGatedInferenceProvider(
            new SyntheticInferenceProvider(), new FixedPolicy(Enabled(DataLane.Amber)));

        Assert.True((await gate.CompleteAsync(Previewed(DataLane.Green), CancellationToken.None)).IsSuccess);
        Assert.True((await gate.CompleteAsync(Previewed(DataLane.Amber), CancellationToken.None)).IsSuccess);
        Assert.Equal("synthetic", (await gate.GetCapabilitiesAsync(CancellationToken.None)).ProviderId);
    }

    [Fact]
    public async Task Capabilities_are_loud_when_policy_is_off()
    {
        var gate = new PolicyGatedInferenceProvider(
            new SyntheticInferenceProvider(), new FixedPolicy(DistrictPolicy.Offline));

        await Assert.ThrowsAsync<InvalidOperationException>(() => gate.GetCapabilitiesAsync(CancellationToken.None));
    }
}
