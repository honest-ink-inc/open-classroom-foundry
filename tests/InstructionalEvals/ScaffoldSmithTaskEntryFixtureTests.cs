// SPDX-License-Identifier: GPL-3.0-or-later
using System.Net;
using System.Text;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.BuiltIn.ScaffoldSmith;
using Foundry.Rendering;

namespace Foundry.Tests.InstructionalEvals;

/// <summary>
/// ADR-005's stratified task-entry corpus. All classroom material is synthetic;
/// ambiguity cases prove preservation rather than inviting the engine to guess.
/// </summary>
public sealed class ScaffoldSmithTaskEntryFixtureTests
{
    public enum FixtureStratum
    {
        MultiDay,
        MaterialsHeavyLab,
        SinglePeriod,
        BoundaryAndAmbiguity,
        Refusal,
    }

    public sealed record Fixture(
        string Id,
        FixtureStratum Stratum,
        string Task,
        string[] Materials,
        string FirstAction,
        string[] Chunks,
        string[] HelpRoutes,
        string DefinitionOfDone,
        string[]? Checkpoints = null,
        string FadeCriterion = "the learner starts within 30 seconds without the card",
        string Language = "en",
        string[]? ExpectedBlockingCodes = null,
        bool RejectsArguments = false)
    {
        public override string ToString() => $"{Id} [{Stratum}]";
    }

    private static readonly DateTimeOffset SomeInstant = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

