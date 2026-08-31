// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using Foundry.App.WinForms;
using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.BuiltIn.BoardToBrief;

namespace Foundry.Tests.UiAutomation;

/// <summary>
/// In-process proof for the Board-to-Brief source-intake boundary. All image
/// and transcript material is synthetic; no installed OCR language is assumed.
/// </summary>
public sealed class BoardToBriefIntakeContractTests
{
    private static readonly DateTimeOffset ApprovalInstant = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Explicit_uncertainty_grammar_editable_order_and_roles_return_only_after_purge()
        => Sta.Run(() =>
        {
            var fixture = GreenFixture(new InMemorySessionByteStore());
            Assert.True(fixture.Store.TryGet(fixture.Envelope.Bytes, out var heldBytes));
            using var form = Intake(
                fixture,
                new FixedOcrService(UncertainRecognition()));
            form.Show();
            PumpUntil(() => Named<Button>(form, BoardToBriefIntakeForm.AcceptCandidateName).Enabled);

            Assert.Null(form.ResultLines);
            Assert.Equal(JobState.DataLaneConfirmed, form.IntakeState);
            Assert.Contains(
                "[unresolved: Todays]",
                Named<TextBox>(form, BoardToBriefIntakeForm.VerifiedTextName).Text,
                StringComparison.Ordinal);

            Named<Button>(form, BoardToBriefIntakeForm.NextUncertainName).PerformClick();
            Assert.Contains("plan", CurrentUncertain(form), StringComparison.Ordinal);
            Named<Button>(form, BoardToBriefIntakeForm.AcceptCandidateName).PerformClick();

            Named<TextBox>(form, BoardToBriefIntakeForm.ReplacementName).Text = "Open";
            Named<Button>(form, BoardToBriefIntakeForm.RetypeName).PerformClick();
            Named<Button>(form, BoardToBriefIntakeForm.MarkIllegibleName).PerformClick();
            Assert.Contains("Todays", CurrentUncertain(form), StringComparison.Ordinal);

            Named<TextBox>(form, BoardToBriefIntakeForm.ReplacementName).Text = "Today's";
            Named<Button>(form, BoardToBriefIntakeForm.RetypeName).PerformClick();

            var roles = Named<DataGridView>(form, BoardToBriefIntakeForm.RoleGridName);
            Assert.Equal(2, roles.Rows.Count);
            Assert.Equal("Today's plan", roles.Rows[0].Cells[0].Value);
            Assert.Equal($"Open {TranscriptSession.IllegibleMarker}", roles.Rows[1].Cells[0].Value);
            roles.Rows[0].Cells[1].Value = BriefRole.Title;
            roles.Rows[1].Cells[0].Value = "Open the notebook";
            roles.Rows[1].Cells[1].Value = BriefRole.Step;

            roles.CurrentCell = roles.Rows[1].Cells[0];
            Named<Button>(form, BoardToBriefIntakeForm.MoveUpName).PerformClick();
            Assert.Equal("Open the notebook", roles.Rows[0].Cells[0].Value);
            Assert.Null(form.ResultLines);
            Assert.Equal(1, fixture.Store.Count);

            Named<Button>(form, BoardToBriefIntakeForm.FinishName).PerformClick();

            Assert.False(form.Visible);
            Assert.Equal(DialogResult.OK, form.DialogResult);
            Assert.Equal(JobState.TransientSourcesPurged, form.IntakeState);
            Assert.Equal(0, fixture.Store.Count);
            Assert.All(heldBytes.ToArray(), value => Assert.Equal(0, value));
            Assert.NotNull(form.ResultLines);
            Assert.Collection(
                form.ResultLines,
                line => Assert.Equal(new BriefLine("Open the notebook", BriefRole.Step), line),
                line => Assert.Equal(new BriefLine("Today's plan", BriefRole.Title), line));
        });

