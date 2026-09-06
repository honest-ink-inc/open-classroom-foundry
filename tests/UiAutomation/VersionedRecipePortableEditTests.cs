// SPDX-License-Identifier: GPL-3.0-or-later
using System.IO;
using Foundry.App.WinForms;
using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.BuiltIn;
using Foundry.Modules.DeterministicPress;
using Foundry.Storage;

namespace Foundry.Tests.UiAutomation;

/// <summary>
/// Current-writer synthetic packages through the real library door, with an
/// injected typed reviewer. This is neither original-C1 execution nor a human,
/// browser, assistive-technology, physical-print or schema-admission finding.
/// </summary>
[Collection(ProjectLibraryRootTestGroup.Name)]
public sealed class VersionedRecipePortableEditTests : IDisposable
{
    private const string Reviewer = "synthetic-portable-edit@example.invalid";
    private const string EditedText = "Synthetic portable edit.";
    private static readonly DateTimeOffset Instant = new(2026, 9, 6, 0, 0, 0, TimeSpan.Zero);
    private readonly string _originalRoot = AppServices.LibraryRoot;
    private readonly string _root = Directory.CreateTempSubdirectory("ocf-versioned-portable-edit-").FullName;

    public VersionedRecipePortableEditTests()
    {
        AppServices.LibraryRoot = _root;
    }