    public static readonly IReadOnlyList<Fixture> Fixtures =
    [
        // Multi-day assignments: the day boundary is explicit in the chunks, not inferred.
        new(
            "multi-day-weather-log",
            FixtureStratum.MultiDay,
            "Three-day local weather log",
            ["weather sheet", "pencil", "ruler"],
            "Write today's date at the top of the weather sheet.",
            ["Day 1: record cloud cover and temperature.", "Day 2: repeat the same observations.", "Day 3: repeat, then compare all three entries."],
            ["Check the posted observation example.", "Ask the teacher which column comes next."],
            "Three dated rows are complete and one comparison is written.",
            ["End of Day 1: one complete row.", "End of Day 2: two complete rows."]),
        new(
            "multi-day-fictional-town-map",
            FixtureStratum.MultiDay,
            "Fictional town map and legend",
            ["fictional town brief", "grid paper", "pencil", "colored pencils"],
            "Circle the four places named in the fictional town brief.",
            ["Day 1: place the four locations on the grid.", "Day 2: draw routes between the locations.", "Day 3: add and check the map legend."],
            ["Use the sample legend card.", "Ask a partner to point to a symbol that is unclear."],
            "The map has four labeled locations, connected routes, and a matching legend.",
            ["Location check before routes.", "Route check before the legend."]),
        new(
            "multi-day-data-display",
            FixtureStratum.MultiDay,
            "Four-day classroom temperature display",
            ["synthetic data table", "graph paper", "pencil", "straightedge"],
            "Underline the smallest value in the supplied synthetic data table.",
            ["Day 1: choose and label both axes.", "Day 2: plot the first half of the supplied values.", "Day 3: plot the remaining values.", "Day 4: write two observations supported by the display."],
            ["Compare the axes with the checklist.", "Ask the teacher to verify one plotted point."],
            "Every supplied value is plotted and two evidence-based observations are attached.",
            ["Axes approved before plotting.", "All points checked before observations."]),
        new(
            "multi-day-paper-model",
            FixtureStratum.MultiDay,
            "Three-day paper structure design",
            ["index cards", "paper tape", "ruler", "design sheet"],
            "Draw one possible base shape on the design sheet.",
            ["Day 1: sketch and label a plan.", "Day 2: build and test the first paper structure.", "Day 3: revise one feature and document the change."],
            ["Use the design-constraint card.", "Ask the teacher to restate the test condition."],
            "The plan, first test result, revision, and second test result are recorded.",
            ["Plan check before building.", "First result recorded before revision."]),
        new(
            "multi-day-comparative-reading",
            FixtureStratum.MultiDay,
            "Two-text comparison across three days",
            ["two teacher-authored passages", "comparison organizer", "pencil"],
            "Read the title and first paragraph of Text A.",
            ["Day 1: annotate Text A for the stated question.", "Day 2: annotate Text B for the same question.", "Day 3: complete the comparison using evidence from both texts."],
            ["Use the evidence-stem card.", "Ask the teacher which passage supports a selected note."],
            "The organizer includes one supported similarity and one supported difference.",
            ["Text A note selected.", "Text B note selected."]),
        new(
            "multi-day-rehearsal-plan",
            FixtureStratum.MultiDay,
            "Four-day scene rehearsal plan",
            ["teacher-authored scene", "rehearsal log", "pencil", "timer"],
            "Mark the first speaking cue in the scene.",
            ["Day 1: read and mark cues.", "Day 2: rehearse the opening section.", "Day 3: rehearse the complete scene.", "Day 4: revise one choice after feedback and rehearse again."],
            ["Use the cue-marking example.", "Ask the director for the next starting line."],
            "The complete scene is rehearsed and the log names one revision.",
            ["Opening section ready.", "Complete run recorded."]),
        new(
            "multi-day-open-image-glossary",
            FixtureStratum.MultiDay,
            "Open-image vocabulary glossary",
            ["rights-cleared image set", "glossary template", "pencil", "asset ledger excerpt"],
            "Match the first rights-cleared image to one supplied term.",
            ["Day 1: match all images and terms.", "Day 2: draft one plain-language definition per term.", "Day 3: verify attribution and revise the glossary."],
            ["Check the supplied term bank.", "Ask the teacher to verify an attribution entry."],
            "Every term has an image, a definition, and its supplied attribution.",
            ["Matches checked before definitions.", "Attributions checked before completion."]),
        new(
            "multi-day-geometry-exhibit",
            FixtureStratum.MultiDay,
            "Three-day geometry example exhibit",
            ["shape cards", "display paper", "pencil", "straightedge", "removable notes"],
            "Sort the first two shape cards by the property named on the prompt.",
            ["Day 1: sort and record the full card set.", "Day 2: choose three examples and label their evidence.", "Day 3: assemble and proofread the exhibit."],
            ["Use the property glossary.", "Ask a partner to test one label against its shape."],
            "Three examples are displayed with accurate property labels and evidence.",
            ["Sort checked before display choices.", "Labels checked before assembly."]),

        // Materials-heavy labs: every fixture names at least eight concrete materials.
        new(
            "lab-shadow-measurement",
            FixtureStratum.MaterialsHeavyLab,
            "Shadow measurement station",
            ["flashlight", "cardboard figure", "modeling clay", "white paper", "ruler", "pencil", "masking tape", "data table"],
            "Place the white paper on the taped work area.",
            ["Stand the figure in clay.", "Place the flashlight at the first marker.", "Trace and measure the shadow.", "Repeat at the second marker.", "Compare the two measurements."],
            ["Check the station diagram.", "Ask the teacher to verify the light marker."],
            "Both shadow measurements and one comparison are recorded.",
            ["First measurement recorded."]),
        new(
            "lab-seed-observation",
            FixtureStratum.MaterialsHeavyLab,
            "Dry seed comparison lab",
            ["three dry seed samples", "sorting tray", "hand lens", "ruler", "index card", "pencil", "data table", "sample labels"],
            "Put one seed sample in each labeled tray section.",
            ["Observe each sample with the hand lens.", "Measure one seed from each sample.", "Record color, shape, and length.", "Write one supported comparison.", "Return every sample to its labeled cup."],
            ["Use the observation word bank.", "Ask the teacher which measurement unit to use."],
            "All three samples have observations and measurements, plus one comparison.",
            ["Three measurement rows complete."]),
        new(
            "lab-ice-insulation",
            FixtureStratum.MaterialsHeavyLab,
            "Ice insulation model lab",
            ["two sealed ice cups", "felt square", "paper square", "two rubber bands", "timer", "tray", "towel", "data sheet", "pencil"],
            "Set both sealed cups on the tray.",
            ["Wrap one cup with felt and one with paper.", "Start the timer.", "Observe both cups at each posted interval.", "Record the final melt level.", "Dry the station."],
            ["Use the wrapping diagram.", "Ask the teacher to call the next interval."],
            "Every interval is recorded and the station is dry and reset.",
            ["Wraps checked before timing.", "Final levels recorded before cleanup."]),
        new(
            "lab-magnet-map",
            FixtureStratum.MaterialsHeavyLab,
            "Magnet interaction map",
            ["bar magnet", "ring magnet", "paper clips", "wood craft stick", "plastic cap", "aluminum foil", "test tray", "result cards", "pencil"],
            "Place the result cards beside the empty test tray.",
            ["Predict one object's interaction.", "Test that object without throwing or striking it.", "Record the result.", "Repeat for every supplied object.", "Sort the results into the supplied categories."],
            ["Check the testing sequence card.", "Ask the teacher to identify the next untested object."],
            "Every supplied object has a prediction, result, and category.",
            ["First test observed by the teacher."]),
        new(
            "lab-soil-filtration-model",
            FixtureStratum.MaterialsHeavyLab,
            "Soil filtration model",
            ["clear cup", "paper filter", "gravel", "sand", "dry soil", "measuring cup", "water", "collection tray", "spoon", "observation sheet"],
            "Put the collection tray under the clear cup.",
            ["Place the filter in the cup.", "Add the supplied layers in the posted order.", "Measure and pour the water slowly.", "Observe the collected water without tasting it.", "Record observations and clean the tray."],
            ["Use the layer-order diagram.", "Ask the teacher to check the model before pouring."],
            "The layer order, measured water amount, observations, and cleanup are complete.",
            ["Model checked before water is poured."]),
        new(
            "lab-sound-vibration",
            FixtureStratum.MaterialsHeavyLab,
            "Sound vibration observation lab",
            ["empty box", "three rubber bands", "paper cup", "plastic wrap", "dry rice", "craft stick", "tray", "observation card", "pencil"],
            "Stretch one rubber band around the empty box.",
            ["Pluck each rubber band gently and record an observation.", "Cover the cup with plastic wrap.", "Place a few rice grains on the wrap.", "Create a nearby sound and observe the grains.", "Return the rice to the labeled container."],
            ["Check the setup picture.", "Ask the teacher to verify the cup covering."],
            "Observations from both models are recorded and loose materials are returned.",
            ["Box model complete before cup model."]),
        new(
            "lab-paper-bridge",
            FixtureStratum.MaterialsHeavyLab,
            "Paper bridge load test",
            ["two books", "index card", "copy paper", "paper clips", "ruler", "masking tape", "test tray", "design sheet", "pencil"],
            "Place the two books on the marked positions in the test tray.",
            ["Sketch one bridge shape.", "Build the bridge between the books.", "Add paper clips one at a time.", "Record the supported count.", "Change one feature and test again."],
            ["Use the fold-example card.", "Ask the teacher to verify the book spacing."],
            "Two bridge designs and their supported counts are recorded.",
            ["Spacing checked before the first test."]),
        new(
            "lab-color-mixing",
            FixtureStratum.MaterialsHeavyLab,
            "Transparent color mixing lab",
            ["three labeled water cups", "red food-color dropper", "blue food-color dropper", "yellow food-color dropper", "six clear sample cups", "tray", "stir sticks", "paper towel", "mixing table", "pencil"],
            "Set all clear sample cups inside the tray.",
            ["Add the posted number of red and blue drops to the first cup.", "Stir and record the observed color.", "Repeat the two remaining posted mixes.", "Compare results with predictions.", "Wipe and reset the tray."],
            ["Use the drop-count card.", "Ask the teacher to check a cup before mixing."],
            "All posted mixtures, observations, comparisons, and cleanup are complete.",
            ["Drop counts checked for the first mixture."]),

        // Single-period tasks are intentionally compact and complete in one sitting.
        new(
            "single-period-vocabulary-sort",
            FixtureStratum.SinglePeriod,
            "Ten-minute vocabulary sort",
            ["term cards", "category mat", "pencil"],
            "Read the first term card aloud or silently.",
            ["Place every card on the best-fit category.", "Choose two placements to explain.", "Record the two explanations."],
            ["Use the category definitions.", "Mark one card with a question note for teacher help."],
            "Every card is placed and two placements have written explanations."),
        new(
            "single-period-geometry-exit",
            FixtureStratum.SinglePeriod,
            "Geometry exit task",
            ["exit card", "pencil", "straightedge"],
            "Write the name of one property shown in the diagram.",
            ["Label the relevant parts.", "Write one claim.", "Support the claim with the labeled diagram."],
            ["Use the property list.", "Ask the teacher to point to the claim box."],
            "The diagram is labeled and the claim has visual evidence."),
        new(
            "single-period-source-observation",
            FixtureStratum.SinglePeriod,
            "Primary-source observation warm-up",
            ["public-domain image", "observation sheet", "pencil"],
            "Write one detail you can see without interpreting it.",
            ["List three literal observations.", "Mark one possible inference as an inference.", "Write one question the source raises."],
            ["Use the observation-versus-inference card.", "Ask the teacher to check one statement's category."],
            "Three observations, one marked inference, and one question are present."),
        new(
            "single-period-paragraph-revision",
            FixtureStratum.SinglePeriod,
            "Revise one teacher-authored paragraph",
            ["teacher-authored paragraph", "revision checklist", "pencil"],
            "Underline the paragraph's main claim.",
            ["Check each sentence against the claim.", "Revise one unclear connection.", "Read the revised paragraph once."],
            ["Use the connection-word bank.", "Ask the teacher to read the revised sentence with you."],
            "One connection is revised and every checklist box is considered."),
        new(
            "single-period-rhythm-rehearsal",
            FixtureStratum.SinglePeriod,
            "Short rhythm rehearsal",
            ["teacher-authored rhythm card", "practice pad", "pencil", "timer"],
            "Tap the first measure slowly once.",
            ["Mark the difficult measure.", "Practice that measure three times.", "Perform the complete rhythm once.", "Record one next step."],
            ["Use the count-aloud card.", "Ask the teacher to model one beat without completing the task."],
            "The complete rhythm is performed and one next step is recorded."),
        new(
            "single-period-contour-sketch",
            FixtureStratum.SinglePeriod,
            "Contour sketch study",
            ["staged object", "drawing paper", "pencil", "timer"],
            "Place the staged object inside the marked viewing area.",
            ["Observe the outer edge for thirty seconds.", "Draw the outer contour.", "Add three observed interior lines.", "Write one noticing."],
            ["Use the contour example.", "Ask the teacher to restate the time limit."],
            "The contour, three interior lines, and one noticing are on the page."),
        new(
            "single-period-map-key",
            FixtureStratum.SinglePeriod,
            "Map-key practice",
            ["fictional map", "symbol key", "response card", "pencil"],
            "Find the north arrow on the fictional map.",
            ["Match three map symbols to the key.", "Trace one route named on the card.", "Describe the route using two direction words."],
            ["Use the direction-word list.", "Ask the teacher to verify the starting point."],
            "Three symbols are matched and the route description uses two direction words."),
        new(
            "single-period-data-table",
            FixtureStratum.SinglePeriod,
            "Quick synthetic data table",
            ["supplied synthetic values", "table template", "pencil", "calculator"],
            "Copy the first supplied value into the matching table row.",
            ["Enter all remaining values.", "Check the units in every row.", "Calculate the requested total.", "Write one pattern you notice."],
            ["Use the completed example row.", "Ask the teacher to verify one unit."],
            "All supplied values and units are entered, with a total and one pattern."),

        // Boundary and ambiguity: valid typed text is preserved; no semantic certainty is invented.
        new(
            "boundary-no-materials",
            FixtureStratum.BoundaryAndAmbiguity,
            "Silent mental rehearsal",
            [],
            "Read the task title once.",
            ["Picture the first move.", "Say the sequence quietly.", "Begin the task."],
            ["Ask the teacher to repeat the task title."],
            "The sequence has been rehearsed and the task has begun.",
            Checkpoints: []),
        new(
            "boundary-one-chunk",
            FixtureStratum.BoundaryAndAmbiguity,
            "One-step submission check",
            ["completed draft", "checklist"],
            "Put the completed draft beside the checklist.",
            ["Check every required item, then place the draft in the collection tray."],
            ["Ask the teacher which checklist version applies."],
            "Every required item is checked and the draft is in the collection tray."),
        new(
            "boundary-one-help-route",
            FixtureStratum.BoundaryAndAmbiguity,
            "Independent practice start",
            ["practice sheet", "pencil"],
            "Write the date on the practice sheet.",
            ["Read the first prompt.", "Complete the work you can support.", "Review each response."],
            ["Place a question marker beside the exact step and ask the teacher."],
            "Every prompt has a response or a visible question marker."),
        new(
            "boundary-null-checkpoints",
            FixtureStratum.BoundaryAndAmbiguity,
            "Brief reading response",
            ["teacher-authored passage", "response card", "pencil"],
            "Read the response question before rereading the passage.",
            ["Reread and mark one relevant line.", "Write a response.", "Check that the marked line supports it."],
            ["Use the response frame if useful.", "Ask the teacher to verify the selected line."],
            "The response cites or points to one relevant line.",
            Checkpoints: null),
        new(
            "boundary-spanish-language",
            FixtureStratum.BoundaryAndAmbiguity,
            "Organizar una explicación breve",
            ["hoja de planificación", "lápiz"],
            "Escribe la idea principal en la primera casilla.",
            ["Añade un dato de apoyo.", "Explica cómo apoya la idea.", "Revisa la explicación."],
            ["Consulta el banco de conectores.", "Pide a la docente que aclare una casilla."],
            "La idea, el dato y la explicación están completos.",
            Language: "es"),
        new(
            "boundary-arabic-rtl",
            FixtureStratum.BoundaryAndAmbiguity,
            "ترتيب خطوات مهمة قصيرة",
            ["بطاقة المهمة", "قلم"],
            "اقرأ الخطوة الأولى على بطاقة المهمة.",
            ["رتب الخطوات الثلاث.", "نفذ الخطوة الأولى.", "راجع الترتيب."],
            ["استخدم مثال الترتيب.", "اطلب من المعلم توضيح خطوة واحدة."],
            "الخطوات مرتبة وتمت مراجعتها.",
            Language: "ar"),
        new(
            "boundary-markup-like-text",
            FixtureStratum.BoundaryAndAmbiguity,
            "Compare <draft> & \"check\" labels",
            ["label cards", "comparison sheet"],
            "Copy the label <draft> exactly, including its brackets.",
            ["Compare <draft> with the & symbol.", "Circle the label \"check\".", "Describe one visible difference."],
            ["Ask the teacher to point to the literal label, not interpret it."],
            "All three literal labels are represented and one difference is described."),
        new(
            "boundary-ambiguity-preserved",
            FixtureStratum.BoundaryAndAmbiguity,
            "Finish it.",
            ["the supplied item"],
            "Start when ready.",
            ["Do the next part.", "Check it."],
            ["Ask what 'it' refers to before making an assumption."],
            "It is done.",
            FadeCriterion: "the teacher replaces the ambiguous source wording with a concrete task"),

        // Refusals: missing structure fails closed; malformed arguments do not mint a document.
        new(
            "refusal-blank-first-action",
            FixtureStratum.Refusal,
            "Structurally incomplete task entry",
            ["task sheet"],
            "   ",
            ["Complete the named task."],
            ["Ask the teacher for the first action."],
            "The named task is complete.",
            ExpectedBlockingCodes: ["task-entry.first"]),
        new(
            "refusal-no-chunks",
            FixtureStratum.Refusal,
            "Task entry without chunks",
            ["task sheet"],
            "Read the title.",
            [],
            ["Ask the teacher to supply the missing task sequence."],
            "The named task is complete.",
            ExpectedBlockingCodes: ["task-entry.chunks"]),
        new(
            "refusal-no-help-route",
            FixtureStratum.Refusal,
            "Task entry without agency route",
            ["task sheet"],
            "Read the title.",
            ["Complete the named task."],
            [],
            "The named task is complete.",
            ExpectedBlockingCodes: ["task-entry.help"]),
        new(
            "refusal-blank-definition-of-done",
            FixtureStratum.Refusal,
            "Task entry without an ending",
            ["task sheet"],
            "Read the title.",
            ["Complete the named task."],
            ["Ask the teacher how completion will be checked."],
            "\t",
            ExpectedBlockingCodes: ["task-entry.done"]),
        new(
            "refusal-blank-material-item",
            FixtureStratum.Refusal,
            "Task entry with an unnamed material",
            ["task sheet", " "],
            "Read the title.",
            ["Complete the named task."],
            ["Ask the teacher to identify the unnamed material."],
            "The named task is complete.",
            ExpectedBlockingCodes: ["doc.list.blank-item"]),
        new(
            "refusal-blank-checkpoint-item",
            FixtureStratum.Refusal,
            "Task entry with an unnamed checkpoint",
            ["task sheet"],
            "Read the title.",
            ["Complete the named task."],
            ["Ask the teacher to identify the missing checkpoint."],
            "The named task is complete.",
            [" "],
            ExpectedBlockingCodes: ["doc.list.blank-item"]),
        new(
            "refusal-blank-task",
            FixtureStratum.Refusal,
            " ",
            ["task sheet"],
            "Read the title.",
            ["Complete the named task."],
            ["Ask the teacher for the task title."],
            "The named task is complete.",
            RejectsArguments: true),
        new(
            "refusal-invalid-language-tag",
            FixtureStratum.Refusal,
            "Task entry with malformed language metadata",
            ["task sheet"],
            "Read the title.",
            ["Complete the named task."],
            ["Ask the teacher for help."],
            "The named task is complete.",
            Language: "en_US",
            RejectsArguments: true),
    ];

