// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Domain;

namespace Foundry.Modules.DeterministicPress;

// The computational-thinking studio (atlas #211-213, #215; second forge menu,
// item 4). The governing invariant twice over: presses take parameters, never
// prose — and the teacher's code is CARGO, preserved exactly, never executed,
// parsed, corrected, or completed. Indentation is content: it survives as
// exact geometry.

/// <summary>
/// Parsons Press (atlas #212): a teacher-supplied WORKING solution scrambled
/// into a seeded line-ordering puzzle, with teacher-authored distractor lines
/// only. Same seed, same puzzle — a contested key is reprintable, never
/// re-rolled in secret.
/// </summary>
public static class ParsonsPress
{
    public const double IndentMmPerSpace = 2.5;

    public static ArtifactDocument Puzzle(
        string prompt,
        IReadOnlyList<string> solutionLines,
        IReadOnlyList<string> distractorLines,
        int seed,
        PageSize size = PageSize.Letter,
        double marginMm = BlankformsPress.DefaultMarginMm)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ArgumentNullException.ThrowIfNull(solutionLines);
        ArgumentNullException.ThrowIfNull(distractorLines);

        if (solutionLines.Count is < 3 or > 14)
        {
            throw new ArgumentException("Between three and fourteen solution lines.", nameof(solutionLines));
        }

        if (distractorLines.Count > 6 || solutionLines.Count + distractorLines.Count > 16)
        {
            throw new ArgumentException("At most six distractors and sixteen lines in all.", nameof(distractorLines));
        }

        // (IsSolution, OriginalIndex) rides along so the key survives duplicates.
        var entries = solutionLines.Select((line, i) => (Line: line, Solution: true, Index: i))
            .Concat(distractorLines.Select((line, i) => (Line: line, Solution: false, Index: i)))
            .ToList();
        new SeededPrng(seed).Shuffle(entries);

        var (width, height) = BlankformsPress.Dimensions(size);
        const double boxHeight = 11;
        const double gap = 3;

        var total = solutionLines.Count + distractorLines.Count;
        if (marginMm + 12 + total * (boxHeight + gap) > height - marginMm)
        {
            throw new ArgumentException("The lines must fit one page; fewer lines or a taller page.", nameof(solutionLines));
        }

        var primitives = new List<VectorPrimitive>
        {
            new TextLabel(marginMm, marginMm + 5, prompt, 5, TextAnchor.Start),
        };

        var letterBySlot = new char[entries.Count];
        for (var slot = 0; slot < entries.Count; slot++)
        {
            var top = marginMm + 12 + slot * (boxHeight + gap);
            var letter = (char)('A' + slot);
            letterBySlot[slot] = letter;

            primitives.Add(new RectShape(marginMm, top, width - 2 * marginMm, boxHeight, 0.4));
            primitives.Add(new TextLabel(marginMm + 4, top + boxHeight / 2 + 2, letter.ToString(), 6));

            var line = entries[slot].Line;
            var indent = line.Length - line.TrimStart().Length;
            primitives.Add(new TextLabel(
                marginMm + 12 + indent * IndentMmPerSpace, top + boxHeight / 2 + 1.8, line.TrimStart(), 4.5, TextAnchor.Start));

            // The numbering box the learner writes the order into.
            primitives.Add(new RectShape(width - marginMm - 12, top + 1.5, 8, 8, 0.5));
        }

        var solutionLetters = new char[solutionLines.Count];
        var distractorLetters = new List<char>();
        for (var slot = 0; slot < entries.Count; slot++)
        {
            if (entries[slot].Solution)
            {
                solutionLetters[entries[slot].Index] = letterBySlot[slot];
            }
            else
            {
                distractorLetters.Add(letterBySlot[slot]);
            }
        }

        var key = $"Answer key (seed {seed}) — correct order: {string.Join(", ", solutionLetters)}."
            + (distractorLetters.Count > 0 ? $" Distractors: {string.Join(", ", distractorLetters)}." : "");

        return new ArtifactDocument(
        [
            new VectorGraphic(width, height, primitives,
                $"A Parsons line-ordering puzzle: {entries.Count} scrambled lines ({distractorLines.Count} distractors), seed {seed}"),
            new TeacherOnlyNotice(key),
        ]);
    }
}

/// <summary>
/// Trace Table Tutor (atlas #213): the teacher's code, numbered and verbatim,
/// above an empty variable-trace table. The press never reads the code's
/// meaning — tracing it is precisely the learner's work.
/// </summary>
public static class TraceTableTutor
{
    public static ArtifactDocument Sheet(
        string prompt,
        IReadOnlyList<string> codeLines,
        IReadOnlyList<string> variables,
        int traceRows,
        PageSize size = PageSize.Letter,
        double marginMm = BlankformsPress.DefaultMarginMm)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ArgumentNullException.ThrowIfNull(codeLines);
        ArgumentNullException.ThrowIfNull(variables);

