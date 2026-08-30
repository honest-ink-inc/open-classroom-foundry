// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Rendering;
using Foundry.Storage;

namespace Foundry.App.WinForms;

/// <summary>
/// The teacher's three-part Green classification for an otherwise unknown
/// reopened package. Every statement is about content; none is a purpose,
/// recipe, governance, or protected-seat assertion.
/// </summary>
public sealed record LoadedProjectGreenChecklist(
    bool IsGreenQualifyingContent,
    bool HasNoLearnerLinkedContent,
    bool HasNoRestrictedContent);

/// <summary>
/// Ephemeral proof that the teacher classified one exact loaded document as
/// Green. Its constructor is private, and both object identity and semantic
/// digest are rechecked at the Gate B boundary so reuse or substitution fails.
/// </summary>
public sealed class LoadedProjectGreenConfirmation
{
    private readonly LoadedProject _loaded;
    private readonly string _artifactSha256;

    private LoadedProjectGreenConfirmation(LoadedProject loaded)
    {
        _loaded = loaded;
        _artifactSha256 = ArtifactDocumentFingerprint.Compute(loaded.Document);
    }

    internal static LoadedProjectGreenConfirmation Create(LoadedProject loaded) => new(loaded);

    internal bool AppliesTo(LoadedProject loaded)
        => ReferenceEquals(_loaded, loaded)
            && string.Equals(
                _artifactSha256,
                ArtifactDocumentFingerprint.Compute(loaded.Document),
                StringComparison.Ordinal);
}

/// <summary>
/// The small shared machinery the authoring surfaces stand on: the shipped
/// symbol catalog, the post-approval outputs, and the review-session plumbing.
/// Everything here accepts only an ApprovedArtifact where output is concerned —
/// the structural gate is not re-negotiated per surface (ADR-004).
/// </summary>
public static class AppServices
{
    /// <summary>
    /// Engine-owned identity used when a mutable package is re-reviewed or
    /// edited. Package module/recipe selectors are never copied into a new
    /// manifest as though they were authenticated provenance.
    /// </summary>
    public const string PortableProjectModuleId = "portable-semantic-document";

    public const string PortableProjectRecipeId = "portable-semantic-editor";

    public const string PortableProjectRecipeVersion = "1.0.0";

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

    /// <summary>
    /// Starts Gate B over an explicitly-laned draft with the validator that
    /// owns that recipe's invariants. Callers cannot accidentally turn Amber
    /// into Green or discard a module validator at the UI boundary.
    /// </summary>
    public static ReviewSession SessionOver(
        DraftArtifact draft,
        IArtifactValidator validator,
        ReviewViewContext? viewContext = null)
        => new(
            draft,
            MachineAtReview(),
            validator,
            viewContext ?? ReviewViewContext.ManualDefault);

    /// <summary>The established Green/manual path used by Module Zero and All Aboard.</summary>
    public static ReviewSession SessionOverGreen(
        ArtifactDocument document,
        ReviewViewContext? viewContext = null)
        => SessionOver(
            DraftArtifact.New(document, DataLane.Green),
            new DefaultArtifactValidator(),
            viewContext);

    /// <summary>
    /// Trusted-test compatibility for exercising legacy persisted purpose
    /// declarations. The shipped public host has no raw-purpose entry point.
    /// </summary>
    internal static ReviewSession SessionOverGreen(
        ArtifactDocument document,
        ArtifactPurpose purpose,
        ReviewViewContext? viewContext = null)
        => SessionOver(
            DraftArtifact.New(document, DataLane.Green, purpose),
            new DefaultArtifactValidator(),
            viewContext);

    public static ReviewSession SessionOverRecipe(
        DraftArtifact draft,
        IArtifactValidator validator,
        RecipeManifest recipe,
        IReadOnlyList<string>? transformationReport = null,
        ReviewViewContext? viewContext = null)
        => SessionOver(
            draft,
            new ReviewNoticeValidator(
                validator,
                ReviewNoticeValidator.RequiredRecipeWarnings(recipe, transformationReport)),
            viewContext);

