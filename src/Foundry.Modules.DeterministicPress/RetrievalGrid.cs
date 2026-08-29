// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Domain;

namespace Foundry.Modules.DeterministicPress;

/// <summary>
/// Retrieval Grid Generator (atlas #226; second forge menu, item 5): spaced-
/// retrieval grids drawn seeded-deterministically from the teacher's OWN
/// question bank — questions verbatim, never rephrased; selection and
/// arrangement seeded, never random at print time. Measurement craft with no
/// measurement claims: the scheduling suggestion is a printed teacher note,
/// not a tracking system.
/// </summary>
public static class RetrievalGrid
{
    public static ArtifactDocument Grids(
        IReadOnlyList<string> questions,
        int gridCount,
        int rows,
        int columns,
        int seed,
        PageSize size = PageSize.Letter,
        double marginMm = BlankformsPress.DefaultMarginMm)
    {
        ArgumentNullException.ThrowIfNull(questions);

        if (questions.Any(string.IsNullOrWhiteSpace) || questions.Distinct(StringComparer.Ordinal).Count() != questions.Count)
        {
            throw new ArgumentException("Questions must be non-blank and distinct.", nameof(questions));
        }

        if (rows is < 2 or > 4 || columns is < 2 or > 4)
        {
            throw new ArgumentException("Grids between two and four rows and columns.", nameof(rows));
        }

        if (gridCount is < 1 or > 6)
        {
            throw new ArgumentException("Between one and six grids per run.", nameof(gridCount));
        }

        var cells = rows * columns;
        if (questions.Count < cells)
        {
            throw new ArgumentException($"A {rows}x{columns} grid draws {cells} questions; the bank has {questions.Count}.", nameof(questions));
        }

        var (width, height) = BlankformsPress.Dimensions(size);
        var cellWidth = (width - 2 * marginMm) / columns;
        var cellHeight = (height - 2 * marginMm - 12) / rows;
        var prng = new SeededPrng(seed);

        var nodes = new List<DocumentNode>
        {
            new TeacherOnlyNotice(
                $"Retrieval spacing suggestion (seed {seed}): grid 1 today, grid 2 in two days, grid 3 next week, "
                + "later grids at growing intervals. The same seed reprints the same grids, so a lost sheet is a reprint, not a reshuffle."),
        };

        for (var g = 0; g < gridCount; g++)
        {
            var order = Enumerable.Range(0, questions.Count).ToList();
            prng.Shuffle(order);

            var primitives = new List<VectorPrimitive>();
            for (var cell = 0; cell < cells; cell++)
            {
                var x = marginMm + cell % columns * cellWidth;
                var y = marginMm + cell / columns * cellHeight;
                primitives.Add(new RectShape(x, y, cellWidth, cellHeight, 0.4));
                primitives.Add(new TextLabel(x + cellWidth / 2, y + 8, questions[order[cell]], 4));
            }

            primitives.Add(new TextLabel(width / 2, marginMm + rows * cellHeight + 8,
                $"Retrieval grid {g + 1} of {gridCount} · seed {seed}", 3.5));

            nodes.Add(new VectorGraphic(width, height, primitives,
                $"Retrieval grid {g + 1} of {gridCount}: {rows} by {columns} cells drawn from the teacher's {questions.Count}-question bank with seed {seed}"));
        }

        return new ArtifactDocument(nodes);
    }
}
