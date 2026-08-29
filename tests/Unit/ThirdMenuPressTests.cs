using Foundry.Domain;
using Foundry.Modules.DeterministicPress;

namespace Foundry.Tests.Unit;

// The third menu's press items (4-8): the math scaffolds, the history presses,
// the learner-held kit, the rubric presses, and the whole-book landscape and
// low-ink variants. Load-bearing assertions: verbatim teacher text, exact
// fade and placement arithmetic, and the pledge printed in ink.

public class MathScaffoldTests
{
    private static readonly IReadOnlyList<string> Steps =
        ["3x + 12 = 27", "3x = 15", "x = 5"];

    [Fact]
    public void The_fade_is_exact_arithmetic_and_the_structure_never_changes()
    {
        var document = WorkedExampleFader.Sheets("Solve: 3(x + 4) = 27", Steps, fadeSheets: 3, "Check it.");
        var pages = document.Nodes.OfType<VectorGraphic>().ToList();

        Assert.Equal(4, pages.Count); // full + three fades

        // keep = n - ceil(n*j/fades): 3, 2, 1, 0 shown; blanks make up the rest.
        int[] expectedShown = [3, 2, 1, 0];
        for (var sheet = 0; sheet < pages.Count; sheet++)
        {
            var shown = pages[sheet].Primitives.OfType<TextLabel>()
                .Count(l => Steps.Contains(l.Text));
            var blanks = pages[sheet].Primitives.OfType<LineSeg>().Count();

            Assert.Equal(expectedShown[sheet], shown);
            Assert.Equal(Steps.Count - expectedShown[sheet], blanks);

            // Structure identical: every sheet numbers all steps and carries the check.
            Assert.Equal(Steps.Count, pages[sheet].Primitives.OfType<TextLabel>().Count(l => l.Text is "1" or "2" or "3"));
            Assert.Contains(pages[sheet].Primitives.OfType<TextLabel>(), l => l.Text == "Check it.");
        }

        // The full sheet shows the steps verbatim, in order.
        var fullSteps = pages[0].Primitives.OfType<TextLabel>()
            .Where(l => Steps.Contains(l.Text)).OrderBy(l => l.Y).Select(l => l.Text);
        Assert.Equal(Steps, fullSteps);

        Assert.Throws<ArgumentException>(() => WorkedExampleFader.Sheets("p", ["one"], 2, "c"));
    }

    [Fact]
    public void Estimation_first_wraps_every_problem_in_the_four_teacher_labeled_sections()
    {
        string[] labels = ["Estimate", "Range", "Exact", "Compare"];
        var document = EstimationFirst.Sheets(["487 + 316", "72 × 9"], labels);
        var graphic = (VectorGraphic)document.Nodes[0];

        Assert.Contains(graphic.Primitives.OfType<TextLabel>(), l => l.Text == "487 + 316");
        foreach (var label in labels)
        {
            Assert.Equal(2, graphic.Primitives.OfType<TextLabel>().Count(l => l.Text == label));
        }

        Assert.Equal(8, graphic.Primitives.OfType<LineSeg>().Count()); // one answer line per section per problem
        Assert.Throws<ArgumentException>(() => EstimationFirst.Sheets(["p"], ["only", "three", "labels"]));
    }
}

public class HistoryPressTests
{
    [Fact]
    public void Timeline_positions_are_proportionally_true_to_the_year()
    {
        var events = TimelineWeaver.Parse(
        [
            ("1950", "Start era"),
            ("1960", "Mid era"),
            ("1970-1975", "Late span"),
        ]);
        var graphic = (VectorGraphic)TimelineWeaver.Sheet(events, 1950, 1980).Nodes[0];

        var axis = graphic.Primitives.OfType<LineSeg>().Single(l => l.StrokeWidthMm == 0.7);
        var span = axis.X2 - axis.X1;

        // 1950 at the origin, 1960 exactly one third along: a decade is the
        // same millimeters everywhere on the line.
        var tick1950 = graphic.Primitives.OfType<LineSeg>().Single(l => l.X1 == l.X2 && Math.Abs(l.X1 - axis.X1) < 1e-9);
        var tick1960 = graphic.Primitives.OfType<LineSeg>().Single(l => l.X1 == l.X2 && Math.Abs(l.X1 - (axis.X1 + span / 3)) < 1e-9);
        Assert.NotNull(tick1950);
        Assert.NotNull(tick1960);

        // The 1970-1975 span rides the axis, heavier, exactly a sixth long.
        var bar = graphic.Primitives.OfType<LineSeg>().Single(l => l.StrokeWidthMm == 1.6);
        Assert.Equal(axis.X1 + span * 20 / 30, bar.X1, 9);
        Assert.Equal(span * 5 / 30, bar.X2 - bar.X1, 9);

        Assert.Contains(graphic.Primitives.OfType<TextLabel>(), l => l.Text == "Late span (1970-1975)");
    }

