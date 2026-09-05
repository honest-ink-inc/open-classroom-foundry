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
/// Synthetic fault injection and real disposable-library recovery through the
/// existing Save buttons. These are UI contracts, not teacher or AT evidence.
/// </summary>
[Collection(ProjectLibraryRootTestGroup.Name)]
public sealed class LibrarySaveRecoveryTests : IDisposable
{
    private readonly string _originalLibraryRoot = AppServices.LibraryRoot;
    private readonly string _temporaryRoot = Path.Combine(
        Path.GetTempPath(),
        "ocf-save-recovery-tests",
        Guid.NewGuid().ToString("N"));

    public LibrarySaveRecoveryTests()
    {
        Directory.CreateDirectory(_temporaryRoot);
        AppServices.LibraryRoot = Path.Combine(_temporaryRoot, "projects");
    }

    public void Dispose()
    {
        AppServices.LibraryRoot = _originalLibraryRoot;
        Directory.Delete(_temporaryRoot, recursive: true);
    }

    [Theory]
    [InlineData("press", "io")]
    [InlineData("press", "access")]
    [InlineData("press", "cancel")]
    [InlineData("studio", "io")]
    [InlineData("studio", "access")]
    [InlineData("studio", "cancel")]
    [InlineData("sequence", "io")]
    [InlineData("sequence", "access")]
    [InlineData("sequence", "cancel")]
    public void An_expected_save_failure_keeps_exact_approval_and_allows_a_real_retry(
        string surfaceKind,
        string failureKind)
        => Sta.Run(() =>
        {
            var calls = new List<SaveCall>();
            string Save(
                ApprovedArtifact artifact,
                string hint,
                string moduleId,
                string recipeId,
                string recipeVersion,
                IAssetCatalog catalog,
                ProjectValidationEnvelope? validation,
                ProjectRenderProfile? renderProfile)
            {
                calls.Add(new SaveCall(
                    artifact,
                    hint,
                    moduleId,
                    recipeId,
                    recipeVersion,
                    catalog,
                    validation,
                    renderProfile));
                if (calls.Count == 1)
                {
                    throw ExpectedFailure(failureKind);
                }

                return AppServices.SaveToLibrary(
                    artifact,
                    hint,
                    moduleId,
                    recipeId,
                    recipeVersion,
                    catalog,
                    validation,
                    renderProfile);
            }

            using var surface = CreateSurface(surfaceKind, Save);
            Approve(surface);
            var approved = Assert.IsType<ApprovedArtifact>(surface.Approval());
            var save = SaveButton(surface.Form);

            Assert.Null(Record.Exception(save.PerformClick));

            Assert.Same(approved, surface.Approval());
            Assert.Single(calls);
            Assert.Same(approved, calls[0].Artifact);
            Assert.False(Directory.Exists(AppServices.LibraryRoot));
            Assert.True(save.Enabled);
            Assert.True(ReviewSurfaceContractTests.ByName(surface.Form, "Export…").Enabled);
            var expectedStatus = UiStrings.WithoutMnemonic(failureKind == "cancel"
                ? UiStrings.StatusSaveCancelled
                : UiStrings.StatusSaveFailed);
            Assert.Equal(expectedStatus, surface.Status());
            Assert.Contains(
                ReviewSurfaceContractTests.Flatten(surface.Form).OfType<Label>(),
                label => label.AccessibilityObject.Name == expectedStatus);
            Assert.DoesNotContain("synthetic-private-detail", surface.Status(), StringComparison.Ordinal);

            save.PerformClick();

            Assert.Equal(2, calls.Count);
            // Production reopens and verifies a fresh catalog on each save in
            // two surfaces. Every other binding must remain the exact context;
            // the real store checks the reviewed asset bytes independently.
            Assert.Equal(calls[0] with { Catalog = calls[1].Catalog }, calls[1]);
            Assert.Same(approved, surface.Approval());
            Assert.StartsWith("Saved to the library as ", surface.Status(), StringComparison.Ordinal);
            AssertSavedDocumentIsExact(approved);
        });