    [Fact]
    public void Missing_roles_and_nonunique_title_keep_bytes_and_result_behind_the_gate()
        => Sta.Run(() =>
        {
            var fixture = GreenFixture(new InMemorySessionByteStore());
            using var form = Intake(fixture, new FixedOcrService(ConfidentRecognition()));
            form.Show();
            PumpUntil(() => Named<DataGridView>(form, BoardToBriefIntakeForm.RoleGridName).Rows.Count == 2);

            var roles = Named<DataGridView>(form, BoardToBriefIntakeForm.RoleGridName);
            var finish = Named<Button>(form, BoardToBriefIntakeForm.FinishName);
            finish.PerformClick();
            Assert.Contains("role", form.StatusText, StringComparison.OrdinalIgnoreCase);
            Assert.Null(form.ResultLines);
            Assert.Equal(JobState.DataLaneConfirmed, form.IntakeState);

            roles.Rows[0].Cells[1].Value = BriefRole.Step;
            roles.Rows[1].Cells[1].Value = BriefRole.Step;
            finish.PerformClick();
            Assert.Contains("Exactly one", form.StatusText, StringComparison.Ordinal);
            Assert.Null(form.ResultLines);
            Assert.Equal(1, fixture.Store.Count);

            roles.Rows[0].Cells[1].Value = BriefRole.Title;
            finish.PerformClick();
            Assert.NotNull(form.ResultLines);
            Assert.Equal(JobState.TransientSourcesPurged, form.IntakeState);
        });

    [Fact]
    public void Amber_is_purged_before_refusal_notice_and_never_calls_OCR()
        => Sta.Run(() =>
        {
            var fixture = Fixture(new InMemorySessionByteStore(), DataLane.Amber);
            var ocr = new RecordingOcrService(ConfidentRecognition());
            var noticeStoreCounts = new List<int>();
            using var form = Intake(
                fixture,
                ocr,
                (_, _, _) => noticeStoreCounts.Add(fixture.Store.Count));
            form.Show();
            PumpUntil(() => !form.Visible);

            Assert.Equal(0, ocr.Calls);
            Assert.Equal([0], noticeStoreCounts);
            Assert.Null(form.ResultLines);
            Assert.Equal(DialogResult.Cancel, form.DialogResult);
            Assert.Equal(JobState.TransientSourcesPurged, form.IntakeState);
            Assert.Equal(0, fixture.Store.Count);
        });

    [Fact]
    public void Safety_pause_cancels_and_settles_OCR_before_purge_and_notice()
        => Sta.Run(() =>
        {
            var fixture = GreenFixture(new InMemorySessionByteStore());
            var ocr = new BlockingOcrService();
            var noticeStoreCounts = new List<int>();
            using var form = Intake(
                fixture,
                ocr,
                (_, _, _) => noticeStoreCounts.Add(fixture.Store.Count));
            form.Show();
            PumpUntil(() => form.OperationPending && ocr.Calls == 1);

            Named<Button>(form, BoardToBriefIntakeForm.SafetyPauseName).PerformClick();
            PumpUntil(() => !form.Visible);

            Assert.True(ocr.ObservedToken.IsCancellationRequested);
            Assert.True(ocr.Settled);
            Assert.Equal([0], noticeStoreCounts);
            Assert.Null(form.ResultLines);
            Assert.Equal(DialogResult.Abort, form.DialogResult);
            Assert.Equal(JobState.TransientSourcesPurged, form.IntakeState);
            Assert.Equal(0, fixture.Store.Count);
        });

    [Fact]
    public void Forced_dispose_settles_OCR_before_purging_and_withholds_results()
        => Sta.Run(() =>
        {
            var fixture = GreenFixture(new InMemorySessionByteStore());
            Assert.True(fixture.Store.TryGet(fixture.Envelope.Bytes, out var heldBytes));
            var ocr = new BlockingOcrService(fixture.Store);
            var form = Intake(fixture, ocr);
            form.Show();
            PumpUntil(() => form.OperationPending && ocr.Calls == 1);

            form.Dispose();
            PumpUntil(() => form.DisposalCleanup.IsCompleted);
            form.DisposalCleanup.GetAwaiter().GetResult();

            Assert.True(form.IsDisposed);
            Assert.True(ocr.ObservedToken.IsCancellationRequested);
            Assert.True(ocr.Settled);
            Assert.Equal(1, ocr.StoreCountAtSettlement);
            Assert.Equal(JobState.TransientSourcesPurged, form.IntakeState);
            Assert.Equal(0, fixture.Store.Count);
            Assert.All(heldBytes.ToArray(), value => Assert.Equal(0, value));
            Assert.Null(form.ResultLines);
        });

