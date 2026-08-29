// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Domain;
using Foundry.Modules.DeterministicPress;

namespace Foundry.Tests.Unit;

// The fourth forge menu's presses. The Chart Press's load-bearing invariant is
// proportional truth: bar length is LINEAR in the teacher's value, asserted as
// arithmetic against the catalog entry's own defaults.

public class ChartPressTests
{
    [Fact]
    public void Bars_are_proportionally_true_to_the_value()
    {
        // The catalog defaults carry the invariant on their face: Sun 18 is
        // exactly twice Shade 9; Window 12 is exactly two-thirds of Sun.
        var definition = PressRoomCatalog.ById("bar-chart");
        var graphic = (VectorGraphic)definition.Build(new PressInputs(PressRoomCatalog.Defaults(definition))).Nodes[0];

        var bars = graphic.Primitives.OfType<RectShape>().Where(r => r.StrokeWidthMm == 0.6).ToList();
        Assert.Equal(3, bars.Count);
        Assert.Equal(bars[1].HeightMm * 2, bars[0].HeightMm, 9);
        Assert.Equal(bars[0].HeightMm * 2 / 3, bars[2].HeightMm, 9);

        // Every bar stands on the same baseline; nothing floats.
        Assert.All(bars, b => Assert.Equal(bars[0].Y + bars[0].HeightMm, b.Y + b.HeightMm, 9));

        // Values and labels ride in ink, verbatim.
        var labels = graphic.Primitives.OfType<TextLabel>().ToList();
        foreach (var text in new[] { "Sun", "Shade", "Window", "18", "9", "12" })
        {
            Assert.Contains(labels, l => l.Text == text);
        }
    }

    [Fact]
    public void Gridlines_fall_at_clean_intervals_evenly_spaced()
    {
        // Max 18 → the smallest clean step needing at most eight lines is 5,
        // so the axis tops out at 20 with gridlines at 5, 10, 15, 20.
        var definition = PressRoomCatalog.ById("bar-chart");
        var graphic = (VectorGraphic)definition.Build(new PressInputs(PressRoomCatalog.Defaults(definition))).Nodes[0];

        var grid = graphic.Primitives.OfType<LineSeg>().Where(l => l.StrokeWidthMm == 0.25).OrderByDescending(l => l.Y1).ToList();
        Assert.Equal(4, grid.Count);
        var spacing = grid[0].Y1 - grid[1].Y1;
        for (var i = 1; i < grid.Count - 1; i++)
        {
            Assert.Equal(spacing, grid[i].Y1 - grid[i + 1].Y1, 9);
        }

        var labels = graphic.Primitives.OfType<TextLabel>().ToList();
        foreach (var numeral in new[] { "0", "5", "10", "15", "20" })
        {
            Assert.Contains(labels, l => l.Text == numeral);
        }
    }

    [Fact]
    public void Horizontal_bars_mirror_the_same_arithmetic_and_meet_the_axis_exactly()
    {
        var graphic = (VectorGraphic)ChartPress.Sheet(
            "Test", ChartPress.Parse([("A", "4"), ("B", "8")]), horizontal: true).Nodes[0];

        var bars = graphic.Primitives.OfType<RectShape>().Where(r => r.StrokeWidthMm == 0.6).ToList();
        Assert.Equal(2, bars.Count);
        Assert.Equal(bars[0].WidthMm * 2, bars[1].WidthMm, 9);
        Assert.Equal(bars[0].X, bars[1].X, 9); // both grow from the zero line

        // Max 8 at step 1: the longest bar ends exactly on the last gridline —
        // the bars and the axis tell one arithmetic.
        var lastGrid = graphic.Primitives.OfType<LineSeg>().Where(l => l.StrokeWidthMm == 0.25).Max(l => l.X1);
        Assert.Equal(lastGrid, bars[1].X + bars[1].WidthMm, 9);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(8, 1)]
    [InlineData(9, 2)]
    [InlineData(16, 2)]
    [InlineData(17, 5)]
    [InlineData(40, 5)]
    [InlineData(41, 10)]
    [InlineData(1000000, 200000)]
    public void The_gridline_step_is_the_smallest_clean_interval(int max, int expected)
        => Assert.Equal(expected, ChartPress.GridStep(max));

