// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.BuiltIn;
using Foundry.Modules.BuiltIn.BoardToBrief;

namespace Foundry.App.WinForms;

/// <summary>
/// One generated, standard-controls-only surface for every built-in module
/// door. The catalog owns fields and builders; this form owns only interaction,
/// fresh Gate B review, and post-approval sinks. Amber doors remain present but
/// have no build delegate, so no UI event or keyboard gesture can waive their
/// district authorization boundary.
/// </summary>
public sealed class ModuleStudioForm : Form
{
    private const string BoardToBriefModeKey = "board-to-brief";

    private readonly Func<ReviewSession, ApprovedArtifact?> _reviewRunner;
    private readonly Func<ExportChoice?>? _exportPicker;
    private readonly Func<string, ReadOnlyMemory<byte>, CancellationToken, Task> _exportWriter;
    private readonly Func<ApprovedArtifact, string, RenderAudience, double, bool, IAssetCatalog?, Task> _printViewOpener;
    private readonly Func<IWin32Window, IReadOnlyList<BriefLine>?> _boardIntakeRunner;
    private readonly bool _modalReview;
    private readonly ListBox _doorList;
    private readonly ComboBox _modeList;
    private readonly FlowLayoutPanel _parameterPanel;
    private readonly ListBox _notes;
    private readonly CheckBox _greenInput;
    private readonly ComboBox _audience;
    private readonly NumericUpDown _textScale;
    private readonly CheckBox _targetLanguageFirst;
    private readonly Button _boardIntake;
    private readonly Button _review;
    private readonly Button _print;
    private readonly Button _printView;
    private readonly Button _export;
    private readonly Button _cancelExport;
    private readonly Button _save;
    private readonly Label _status;
    private readonly Dictionary<string, Func<object?>> _valueReaders = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Control> _fieldContainers = new(StringComparer.Ordinal);
    private bool _loadingFields;
    private bool _reviewPending;
    private bool _exportInProgress;
    private bool _exportDispatchPending;
    private bool _printViewInProgress;
    private bool _formClosing;
    private readonly Lock _exportCancellationSync = new();
    private CancellationTokenSource? _exportCancellation;
    private Task? _exportCancellationWork;
    private Task? _activeExportWork;
    private long _stateGeneration;
    private ApprovedContext? _context;
    private ProjectValidationEnvelope? _validationEnvelope;
    private ProjectRenderProfile? _renderProfile;

    private sealed record DisplayItem<T>(T Value, string Text)
    {
        public override string ToString() => Text;
    }

    private sealed record ChoiceItem(string Value, string Text)
    {
        public override string ToString() => Text;
    }

    private sealed record ApprovedContext(ModuleDoorDefinition Door, ModuleModeDefinition Mode);

    public sealed record ExportChoice(string Path, RenderTarget Target);

    public ModuleStudioForm(
        Func<ReviewSession, ApprovedArtifact?>? reviewRunner = null,
        Func<ExportChoice?>? exportPicker = null,
        Func<string, ReadOnlyMemory<byte>, CancellationToken, Task>? exportWriter = null,
        Func<ApprovedArtifact, string, RenderAudience, double, bool, IAssetCatalog?, Task>? printViewOpener = null,
        Func<IWin32Window, IReadOnlyList<BriefLine>?>? boardIntakeRunner = null)
    {
        _modalReview = reviewRunner is null;
        _reviewRunner = reviewRunner ?? RunModalReview;
        _exportPicker = exportPicker;
        _exportWriter = exportWriter
            ?? ((destination, content, cancellationToken) =>
                AppServices.WriteExportBytesAsync(destination, content, cancellationToken));
        _printViewOpener = printViewOpener ?? AppServices.OpenPrintViewAsync;
        _boardIntakeRunner = boardIntakeRunner ?? RunBoardIntake;

        Text = UiStrings.WithoutMnemonic(UiStrings.ModuleStudioWindowTitle);
        // Keep the ordinary 1180 x 720 design surface, but leave enough
        // headroom for 125% scaling inside the 1366 x 768 hardware floor.
        // The field pane already owns scrolling; a taller minimum only made
        // controls unreachable behind window-manager chrome.
        MinimumSize = new Size(960, 560);
        Size = new Size(1180, 720);
        AutoScaleMode = AutoScaleMode.Dpi;

        _doorList = new ListBox
        {
            Dock = DockStyle.Fill,
            AccessibleName = UiStrings.WithoutMnemonic(UiStrings.ModuleDoors),
            HorizontalScrollbar = true,
            IntegralHeight = false,
        };
        foreach (var door in ModuleStudioCatalog.All)
        {
            _doorList.Items.Add(new DisplayItem<ModuleDoorDefinition>(door, Display(door.Display)));
        }

        _modeList = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            AccessibleName = UiStrings.WithoutMnemonic(UiStrings.ModuleMode),
        };