    [Fact]
    public void Timeline_parsing_and_range_are_refused_loudly_when_wrong()
    {
        Assert.Throws<ArgumentException>(() => TimelineWeaver.Parse([("no-year", "label")]));
        Assert.Throws<ArgumentException>(() => TimelineWeaver.Parse([("1950", null)]));
        Assert.Throws<ArgumentException>(() => TimelineWeaver.Sheet(
            TimelineWeaver.Parse([("1950", "a"), ("1990", "b")]), 1950, 1980));
    }

    [Fact]
    public void The_synthesis_table_crosses_every_claim_with_every_source_and_ends_in_provenance()
    {
        var graphic = (VectorGraphic)SourceSynthesisTable.Sheet(
            ["Claim one", "Claim two", "Claim three"], ["Diary", "Ledger"],
            "A = agrees", "Who made this?").Nodes[0];

        // 2 headers + (3 claims + provenance) x (1 label + 2 source cells).
        Assert.Equal(2 + 4 * 3, graphic.Primitives.OfType<RectShape>().Count());
        Assert.Contains(graphic.Primitives.OfType<TextLabel>(), l => l.Text == "Who made this?");
        Assert.Contains(graphic.Primitives.OfType<TextLabel>(), l => l.Text == "Ledger");
    }
}

public class LearnerHeldKitTests
{
    [Fact]
    public void The_passport_is_four_pages_and_every_page_carries_the_pledge_in_ink()
    {
        var document = LearnerHeldKit.PortfolioPassport(
            ["What is it?", "Why I chose it"], ["Before, I...", "Now, I..."], contentsRows: 8,
            "Never in a data system.");
        var pages = document.Nodes.OfType<VectorGraphic>().ToList();

        Assert.Equal(4, pages.Count);
        Assert.All(pages, page => Assert.Contains(
            page.Primitives.OfType<TextLabel>(), l => l.Text == "Never in a data system."));

        // The contents page rules exactly the declared rows.
        Assert.Equal(8, pages[0].Primitives.OfType<LineSeg>().Count());

        // Four selection slips each carry both prompts.
        Assert.Equal(4 * 2, pages[1].Primitives.OfType<TextLabel>()
            .Count(l => l.Text is "What is it?" or "Why I chose it"));
    }

    [Fact]
    public void Strategy_cards_and_goal_sheets_stay_teacher_worded_and_pledged()
    {
        var shelf = LearnerHeldKit.StrategyShelf(
            ["Reread slowly", "Draw it", "Break it apart", "Breathe"], "Mine.");
        var page = (VectorGraphic)shelf.Nodes[0];
        Assert.Equal(4, page.Primitives.OfType<RectShape>().Count());
        Assert.Contains(page.Primitives.OfType<TextLabel>(), l => l.Text == "Mine.");

        var goal = LearnerHeldKit.GoalPost(["My goal", "How I will know"], "In my folder only.");
        var sheet = (VectorGraphic)goal.Nodes[0];
        Assert.Contains(sheet.Primitives.OfType<TextLabel>(), l => l.Text == "My goal");
        Assert.Contains(sheet.Primitives.OfType<TextLabel>(), l => l.Text == "In my folder only.");
        Assert.Equal(6, sheet.Primitives.OfType<LineSeg>().Count()); // three ruled lines per prompt

        Assert.Throws<ArgumentException>(() => LearnerHeldKit.StrategyShelf(["one", "two"], "p"));
    }
}

