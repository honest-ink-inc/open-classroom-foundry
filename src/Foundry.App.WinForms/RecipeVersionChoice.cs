// SPDX-License-Identifier: GPL-3.0-or-later
namespace Foundry.App.WinForms;

/// <summary>Visible text never serves as an execution selector.</summary>
internal sealed record RecipeVersionChoice(string Version, bool IsReplacement)
{
    public override string ToString() => UiStrings.FormatWithoutMnemonic(
        IsReplacement ? UiStrings.ReplacementRecipeVersion : UiStrings.HistoricalRecipeVersion, Version);
}