    public static TheoryData<int> FixtureIndexes()
    {
        var data = new TheoryData<int>();
        for (var index = 0; index < Fixtures.Count; index++)
        {
            data.Add(index);
        }

        return data;
    }

    [Fact]
    public void Corpus_is_forty_cases_with_eight_in_each_required_stratum()
    {
        Assert.Equal(40, Fixtures.Count);
        Assert.Equal(Fixtures.Count, Fixtures.Select(fixture => fixture.Id).Distinct(StringComparer.Ordinal).Count());

        foreach (var stratum in Enum.GetValues<FixtureStratum>())
        {
            var count = Fixtures.Count(fixture => fixture.Stratum == stratum);
            Assert.True(count == 8, $"Expected 8 {stratum} fixtures; found {count}.");
        }

        Assert.All(
            Fixtures.Where(fixture => fixture.Stratum == FixtureStratum.MultiDay),
            fixture => Assert.True(
                fixture.Chunks.Count(chunk => chunk.StartsWith("Day ", StringComparison.Ordinal)) >= 2,
                $"{fixture}: a multi-day assignment must expose at least two day boundaries."));
        Assert.All(
            Fixtures.Where(fixture => fixture.Stratum == FixtureStratum.MaterialsHeavyLab),
            fixture => Assert.True(
                fixture.Materials.Length >= 8,
                $"{fixture}: a materials-heavy lab must name at least eight materials."));
        Assert.All(
            Fixtures.Where(fixture => fixture.Stratum == FixtureStratum.SinglePeriod),
            fixture => Assert.True(
                fixture.Chunks.Length <= 4 && fixture.Chunks.All(chunk => !chunk.StartsWith("Day ", StringComparison.Ordinal)),
                $"{fixture}: a single-period task must stay compact and have no day boundary."));
        Assert.Contains(Fixtures, fixture => fixture.Id == "boundary-ambiguity-preserved");
        Assert.Equal(2, Fixtures.Count(fixture => fixture.RejectsArguments));
    }

