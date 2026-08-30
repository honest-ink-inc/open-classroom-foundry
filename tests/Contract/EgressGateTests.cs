using System.Reflection;
using System.Runtime.InteropServices;
using Foundry.Domain;
using Foundry.Inference;
using Foundry.Inference.Synthetic;

namespace Foundry.Tests.Contract;

/// <summary>Executable Gate A: the exact-outbound-preview contract of plan §5.</summary>
public class EgressGateTests
{
    private static readonly DateTimeOffset SomeInstant = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    private static InferenceRequest SomeRequest(string text = "grid: 5mm") => new(
        RecipeId: "blankforms.graph-paper",
        RecipeVersion: "1.0.0",
        OutputSchemaId: "schema.blankforms.v1",
        Parts: [new TextPart(text), new ImagePart(new byte[] { 1, 2, 3, 4 }, "image/png")],
        PayloadLane: DataLane.Green);

    [Fact]
    public void The_preview_shows_the_exact_outbound_parts_of_the_frozen_request_snapshot()
    {
        var request = SomeRequest();

        var preview = EgressGate.Preview(request, SyntheticInferenceProvider.DefaultCapabilities);

        Assert.Collection(
            preview.Parts,
            part => Assert.Equal("grid: 5mm", Assert.IsType<OutboundTextPreview>(part).ExactText),
            part =>
            {
                var image = Assert.IsType<OutboundImagePreview>(part);
                Assert.Equal(4, image.ByteCount);
                Assert.Equal("image/png", image.MimeType);
            });

        Assert.Equal("synthetic", preview.ProviderId);
        Assert.Equal("synthetic-1", preview.DeploymentId);
        Assert.Equal("synthetic-1.0", preview.PinnedModelVersion);
        Assert.Equal(SyntheticInferenceProvider.EndpointOrigin, preview.EndpointOrigin);
        Assert.Equal(DataLane.Green, preview.PayloadLane);

        Assert.NotSame(request, preview.Request);
        Assert.Equal(request.RecipeId, preview.Request.RecipeId);
        Assert.Equal(request.RecipeVersion, preview.Request.RecipeVersion);
        Assert.Equal(request.OutputSchemaId, preview.Request.OutputSchemaId);
        Assert.Equal(request.PayloadLane, preview.Request.PayloadLane);
    }

    [Fact]
    public void The_payload_hash_is_deterministic_and_content_sensitive()
    {
        var capabilities = SyntheticInferenceProvider.DefaultCapabilities;

        var first = EgressGate.Preview(SomeRequest(), capabilities).PayloadSha256;
        var second = EgressGate.Preview(SomeRequest(), capabilities).PayloadSha256;
        var different = EgressGate.Preview(SomeRequest("grid: 10mm"), capabilities).PayloadSha256;

        Assert.Equal(first, second);
        Assert.NotEqual(first, different);
    }

    [Fact]
    public void Confirmation_requires_a_named_teacher_and_binds_the_hash()
    {
        var preview = EgressGate.Preview(SomeRequest(), SyntheticInferenceProvider.DefaultCapabilities);

        Assert.Throws<ArgumentException>(() => EgressGate.Confirm(preview, "  ", SomeInstant));

        var confirmed = EgressGate.Confirm(preview, "teacher@example.org", SomeInstant);

        Assert.Equal(preview.PayloadSha256, confirmed.Receipt.PayloadSha256);
        Assert.Equal("teacher@example.org", confirmed.Receipt.ConfirmedBy);
        Assert.Equal(preview.ProviderId, confirmed.Receipt.ProviderId);
        Assert.Equal(preview.DeploymentId, confirmed.Receipt.DeploymentId);
        Assert.Equal(preview.PinnedModelVersion, confirmed.Receipt.PinnedModelVersion);
        Assert.Equal(preview.EndpointOrigin, confirmed.Receipt.EndpointOrigin);
        Assert.NotSame(preview.Request, confirmed.Request);
        Assert.Equal(preview.Request.RecipeId, confirmed.Request.RecipeId);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData((int)DataLane.Restricted)]
    [InlineData(int.MaxValue)]
    public void Preview_refuses_undefined_and_restricted_payload_lanes(int refusedLane)
    {
        var request = SomeRequest() with { PayloadLane = (DataLane)refusedLane };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => EgressGate.Preview(request, SyntheticInferenceProvider.DefaultCapabilities));