        if (codeLines.Count is < 1 or > 16)
        {
            throw new ArgumentException("Between one and sixteen code lines.", nameof(codeLines));
        }

        if (variables.Count is < 1 or > 5 || variables.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Between one and five named variables.", nameof(variables));
        }

        if (traceRows is < 3 or > 14)
        {
            throw new ArgumentException("Between three and fourteen trace rows.", nameof(traceRows));
        }

        var (width, height) = BlankformsPress.Dimensions(size);
        const double codeLineHeight = 6;
        var primitives = new List<VectorPrimitive>
        {
            new TextLabel(marginMm, marginMm + 5, prompt, 5, TextAnchor.Start),
        };

        var codeTop = marginMm + 12;
        for (var i = 0; i < codeLines.Count; i++)
        {
            var y = codeTop + i * codeLineHeight;
            primitives.Add(new TextLabel(marginMm + 6, y, (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), 4, TextAnchor.End));

            var line = codeLines[i];
            var indent = line.Length - line.TrimStart().Length;
            primitives.Add(new TextLabel(
                marginMm + 10 + indent * ParsonsPress.IndentMmPerSpace, y, line.TrimStart(), 4.5, TextAnchor.Start));
        }

        // The trace table: Line | each variable | Output.
        var columns = new List<string> { "Line" };
        columns.AddRange(variables);
        columns.Add("Output");

        var tableTop = codeTop + codeLines.Count * codeLineHeight + 8;
        const double headerHeight = 10;
        const double rowHeight = 10;
        if (tableTop + headerHeight + traceRows * rowHeight > height - marginMm)
        {
            throw new ArgumentException("Code plus trace table must fit one page; fewer lines or rows.", nameof(traceRows));
        }

        var columnWidth = (width - 2 * marginMm) / columns.Count;
        for (var c = 0; c < columns.Count; c++)
        {
            var x = marginMm + c * columnWidth;
            primitives.Add(new RectShape(x, tableTop, columnWidth, headerHeight, 0.6));
            primitives.Add(new TextLabel(x + columnWidth / 2, tableTop + headerHeight / 2 + 1.8, columns[c], 4.5));
            for (var r = 0; r < traceRows; r++)
            {
                primitives.Add(new RectShape(x, tableTop + headerHeight + r * rowHeight, columnWidth, rowHeight, 0.35));
            }
        }

        return new ArtifactDocument([new VectorGraphic(width, height, primitives,
            $"A trace-table sheet: {codeLines.Count} numbered code lines above an empty table for {variables.Count} variables over {traceRows} rows")]);
    }
}

/// <summary>
/// Bug Zoo (atlas #214; fourth forge menu, item 4): a buggy program the
/// TEACHER wrote, printed verbatim with the trace-table discipline —
/// indentation as geometry, code never interpreted — above diagnose, repair,
/// and explain sections. The teacher's note naming the intended misconception
/// is required and rides as a teacher-only notice: teacher-authored bugs
/// only, and the note is that authorship in ink.
/// </summary>
public static class BugZoo
{
    public static ArtifactDocument Sheet(
        string prompt,
        IReadOnlyList<string> codeLines,
        IReadOnlyList<string> sectionLabels,
        string misconceptionNote,
        PageSize size = PageSize.Letter,
        double marginMm = BlankformsPress.DefaultMarginMm)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ArgumentNullException.ThrowIfNull(codeLines);
        ArgumentNullException.ThrowIfNull(sectionLabels);

        if (codeLines.Count is < 1 or > 16)
        {
            throw new ArgumentException("Between one and sixteen code lines.", nameof(codeLines));
        }

        if (sectionLabels.Count != 3 || sectionLabels.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Exactly three non-blank section labels: diagnose, repair, explain.", nameof(sectionLabels));
        }

        if (string.IsNullOrWhiteSpace(misconceptionNote))
        {
            throw new ArgumentException("Name the intended misconception; Bug Zoo prints teacher-authored bugs only.", nameof(misconceptionNote));
        }

        var (width, height) = BlankformsPress.Dimensions(size);
        const double codeLineHeight = 6;
        var primitives = new List<VectorPrimitive>
        {
            new TextLabel(marginMm, marginMm + 5, prompt, 5, TextAnchor.Start),
        };

        var codeTop = marginMm + 12;
        for (var i = 0; i < codeLines.Count; i++)
        {
            var y = codeTop + i * codeLineHeight;
            primitives.Add(new TextLabel(marginMm + 6, y, (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), 4, TextAnchor.End));

            var line = codeLines[i];
            var indent = line.Length - line.TrimStart().Length;
            primitives.Add(new TextLabel(
                marginMm + 10 + indent * ParsonsPress.IndentMmPerSpace, y, line.TrimStart(), 4.5, TextAnchor.Start));
        }

