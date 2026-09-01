// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.BuiltIn;
using Foundry.Modules.BuiltIn.AllAboard;

namespace Foundry.App.WinForms;

/// <summary>
/// The SequenceSlate surface (stable legacy id: all-aboard) — the ratified 0.1
/// slice, now complete (third forge
/// menu, item 3): Task strip, First/Then, Now/Next/Done, and Agency cards,
/// all with tested builders, symbols chosen by MEANING from the shipped CC0
/// pack, agency labels overridable per card so a classroom prints "Alto," not
/// the catalog's English (RC-2). THE WALL: Choice Board and Change Preview
/// also appear in the ratified flow's list, but they carry choice-preservation
/// semantics and capture adjacency that belong to the AAC/SLP seat and the
/// district instrument — their absence here is deliberate, and adding them is
/// a governance act, not a code change.
/// </summary>
public sealed class AllAboardForm : Form
{
    private readonly IAssetCatalog _catalog;
    private readonly Func<ReviewSession, ApprovedArtifact?> _reviewRunner;
    private readonly bool _modalReview;
    private readonly ComboBox _mode;
    private readonly TableLayoutPanel _grid;
    private readonly List<(TextBox Text, ComboBox Symbol)> _steps = [];
    private readonly List<(CheckBox Include, TextBox Override)> _agency = [];
    private readonly IReadOnlyList<AssetProvenance> _symbols;
    private readonly List<string> _symbolNames;
    private TextBox? _title;
    private readonly Button _review;
    private readonly Button _print;
    private readonly Button _printView;
    private readonly Button _export;
    private readonly Button _cancelExport;
    private readonly Button _save;
    private readonly Label _status;
    private bool _loadingMode;
    private bool _reviewPending;
    private bool _exportInProgress;
    private bool _printViewInProgress;
    private CancellationTokenSource? _exportCancellation;
    private long _stateGeneration;
    private RecipeManifest _approvedRecipe = AllAboardRecipes.TaskStrip;
    private readonly Func<ExportChoice?> _exportPicker;
    private readonly Func<ApprovedArtifact, string, IAssetCatalog?, CancellationToken, Task> _pdfExporter;
    private readonly Func<ApprovedArtifact, string, RenderAudience, double, bool, IAssetCatalog?, Task> _printViewOpener;

    public sealed record ExportChoice(string Path, int FilterIndex);

    public AllAboardForm(
        IAssetCatalog catalog,
        Func<ReviewSession, ApprovedArtifact?>? reviewRunner = null,
        Func<ExportChoice?>? exportPicker = null,
        Func<ApprovedArtifact, string, IAssetCatalog?, CancellationToken, Task>? pdfExporter = null,
        Func<ApprovedArtifact, string, RenderAudience, double, bool, IAssetCatalog?, Task>? printViewOpener = null)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _modalReview = reviewRunner is null;
        _reviewRunner = reviewRunner ?? RunModalReview;
        _exportPicker = exportPicker ?? PickExportDialog;
        _pdfExporter = pdfExporter ?? ((artifact, destination, assets, cancellationToken) =>
            AppServices.ExportPdfAsync(
                artifact,
                destination,
                assets,
                cancellationToken: cancellationToken));
        _printViewOpener = printViewOpener ?? AppServices.OpenPrintViewAsync;

        // The pack may hold two symbols with one meaning (it ships two Help
        // variants): meaning stays the name, and only duplicates append their
        // alt text — so every row a screen reader hears is distinct.
        // The picker and the resulting document share this one typed snapshot;
        // a catalog changing order between reads must never change a selection.
        _symbols = [.. _catalog.All];
        var duplicated = _symbols.GroupBy(p => p.IntendedMeaning)
            .Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
        _symbolNames = [.. _symbols.Select(p => duplicated.Contains(p.IntendedMeaning)
            ? UiStrings.FormatWithoutMnemonic(UiStrings.SymbolDisambiguation, p.IntendedMeaning, p.AltText)
            : p.IntendedMeaning)];

        Text = UiStrings.WithoutMnemonic(UiStrings.AllAboardWindowTitle);
        MinimumSize = new Size(720, 560);