        Assert.Equal("request", exception.ParamName);
    }

    [Fact]
    public void Endpoint_origin_is_normalized_into_the_preview_and_receipt()
    {
        var capabilities = SyntheticInferenceProvider.DefaultCapabilities with
        {
            EndpointOrigin = "HTTPS://SYNTHETIC.INVALID:443/a/provider/path?ignored=true",
        };

        var preview = EgressGate.Preview(SomeRequest(), capabilities);
        var confirmed = EgressGate.Confirm(preview, "teacher@example.org", SomeInstant);

        Assert.Equal(SyntheticInferenceProvider.EndpointOrigin, preview.EndpointOrigin);
        Assert.Equal(SyntheticInferenceProvider.EndpointOrigin, confirmed.Receipt.EndpointOrigin);
    }

    [Fact]
    public void A_provider_endpoint_origin_change_requires_a_fresh_Gate_A_confirmation()
    {
        var original = SyntheticInferenceProvider.DefaultCapabilities;
        var moved = original with { EndpointOrigin = "https://replacement.synthetic.invalid" };
        var staleConfirmation = EgressGate.Confirm(
            EgressGate.Preview(SomeRequest(), original),
            "teacher@example.org",
            SomeInstant);

        var exception = Assert.Throws<InvalidOperationException>(
            () => EgressGate.EnsureProviderMatches(staleConfirmation, moved));

        Assert.Contains(SyntheticInferenceProvider.EndpointOrigin, exception.Message, StringComparison.Ordinal);
        Assert.Contains("https://replacement.synthetic.invalid", exception.Message, StringComparison.Ordinal);

        var freshConfirmation = EgressGate.Confirm(
            EgressGate.Preview(SomeRequest(), moved),
            "teacher@example.org",
            SomeInstant);
        EgressGate.EnsureProviderMatches(freshConfirmation, moved);
    }

    [Fact]
    public void Provider_capabilities_retain_the_original_constructor_and_deconstruction_shape()
    {
        var capabilities = new ProviderCapabilities(
            "source-compatible",
            "deployment",
            "model",
            SupportsImageInput: true,
            SupportsStructuredOutput: false);

        var (providerId, deploymentId, pinnedModelVersion, supportsImageInput, supportsStructuredOutput) = capabilities;

        Assert.Equal("source-compatible", providerId);
        Assert.Equal("deployment", deploymentId);
        Assert.Equal("model", pinnedModelVersion);
        Assert.True(supportsImageInput);
        Assert.False(supportsStructuredOutput);
        Assert.Null(capabilities.EndpointOrigin);
    }

    [Fact]
    public async Task Preview_freezes_the_part_list_and_image_bytes_before_confirmation_and_dispatch()
    {
        var sourceBytes = new byte[] { 1, 2, 3, 4 };
        var sourceParts = new List<InferencePart>
        {
            new TextPart("before"),
            new ImagePart(sourceBytes, "image/png"),
        };
        var sourceRequest = new InferenceRequest(
            "all-aboard.task-strip",
            "0.1.0",
            "schema.all-aboard.v1",
            sourceParts,
            DataLane.Green);

        var preview = EgressGate.Preview(sourceRequest, SyntheticInferenceProvider.DefaultCapabilities);
        var previewCopy = preview.Request;

        sourceParts[0] = new TextPart("mutated after preview");
        sourceParts.Add(new TextPart("not previewed"));
        sourceBytes[0] = 91;
        MutateFirstImageByte(previewCopy, 92);

        var confirmed = EgressGate.Confirm(preview, "teacher@example.org", SomeInstant);
        var confirmationCopy = confirmed.Request;

        sourceParts.Clear();
        sourceBytes[1] = 93;
        MutateFirstImageByte(confirmationCopy, 94);

        var provider = new CapturingProvider();
        await provider.CompleteAsync(confirmed, CancellationToken.None);

        var dispatched = Assert.IsType<InferenceRequest>(provider.ObservedRequest);
        Assert.Collection(
            dispatched.Parts,
            part => Assert.Equal("before", Assert.IsType<TextPart>(part).Text),
            part => Assert.Equal(
                new byte[] { 1, 2, 3, 4 },
                Assert.IsType<ImagePart>(part).Bytes.ToArray()));
        Assert.Equal(
            preview.PayloadSha256,
            EgressGate.Preview(dispatched, SyntheticInferenceProvider.DefaultCapabilities).PayloadSha256);
    }

    [Fact]
    public void Providers_cannot_accept_an_unpreviewed_request()
    {
        var parameters = typeof(IInferenceProvider)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .SelectMany(m => m.GetParameters())
            .ToList();

        Assert.DoesNotContain(parameters, p => p.ParameterType == typeof(InferenceRequest));
        Assert.Contains(parameters, p => p.ParameterType == typeof(PreviewedRequest));
    }

    [Fact]
    public void Previewed_requests_have_no_public_constructor()
    {
        Assert.Empty(typeof(PreviewedRequest).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(typeof(OutboundPreview).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
    }

    private static void MutateFirstImageByte(InferenceRequest request, byte value)
    {
        var image = Assert.IsType<ImagePart>(request.Parts[1]);
        Assert.True(MemoryMarshal.TryGetArray(image.Bytes, out ArraySegment<byte> segment));
        segment.Array![segment.Offset] = value;
    }

    private sealed class CapturingProvider : IInferenceProvider
    {
        public InferenceRequest? ObservedRequest { get; private set; }

        public Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken)
            => Task.FromResult(SyntheticInferenceProvider.DefaultCapabilities);

        public Task<InferenceResult> CompleteAsync(PreviewedRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObservedRequest = request.Request;
            return Task.FromResult(InferenceResult.Success("{}"));
        }
    }
}
