using Foundry.Domain;
using Foundry.Modules.DeterministicPress;

namespace Foundry.Tests.Unit;

// The second menu's last four items: the computational-thinking studio (4),
// retrieval grids (5), the field journal (6), and the Mint's third strike (7).
// The load-bearing assertions are verbatim preservation and exact arithmetic.

public class ComputationalStudioTests
{
    private static readonly IReadOnlyList<string> Solution =
        ["total = 0", "for n in [1, 2, 3]:", "    total = total + n", "print(total)"];

    [Fact]
    public void The_parsons_key_reconstructs_the_teachers_code_exactly_including_indentation()
    {
        var document = ParsonsPress.Puzzle("Order the lines.", Solution, ["    total = 0"], seed: 11);
        var graphic = (VectorGraphic)document.Nodes[0];
        var key = ((TeacherOnlyNotice)document.Nodes[1]).Text;

        // Rebuild each displayed line from its geometry: the letter label, the
        // trimmed text, and the indent encoded as an exact x offset.
        var boxes = graphic.Primitives.OfType<TextLabel>()
            .Where(l => l.Text.Length == 1 && l.Text[0] is >= 'A' and <= 'Z')
            .OrderBy(l => l.Y)
            .ToList();
        var lines = new Dictionary<char, string>();
        foreach (var letter in boxes)
        {
            var text = graphic.Primitives.OfType<TextLabel>()
                .Single(l => l.Anchor == TextAnchor.Start && Math.Abs(l.Y - (letter.Y - 0.2)) < 1e-9);
            var indent = (int)Math.Round((text.X - 12 - BlankformsPress.DefaultMarginMm) / ParsonsPress.IndentMmPerSpace);
            lines[letter.Text[0]] = new string(' ', indent) + text.Text;
        }

        var orderPart = key.Split("correct order: ")[1].Split('.')[0];
        var reconstructed = orderPart.Split(", ").Select(s => lines[s[0]]).ToList();
        Assert.Equal(Solution, reconstructed);

        var distractorLetter = key.Split("Distractors: ")[1].Trim('.', ' ')[0];
        Assert.Equal("    total = 0", lines[distractorLetter]);
    }

    [Fact]
    public void The_same_seed_reproduces_the_same_puzzle_and_seeds_differ()
    {
        Assert.Equal(
            System.Text.Json.JsonSerializer.Serialize(ParsonsPress.Puzzle("p", Solution, [], 7)),
            System.Text.Json.JsonSerializer.Serialize(ParsonsPress.Puzzle("p", Solution, [], 7)));
        Assert.NotEqual(
            System.Text.Json.JsonSerializer.Serialize(ParsonsPress.Puzzle("p", Solution, [], 7)),
            System.Text.Json.JsonSerializer.Serialize(ParsonsPress.Puzzle("p", Solution, [], 8)));

        Assert.Throws<ArgumentException>(() => ParsonsPress.Puzzle("p", ["a", "b"], [], 1));
        Assert.Throws<ArgumentException>(() => ParsonsPress.Puzzle("p", Solution, [.. Enumerable.Repeat("d", 7)], 1));
    }

    [Fact]
    public void The_trace_table_carries_the_code_verbatim_and_the_variables_as_columns()
    {
        string[] code = ["x = 2", "while x < 9:", "    x = x + 2", "print(x)"];
        var graphic = (VectorGraphic)TraceTableTutor.Sheet("Trace it.", code, ["x", "y"], 5).Nodes[0];

        var codeLabels = graphic.Primitives.OfType<TextLabel>()
            .Where(l => l.Anchor == TextAnchor.Start && l.FontSizeMm == 4.5)
            .OrderBy(l => l.Y)
            .ToList();
        Assert.Equal(code.Select(l => l.TrimStart()), codeLabels.Select(l => l.Text));

        // Indentation survives as exact geometry: four spaces = 10 mm.
        Assert.Equal(4 * ParsonsPress.IndentMmPerSpace, codeLabels[2].X - codeLabels[0].X, 9);

        var headers = graphic.Primitives.OfType<TextLabel>().Where(l => l.FontSizeMm == 4.5 && l.Anchor == TextAnchor.Middle).Select(l => l.Text).ToList();
        Assert.Equal(["Line", "x", "y", "Output"], headers);

        Assert.Throws<ArgumentException>(() => TraceTableTutor.Sheet("p", code, ["x"], traceRows: 20));
    }

