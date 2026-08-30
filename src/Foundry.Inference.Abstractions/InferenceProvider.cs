// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Domain;

namespace Foundry.Inference;

// The inference boundary of implementation plan §6.7. The provider receives a
// minimum explicit payload and returns a strict structured object. The interface
// shape enforces statelessness: there is no session, thread, history, tool,
// filesystem, or approval concept here, and none may be added.

public abstract record InferencePart;

public sealed record TextPart(string Text) : InferencePart;

public sealed record ImagePart(ReadOnlyMemory<byte> Bytes, string MimeType) : InferencePart;

public sealed record InferenceRequest(
    string RecipeId,
    string RecipeVersion,
    string OutputSchemaId,
    IReadOnlyList<InferencePart> Parts,
    DataLane PayloadLane);

/// <summary>The provider-failure taxonomy of plan §12's fake-provider suite, made a type.</summary>
public enum InferenceOutcome
{
    StructuredOutput,
    Refusal,
    ContentFiltered,
    MalformedOutput,
    SchemaMismatch,
    Truncated,
    Timeout,
    Unauthorized,
    RateLimited,
    ProviderError,
    UnsupportedCapability,

    /// <summary>District policy refused the call before any egress (R2-1): cloud inference disabled, or the payload lane exceeds the district maximum.</summary>
    PolicyRefused,
}

public sealed record InferenceResult(InferenceOutcome Outcome, string? StructuredJson)
{
    public static InferenceResult Success(string structuredJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(structuredJson);
        return new InferenceResult(InferenceOutcome.StructuredOutput, structuredJson);
    }

    public static InferenceResult Failure(InferenceOutcome outcome)
    {
        if (outcome == InferenceOutcome.StructuredOutput)
        {
            throw new ArgumentException("A structured output is not a failure.", nameof(outcome));
        }

        return new InferenceResult(outcome, null);
    }

    public bool IsSuccess => Outcome == InferenceOutcome.StructuredOutput;
}

public sealed record ProviderCapabilities(
    string ProviderId,
    string DeploymentId,
    string? PinnedModelVersion,
    bool SupportsImageInput,
    bool SupportsStructuredOutput,
    string? EndpointOrigin = null)
{
    /// <summary>Retains the original five-value deconstruction contract.</summary>
    public void Deconstruct(
        out string providerId,
        out string deploymentId,
        out string? pinnedModelVersion,
        out bool supportsImageInput,
        out bool supportsStructuredOutput)
    {
        providerId = ProviderId;
        deploymentId = DeploymentId;
        pinnedModelVersion = PinnedModelVersion;
        supportsImageInput = SupportsImageInput;
        supportsStructuredOutput = SupportsStructuredOutput;
    }
}

/// <summary>
/// Canonicalizes the HTTPS origin that identifies one provider endpoint.
/// Paths, queries, fragments, host casing, internationalized host spelling,
/// and default ports cannot create alternate spellings of the same authority.
/// </summary>
public static class InferenceEndpointOrigin
{
    public static string Normalize(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        if (!endpoint.IsAbsoluteUri
            || endpoint.Scheme != Uri.UriSchemeHttps
            || string.IsNullOrWhiteSpace(endpoint.IdnHost)
            || !string.IsNullOrEmpty(endpoint.UserInfo))
        {
            throw new ArgumentException(
                "An inference endpoint must be an absolute HTTPS URI without user information.",
                nameof(endpoint));
        }

        var host = endpoint.HostNameType == UriHostNameType.IPv6
            ? $"[{endpoint.IdnHost}]"
            : endpoint.IdnHost.ToLowerInvariant();
        var port = endpoint.IsDefaultPort ? string.Empty : $":{endpoint.Port}";
        return $"{endpoint.Scheme.ToLowerInvariant()}://{host}{port}";
    }

    public static bool TryNormalize(string? endpoint, out string normalizedOrigin)
    {
        normalizedOrigin = string.Empty;
        if (string.IsNullOrWhiteSpace(endpoint)
            || !Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        try
        {
            normalizedOrigin = Normalize(uri);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}

public interface IInferenceProvider
{
    Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Accepts only a <see cref="PreviewedRequest"/>: Gate A is structural. A raw
    /// <see cref="InferenceRequest"/> cannot reach any provider without the teacher
    /// confirming the exact outbound payload through <see cref="EgressGate"/>.
    /// </summary>
    Task<InferenceResult> CompleteAsync(PreviewedRequest request, CancellationToken cancellationToken);
}