    /// <summary>A review runner may return approval only for the session's exact current revision.</summary>
    public static bool IsExactApproval(ReviewSession session, ApprovedArtifact approved)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(approved);
        var revision = session.Draft.Revision;
        return session.Machine.State == JobState.Approved
            && ReferenceEquals(session.ApprovedResult, approved)
            && ReferenceEquals(approved.Revision, revision)
            && approved.Receipt.ArtifactId == revision.Id
            && approved.Receipt.RevisionNumber == revision.Number;
    }

    public static byte[] Render(ApprovedArtifact artifact, RenderTarget target)
        => Render(artifact, new RenderRequest(target));

    public static byte[] Render(ApprovedArtifact artifact, RenderRequest request)
        => new AccessibleHtmlRenderer().RenderAsync(
                artifact, request, CancellationToken.None)
            .GetAwaiter().GetResult().Content.ToArray();

    /// <summary>
    /// Writes the print view to a temporary file and opens it in the default
    /// browser. The teacher still verifies paper size and 100-percent scale in
    /// the browser/printer dialog; opening a view cannot enforce driver state.
    /// </summary>
    public static void OpenPrintView(
        ApprovedArtifact artifact,
        string name,
        RenderAudience audience = RenderAudience.Learner,
        double textScalePercent = 100,
        bool targetLanguageFirst = false)
    {
        var directory = Path.Combine(Path.GetTempPath(), EngineIdentity.InternalId, "print-view");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{name}.print.html");
        File.WriteAllBytes(
            path,
            Render(
                artifact,
                new RenderRequest(RenderTarget.PrintHtml, audience, textScalePercent, targetLanguageFirst)));

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
    }

    /// <summary>
    /// The teacher's project library root. The ordinary default is itself
    /// version-addressed; a managed deployment may replace it only through the
    /// validated production switch before a form opens. The internal setter is
    /// retained solely for disposable UI test libraries.
    /// </summary>
    internal static string DefaultLibraryRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        EngineIdentity.InternalId,
        EngineIdentity.EngineVersion,
        "projects");

    public static string LibraryRoot { get; internal set; } = DefaultLibraryRoot;

    /// <summary>Saves to the teacher's project library; returns the project name used.</summary>
    public static string SaveToLibrary(
        ApprovedArtifact artifact,
        string hintPrefix,
        string moduleId,
        string recipeId,
        string recipeVersion,
        IAssetCatalog catalog,
        ProjectValidationEnvelope? validation = null,
        ProjectRenderProfile? renderProfile = null)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        validation ??= ProjectValidationEnvelope.Exact(artifact, recipeId, recipeVersion);
        renderProfile ??= ProjectRenderProfile.For(artifact);
        var documentHash = ArtifactDocumentFingerprint.Compute(artifact.Revision.Document);
        if (!string.Equals(validation.RecipeId, recipeId, StringComparison.Ordinal)
            || !string.Equals(validation.RecipeVersion, recipeVersion, StringComparison.Ordinal)
            || validation.Lane != artifact.Revision.Lane
            || validation.Purpose != artifact.Revision.Purpose
            || !string.Equals(validation.ArtifactSha256, documentHash, StringComparison.Ordinal)
            || !string.Equals(renderProfile.ArtifactSha256, documentHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                UiStrings.WithoutMnemonic(UiStrings.SavedArtifactContextMismatch));
        }

        var store = new OcfprojProjectStore(LibraryRoot, new AccessibleHtmlRenderer(), catalog);

        var hint = UiStrings.Format("{0}-{1}", hintPrefix,
            DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture));
        store.SaveGreenProjectAsync(
            artifact,
            new ProjectSaveRequest(
                hint,
                moduleId,
                recipeId,
                recipeVersion,
                DateTimeOffset.UtcNow,
                validation,
                renderProfile),
            CancellationToken.None).GetAwaiter().GetResult();
        return hint;
    }

    /// <summary>Reopens a saved Green project through the hardened reader; reversibility (constitution 11) as one call.</summary>
    public static LoadedProject OpenFromLibrary(string path)
    {
        var resolved = ProjectLibraryRootConfiguration.ResolveProjectFileInsideConfiguredRoot(path);
        return OcfprojProjectStore.LoadProjectFileAsync(resolved, CancellationToken.None)
            .GetAwaiter().GetResult();
    }

    /// <summary>
    /// Refuses the legacy call shape that omitted a teacher lane preflight.
    /// Unknown package content defaults to Amber and cannot enter early-release
    /// Gate B or its Green-only sinks through an implicit overload.
    /// </summary>
    public static ReviewSession SessionOverLoadedProject(LoadedProject loaded)
        => throw new InvalidOperationException(
            UiStrings.WithoutMnemonic(UiStrings.StatusLoadedProjectPreflightNotConfirmed));

    /// <summary>
    /// Records a teacher's explicit content classification for this one loaded
    /// document. Unknown defaults to Amber, so an incomplete checklist cannot
    /// create the Green draft that the early-release review and sinks require.
    /// </summary>
    public static LoadedProjectGreenConfirmation ConfirmLoadedProjectGreen(
        LoadedProject loaded,
        LoadedProjectGreenChecklist checklist)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        ArgumentNullException.ThrowIfNull(checklist);
        if (!checklist.IsGreenQualifyingContent
            || !checklist.HasNoLearnerLinkedContent
            || !checklist.HasNoRestrictedContent)
        {
            throw new InvalidOperationException(
                UiStrings.WithoutMnemonic(UiStrings.LoadedProjectGreenChecklistIncomplete));
        }

        return LoadedProjectGreenConfirmation.Create(loaded);
    }

    /// <summary>
    /// Starts Gate B only after a teacher classified this exact package content
    /// as Green. Mutable package lane and purpose claims remain unauthenticated:
    /// purpose is always Unknown and the generic origin warning remains.
    /// </summary>
    public static ReviewSession SessionOverLoadedProject(
        LoadedProject loaded,
        LoadedProjectGreenConfirmation confirmation)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        ArgumentNullException.ThrowIfNull(confirmation);
        if (!confirmation.AppliesTo(loaded))
        {
            throw new InvalidOperationException(
                UiStrings.WithoutMnemonic(UiStrings.StatusLoadedProjectPreflightNotConfirmed));
        }

        if (loaded.Validation is null || loaded.RenderProfile is null)
        {
            throw new InvalidOperationException(
                UiStrings.WithoutMnemonic(UiStrings.SavedProjectNeedsManagedUpgrade));
        }

        var notices = ResolveTrustedSavedNotices();
        var validator = PersistedProjectValidator.Create(loaded.Validation, notices);
        return SessionOver(
            DraftArtifact.New(loaded.Document, DataLane.Green),
            validator);
    }

    private static ValidationIssue[] ResolveTrustedSavedNotices()
    {
        // No field inside a mutable ZIP can prove which recipe, seat, or warning
        // produced it. Reopen therefore uses one engine-owned, provenance-free
        // warning independent of every package selector and inventory.
        return
        [
            ValidationIssue.Warning(
                "project.origin-unverified",
                UiStrings.WithoutMnemonic(UiStrings.SavedOriginUnverifiedWarning),
                requiresAcknowledgement: true),
        ];
    }

    /// <summary>Silent print through the same structural gate as every output; native vector PDF first, Edge fallback.</summary>
    public static void Print(
        ApprovedArtifact artifact,
        RenderAudience audience = RenderAudience.Learner,
        double textScalePercent = 100,
        bool targetLanguageFirst = false)
        => new Infrastructure.Windows.WindowsPdfPrinter(new AccessibleHtmlRenderer())
            .PrintAsync(
                artifact,
                new PrintRequest(
                    PrinterName: "",
                    Duplex: false,
                    Copies: 1,
                    Audience: audience,
                    TextScalePercent: textScalePercent,
                    TargetLanguageFirst: targetLanguageFirst),
                CancellationToken.None)
            .GetAwaiter().GetResult();

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
