// SPDX-License-Identifier: GPL-3.0-or-later
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Infrastructure.Windows;
using Foundry.Modules.BuiltIn.BoardToBrief;

namespace Foundry.App.WinForms;

/// <summary>
/// Source-verification intake for Board to Brief. This surface owns one
/// session-scoped byte store from capture through source comparison and role
/// assignment. It returns verified rows only after terminal purge; it never
/// creates, reviews, approves, renders, saves, or exports an artifact.
/// </summary>
public sealed class BoardToBriefIntakeForm : Form
{
    private static readonly Lazy<DistrictPolicy> InstalledDistrictPolicy = new(
        static () => new JsonDistrictPolicyProvider().Current,
        isThreadSafe: true);

    internal const string SourceImageName = "board-intake-source-image";
    internal const string CandidateTextName = "board-intake-candidate-text";
    internal const string VerifiedTextName = "board-intake-verified-text";
    internal const string CurrentUncertainName = "board-intake-current-uncertain";
    internal const string ReplacementName = "board-intake-replacement";
    internal const string NextUncertainName = "board-intake-next-uncertain";
    internal const string AcceptCandidateName = "board-intake-accept-candidate";
    internal const string RetypeName = "board-intake-retype";
    internal const string MarkIllegibleName = "board-intake-mark-illegible";
    internal const string ManualInputName = "board-intake-manual-transcript";
    internal const string UseManualName = "board-intake-use-manual";
    internal const string RoleGridName = "board-intake-role-grid";
    internal const string MoveUpName = "board-intake-move-up";
    internal const string MoveDownName = "board-intake-move-down";
    internal const string FinishName = "board-intake-finish";
    internal const string SafetyPauseName = "board-intake-safety-pause";
    internal const string RetryPurgeName = "board-intake-retry-purge";
    internal const string CancelName = "board-intake-cancel";
    internal const string StatusName = "board-intake-status";

    private enum PendingExit
    {
        None,
        Success,
        Cancel,
        SafetyPause,
    }

    private sealed record OwnedDependencies(
        ISessionByteStore Store,
        CaptureSession Session,
        IOcrService Ocr,
        DistrictPolicy Policy);

    private sealed record RoleChoice(BriefRole Role, string Text)
    {
        public override string ToString() => Text;
    }

    private readonly ISessionByteStore _store;
    private readonly CaptureSession _session;
    private readonly IOcrService _ocr;
    private readonly DistrictPolicy _policy;
    private readonly Func<IWin32Window, DialogResult> _captureRunner;
    private readonly Action<string, string, MessageBoxIcon> _presentNotice;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly PictureBox _sourceImage;
    private readonly TextBox _candidateText;
    private readonly TextBox _verifiedText;
    private readonly Label _currentUncertain;
    private readonly TextBox _replacement;
    private readonly Button _nextUncertain;
    private readonly Button _acceptCandidate;
    private readonly Button _retype;
    private readonly Button _markIllegible;
    private readonly TextBox _manualInput;
    private readonly Button _useManual;
    private readonly DataGridView _roleGrid;
    private readonly Button _moveUp;
    private readonly Button _moveDown;
    private readonly Button _finish;
    private readonly Button _safetyPause;
    private readonly Button _retryPurge;
    private readonly Button _cancel;
    private readonly Label _status;
    private readonly Panel _workSurface;
    private readonly IReadOnlyList<RoleChoice> _roleChoices;
    private TranscriptSession? _transcript;
    private Task<OcrResult>? _ocrTask;
    private Task? _disposalCleanup;
    private int _currentUncertainIndex = -1;
    private IReadOnlyList<BriefLine>? _pendingLines;
    private Action? _afterSuccessfulPurge;
    private PendingExit _pendingExit;
    private bool _shown;
    private bool _stopping;
    private bool _purgeRecovery;
    private bool _allowClose;
    private bool _disposed;

    public BoardToBriefIntakeForm()
        : this(CreateOwnedDependencies())
    {
    }

    private BoardToBriefIntakeForm(OwnedDependencies dependencies)
        : this(
            dependencies.Store,
            dependencies.Session,
            dependencies.Ocr,
            dependencies.Policy,
            captureRunner: null)
    {
    }

