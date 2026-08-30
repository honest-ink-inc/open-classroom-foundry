// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.App.WinForms;

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
    private readonly Button _import;
    private readonly Button _rotate;
    private readonly RadioButton _stagedGreen;
    private readonly RadioButton _keepAmber;
    private readonly Button _confirm;
    private readonly Button _safetyPause;
    private readonly Label _status;

    public CaptureForm(CaptureSession session, DistrictPolicy policy)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));

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
        // No AccessibleName override: the message itself is what AT hears.
        _status = new Label { Dock = DockStyle.Bottom, AutoSize = false, Height = 28, UseMnemonic = false };

        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(12) };
        layout.Controls.AddRange([_import, _rotate, _stagedGreen, _keepAmber, _confirm, _safetyPause]);
        Controls.Add(layout);
        Controls.Add(_status);

        UiLocale.ApplyChrome(this);
    }

    private static Button MakeButton(string text, EventHandler onClick)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += onClick;
        return button;
    }

    private async Task ImportAsync()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = $"{UiStrings.WithoutMnemonic(UiStrings.ImagesFilterLabel)}|*.png;*.jpg;*.jpeg;*.bmp",
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            // The path dies here: only bytes and a type travel onward (plan §6.5).
            var bytes = await File.ReadAllBytesAsync(dialog.FileName);
            var mime = Path.GetExtension(dialog.FileName).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".bmp" => "image/bmp",
                _ => "image/jpeg",
            };
            await _session.CaptureAsync(new CaptureRequest(ByteImportCaptureSource.Kind, mime, bytes), CancellationToken.None);
            await _session.NormalizeAsync(new NormalizationRequest(), CancellationToken.None);
            SetStatus(UiStrings.StatusImported);
        }
    }

    private async Task RotateAsync()
    {
        await _session.NormalizeAsync(new NormalizationRequest(RotationDegrees.Rotate90), CancellationToken.None);
        SetStatus(UiStrings.StatusRotated);
    }

    private void ConfirmLane()
    {
        var lane = _stagedGreen.Checked ? DataLane.Green : DataLane.Amber;
        _session.ConfirmLane(lane);
        SetStatus(UiStrings.StatusLaneConfirmed, lane);
        DialogResult = DialogResult.OK;
        Close();
    }

    private void SafetyPause()
    {
        var result = _session.InvokeSafetyPause(_policy);
        MessageBox.Show(this, result.ProcedureText, UiStrings.WithoutMnemonic(UiStrings.PauseCaption),
            MessageBoxButtons.OK, MessageBoxIcon.Information);
        DialogResult = DialogResult.Abort;
        Close();
    }

    private void SetStatus(string template, params object?[] arguments)
    {
        var text = UiStrings.FormatWithoutMnemonic(template, arguments);
        _status.Text = text;
        _status.AccessibleName = text;
    }
}
