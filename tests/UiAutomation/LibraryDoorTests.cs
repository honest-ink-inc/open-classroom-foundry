// SPDX-License-Identifier: GPL-3.0-or-later
using System.IO;
using Foundry.App.WinForms;
using Foundry.Application;
using Foundry.Domain;

namespace Foundry.Tests.UiAutomation;

// The library door and one-keystroke paper (third forge menu, items 1 and 2):
// reversibility exercised through the REAL surface — save, reopen through the
// hardened reader, re-review, re-approve — plus the wall-tile flow whose
// output passes Gate B itself, and the print gate.

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
        => session.CanApprove ? session.Approve(Environment.UserName, DateTimeOffset.UtcNow) : null;

    [Fact]
    public void Saved_projects_reopen_through_the_hardened_reader_into_a_fresh_review()
        => Sta.Run(() =>
        {
            string? pickedPath = null;
            using var form = new PressRoomForm(GateRespectingApprove, () => pickedPath);
            form.Show();

            // Forge and approve a single-sheet artifact, then save it.
            var list = (ListBox)ReviewSurfaceContractTests.ByName(form, "Presses");
            list.SelectedIndex = Modules.DeterministicPress.PressRoomCatalog.All.ToList().FindIndex(d => d.Id == "tangram");
            ((Button)ReviewSurfaceContractTests.ByName(form, "Review and approve…")).PerformClick();
            Assert.NotNull(form.ApprovedResult);
            ((Button)ReviewSurfaceContractTests.ByName(form, "Save to library")).PerformClick();

            var saved = Assert.Single(Directory.GetFiles(_tempRoot, "*.ocfproj"));
            var firstApproval = form.ApprovedResult;

            // Reopen: the door runs the whole road again — reader, Gate B, approval.
            pickedPath = saved;
            form.OpenFromLibrary();

            Assert.NotNull(form.ApprovedResult);
            Assert.NotSame(firstApproval, form.ApprovedResult);
            Assert.Contains("Approved", form.StatusText, StringComparison.Ordinal);
            Assert.True(ReviewSurfaceContractTests.ByName(form, "Print").Enabled);
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
}
