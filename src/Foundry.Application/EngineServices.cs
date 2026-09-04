// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Application;

/// <summary>Lane rules of plan §4: the envelope's declared lane governs; detection escalates only.</summary>
public sealed class DefaultDataPolicyEvaluator : IDataPolicyEvaluator
{
    public DataLane Evaluate(SourceEnvelope source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Lane;
    }

    public DataLane EscalateFromDetection(DataLane current, DataLane detected)
        => LanePolicy.Escalate(current, detected);
}

/// <summary>Structural document validation; module invariants layer on top per recipe.</summary>
public sealed class DefaultArtifactValidator : IArtifactValidator
{
    public IReadOnlyList<ValidationIssue> Validate(ArtifactDocument document)
        => DocumentValidator.Validate(document);
}

/// <summary>
/// In-memory diagnostics for development and tests. Content-free by policy: an event
/// that fails <see cref="DiagnosticPolicy"/> throws, so a content leak is loud in CI
/// long before any production sink exists.
/// </summary>
public sealed class InMemoryDiagnosticsSink : IDiagnosticsSink
{
    private readonly List<DiagnosticEvent> _events = [];

    public IReadOnlyList<DiagnosticEvent> Events => _events;

    public void Record(DiagnosticEvent diagnosticEvent)
    {
        if (!DiagnosticPolicy.IsContentFree(diagnosticEvent))
        {
            throw new InvalidOperationException(
                "Rejected diagnostic event: a field failed the content-free policy. Diagnostics never carry content.");
        }

        _events.Add(diagnosticEvent);
    }
}
