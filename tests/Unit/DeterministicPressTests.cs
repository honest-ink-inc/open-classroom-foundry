using Foundry.Domain;
using Foundry.Modules.DeterministicPress;

namespace Foundry.Tests.Unit;

public class BlankformsPressTests
{
    [Fact]
    public void Graph_paper_lines_sit_at_exact_pitch_multiples_inside_the_margins()
    {
        var document = BlankformsPress.GraphPaper(PageSize.Letter, pitchMm: 5, marginMm: 12);
        var graphic = Assert.IsType<VectorGraphic>(Assert.Single(document.Nodes));

        Assert.Equal(215.9, graphic.WidthMm);
        Assert.Equal(279.4, graphic.HeightMm);

        var verticals = graphic.Primitives.OfType<LineSeg>().Where(l => l.X1 == l.X2).ToList();
        var horizontals = graphic.Primitives.OfType<LineSeg>().Where(l => l.Y1 == l.Y2).ToList();

        // Printable width 191.9mm -> lines at 12 + 5k up to 202: 39 verticals.
        Assert.Equal(39, verticals.Count);
        Assert.All(verticals, l =>
        {
            Assert.Equal(0, (l.X1 - 12) % 5, 6);
            Assert.InRange(l.X1, 12, 215.9 - 12);
        });
        Assert.All(horizontals, l => Assert.Equal(0, (l.Y1 - 12) % 5, 6));

        // Every fifth line is the major weight.
        Assert.Equal(8, verticals.Count(l => l.StrokeWidthMm == 0.5));
    }

    [Fact]
    public void Identical_parameters_produce_identical_geometry()
    {
        // Record equality is reference-based for collections; serialized bytes are
        // the determinism claim that matters (spec: byte-identical for identical parameters).
        Assert.Equal(
            System.Text.Json.JsonSerializer.Serialize(BlankformsPress.GraphPaper(PageSize.A4, 4, 10)),
            System.Text.Json.JsonSerializer.Serialize(BlankformsPress.GraphPaper(PageSize.A4, 4, 10)));
    }

    [Fact]
    public void The_number_line_places_every_numeral_at_its_exact_tick()
    {
        var document = BlankformsPress.NumberLine(0, 10);
        var graphic = Assert.IsType<VectorGraphic>(Assert.Single(document.Nodes));

        var labels = graphic.Primitives.OfType<TextLabel>().ToList();
        Assert.Equal(11, labels.Count);
        Assert.Equal("0", labels[0].Text);
        Assert.Equal("10", labels[10].Text);

        var spacing = labels[1].X - labels[0].X;
        for (var i = 1; i < labels.Count; i++)
        {
            Assert.Equal(spacing, labels[i].X - labels[i - 1].X, 6);
        }
    }

    [Fact]
    public void Ten_frames_are_exactly_ten_cells_each()
    {
        var document = BlankformsPress.TenFrames(frames: 3, cellMm: 20);
        var graphic = Assert.IsType<VectorGraphic>(Assert.Single(document.Nodes));

        var cells = graphic.Primitives.OfType<RectShape>().ToList();
        Assert.Equal(30, cells.Count);
        Assert.All(cells, c =>
        {
            Assert.Equal(20, c.WidthMm);
            Assert.Equal(20, c.HeightMm);
        });
    }

    [Fact]
    public void The_clock_face_has_hour_marks_minute_ticks_and_optional_numerals()
    {
        var full = (VectorGraphic)BlankformsPress.ClockFace().Nodes[0];
        var plain = (VectorGraphic)BlankformsPress.ClockFace(numerals: false, minuteTicks: false).Nodes[0];

        Assert.Equal(12, full.Primitives.OfType<TextLabel>().Count());
        Assert.Equal(60, full.Primitives.OfType<LineSeg>().Count()); // 12 hour + 48 minute (RC-15)
        Assert.Equal(12, plain.Primitives.OfType<LineSeg>().Count());
        Assert.Empty(plain.Primitives.OfType<TextLabel>());
    }

    [Fact]
    public void Number_line_subdivisions_place_minor_ticks_exactly_between_integers()
    {
        var graphic = (VectorGraphic)BlankformsPress.NumberLine(0, 4, subdivisions: 4).Nodes[0];

        var ticks = graphic.Primitives.OfType<LineSeg>().Where(l => l.X1 == l.X2).ToList();
        Assert.Equal(5, ticks.Count(t => Math.Abs(t.Y2 - t.Y1 - 8) < 1e-9));   // major, 8mm tall
        Assert.Equal(12, ticks.Count(t => Math.Abs(t.Y2 - t.Y1 - 5) < 1e-9));  // 3 minors × 4 units, 5mm tall
        Assert.Equal(5, graphic.Primitives.OfType<TextLabel>().Count());        // integers only labeled
    }

