// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Foundry.Domain;

namespace Foundry.Inference;

// Gate A made structural (plan §5): the teacher sees the exact outbound derivative,
// and a provider physically cannot be called without that confirmation, because
// IInferenceProvider accepts only a PreviewedRequest — constructable solely here,
// from the frozen request snapshot that was previewed.

public abstract record OutboundPartPreview;

/// <summary>The exact text that will leave the machine — verbatim, not summarized.</summary>
public sealed record OutboundTextPreview(string ExactText) : OutboundPartPreview;

/// <summary>The exact image bytes that will leave the machine; the review surface renders them from the frozen request snapshot.</summary>
public sealed record OutboundImagePreview(int ByteCount, string MimeType) : OutboundPartPreview;

public sealed class OutboundPreview
{
    private readonly InferenceRequest _request;

    internal OutboundPreview(
        InferenceRequest request,
        IReadOnlyList<OutboundPartPreview> parts,
        string payloadSha256,
        string providerId,
        string deploymentId,
        string? pinnedModelVersion,
        string endpointOrigin,
        DataLane payloadLane)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentId);
        if (pinnedModelVersion is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pinnedModelVersion);
        }

        _request = InferenceRequestSnapshot.Copy(request);
        Parts = parts;
        PayloadSha256 = payloadSha256;
        ProviderId = providerId;
        DeploymentId = deploymentId;
        PinnedModelVersion = pinnedModelVersion;
        EndpointOrigin = InferenceEndpointOrigin.Normalize(new Uri(endpointOrigin, UriKind.Absolute));
        PayloadLane = payloadLane;
    }

    /// <summary>
    /// Returns a defensive copy for display. The dispatch snapshot remains
    /// unreachable so preview code cannot mutate what a later confirmation sends.
    /// </summary>
    public InferenceRequest Request => InferenceRequestSnapshot.Copy(_request);

    public IReadOnlyList<OutboundPartPreview> Parts { get; }

    public string PayloadSha256 { get; }

    public string ProviderId { get; }

    public string DeploymentId { get; }

    public string? PinnedModelVersion { get; }

    public string EndpointOrigin { get; }

    public DataLane PayloadLane { get; }

    internal InferenceRequest CopyRequestForConfirmation()
        => InferenceRequestSnapshot.Copy(_request);
}

/// <summary>Proof that a named teacher confirmed one exact payload for one provider deployment at one moment.</summary>
public sealed record EgressReceipt(string PayloadSha256, string ConfirmedBy, DateTimeOffset ConfirmedAtUtc)
{
    /// <summary>
    /// Creates a provider-bound receipt. The three-argument constructor remains
    /// available for source compatibility, but Gate A confirmations always use
    /// this constructor and providers reject receipts without these coordinates.
    /// </summary>
    public EgressReceipt(
        string payloadSha256,
        string confirmedBy,
        DateTimeOffset confirmedAtUtc,
        string providerId,
        string deploymentId,
        string? pinnedModelVersion,
        string? endpointOrigin = null)
        : this(payloadSha256, confirmedBy, confirmedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentId);
        if (pinnedModelVersion is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pinnedModelVersion);
        }

        ProviderId = providerId;
        DeploymentId = deploymentId;
        PinnedModelVersion = pinnedModelVersion;
        if (endpointOrigin is not null)
        {
            EndpointOrigin = InferenceEndpointOrigin.Normalize(new Uri(endpointOrigin, UriKind.Absolute));
        }
    }

    public string ProviderId { get; } = string.Empty;

    public string DeploymentId { get; } = string.Empty;

    public string? PinnedModelVersion { get; }

    public string? EndpointOrigin { get; }
}

/// <summary>The only argument a provider accepts. There is no other construction path.</summary>
public sealed class PreviewedRequest
{
    private readonly InferenceRequest _request;

    internal PreviewedRequest(InferenceRequest request, EgressReceipt receipt)
    {
        _request = InferenceRequestSnapshot.Copy(request);
        Receipt = receipt;
    }

    /// <summary>
    /// Returns a defensive copy. A caller can inspect the confirmed request but
    /// cannot mutate the snapshot that a provider will subsequently observe.
    /// </summary>
    public InferenceRequest Request => InferenceRequestSnapshot.Copy(_request);

    public EgressReceipt Receipt { get; }

    /// <summary>The frozen payload lane, exposed without handing out the request snapshot.</summary>
    public DataLane PayloadLane => _request.PayloadLane;
}

public static class EgressGate
{
    /// <summary>Freezes the request once and builds the preview from that exact immutable snapshot.</summary>
    public static OutboundPreview Preview(InferenceRequest request, ProviderCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(capabilities);

        if (!InferenceEndpointOrigin.TryNormalize(capabilities.EndpointOrigin, out var endpointOrigin))
        {
            throw new ArgumentException(
                "Provider capabilities must identify a valid endpoint origin before Gate A preview.",
                nameof(capabilities));
        }

        var frozenRequest = InferenceRequestSnapshot.Copy(request);
        var parts = new OutboundPartPreview[frozenRequest.Parts.Count];
        for (var index = 0; index < frozenRequest.Parts.Count; index++)
        {
            var part = frozenRequest.Parts[index];
            parts[index] = part switch
            {
                TextPart text => new OutboundTextPreview(text.Text),
                ImagePart image => new OutboundImagePreview(image.Bytes.Length, image.MimeType),
                _ => throw new InvalidOperationException($"Unknown inference part {part.GetType().Name}."),
            };
        }

        return new OutboundPreview(
            frozenRequest,
            Array.AsReadOnly(parts),
            ComputePayloadSha256(frozenRequest),
            capabilities.ProviderId,
            capabilities.DeploymentId,
            capabilities.PinnedModelVersion,
            endpointOrigin,
            frozenRequest.PayloadLane);
    }

