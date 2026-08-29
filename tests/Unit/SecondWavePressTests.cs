using Foundry.Domain;
using Foundry.Modules.DeterministicPress;

namespace Foundry.Tests.Unit;

public class SecondWaveBlankformsTests
{
    [Fact]
    public void The_hundred_chart_counts_a_hundred_cells_in_exact_rows_of_ten()
    {
        var graphic = (VectorGraphic)BlankformsPress.HundredChart(start: 1, cellMm: 17).Nodes[0];

        var cells = graphic.Primitives.OfType<RectShape>().ToList();
        Assert.Equal(100, cells.Count);
        Assert.All(cells, c =>
        {
            Assert.Equal(17, c.WidthMm);
            Assert.Equal(17, c.HeightMm);
        });

        var labels = graphic.Primitives.OfType<TextLabel>().ToList();
        Assert.Equal(100, labels.Count);
        var one = labels.Single(l => l.Text == "1");
        var hundred = labels.Single(l => l.Text == "100");
        Assert.Equal(9 * 17, hundred.X - one.X, 9);
        Assert.Equal(9 * 17, hundred.Y - one.Y, 9);

        Assert.Empty(((VectorGraphic)BlankformsPress.HundredChart(labeled: false).Nodes[0]).Primitives.OfType<TextLabel>());
        Assert.Throws<ArgumentException>(() => BlankformsPress.HundredChart(start: 901));
        Assert.Throws<ArgumentException>(() => BlankformsPress.HundredChart(cellMm: 30));
    }

    [Fact]
    public void Dot_paper_places_every_dot_on_the_exact_lattice()
    {
        var graphic = (VectorGraphic)BlankformsPress.DotPaper(pitchMm: 10).Nodes[0];
        var dots = graphic.Primitives.OfType<CircleShape>().ToList();

        // Letter, 12 mm margins: 20 columns (12..202) by 26 rows (12..262).
        Assert.Equal(20 * 26, dots.Count);
        Assert.All(dots, d =>
        {
            Assert.True(d.Filled);
            Assert.Equal(0, (d.CenterX - 12) % 10, 9);
            Assert.Equal(0, (d.CenterY - 12) % 10, 9);
        });
    }

    [Fact]
    public void Isometric_rows_sit_at_the_triangular_lattice_height_with_alternating_offsets()
    {
        var graphic = (VectorGraphic)BlankformsPress.IsometricDotPaper(pitchMm: 10).Nodes[0];
        var rows = graphic.Primitives.OfType<CircleShape>()
            .GroupBy(d => d.CenterY)
            .OrderBy(g => g.Key)
            .ToList();

        var rowHeight = 10 * Math.Sqrt(3) / 2;
        for (var r = 1; r < rows.Count; r++)
        {
            Assert.Equal(rowHeight, rows[r].Key - rows[r - 1].Key, 9);
        }

        Assert.Equal(12, rows[0].Min(d => d.CenterX), 9);
        Assert.Equal(17, rows[1].Min(d => d.CenterX), 9); // odd rows shift half a pitch
        Assert.Equal(rows[0].Count(), rows[1].Count() + 1);
    }

    [Fact]
    public void The_first_quadrant_grid_puts_its_axes_on_the_lower_left_edges()
    {
        var graphic = (VectorGraphic)BlankformsPress.CoordinateGrid(quadrants: GridQuadrants.First).Nodes[0];
        var axes = graphic.Primitives.OfType<LineSeg>().Where(l => l.StrokeWidthMm == 0.7).ToList();

        Assert.Equal(2, axes.Count);
        var xAxis = Assert.Single(axes, l => l.Y1 == l.Y2);
        var yAxis = Assert.Single(axes, l => l.X1 == l.X2);
        Assert.Equal(graphic.HeightMm - 12, xAxis.Y1, 9);
        Assert.Equal(12, yAxis.X1, 9);
        Assert.Contains("First-quadrant", graphic.Description, StringComparison.Ordinal);

        // The four-quadrant default is unchanged: axes cross at the page center.
        var four = (VectorGraphic)BlankformsPress.CoordinateGrid().Nodes[0];
        Assert.Contains(four.Primitives.OfType<LineSeg>(), l => l.StrokeWidthMm == 0.7 && Math.Abs(l.Y1 - four.HeightMm / 2) < 1e-9);
    }
}