    /// <summary>Deterministic seam for in-process proof; production always opens CaptureForm.</summary>
    internal BoardToBriefIntakeForm(
        ISessionByteStore store,
        CaptureSession session,
        IOcrService ocr,
        DistrictPolicy policy,
        Func<IWin32Window, DialogResult>? captureRunner,
        Action<string, string, MessageBoxIcon>? noticePresenter = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _ocr = ocr ?? throw new ArgumentNullException(nameof(ocr));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _captureRunner = captureRunner ?? RunCapture;
        _presentNotice = noticePresenter ?? ((message, caption, icon) => MessageBox.Show(
            this,
            message,
            caption,
            MessageBoxButtons.OK,
            icon));
        _roleChoices =
        [
            new(BriefRole.Title, UiStrings.WithoutMnemonic(UiStrings.BoardIntakeRoleTitle)),
            new(BriefRole.Step, UiStrings.WithoutMnemonic(UiStrings.BoardIntakeRoleStep)),
            new(BriefRole.Material, UiStrings.WithoutMnemonic(UiStrings.BoardIntakeRoleMaterial)),
            new(BriefRole.Vocabulary, UiStrings.WithoutMnemonic(UiStrings.BoardIntakeRoleVocabulary)),
            new(BriefRole.Date, UiStrings.WithoutMnemonic(UiStrings.BoardIntakeRoleDate)),
            new(BriefRole.Note, UiStrings.WithoutMnemonic(UiStrings.BoardIntakeRoleNote)),
        ];

        Text = UiStrings.WithoutMnemonic(UiStrings.BoardIntakeWindowTitle);
        MinimumSize = new Size(960, 560);
        Size = new Size(1180, 720);
        AutoScaleMode = AutoScaleMode.Dpi;

        _sourceImage = new PictureBox
        {
            Name = SourceImageName,
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            SizeMode = PictureBoxSizeMode.Zoom,
            AccessibleName = UiStrings.WithoutMnemonic(UiStrings.BoardIntakeSourceImage),
        };
        _candidateText = ReadOnlyText(CandidateTextName, UiStrings.BoardIntakeCandidateText);
        _verifiedText = ReadOnlyText(VerifiedTextName, UiStrings.BoardIntakeVerifiedText);
        _currentUncertain = new Label
        {
            Name = CurrentUncertainName,
            AutoSize = true,
            UseMnemonic = false,
            Text = UiStrings.WithoutMnemonic(UiStrings.BoardIntakeNoCurrentUncertain),
            AccessibleName = UiStrings.WithoutMnemonic(UiStrings.BoardIntakeNoCurrentUncertain),
        };
        _replacement = new TextBox
        {
            Name = ReplacementName,
            AccessibleName = UiStrings.WithoutMnemonic(UiStrings.BoardIntakeReplacement),
            Width = 280,
        };
        _nextUncertain = MakeButton(NextUncertainName, UiStrings.BoardIntakeNextUncertain, (_, _) => SelectNextUncertain());
        _acceptCandidate = MakeButton(AcceptCandidateName, UiStrings.BoardIntakeAcceptCandidate, (_, _) => AcceptCandidate());
        _retype = MakeButton(RetypeName, UiStrings.BoardIntakeRetype, (_, _) => Retype());
        _markIllegible = MakeButton(MarkIllegibleName, UiStrings.BoardIntakeMarkIllegible, (_, _) => MarkIllegible());
        _manualInput = new TextBox
        {
            Name = ManualInputName,
            Multiline = true,
            AcceptsReturn = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            AccessibleName = UiStrings.WithoutMnemonic(UiStrings.BoardIntakeManualInput),
        };
        _useManual = MakeButton(UseManualName, UiStrings.BoardIntakeUseManual, (_, _) => UseManualTranscript());
        _roleGrid = CreateRoleGrid();
        _moveUp = MakeButton(MoveUpName, UiStrings.BoardIntakeMoveLineUp, (_, _) => MoveSelectedLine(-1));
        _moveDown = MakeButton(MoveDownName, UiStrings.BoardIntakeMoveLineDown, (_, _) => MoveSelectedLine(1));
        _finish = MakeButton(FinishName, UiStrings.BoardIntakeFinish, (_, _) => Finish());
        _safetyPause = MakeButton(SafetyPauseName, UiStrings.SafetyPause, async (_, _) => await RequestTerminalAsync(PendingExit.SafetyPause));
        _retryPurge = MakeButton(RetryPurgeName, UiStrings.RetrySecurePurge, (_, _) => RetryPurge());
        _cancel = MakeButton(CancelName, UiStrings.BoardIntakeCancel, async (_, _) => await RequestTerminalAsync(PendingExit.Cancel));
        _retryPurge.Visible = false;
        _status = new Label
        {
            Name = StatusName,
            Dock = DockStyle.Bottom,
            AutoSize = false,
            Height = 38,
            UseMnemonic = false,
        };

        _workSurface = BuildWorkSurface();
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            WrapContents = true,
            Padding = new Padding(8),
        };
        actions.Controls.AddRange([_finish, _safetyPause, _retryPurge, _cancel]);