    [Theory]
    [InlineData("press")]
    [InlineData("studio")]
    [InlineData("sequence")]
    public void A_real_unavailable_library_keeps_the_approval_and_can_be_retried(string surfaceKind)
        => Sta.Run(() =>
        {
            var obstructionPath = AppServices.LibraryRoot;
            File.WriteAllText(obstructionPath, "Synthetic root obstruction.");
            using var surface = CreateSurface(surfaceKind);
            Approve(surface);
            var approved = Assert.IsType<ApprovedArtifact>(surface.Approval());
            var save = SaveButton(surface.Form);

            Assert.Null(Record.Exception(save.PerformClick));

            Assert.Same(approved, surface.Approval());
            Assert.Equal("Synthetic root obstruction.", File.ReadAllText(obstructionPath));
            Assert.Equal(UiStrings.WithoutMnemonic(UiStrings.StatusSaveFailed), surface.Status());
            Assert.DoesNotContain(obstructionPath, surface.Status(), StringComparison.OrdinalIgnoreCase);
            Assert.True(save.Enabled);

            File.Delete(obstructionPath);
            Directory.CreateDirectory(obstructionPath);
            save.PerformClick();

            Assert.Same(approved, surface.Approval());
            Assert.StartsWith("Saved to the library as ", surface.Status(), StringComparison.Ordinal);
            AssertSavedDocumentIsExact(approved);
        });

    [Theory]
    [InlineData("press")]
    [InlineData("studio")]
    [InlineData("sequence")]
    public void A_programming_error_is_not_misreported_as_a_recoverable_save_failure(string surfaceKind)
        => Sta.Run(() =>
        {
            var programmingError = new InvalidOperationException("Synthetic programming failure.");
            using var surface = CreateSurface(
                surfaceKind,
                (_, _, _, _, _, _, _, _) => throw programmingError);
            Approve(surface);
            var previousStatus = surface.Status();

            Assert.Same(programmingError, Record.Exception(SaveButton(surface.Form).PerformClick));
            Assert.Equal(previousStatus, surface.Status());
        });

    [Theory]
    [InlineData("press")]
    [InlineData("studio")]
    [InlineData("sequence")]
    public void A_save_without_approval_never_invokes_the_injected_operation(string surfaceKind)
        => Sta.Run(() =>
        {
            var calls = 0;
            using var surface = CreateSurface(surfaceKind, (_, _, _, _, _, _, _, _) =>
            {
                calls++;
                return "must-not-exist";
            });

            SaveButton(surface.Form).PerformClick();

            Assert.Null(surface.Approval());
            Assert.Equal(0, calls);
            Assert.False(Directory.Exists(AppServices.LibraryRoot));
        });

    [Theory]
    [InlineData("press")]
    [InlineData("studio")]
    [InlineData("sequence")]
    public void An_Amber_artifact_never_reaches_the_injected_save_operation(string surfaceKind)
        => Sta.Run(() =>
        {
            var calls = 0;
            using var surface = CreateSurface(surfaceKind, (_, _, _, _, _, _, _, _) =>
            {
                calls++;
                return "must-not-exist";
            });
            Approve(surface);
            var amber = ApprovalGate.Approve(
                DraftArtifact.New(
                    new ArtifactDocument([new Paragraph("Synthetic Amber save refusal.")]),
                    DataLane.Amber),
                "Synthetic test teacher",
                [],
                DateTimeOffset.UnixEpoch);
            var approvedProperty = surface.Form.GetType().GetProperty("ApprovedResult")
                ?? throw new InvalidOperationException("The audited approval property was not found.");
            // Adversarial fixture only: a future UI state bug must not turn an
            // injected save seam into authority for a non-Green artifact.
            approvedProperty.SetValue(surface.Form, amber);

            var failure = Assert.Throws<InvalidOperationException>(SaveButton(surface.Form).PerformClick);

            Assert.Contains("Only Green-lane products", failure.Message, StringComparison.Ordinal);
            Assert.Equal(0, calls);
            Assert.False(Directory.Exists(AppServices.LibraryRoot));
        });