public class SecondWaveManipulativeTests
{
    [Fact]
    public void Fraction_circle_sectors_are_exact_arcs_with_radii_landing_on_the_circle()
    {
        var graphic = (VectorGraphic)ManipulativeMint.FractionCircles([3, 4]).Nodes[0];

        Assert.Equal(3, graphic.Primitives.OfType<CircleShape>().Count()); // whole + two
        Assert.Equal(3 + 4, graphic.Primitives.OfType<LineSeg>().Count());

        var circles = graphic.Primitives.OfType<CircleShape>().ToList();
        foreach (var line in graphic.Primitives.OfType<LineSeg>())
        {
            var circle = circles.Single(c => Math.Abs(c.CenterX - line.X1) < 1e-9 && Math.Abs(c.CenterY - line.Y1) < 1e-9);
            var length = Math.Sqrt(Math.Pow(line.X2 - line.X1, 2) + Math.Pow(line.Y2 - line.Y1, 2));
            Assert.Equal(circle.RadiusMm, length, 9);
        }

        Assert.Equal(3, graphic.Primitives.OfType<TextLabel>().Count(l => l.Text == "1/3"));
        Assert.Single(graphic.Primitives.OfType<TextLabel>(), l => l.Text == "1");

        var unlabeled = (VectorGraphic)ManipulativeMint.FractionCircles([2], labeled: false).Nodes[0];
        Assert.Empty(unlabeled.Primitives.OfType<TextLabel>());
        Assert.Throws<ArgumentException>(() => ManipulativeMint.FractionCircles([17]));
        Assert.Throws<ArgumentException>(() => ManipulativeMint.FractionCircles([2, 3, 4, 5, 6, 7, 8, 9, 10]));
    }

    [Fact]
    public void Spinner_sectors_divide_the_circle_equally_with_labels_at_mid_sector()
    {
        var graphic = (VectorGraphic)ManipulativeMint.SpinnerFace(4, ["1", "2", "3", "4"]).Nodes[0];
        var face = graphic.Primitives.OfType<CircleShape>().Single(c => !c.Filled);
        var radii = graphic.Primitives.OfType<LineSeg>().ToList();

        Assert.Equal(4, radii.Count);
        Assert.All(radii, l =>
        {
            var length = Math.Sqrt(Math.Pow(l.X2 - l.X1, 2) + Math.Pow(l.Y2 - l.Y1, 2));
            Assert.Equal(face.RadiusMm, length, 9);
        });

        // Adjacent boundary angles are exactly a quarter turn apart.
        var angles = radii.Select(l => Math.Atan2(l.Y2 - l.Y1, l.X2 - l.X1)).OrderBy(a => a).ToList();
        for (var i = 1; i < angles.Count; i++)
        {
            Assert.Equal(Math.PI / 2, angles[i] - angles[i - 1], 9);
        }

        var labels = graphic.Primitives.OfType<TextLabel>().Where(l => l.Text.Length == 1).ToList();
        Assert.Equal(4, labels.Count);
        Assert.All(labels, l =>
        {
            var distance = Math.Sqrt(Math.Pow(l.X - face.CenterX, 2) + Math.Pow(l.Y - 2 - face.CenterY, 2));
            Assert.Equal(0.65 * face.RadiusMm, distance, 9);
        });

        Assert.Throws<ArgumentException>(() => ManipulativeMint.SpinnerFace(1));
        Assert.Throws<ArgumentException>(() => ManipulativeMint.SpinnerFace(3, ["only", "two"]));
    }

    [Fact]
    public void The_box_net_unfolds_six_faces_with_matching_areas_and_dashed_folds()
    {
        var graphic = (VectorGraphic)ManipulativeMint.BoxNet(55, 35, 30).Nodes[0];
        var faces = graphic.Primitives.OfType<RectShape>().ToList();

        Assert.Equal(6, faces.Count);
        Assert.Equal(2, faces.Count(f => f.WidthMm == 55 && f.HeightMm == 30)); // front and back
        Assert.Equal(2, faces.Count(f => f.WidthMm == 35 && f.HeightMm == 30)); // sides
        Assert.Equal(2, faces.Count(f => f.WidthMm == 55 && f.HeightMm == 35)); // top and bottom
        // Five shared edges fold; the sixth dashed line is the legend's sample.
        Assert.Equal(5, graphic.Primitives.OfType<LineSeg>().Count(l => l.Dashed && l.StrokeWidthMm == 0.35));
        Assert.Contains(graphic.Primitives.OfType<TextLabel>(), l => l.Text == "fold");

        Assert.Throws<ArgumentException>(() => ManipulativeMint.BoxNet(120, 60, 60)); // cannot fit the page
        Assert.Throws<ArgumentException>(() => ManipulativeMint.BoxNet(0, 35, 30));
    }
}

