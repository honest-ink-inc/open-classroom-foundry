// SPDX-License-Identifier: GPL-3.0-or-later
using System.Net;
using System.Text;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.BuiltIn.TalkMoves;
using Foundry.Rendering;

namespace Foundry.Tests.InstructionalEvals;

/// <summary>
/// Synthetic structural corpus for Forumwright (stable recipe id
/// <c>talk-moves-studio</c>; plan §10.9). It proves the implemented question
/// map, participation-mode, facilitation-move, optional-frame, audience, and
/// deterministic-rendering contracts. It does not prove discussion quality,
/// sensitive-topic safety, available-time fit, curriculum review, language
/// review, accessibility, AAC/SLP review, or teacher usability.
/// </summary>
public sealed class TalkMovesFixtureTests
{
    public sealed record Fixture(
        string Id,
        string Stratum,
        string Topic,
        DiscussionQuestion[] Questions,
        string[] ParticipationModes,
        string InviteMove,
        string BuildMove,
        string PressForEvidenceMove,
        string RepairMove,
        string SynthesizeMove,
        string[]? SentenceFrames = null,
        string Language = "en",
        string[]? ExpectedBlockingCodes = null,
        string? MissingMoveFamily = null)
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
    public void The_corpus_is_thirty_eight_distinct_synthetic_cases_across_question_mode_frame_script_and_refusal_strata()
    {
        Assert.Equal(38, Fixtures.Count);
        Assert.Equal(Fixtures.Count, Fixtures.Select(fixture => fixture.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.True(Fixtures.Select(fixture => fixture.Stratum).Distinct(StringComparer.Ordinal).Count() >= 8);
        Assert.Equal(12, Fixtures.Count(fixture => fixture.ExpectedBlockingCodes is { Length: > 0 }));
        Assert.True(Fixtures.Select(fixture => fixture.Language).Distinct(StringComparer.Ordinal).Count() >= 5);
        Assert.Contains(Fixtures, fixture => fixture.Questions.Length >= 5 && fixture.ExpectedBlockingCodes is null);
        Assert.Contains(Fixtures, fixture => fixture.ParticipationModes.Length >= 8 && fixture.ExpectedBlockingCodes is null);
        Assert.Contains(Fixtures, fixture => fixture.SentenceFrames is null && fixture.ExpectedBlockingCodes is null);
        Assert.Contains(Fixtures, fixture => fixture.SentenceFrames is { Length: 0 } && fixture.ExpectedBlockingCodes is null);
        Assert.Contains(Fixtures, fixture => fixture.SentenceFrames is { Length: >= 3 } && fixture.ExpectedBlockingCodes is null);
        Assert.Equal(
            ["build", "invite", "press for evidence", "repair", "synthesize"],
            Fixtures.Where(fixture => fixture.MissingMoveFamily is not null)
                .Select(fixture => fixture.MissingMoveFamily)
                .OrderBy(family => family, StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(FixtureIndexes))]
    public async Task Every_fixture_fails_closed_or_preserves_the_implemented_discussion_contract_and_renders_deterministically(int fixtureIndex)
    {
        var fixture = Fixtures[fixtureIndex];
        var result = TalkMovesBuilder.Build(
            fixture.Topic,
            fixture.Questions,
            fixture.ParticipationModes,
            fixture.InviteMove,
            fixture.BuildMove,
            fixture.PressForEvidenceMove,
            fixture.RepairMove,
            fixture.SynthesizeMove,
            fixture.SentenceFrames,
            fixture.Language);

        Assert.Equal(
            Sorted(fixture.ExpectedBlockingCodes),
            Sorted(result.Issues
                .Where(issue => issue.Severity == ValidationSeverity.Blocking)
                .Select(issue => issue.Code)));
        Assert.Equal(fixture.Language, result.Document.Language);

        if (fixture.MissingMoveFamily is { } family)
        {
            Assert.Contains(
                result.Issues,
                issue => issue.Code == "talk.moves"
                    && issue.Message.Contains($"'{family}'", StringComparison.Ordinal));
        }

        if (fixture.ExpectedBlockingCodes is { Length: > 0 })
        {
            Assert.Throws<InvalidOperationException>(() => ApprovalGate.Approve(
                DraftArtifact.New(result.Document, DataLane.Green),
                "synthetic-corpus@example.invalid",
                result.Issues,
                SomeInstant));
            return;
        }

        Assert.True(fixture.Questions.Length >= 1);
        Assert.True(fixture.ParticipationModes.Length >= 3);
        Assert.All(fixture.Questions, question =>
        {
            Assert.False(string.IsNullOrWhiteSpace(question.Question));
            Assert.False(string.IsNullOrWhiteSpace(question.Purpose));
            Assert.False(string.IsNullOrWhiteSpace(question.EvidenceTarget));
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
                "موضوع تجريبي AR-1",
                [Q("AR-1", "سؤال تجريبي AR-1؟", "غرض تجريبي AR-1", "دليل تجريبي AR-1")],
                language: "ar"),
            F(
                "language-hebrew",
                "language-layout",
                "נושא ניסויי HE-1",
                [Q("HE-1", "שאלה ניסויית HE-1?", "מטרה ניסויית HE-1", "ראיה ניסויית HE-1")],
                language: "he"),
            F(
                "language-japanese",
                "language-layout",
                "合成話題 JA-1",
                [Q("JA-1", "合成質問 JA-1？", "合成目的 JA-1", "合成証拠 JA-1")],
                language: "ja"),
            F(
                "language-traditional-chinese",
                "language-layout",
                "合成主題 ZH-1",
                [Q("ZH-1", "合成問題 ZH-1？", "合成目的 ZH-1", "合成證據 ZH-1")],
                language: "zh-Hant"),

            F("questions-one", "question-depth", "Synthetic one-question discussion Q-1", [Q("Q-1")]),
            F("questions-three", "question-depth", "Synthetic three-question discussion Q-3", Questions("Q-3", 3)),
            F("questions-five", "question-depth", "Synthetic five-question discussion Q-5", Questions("Q-5", 5)),

            F(
                "modes-minimum-three",
                "mode-breadth",
                "Synthetic three-mode discussion M-3",
                [Q("M-3")],
                participationModes: ["Speak M-3", "Write M-3", "Point M-3"]),
            F(
                "modes-five",
                "mode-breadth",
                "Synthetic five-mode discussion M-5",
                [Q("M-5")],
                participationModes: ["Speak M-5", "Write M-5", "Point M-5", "Draw M-5", "Partner-supported M-5"]),
            F(
                "modes-eight",
                "mode-breadth",
                "Synthetic eight-mode discussion M-8",
                [Q("M-8")],
                participationModes:
                [
                    "Speak M-8",
                    "Write M-8",
                    "Point M-8",
                    "Draw M-8",
                    "Select a teacher-offered card M-8",
                    "Use an established AAC response M-8",
                    "Partner-supported response M-8",
                    "Request more processing time M-8",
                ]),

            F("frames-none", "optional-frames", "Synthetic no-frame discussion F-0", [Q("F-0")]),
            F(
                "frames-empty",
                "optional-frames",
                "Synthetic reviewed-empty-frame discussion F-E",
                [Q("F-E")],
                sentenceFrames: []),
            F(
                "frames-three",
                "optional-frames",
                "Synthetic three-frame discussion F-3",
                [Q("F-3")],
                sentenceFrames:
                [
                    "One synthetic possibility F-3 is ...",
                    "My synthetic evidence F-3 is ...",
                    "I want to revise synthetic idea F-3 because ...",
                ]),

            F(
                "rendering-long-text",
                "rendering",
                $"Synthetic long discussion LONG-1 {new string('t', 220)}",
                [Q("LONG-1", $"What does synthetic marker LONG-1 {new string('q', 320)} show?")]),
            F(
                "rendering-markup-like",
                "rendering",
                "Synthetic <topic> & discussion MARK-1",
                [Q("MARK-1", "Does synthetic <claim> & marker MARK-1 remain literal?")],
                sentenceFrames: ["Synthetic <frame> & token MARK-1 remains optional."]),
            F(
                "rendering-mixed-script",
                "rendering",
                "Synthetic α موضوع 合成 topic MIX-1",
                [Q("MIX-1", "Synthetic سؤال β 合成問題 MIX-1?")],
                participationModes: ["Speak α MIX-1", "Write موضوع MIX-1", "Point 合成 MIX-1"]),

            F(
                "refusal-no-questions",
                "refusal-question-map",
                "Synthetic no-question discussion R-Q0",
                [],
                expectedBlockingCodes: ["doc.list.empty", "talk.questions"]),
            F(
                "refusal-blank-question",
                "refusal-question-map",
                "Synthetic blank-question discussion R-QQ",
                [Q("R-QQ", question: " ")],
                expectedBlockingCodes: ["doc.list.blank-item", "talk.question-map"]),
            F(
                "refusal-blank-purpose",
                "refusal-question-map",
                "Synthetic blank-purpose discussion R-QP",
                [Q("R-QP", purpose: " ")],
                expectedBlockingCodes: ["talk.question-map"]),
            F(
                "refusal-blank-evidence",
                "refusal-question-map",
                "Synthetic blank-evidence discussion R-QE",
                [Q("R-QE", evidence: " ")],
                expectedBlockingCodes: ["talk.question-map"]),
            F(
                "refusal-two-modes",
                "refusal-modes",
                "Synthetic two-mode discussion R-M2",
                [Q("R-M2")],
                participationModes: ["Speak R-M2", "Write R-M2"],
                expectedBlockingCodes: ["talk.modes"]),
            F(
                "refusal-duplicate-modes",
                "refusal-modes",
                "Synthetic duplicate-mode discussion R-MD",
                [Q("R-MD")],
                participationModes: ["Write R-MD", " write r-md ", "WRITE R-MD"],
                expectedBlockingCodes: ["talk.modes"]),
            F(
                "refusal-reserved-pass-mode",
                "refusal-modes",
                "Synthetic reserved-pass discussion R-MP",
                [Q("R-MP")],
                participationModes: ["Speak R-MP", "Write R-MP", "Point R-MP", TalkMovesBuilder.PassOption],
                expectedBlockingCodes: ["talk.modes"]),
            F(
                "refusal-missing-invite",
                "refusal-moves",
                "Synthetic missing-invite discussion R-I",
                [Q("R-I")],
                inviteMove: " ",
                expectedBlockingCodes: ["talk.moves"],
                missingMoveFamily: "invite"),
            F(
                "refusal-missing-build",
                "refusal-moves",
                "Synthetic missing-build discussion R-B",
                [Q("R-B")],
                buildMove: " ",
                expectedBlockingCodes: ["talk.moves"],
                missingMoveFamily: "build"),
            F(
                "refusal-missing-press",
                "refusal-moves",
                "Synthetic missing-press discussion R-P",
                [Q("R-P")],
                pressForEvidenceMove: " ",
                expectedBlockingCodes: ["talk.moves"],
                missingMoveFamily: "press for evidence"),
            F(
                "refusal-missing-repair",
                "refusal-moves",
                "Synthetic missing-repair discussion R-R",
                [Q("R-R")],
                repairMove: " ",
                expectedBlockingCodes: ["talk.moves"],
                missingMoveFamily: "repair"),
            F(
                "refusal-missing-synthesize",
                "refusal-moves",
                "Synthetic missing-synthesis discussion R-S",
                [Q("R-S")],
                synthesizeMove: " ",
                expectedBlockingCodes: ["talk.moves"],
                missingMoveFamily: "synthesize"),
        };

        return fixtures;
    }

    private static Fixture Subject(string id, string subject)
        => F(
            id,
            "subject-breadth",
            $"Synthetic {subject} discussion {id}",
            [Q(id, $"What does synthetic {subject} evidence {id} show?")]);

    private static DiscussionQuestion[] Questions(string token, int count)
        => [.. Enumerable.Range(1, count).Select(index => Q($"{token}-{index}"))];

    private static DiscussionQuestion Q(
        string token,
        string? question = null,
        string? purpose = null,
        string? evidence = null)
        => new(
            question ?? $"What does synthetic evidence {token} show?",
            purpose ?? $"Compare synthetic claims {token}.",
            evidence ?? $"Synthetic evidence marker {token}.");

    private static Fixture F(
        string id,
        string stratum,
        string topic,
        DiscussionQuestion[] questions,
        string[]? participationModes = null,
        string? inviteMove = null,
        string? buildMove = null,
        string? pressForEvidenceMove = null,
        string? repairMove = null,
        string? synthesizeMove = null,
        string[]? sentenceFrames = null,
        string language = "en",
        string[]? expectedBlockingCodes = null,
        string? missingMoveFamily = null)
        => new(
            id,
            stratum,
            topic,
            questions,
            participationModes ?? ["Speak a synthetic response", "Write a synthetic response", "Point to synthetic evidence"],
            inviteMove ?? $"Invite a mode choice {id}.",
            buildMove ?? $"Build on a synthetic idea {id}.",
            pressForEvidenceMove ?? $"Press for synthetic evidence {id}.",
            repairMove ?? $"Repair a synthetic misunderstanding {id}.",
            synthesizeMove ?? $"Synthesize synthetic claims {id}.",
            sentenceFrames,
            language,
            expectedBlockingCodes,
            missingMoveFamily);

    private static string[] Sorted(IEnumerable<string>? values)
        => [.. (values ?? []).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal)];

    private static void AssertImplementedDocumentShape(Fixture fixture, ArtifactDocument document)
    {
        Assert.Equal(fixture.Topic, Assert.IsType<Heading>(document.Nodes[0]).Text);

        var lists = document.Nodes.OfType<UnorderedList>().ToArray();
        var expectedListCount = fixture.SentenceFrames is { Length: > 0 } ? 3 : 2;
        Assert.Equal(expectedListCount, lists.Length);
        Assert.Equal(fixture.Questions.Select(question => question.Question), lists[0].Items);
        Assert.Equal([.. fixture.ParticipationModes, TalkMovesBuilder.PassOption], lists[1].Items);
        Assert.Equal(1, lists[1].Items.Count(item => item == TalkMovesBuilder.PassOption));
        Assert.Equal(TalkMovesBuilder.PassOption, lists[1].Items[^1]);

        if (fixture.SentenceFrames is { Length: > 0 } frames)
        {
            Assert.Equal(frames, lists[2].Items);
            Assert.Contains(
                document.Nodes.OfType<Heading>(),
                heading => heading.Text.Contains("optional", StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            Assert.DoesNotContain(
                document.Nodes.OfType<Heading>(),
                heading => heading.Text.Contains("Sentence starters", StringComparison.Ordinal));
        }

        var notices = document.Nodes.OfType<TeacherOnlyNotice>().ToArray();
        Assert.Equal(fixture.Questions.Length + 2, notices.Length);
        Assert.All(fixture.Questions, question => Assert.Contains(
            notices,
            notice => notice.Text.Contains($"Purpose: {question.Purpose}", StringComparison.Ordinal)
                && notice.Text.Contains($"Evidence to press for: {question.EvidenceTarget}", StringComparison.Ordinal)));
        Assert.Contains(notices, notice => notice.Text.Contains("Facilitation moves", StringComparison.Ordinal));
        Assert.Contains(notices, notice => notice.Text.Contains("Equity reflection", StringComparison.Ordinal));
    }

    private static async Task AssertApprovedRenderingIsDeterministicAsync(Fixture fixture, TalkMovesResult result)
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
        Assert.Contains(WebUtility.HtmlEncode(fixture.Topic), learner, StringComparison.Ordinal);
        Assert.Contains(WebUtility.HtmlEncode(TalkMovesBuilder.PassOption), learner, StringComparison.Ordinal);
        Assert.DoesNotContain("Equity reflection", learner, StringComparison.Ordinal);
        Assert.DoesNotContain("Facilitation moves", learner, StringComparison.Ordinal);
        Assert.Contains("Equity reflection", teacher, StringComparison.Ordinal);
        Assert.Contains("Facilitation moves", teacher, StringComparison.Ordinal);
        Assert.Contains("@page", print, StringComparison.Ordinal);

        foreach (var question in fixture.Questions)
        {
            Assert.Contains(WebUtility.HtmlEncode(question.Question), learner, StringComparison.Ordinal);
            Assert.DoesNotContain(WebUtility.HtmlEncode(question.Purpose), learner, StringComparison.Ordinal);
            Assert.DoesNotContain(WebUtility.HtmlEncode(question.EvidenceTarget), learner, StringComparison.Ordinal);
            Assert.Contains(WebUtility.HtmlEncode(question.Purpose), teacher, StringComparison.Ordinal);
            Assert.Contains(WebUtility.HtmlEncode(question.EvidenceTarget), teacher, StringComparison.Ordinal);
        }

        foreach (var mode in fixture.ParticipationModes)
        {
            Assert.Contains(WebUtility.HtmlEncode(mode), learner, StringComparison.Ordinal);
        }

        foreach (var frame in fixture.SentenceFrames ?? [])
        {
            Assert.Contains(WebUtility.HtmlEncode(frame), learner, StringComparison.Ordinal);
        }

        foreach (var move in new[]
        {
            fixture.InviteMove,
            fixture.BuildMove,
            fixture.PressForEvidenceMove,
            fixture.RepairMove,
            fixture.SynthesizeMove,
        })
        {
            Assert.DoesNotContain(WebUtility.HtmlEncode(move), learner, StringComparison.Ordinal);
            Assert.Contains(WebUtility.HtmlEncode(move), teacher, StringComparison.Ordinal);
        }
    }
}
