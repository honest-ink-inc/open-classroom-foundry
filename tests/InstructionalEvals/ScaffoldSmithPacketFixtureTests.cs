// SPDX-License-Identifier: GPL-3.0-or-later
using System.Net;
using System.Text;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.BuiltIn.ScaffoldSmith;
using Foundry.Rendering;

namespace Foundry.Tests.InstructionalEvals;

/// <summary>
/// Synthetic structural corpus for Scaffold Smith's packet recipe (plan §10.3).
/// It proves exact target/criterion/support preservation, nonblank rationale
/// fields, optional support furniture, teacher-only rationale/removal content,
/// refusal, and deterministic rendering. It does not detect answer leakage,
/// judge barrier or fade quality, prove target fidelity, or supply curriculum,
/// accessibility, specialist, or teacher-usability evidence.
/// </summary>
public sealed class ScaffoldSmithPacketFixtureTests
{
    public sealed record Fixture(
        string Id,
        string Stratum,
        string Task,
        LearningTarget Target,
        string[] SuccessCriteria,
        ScaffoldSpec[] Scaffolds,
        string[]? HintLadder = null,
        string[]? VocabularyBank = null,
        string? SentenceFrame = null,
        string Language = "en",
        string[]? ExpectedBlockingCodes = null)
    {
        public override string ToString() => $"{Id} [{Stratum}]";
    }

