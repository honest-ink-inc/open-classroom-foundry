// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.DeterministicPress;

namespace Foundry.App.WinForms;

/// <summary>
/// The Press Room — the main surface (second forge menu, item 1). The press
/// catalog's typed parameters generate the form; the parameters-never-prose
/// invariant is therefore visible: there is nowhere to type prose. Nothing
/// here renders, exports, or saves without the typed ApprovedArtifact that
/// only the review surface can produce (ADR-004). Standard controls only.
/// </summary>
public sealed class PressRoomForm : Form
{
    private readonly Func<ReviewSession, ApprovedArtifact?> _reviewRunner;
    private readonly bool _modalReview;
    private readonly ListBox _pressList;
    private readonly TableLayoutPanel _parameterPanel;
    private readonly Label _budget;
    private readonly Button _review;
    private readonly Button _printView;
    private readonly Button _export;
    private readonly Button _save;
    private readonly Label _status;
    private readonly Dictionary<string, Func<string>> _valueReaders = new(StringComparer.Ordinal);
    private PressDefinition? _approvedDefinition;

    /// <summary>The runner seam exists so tests can drive the flow without a modal dialog; production uses the real ReviewForm.</summary>
    public PressRoomForm(Func<ReviewSession, ApprovedArtifact?>? reviewRunner = null)
    {
        _modalReview = reviewRunner is null;
        _reviewRunner = reviewRunner ?? RunModalReview;

        Text = UiStrings.MainWindowTitle;
        MinimumSize = new Size(860, 560);

        _pressList = new ListBox { Dock = DockStyle.Fill, AccessibleName = UiStrings.PressList };
        foreach (var definition in PressRoomCatalog.All)
        {
            _pressList.Items.Add(UiStrings.Localize(definition.Title));
        }

        _budget = new Label { AutoSize = true, AccessibleName = UiStrings.Format(UiStrings.BudgetLine, PressRoomCatalog.BudgetMinutes) };
        _budget.Text = _budget.AccessibleName;

        _parameterPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoScroll = true,
        };
        _parameterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _parameterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _review = MakeButton(UiStrings.ReviewAndApprove, (_, _) => ReviewAndApprove());
        _printView = MakeButton(UiStrings.OpenPrintView, (_, _) => OpenPrintView());
        _export = MakeButton(UiStrings.ExportEllipsis, (_, _) => Export());
        _save = MakeButton(UiStrings.SaveToLibrary, (_, _) => SaveToLibrary());
        // Deferred one beat like every modal-opener (harness finding, 29 Aug 2026).
        var allAboard = MakeButton(UiStrings.AllAboardOpen, (_, _) => BeginInvoke(OpenAllAboard));

        // No AccessibleName override: the message itself must be what a screen
        // reader hears, not the word "Status" (a harness finding, 29 Aug 2026).
        _status = new Label { Dock = DockStyle.Bottom, AutoSize = false, Height = 28 };
        SetStatus(UiStrings.StatusReady);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        buttons.Controls.AddRange([_review, _printView, _export, _save, allAboard]);

