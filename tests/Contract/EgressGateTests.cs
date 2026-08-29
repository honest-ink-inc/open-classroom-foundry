using System.Reflection;
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
    public void The_preview_shows_the_exact_outbound_parts_of_the_very_request_that_will_be_sent()
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
        Assert.Equal(DataLane.Green, preview.PayloadLane);

        // Exactness by identity: the preview wraps the same object the provider will receive.
        Assert.Same(request, preview.Request);
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
        Assert.Same(preview.Request, confirmed.Request);
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
    }
}