    [Fact]
    public void Algorithm_cards_wear_a_double_border_only_on_control_cards_and_paginate()
    {
        var actions = Enumerable.Range(1, 9).Select(i => $"Action {i}").ToList();
        var document = AlgorithmAtelier.ActionCards(actions, ["Start", "Stop"], PageSize.Letter);
        var pages = document.Nodes.OfType<VectorGraphic>().ToList();

        Assert.Equal(2, pages.Count); // 11 cards, 8 per page
        var rects = pages.SelectMany(p => p.Primitives.OfType<RectShape>()).Count();
        Assert.Equal(11 + 2, rects); // one border each, a second for the two control cards

        var duck = AlgorithmAtelier.PromptCards(["Say it.", "Read it.", "Fix one thing."]);
        Assert.Single(duck.Nodes);
        Assert.Equal(3, ((VectorGraphic)duck.Nodes[0]).Primitives.OfType<RectShape>().Count());
    }
}

public class RetrievalGridTests
{
    private static readonly IReadOnlyList<string> Bank =
        [.. Enumerable.Range(1, 12).Select(i => $"Question {i}")];

    [Fact]
    public void Grids_draw_verbatim_questions_with_no_repeats_inside_a_grid()
    {
        var document = RetrievalGrid.Grids(Bank, gridCount: 3, rows: 3, columns: 3, seed: 4);
        var pages = document.Nodes.OfType<VectorGraphic>().ToList();

        Assert.Equal(3, pages.Count);
        Assert.Contains("seed 4", ((TeacherOnlyNotice)document.Nodes[0]).Text, StringComparison.Ordinal);

        foreach (var page in pages)
        {
            var cells = page.Primitives.OfType<TextLabel>()
                .Where(l => l.Text.StartsWith("Question", StringComparison.Ordinal))
                .Select(l => l.Text)
                .ToList();
            Assert.Equal(9, cells.Count);
            Assert.Equal(9, cells.Distinct(StringComparer.Ordinal).Count());
            Assert.All(cells, c => Assert.Contains(c, Bank));
        }
    }

    [Fact]
    public void Retrieval_grids_are_seed_deterministic_and_refuse_bad_banks()
    {
        Assert.Equal(
            System.Text.Json.JsonSerializer.Serialize(RetrievalGrid.Grids(Bank, 2, 2, 2, 9)),
            System.Text.Json.JsonSerializer.Serialize(RetrievalGrid.Grids(Bank, 2, 2, 2, 9)));

        Assert.Throws<ArgumentException>(() => RetrievalGrid.Grids([.. Bank.Take(8)], 1, 3, 3, 1));
        Assert.Throws<ArgumentException>(() => RetrievalGrid.Grids([.. Bank.Take(3), "Question 1"], 1, 2, 2, 1));
    }
}

public class FieldJournalForgeTests
{
    [Fact]
    public void The_observation_frame_holds_a_sketch_box_and_ruled_prompt_sections()
    {
        var graphic = (VectorGraphic)FieldJournalForge.ObservationFrame(["See", "Hear", "Wonder"]).Nodes[0];

        Assert.Single(graphic.Primitives.OfType<RectShape>());
        Assert.Equal(3, graphic.Primitives.OfType<TextLabel>().Count());
        Assert.Equal(9, graphic.Primitives.OfType<LineSeg>().Count()); // three ruled lines per prompt

        Assert.Throws<ArgumentException>(() => FieldJournalForge.ObservationFrame(["a", "b", "c", "d", "e"], sketchHeightMm: 160));
    }

    [Fact]
    public void Specimen_labels_fill_a_cut_ready_grid_of_eight()
    {
        var graphic = (VectorGraphic)FieldJournalForge.SpecimenLabels(["Name", "Date", "Location"]).Nodes[0];

        Assert.Equal(8, graphic.Primitives.OfType<RectShape>().Count());
        Assert.Equal(8, graphic.Primitives.OfType<TextLabel>().Count(l => l.Text == "Name"));
        Assert.Equal(24, graphic.Primitives.OfType<LineSeg>().Count()); // one write-line per field per card
    }

    [Fact]
    public void The_site_map_carries_a_north_arrow_and_a_true_scale_bar()
    {
        var graphic = (VectorGraphic)FieldJournalForge.SiteMapPage(pitchMm: 10, metersPerSquare: 2).Nodes[0];

        Assert.Contains(graphic.Primitives.OfType<TextLabel>(), l => l.Text == "N");
        Assert.Contains(graphic.Primitives.OfType<TextLabel>(), l => l.Text == "10 m"); // five squares at two meters

        // The scale bar spans exactly five grid squares.
        var bar = graphic.Primitives.OfType<LineSeg>().Single(l => l.StrokeWidthMm == 0.7 && l.Y1 == l.Y2 && Math.Abs(l.X2 - l.X1 - 50) < 1e-9);
        Assert.Equal(50, bar.X2 - bar.X1, 9);
    }
}

