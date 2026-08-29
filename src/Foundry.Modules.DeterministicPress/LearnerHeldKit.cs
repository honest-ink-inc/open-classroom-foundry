// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Domain;

namespace Foundry.Modules.DeterministicPress;

// The learner-held kit (atlas #222-#224; third forge menu, item 6): paper the
// LEARNER keeps. This resolves the review's portfolio deferral (F4) in the
// only lane that never needed a gate — the identified, longitudinal record
// exists solely in the learner's hands, and every sheet says so in ink.

public static class LearnerHeldKit
{
    /// <summary>Portfolio Passport (atlas #224): table of contents, selection slips, caption frames, and growth-reflection pages the learner maintains.</summary>
    public static ArtifactDocument PortfolioPassport(
        IReadOnlyList<string> selectionPrompts,
        IReadOnlyList<string> reflectionPrompts,
        int contentsRows,
        string pledge,
        PageSize size = PageSize.Letter,
        double marginMm = BlankformsPress.DefaultMarginMm)
    {
        ValidatePrompts(selectionPrompts, 1, 4, nameof(selectionPrompts));
        ValidatePrompts(reflectionPrompts, 1, 4, nameof(reflectionPrompts));
        ArgumentException.ThrowIfNullOrWhiteSpace(pledge);
        if (contentsRows is < 4 or > 14)
        {
            throw new ArgumentException("Between four and fourteen contents rows.", nameof(contentsRows));
        }

        var (width, height) = BlankformsPress.Dimensions(size);
        var nodes = new List<DocumentNode>
        {
            ContentsPage(width, height, marginMm, contentsRows, pledge),
            SelectionSlipsPage(width, height, marginMm, selectionPrompts, pledge),
            CaptionFramesPage(width, height, marginMm, pledge),
            PromptedLinesPage(width, height, marginMm, reflectionPrompts, pledge,
                "A growth-reflection page: each prompt followed by ruled lines the learner fills"),
        };

        return new ArtifactDocument(nodes);
    }

    /// <summary>My Strategy Shelf (atlas #223): strategy cards the learner assembles from teacher-offered, teacher-editable sets.</summary>
    public static ArtifactDocument StrategyShelf(
        IReadOnlyList<string> strategies,
        string pledge,
        PageSize size = PageSize.Letter,
        double marginMm = BlankformsPress.DefaultMarginMm)
    {
        ArgumentNullException.ThrowIfNull(strategies);
        ArgumentException.ThrowIfNullOrWhiteSpace(pledge);
        if (strategies.Count is < 4 or > 24 || strategies.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Between four and twenty-four non-blank strategies.", nameof(strategies));
        }

        const int columns = 2;
        const int rows = 4;
        var (width, height) = BlankformsPress.Dimensions(size);
        var cardWidth = (width - 2 * marginMm) / columns;
        var cardHeight = (height - 2 * marginMm - 10) / rows;
        var perPage = columns * rows;

        var nodes = new List<DocumentNode>();
        for (var page = 0; page * perPage < strategies.Count; page++)
        {
            var primitives = new List<VectorPrimitive>();
            var first = page * perPage;
            var last = Math.Min(first + perPage, strategies.Count);

            for (var i = first; i < last; i++)
            {
                var slot = i - first;
                var x = marginMm + slot % columns * cardWidth;
                var y = marginMm + slot / columns * cardHeight;
                primitives.Add(new RectShape(x, y, cardWidth, cardHeight, 0.5));
                primitives.Add(new TextLabel(x + cardWidth / 2, y + cardHeight / 2 + 2, strategies[i], 5));
            }

            primitives.Add(Pledge(width, height, marginMm, pledge));
            nodes.Add(new VectorGraphic(width, height, primitives,
                $"Strategy cards, page {page + 1}: cut-ready cards the learner chooses and keeps"));
        }

        return new ArtifactDocument(nodes);
    }

    /// <summary>Goal Post (atlas #222): goal-setting and self-monitoring sheets that live in the learner's folder, never in a data system.</summary>
    public static ArtifactDocument GoalPost(
        IReadOnlyList<string> prompts,
        string pledge,
        PageSize size = PageSize.Letter,
        double marginMm = BlankformsPress.DefaultMarginMm)
    {
        ValidatePrompts(prompts, 2, 6, nameof(prompts));
        ArgumentException.ThrowIfNullOrWhiteSpace(pledge);

        var (width, height) = BlankformsPress.Dimensions(size);
        return new ArtifactDocument([PromptedLinesPage(width, height, marginMm, prompts, pledge,
            "A learner-held goal sheet: each prompt followed by ruled lines the learner fills")]);
    }

