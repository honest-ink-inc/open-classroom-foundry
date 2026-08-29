// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Foundry.Domain;

namespace Foundry.Inference;

// Gate A made structural (plan §5): the teacher sees the exact outbound derivative,
// and a provider physically cannot be called without that confirmation, because
// IInferenceProvider accepts only a PreviewedRequest — constructable solely here,
// from the very request object that will be sent.

public abstract record OutboundPartPreview;

/// <summary>The exact text that will leave the machine — verbatim, not summarized.</summary>
public sealed record OutboundTextPreview(string ExactText) : OutboundPartPreview;

/// <summary>The exact image bytes that will leave the machine; the review surface renders them from the request itself.</summary>
public sealed record OutboundImagePreview(int ByteCount, string MimeType) : OutboundPartPreview;

public sealed record OutboundPreview(
    InferenceRequest Request,
    IReadOnlyList<OutboundPartPreview> Parts,
    string PayloadSha256,
    string ProviderId,
    string DeploymentId,
    DataLane PayloadLane);

/// <summary>Proof that a named teacher confirmed one exact payload at one moment.</summary>
public sealed record EgressReceipt(string PayloadSha256, string ConfirmedBy, DateTimeOffset ConfirmedAtUtc);

/// <summary>The only argument a provider accepts. There is no other construction path.</summary>
public sealed class PreviewedRequest
{
    internal PreviewedRequest(InferenceRequest request, EgressReceipt receipt)
    {
        Request = request;
        Receipt = receipt;
    }

    public InferenceRequest Request { get; }

    public EgressReceipt Receipt { get; }
}

public static class EgressGate
{
    /// <summary>Builds the preview from the same request object that will be sent — exactness by identity, not by copy.</summary>
    public static OutboundPreview Preview(InferenceRequest request, ProviderCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(capabilities);

        var parts = new List<OutboundPartPreview>(request.Parts.Count);
        foreach (var part in request.Parts)
        {
            parts.Add(part switch
            {
                TextPart text => new OutboundTextPreview(text.Text),
                ImagePart image => new OutboundImagePreview(image.Bytes.Length, image.MimeType),
                _ => throw new InvalidOperationException($"Unknown inference part {part.GetType().Name}."),
            });
        }

        return new OutboundPreview(
            request,
            parts,
            ComputePayloadSha256(request),
            capabilities.ProviderId,
            capabilities.DeploymentId,
            request.PayloadLane);
    }

    public static PreviewedRequest Confirm(OutboundPreview preview, string confirmedBy, DateTimeOffset confirmedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(preview);
        ArgumentException.ThrowIfNullOrWhiteSpace(confirmedBy);

        var receipt = new EgressReceipt(preview.PayloadSha256, confirmedBy, confirmedAtUtc);
        return new PreviewedRequest(preview.Request, receipt);
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
