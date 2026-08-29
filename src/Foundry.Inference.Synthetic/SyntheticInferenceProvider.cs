// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Inference;

namespace Foundry.Inference.Synthetic;

/// <summary>One scripted provider response: an optional delay, then a result.</summary>
public sealed record SyntheticStep(InferenceResult Result, TimeSpan Delay)
{
    public static SyntheticStep Structured(string structuredJson)
        => new(InferenceResult.Success(structuredJson), TimeSpan.Zero);

    public static SyntheticStep Outcome(InferenceOutcome outcome)
        => new(InferenceResult.Failure(outcome), TimeSpan.Zero);

    public static SyntheticStep DelayedOutcome(InferenceOutcome outcome, TimeSpan delay)
        => new(InferenceResult.Failure(outcome), delay);
}

/// <summary>
/// The deterministic provider for CI and offline development (plan §6.7, §12).
/// No randomness, no network, no state beyond its explicit script: the same script
/// always produces the same sequence of results. Covers every fake-provider case —
/// valid output, refusal, content filtering, malformed output, schema mismatch,
/// truncation, timeout, 401/403, 429, 5xx, cancellation, unsupported capability.
/// </summary>
public sealed class SyntheticInferenceProvider : IInferenceProvider
{
    public const string EmptyStructuredOutput = "{}";

    private readonly Queue<SyntheticStep> _script;
    private readonly ProviderCapabilities _capabilities;

    public SyntheticInferenceProvider(ProviderCapabilities? capabilities = null, params SyntheticStep[] script)
    {
        _capabilities = capabilities ?? DefaultCapabilities;
        _script = new Queue<SyntheticStep>(script);
    }

    public static ProviderCapabilities DefaultCapabilities { get; } = new(
        ProviderId: "synthetic",
        DeploymentId: "synthetic-1",
        PinnedModelVersion: "synthetic-1.0",
        SupportsImageInput: true,
        SupportsStructuredOutput: true);

    public Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_capabilities);
    }

    public async Task<InferenceResult> CompleteAsync(PreviewedRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        // An exhausted or empty script answers with a benign structured object,
        // so simple tests need no scripting at all.
        var step = _script.Count > 0
            ? _script.Dequeue()
            : SyntheticStep.Structured(EmptyStructuredOutput);

        if (step.Delay > TimeSpan.Zero)
        {
            await Task.Delay(step.Delay, cancellationToken).ConfigureAwait(false);
        }

        return step.Result;
    }
}
