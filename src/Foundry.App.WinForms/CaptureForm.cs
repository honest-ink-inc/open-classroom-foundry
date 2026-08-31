// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Infrastructure.Windows;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Foundry.App.WinForms;

internal enum CaptureCompletionMode
{
    PurgeOnCompletion,
    RetainForOwner,
}

/// <summary>
/// Capture surface — prototype, standard controls only (ADR-002). All behavior
/// lives in <see cref="CaptureSession"/>; this form binds it. The safety-pause
/// control implements the Gate C design's UI criteria: always visible, keyboard
/// reachable, and worded as the adult's own observation — never a detection
/// claim. Pending before any pilot: NVDA/Narrator walkthrough, crop/redaction
/// drawing, and live camera wiring.
/// </summary>
public sealed class CaptureForm : Form
{
    private readonly CaptureSession _session;
    private readonly DistrictPolicy _policy;
    private readonly Action<SafetyPauseResult> _presentSafetyPause;
    private readonly CaptureCompletionMode _completionMode;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly Button _import;
    private readonly Button _rotate;
    private readonly RadioButton _stagedGreen;
    private readonly RadioButton _keepAmber;
    private readonly Button _confirm;
    private readonly Button _safetyPause;
    private readonly Button _retryPurge;
    private readonly Label _status;
    private bool _stopping;
    private bool _completed;
    private bool _disposeCancellationWhenIdle;
    private bool _cancellationDisposed;
    private bool _terminalPurgePending;
    private DialogResult _pendingPurgeDialogResult;

    internal bool OperationPending { get; private set; }

    public CaptureForm(CaptureSession session, DistrictPolicy policy)
        : this(session, policy, CaptureCompletionMode.PurgeOnCompletion, safetyPausePresenter: null)
    {
    }

    internal CaptureForm(
        CaptureSession session,
        DistrictPolicy policy,
        Action<SafetyPauseResult>? safetyPausePresenter)
        : this(session, policy, CaptureCompletionMode.PurgeOnCompletion, safetyPausePresenter)
    {
    }

    /// <summary>
    /// A parent-owned workflow may retain the normalized session bytes after
    /// lane confirmation. The parent then owns every terminal path and must call
    /// CompleteCapture, Cancel, or InvokeSafetyPause before it can close.
    /// Standalone callers keep the public constructor's purge-on-completion law.
    /// </summary>
    internal CaptureForm(
        CaptureSession session,
        DistrictPolicy policy,
        CaptureCompletionMode completionMode,
        Action<SafetyPauseResult>? safetyPausePresenter = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _completionMode = completionMode;
        _presentSafetyPause = safetyPausePresenter ?? (result => MessageBox.Show(
            this,
            result.ProcedureText,
            UiStrings.WithoutMnemonic(UiStrings.PauseCaption),
            MessageBoxButtons.OK,
            MessageBoxIcon.Information));

        Text = UiStrings.WithoutMnemonic(UiStrings.CaptureWindowTitle);
        MinimumSize = new Size(640, 420);

        _import = MakeButton(UiStrings.ImportImage, async (_, _) => await ImportAsync());
        _rotate = MakeButton(UiStrings.Rotate90, async (_, _) => await RotateAsync());
        // No AccessibleName overrides: the full visible text IS the accessible
        // name, so the lane's meaning is announced, not just its color
        // (walkthrough step 14 — meaning in the name, not adjacent text).
        _stagedGreen = new RadioButton
        {
            Text = UiStrings.LaneGreen,
            AutoSize = true,
        };
        _keepAmber = new RadioButton
        {
            Text = UiStrings.LaneAmber,
            AutoSize = true,
            Checked = true,
        };
        _confirm = MakeButton(UiStrings.ConfirmLane, (_, _) => ConfirmLane());
        _safetyPause = MakeButton(UiStrings.SafetyPause, (_, _) => SafetyPause());
        _retryPurge = MakeButton(UiStrings.RetrySecurePurge, (_, _) => RetryPurge());
        _retryPurge.Visible = false;
        // No AccessibleName override: the message itself is what AT hears.
        _status = new Label { Dock = DockStyle.Bottom, AutoSize = false, Height = 28, UseMnemonic = false };

        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(12) };
        layout.Controls.AddRange([_import, _rotate, _stagedGreen, _keepAmber, _confirm, _safetyPause, _retryPurge]);
        Controls.Add(layout);
        Controls.Add(_status);