    [Fact]
    public void Parsing_and_validation_refuse_loudly_when_wrong()
    {
        Assert.Throws<ArgumentException>(() => ChartPress.Parse([("A", null)]));
        Assert.Throws<ArgumentException>(() => ChartPress.Parse([("A", "four")]));
        Assert.Throws<ArgumentException>(() => ChartPress.Parse([("A", "-3")]));
        Assert.Throws<ArgumentException>(() => ChartPress.Parse([("", "5")]));

        var pair = ChartPress.Parse([("A", "1"), ("B", "2")]);
        Assert.Throws<ArgumentException>(() => ChartPress.Sheet("", pair));
        Assert.Throws<ArgumentException>(() => ChartPress.Sheet("T", [new ChartDatum("A", 1)]));
        Assert.Throws<ArgumentException>(() => ChartPress.Sheet("T",
            [.. Enumerable.Range(1, 13).Select(i => new ChartDatum($"B{i}", i))]));
        Assert.Throws<ArgumentException>(() => ChartPress.Sheet("T",
            [new ChartDatum("A", 0), new ChartDatum("B", 0)]));
    }
}

// Bell-to-Bell's load-bearing invariant is cumulative clock arithmetic with
// a loud overrun refusal, asserted cell by cell against the catalog entry's
// own defaults — which meet the bell exactly.

public class BellToBellTests
{
    private static TableNode DefaultPlanTable(out ArtifactDocument document)
    {
        var definition = PressRoomCatalog.ById("bell-to-bell");
        document = definition.Build(new PressInputs(PressRoomCatalog.Defaults(definition)));
        return document.Nodes.OfType<TableNode>().Single();
    }

    [Fact]
    public void Clock_times_accumulate_activity_by_activity_with_transitions_counted()
    {
        var table = DefaultPlanTable(out var document);

        // 8:30 + 5 + 1 = 8:36; + 15 + 1 = 8:52; + 20 + 1 = 9:13; + 8 + 1 = 9:22.
        Assert.Equal(["8:30", "5", "Warm-up", "1"], table.Rows[0]);
        Assert.Equal(["8:36", "15", "Mini-lesson", "1"], table.Rows[1]);
        Assert.Equal(["8:52", "20", "Guided practice", "1"], table.Rows[2]);
        Assert.Equal(["9:13", "8", "Share out", "1"], table.Rows[3]);

        // The closure holds the last three minutes before the 9:25 bell.
        Assert.Equal(["9:22", "3", "Pack up, reflect, and reset", ""], table.Rows[4]);

        Assert.Contains(document.Nodes.OfType<Paragraph>(),
            p => p.Text == "55 of 55 minutes planned; the bell at 9:25 is met exactly.");
    }

    [Fact]
    public void Open_minutes_are_named_and_the_closure_stays_protected_at_the_bell()
    {
        var definition = PressRoomCatalog.ById("bell-to-bell");
        var values = PressRoomCatalog.Defaults(definition);
        values["period"] = "60";
        var document = definition.Build(new PressInputs(values));

        // Five open minutes appear between the last transition and a closure
        // still anchored to the end of the period: 8:30 + 60 - 3 = 9:27.
        var table = document.Nodes.OfType<TableNode>().Single();
        Assert.Equal("9:27", table.Rows[4][0]);
        Assert.Contains(document.Nodes.OfType<Paragraph>(),
            p => p.Text == "55 of 60 minutes planned; 5 minute(s) open before the closure at 9:27.");
    }

