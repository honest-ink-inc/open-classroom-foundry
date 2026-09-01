// SPDX-License-Identifier: GPL-3.0-or-later
using System.Net;
using System.Text;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.BuiltIn.LessonLoom;
using Foundry.Rendering;

namespace Foundry.Tests.InstructionalEvals;

/// <summary>
/// Synthetic structural corpus for StrandPlan (stable recipe id
/// <c>lesson-loom</c>; plan §10.7). It proves deterministic timing,
/// check/response and closure invariants, semantic output, audience separation,
/// and rendering breadth. It is not curriculum review or teacher-usability
/// evidence.
/// </summary>
public sealed class LessonLoomFixtureTests
{
    public sealed record Fixture(
        string Id,
        string Stratum,
        string Title,
        LearningTarget Target,
        int TotalMinutes,
        LessonPhase[] Phases,
        string[] Materials,
        string[] AccessRoutes,
        string[]? Contingencies = null,
        string Language = "en",
        string[]? ExpectedBlockingCodes = null,
        string[]? ExpectedWarningCodes = null)
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
    public void The_corpus_is_thirty_six_distinct_synthetic_cases_across_subject_duration_language_and_refusal_strata()
    {
        Assert.Equal(36, Fixtures.Count);
        Assert.Equal(Fixtures.Count, Fixtures.Select(fixture => fixture.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.True(Fixtures.Select(fixture => fixture.Stratum).Distinct(StringComparer.Ordinal).Count() >= 8);
        Assert.Equal(10, Fixtures.Count(fixture => fixture.ExpectedBlockingCodes is { Length: > 0 }));
        Assert.True(Fixtures.Select(fixture => fixture.Language).Distinct(StringComparer.Ordinal).Count() >= 5);
        Assert.Contains(Fixtures, fixture => fixture.TotalMinutes == 15 && fixture.ExpectedBlockingCodes is null);
        Assert.Contains(Fixtures, fixture => fixture.TotalMinutes == 240 && fixture.ExpectedBlockingCodes is null);
        Assert.Contains(Fixtures, fixture => fixture.Materials.Length == 0 && fixture.ExpectedBlockingCodes is null);
        Assert.Contains(Fixtures, fixture => fixture.AccessRoutes.Length == 0 && fixture.ExpectedBlockingCodes is null);
        Assert.Contains(Fixtures, fixture => fixture.Contingencies is { Length: > 1 });
        Assert.Contains(Fixtures, fixture => fixture.Phases.Count(phase => phase.Check is not null) >= 4);
    }

    [Theory]
    [MemberData(nameof(FixtureIndexes))]
    public async Task Every_fixture_fails_closed_or_approves_and_renders_deterministically(int fixtureIndex)
    {
        var fixture = Fixtures[fixtureIndex];
        var result = LessonLoomBuilder.Build(
            fixture.Title,
            fixture.Target,
            fixture.TotalMinutes,
            fixture.Phases,
            fixture.Materials,
            fixture.AccessRoutes,
            fixture.Contingencies,
            fixture.Language);

        Assert.Equal(
            Sorted(fixture.ExpectedBlockingCodes),
            Sorted(result.Issues
                .Where(issue => issue.Severity == ValidationSeverity.Blocking)
                .Select(issue => issue.Code)));
        Assert.Equal(
            Sorted(fixture.ExpectedWarningCodes),
            Sorted(result.Issues
                .Where(issue => issue.Severity == ValidationSeverity.Warning)
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

        Assert.Equal(fixture.TotalMinutes, fixture.Phases.Sum(phase => phase.Minutes));
        Assert.True(fixture.Phases.Count(phase => phase.Check is not null) >= 2);
        Assert.NotNull(fixture.Phases[^1].Check);
        Assert.All(fixture.Phases.Where(phase => phase.Check is not null),
            phase => Assert.False(string.IsNullOrWhiteSpace(phase.Response)));
        await AssertApprovedRenderingIsDeterministicAsync(fixture, result);
    }

    private static List<Fixture> CreateFixtures()
    {
        var fixtures = new List<Fixture>
        {
            Subject("subject-mathematics", "Mathematics", 45),
            Subject("subject-literacy", "Literacy", 50),
            Subject("subject-science", "Science", 60),
            Subject("subject-history", "History", 40),
            Subject("subject-art", "Art", 45),
            Subject("subject-music", "Music", 30),
            Subject("subject-physical-education", "Physical education", 35),
            Subject("subject-computing", "Computing", 55),
            Subject("subject-engineering", "Engineering", 70),
            Subject("subject-civics", "Civics", 45),

            F("language-arabic", "language-layout", "خطة تجريبية AR-1",
                T("هدف تجريبي AR-1", "دليل تجريبي AR-1"), 45,
                SoundPhases(45, "AR-1", "عمل تجريبي"), language: "ar"),
            F("language-hebrew", "language-layout", "תוכנית ניסוי HE-1",
                T("יעד ניסוי HE-1", "ראיה ניסויית HE-1"), 45,
                SoundPhases(45, "HE-1", "עבודה ניסויית"), language: "he"),
            F("language-japanese", "language-layout", "合成計画 JA-1",
                T("合成目標 JA-1", "合成証拠 JA-1"), 45,
                SoundPhases(45, "JA-1", "合成作業"), language: "ja"),
            F("language-traditional-chinese", "language-layout", "合成計畫 ZH-1",
                T("合成目標 ZH-1", "合成證據 ZH-1"), 45,
                SoundPhases(45, "ZH-1", "合成工作"), language: "zh-Hant"),

            F("duration-fifteen", "duration-edge", "Synthetic 15 minute plan",
                T("Complete synthetic target D-15.", "Produce synthetic evidence D-15."), 15,
                SoundPhases(15, "D-15")),
            F("duration-ninety", "duration-edge", "Synthetic 90 minute plan",
                T("Complete synthetic target D-90.", "Produce synthetic evidence D-90."), 90,
                SoundPhases(90, "D-90")),
            F("duration-one-eighty", "duration-edge", "Synthetic 180 minute plan",
                T("Complete synthetic target D-180.", "Produce synthetic evidence D-180."), 180,
                SoundPhases(180, "D-180")),
            F("duration-two-forty", "duration-edge", "Synthetic 240 minute plan",
                T("Complete synthetic target D-240.", "Produce synthetic evidence D-240."), 240,
                SoundPhases(240, "D-240")),

            F("warning-no-materials", "declared-absence", "Synthetic no-material plan",
                T("Complete synthetic target W-M.", "Produce synthetic evidence W-M."), 45,
                SoundPhases(45, "W-M"), materials: [],
                expectedWarningCodes: ["loom.materials"]),
            F("warning-no-access-routes", "declared-absence", "Synthetic no-access plan",
                T("Complete synthetic target W-A.", "Produce synthetic evidence W-A."), 45,
                SoundPhases(45, "W-A"), accessRoutes: [],
                expectedWarningCodes: ["loom.access"]),
            F("warning-neither-list", "declared-absence", "Synthetic two-warning plan",
                T("Complete synthetic target W-B.", "Produce synthetic evidence W-B."), 45,
                SoundPhases(45, "W-B"), materials: [], accessRoutes: [],
                expectedWarningCodes: ["loom.access", "loom.materials"]),

            F("two-contingencies", "contingency", "Synthetic contingency plan",
                T("Complete synthetic target C-2.", "Produce synthetic evidence C-2."), 45,
                SoundPhases(45, "C-2"),
                contingencies:
                [
                    "Synthetic contingency C-2A preserves the target.",
                    "Synthetic contingency C-2B preserves closure evidence.",
                ]),
            F("four-checks", "decision-breadth", "Synthetic four-check plan",
                T("Complete synthetic target Q-4.", "Produce synthetic evidence Q-4."), 40,
                [
                    P("Launch", 5, "Synthetic work Q-4A.", "Synthetic check Q-4A.", "Synthetic response Q-4A."),
                    P("Model", 10, "Synthetic work Q-4B.", "Synthetic check Q-4B.", "Synthetic response Q-4B."),
                    P("Practice", 20, "Synthetic work Q-4C.", "Synthetic check Q-4C.", "Synthetic response Q-4C."),
                    P("Closure", 5, "Synthetic work Q-4D.", "Synthetic check Q-4D.", "Synthetic response Q-4D."),
                ]),
            F("two-phase-minimum", "decision-breadth", "Synthetic two-phase plan",
                T("Complete synthetic target Q-2.", "Produce synthetic evidence Q-2."), 2,
                [
                    P("Start", 1, "Synthetic work Q-2A.", "Synthetic check Q-2A.", "Synthetic response Q-2A."),
                    P("Close", 1, "Synthetic work Q-2B.", "Synthetic check Q-2B.", "Synthetic response Q-2B."),
                ]),
            F("long-text", "rendering", "Synthetic long-content plan",
                T("Complete synthetic target LONG-1.", "Produce synthetic evidence LONG-1."), 45,
                [
                    P("Launch", 10, $"Synthetic learner work LONG-1 {new string('x', 280)}.", "Synthetic check LONG-1A.", "Synthetic response LONG-1A."),
                    P("Work", 30, $"Synthetic learner work LONG-1 {new string('y', 360)}."),
                    P("Closure", 5, "Synthetic closure LONG-1.", "Synthetic check LONG-1B.", "Synthetic response LONG-1B."),
                ]),
            F("mixed-script", "rendering", "Synthetic α plan MIX-1",
                T("Synthetic target β MIX-1.", "Synthetic evidence γ MIX-1."), 45,
                SoundPhases(45, "MIX-1", "Synthetic عمل 作業")),

            F("refusal-empty-phases", "refusal-structure", "Synthetic empty-phase plan",
                T("Complete synthetic target R-EMPTY.", "Produce synthetic evidence R-EMPTY."), 45, [],
                expectedBlockingCodes: ["doc.table.empty", "loom.checks", "loom.phases"]),
            F("refusal-total-too-high", "refusal-timing", "Synthetic high-total plan",
                T("Complete synthetic target R-HIGH.", "Produce synthetic evidence R-HIGH."), 50,
                SoundPhases(45, "R-HIGH"), expectedBlockingCodes: ["loom.timing"]),
            F("refusal-total-too-low", "refusal-timing", "Synthetic low-total plan",
                T("Complete synthetic target R-LOW.", "Produce synthetic evidence R-LOW."), 40,
                SoundPhases(45, "R-LOW"), expectedBlockingCodes: ["loom.timing"]),
            F("refusal-zero-minute", "refusal-timing", "Synthetic zero-minute plan",
                T("Complete synthetic target R-ZERO.", "Produce synthetic evidence R-ZERO."), 10,
                [
                    P("Start", 0, "Synthetic work R-ZERO-A.", "Synthetic check R-ZERO-A.", "Synthetic response R-ZERO-A."),
                    P("Closure", 10, "Synthetic work R-ZERO-B.", "Synthetic check R-ZERO-B.", "Synthetic response R-ZERO-B."),
                ], expectedBlockingCodes: ["loom.minutes"]),
            F("refusal-negative-minute", "refusal-timing", "Synthetic negative-minute plan",
                T("Complete synthetic target R-NEG.", "Produce synthetic evidence R-NEG."), 10,
                [
                    P("Start", -5, "Synthetic work R-NEG-A.", "Synthetic check R-NEG-A.", "Synthetic response R-NEG-A."),
                    P("Closure", 15, "Synthetic work R-NEG-B.", "Synthetic check R-NEG-B.", "Synthetic response R-NEG-B."),
                ], expectedBlockingCodes: ["loom.minutes"]),
            F("refusal-missing-response", "refusal-evidence", "Synthetic missing-response plan",
                T("Complete synthetic target R-RESP.", "Produce synthetic evidence R-RESP."), 45,
                [
                    P("Launch", 10, "Synthetic work R-RESP-A.", "Synthetic check R-RESP-A."),
                    P("Work", 30, "Synthetic work R-RESP-B."),
                    P("Closure", 5, "Synthetic work R-RESP-C.", "Synthetic check R-RESP-C.", "Synthetic response R-RESP-C."),
                ], expectedBlockingCodes: ["loom.check-response"]),
            F("refusal-blank-response", "refusal-evidence", "Synthetic blank-response plan",
                T("Complete synthetic target R-BLANK.", "Produce synthetic evidence R-BLANK."), 45,
                [
                    P("Launch", 10, "Synthetic work R-BLANK-A.", "Synthetic check R-BLANK-A.", "   "),
                    P("Work", 30, "Synthetic work R-BLANK-B."),
                    P("Closure", 5, "Synthetic work R-BLANK-C.", "Synthetic check R-BLANK-C.", "Synthetic response R-BLANK-C."),
                ], expectedBlockingCodes: ["loom.check-response"]),
            F("refusal-one-check", "refusal-evidence", "Synthetic one-check plan",
                T("Complete synthetic target R-ONE.", "Produce synthetic evidence R-ONE."), 45,
                [
                    P("Launch", 10, "Synthetic work R-ONE-A."),
                    P("Work", 30, "Synthetic work R-ONE-B."),
                    P("Closure", 5, "Synthetic work R-ONE-C.", "Synthetic check R-ONE-C.", "Synthetic response R-ONE-C."),
                ], expectedBlockingCodes: ["loom.checks"]),
            F("refusal-no-closure-evidence", "refusal-evidence", "Synthetic no-closure-check plan",
                T("Complete synthetic target R-CLOSE.", "Produce synthetic evidence R-CLOSE."), 45,
                [
                    P("Launch", 10, "Synthetic work R-CLOSE-A.", "Synthetic check R-CLOSE-A.", "Synthetic response R-CLOSE-A."),
                    P("Work", 30, "Synthetic work R-CLOSE-B.", "Synthetic check R-CLOSE-B.", "Synthetic response R-CLOSE-B."),
                    P("Closure", 5, "Synthetic work R-CLOSE-C."),
                ], expectedBlockingCodes: ["loom.closure"]),
            F("refusal-blank-phase-cells", "refusal-structure", "Synthetic blank-cell plan",
                T("Complete synthetic target R-CELL.", "Produce synthetic evidence R-CELL."), 45,
                [
                    P(" ", 10, " ", "Synthetic check R-CELL-A.", "Synthetic response R-CELL-A."),
                    P("Work", 30, "Synthetic work R-CELL-B."),
                    P("Closure", 5, "Synthetic work R-CELL-C.", "Synthetic check R-CELL-C.", "Synthetic response R-CELL-C."),
                ], expectedBlockingCodes: ["doc.table.blank-cell"]),
        };

        return fixtures;
    }

    private static Fixture Subject(string id, string subject, int totalMinutes)
        => F(
            id,
            "subject-breadth",
            $"Synthetic {subject} plan {id}",
            T($"Complete synthetic {subject} target {id}.", $"Produce synthetic {subject} evidence {id}."),
            totalMinutes,
            SoundPhases(totalMinutes, id));

    private static LessonPhase[] SoundPhases(int totalMinutes, string token, string workPrefix = "Synthetic learner work")
    {
        var launchMinutes = Math.Max(1, totalMinutes / 4);
        var closureMinutes = Math.Max(1, totalMinutes / 5);
        var workMinutes = totalMinutes - launchMinutes - closureMinutes;
        return
        [
            P("Launch", launchMinutes, $"{workPrefix} {token}-A.", $"Synthetic check {token}-A.", $"Synthetic response {token}-A."),
            P("Work", workMinutes, $"{workPrefix} {token}-B."),
            P("Closure", closureMinutes, $"{workPrefix} {token}-C.", $"Synthetic check {token}-C.", $"Synthetic response {token}-C."),
        ];
    }

    private static Fixture F(
        string id,
        string stratum,
        string title,
        LearningTarget target,
        int totalMinutes,
        LessonPhase[] phases,
        string[]? materials = null,
        string[]? accessRoutes = null,
        string[]? contingencies = null,
        string language = "en",
        string[]? expectedBlockingCodes = null,
        string[]? expectedWarningCodes = null)
        => new(
            id,
            stratum,
            title,
            target,
            totalMinutes,
            phases,
            materials ?? ["Synthetic material"],
            accessRoutes ?? ["Synthetic access route preserves the target"],
            contingencies,
            language,
            expectedBlockingCodes,
            expectedWarningCodes);

    private static LearningTarget T(string statement, string evidence) => new(statement, evidence);

    private static LessonPhase P(string name, int minutes, string learnerWork, string? check = null, string? response = null)
        => new(name, minutes, learnerWork, check, response);

    private static string[] Sorted(IEnumerable<string>? values)
        => [.. (values ?? []).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal)];

    private static async Task AssertApprovedRenderingIsDeterministicAsync(Fixture fixture, LessonResult result)
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
        Assert.Contains(WebUtility.HtmlEncode(fixture.Title), learner, StringComparison.Ordinal);
        Assert.Contains(WebUtility.HtmlEncode(fixture.Target.Statement), learner, StringComparison.Ordinal);
        Assert.DoesNotContain(WebUtility.HtmlEncode(fixture.Target.EvidenceOfLearning), learner, StringComparison.Ordinal);
        Assert.Contains(WebUtility.HtmlEncode(fixture.Target.EvidenceOfLearning), teacher, StringComparison.Ordinal);
        Assert.Contains("@page", print, StringComparison.Ordinal);

        foreach (var phase in fixture.Phases)
        {
            Assert.Contains(WebUtility.HtmlEncode(phase.Name), learner, StringComparison.Ordinal);
            Assert.Contains(WebUtility.HtmlEncode(phase.LearnerWork), learner, StringComparison.Ordinal);
        }

        foreach (var contingency in fixture.Contingencies ?? [])
        {
            Assert.DoesNotContain(WebUtility.HtmlEncode(contingency), learner, StringComparison.Ordinal);
            Assert.Contains(WebUtility.HtmlEncode(contingency), teacher, StringComparison.Ordinal);
        }
    }
}