        _mode = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 200,
            AccessibleName = UiStrings.WithoutMnemonic(UiStrings.OutputMode),
        };
        _mode.Items.AddRange([
            UiStrings.WithoutMnemonic(UiStrings.ModeTaskStrip),
            UiStrings.WithoutMnemonic(UiStrings.ModeFirstThen),
            UiStrings.WithoutMnemonic(UiStrings.ModeNowNextDone),
            UiStrings.WithoutMnemonic(UiStrings.ModeAgencyCards),
        ]);
        _mode.SelectedIndexChanged += (_, _) => LoadMode();

        _grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoScroll = true,
            AutoScrollMargin = new Size(16, 16),
        };
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _grid.Layout += (_, _) => RefreshGridScrollExtent();

        _review = MakeButton(UiStrings.ReviewAndApprove, (_, _) => ReviewAndApprove());
        _print = MakeButton(UiStrings.PrintButton, (_, _) => WithApproved(a =>
        {
            try
            {
                AppServices.Print(a, assetCatalog: _catalog);
                SetStatus(UiStrings.StatusPrinted);
            }
            catch (Exception failure) when (failure is InvalidOperationException or IOException or NotSupportedException)
            {
                SetStatus(UiStrings.StatusRefused, failure.Message);
            }
        }));
        _printView = MakeButton(
            UiStrings.OpenPrintView,
            async (_, _) => await WithApprovedAsync(OpenPrintViewAsync));
        _export = MakeButton(UiStrings.ExportEllipsis, async (_, _) => await WithApprovedAsync(ExportAsync));
        _cancelExport = MakeButton(UiStrings.CancelExport, (_, _) => _exportCancellation?.Cancel());
        _save = MakeButton(UiStrings.SaveToLibrary, (_, _) => WithApproved(a =>
        {
            var hint = AppServices.SaveToLibrary(
                a,
                PublicFileStem(_approvedRecipe),
                ModulePublicIdentity.VisualSupport.LegacyId,
                _approvedRecipe.Id,
                _approvedRecipe.Version,
                _catalog);
            SetStatus(UiStrings.StatusSaved, hint);
        }));

        // The message itself is what AT hears.
        _status = ReflowingStatusLabel.Attach(new Label(), minimumHeight: 28);
        SetStatus(UiStrings.StatusReady);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        buttons.Controls.AddRange([_review, _print, _printView, _export, _cancelExport, _save]);

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(4) };
        top.Controls.Add(new Label { Text = UiStrings.OutputMode, AutoSize = true, Anchor = AnchorStyles.Left });
        top.Controls.Add(_mode);

        Controls.Add(_grid);
        Controls.Add(top);
        Controls.Add(buttons);
        Controls.Add(_status);

        _mode.SelectedIndex = 0;
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

    private void LoadMode()
    {
        _stateGeneration++;
        _loadingMode = true;
        ClearApproval();

        _grid.SuspendLayout();
        _grid.Controls.Clear();
        _grid.RowCount = 0;
        _steps.Clear();
        _agency.Clear();
        _title = null;

        switch (_mode.SelectedIndex)
        {
            case 1:
                AddCardRows(
                    UiStrings.Localize(UiCatalogIds.AllAboardFirstCard, "First"),
                    UiStrings.Localize(UiCatalogIds.AllAboardThenCard, "Then"));
                break;
            case 2:
                AddCardRows(
                    UiStrings.Localize(UiCatalogIds.AllAboardNowCard, "Now"),
                    UiStrings.Localize(UiCatalogIds.AllAboardNextCard, "Next"),
                    UiStrings.Localize(UiCatalogIds.AllAboardDoneCard, "Done"));
                break;
            case 3:
                AddAgencyRows();
                break;
            default:
                AddTaskStripRows();
                break;
        }

        _grid.ResumeLayout();
        _loadingMode = false;
        SetStatus(UiStrings.StatusReady);
    }

    private void AddTaskStripRows()
    {
        _title = new TextBox { Width = 360, AccessibleName = UiStrings.WithoutMnemonic(UiStrings.TaskTitle) };
        AddRow(UiStrings.TaskTitle, _title);

        for (var i = 1; i <= AllAboardBuilders.MaximumSteps; i++)
        {
            var text = new TextBox { Width = 360, AccessibleName = UiStrings.FormatWithoutMnemonic(UiStrings.StepTextLabel, i) };
            var symbol = SymbolPicker(UiStrings.StepSymbolLabel, i);
            AddRow(UiStrings.StepTextLabel, text, i);
            AddRow(UiStrings.StepSymbolLabel, symbol, i);
            _steps.Add((text, symbol));
        }
    }

    private void AddCardRows(params string[] cardNames)
    {
        foreach (var name in cardNames)
        {
            var text = new TextBox { Width = 360, AccessibleName = UiStrings.FormatWithoutMnemonic(UiStrings.CardTextLabel, name) };
            var symbol = SymbolPicker(UiStrings.CardSymbolLabel, name);
            AddRow(UiStrings.CardTextLabel, text, name);
            AddRow(UiStrings.CardSymbolLabel, symbol, name);
            _steps.Add((text, symbol));
        }
    }

    private void AddAgencyRows()
    {
        // Each card: include it or not, and optionally override its printed
        // label — "Alto," not the catalog's English (RC-2). The MEANING still
        // names the row: never "image", never a filename.
        for (var i = 0; i < _symbols.Count; i++)
        {
            var include = new CheckBox
            {
                Text = _symbolNames[i],
                AutoSize = true,
                Checked = true,
                // Pack-provided meanings are content, not static chrome access
                // keys; a literal '&' must remain visible and audible.
                UseMnemonic = false,
            };
            var overrideBox = new TextBox
            {
                Width = 220,
                AccessibleName = UiStrings.FormatWithoutMnemonic(UiStrings.AgencyOverrideLabel, _symbolNames[i]),
            };

            var row = _grid.RowCount++;
            _grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _grid.Controls.Add(include, 0, row);
            _grid.Controls.Add(overrideBox, 1, row);
            TrackInput(include);
            TrackInput(overrideBox);
            _agency.Add((include, overrideBox));
        }
    }

    private ComboBox SymbolPicker(string accessibleNameTemplate, params object?[] arguments)
    {
        var symbol = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 180,
            AccessibleName = UiStrings.FormatWithoutMnemonic(accessibleNameTemplate, arguments),
        };
        symbol.Items.Add(UiStrings.WithoutMnemonic(UiStrings.NoSymbol));
        foreach (var name in _symbolNames)
        {
            symbol.Items.Add(name);
        }

        ComboBoxReadingPath.EnsureEveryItemFits(symbol);
        symbol.SelectedIndex = 0;
        return symbol;
    }

    private void AddRow(string labelTemplate, Control control, params object?[] arguments)
    {
        var row = _grid.RowCount++;
        _grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _grid.Controls.Add(new Label
        {
            Text = UiStrings.FormatWithoutMnemonic(labelTemplate, arguments),
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            // Card names come from the dynamic catalog, where '&' is literal.
            UseMnemonic = false,
        }, 0, row);
        _grid.Controls.Add(control, 1, row);
        TrackInput(control);
    }

    private void RefreshGridScrollExtent()
    {
        var scrollPosition = _grid.AutoScrollPosition;
        var contentBottom = _grid.Controls.Cast<Control>()
            .Where(control => control.Visible)
            .Select(control => control.Bottom - scrollPosition.Y + control.Margin.Bottom)
            .DefaultIfEmpty(0)
            .Max();
        var minimumHeight = checked(contentBottom + _grid.AutoScrollMargin.Height);
        if (_grid.AutoScrollMinSize.Height != minimumHeight)
        {
            _grid.AutoScrollMinSize = new Size(0, minimumHeight);
        }
    }

    private void TrackInput(Control control)
    {
        switch (control)
        {
            case TextBox box:
                box.TextChanged += (_, _) => InputChanged();
                break;
            case ComboBox combo:
                combo.SelectedIndexChanged += (_, _) => InputChanged();
                break;
            case CheckBox check:
                check.CheckedChanged += (_, _) => InputChanged();
                break;
        }
    }

    private AssetId? SymbolAt(int index)
        => _steps[index].Symbol.SelectedIndex > 0 ? _symbols[_steps[index].Symbol.SelectedIndex - 1].Id : null;

    private CardSpec CardAt(int index)
        => new(_steps[index].Text.Text, Symbol: SymbolAt(index));

    private void ReviewAndApprove()
    {
        ClearApproval();

        AllAboardBuildOutcome outcome;
        try
        {
            switch (_mode.SelectedIndex)
            {
                case 1:
                    outcome = AllAboardBuilders.BuildFirstThen(CardAt(0), CardAt(1), _catalog);
                    break;

                case 2:
                    outcome = AllAboardBuilders.BuildNowNextDone(CardAt(0), CardAt(1), CardAt(2), _catalog);
                    break;

                case 3:
                    var chosen = _agency.Select((row, i) => (row, i)).Where(pair => pair.row.Include.Checked).ToList();
                    outcome = AllAboardBuilders.BuildAgencyCards(
                        [.. chosen.Select(pair => _symbols[pair.i].Id)],
                        _catalog,
                        labels: chosen.Any(pair => !string.IsNullOrWhiteSpace(pair.row.Override.Text))
                            ? [.. chosen.Select(pair => string.IsNullOrWhiteSpace(pair.row.Override.Text)
                                ? _symbols[pair.i].IntendedMeaning
                                : pair.row.Override.Text)]
                            : null);
                    break;

                default:
                    var steps = _steps
                        .Where(s => !string.IsNullOrWhiteSpace(s.Text.Text) || s.Symbol.SelectedIndex > 0)
                        .Select(s => new StepSpec(s.Text.Text,
                            s.Symbol.SelectedIndex > 0 ? _symbols[s.Symbol.SelectedIndex - 1].Id : null))
                        .ToList();
                    outcome = AllAboardBuilders.BuildTaskStrip(_title!.Text, steps, _catalog);
                    break;
            }
        }
        catch (ArgumentException refusal)
        {
            SetStatus(UiStrings.StatusRefused, refusal.Message);
            return;
        }

        var session = AppServices.SessionOverRecipe(
            outcome.CreateDraft(),
            new DefaultArtifactValidator(),
            outcome.Recipe,
            viewContext: new ReviewViewContext(
                ReviewViewContext.ManualDefault.PreviewRequest,
                assetCatalog: _catalog));
        var generation = _stateGeneration;
        BeginPendingReview();
        if (_modalReview)
        {
            // Deferred one message-loop beat (harness finding, 29 Aug 2026).
            BeginInvoke(() => CompleteReview(outcome.Recipe, session, generation));
        }
        else
        {
            CompleteReview(outcome.Recipe, session, generation);
        }
    }

    private void CompleteReview(RecipeManifest recipe, ReviewSession session, long generation)
    {
        try
        {
            var approved = _reviewRunner(session);
            if (approved is null
                || generation != _stateGeneration
                || !AppServices.IsExactApproval(session, approved))
            {
                SetStatus(UiStrings.StatusNotApproved);
                return;
            }

            ApprovedResult = approved;
            _approvedRecipe = recipe;
            UpdateGatedButtons();
            SetStatus(UiStrings.StatusAllAboardApprovedAccessHeld);
        }
        finally
        {
            EndPendingReview();
        }
    }

    private ApprovedArtifact? RunModalReview(ReviewSession session)
    {
        using var review = new ReviewForm(session);
        return review.ShowDialog(this) == DialogResult.OK ? review.Result : null;
    }

    private void WithApproved(Action<ApprovedArtifact> action)
    {
        if (ApprovedResult is not null)
        {
            action(ApprovedResult);
        }
    }

    private async Task WithApprovedAsync(Func<ApprovedArtifact, Task> action)
    {
        if (ApprovedResult is not null)
        {
            await action(ApprovedResult);
        }
    }

    private async Task OpenPrintViewAsync(ApprovedArtifact approved)
    {
        if (_printViewInProgress)
        {
            return;
        }

        _printViewInProgress = true;
        UpdateGatedButtons();
        SetStatus(UiStrings.StatusPrintViewOpening);
        try
        {
            await _printViewOpener(
                approved,
                PublicFileStem(_approvedRecipe),
                RenderAudience.Learner,
                100,
                false,
                _catalog);
            SetStatus(UiStrings.StatusPrintView);
        }
        catch (Exception failure) when (IsExpectedPrintViewFailure(failure))
        {
            SetStatus(UiStrings.StatusPrintViewRefused);
        }
        finally
        {
            _printViewInProgress = false;
            UpdateGatedButtons();
        }
    }

    private ExportChoice? PickExportDialog()
    {
        using var dialog = new SaveFileDialog
        {
            FileName = PublicFileStem(_approvedRecipe),
            Filter = $"{UiStrings.WithoutMnemonic(UiStrings.ExportFilterPdf)}|*.pdf|{UiStrings.WithoutMnemonic(UiStrings.ExportFilterSvg)}|*.svg|{UiStrings.WithoutMnemonic(UiStrings.ExportFilterPrint)}|*.html|{UiStrings.WithoutMnemonic(UiStrings.ExportFilterAccessible)}|*.html",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return null;
        }

        return new ExportChoice(dialog.FileName, dialog.FilterIndex);
    }

    internal static string PublicFileStem(RecipeManifest recipe)
        => ModulePublicIdentity.FileStemFor(recipe);

    internal async Task ExportAsync(ApprovedArtifact approved)
    {
        if (_exportInProgress)
        {
            return;
        }

        try
        {
            var choice = _exportPicker();
            if (choice is null)
            {
                return;
            }

            var target = choice.FilterIndex switch
            {
                1 => RenderTarget.PrintPdf,
                2 => RenderTarget.Svg,
                4 => RenderTarget.AccessibleHtml,
                _ => RenderTarget.PrintHtml,
            };
            if (!_approvedRecipe.SupportedExports.Contains(target))
            {
                throw new NotSupportedException(target.ToString());
            }

            _exportCancellation = new CancellationTokenSource();
            var exportToken = _exportCancellation.Token;
            _exportInProgress = true;
            UpdateGatedButtons();
            SetStatus(UiStrings.StatusExporting, Path.GetFileName(choice.Path));

            if (target == RenderTarget.PrintPdf)
            {
                await _pdfExporter(approved, choice.Path, _catalog, exportToken);
            }
            else
            {
                var bytes = await Task.Run(
                    () => AppServices.Render(approved, target, _catalog, exportToken),
                    exportToken);
                await AppServices.WriteExportBytesAsync(choice.Path, bytes, exportToken);
            }

            SetStatus(UiStrings.StatusExported, Path.GetFileName(choice.Path));
        }
        catch (OperationCanceledException) when (_exportCancellation?.IsCancellationRequested == true)
        {
            SetStatus(UiStrings.StatusExportCancelled);
        }
        catch (Exception refusal) when (refusal is InvalidOperationException
            or IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException)
        {
            SetStatus(UiStrings.StatusRefused, refusal.Message);
        }
        finally
        {
            _exportCancellation?.Dispose();
            _exportCancellation = null;
            _exportInProgress = false;
            UpdateGatedButtons();
        }
    }

    private void UpdateGatedButtons()
    {
        if (IsDisposed)
        {
            return;
        }

        // The structural gate, visible: nothing unlocks before typed approval.
        var idle = !_reviewPending && !_exportInProgress && !_printViewInProgress;
        var enabled = idle && ApprovedResult is not null;
        _review.Enabled = idle;
        _print.Enabled = enabled;
        _printView.Enabled = enabled;
        _export.Enabled = enabled;
        _cancelExport.Enabled = _exportInProgress;
        _save.Enabled = enabled;
        _mode.Enabled = idle;
        _grid.Enabled = idle;
    }

    private void InputChanged()
    {
        if (_loadingMode)
        {
            return;
        }

        _stateGeneration++;
        ClearApproval();
        SetStatus(UiStrings.StatusModuleChanged);
    }

    private void ClearApproval()
    {
        ApprovedResult = null;
        UpdateGatedButtons();
    }

    private void BeginPendingReview()
    {
        _reviewPending = true;
        _mode.Enabled = false;
        _grid.Enabled = false;
        UpdateGatedButtons();
    }

    private void EndPendingReview()
    {
        _reviewPending = false;
        _mode.Enabled = true;
        _grid.Enabled = true;
        UpdateGatedButtons();
    }

    private void SetStatus(string template, params object?[] arguments)
    {
        if (IsDisposed)
        {
            return;
        }

        var text = UiStrings.FormatWithoutMnemonic(template, arguments);
        _status.Text = text;
        _status.AccessibleName = text;
    }

    private static bool IsExpectedPrintViewFailure(Exception failure)
        => failure is InvalidOperationException
            or IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException
            or System.ComponentModel.Win32Exception;

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _exportCancellation?.Cancel();
        base.OnFormClosed(e);
    }
}