        _parameterPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AccessibleName = UiStrings.WithoutMnemonic(UiStrings.ModuleInputs),
        };

        _notes = new ListBox
        {
            Dock = DockStyle.Fill,
            // Safeguards are deliberately exact, sometimes sentence-length
            // statements. A single-line ListBox must provide its native
            // horizontal reading path instead of silently clipping that text.
            HorizontalScrollbar = true,
            IntegralHeight = false,
            AccessibleName = UiStrings.WithoutMnemonic(UiStrings.ModuleNotes),
        };

        _greenInput = new CheckBox
        {
            AutoSize = true,
            Text = UiStrings.GreenInputAttestation,
            AccessibleName = UiStrings.WithoutMnemonic(UiStrings.GreenInputAttestation),
        };

        _audience = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 180,
            AccessibleName = UiStrings.WithoutMnemonic(UiStrings.OutputAudience),
        };
        _audience.Items.AddRange(
        [
            new DisplayItem<RenderAudience>(
                RenderAudience.Teacher,
                UiStrings.WithoutMnemonic(UiStrings.AudienceTeacher)),
            new DisplayItem<RenderAudience>(
                RenderAudience.Learner,
                UiStrings.WithoutMnemonic(UiStrings.AudienceLearner)),
        ]);
        _audience.SelectedIndex = 0;

        _textScale = new NumericUpDown
        {
            Minimum = 100,
            Maximum = 200,
            Increment = 25,
            Value = 100,
            Width = 90,
            AccessibleName = UiStrings.WithoutMnemonic(UiStrings.TextScalePercent),
        };
        _targetLanguageFirst = new CheckBox
        {
            AutoSize = true,
            Text = UiStrings.TargetLanguageFirst,
            AccessibleName = UiStrings.WithoutMnemonic(UiStrings.TargetLanguageFirst),
        };

        _boardIntake = MakeButton(UiStrings.ImportAndVerifyBoard, (_, _) => ImportAndVerifyBoard());
        _boardIntake.Name = "board-to-brief-intake";
        _boardIntake.Visible = false;
        _review = MakeButton(UiStrings.ReviewAndApprove, (_, _) => ReviewAndApprove());
        _print = MakeButton(UiStrings.PrintButton, (_, _) => PrintApproved());
        _printView = MakeButton(UiStrings.OpenPrintView, async (_, _) => await OpenPrintViewAsync());
        _export = MakeButton(UiStrings.ExportEllipsis, (_, _) => QueueExport());
        _cancelExport = MakeButton(UiStrings.CancelExport, (_, _) => _ = RequestExportCancellation());
        _save = MakeButton(UiStrings.SaveToLibrary, (_, _) => SaveToLibrary());

        _status = new Label { Dock = DockStyle.Bottom, AutoSize = false, Height = 34, UseMnemonic = false };
        SetStatus(UiStrings.StatusModuleReady);

        var modeRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoSize = false,
            Height = 32,
        };
        modeRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        modeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        modeRow.Controls.Add(new Label { Text = UiStrings.ModuleMode, AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        modeRow.Controls.Add(_modeList, 1, 0);

        var laneGroup = new GroupBox
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Text = UiStrings.ModuleLaneConfirmation,
            AccessibleName = UiStrings.WithoutMnemonic(UiStrings.ModuleLaneConfirmation),
        };
        laneGroup.Controls.Add(_greenInput);

        var outputOptions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true,
        };
        outputOptions.Controls.Add(new Label { Text = UiStrings.OutputAudience, AutoSize = true, Anchor = AnchorStyles.Left });
        outputOptions.Controls.Add(_audience);
        outputOptions.Controls.Add(new Label { Text = UiStrings.TextScalePercent, AutoSize = true, Anchor = AnchorStyles.Left });
        outputOptions.Controls.Add(_textScale);
        outputOptions.Controls.Add(_targetLanguageFirst);

        var outputGroup = new GroupBox
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Text = UiStrings.ModuleOutputOptions,
            AccessibleName = UiStrings.WithoutMnemonic(UiStrings.ModuleOutputOptions),
        };
        outputGroup.Controls.Add(outputOptions);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true,
        };
        buttons.Controls.AddRange([_boardIntake, _review, _print, _printView, _export, _cancelExport, _save]);

        var right = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            AutoScroll = true,
        };
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 68));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 32));
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.Controls.Add(modeRow, 0, 0);
        right.Controls.Add(laneGroup, 0, 1);
        right.Controls.Add(_parameterPanel, 0, 2);
        right.Controls.Add(_notes, 0, 3);
        right.Controls.Add(outputGroup, 0, 4);
        right.Controls.Add(buttons, 0, 5);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            AutoScroll = true,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 76));
        layout.Controls.Add(_doorList, 0, 0);
        layout.Controls.Add(right, 1, 0);

        Controls.Add(layout);
        Controls.Add(_status);

        _doorList.SelectedIndexChanged += (_, _) => LoadDoor();
        _modeList.SelectedIndexChanged += (_, _) => LoadMode();
        _greenInput.CheckedChanged += (_, _) => GreenConfirmationChanged();
        _audience.SelectedIndexChanged += (_, _) => OutputOptionChanged();
        _textScale.ValueChanged += (_, _) => OutputOptionChanged();
        _targetLanguageFirst.CheckedChanged += (_, _) => OutputOptionChanged();
        _doorList.SelectedIndex = 0;

        UiLocale.ApplyChrome(this);
    }

    public ModuleDoorDefinition? SelectedDoor
        => (_doorList.SelectedItem as DisplayItem<ModuleDoorDefinition>)?.Value;

    public ModuleModeDefinition? SelectedMode
        => (_modeList.SelectedItem as DisplayItem<ModuleModeDefinition>)?.Value;

    public ApprovedArtifact? ApprovedResult { get; private set; }

    public string StatusText => _status.Text;

    private static string Display(ModuleDisplayText text)
        => UiStrings.Localize(text.LocalizationId, text.Fallback);

    private static Button MakeButton(string text, EventHandler click)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            AccessibleName = UiStrings.WithoutMnemonic(text),
        };
        button.Click += click;
        return button;
    }

    private void LoadDoor()
    {
        if (SelectedDoor is not { } door)
        {
            return;
        }

        _loadingFields = true;
        _modeList.Items.Clear();
        foreach (var mode in door.Modes)
        {
            _modeList.Items.Add(new DisplayItem<ModuleModeDefinition>(mode, Display(mode.Display)));
        }

        _modeList.SelectedIndex = 0;
        _modeList.Enabled = door.Modes.Count > 1;
        _loadingFields = false;
        LoadMode();
    }

    private void LoadMode()
    {
        if (_loadingFields || SelectedDoor is not { } door || SelectedMode is not { } mode)
        {
            return;
        }

        _stateGeneration++;
        _loadingFields = true;
        ClearApproval();
        _parameterPanel.SuspendLayout();
        _parameterPanel.Controls.Clear();
        _valueReaders.Clear();
        _fieldContainers.Clear();

        foreach (var field in mode.Fields)
        {
            AddField(field);
        }

        _boardIntake.Visible = string.Equals(mode.Key, BoardToBriefModeKey, StringComparison.Ordinal);

        // Catalog-owned synthetic starters are known Green. Every free-text or
        // table edit clears this state, because automated code may escalate a
        // lane but must never certify teacher-entered prose as Green.
        _greenInput.Checked = mode.DefaultsAreSynthetic;
        _greenInput.Enabled = mode.IsBuildAvailable;
        _parameterPanel.Enabled = mode.IsBuildAvailable;
        _parameterPanel.ResumeLayout();
        PopulateNotes(door, mode, []);
        _loadingFields = false;
        UpdateConditions();
        if (!mode.IsBuildAvailable)
        {
            SetStatus(UiStrings.StatusModuleUnavailable, Display(mode.UnavailableReason!));
        }
        else
        {
            SetStatus(ReadinessStatus());
        }

        UpdateGatedButtons();
    }

    private void AddField(ModuleFieldDefinition field)
    {
        var label = Display(field.Display);
        var group = new GroupBox
        {
            AutoSize = false,
            Width = 780,
            Height = field.Kind is ModuleFieldKind.Multiline or ModuleFieldKind.Lines or ModuleFieldKind.RecordTable ? 170 : 78,
            // GroupBox has no UseMnemonic switch, so escape the native prefix
            // marker while preserving the exact catalog text for accessibility.
            Text = EscapeMnemonicMarkers(label),
            AccessibleName = label,
            Padding = new Padding(10),
        };
        _fieldContainers[field.Key] = group;

        Control control = field.Kind switch
        {
            ModuleFieldKind.Text => TextInput(field, multiline: false),
            ModuleFieldKind.Multiline or ModuleFieldKind.Lines => TextInput(field, multiline: true),
            ModuleFieldKind.Integer => IntegerInput(field),
            ModuleFieldKind.Toggle => ToggleInput(field),
            ModuleFieldKind.Choice => ChoiceInput(field),
            ModuleFieldKind.RecordTable => RecordTable(field),
            ModuleFieldKind.ApprovedArtifact => ApprovedArtifactInput(field),
            ModuleFieldKind.Notice => Notice(field),
            _ => throw new NotSupportedException(field.Kind.ToString()),
        };

        control.Dock = DockStyle.Fill;
        control.AccessibleName ??= label;
        if (field.IsSensitive)
        {
            control.AccessibleDescription = UiStrings.WithoutMnemonic(UiStrings.ModuleSensitiveInput);
        }

        group.Controls.Add(control);
        _parameterPanel.Controls.Add(group);
    }

    private TextBox TextInput(ModuleFieldDefinition field, bool multiline)
    {
        var box = new TextBox
        {
            Multiline = multiline,
            AcceptsReturn = multiline,
            ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None,
            Text = field.DefaultValue?.ToString() ?? "",
        };
        box.TextChanged += (_, _) => ContentInputChanged();
        _valueReaders[field.Key] = () => box.Text;
        return box;
    }

    private NumericUpDown IntegerInput(ModuleFieldDefinition field)
    {
        var spinner = new NumericUpDown
        {
            Minimum = field.Minimum ?? int.MinValue,
            Maximum = field.Maximum ?? int.MaxValue,
            Value = decimal.Parse(field.DefaultValue?.ToString() ?? "0", CultureInfo.InvariantCulture),
            Width = 120,
        };
        spinner.ValueChanged += (_, _) => BoundedInputChanged();
        _valueReaders[field.Key] = () => spinner.Value.ToString(CultureInfo.InvariantCulture);
        return spinner;
    }

    private CheckBox ToggleInput(ModuleFieldDefinition field)
    {
        var check = new CheckBox
        {
            AutoSize = true,
            Text = Display(field.Display),
            Checked = string.Equals(field.DefaultValue?.ToString(), "true", StringComparison.Ordinal),
            // Dynamic module labels treat '&' literally; access keys belong
            // only to the separately validated static chrome inventory.
            UseMnemonic = false,
        };
        check.CheckedChanged += (_, _) => BoundedInputChanged();
        _valueReaders[field.Key] = () => check.Checked ? "true" : "false";
        return check;
    }

    private ComboBox ChoiceInput(ModuleFieldDefinition field)
    {
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var choice in field.Choices)
        {
            combo.Items.Add(new ChoiceItem(choice.Value, Display(choice.Display)));
        }

        combo.SelectedIndex = field.Choices.ToList().FindIndex(choice =>
            string.Equals(choice.Value, field.DefaultValue?.ToString(), StringComparison.Ordinal));
        combo.SelectedIndexChanged += (_, _) => BoundedInputChanged();
        _valueReaders[field.Key] = () => (combo.SelectedItem as ChoiceItem)?.Value;
        return combo;
    }

    private DataGridView RecordTable(ModuleFieldDefinition field)
    {
        var grid = new DataGridView
        {
            AutoGenerateColumns = false,
            AllowUserToAddRows = true,
            AllowUserToDeleteRows = true,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            AccessibleDescription = UiStrings.WithoutMnemonic(UiStrings.RecordTableHint),
        };

        foreach (var column in field.Columns)
        {
            DataGridViewColumn gridColumn;
            if (column.Choices.Count > 0)
            {
                gridColumn = new DataGridViewComboBoxColumn
                {
                    DataSource = column.Choices.Select(choice => new ChoiceItem(choice.Value, Display(choice.Display))).ToList(),
                    DisplayMember = nameof(ChoiceItem.Text),
                    ValueMember = nameof(ChoiceItem.Value),
                    ValueType = typeof(string),
                    FlatStyle = FlatStyle.Flat,
                };
            }
            else
            {
                gridColumn = new DataGridViewTextBoxColumn();
            }

            gridColumn.Name = column.Key;
            gridColumn.HeaderText = Display(column.Display);
            grid.Columns.Add(gridColumn);
        }

        foreach (var line in (field.DefaultValue?.ToString() ?? "")
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            grid.Rows.Add([.. line.Split('|').Select(cell => (object)cell.Trim())]);
        }

        grid.CellValueChanged += (_, _) => ContentInputChanged();
        grid.RowsAdded += (_, _) => ContentInputChanged();
        grid.RowsRemoved += (_, _) => ContentInputChanged();
        grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (grid.IsCurrentCellDirty)
            {
                grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };
        _valueReaders[field.Key] = () => grid.Rows
            .Cast<DataGridViewRow>()
            .Where(row => !row.IsNewRow)
            .Select(row => (IEnumerable<string>)[.. row.Cells.Cast<DataGridViewCell>().Select(cell => cell.Value?.ToString() ?? "")])
            .ToArray();
        return grid;
    }

    private TextBox ApprovedArtifactInput(ModuleFieldDefinition field)
    {
        var selected = new TextBox
        {
            ReadOnly = true,
            Text = UiStrings.WithoutMnemonic(UiStrings.AccessPurposeAuthorityAbsent),
            AccessibleName = Display(field.Display),
            Dock = DockStyle.Fill,
        };
        _valueReaders[field.Key] = () => null;
        return selected;
    }

    private static Label Notice(ModuleFieldDefinition field)
        => new()
        {
            AutoSize = false,
            Text = Display(field.Display),
            AccessibleName = Display(field.Display),
            UseMnemonic = false,
        };

    private static string EscapeMnemonicMarkers(string text)
        => text.Replace("&", "&&", StringComparison.Ordinal);

    private void ReviewAndApprove()
    {
        if (SelectedDoor is not { } door || SelectedMode is not { Build: not null } mode)
        {
            return;
        }

        if (!_greenInput.Checked)
        {
            SetStatus(UiStrings.StatusModuleGreenRequired);
            return;
        }

        ClearApproval();
        ModuleBuildOutcome outcome;
        try
        {
            outcome = mode.Build(new ModuleInputValues(
                _valueReaders.ToDictionary(pair => pair.Key, pair => pair.Value(), StringComparer.Ordinal)));
        }
        catch (Exception refusal) when (refusal is ArgumentException or InvalidOperationException)
        {
            SetStatus(UiStrings.StatusRefused, refusal.Message);
            _parameterPanel.SelectNextControl(null, true, true, true, false);
            return;
        }

        PopulateNotes(door, mode, outcome.TransformationReport);
        var blockingIssues = outcome.Issues
            .Where(issue => issue.Severity == ValidationSeverity.Blocking)
            .ToList();
        if (blockingIssues.Count > 0)
        {
            foreach (var issue in blockingIssues)
            {
                _notes.Items.Add(UiStrings.FormatWithoutMnemonic(
                    UiStrings.ModuleIssueDetail,
                    issue.Code,
                    issue.Message));
            }

            SetStatus(UiStrings.StatusModuleBuiltWithIssues, blockingIssues.Count);
            UpdateGatedButtons();
            return;
        }

        var session = AppServices.SessionOver(
            outcome.CreateDraft(),
            outcome.Validator,
            new ReviewViewContext(new RenderRequest(
                RenderTarget.PrintHtml,
                SelectedAudience(),
                (double)_textScale.Value,
                _targetLanguageFirst.Checked)));
        var context = new ApprovedContext(door, mode);
        var generation = _stateGeneration;
        BeginPendingReview();
        if (_modalReview)
        {
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
                SetStatus(UiStrings.StatusModuleNotApproved);
                UpdateGatedButtons();
                return;
            }

            ApprovedResult = approved;
            _context = context;
            _validationEnvelope = ProjectValidationEnvelope.Exact(
                approved,
                context.Mode.Recipe.Id,
                context.Mode.Recipe.Version);
            _renderProfile = ProjectRenderProfile.For(
                approved,
                SelectedAudience(),
                (double)_textScale.Value,
                _targetLanguageFirst.Checked);
            SetStatus(UiStrings.StatusModuleApproved);
            UpdateGatedButtons();
        }
        finally
        {
            EndPendingReview();
        }
    }

    private void BeginPendingReview()
    {
        _reviewPending = true;
        SetMutableControlsEnabled(enabled: false);
        UpdateGatedButtons();
    }

    private void EndPendingReview()
    {
        _reviewPending = false;
        SetMutableControlsEnabled(enabled: true);
        UpdateGatedButtons();
    }

    private void SetMutableControlsEnabled(bool enabled)
    {
        _doorList.Enabled = enabled;
        _modeList.Enabled = enabled && (SelectedDoor?.Modes.Count ?? 0) > 1;
        _parameterPanel.Enabled = enabled && SelectedMode is { IsBuildAvailable: true };
        _greenInput.Enabled = enabled && SelectedMode is { IsBuildAvailable: true };
        _audience.Enabled = enabled;
        _textScale.Enabled = enabled;
        _targetLanguageFirst.Enabled = enabled;
        _boardIntake.Enabled = enabled
            && SelectedMode is { Key: BoardToBriefModeKey, IsBuildAvailable: true };
    }

    private ApprovedArtifact? RunModalReview(ReviewSession session)
    {
        using var review = new ReviewForm(session);
        return review.ShowDialog(this) == DialogResult.OK ? review.Result : null;
    }

    private static IReadOnlyList<BriefLine>? RunBoardIntake(IWin32Window owner)
    {
        using var intake = new BoardToBriefIntakeForm();
        return intake.ShowDialog(owner) == DialogResult.OK ? intake.ResultLines : null;
    }

    private void ImportAndVerifyBoard()
    {
        if (SelectedMode is not { Key: BoardToBriefModeKey, IsBuildAvailable: true })
        {
            return;
        }

        var lines = _boardIntakeRunner(this);
        if (lines is null)
        {
            return;
        }

        if (lines.Count == 0
            || lines.Any(line => line is null
                || string.IsNullOrWhiteSpace(line.Text)
                || !Enum.IsDefined(line.Role))
            || lines.Count(line => line.Role == BriefRole.Title) != 1)
        {
            SetStatus(UiStrings.StatusBoardIntakeRowsRefused);
            return;
        }

        if (!_fieldContainers.TryGetValue("lines", out var container)
            || container.Controls.OfType<DataGridView>().SingleOrDefault() is not { } grid)
        {
            SetStatus(UiStrings.StatusBoardIntakeTableUnavailable);
            return;
        }

        _stateGeneration++;
        _loadingFields = true;
        try
        {
            grid.Rows.Clear();
            foreach (var line in lines)
            {
                grid.Rows.Add(line.Text, BoardRoleValue(line.Role));
            }

            // Transcription and role decisions occur after source-lane
            // confirmation. They never inherit that attestation or stale Gate B.
            _greenInput.Checked = false;
        }
        finally
        {
            _loadingFields = false;
        }

        ClearApproval();
        UpdateConditions();
        SetStatus(UiStrings.StatusBoardIntakeImported);
        UpdateGatedButtons();
    }

    private static string BoardRoleValue(BriefRole role) => role switch
    {
        BriefRole.Title => "title",
        BriefRole.Step => "step",
        BriefRole.Material => "material",
        BriefRole.Vocabulary => "vocabulary",
        BriefRole.Date => "date",
        BriefRole.Note => "note",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    };

    private async Task OpenPrintViewAsync()
    {
        var approved = ApprovedResult;
        var context = _context;
        if (_printViewInProgress || approved is null || context is null)
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
                PublicFileStem(context),
                SelectedAudience(),
                (double)_textScale.Value,
                _targetLanguageFirst.Checked,
                null);
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

    private void PrintApproved()
    {
        if (ApprovedResult is null)
        {
            return;
        }

        try
        {
            AppServices.Print(
                ApprovedResult,
                SelectedAudience(),
                (double)_textScale.Value,
                _targetLanguageFirst.Checked);
            SetStatus(UiStrings.StatusPrinted);
        }
        catch (Exception refusal) when (refusal is InvalidOperationException or IOException or NotSupportedException)
        {
            SetStatus(UiStrings.StatusRefused, refusal.Message);
        }
    }

    private async Task ExportAsync()
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

        var exportCancellation = new CancellationTokenSource();
        lock (_exportCancellationSync)
        {
            _exportCancellation = exportCancellation;
            _exportCancellationWork = null;
        }

        var exportToken = exportCancellation.Token;
        _exportInProgress = true;
        UpdateGatedButtons();

        ExportChoice? choice = null;
        try
        {
            choice = _exportPicker is null
                ? PickExportDialog(context)
                : _exportPicker();
            exportToken.ThrowIfCancellationRequested();
            if (_formClosing || IsDisposed || Disposing)
            {
                throw new OperationCanceledException(exportToken);
            }

            if (choice is null)
            {
                return;
            }

            if (!SupportedExportTargets(context.Mode).Contains(choice.Target))
            {
                SetStatus(UiStrings.StatusRefused, choice.Target.ToString());
                return;
            }

            SetStatus(UiStrings.StatusExporting, Path.GetFileName(choice.Path));

            var request = new RenderRequest(
                choice.Target,
                SelectedAudience(),
                (double)_textScale.Value,
                _targetLanguageFirst.Checked);
            var bytes = await Task.Run(
                () => AppServices.Render(approved, request, cancellationToken: exportToken),
                exportToken);
            exportToken.ThrowIfCancellationRequested();
            if (_formClosing || IsDisposed || Disposing)
            {
                throw new OperationCanceledException(exportToken);
            }

            // Run the sink outside the WinForms synchronization context and
            // retain the operation itself. Dispose can then cancel and drain
            // the writer's stage cleanup without deadlocking on a continuation
            // that needs the UI thread.
            var exportWork = Task.Run(
                () => _exportWriter(choice.Path, bytes, exportToken),
                CancellationToken.None);
            Volatile.Write(ref _activeExportWork, exportWork);
            try
            {
                await exportWork;
            }
            finally
            {
                _ = Interlocked.CompareExchange(ref _activeExportWork, null, exportWork);
            }
        }
        catch (OperationCanceledException)
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
            SetStatus(UiStrings.StatusRefused, refusal.Message);
            return;
        }
        finally
        {
            ReleaseExportCancellation(exportCancellation);
            _exportInProgress = false;
            UpdateGatedButtons();
        }

        SetStatus(UiStrings.StatusExported, Path.GetFileName(choice.Path));
    }

    private void QueueExport()
    {
        if (_formClosing
            || _exportDispatchPending
            || _exportInProgress
            || ApprovedResult is null
            || _context is null)
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
            SetStatus(UiStrings.StatusRefused, failure.Message);
        }
    }

    private ExportChoice? PickExportDialog(ApprovedContext context)
    {
        var mode = context.Mode;
        var targets = SupportedExportTargets(mode);
        if (targets.Count == 0)
        {
            return null;
        }

        using var dialog = new SaveFileDialog
        {
            FileName = PublicFileStem(context),
            Filter = string.Join('|', targets.Select(target =>
                $"{ExportLabel(target)}|*.html")),
        };
        return dialog.ShowDialog(this) == DialogResult.OK
            ? new ExportChoice(dialog.FileName, targets[dialog.FilterIndex - 1])
            : null;
    }

    private static string ExportLabel(RenderTarget target)
        => target == RenderTarget.AccessibleHtml
            ? UiStrings.WithoutMnemonic(UiStrings.ExportFilterModuleAccessible)
            : UiStrings.WithoutMnemonic(UiStrings.ExportFilterModulePrint);

    private static IReadOnlyList<RenderTarget> SupportedExportTargets(ModuleModeDefinition mode)
        => [.. mode.Recipe.SupportedExports
            .Where(target => target is RenderTarget.AccessibleHtml or RenderTarget.PrintHtml)
            .Distinct()];

    private void SaveToLibrary()
    {
        if (ApprovedResult is null || _context is null)
        {
            return;
        }

        var name = AppServices.SaveToLibrary(
            ApprovedResult,
            PublicFileStem(_context),
            _context.Door.Id,
            _context.Mode.Recipe.Id,
            _context.Mode.Recipe.Version,
            AppServices.SymbolCatalog(),
            _validationEnvelope,
            _renderProfile);
        SetStatus(UiStrings.StatusSaved, name);
    }

    private void PopulateNotes(
        ModuleDoorDefinition door,
        ModuleModeDefinition mode,
        IReadOnlyList<string> transformationReport)
    {
        _notes.Items.Clear();
        _notes.Items.Add(UiStrings.FormatWithoutMnemonic(
            UiStrings.ModuleLaneAndRecipe,
            mode.Lane,
            Display(door.Display),
            mode.Recipe.Version));
        if (mode.DefaultsAreSynthetic)
        {
            _notes.Items.Add(UiStrings.WithoutMnemonic(UiStrings.ModuleSyntheticStarter));
        }

        if (mode.UnavailableReason is not null)
        {
            _notes.Items.Add(Display(mode.UnavailableReason));
        }

        foreach (var prohibited in mode.Recipe.ProhibitedPurposes)
        {
            _notes.Items.Add(UiStrings.FormatWithoutMnemonic(UiStrings.ModuleProhibitedPurpose, prohibited));
        }

        foreach (var warning in mode.Recipe.Warnings.Concat(transformationReport).Distinct(StringComparer.Ordinal))
        {
            _notes.Items.Add(warning);
        }
    }

    private RenderAudience SelectedAudience()
        => (_audience.SelectedItem as DisplayItem<RenderAudience>)?.Value ?? RenderAudience.Teacher;

    private static string PublicFileStem(ApprovedContext context)
        => PublicFileStem(context.Door, context.Mode);

    internal static string PublicFileStem(ModuleDoorDefinition door, ModuleModeDefinition mode)
        => ModulePublicIdentity.FileStemFor(door, mode);

    private void ContentInputChanged()
    {
        if (_loadingFields)
        {
            return;
        }

        _stateGeneration++;
        _loadingFields = true;
        _greenInput.Checked = false;
        _loadingFields = false;
        ClearApproval();
        UpdateConditions();
        SetStatus(UiStrings.StatusModuleGreenRequired);
        UpdateGatedButtons();
    }

    private void BoundedInputChanged()
    {
        if (_loadingFields)
        {
            return;
        }

        _stateGeneration++;
        ClearApproval();
        UpdateConditions();
        SetStatus(IsInputReady() ? UiStrings.StatusModuleChanged : ReadinessStatus());
        UpdateGatedButtons();
    }

    private void OutputOptionChanged()
    {
        if (_loadingFields)
        {
            return;
        }

        _stateGeneration++;
        ClearApproval();
        SetStatus(IsInputReady() ? UiStrings.StatusModuleChanged : ReadinessStatus());
        UpdateGatedButtons();
    }

    private void GreenConfirmationChanged()
    {
        if (_loadingFields)
        {
            return;
        }

        _stateGeneration++;
        if (SelectedMode is { IsBuildAvailable: false, UnavailableReason: { } reason })
        {
            _loadingFields = true;
            _greenInput.Checked = false;
            _loadingFields = false;
            ClearApproval();
            SetStatus(UiStrings.StatusModuleUnavailable, Display(reason));
            UpdateGatedButtons();
            return;
        }

        ClearApproval();
        SetStatus(ReadinessStatus());
        UpdateGatedButtons();
    }

    private void UpdateConditions()
    {
        if (SelectedMode is not { } mode)
        {
            return;
        }

        foreach (var field in mode.Fields.Where(field => field.Condition is not null))
        {
            var condition = field.Condition!;
            var actual = _valueReaders.TryGetValue(condition.FieldKey, out var reader)
                ? reader()?.ToString()
                : null;
            var equals = string.Equals(actual, condition.SubmittedValue, StringComparison.Ordinal);
            _fieldContainers[field.Key].Visible = condition.EqualsSubmittedValue ? equals : !equals;
        }
    }

    private void ClearApproval()
    {
        ApprovedResult = null;
        _context = null;
        _validationEnvelope = null;
        _renderProfile = null;
        UpdateGatedButtons();
    }

    private bool IsInputReady() => _greenInput.Checked;

    private string ReadinessStatus()
        => !_greenInput.Checked
            ? UiStrings.StatusModuleGreenRequired
            : UiStrings.StatusModuleReady;

    private void UpdateGatedButtons()
    {
        if (IsDisposed)
        {
            return;
        }

        var idle = !_reviewPending
            && !_exportDispatchPending
            && !_exportInProgress
            && !_printViewInProgress;
        var approved = idle && ApprovedResult is not null && _context is not null;
        var supported = _context is null ? [] : SupportedExportTargets(_context.Mode);
        SetMutableControlsEnabled(idle);
        _review.Enabled = idle
            && SelectedMode is { IsBuildAvailable: true }
            && IsInputReady();
        _boardIntake.Enabled = idle
            && SelectedMode is { Key: BoardToBriefModeKey, IsBuildAvailable: true };
        _print.Enabled = approved && supported.Contains(RenderTarget.PrintHtml);
        _printView.Enabled = approved && supported.Contains(RenderTarget.PrintHtml);
        _export.Enabled = approved && supported.Count > 0;
        _cancelExport.Enabled = _exportInProgress;
        _save.Enabled = approved;
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

    private Task RequestExportCancellation()
    {
        lock (_exportCancellationSync)
        {
            if (_exportCancellationWork is not null)
            {
                return _exportCancellationWork;
            }

            if (_exportCancellation is null)
            {
                return Task.CompletedTask;
            }

            // CancelAsync marks the token before returning but runs registered
            // callbacks away from this WinForms call stack. Retain and observe
            // that work: a callback may be slow, may fault, or may outlive the
            // surface, but none of those conditions may seize the UI thread.
            _exportCancellationWork = _exportCancellation.CancelAsync();
            ObserveFault(_exportCancellationWork);
            return _exportCancellationWork;
        }
    }

    private void ReleaseExportCancellation(CancellationTokenSource cancellation)
    {
        Task? cancellationWork;
        lock (_exportCancellationSync)
        {
            if (!ReferenceEquals(_exportCancellation, cancellation))
            {
                return;
            }

            cancellationWork = _exportCancellationWork;
            _exportCancellation = null;
            _exportCancellationWork = null;
        }

        if (cancellationWork is { IsCompleted: false })
        {
            var deferredDispose = cancellationWork.ContinueWith(
                static (_, state) => ((CancellationTokenSource)state!).Dispose(),
                cancellation,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            ObserveFault(deferredDispose);
            return;
        }

        cancellation.Dispose();
    }

    private static void ObserveFault(Task work)
        => _ = work.ContinueWith(
            static completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

    private static void DrainExportShutdown(Task cancellationWork, Task? exportWork)
    {
        var shutdown = exportWork is null
            ? cancellationWork
            : Task.WhenAll(cancellationWork, exportWork);
        ObserveFault(shutdown);

        try
        {
            // One ceiling covers both callback dispatch and the active writer.
            // Cooperative storage drains here. A stalled callback or storage
            // operation may truthfully continue after the form is gone.
            _ = shutdown.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // Wait observed the terminal fault/cancellation. Cancellation
            // callback failures belong to teardown evidence, not the UI stack.
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _formClosing = true;
        _ = RequestExportCancellation();
        base.OnFormClosing(e);
        if (e.Cancel)
        {
            _formClosing = false;
        }
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing && !IsDisposed)
            {
                // Dispose() does not raise FormClosing. Treat it as the same
                // terminal boundary without running untrusted token callbacks
                // on the WinForms thread. Cooperative writers finish their
                // owned stage cleanup; stalled work remains bounded.
                _formClosing = true;
                var cancellationWork = RequestExportCancellation();
                var exportWork = Volatile.Read(ref _activeExportWork);
                if (exportWork is not null)
                {
                    ObserveFault(exportWork);
                }

                DrainExportShutdown(cancellationWork, exportWork);
            }
        }
        finally
        {
            // A throwing callback is represented by the retained task and can
            // never prevent the actual WinForms surface from being disposed.
            base.Dispose(disposing);
        }
    }
}
