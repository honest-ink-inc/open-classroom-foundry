// SPDX-License-Identifier: GPL-3.0-or-later
using System.Net;
using System.Text;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.BuiltIn.DirectionsDuet;
using Foundry.Rendering;

namespace Foundry.Tests.InstructionalEvals;

/// <summary>
/// Synthetic structural corpus for Directions Duet (plan §10.5). Target strings
/// are test tokens, not reviewed translations. These fixtures prove alignment,
/// exact-value preservation, refusal, status, and deterministic rendering only.
/// </summary>
public sealed class DirectionsDuetFixtureTests
{
    public sealed record Fixture(
        string Id,
        string Stratum,
        DuetStep[] Steps,
        LockedField[] LockedFields,
        Glossary Glossary,
        string SourceLocale = "en",
        string TargetLocale = "es",
        string? ComprehensionCheck = null,
        bool LockedFieldInventoryReviewed = true,
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
    public void The_corpus_is_thirty_six_distinct_synthetic_cases_across_every_locked_kind()
    {
        Assert.Equal(36, Fixtures.Count);
        Assert.Equal(Fixtures.Count, Fixtures.Select(fixture => fixture.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.True(Fixtures.Select(fixture => fixture.Stratum).Distinct(StringComparer.Ordinal).Count() >= 6);
        Assert.True(Fixtures.Count(fixture => fixture.ExpectedBlockingCodes is { Length: > 0 }) >= 10);
        Assert.True(Fixtures.Select(fixture => fixture.TargetLocale).Distinct(StringComparer.Ordinal).Count() >= 4);
        Assert.Contains(Fixtures, fixture => fixture.LockedFields.Length == 0
            && fixture.LockedFieldInventoryReviewed
            && fixture.ExpectedBlockingCodes is null);
        Assert.Contains(Fixtures, fixture => !fixture.LockedFieldInventoryReviewed);
        Assert.Equal(
            Enum.GetValues<LockedFieldKind>().OrderBy(kind => kind),
            Fixtures.SelectMany(fixture => fixture.LockedFields).Select(field => field.Kind).Distinct().OrderBy(kind => kind));
    }

    [Theory]
    [MemberData(nameof(FixtureIndexes))]
    public async Task Every_fixture_preserves_declared_alignment_and_fails_closed_or_renders(int fixtureIndex)
    {
        var fixture = Fixtures[fixtureIndex];
        var result = DirectionsDuetBuilder.Build(
            $"Synthetic fixture {fixture.Id}",
            fixture.Steps,
            fixture.SourceLocale,
            fixture.TargetLocale,
            fixture.Glossary,
            fixture.LockedFields,
            fixture.LockedFieldInventoryReviewed,
            comprehensionCheck: fixture.ComprehensionCheck);

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
        Assert.Equal(fixture.SourceLocale, result.Document.Language);
        Assert.Equal(
            fixture.Steps,
            result.Document.Nodes.OfType<BilingualPair>()
                .Select(pair => new DuetStep(pair.SourceText, pair.TargetText)));

        var status = result.Document.Nodes.OfType<TeacherOnlyNotice>()
            .Single(notice => notice.Text.Contains("Translation status", StringComparison.Ordinal));
        Assert.Contains($"Working glossary {fixture.Glossary.Version}", status.Text, StringComparison.Ordinal);
        Assert.Contains("not approved by this application", status.Text, StringComparison.Ordinal);
        Assert.Contains("NOT yet language-reviewed", status.Text, StringComparison.Ordinal);

        if (expectedCodes.Length > 0)
        {
            Assert.Throws<InvalidOperationException>(() => ApprovalGate.Approve(
                DraftArtifact.New(result.Document, DataLane.Green),
                "synthetic-corpus@example.invalid",
                result.Issues,
                SomeInstant));
            return;
        }

        AssertDeclaredRowParity(fixture);
        await AssertApprovedRenderingIsDeterministicAsync(fixture, result);
    }

    private static List<Fixture> CreateFixtures()
    {
        var kindTokens = new (LockedFieldKind Kind, string Token)[]
        {
            (LockedFieldKind.Date, "DATE-2026-09-08"),
            (LockedFieldKind.Number, "N=17"),
            (LockedFieldKind.ProperName, "NAME-A"),
            (LockedFieldKind.Negation, "NOT-TOKEN"),
            (LockedFieldKind.Quotation, "\"QUOTE-X\""),
            (LockedFieldKind.Citation, "CITE-47"),
            (LockedFieldKind.Unit, "25-mL"),
            (LockedFieldKind.Url, "https://example.invalid/a"),
            (LockedFieldKind.Condition, "IF-TOKEN"),
            (LockedFieldKind.RightsMetadata, "CC0-SYNTHETIC"),
        };

        var fixtures = kindTokens.Select(item => F(
            $"kind-{item.Kind.ToString().ToLowerInvariant()}",
            "locked-kind",
            [P(
                $"Synthetic source keeps {item.Token}{(item.Kind == LockedFieldKind.Url ? string.Empty : ".")}",
                $"Synthetic target keeps {item.Token}{(item.Kind == LockedFieldKind.Url ? string.Empty : ".")}")],
            [L(item.Kind, item.Token)])).ToList();

        fixtures.AddRange(
        [
            F("multi-two-locks", "valid-alignment",
                [P("Source A LOCK-A.", "Target A LOCK-A."), P("Source B LOCK-B.", "Target B LOCK-B.")],
                [L(LockedFieldKind.Number, "LOCK-A"), L(LockedFieldKind.Number, "LOCK-B")]),
            F("multi-shared-lock", "valid-alignment",
                [P("Source A SHARED.", "Target A SHARED."), P("Source B SHARED.", "Target B SHARED.")],
                [L(LockedFieldKind.Condition, "SHARED")]),
            F("same-row-repeat", "valid-alignment",
                [P("REPEAT then REPEAT.", "Target keeps REPEAT once."), P("Plain source.", "Plain target.")],
                [L(LockedFieldKind.Number, "REPEAT")]),
            F("no-lock-bearing-fact", "valid-alignment",
                [P("Synthetic source action.", "Synthetic target action.")]),
            F("refusal-inventory-unreviewed", "refusal-boundary",
                [P("Synthetic source action.", "Synthetic target action.")],
                lockedFieldInventoryReviewed: false,
                expectedBlockingCodes: ["locked.inventory-review-required"]),
            F("comprehension-check", "valid-alignment",
                [P("Source keeps CHECK-1.", "Target keeps CHECK-1.")],
                [L(LockedFieldKind.Number, "CHECK-1")], comprehensionCheck: "Point to synthetic card A."),
            F("long-target", "layout",
                [P("Source keeps LONG-1.", $"Synthetic target keeps LONG-1 and {new string('x', 320)}.")],
                [L(LockedFieldKind.Number, "LONG-1")]),
            F("rtl-ar", "layout",
                [P("Synthetic source keeps RTL-A.", "نص تجريبي RTL-A")],
                [L(LockedFieldKind.ProperName, "RTL-A")], targetLocale: "ar"),
            F("rtl-he", "layout",
                [P("Synthetic source keeps RTL-H.", "טקסט ניסוי RTL-H")],
                [L(LockedFieldKind.ProperName, "RTL-H")], targetLocale: "he"),
            F("cjk-ja", "layout",
                [P("Synthetic source keeps CJK-J.", "テスト CJK-J")],
                [L(LockedFieldKind.ProperName, "CJK-J")], targetLocale: "ja"),
            F("mixed-script", "layout",
                [P("Synthetic α keeps MIX-1.", "Synthetic β keeps MIX-1.")],
                [L(LockedFieldKind.Number, "MIX-1")]),
            F("glossary-valid", "glossary",
                [P("Source uses SRC-TERM.", "Target uses TGT-TERM.")],
                glossary: G("g-valid", "SRC-TERM", "TGT-TERM")),
            F("glossary-source-case", "glossary",
                [P("Source uses src-term.", "Target uses tgt-term.")],
                glossary: G("g-case", "SRC-TERM", "TGT-TERM")),
            F("duplicate-lock-declaration", "valid-alignment",
                [P("Source keeps DUP-1.", "Target keeps DUP-1.")],
                [L(LockedFieldKind.Number, "DUP-1"), L(LockedFieldKind.Number, "DUP-1")]),
            F("refusal-source-only", "refusal-alignment",
                [P("Source keeps ONLY-S.", "Target omits it.")],
                [L(LockedFieldKind.Number, "ONLY-S")], expectedBlockingCodes: ["duet.locked"]),
            F("refusal-target-only", "refusal-alignment",
                [P("Source omits it.", "Target adds ONLY-T.")],
                [L(LockedFieldKind.Number, "ONLY-T")], expectedBlockingCodes: ["duet.locked"]),
            F("refusal-swapped-two", "refusal-alignment",
                [P("Source A LOCK-A.", "Target A LOCK-B."), P("Source B LOCK-B.", "Target B LOCK-A.")],
                [L(LockedFieldKind.Number, "LOCK-A"), L(LockedFieldKind.Number, "LOCK-B")],
                expectedBlockingCodes: ["duet.locked"]),
            F("refusal-moved-single", "refusal-alignment",
                [P("Source A MOVE-1.", "Target A plain."), P("Source B plain.", "Target B MOVE-1.")],
                [L(LockedFieldKind.Condition, "MOVE-1")], expectedBlockingCodes: ["duet.locked"]),
            F("refusal-repeated-row-set", "refusal-alignment",
                [P("Source A MULTI.", "Target A MULTI."), P("Source B MULTI.", "Target B plain.")],
                [L(LockedFieldKind.Condition, "MULTI")], expectedBlockingCodes: ["duet.locked"]),
            F("refusal-declared-missing", "refusal-boundary",
                [P("Source plain.", "Target plain.")],
                [L(LockedFieldKind.Date, "ABSENT-DATE")], expectedBlockingCodes: ["duet.locked"]),
            F("refusal-blank-lock", "refusal-boundary",
                [P("Source has spaces.", "Target has spaces.")],
                [L(LockedFieldKind.Url, " ")], expectedBlockingCodes: ["locked.empty"]),
            F("refusal-missing-target", "refusal-boundary",
                [P("Source action.", " ")], expectedBlockingCodes: ["doc.bilingual.target", "duet.target-missing"]),
            F("refusal-empty-steps", "refusal-boundary", [], expectedBlockingCodes: ["duet.empty"]),
            F("refusal-glossary", "glossary",
                [P("Source uses SRC-TERM.", "Target omits the required token.")],
                glossary: G("g-refusal", "SRC-TERM", "TGT-TERM"), expectedBlockingCodes: ["duet.glossary"]),
            F("refusal-glossary-second-row", "glossary",
                [P("First source.", "First target."), P("SRC-TERM appears here.", "Required token is absent here.")],
                glossary: G("g-row", "SRC-TERM", "TGT-TERM"), expectedBlockingCodes: ["duet.glossary"]),
            F("refusal-lock-and-target", "refusal-boundary",
                [P("Source keeps LOCK-Z.", " ")],
                [L(LockedFieldKind.Number, "LOCK-Z")],
                expectedBlockingCodes: ["doc.bilingual.target", "duet.locked", "duet.target-missing"]),
        ]);

        return fixtures;
    }

    private static void AssertDeclaredRowParity(Fixture fixture)
    {
        foreach (var field in fixture.LockedFields)
        {
            Assert.False(string.IsNullOrWhiteSpace(field.ExactValue));
            var sourceRows = fixture.Steps.Select((step, index) => (step, index))
                .Where(pair => pair.step.SourceText.Contains(field.ExactValue, StringComparison.Ordinal))
                .Select(pair => pair.index);
            var targetRows = fixture.Steps.Select((step, index) => (step, index))
                .Where(pair => pair.step.TargetText.Contains(field.ExactValue, StringComparison.Ordinal))
                .Select(pair => pair.index);
            Assert.Equal(sourceRows, targetRows);
        }
    }

    private static async Task AssertApprovedRenderingIsDeterministicAsync(Fixture fixture, DuetResult result)
    {
        var approved = ApprovalGate.Approve(
            DraftArtifact.New(result.Document, DataLane.Green),
            "synthetic-corpus@example.invalid",
            result.Issues,
            SomeInstant);
        var renderer = new AccessibleHtmlRenderer();
        var request = new RenderRequest(RenderTarget.AccessibleHtml);
        var first = await renderer.RenderAsync(approved, request, CancellationToken.None);
        var second = await renderer.RenderAsync(approved, request, CancellationToken.None);
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
        Assert.Contains($"<html lang=\"{fixture.SourceLocale}\"", learner, StringComparison.Ordinal);
        Assert.Contains($"lang=\"{fixture.TargetLocale}\"", learner, StringComparison.Ordinal);
        Assert.Contains("dir=\"auto\"", learner, StringComparison.Ordinal);
        Assert.Contains("@page", print, StringComparison.Ordinal);
        Assert.DoesNotContain("NOT yet language-reviewed", learner, StringComparison.Ordinal);
        Assert.Contains("NOT yet language-reviewed", teacher, StringComparison.Ordinal);

        foreach (var step in fixture.Steps)
        {
            Assert.Contains(WebUtility.HtmlEncode(step.SourceText), learner, StringComparison.Ordinal);
            Assert.Contains(WebUtility.HtmlEncode(step.TargetText), learner, StringComparison.Ordinal);
        }
    }

    private static Fixture F(
        string id,
        string stratum,
        DuetStep[] steps,
        LockedField[]? lockedFields = null,
        Glossary? glossary = null,
        string sourceLocale = "en",
        string targetLocale = "es",
        string? comprehensionCheck = null,
        bool lockedFieldInventoryReviewed = true,
        string[]? expectedBlockingCodes = null)
        => new(
            id,
            stratum,
            steps,
            lockedFields ?? [],
            glossary ?? Glossary.Empty,
            sourceLocale,
            targetLocale,
            comprehensionCheck,
            lockedFieldInventoryReviewed,
            expectedBlockingCodes);

    private static DuetStep P(string source, string target) => new(source, target);

    private static LockedField L(LockedFieldKind kind, string exactValue) => new(kind, exactValue);

    private static Glossary G(string version, string source, string target)
        => new(version, [new GlossaryEntry(source, target)]);
}