    [Fact]
    public void Unexpected_OCR_fault_during_terminal_cancel_cannot_block_purge()
        => Sta.Run(() =>
        {
            var fixture = GreenFixture(new InMemorySessionByteStore());
            var ocr = new FaultOnCancellationOcrService();
            using var form = Intake(fixture, ocr);
            form.Show();
            PumpUntil(() => form.OperationPending && ocr.Calls == 1);

            Named<Button>(form, BoardToBriefIntakeForm.CancelName).PerformClick();
            PumpUntil(() => !form.Visible);

            Assert.True(ocr.Settled);
            Assert.Equal(DialogResult.Cancel, form.DialogResult);
            Assert.Equal(JobState.TransientSourcesPurged, form.IntakeState);
            Assert.Equal(0, fixture.Store.Count);
            Assert.Null(form.ResultLines);
        });

    [Fact]
    public void Capture_surface_failure_cancels_and_purges_without_calling_OCR()
        => Sta.Run(() =>
        {
            var fixture = GreenFixture(new InMemorySessionByteStore());
            var ocr = new RecordingOcrService(ConfidentRecognition());
            using var form = new BoardToBriefIntakeForm(
                fixture.Store,
                fixture.Session,
                ocr,
                DistrictPolicy.Offline,
                captureRunner: _ => throw new KeyNotFoundException("Synthetic capture surface failure."),
                noticePresenter: (_, _, _) => { });

            form.Show();
            PumpUntil(() => !form.Visible);

            Assert.Equal(0, ocr.Calls);
            Assert.Equal(DialogResult.Cancel, form.DialogResult);
            Assert.Equal(JobState.TransientSourcesPurged, form.IntakeState);
            Assert.Equal(0, fixture.Store.Count);
            Assert.Null(form.ResultLines);
        });

    [Fact]
    public void Incomplete_purge_exposes_only_retry_and_withholds_frozen_result()
        => Sta.Run(() =>
        {
            var store = new FailsFirstPurgeStore();
            var fixture = GreenFixture(store);
            Assert.True(store.TryGet(fixture.Envelope.Bytes, out var heldBytes));
            using var form = Intake(fixture, new FixedOcrService(OneConfidentTitle()));
            form.Show();
            PumpUntil(() => Named<DataGridView>(form, BoardToBriefIntakeForm.RoleGridName).Rows.Count == 1);
            var roles = Named<DataGridView>(form, BoardToBriefIntakeForm.RoleGridName);
            roles.Rows[0].Cells[1].Value = BriefRole.Title;

            Named<Button>(form, BoardToBriefIntakeForm.FinishName).PerformClick();

            Assert.True(form.Visible);
            Assert.Equal(JobState.PurgeIncomplete, form.IntakeState);
            Assert.Null(form.ResultLines);
            Assert.Equal(DialogResult.None, form.DialogResult);
            var enabledButtons = Controls(form).OfType<Button>()
                .Where(button => button.Visible && button.Enabled)
                .ToList();
            var retry = Assert.Single(enabledButtons);
            Assert.Equal(BoardToBriefIntakeForm.RetryPurgeName, retry.Name);

            retry.PerformClick();

            Assert.False(form.Visible);
            Assert.Equal(DialogResult.OK, form.DialogResult);
            Assert.Equal(JobState.TransientSourcesPurged, form.IntakeState);
            Assert.Equal(0, store.Count);
            Assert.All(heldBytes.ToArray(), value => Assert.Equal(0, value));
            Assert.Equal("Synthetic title", Assert.Single(form.ResultLines!).Text);
        });

