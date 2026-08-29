// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Contracts;
using Foundry.Inference;

namespace Foundry.Application;

/// <summary>
/// Council finding R2-1: district policy is law at the provider boundary, not
/// configuration beside it. With cloud inference disabled, or a payload lane
/// above the district maximum, the call is refused before any egress — the
/// inner provider is never touched. Composition roots wrap every real provider
/// in this gate; the deterministic authoring path never reaches here at all.
/// </summary>
public sealed class PolicyGatedInferenceProvider(IInferenceProvider inner, IDistrictPolicyProvider policy) : IInferenceProvider
{
    public Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken)
    {
        if (!policy.Current.CloudInferenceEnabled)
        {
            throw new InvalidOperationException(
                "District policy has not enabled cloud inference; there are no capabilities to report. The deterministic authoring path is unaffected.");
        }

        return inner.GetCapabilitiesAsync(cancellationToken);
    }

    public Task<InferenceResult> CompleteAsync(PreviewedRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!policy.Current.CloudInferenceEnabled)
        {
            return Task.FromResult(InferenceResult.Failure(InferenceOutcome.PolicyRefused));
        }

        if (request.Request.PayloadLane > policy.Current.MaximumLane)
        {
            return Task.FromResult(InferenceResult.Failure(InferenceOutcome.PolicyRefused));
        }

        return inner.CompleteAsync(request, cancellationToken);
    }
}