    [Fact]
    public void An_overrunning_plan_is_refused_with_the_arithmetic_in_the_message()
    {
        var definition = PressRoomCatalog.ById("bell-to-bell");
        var values = PressRoomCatalog.Defaults(definition);
        values["period"] = "50";

        var refusal = Assert.Throws<ArgumentException>(() => definition.Build(new PressInputs(values)));
        Assert.Contains("needs 55 minutes but the period holds 50; trim 5 minute(s)", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Start_times_and_rows_are_refused_loudly_when_wrong()
    {
        Assert.Throws<ArgumentException>(() => BellToBell.ParseStartMinutes("8h30"));
        Assert.Throws<ArgumentException>(() => BellToBell.ParseStartMinutes("25:00"));
        Assert.Throws<ArgumentException>(() => BellToBell.ParseStartMinutes("8:75"));
        Assert.Equal(8 * 60 + 30, BellToBell.ParseStartMinutes("8:30"));
        Assert.Equal(13 * 60 + 5, BellToBell.ParseStartMinutes("13:05"));

        Assert.Throws<ArgumentException>(() => BellToBell.Parse([("5", null)]));
        Assert.Throws<ArgumentException>(() => BellToBell.Parse([("five", "Warm-up")]));
        Assert.Throws<ArgumentException>(() => BellToBell.Parse([("0", "Warm-up")]));

        var one = BellToBell.Parse([("5", "Warm-up")]);
        Assert.Throws<ArgumentException>(() => BellToBell.Plan("", "8:30", one, 55, 1, "Closure", 3));
        Assert.Throws<ArgumentException>(() => BellToBell.Plan("T", "8:30", one, 55, 1, "", 3));
        Assert.Throws<ArgumentException>(() => BellToBell.Plan("T", "8:30", [], 55, 1, "Closure", 3));
    }
}

// The verbatim-text kin (menu 4, item 4): Bug Zoo's code and the fluency
// passage must reconstruct EXACTLY from the printed ink — the same discipline
// the Parsons key test enforces.

public class BugZooTests
{
    [Fact]
    public void The_buggy_code_reconstructs_verbatim_with_indentation_as_geometry()
    {
        var definition = PressRoomCatalog.ById("bug-zoo");
        var document = definition.Build(new PressInputs(PressRoomCatalog.Defaults(definition)));
        var graphic = (VectorGraphic)document.Nodes[0];

        const double codeLeft = BlankformsPress.DefaultMarginMm + 10;
        // Section labels share font and anchor but sit at the margin; code
        // lines start at the numbered gutter — x tells them apart.
        var reconstructed = graphic.Primitives.OfType<TextLabel>()
            .Where(l => l.Anchor == TextAnchor.Start && l.FontSizeMm == 4.5 && l.X >= codeLeft)
            .OrderBy(l => l.Y)
            .Select(l => new string(' ', (int)Math.Round((l.X - codeLeft) / ParsonsPress.IndentMmPerSpace)) + l.Text)
            .ToList();

        Assert.Equal(["total = 0", "for n in [1, 2, 3]:", "    total = n", "print(total)"], reconstructed);
    }

    [Fact]
    public void The_misconception_note_rides_teacher_only_and_the_sections_stand()
    {
        var definition = PressRoomCatalog.ById("bug-zoo");
        var document = definition.Build(new PressInputs(PressRoomCatalog.Defaults(definition)));

        var notice = Assert.IsType<TeacherOnlyNotice>(document.Nodes[1]);
        Assert.StartsWith("Intended misconception (teacher only): Assignment replaces", notice.Text, StringComparison.Ordinal);

        var labels = ((VectorGraphic)document.Nodes[0]).Primitives.OfType<TextLabel>().ToList();
        Assert.Contains(labels, l => l.Text.StartsWith("Diagnose", StringComparison.Ordinal));
        Assert.Contains(labels, l => l.Text.StartsWith("Repair", StringComparison.Ordinal));
        Assert.Contains(labels, l => l.Text.StartsWith("Explain", StringComparison.Ordinal));
    }

    [Fact]
    public void Bug_zoo_refuses_loudly_without_its_teacher_authorship()
    {
        string[] code = ["x = 1"];
        string[] sections = ["Diagnose", "Repair", "Explain"];

        Assert.Contains("teacher-authored",
            Assert.Throws<ArgumentException>(() => BugZoo.Sheet("Prompt", code, sections, " ")).Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => BugZoo.Sheet("Prompt", [], sections, "Note"));
        Assert.Throws<ArgumentException>(() => BugZoo.Sheet("Prompt", code, ["Diagnose", "Repair"], "Note"));
        Assert.Throws<ArgumentException>(() => BugZoo.Sheet("Prompt", code, ["Diagnose", " ", "Explain"], "Note"));
        Assert.Throws<ArgumentException>(() => BugZoo.Sheet("Prompt",
            [.. Enumerable.Range(1, 17).Select(i => $"line{i}")], sections, "Note"));
    }
}

public class FluencyRehearsalTests
{
    [Fact]
    public void The_passage_reconstructs_exactly_and_marks_become_breath_breaks()
    {
        string[] passage =
        [
            "The little boat | rocked gently | on the bright water,",
            "and the river | carried it | all the way home.",
        ];
        var graphic = (VectorGraphic)FluencyRehearsal.Sheet("The Little Boat", passage, 3, "I noticed...").Nodes[0];

        var printed = graphic.Primitives.OfType<TextLabel>()
            .Where(l => l.FontSizeMm == 7)
            .OrderBy(l => l.Y)
            .Select(l => l.Text)
            .ToList();

        // Reconstruction like the Parsons key: splitting the printed line on
        // the breath-break slash returns the teacher's phrases verbatim.
        Assert.Equal(passage.Length, printed.Count);
        for (var i = 0; i < passage.Length; i++)
        {
            Assert.Equal(
                passage[i].Split('|').Select(s => s.Trim()),
                printed[i].Split(FluencyRehearsal.BreakMark));
        }
    }

    [Fact]
    public void Tally_boxes_match_the_readings_and_the_reflection_prompt_prints()
    {
        var definition = PressRoomCatalog.ById("fluency-rehearsal");
        var graphic = (VectorGraphic)definition.Build(new PressInputs(PressRoomCatalog.Defaults(definition))).Nodes[0];

        Assert.Equal(3, graphic.Primitives.OfType<RectShape>().Count(r => r.StrokeWidthMm == 0.5));
        Assert.Contains(graphic.Primitives.OfType<TextLabel>(), l => l.Text == "After my last reading, I noticed...");
        Assert.Equal(2, graphic.Primitives.OfType<LineSeg>().Count(l => l.StrokeWidthMm == 0.3));
    }

    [Fact]
    public void Fluency_refuses_loudly_when_wrong()
    {
        string[] fine = ["A short | passage."];

        Assert.Contains("empty phrase",
            Assert.Throws<ArgumentException>(() => FluencyRehearsal.Sheet("T", ["words || more"], 3, "R")).Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => FluencyRehearsal.Sheet("T", ["ends with a mark |"], 3, "R"));
        Assert.Throws<ArgumentException>(() => FluencyRehearsal.Sheet(" ", fine, 3, "R"));
        Assert.Throws<ArgumentException>(() => FluencyRehearsal.Sheet("T", fine, 0, "R"));
        Assert.Throws<ArgumentException>(() => FluencyRehearsal.Sheet("T", fine, 3, " "));
        Assert.Throws<ArgumentException>(() => FluencyRehearsal.Sheet("T",
            [.. Enumerable.Range(1, 15).Select(i => $"line {i}")], 3, "R"));
    }
}

// The card and protocol trio (menu 4, item 5): card kinds distinguished by
// SHAPE — single border, double border, dashed frame — never color alone.

public class ConceptSortTests
{
    [Fact]
    public void Card_kinds_are_shape_distinguished_and_the_concept_stays_on_the_teacher_key()
    {
        var definition = PressRoomCatalog.ById("concept-sort");
        var document = definition.Build(new PressInputs(PressRoomCatalog.Defaults(definition)));

        // 4 + 4 + 2 = 10 cards across two pages of eight, then the key.
        var pages = document.Nodes.OfType<VectorGraphic>().ToList();
        Assert.Equal(2, pages.Count);

        var primitives = pages.SelectMany(p => p.Primitives).ToList();
        Assert.Equal(10, primitives.OfType<RectShape>().Count(r => r.StrokeWidthMm == 0.5)); // every card's outer border
        Assert.Equal(4, primitives.OfType<RectShape>().Count(r => r.StrokeWidthMm == 0.35)); // nonexamples' inner border
        Assert.Equal(8, primitives.OfType<LineSeg>().Count(l => l.Dashed)); // two ambiguous cards, four dashed sides each

        // The concept never prints on a card — only on the teacher key.
        Assert.DoesNotContain(primitives.OfType<TextLabel>(), l => l.Text.Contains("Mammals", StringComparison.Ordinal));
        var key = Assert.IsType<TeacherOnlyNotice>(document.Nodes[^1]);
        Assert.Contains("Mammals", key.Text, StringComparison.Ordinal);
        Assert.Contains("dashed frame = deliberately ambiguous", key.Text, StringComparison.Ordinal);

        Assert.Contains(primitives.OfType<TextLabel>(), l => l.Text == "Platypus");
    }

    [Fact]
    public void Concept_sort_refuses_loudly_when_wrong()
    {
        Assert.Throws<ArgumentException>(() => ConceptSortStudio.Cards(" ", ["a", "b"], ["c", "d"], []));
        Assert.Throws<ArgumentException>(() => ConceptSortStudio.Cards("C", ["only"], ["c", "d"], []));
        Assert.Throws<ArgumentException>(() => ConceptSortStudio.Cards("C", ["a", "b"], ["c", "d"],
            [.. Enumerable.Range(1, 9).Select(i => $"m{i}")]));
    }
}

public class RoleWheelTests
{
    [Fact]
    public void Roles_carry_their_accountable_actions_and_the_rotation_note_rides_every_page()
    {
        var nine = Enumerable.Range(1, 9).Select(i => ($"Role {i}", (string?)$"Action {i}")).ToList();
        var document = DiscussionRoleWheel.Cards(nine, "Rotate clockwise.");

        var pages = document.Nodes.OfType<VectorGraphic>().ToList();
        Assert.Equal(2, pages.Count);
        Assert.All(pages, p => Assert.Single(p.Primitives.OfType<TextLabel>(), l => l.Text == "Rotate clockwise."));

        var labels = pages.SelectMany(p => p.Primitives.OfType<TextLabel>()).ToList();
        Assert.Contains(labels, l => l.Text == "Role 9");
        Assert.Contains(labels, l => l.Text == "Action 9");

        Assert.Contains("accountable action",
            Assert.Throws<ArgumentException>(() => DiscussionRoleWheel.Cards([("Skeptic", null), ("Recorder", "Write.")], "Rotate.")).Message,
            StringComparison.Ordinal);
    }
}

public class PeerFeedbackTests
{
    [Fact]
    public void The_sheet_carries_the_rule_the_stems_and_the_author_decision_box()
    {
        var definition = PressRoomCatalog.ById("peer-feedback");
        var graphic = (VectorGraphic)definition.Build(new PressInputs(PressRoomCatalog.Defaults(definition))).Nodes[0];

        var labels = graphic.Primitives.OfType<TextLabel>().ToList();
        Assert.Contains(labels, l => l.Text.StartsWith("Every comment points at the work", StringComparison.Ordinal));
        Assert.Contains(labels, l => l.Text == "One strength I noticed is...");
        Assert.Contains(labels, l => l.Text.StartsWith("The author decides", StringComparison.Ordinal));

        // Three stem lines plus three author-box lines, all ruled at 0.3.
        Assert.Equal(6, graphic.Primitives.OfType<LineSeg>().Count(l => l.StrokeWidthMm == 0.3));
        // The rule box and the author box both stand as rectangles.
        Assert.Single(graphic.Primitives.OfType<RectShape>(), r => r.StrokeWidthMm == 0.6);
        Assert.Single(graphic.Primitives.OfType<RectShape>(), r => r.StrokeWidthMm == 0.7);

        Assert.Throws<ArgumentException>(() => PeerFeedbackBuilder.Sheet("T", "Rule", ["one stem"], "Author"));
        Assert.Throws<ArgumentException>(() => PeerFeedbackBuilder.Sheet("T", " ", ["a...", "b..."], "Author"));
    }
}

// Glossary Garden (menu 4, item 6): bilingual pairs verbatim, with the lang
// semantics owned by the tested renderer via BilingualPair.

public class GlossaryGardenTests
{
    [Fact]
    public void Bilingual_entries_ride_BilingualPair_verbatim_with_correct_language_tags()
    {
        var definition = PressRoomCatalog.ById("glossary-garden");
        var document = definition.Build(new PressInputs(PressRoomCatalog.Defaults(definition)));

        Assert.Equal("en", document.Language);

        var pairs = document.Nodes.OfType<BilingualPair>().ToList();
        Assert.Equal(3, pairs.Count);
        Assert.All(pairs, p =>
        {
            Assert.Equal("en", p.SourceLocale);
            Assert.Equal("es", p.TargetLocale);
        });

        // Verbatim, diacritics and all — the translations are the teacher's.
        Assert.Equal(("evaporation", "evaporación"), (pairs[0].SourceText, pairs[0].TargetText));
        Assert.Equal(("precipitation", "precipitación"), (pairs[2].SourceText, pairs[2].TargetText));

        // Every entry is a term heading plus its meaning; pairs sit beside
        // their own term.
        Assert.Contains(document.Nodes.OfType<Heading>(), h => h.Level == 2 && h.Text == "condensation");
        Assert.Contains(document.Nodes.OfType<Paragraph>(), p => p.Text == "Vapor becomes liquid drops.");
    }

    [Fact]
    public void Monolingual_entries_carry_no_pair_and_mixed_lists_are_honest()
    {
        var entries = GlossaryGarden.Parse(["axis | The line a graph measures along.", "origin | Where the axes cross. | origen"]);
        var document = GlossaryGarden.Sheet("Graphs", entries, "en", "es");

        var pair = Assert.Single(document.Nodes.OfType<BilingualPair>());
        Assert.Equal("origin", pair.SourceText);
        Assert.Null(entries[0].Translation);
    }

    [Fact]
    public void Glossary_refuses_loudly_when_wrong()
    {
        Assert.Throws<ArgumentException>(() => GlossaryGarden.Parse(["term only"]));
        Assert.Throws<ArgumentException>(() => GlossaryGarden.Parse(["term | meaning | "]));
        Assert.Throws<ArgumentException>(() => GlossaryGarden.Parse(["a | b | c | d"]));

        var two = GlossaryGarden.Parse(["a | one", "b | two"]);
        Assert.Throws<ArgumentException>(() => GlossaryGarden.Sheet(" ", two, "en", "es"));
        Assert.Throws<ArgumentException>(() => GlossaryGarden.Sheet("T", [new GlossaryEntry("a", "one", null)], "en", "es"));
        Assert.Contains("language tag",
            Assert.Throws<ArgumentException>(() => GlossaryGarden.Sheet("T", two, "e n", "es")).Message, StringComparison.Ordinal);
    }
}

// The Studio Sampler (menu 4, item 9): the forge prints its own catalog, and
// no engine can ever go missing silently — the partition is asserted against
// the catalog itself.

public class StudioSamplerTests
{
    [Fact]
    public void Every_catalog_engine_is_represented_exactly_once_bound_or_named()
    {
        var plan = StudioSampler.Plan();
        var included = plan.Included.Select(e => e.Definition.Id).ToList();
        var listed = plan.ListedOnly.Select(e => e.Definition.Id).ToList();

        Assert.Empty(included.Intersect(listed, StringComparer.Ordinal));
        Assert.Equal(
            PressRoomCatalog.All.Select(d => d.Id).Order(StringComparer.Ordinal),
            included.Concat(listed).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void The_cover_names_every_engine_with_its_recipe_id_and_the_pages_share_one_size()
    {
        var document = StudioSampler.Catalog();
        var pages = document.Nodes.OfType<VectorGraphic>().ToList();

        Assert.Equal(StudioSampler.Plan().Included.Count + 1, pages.Count);
        Assert.All(pages, p =>
        {
            Assert.Equal(215.9, p.WidthMm);
            Assert.Equal(279.4, p.HeightMm);
        });

        var cover = pages[0].Primitives.OfType<TextLabel>().Select(l => l.Text).ToList();
        foreach (var definition in PressRoomCatalog.All)
        {
            Assert.Contains(cover, t =>
                t.Contains(definition.Title, StringComparison.Ordinal)
                && t.Contains(definition.Recipe.Id, StringComparison.Ordinal));
        }

        Assert.False(DocumentValidator.HasBlockingIssues(DocumentValidator.Validate(document)));
    }

    [Fact]
    public void The_skips_are_the_honest_ones_and_carry_their_reasons()
    {
        var listed = StudioSampler.Plan().ListedOnly.ToDictionary(e => e.Definition.Id, e => e.Reason, StringComparer.Ordinal);

        Assert.Equal("landscape", listed["timeline"]);
        Assert.Equal("landscape", listed["bar-chart"]);
        Assert.Equal("prose", listed["booklet-guide"]);
        Assert.Equal("prose", listed["bell-to-bell"]);
        Assert.Equal("prose", listed["glossary-garden"]);
        Assert.Equal(5, listed.Count);
    }

    [Fact]
    public void The_sampler_is_byte_identical_across_builds()
        => Assert.Equal(
            System.Text.Json.JsonSerializer.Serialize(StudioSampler.Catalog()),
            System.Text.Json.JsonSerializer.Serialize(StudioSampler.Catalog()));
}

// Class Sets (menu 4, item 10): the seeded presses multiplied. The
// load-bearing invariant is reprintability by printed number: variant v of a
// set IS the single press at seed base + v − 1.

public class ClassSetsTests
{
    private static ArtifactDocument WordSearchSet(int baseSeed, int variants)
    {
        var definition = PressRoomCatalog.ById("word-search");
        return ClassSets.Compose(definition, PressRoomCatalog.Defaults(definition), baseSeed, variants);
    }

    [Fact]
    public void Every_page_carries_its_variant_number_and_seed_in_ink_and_no_two_grids_match()
    {
        var set = WordSearchSet(baseSeed: 100, variants: 3);
        var pages = set.Nodes.OfType<VectorGraphic>().ToList();

        // The word search builds two sheets (puzzle and key) per variant.
        Assert.Equal(6, pages.Count);
        for (var variant = 1; variant <= 3; variant++)
        {
            var stamp = $"Variant {variant} of 3 · seed {99 + variant}";
            foreach (var page in pages.Skip((variant - 1) * 2).Take(2))
            {
                Assert.Single(page.Primitives.OfType<TextLabel>(), l => l.Text == stamp);
                Assert.Contains($"(variant {variant} of 3, seed {99 + variant})", page.Description, StringComparison.Ordinal);
            }
        }

        // Neighboring desks cannot copy: the three puzzles are three grids.
        var grids = Enumerable.Range(0, 3)
            .Select(v => string.Join("", pages[v * 2].Primitives.OfType<TextLabel>().Select(l => l.Text)))
            .ToList();
        Assert.Equal(3, grids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void A_lost_sheet_is_reprintable_by_its_printed_number()
    {
        // Variant 2 of a base-100 set, with its stamp set aside, is byte for
        // byte the single press at seed 101 — THE reprint claim.
        var definition = PressRoomCatalog.ById("word-search");
        var set = WordSearchSet(baseSeed: 100, variants: 3);

        var single = PressRoomCatalog.Defaults(definition);
        single["seed"] = "101";
        var reprint = definition.Build(new PressInputs(single)).Nodes.OfType<VectorGraphic>().ToList();

        var variantPages = set.Nodes.OfType<VectorGraphic>().Skip(2).Take(2).ToList();
        for (var i = 0; i < 2; i++)
        {
            var unstamped = variantPages[i] with
            {
                Primitives = [.. variantPages[i].Primitives.Take(variantPages[i].Primitives.Count - 1)],
                Description = reprint[i].Description,
            };
            Assert.Equal(
                System.Text.Json.JsonSerializer.Serialize(reprint[i]),
                System.Text.Json.JsonSerializer.Serialize(unstamped));
        }
    }

    [Fact]
    public void Teacher_only_notices_ride_along_named_by_their_variant()
    {
        var definition = PressRoomCatalog.ById("parsons-puzzle");
        var set = ClassSets.Compose(definition, PressRoomCatalog.Defaults(definition), baseSeed: 7, variants: 2);

        var notices = set.Nodes.OfType<TeacherOnlyNotice>().ToList();
        Assert.Equal(2, notices.Count);
        Assert.StartsWith("Variant 1: Answer key (seed 7)", notices[0].Text, StringComparison.Ordinal);
        Assert.StartsWith("Variant 2: Answer key (seed 8)", notices[1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Class_sets_refuse_loudly_when_wrong_and_stay_deterministic()
    {
        var unseeded = PressRoomCatalog.ById("graph-paper");
        Assert.Contains("seeded presses only",
            Assert.Throws<ArgumentException>(() =>
                ClassSets.Compose(unseeded, PressRoomCatalog.Defaults(unseeded), 1, 3)).Message, StringComparison.Ordinal);

        var seeded = PressRoomCatalog.ById("word-search");
        var defaults = PressRoomCatalog.Defaults(seeded);
        Assert.Throws<ArgumentException>(() => ClassSets.Compose(seeded, defaults, 1, 1));
        Assert.Throws<ArgumentException>(() => ClassSets.Compose(seeded, defaults, 1, 41));
        Assert.Throws<ArgumentException>(() => ClassSets.Compose(seeded, defaults, 99999998, 3));
        Assert.Throws<ArgumentException>(() => ClassSets.Compose(seeded, defaults, 0, 3));

        Assert.Equal(
            System.Text.Json.JsonSerializer.Serialize(WordSearchSet(20260908, 4)),
            System.Text.Json.JsonSerializer.Serialize(WordSearchSet(20260908, 4)));
    }
}
