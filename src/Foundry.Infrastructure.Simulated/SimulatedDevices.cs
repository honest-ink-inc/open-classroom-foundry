using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Infrastructure.Simulated;

// The virtual bench of plan §12: deterministic stand-ins for camera, printer,
// exporter, and project store, so CI exercises full workflows with no hardware.
// Every sink here accepts only ApprovedArtifact — the compiler enforces ADR-004
// in tests exactly as in production.

/// <summary>Serves a configured frame into the session store as if a camera had captured it.</summary>
public sealed class SimulatedCameraSource(ISessionByteStore store, ReadOnlyMemory<byte> frame, string mimeType = "image/png") : ICaptureSource
{
    public const string Kind = "camera-simulator";

    public Task<SourceEnvelope> CaptureAsync(CaptureRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var reference = store.Put(frame);

        return Task.FromResult(new SourceEnvelope(
            SourceKind: Kind,
            MimeType: mimeType,
            PageCount: 1,
            Lane: LanePolicy.DefaultForUnknown,
            MetadataStripped: false,
            TeacherStatedRights: string.Empty,
            Bytes: reference));
    }
}

public sealed record RecordedPrintJob(ApprovalReceipt Receipt, PrintRequest Request);

/// <summary>Records print jobs instead of printing; refuses nothing an ApprovedArtifact allows.</summary>
public sealed class VirtualPrintSink : IPrinter
{
    private readonly List<RecordedPrintJob> _jobs = [];

    public IReadOnlyList<RecordedPrintJob> Jobs => _jobs;

    public Task PrintAsync(ApprovedArtifact artifact, PrintRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        _jobs.Add(new RecordedPrintJob(artifact.Receipt, request));
        return Task.CompletedTask;
    }
}

public sealed record RecordedExport(ApprovalReceipt Receipt, ExportRequest Request);

public sealed class RecordingExporter : IExporter
{
    private readonly List<RecordedExport> _exports = [];

    public IReadOnlyList<RecordedExport> Exports => _exports;

    public Task ExportAsync(ApprovedArtifact artifact, ExportRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        _exports.Add(new RecordedExport(artifact.Receipt, request));
        return Task.CompletedTask;
    }
}

public sealed record RecordedProjectSave(ApprovalReceipt Receipt, ProjectSaveRequest Request);

/// <summary>
/// Records deliberate Green saves — and enforces the lane contract exactly as the
/// real store must: an Amber or Restricted artifact is refused, never persisted.
/// </summary>
public sealed class RecordingProjectStore : IProjectStore
{
    private readonly List<RecordedProjectSave> _saves = [];

    public IReadOnlyList<RecordedProjectSave> Saves => _saves;

    public Task SaveGreenProjectAsync(ApprovedArtifact artifact, ProjectSaveRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (artifact.Revision.Lane != DataLane.Green)
        {
            throw new InvalidOperationException(
                $"Only Green-lane products may be saved to the project library; this artifact is {artifact.Revision.Lane}.");
        }

        _saves.Add(new RecordedProjectSave(artifact.Receipt, request));
        return Task.CompletedTask;
    }
}