        var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        right.Controls.Add(_budget, 0, 0);
        right.Controls.Add(_parameterPanel, 0, 1);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66));
        layout.Controls.Add(_pressList, 0, 0);
        layout.Controls.Add(right, 1, 0);

        Controls.Add(layout);
        Controls.Add(buttons);
        Controls.Add(_status);

        _pressList.SelectedIndexChanged += (_, _) => LoadPress();
        _pressList.SelectedIndex = 0;

        UiLocale.ApplyChrome(this);
    }

    public PressDefinition? SelectedPress
        => _pressList.SelectedIndex >= 0 ? PressRoomCatalog.All[_pressList.SelectedIndex] : null;

    /// <summary>Non-null only after the review surface produced a typed approval.</summary>
    public ApprovedArtifact? ApprovedResult { get; private set; }

    /// <summary>What the status line currently says — also its accessible name.</summary>
    public string StatusText => _status.Text;

    private static Button MakeButton(string text, EventHandler onClick)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += onClick;
        return button;
    }

    private void LoadPress()
    {
        ApprovedResult = null;
        _approvedDefinition = null;
        UpdateGatedButtons();

        _parameterPanel.SuspendLayout();
        _parameterPanel.Controls.Clear();
        _parameterPanel.RowCount = 0;
        _valueReaders.Clear();

        if (SelectedPress is not { } definition)
        {
            _parameterPanel.ResumeLayout();
            return;
        }

        foreach (var parameter in definition.Parameters)
        {
            AddParameterRow(parameter);
        }

        _parameterPanel.ResumeLayout();
        SetStatus(UiStrings.StatusReady);
    }

    private void AddParameterRow(PressParameter parameter)
    {
        var label = UiStrings.Localize(parameter.Label);
        Control control;
        switch (parameter)
        {
            case NumberParameter number:
                var spinner = new NumericUpDown
                {
                    Minimum = (decimal)number.Minimum,
                    Maximum = (decimal)number.Maximum,
                    DecimalPlaces = number.DecimalPlaces,
                    Increment = number.DecimalPlaces > 0 ? 0.5m : 1m,
                    Value = (decimal)number.Default,
                    Width = 110,
                };
                _valueReaders[parameter.Key] = () => spinner.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                control = spinner;
                break;

            case ChoiceParameter choice:
                var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
                foreach (var option in choice.Options)
                {
                    combo.Items.Add(option);
                }

                combo.SelectedIndex = choice.Options.ToList().IndexOf(choice.Default);
                _valueReaders[parameter.Key] = () => (string)combo.SelectedItem!;
                control = combo;
                break;

            case ToggleParameter toggle:
                var check = new CheckBox { Text = label, AutoSize = true, Checked = toggle.Default };
                _valueReaders[parameter.Key] = () => check.Checked ? "true" : "false";
                control = check;
                break;

            case LinesParameter lines:
                var multiline = new TextBox
                {
                    Multiline = true,
                    ScrollBars = ScrollBars.Vertical,
                    Height = 96,
                    Width = 320,
                    Text = lines.DefaultText,
                    AcceptsReturn = true,
                };
                _valueReaders[parameter.Key] = () => multiline.Text;
                control = multiline;
                break;

            case TextParameter text:
                var box = new TextBox { Width = 220, Text = text.Default };
                _valueReaders[parameter.Key] = () => box.Text;
                control = box;
                break;

            default:
                // Developer-facing: the type name alone identifies the gap.
                throw new NotSupportedException(parameter.GetType().Name);
        }

        control.AccessibleName = label;

        var row = _parameterPanel.RowCount++;
        _parameterPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        if (control is CheckBox)
        {
            _parameterPanel.Controls.Add(control, 1, row);
        }
        else
        {
            _parameterPanel.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            _parameterPanel.Controls.Add(control, 1, row);
        }
    }

    private void ReviewAndApprove()
    {
        if (SelectedPress is not { } definition)
        {
            return;
        }

        ApprovedResult = null;
        _approvedDefinition = null;

        ArtifactDocument document;
        try
        {
            document = definition.Build(new PressInputs(
                _valueReaders.ToDictionary(pair => pair.Key, pair => pair.Value(), StringComparer.Ordinal)));
        }
        catch (ArgumentException refusal)
        {
            SetStatus(UiStrings.Format(UiStrings.StatusRefused, refusal.Message));
            _parameterPanel.Controls.OfType<Control>().FirstOrDefault(c => c.CanSelect)?.Focus();
            return;
        }

        var session = AppServices.SessionOver(document);

        if (_modalReview)
        {
            // The modal opens on the next message-loop beat, not re-entrantly
            // from the click: assistive technology and UI Automation see the
            // click complete, then the dialog arrive — no call left pending.
            BeginInvoke(() => CompleteReview(definition, session));
        }
        else
        {
            CompleteReview(definition, session);
        }
    }

    private void CompleteReview(PressDefinition definition, ReviewSession session)
    {
        var approved = _reviewRunner(session);
        if (approved is null)
        {
            SetStatus(UiStrings.StatusNotApproved);
            return;
        }

        ApprovedResult = approved;
        _approvedDefinition = definition;
        UpdateGatedButtons();
        SetStatus(UiStrings.StatusApproved);
    }

    private static ApprovedArtifact? RunModalReview(ReviewSession session)
    {
        using var review = new ReviewForm(session);
        return review.ShowDialog() == DialogResult.OK ? review.Result : null;
    }

    private void OpenPrintView()
    {
        if (ApprovedResult is null)
        {
            return;
        }

        AppServices.OpenPrintView(ApprovedResult, _approvedDefinition!.Id);
        SetStatus(UiStrings.StatusPrintView);
    }

    private void Export()
    {
        if (ApprovedResult is null)
        {
            return;
        }

        using var dialog = new SaveFileDialog
        {
            FileName = _approvedDefinition!.Id,
            Filter = $"{UiStrings.ExportFilterPdf}|*.pdf|{UiStrings.ExportFilterPrint}|*.html|{UiStrings.ExportFilterAccessible}|*.html|{UiStrings.ExportFilterSvg}|*.svg",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var target = dialog.FilterIndex switch
        {
            1 => RenderTarget.PrintPdf,
            3 => RenderTarget.AccessibleHtml,
            4 => RenderTarget.Svg,
            _ => RenderTarget.PrintHtml,
        };

        try
        {
            File.WriteAllBytes(dialog.FileName, AppServices.Render(ApprovedResult, target));
        }
        catch (NotSupportedException refusal)
        {
            // Single-sheet-only SVG and vector-only PDF refuse loudly; say so.
            SetStatus(UiStrings.Format(UiStrings.StatusRefused, refusal.Message));
            return;
        }

        SetStatus(UiStrings.Format(UiStrings.StatusExported, Path.GetFileName(dialog.FileName)));
    }

    private void SaveToLibrary()
    {
        if (ApprovedResult is null)
        {
            return;
        }

        var hint = AppServices.SaveToLibrary(
            ApprovedResult, _approvedDefinition!.Id, "deterministic-press",
            _approvedDefinition.Recipe.Id, _approvedDefinition.Recipe.Version, new AppServices.NoAssetsCatalog());
        SetStatus(UiStrings.Format(UiStrings.StatusSaved, hint));
    }

    private void UpdateGatedButtons()
    {
        // The structural gate, visible: these do nothing until a typed approval exists.
        _printView.Enabled = ApprovedResult is not null;
        _export.Enabled = ApprovedResult is not null;
        _save.Enabled = ApprovedResult is not null;
    }

    private void SetStatus(string text) => _status.Text = text;

    private void OpenAllAboard()
    {
        using var form = new AllAboardForm(AppServices.SymbolCatalog());
        form.ShowDialog(this);
    }
}