    [Theory]
    [MemberData(nameof(FixtureIndexes))]
    public async Task Every_fixture_builds_or_refuses_with_exact_diagnostics(int fixtureIndex)
    {
        var fixture = Fixtures[fixtureIndex];
        ScaffoldResult? result = null;

        var exception = Record.Exception(() => result = ScaffoldSmithBuilder.BuildTaskEntry(
            fixture.Task,
            fixture.Materials,
            fixture.FirstAction,
            fixture.Chunks,
            fixture.HelpRoutes,
            fixture.DefinitionOfDone,
            fixture.Checkpoints,
            fixture.FadeCriterion,
            fixture.Language));

        if (fixture.RejectsArguments)
        {
            Assert.True(
                exception is ArgumentException,
                $"{fixture}: expected an ArgumentException; actual: {Describe(exception)}.");
            Assert.Null(result);
            return;
        }

        Assert.True(exception is null, $"{fixture}: unexpected {Describe(exception)}.");
        Assert.NotNull(result);

        var expectedCodes = (fixture.ExpectedBlockingCodes ?? []).Order(StringComparer.Ordinal).ToArray();
        var blockingIssues = result.Issues
            .Where(issue => issue.Severity == ValidationSeverity.Blocking)
            .OrderBy(issue => issue.Code, StringComparer.Ordinal)
            .ToArray();
        var actualCodes = blockingIssues.Select(issue => issue.Code).ToArray();
        Assert.True(
            expectedCodes.SequenceEqual(actualCodes, StringComparer.Ordinal),
            $"{fixture}: expected blocking codes [{string.Join(", ", expectedCodes)}]; "
            + $"actual [{string.Join(", ", actualCodes)}]. Issues: {Describe(blockingIssues)}");

        if (expectedCodes.Length > 0)
        {
            return;
        }

        AssertValidSemanticShape(fixture, result.Document);
        await AssertApprovedRenderingAsync(fixture, result);
    }

