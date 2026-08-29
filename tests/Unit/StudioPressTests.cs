using Foundry.Domain;
using Foundry.Modules.DeterministicPress;

namespace Foundry.Tests.Unit;

public class StudioPressTests
{
    [Fact]
    public void Fraction_strip_proportions_are_exact_arithmetic()
    {
        var document = ManipulativeMint.FractionStrips([2, 3, 4]);
        var graphic = Assert.IsType<VectorGraphic>(Assert.Single(document.Nodes));
        var cells = graphic.Primitives.OfType<RectShape>().ToList();

        Assert.Equal(1 + 2 + 3 + 4, cells.Count);

        var wholeWidth = cells[0].WidthMm;
        foreach (var denominator in new[] { 2, 3, 4 })
        {
            var row = cells.Where(c => Math.Abs(c.WidthMm - wholeWidth / denominator) < 1e-9).ToList();
            Assert.Equal(denominator, row.Count);
            Assert.Equal(wholeWidth, row.Sum(c => c.WidthMm), 9);
        }

        Assert.Contains(graphic.Primitives.OfType<TextLabel>(), l => l.Text == "1/3");

        // RC-14: the unlabeled variant is where the reasoning lives.
        var unlabeled = (VectorGraphic)ManipulativeMint.FractionStrips([2, 3, 4], labeled: false).Nodes[0];
        Assert.Empty(unlabeled.Primitives.OfType<TextLabel>());
        Assert.Contains("unlabeled", unlabeled.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void The_die_net_has_six_faces_twenty_one_pips_and_dashed_folds()
    {
        var graphic = (VectorGraphic)ManipulativeMint.DiceNet().Nodes[0];

        Assert.Equal(6, graphic.Primitives.OfType<RectShape>().Count());
        Assert.Equal(21, graphic.Primitives.OfType<CircleShape>().Count(c => c.Filled));
        Assert.Equal(5, graphic.Primitives.OfType<LineSeg>().Count(l => l.Dashed));
    }

    [Fact]
    public void The_flap_book_speaks_the_line_language_with_a_legend()
    {
        var graphic = (VectorGraphic)FoldablesFoundry.FlapBook(4, ["Who", "What", "Where", "Why"]).Nodes[0];

        // Three cut lines between four flaps, stopping at the fold.
        var cuts = graphic.Primitives.OfType<LineSeg>().Where(l => !l.Dashed && l.X1 == l.X2).ToList();
        Assert.Equal(3, cuts.Count);
        Assert.All(cuts, c => Assert.Equal(graphic.HeightMm / 2, Math.Max(c.Y1, c.Y2)));

        Assert.Contains(graphic.Primitives.OfType<LineSeg>(), l => l.Dashed && l.Y1 == l.Y2);
        Assert.Contains(graphic.Primitives.OfType<TextLabel>(), l => l.Text == "fold");
        Assert.Contains(graphic.Primitives.OfType<TextLabel>(), l => l.Text == "cut");
        Assert.Contains(graphic.Primitives.OfType<TextLabel>(), l => l.Text == "Why");

        Assert.Throws<ArgumentException>(() => FoldablesFoundry.FlapBook(3, ["only", "two"]));
    }

    [Fact]
    public void Big_print_tiles_translate_geometry_exactly_and_say_so()
    {
        var source = new VectorGraphic(400, 250, [new LineSeg(390, 100, 395, 100)], "A wide banner");

        var document = BigPrintShop.Tile(source, columns: 2, rows: 1, overlapMm: 10);
        var tiles = document.Nodes.OfType<VectorGraphic>().ToList();

        Assert.Equal(2, tiles.Count);

        // Tile 2's window starts at pageWidth - overlap; the line lands translated by that offset.
        var offset = tiles[0].WidthMm - 10;
        var line = tiles[1].Primitives.OfType<LineSeg>().Single(l => Math.Abs(l.X1 - (390 - offset)) < 1e-9);
        Assert.Equal(100, line.Y1);

        Assert.All(tiles, t => Assert.Contains(t.Primitives.OfType<TextLabel>(), l => l.Text.Contains("100%", StringComparison.Ordinal)));
        Assert.Contains("overlap", Assert.IsType<TeacherOnlyNotice>(document.Nodes[0]).Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Handwriting_rows_have_the_dashed_midline_and_recycled_model_words()
    {
        var graphic = (VectorGraphic)HandwritingFoundry.PracticeRows(6, modelWords: ["min", "sun"]).Nodes[0];

        Assert.Equal(6, graphic.Primitives.OfType<LineSeg>().Count(l => l.Dashed));
        Assert.Equal(18, graphic.Primitives.OfType<LineSeg>().Count());
        Assert.Equal(3, graphic.Primitives.OfType<TextLabel>().Count(l => l.Text == "min"));
        Assert.Equal(3, graphic.Primitives.OfType<TextLabel>().Count(l => l.Text == "sun"));
    }

    [Fact]
    public void Label_sheets_split_at_capacity_and_carry_optional_second_lines()
    {
        var labels = Enumerable.Range(1, 12)
            .Select(i => new LabelSpec($"Bin {i}", i % 2 == 0 ? $"Caja {i}" : null))
            .ToList();

        var document = LabelLathe.Sheets(labels);
        var sheets = document.Nodes.OfType<VectorGraphic>().ToList();

        Assert.Equal(2, sheets.Count);
        Assert.Equal(10, sheets[0].Primitives.OfType<RectShape>().Count());
        Assert.Equal(2, sheets[1].Primitives.OfType<RectShape>().Count());
        Assert.Contains(sheets[0].Primitives.OfType<TextLabel>(), l => l.Text == "Caja 2");
    }

    [Fact]
    public void The_new_blankforms_hold_their_shape()
    {
        var calendar = (VectorGraphic)BlankformsPress.MonthCalendar(["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"]).Nodes[0];
        Assert.Equal(35, calendar.Primitives.OfType<RectShape>().Count());
        Assert.Equal(7, calendar.Primitives.OfType<TextLabel>().Count());

        var cornell = (VectorGraphic)BlankformsPress.CornellNotes().Nodes[0];
        Assert.Single(cornell.Primitives.OfType<RectShape>());
        Assert.Equal(2, cornell.Primitives.OfType<LineSeg>().Count());

        var lab = (VectorGraphic)BlankformsPress.LabTable(["Trial", "Mass (g)", "Time (s)"], dataRows: 5).Nodes[0];
        Assert.Equal(3 + 15, lab.Primitives.OfType<RectShape>().Count());
        Assert.Contains(lab.Primitives.OfType<TextLabel>(), l => l.Text == "Mass (g)");
    }

    [Fact]
    public void All_press_documents_validate_and_all_eighteen_recipes_stand()
    {
        foreach (var document in new[]
        {
            ManipulativeMint.FractionStrips([2, 4]), ManipulativeMint.DiceNet(),
            FoldablesFoundry.FlapBook(3), BigPrintShop.Tile(new VectorGraphic(300, 200, [new LineSeg(0, 0, 300, 200)], "Banner"), 2, 1),
            HandwritingFoundry.PracticeRows(), LabelLathe.Sheets([new LabelSpec("Scissors")]),
            BlankformsPress.MonthCalendar(["M", "T", "W", "T", "F", "S", "S"]),
            BlankformsPress.CornellNotes(), BlankformsPress.LabTable(["A", "B"]),
        })
        {
            Assert.False(DocumentValidator.HasBlockingIssues(DocumentValidator.Validate(document)));
        }

        Assert.Equal(18, DeterministicPressRecipes.All.Count);
        Assert.All(DeterministicPressRecipes.All, r =>
        {
            Assert.Equal(DataLane.Green, r.MaximumLane);
            Assert.Empty(r.RequiredProviderCapabilities);
        });
    }
}
