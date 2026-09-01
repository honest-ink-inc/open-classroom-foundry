// SPDX-License-Identifier: GPL-3.0-or-later
namespace Foundry.App.WinForms;

/// <summary>
/// Gives every item in a fixed-width selector one explicit visual reading path.
/// The collapsed selector may remain compact; opening it must expose the whole
/// current item text instead of silently clipping the distinguishing words.
/// </summary>
internal static class ComboBoxReadingPath
{
    public static void EnsureEveryItemFits(ComboBox combo)
    {
        ArgumentNullException.ThrowIfNull(combo);

        combo.FontChanged -= RecalculateForCurrentFont;
        combo.FontChanged += RecalculateForCurrentFont;
        Recalculate(combo);
    }

    private static void RecalculateForCurrentFont(object? sender, EventArgs e)
    {
        if (sender is ComboBox combo)
        {
            Recalculate(combo);
        }
    }

    private static void Recalculate(ComboBox combo)
    {
        var widestItem = combo.Items.Cast<object>()
            .Select(item => TextRenderer.MeasureText(
                item.ToString() ?? "",
                combo.Font,
                Size.Empty,
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width)
            .DefaultIfEmpty(0)
            .Max();
        combo.DropDownWidth = Math.Max(
            combo.Width,
            widestItem + SystemInformation.VerticalScrollBarWidth + 8);
    }
}