public class RubricPressTests
{
    [Fact]
    public void The_one_point_rubric_centers_every_criterion_between_evidence_columns()
    {
        var graphic = (VectorGraphic)RubricPresses.OnePointRubric(
            ["Clear claim", "Cited evidence"], "Growing toward", "Going beyond").Nodes[0];

        Assert.Equal(3 + 6, graphic.Primitives.OfType<RectShape>().Count()); // headers + 2 rows x 3 columns
        var criterion = graphic.Primitives.OfType<TextLabel>().Single(l => l.Text == "Clear claim");
        Assert.Equal(graphic.WidthMm / 2, criterion.X, 9); // dead center of the middle column
    }

    [Fact]
    public void Success_criteria_and_done_definition_carry_checkboxes_and_the_final_self_check()
    {
        var criteria = (VectorGraphic)RubricPresses.SuccessCriteria(
            "I can explain the seasons.", ["I name the tilt", "I use a diagram"],
            ["Beginning", "Meeting", "Beyond"]).Nodes[0];
        Assert.Equal(2 + 3, criteria.Primitives.OfType<RectShape>().Count()); // checkboxes + continuum cells
        Assert.Contains(criteria.Primitives.OfType<TextLabel>(), l => l.Text == "Beyond");

        var done = (VectorGraphic)RubricPresses.DoneDefinition(
            ["All answered", "Name on top"], ["Full sentences"], ["Guesses"], "Final check.").Nodes[0];
        Assert.Equal(2 + 2 + 1, done.Primitives.OfType<RectShape>().Count()); // checklist boxes + columns + final box
        Assert.Contains(done.Primitives.OfType<TextLabel>(), l => l.Text == "Final check.");
    }
}

public class WholeBookVariantTests
{
    [Fact]
    public void Landscape_swaps_the_declared_dimensions_exactly()
    {
        Assert.Equal((279.4, 215.9), BlankformsPress.Dimensions(PageSize.LetterLandscape));
        Assert.Equal((297.0, 210.0), BlankformsPress.Dimensions(PageSize.A4Landscape));

        var graph = (VectorGraphic)BlankformsPress.GraphPaper(PageSize.LetterLandscape).Nodes[0];
        Assert.Equal(279.4, graph.WidthMm);

        var chart = (VectorGraphic)BlankformsPress.HundredChart(size: PageSize.A4Landscape).Nodes[0];
        Assert.Equal(297, chart.WidthMm);

        // The calibration instrument is honest about being portrait.
        Assert.Throws<ArgumentException>(() => CalibrationPress.ProofPage(PageSize.LetterLandscape));
    }

    [Fact]
    public void Low_ink_scales_weights_outlines_fills_and_never_moves_geometry()
    {
        var original = BlankformsPress.DotPaper();
        var lowInk = LowInkPress.Apply(original);

        var before = ((VectorGraphic)original.Nodes[0]).Primitives.OfType<CircleShape>().ToList();
        var after = ((VectorGraphic)lowInk.Nodes[0]).Primitives.OfType<CircleShape>().ToList();

        Assert.Equal(before.Count, after.Count);
        for (var i = 0; i < before.Count; i++)
        {
            Assert.Equal(before[i].CenterX, after[i].CenterX);
            Assert.Equal(before[i].CenterY, after[i].CenterY);
            Assert.Equal(before[i].RadiusMm, after[i].RadiusMm);
            Assert.False(after[i].Filled);
            Assert.Equal(Math.Max(LowInkPress.MinimumStrokeMm, before[i].StrokeWidthMm * LowInkPress.StrokeFactor), after[i].StrokeWidthMm, 9);
        }

        Assert.Contains("(low ink)", ((VectorGraphic)lowInk.Nodes[0]).Description, StringComparison.Ordinal);
        Assert.False(DocumentValidator.HasBlockingIssues(DocumentValidator.Validate(lowInk)));
    }

    [Fact]
    public void The_recipe_book_stands_at_twenty_two_all_green_all_provider_free()
    {
        Assert.Equal(22, DeterministicPressRecipes.All.Count);
        Assert.All(DeterministicPressRecipes.All, r =>
        {
            Assert.Equal(DataLane.Green, r.MaximumLane);
            Assert.Empty(r.RequiredProviderCapabilities);
        });
        Assert.Contains(DeterministicPressRecipes.LearnerHeld.ProhibitedPurposes,
            p => p.Contains("data system", StringComparison.Ordinal));
    }
}
