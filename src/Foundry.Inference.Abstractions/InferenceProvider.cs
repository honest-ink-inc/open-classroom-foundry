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
    bool SupportsStructuredOutput);

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