    private static void AssertValidSemanticShape(Fixture fixture, ArtifactDocument document)
    {
        Assert.Equal(fixture.Language, document.Language);
        Assert.Equal(fixture.Task, Assert.IsType<Heading>(document.Nodes[0]).Text);
        Assert.Contains(document.Nodes.OfType<Card>(), card => card.Title == "First" && card.Body == fixture.FirstAction);
        Assert.Contains(document.Nodes.OfType<Card>(), card => card.Title == "Done means" && card.Body == fixture.DefinitionOfDone);
        Assert.Contains(document.Nodes.OfType<OrderedSteps>(), steps => steps.Steps.SequenceEqual(fixture.Chunks));
        Assert.Contains(document.Nodes.OfType<OrderedSteps>(), steps => steps.Steps.SequenceEqual(fixture.HelpRoutes));

        if (fixture.Materials.Length == 0)
        {
            Assert.DoesNotContain(document.Nodes.OfType<Heading>(), heading => heading.Text == "Materials");
        }
        else
        {
            Assert.Contains(document.Nodes.OfType<UnorderedList>(), list => list.Items.SequenceEqual(fixture.Materials));
        }

        if (fixture.Checkpoints is { Length: > 0 })
        {
            Assert.Contains(document.Nodes.OfType<UnorderedList>(), list => list.Items.SequenceEqual(fixture.Checkpoints));
        }

        Assert.Contains(
            document.Nodes.OfType<TeacherOnlyNotice>(),
            notice => notice.Text.Contains(fixture.FadeCriterion, StringComparison.Ordinal));
    }