public class PuzzlePressTests
{
    private static readonly IReadOnlyList<string> Entries =
        [.. Enumerable.Range(1, 30).Select(i => $"entry {i}")];

    [Fact]
    public void The_same_seed_reproduces_the_same_bingo_cards_and_seeds_differ()
    {
        var first = System.Text.Json.JsonSerializer.Serialize(PuzzlePress.BingoBoards(Entries, cards: 3, seed: 7));
        var second = System.Text.Json.JsonSerializer.Serialize(PuzzlePress.BingoBoards(Entries, cards: 3, seed: 7));
        var other = System.Text.Json.JsonSerializer.Serialize(PuzzlePress.BingoBoards(Entries, cards: 3, seed: 8));

        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
    }

    [Fact]
    public void Each_bingo_card_draws_distinct_entries_with_a_starred_free_center()
    {
        var document = PuzzlePress.BingoBoards(Entries, cards: 2, seed: 42);
        var cards = document.Nodes.OfType<VectorGraphic>().ToList();

        Assert.Equal(2, cards.Count);
        foreach (var card in cards)
        {
            Assert.Equal(25, card.Primitives.OfType<RectShape>().Count());

            var texts = card.Primitives.OfType<TextLabel>().Where(l => l.Text.StartsWith("entry", StringComparison.Ordinal)).ToList();
            Assert.Equal(24, texts.Count);
            Assert.Equal(24, texts.Select(t => t.Text).Distinct().Count());
            // The free center is a DRAWN five-chord star (geometry, not a
            // glyph, so it survives the native PDF press's type case).
            Assert.Equal(5, card.Primitives.OfType<LineSeg>().Count(l => l.StrokeWidthMm == 0.7));
            Assert.Contains(card.Primitives.OfType<TextLabel>(), l => l.Text.Contains("seed 42", StringComparison.Ordinal));
        }

        // Two cards from one run are (deterministically) different draws.
        Assert.NotEqual(
            cards[0].Primitives.OfType<TextLabel>().Select(l => l.Text).ToList(),
            [.. cards[1].Primitives.OfType<TextLabel>().Select(l => l.Text)]);
    }

    [Fact]
    public void Bingo_refuses_short_blank_or_duplicate_lists()
    {
        Assert.Throws<ArgumentException>(() => PuzzlePress.BingoBoards([.. Entries.Take(20)], 1, 1));
        Assert.Throws<ArgumentException>(() => PuzzlePress.BingoBoards([.. Entries.Take(23), " "], 1, 1));
        Assert.Throws<ArgumentException>(() => PuzzlePress.BingoBoards([.. Entries.Take(24), "entry 1"], 1, 1));
        Assert.Throws<ArgumentException>(() => PuzzlePress.BingoBoards(Entries, 0, 1));

        // Without the free center the list must cover all twenty-five cells.
        var full = (VectorGraphic)PuzzlePress.BingoBoards([.. Entries.Take(25)], 1, 1, freeCenter: false).Nodes[0];
        Assert.Equal(25, full.Primitives.OfType<TextLabel>().Count(l => l.Text.StartsWith("entry", StringComparison.Ordinal)));
    }

    [Fact]
    public void Every_hidden_word_is_actually_findable_in_the_printed_grid()
    {
        string[] words = ["fraction", "decimal", "percent", "ratio", "graph", "sum"];
        var document = PuzzlePress.WordSearch(words, seed: 20260829, gridSize: 12);
        var puzzle = (VectorGraphic)document.Nodes[0];

        var grid = GridFromLabels(puzzle, 12);
        foreach (var word in words)
        {
            Assert.True(Found(grid, word.ToUpperInvariant()), $"'{word}' is not findable in the grid");
        }

        // The word bank keeps the teacher's order, verbatim and uppercase.
        var bank = puzzle.Primitives.OfType<TextLabel>().Where(l => l.Anchor == TextAnchor.Start).Select(l => l.Text).ToList();
        Assert.Equal(words.Select(w => w.ToUpperInvariant()), bank);
    }