    [Fact]
    public void Staves_are_five_lines_each_and_forms_validate_cleanly()
    {
        var staves = (VectorGraphic)BlankformsPress.MusicStaves(4).Nodes[0];
        Assert.Equal(4 * 5, staves.Primitives.OfType<LineSeg>().Count(l => l.Y1 == l.Y2));

        foreach (var document in new[]
        {
            BlankformsPress.GraphPaper(), BlankformsPress.CoordinateGrid(), BlankformsPress.NumberLine(),
            BlankformsPress.TenFrames(), BlankformsPress.ClockFace(), BlankformsPress.MusicStaves(),
        })
        {
            Assert.False(DocumentValidator.HasBlockingIssues(DocumentValidator.Validate(document)));
        }
    }
}

public class FlashcardFlywheelTests
{
    private static IReadOnlyList<FlashcardPair> Pairs(int count)
        => [.. Enumerable.Range(1, count).Select(i => new FlashcardPair($"term {i}", $"answer {i}"))];

    [Fact]
    public void Sheets_alternate_front_back_and_ten_pairs_take_two_sheets()
    {
        var result = FlashcardFlywheel.Build(Pairs(10));
        var graphics = result.Document.Nodes.OfType<VectorGraphic>().ToList();

        Assert.Equal(4, graphics.Count);
        Assert.Contains("front", graphics[0].Description, StringComparison.Ordinal);
        Assert.Contains("back", graphics[1].Description, StringComparison.Ordinal);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Every_answer_lands_exactly_behind_its_term_after_a_long_edge_flip()
    {
        var result = FlashcardFlywheel.Build(Pairs(8));
        var graphics = result.Document.Nodes.OfType<VectorGraphic>().ToList();
        var front = graphics[0];
        var back = graphics[1];

        for (var i = 0; i < 8; i++)
        {
            var term = front.Primitives.OfType<TextLabel>().Single(l => l.Text == $"term {i + 1}");
            var answer = back.Primitives.OfType<TextLabel>().Single(l => l.Text == $"answer {i + 1}");

            // A long-edge duplex flip mirrors x about the page center; y is unchanged.
            Assert.Equal(front.WidthMm - term.X, answer.X, 6);
            Assert.Equal(term.Y, answer.Y, 6);
        }
    }

    [Fact]
    public void Overflow_is_flagged_and_the_text_is_kept_whole()
    {
        var longAnswer = new string('x', 90);
        var result = FlashcardFlywheel.Build([new FlashcardPair("short", longAnswer)]);

        Assert.Contains(result.Issues, i => i.Code == "flashcard.overflow" && i.Severity == ValidationSeverity.Warning);
        Assert.Contains(
            result.Document.Nodes.OfType<VectorGraphic>().SelectMany(g => g.Primitives.OfType<TextLabel>()),
            l => l.Text == longAnswer);
    }

    [Fact]
    public void Blank_sides_are_refused_outright()
    {
        Assert.Throws<ArgumentException>(() => FlashcardFlywheel.Build([new FlashcardPair("term", "  ")]));
        Assert.Throws<ArgumentException>(() => FlashcardFlywheel.Build([]));
    }
}

public class BookletImpositionTests
{
    [Fact]
    public void Every_page_count_from_one_to_sixty_four_folds_into_perfect_reading_order()
    {
        for (var contentPages = 1; contentPages <= 64; contentPages++)
        {
            var plan = BookletImposition.Compute(contentPages);

            Assert.Equal(0, plan.TotalPages % 4);
            Assert.Equal(plan.TotalPages, plan.ContentPages + plan.BlankPagesAdded);
            Assert.InRange(plan.BlankPagesAdded, 0, 3);

            var allPages = plan.Sheets
                .SelectMany(s => new[] { s.FrontLeft, s.FrontRight, s.BackLeft, s.BackRight })
                .OrderBy(p => p)
                .ToList();
            Assert.Equal(Enumerable.Range(1, plan.TotalPages), allPages);

            Assert.Equal(
                Enumerable.Range(1, plan.TotalPages),
                BookletImposition.FoldedReadingOrder(plan));
        }
    }

    [Fact]
    public void The_classic_eight_page_signature_matches_the_printers_rule_of_thumb()
    {
        var plan = BookletImposition.Compute(8);

        Assert.Collection(
            plan.Sheets,
            s => Assert.Equal((8, 1, 2, 7), (s.FrontLeft, s.FrontRight, s.BackLeft, s.BackRight)),
            s => Assert.Equal((6, 3, 4, 5), (s.FrontLeft, s.FrontRight, s.BackLeft, s.BackRight)));
    }

    [Fact]
    public void The_guide_marks_padding_blanks_explicitly()
    {
        var guide = BookletImposition.Guide(BookletImposition.Compute(6));
        var table = Assert.Single(guide.Nodes.OfType<TableNode>());

        Assert.Contains(table.Rows, row => row.Contains("blank"));
        Assert.False(DocumentValidator.HasBlockingIssues(DocumentValidator.Validate(guide)));
    }

    [Fact]
    public void The_press_recipes_are_green_parameter_only_and_provider_free()
    {
        Assert.All(DeterministicPressRecipes.All, recipe =>
        {
            Assert.Equal(DataLane.Green, recipe.MaximumLane);
            Assert.Empty(recipe.RequiredProviderCapabilities);
            Assert.Contains(recipe.ProhibitedPurposes, p => p.Contains("never prose", StringComparison.Ordinal));
        });
    }
}
