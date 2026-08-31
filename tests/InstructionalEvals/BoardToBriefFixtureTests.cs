// SPDX-License-Identifier: GPL-3.0-or-later
using System.Net;
using System.Text;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.BuiltIn.BoardToBrief;
using Foundry.Rendering;

namespace Foundry.Tests.InstructionalEvals;

/// <summary>
/// Synthetic, subject-diverse acceptance corpus for Board to Brief (plan §10.2).
/// The fixtures exercise literal preservation and deterministic structure; they
/// do not stand in for protected AAC/SLP or multilingual review.
/// </summary>
public sealed class BoardToBriefFixtureTests
{
    public sealed record Fixture(
        string Id,
        string Subject,
        BriefLine[] Lines,
        LockedField[] LockedFields,
        string Language = "en",
        string MaterialsLabel = "Materials",
        string VocabularyLabel = "Vocabulary",
        string[]? ExpectedBlockingCodes = null)
    {
        public override string ToString() => $"{Id} [{Subject}]";
    }

    private static readonly DateTimeOffset SomeInstant = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

    public static readonly IReadOnlyList<Fixture> Fixtures =
    [
        F("science-water-cycle", "science",
            [T("Water cycle model"), D("Tuesday, September 8"), S("Label evaporation, condensation, and precipitation."), S("Draw arrows to show the water path."), M("blue pencil"), V("condensation"), N("Keep the answer key on the teacher page.")],
            [L(LockedFieldKind.Date, "Tuesday, September 8")]),
        F("math-coordinate-plane", "mathematics",
            [T("Coordinate plane practice"), S("Plot A = (−3, 4)."), S("Plot B = (2, −1)."), S("Connect A → B."), M("graph paper"), V("ordered pair")],
            [L(LockedFieldKind.Number, "(−3, 4)"), L(LockedFieldKind.Number, "(2, −1)")]),
        F("literacy-evidence", "literacy",
            [T("Evidence paragraph"), D("Due Thursday"), S("Reread p. 47, ¶ 2."), S("Copy one exact phrase in quotation marks."), V("inference"), N("Model a citation only if requested.")],
            [L(LockedFieldKind.Citation, "p. 47, ¶ 2"), L(LockedFieldKind.Date, "Thursday")]),
        F("history-source-comparison", "history",
            [T("Two-source comparison"), S("Read Source A before Source B."), S("Record one agreement and one difference."), M("source packet"), V("perspective"), N("Do not identify a preferred interpretation for learners.")]),
        F("art-color-study", "visual art",
            [T("Warm and cool color study"), S("Divide the paper into two equal spaces."), S("Use warm colors on the left."), S("Use cool colors on the right."), M("tempera paint"), M("brush"), V("contrast")]),
        F("music-rhythm", "music",
            [T("Rhythm rehearsal"), S("Clap mm. 5–8 at 72 BPM."), S("Repeat without stopping."), M("metronome"), V("syncopation")],
            [L(LockedFieldKind.Number, "mm. 5–8"), L(LockedFieldKind.Unit, "72 BPM")]),
        F("pe-stations", "physical education",
            [T("Movement stations"), S("Work for 45 s at each station."), S("Move clockwise when the signal sounds."), M("four cones"), N("Offer the seated movement card without requiring disclosure.")],
            [L(LockedFieldKind.Unit, "45 s")]),
        F("cte-measurement", "career and technical education",
            [T("Measure and mark"), S("Measure 12.5 cm from the left edge."), S("Mark, check, then cut."), M("ruler"), M("safety scissors")],
            [L(LockedFieldKind.Unit, "12.5 cm")]),
        F("library-shelf", "library",
            [T("Shelf-order check"), S("Read the spine label."), S("Place QA 76.73 .C15 before QA 76.9 .D3."), V("call number")],
            [L(LockedFieldKind.Citation, "QA 76.73 .C15"), L(LockedFieldKind.Citation, "QA 76.9 .D3")]),
        F("routine-arrival", "classroom routine",
            [T("Morning arrival"), S("Hang up your bag."), S("Choose lunch on the posted chart."), S("Begin the warm-up."), M("warm-up sheet")]),
        F("earth-science-rocks", "earth science",
            [T("Rock observation"), S("Observe Sample C without scratching it."), S("Record color, texture, and luster."), M("hand lens"), V("luster")],
            [L(LockedFieldKind.Negation, "without scratching")]),
        F("chemistry-volume", "chemistry",
            [T("Volume transfer model"), S("Pour exactly 25 mL of colored water."), S("Read the meniscus at eye level."), M("graduated cylinder"), V("meniscus"), N("Use colored water only; this is not a chemical reaction.")],
            [L(LockedFieldKind.Unit, "25 mL"), L(LockedFieldKind.Negation, "not a chemical reaction")]),
        F("geometry-proof", "geometry",
            [T("Triangle congruence check"), S("Mark AB ≅ DE."), S("Mark ∠B ≅ ∠E."), S("Name the supported congruence condition."), V("congruent")],
            [L(LockedFieldKind.Condition, "AB ≅ DE"), L(LockedFieldKind.Condition, "∠B ≅ ∠E")]),
        F("computing-debug", "computer science",
            [T("Trace the loop"), S("Start with count = 0."), S("Repeat while count < 4."), S("Record the value after each pass."), V("iteration")],
            [L(LockedFieldKind.Condition, "count < 4"), L(LockedFieldKind.Number, "count = 0")]),
        F("engineering-load", "engineering",
            [T("Paper bridge test"), S("Place supports 20 cm apart."), S("Add one washer at a time."), S("Stop at 10 washers or when the bridge bends."), M("paper strip"), M("washers")],
            [L(LockedFieldKind.Unit, "20 cm"), L(LockedFieldKind.Number, "10 washers")]),
        F("drama-cues", "drama",
            [T("Scene cue rehearsal"), S("Begin after the line “The gate is open.”"), S("Pause for two beats before crossing."), V("cue"), N("The quoted line is a synthetic rehearsal cue.")],
            [L(LockedFieldKind.Quotation, "“The gate is open.”")]),
        F("health-label", "health",
            [T("Read a sample label"), S("Find the serving size on the fictional label."), S("Compare 8 g with 12 g."), M("fictional label card"), V("serving size")],
            [L(LockedFieldKind.Unit, "8 g"), L(LockedFieldKind.Unit, "12 g")]),
        F("media-url", "media literacy",
            [T("Source address check"), S("Compare https://example.org/source-a with the printed address."), S("Circle any character that differs."), V("domain")],
            [L(LockedFieldKind.Url, "https://example.org/source-a")]),
        F("geography-map", "geography",
            [T("Map route description"), S("Start at Grid B4."), S("Travel north → east → south."), S("Name the ending grid square."), M("fictional map")],
            [L(LockedFieldKind.ProperName, "Grid B4"), L(LockedFieldKind.Condition, "north → east → south")]),
        F("economics-budget", "economics",
            [T("Fictional budget comparison"), S("Keep the total at or below $18.50."), S("Explain one tradeoff."), M("fictional price list"), V("tradeoff")],
            [L(LockedFieldKind.Condition, "at or below $18.50")]),
        F("astronomy-scale", "astronomy",
            [T("Moon-distance scale"), S("Use 1 cm = 10,000 km."), S("Label the model as a scale representation."), M("paper strip"), V("scale")],
            [L(LockedFieldKind.Unit, "1 cm = 10,000 km")]),
        F("ecology-quadrat", "ecology",
            [T("Synthetic quadrat count"), S("Count only the printed clover symbols."), S("Record n = 17 without estimating."), M("synthetic quadrat sheet"), V("sample")],
            [L(LockedFieldKind.Number, "n = 17"), L(LockedFieldKind.Negation, "without estimating")]),
        F("language-revision", "language arts",
            [T("Sentence revision"), S("Preserve the name Dr. Amari Vega."), S("Change only the sentence opening."), V("syntax")],
            [L(LockedFieldKind.ProperName, "Dr. Amari Vega"), L(LockedFieldKind.Condition, "only the sentence opening")]),
        F("civics-preamble", "civics",
            [T("Preamble phrase study"), S("Copy “We the People” exactly."), S("Describe the phrase's role in one sentence."), V("preamble")],
            [L(LockedFieldKind.Quotation, "“We the People”")]),
        F("algebra-overbar", "algebra",
            [T("Mean notation"), S("Write x̄ = 14."), S("Explain what x̄ represents in this synthetic set."), V("mean")],
            [L(LockedFieldKind.Number, "x̄ = 14")]),
        F("fractions-recipe", "mathematics",
            [T("Fraction scaling"), S("Begin with 3/4 cup."), S("Double every listed amount."), M("fictional recipe card")],
            [L(LockedFieldKind.Unit, "3/4 cup")]),
        F("diagram-reference", "biology",
            [T("Cell diagram labels"), S("Move A → nucleus."), S("Move B → cell membrane."), S("Leave C ? until the teacher checks it."), V("organelle")],
            [L(LockedFieldKind.Condition, "C ?")]),
        F("rights-metadata", "digital citizenship",
            [T("Image credit check"), S("Keep “CC BY 4.0 — Example Artist” beside the sample image."), S("Verify the supplied source record."), M("rights ledger excerpt")],
            [L(LockedFieldKind.RightsMetadata, "CC BY 4.0 — Example Artist")]),
        F("spanish-directions", "world languages",
            [T("Rutina de laboratorio"), S("Ponte las gafas de seguridad."), S("Mide 15 mL de agua."), M("vaso medidor"), V("medir")],
            [L(LockedFieldKind.Unit, "15 mL")], language: "es", materialsLabel: "Materiales", vocabularyLabel: "Vocabulario"),
        F("french-directions", "world languages",
            [T("Préparer le cahier"), S("Écris la date : 8 septembre."), S("Souligne le titre."), M("cahier"), V("souligner")],
            [L(LockedFieldKind.Date, "8 septembre")], language: "fr", materialsLabel: "Matériel", vocabularyLabel: "Vocabulaire"),
        F("arabic-directions", "world languages",
            [T("ترتيب المواد"), S("ضع القلم بجانب الدفتر."), S("لا تفتح الكتاب بعد."), M("قلم"), V("بجانب")],
            [L(LockedFieldKind.Negation, "لا تفتح الكتاب بعد")], language: "ar", materialsLabel: "المواد", vocabularyLabel: "المفردات"),
        F("japanese-directions", "world languages",
            [T("実験の準備"), S("カードAを左に置く。"), S("カードBを右に置く。"), M("カードA"), V("左")],
            [L(LockedFieldKind.ProperName, "カードA")], language: "ja", materialsLabel: "材料", vocabularyLabel: "ことば"),
        F("refusal-no-title", "boundary",
            [S("Read the first line."), M("direction card")], expectedBlockingCodes: ["brief.title"]),
        F("refusal-two-titles", "boundary",
            [T("First possible title"), T("Second possible title"), S("Do not choose between them automatically.")], expectedBlockingCodes: ["brief.title"]),
        F("refusal-missing-locked", "boundary",
            [T("Unresolved date"), S("Ask the teacher to retype the date.")],
            [L(LockedFieldKind.Date, "Monday, September 14")], expectedBlockingCodes: ["locked.missing"]),
        F("refusal-empty-locked", "boundary",
            [T("Incomplete protected fact"), S("Return to verification.")],
            [L(LockedFieldKind.Number, " ")], expectedBlockingCodes: ["locked.empty"]),
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
    public void The_corpus_is_deliberately_broad_and_entirely_synthetic()
    {
        Assert.Equal(36, Fixtures.Count);
        Assert.Equal(Fixtures.Count, Fixtures.Select(fixture => fixture.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.True(Fixtures.Select(fixture => fixture.Subject).Distinct(StringComparer.Ordinal).Count() >= 20);
        Assert.True(Fixtures.Count(fixture => fixture.Language != "en") >= 4);
        Assert.True(Fixtures.Count(fixture => fixture.ExpectedBlockingCodes is { Length: > 0 }) >= 4);
        Assert.Equal(
            Enum.GetValues<LockedFieldKind>().OrderBy(kind => kind),
            Fixtures.SelectMany(fixture => fixture.LockedFields).Select(field => field.Kind).Distinct().OrderBy(kind => kind));
    }

    [Theory]
    [MemberData(nameof(FixtureIndexes))]
    public async Task Every_fixture_preserves_source_structure_and_fails_closed_or_renders(int fixtureIndex)
    {
        var fixture = Fixtures[fixtureIndex];
        var result = BoardToBriefBuilder.Build(
            fixture.Lines,
            fixture.LockedFields,
            fixture.Language,
            fixture.MaterialsLabel,
            fixture.VocabularyLabel);
        var blockingCodes = result.Issues
            .Where(issue => issue.Severity == ValidationSeverity.Blocking)
            .Select(issue => issue.Code)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();
        var expectedCodes = (fixture.ExpectedBlockingCodes ?? [])
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expectedCodes, blockingCodes);
        AssertSourceTraceability(fixture, result.Document);

        if (expectedCodes.Length > 0)
        {
            Assert.Throws<InvalidOperationException>(() => ApprovalGate.Approve(
                DraftArtifact.New(result.Document, DataLane.Green),
                "teacher@example.org",
                result.Issues,
                SomeInstant));
            return;
        }

        AssertSemanticShape(fixture, result.Document);
        await AssertApprovedRenderingIsDeterministicAsync(fixture, result);
    }

    private static void AssertSourceTraceability(Fixture fixture, ArtifactDocument document)
    {
        var permitted = fixture.Lines.Select(line => line.Text)
            .Concat([fixture.MaterialsLabel, fixture.VocabularyLabel])
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(DocumentText.CollectStrings(document), text => Assert.Contains(text, permitted));
    }

    private static void AssertSemanticShape(Fixture fixture, ArtifactDocument document)
    {
        Assert.Equal(fixture.Language, document.Language);
        Assert.Equal(Assert.Single(fixture.Lines, line => line.Role == BriefRole.Title).Text,
            Assert.IsType<Heading>(document.Nodes[0]).Text);
        Assert.Equal(
            fixture.Lines.Where(line => line.Role == BriefRole.Date).Select(line => line.Text),
            document.Nodes.OfType<Paragraph>().Select(paragraph => paragraph.Text));

        var expectedSteps = fixture.Lines.Where(line => line.Role == BriefRole.Step).Select(line => line.Text).ToArray();
        var actualSteps = document.Nodes.OfType<OrderedSteps>().SingleOrDefault()?.Steps ?? [];
        Assert.Equal(expectedSteps, actualSteps);

        var expectedLists = new[] { BriefRole.Material, BriefRole.Vocabulary }
            .Select(role => fixture.Lines.Where(line => line.Role == role).Select(line => line.Text).ToArray())
            .Where(items => items.Length > 0)
            .ToArray();
        Assert.Equal(expectedLists.Length, document.Nodes.OfType<UnorderedList>().Count());
        Assert.All(expectedLists.Select((items, index) => (items, index)), pair =>
            Assert.Equal(pair.items, document.Nodes.OfType<UnorderedList>().ElementAt(pair.index).Items));
        Assert.Equal(
            fixture.Lines.Where(line => line.Role == BriefRole.Note).Select(line => line.Text),
            document.Nodes.OfType<TeacherOnlyNotice>().Select(note => note.Text));

        var flattened = string.Join('\n', DocumentText.CollectStrings(document));
        Assert.All(fixture.LockedFields, field => Assert.Contains(field.ExactValue, flattened, StringComparison.Ordinal));
    }

    private static async Task AssertApprovedRenderingIsDeterministicAsync(Fixture fixture, BriefResult result)
    {
        var approved = ApprovalGate.Approve(
            DraftArtifact.New(result.Document, DataLane.Green),
            "teacher@example.org",
            result.Issues,
            SomeInstant);
        var renderer = new AccessibleHtmlRenderer();
        var learnerRequest = new RenderRequest(RenderTarget.AccessibleHtml);
        var teacherRequest = new RenderRequest(RenderTarget.AccessibleHtml, RenderAudience.Teacher);

        var firstLearner = await renderer.RenderAsync(approved, learnerRequest, CancellationToken.None);
        var secondLearner = await renderer.RenderAsync(approved, learnerRequest, CancellationToken.None);
        var teacher = Encoding.UTF8.GetString(
            (await renderer.RenderAsync(approved, teacherRequest, CancellationToken.None)).Content.Span);
        var print = Encoding.UTF8.GetString(
            (await renderer.RenderAsync(approved, new RenderRequest(RenderTarget.PrintHtml), CancellationToken.None)).Content.Span);
        var learner = Encoding.UTF8.GetString(firstLearner.Content.Span);

        Assert.True(firstLearner.Content.Span.SequenceEqual(secondLearner.Content.Span), $"{fixture}: learner rendering drifted.");
        Assert.Contains(WebUtility.HtmlEncode(fixture.Lines.Single(line => line.Role == BriefRole.Title).Text), learner, StringComparison.Ordinal);
        Assert.Contains($"lang=\"{fixture.Language}\"", learner, StringComparison.Ordinal);
        Assert.Contains("@page", print, StringComparison.Ordinal);
        Assert.DoesNotContain("<aside class=\"teacher-only\"", learner, StringComparison.Ordinal);
        Assert.DoesNotContain("<aside class=\"teacher-only\"", print, StringComparison.Ordinal);

        foreach (var note in fixture.Lines.Where(line => line.Role == BriefRole.Note))
        {
            Assert.DoesNotContain(WebUtility.HtmlEncode(note.Text), learner, StringComparison.Ordinal);
            Assert.Contains(WebUtility.HtmlEncode(note.Text), teacher, StringComparison.Ordinal);
        }

        if (fixture.Language == "ar")
        {
            Assert.Contains("<html lang=\"ar\" dir=\"rtl\">", learner, StringComparison.Ordinal);
        }
    }

    private static Fixture F(
        string id,
        string subject,
        BriefLine[] lines,
        LockedField[]? lockedFields = null,
        string language = "en",
        string materialsLabel = "Materials",
        string vocabularyLabel = "Vocabulary",
        string[]? expectedBlockingCodes = null)
        => new(id, subject, lines, lockedFields ?? [], language, materialsLabel, vocabularyLabel, expectedBlockingCodes);

    private static BriefLine T(string text) => new(text, BriefRole.Title);

    private static BriefLine D(string text) => new(text, BriefRole.Date);

    private static BriefLine S(string text) => new(text, BriefRole.Step);

    private static BriefLine M(string text) => new(text, BriefRole.Material);

    private static BriefLine V(string text) => new(text, BriefRole.Vocabulary);

    private static BriefLine N(string text) => new(text, BriefRole.Note);

    private static LockedField L(LockedFieldKind kind, string exactValue) => new(kind, exactValue);
}
