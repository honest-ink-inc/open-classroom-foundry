// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Contracts;
using Foundry.Domain;
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
    public async Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken)
    {
        var currentPolicy = policy.Current;
        if (!HasCompleteCloudGrant(currentPolicy))
        {
            throw new InvalidOperationException(
                "District policy has not granted one complete cloud provider deployment; there are no capabilities to report. The deterministic authoring path is unaffected.");
        }

        var capabilities = await inner.GetCapabilitiesAsync(cancellationToken).ConfigureAwait(false);
        currentPolicy = policy.Current;
        if (!HasCompleteCloudGrant(currentPolicy)
            || !PolicyAuthorizes(currentPolicy, capabilities))
        {
            throw new InvalidOperationException(
                "The configured inference provider and deployment do not match the district policy grant.");
        }

        return capabilities;
    }

    public async Task<InferenceResult> CompleteAsync(PreviewedRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var currentPolicy = policy.Current;

        if (!HasCompleteCloudGrant(currentPolicy))
        {
            return InferenceResult.Failure(InferenceOutcome.PolicyRefused);
        }

        if (!Enum.IsDefined(request.PayloadLane)
            || request.PayloadLane == DataLane.Restricted
            || request.PayloadLane > currentPolicy.MaximumLane)
        {
            return InferenceResult.Failure(InferenceOutcome.PolicyRefused);
        }

        var capabilities = await inner.GetCapabilitiesAsync(cancellationToken).ConfigureAwait(false);

        // Re-read after the asynchronous capability query so a withdrawn or
        // changed district grant cannot be ignored merely because dispatch began
        // under an older snapshot.
        currentPolicy = policy.Current;
        if (!HasCompleteCloudGrant(currentPolicy)
            || request.PayloadLane == DataLane.Restricted
            || request.PayloadLane > currentPolicy.MaximumLane
            || !PolicyAuthorizes(currentPolicy, capabilities))
        {
            return InferenceResult.Failure(InferenceOutcome.PolicyRefused);
        }

        EgressGate.EnsureProviderMatches(request, capabilities);
        return await inner.CompleteAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static bool HasCompleteCloudGrant(DistrictPolicy policy)
        => policy.CloudInferenceEnabled
            && policy.MaximumLane is DataLane.Green or DataLane.Amber
            && policy.AllowedEndpoints is { Count: > 0 }
            && policy.AllowedEndpoints.Any(
                endpoint => InferenceEndpointOrigin.TryNormalize(endpoint, out _))
            && !string.IsNullOrWhiteSpace(policy.ProviderId)
            && !string.IsNullOrWhiteSpace(policy.DeploymentId);

    private static bool PolicyAuthorizes(
        DistrictPolicy policy,
        ProviderCapabilities capabilities)
    {
        if (!string.Equals(policy.ProviderId, capabilities.ProviderId, StringComparison.Ordinal)
            || !string.Equals(policy.DeploymentId, capabilities.DeploymentId, StringComparison.Ordinal)
            || !InferenceEndpointOrigin.TryNormalize(capabilities.EndpointOrigin, out var providerOrigin))
        {
            return false;
        }

        return policy.AllowedEndpoints.Any(endpoint =>
            InferenceEndpointOrigin.TryNormalize(endpoint, out var allowedOrigin)
            && string.Equals(allowedOrigin, providerOrigin, StringComparison.Ordinal));
    }
}