    private static async Task AssertApprovedRenderingAsync(Fixture fixture, ScaffoldResult result)
    {
        var approved = ApprovalGate.Approve(
            DraftArtifact.New(result.Document, DataLane.Green),
            "teacher@example.org",
            result.Issues,
            SomeInstant);
        var renderer = new AccessibleHtmlRenderer();
        var learner = Encoding.UTF8.GetString(
            (await renderer.RenderAsync(
                approved,
                new RenderRequest(RenderTarget.AccessibleHtml),
                CancellationToken.None)).Content.Span);
        var teacher = Encoding.UTF8.GetString(
            (await renderer.RenderAsync(
                approved,
                new RenderRequest(RenderTarget.AccessibleHtml, RenderAudience.Teacher),
                CancellationToken.None)).Content.Span);
        var print = Encoding.UTF8.GetString(
            (await renderer.RenderAsync(
                approved,
                new RenderRequest(RenderTarget.PrintHtml),
                CancellationToken.None)).Content.Span);

        var encodedTask = WebUtility.HtmlEncode(fixture.Task);
        Assert.Contains(encodedTask, learner, StringComparison.Ordinal);
        Assert.Contains($"lang=\"{fixture.Language}\"", learner, StringComparison.Ordinal);
        Assert.DoesNotContain("<aside class=\"teacher-only\"", learner, StringComparison.Ordinal);
        Assert.DoesNotContain("TaskDock preset", learner, StringComparison.Ordinal);
        Assert.DoesNotContain("<aside class=\"teacher-only\"", print, StringComparison.Ordinal);
        Assert.Contains("<aside class=\"teacher-only\"", teacher, StringComparison.Ordinal);
        Assert.DoesNotContain("TaskDock", teacher, StringComparison.Ordinal);
        Assert.Contains(fixture.FadeCriterion, WebUtility.HtmlDecode(teacher), StringComparison.Ordinal);
        Assert.Contains("@page", print, StringComparison.Ordinal);

        if (fixture.Language == "ar")
        {
            Assert.Contains("<html lang=\"ar\" dir=\"rtl\">", learner, StringComparison.Ordinal);
        }
    }

    private static string Describe(Exception? exception)
        => exception is null ? "no exception" : $"{exception.GetType().Name}: {exception.Message}";

    private static string Describe(IEnumerable<ValidationIssue> issues)
        => string.Join(" | ", issues.Select(issue => $"{issue.Code}: {issue.Message}"));
}
