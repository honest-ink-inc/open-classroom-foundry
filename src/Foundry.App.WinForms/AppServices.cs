// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Rendering;
using Foundry.Storage;

namespace Foundry.App.WinForms;

/// <summary>
/// The small shared machinery both authoring surfaces stand on: the shipped
/// symbol catalog, the post-approval outputs, and the review-session plumbing.
/// Everything here accepts only an ApprovedArtifact where output is concerned —
/// the structural gate is not re-negotiated per surface (ADR-004).
/// </summary>
public static class AppServices
{
    /// <summary>The shipped CC0 pack beside the executable; an empty catalog when absent — the app still runs, symbol-less.</summary>
    public static IAssetCatalog SymbolCatalog()
    {
        var packaged = Path.Combine(AppContext.BaseDirectory, "assets", "symbols");
        return Directory.Exists(packaged) ? new JsonAssetCatalog(packaged) : new NoAssetsCatalog();
    }

    public static JobStateMachine MachineAtReview()
    {
        var machine = new JobStateMachine();
        foreach (var state in new[]
        {
            JobState.Imported, JobState.Normalized, JobState.DataLaneConfirmed,
            JobState.DraftGenerated, JobState.SchemaValidated, JobState.InvariantsValidated,
            JobState.AwaitingTeacherReview,
        })
        {
            machine.Transition(state);
        }

        return machine;
    }

    public static ReviewSession SessionOver(ArtifactDocument document)
        => new(DraftArtifact.New(document, DataLane.Green), MachineAtReview(), new DefaultArtifactValidator(), new DomainApprovalGate());

    public static byte[] Render(ApprovedArtifact artifact, RenderTarget target)
        => new AccessibleHtmlRenderer().RenderAsync(
                artifact, new RenderRequest(target, RenderAudience.Learner), CancellationToken.None)
            .GetAwaiter().GetResult().Content.ToArray();

    /// <summary>Writes the print view to a temp file and opens it in the default browser; printing happens there at 100 percent scale.</summary>
    public static void OpenPrintView(ApprovedArtifact artifact, string name)
    {
        var directory = Path.Combine(Path.GetTempPath(), EngineIdentity.InternalId, "print-view");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{name}.print.html");
        File.WriteAllBytes(path, Render(artifact, RenderTarget.PrintHtml));

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
    }

    /// <summary>Saves to the teacher's Documents-folder project library; returns the project name used.</summary>
    public static string SaveToLibrary(ApprovedArtifact artifact, string hintPrefix, string moduleId, string recipeId, string recipeVersion, IAssetCatalog catalog)
    {
        var library = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), EngineIdentity.InternalId, "projects");
        var store = new OcfprojProjectStore(library, new AccessibleHtmlRenderer(), catalog);

        var hint = UiStrings.Format("{0}-{1}", hintPrefix,
            DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture));
        store.SaveGreenProjectAsync(
            artifact,
            new ProjectSaveRequest(hint, moduleId, recipeId, recipeVersion, DateTimeOffset.UtcNow),
            CancellationToken.None).GetAwaiter().GetResult();
        return hint;
    }

    /// <summary>For artifacts that reference no assets; the store never consults it.</summary>
    public sealed class NoAssetsCatalog : IAssetCatalog
    {
        public IReadOnlyList<AssetProvenance> All => [];

        public AssetProvenance? Find(AssetId id) => null;

        public bool TryGetContent(AssetId id, out ReadOnlyMemory<byte> content, out string mimeType)
        {
            content = default;
            mimeType = "";
            return false;
        }
    }
}
