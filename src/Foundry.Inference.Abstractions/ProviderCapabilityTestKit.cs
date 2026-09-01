// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text;
using System.Text.Json;
using Foundry.Domain;

namespace Foundry.Inference;

/// <summary>
/// The one conformance bar every provider must clear before any module trusts it
/// (plan §6.7) — the same kit for Azure, for the future local adapter, and for the
/// synthetic provider that stands in for both. The caller supplies confirmation,
/// because Gate A belongs to a person: in CI that person is the test author.
/// </summary>
public static class ProviderCapabilityTestKit
{
    public const string CapabilityProbeOutputSchemaId = "schema.capability-probe.v1";

    public const string CapabilityProbeExpectedJson = """{"capabilityProbe":"ok"}""";

    /// <summary>
    /// The exact schema a configured provider may bind for the shared probe.
    /// The kit also validates the returned bytes itself, so an unregistered or
    /// ignored provider schema cannot manufacture a clean conformance result.
    /// </summary>
    public const string CapabilityProbeOutputSchemaJson =
        """{"type":"object","properties":{"capabilityProbe":{"type":"string","enum":["ok"]}},"required":["capabilityProbe"],"additionalProperties":false}""";

    private const int MaxCapabilityProbeUtf8Bytes = 256;
    private const int MaxCapabilityProbeJsonDepth = 4;

    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    /// <summary>
    /// Runs the common text, image, structured-object, and cancellation probes.
    /// Refusal behavior is deliberately absent from this source-compatible
    /// overload because the kit must not invent content that a configured
    /// deployment ought to refuse. Its findings therefore always record that
    /// full conformance remains unproved.
    /// </summary>
    public static Task<IReadOnlyList<string>> RunAsync(
        IInferenceProvider provider,
        Func<InferenceRequest, PreviewedRequest> confirm,
        CancellationToken cancellationToken)
        => RunCoreAsync(provider, confirm, expectedRefusalProbe: null, cancellationToken);

    /// <summary>
    /// Runs the common probes plus one caller-supplied request whose expected
    /// outcome is <see cref="InferenceOutcome.Refusal"/>. The caller owns the
    /// harmless, deployment-specific reason that makes refusal expected; the
    /// shared kit supplies no model-policy assumption of its own.
    /// </summary>
    public static Task<IReadOnlyList<string>> RunAsync(
        IInferenceProvider provider,
        Func<InferenceRequest, PreviewedRequest> confirm,
        InferenceRequest expectedRefusalProbe,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expectedRefusalProbe);
        return RunCoreAsync(provider, confirm, expectedRefusalProbe, cancellationToken);
    }

    private static async Task<IReadOnlyList<string>> RunCoreAsync(
        IInferenceProvider provider,
        Func<InferenceRequest, PreviewedRequest> confirm,
        InferenceRequest? expectedRefusalProbe,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(confirm);

        var findings = new List<string>();

        var capabilities = await provider.GetCapabilitiesAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(capabilities.ProviderId) || string.IsNullOrWhiteSpace(capabilities.DeploymentId))
        {
            findings.Add("Capabilities must name the provider and deployment for the evaluation record.");
        }

        if (!capabilities.SupportsStructuredOutput)
        {
            findings.Add("Structured output is unsupported; no module may use this provider (free-form output is prohibited).");
        }

        var textProbe = new InferenceRequest(
            "capability-probe", "1.0.0", CapabilityProbeOutputSchemaId,
            [new TextPart($"Return exactly this JSON object and nothing else: {CapabilityProbeExpectedJson}")],
            DataLane.Green);

        await CheckStructuredObjectProbeAsync(
            provider,
            confirm,
            textProbe,
            "The text structured probe",
            findings,
            cancellationToken).ConfigureAwait(false);

        if (!capabilities.SupportsImageInput)
        {
            findings.Add("Image input is unsupported; image-requiring modules may not use this provider.");
        }
        else
        {
            var imageProbe = new InferenceRequest(
                "capability-image-probe",
                "1.0.0",
                CapabilityProbeOutputSchemaId,
                [
                    new TextPart(
                        $"Accept the supplied synthetic test pixel, then return exactly this JSON object and nothing else: {CapabilityProbeExpectedJson}"),
                    new ImagePart(OnePixelPng, "image/png"),
                ],
                DataLane.Green);
            await CheckStructuredObjectProbeAsync(
                provider,
                confirm,
                imageProbe,
                "The image structured probe",
                findings,
                cancellationToken).ConfigureAwait(false);
        }

        if (expectedRefusalProbe is not null)
        {
            var refusal = await provider.CompleteAsync(
                confirm(expectedRefusalProbe),
                cancellationToken).ConfigureAwait(false);
            if (refusal.Outcome != InferenceOutcome.Refusal)
            {
                findings.Add(
                    $"The explicitly expected refusal probe returned {refusal.Outcome} instead of Refusal.");
            }
            else if (refusal.StructuredJson is not null)
            {
                findings.Add("The explicitly expected refusal probe attached structured output to a refusal.");
            }
        }
        else
        {
            findings.Add(
                "Refusal behavior was not tested; full provider conformance requires a caller-supplied expected-refusal probe.");
        }

        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync().ConfigureAwait(false);
        try
        {
            _ = await provider.CompleteAsync(confirm(textProbe), cancelled.Token).ConfigureAwait(false);
            findings.Add("A cancelled token was not honored; teachers must always be able to stop a call.");
        }
        catch (OperationCanceledException)
        {
            // Honored, as required.
        }

        return findings;
    }

    private static async Task CheckStructuredObjectProbeAsync(
        IInferenceProvider provider,
        Func<InferenceRequest, PreviewedRequest> confirm,
        InferenceRequest probe,
        string probeName,
        List<string> findings,
        CancellationToken cancellationToken)
    {
        var result = await provider.CompleteAsync(confirm(probe), cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.StructuredJson))
        {
            findings.Add($"{probeName} returned {result.Outcome} instead of structured output.");
            return;
        }

        if (result.StructuredJson.Length > MaxCapabilityProbeUtf8Bytes
            || Encoding.UTF8.GetByteCount(result.StructuredJson) > MaxCapabilityProbeUtf8Bytes)
        {
            findings.Add($"{probeName} did not return the exact bounded capability-probe object.");
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(
                result.StructuredJson,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = MaxCapabilityProbeJsonDepth,
                });
            var properties = document.RootElement.ValueKind == JsonValueKind.Object
                ? document.RootElement.EnumerateObject().ToArray()
                : [];
            if (properties.Length != 1
                || !properties[0].NameEquals("capabilityProbe")
                || properties[0].Value.ValueKind != JsonValueKind.String
                || !string.Equals(properties[0].Value.GetString(), "ok", StringComparison.Ordinal))
            {
                findings.Add($"{probeName} did not return the exact bounded capability-probe object.");
            }
        }
        catch (JsonException)
        {
            findings.Add($"{probeName} did not return the exact bounded capability-probe object.");
        }
    }
}
