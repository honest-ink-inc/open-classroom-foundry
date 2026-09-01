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

        Text = UiStrings.WithoutMnemonic(UiStrings.LoadedProjectPreflightWindowTitle);
        MinimumSize = new Size(680, 430);
        Size = new Size(760, 500);
        AutoScaleMode = AutoScaleMode.Dpi;

        var introduction = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(700, 0),
            Text = UiStrings.LoadedProjectPreflightIntroduction,
            AccessibleName = UiStrings.WithoutMnemonic(UiStrings.LoadedProjectPreflightIntroduction),
        };
        var exactDocument = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            MinimumSize = new Size(0, 120),
            Text = ExactArtifactDocumentText.Describe(loaded.Document),
            AccessibleName = UiStrings.WithoutMnemonic(UiStrings.LoadedProjectExactDocument),
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
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
        };
        checklist.Controls.AddRange(
            [_greenContent, _noLearnerLinkedContent, _noRestrictedContent]);
        checklist.Layout += (_, _) => ReflowChecklist(checklist);
        foreach (var item in new[] { _greenContent, _noLearnerLinkedContent, _noRestrictedContent })
        {
            item.FontChanged += (_, _) => checklist.PerformLayout();
        }

        var group = new GroupBox
        {
            Dock = DockStyle.Fill,
            MinimumSize = new Size(0, 150),
            Text = UiStrings.LoadedProjectPreflightChecklist,
            AccessibleName = UiStrings.WithoutMnemonic(UiStrings.LoadedProjectPreflightChecklist),
        };
        group.Controls.Add(checklist);

        var exactBinding = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(700, 0),
            Text = UiStrings.LoadedProjectPreflightExactBinding,
            AccessibleName = UiStrings.WithoutMnemonic(UiStrings.LoadedProjectPreflightExactBinding),
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
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 5,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        // Keep the compact floor honest without freezing the ordinary larger
        // window to floor-sized content rows. The exact document is the
        // inspection surface, so it owns every surplus pixel; the terminal
        // actions must never become the accidental expansion row.
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        exactDocument.Margin = new Padding(3, 3, 36, 3);
        group.Margin = new Padding(3, 3, 36, 3);
        buttons.Margin = new Padding(3, 3, 36, 24);
        layout.Controls.Add(introduction, 0, 0);
        layout.Controls.Add(exactDocument, 0, 1);
        layout.Controls.Add(group, 0, 2);
        layout.Controls.Add(exactBinding, 0, 3);
        layout.Controls.Add(buttons, 0, 4);

        var viewport = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
        };
        viewport.Controls.Add(layout);
        viewport.Layout += (_, _) =>
        {
            if (viewport.ClientSize.Width <= 0 || viewport.ClientSize.Height <= 0)
            {
                return;
            }

            var desiredWidth = viewport.ClientSize.Width;
            ConstrainWrappingLabels(desiredWidth);
            var preferredHeight = layout.GetPreferredSize(new Size(desiredWidth, 0)).Height;
            if (preferredHeight > viewport.ClientSize.Height)
            {
                desiredWidth = Math.Max(
                    1,
                    viewport.ClientSize.Width - SystemInformation.VerticalScrollBarWidth);
                ConstrainWrappingLabels(desiredWidth);
                preferredHeight = layout.GetPreferredSize(new Size(desiredWidth, 0)).Height;
            }

            var desired = new Size(
                desiredWidth,
                Math.Max(viewport.ClientSize.Height, preferredHeight));
            if (layout.Size != desired)
            {
                layout.Size = desired;
            }

            void ConstrainWrappingLabels(int width)
            {
                var availableWidth = Math.Max(
                    1,
                    width - layout.Padding.Horizontal - introduction.Margin.Horizontal);
                introduction.MaximumSize = new Size(availableWidth, 0);
                exactBinding.MaximumSize = new Size(availableWidth, 0);
            }
        };
        Controls.Add(viewport);

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
            AutoSize = false,
            MaximumSize = new Size(680, 0),
            MinimumSize = new Size(0, 40),
            Text = text,
            AccessibleName = UiStrings.WithoutMnemonic(text),
        };
        return item;
    }

    private static void ReflowChecklist(FlowLayoutPanel checklist)
    {
        var verticalScrollbarWidth = checklist.VerticalScroll.Visible
            ? SystemInformation.VerticalScrollBarWidth
            : 0;
        foreach (var item in checklist.Controls.OfType<CheckBox>())
        {
            var width = Math.Max(
                1,
                checklist.ClientSize.Width
                    - checklist.Padding.Horizontal
                    - item.Margin.Horizontal
                    - verticalScrollbarWidth);
            var textWidth = Math.Max(
                1,
                width - item.Padding.Horizontal - SystemInformation.MenuCheckSize.Width - 3);
            var required = TextRenderer.MeasureText(
                UiStrings.WithoutMnemonic(item.Text),
                item.Font,
                new Size(textWidth, int.MaxValue),
                TextFormatFlags.WordBreak |
                TextFormatFlags.TextBoxControl |
                TextFormatFlags.NoPadding |
                TextFormatFlags.NoPrefix);
            var desired = new Size(
                Math.Min(width, item.MaximumSize.Width),
                Math.Max(item.MinimumSize.Height, required.Height + item.Padding.Vertical));
            if (item.Size != desired)
            {
                item.Size = desired;
            }
        }
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