        var sectionsTop = codeTop + codeLines.Count * codeLineHeight + 8;
        const double minSectionHeight = 30;
        if (height - marginMm - sectionsTop < 3 * minSectionHeight)
        {
            throw new ArgumentException("The code plus the three sections must fit one page; fewer code lines.", nameof(codeLines));
        }

        var sectionHeight = (height - marginMm - sectionsTop) / 3;
        for (var s = 0; s < 3; s++)
        {
            var top = sectionsTop + s * sectionHeight;
            primitives.Add(new TextLabel(marginMm, top + 5, sectionLabels[s], 4.5, TextAnchor.Start));
            for (var y = top + 14; y <= top + sectionHeight - 3; y += 9)
            {
                primitives.Add(new LineSeg(marginMm, y, width - marginMm, y, 0.3));
            }
        }

        return new ArtifactDocument(
        [
            new VectorGraphic(width, height, primitives,
                $"A Bug Zoo sheet: {codeLines.Count} numbered lines of the teacher's buggy program above diagnose, repair, and explain sections"),
            new TeacherOnlyNotice($"Intended misconception (teacher only): {misconceptionNote}"),
        ]);
    }
}

/// <summary>
/// Unplugged Algorithm Atelier (atlas #211) and Rubber Duck Deck (atlas #215):
/// card decks the learners execute or interrogate as human programs. Every
/// word on every card is teacher-typed or teacher-editable — control-card
/// wording included, so any classroom language works.
/// </summary>
public static class AlgorithmAtelier
{
    private const int CardsPerRow = 2;
    private const int CardsPerColumn = 4;

    public static ArtifactDocument ActionCards(
        IReadOnlyList<string> actionLines,
        IReadOnlyList<string> controlLines,
        PageSize size = PageSize.Letter,
        double marginMm = BlankformsPress.DefaultMarginMm)
    {
        ArgumentNullException.ThrowIfNull(actionLines);
        ArgumentNullException.ThrowIfNull(controlLines);

        if (actionLines.Count is < 3 or > 24 || actionLines.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Between three and twenty-four non-blank action cards.", nameof(actionLines));
        }

        if (controlLines.Count > 12 || controlLines.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("At most twelve non-blank control cards.", nameof(controlLines));
        }

        var cards = actionLines.Select(text => (Text: text, Control: false))
            .Concat(controlLines.Select(text => (Text: text, Control: true)))
            .ToList();

        return Deck(cards, size, marginMm,
            page => $"Algorithm cards, page {page}: teacher-typed action cards and double-bordered control cards for human-program sequencing");
    }

    public static ArtifactDocument PromptCards(
        IReadOnlyList<string> promptLines,
        PageSize size = PageSize.Letter,
        double marginMm = BlankformsPress.DefaultMarginMm)
    {
        ArgumentNullException.ThrowIfNull(promptLines);
        if (promptLines.Count is < 3 or > 16 || promptLines.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Between three and sixteen non-blank prompt cards.", nameof(promptLines));
        }

        return Deck([.. promptLines.Select(text => (Text: text, Control: false))], size, marginMm,
            page => $"Rubber-duck cards, page {page}: self-explanation and debugging prompts to work through before asking for help");
    }

    private static ArtifactDocument Deck(
        List<(string Text, bool Control)> cards,
        PageSize size,
        double marginMm,
        Func<int, string> describe)
    {
        var (width, height) = BlankformsPress.Dimensions(size);
        var cardWidth = (width - 2 * marginMm) / CardsPerRow;
        var cardHeight = (height - 2 * marginMm) / CardsPerColumn;
        var perPage = CardsPerRow * CardsPerColumn;

        var nodes = new List<DocumentNode>();
        for (var page = 0; page * perPage < cards.Count; page++)
        {
            var primitives = new List<VectorPrimitive>();
            var first = page * perPage;
            var last = Math.Min(first + perPage, cards.Count);

            for (var i = first; i < last; i++)
            {
                var slot = i - first;
                var x = marginMm + slot % CardsPerRow * cardWidth;
                var y = marginMm + slot / CardsPerRow * cardHeight;

                primitives.Add(new RectShape(x, y, cardWidth, cardHeight, 0.5));
                if (cards[i].Control)
                {
                    // Control cards wear a double border; shape, never color alone.
                    primitives.Add(new RectShape(x + 2, y + 2, cardWidth - 4, cardHeight - 4, 0.35));
                }

                primitives.Add(new TextLabel(x + cardWidth / 2, y + cardHeight / 2 + 2, cards[i].Text, 5));
            }

            nodes.Add(new VectorGraphic(width, height, primitives, describe(page + 1)));
        }

        return new ArtifactDocument(nodes);
    }
}