    [Fact]
    public void Module_handoff_preserves_order_but_revokes_green_and_stale_approval()
        => Sta.Run(() =>
        {
            IReadOnlyList<BriefLine> returned =
            [
                new("Second action", BriefRole.Step),
                new("Synthetic imported title", BriefRole.Title),
            ];
            using var form = new ModuleStudioForm(
                reviewRunner: Approve,
                boardIntakeRunner: _ => returned);
            form.Show();
            var review = ButtonByText(form, "Review and approve…");
            review.PerformClick();
            Assert.NotNull(form.ApprovedResult);

            Named<Button>(form, "board-to-brief-intake").PerformClick();

            var lines = FieldGrid(form, "Verified lines and roles");
            var dataRows = lines.Rows.Cast<DataGridViewRow>().Where(row => !row.IsNewRow).ToArray();
            Assert.Equal(2, dataRows.Length);
            Assert.Equal("Second action", dataRows[0].Cells[0].Value);
            Assert.Equal("step", dataRows[0].Cells[1].Value);
            Assert.Equal("Synthetic imported title", dataRows[1].Cells[0].Value);
            Assert.Equal("title", dataRows[1].Cells[1].Value);
            Assert.False(GreenConfirmation(form).Checked);
            Assert.False(review.Enabled);
            Assert.Null(form.ApprovedResult);
            Assert.Contains("Gate B", form.StatusText, StringComparison.Ordinal);
        });

    private static BoardToBriefIntakeForm Intake(
        IntakeFixture fixture,
        IOcrService ocr,
        Action<string, string, MessageBoxIcon>? notice = null)
        => new(
            fixture.Store,
            fixture.Session,
            ocr,
            DistrictPolicy.Offline,
            captureRunner: _ => DialogResult.OK,
            noticePresenter: notice ?? ((_, _, _) => { }));

    private static IntakeFixture GreenFixture(ISessionByteStore store)
        => Fixture(store, DataLane.Green);

    private static IntakeFixture Fixture(ISessionByteStore store, DataLane lane)
    {
        var session = new CaptureSession(
            new ByteImportCaptureSource(store),
            new MetadataOnlyNormalizer(),
            store);
        var captured = session.CaptureAsync(
                new CaptureRequest(ByteImportCaptureSource.Kind, "image/png", TinyPng()),
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        var normalized = session.NormalizeAsync(new NormalizationRequest(), CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        Assert.Equal(captured.Bytes, normalized.Bytes);
        return new IntakeFixture(store, session, session.ConfirmLane(lane));
    }

    private static byte[] TinyPng()
    {
        using var bitmap = new Bitmap(4, 4);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.White);
        }

        using var output = new MemoryStream();
        bitmap.Save(output, ImageFormat.Png);
        return output.ToArray();
    }

    private static OcrResult UncertainRecognition()
        => new(
        [
            Uncertain("Todays", 0),
            Uncertain("plan", 0),
            Uncertain("Open", 1),
            Uncertain("notebook", 1),
        ]);

    private static OcrResult ConfidentRecognition()
        => new(
        [
            new OcrToken("Synthetic title", 1),
            new OcrToken("Open the notebook", 1) { LineIndex = 1 },
        ]);

    private static OcrResult OneConfidentTitle()
        => new([new OcrToken("Synthetic title", 1)]);

    private static OcrToken Uncertain(string text, int line)
        => new(text, 0)
        {
            LineIndex = line,
            ConfidenceAvailable = false,
        };

    private static T Named<T>(Control root, string name)
        where T : Control
        => Controls(root).OfType<T>().Single(control => control.Name == name);

    private static List<Control> Controls(Control root)
        => [.. ReviewSurfaceContractTests.Flatten(root)];

    private static string CurrentUncertain(BoardToBriefIntakeForm form)
        => Named<Label>(form, BoardToBriefIntakeForm.CurrentUncertainName).Text;

    private static Button ButtonByText(Control root, string text)
        => Controls(root).OfType<Button>().Single(button =>
            UiStrings.WithoutMnemonic(button.Text) == text);