        UiLocale.ApplyChrome(this);
        UpdateControlAvailability();
    }

    private static Button MakeButton(string text, EventHandler onClick)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += onClick;
        return button;
    }

    private async Task ImportAsync()
    {
        if (_session.Machine.State == JobState.Imported && _session.Envelope is not null)
        {
            if (TryBeginOperation(out var retryCancellationToken))
            {
                await NormalizeCapturedAsync(retryCancellationToken);
            }

            return;
        }

        if (_session.Machine.State != JobState.New || OperationPending || _stopping)
        {
            return;
        }

        using var dialog = new OpenFileDialog
        {
            Filter = $"{UiStrings.WithoutMnemonic(UiStrings.ImagesFilterLabel)}|*.png;*.jpg;*.jpeg;*.bmp",
        };

        if (dialog.ShowDialog(this) != DialogResult.OK || !TryBeginOperation(out var cancellationToken))
        {
            return;
        }

        byte[]? importedBytes = null;
        try
        {
            // The path dies here: only bytes and a type travel onward (plan §6.5).
            importedBytes = await ReadBoundedImageAsync(dialog.FileName, cancellationToken);
            var mime = Path.GetExtension(dialog.FileName).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".bmp" => "image/bmp",
                _ => "image/jpeg",
            };
            await _session.CaptureAsync(
                new CaptureRequest(ByteImportCaptureSource.Kind, mime, importedBytes),
                cancellationToken);
            await _session.NormalizeAsync(new NormalizationRequest(), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            SetStatus(UiStrings.StatusImported);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Close and Gate C own this cancellation. Both are already leaving
            // the surface, so a late status update must not revive its controls.
        }
        catch (Exception failure) when (IsHandledCaptureFailure(failure))
        {
            HandleOperationFailure(failure);
        }
        finally
        {
            if (importedBytes is not null)
            {
                CryptographicOperations.ZeroMemory(importedBytes);
            }

            EndOperation();
        }
    }

    private static async Task<byte[]> ReadBoundedImageAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var input = new FileStream(
            path,
            new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            });
        var length = input.Length;
        if (length is <= 0 or > ImageNormalizer.MaxEncodedImageBytes)
        {
            throw new InvalidDataException(UiStrings.ImportImageSizeRefused);
        }

        var bytes = GC.AllocateUninitializedArray<byte>((int)length);
        try
        {
            await input.ReadExactlyAsync(bytes, cancellationToken);
            return bytes;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(bytes);
            throw;
        }
    }

    private async Task NormalizeCapturedAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _session.NormalizeAsync(new NormalizationRequest(), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            SetStatus(UiStrings.StatusImported);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Close and Gate C own this cancellation; do not touch a closing UI.
        }
        catch (Exception failure) when (IsHandledCaptureFailure(failure))
        {
            HandleOperationFailure(failure);
        }
        finally
        {
            EndOperation();
        }
    }

    private void HandleOperationFailure(Exception failure)
    {
        if (_stopping || IsDisposed || Disposing)
        {
            return;
        }

        var canRetryNormalization = _session.Machine.State == JobState.Imported
            && _session.Envelope is not null;
        SetStatus(canRetryNormalization ? UiStrings.StatusNormalizationRetry : UiStrings.StatusCaptureRefused,
            failure.Message);
    }

    private void SetImportMode(bool retryNormalization)
    {
        _import.Text = retryNormalization ? UiStrings.RetryNormalization : UiStrings.ImportImage;
    }

    private async Task RotateAsync()
    {
        if (_session.Machine.State != JobState.Normalized
            || !TryBeginOperation(out var cancellationToken))
        {
            return;
        }

        try
        {
            await _session.NormalizeAsync(
                new NormalizationRequest(RotationDegrees.Rotate90),
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            SetStatus(UiStrings.StatusRotated);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Close and Gate C own this cancellation; do not touch a closing UI.
        }
        catch (Exception failure) when (IsHandledCaptureFailure(failure))
        {
            HandleOperationFailure(failure);
        }
        finally
        {
            EndOperation();
        }
    }

    private void ConfirmLane()
    {
        if (OperationPending || _stopping || _session.Machine.State != JobState.Normalized)
        {
            return;
        }

        var lane = _stagedGreen.Checked ? DataLane.Green : DataLane.Amber;
        _session.ConfirmLane(lane);
        _completed = true;
        if (_completionMode == CaptureCompletionMode.RetainForOwner)
        {
            SetStatus(UiStrings.StatusLaneConfirmed, lane);
            DialogResult = DialogResult.OK;
            Close();
            return;
        }

        var purged = _session.CompleteCapture();
        if (!purged)
        {
            BeginPurgeRecovery(DialogResult.OK);
            return;
        }

        SetStatus(UiStrings.StatusLaneConfirmed, lane);
        DialogResult = DialogResult.OK;
        Close();
    }

    private void SafetyPause()
    {
        if (_stopping || !JobStateMachine.CanTransition(_session.Machine.State, JobState.Blocked))
        {
            return;
        }

        _stopping = true;
        CancelPendingOperation();
        UpdateControlAvailability();
        var result = _session.InvokeSafetyPause(_policy);
        try
        {
            _presentSafetyPause(result);
        }
        finally
        {
            if (OperationPending)
            {
                BeginPendingTerminalClose(DialogResult.Abort);
            }
            else if (_session.Machine.State == JobState.PurgeIncomplete)
            {
                BeginPurgeRecovery(DialogResult.Abort);
            }
            else
            {
                DialogResult = DialogResult.Abort;
                Close();
            }
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _stopping = true;
        CancelPendingOperation();
        var purgeWasAlreadyIncomplete = _session.Machine.State == JobState.PurgeIncomplete;

        if (!_completed
            && _session.Machine.State is not (JobState.Blocked or JobState.PurgeIncomplete)
            && _session.Cancel())
        {
            DialogResult = DialogResult.Cancel;
        }

        var requestedResult = _pendingPurgeDialogResult != DialogResult.None
            ? _pendingPurgeDialogResult
            : DialogResult == DialogResult.None
                ? DialogResult.Cancel
                : DialogResult;

        // Cancellation can request and perform an initial purge while a source
        // or normalizer still owns an in-flight operation. The session repeats
        // that purge after the operation settles. Do not dispose this recovery
        // surface until that final purge has truthfully reached its terminal
        // state; a late failure must remain visible and retryable.
        if (OperationPending)
        {
            e.Cancel = true;
            BeginPendingTerminalClose(requestedResult);
            base.OnFormClosing(e);
            return;
        }

        if (_session.Machine.State == JobState.PurgeIncomplete)
        {
            // A close attempted from an already-visible recovery state is an
            // explicit retry. A purge that failed for the first time during
            // this close attempt must remain visible instead of being retried
            // silently in the same call stack.
            if (!purgeWasAlreadyIncomplete || !_session.PurgeTransientSources())
            {
                e.Cancel = true;
                BeginPurgeRecovery(requestedResult);
                base.OnFormClosing(e);
                return;
            }
        }

        UpdateControlAvailability();
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_cancellationDisposed)
        {
            _disposeCancellationWhenIdle = true;
            CancelPendingOperation();
            DisposeCancellationWhenIdle();
        }

        base.Dispose(disposing);
    }

    private void SetStatus(string template, params object?[] arguments)
    {
        if ((_stopping && !_terminalPurgePending) || IsDisposed || Disposing)
        {
            return;
        }

        var text = UiStrings.FormatWithoutMnemonic(template, arguments);
        _status.Text = text;
        _status.AccessibleName = text;
    }

    private bool TryBeginOperation(out CancellationToken cancellationToken)
    {
        cancellationToken = default;
        if (OperationPending || _stopping || IsDisposed || Disposing)
        {
            return false;
        }

        OperationPending = true;
        cancellationToken = _lifetimeCancellation.Token;
        UpdateControlAvailability();
        return true;
    }

    private void EndOperation()
    {
        OperationPending = false;
        DisposeCancellationWhenIdle();

        if (_terminalPurgePending && _stopping)
        {
            CompletePendingTerminalClose();
            return;
        }

        UpdateControlAvailability();
    }

    private void CancelPendingOperation()
    {
        if (!_cancellationDisposed && !_lifetimeCancellation.IsCancellationRequested)
        {
            _lifetimeCancellation.Cancel();
        }
    }

    private void DisposeCancellationWhenIdle()
    {
        if (_disposeCancellationWhenIdle && !OperationPending && !_cancellationDisposed)
        {
            _lifetimeCancellation.Dispose();
            _cancellationDisposed = true;
        }
    }

    private void UpdateControlAvailability()
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        var idle = !OperationPending && !_stopping;
        var purgeIncomplete = _session.Machine.State == JobState.PurgeIncomplete;
        var retryNormalization = _session.Machine.State == JobState.Imported
            && _session.Envelope is not null;
        SetImportMode(retryNormalization);
        _import.Enabled = idle && (_session.Machine.State == JobState.New || retryNormalization);
        SetCapturedControlsEnabled(idle && _session.Machine.State == JobState.Normalized);
        _safetyPause.Enabled = !_stopping
            && JobStateMachine.CanTransition(_session.Machine.State, JobState.Blocked);
        _retryPurge.Visible = purgeIncomplete;
        _retryPurge.Enabled = purgeIncomplete && !OperationPending;
    }

    private static bool IsHandledCaptureFailure(Exception failure)
        => failure is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or ArgumentException
            or NotSupportedException
            or ExternalException;

    private void SetCapturedControlsEnabled(bool enabled)
    {
        _rotate.Enabled = enabled;
        _stagedGreen.Enabled = enabled;
        _keepAmber.Enabled = enabled;
        _confirm.Enabled = enabled;
    }

    private void BeginPurgeRecovery(DialogResult successDialogResult)
    {
        _terminalPurgePending = true;
        _pendingPurgeDialogResult = successDialogResult;
        DialogResult = DialogResult.None;
        _stopping = true;
        SetStatus(UiStrings.StatusPurgeIncomplete);
        UpdateControlAvailability();
    }

    private void BeginPendingTerminalClose(DialogResult successDialogResult)
    {
        _terminalPurgePending = true;
        _pendingPurgeDialogResult = successDialogResult;
        DialogResult = DialogResult.None;
        _stopping = true;
        UpdateControlAvailability();
    }

    private void CompletePendingTerminalClose()
    {
        if (OperationPending || IsDisposed || Disposing)
        {
            return;
        }

        if (_session.Machine.State == JobState.PurgeIncomplete)
        {
            BeginPurgeRecovery(_pendingPurgeDialogResult);
            return;
        }

        if (_session.Machine.State != JobState.TransientSourcesPurged)
        {
            // A requested terminal purge must settle as either verified or
            // explicitly incomplete. Refuse to close on any other evidence.
            BeginPendingTerminalClose(_pendingPurgeDialogResult);
            return;
        }

        var result = _pendingPurgeDialogResult;
        _terminalPurgePending = false;
        _pendingPurgeDialogResult = DialogResult.None;
        DialogResult = result;
        Close();
    }

    private void RetryPurge()
    {
        if (!_terminalPurgePending
            || OperationPending
            || _session.Machine.State != JobState.PurgeIncomplete)
        {
            return;
        }

        if (!_session.PurgeTransientSources())
        {
            SetStatus(UiStrings.StatusPurgeIncomplete);
            UpdateControlAvailability();
            return;
        }

        _terminalPurgePending = false;
        _completed = true;
        DialogResult = _pendingPurgeDialogResult;
        Close();
    }
}
