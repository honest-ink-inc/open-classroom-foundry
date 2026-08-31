// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Infrastructure.Windows;
using System.Drawing.Imaging;
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
/// claim. The exact normalized image is shown with a keyboard-operable pixel
/// crop proposal; drawing redactions and live camera wiring remain pending.
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
    private readonly PictureBox _preview;
    private readonly GroupBox _cropGroup;
    private readonly NumericUpDown _cropX;
    private readonly NumericUpDown _cropY;
    private readonly NumericUpDown _cropWidth;
    private readonly NumericUpDown _cropHeight;
    private readonly Button _applyCrop;
    private readonly Button _resetCrop;
    private readonly Label _status;
    private int _previewPixelWidth;
    private int _previewPixelHeight;
    private SessionByteReference? _previewReference;
    private bool _updatingCropControls;
    private bool _cropProposalDirty;
    private bool _initialPreviewLoadStarted;
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
        _safetyPause = MakeButton(UiStrings.SafetyPause, (_, _) => SafetyPause());
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
        _retryPurge = MakeButton(UiStrings.RetrySecurePurge, (_, _) => RetryPurge());
        _retryPurge.Visible = false;

        _preview = new PictureBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            SizeMode = PictureBoxSizeMode.Zoom,
            TabStop = false,
            RightToLeft = RightToLeft.No,
            AccessibleName = UiStrings.WithoutMnemonic(UiStrings.CapturePreviewAccessibleName),
            AccessibleDescription = UiStrings.WithoutMnemonic(UiStrings.CapturePreviewAccessibleDescription),
        };

        _cropX = MakeCropValue(UiStrings.CropLeftX);
        _cropY = MakeCropValue(UiStrings.CropTopY);
        _cropWidth = MakeCropValue(UiStrings.CropWidth);
        _cropHeight = MakeCropValue(UiStrings.CropHeight);
        foreach (var value in new[] { _cropX, _cropY, _cropWidth, _cropHeight })
        {
            value.ValueChanged += (_, _) => CropProposalChanged();
        }

        _applyCrop = MakeButton(UiStrings.ApplyCrop, async (_, _) => await ApplyCropAsync());
        _resetCrop = MakeButton(UiStrings.ResetCrop, (_, _) => ResetCropProposal(announce: true));

        var cropLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 7,
            Padding = new Padding(8),
        };
        cropLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        cropLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        var cropExplanation = new Label
        {
            Text = UiStrings.CropCoordinateExplanation,
            AutoSize = true,
            UseMnemonic = false,
            MaximumSize = new Size(400, 0),
        };
        cropLayout.Controls.Add(cropExplanation, 0, 0);
        cropLayout.SetColumnSpan(cropExplanation, 2);
        AddCropRow(cropLayout, 1, UiStrings.CropLeftX, _cropX);
        AddCropRow(cropLayout, 2, UiStrings.CropTopY, _cropY);
        AddCropRow(cropLayout, 3, UiStrings.CropWidth, _cropWidth);
        AddCropRow(cropLayout, 4, UiStrings.CropHeight, _cropHeight);
        cropLayout.Controls.Add(_applyCrop, 0, 5);
        cropLayout.Controls.Add(_resetCrop, 1, 5);
        _cropGroup = new GroupBox
        {
            Text = UiStrings.CropProposal,
            Width = 420,
            Height = 260,
        };
        _cropGroup.Controls.Add(cropLayout);

        // No AccessibleName override: the message itself is what AT hears.
        _status = new Label
        {
            Name = "CaptureStatus",
            Dock = DockStyle.Bottom,
            AutoSize = false,
            Height = 44,
            UseMnemonic = false,
        };

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(12),
        };
        actions.Controls.AddRange(
        [
            _import,
            _safetyPause,
            _rotate,
            _cropGroup,
            _stagedGreen,
            _keepAmber,
            _confirm,
            _retryPurge,
        ]);

        var previewGroup = new GroupBox
        {
            Text = UiStrings.CapturePreview,
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
        };
        previewGroup.Controls.Add(_preview);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        layout.Controls.Add(previewGroup, 0, 0);
        layout.Controls.Add(actions, 1, 0);
        Controls.Add(layout);
        Controls.Add(_status);

        UiLocale.ApplyChrome(this);
        Shown += async (_, _) => await LoadInitialPreviewAsync();
        UpdateControlAvailability();
    }

    private static Button MakeButton(string text, EventHandler onClick)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += onClick;
        return button;
    }

    private static NumericUpDown MakeCropValue(string accessibleName)
        => new()
        {
            Minimum = 0,
            Maximum = ImageNormalizer.MaxImageDimension,
            DecimalPlaces = 0,
            ThousandsSeparator = true,
            Width = 120,
            AccessibleName = UiStrings.WithoutMnemonic(accessibleName),
        };

    private static void AddCropRow(TableLayoutPanel layout, int row, string labelText, NumericUpDown value)
    {
        var label = new Label
        {
            Text = labelText,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
        };
        label.Click += (_, _) => value.Focus();
        layout.Controls.Add(label, 0, row);
        layout.Controls.Add(value, 1, row);
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
            await ReloadAuthoritativePreviewAsync(cancellationToken);
            ResetLaneAttestation();
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
            await ReloadAuthoritativePreviewAsync(cancellationToken);
            ResetLaneAttestation();
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
            || _previewReference is not { } expectedPreviewReference
            || !TryBeginOperation(out var cancellationToken))
        {
            return;
        }

        try
        {
            await _session.NormalizeAsync(
                new NormalizationRequest(RotationDegrees.Rotate90),
                expectedPreviewReference,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await ReloadAuthoritativePreviewAsync(cancellationToken);
            ResetLaneAttestation();
            SetStatus(UiStrings.StatusRotated);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Close and Gate C own this cancellation; do not touch a closing UI.
        }
        catch (CaptureGenerationChangedException)
        {
            RefuseStalePreview();
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

    private async Task ApplyCropAsync()
    {
        if (_session.Machine.State != JobState.Normalized
            || !_cropProposalDirty
            || _previewReference is not { } expectedPreviewReference)
        {
            return;
        }

        if (!TryCropRectangle(out var crop, out var refusal))
        {
            SetStatus(UiStrings.StatusCropRefused, refusal);
            return;
        }

        if (!TryBeginOperation(out var cancellationToken))
        {
            return;
        }

        try
        {
            await _session.NormalizeAsync(
                new NormalizationRequest(RotationDegrees.None, Crop: crop),
                expectedPreviewReference,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await ReloadAuthoritativePreviewAsync(cancellationToken);
            ResetLaneAttestation();
            SetStatus(UiStrings.StatusCropped, _previewPixelWidth, _previewPixelHeight);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Close and Gate C own this cancellation; do not touch a closing UI.
        }
        catch (CaptureGenerationChangedException)
        {
            RefuseStalePreview();
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

    private bool TryCropRectangle(out CropRectangle crop, out string refusal)
    {
        var x = decimal.ToInt32(_cropX.Value);
        var y = decimal.ToInt32(_cropY.Value);
        var width = decimal.ToInt32(_cropWidth.Value);
        var height = decimal.ToInt32(_cropHeight.Value);
        crop = new CropRectangle(x, y, width, height);

        if (_previewPixelWidth <= 0
            || _previewPixelHeight <= 0
            || x < 0
            || y < 0
            || width <= 0
            || height <= 0
            || (long)x + width > _previewPixelWidth
            || (long)y + height > _previewPixelHeight)
        {
            refusal = UiStrings.FormatWithoutMnemonic(
                UiStrings.CropBoundsInvalid,
                _previewPixelWidth,
                _previewPixelHeight);
            return false;
        }

        refusal = string.Empty;
        return true;
    }

    private async Task LoadInitialPreviewAsync()
    {
        if (_initialPreviewLoadStarted || _session.Machine.State != JobState.Normalized)
        {
            return;
        }

        _initialPreviewLoadStarted = true;
        if (!TryBeginOperation(out var cancellationToken))
        {
            return;
        }

        try
        {
            SetStatus(UiStrings.StatusPreviewLoading);
            await ReloadAuthoritativePreviewAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            ResetLaneAttestation();
            SetStatus(UiStrings.StatusPreviewReady, _previewPixelWidth, _previewPixelHeight);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Close and Gate C own this cancellation; do not touch a closing UI.
        }
        catch (Exception failure) when (IsHandledCaptureFailure(failure))
        {
            ClearPreview();
            SetStatus(UiStrings.StatusPreviewRefused, failure.Message);
        }
        finally
        {
            EndOperation();
        }
    }

    private async Task ReloadAuthoritativePreviewAsync(CancellationToken cancellationToken)
    {
        PreviewCandidate? candidate = null;
        var published = false;
        var candidateTransferred = false;
        try
        {
            candidate = await Task.Run(
                () => CopyAndDecodeAuthoritativePreview(cancellationToken),
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            Image? previous = null;
            published = _session.TryPublishForAuthoritativeEnvelope(
                candidate.Reference,
                () =>
                {
                    previous = _preview.Image;
                    _preview.Image = candidate.Image;
                    candidateTransferred = true;
                    _previewReference = candidate.Reference;
                    _previewPixelWidth = candidate.Image.Width;
                    _previewPixelHeight = candidate.Image.Height;
                });
            if (!published)
            {
                throw new InvalidOperationException(
                    UiStrings.WithoutMnemonic(UiStrings.CapturePreviewGenerationChanged));
            }

            OverwriteAndDispose(previous);
            _preview.AccessibleDescription = UiStrings.FormatWithoutMnemonic(
                UiStrings.CapturePreviewDimensions,
                _previewPixelWidth,
                _previewPixelHeight);
            ResetCropProposal(announce: false);
        }
        catch
        {
            ClearPreview();
            throw;
        }
        finally
        {
            if (!candidateTransferred && candidate is not null)
            {
                OverwriteAndDispose(candidate.Image);
            }
        }
    }

    private PreviewCandidate CopyAndDecodeAuthoritativePreview(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_session.TryCopyAuthoritativeEnvelope(ImageNormalizer.MaxEncodedImageBytes, out var copy)
            || copy is null)
        {
            throw new InvalidOperationException(UiStrings.WithoutMnemonic(UiStrings.CapturePreviewCopyRefused));
        }

        MemoryStream? stream = null;
        Image? decoded = null;
        Bitmap? replacement = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!copy.Envelope.MetadataStripped
                || copy.Envelope.PageCount != 1
                || !string.Equals(copy.Envelope.MimeType, "image/png", StringComparison.Ordinal))
            {
                throw new InvalidDataException(UiStrings.WithoutMnemonic(UiStrings.CapturePreviewEnvelopeRefused));
            }

            stream = new MemoryStream(copy.Content, writable: false);
            decoded = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: true);
            cancellationToken.ThrowIfCancellationRequested();
            ValidatePreviewDimensions(decoded.Width, decoded.Height);

            replacement = new Bitmap(decoded.Width, decoded.Height, PixelFormat.Format24bppRgb);
            using (var graphics = Graphics.FromImage(replacement))
            {
                graphics.DrawImage(
                    decoded,
                    new Rectangle(0, 0, decoded.Width, decoded.Height),
                    new Rectangle(0, 0, decoded.Width, decoded.Height),
                    GraphicsUnit.Pixel);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var candidate = new PreviewCandidate(copy.Envelope.Bytes, replacement);
            replacement = null;
            return candidate;
        }
        catch (OutOfMemoryException failure)
        {
            throw new InvalidDataException(UiStrings.WithoutMnemonic(UiStrings.CapturePreviewDecodeRefused), failure);
        }
        finally
        {
            OverwriteAndDispose(decoded);
            stream?.Dispose();
            OverwriteAndDispose(replacement);
            CryptographicOperations.ZeroMemory(copy.Content);
        }
    }

    private sealed record PreviewCandidate(SessionByteReference Reference, Bitmap Image);

    private static void ValidatePreviewDimensions(int width, int height)
    {
        if (width <= 0
            || height <= 0
            || width > ImageNormalizer.MaxImageDimension
            || height > ImageNormalizer.MaxImageDimension
            || checked((long)width * height) > ImageNormalizer.MaxDecodedPixels)
        {
            throw new InvalidDataException(UiStrings.WithoutMnemonic(UiStrings.CapturePreviewDecodeRefused));
        }
    }

    private void CropProposalChanged()
    {
        if (_updatingCropControls)
        {
            return;
        }

        var wasDirty = _cropProposalDirty;
        _cropProposalDirty = _previewPixelWidth > 0
            && (_cropX.Value != 0
                || _cropY.Value != 0
                || _cropWidth.Value != _previewPixelWidth
                || _cropHeight.Value != _previewPixelHeight);
        if (_cropProposalDirty)
        {
            SetStatus(UiStrings.StatusCropPending);
        }
        else if (wasDirty)
        {
            SetStatus(UiStrings.StatusCropReset, _previewPixelWidth, _previewPixelHeight);
        }

        UpdateControlAvailability();
    }

    private void ResetCropProposal(bool announce)
    {
        if (_previewPixelWidth <= 0 || _previewPixelHeight <= 0)
        {
            _cropProposalDirty = false;
            return;
        }

        _updatingCropControls = true;
        try
        {
            ConfigureCropValue(_cropX, 0, _previewPixelWidth - 1, 0);
            ConfigureCropValue(_cropY, 0, _previewPixelHeight - 1, 0);
            ConfigureCropValue(_cropWidth, 1, _previewPixelWidth, _previewPixelWidth);
            ConfigureCropValue(_cropHeight, 1, _previewPixelHeight, _previewPixelHeight);
            _cropProposalDirty = false;
        }
        finally
        {
            _updatingCropControls = false;
        }

        if (announce)
        {
            SetStatus(UiStrings.StatusCropReset, _previewPixelWidth, _previewPixelHeight);
        }

        UpdateControlAvailability();
    }

    private static void ConfigureCropValue(NumericUpDown value, int minimum, int maximum, int current)
    {
        value.Minimum = minimum;
        value.Maximum = maximum;
        value.Value = current;
    }

    private void ResetLaneAttestation()
    {
        _keepAmber.Checked = true;
        _stagedGreen.Checked = false;
    }

    private void ClearPreview()
    {
        if (_preview.IsDisposed)
        {
            _previewReference = null;
            _previewPixelWidth = 0;
            _previewPixelHeight = 0;
            _cropProposalDirty = false;
            return;
        }

        var image = _preview.Image;
        _preview.Image = null;
        OverwriteAndDispose(image);
        _previewReference = null;
        _previewPixelWidth = 0;
        _previewPixelHeight = 0;
        _cropProposalDirty = false;
        _preview.AccessibleDescription = UiStrings.WithoutMnemonic(UiStrings.CapturePreviewAccessibleDescription);

        _updatingCropControls = true;
        try
        {
            foreach (var value in new[] { _cropX, _cropY, _cropWidth, _cropHeight })
            {
                value.Minimum = 0;
                value.Maximum = ImageNormalizer.MaxImageDimension;
                value.Value = 0;
            }
        }
        finally
        {
            _updatingCropControls = false;
        }
    }

    private static void OverwriteAndDispose(Image? image)
    {
        if (image is null)
        {
            return;
        }

        try
        {
            using var graphics = Graphics.FromImage(image);
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
            image.Dispose();
        }
    }

    private void ConfirmLane()
    {
        if (OperationPending
            || _stopping
            || _session.Machine.State != JobState.Normalized
            || _previewReference is not { } previewReference)
        {
            return;
        }

        var lane = _stagedGreen.Checked ? DataLane.Green : DataLane.Amber;
        try
        {
            _session.ConfirmLane(lane, previewReference);
        }
        catch (InvalidOperationException)
        {
            RefuseStalePreview();
            return;
        }

        ClearPreview();
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

    private void RefuseStalePreview()
    {
        ClearPreview();
        SetStatus(
            UiStrings.StatusPreviewRefused,
            UiStrings.WithoutMnemonic(UiStrings.CapturePreviewGenerationChanged));
        UpdateControlAvailability();
    }

    private void SafetyPause()
    {
        if (_stopping || !JobStateMachine.CanTransition(_session.Machine.State, JobState.Blocked))
        {
            return;
        }

        _stopping = true;
        CancelPendingOperation();
        ClearPreview();
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
        ClearPreview();
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
            if (_completionMode != CaptureCompletionMode.RetainForOwner || !_completed)
            {
                _session.AbandonAndPurge();
            }

            DisposeCancellationWhenIdle();
        }

        if (disposing)
        {
            ClearPreview();
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
            try
            {
                _lifetimeCancellation.Cancel();
            }
            catch (Exception)
            {
                // A borrower-controlled cancellation callback cannot suppress
                // Gate C, window-close, or forced-disposal session purge.
            }
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
        SetCapturedControlsEnabled(
            idle
                && _session.Machine.State == JobState.Normalized
                && _preview.Image is not null
                && _previewReference is not null);
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
        _rotate.Enabled = enabled && !_cropProposalDirty;
        _cropGroup.Enabled = enabled;
        _applyCrop.Enabled = enabled && _cropProposalDirty;
        _resetCrop.Enabled = enabled && _cropProposalDirty;
        _stagedGreen.Enabled = enabled;
        _keepAmber.Enabled = enabled;
        _confirm.Enabled = enabled && !_cropProposalDirty;
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