    [Theory]
    [InlineData("lesson-loom", "lesson-loom", "0.1.0")]
    [InlineData("lesson-loom", "lesson-loom", "0.2.0")]
    [InlineData("board-to-brief", "board-to-brief", "0.1.0")]
    [InlineData("board-to-brief", "board-to-brief", "0.2.0")]
    [InlineData("source-lens", "source-lens", "0.1.0")]
    [InlineData("source-lens", "source-lens", "0.2.0")]
    [InlineData("press.charts", "bar-chart", "0.1.0")]
    [InlineData("press.charts", "bar-chart", "0.2.0")]
    [InlineData("press.learner-held", "goal-post", "0.1.0")]
    [InlineData("press.learner-held", "goal-post", "0.2.0")]
    [InlineData("press.learner-held", "portfolio-passport", "0.1.0")]
    [InlineData("press.learner-held", "portfolio-passport", "0.2.0")]
    [InlineData("press.learner-held", "strategy-shelf", "0.1.0")]
    [InlineData("press.learner-held", "strategy-shelf", "0.2.0")]
    [InlineData("press.calibration", "calibration-proof", "0.1.0")]
    [InlineData("press.calibration", "calibration-proof", "0.2.0")]
    [InlineData("press.flashcards", "flashcards", "0.1.0")]
    [InlineData("press.flashcards", "flashcards", "0.2.0")]
    public void Exact_recipe_package_requires_fresh_portable_edit_review_and_preserves_the_source(
        string recipeId, string definitionId, string version)
        => Sta.Run(() =>
        {
            var (ModuleId, Session) = BuildSelected(recipeId, definitionId, version);
            var originalApproval = ApproveFresh(Session);
            var hint = AppServices.SaveToLibrary(
                originalApproval, "synthetic-source", ModuleId, recipeId, version,
                new AppServices.NoAssetsCatalog());
            var sourcePath = Path.Combine(_root, hint + OcfprojProjectStore.Extension);
            var sourceBytes = File.ReadAllBytes(sourcePath);
            var source = AppServices.OpenFromLibrary(sourcePath);
            Assert.Equal(recipeId, source.Manifest.RecipeId);
            Assert.Equal(version, source.Manifest.RecipeVersion);
            Assert.Equal("0.8.0-alpha", source.Manifest.EngineVersion);
            Assert.Equal(ArtifactDocumentFingerprint.Compute(originalApproval.Revision.Document),
                ArtifactDocumentFingerprint.Compute(source.Document));
            Assert.NotNull(source.Validation);
            Assert.NotNull(source.RenderProfile);

            var allowPreflight = false;
            var preflightCalls = 0;
            var reviewCalls = 0;
            ArtifactDocument? editedDocument = null;
            PressRoomForm? active = null;
            using var form = new PressRoomForm(
                session =>
                {
                    reviewCalls++;
                    Assert.Equal(1, reviewCalls);
                    Assert.False(SaveButton(active!).Enabled);
                    Assert.Null(active!.ApprovedResult);
                    Assert.Equal(DataLane.Green, session.Draft.Revision.Lane);
                    Assert.Equal(ArtifactPurpose.Unknown, session.Draft.Revision.Purpose);
                    Assert.NotEqual(originalApproval.Revision.Id, session.Draft.Revision.Id);
                    Assert.Equal(ArtifactDocumentFingerprint.Compute(source.Document),
                        ArtifactDocumentFingerprint.Compute(session.Draft.Revision.Document));
                    Assert.Contains(session.RequiredAcknowledgements, issue =>
                        issue.Code == "project.origin-unverified" && issue.RequiresAcknowledgement);
                    Assert.DoesNotContain(session.Issues, issue => issue.Code == "project.saved-revision-changed");
                    Assert.False(session.CanApprove);
                    Assert.Throws<InvalidOperationException>(() => session.Approve(Reviewer, Instant));
                    session.SetRequiredIssuesAcknowledged(true);
                    Assert.True(session.CanApprove);

                    var oldRevision = session.Draft.Revision;
                    var expectedNodes = oldRevision.Document.Nodes.ToArray();
                    var editIndex = Array.FindIndex(expectedNodes, node => node is Heading or VectorGraphic);
                    Assert.True(editIndex >= 0);
                    expectedNodes[editIndex] = EditSelectedNode(expectedNodes[editIndex]);
                    var expectedEdit = new ArtifactDocument(expectedNodes, oldRevision.Document.Language);
                    session.ReplaceNode(editIndex, expectedNodes[editIndex]);
                    editedDocument = session.Draft.Revision.Document;
                    Assert.Equal(oldRevision.Number + 1, session.Draft.Revision.Number);
                    Assert.NotEqual(ArtifactDocumentFingerprint.Compute(oldRevision.Document),
                        ArtifactDocumentFingerprint.Compute(editedDocument));
                    Assert.Equal(oldRevision.Document.Nodes.Count, editedDocument.Nodes.Count);
                    // Nested collections are freshly frozen, so compare exact
                    // serialized values rather than record/reference equality.
                    Assert.Equal(ArtifactDocumentFingerprint.Compute(expectedEdit),
                        ArtifactDocumentFingerprint.Compute(editedDocument));
                    Assert.Equal(oldRevision.Document.Language, editedDocument.Language);
                    Assert.Contains(session.RequiredAcknowledgements, issue =>
                        issue.Code == "project.saved-revision-changed" && issue.RequiresAcknowledgement);
                    Assert.False(session.CanApprove);
                    Assert.Null(session.ApprovedResult);
                    Assert.Throws<InvalidOperationException>(() => session.Approve(Reviewer, Instant));
                    var approval = ApproveFresh(session);
                    Assert.True(AppServices.IsExactApproval(session, approval));
                    return approval;
                },
                () => sourcePath,
                loadedProjectPreflight: loaded =>
                {
                    preflightCalls++;
                    Assert.Equal(recipeId, loaded.Manifest.RecipeId);
                    Assert.Equal(version, loaded.Manifest.RecipeVersion);
                    return allowPreflight ? ConfirmSynthetic(loaded) : null;
                });
            active = form;
            form.Show();

            // New authoring confirmation cannot certify an imported package,
            // and its absence cannot replace or block that package's own preflight.
            Assert.Single(ReviewSurfaceContractTests.Flatten(form).OfType<CheckBox>(),
                control => control.Name == "press-green-input").Checked = false;

            // A stored Green label and old approval do not replace fresh preflight.
            form.OpenFromLibrary();
            Assert.Equal(1, preflightCalls);
            Assert.Equal(0, reviewCalls);
            Assert.Null(form.ApprovedResult);
            Assert.False(SaveButton(form).Enabled);
            Assert.False(ReviewSurfaceContractTests.ByName(form, "Print").Enabled);
            Assert.False(ReviewSurfaceContractTests.ByName(form, "Open print view").Enabled);
            Assert.False(ReviewSurfaceContractTests.ByName(form, "Export…").Enabled);
            Assert.Single(Directory.GetFiles(_root, "*.ocfproj"));
            Assert.Equal(sourceBytes, File.ReadAllBytes(sourcePath));

            allowPreflight = true;
            form.OpenFromLibrary();
            Assert.Equal(2, preflightCalls);
            Assert.Equal(1, reviewCalls);
            Assert.NotNull(editedDocument);
            Assert.NotNull(form.ApprovedResult);
            Assert.NotSame(originalApproval, form.ApprovedResult);
            Assert.Same(editedDocument, form.ApprovedResult.Revision.Document);
            Assert.Equal(ArtifactPurpose.Unknown, form.ApprovedResult.Revision.Purpose);
            Assert.True(SaveButton(form).Enabled);
            SaveButton(form).PerformClick();

            var packages = Directory.GetFiles(_root, "*.ocfproj");
            Assert.Equal(2, packages.Length);
            Assert.Equal(sourceBytes, File.ReadAllBytes(sourcePath));
            var editedPath = Assert.Single(packages, path => !string.Equals(path, sourcePath, StringComparison.Ordinal));
            var editedBytes = File.ReadAllBytes(editedPath);
            var portable = AppServices.OpenFromLibrary(editedPath);
            Assert.Equal(AppServices.PortableProjectModuleId, portable.Manifest.ModuleId);
            Assert.Equal(AppServices.PortableProjectRecipeId, portable.Manifest.RecipeId);
            Assert.Equal(AppServices.PortableProjectRecipeVersion, portable.Manifest.RecipeVersion);
            Assert.NotEqual(recipeId, portable.Manifest.RecipeId);
            Assert.Equal("0.8.0-alpha", portable.Manifest.EngineVersion);
            Assert.Equal("1", portable.Manifest.SchemaVersion);
            Assert.Equal(DataLane.Green, portable.Manifest.DataLane);
            Assert.Equal(ArtifactPurpose.Unknown, portable.Manifest.Purpose);
            Assert.Equal(ArtifactDocumentFingerprint.Compute(editedDocument),
                ArtifactDocumentFingerprint.Compute(portable.Document));
            Assert.Equal(DocumentText.CollectStrings(editedDocument), DocumentText.CollectStrings(portable.Document));
            Assert.NotNull(portable.Validation);
            Assert.Equal(AppServices.PortableProjectRecipeId, portable.Validation.RecipeId);
            Assert.Equal(AppServices.PortableProjectRecipeVersion, portable.Validation.RecipeVersion);
            Assert.Contains("project.saved-revision-changed", portable.Validation.UntrustedNoticeCodes);
            Assert.Empty(portable.Manifest.AssetIds);

            // Even the newly saved exact edit reopens without the prior capability.
            Assert.Throws<InvalidOperationException>(() => AppServices.SessionOverLoadedProject(portable));
            var nextReview = AppServices.SessionOverLoadedProject(portable, ConfirmSynthetic(portable));
            Assert.NotEqual(form.ApprovedResult.Revision.Id, nextReview.Draft.Revision.Id);
            Assert.Null(nextReview.ApprovedResult);
            Assert.False(nextReview.CanApprove);
            Assert.Contains(nextReview.RequiredAcknowledgements, issue => issue.Code == "project.origin-unverified");
            Assert.DoesNotContain(nextReview.Issues, issue => issue.Code == "project.saved-revision-changed");
            Assert.Throws<InvalidOperationException>(() => nextReview.Approve(Reviewer, Instant));
            nextReview.Cancel();
            Assert.Equal(editedBytes, File.ReadAllBytes(editedPath));
            Assert.Equal(sourceBytes, File.ReadAllBytes(sourcePath));
        });

