// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.BuiltIn;

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
    private readonly Func<ReviewSession, ApprovedArtifact?> _reviewRunner;
    private readonly Func<string?> _libraryPicker;
    private readonly Func<ExportChoice?>? _exportPicker;
    private readonly Func<Storage.LoadedProject, LoadedProjectGreenConfirmation?> _loadedProjectPreflight;
    private readonly bool _modalReview;
    private readonly ListBox _doorList;
    private readonly ComboBox _modeList;
    private readonly FlowLayoutPanel _parameterPanel;
    private readonly ListBox _notes;
    private readonly CheckBox _greenInput;
    private readonly CheckBox _classroomSupportPurpose;
    private readonly ComboBox _audience;
    private readonly NumericUpDown _textScale;
    private readonly CheckBox _targetLanguageFirst;
    private readonly Button _review;
    private readonly Button _print;
    private readonly Button _printView;
    private readonly Button _export;
    private readonly Button _save;
    private readonly Label _status;
    private readonly Dictionary<string, Func<object?>> _valueReaders = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Control> _fieldContainers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ApprovedArtifact> _approvedInputs = new(StringComparer.Ordinal);
    private bool _loadingFields;
    private bool _reviewPending;
    private long _stateGeneration;
    private ApprovedContext? _context;
    private ProjectValidationEnvelope? _validationEnvelope;
    private ProjectRenderProfile? _renderProfile;
    private ApprovedArtifact? _trustedCurrentClassroomSupport;

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
        Func<string?>? libraryPicker = null,
        Func<ExportChoice?>? exportPicker = null,
        Func<Storage.LoadedProject, LoadedProjectGreenConfirmation?>? loadedProjectPreflight = null)
    {
        _modalReview = reviewRunner is null;
        _reviewRunner = reviewRunner ?? RunModalReview;
        _libraryPicker = libraryPicker ?? PickFromLibraryDialog;
        _exportPicker = exportPicker;
        _loadedProjectPreflight = loadedProjectPreflight ?? RunLoadedProjectPreflight;

        Text = UiStrings.ModuleStudioWindowTitle;
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
            AccessibleName = UiStrings.ModuleDoors,
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
            AccessibleName = UiStrings.ModuleMode,
        };

        _parameterPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AccessibleName = UiStrings.ModuleInputs,
        };

        _notes = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
            AccessibleName = UiStrings.ModuleNotes,
        };

        _greenInput = new CheckBox
        {
            AutoSize = true,
            Text = UiStrings.GreenInputAttestation,
            AccessibleName = UiStrings.GreenInputAttestation,
        };

        _classroomSupportPurpose = new CheckBox
        {
            AutoSize = true,
            Text = UiStrings.ModuleClassroomSupportPurpose,
            AccessibleName = UiStrings.ModuleClassroomSupportPurpose,
        };

        _audience = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 180,
            AccessibleName = UiStrings.OutputAudience,
        };
        _audience.Items.AddRange(
        [
            new DisplayItem<RenderAudience>(RenderAudience.Teacher, UiStrings.AudienceTeacher),
            new DisplayItem<RenderAudience>(RenderAudience.Learner, UiStrings.AudienceLearner),
        ]);
        _audience.SelectedIndex = 0;

        _textScale = new NumericUpDown
        {
            Minimum = 100,
            Maximum = 200,
            Increment = 25,
            Value = 100,
            Width = 90,
            AccessibleName = UiStrings.TextScalePercent,
        };
        _targetLanguageFirst = new CheckBox
        {
            AutoSize = true,
            Text = UiStrings.TargetLanguageFirst,
            AccessibleName = UiStrings.TargetLanguageFirst,
        };

        _review = MakeButton(UiStrings.ReviewAndApprove, (_, _) => ReviewAndApprove());
        _print = MakeButton(UiStrings.PrintButton, (_, _) => PrintApproved());
        _printView = MakeButton(UiStrings.OpenPrintView, (_, _) => OpenPrintView());
        _export = MakeButton(UiStrings.ExportEllipsis, (_, _) => BeginInvoke(Export));
        _save = MakeButton(UiStrings.SaveToLibrary, (_, _) => SaveToLibrary());

        _status = new Label { Dock = DockStyle.Bottom, AutoSize = false, Height = 34 };
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
            AccessibleName = UiStrings.ModuleLaneConfirmation,
        };
        laneGroup.Controls.Add(_greenInput);

        var purposeGroup = new GroupBox
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Text = UiStrings.ModulePurposeClassification,
            AccessibleName = UiStrings.ModulePurposeClassification,
        };
        purposeGroup.Controls.Add(_classroomSupportPurpose);

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
            AccessibleName = UiStrings.ModuleOutputOptions,
        };
        outputGroup.Controls.Add(outputOptions);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true,
        };
        buttons.Controls.AddRange([_review, _print, _printView, _export, _save]);

        var right = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            AutoScroll = true,
        };
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 68));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 32));
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.Controls.Add(modeRow, 0, 0);
        right.Controls.Add(laneGroup, 0, 1);
        right.Controls.Add(purposeGroup, 0, 2);
        right.Controls.Add(_parameterPanel, 0, 3);
        right.Controls.Add(_notes, 0, 4);
        right.Controls.Add(outputGroup, 0, 5);
        right.Controls.Add(buttons, 0, 6);

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
        _classroomSupportPurpose.CheckedChanged += (_, _) => PurposeClassificationChanged();
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
        var button = new Button { Text = text, AutoSize = true, AccessibleName = text };
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
        if (_loadingFields || SelectedMode is not { } mode)
        {
            return;
        }

        _stateGeneration++;
        _loadingFields = true;
        ClearApproval();
        _approvedInputs.Clear();
        _parameterPanel.SuspendLayout();
        _parameterPanel.Controls.Clear();
        _valueReaders.Clear();
        _fieldContainers.Clear();

        foreach (var field in mode.Fields)
        {
            AddField(field);
        }

        // Catalog-owned synthetic starters are known Green. Every free-text or
        // table edit clears this state, because automated code may escalate a
        // lane but must never certify teacher-entered prose as Green.
        _greenInput.Checked = mode.DefaultsAreSynthetic;
        _greenInput.Enabled = mode.IsBuildAvailable;
        _classroomSupportPurpose.Checked = mode.DefaultsAreSynthetic;
        _classroomSupportPurpose.Enabled = mode.IsBuildAvailable
            && mode.DefaultKind != ModuleDefaultKind.RequiresApprovedArtifact;
        _parameterPanel.Enabled = mode.IsBuildAvailable;
        _parameterPanel.ResumeLayout();
        PopulateNotes(mode, []);
        _loadingFields = false;
        UpdateConditions();
        SetStatus(!mode.IsBuildAvailable
            ? UiStrings.Format(UiStrings.StatusModuleUnavailable, Display(mode.UnavailableReason!))
            : ReadinessStatus());
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
            Text = label,
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
            control.AccessibleDescription = UiStrings.ModuleSensitiveInput;
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
            AccessibleDescription = UiStrings.RecordTableHint,
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

    private TableLayoutPanel ApprovedArtifactInput(ModuleFieldDefinition field)
    {
        var panel = new TableLayoutPanel { ColumnCount = 2, Dock = DockStyle.Fill };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var selected = new TextBox
        {
            ReadOnly = true,
            Text = UiStrings.NoApprovedProject,
            AccessibleName = Display(field.Display),
            Dock = DockStyle.Fill,
        };
        if (_trustedCurrentClassroomSupport is not null)
        {
            _approvedInputs[field.Key] = _trustedCurrentClassroomSupport;
            selected.Text = UiStrings.CurrentApprovedArtifactSelected;
        }

        var choose = MakeButton(
            UiStrings.ChooseApprovedProject,
            (_, _) => BeginInvoke(() => ChooseApprovedArtifact(field, selected)));
        panel.Controls.Add(selected, 0, 0);
        panel.Controls.Add(choose, 1, 0);
        _valueReaders[field.Key] = () => _approvedInputs.GetValueOrDefault(field.Key);
        return panel;
    }

    private static Label Notice(ModuleFieldDefinition field)
        => new()
        {
            AutoSize = false,
            Text = Display(field.Display),
            AccessibleName = Display(field.Display),
        };

    private void ChooseApprovedArtifact(ModuleFieldDefinition field, TextBox selected)
    {
        var path = _libraryPicker();
        if (path is null)
        {
            return;
        }

        Storage.LoadedProject loaded;
        try
        {
            loaded = AppServices.OpenFromLibrary(path);
        }
        catch (Exception refusal) when (refusal is InvalidOperationException or IOException or InvalidDataException)
        {
            SetStatus(UiStrings.Format(UiStrings.StatusRefused, refusal.Message));
            return;
        }

        ReviewSession session;
        try
        {
            var laneConfirmation = _loadedProjectPreflight(loaded);
            if (laneConfirmation is null)
            {
                _approvedInputs.Remove(field.Key);
                selected.Text = UiStrings.NoApprovedProject;
                ClearApproval();
                SetStatus(UiStrings.StatusLoadedProjectPreflightNotConfirmed);
                return;
            }

            session = AppServices.SessionOverLoadedProject(loaded, laneConfirmation);
        }
        catch (InvalidOperationException refusal)
        {
            SetStatus(UiStrings.Format(UiStrings.StatusRefused, refusal.Message));
            return;
        }
        var generation = _stateGeneration;
        BeginPendingReview();
        ApprovedArtifact? approved;
        try
        {
            approved = _reviewRunner(session);
        }
        finally
        {
            EndPendingReview();
        }

        if (approved is null)
        {
            _approvedInputs.Remove(field.Key);
            selected.Text = UiStrings.NoApprovedProject;
            ClearApproval();
            SetStatus(UiStrings.StatusModuleProjectNotApproved);
            return;
        }

        if (generation != _stateGeneration || !AppServices.IsExactApproval(session, approved))
        {
            _approvedInputs.Remove(field.Key);
            selected.Text = UiStrings.NoApprovedProject;
            ClearApproval();
            SetStatus(UiStrings.StatusModuleProjectNotApproved);
            return;
        }

        _stateGeneration++;
        _approvedInputs[field.Key] = approved;
        selected.Text = UiStrings.Format(UiStrings.ApprovedProjectSelected, Path.GetFileNameWithoutExtension(path));
        _loadingFields = true;
        _greenInput.Checked = true;
        _loadingFields = false;
        ClearApproval();
        SetStatus(UiStrings.StatusModuleProjectApproved);
        UpdateGatedButtons();
    }

    private LoadedProjectGreenConfirmation? RunLoadedProjectPreflight(
        Storage.LoadedProject loaded)
    {
        using var preflight = new LoadedProjectPreflightForm(loaded);
        return preflight.ShowDialog(this) == DialogResult.OK
            ? preflight.Confirmation
            : null;
    }

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

        var purposeIsInherited = mode.DefaultKind == ModuleDefaultKind.RequiresApprovedArtifact;
        if (!purposeIsInherited && !_classroomSupportPurpose.Checked)
        {
            SetStatus(UiStrings.StatusModulePurposeRequired);
            return;
        }

        ClearApproval();
        ModuleBuildOutcome outcome;
        try
        {
            outcome = mode.Build(new ModuleInputValues(
                _valueReaders.ToDictionary(pair => pair.Key, pair => pair.Value(), StringComparer.Ordinal)));
            if (!purposeIsInherited)
            {
                outcome = outcome.ClassifyAsClassroomSupport();
            }
        }
        catch (Exception refusal) when (refusal is ArgumentException or InvalidOperationException)
        {
            SetStatus(UiStrings.Format(UiStrings.StatusRefused, refusal.Message));
            _parameterPanel.SelectNextControl(null, true, true, true, false);
            return;
        }

        PopulateNotes(mode, outcome.TransformationReport);
        var blockingIssues = outcome.Issues
            .Where(issue => issue.Severity == ValidationSeverity.Blocking)
            .ToList();
        if (blockingIssues.Count > 0)
        {
            var summary = UiStrings.Format(UiStrings.StatusModuleBuiltWithIssues, blockingIssues.Count);
            foreach (var issue in blockingIssues)
            {
                _notes.Items.Add(UiStrings.Format(
                    UiStrings.ModuleIssueDetail,
                    issue.Code,
                    issue.Message));
            }

            SetStatus(summary);
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
            if (approved.Revision.Purpose == ArtifactPurpose.ClassroomSupport)
            {
                // Only an exact artifact produced and approved in this running
                // typed studio carries purpose across the Access boundary.
                // A mutable package can assert the same enum but cannot mint
                // this in-memory provenance.
                _trustedCurrentClassroomSupport = approved;
            }

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
        _classroomSupportPurpose.Enabled = enabled
            && SelectedMode is { IsBuildAvailable: true, DefaultKind: not ModuleDefaultKind.RequiresApprovedArtifact };
        _audience.Enabled = enabled;
        _textScale.Enabled = enabled;
        _targetLanguageFirst.Enabled = enabled;
    }

    private static ApprovedArtifact? RunModalReview(ReviewSession session)
    {
        using var review = new ReviewForm(session);
        return review.ShowDialog() == DialogResult.OK ? review.Result : null;
    }

    private void OpenPrintView()
    {
        if (ApprovedResult is null || _context is null)
        {
            return;
        }

        AppServices.OpenPrintView(
            ApprovedResult,
            _context.Mode.Key,
            SelectedAudience(),
            (double)_textScale.Value,
            _targetLanguageFirst.Checked);
        SetStatus(UiStrings.StatusPrintView);
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
            SetStatus(UiStrings.Format(UiStrings.StatusRefused, refusal.Message));
        }
    }

    private void Export()
    {
        if (ApprovedResult is null || _context is null)
        {
            return;
        }

        var choice = _exportPicker is null
            ? PickExportDialog(_context.Mode)
            : _exportPicker();
        if (choice is null)
        {
            return;
        }

        if (!SupportedExportTargets(_context.Mode).Contains(choice.Target))
        {
            SetStatus(UiStrings.Format(UiStrings.StatusRefused, choice.Target.ToString()));
            return;
        }

        try
        {
            var request = new RenderRequest(
                choice.Target,
                SelectedAudience(),
                (double)_textScale.Value,
                _targetLanguageFirst.Checked);
            File.WriteAllBytes(choice.Path, AppServices.Render(ApprovedResult, request));
            SetStatus(UiStrings.Format(UiStrings.StatusExported, Path.GetFileName(choice.Path)));
        }
        catch (Exception refusal) when (refusal is InvalidOperationException or IOException or NotSupportedException)
        {
            SetStatus(UiStrings.Format(UiStrings.StatusRefused, refusal.Message));
        }
    }

    private ExportChoice? PickExportDialog(ModuleModeDefinition mode)
    {
        var targets = SupportedExportTargets(mode);
        if (targets.Count == 0)
        {
            return null;
        }

        using var dialog = new SaveFileDialog
        {
            FileName = mode.Key,
            Filter = string.Join('|', targets.Select(target =>
                $"{ExportLabel(target)}|*.html")),
        };
        return dialog.ShowDialog(this) == DialogResult.OK
            ? new ExportChoice(dialog.FileName, targets[dialog.FilterIndex - 1])
            : null;
    }

    private static string ExportLabel(RenderTarget target)
        => target == RenderTarget.AccessibleHtml
            ? UiStrings.ExportFilterModuleAccessible
            : UiStrings.ExportFilterModulePrint;

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
            _context.Mode.Key,
            _context.Door.Id,
            _context.Mode.Recipe.Id,
            _context.Mode.Recipe.Version,
            AppServices.SymbolCatalog(),
            _validationEnvelope,
            _renderProfile);
        SetStatus(UiStrings.Format(UiStrings.StatusSaved, name));
    }

    private void PopulateNotes(ModuleModeDefinition mode, IReadOnlyList<string> transformationReport)
    {
        _notes.Items.Clear();
        _notes.Items.Add(UiStrings.Format(
            UiStrings.ModuleLaneAndRecipe,
            mode.Lane,
            mode.Recipe.Id,
            mode.Recipe.Version));
        if (mode.DefaultsAreSynthetic)
        {
            _notes.Items.Add(UiStrings.ModuleSyntheticStarter);
        }

        if (mode.DefaultKind == ModuleDefaultKind.RequiresApprovedArtifact)
        {
            _notes.Items.Add(UiStrings.ModuleApprovedInputRequired);
        }

        if (mode.UnavailableReason is not null)
        {
            _notes.Items.Add(Display(mode.UnavailableReason));
        }

        foreach (var prohibited in mode.Recipe.ProhibitedPurposes)
        {
            _notes.Items.Add(UiStrings.Format(UiStrings.ModuleProhibitedPurpose, prohibited));
        }

        foreach (var warning in mode.Recipe.Warnings.Concat(transformationReport).Distinct(StringComparer.Ordinal))
        {
            _notes.Items.Add(warning);
        }
    }

    private RenderAudience SelectedAudience()
        => (_audience.SelectedItem as DisplayItem<RenderAudience>)?.Value ?? RenderAudience.Teacher;

    private void ContentInputChanged()
    {
        if (_loadingFields)
        {
            return;
        }

        _stateGeneration++;
        _loadingFields = true;
        _greenInput.Checked = false;
        _classroomSupportPurpose.Checked = false;
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
            SetStatus(UiStrings.Format(UiStrings.StatusModuleUnavailable, Display(reason)));
            UpdateGatedButtons();
            return;
        }

        ClearApproval();
        SetStatus(ReadinessStatus());
        UpdateGatedButtons();
    }

    private void PurposeClassificationChanged()
    {
        if (_loadingFields)
        {
            return;
        }

        _stateGeneration++;
        if (SelectedMode is { IsBuildAvailable: false, UnavailableReason: { } reason })
        {
            _loadingFields = true;
            _classroomSupportPurpose.Checked = false;
            _loadingFields = false;
            ClearApproval();
            SetStatus(UiStrings.Format(UiStrings.StatusModuleUnavailable, Display(reason)));
            UpdateGatedButtons();
            return;
        }

        ClearApproval();
        SetStatus(!_greenInput.Checked
            ? UiStrings.StatusModuleGreenRequired
            : _classroomSupportPurpose.Checked
                ? UiStrings.StatusModuleReady
                : UiStrings.StatusModulePurposeRequired);
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

    private bool IsInputReady()
        => _greenInput.Checked
            && (SelectedMode?.DefaultKind == ModuleDefaultKind.RequiresApprovedArtifact
                || _classroomSupportPurpose.Checked);

    private string ReadinessStatus()
        => !_greenInput.Checked
            ? UiStrings.StatusModuleGreenRequired
            : SelectedMode?.DefaultKind != ModuleDefaultKind.RequiresApprovedArtifact
                && !_classroomSupportPurpose.Checked
                    ? UiStrings.StatusModulePurposeRequired
                    : UiStrings.StatusModuleReady;

    private void UpdateGatedButtons()
    {
        var approved = !_reviewPending && ApprovedResult is not null && _context is not null;
        var supported = _context is null ? [] : SupportedExportTargets(_context.Mode);
        _review.Enabled = !_reviewPending
            && SelectedMode is { IsBuildAvailable: true }
            && IsInputReady();
        _print.Enabled = approved && supported.Contains(RenderTarget.PrintHtml);
        _printView.Enabled = approved && supported.Contains(RenderTarget.PrintHtml);
        _export.Enabled = approved && supported.Count > 0;
        _save.Enabled = approved;
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

    private void SetStatus(string text)
    {
        _status.Text = text;
        _status.AccessibleName = text;
    }
}