    private static DataGridView FieldGrid(ModuleStudioForm form, string accessibleName)
        => Controls(form).OfType<DataGridView>().Single(grid =>
            grid.Parent is GroupBox
            && grid.AccessibilityObject.Name == accessibleName);

    private static CheckBox GreenConfirmation(ModuleStudioForm form)
        => Controls(form).OfType<CheckBox>().Single(check =>
            UiStrings.WithoutMnemonic(check.Text).StartsWith(
                "I confirm these inputs are staged",
                StringComparison.Ordinal));

    private static ApprovedArtifact? Approve(ReviewSession session)
    {
        session.SetRequiredIssuesAcknowledged(true);
        return session.CanApprove
            ? session.Approve("Synthetic test teacher", ApprovalInstant)
            : null;
    }

    private static void PumpUntil(Func<bool> condition)
    {
        var timeout = Stopwatch.StartNew();
        while (!condition())
        {
            if (timeout.Elapsed > TimeSpan.FromSeconds(5))
            {
                throw new TimeoutException("Synthetic Board-to-Brief UI state did not settle.");
            }

            System.Windows.Forms.Application.DoEvents();
            Thread.Sleep(5);
        }
    }

    private sealed record IntakeFixture(
        ISessionByteStore Store,
        CaptureSession Session,
        SourceEnvelope Envelope);

    private sealed class MetadataOnlyNormalizer : IDocumentNormalizer
    {
        public Task<SourceEnvelope> NormalizeAsync(
            SourceEnvelope source,
            NormalizationRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(source with
            {
                MimeType = "image/png",
                MetadataStripped = true,
            });
        }
    }

    private sealed class FixedOcrService(OcrResult result) : IOcrService
    {
        public Task<OcrResult> RecognizeAsync(SourceEnvelope source, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(result);
        }
    }

    private sealed class RecordingOcrService(OcrResult result) : IOcrService
    {
        public int Calls { get; private set; }

        public Task<OcrResult> RecognizeAsync(SourceEnvelope source, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return Task.FromResult(result);
        }
    }

    private sealed class BlockingOcrService(ISessionByteStore? store = null) : IOcrService
    {
        public int Calls { get; private set; }

        public CancellationToken ObservedToken { get; private set; }

        public bool Settled { get; private set; }

        public int? StoreCountAtSettlement { get; private set; }

        public async Task<OcrResult> RecognizeAsync(SourceEnvelope source, CancellationToken cancellationToken)
        {
            Calls++;
            ObservedToken = cancellationToken;
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException("Synthetic OCR cancellation unexpectedly completed.");
            }
            finally
            {
                StoreCountAtSettlement = store?.Count;
                Settled = true;
            }
        }
    }

    private sealed class FaultOnCancellationOcrService : IOcrService
    {
        public int Calls { get; private set; }

        public bool Settled { get; private set; }

        public async Task<OcrResult> RecognizeAsync(SourceEnvelope source, CancellationToken cancellationToken)
        {
            Calls++;
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException("Synthetic OCR cancellation unexpectedly completed.");
            }
            catch (OperationCanceledException)
            {
                Settled = true;
                throw new KeyNotFoundException("Synthetic unexpected OCR terminal fault.");
            }
        }
    }

    private sealed class FailsFirstPurgeStore : ISessionByteStore
    {
        private readonly InMemorySessionByteStore _inner = new();
        private bool _failNextPurge = true;

        public int Count => _inner.Count;

        public SessionByteReference Put(ReadOnlyMemory<byte> content) => _inner.Put(content);

        public bool TryGet(SessionByteReference reference, out ReadOnlyMemory<byte> content)
            => _inner.TryGet(reference, out content);

        public void Release(SessionByteReference reference) => _inner.Release(reference);

        public void PurgeAll()
        {
            if (_failNextPurge)
            {
                _failNextPurge = false;
                throw new IOException("Synthetic first purge refusal.");
            }

            _inner.PurgeAll();
        }
    }
}
