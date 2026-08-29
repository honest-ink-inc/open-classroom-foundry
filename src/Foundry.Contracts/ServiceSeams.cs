// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Domain;

namespace Foundry.Contracts;

// The service seams of implementation plan §6.2. Modules reach devices, storage,
// and diagnostics only through these; the render/export/print/save sinks accept
// ApprovedArtifact and nothing else (ADR-004) — verified by SinkContractTests.

public interface IApprovalGate
{
    ApprovedArtifact Approve(DraftArtifact draft, string approvedBy, IReadOnlyList<ValidationIssue> outstandingIssues, DateTimeOffset approvedAtUtc);
}

public interface IDataPolicyEvaluator
{
    DataLane Evaluate(SourceEnvelope source);

    /// <summary>Detection may escalate a lane; it may never certify content as Green.</summary>
    DataLane EscalateFromDetection(DataLane current, DataLane detected);
}

public interface IArtifactValidator
{
    IReadOnlyList<ValidationIssue> Validate(ArtifactDocument document);
}

public interface IStructuredOutputValidator
{
    IReadOnlyList<ValidationIssue> Validate(string outputSchemaId, string structuredJson);
}

public interface IDiagnosticsSink
{
    /// <summary>Implementations must reject events that fail <see cref="DiagnosticPolicy"/> — loudly, not silently.</summary>
    void Record(DiagnosticEvent diagnosticEvent);
}

public interface ICaptureSource
{
    Task<SourceEnvelope> CaptureAsync(CaptureRequest request, CancellationToken cancellationToken);
}

public interface IDocumentNormalizer
{
    Task<SourceEnvelope> NormalizeAsync(SourceEnvelope source, NormalizationRequest request, CancellationToken cancellationToken);
}

public interface IOcrService
{
    Task<OcrResult> RecognizeAsync(SourceEnvelope source, CancellationToken cancellationToken);
}

public interface IRedactionAssistant
{
    Task<RedactionSuggestions> SuggestAsync(SourceEnvelope source, CancellationToken cancellationToken);
}

public interface IRenderer
{
    Task<RenderedOutput> RenderAsync(ApprovedArtifact artifact, RenderRequest request, CancellationToken cancellationToken);
}

public interface IExporter
{
    Task ExportAsync(ApprovedArtifact artifact, ExportRequest request, CancellationToken cancellationToken);
}

public interface IPrinter
{
    Task PrintAsync(ApprovedArtifact artifact, PrintRequest request, CancellationToken cancellationToken);
}

public interface IProjectStore
{
    /// <summary>Deliberate save of a Green-lane product; implementations refuse any other lane.</summary>
    Task SaveGreenProjectAsync(ApprovedArtifact artifact, ProjectSaveRequest request, CancellationToken cancellationToken);
}

public interface IAssetCatalog
{
    IReadOnlyList<AssetProvenance> All { get; }

    AssetProvenance? Find(AssetId id);

    bool TryGetContent(AssetId id, out ReadOnlyMemory<byte> content, out string mimeType);
}

public interface IRecipeRegistry
{
    IReadOnlyList<RecipeManifest> All { get; }

    RecipeManifest? Find(string recipeId, string recipeVersion);
}

public interface IRecipeRunner
{
    Task<DraftArtifact> RunAsync(RecipeManifest recipe, RecipeRunInputs inputs, CancellationToken cancellationToken);
}