    private static VectorGraphic ContentsPage(double width, double height, double marginMm, int rows, string pledge)
    {
        var primitives = new List<VectorPrimitive>();
        var rowHeight = (height - 2 * marginMm - 10) / rows;
        for (var r = 0; r < rows; r++)
        {
            var y = marginMm + r * rowHeight;
            primitives.Add(new TextLabel(marginMm + 6, y + rowHeight / 2 + 2,
                (r + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), 5, TextAnchor.End));
            primitives.Add(new LineSeg(marginMm + 10, y + rowHeight / 2 + 4, width - marginMm, y + rowHeight / 2 + 4, 0.3));
        }

        primitives.Add(Pledge(width, height, marginMm, pledge));
        return new VectorGraphic(width, height, primitives,
            $"A table of contents the learner maintains: {rows} numbered ruled rows");
    }

    private static VectorGraphic SelectionSlipsPage(double width, double height, double marginMm, IReadOnlyList<string> prompts, string pledge)
    {
        const int slips = 4;
        var primitives = new List<VectorPrimitive>();
        var slipHeight = (height - 2 * marginMm - 10) / slips;
        for (var s = 0; s < slips; s++)
        {
            var top = marginMm + s * slipHeight;
            primitives.Add(new RectShape(marginMm, top, width - 2 * marginMm, slipHeight - 3, 0.5));
            for (var p = 0; p < prompts.Count; p++)
            {
                var y = top + 4 + p * ((slipHeight - 10) / prompts.Count);
                primitives.Add(new TextLabel(marginMm + 4, y + 4, prompts[p], 4, TextAnchor.Start));
                primitives.Add(new LineSeg(marginMm + 60, y + 5.5, width - marginMm - 4, y + 5.5, 0.3));
            }
        }

        primitives.Add(Pledge(width, height, marginMm, pledge));
        return new VectorGraphic(width, height, primitives,
            $"Four cut-ready selection slips, each carrying the {prompts.Count} choosing prompts");
    }

    private static VectorGraphic CaptionFramesPage(double width, double height, double marginMm, string pledge)
    {
        const int frames = 2;
        var primitives = new List<VectorPrimitive>();
        var frameHeight = (height - 2 * marginMm - 10) / frames;
        for (var f = 0; f < frames; f++)
        {
            var top = marginMm + f * frameHeight;
            primitives.Add(new RectShape(marginMm, top, width - 2 * marginMm, frameHeight - 18, 0.5));
            primitives.Add(new LineSeg(marginMm, top + frameHeight - 10, width - marginMm, top + frameHeight - 10, 0.3));
            primitives.Add(new LineSeg(marginMm, top + frameHeight - 4, width - marginMm, top + frameHeight - 4, 0.3));
        }

        primitives.Add(Pledge(width, height, marginMm, pledge));
        return new VectorGraphic(width, height, primitives,
            "Two caption frames: a mounting box with ruled caption lines beneath each");
    }

    private static VectorGraphic PromptedLinesPage(double width, double height, double marginMm, IReadOnlyList<string> prompts, string pledge, string description)
    {
        const int linesPerPrompt = 3;
        const double lineSpacing = 9;
        var primitives = new List<VectorPrimitive>();
        var block = (height - 2 * marginMm - 10) / prompts.Count;
        for (var p = 0; p < prompts.Count; p++)
        {
            var top = marginMm + p * block;
            primitives.Add(new TextLabel(marginMm, top + 5, prompts[p], 5, TextAnchor.Start));
            for (var line = 1; line <= linesPerPrompt; line++)
            {
                primitives.Add(new LineSeg(marginMm, top + 7 + line * lineSpacing, width - marginMm, top + 7 + line * lineSpacing, 0.3));
            }
        }

        primitives.Add(Pledge(width, height, marginMm, pledge));
        return new VectorGraphic(width, height, primitives, description);
    }

    /// <summary>The commitment, in ink, on every sheet of the kit.</summary>
    private static TextLabel Pledge(double width, double height, double marginMm, string pledge)
        => new(width / 2, height - marginMm + 6, pledge, 3.5);

    private static void ValidatePrompts(IReadOnlyList<string> prompts, int minimum, int maximum, string name)
    {
        ArgumentNullException.ThrowIfNull(prompts);
        if (prompts.Count < minimum || prompts.Count > maximum || prompts.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException($"Between {minimum} and {maximum} non-blank prompts.", name);
        }
    }
}
