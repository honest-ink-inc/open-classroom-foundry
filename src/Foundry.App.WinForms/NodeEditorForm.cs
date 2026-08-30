// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using Foundry.Domain;

namespace Foundry.App.WinForms;

/// <summary>
/// Typed, standard-control editor for one exact semantic node. The form never
/// receives a sink or an approval capability. It returns only a replacement
/// <see cref="DocumentNode"/>; <see cref="Application.ReviewSession"/> owns the
/// revision change and every later approval decision.
/// </summary>
public sealed class NodeEditorForm : Form
{
    private readonly DocumentNode _original;
    private readonly TableLayoutPanel _fields;
    private readonly Label _status;
    private readonly Button _apply;
    private readonly Button _discard;
    private readonly Dictionary<string, TextBox> _text = new(StringComparer.Ordinal);
    private readonly Dictionary<string, NumericUpDown> _numbers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CheckBox> _checks = new(StringComparer.Ordinal);
    private DataGridView? _sequence;
    private DataGridView? _table;
    private NumericUpDown? _tableColumns;
    private CheckBox? _tableHasHeader;
    private List<VectorPrimitive>? _primitives;
    private ListBox? _primitiveList;
    private ComboBox? _primitiveType;
    private TableLayoutPanel? _primitiveFields;
    private readonly Dictionary<string, TextBox> _primitiveText = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CheckBox> _primitiveChecks = new(StringComparer.Ordinal);
    private ComboBox? _primitiveAnchor;
    private Button? _applyPrimitive;
    private Button? _discardPrimitive;
    private Button? _addPrimitive;
    private Button? _replacePrimitiveType;
    private Button? _removePrimitive;
    private Button? _movePrimitiveUp;
    private Button? _movePrimitiveDown;
    private readonly bool _loading = true;
    private bool _loadingPrimitive;
    private bool _dirty;
    private bool _primitiveDirty;
    private bool _explicitClose;
    private int _loadedPrimitiveIndex = -1;

    public NodeEditorForm(DocumentNode node)
    {
        _original = node ?? throw new ArgumentNullException(nameof(node));

        Text = UiStrings.WithoutMnemonic(UiStrings.NodeEditorWindowTitle);
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        MinimumSize = new Size(640, 420);
        Size = new Size(860, 620);

        var original = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            WordWrap = true,
            ScrollBars = ScrollBars.Both,
            AccessibleName = UiStrings.WithoutMnemonic(UiStrings.ExactSelectedElementBeforeEdit),
            Text = ExactArtifactDocumentText.Describe(new ArtifactDocument([node])),
        };

        var originalGroup = new GroupBox
        {
            Dock = DockStyle.Fill,
            Text = UiStrings.ExactSelectedElementBeforeEdit,
            AccessibleName = UiStrings.WithoutMnemonic(UiStrings.ExactSelectedElementBeforeEdit),
        };
        originalGroup.Controls.Add(original);

