// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.BuiltIn.AllAboard;

namespace Foundry.App.WinForms;

/// <summary>
/// The All Aboard typed-steps surface — the RATIFIED 0.1 slice only (second
/// forge menu, item 2): a title, three to eight teacher-typed steps, and
/// per-step symbols from the shipped CC0 pack chosen by their MEANING, never a
/// filename (walkthrough step 6). The wall stands: no new visual-support
/// interaction pattern, no symbol-set expansion, no co-design decision lives
/// here — that territory is sealed for the AAC/SLP seat. Bilingual authoring
/// waits with it; the engine's bilingual capability is untouched.
/// </summary>
public sealed class AllAboardForm : Form
{
    private readonly IAssetCatalog _catalog;
    private readonly Func<ReviewSession, ApprovedArtifact?> _reviewRunner;
    private readonly bool _modalReview;
    private readonly TextBox _title;
    private readonly List<(TextBox Text, ComboBox Symbol)> _steps = [];
    private readonly Button _review;
    private readonly Button _print;
    private readonly Button _printView;
    private readonly Button _export;
    private readonly Button _save;
    private readonly Label _status;

    public AllAboardForm(IAssetCatalog catalog, Func<ReviewSession, ApprovedArtifact?>? reviewRunner = null)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _modalReview = reviewRunner is null;
        _reviewRunner = reviewRunner ?? RunModalReview;

        Text = UiStrings.AllAboardWindowTitle;
        MinimumSize = new Size(720, 560);

        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoScroll = true };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _title = new TextBox { Width = 360, AccessibleName = UiStrings.TaskTitle };
        AddRow(grid, UiStrings.TaskTitle, _title);

        // The symbol list speaks each symbol's intended meaning from its
        // provenance record — never "image", never a filename.
        var symbolNames = _catalog.All.Select(p => p.IntendedMeaning).ToList();
        for (var i = 1; i <= AllAboardBuilders.MaximumSteps; i++)
        {
            var text = new TextBox { Width = 360, AccessibleName = UiStrings.Format(UiStrings.StepTextLabel, i) };
            var symbol = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 180,
                AccessibleName = UiStrings.Format(UiStrings.StepSymbolLabel, i),
            };
            symbol.Items.Add(UiStrings.NoSymbol);
            foreach (var name in symbolNames)
            {
                symbol.Items.Add(name);
            }

            symbol.SelectedIndex = 0;

            AddRow(grid, UiStrings.Format(UiStrings.StepTextLabel, i), text);
            AddRow(grid, UiStrings.Format(UiStrings.StepSymbolLabel, i), symbol);
            _steps.Add((text, symbol));
        }

        _review = MakeButton(UiStrings.ReviewAndApprove, (_, _) => ReviewAndApprove());
        _print = MakeButton(UiStrings.PrintButton, (_, _) => WithApproved(a =>
        {
            try
            {
                AppServices.Print(a);
                SetStatus(UiStrings.StatusPrinted);
            }
            catch (Exception failure) when (failure is InvalidOperationException or IOException or NotSupportedException)
            {
                SetStatus(UiStrings.Format(UiStrings.StatusRefused, failure.Message));
            }
        }));
        _printView = MakeButton(UiStrings.OpenPrintView, (_, _) => WithApproved(a =>
        {
            AppServices.OpenPrintView(a, "all-aboard-task-strip");
            SetStatus(UiStrings.StatusPrintView);
        }));
        _export = MakeButton(UiStrings.ExportEllipsis, (_, _) => WithApproved(Export));
        _save = MakeButton(UiStrings.SaveToLibrary, (_, _) => WithApproved(a =>
        {
            var recipe = AllAboardRecipes.TaskStrip;
            var hint = AppServices.SaveToLibrary(a, "task-strip", "all-aboard", recipe.Id, recipe.Version, _catalog);
            SetStatus(UiStrings.Format(UiStrings.StatusSaved, hint));
        }));

        // No AccessibleName override: the message itself is what AT hears.
        _status = new Label { Dock = DockStyle.Bottom, AutoSize = false, Height = 28 };
        SetStatus(UiStrings.StatusReady);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        buttons.Controls.AddRange([_review, _print, _printView, _export, _save]);

        Controls.Add(grid);
        Controls.Add(buttons);
        Controls.Add(_status);

        UpdateGatedButtons();
        UiLocale.ApplyChrome(this);
    }

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

    private static void AddRow(TableLayoutPanel grid, string label, Control control)
    {
        var row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        grid.Controls.Add(control, 1, row);
    }

    private void ReviewAndApprove()
    {
        ApprovedResult = null;
        UpdateGatedButtons();

        ArtifactDocument document;
        try
        {
            // A row counts as a step when it carries text or a symbol; a
            // symbol on a blank row flows through as a blank step so the
            // review surface's validation says so out loud (walkthrough 7).
            var steps = _steps
                .Where(s => !string.IsNullOrWhiteSpace(s.Text.Text) || s.Symbol.SelectedIndex > 0)
                .Select(s => new StepSpec(
                    s.Text.Text,
                    s.Symbol.SelectedIndex > 0 ? _catalog.All[s.Symbol.SelectedIndex - 1].Id : null))
                .ToList();

            document = AllAboardBuilders.TaskStrip(_title.Text, steps, _catalog);
        }
        catch (ArgumentException refusal)
        {
            SetStatus(UiStrings.Format(UiStrings.StatusRefused, refusal.Message));
            _title.Focus();
            return;
        }

        var session = AppServices.SessionOver(document);
        if (_modalReview)
        {
            // Deferred one message-loop beat: automation sees the click
            // complete, then the dialog arrive (harness finding, 29 Aug 2026).
            BeginInvoke(() => CompleteReview(session));
        }
        else
        {
            CompleteReview(session);
        }
    }

    private void CompleteReview(ReviewSession session)
    {
        var approved = _reviewRunner(session);
        if (approved is null)
        {
            SetStatus(UiStrings.StatusNotApproved);
            return;
        }

        ApprovedResult = approved;
        UpdateGatedButtons();
        SetStatus(UiStrings.StatusApproved);
    }

    private static ApprovedArtifact? RunModalReview(ReviewSession session)
    {
        using var review = new ReviewForm(session);
        return review.ShowDialog() == DialogResult.OK ? review.Result : null;
    }

    private void WithApproved(Action<ApprovedArtifact> action)
    {
        if (ApprovedResult is not null)
        {
            action(ApprovedResult);
        }
    }

    private void Export(ApprovedArtifact approved)
    {
        using var dialog = new SaveFileDialog
        {
            FileName = "task-strip",
            Filter = $"{UiStrings.ExportFilterPrint}|*.html|{UiStrings.ExportFilterAccessible}|*.html",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var target = dialog.FilterIndex == 2 ? RenderTarget.AccessibleHtml : RenderTarget.PrintHtml;
        File.WriteAllBytes(dialog.FileName, AppServices.Render(approved, target));
        SetStatus(UiStrings.Format(UiStrings.StatusExported, Path.GetFileName(dialog.FileName)));
    }

    private void UpdateGatedButtons()
    {
        // The structural gate, visible: nothing unlocks before typed approval.
        _print.Enabled = ApprovedResult is not null;
        _printView.Enabled = ApprovedResult is not null;
        _export.Enabled = ApprovedResult is not null;
        _save.Enabled = ApprovedResult is not null;
    }

    private void SetStatus(string text) => _status.Text = text;
}
