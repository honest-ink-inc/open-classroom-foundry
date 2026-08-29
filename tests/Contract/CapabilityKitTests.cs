using Foundry.Inference;
using Foundry.Inference.Synthetic;
using Xunit;

namespace Foundry.Tests.Contract;

/// <summary>The one conformance bar for every provider — exercised here against the synthetic stand-in.</summary>
public class CapabilityKitTests
{
    private static readonly DateTimeOffset SomeInstant = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    private static PreviewedRequest Confirm(InferenceRequest request)
        => EgressGate.Confirm(
            EgressGate.Preview(request, SyntheticInferenceProvider.DefaultCapabilities),
            "kit@example.org", SomeInstant);

    [Fact]
    public async Task A_conforming_provider_passes_clean()
    {
        var findings = await ProviderCapabilityTestKit.RunAsync(
            new SyntheticInferenceProvider(), Confirm, CancellationToken.None);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task A_provider_without_structured_output_is_named_unusable()
    {
        var provider = new SyntheticInferenceProvider(
            SyntheticInferenceProvider.DefaultCapabilities with { SupportsStructuredOutput = false });

        var findings = await ProviderCapabilityTestKit.RunAsync(provider, Confirm, CancellationToken.None);

        Assert.Contains(findings, f => f.Contains("Structured output is unsupported", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_provider_that_refuses_the_probe_is_flagged()
    {
        var provider = new SyntheticInferenceProvider(null, SyntheticStep.Outcome(InferenceOutcome.Refusal));

        var findings = await ProviderCapabilityTestKit.RunAsync(provider, Confirm, CancellationToken.None);

        Assert.Contains(findings, f => f.Contains("Refusal", StringComparison.Ordinal));
    }
}