        Controls.Add(_workSurface);
        Controls.Add(actions);
        Controls.Add(_status);
        SetStatus(UiStrings.StatusBoardIntakeStarting);
        UpdateControlState();
        UiLocale.ApplyChrome(this);
    }

    public IReadOnlyList<BriefLine>? ResultLines { get; private set; }

    internal JobState IntakeState => _session.Machine.State;

    internal string StatusText => _status.Text;

    internal bool OperationPending => _ocrTask is { IsCompleted: false };

    internal Task CaptureWork { get; private set; } = Task.CompletedTask;

    internal Task DisposalCleanup => _disposalCleanup ?? Task.CompletedTask;

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (_shown)
        {
            return;
        }

        _shown = true;
        BeginInvoke(new Action(() =>
        {
            CaptureWork = CaptureAndRecognizeFailClosedAsync();
            ObserveFault(CaptureWork);
        }));
    }

    private async Task CaptureAndRecognizeFailClosedAsync()
    {
        try
        {
            await CaptureAndRecognizeAsync();
        }
        catch (Exception)
        {
            // The top-level shown operation cannot disclose arbitrary provider
            // or control exceptions, and it cannot leave source bytes behind
            // an apparently usable form. Reuse the ordinary terminal boundary
            // so OCR settles before the session is purged and closed.
            SetStatus(UiStrings.StatusBoardIntakeImageUnavailable);
            await RequestTerminalAsync(PendingExit.Cancel);
        }
    }

    private static OwnedDependencies CreateOwnedDependencies()
    {
        var store = new InMemorySessionByteStore();
        return new OwnedDependencies(
            store,
            new CaptureSession(new ByteImportCaptureSource(store), new ImageNormalizer(store), store),
            new WindowsOcrService(store),
            InstalledDistrictPolicy.Value);
    }

    private DialogResult RunCapture(IWin32Window owner)
    {
        using var capture = new CaptureForm(
            _session,
            _policy,
            CaptureCompletionMode.RetainForOwner);
        return capture.ShowDialog(owner);
    }

    private async Task CaptureAndRecognizeAsync()
    {
        if (_disposed || _stopping)
        {
            return;
        }

        DialogResult captureResult;
        try
        {
            captureResult = _captureRunner(this);
        }
        catch (Exception)
        {
            SetStatus(UiStrings.StatusBoardIntakeImageUnavailable);
            await RequestTerminalAsync(PendingExit.Cancel);
            return;
        }

        if (captureResult != DialogResult.OK)
        {
            await RequestTerminalAsync(PendingExit.Cancel);
            return;
        }

        var envelope = _session.Envelope;
        if (envelope is null)
        {
            SetStatus(UiStrings.StatusBoardIntakeImageUnavailable);
            await RequestTerminalAsync(PendingExit.Cancel);
            return;
        }

        if (envelope.Lane != DataLane.Green)
        {
            SetStatus(UiStrings.StatusBoardIntakeAmberRefused);
            await RequestTerminalAsync(PendingExit.Cancel, () => _presentNotice(
                UiStrings.WithoutMnemonic(UiStrings.StatusBoardIntakeAmberRefused),
                UiStrings.WithoutMnemonic(UiStrings.BoardIntakeWindowTitle),
                MessageBoxIcon.Warning));
            return;
        }

        if (!TryLoadSourcePreview(envelope))
        {
            SetStatus(UiStrings.StatusBoardIntakeImageUnavailable);
            await RequestTerminalAsync(PendingExit.Cancel);
            return;
        }

        SetStatus(UiStrings.StatusBoardIntakeOcrRunning);
        var lifetimeToken = _lifetimeCancellation.Token;
        try
        {
            _ocrTask = _ocr.RecognizeAsync(envelope, lifetimeToken);
            UpdateControlState();
            var recognition = await _ocrTask;
            if (!_stopping)
            {
                StartTranscript(recognition);
                SetStatus(_transcript!.IsComplete
                    ? UiStrings.StatusBoardIntakeRolesReady
                    : UiStrings.StatusBoardIntakeOcrReady);
            }
        }
        catch (OperationCanceledException) when (lifetimeToken.IsCancellationRequested)
        {
            // The terminal requester awaits this task before purging the store.
        }
        catch (Exception) when (_stopping && lifetimeToken.IsCancellationRequested)
        {
            // A terminal requester independently awaits and contains the OCR
            // task before purging. The same fault must not also escape this
            // shown-form operation through its async WinForms dispatcher.
        }
        catch (Exception failure) when (IsExpectedOcrFailure(failure))
        {
            if (!_stopping)
            {
                SetStatus(UiStrings.StatusBoardIntakeOcrFallback, failure.Message);
            }
        }
        finally
        {
            _ocrTask = null;
            UpdateControlState();
        }
    }

    private bool TryLoadSourcePreview(SourceEnvelope envelope)
    {
        if (!_store.TryGet(envelope.Bytes, out var stored))
        {
            return false;
        }

        var ownedBytes = stored.ToArray();
        try
        {
            using var stream = new MemoryStream(ownedBytes, writable: false);
            using var decoded = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: true);
            ReplaceSourceImage(new Bitmap(decoded));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (ExternalException)
        {
            return false;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(ownedBytes);
        }
    }

    private void ReplaceSourceImage(Image? replacement)
    {
        var previous = _sourceImage.Image;
        _sourceImage.Image = replacement;
        if (previous is null)
        {
            return;
        }

        try
        {
            using var graphics = Graphics.FromImage(previous);
            graphics.Clear(Color.Black);
        }
        catch (ArgumentException)
        {
            // Disposal still releases a non-writable decoder-owned image.
        }
        catch (ExternalException)
        {
            // Disposal still releases an unavailable GDI surface.
        }
        finally
        {
            previous.Dispose();
        }
    }

    private void StartTranscript(OcrResult recognition)
    {
        _transcript = new TranscriptSession(recognition);
        _currentUncertainIndex = -1;
        _roleGrid.Rows.Clear();
        RefreshTranscriptViews();
        if (_transcript.IsComplete)
        {
            PopulateRoleRows();
            return;
        }

        SelectNextUncertain();
    }

    private void UseManualTranscript()
    {
        var lines = _manualInput.Text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
        // One ordinary final newline is formatting, not a source line. Any
        // other empty/whitespace line is ambiguous and must be corrected.
        if (lines.Length > 0 && lines[^1].Length == 0)
        {
            lines = [.. lines.Take(lines.Length - 1)];
        }

        if (lines.Length == 0)
        {
            SetStatus(UiStrings.StatusBoardIntakeManualRequired);
            return;
        }

        if (lines.Any(string.IsNullOrWhiteSpace))
        {
            SetStatus(UiStrings.StatusBoardIntakeManualWhitespace);
            return;
        }

        var tokens = lines.Select((line, index) => new OcrToken(line, 0)
        {
            LineIndex = index,
            ConfidenceAvailable = false,
            LayoutMetadataAvailable = true,
            LeadingText = string.Empty,
            TrailingText = string.Empty,
        }).ToArray();
        StartTranscript(new OcrResult(tokens));
        SetStatus(UiStrings.StatusBoardIntakeManualLoaded);
    }

    private void SelectNextUncertain()
    {
        if (_transcript is null)
        {
            return;
        }

        _currentUncertainIndex = _transcript.NextUnresolvedIndex(_currentUncertainIndex)
            ?? _transcript.NextUnresolvedIndex()
            ?? -1;
        RefreshTranscriptViews();
        if (_currentUncertainIndex >= 0)
        {
            _replacement.Focus();
        }
    }

    private void AcceptCandidate()
    {
        if (!TryCurrentToken(out var token))
        {
            return;
        }

        _transcript!.Resolve(_currentUncertainIndex, token.RecognizedText);
        ResolveAndAdvance();
    }

    private void Retype()
    {
        if (!TryCurrentToken(out _))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_replacement.Text))
        {
            SetStatus(UiStrings.StatusBoardIntakeRetypeRequired);
            _replacement.Focus();
            return;
        }

        _transcript!.Resolve(_currentUncertainIndex, _replacement.Text);
        ResolveAndAdvance();
    }

    private void MarkIllegible()
    {
        if (!TryCurrentToken(out _))
        {
            return;
        }

        _transcript!.MarkIllegible(_currentUncertainIndex);
        ResolveAndAdvance();
    }

    private bool TryCurrentToken(out TranscriptToken token)
    {
        token = default!;
        if (_transcript is null
            || _currentUncertainIndex < 0
            || _currentUncertainIndex >= _transcript.Tokens.Count)
        {
            return false;
        }

        token = _transcript.Tokens[_currentUncertainIndex];
        return token.State == TranscriptTokenState.Uncertain;
    }

    private void ResolveAndAdvance()
    {
        _replacement.Clear();
        _currentUncertainIndex = _transcript!.NextUnresolvedIndex(_currentUncertainIndex)
            ?? _transcript.NextUnresolvedIndex()
            ?? -1;
        RefreshTranscriptViews();
        if (_transcript.IsComplete)
        {
            PopulateRoleRows();
            SetStatus(UiStrings.StatusBoardIntakeRolesReady);
        }
    }

    private void RefreshTranscriptViews()
    {
        if (_transcript is null)
        {
            _candidateText.Clear();
            _verifiedText.Clear();
            SetCurrentUncertain(null);
            UpdateControlState();
            return;
        }

        _candidateText.Text = RenderTokenLines(_transcript, verified: false);
        _verifiedText.Text = RenderTokenLines(_transcript, verified: true);
        SetCurrentUncertain(TryCurrentToken(out var token) ? token : null);
        UpdateControlState();
    }

    private static string RenderTokenLines(TranscriptSession transcript, bool verified)
    {
        var lines = transcript.ProjectLines(token => verified
            ? token.State switch
            {
                TranscriptTokenState.Resolved => token.ResolvedText!,
                TranscriptTokenState.Illegible => TranscriptSession.IllegibleMarker,
                TranscriptTokenState.Uncertain => UiStrings.FormatWithoutMnemonic(
                    UiStrings.BoardIntakeUnresolvedMarker,
                    token.RecognizedText),
                _ => token.RecognizedText,
            }
            : token.RecognizedText);

        return string.Join(Environment.NewLine, lines);
    }

    private void SetCurrentUncertain(TranscriptToken? token)
    {
        var text = token is null
            ? UiStrings.WithoutMnemonic(UiStrings.BoardIntakeNoCurrentUncertain)
            : UiStrings.FormatWithoutMnemonic(
                UiStrings.BoardIntakeCurrentUncertain,
                _currentUncertainIndex + 1,
                token.RecognizedText);
        _currentUncertain.Text = text;
        _currentUncertain.AccessibleName = text;
    }

    private void PopulateRoleRows()
    {
        if (_transcript is null || !_transcript.IsComplete)
        {
            return;
        }

        _roleGrid.Rows.Clear();
        foreach (var line in _transcript.VerifiedLines())
        {
            _roleGrid.Rows.Add(line);
        }
        if (_roleGrid.Rows.Count > 0)
        {
            _roleGrid.CurrentCell = _roleGrid.Rows[0].Cells[0];
        }
        UpdateControlState();
    }

    private void MoveSelectedLine(int offset)
    {
        if (_roleGrid.CurrentRow is not { IsNewRow: false } selected
            || _roleGrid.SelectedRows.Count > 1)
        {
            SetStatus(UiStrings.StatusBoardIntakeLineSelectionRequired);
            return;
        }

        var targetIndex = selected.Index + offset;
        if (targetIndex < 0 || targetIndex >= _roleGrid.Rows.Count)
        {
            SetStatus(UiStrings.StatusBoardIntakeLineBoundary);
            return;
        }

        var text = selected.Cells[0].Value;
        var role = selected.Cells[1].Value;
        _roleGrid.Rows.RemoveAt(selected.Index);
        _roleGrid.Rows.Insert(targetIndex, 1);
        _roleGrid.Rows[targetIndex].Cells[0].Value = text;
        _roleGrid.Rows[targetIndex].Cells[1].Value = role;
        _roleGrid.CurrentCell = _roleGrid.Rows[targetIndex].Cells[0];
        _roleGrid.Rows[targetIndex].Selected = true;
        UpdateControlState();
    }

    private void Finish()
    {
        if (_transcript is null || !_transcript.IsComplete || _roleGrid.Rows.Count == 0)
        {
            return;
        }

        if ((_roleGrid.IsCurrentCellDirty
                && !_roleGrid.CommitEdit(DataGridViewDataErrorContexts.Commit))
            || !_roleGrid.EndEdit())
        {
            SetStatus(UiStrings.StatusBoardIntakeGridError);
            return;
        }

        var lines = new List<BriefLine>(_roleGrid.Rows.Count);
        foreach (DataGridViewRow row in _roleGrid.Rows)
        {
            if (row.Cells[1].Value is not BriefRole role)
            {
                SetStatus(UiStrings.StatusBoardIntakeRoleRequired);
                _roleGrid.CurrentCell = row.Cells[1];
                return;
            }

            var text = row.Cells[0].Value?.ToString();
            if (string.IsNullOrWhiteSpace(text))
            {
                SetStatus(UiStrings.StatusBoardIntakeRoleRequired);
                _roleGrid.CurrentCell = row.Cells[0];
                return;
            }

            lines.Add(new BriefLine(text, role));
        }

        if (lines.Count(line => line.Role == BriefRole.Title) != 1)
        {
            SetStatus(UiStrings.StatusBoardIntakeOneTitleRequired);
            return;
        }

        _pendingLines = Array.AsReadOnly(lines.ToArray());
        _pendingExit = PendingExit.Success;
        _stopping = true;
        ClearTransientReviewState();
        if (!_session.CompleteCapture())
        {
            EnterPurgeRecovery();
            return;
        }

        CompleteExit(PendingExit.Success);
    }

    private async Task RequestTerminalAsync(PendingExit exit, Action? afterSuccessfulPurge = null)
    {
        if (_stopping && !_purgeRecovery)
        {
            return;
        }

        if (_purgeRecovery)
        {
            SetStatus(UiStrings.StatusBoardIntakePurgeIncomplete);
            return;
        }

        _stopping = true;
        _pendingExit = exit;
        _afterSuccessfulPurge = afterSuccessfulPurge;
        _pendingLines = null;
        UpdateControlState();
        try
        {
            await _lifetimeCancellation.CancelAsync();
        }
        catch (Exception)
        {
            // Cancellation callbacks are outside this form's control. The token
            // has still been marked; OCR settlement remains the purge boundary.
        }

        var activeOcr = _ocrTask;
        if (activeOcr is not null)
        {
            try
            {
                await activeOcr;
            }
            catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
            {
            }
            catch (Exception)
            {
                // The user is leaving; settlement, not OCR success, gates purge.
            }
        }

        if (_disposed)
        {
            // Forced disposal owns terminal cleanup and cannot race WinForms
            // controls or a second session transition after this await.
            return;
        }

        ClearTransientReviewState();
        bool purged;
        if (exit == PendingExit.SafetyPause)
        {
            var result = _session.InvokeSafetyPause(_policy);
            purged = _session.Machine.State == JobState.TransientSourcesPurged;
            _presentNotice(
                result.ProcedureText,
                UiStrings.WithoutMnemonic(UiStrings.PauseCaption),
                MessageBoxIcon.Information);
        }
        else
        {
            _session.Cancel();
            purged = _session.Machine.State == JobState.TransientSourcesPurged;
        }

        if (!purged)
        {
            EnterPurgeRecovery();
            return;
        }

        RunAfterSuccessfulPurge();
        CompleteExit(exit);
    }

    private void EnterPurgeRecovery()
    {
        _purgeRecovery = true;
        _stopping = true;
        ResultLines = null;
        SetStatus(UiStrings.StatusBoardIntakePurgeIncomplete);
        UpdateControlState();
    }

    private void RetryPurge()
    {
        if (!_purgeRecovery || _session.Machine.State != JobState.PurgeIncomplete)
        {
            return;
        }

        if (!_session.PurgeTransientSources())
        {
            SetStatus(UiStrings.StatusBoardIntakePurgeIncomplete);
            return;
        }

        _purgeRecovery = false;
        RunAfterSuccessfulPurge();
        CompleteExit(_pendingExit);
    }

    private void RunAfterSuccessfulPurge()
    {
        var afterPurge = _afterSuccessfulPurge;
        _afterSuccessfulPurge = null;
        afterPurge?.Invoke();
    }

    private void ClearTransientReviewState()
    {
        ReplaceSourceImage(null);
        _transcript = null;
        _currentUncertainIndex = -1;
        _candidateText.Clear();
        _verifiedText.Clear();
        _replacement.Clear();
        _replacement.ClearUndo();
        _manualInput.Clear();
        _manualInput.ClearUndo();
        _roleGrid.CancelEdit();
        _roleGrid.Rows.Clear();
        SetCurrentUncertain(null);

        // Verified text necessarily occupies managed strings while the teacher
        // compares and edits it. Raw encoded bytes are zeroed and images are
        // overwritten/disposed explicitly; dropping every control/session
        // reference here bounds the remaining strings to ordinary GC lifetime.
    }

    private void CompleteExit(PendingExit exit)
    {
        if (_session.Machine.State != JobState.TransientSourcesPurged)
        {
            EnterPurgeRecovery();
            return;
        }

        _allowClose = true;
        ResultLines = exit == PendingExit.Success ? _pendingLines : null;
        if (exit == PendingExit.Success)
        {
            SetStatus(UiStrings.StatusBoardIntakeReturned);
            DialogResult = DialogResult.OK;
        }
        else
        {
            DialogResult = exit == PendingExit.SafetyPause ? DialogResult.Abort : DialogResult.Cancel;
        }
        Close();
    }

    private void UpdateControlState()
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        var active = !_stopping && !_purgeRecovery;
        var ocrPending = _ocrTask is { IsCompleted: false };
        var hasCurrent = active && !ocrPending && TryCurrentToken(out _);
        var transcriptComplete = active && !ocrPending && _transcript?.IsComplete == true;
        _workSurface.Enabled = active && !ocrPending;
        _manualInput.Enabled = active && !ocrPending;
        _useManual.Enabled = active && !ocrPending;
        _replacement.Enabled = hasCurrent;
        _nextUncertain.Enabled = active && !ocrPending && _transcript?.UnresolvedCount > 0;
        _acceptCandidate.Enabled = hasCurrent;
        _retype.Enabled = hasCurrent;
        _markIllegible.Enabled = hasCurrent;
        _roleGrid.Enabled = transcriptComplete;
        _moveUp.Enabled = transcriptComplete && _roleGrid.Rows.Count > 1;
        _moveDown.Enabled = transcriptComplete && _roleGrid.Rows.Count > 1;
        _finish.Enabled = transcriptComplete && _roleGrid.Rows.Count > 0;
        _safetyPause.Visible = true;
        _safetyPause.Enabled = active && JobStateMachine.CanTransition(_session.Machine.State, JobState.Blocked);
        _cancel.Enabled = active;
        _retryPurge.Visible = _purgeRecovery;
        _retryPurge.Enabled = _purgeRecovery;
    }

    private Panel BuildWorkSurface()
    {
        var intro = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            UseMnemonic = false,
            Text = UiStrings.WithoutMnemonic(UiStrings.BoardIntakeIntroduction),
            AccessibleName = UiStrings.WithoutMnemonic(UiStrings.BoardIntakeIntroduction),
        };
        var sourceGroup = Group(UiStrings.BoardIntakeSourceImage, _sourceImage);
        var candidateGroup = Group(UiStrings.BoardIntakeCandidateText, _candidateText);
        var verifiedGroup = Group(UiStrings.BoardIntakeVerifiedText, _verifiedText);

        var uncertainActions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        uncertainActions.Controls.AddRange([
            _currentUncertain,
            new Label { Text = UiStrings.BoardIntakeReplacement, AutoSize = true, Anchor = AnchorStyles.Left },
            _replacement,
            _nextUncertain,
            _acceptCandidate,
            _retype,
            _markIllegible,
        ]);

        var manualLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        manualLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        manualLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        manualLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        manualLayout.Controls.Add(new Label
        {
            Text = UiStrings.BoardIntakeManualInstructions,
            AutoSize = true,
            MaximumSize = new Size(530, 0),
            UseMnemonic = false,
        }, 0, 0);
        manualLayout.Controls.Add(_manualInput, 0, 1);
        manualLayout.Controls.Add(_useManual, 0, 2);

        var comparison = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 3,
            MinimumSize = new Size(0, 350),
        };
        comparison.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        comparison.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        comparison.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
        comparison.RowStyles.Add(new RowStyle(SizeType.Percent, 32));
        comparison.RowStyles.Add(new RowStyle(SizeType.Percent, 26));
        comparison.Controls.Add(sourceGroup, 0, 0);
        comparison.SetRowSpan(sourceGroup, 3);
        comparison.Controls.Add(candidateGroup, 1, 0);
        comparison.Controls.Add(verifiedGroup, 1, 1);
        // At the hardware floor this cell is intentionally a viewport, not a
        // request to compress two workflows into a few remaining pixels. The
        // explicit virtual height gives both the wrapping uncertainty grammar
        // and the manual fallback a nonzero layout; AutoScroll keeps every
        // standard control keyboard-reachable at neutral and expanded chrome.
        var lowerRight = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            AutoScroll = true,
            AutoScrollMinSize = new Size(0, 260),
        };
        lowerRight.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        lowerRight.RowStyles.Add(new RowStyle(SizeType.Absolute, 148));
        lowerRight.Controls.Add(uncertainActions, 0, 0);
        var manualGroup = Group(UiStrings.BoardIntakeManualTranscript, manualLayout);
        lowerRight.Controls.Add(manualGroup, 0, 1);
        comparison.Controls.Add(lowerRight, 1, 2);
        var comparisonViewport = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
        };
        comparisonViewport.Controls.Add(comparison);
        comparisonViewport.Layout += (_, _) =>
        {
            var needsVerticalScroll = comparison.MinimumSize.Height > comparisonViewport.ClientSize.Height;
            var scrollBarWidth = needsVerticalScroll
                ? SystemInformation.VerticalScrollBarWidth
                : 0;
            var widestCaptionColumn = new[] { sourceGroup, candidateGroup, verifiedGroup, manualGroup }
                .Max(RequiredGroupWidth);
            var desired = new Size(
                Math.Max(
                    checked(widestCaptionColumn * 2),
                    Math.Max(1, comparisonViewport.ClientSize.Width - scrollBarWidth)),
                Math.Max(comparison.MinimumSize.Height, comparisonViewport.ClientSize.Height));
            if (comparison.Size != desired)
            {
                comparison.Size = desired;
            }
        };

        var roleActions = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, WrapContents = true };
        roleActions.Controls.AddRange([_moveUp, _moveDown]);
        var rolePanel = new Panel { Dock = DockStyle.Fill };
        rolePanel.Controls.Add(_roleGrid);
        rolePanel.Controls.Add(roleActions);
        _roleGrid.Dock = DockStyle.Fill;
        var roles = Group(UiStrings.BoardIntakeLineRoles, rolePanel);

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(8),
        };
        body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        // Preserve a useful comparison canvas at its own workflow boundary.
        // At the neutral default/floor the row is already taller, so no
        // scrollbar is manufactured; expanded chrome scrolls inside the
        // comparison rather than turning the whole form into a fixed canvas.
        comparisonViewport.Margin = new Padding(3, 3, 24, 3);
        roles.MinimumSize = new Size(0, 160);
        // Keep the right gutter available for a scrollbar without spending the
        // same width again below the grid. The smaller decorative bottom inset
        // leaves the comparison row enough typography slack at the default
        // window size while preserving the role grid's full 160 px minimum.
        roles.Margin = new Padding(3, 3, 24, 8);
        body.Controls.Add(intro, 0, 0);
        body.Controls.Add(comparisonViewport, 0, 1);
        body.Controls.Add(roles, 0, 2);
        body.Layout += (_, _) =>
        {
            var scrollBarWidth = body.VerticalScroll.Visible
                ? SystemInformation.VerticalScrollBarWidth
                : 0;
            var availableWidth = Math.Max(
                1,
                body.ClientSize.Width
                    - body.Padding.Horizontal
                    - intro.Margin.Horizontal
                    - scrollBarWidth);
            if (intro.MaximumSize.Width != availableWidth)
            {
                intro.MaximumSize = new Size(availableWidth, 0);
            }
        };
        return new Panel { Dock = DockStyle.Fill, Controls = { body } };
    }

    private DataGridView CreateRoleGrid()
    {
        var grid = new DataGridView
        {
            Name = RoleGridName,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToOrderColumns = false,
            RowHeadersVisible = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            AccessibleName = UiStrings.WithoutMnemonic(UiStrings.BoardIntakeLineRoles),
        };
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "text",
            HeaderText = UiStrings.WithoutMnemonic(UiStrings.BoardIntakeLineColumn),
            ReadOnly = false,
            FillWeight = 75,
        });
        grid.Columns.Add(new DataGridViewComboBoxColumn
        {
            Name = "role",
            HeaderText = UiStrings.WithoutMnemonic(UiStrings.BoardIntakeRoleColumn),
            DataSource = _roleChoices.ToList(),
            DisplayMember = nameof(RoleChoice.Text),
            ValueMember = nameof(RoleChoice.Role),
            ValueType = typeof(BriefRole),
            FlatStyle = FlatStyle.Flat,
            FillWeight = 25,
        });
        grid.DataError += (_, e) =>
        {
            e.ThrowException = false;
            SetStatus(UiStrings.StatusBoardIntakeGridError);
        };
        grid.SelectionChanged += (_, _) => UpdateControlState();
        return grid;
    }

    private static TextBox ReadOnlyText(string name, string accessibleName)
        => new()
        {
            Name = name,
            ReadOnly = true,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Dock = DockStyle.Fill,
            AccessibleName = UiStrings.WithoutMnemonic(accessibleName),
        };

    private static Button MakeButton(string name, string text, EventHandler click)
    {
        var button = new Button
        {
            Name = name,
            Text = text,
            AutoSize = true,
            AccessibleName = UiStrings.WithoutMnemonic(text),
        };
        button.Click += click;
        return button;
    }

    private static GroupBox Group(string text, Control content)
    {
        var group = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = text.Replace("&", "&&", StringComparison.Ordinal),
            AccessibleName = UiStrings.WithoutMnemonic(text),
            Padding = new Padding(8),
        };
        group.Controls.Add(content);
        content.Dock = DockStyle.Fill;
        return group;
    }

    private static int RequiredGroupWidth(GroupBox group)
    {
        var caption = group.AccessibleName ?? UiStrings.WithoutMnemonic(group.Text);
        var required = TextRenderer.MeasureText(
            caption,
            group.Font,
            Size.Empty,
            TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        return required.Width + group.Margin.Horizontal + 16;
    }

    private void SetStatus(string template, params object?[] arguments)
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        var text = UiStrings.FormatWithoutMnemonic(template, arguments);
        _status.Text = text;
        _status.AccessibleName = text;
    }

    private static bool IsExpectedOcrFailure(Exception failure)
        => failure is InvalidOperationException
            or IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException
            or ExternalException
            or System.ComponentModel.Win32Exception;

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_allowClose)
        {
            base.OnFormClosing(e);
            return;
        }

        e.Cancel = true;
        if (_purgeRecovery)
        {
            SetStatus(UiStrings.StatusBoardIntakePurgeIncomplete);
        }
        else if (!_stopping)
        {
            BeginInvoke(new Action(async () => await RequestTerminalAsync(PendingExit.Cancel)));
        }
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            if (_allowClose)
            {
                ReplaceSourceImage(null);
                _lifetimeCancellation.Dispose();
            }
            else
            {
                // Dispose() does not raise FormClosing. Withhold every result,
                // clear UI-held source material while controls still exist, and
                // let OCR settle before touching its session-owned byte store.
                _stopping = true;
                _pendingExit = PendingExit.Cancel;
                _pendingLines = null;
                _afterSuccessfulPurge = null;
                ResultLines = null;
                ClearTransientReviewState();

                var activeOcr = _ocrTask;
                _disposalCleanup = CompleteForcedDisposalAsync(activeOcr);
                ObserveFault(_disposalCleanup);
            }
        }
        base.Dispose(disposing);
    }

    private async Task CompleteForcedDisposalAsync(Task<OcrResult>? activeOcr)
    {
        try
        {
            try
            {
                await _lifetimeCancellation.CancelAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                // A callback fault cannot suppress settlement or secure purge.
            }

            if (activeOcr is not null)
            {
                try
                {
                    await activeOcr.ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // The service is settled; its success is irrelevant here.
                }
            }

            PurgeAfterForcedDisposal();
        }
        finally
        {
            _lifetimeCancellation.Dispose();
        }
    }

    private void PurgeAfterForcedDisposal()
    {
        try
        {
            var state = _session.Machine.State;
            if (state == JobState.TransientSourcesPurged)
            {
                return;
            }

            if (state is JobState.Completed
                or JobState.Cancelled
                or JobState.Blocked
                or JobState.Declined
                or JobState.PurgeIncomplete)
            {
                _session.PurgeTransientSources();
            }
            else
            {
                _session.Cancel();
            }

            // There is no recovery surface after forced disposal. Retry once,
            // then leave PurgeIncomplete truthful if the store still refuses.
            if (_session.Machine.State == JobState.PurgeIncomplete)
            {
                _session.PurgeTransientSources();
            }
        }
        catch (InvalidOperationException)
        {
            // A concurrent terminal requester won the state transition. Its
            // state remains the authoritative evidence; never claim success.
        }
    }

    private static void ObserveFault(Task work)
        => _ = work.ContinueWith(
            static completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
}
