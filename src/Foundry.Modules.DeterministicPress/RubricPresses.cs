// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Domain;

namespace Foundry.Modules.DeterministicPress;

// The rubric and criteria presses (atlas #24, #65, #70; third forge menu,
// item 7): the teacher's own criteria as tables and checklists — no criterion
// is ever invented, weighted, or scored by the press.

public static class RubricPresses
{
    /// <summary>One-Point Rubric (atlas #65): criteria centered, evidence columns beside — growth on one side, beyond on the other.</summary>
    public static ArtifactDocument OnePointRubric(
        IReadOnlyList<string> criteria,
        string belowHeader,
        string beyondHeader,
        PageSize size = PageSize.Letter,
        double marginMm = BlankformsPress.DefaultMarginMm)
    {
        ValidateLines(criteria, 2, 8, nameof(criteria));
        ArgumentException.ThrowIfNullOrWhiteSpace(belowHeader);
        ArgumentException.ThrowIfNullOrWhiteSpace(beyondHeader);

        var (width, height) = BlankformsPress.Dimensions(size);
        var columnWidth = (width - 2 * marginMm) / 3;
        const double headerHeight = 12;
        var rowHeight = (height - 2 * marginMm - headerHeight) / criteria.Count;

        var primitives = new List<VectorPrimitive>();
        string[] headers = [belowHeader, "", beyondHeader];
        for (var c = 0; c < 3; c++)
        {
            var x = marginMm + c * columnWidth;
            primitives.Add(new RectShape(x, marginMm, columnWidth, headerHeight, 0.6));
            if (headers[c].Length > 0)
            {
                primitives.Add(new TextLabel(x + columnWidth / 2, marginMm + headerHeight / 2 + 1.5, headers[c], 4));
            }

            for (var r = 0; r < criteria.Count; r++)
            {
                primitives.Add(new RectShape(x, marginMm + headerHeight + r * rowHeight, columnWidth, rowHeight, 0.4));
            }
        }

        for (var r = 0; r < criteria.Count; r++)
        {
            primitives.Add(new TextLabel(
                marginMm + columnWidth + columnWidth / 2,
                marginMm + headerHeight + r * rowHeight + 8,
                criteria[r], 4.5));
        }

        return new ArtifactDocument([new VectorGraphic(width, height, primitives,
            $"A one-point rubric: {criteria.Count} criteria centered, evidence columns for growing toward and going beyond")]);
    }

    /// <summary>Success Criteria Studio (atlas #70): a learner checklist with a quality continuum at the foot.</summary>
    public static ArtifactDocument SuccessCriteria(
        string objective,
        IReadOnlyList<string> criteria,
        IReadOnlyList<string> continuumLabels,
        PageSize size = PageSize.Letter,
        double marginMm = BlankformsPress.DefaultMarginMm)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objective);
        ValidateLines(criteria, 2, 10, nameof(criteria));
        ValidateLines(continuumLabels, 2, 4, nameof(continuumLabels));

        var (width, height) = BlankformsPress.Dimensions(size);
        const double rowHeight = 14;

        var primitives = new List<VectorPrimitive>
        {
            new TextLabel(marginMm, marginMm + 5, objective, 5.5, TextAnchor.Start),
        };

        for (var r = 0; r < criteria.Count; r++)
        {
            var y = marginMm + 14 + r * rowHeight;
            primitives.Add(new RectShape(marginMm, y, 6, 6, 0.5));
            primitives.Add(new TextLabel(marginMm + 10, y + 5, criteria[r], 4.5, TextAnchor.Start));
        }

        // The quality continuum: equal cells across the foot of the page.
        var continuumTop = height - marginMm - 20;
        var cellWidth = (width - 2 * marginMm) / continuumLabels.Count;
        for (var c = 0; c < continuumLabels.Count; c++)
        {
            var x = marginMm + c * cellWidth;
            primitives.Add(new RectShape(x, continuumTop, cellWidth, 20, 0.5));
            primitives.Add(new TextLabel(x + cellWidth / 2, continuumTop + 6, continuumLabels[c], 4));
        }

        return new ArtifactDocument([new VectorGraphic(width, height, primitives,
            $"A success-criteria checklist: {criteria.Count} observable criteria under the objective, a {continuumLabels.Count}-stage quality continuum at the foot")]);
    }

    /// <summary>Done Definition (atlas #24): the completion checklist with looks-like and doesn't-look-like columns and a final self-check.</summary>
    public static ArtifactDocument DoneDefinition(
        IReadOnlyList<string> checklist,
        IReadOnlyList<string> examples,
        IReadOnlyList<string> nonexamples,
        string finalCheck,
        PageSize size = PageSize.Letter,
        double marginMm = BlankformsPress.DefaultMarginMm)
    {
        ValidateLines(checklist, 2, 10, nameof(checklist));
        ArgumentNullException.ThrowIfNull(examples);
        ArgumentNullException.ThrowIfNull(nonexamples);
        ArgumentException.ThrowIfNullOrWhiteSpace(finalCheck);

        if (examples.Count > 6 || nonexamples.Count > 6)
        {
            throw new ArgumentException("At most six examples and six nonexamples.", nameof(examples));
        }

        var (width, height) = BlankformsPress.Dimensions(size);
        const double rowHeight = 12;

        var primitives = new List<VectorPrimitive>();
        for (var r = 0; r < checklist.Count; r++)
        {
            var y = marginMm + r * rowHeight;
            primitives.Add(new RectShape(marginMm, y, 6, 6, 0.5));
            primitives.Add(new TextLabel(marginMm + 10, y + 5, checklist[r], 4.5, TextAnchor.Start));
        }

        // Looks-like and doesn't-look-like, side by side beneath the checklist.
        var columnsTop = marginMm + checklist.Count * rowHeight + 8;
        var columnWidth = (width - 2 * marginMm) / 2;
        var columnHeight = height - marginMm - 22 - columnsTop;
        for (var c = 0; c < 2; c++)
        {
            var x = marginMm + c * columnWidth;
            primitives.Add(new RectShape(x, columnsTop, columnWidth, columnHeight, 0.5));
            var entries = c == 0 ? examples : nonexamples;
            for (var e = 0; e < entries.Count; e++)
            {
                primitives.Add(new TextLabel(x + 4, columnsTop + 8 + e * 9, entries[e], 4, TextAnchor.Start));
            }
        }

        var checkY = height - marginMm - 12;
        primitives.Add(new RectShape(marginMm, checkY, 6, 6, 0.5));
        primitives.Add(new TextLabel(marginMm + 10, checkY + 5, finalCheck, 4.5, TextAnchor.Start));

        return new ArtifactDocument([new VectorGraphic(width, height, primitives,
            $"A definition of done: {checklist.Count} checklist items, looks-like and doesn't-look-like columns, and a final self-check")]);
    }

    private static void ValidateLines(IReadOnlyList<string> lines, int minimum, int maximum, string name)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (lines.Count < minimum || lines.Count > maximum || lines.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException($"Between {minimum} and {maximum} non-blank lines.", name);
        }
    }
}
