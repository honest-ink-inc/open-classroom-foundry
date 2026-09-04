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
    private readonly Button _cancelExport;
    private readonly Button _save;
    private readonly Label _status;
    private readonly Button _print;
    private readonly Button _tile;
    private readonly Button _openLibrary;
    private readonly Button _allAboard;
    private readonly Button _builtInStudios;
    private readonly CheckBox _lowInk;
    private readonly Dictionary<string, Func<string>> _valueReaders = new(StringComparer.Ordinal);
    private readonly Func<string?> _libraryPicker;
    private readonly Func<ExportChoice?> _exportPicker;
    private readonly Func<Storage.LoadedProject, LoadedProjectGreenConfirmation?> _loadedProjectPreflight;
    private readonly Func<ApprovedArtifact, string, IAssetCatalog?, CancellationToken, Task> _pdfExporter;
    private readonly Func<ApprovedArtifact, string, RenderAudience, double, bool, IAssetCatalog?, Task> _printViewOpener;
    private bool _loadingParameters;
    private bool _reviewPending;
    private bool _exportInProgress;
    private bool _exportDispatchPending;
    private bool _printViewInProgress;
    private CancellationTokenSource? _exportCancellation;
    private long _stateGeneration;
    private ApprovedContext? _context;

    /// <summary>What an approval belongs to, whether it was pressed here or reopened from the library.</summary>
    private sealed record ApprovedContext(
        string Name,
        string ModuleId,
        string RecipeId,
        string RecipeVersion,
        IAssetCatalog? AssetCatalog = null);

    /// <summary>Where an export goes and as what (the 1-based filter index of the export dialog).</summary>
    public sealed record ExportChoice(string Path, int FilterIndex);

    /// <summary>Visible text is translated; Value is the catalog's unchanged submitted choice.</summary>
    private sealed record ChoiceDisplayItem(string Value, string Text)
    {
        public override string ToString() => Text;
    }

    /// <summary>The runner and picker seams exist so tests can drive the flows without modal dialogs; production uses the real ReviewForm and file dialogs. The export seam earns its place the hard way: the shell Save As dialog's name field cannot be committed by any cross-process automation (harness finding, 29 Aug 2026).</summary>
    public PressRoomForm(
        Func<ReviewSession, ApprovedArtifact?>? reviewRunner = null,
        Func<string?>? libraryPicker = null,
        Func<ExportChoice?>? exportPicker = null,
        Func<Storage.LoadedProject, LoadedProjectGreenConfirmation?>? loadedProjectPreflight = null,
        Func<ApprovedArtifact, string, IAssetCatalog?, CancellationToken, Task>? pdfExporter = null,
        Func<ApprovedArtifact, string, RenderAudience, double, bool, IAssetCatalog?, Task>? printViewOpener = null)
    {
        _modalReview = reviewRunner is null;
        _reviewRunner = reviewRunner ?? RunModalReview;
        _libraryPicker = libraryPicker ?? PickFromLibraryDialog;
        _exportPicker = exportPicker ?? PickExportDialog;
        _loadedProjectPreflight = loadedProjectPreflight ?? RunLoadedProjectPreflight;
        _pdfExporter = pdfExporter ?? ((artifact, destination, assets, cancellationToken) =>
            AppServices.ExportPdfAsync(
                artifact,
                destination,
                assets,
                cancellationToken: cancellationToken));
        _printViewOpener = printViewOpener ?? AppServices.OpenPrintViewAsync;

        Text = UiStrings.WithoutMnemonic(UiStrings.MainWindowTitle);
        MinimumSize = new Size(860, 560);

        _pressList = new ListBox
        {
            Dock = DockStyle.Fill,
            AccessibleName = UiStrings.WithoutMnemonic(UiStrings.PressList),
            HorizontalScrollbar = true,
        };
        foreach (var definition in PressRoomCatalog.All)
        {
            _pressList.Items.Add(UiStrings.Localize(UiCatalogIds.PressTitle(definition.Id), definition.Title));
        }

        _budget = new Label
        {
            AutoSize = true,
            Text = UiStrings.Format(UiStrings.BudgetLine, PressRoomCatalog.BudgetMinutes),
            AccessibleName = UiStrings.FormatWithoutMnemonic(UiStrings.BudgetLine, PressRoomCatalog.BudgetMinutes),
        };
        _lowInk = new CheckBox { Text = UiStrings.LowInkToggle, AutoSize = true };

        _parameterPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoScroll = true,
        };
        _parameterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _parameterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _parameterPanel.Layout += (_, _) => RefreshParameterScrollExtent();

        _review = MakeButton(UiStrings.ReviewAndApprove, (_, _) => ReviewAndApprove());
        _print = MakeButton(UiStrings.PrintButton, (_, _) => PrintApproved());
        _printView = MakeButton(UiStrings.OpenPrintView, async (_, _) => await OpenPrintViewAsync());
        // Modal-openers defer one beat (harness finding, 29 Aug 2026) — Export
        // included, since the pilot dress rehearsal drives its save dialog.
        _export = MakeButton(UiStrings.ExportEllipsis, (_, _) => QueueExport());
        _cancelExport = MakeButton(UiStrings.CancelExport, (_, _) => _exportCancellation?.Cancel());
        _save = MakeButton(UiStrings.SaveToLibrary, (_, _) => SaveToLibrary());
        _tile = MakeButton(UiStrings.TileForWall, (_, _) => BeginInvoke(ShowTileDialog));
        _openLibrary = MakeButton(UiStrings.OpenFromLibrary, (_, _) => BeginInvoke(OpenFromLibrary));
        _allAboard = MakeButton(UiStrings.AllAboardOpen, (_, _) => BeginInvoke(OpenAllAboard));
        _builtInStudios = MakeButton(UiStrings.BuiltInStudiosOpen, (_, _) => BeginInvoke(OpenModuleStudios));

        // The message itself must be what a screen reader hears, not the word
        // "Status" (a harness finding, 29 Aug 2026).
        _status = ReflowingStatusLabel.Attach(new Label(), minimumHeight: 28);
        SetStatus(UiStrings.StatusReady);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        buttons.Controls.AddRange([_review, _print, _printView, _export, _cancelExport, _save, _tile, _openLibrary, _allAboard, _builtInStudios]);

        var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        right.Controls.Add(_budget, 0, 0);
        right.Controls.Add(_lowInk, 0, 1);
        right.Controls.Add(_parameterPanel, 0, 2);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(_pressList, 0, 0);
        layout.Controls.Add(right, 1, 0);

        Controls.Add(layout);
        Controls.Add(buttons);
        Controls.Add(_status);

        _pressList.SelectedIndexChanged += (_, _) => LoadPress();
        _lowInk.CheckedChanged += (_, _) => InputChanged();
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
        _stateGeneration++;
        _loadingParameters = true;
        ApprovedResult = null;
        _context = null;
        UpdateGatedButtons();

        _parameterPanel.SuspendLayout();
        _parameterPanel.Controls.Clear();
        _parameterPanel.RowCount = 0;
        _valueReaders.Clear();

        if (SelectedPress is not { } definition)
        {
            _parameterPanel.ResumeLayout();
            _loadingParameters = false;
            return;
        }

        foreach (var parameter in definition.Parameters)
        {
            AddParameterRow(parameter);
        }

        _parameterPanel.ResumeLayout();
        _loadingParameters = false;
        SetStatus(UiStrings.StatusReady);
    }

    private void AddParameterRow(PressParameter parameter)
    {
        var pressId = SelectedPress?.Id ?? throw new InvalidOperationException();
        var label = UiStrings.Localize(UiCatalogIds.PressParameter(pressId, parameter.Key), parameter.Label);
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
                    MinimumSize = new Size(110, 0),
                };
                _valueReaders[parameter.Key] = () => spinner.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                control = spinner;
                break;

            case ChoiceParameter choice:
                var combo = new ComboBox
                {
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Width = 160,
                    MinimumSize = new Size(160, 0),
                };
                for (var optionIndex = 0; optionIndex < choice.Options.Count; optionIndex++)
                {
                    var value = choice.Options[optionIndex];
                    combo.Items.Add(new ChoiceDisplayItem(
                        value,
                        UiStrings.Localize(UiCatalogIds.PressChoice(pressId, parameter.Key, value), value)));
                }

                ComboBoxReadingPath.EnsureEveryItemFits(combo);
                combo.SelectedIndex = choice.Options.ToList().IndexOf(choice.Default);
                _valueReaders[parameter.Key] = () => ((ChoiceDisplayItem)combo.SelectedItem!).Value;
                control = combo;
                break;

            case ToggleParameter toggle:
                var check = ReflowingCheckBox.Attach(
                    new CheckBox
                    {
                        Text = label,
                        Checked = toggle.Default,
                        // Dynamic catalog labels treat '&' as authored text. Only
                        // static UiStrings chrome participates in access keys.
                        UseMnemonic = false,
                    },
                    minimumWidth: 160,
                    minimumHeight: 36);
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
                    MinimumSize = new Size(320, 96),
                    Text = lines.DefaultText,
                    AcceptsReturn = true,
                };
                _valueReaders[parameter.Key] = () => multiline.Text;
                control = multiline;
                break;

            case TextParameter text:
                var box = new TextBox
                {
                    Width = 220,
                    MinimumSize = new Size(220, 0),
                    Text = text.Default,
                };
                _valueReaders[parameter.Key] = () => box.Text;
                control = box;
                break;

            default:
                // Developer-facing: the type name alone identifies the gap.
                throw new NotSupportedException(parameter.GetType().Name);
        }

        control.AccessibleName = label;
        switch (control)
        {
            case NumericUpDown spinner:
                spinner.ValueChanged += (_, _) => InputChanged();
                break;
            case ComboBox combo:
                combo.SelectedIndexChanged += (_, _) => InputChanged();
                break;
            case CheckBox check:
                check.CheckedChanged += (_, _) => InputChanged();
                break;
            case TextBox box:
                box.TextChanged += (_, _) => InputChanged();
                break;
        }

        var row = _parameterPanel.RowCount++;
        _parameterPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        if (control is CheckBox)
        {
            _parameterPanel.Controls.Add(control, 1, row);
        }
        else
        {
            _parameterPanel.Controls.Add(new Label
            {
                Text = label,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                // Dynamic parameter labels may contain a literal ampersand.
                UseMnemonic = false,
            }, 0, row);
            _parameterPanel.Controls.Add(control, 1, row);
        }
    }

    private void RefreshParameterScrollExtent()
    {
        var visible = _parameterPanel.Controls.Cast<Control>()
            .Where(control => control.Visible)
            .ToArray();

        // Bounds are display coordinates and move whenever AutoScroll moves;
        // a bound-derived minimum therefore chases the scrollbar, especially
        // in a mirrored layout. Derive the logical extent from the intrinsic
        // requirement of each table row and column instead. This stays stable
        // across scrolling while still reacting to font and locale changes.
        var columnWidths = new int[_parameterPanel.ColumnCount];
        var rowHeights = new int[_parameterPanel.RowCount];
        foreach (var control in visible)
        {
            var preferred = control.GetPreferredSize(Size.Empty);
            var requiredWidth = control is CheckBox
                ? control.MinimumSize.Width
                : Math.Max(control.MinimumSize.Width, preferred.Width);
            var requiredHeight = Math.Max(control.MinimumSize.Height, preferred.Height);
            var column = _parameterPanel.GetColumn(control);
            var row = _parameterPanel.GetRow(control);
            if ((uint)column < (uint)columnWidths.Length)
            {
                columnWidths[column] = Math.Max(
                    columnWidths[column],
                    checked(requiredWidth + control.Margin.Horizontal));
            }

            if ((uint)row < (uint)rowHeights.Length)
            {
                rowHeights[row] = Math.Max(
                    rowHeights[row],
                    checked(requiredHeight + control.Margin.Vertical));
            }
        }

        var desired = new Size(
            checked(columnWidths.Sum() + 16),
            checked(rowHeights.Sum() + 16));
        if (_parameterPanel.AutoScrollMinSize != desired)
        {
            _parameterPanel.AutoScrollMinSize = desired;
        }
    }

    private void ReviewAndApprove()
    {
        if (SelectedPress is not { } definition)
        {
            return;
        }

        ClearApproval();

        ArtifactDocument document;
        try
        {
            document = definition.Build(new PressInputs(
                _valueReaders.ToDictionary(pair => pair.Key, pair => pair.Value(), StringComparer.Ordinal)));
        }
        catch (ArgumentException refusal)
        {
            SetStatus(UiStrings.StatusRefused, refusal.Message);
            _parameterPanel.Controls.OfType<Control>().FirstOrDefault(c => c.CanSelect)?.Focus();
            return;
        }

        if (_lowInk.Checked)
        {
            // Applied BEFORE Gate B: the teacher reviews what will print.
            document = LowInkPress.Apply(document);
        }

        var session = AppServices.SessionOverRecipe(
            DraftArtifact.New(document, DataLane.Green),
            new DefaultArtifactValidator(),
            definition.Recipe);
        var context = new ApprovedContext(definition.Id, "deterministic-press", definition.Recipe.Id, definition.Recipe.Version);
        var generation = _stateGeneration;
        BeginPendingReview();

        if (_modalReview)
        {
            // The modal opens on the next message-loop beat, not re-entrantly
            // from the click: assistive technology and UI Automation see the
            // click complete, then the dialog arrive — no call left pending.
            BeginInvoke(() => CompleteReview(context, session, generation));
        }
        else
        {
            CompleteReview(context, session, generation);
        }
    }

    private void CompleteReview(ApprovedContext context, ReviewSession session, long generation)
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
            _context = context;
            UpdateGatedButtons();
            SetStatus(UiStrings.StatusApproved);
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

    private async Task OpenPrintViewAsync()
    {
        var approved = ApprovedResult;
        var context = _context;
        if (_printViewInProgress || approved is null || context is null)
        {
            return;
        }

        try
        {
            ArtifactSinkAuthorizationGate.DemandPrint(approved, amberAuthorization: null);
        }
        catch (Exception failure) when (IsExpectedPrintViewFailure(failure))
        {
            SetStatus(UiStrings.StatusPrintViewRefused);
            return;
        }

        _printViewInProgress = true;
        UpdateGatedButtons();
        SetStatus(UiStrings.StatusPrintViewOpening);
        try
        {
            await _printViewOpener(
                approved,
                context.Name,
                RenderAudience.Learner,
                100,
                false,
                context.AssetCatalog);
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

    private static bool IsExpectedPrintViewFailure(Exception failure)
        => failure is InvalidOperationException
            or IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException
            or System.ComponentModel.Win32Exception;

    private void PrintApproved()
    {
        if (ApprovedResult is null)
        {
            return;
        }

        try
        {
            AppServices.Print(ApprovedResult, assetCatalog: _context!.AssetCatalog);
            SetStatus(UiStrings.StatusPrinted);
        }
        catch (Exception failure) when (failure is InvalidOperationException or IOException or NotSupportedException)
        {
            // Print failures land in the speaking status, never a dialog trap.
            SetStatus(UiStrings.StatusRefused, failure.Message);
        }
    }

    internal async Task ExportAsync()
    {
        if (_exportInProgress)
        {
            return;
        }

        var approved = ApprovedResult;
        var context = _context;
        if (approved is null || context is null)
        {
            return;
        }

        ExportChoice? choice = null;
        try
        {
            // The picker is part of the Export operation. Refuse the lane
            // before invoking any supplied or production destination picker.
            ArtifactSinkAuthorizationGate.DemandExport(approved, amberAuthorization: null);

            choice = _exportPicker();
            if (choice is null)
            {
                return;
            }

            _exportCancellation = new CancellationTokenSource();
            var exportToken = _exportCancellation.Token;
            _exportInProgress = true;
            UpdateGatedButtons();
            SetStatus(UiStrings.StatusExporting, Path.GetFileName(choice.Path));

            if (choice.FilterIndex == 1)
            {
                await _pdfExporter(approved, choice.Path, context.AssetCatalog, exportToken);
            }
            else
            {
                var bytes = await Task.Run(() => choice.FilterIndex switch
                {
                    2 => ImposeBooklet(approved, exportToken),
                    4 => AppServices.Render(approved, RenderTarget.AccessibleHtml, context.AssetCatalog, exportToken),
                    5 => AppServices.Render(approved, RenderTarget.Svg, context.AssetCatalog, exportToken),
                    _ => AppServices.Render(approved, RenderTarget.PrintHtml, context.AssetCatalog, exportToken),
                }, exportToken);
                await AppServices.WriteExportBytesAsync(choice.Path, bytes, exportToken);
            }
        }
        catch (OperationCanceledException) when (_exportCancellation?.IsCancellationRequested == true)
        {
            SetStatus(UiStrings.StatusExportCancelled);
            return;
        }
        catch (Exception refusal) when (refusal is InvalidOperationException
            or IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException)
        {
            // Single-sheet-only SVG and uniform-page booklets
            // refuse loudly; say so.
            SetStatus(UiStrings.StatusRefused, refusal.Message);
            return;
        }
        finally
        {
            _exportCancellation?.Dispose();
            _exportCancellation = null;
            _exportInProgress = false;
            UpdateGatedButtons();
        }

        SetStatus(UiStrings.StatusExported, Path.GetFileName(choice.Path));
    }

    private void QueueExport()
    {
        if (_exportDispatchPending || _exportInProgress || ApprovedResult is null)
        {
            return;
        }

        _exportDispatchPending = true;
        UpdateGatedButtons();
        try
        {
            BeginInvoke(new Action(async () =>
            {
                try
                {
                    if (!IsDisposed)
                    {
                        await ExportAsync();
                    }
                }
                finally
                {
                    _exportDispatchPending = false;
                    UpdateGatedButtons();
                }
            }));
        }
        catch (InvalidOperationException failure)
        {
            _exportDispatchPending = false;
            UpdateGatedButtons();
            if (!IsDisposed)
            {
                SetStatus(UiStrings.StatusRefused, failure.Message);
            }
        }
    }

    private ExportChoice? PickExportDialog()
    {
        using var dialog = new SaveFileDialog
        {
            FileName = _context!.Name,
            Filter = $"{UiStrings.WithoutMnemonic(UiStrings.ExportFilterPdf)}|*.pdf|{UiStrings.WithoutMnemonic(UiStrings.ExportFilterBooklet)}|*.pdf|{UiStrings.WithoutMnemonic(UiStrings.ExportFilterPrint)}|*.html|{UiStrings.WithoutMnemonic(UiStrings.ExportFilterAccessible)}|*.html|{UiStrings.WithoutMnemonic(UiStrings.ExportFilterSvg)}|*.svg",
        };
        return dialog.ShowDialog(this) == DialogResult.OK
            ? new ExportChoice(dialog.FileName, dialog.FilterIndex)
            : null;
    }

    private static byte[] ImposeBooklet(
        ApprovedArtifact approved,
        CancellationToken cancellationToken)
    {
        var contentPages = approved.Revision.Document.Nodes.OfType<VectorGraphic>().Count();
        if (contentPages < 2)
        {
            throw new NotSupportedException(UiStrings.WithoutMnemonic(UiStrings.BookletNeedsPages));
        }

        return Rendering.VectorPdfWriter.WriteImposed(
            approved,
            BookletImposition.PdfSides(BookletImposition.Compute(contentPages)),
            RenderAudience.Teacher,
            cancellationToken);
    }

    private void SaveToLibrary()
    {
        if (ApprovedResult is null)
        {
            return;
        }

        var hint = AppServices.SaveToLibrary(
            ApprovedResult, _context!.Name, _context.ModuleId,
            _context.RecipeId, _context.RecipeVersion,
            _context.AssetCatalog ?? AppServices.SymbolCatalog());
        SetStatus(UiStrings.StatusSaved, hint);
    }

    /// <summary>Reversibility, visible: a saved project reopens into a fresh Gate B review — reopen, re-review, re-approve, reprint.</summary>
    public void OpenFromLibrary()
    {
        var path = _libraryPicker();
        if (path is null)
        {
            return;
        }

        ClearApproval();

        Storage.LoadedProject loaded;
        try
        {
            loaded = AppServices.OpenFromLibrary(path);
        }
        catch (Exception refusal) when (refusal is InvalidOperationException or IOException or InvalidDataException)
        {
            // The hardened reader's refusals — tampered lane, colliding names,
            // schema drift — arrive here and speak.
            SetStatus(UiStrings.StatusRefused, refusal.Message);
            return;
        }

        // A mutable package cannot authenticate its module or recipe selectors.
        // Any deliberate re-save is a portable semantic edit under an
        // engine-owned identity, never fabricated continuation provenance.
        var context = new ApprovedContext(
                Path.GetFileNameWithoutExtension(path),
                AppServices.PortableProjectModuleId,
                AppServices.PortableProjectRecipeId,
                AppServices.PortableProjectRecipeVersion,
                loaded.Assets);
        ReviewSession session;
        try
        {
            var laneConfirmation = _loadedProjectPreflight(loaded);
            if (laneConfirmation is null)
            {
                SetStatus(UiStrings.StatusLoadedProjectPreflightNotConfirmed);
                return;
            }

            session = AppServices.SessionOverLoadedProject(loaded, laneConfirmation);
        }
        catch (InvalidOperationException refusal)
        {
            SetStatus(UiStrings.StatusRefused, refusal.Message);
            return;
        }
        var generation = ++_stateGeneration;
        BeginPendingReview();
        CompleteReview(context, session, generation);
    }

    private LoadedProjectGreenConfirmation? RunLoadedProjectPreflight(Storage.LoadedProject loaded)
    {
        using var preflight = new LoadedProjectPreflightForm(loaded);
        return preflight.ShowDialog(this) == DialogResult.OK
            ? preflight.Confirmation
            : null;
    }

    private void ShowTileDialog()
    {
        if (ApprovedResult is null)
        {
            return;
        }

        using var dialog = new TileForm();
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            TileApproved(dialog.Columns, dialog.Rows);
        }
    }

    /// <summary>
    /// Big Print Shop over the approved artifact in hand: the tiled wall
    /// display is a NEW document, so it passes Gate B itself before anything
    /// renders — the gate is structural, not hereditary.
    /// </summary>
    public void TileApproved(int columns, int rows)
    {
        if (ApprovedResult is null || _context is null)
        {
            return;
        }

        var sheets = ApprovedResult.Revision.Document.Nodes.OfType<VectorGraphic>().ToList();
        if (sheets.Count != 1)
        {
            SetStatus(UiStrings.StatusRefused, UiStrings.WithoutMnemonic(UiStrings.TileNeedsSingleSheet));
            return;
        }

        ArtifactDocument tiled;
        try
        {
            tiled = BigPrintShop.Tile(sheets[0], columns, rows);
        }
        catch (ArgumentException refusal)
        {
            SetStatus(UiStrings.StatusRefused, refusal.Message);
            return;
        }

        var context = new ApprovedContext(
                _context.Name + "-tiles", "deterministic-press",
                DeterministicPressRecipes.BigPrint.Id, DeterministicPressRecipes.BigPrint.Version);
        var session = AppServices.SessionOverRecipe(
            DraftArtifact.TrustedLayoutDerivative(ApprovedResult, tiled),
            new DefaultArtifactValidator(),
            DeterministicPressRecipes.BigPrint);
        var generation = ++_stateGeneration;
        BeginPendingReview();
        CompleteReview(context, session, generation);
    }

    private static string? PickFromLibraryDialog()
    {
        Directory.CreateDirectory(AppServices.LibraryRoot);
        using var dialog = new OpenFileDialog
        {
            InitialDirectory = AppServices.LibraryRoot,
            Filter = $"*{Storage.OcfprojProjectStore.Extension}|*{Storage.OcfprojProjectStore.Extension}",
        };
        return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : null;
    }

    private void UpdateGatedButtons()
    {
        if (IsDisposed)
        {
            return;
        }

        // The structural gate, visible: these do nothing until a typed approval exists.
        var idle = !_reviewPending
            && !_exportDispatchPending
            && !_exportInProgress
            && !_printViewInProgress;
        var approved = idle && ApprovedResult is not null;
        _review.Enabled = idle;
        _print.Enabled = approved;
        _printView.Enabled = approved;
        _export.Enabled = approved;
        _cancelExport.Enabled = _exportInProgress;
        _save.Enabled = approved;
        _tile.Enabled = approved;
        _pressList.Enabled = idle;
        _parameterPanel.Enabled = idle;
        _lowInk.Enabled = idle;
        _openLibrary.Enabled = idle;
        _allAboard.Enabled = idle;
        _builtInStudios.Enabled = idle;
    }

    private void InputChanged()
    {
        if (_loadingParameters)
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
        _context = null;
        UpdateGatedButtons();
    }

    private void BeginPendingReview()
    {
        _reviewPending = true;
        _pressList.Enabled = false;
        _parameterPanel.Enabled = false;
        _lowInk.Enabled = false;
        UpdateGatedButtons();
    }

    private void EndPendingReview()
    {
        _reviewPending = false;
        _pressList.Enabled = true;
        _parameterPanel.Enabled = true;
        _lowInk.Enabled = true;
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

    private void OpenAllAboard()
    {
        using var form = new AllAboardForm(AppServices.SymbolCatalog());
        form.ShowDialog(this);
    }

    private void OpenModuleStudios()
    {
        using var form = new ModuleStudioForm();
        form.ShowDialog(this);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _exportCancellation?.Cancel();
        base.OnFormClosed(e);
    }
}