    private static readonly DateTimeOffset SomeInstant = new(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

    public static readonly IReadOnlyList<Fixture> Fixtures = CreateFixtures();

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
    public void The_corpus_is_thirty_six_distinct_synthetic_cases_for_the_packet_recipe_not_the_task_entry_preset()
    {
        Assert.Equal(36, Fixtures.Count);
        Assert.Equal(Fixtures.Count, Fixtures.Select(fixture => fixture.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.True(Fixtures.Select(fixture => fixture.Stratum).Distinct(StringComparer.Ordinal).Count() >= 8);
        Assert.Equal(10, Fixtures.Count(fixture => fixture.ExpectedBlockingCodes is { Length: > 0 }));
        Assert.True(Fixtures.Select(fixture => fixture.Language).Distinct(StringComparer.Ordinal).Count() >= 5);
        Assert.Contains(Fixtures, fixture => fixture.Scaffolds.Length >= 5 && fixture.ExpectedBlockingCodes is null);
        Assert.Contains(Fixtures, fixture => fixture.HintLadder is null && fixture.ExpectedBlockingCodes is null);
        Assert.Contains(Fixtures, fixture => fixture.HintLadder is { Length: >= 5 } && fixture.ExpectedBlockingCodes is null);
        Assert.Contains(Fixtures, fixture => fixture.VocabularyBank is null && fixture.ExpectedBlockingCodes is null);
        Assert.Contains(Fixtures, fixture => fixture.VocabularyBank is { Length: >= 5 } && fixture.ExpectedBlockingCodes is null);
        Assert.Contains(Fixtures, fixture => fixture.SentenceFrame is not null && fixture.ExpectedBlockingCodes is null);

        var recipe = Assert.Single(ScaffoldSmithBuilder.Recipes, item => item.Id == "scaffold-smith.packet");
        Assert.Equal("0.1.0", recipe.Version);
        Assert.Equal("schema.scaffold-smith.v1", recipe.OutputSchemaId);
        Assert.Equal("0.1", recipe.EvaluationSuiteVersion);
    }

    [Theory]
    [MemberData(nameof(FixtureIndexes))]
    public async Task Every_fixture_fails_closed_or_preserves_the_implemented_packet_contract_and_renders_deterministically(int fixtureIndex)
    {
        var fixture = Fixtures[fixtureIndex];
        var result = ScaffoldSmithBuilder.BuildPacket(
            fixture.Task,
            fixture.Target,
            fixture.SuccessCriteria,
            fixture.Scaffolds,
            fixture.HintLadder,
            fixture.VocabularyBank,
            fixture.SentenceFrame,
            fixture.Language);

        Assert.Equal(
            Sorted(fixture.ExpectedBlockingCodes),
            Sorted(result.Issues
                .Where(issue => issue.Severity == ValidationSeverity.Blocking)
                .Select(issue => issue.Code)));
        Assert.Equal(fixture.Language, result.Document.Language);

        if (fixture.ExpectedBlockingCodes is { Length: > 0 })
        {
            Assert.Throws<InvalidOperationException>(() => ApprovalGate.Approve(
                DraftArtifact.New(result.Document, DataLane.Green),
                "synthetic-corpus@example.invalid",
                result.Issues,
                SomeInstant));
            return;
        }

        Assert.False(string.IsNullOrWhiteSpace(fixture.Target.Statement));
        Assert.False(string.IsNullOrWhiteSpace(fixture.Target.EvidenceOfLearning));
        Assert.NotEmpty(fixture.SuccessCriteria);
        Assert.NotEmpty(fixture.Scaffolds);
        Assert.All(fixture.Scaffolds, scaffold =>
        {
            Assert.False(string.IsNullOrWhiteSpace(scaffold.Support));
            Assert.False(string.IsNullOrWhiteSpace(scaffold.BarrierAddressed));
            Assert.False(string.IsNullOrWhiteSpace(scaffold.DemandPreserved));
            Assert.False(string.IsNullOrWhiteSpace(scaffold.FadeCriterion));
        });

        AssertImplementedDocumentShape(fixture, result.Document);
        await AssertApprovedRenderingIsDeterministicAsync(fixture, result);
    }

    private static List<Fixture> CreateFixtures()
    {
        var fixtures = new List<Fixture>
        {
            Subject("subject-mathematics", "Mathematics"),
            Subject("subject-literacy", "Literacy"),
            Subject("subject-science", "Science"),
            Subject("subject-history", "History"),
            Subject("subject-art", "Art"),
            Subject("subject-music", "Music"),
            Subject("subject-physical-education", "Physical education"),
            Subject("subject-computing", "Computing"),
            Subject("subject-engineering", "Engineering"),
            Subject("subject-civics", "Civics"),

            F(
                "language-arabic",
                "language-layout",
                "مهمة تجريبية AR-1",
                T("هدف تجريبي AR-1", "دليل تجريبي AR-1"),
                ["معيار تجريبي AR-1"],
                [S("AR-1", "دعم تجريبي AR-1", "حاجز تجريبي AR-1", "طلب محفوظ AR-1", "معيار تلاشي AR-1")],
                language: "ar"),
            F(
                "language-hebrew",
                "language-layout",
                "משימה ניסויית HE-1",
                T("יעד ניסויי HE-1", "ראיה ניסויית HE-1"),
                ["קריטריון ניסויי HE-1"],
                [S("HE-1", "תמיכה ניסויית HE-1", "מחסום ניסויי HE-1", "דרישה נשמרת HE-1", "קריטריון דעיכה HE-1")],
                language: "he"),
            F(
                "language-japanese",
                "language-layout",
                "合成課題 JA-1",
                T("合成目標 JA-1", "合成証拠 JA-1"),
                ["合成基準 JA-1"],
                [S("JA-1", "合成支援 JA-1", "合成障壁 JA-1", "保持する要求 JA-1", "合成除去基準 JA-1")],
                language: "ja"),
            F(
                "language-traditional-chinese",
                "language-layout",
                "合成任務 ZH-1",
                T("合成目標 ZH-1", "合成證據 ZH-1"),
                ["合成準則 ZH-1"],
                [S("ZH-1", "合成支援 ZH-1", "合成障礙 ZH-1", "保留要求 ZH-1", "合成移除準則 ZH-1")],
                language: "zh-Hant"),

            F(
                "scaffolds-one",
                "scaffold-count",
                "Synthetic one-support packet S-1",
                T("Synthetic target S-1", "Synthetic evidence S-1"),
                ["Synthetic criterion S-1"],
                Scaffolds("S-1", 1)),
            F(
                "scaffolds-three",
                "scaffold-count",
                "Synthetic three-support packet S-3",
                T("Synthetic target S-3", "Synthetic evidence S-3"),
                ["Synthetic criterion S-3A", "Synthetic criterion S-3B"],
                Scaffolds("S-3", 3)),
            F(
                "scaffolds-five",
                "scaffold-count",
                "Synthetic five-support packet S-5",
                T("Synthetic target S-5", "Synthetic evidence S-5"),
                ["Synthetic criterion S-5A", "Synthetic criterion S-5B", "Synthetic criterion S-5C"],
                Scaffolds("S-5", 5)),

            F(
                "hints-none",
                "hint-breadth",
                "Synthetic no-hint packet H-0",
                T("Synthetic target H-0", "Synthetic evidence H-0"),
                ["Synthetic criterion H-0"],
                [S("H-0")]),
            F(
                "hints-two",
                "hint-breadth",
                "Synthetic two-hint packet H-2",
                T("Synthetic target H-2", "Synthetic evidence H-2"),
                ["Synthetic criterion H-2"],
                [S("H-2")],
                hintLadder: ["Synthetic hint H-2A", "Synthetic hint H-2B"]),
            F(
                "hints-five",
                "hint-breadth",
                "Synthetic five-hint packet H-5",
                T("Synthetic target H-5", "Synthetic evidence H-5"),
                ["Synthetic criterion H-5"],
                [S("H-5")],
                hintLadder:
                [
                    "Synthetic hint H-5A",
                    "Synthetic hint H-5B",
                    "Synthetic hint H-5C",
                    "Synthetic hint H-5D",
                    "Synthetic hint H-5E",
                ]),

            F(
                "vocabulary-none",
                "vocabulary-breadth",
                "Synthetic no-vocabulary packet V-0",
                T("Synthetic target V-0", "Synthetic evidence V-0"),
                ["Synthetic criterion V-0"],
                [S("V-0")]),
            F(
                "vocabulary-five",
                "vocabulary-breadth",
                "Synthetic five-word packet V-5",
                T("Synthetic target V-5", "Synthetic evidence V-5"),
                ["Synthetic criterion V-5"],
                [S("V-5")],
                vocabularyBank: ["term V-5A", "term V-5B", "term V-5C", "term V-5D", "term V-5E"]),

            F(
                "frame-none",
                "optional-frame",
                "Synthetic no-frame packet F-0",
                T("Synthetic target F-0", "Synthetic evidence F-0"),
                ["Synthetic criterion F-0"],
                [S("F-0")]),
            F(
                "frame-present",
                "optional-frame",
                "Synthetic optional-frame packet F-1",
                T("Synthetic target F-1", "Synthetic evidence F-1"),
                ["Synthetic criterion F-1"],
                [S("F-1")],
                sentenceFrame: "One synthetic possibility F-1 is ... because ..."),

            F(
                "rendering-long-text",
                "rendering",
                $"Synthetic long packet LONG-1 {new string('t', 220)}",
                T($"Synthetic target LONG-1 {new string('x', 260)}", $"Synthetic evidence LONG-1 {new string('e', 280)}"),
                [$"Synthetic criterion LONG-1 {new string('c', 300)}"],
                [S("LONG-1", support: $"Synthetic support LONG-1 {new string('s', 320)}")]),
            F(
                "rendering-markup-mixed-script",
                "rendering",
                "Synthetic <task> & α مهمة 合成 MIX-1",
                T("Synthetic <target> & β MIX-1", "Synthetic <evidence> & γ MIX-1"),
                ["Synthetic <criterion> & δ MIX-1"],
                [S("MIX-1", "Synthetic <support> & دعم MIX-1", "Synthetic <barrier> & 障壁 MIX-1", "Synthetic <demand> & طلب MIX-1", "Synthetic <fade> & 除去 MIX-1")],
                hintLadder: ["Synthetic <hint> MIX-1A", "Synthetic <hint> MIX-1B"],
                vocabularyBank: ["Synthetic <term> MIX-1"],
                sentenceFrame: "Synthetic <frame> & token MIX-1 ..."),

            F(
                "refusal-blank-target",
                "refusal-target",
                "Synthetic blank-target packet R-TS",
                T(" ", "Synthetic evidence R-TS"),
                ["Synthetic criterion R-TS"],
                [S("R-TS")],
                expectedBlockingCodes: ["doc.paragraph.empty", "scaffold.target"]),
            F(
                "refusal-blank-evidence",
                "refusal-target",
                "Synthetic blank-evidence packet R-TE",
                T("Synthetic target R-TE", " "),
                ["Synthetic criterion R-TE"],
                [S("R-TE")],
                expectedBlockingCodes: ["scaffold.target"]),
            F(
                "refusal-no-criteria",
                "refusal-criteria",
                "Synthetic no-criteria packet R-C0",
                T("Synthetic target R-C0", "Synthetic evidence R-C0"),
                [],
                [S("R-C0")],
                expectedBlockingCodes: ["doc.list.empty", "scaffold.criteria"]),
            F(
                "refusal-blank-criterion",
                "refusal-criteria",
                "Synthetic blank-criterion packet R-CB",
                T("Synthetic target R-CB", "Synthetic evidence R-CB"),
                [" "],
                [S("R-CB")],
                expectedBlockingCodes: ["doc.list.blank-item"]),
            F(
                "refusal-no-scaffolds",
                "refusal-rationale",
                "Synthetic no-scaffold packet R-S0",
                T("Synthetic target R-S0", "Synthetic evidence R-S0"),
                ["Synthetic criterion R-S0"],
                [],
                expectedBlockingCodes: ["scaffold.none"]),
            F(
                "refusal-blank-support",
                "refusal-rationale",
                "Synthetic blank-support packet R-SS",
                T("Synthetic target R-SS", "Synthetic evidence R-SS"),
                ["Synthetic criterion R-SS"],
                [S("R-SS", support: " ")],
                expectedBlockingCodes: ["doc.list.blank-item", "scaffold.rationale"]),
            F(
                "refusal-blank-barrier",
                "refusal-rationale",
                "Synthetic blank-barrier packet R-SB",
                T("Synthetic target R-SB", "Synthetic evidence R-SB"),
                ["Synthetic criterion R-SB"],
                [S("R-SB", barrier: " ")],
                expectedBlockingCodes: ["scaffold.rationale"]),
            F(
                "refusal-blank-demand",
                "refusal-rationale",
                "Synthetic blank-demand packet R-SD",
                T("Synthetic target R-SD", "Synthetic evidence R-SD"),
                ["Synthetic criterion R-SD"],
                [S("R-SD", demand: " ")],
                expectedBlockingCodes: ["scaffold.rationale"]),
            F(
                "refusal-blank-fade",
                "refusal-rationale",
                "Synthetic blank-fade packet R-SF",
                T("Synthetic target R-SF", "Synthetic evidence R-SF"),
                ["Synthetic criterion R-SF"],
                [S("R-SF", fade: " ")],
                expectedBlockingCodes: ["scaffold.rationale"]),
            F(
                "refusal-one-hint",
                "refusal-hint-ladder",
                "Synthetic one-hint packet R-H1",
                T("Synthetic target R-H1", "Synthetic evidence R-H1"),
                ["Synthetic criterion R-H1"],
                [S("R-H1")],
                hintLadder: ["Synthetic only hint R-H1"],
                expectedBlockingCodes: ["scaffold.ladder"]),
        };

        return fixtures;
    }

    private static Fixture Subject(string id, string subject)
        => F(
            id,
            "subject-breadth",
            $"Synthetic {subject} scaffold packet {id}",
            T($"Complete synthetic {subject} target {id}", $"Produce synthetic {subject} evidence {id}"),
            [$"Synthetic {subject} criterion {id}A", $"Synthetic {subject} criterion {id}B"],
            [S(id)]);

    private static LearningTarget T(string statement, string evidence) => new(statement, evidence);

    private static ScaffoldSpec[] Scaffolds(string token, int count)
        => [.. Enumerable.Range(1, count).Select(index => S($"{token}-{index}"))];

    private static ScaffoldSpec S(
        string token,
        string? support = null,
        string? barrier = null,
        string? demand = null,
        string? fade = null)
        => new(
            support ?? $"Synthetic support {token}",
            barrier ?? $"Synthetic barrier {token}",
            demand ?? $"Synthetic demand preserved {token}",
            fade ?? $"Synthetic fade criterion {token}");

    private static Fixture F(
        string id,
        string stratum,
        string task,
        LearningTarget target,
        string[] successCriteria,
        ScaffoldSpec[] scaffolds,
        string[]? hintLadder = null,
        string[]? vocabularyBank = null,
        string? sentenceFrame = null,
        string language = "en",
        string[]? expectedBlockingCodes = null)
        => new(
            id,
            stratum,
            task,
            target,
            successCriteria,
            scaffolds,
            hintLadder,
            vocabularyBank,
            sentenceFrame,
            language,
            expectedBlockingCodes);

    private static string[] Sorted(IEnumerable<string>? values)
        => [.. (values ?? []).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal)];

    private static void AssertImplementedDocumentShape(Fixture fixture, ArtifactDocument document)
    {
        Assert.Equal(fixture.Task, Assert.IsType<Heading>(document.Nodes[0]).Text);
        Assert.Equal(fixture.Target.Statement, Assert.IsType<Paragraph>(document.Nodes[1]).Text);

        var lists = document.Nodes.OfType<UnorderedList>().ToArray();
        Assert.Contains(lists, list => list.Items.SequenceEqual(fixture.SuccessCriteria));
        Assert.Contains(lists, list => list.Items.SequenceEqual(fixture.Scaffolds.Select(scaffold => scaffold.Support)));

        if (fixture.HintLadder is { Length: > 1 } hints)
        {
            Assert.Equal(hints, Assert.Single(document.Nodes.OfType<OrderedSteps>()).Steps);
        }
        else
        {
            Assert.Empty(document.Nodes.OfType<OrderedSteps>());
        }

        if (fixture.VocabularyBank is { Length: > 0 } words)
        {
            Assert.Contains(lists, list => list.Items.SequenceEqual(words));
        }

        if (!string.IsNullOrWhiteSpace(fixture.SentenceFrame))
        {
            var frame = Assert.Single(document.Nodes.OfType<Card>());
            Assert.Contains("optional", frame.Title, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(fixture.SentenceFrame, frame.Body);
        }
        else
        {
            Assert.Empty(document.Nodes.OfType<Card>());
        }

        var notices = document.Nodes.OfType<TeacherOnlyNotice>().ToArray();
        Assert.Equal(fixture.Scaffolds.Length + 2, notices.Length);
        Assert.Contains(notices, notice => notice.Text == $"Evidence of learning: {fixture.Target.EvidenceOfLearning}");
        Assert.Contains(notices, notice => notice.Text.StartsWith("Removal plan:", StringComparison.Ordinal));
        Assert.All(fixture.Scaffolds, scaffold => Assert.Contains(
            notices,
            notice => notice.Text.Contains($"Support: {scaffold.Support}", StringComparison.Ordinal)
                && notice.Text.Contains($"Barrier addressed: {scaffold.BarrierAddressed}", StringComparison.Ordinal)
                && notice.Text.Contains($"Demand preserved: {scaffold.DemandPreserved}", StringComparison.Ordinal)
                && notice.Text.Contains($"Fade when: {scaffold.FadeCriterion}", StringComparison.Ordinal)));
    }

    private static async Task AssertApprovedRenderingIsDeterministicAsync(Fixture fixture, ScaffoldResult result)
    {
        var approved = ApprovalGate.Approve(
            DraftArtifact.New(result.Document, DataLane.Green),
            "synthetic-corpus@example.invalid",
            result.Issues,
            SomeInstant);
        var renderer = new AccessibleHtmlRenderer();
        var learnerRequest = new RenderRequest(RenderTarget.AccessibleHtml);
        var first = await renderer.RenderAsync(approved, learnerRequest, CancellationToken.None);
        var second = await renderer.RenderAsync(approved, learnerRequest, CancellationToken.None);
        var learner = Encoding.UTF8.GetString(first.Content.Span);
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

        Assert.True(first.Content.Span.SequenceEqual(second.Content.Span), $"{fixture}: learner rendering drifted.");
        Assert.Contains($"<html lang=\"{fixture.Language}\"", learner, StringComparison.Ordinal);
        Assert.Contains(WebUtility.HtmlEncode(fixture.Task), learner, StringComparison.Ordinal);
        Assert.Contains(WebUtility.HtmlEncode(fixture.Target.Statement), learner, StringComparison.Ordinal);
        Assert.DoesNotContain(WebUtility.HtmlEncode(fixture.Target.EvidenceOfLearning), learner, StringComparison.Ordinal);
        Assert.Contains(WebUtility.HtmlEncode(fixture.Target.EvidenceOfLearning), teacher, StringComparison.Ordinal);
        Assert.DoesNotContain("Removal plan:", learner, StringComparison.Ordinal);
        Assert.Contains("Removal plan:", teacher, StringComparison.Ordinal);
        Assert.Contains("@page", print, StringComparison.Ordinal);

        foreach (var criterion in fixture.SuccessCriteria)
        {
            Assert.Contains(WebUtility.HtmlEncode(criterion), learner, StringComparison.Ordinal);
        }

        foreach (var scaffold in fixture.Scaffolds)
        {
            Assert.Contains(WebUtility.HtmlEncode(scaffold.Support), learner, StringComparison.Ordinal);
            Assert.DoesNotContain(WebUtility.HtmlEncode(scaffold.BarrierAddressed), learner, StringComparison.Ordinal);
            Assert.DoesNotContain(WebUtility.HtmlEncode(scaffold.DemandPreserved), learner, StringComparison.Ordinal);
            Assert.DoesNotContain(WebUtility.HtmlEncode(scaffold.FadeCriterion), learner, StringComparison.Ordinal);
            Assert.Contains(WebUtility.HtmlEncode(scaffold.BarrierAddressed), teacher, StringComparison.Ordinal);
            Assert.Contains(WebUtility.HtmlEncode(scaffold.DemandPreserved), teacher, StringComparison.Ordinal);
            Assert.Contains(WebUtility.HtmlEncode(scaffold.FadeCriterion), teacher, StringComparison.Ordinal);
        }

        foreach (var hint in fixture.HintLadder ?? [])
        {
            Assert.Contains(WebUtility.HtmlEncode(hint), learner, StringComparison.Ordinal);
        }

        foreach (var word in fixture.VocabularyBank ?? [])
        {
            Assert.Contains(WebUtility.HtmlEncode(word), learner, StringComparison.Ordinal);
        }

        if (fixture.SentenceFrame is { } frame)
        {
            Assert.Contains(WebUtility.HtmlEncode(frame), learner, StringComparison.Ordinal);
        }
    }
}