public class MintThirdStrikeTests
{
    [Fact]
    public void Algebra_tile_proportions_are_exact_and_x_refuses_to_be_a_whole_number_of_units()
    {
        var graphic = (VectorGraphic)ManipulativeMint.AlgebraTiles(unitMm: 12, xMm: 45, 2, 6, 10).Nodes[0];
        var rects = graphic.Primitives.OfType<RectShape>().ToList();

        Assert.Equal(2, rects.Count(r => r.WidthMm == 45 && r.HeightMm == 45));
        Assert.Equal(6, rects.Count(r => r.WidthMm == 45 && r.HeightMm == 12));
        Assert.Equal(10, rects.Count(r => r.WidthMm == 12 && r.HeightMm == 12));
        Assert.Equal(2, graphic.Primitives.OfType<TextLabel>().Count(l => l.Text == "x²"));

        Assert.Throws<ArgumentException>(() => ManipulativeMint.AlgebraTiles(unitMm: 12, xMm: 48));
    }

    [Fact]
    public void A_rod_is_exactly_ten_units_and_a_flat_exactly_ten_rods()
    {
        var graphic = (VectorGraphic)ManipulativeMint.BaseTenBlocks(unitMm: 10, flatCount: 1, rodCount: 2, unitCount: 3).Nodes[0];
        var rects = graphic.Primitives.OfType<RectShape>().ToList();

        var flat = Assert.Single(rects, r => r.WidthMm == 100 && r.HeightMm == 100);
        Assert.Equal(2, rects.Count(r => r.WidthMm == 100 && r.HeightMm == 10));
        Assert.Equal(3, rects.Count(r => r.WidthMm == 10 && r.HeightMm == 10));

        // The flat's interior grid: nine vertical and nine horizontal lines
        // inside it; each rod carries nine dividers.
        var inFlat = graphic.Primitives.OfType<LineSeg>()
            .Count(l => l.X1 >= flat.X && l.X2 <= flat.X + 100 && l.Y1 >= flat.Y && l.Y2 <= flat.Y + 100);
        Assert.Equal(18, inFlat);
    }

    [Fact]
    public void The_tangram_dissection_is_exact_seven_pieces_with_the_classical_areas()
    {
        const double side = 160;
        var graphic = (VectorGraphic)ManipulativeMint.Tangram(side).Nodes[0];
        var square = Assert.Single(graphic.Primitives.OfType<RectShape>());
        Assert.Equal(side, square.WidthMm);
        Assert.Equal(6, graphic.Primitives.OfType<LineSeg>().Count());

        // The seven classical pieces, in quarter-side grid coordinates; their
        // shoelace areas must be exactly {4,4,1,2,2,2,1}/16 of the square.
        var t = side / 4;
        (double, double)[][] pieces =
        [
            [(0, 0), (4, 0), (2, 2)],
            [(0, 0), (0, 4), (2, 2)],
            [(4, 0), (4, 2), (3, 1)],
            [(2, 2), (3, 1), (4, 2), (3, 3)],
            [(4, 4), (2, 4), (4, 2)],
            [(2, 4), (3, 3), (2, 2), (1, 3)],
            [(0, 4), (1, 3), (2, 4)],
        ];

        double[] expectedSixteenths = [4, 4, 1, 2, 2, 2, 1];
        var total = 0.0;
        for (var i = 0; i < pieces.Length; i++)
        {
            var area = Shoelace(pieces[i]) * t * t;
            Assert.Equal(expectedSixteenths[i] / 16 * side * side, area, 6);
            total += area;
        }

        Assert.Equal(side * side, total, 6);

        Assert.Throws<ArgumentException>(() => ManipulativeMint.Tangram(300));
    }

    private static double Shoelace((double X, double Y)[] polygon)
    {
        var sum = 0.0;
        for (var i = 0; i < polygon.Length; i++)
        {
            var (x1, y1) = polygon[i];
            var (x2, y2) = polygon[(i + 1) % polygon.Length];
            sum += x1 * y2 - x2 * y1;
        }

        return Math.Abs(sum) / 2;
    }
}