    private static Exception ExpectedFailure(string kind)
        => kind switch
        {
            "io" => new IOException("synthetic-private-detail: unavailable destination"),
            "access" => new UnauthorizedAccessException("synthetic-private-detail: denied destination"),
            "cancel" => new OperationCanceledException("synthetic-private-detail: cancelled save"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    private static Surface CreateSurface(string kind, ProjectLibrarySaveOperation? save = null)
    {
        Surface surface;
        switch (kind)
        {
            case "press":
                var press = new PressRoomForm(Review, librarySaver: save);
                surface = new Surface(press, () => press.ApprovedResult, () => press.StatusText);
                press.Show();
                ((ListBox)ReviewSurfaceContractTests.ByName(press, "Presses")).SelectedIndex =
                    PressRoomCatalog.All.ToList().FindIndex(definition => definition.Id == "tangram");
                break;
            case "studio":
                var studio = new ModuleStudioForm(Review, librarySaver: save);
                surface = new Surface(studio, () => studio.ApprovedResult, () => studio.StatusText);
                studio.Show();
                var door = ModuleStudioCatalog.All.Single(candidate =>
                    candidate.Modes.Any(mode => mode.Key == "scaffold-smith.packet"));
                ((ListBox)ReviewSurfaceContractTests.ByName(studio, "Module doors")).SelectedIndex =
                    ModuleStudioCatalog.All.ToList().IndexOf(door);
                ReviewSurfaceContractTests.Flatten(studio).OfType<ComboBox>()
                    .Single(control => control.AccessibilityObject.Name == "Studio mode").SelectedIndex =
                    door.Modes.ToList().FindIndex(mode => mode.Key == "scaffold-smith.packet");
                break;
            case "sequence":
                var sequence = new AllAboardForm(AppServices.SymbolCatalog(), Review, librarySaver: save);
                surface = new Surface(sequence, () => sequence.ApprovedResult, () => sequence.StatusText);
                sequence.Show();
                Input(sequence, "Task title").Text = "Synthetic paper preparation";
                Input(sequence, "Step 1 text").Text = "Place the synthetic paper.";
                Input(sequence, "Step 2 text").Text = "Read the synthetic paper.";
                Input(sequence, "Step 3 text").Text = "Return the synthetic paper.";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }

        return surface;
    }

    private static TextBox Input(Form form, string accessibleName)
        => ReviewSurfaceContractTests.Flatten(form).OfType<TextBox>()
            .Single(control => control.AccessibilityObject.Name == accessibleName);

    private static void Approve(Surface surface)
    {
        ((Button)ReviewSurfaceContractTests.ByName(surface.Form, "Review and approve…")).PerformClick();
        Assert.NotNull(surface.Approval());
    }

    private static ApprovedArtifact? Review(ReviewSession session)
    {
        session.SetRequiredIssuesAcknowledged(acknowledged: true);
        return session.CanApprove
            ? session.Approve("Synthetic test teacher", DateTimeOffset.UnixEpoch)
            : null;
    }

    private static Button SaveButton(Form form)
        => (Button)ReviewSurfaceContractTests.ByName(form, "Save to library");

    private static void AssertSavedDocumentIsExact(ApprovedArtifact approved)
    {
        var path = Assert.Single(Directory.EnumerateFiles(AppServices.LibraryRoot, "*.ocfproj"));
        var loaded = OcfprojProjectStore.LoadProjectFileAsync(path, CancellationToken.None)
            .GetAwaiter().GetResult();
        Assert.Equal(
            ArtifactDocumentFingerprint.Compute(approved.Revision.Document),
            ArtifactDocumentFingerprint.Compute(loaded.Document));
        Assert.Empty(Directory.EnumerateFiles(AppServices.LibraryRoot, "*.stage"));
    }

    private sealed record Surface(
        Form Form,
        Func<ApprovedArtifact?> Approval,
        Func<string> Status) : IDisposable
    {
        public void Dispose() => Form.Dispose();
    }

    private sealed record SaveCall(
        ApprovedArtifact Artifact,
        string Hint,
        string ModuleId,
        string RecipeId,
        string RecipeVersion,
        IAssetCatalog Catalog,
        ProjectValidationEnvelope? Validation,
        ProjectRenderProfile? RenderProfile);
}