        _fields = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            Dock = DockStyle.Top,
            Padding = new Padding(8),
            AccessibleName = UiStrings.WithoutMnemonic(UiStrings.TypedElementFields),
        };
        _fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        _fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66));

        var editorScroller = new Panel
        {
            AutoScroll = true,
            Dock = DockStyle.Fill,
            AccessibleName = UiStrings.WithoutMnemonic(UiStrings.TypedElementFields),
        };
        editorScroller.Controls.Add(_fields);

        _status = new Label
        {
            AutoEllipsis = false,
            AutoSize = false,
            Dock = DockStyle.Fill,
            AccessibleRole = AccessibleRole.StatusBar,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false,
        };

        _apply = MakeButton(UiStrings.ApplyReplacement, (_, _) => ApplyAndClose());
        _discard = MakeButton(UiStrings.DiscardReplacement, (_, _) => DiscardAndClose());
        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
        };
        buttons.Controls.AddRange([_apply, _discard]);

        var layout = new TableLayoutPanel
        {
            ColumnCount = 1,
            RowCount = 4,
            Dock = DockStyle.Fill,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(originalGroup, 0, 0);
        layout.Controls.Add(editorScroller, 0, 1);
        layout.Controls.Add(_status, 0, 2);
        layout.Controls.Add(buttons, 0, 3);
        Controls.Add(layout);

        BuildFields(node);
        _loading = false;
        UpdateActions();
        UiLocale.ApplyChrome(this);
    }

    /// <summary>The typed replacement after explicit Apply; otherwise null.</summary>
    public DocumentNode? Result { get; private set; }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        var working = Screen.FromControl(this).WorkingArea;
        Size = new Size(
            Math.Min(Width, Math.Max(MinimumSize.Width, working.Width - 32)),
            Math.Min(Height, Math.Max(MinimumSize.Height, working.Height - 32)));
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if ((_dirty || _primitiveDirty) && !_explicitClose)
        {
            e.Cancel = true;
            SetStatus(UiStrings.PendingReplacementMustBeAppliedOrDiscarded);
            (_primitiveDirty ? _applyPrimitive : _apply)?.Focus();
            return;
        }

        if (!_explicitClose)
        {
            _explicitClose = true;
            DialogResult = DialogResult.Cancel;
        }

        base.OnFormClosing(e);
    }

    private void BuildFields(DocumentNode node)
    {
        switch (node)
        {
            case Heading heading:
                AddNumber("level", UiStrings.EditorHeadingLevel, heading.Level, -1000, 1000);
                AddText("text", UiStrings.EditorText, heading.Text, multiline: true);
                break;

            case Paragraph paragraph:
                AddText("text", UiStrings.EditorText, paragraph.Text, multiline: true);
                break;

            case OrderedSteps steps:
                AddSequence(steps.Steps);
                break;

            case UnorderedList list:
                AddSequence(list.Items);
                break;

            case ChoiceSet choices:
                AddSequence(choices.Options);
                break;

            case TableNode table:
                AddTable(table);
                break;

            case Card card:
                AddText("title", UiStrings.EditorCardTitle, card.Title);
                AddText("body", UiStrings.EditorCardBody, card.Body, multiline: true);
                break;

            case ImageReference image:
                AddText("asset", UiStrings.EditorAssetIdentity, image.Asset.Value);
                AddText("alt", UiStrings.EditorAlternativeText, image.AltText, multiline: true);
                break;

            case BilingualPair pair:
                AddText("sourceText", UiStrings.EditorSourceText, pair.SourceText, multiline: true);
                AddText("targetText", UiStrings.EditorTargetText, pair.TargetText, multiline: true);
                AddText("sourceLocale", UiStrings.EditorSourceLocale, pair.SourceLocale);
                AddText("targetLocale", UiStrings.EditorTargetLocale, pair.TargetLocale);
                break;

            case EvidenceLink evidence:
                AddText("claim", UiStrings.EditorClaim, evidence.Claim, multiline: true);
                AddText("sourcePointer", UiStrings.EditorSourcePointer, evidence.SourcePointer, multiline: true);
                break;

            case Citation citation:
                AddText("text", UiStrings.EditorText, citation.Text, multiline: true);
                break;

            case TeacherOnlyNotice notice:
                AddText("text", UiStrings.EditorText, notice.Text, multiline: true);
                break;

            case StepRow step:
                AddStepRow(step);
                break;

            case PageBreak:
                AddFullWidth(new Label
                {
                    AutoSize = true,
                    MaximumSize = new Size(720, 0),
                    Text = UiStrings.NodeEditorNoEditableFields,
                    AccessibleName = UiStrings.WithoutMnemonic(UiStrings.NodeEditorNoEditableFields),
                });
                break;

            case VectorGraphic graphic:
                AddVector(graphic);
                break;

            default:
                AddFullWidth(new Label
                {
                    AutoSize = true,
                    Text = UiStrings.NodeEditorNoEditableFields,
                    AccessibleName = UiStrings.WithoutMnemonic(UiStrings.NodeEditorNoEditableFields),
                });
                break;
        }
    }

    private void AddStepRow(StepRow step)
    {
        AddText("text", UiStrings.EditorText, step.Text, multiline: true);
        var includeTranslation = AddCheck(
            "includeTranslation",
            UiStrings.EditorIncludeTranslation,
            step.TargetText is not null || step.SourceLocale is not null || step.TargetLocale is not null);
        var target = AddText("targetText", UiStrings.EditorTargetText, step.TargetText ?? string.Empty, multiline: true);
        var sourceLocale = AddText("sourceLocale", UiStrings.EditorSourceLocale, step.SourceLocale ?? string.Empty);
        var targetLocale = AddText("targetLocale", UiStrings.EditorTargetLocale, step.TargetLocale ?? string.Empty);

        var includeSymbol = AddCheck("includeSymbol", UiStrings.EditorIncludeSymbol, step.Symbol is not null);
        var asset = AddText("asset", UiStrings.EditorAssetIdentity, step.Symbol?.Asset.Value ?? string.Empty);
        var alt = AddText("alt", UiStrings.EditorAlternativeText, step.Symbol?.AltText ?? string.Empty, multiline: true);

        void UpdateOptionalFields()
        {
            target.Enabled = includeTranslation.Checked;
            sourceLocale.Enabled = includeTranslation.Checked;
            targetLocale.Enabled = includeTranslation.Checked;
            asset.Enabled = includeSymbol.Checked;
            alt.Enabled = includeSymbol.Checked;
        }

        includeTranslation.CheckedChanged += (_, _) => UpdateOptionalFields();
        includeSymbol.CheckedChanged += (_, _) => UpdateOptionalFields();
        UpdateOptionalFields();
    }

    private void AddSequence(IReadOnlyList<string> values)
    {
        _sequence = NewGrid(UiStrings.EditorSequenceItems);
        _sequence.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = UiStrings.WithoutMnemonic(UiStrings.EditorItemText),
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
        });
        foreach (var value in values)
        {
            _sequence.Rows.Add(value);
        }

        _sequence.CellValueChanged += (_, _) => MarkDirty();
        _sequence.CellEndEdit += (_, _) => MarkDirty();

        var add = MakeButton(UiStrings.AddItem, (_, _) =>
        {
            var index = _sequence.Rows.Add(string.Empty);
            _sequence.CurrentCell = _sequence.Rows[index].Cells[0];
            MarkDirty();
        });
        var remove = MakeButton(UiStrings.RemoveItem, (_, _) => RemoveGridRow(_sequence));
        var up = MakeButton(UiStrings.MoveItemUp, (_, _) => MoveGridRow(_sequence, -1, firstMovableRow: 0));
        var down = MakeButton(UiStrings.MoveItemDown, (_, _) => MoveGridRow(_sequence, +1, firstMovableRow: 0));
        AddGridWithButtons(_sequence, [add, remove, up, down]);
    }

    private void AddTable(TableNode table)
    {
        _tableHasHeader = AddCheck("hasHeader", UiStrings.EditorTableHasHeader, table.HeaderRow is not null);
        var columnCount = Math.Max(
            1,
            Math.Max(
                table.HeaderRow?.Count ?? 0,
                table.Rows.Count == 0 ? 0 : table.Rows.Max(row => row.Count)));
        _tableColumns = AddNumber(
            "tableColumns",
            UiStrings.EditorTableColumnCount,
            columnCount,
            1,
            50);
        _table = NewGrid(UiStrings.EditorTableCells);
        RebuildTableColumns(columnCount, []);

        if (table.HeaderRow is not null)
        {
            _table.Rows.Add(Pad(table.HeaderRow, columnCount));
        }

        foreach (var row in table.Rows)
        {
            _table.Rows.Add(Pad(row, columnCount));
        }

        UpdateTableRowHeaders();
        _table.CellValueChanged += (_, _) => MarkDirty();
        _table.CellEndEdit += (_, _) => MarkDirty();
        _table.RowsAdded += (_, _) => UpdateTableRowHeaders();
        _table.RowsRemoved += (_, _) => UpdateTableRowHeaders();

        _tableColumns.ValueChanged += (_, _) =>
        {
            if (_loading)
            {
                return;
            }

            var rows = ReadGridRows(_table);
            RebuildTableColumns(decimal.ToInt32(_tableColumns.Value), rows);
            MarkDirty();
        };
        _tableHasHeader.CheckedChanged += (_, _) =>
        {
            if (_loading)
            {
                return;
            }

            if (_tableHasHeader.Checked)
            {
                _table.Rows.Insert(0, new object[_table.ColumnCount]);
            }
            else if (_table.Rows.Count > 0)
            {
                _table.Rows.RemoveAt(0);
            }

            UpdateTableRowHeaders();
            MarkDirty();
        };

        var add = MakeButton(UiStrings.AddTableRow, (_, _) =>
        {
            var index = _table.Rows.Add(new object[_table.ColumnCount]);
            _table.CurrentCell = _table.Rows[index].Cells[0];
            UpdateTableRowHeaders();
            MarkDirty();
        });
        var remove = MakeButton(UiStrings.RemoveTableRow, (_, _) =>
        {
            var first = _tableHasHeader.Checked ? 1 : 0;
            RemoveGridRow(_table, first);
        });
        var up = MakeButton(UiStrings.MoveTableRowUp, (_, _) =>
            MoveGridRow(_table, -1, _tableHasHeader.Checked ? 1 : 0));
        var down = MakeButton(UiStrings.MoveTableRowDown, (_, _) =>
            MoveGridRow(_table, +1, _tableHasHeader.Checked ? 1 : 0));
        AddGridWithButtons(_table, [add, remove, up, down]);
    }

    private void AddVector(VectorGraphic graphic)
    {
        AddText("description", UiStrings.EditorVectorDescription, graphic.Description, multiline: true);
        AddText("width", UiStrings.EditorVectorWidthMm, Number(graphic.WidthMm));
        AddText("height", UiStrings.EditorVectorHeightMm, Number(graphic.HeightMm));

        _primitives = [.. graphic.Primitives];
        _primitiveList = new ListBox
        {
            Dock = DockStyle.Fill,
            AccessibleName = UiStrings.WithoutMnemonic(UiStrings.EditorVectorPrimitives),
        };
        _primitiveList.SelectedIndexChanged += (_, _) => PrimitiveSelectionChanged();

        _primitiveType = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            AccessibleName = UiStrings.WithoutMnemonic(UiStrings.EditorVectorPrimitiveType),
            Width = 180,
        };
        _primitiveType.Items.AddRange(
        [
            new PrimitiveChoice(typeof(LineSeg), UiStrings.WithoutMnemonic(UiStrings.PrimitiveLine)),
            new PrimitiveChoice(typeof(CircleShape), UiStrings.WithoutMnemonic(UiStrings.PrimitiveCircle)),
            new PrimitiveChoice(typeof(RectShape), UiStrings.WithoutMnemonic(UiStrings.PrimitiveRectangle)),
            new PrimitiveChoice(typeof(TextLabel), UiStrings.WithoutMnemonic(UiStrings.PrimitiveTextLabel)),
        ]);
        _primitiveType.SelectedIndex = 0;

        _addPrimitive = MakeButton(UiStrings.AddPrimitive, (_, _) => AddPrimitive());
        _replacePrimitiveType = MakeButton(UiStrings.ReplacePrimitiveType, (_, _) => ReplacePrimitiveType());
        _removePrimitive = MakeButton(UiStrings.RemovePrimitive, (_, _) => RemovePrimitive());
        _movePrimitiveUp = MakeButton(UiStrings.MovePrimitiveUp, (_, _) => MovePrimitive(-1));
        _movePrimitiveDown = MakeButton(UiStrings.MovePrimitiveDown, (_, _) => MovePrimitive(+1));
        _applyPrimitive = MakeButton(UiStrings.ApplyPrimitiveEdit, (_, _) => ApplyPrimitiveEdit());
        _discardPrimitive = MakeButton(UiStrings.DiscardPrimitiveEdit, (_, _) => DiscardPrimitiveEdit());

        var primitiveActions = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
        };
        primitiveActions.Controls.AddRange(
        [
            _primitiveType,
            _addPrimitive,
            _replacePrimitiveType,
            _removePrimitive,
            _movePrimitiveUp,
            _movePrimitiveDown,
        ]);

        _primitiveFields = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            Dock = DockStyle.Top,
        };
        _primitiveFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        _primitiveFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));

        var primitiveEditActions = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
        };
        primitiveEditActions.Controls.AddRange([_applyPrimitive, _discardPrimitive]);

        var fieldScroller = new Panel { AutoScroll = true, Dock = DockStyle.Fill };
        fieldScroller.Controls.Add(_primitiveFields);
        fieldScroller.Controls.Add(primitiveEditActions);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            AccessibleName = UiStrings.WithoutMnemonic(UiStrings.EditorVectorPrimitives),
            SplitterDistance = 270,
        };
        split.Panel1.Controls.Add(_primitiveList);
        split.Panel1.Controls.Add(primitiveActions);
        split.Panel2.Controls.Add(fieldScroller);

        var group = new GroupBox
        {
            Dock = DockStyle.Fill,
            Height = 360,
            Text = UiStrings.EditorVectorPrimitives,
            AccessibleName = UiStrings.WithoutMnemonic(UiStrings.EditorVectorPrimitives),
        };
        group.Controls.Add(split);
        AddFullWidth(group, 360);

        RefreshPrimitiveList(0);
    }

    private TextBox AddText(string key, string label, string value, bool multiline = false)
    {
        var control = new TextBox
        {
            AccessibleName = UiStrings.WithoutMnemonic(label),
            Dock = DockStyle.Fill,
            Multiline = multiline,
            ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None,
            WordWrap = true,
            Text = value,
        };
        if (multiline)
        {
            control.MinimumSize = new Size(100, 72);
        }

        control.TextChanged += (_, _) => MarkDirty();
        _text.Add(key, control);
        AddRow(label, control, multiline ? 82 : 32);
        return control;
    }

    private NumericUpDown AddNumber(string key, string label, decimal value, decimal minimum, decimal maximum)
    {
        var control = new NumericUpDown
        {
            AccessibleName = UiStrings.WithoutMnemonic(label),
            Dock = DockStyle.Fill,
            Minimum = minimum,
            Maximum = maximum,
            Value = Math.Clamp(value, minimum, maximum),
        };
        control.ValueChanged += (_, _) => MarkDirty();
        _numbers.Add(key, control);
        AddRow(label, control, 32);
        return control;
    }

    private CheckBox AddCheck(string key, string label, bool value)
    {
        var control = new CheckBox
        {
            AutoSize = true,
            AccessibleName = UiStrings.WithoutMnemonic(label),
            Text = label,
            Checked = value,
        };
        control.CheckedChanged += (_, _) => MarkDirty();
        _checks.Add(key, control);
        AddFullWidth(control, 32);
        return control;
    }

    private void AddRow(string label, Control control, int height)
    {
        var row = _fields.RowCount++;
        _fields.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        _fields.Controls.Add(new Label
        {
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Text = label,
        }, 0, row);
        _fields.Controls.Add(control, 1, row);
    }

    private void AddFullWidth(Control control, int height = 44)
    {
        var row = _fields.RowCount++;
        _fields.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        _fields.Controls.Add(control, 0, row);
        _fields.SetColumnSpan(control, 2);
    }

    private void AddGridWithButtons(DataGridView grid, IReadOnlyList<Button> buttons)
    {
        var host = new Panel { Dock = DockStyle.Fill };
        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
        };
        actions.Controls.AddRange([.. buttons]);
        host.Controls.Add(grid);
        host.Controls.Add(actions);
        AddFullWidth(host, 280);
    }

    private static DataGridView NewGrid(string accessibleName)
        => new()
        {
            AccessibleName = UiStrings.WithoutMnemonic(accessibleName),
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToOrderColumns = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            Dock = DockStyle.Fill,
            MultiSelect = false,
            RowHeadersVisible = true,
            RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.AutoSizeToAllHeaders,
            SelectionMode = DataGridViewSelectionMode.CellSelect,
        };

    private void RebuildTableColumns(int columnCount, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        if (_table is null)
        {
            return;
        }

        _table.Columns.Clear();
        for (var column = 0; column < columnCount; column++)
        {
            _table.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = UiStrings.FormatWithoutMnemonic(UiStrings.EditorTableColumn, column + 1),
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            });
        }

        foreach (var row in rows)
        {
            _table.Rows.Add(Pad(row, columnCount));
        }

        UpdateTableRowHeaders();
    }

    private void UpdateTableRowHeaders()
    {
        if (_table is null || _tableHasHeader is null)
        {
            return;
        }

        for (var row = 0; row < _table.Rows.Count; row++)
        {
            _table.Rows[row].HeaderCell.Value = _tableHasHeader.Checked && row == 0
                ? UiStrings.WithoutMnemonic(UiStrings.EditorTableHeaderRow)
                : UiStrings.FormatWithoutMnemonic(
                    UiStrings.EditorTableDataRow,
                    row + (_tableHasHeader.Checked ? 0 : 1));
        }
    }

    private void RemoveGridRow(DataGridView grid, int firstMovableRow = 0)
    {
        var row = grid.CurrentCell?.RowIndex ?? -1;
        if (row < firstMovableRow || row >= grid.Rows.Count)
        {
            return;
        }

        grid.Rows.RemoveAt(row);
        UpdateTableRowHeaders();
        MarkDirty();
    }

    private void MoveGridRow(DataGridView grid, int delta, int firstMovableRow)
    {
        grid.EndEdit();
        var row = grid.CurrentCell?.RowIndex ?? -1;
        var target = row + delta;
        if (row < firstMovableRow || target < firstMovableRow || target >= grid.Rows.Count)
        {
            return;
        }

        var values = grid.Rows[row].Cells.Cast<DataGridViewCell>()
            .Select(cell => cell.Value ?? string.Empty)
            .ToArray();
        grid.Rows.RemoveAt(row);
        grid.Rows.Insert(target, values);
        grid.CurrentCell = grid.Rows[target].Cells[0];
        UpdateTableRowHeaders();
        MarkDirty();
    }

    private void PrimitiveSelectionChanged()
    {
        if (_primitiveList is null || _primitives is null)
        {
            return;
        }

        var selected = _primitiveList.SelectedIndex;
        if (_primitiveDirty && selected != _loadedPrimitiveIndex)
        {
            _loadingPrimitive = true;
            _primitiveList.SelectedIndex = _loadedPrimitiveIndex;
            _loadingPrimitive = false;
            return;
        }

        if (!_loadingPrimitive)
        {
            LoadPrimitive(selected);
        }
    }

    private void LoadPrimitive(int index)
    {
        if (_primitiveFields is null || _primitives is null)
        {
            return;
        }

        _loadingPrimitive = true;
        _loadedPrimitiveIndex = index;
        _primitiveFields.Controls.Clear();
        _primitiveFields.RowStyles.Clear();
        _primitiveFields.RowCount = 0;
        _primitiveText.Clear();
        _primitiveChecks.Clear();
        _primitiveAnchor = null;

        if (index >= 0 && index < _primitives.Count)
        {
            switch (_primitives[index])
            {
                case LineSeg line:
                    AddPrimitiveText("x1", UiStrings.EditorX1Mm, Number(line.X1));
                    AddPrimitiveText("y1", UiStrings.EditorY1Mm, Number(line.Y1));
                    AddPrimitiveText("x2", UiStrings.EditorX2Mm, Number(line.X2));
                    AddPrimitiveText("y2", UiStrings.EditorY2Mm, Number(line.Y2));
                    AddPrimitiveText("stroke", UiStrings.EditorStrokeWidthMm, Number(line.StrokeWidthMm));
                    AddPrimitiveCheck("flag", UiStrings.EditorDashed, line.Dashed);
                    break;

                case CircleShape circle:
                    AddPrimitiveText("x", UiStrings.EditorCenterXMm, Number(circle.CenterX));
                    AddPrimitiveText("y", UiStrings.EditorCenterYMm, Number(circle.CenterY));
                    AddPrimitiveText("radius", UiStrings.EditorRadiusMm, Number(circle.RadiusMm));
                    AddPrimitiveText("stroke", UiStrings.EditorStrokeWidthMm, Number(circle.StrokeWidthMm));
                    AddPrimitiveCheck("flag", UiStrings.EditorFilled, circle.Filled);
                    break;

                case RectShape rectangle:
                    AddPrimitiveText("x", UiStrings.EditorXPositionMm, Number(rectangle.X));
                    AddPrimitiveText("y", UiStrings.EditorYPositionMm, Number(rectangle.Y));
                    AddPrimitiveText("width", UiStrings.EditorWidthMm, Number(rectangle.WidthMm));
                    AddPrimitiveText("height", UiStrings.EditorHeightMm, Number(rectangle.HeightMm));
                    AddPrimitiveText("stroke", UiStrings.EditorStrokeWidthMm, Number(rectangle.StrokeWidthMm));
                    AddPrimitiveCheck("flag", UiStrings.EditorFilled, rectangle.Filled);
                    break;

                case TextLabel label:
                    AddPrimitiveText("x", UiStrings.EditorXPositionMm, Number(label.X));
                    AddPrimitiveText("y", UiStrings.EditorYPositionMm, Number(label.Y));
                    AddPrimitiveText("text", UiStrings.EditorLabelText, label.Text);
                    AddPrimitiveText("font", UiStrings.EditorFontSizeMm, Number(label.FontSizeMm));
                    AddPrimitiveAnchor(label.Anchor);
                    break;
            }
        }

        _primitiveDirty = false;
        _loadingPrimitive = false;
        UpdateActions();
    }

    private void AddPrimitiveText(string key, string label, string value)
    {
        var control = new TextBox
        {
            AccessibleName = UiStrings.WithoutMnemonic(label),
            Dock = DockStyle.Fill,
            Text = value,
        };
        control.TextChanged += (_, _) => MarkPrimitiveDirty();
        _primitiveText.Add(key, control);
        AddPrimitiveRow(label, control);
    }

    private void AddPrimitiveCheck(string key, string label, bool value)
    {
        var control = new CheckBox
        {
            AutoSize = true,
            AccessibleName = UiStrings.WithoutMnemonic(label),
            Text = label,
            Checked = value,
        };
        control.CheckedChanged += (_, _) => MarkPrimitiveDirty();
        _primitiveChecks.Add(key, control);
        AddPrimitiveFullWidth(control);
    }

    private void AddPrimitiveAnchor(TextAnchor anchor)
    {
        _primitiveAnchor = new ComboBox
        {
            AccessibleName = UiStrings.WithoutMnemonic(UiStrings.EditorTextAnchor),
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        _primitiveAnchor.Items.AddRange(
        [
            new AnchorChoice(TextAnchor.Start, UiStrings.WithoutMnemonic(UiStrings.TextAnchorStart)),
            new AnchorChoice(TextAnchor.Middle, UiStrings.WithoutMnemonic(UiStrings.TextAnchorMiddle)),
            new AnchorChoice(TextAnchor.End, UiStrings.WithoutMnemonic(UiStrings.TextAnchorEnd)),
        ]);
        _primitiveAnchor.SelectedIndex = anchor switch
        {
            TextAnchor.Start => 0,
            TextAnchor.Middle => 1,
            TextAnchor.End => 2,
            _ => 1,
        };
        _primitiveAnchor.SelectedIndexChanged += (_, _) => MarkPrimitiveDirty();
        AddPrimitiveRow(UiStrings.EditorTextAnchor, _primitiveAnchor);
    }

    private void AddPrimitiveRow(string label, Control control)
    {
        if (_primitiveFields is null)
        {
            return;
        }

        var row = _primitiveFields.RowCount++;
        // Primitive labels are substantially longer than their numeric fields
        // (and grow again in translated chrome). Let the row honor the label's
        // real preferred height instead of cutting a wrapped caption at 32 px.
        _primitiveFields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _primitiveFields.Controls.Add(new Label
        {
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Text = label,
        }, 0, row);
        _primitiveFields.Controls.Add(control, 1, row);
    }

    private void AddPrimitiveFullWidth(Control control)
    {
        if (_primitiveFields is null)
        {
            return;
        }

        var row = _primitiveFields.RowCount++;
        _primitiveFields.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        _primitiveFields.Controls.Add(control, 0, row);
        _primitiveFields.SetColumnSpan(control, 2);
    }

    private void AddPrimitive()
    {
        if (_primitives is null || _primitiveType?.SelectedItem is not PrimitiveChoice choice)
        {
            return;
        }

        _primitives.Add(DefaultPrimitive(choice.Type));
        _dirty = true;
        RefreshPrimitiveList(_primitives.Count - 1);
    }

    private void ReplacePrimitiveType()
    {
        if (_primitives is null
            || _primitiveList is null
            || _primitiveType?.SelectedItem is not PrimitiveChoice choice
            || _primitiveList.SelectedIndex < 0)
        {
            return;
        }

        var index = _primitiveList.SelectedIndex;
        _primitives[index] = DefaultPrimitive(choice.Type);
        _dirty = true;
        RefreshPrimitiveList(index);
    }

    private void RemovePrimitive()
    {
        if (_primitives is null || _primitiveList is null || _primitiveList.SelectedIndex < 0)
        {
            return;
        }

        var index = _primitiveList.SelectedIndex;
        _primitives.RemoveAt(index);
        _dirty = true;
        RefreshPrimitiveList(Math.Min(index, _primitives.Count - 1));
    }

    private void MovePrimitive(int delta)
    {
        if (_primitives is null || _primitiveList is null)
        {
            return;
        }

        var index = _primitiveList.SelectedIndex;
        var target = index + delta;
        if (index < 0 || target < 0 || target >= _primitives.Count)
        {
            return;
        }

        var primitive = _primitives[index];
        _primitives.RemoveAt(index);
        _primitives.Insert(target, primitive);
        _dirty = true;
        RefreshPrimitiveList(target);
    }

    private void ApplyPrimitiveEdit()
    {
        if (!_primitiveDirty || _primitives is null || _loadedPrimitiveIndex < 0)
        {
            return;
        }

        if (!TryBuildPrimitive(_primitives[_loadedPrimitiveIndex], out var replacement))
        {
            return;
        }

        _primitives[_loadedPrimitiveIndex] = replacement!;
        _dirty = true;
        _primitiveDirty = false;
        RefreshPrimitiveList(_loadedPrimitiveIndex);
    }

    private void DiscardPrimitiveEdit()
    {
        if (_primitiveDirty)
        {
            LoadPrimitive(_loadedPrimitiveIndex);
        }
    }

    private bool TryBuildPrimitive(VectorPrimitive original, out VectorPrimitive? replacement)
    {
        replacement = original switch
        {
            LineSeg when TryPrimitiveNumber("x1", UiStrings.EditorX1Mm, out var x1)
                && TryPrimitiveNumber("y1", UiStrings.EditorY1Mm, out var y1)
                && TryPrimitiveNumber("x2", UiStrings.EditorX2Mm, out var x2)
                && TryPrimitiveNumber("y2", UiStrings.EditorY2Mm, out var y2)
                && TryPrimitiveNumber("stroke", UiStrings.EditorStrokeWidthMm, out var lineStroke)
                => new LineSeg(x1, y1, x2, y2, lineStroke, _primitiveChecks["flag"].Checked),
            CircleShape when TryPrimitiveNumber("x", UiStrings.EditorCenterXMm, out var centerX)
                && TryPrimitiveNumber("y", UiStrings.EditorCenterYMm, out var centerY)
                && TryPrimitiveNumber("radius", UiStrings.EditorRadiusMm, out var radius)
                && TryPrimitiveNumber("stroke", UiStrings.EditorStrokeWidthMm, out var circleStroke)
                => new CircleShape(centerX, centerY, radius, circleStroke, _primitiveChecks["flag"].Checked),
            RectShape when TryPrimitiveNumber("x", UiStrings.EditorXPositionMm, out var rectangleX)
                && TryPrimitiveNumber("y", UiStrings.EditorYPositionMm, out var rectangleY)
                && TryPrimitiveNumber("width", UiStrings.EditorWidthMm, out var rectangleWidth)
                && TryPrimitiveNumber("height", UiStrings.EditorHeightMm, out var rectangleHeight)
                && TryPrimitiveNumber("stroke", UiStrings.EditorStrokeWidthMm, out var rectangleStroke)
                => new RectShape(
                    rectangleX,
                    rectangleY,
                    rectangleWidth,
                    rectangleHeight,
                    rectangleStroke,
                    _primitiveChecks["flag"].Checked),
            TextLabel when TryPrimitiveNumber("x", UiStrings.EditorXPositionMm, out var labelX)
                && TryPrimitiveNumber("y", UiStrings.EditorYPositionMm, out var labelY)
                && TryPrimitiveNumber("font", UiStrings.EditorFontSizeMm, out var font)
                && _primitiveAnchor?.SelectedItem is AnchorChoice anchor
                => new TextLabel(labelX, labelY, _primitiveText["text"].Text, font, anchor.Value),
            _ => null,
        };

        return replacement is not null;
    }

    private bool TryPrimitiveNumber(string key, string label, out double value)
        => TryNumber(_primitiveText[key], label, out value);

    private void RefreshPrimitiveList(int selectedIndex)
    {
        if (_primitiveList is null || _primitives is null)
        {
            return;
        }

        _loadingPrimitive = true;
        _primitiveList.BeginUpdate();
        _primitiveList.Items.Clear();
        foreach (var primitive in _primitives)
        {
            _primitiveList.Items.Add(DescribePrimitive(primitive));
        }

        _primitiveList.EndUpdate();
        _primitiveList.SelectedIndex = _primitiveList.Items.Count == 0
            ? -1
            : Math.Clamp(selectedIndex, 0, _primitiveList.Items.Count - 1);
        _loadingPrimitive = false;
        LoadPrimitive(_primitiveList.SelectedIndex);
        UpdateActions();
    }

    private void MarkDirty()
    {
        if (!_loading)
        {
            _dirty = true;
            UpdateActions();
        }
    }

    private void MarkPrimitiveDirty()
    {
        if (!_loadingPrimitive)
        {
            _primitiveDirty = true;
            UpdateActions();
        }
    }

    private void UpdateActions()
    {
        _apply.Enabled = _dirty && !_primitiveDirty;
        SetStatus(_dirty || _primitiveDirty
            ? UiStrings.PendingReplacementMustBeAppliedOrDiscarded
            : string.Empty);

        if (_primitiveList is null)
        {
            return;
        }

        var hasSelection = _primitiveList.SelectedIndex >= 0;
        _primitiveList.Enabled = !_primitiveDirty;
        _primitiveType?.Enabled = !_primitiveDirty;

        _addPrimitive!.Enabled = !_primitiveDirty;
        _replacePrimitiveType!.Enabled = hasSelection && !_primitiveDirty;
        _removePrimitive!.Enabled = hasSelection && !_primitiveDirty;
        _movePrimitiveUp!.Enabled = hasSelection && !_primitiveDirty && _primitiveList.SelectedIndex > 0;
        _movePrimitiveDown!.Enabled = hasSelection
            && !_primitiveDirty
            && _primitives is not null
            && _primitiveList.SelectedIndex < _primitives.Count - 1;
        _applyPrimitive!.Enabled = _primitiveDirty;
        _discardPrimitive!.Enabled = _primitiveDirty;
    }

    private void ApplyAndClose()
    {
        if (!_dirty || _primitiveDirty)
        {
            return;
        }

        var result = BuildResult();
        if (result is null)
        {
            return;
        }

        Result = result;
        _explicitClose = true;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void DiscardAndClose()
    {
        Result = null;
        _explicitClose = true;
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private DocumentNode? BuildResult()
    {
        _sequence?.EndEdit();
        _table?.EndEdit();

        return _original switch
        {
            Heading => new Heading(decimal.ToInt32(_numbers["level"].Value), _text["text"].Text),
            Paragraph => new Paragraph(_text["text"].Text),
            OrderedSteps => new OrderedSteps(ReadSingleColumn(_sequence!)),
            UnorderedList => new UnorderedList(ReadSingleColumn(_sequence!)),
            ChoiceSet => new ChoiceSet(ReadSingleColumn(_sequence!)),
            TableNode => BuildTable(),
            Card => new Card(_text["title"].Text, _text["body"].Text),
            ImageReference => new ImageReference(new AssetId(_text["asset"].Text), _text["alt"].Text),
            BilingualPair => new BilingualPair(
                _text["sourceText"].Text,
                _text["targetText"].Text,
                _text["sourceLocale"].Text,
                _text["targetLocale"].Text),
            EvidenceLink => new EvidenceLink(_text["claim"].Text, _text["sourcePointer"].Text),
            Citation => new Citation(_text["text"].Text),
            TeacherOnlyNotice => new TeacherOnlyNotice(_text["text"].Text),
            StepRow => BuildStepRow(),
            VectorGraphic when TryNumber(_text["width"], UiStrings.EditorVectorWidthMm, out var width)
                && TryNumber(_text["height"], UiStrings.EditorVectorHeightMm, out var height)
                => new VectorGraphic(width, height, [.. _primitives!], _text["description"].Text),
            _ => null,
        };
    }

    private StepRow BuildStepRow()
    {
        var includeTranslation = _checks["includeTranslation"].Checked;
        var symbol = _checks["includeSymbol"].Checked
            ? new ImageReference(new AssetId(_text["asset"].Text), _text["alt"].Text)
            : null;
        return new StepRow(
            _text["text"].Text,
            symbol,
            includeTranslation ? _text["targetText"].Text : null,
            includeTranslation ? _text["sourceLocale"].Text : null,
            includeTranslation ? _text["targetLocale"].Text : null);
    }

    private TableNode BuildTable()
    {
        var rows = ReadGridRows(_table!);
        IReadOnlyList<string>? header = null;
        if (_tableHasHeader!.Checked && rows.Count > 0)
        {
            header = rows[0];
            rows.RemoveAt(0);
        }

        return new TableNode(header, rows);
    }

    private bool TryNumber(TextBox control, string label, out double value)
    {
        if (double.TryParse(
                control.Text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value)
            && double.IsFinite(value))
        {
            return true;
        }

        SetStatus(UiStrings.NodeEditorInvalidNumber, UiStrings.WithoutMnemonic(label));
        control.Focus();
        return false;
    }

    private static List<string> ReadSingleColumn(DataGridView grid)
        => [.. grid.Rows.Cast<DataGridViewRow>().Select(row => Convert.ToString(row.Cells[0].Value, CultureInfo.InvariantCulture) ?? string.Empty)];

    private static List<IReadOnlyList<string>> ReadGridRows(DataGridView grid)
        => [.. grid.Rows.Cast<DataGridViewRow>()
            .Select(row => (IReadOnlyList<string>)[.. row.Cells.Cast<DataGridViewCell>().Select(cell => Convert.ToString(cell.Value, CultureInfo.InvariantCulture) ?? string.Empty)])];

    private static object[] Pad(IReadOnlyList<string> values, int count)
        => [.. Enumerable.Range(0, count).Select(index => (object)(index < values.Count ? values[index] : string.Empty))];

    private static VectorPrimitive DefaultPrimitive(Type type)
        => type == typeof(LineSeg)
            ? new LineSeg(0, 0, 10, 0)
            : type == typeof(CircleShape)
                ? new CircleShape(10, 10, 5)
                : type == typeof(RectShape)
                    ? new RectShape(0, 0, 10, 10)
                    : new TextLabel(0, 0, UiStrings.WithoutMnemonic(UiStrings.PrimitiveTextLabel));

    private static string DescribePrimitive(VectorPrimitive primitive)
        => primitive switch
        {
            LineSeg line => UiStrings.FormatWithoutMnemonic(
                UiStrings.NodeVectorLineDetail,
                Number(line.X1),
                Number(line.Y1),
                Number(line.X2),
                Number(line.Y2),
                Number(line.StrokeWidthMm),
                UiStrings.WithoutMnemonic(line.Dashed ? UiStrings.BooleanYes : UiStrings.BooleanNo)),
            CircleShape circle => UiStrings.FormatWithoutMnemonic(
                UiStrings.NodeVectorCircleDetail,
                Number(circle.CenterX),
                Number(circle.CenterY),
                Number(circle.RadiusMm),
                Number(circle.StrokeWidthMm),
                UiStrings.WithoutMnemonic(circle.Filled ? UiStrings.BooleanYes : UiStrings.BooleanNo)),
            RectShape rectangle => UiStrings.FormatWithoutMnemonic(
                UiStrings.NodeVectorRectangleDetail,
                Number(rectangle.X),
                Number(rectangle.Y),
                Number(rectangle.WidthMm),
                Number(rectangle.HeightMm),
                Number(rectangle.StrokeWidthMm),
                UiStrings.WithoutMnemonic(rectangle.Filled ? UiStrings.BooleanYes : UiStrings.BooleanNo)),
            TextLabel label => UiStrings.FormatWithoutMnemonic(
                UiStrings.NodeVectorTextLabelDetail,
                Number(label.X),
                Number(label.Y),
                label.Text,
                Number(label.FontSizeMm),
                label.Anchor switch
                {
                    TextAnchor.Start => UiStrings.WithoutMnemonic(UiStrings.TextAnchorStart),
                    TextAnchor.Middle => UiStrings.WithoutMnemonic(UiStrings.TextAnchorMiddle),
                    TextAnchor.End => UiStrings.WithoutMnemonic(UiStrings.TextAnchorEnd),
                    _ => label.Anchor.ToString(),
                }),
            _ => primitive.GetType().Name,
        };

    private static string Number(double value)
        => value.ToString("R", CultureInfo.InvariantCulture);

    private void SetStatus(string template, params object?[] arguments)
    {
        var text = UiStrings.FormatWithoutMnemonic(template, arguments);
        _status.Text = text;
        _status.AccessibleName = text;
        _status.Visible = text.Length > 0;
    }

    private static Button MakeButton(string text, EventHandler onClick)
    {
        var button = new Button
        {
            AutoSize = true,
            Text = text,
        };
        button.Click += onClick;
        return button;
    }

    private sealed record PrimitiveChoice(Type Type, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record AnchorChoice(TextAnchor Value, string Label)
    {
        public override string ToString() => Label;
    }
}