    private static (string ModuleId, ReviewSession Session) BuildSelected(
        string recipeId, string definitionId, string version)
    {
        if (!recipeId.StartsWith("press.", StringComparison.Ordinal))
        {
            var selected = ModuleStudioCatalog.ByModeKey(definitionId, version);
            var values = ModuleStudioCatalog.Defaults(ModuleStudioCatalog.ByModeKey(definitionId, "0.1.0"));
            var outcome = selected.Build!(new ModuleInputValues(values));
            Assert.Equal(recipeId, outcome.Recipe.Id);
            Assert.Equal(version, outcome.Recipe.Version);
            return (recipeId, AppServices.SessionOver(outcome.CreateDraft(), outcome.Validator));
        }

        var press = PressRoomCatalog.ById(definitionId, version);
        var inputs = PressRoomCatalog.Defaults(PressRoomCatalog.ById(definitionId, "0.1.0"));
        var built = press.BuildForReview(new PressInputs(inputs));
        Assert.Equal(recipeId, press.Recipe.Id);
        Assert.Equal(version, press.Recipe.Version);
        return ("deterministic-press", AppServices.SessionOverRecipe(
            DraftArtifact.New(built.Document, DataLane.Green),
            new ReviewNoticeValidator(new DefaultArtifactValidator(), built.Issues), press.Recipe));
    }

    private static DocumentNode EditSelectedNode(DocumentNode node)
    {
        if (node is Heading heading)
        {
            return heading with { Text = EditedText };
        }

        var page = Assert.IsType<VectorGraphic>(node);
        var primitives = page.Primitives.ToArray();
        var labelIndex = Array.FindIndex(primitives, primitive => primitive is TextLabel);
        Assert.True(labelIndex >= 0);
        primitives[labelIndex] = Assert.IsType<TextLabel>(primitives[labelIndex]) with { Text = EditedText };
        return page with { Primitives = Array.AsReadOnly(primitives) };
    }

    private static ApprovedArtifact ApproveFresh(ReviewSession session)
    {
        Assert.Null(session.ApprovedResult);
        Assert.NotEmpty(session.RequiredAcknowledgements);
        Assert.False(session.CanApprove);
        Assert.DoesNotContain(session.Issues, issue => issue.Severity == ValidationSeverity.Blocking);
        Assert.Throws<InvalidOperationException>(() => session.Approve(Reviewer, Instant));
        session.SetRequiredIssuesAcknowledged(true);
        Assert.True(session.CanApprove);
        return session.Approve(Reviewer, Instant);
    }

    private static LoadedProjectGreenConfirmation ConfirmSynthetic(LoadedProject loaded)
        => AppServices.ConfirmLoadedProjectGreen(loaded,
            new LoadedProjectGreenChecklist(true, true, true));

    private static Button SaveButton(Form form)
        => Assert.IsType<Button>(ReviewSurfaceContractTests.ByName(form, UiStrings.WithoutMnemonic(UiStrings.SaveToLibrary)));

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        AppServices.LibraryRoot = _originalRoot;
        try
        {
            // Only this instance's unique disposable library is owned.
            Directory.Delete(_root, recursive: true);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            // Cleanup must not replace an assertion or a package failure.
        }
    }
}
