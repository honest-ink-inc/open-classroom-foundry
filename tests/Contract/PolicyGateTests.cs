using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Inference;
using Foundry.Inference.Synthetic;

namespace Foundry.Tests.Contract;

/// <summary>R2-1: district policy is law at the provider boundary — refused before any egress.</summary>
public class PolicyGateTests
{
    private static readonly DateTimeOffset SomeInstant = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    private sealed class FixedPolicy(DistrictPolicy policy) : IDistrictPolicyProvider
    {
        public DistrictPolicy Current { get; } = policy;
    }

    private sealed class RecordingProvider(ProviderCapabilities capabilities) : IInferenceProvider
    {
        public int CapabilityCalls { get; private set; }

        public int CompleteCalls { get; private set; }

        public Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CapabilityCalls++;
            return Task.FromResult(capabilities);
        }

        public Task<InferenceResult> CompleteAsync(
            PreviewedRequest request,
            CancellationToken cancellationToken)
        {
            CompleteCalls++;
            return Task.FromResult(InferenceResult.Success("{}"));
        }
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
        ["https://synthetic.invalid/openai"], "synthetic", "synthetic-1", maximumLane, CloudInferenceEnabled: true);

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

    [Theory]
    [InlineData(-1)]
    [InlineData((int)DataLane.Restricted)]
    [InlineData(int.MaxValue)]
    public async Task An_undefined_or_Restricted_district_lane_ceiling_is_refused_before_the_inner_provider(
        int refusedLane)
    {
        var inner = new RecordingProvider(SyntheticInferenceProvider.DefaultCapabilities);
        var gate = new PolicyGatedInferenceProvider(
            inner,
            new FixedPolicy(Enabled((DataLane)refusedLane)));
        var request = Previewed(DataLane.Green);

        var result = await gate.CompleteAsync(request, CancellationToken.None);

        Assert.Equal(InferenceOutcome.PolicyRefused, result.Outcome);
        Assert.Equal(0, inner.CapabilityCalls);
        Assert.Equal(0, inner.CompleteCalls);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => gate.GetCapabilitiesAsync(CancellationToken.None));
        Assert.Equal(0, inner.CapabilityCalls);
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
    public async Task A_direct_call_cannot_bypass_the_confirmed_provider_model_through_a_custom_inner()
    {
        var mismatched = SyntheticInferenceProvider.DefaultCapabilities with
        {
            PinnedModelVersion = "synthetic-2.0",
        };
        var inner = new RecordingProvider(mismatched);
        var gate = new PolicyGatedInferenceProvider(
            inner,
            new FixedPolicy(Enabled(DataLane.Green)));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => gate.CompleteAsync(Previewed(DataLane.Green), CancellationToken.None));

        Assert.Equal(1, inner.CapabilityCalls);
        Assert.Equal(0, inner.CompleteCalls);
    }

    [Fact]
    public async Task A_provider_outside_the_district_identity_grant_is_refused_before_dispatch()
    {
        var azure = new ProviderCapabilities(
            "azure-openai",
            "district-gpt",
            PinnedModelVersion: null,
            SupportsImageInput: true,
            SupportsStructuredOutput: true,
            EndpointOrigin: "https://district.example");
        var inner = new RecordingProvider(azure);
        var gate = new PolicyGatedInferenceProvider(
            inner,
            new FixedPolicy(Enabled(DataLane.Green)));

        var result = await gate.CompleteAsync(Previewed(DataLane.Green), CancellationToken.None);

        Assert.Equal(InferenceOutcome.PolicyRefused, result.Outcome);
        Assert.Equal(1, inner.CapabilityCalls);
        Assert.Equal(0, inner.CompleteCalls);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => gate.GetCapabilitiesAsync(CancellationToken.None));
        Assert.Equal(2, inner.CapabilityCalls);
    }

    [Fact]
    public async Task A_provider_origin_outside_the_actual_district_allowlist_is_refused_before_dispatch()
    {
        var inner = new RecordingProvider(SyntheticInferenceProvider.DefaultCapabilities);
        var policy = Enabled(DataLane.Green) with
        {
            AllowedEndpoints = ["https://different-district.example/openai"],
        };
        var gate = new PolicyGatedInferenceProvider(inner, new FixedPolicy(policy));

        var result = await gate.CompleteAsync(Previewed(DataLane.Green), CancellationToken.None);

        Assert.Equal(InferenceOutcome.PolicyRefused, result.Outcome);
        Assert.Equal(1, inner.CapabilityCalls);
        Assert.Equal(0, inner.CompleteCalls);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => gate.GetCapabilitiesAsync(CancellationToken.None));
        Assert.Equal(2, inner.CapabilityCalls);
    }

    [Theory]
    [InlineData(null, "synthetic-1")]
    [InlineData("synthetic", null)]
    [InlineData("", "synthetic-1")]
    [InlineData("synthetic", "")]
    public async Task An_incomplete_district_identity_grant_never_touches_the_inner_provider(
        string? providerId,
        string? deploymentId)
    {
        var inner = new RecordingProvider(SyntheticInferenceProvider.DefaultCapabilities);
        var incomplete = Enabled(DataLane.Green) with
        {
            ProviderId = providerId,
            DeploymentId = deploymentId,
        };
        var gate = new PolicyGatedInferenceProvider(inner, new FixedPolicy(incomplete));

        var result = await gate.CompleteAsync(Previewed(DataLane.Green), CancellationToken.None);

        Assert.Equal(InferenceOutcome.PolicyRefused, result.Outcome);
        Assert.Equal(0, inner.CapabilityCalls);
        Assert.Equal(0, inner.CompleteCalls);
    }

    [Fact]
    public async Task Capabilities_are_loud_when_policy_is_off()
    {
        var gate = new PolicyGatedInferenceProvider(
            new SyntheticInferenceProvider(), new FixedPolicy(DistrictPolicy.Offline));

        await Assert.ThrowsAsync<InvalidOperationException>(() => gate.GetCapabilitiesAsync(CancellationToken.None));
    }
}
