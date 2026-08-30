// SPDX-License-Identifier: GPL-3.0-or-later
namespace Foundry.App.WinForms;

/// <summary>Two numbers and a button: how large the wall display tiles. Standard controls only.</summary>
public sealed class TileForm : Form
{
    private readonly NumericUpDown _columns;
    private readonly NumericUpDown _rows;

    public TileForm()
    {
        Text = UiStrings.WithoutMnemonic(UiStrings.TileForWall).TrimEnd('…');
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(260, 130);

        _columns = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 4,
            Value = 2,
            Width = 70,
            AccessibleName = UiStrings.WithoutMnemonic(UiStrings.TileColumns),
        };
        _rows = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 4,
            Value = 2,
            Width = 70,
            AccessibleName = UiStrings.WithoutMnemonic(UiStrings.TileRows),
        };

        var make = new Button { Text = UiStrings.TileMake, AutoSize = true, DialogResult = DialogResult.OK };
        AcceptButton = make;

        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Padding = new Padding(12) };
        grid.Controls.Add(new Label { Text = UiStrings.TileColumns, AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        grid.Controls.Add(_columns, 1, 0);
        grid.Controls.Add(new Label { Text = UiStrings.TileRows, AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        grid.Controls.Add(_rows, 1, 1);
        grid.Controls.Add(make, 1, 2);
        Controls.Add(grid);

        UiLocale.ApplyChrome(this);
    }

    public int Columns => (int)_columns.Value;

    public int Rows => (int)_rows.Value;
}