    [Fact]
    public void The_answer_key_carries_only_the_hidden_words_letters()
    {
        var document = PuzzlePress.WordSearch(["cat", "dog"], seed: 5, gridSize: 8);

        Assert.Contains(document.Nodes.OfType<TeacherOnlyNotice>(), n => n.Text.Contains("Answer key", StringComparison.Ordinal));
        var puzzle = (VectorGraphic)document.Nodes[0];
        var key = document.Nodes.OfType<VectorGraphic>().Last();

        Assert.Equal(64, puzzle.Primitives.OfType<TextLabel>().Count(l => l.Text.Length == 1));
        Assert.Equal(6, key.Primitives.OfType<TextLabel>().Count(l => l.Text.Length == 1));

        var plain = PuzzlePress.WordSearch(["cat", "dog"], seed: 5, gridSize: 8, includeAnswerKey: false);
        Assert.Single(plain.Nodes);
    }

    [Fact]
    public void Word_searches_are_seed_deterministic_and_refuse_bad_words()
    {
        string[] words = ["river", "delta", "basin"];
        Assert.Equal(
            System.Text.Json.JsonSerializer.Serialize(PuzzlePress.WordSearch(words, seed: 3)),
            System.Text.Json.JsonSerializer.Serialize(PuzzlePress.WordSearch(words, seed: 3)));

        Assert.Throws<ArgumentException>(() => PuzzlePress.WordSearch([], 1));
        Assert.Throws<ArgumentException>(() => PuzzlePress.WordSearch(["a"], 1));
        Assert.Throws<ArgumentException>(() => PuzzlePress.WordSearch(["two words"], 1));
        Assert.Throws<ArgumentException>(() => PuzzlePress.WordSearch(["thirteenletter"], 1, gridSize: 12));
        Assert.Throws<ArgumentException>(() => PuzzlePress.WordSearch(["ok"], 1, gridSize: 5));
    }

    [Fact]
    public void All_new_press_documents_validate_and_the_recipe_book_holds_twenty_three()
    {
        foreach (var document in new[]
        {
            CalibrationPress.ProofPage(),
            BlankformsPress.HundredChart(),
            BlankformsPress.DotPaper(),
            BlankformsPress.IsometricDotPaper(),
            BlankformsPress.CoordinateGrid(quadrants: GridQuadrants.First),
            ManipulativeMint.FractionCircles([2, 3]),
            ManipulativeMint.SpinnerFace(6),
            ManipulativeMint.BoxNet(),
            PuzzlePress.BingoBoards(Entries, 1, 9),
            PuzzlePress.WordSearch(["water", "cycle"], 9),
        })
        {
            Assert.False(DocumentValidator.HasBlockingIssues(DocumentValidator.Validate(document)));
        }

        Assert.Equal(23, DeterministicPressRecipes.All.Count);
        Assert.Contains(DeterministicPressRecipes.Puzzles, DeterministicPressRecipes.All);
        Assert.Contains(DeterministicPressRecipes.Grouping, DeterministicPressRecipes.All);
    }

    private static char[,] GridFromLabels(VectorGraphic puzzle, int gridSize)
    {
        var letters = puzzle.Primitives.OfType<TextLabel>()
            .Where(l => l.Text.Length == 1 && l.Anchor == TextAnchor.Middle)
            .OrderBy(l => l.Y).ThenBy(l => l.X)
            .ToList();
        Assert.Equal(gridSize * gridSize, letters.Count);

        var grid = new char[gridSize, gridSize];
        for (var i = 0; i < letters.Count; i++)
        {
            grid[i / gridSize, i % gridSize] = letters[i].Text[0];
        }

        return grid;
    }

    private static bool Found(char[,] grid, string word)
    {
        var size = grid.GetLength(0);
        (int Dr, int Dc)[] directions = [(1, 0), (0, 1), (1, 1), (-1, 1), (-1, 0), (0, -1), (-1, -1), (1, -1)];

        for (var row = 0; row < size; row++)
        {
            for (var col = 0; col < size; col++)
            {
                foreach (var (dr, dc) in directions)
                {
                    var i = 0;
                    while (i < word.Length)
                    {
                        var r = row + dr * i;
                        var c = col + dc * i;
                        if (r < 0 || r >= size || c < 0 || c >= size || grid[r, c] != word[i])
                        {
                            break;
                        }

                        i++;
                    }

                    if (i == word.Length)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}
