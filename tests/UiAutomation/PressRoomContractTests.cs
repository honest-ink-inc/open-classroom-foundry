// SPDX-License-Identifier: GPL-3.0-or-later
using System.IO;
using Foundry.App.WinForms;
using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.DeterministicPress;

namespace Foundry.Tests.UiAutomation;

// The Press Room's accessibility and flow contract (second forge menu, item 1
// DoD): a teacher reaches an approved artifact from a cold start by keyboard-
// equivalent steps, the structural gate is visible as disabled controls, the
// budget is displayed, and refusals land in the announced status.

public class PressRoomContractTests
{
    private static ApprovedArtifact AutoApprove(ReviewSession session)
    {
        session.SetRequiredIssuesAcknowledged(acknowledged: true);
        return session.Approve(Environment.UserName, DateTimeOffset.UtcNow);
    }

    [Fact]
    public void App_render_bridge_forwards_export_cancellation_into_the_renderer()
    {
        var approved = AutoApprove(AppServices.SessionOverGreen(
            new ArtifactDocument([new Paragraph("Synthetic cancellable export")])));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => AppServices.Render(
            approved,
            RenderTarget.AccessibleHtml,
            cancellationToken: cancellation.Token));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(99d)]
    [InlineData(201d)]
    public async Task Native_pdf_export_cannot_bypass_the_persisted_text_scale_contract(double textScalePercent)
    {
        var approved = AutoApprove(AppServices.SessionOverGreen(new ArtifactDocument([
            new VectorGraphic(
                10,
                10,
                [new LineSeg(1, 1, 9, 9)],
                "Synthetic bounded scale sheet"),
        ])));
        var destination = Path.Combine(
            Path.GetTempPath(),
            $"honest-ink-invalid-scale-{Guid.NewGuid():N}.pdf");

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => AppServices.ExportPdfAsync(
            approved,
            destination,
            textScalePercent: textScalePercent));

        Assert.False(File.Exists(destination));
    }

    [Fact]
    public async Task Native_pdf_export_refuses_amber_before_creating_the_destination()
    {
        var approved = ApprovalGate.Approve(
            DraftArtifact.New(new ArtifactDocument([
                new VectorGraphic(
                    10,
                    10,
                    [new LineSeg(1, 1, 9, 9)],
                    "Synthetic Amber authorization sheet"),
            ]), DataLane.Amber),
            "teacher@example.org",
            [],
            new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero));
        var destination = Path.Combine(
            Path.GetTempPath(),
            $"honest-ink-amber-native-refusal-{Guid.NewGuid():N}.pdf");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AppServices.ExportPdfAsync(approved, destination));

        Assert.Contains("request-bound", exception.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(destination));
    }

    private static void WithPressRoom(Func<ReviewSession, ApprovedArtifact?> runner, Action<PressRoomForm> assert)
        => Sta.Run(() =>
        {
            using var form = new PressRoomForm(runner);
            form.Show();
            assert(form);
        });

    private static void SelectPress(PressRoomForm form, string id)
    {
        var list = (ListBox)ReviewSurfaceContractTests.ByName(form, "Presses");
        list.SelectedIndex = PressRoomCatalog.All.ToList().FindIndex(d => d.Id == id);
    }

    private static void PumpUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            System.Windows.Forms.Application.DoEvents();
            Thread.Sleep(10);
        }

        Assert.True(condition(), "The asynchronous UI operation did not reach its expected state.");
    }

    /// <summary>
    /// A parameter row holds a visual Label AND its named input, and a
    /// NumericUpDown's internal children inherit its name — this picks the
    /// top-level input control itself.
    /// </summary>
    private static Control Input(PressRoomForm form, string accessibleName)
        => ReviewSurfaceContractTests.Flatten(form)
            .Single(c => c is not Label
                && c.Parent is not NumericUpDown
                && c.AccessibilityObject.Name == accessibleName);

    [Fact]
    public void Cold_start_to_typed_approval_press_parameters_review_approve()
        => WithPressRoom(AutoApprove, form =>
        {
            SelectPress(form, "word-search");
            ((Button)ReviewSurfaceContractTests.ByName(form, "Review and approve…")).PerformClick();

            Assert.NotNull(form.ApprovedResult);
            Assert.True(ReviewSurfaceContractTests.ByName(form, "Open print view").Enabled);
            Assert.True(ReviewSurfaceContractTests.ByName(form, "Export…").Enabled);
            Assert.True(ReviewSurfaceContractTests.ByName(form, "Save to library").Enabled);
        });

    [Fact]
    public void Print_view_failure_is_announced_instead_of_escaping_the_UI_event()
        => Sta.Run(() =>
        {
            using var form = new PressRoomForm(
                AutoApprove,
                printViewOpener: (_, _, _, _, _, _) =>
                    throw new IOException("synthetic print-view refusal"));
            form.Show();
            SelectPress(form, "graph-paper");
            ((Button)ReviewSurfaceContractTests.ByName(form, "Review and approve…")).PerformClick();

            ((Button)ReviewSurfaceContractTests.ByName(form, "Open print view")).PerformClick();

            Assert.Equal(
                UiStrings.WithoutMnemonic(UiStrings.StatusPrintViewRefused),
                form.StatusText);
            Assert.NotNull(form.ApprovedResult);
            Assert.True(ReviewSurfaceContractTests.ByName(form, "Open print view").Enabled);
        });

    [Fact]
    public void Print_view_handoff_keeps_the_press_room_responsive_and_gated_until_response_write()
        => Sta.Run(() =>
        {
            var release = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using var form = new PressRoomForm(
                AutoApprove,
                printViewOpener: (_, _, _, _, _, _) => release.Task);
            form.Show();
            SelectPress(form, "graph-paper");
            ((Button)ReviewSurfaceContractTests.ByName(form, "Review and approve…")).PerformClick();

            var printView = (Button)ReviewSurfaceContractTests.ByName(form, "Open print view");
            printView.PerformClick();

            Assert.Equal(
                UiStrings.WithoutMnemonic(UiStrings.StatusPrintViewOpening),
                form.StatusText);
            Assert.False(printView.Enabled);
            Assert.False(ReviewSurfaceContractTests.ByName(form, "Presses").Enabled);

            release.TrySetResult(true);
            PumpUntil(() => printView.Enabled
                && form.StatusText == UiStrings.WithoutMnemonic(UiStrings.StatusPrintView));
        });

    [Fact]
    public void Pdf_export_gates_mutation_announces_progress_and_remains_keyboard_cancellable()
        => Sta.Run(() =>
        {
            var destination = Path.Combine(Path.GetTempPath(), $"press-room-{Guid.NewGuid():N}.pdf");
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pickerCalls = 0;
            var exporterCalls = 0;
            using var form = new PressRoomForm(
                AutoApprove,
                exportPicker: () =>
                {
                    pickerCalls++;
                    return new PressRoomForm.ExportChoice(destination, 1);
                },
                pdfExporter: async (_, _, _, cancellationToken) =>
                {
                    exporterCalls++;
                    started.TrySetResult(true);
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                });
            form.Show();
            SelectPress(form, "graph-paper");
            ((Button)ReviewSurfaceContractTests.ByName(form, "Review and approve…")).PerformClick();

            var export = (Button)ReviewSurfaceContractTests.ByName(form, "Export…");
            var cancel = (Button)ReviewSurfaceContractTests.ByName(form, "Cancel export");
            export.PerformClick();
            export.PerformClick();
            PumpUntil(() => started.Task.IsCompleted);

            Assert.Equal(1, pickerCalls);
            Assert.Equal(1, exporterCalls);
            Assert.False(export.Enabled);
            Assert.True(cancel.Enabled);
            Assert.Contains("Exporting", form.StatusText, StringComparison.Ordinal);
            Assert.False(ReviewSurfaceContractTests.ByName(form, "Presses").Enabled);
            Assert.False(ReviewSurfaceContractTests.ByName(form, "Open from library…").Enabled);

            cancel.PerformClick();
            PumpUntil(() => form.StatusText.Contains("cancelled", StringComparison.OrdinalIgnoreCase));

            Assert.False(cancel.Enabled);
            Assert.True(export.Enabled);
            Assert.False(File.Exists(destination));
        });

    [Fact]
    public void Unrequested_exporter_cancellation_reaches_the_UI_fault_boundary_and_recovers_controls()
        => Sta.Run(() =>
        {
            var destination = Path.Combine(Path.GetTempPath(), $"press-room-unrequested-{Guid.NewGuid():N}.pdf");
            File.WriteAllText(destination, "original exact bytes");
            Exception? escapedThreadException = null;
            void captureThreadException(object _, ThreadExceptionEventArgs args)
            {
                escapedThreadException ??= args.Exception;
            }

            System.Windows.Forms.Application.ThreadException += captureThreadException;
            try
            {
                var exporterFailure = new OperationCanceledException(
                    "Synthetic unrequested Press Room exporter cancellation.");
                using var form = new PressRoomForm(
                    AutoApprove,
                    exportPicker: () => new PressRoomForm.ExportChoice(destination, 1),
                    pdfExporter: (_, _, _, cancellationToken) =>
                    {
                        Assert.False(cancellationToken.IsCancellationRequested);
                        return Task.FromException(exporterFailure);
                    });
                form.Show();
                SelectPress(form, "graph-paper");
                ((Button)ReviewSurfaceContractTests.ByName(form, "Review and approve…")).PerformClick();

                var export = (Button)ReviewSurfaceContractTests.ByName(form, "Export…");
                var cancel = (Button)ReviewSurfaceContractTests.ByName(form, "Cancel export");
                export.PerformClick();
                PumpUntil(() => escapedThreadException is not null
                    && export.Enabled
                    && !cancel.Enabled);

                Assert.Same(exporterFailure, escapedThreadException);
                Assert.Equal("original exact bytes", File.ReadAllText(destination));
                Assert.DoesNotContain("cancelled", form.StatusText, StringComparison.OrdinalIgnoreCase);
                Assert.True(ReviewSurfaceContractTests.ByName(form, "Presses").Enabled);
                Assert.True(ReviewSurfaceContractTests.ByName(form, "Open from library…").Enabled);
            }
            finally
            {
                System.Windows.Forms.Application.ThreadException -= captureThreadException;
                File.Delete(destination);
            }
        });

    [Fact]
    public void Export_picker_failure_is_announced_instead_of_escaping_the_ui_dispatch()
        => Sta.Run(() =>
        {
            using var form = new PressRoomForm(
                AutoApprove,
                exportPicker: () => throw new InvalidOperationException("Synthetic picker refusal."));
            form.Show();
            SelectPress(form, "graph-paper");
            ((Button)ReviewSurfaceContractTests.ByName(form, "Review and approve…")).PerformClick();

            ((Button)ReviewSurfaceContractTests.ByName(form, "Export…")).PerformClick();
            PumpUntil(() => form.StatusText.Contains("Synthetic picker refusal", StringComparison.Ordinal));

            Assert.Contains("refused", form.StatusText, StringComparison.OrdinalIgnoreCase);
            Assert.True(ReviewSurfaceContractTests.ByName(form, "Export…").Enabled);
        });

    [Fact]
    public void The_structural_gate_is_visible_nothing_unlocks_before_approval()
        => WithPressRoom(AutoApprove, form =>
        {
            Assert.Null(form.ApprovedResult);
            Assert.False(ReviewSurfaceContractTests.ByName(form, "Open print view").Enabled);
            Assert.False(ReviewSurfaceContractTests.ByName(form, "Export…").Enabled);
            Assert.False(ReviewSurfaceContractTests.ByName(form, "Save to library").Enabled);
        });

    [Fact]
    public void Changing_a_press_input_revokes_the_exact_approval_and_every_sink()
        => WithPressRoom(AutoApprove, form =>
        {
            SelectPress(form, "graph-paper");
            ((Button)ReviewSurfaceContractTests.ByName(form, "Review and approve…")).PerformClick();
            Assert.NotNull(form.ApprovedResult);

            ((NumericUpDown)Input(form, "Square size (mm)")).Value += 1;

            Assert.Null(form.ApprovedResult);
            Assert.Contains("fresh review", form.StatusText, StringComparison.Ordinal);
            Assert.False(ReviewSurfaceContractTests.ByName(form, "Print").Enabled);
            Assert.False(ReviewSurfaceContractTests.ByName(form, "Open print view").Enabled);
            Assert.False(ReviewSurfaceContractTests.ByName(form, "Export…").Enabled);
            Assert.False(ReviewSurfaceContractTests.ByName(form, "Save to library").Enabled);
        });

    [Fact]
    public void A_press_review_runner_cannot_substitute_another_revision()
        => WithPressRoom(_ =>
        {
            var other = AppServices.SessionOverGreen(
                new ArtifactDocument([new Heading(1, "Different synthetic sheet")]));
            return AutoApprove(other);
        }, form =>
        {
            ((Button)ReviewSurfaceContractTests.ByName(form, "Review and approve…")).PerformClick();

            Assert.Null(form.ApprovedResult);
            Assert.Contains("without approval", form.StatusText, StringComparison.Ordinal);
            Assert.False(ReviewSurfaceContractTests.ByName(form, "Export…").Enabled);
        });

    [Fact]
    public void A_declined_review_unlocks_nothing()
        => WithPressRoom(_ => null, form =>
        {
            SelectPress(form, "graph-paper");
            ((Button)ReviewSurfaceContractTests.ByName(form, "Review and approve…")).PerformClick();

            Assert.Null(form.ApprovedResult);
            Assert.False(ReviewSurfaceContractTests.ByName(form, "Export…").Enabled);
        });

    [Fact]
    public void Part2_Step7_a_press_refusal_is_surfaced_in_the_status_not_swallowed()
        => WithPressRoom(AutoApprove, form =>
        {
            SelectPress(form, "word-search");
            var words = (TextBox)Input(form, "Words to hide, one per line");
            words.Text = "two words on one line";

            ((Button)ReviewSurfaceContractTests.ByName(form, "Review and approve…")).PerformClick();

            Assert.Null(form.ApprovedResult);
            Assert.Contains("refused", form.StatusText, StringComparison.Ordinal);
            Assert.Contains("letters only", form.StatusText, StringComparison.Ordinal);
        });

    [Fact]
    public void The_three_minute_budget_is_displayed_as_the_constitution_requires()
        => WithPressRoom(AutoApprove, form =>
        {
            var budget = ReviewSurfaceContractTests.Flatten(form).OfType<Label>()
                .Single(l => l.Text.Contains("budget", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("3", budget.Text, StringComparison.Ordinal);
        });

    [Fact]
    public void Part2_Step4_choosing_a_press_regenerates_its_labeled_parameter_controls()
        => WithPressRoom(AutoApprove, form =>
        {
            SelectPress(form, "graph-paper");
            Assert.IsType<NumericUpDown>(Input(form, "Square size (mm)"));

            SelectPress(form, "grouping-cards");
            Assert.IsType<NumericUpDown>(Input(form, "Group size"));
            Assert.IsType<TextBox>(Input(form,
                "Roster labels, one per line - synthetic or first-name-free only"));
        });

    [Fact]
    public void Part1_Step2_every_focusable_press_room_control_is_named_and_roled()
        => WithPressRoom(AutoApprove, form =>
        {
            SelectPress(form, "bingo-cards");
            var focusable = ReviewSurfaceContractTests.Flatten(form).Where(c => c.TabStop && c.CanSelect).ToList();

            Assert.NotEmpty(focusable);
            Assert.All(focusable, control =>
            {
                Assert.False(string.IsNullOrWhiteSpace(control.AccessibilityObject.Name),
                    $"{control.GetType().Name} is an unnamed focusable control");
                Assert.NotEqual(AccessibleRole.None, control.AccessibilityObject.Role);
            });
        });

    [Fact]
    public void The_press_list_speaks_every_catalog_title()
        => WithPressRoom(AutoApprove, form =>
        {
            var list = (ListBox)ReviewSurfaceContractTests.ByName(form, "Presses");
            Assert.Equal(PressRoomCatalog.All.Count, list.Items.Count);
            Assert.Equal(PressRoomCatalog.All.Select(d => d.Title), list.Items.Cast<string>());
        });
}
