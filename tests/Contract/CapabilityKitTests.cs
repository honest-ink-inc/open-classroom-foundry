using Foundry.Domain;
using Foundry.Inference;
using Foundry.Inference.Synthetic;
using System.Text.Json;

namespace Foundry.Tests.Contract;

/// <summary>The one conformance bar for every provider — exercised here against the synthetic stand-in.</summary>
public class CapabilityKitTests
{
    private static readonly DateTimeOffset SomeInstant = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    private static PreviewedRequest Confirm(InferenceRequest request)
        => EgressGate.Confirm(
            EgressGate.Preview(request, SyntheticInferenceProvider.DefaultCapabilities),
            "kit@example.org", SomeInstant);

    private static InferenceRequest ExpectedRefusalProbe() => new(
        "capability-refusal-probe",
        "1.0.0",
        ProviderCapabilityTestKit.CapabilityProbeOutputSchemaId,
        [new TextPart("synthetic expected-refusal probe")],
        DataLane.Green);

    [Fact]
    public void The_published_probe_schema_matches_the_exact_runtime_object_contract()
    {
        using var schema = JsonDocument.Parse(ProviderCapabilityTestKit.CapabilityProbeOutputSchemaJson);
        var root = schema.RootElement;

        Assert.Equal("object", root.GetProperty("type").GetString());
        Assert.False(root.GetProperty("additionalProperties").GetBoolean());

        var property = Assert.Single(root.GetProperty("properties").EnumerateObject());
        Assert.Equal("capabilityProbe", property.Name);
        Assert.Equal("string", property.Value.GetProperty("type").GetString());
        Assert.Equal(
            ["ok"],
            property.Value.GetProperty("enum").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(
            ["capabilityProbe"],
            root.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
    }

    [Fact]
    public async Task A_conforming_provider_passes_clean()
    {
        var provider = new RecordingProvider(
            responses:
            [
                InferenceResult.Success(ProviderCapabilityTestKit.CapabilityProbeExpectedJson),
                InferenceResult.Success(ProviderCapabilityTestKit.CapabilityProbeExpectedJson),
                InferenceResult.Failure(InferenceOutcome.Refusal),
            ]);

        var findings = await ProviderCapabilityTestKit.RunAsync(
            provider, Confirm, ExpectedRefusalProbe(), CancellationToken.None);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task Omitting_the_deployment_specific_refusal_probe_cannot_report_full_conformance()
    {
        var findings = await ProviderCapabilityTestKit.RunAsync(
            new RecordingProvider(), Confirm, CancellationToken.None);

        Assert.Contains(
            findings,
            finding => finding.Contains("Refusal behavior was not tested", StringComparison.Ordinal));
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

    [Fact]
    public async Task The_common_kit_exercises_a_confirmed_image_request()
    {
        var provider = new RecordingProvider(
            responses:
            [
                InferenceResult.Success(ProviderCapabilityTestKit.CapabilityProbeExpectedJson),
                InferenceResult.Success(ProviderCapabilityTestKit.CapabilityProbeExpectedJson),
                InferenceResult.Failure(InferenceOutcome.Refusal),
            ]);

        var findings = await ProviderCapabilityTestKit.RunAsync(
            provider,
            Confirm,
            ExpectedRefusalProbe(),
            CancellationToken.None);

        Assert.Empty(findings);
        Assert.Equal(3, provider.Requests.Count);
        Assert.Contains(provider.Requests[0].Parts, part => part is TextPart);
        var image = Assert.Single(provider.Requests[1].Parts.OfType<ImagePart>());
        Assert.Equal("image/png", image.MimeType);
        Assert.NotEmpty(image.Bytes.ToArray());
    }

    [Fact]
    public async Task A_provider_without_image_input_is_named_unavailable_for_image_paths()
    {
        var provider = new RecordingProvider(
            SyntheticInferenceProvider.DefaultCapabilities with { SupportsImageInput = false });

        var findings = await ProviderCapabilityTestKit.RunAsync(
            provider,
            Confirm,
            CancellationToken.None);

        Assert.Contains(findings, finding => finding.Contains("Image input is unsupported", StringComparison.Ordinal));
        Assert.Single(provider.Requests);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    public async Task A_structured_success_must_contain_the_exact_probe_object(string structuredOutput)
    {
        var provider = new SyntheticInferenceProvider(
            capabilities: null,
            SyntheticStep.Structured(structuredOutput));

        var findings = await ProviderCapabilityTestKit.RunAsync(
            provider,
            Confirm,
            CancellationToken.None);

        Assert.Contains(findings, finding => finding.Contains("exact bounded capability-probe object", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"capabilityProbe\":\"wrong\"}")]
    [InlineData("{\"capabilityProbe\":\"ok\",\"extra\":true}")]
    [InlineData("{\"capabilityProbe\":\"ok\",\"capabilityProbe\":\"ok\"}")]
    public async Task Wrong_extra_and_duplicate_probe_properties_fail_conformance(string structuredOutput)
    {
        var provider = new RecordingProvider(
            responses:
            [
                InferenceResult.Success(structuredOutput),
                InferenceResult.Success(ProviderCapabilityTestKit.CapabilityProbeExpectedJson),
                InferenceResult.Failure(InferenceOutcome.Refusal),
            ]);

        var findings = await ProviderCapabilityTestKit.RunAsync(
            provider,
            Confirm,
            ExpectedRefusalProbe(),
            CancellationToken.None);

        Assert.Contains(
            findings,
            finding => finding.Contains("exact bounded capability-probe object", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_image_probe_must_return_the_same_exact_probe_object()
    {
        var provider = new RecordingProvider(
            responses:
            [
                InferenceResult.Success(ProviderCapabilityTestKit.CapabilityProbeExpectedJson),
                InferenceResult.Success("{}"),
                InferenceResult.Failure(InferenceOutcome.Refusal),
            ]);

        var findings = await ProviderCapabilityTestKit.RunAsync(
            provider,
            Confirm,
            ExpectedRefusalProbe(),
            CancellationToken.None);

        Assert.Contains(
            findings,
            finding => finding.Contains("image structured probe", StringComparison.OrdinalIgnoreCase)
                && finding.Contains("exact bounded capability-probe object", StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_oversized_probe_object_fails_before_json_binding()
    {
        var provider = new RecordingProvider(
            responses:
            [
                InferenceResult.Success(
                    $"{{\"capabilityProbe\":\"ok\",\"padding\":\"{new string('x', 256)}\"}}"),
                InferenceResult.Success(ProviderCapabilityTestKit.CapabilityProbeExpectedJson),
                InferenceResult.Failure(InferenceOutcome.Refusal),
            ]);

        var findings = await ProviderCapabilityTestKit.RunAsync(
            provider,
            Confirm,
            ExpectedRefusalProbe(),
            CancellationToken.None);

        Assert.Contains(
            findings,
            finding => finding.Contains("exact bounded capability-probe object", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_caller_can_explicitly_probe_expected_refusal_without_a_shared_model_assumption()
    {
        var provider = new RecordingProvider(
            responses:
            [
                InferenceResult.Success(ProviderCapabilityTestKit.CapabilityProbeExpectedJson),
                InferenceResult.Success(ProviderCapabilityTestKit.CapabilityProbeExpectedJson),
                InferenceResult.Failure(InferenceOutcome.Refusal),
            ]);

        var findings = await ProviderCapabilityTestKit.RunAsync(
            provider,
            Confirm,
            ExpectedRefusalProbe(),
            CancellationToken.None);

        Assert.Empty(findings);
        Assert.Equal(3, provider.Requests.Count);
        Assert.Equal("capability-refusal-probe", provider.Requests[2].RecipeId);
    }

    [Fact]
    public async Task An_expected_refusal_probe_that_succeeds_is_flagged()
    {
        var provider = new RecordingProvider();

        var findings = await ProviderCapabilityTestKit.RunAsync(
            provider,
            Confirm,
            ExpectedRefusalProbe(),
            CancellationToken.None);

        Assert.Contains(
            findings,
            finding => finding.Contains("instead of Refusal", StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_expected_refusal_probe_cannot_attach_structured_output()
    {
        var provider = new RecordingProvider(
            responses:
            [
                InferenceResult.Success(ProviderCapabilityTestKit.CapabilityProbeExpectedJson),
                InferenceResult.Success(ProviderCapabilityTestKit.CapabilityProbeExpectedJson),
                new InferenceResult(InferenceOutcome.Refusal, "{}"),
            ]);

        var findings = await ProviderCapabilityTestKit.RunAsync(
            provider,
            Confirm,
            ExpectedRefusalProbe(),
            CancellationToken.None);

        Assert.Contains(
            findings,
            finding => finding.Contains("attached structured output", StringComparison.Ordinal));
    }

    private sealed class RecordingProvider : IInferenceProvider
    {
        private readonly ProviderCapabilities _capabilities;
        private readonly Queue<InferenceResult> _responses;

        public RecordingProvider(
            ProviderCapabilities? capabilities = null,
            params InferenceResult[] responses)
        {
            _capabilities = capabilities ?? SyntheticInferenceProvider.DefaultCapabilities;
            _responses = new Queue<InferenceResult>(responses);
        }

        public List<InferenceRequest> Requests { get; } = [];

        public Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_capabilities);
        }

        public Task<InferenceResult> CompleteAsync(
            PreviewedRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request.Request);
            return Task.FromResult(_responses.Count > 0
                ? _responses.Dequeue()
                : InferenceResult.Success(ProviderCapabilityTestKit.CapabilityProbeExpectedJson));
        }
    }
}
