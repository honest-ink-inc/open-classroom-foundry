// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Inference;

namespace Foundry.Application;

/// <summary>
/// The engine's inference orchestration: modules never touch a provider (ADR-001);
/// they hand intent here. The two-step shape is Gate A made unavoidable — Prepare
/// yields the exact outbound preview, and only a teacher-confirmed
/// <see cref="PreviewedRequest"/> can reach <see cref="RunAsync"/>. A suggestion is
/// always a proposal into the review surface, never an approved artifact.
/// </summary>
public sealed class SuggestionRunner(IInferenceProvider provider)
{
    public async Task<OutboundPreview> PrepareAsync(InferenceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var capabilities = await provider.GetCapabilitiesAsync(cancellationToken).ConfigureAwait(false);
        return EgressGate.Preview(request, capabilities);
    }

    public async Task<InferenceResult> RunAsync(PreviewedRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var capabilities = await provider.GetCapabilitiesAsync(cancellationToken).ConfigureAwait(false);
        EgressGate.EnsureProviderMatches(request, capabilities);
        return await provider.CompleteAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
