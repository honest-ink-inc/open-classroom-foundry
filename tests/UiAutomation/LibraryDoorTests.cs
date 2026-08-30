// SPDX-License-Identifier: GPL-3.0-or-later
using System.IO;
using Foundry.App.WinForms;
using Foundry.Application;
using Foundry.Domain;
using Foundry.Storage;

namespace Foundry.Tests.UiAutomation;

// The library door and one-keystroke paper (third forge menu, items 1 and 2):
// reversibility exercised through the REAL surface — save, reopen through the
// hardened reader, re-review, re-approve — plus the wall-tile flow whose
// output passes Gate B itself, and the print gate.

[Collection(ProjectLibraryRootTestGroup.Name)]
public class LibraryDoorTests : IDisposable
{
    private readonly string _originalRoot = AppServices.LibraryRoot;
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "ocf-library-tests", Guid.NewGuid().ToString("N"));

    public LibraryDoorTests()
    {
        AppServices.LibraryRoot = _tempRoot;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        AppServices.LibraryRoot = _originalRoot;
        try
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }
        catch (IOException)
        {
            // Temp cleanup is best-effort.
        }
    }

    private static ApprovedArtifact? GateRespectingApprove(ReviewSession session)
    {
        session.SetRequiredIssuesAcknowledged(acknowledged: true);
        return session.CanApprove ? session.Approve(Environment.UserName, DateTimeOffset.UtcNow) : null;
    }

    [Fact]
    public void Default_and_managed_library_roots_are_version_addressed_and_exact()
    {
        Assert.Contains(
            EngineIdentity.EngineVersion,
            AppServices.DefaultLibraryRoot.Split(Path.DirectorySeparatorChar),
            StringComparer.Ordinal);

        var managedRoot = Path.Combine(_tempRoot, EngineIdentity.EngineVersion, "prepared-library");
        Directory.CreateDirectory(managedRoot);

        Assert.True(ProjectLibraryRootConfiguration.ApplyIfPresent(
            [ProjectLibraryRootConfiguration.Switch, managedRoot]));
        Assert.Equal(Path.GetFullPath(managedRoot), AppServices.LibraryRoot);

        var repeated = Assert.Throws<ProjectLibraryRootException>(() =>
            ProjectLibraryRootConfiguration.ApplyIfPresent(
            [
                ProjectLibraryRootConfiguration.Switch,
                managedRoot,
                ProjectLibraryRootConfiguration.Switch,
                managedRoot,
            ]));
        Assert.Equal(ProjectLibraryRootFailureCodes.SwitchInvalid, repeated.Code);
        Assert.DoesNotContain(managedRoot, repeated.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_library_root_rejects_relative_missing_unversioned_and_reparse_addresses()
    {
        var relative = Assert.Throws<ProjectLibraryRootException>(() =>
            ProjectLibraryRootConfiguration.ValidateProductionRoot("relative"));
        Assert.Equal(ProjectLibraryRootFailureCodes.RootInvalid, relative.Code);

        var missing = Assert.Throws<ProjectLibraryRootException>(() =>
            ProjectLibraryRootConfiguration.ValidateProductionRoot(
                Path.Combine(_tempRoot, EngineIdentity.EngineVersion, "missing")));
        Assert.Equal(ProjectLibraryRootFailureCodes.RootInvalid, missing.Code);

        var unversioned = Path.Combine(_tempRoot, "unversioned");
        Directory.CreateDirectory(unversioned);
        var version = Assert.Throws<ProjectLibraryRootException>(() =>
            ProjectLibraryRootConfiguration.ValidateProductionRoot(unversioned));
        Assert.Equal(ProjectLibraryRootFailureCodes.VersionSegmentMissing, version.Code);

        if (OperatingSystem.IsWindows())
        {
            var target = Path.Combine(_tempRoot, "link-target", EngineIdentity.EngineVersion);
            var link = Path.Combine(_tempRoot, "linked-root");
            Directory.CreateDirectory(target);
            Directory.CreateSymbolicLink(link, target);
            try
            {
                var reparse = Assert.Throws<ProjectLibraryRootException>(() =>
                    ProjectLibraryRootConfiguration.ValidateProductionRoot(link));
                Assert.Equal(ProjectLibraryRootFailureCodes.RootInvalid, reparse.Code);
                Assert.DoesNotContain(link, reparse.Message, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                Directory.Delete(link, recursive: false);
            }
        }
    }

    [Fact]
    public void Shipped_uia_harness_cannot_override_the_validated_library_root_from_the_keyboard()
    {
        var managedRoot = Path.Combine(_tempRoot, EngineIdentity.EngineVersion, "prepared-library");
        Directory.CreateDirectory(managedRoot);
        _ = ProjectLibraryRootConfiguration.ApplyIfPresent(
            [ProjectLibraryRootConfiguration.Switch, managedRoot]);
        var admitted = AppServices.LibraryRoot;

        var refusal = Assert.Throws<ProjectLibraryRootException>(() => UiaHarness.FromArgs(
        [
            UiaHarness.Switch,
            "pressroom",
            ProjectLibraryRootConfiguration.Switch,
            managedRoot,
            UiaHarness.LibraryRootSwitch,
            Path.Combine(_tempRoot, "arbitrary-unversioned-root"),
        ]));

        Assert.Equal(ProjectLibraryRootFailureCodes.SwitchInvalid, refusal.Code);
        Assert.Equal(admitted, AppServices.LibraryRoot);
        Assert.DoesNotContain(_tempRoot, refusal.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Shipped_uia_harness_refuses_a_real_versioned_library_and_admits_only_an_empty_disposable_rehearsal_root()
    {
        var realLibrary = Path.Combine(_tempRoot, EngineIdentity.EngineVersion, "teacher-library");
        Directory.CreateDirectory(realLibrary);
        _ = ProjectLibraryRootConfiguration.ApplyIfPresent(
            [ProjectLibraryRootConfiguration.Switch, realLibrary]);

        var refusal = Assert.Throws<ProjectLibraryRootException>(() => UiaHarness.FromArgs(
        [
            UiaHarness.Switch,
            "pressroom",
            ProjectLibraryRootConfiguration.Switch,
            realLibrary,
        ]));
        Assert.Equal(ProjectLibraryRootFailureCodes.RootInvalid, refusal.Code);
        Assert.DoesNotContain(realLibrary, refusal.Message, StringComparison.OrdinalIgnoreCase);

        var rehearsalRoot = Path.Combine(
            Path.GetTempPath(),
            "ocf-rehearsal-" + Guid.NewGuid().ToString("N"));
        var disposableLibrary = Path.Combine(
            rehearsalRoot,
            EngineIdentity.EngineVersion,
            "prepared-library");
        Directory.CreateDirectory(disposableLibrary);
        try
        {
            _ = ProjectLibraryRootConfiguration.ApplyIfPresent(
                [ProjectLibraryRootConfiguration.Switch, disposableLibrary]);
            using var harness = UiaHarness.FromArgs(
            [
                UiaHarness.Switch,
                "pressroom",
                ProjectLibraryRootConfiguration.Switch,
                disposableLibrary,
            ]);

            Assert.IsType<PressRoomForm>(harness);
            Assert.Equal(Path.GetFullPath(disposableLibrary), AppServices.LibraryRoot);
        }
        finally
        {
            Directory.Delete(rehearsalRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData("pressroom")]
    [InlineData("allaboard")]
    [InlineData("modules")]
    public void Storage_capable_uia_harness_modes_refuse_to_inherit_the_real_default_library(
        string harnessMode)
    {
        var realLibrary = Path.Combine(_tempRoot, EngineIdentity.EngineVersion, "teacher-library");
        Directory.CreateDirectory(realLibrary);
        AppServices.LibraryRoot = realLibrary;

        var refusal = Assert.Throws<ProjectLibraryRootException>(() => UiaHarness.FromArgs(
        [
            UiaHarness.Switch,
            harnessMode,
        ]));

        Assert.Equal(ProjectLibraryRootFailureCodes.RootInvalid, refusal.Code);
        Assert.Equal(realLibrary, AppServices.LibraryRoot);
        Assert.DoesNotContain(realLibrary, refusal.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Missing_unknown_and_repeated_uia_harness_modes_fail_closed_instead_of_opening_production()
    {
        foreach (var args in new[]
        {
            new[] { UiaHarness.Switch },
            [UiaHarness.Switch, "pressrom"],
            [UiaHarness.Switch, "review", UiaHarness.Switch, "capture"],
        })
        {
            var refusal = Assert.Throws<InvalidDataException>(() => UiaHarness.FromArgs(args));
            Assert.Equal(UiStrings.UiaHarnessSwitchInvalid, refusal.Message);
        }
    }

    [Fact]
    public void Uia_harness_export_is_confined_to_one_new_pdf_inside_the_disposable_rehearsal_root()
    {
        var rehearsalRoot = Path.Combine(
            Path.GetTempPath(),
            "ocf-rehearsal-" + Guid.NewGuid().ToString("N"));
        var disposableLibrary = Path.Combine(
            rehearsalRoot,
            EngineIdentity.EngineVersion,
            "prepared-library");
        Directory.CreateDirectory(disposableLibrary);
        var outside = Path.Combine(_tempRoot, "important.pdf");
        var sentinel = "HOLD"u8.ToArray();
        Directory.CreateDirectory(_tempRoot);
        File.WriteAllBytes(outside, sentinel);
        try
        {
            _ = ProjectLibraryRootConfiguration.ApplyIfPresent(
                [ProjectLibraryRootConfiguration.Switch, disposableLibrary]);

            var outsideRefusal = Assert.Throws<InvalidDataException>(() => UiaHarness.FromArgs(
            [
                UiaHarness.Switch,
                "pressroom",
                ProjectLibraryRootConfiguration.Switch,
                disposableLibrary,
                UiaHarness.ExportToSwitch,
                outside,
            ]));
            Assert.Equal(UiStrings.UiaHarnessExportInvalid, outsideRefusal.Message);
            Assert.Equal(sentinel, File.ReadAllBytes(outside));

            var missingRefusal = Assert.Throws<InvalidDataException>(() => UiaHarness.FromArgs(
            [
                UiaHarness.Switch,
                "pressroom",
                ProjectLibraryRootConfiguration.Switch,
                disposableLibrary,
                UiaHarness.ExportToSwitch,
            ]));
            Assert.Equal(UiStrings.UiaHarnessExportInvalid, missingRefusal.Message);

            var irrelevantRefusal = Assert.Throws<InvalidDataException>(() => UiaHarness.FromArgs(
            [
                UiaHarness.Switch,
                "review",
                UiaHarness.ExportToSwitch,
                outside,
            ]));
            Assert.Equal(UiStrings.UiaHarnessExportInvalid, irrelevantRefusal.Message);

            var inside = Path.Combine(disposableLibrary, "rehearsal-booklet.pdf");
            using var admitted = UiaHarness.FromArgs(
            [
                UiaHarness.Switch,
                "pressroom",
                ProjectLibraryRootConfiguration.Switch,
                disposableLibrary,
                UiaHarness.ExportToSwitch,
                inside,
            ]);
            Assert.IsType<PressRoomForm>(admitted);
            Assert.False(File.Exists(inside));
        }
        finally
        {
            Directory.Delete(rehearsalRoot, recursive: true);
        }
    }

    [Fact]
    public void Harness_exception_diagnostics_never_copy_messages_paths_or_authored_text()
    {
        var sensitive = Path.Combine(_tempRoot, "synthetic-authored-text.txt");
        var diagnostic = UiaHarness.ContentFreeExceptionDiagnostic(
            new InvalidOperationException($"Could not read {sensitive}"));

        Assert.Equal("Unhandled UI thread exception (InvalidOperationException).", diagnostic);
        Assert.DoesNotContain(_tempRoot, diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("authored", diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Save_and_open_are_confined_to_the_configured_library_root()
    {
        var managedRoot = Path.Combine(_tempRoot, EngineIdentity.EngineVersion, "prepared-library");
        Directory.CreateDirectory(managedRoot);
        _ = ProjectLibraryRootConfiguration.ApplyIfPresent(
            [ProjectLibraryRootConfiguration.Switch, managedRoot]);

        var session = AppServices.SessionOverGreen(
            new ArtifactDocument([new Heading(1, "Synthetic library boundary fixture")]));
        var approved = session.Approve("Synthetic test teacher", DateTimeOffset.UnixEpoch);
        var hint = AppServices.SaveToLibrary(
            approved,
            "boundary",
            "synthetic-module",
            "synthetic-recipe",
            "1.0.0",
            new AppServices.NoAssetsCatalog());
        var saved = Path.Combine(managedRoot, hint + OcfprojProjectStore.Extension);

        Assert.True(File.Exists(saved));
        Assert.Single(Directory.EnumerateFiles(managedRoot, "*.ocfproj", SearchOption.AllDirectories));
        var loaded = AppServices.OpenFromLibrary(saved);
        Assert.Equal("Synthetic library boundary fixture", loaded.Document.Nodes.OfType<Heading>().Single().Text);

        var outside = Path.Combine(_tempRoot, "outside.ocfproj");
        File.Copy(saved, outside);
        var refusal = Assert.Throws<InvalidOperationException>(() => AppServices.OpenFromLibrary(outside));
        Assert.Equal("The selected project is outside the configured project library.", refusal.Message);
        Assert.DoesNotContain(outside, refusal.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Saved_projects_reopen_through_the_hardened_reader_into_a_fresh_review()
        => Sta.Run(() =>
        {
            string? pickedPath = null;
            var preflightCalls = 0;
            using var form = new PressRoomForm(
                GateRespectingApprove,
                () => pickedPath,
                loadedProjectPreflight: loaded =>
                {
                    preflightCalls++;
                    return ConfirmSyntheticLoadedProject(loaded);
                });
            form.Show();

            // Forge and approve a single-sheet artifact, then save it.
            var list = (ListBox)ReviewSurfaceContractTests.ByName(form, "Presses");
            list.SelectedIndex = Modules.DeterministicPress.PressRoomCatalog.All.ToList().FindIndex(d => d.Id == "tangram");
            ((Button)ReviewSurfaceContractTests.ByName(form, "Review and approve…")).PerformClick();
            Assert.True(form.ApprovedResult is not null, form.StatusText);
            ((Button)ReviewSurfaceContractTests.ByName(form, "Save to library")).PerformClick();

            var saved = Assert.Single(Directory.GetFiles(_tempRoot, "*.ocfproj"));
            var firstApproval = form.ApprovedResult;

            // Reopen: the door runs the whole road again — reader, Gate B, approval.
            pickedPath = saved;
            form.OpenFromLibrary();

            Assert.True(form.ApprovedResult is not null, form.StatusText);
            Assert.NotSame(firstApproval, form.ApprovedResult);
            Assert.Equal(1, preflightCalls);
            Assert.Equal(ArtifactPurpose.Unknown, form.ApprovedResult.Revision.Purpose);
            Assert.Contains("Approved", form.StatusText, StringComparison.Ordinal);
            Assert.True(ReviewSurfaceContractTests.ByName(form, "Print").Enabled);
        });

    [Fact]
    public void Reopened_semantic_content_can_be_edited_offline_and_resaved_without_fabricating_origin_provenance()
        => Sta.Run(() =>
        {
            string? pickedPath = null;
            var reopenedReviewSeen = false;
            ApprovedArtifact? ReviewAndEdit(ReviewSession session)
            {
                if (session.RequiredAcknowledgements.Any(issue =>
                        issue.Code == "project.origin-unverified"))
                {
                    reopenedReviewSeen = true;
                    session.ReplaceNode(0, new Paragraph("Portable offline edit, reviewed exactly."));
                    Assert.Contains(session.RequiredAcknowledgements, issue =>
                        issue.Code == "project.saved-revision-changed"
                        && issue.RequiresAcknowledgement);
                }

                session.SetRequiredIssuesAcknowledged(acknowledged: true);
                return session.CanApprove
                    ? session.Approve("Synthetic test teacher", DateTimeOffset.UnixEpoch)
                    : null;
            }

            using var form = new PressRoomForm(
                ReviewAndEdit,
                () => pickedPath,
                loadedProjectPreflight: ConfirmSyntheticLoadedProject);
            form.Show();

            var list = (ListBox)ReviewSurfaceContractTests.ByName(form, "Presses");
            list.SelectedIndex = Modules.DeterministicPress.PressRoomCatalog.All.ToList()
                .FindIndex(definition => definition.Id == "tangram");
            ((Button)ReviewSurfaceContractTests.ByName(form, "Review and approve…")).PerformClick();
            ((Button)ReviewSurfaceContractTests.ByName(form, "Save to library")).PerformClick();
            pickedPath = Assert.Single(Directory.GetFiles(_tempRoot, "*.ocfproj"));

            form.OpenFromLibrary();
            Assert.True(reopenedReviewSeen);
            Assert.NotNull(form.ApprovedResult);
            Assert.Equal(ArtifactPurpose.Unknown, form.ApprovedResult.Revision.Purpose);
            ((Button)ReviewSurfaceContractTests.ByName(form, "Save to library")).PerformClick();

            var saved = Directory.GetFiles(_tempRoot, "*.ocfproj");
            Assert.Equal(2, saved.Length);
            var portable = saved
                .Select(path => OcfprojProjectStore.LoadProjectFileAsync(path, CancellationToken.None)
                    .GetAwaiter().GetResult())
                .Single(project => project.Manifest.ModuleId == AppServices.PortableProjectModuleId);
            Assert.Equal(AppServices.PortableProjectRecipeId, portable.Manifest.RecipeId);
            Assert.Equal(AppServices.PortableProjectRecipeVersion, portable.Manifest.RecipeVersion);
            Assert.Equal(ArtifactPurpose.Unknown, portable.Manifest.Purpose);
            Assert.Contains(portable.Document.Nodes, node =>
                node is Paragraph { Text: "Portable offline edit, reviewed exactly." });
        });

    [Fact]
    public void A_loaded_package_cannot_reach_review_or_any_sink_when_lane_preflight_is_not_completed()
        => Sta.Run(() =>
        {
            string? pickedPath = null;
            var reviewCalls = 0;
            using var form = new PressRoomForm(
                session =>
                {
                    reviewCalls++;
                    return GateRespectingApprove(session);
                },
                () => pickedPath,
                loadedProjectPreflight: _ => null);
            form.Show();

            var list = (ListBox)ReviewSurfaceContractTests.ByName(form, "Presses");
            list.SelectedIndex = Modules.DeterministicPress.PressRoomCatalog.All.ToList().FindIndex(d => d.Id == "tangram");
            ((Button)ReviewSurfaceContractTests.ByName(form, "Review and approve…")).PerformClick();
            ((Button)ReviewSurfaceContractTests.ByName(form, "Save to library")).PerformClick();
            pickedPath = Assert.Single(Directory.GetFiles(_tempRoot, "*.ocfproj"));

            form.OpenFromLibrary();

            Assert.Equal(1, reviewCalls);
            Assert.Null(form.ApprovedResult);
            Assert.Contains("Amber by default", form.StatusText, StringComparison.Ordinal);
            Assert.False(ReviewSurfaceContractTests.ByName(form, "Print").Enabled);
            Assert.False(ReviewSurfaceContractTests.ByName(form, "Open print view").Enabled);
            Assert.False(ReviewSurfaceContractTests.ByName(form, "Export…").Enabled);
            Assert.False(ReviewSurfaceContractTests.ByName(form, "Save to library").Enabled);
        });

    [Fact]
    public void A_tampered_package_is_refused_in_the_speaking_status()
        => Sta.Run(() =>
        {
            string? pickedPath = null;
            using var form = new PressRoomForm(GateRespectingApprove, () => pickedPath);
            form.Show();

            Directory.CreateDirectory(_tempRoot);
            var hostile = Path.Combine(_tempRoot, "hostile.ocfproj");
            File.WriteAllBytes(hostile, [0x50, 0x4B, 0x03, 0x04, 0xFF]);

            pickedPath = hostile;
            form.OpenFromLibrary();

            Assert.Null(form.ApprovedResult);
            Assert.Contains("refused", form.StatusText, StringComparison.Ordinal);
        });

    [Fact]
    public void Wall_tiles_are_a_new_document_that_passes_gate_b_itself()
        => Sta.Run(() =>
        {
            using var form = new PressRoomForm(GateRespectingApprove);
            form.Show();

            var list = (ListBox)ReviewSurfaceContractTests.ByName(form, "Presses");
            list.SelectedIndex = Modules.DeterministicPress.PressRoomCatalog.All.ToList().FindIndex(d => d.Id == "tangram");
            ((Button)ReviewSurfaceContractTests.ByName(form, "Review and approve…")).PerformClick();
            var single = form.ApprovedResult;
            Assert.NotNull(single);

            form.TileApproved(2, 2);

            Assert.NotNull(form.ApprovedResult);
            Assert.NotSame(single, form.ApprovedResult);
            Assert.Equal(4, form.ApprovedResult.Revision.Document.Nodes.OfType<VectorGraphic>().Count());
            Assert.Contains(form.ApprovedResult.Revision.Document.Nodes.OfType<TeacherOnlyNotice>(),
                n => n.Text.Contains("100 percent", StringComparison.Ordinal));
        });

    [Fact]
    public void Tiling_a_multi_sheet_artifact_is_refused_out_loud()
        => Sta.Run(() =>
        {
            using var form = new PressRoomForm(GateRespectingApprove);
            form.Show();

            var list = (ListBox)ReviewSurfaceContractTests.ByName(form, "Presses");
            list.SelectedIndex = Modules.DeterministicPress.PressRoomCatalog.All.ToList().FindIndex(d => d.Id == "bingo-cards");
            ((Button)ReviewSurfaceContractTests.ByName(form, "Review and approve…")).PerformClick();
            Assert.NotNull(form.ApprovedResult);

            form.TileApproved(2, 2);

            Assert.Contains("single-sheet", form.StatusText, StringComparison.Ordinal);
        });

    [Fact]
    public void Print_and_tile_sit_behind_the_same_structural_gate()
        => Sta.Run(() =>
        {
            using var form = new PressRoomForm(GateRespectingApprove);
            form.Show();

            Assert.False(ReviewSurfaceContractTests.ByName(form, "Print").Enabled);
            Assert.False(ReviewSurfaceContractTests.ByName(form, "Tile for wall…").Enabled);
        });

    private static LoadedProjectGreenConfirmation ConfirmSyntheticLoadedProject(LoadedProject loaded)
        => AppServices.ConfirmLoadedProjectGreen(
            loaded,
            new LoadedProjectGreenChecklist(
                IsGreenQualifyingContent: true,
                HasNoLearnerLinkedContent: true,
                HasNoRestrictedContent: true));
}