    public static PreviewedRequest Confirm(OutboundPreview preview, string confirmedBy, DateTimeOffset confirmedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(preview);
        ArgumentException.ThrowIfNullOrWhiteSpace(confirmedBy);

        var receipt = new EgressReceipt(
            preview.PayloadSha256,
            confirmedBy,
            confirmedAtUtc,
            preview.ProviderId,
            preview.DeploymentId,
            preview.PinnedModelVersion,
            preview.EndpointOrigin);
        return new PreviewedRequest(preview.CopyRequestForConfirmation(), receipt);
    }

    /// <summary>
    /// Refuses dispatch unless the provider, deployment, model, and endpoint
    /// origin are exactly the ones the teacher saw and confirmed. Identity
    /// matching is ordinal; endpoint spellings are normalized first.
    /// </summary>
    public static void EnsureProviderMatches(PreviewedRequest request, ProviderCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(capabilities);

        var receiptHasEndpoint = InferenceEndpointOrigin.TryNormalize(
            request.Receipt.EndpointOrigin,
            out var receiptEndpointOrigin);
        var providerHasEndpoint = InferenceEndpointOrigin.TryNormalize(
            capabilities.EndpointOrigin,
            out var providerEndpointOrigin);

        if (!string.Equals(request.Receipt.ProviderId, capabilities.ProviderId, StringComparison.Ordinal)
            || !string.Equals(request.Receipt.DeploymentId, capabilities.DeploymentId, StringComparison.Ordinal)
            || !string.Equals(
                request.Receipt.PinnedModelVersion,
                capabilities.PinnedModelVersion,
                StringComparison.Ordinal)
            || !receiptHasEndpoint
            || !providerHasEndpoint
            || !string.Equals(receiptEndpointOrigin, providerEndpointOrigin, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Gate A confirmation targets provider '{request.Receipt.ProviderId}' deployment '{request.Receipt.DeploymentId}', " +
                $"model '{request.Receipt.PinnedModelVersion ?? "unversioned"}', endpoint origin " +
                $"'{request.Receipt.EndpointOrigin ?? "unspecified"}', not provider '{capabilities.ProviderId}' " +
                $"deployment '{capabilities.DeploymentId}', model '{capabilities.PinnedModelVersion ?? "unversioned"}', " +
                $"endpoint origin '{capabilities.EndpointOrigin ?? "unspecified"}'. " +
                "Preview and confirm the request again.");
        }
    }

    private static string ComputePayloadSha256(InferenceRequest request)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        AppendString(hash, request.RecipeId);
        AppendString(hash, request.RecipeVersion);
        AppendString(hash, request.OutputSchemaId);
        AppendString(hash, ((int)request.PayloadLane).ToString(CultureInfo.InvariantCulture));

        foreach (var part in request.Parts)
        {
            switch (part)
            {
                case TextPart text:
                    AppendString(hash, "text");
                    AppendString(hash, text.Text);
                    break;
                case ImagePart image:
                    AppendString(hash, "image");
                    AppendString(hash, image.MimeType);
                    AppendLength(hash, image.Bytes.Length);
                    hash.AppendData(image.Bytes.Span);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown inference part {part.GetType().Name}.");
            }
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void AppendString(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        AppendLength(hash, bytes.Length);
        hash.AppendData(bytes);
    }

    private static void AppendLength(IncrementalHash hash, int length)
        => hash.AppendData(BitConverter.GetBytes(length));
}

internal static class InferenceRequestSnapshot
{
    public static InferenceRequest Copy(InferenceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Parts);

        if (!Enum.IsDefined(request.PayloadLane)
            || request.PayloadLane == DataLane.Restricted)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.PayloadLane,
                "An inference request must use a defined, non-Restricted data lane.");
        }

        var parts = new InferencePart[request.Parts.Count];
        for (var index = 0; index < request.Parts.Count; index++)
        {
            var part = request.Parts[index]
                ?? throw new InvalidOperationException("An inference request cannot contain a null part.");
            parts[index] = part switch
            {
                TextPart text => new TextPart(text.Text),
                ImagePart image => new ImagePart(image.Bytes.ToArray(), image.MimeType),
                _ => throw new InvalidOperationException($"Unknown inference part {part.GetType().Name}."),
            };
        }

        return new InferenceRequest(
            request.RecipeId,
            request.RecipeVersion,
            request.OutputSchemaId,
            Array.AsReadOnly(parts),
            request.PayloadLane);
    }
}
