// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Domain;

namespace Foundry.Modules.DeterministicPress;

// The math scaffold presses (atlas #81, #87; third forge menu, item 4).
// The teacher's worked steps are cargo — verbatim, never solved, checked,
// or completed by the press. The fade is exact arithmetic.

/// <summary>
/// Worked Example Fader (atlas #81): the teacher's complete worked solution,
/// then progressively faded practice sheets — later steps giving way to ruled
/// blanks, last step first, until the final sheet is independent practice.
/// Every sheet keeps the identical step structure: slot k is step k, worked
/// or blank, on every page.
/// </summary>
public static class WorkedExampleFader
{
    public static ArtifactDocument Sheets(
        string problem,
        IReadOnlyList<string> steps,
        int fadeSheets,
        string selfCheck,
        PageSize size = PageSize.Letter,
        double marginMm = BlankformsPress.DefaultMarginMm)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(problem);
        ArgumentException.ThrowIfNullOrWhiteSpace(selfCheck);
        ArgumentNullException.ThrowIfNull(steps);

        if (steps.Count is < 2 or > 12)
        {
            throw new ArgumentException("Between two and twelve worked steps.", nameof(steps));
        }

        if (fadeSheets is < 1 or > 4)
        {
            throw new ArgumentException("Between one and four faded sheets.", nameof(fadeSheets));
        }

        var (width, height) = BlankformsPress.Dimensions(size);
        const double stepHeight = 14;
        if (marginMm + 14 + steps.Count * stepHeight + 16 > height - marginMm)
        {
            throw new ArgumentException("Problem, steps, and self-check must fit one page.", nameof(steps));
        }

        var nodes = new List<DocumentNode>();
        for (var sheet = 0; sheet <= fadeSheets; sheet++)
        {
            // Sheet 0 keeps everything; the last sheet keeps nothing.
            var keep = steps.Count - (int)Math.Ceiling((double)steps.Count * sheet / fadeSheets);
            if (sheet == 0)
            {
                keep = steps.Count;
            }

            var primitives = new List<VectorPrimitive>
            {
                new TextLabel(marginMm, marginMm + 5, problem, 5, TextAnchor.Start),
            };

            for (var i = 0; i < steps.Count; i++)
            {
                var y = marginMm + 14 + i * stepHeight;
                primitives.Add(new TextLabel(marginMm + 6, y + 6,
                    (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), 4.5, TextAnchor.End));

                if (i < keep)
                {
                    primitives.Add(new TextLabel(marginMm + 10, y + 6, steps[i], 4.5, TextAnchor.Start));
                }
                else
                {
                    primitives.Add(new LineSeg(marginMm + 10, y + 8, width - marginMm, y + 8, 0.3));
                }
            }

            var checkY = marginMm + 14 + steps.Count * stepHeight + 6;
            primitives.Add(new RectShape(marginMm, checkY, 6, 6, 0.5));
            primitives.Add(new TextLabel(marginMm + 9, checkY + 5, selfCheck, 4, TextAnchor.Start));

            nodes.Add(new VectorGraphic(width, height, primitives,
                sheet == 0
                    ? $"Worked example, fully shown: {steps.Count} steps"
                    : $"Faded practice sheet {sheet} of {fadeSheets}: {keep} of {steps.Count} steps shown, the rest ruled blank"));
        }

        return new ArtifactDocument(nodes);
    }
}

/// <summary>
/// Estimation First (atlas #87): each teacher-typed problem wrapped in the
/// estimate → range → exact → compare scaffold — magnitude before arithmetic,
/// every section label the teacher's own words.
/// </summary>
public static class EstimationFirst
{
    public static ArtifactDocument Sheets(
        IReadOnlyList<string> problems,
        IReadOnlyList<string> sectionLabels,
        PageSize size = PageSize.Letter,
        double marginMm = BlankformsPress.DefaultMarginMm)
    {
        ArgumentNullException.ThrowIfNull(problems);
        ArgumentNullException.ThrowIfNull(sectionLabels);

        if (problems.Count is < 1 or > 12 || problems.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Between one and twelve non-blank problems.", nameof(problems));
        }

        if (sectionLabels.Count != 4 || sectionLabels.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Exactly four non-blank section labels.", nameof(sectionLabels));
        }

        var (width, height) = BlankformsPress.Dimensions(size);
        const double sectionHeight = 11;
        var blockHeight = 8 + 4 * sectionHeight + 6;
        var perPage = (int)Math.Floor((height - 2 * marginMm) / blockHeight);
        if (perPage < 1)
        {
            throw new ArgumentException("At least one problem block must fit the page.", nameof(problems));
        }

        var nodes = new List<DocumentNode>();
        for (var page = 0; page * perPage < problems.Count; page++)
        {
            var primitives = new List<VectorPrimitive>();
            var first = page * perPage;
            var last = Math.Min(first + perPage, problems.Count);

            for (var i = first; i < last; i++)
            {
                var top = marginMm + (i - first) * blockHeight;
                primitives.Add(new TextLabel(marginMm, top + 5, problems[i], 5, TextAnchor.Start));

                for (var s = 0; s < 4; s++)
                {
                    var y = top + 8 + s * sectionHeight;
                    primitives.Add(new TextLabel(marginMm + 4, y + 5, sectionLabels[s], 4, TextAnchor.Start));
                    primitives.Add(new LineSeg(marginMm + 70, y + 6.5, width - marginMm, y + 6.5, 0.3));
                }
            }

            nodes.Add(new VectorGraphic(width, height, primitives,
                $"Estimation-first sheet {page + 1}: problems {first + 1} to {last} of {problems.Count}, each with the four-section scaffold"));
        }

        return new ArtifactDocument(nodes);
    }
}
