// SPDX-License-Identifier: GPL-3.0-or-later
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
    public static async Task<IReadOnlyList<string>> RunAsync(
        IInferenceProvider provider,
        Func<InferenceRequest, PreviewedRequest> confirm,
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

        var probe = new InferenceRequest(
            "capability-probe", "1.0.0", "schema.capability-probe.v1",
            [new TextPart("probe")], DataLane.Green);

        var result = await provider.CompleteAsync(confirm(probe), cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.StructuredJson))
        {
            findings.Add($"The structured probe returned {result.Outcome} instead of structured output.");
        }

        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync().ConfigureAwait(false);
        try
        {
            _ = await provider.CompleteAsync(confirm(probe), cancelled.Token).ConfigureAwait(false);
            findings.Add("A cancelled token was not honored; teachers must always be able to stop a call.");
        }
        catch (OperationCanceledException)
        {
            // Honored, as required.
        }

        return findings;
    }
}
