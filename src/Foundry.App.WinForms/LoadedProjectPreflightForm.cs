// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Storage;

namespace Foundry.App.WinForms;

/// <summary>
/// Teacher-owned lane classification for a reopened package. The package's
/// own Green claim is mutable and therefore never selected or trusted here.
/// Standard check boxes make each boundary in plan section 4 explicit; this is
/// a content classification, not a purpose, recipe, seat, or policy waiver.
/// </summary>
public sealed class LoadedProjectPreflightForm : Form
{
    private readonly LoadedProject _loaded;
    private readonly CheckBox _greenContent;
    private readonly CheckBox _noLearnerLinkedContent;
    private readonly CheckBox _noRestrictedContent;
    private readonly Button _continue;

    public LoadedProjectPreflightForm(LoadedProject loaded)
    {
        _loaded = loaded ?? throw new ArgumentNullException(nameof(loaded));

        Text = UiStrings.LoadedProjectPreflightWindowTitle;
        MinimumSize = new Size(680, 430);
        Size = new Size(760, 500);
        AutoScaleMode = AutoScaleMode.Dpi;

        var introduction = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(700, 0),
            Text = UiStrings.LoadedProjectPreflightIntroduction,
            AccessibleName = UiStrings.LoadedProjectPreflightIntroduction,
        };
        var exactDocument = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Text = ExactArtifactDocumentText.Describe(loaded.Document),
            AccessibleName = UiStrings.LoadedProjectExactDocument,
        };
        _greenContent = ChecklistItem(UiStrings.LoadedProjectGreenContent);
        _noLearnerLinkedContent = ChecklistItem(UiStrings.LoadedProjectNoLearnerLinkedContent);
        _noRestrictedContent = ChecklistItem(UiStrings.LoadedProjectNoRestrictedContent);
        _greenContent.CheckedChanged += ChecklistChanged;
        _noLearnerLinkedContent.CheckedChanged += ChecklistChanged;
        _noRestrictedContent.CheckedChanged += ChecklistChanged;

        var checklist = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
        };
        checklist.Controls.AddRange(
            [_greenContent, _noLearnerLinkedContent, _noRestrictedContent]);

        var group = new GroupBox
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Text = UiStrings.LoadedProjectPreflightChecklist,
            AccessibleName = UiStrings.LoadedProjectPreflightChecklist,
        };
        group.Controls.Add(checklist);

        var exactBinding = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(700, 0),
            Text = UiStrings.LoadedProjectPreflightExactBinding,
            AccessibleName = UiStrings.LoadedProjectPreflightExactBinding,
        };
        _continue = Button(UiStrings.ContinueToExactReview, (_, _) => ConfirmAndClose());
        var cancel = Button(UiStrings.Cancel, (_, _) => CancelAndClose());
        cancel.DialogResult = DialogResult.Cancel;

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
        };
        buttons.Controls.AddRange([_continue, cancel]);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 5,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(introduction, 0, 0);
        layout.Controls.Add(exactDocument, 0, 1);
        layout.Controls.Add(group, 0, 2);
        layout.Controls.Add(exactBinding, 0, 3);
        layout.Controls.Add(buttons, 0, 4);
        Controls.Add(layout);

        AcceptButton = _continue;
        CancelButton = cancel;
        UpdateAvailability();
        UiLocale.ApplyChrome(this);
    }

    public LoadedProjectGreenConfirmation? Confirmation { get; private set; }

    private static CheckBox ChecklistItem(string text)
    {
        var item = new CheckBox
        {
            AutoSize = true,
            MaximumSize = new Size(680, 0),
            Text = text,
            AccessibleName = text,
        };
        return item;
    }

    private static Button Button(string text, EventHandler onClick)
    {
        var button = new Button { AutoSize = true, Text = text };
        button.Click += onClick;
        return button;
    }

    private void UpdateAvailability()
        => _continue.Enabled = _greenContent.Checked
            && _noLearnerLinkedContent.Checked
            && _noRestrictedContent.Checked;

    private void ConfirmAndClose()
    {
        if (!_continue.Enabled)
        {
            return;
        }

        Confirmation = AppServices.ConfirmLoadedProjectGreen(
            _loaded,
            new LoadedProjectGreenChecklist(
                _greenContent.Checked,
                _noLearnerLinkedContent.Checked,
                _noRestrictedContent.Checked));
        DialogResult = DialogResult.OK;
        Close();
    }

    private void CancelAndClose()
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _greenContent.Focus();
    }

    private void ChecklistChanged(object? sender, EventArgs e) => UpdateAvailability();
}
