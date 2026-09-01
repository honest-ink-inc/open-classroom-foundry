// SPDX-License-Identifier: GPL-3.0-or-later
using System.Net;
using System.Text;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.BuiltIn.DirectionsDuet;
using Foundry.Modules.BuiltIn.FamilyBridge;
using Foundry.Rendering;

namespace Foundry.Tests.InstructionalEvals;

/// <summary>
/// Wholly synthetic structural corpus for KinDispatch (stable recipe id
/// <c>family-bridge</c>; plan §10.10). Target strings are conspicuous test
/// tokens, not translations. Generic artifact approval of a bilingual draft is
/// not language review. These fixtures prove only current literal-preservation,
/// structure, validation, audience-filtering, and deterministic-rendering
/// behavior; they confer no language, accessibility, family, or protected-seat
/// approval.
/// </summary>
public sealed class FamilyBridgeFixtureTests
{
    public enum FixtureDisposition
    {
        SourceValid,
        RenderableUnreviewedDraft,
        Blocking,
    }

    public sealed record Fixture(
        string Id,
        FixtureDisposition Disposition,
        string Title,
        BridgeParagraph[] Paragraphs,
        string RequestedAction,
        string Contact,
        Glossary Glossary,
        LockedField[] LockedFields,
        bool LockedFieldInventoryReviewed = true,
        string? Deadline = null,
        string SourceLocale = "en",
        string? TargetLocale = null,
        string? TargetRequestedAction = null,
        string? TargetContact = null,
        string? TargetDeadline = null,
        string[]? ExpectedBlockingCodes = null,
        string[]? ExpectedWarningCodes = null)
    {
        public override string ToString() => $"{Id} [{Disposition}]";
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
        Assert.Equal(9, Fixtures.Count(fixture => fixture.Disposition == FixtureDisposition.SourceValid));
        Assert.Equal(9, Fixtures.Count(fixture => fixture.Disposition == FixtureDisposition.RenderableUnreviewedDraft));
        Assert.Equal(18, Fixtures.Count(fixture => fixture.Disposition == FixtureDisposition.Blocking));
        Assert.Equal(
            Enum.GetValues<LockedFieldKind>().OrderBy(kind => kind),
            Fixtures
                .Where(fixture => fixture.Disposition == FixtureDisposition.SourceValid)
                .SelectMany(fixture => fixture.LockedFields)
                .Select(field => field.Kind)
                .Distinct()
                .OrderBy(kind => kind));
        Assert.Contains(Fixtures, fixture => fixture.Disposition == FixtureDisposition.SourceValid
            && fixture.LockedFieldInventoryReviewed
            && fixture.LockedFields.Length == 0);
        Assert.Equal(
            ["ar", "es", "he", "ja", "zh-Hant"],
            Fixtures
                .Where(fixture => fixture.Disposition == FixtureDisposition.RenderableUnreviewedDraft)
                .Select(fixture => fixture.TargetLocale)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(locale => locale, StringComparer.Ordinal));
        Assert.Single(Fixtures, fixture => fixture.ExpectedWarningCodes is ["bridge.actions"]);

        Assert.Equal("family-bridge", FamilyBridgeBuilder.Recipe.Id);
        Assert.Equal("0.1.0", FamilyBridgeBuilder.Recipe.Version);
        Assert.Equal("schema.family-bridge.v1", FamilyBridgeBuilder.Recipe.OutputSchemaId);
        Assert.Equal("0.1", FamilyBridgeBuilder.Recipe.EvaluationSuiteVersion);
    }

    [Theory]
    [MemberData(nameof(FixtureIndexes))]
    public async Task Every_fixture_blocks_or_preserves_the_current_structure_and_renders_deterministically(int fixtureIndex)
    {
        var fixture = Fixtures[fixtureIndex];
        var result = FamilyBridgeBuilder.Build(
            fixture.Title,
            fixture.Paragraphs,
            fixture.RequestedAction,
            fixture.Contact,
            fixture.Glossary,
            fixture.LockedFields,
            fixture.LockedFieldInventoryReviewed,
            fixture.Deadline,
            fixture.SourceLocale,
            fixture.TargetLocale,
            reviewedBy: null,
            fixture.TargetRequestedAction,
            fixture.TargetContact,
            fixture.TargetDeadline);

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
        Assert.Equal(fixture.SourceLocale, result.Document.Language);

        if (fixture.Disposition == FixtureDisposition.Blocking)
        {
            Assert.NotEmpty(fixture.ExpectedBlockingCodes ?? []);
            Assert.Throws<InvalidOperationException>(() => ApprovalGate.Approve(
                DraftArtifact.New(result.Document, DataLane.Green),
                "synthetic-corpus@example.invalid",
                result.Issues,
                SomeInstant));
            return;
        }

        Assert.Empty(fixture.ExpectedBlockingCodes ?? []);
        AssertImplementedDocumentShape(fixture, result.Document);
        await AssertGenericApprovalAndRenderingAreDeterministicAsync(fixture, result);
    }

    private static List<Fixture> CreateFixtures()
        =>
        [
            F(
                "source-reviewed-empty-inventory",
                FixtureDisposition.SourceValid,
                [P("Synthetic source notice READY-1.")]),
            F(
                "source-date-number",
                FixtureDisposition.SourceValid,
                [P("Synthetic schedule keeps DATE-10 and N=17.")],
                deadline: "DATE-10",
                lockedFields:
                [
                    L(LockedFieldKind.Date, "DATE-10"),
                    L(LockedFieldKind.Number, "N=17"),
                ]),
            F(
                "source-proper-name",
                FixtureDisposition.SourceValid,
                [P("Synthetic office notice.")],
                contact: "OFFICE-9",
                lockedFields: [L(LockedFieldKind.ProperName, "OFFICE-9")]),
            F(
                "source-negation",
                FixtureDisposition.SourceValid,
                [P("Synthetic action notice.")],
                requestedAction: "Keep NOT-TOKEN unchanged.",
                lockedFields: [L(LockedFieldKind.Negation, "NOT-TOKEN")]),
            F(
                "source-quotation",
                FixtureDisposition.SourceValid,
                [P("Synthetic record includes \"QUOTE-X\" exactly.")],
                lockedFields: [L(LockedFieldKind.Quotation, "\"QUOTE-X\"")]),
            F(
                "source-citation",
                FixtureDisposition.SourceValid,
                [P("Synthetic source cites CITE-47.")],
                lockedFields: [L(LockedFieldKind.Citation, "CITE-47")]),
            F(
                "source-unit-url",
                FixtureDisposition.SourceValid,
                [P("Use 25-mL at https://example.invalid/a")],
                lockedFields:
                [
                    L(LockedFieldKind.Unit, "25-mL"),
                    L(LockedFieldKind.Url, "https://example.invalid/a"),
                ]),
            F(
                "source-condition",
                FixtureDisposition.SourceValid,
                [P("Synthetic condition notice.")],
                requestedAction: "Return the item IF-TOKEN.",
                lockedFields: [L(LockedFieldKind.Condition, "IF-TOKEN")]),
            F(
                "source-rights-metadata",
                FixtureDisposition.SourceValid,
                [P("Synthetic rights marker CC0-SYNTHETIC.")],
                lockedFields: [L(LockedFieldKind.RightsMetadata, "CC0-SYNTHETIC")]),

            F(
                "draft-es-glossary",
                FixtureDisposition.RenderableUnreviewedDraft,
                [P("Synthetic SRC-TERM notice.", "TARGET-ES TGT-TERM.")],
                glossary: G("synthetic-g1", "SRC-TERM", "TGT-TERM"),
                targetLocale: "es",
                targetRequestedAction: "TARGET-ES-ACTION",
                targetContact: "TARGET-ES-CONTACT"),
            F(
                "draft-ar",
                FixtureDisposition.RenderableUnreviewedDraft,
                [P("Synthetic message AR-1.", "رمز-اختبار AR-1")],
                targetLocale: "ar",
                targetRequestedAction: "رمز-إجراء AR-A",
                targetContact: "رمز-اتصال AR-C"),
            F(
                "draft-he",
                FixtureDisposition.RenderableUnreviewedDraft,
                [P("Synthetic message HE-1.", "אסימון-בדיקה HE-1")],
                targetLocale: "he",
                targetRequestedAction: "אסימון-פעולה HE-A",
                targetContact: "אסימון-קשר HE-C"),
            F(
                "draft-ja",
                FixtureDisposition.RenderableUnreviewedDraft,
                [P("Synthetic message JA-1.", "テストトークン JA-1")],
                targetLocale: "ja",
                targetRequestedAction: "アクショントークン JA-A",
                targetContact: "連絡トークン JA-C"),
            F(
                "draft-zh-hant",
                FixtureDisposition.RenderableUnreviewedDraft,
                [P("Synthetic message ZH-1.", "測試代碼 ZH-1")],
                targetLocale: "zh-Hant",
                targetRequestedAction: "動作代碼 ZH-A",
                targetContact: "聯絡代碼 ZH-C"),
            F(
                "draft-long-mixed-script",
                FixtureDisposition.RenderableUnreviewedDraft,
                [P("Synthetic source MIX-1.", $"TARGET α عربي 合成 MIX-1 {new string('x', 320)}")],
                targetLocale: "es",
                targetRequestedAction: "TARGET-MIX-ACTION",
                targetContact: "TARGET-MIX-CONTACT"),
            F(
                "draft-body-facts-reordered",
                FixtureDisposition.RenderableUnreviewedDraft,
                [
                    P("Source BODY-A.", "TARGET BODY-B."),
                    P("Source BODY-B.", "TARGET BODY-A."),
                ],
                lockedFields:
                [
                    L(LockedFieldKind.Condition, "BODY-A"),
                    L(LockedFieldKind.Condition, "BODY-B"),
                ],
                targetLocale: "es",
                targetRequestedAction: "TARGET-REORDER-ACTION",
                targetContact: "TARGET-REORDER-CONTACT"),
            F(
                "draft-body-fact-repeated",
                FixtureDisposition.RenderableUnreviewedDraft,
                [
                    P("Source REPEAT-1.", "TARGET REPEAT-1."),
                    P("Source plain.", "TARGET repeats REPEAT-1."),
                ],
                lockedFields: [L(LockedFieldKind.Condition, "REPEAT-1")],
                targetLocale: "es",
                targetRequestedAction: "TARGET-REPEAT-ACTION",
                targetContact: "TARGET-REPEAT-CONTACT"),
            F(
                "draft-role-locks-multiple-ask-warning",
                FixtureDisposition.RenderableUnreviewedDraft,
                [P("Synthetic role notice.", "TARGET-ROLE-BODY")],
                requestedAction: "Return ACT-1 and read the notice.",
                contact: "OFFICE-9",
                deadline: "DATE-10",
                lockedFields:
                [
                    L(LockedFieldKind.Condition, "ACT-1"),
                    L(LockedFieldKind.Date, "DATE-10"),
                    L(LockedFieldKind.ProperName, "OFFICE-9"),
                ],
                targetLocale: "es",
                targetRequestedAction: "TARGET ACTION ACT-1",
                targetContact: "TARGET CONTACT OFFICE-9",
                targetDeadline: "DATE-10",
                expectedWarningCodes: ["bridge.actions"]),

            F(
                "refusal-inventory-not-reviewed",
                FixtureDisposition.Blocking,
                [P("Synthetic source notice.")],
                lockedFieldInventoryReviewed: false,
                expectedBlockingCodes: ["locked.inventory-review-required"]),
            F(
                "refusal-empty-body",
                FixtureDisposition.Blocking,
                [],
                expectedBlockingCodes: ["bridge.empty"]),
            F(
                "refusal-blank-action-contact",
                FixtureDisposition.Blocking,
                [P("Synthetic source notice.")],
                requestedAction: " ",
                contact: " ",
                expectedBlockingCodes: ["bridge.action", "bridge.contact"]),
            F(
                "refusal-readability-twenty-one-words",
                FixtureDisposition.Blocking,
                [P("One two three four five six seven eight nine ten eleven twelve thirteen fourteen fifteen sixteen seventeen eighteen nineteen twenty twentyone.")],
                expectedBlockingCodes: ["bridge.readability"]),
            F(
                "refusal-target-content-without-locale",
                FixtureDisposition.Blocking,
                [P("Synthetic source notice.", "TARGET-BODY")],
                targetRequestedAction: "TARGET-ACTION",
                expectedBlockingCodes: ["bridge.target-without-locale"]),
            F(
                "refusal-missing-body-target",
                FixtureDisposition.Blocking,
                [new BridgeParagraph("Synthetic source notice.")],
                targetLocale: "es",
                targetRequestedAction: "TARGET-ACTION",
                targetContact: "TARGET-CONTACT",
                expectedBlockingCodes: ["bridge.target-missing"]),
            F(
                "refusal-missing-structured-targets",
                FixtureDisposition.Blocking,
                [P("Synthetic source notice.", "TARGET-BODY")],
                deadline: "DATE-10",
                targetLocale: "es",
                expectedBlockingCodes:
                [
                    "bridge.target-action-missing",
                    "bridge.target-contact-missing",
                    "bridge.target-deadline-missing",
                    "doc.bilingual.target",
                ]),
            F(
                "refusal-target-deadline-without-source",
                FixtureDisposition.Blocking,
                [P("Synthetic source notice.", "TARGET-BODY")],
                targetLocale: "es",
                targetRequestedAction: "TARGET-ACTION",
                targetContact: "TARGET-CONTACT",
                targetDeadline: "TARGET-DATE",
                expectedBlockingCodes: ["bridge.target-deadline-without-source"]),
            F(
                "refusal-working-glossary-term-missing",
                FixtureDisposition.Blocking,
                [P("Synthetic SRC-TERM notice.", "TARGET omits required term.")],
                glossary: G("synthetic-g-refusal", "SRC-TERM", "TGT-TERM"),
                targetLocale: "es",
                targetRequestedAction: "TARGET-ACTION",
                targetContact: "TARGET-CONTACT",
                expectedBlockingCodes: ["bridge.glossary"]),
            F(
                "refusal-monolingual-lock-absent",
                FixtureDisposition.Blocking,
                [P("Synthetic source notice.")],
                lockedFields: [L(LockedFieldKind.Date, "DATE-ABSENT")],
                expectedBlockingCodes: ["locked.missing"]),
            F(
                "refusal-generated-notice-cannot-satisfy-lock",
                FixtureDisposition.Blocking,
                [P("Synthetic source notice.")],
                lockedFields: [L(LockedFieldKind.Condition, "no recipient list")],
                expectedBlockingCodes: ["locked.missing"]),
            F(
                "refusal-generated-card-title-cannot-satisfy-lock",
                FixtureDisposition.Blocking,
                [P("Synthetic source notice.")],
                lockedFields: [L(LockedFieldKind.ProperName, "What we ask")],
                expectedBlockingCodes: ["locked.missing"]),
            F(
                "refusal-body-lock-moved-to-contact",
                FixtureDisposition.Blocking,
                [P("Source DATE-10.", "TARGET omits body date.")],
                contact: "Office",
                lockedFields: [L(LockedFieldKind.Date, "DATE-10")],
                targetLocale: "es",
                targetRequestedAction: "TARGET-ACTION",
                targetContact: "TARGET CONTACT DATE-10",
                expectedBlockingCodes: ["bridge.locked"]),
            F(
                "refusal-action-lock-moved-to-body",
                FixtureDisposition.Blocking,
                [P("Source body plain.", "TARGET BODY ACT-1.")],
                requestedAction: "Return ACT-1.",
                lockedFields: [L(LockedFieldKind.Condition, "ACT-1")],
                targetLocale: "es",
                targetRequestedAction: "TARGET ACTION OMITTED",
                targetContact: "TARGET-CONTACT",
                expectedBlockingCodes: ["bridge.locked"]),
            F(
                "refusal-deadline-lock-moved-to-body",
                FixtureDisposition.Blocking,
                [P("Source body plain.", "TARGET BODY DATE-10.")],
                deadline: "DATE-10",
                lockedFields: [L(LockedFieldKind.Date, "DATE-10")],
                targetLocale: "es",
                targetRequestedAction: "TARGET-ACTION",
                targetContact: "TARGET-CONTACT",
                targetDeadline: "DATE-11",
                expectedBlockingCodes: ["bridge.locked"]),
            F(
                "refusal-contact-lock-moved-to-body",
                FixtureDisposition.Blocking,
                [P("Source body plain.", "TARGET BODY OFFICE-9.")],
                contact: "OFFICE-9",
                lockedFields: [L(LockedFieldKind.ProperName, "OFFICE-9")],
                targetLocale: "es",
                targetRequestedAction: "TARGET-ACTION",
                targetContact: "TARGET OTHER CONTACT",
                expectedBlockingCodes: ["bridge.locked"]),
            F(
                "refusal-source-only-title-lock",
                FixtureDisposition.Blocking,
                [P("Source body plain.", "TARGET BODY")],
                title: "Synthetic title CODE-7",
                lockedFields: [L(LockedFieldKind.Condition, "CODE-7")],
                targetLocale: "es",
                targetRequestedAction: "TARGET-ACTION",
                targetContact: "TARGET-CONTACT",
                expectedBlockingCodes: ["bridge.locked"]),
            F(
                "refusal-numeric-substrings-and-blank-lock",
                FixtureDisposition.Blocking,
                [P("Station 13 costs $4.50.", "TARGET station 13 costs $4.50.")],
                lockedFields:
                [
                    L(LockedFieldKind.Number, "3"),
                    L(LockedFieldKind.Number, "$4.5"),
                    L(LockedFieldKind.Number, " "),
                ],
                targetLocale: "es",
                targetRequestedAction: "TARGET-ACTION",
                targetContact: "TARGET-CONTACT",
                expectedBlockingCodes: ["bridge.locked", "locked.empty"]),
        ];

    private static void AssertImplementedDocumentShape(Fixture fixture, ArtifactDocument document)
    {
        Assert.Equal(ExpectedDocumentNodes(fixture), document.Nodes);

        var title = Assert.IsType<Heading>(document.Nodes[0]);
        Assert.Equal(1, title.Level);
        Assert.Equal(fixture.Title, title.Text);

        var notices = document.Nodes.OfType<TeacherOnlyNotice>().ToArray();
        Assert.Contains(notices, notice => notice.Text.Contains("Fact-lock summary", StringComparison.Ordinal));
        Assert.Contains(notices, notice => notice.Text.Contains("no recipient list", StringComparison.Ordinal));

        if (fixture.Disposition == FixtureDisposition.SourceValid)
        {
            Assert.DoesNotContain(notices, notice => notice.Text.Contains("Translation status", StringComparison.Ordinal));
            Assert.Empty(document.Nodes.OfType<BilingualPair>());
            Assert.Equal(
                fixture.Paragraphs.Select(paragraph => paragraph.SourceText),
                document.Nodes.OfType<Paragraph>().Select(paragraph => paragraph.Text));

            var expectedCards = new List<Card> { new("What we ask", fixture.RequestedAction) };
            if (!string.IsNullOrWhiteSpace(fixture.Deadline))
            {
                expectedCards.Add(new Card("By when", fixture.Deadline));
            }

            expectedCards.Add(new Card("Questions? Contact", fixture.Contact));
            Assert.Equal(expectedCards, document.Nodes.OfType<Card>());
            return;
        }

        Assert.Equal(FixtureDisposition.RenderableUnreviewedDraft, fixture.Disposition);
        var status = notices.Single(notice => notice.Text.Contains("Translation status", StringComparison.Ordinal));
        Assert.Contains($"Working glossary {fixture.Glossary.Version}", status.Text, StringComparison.Ordinal);
        Assert.Contains("not approved by this application", status.Text, StringComparison.Ordinal);
        Assert.Contains("NOT yet language-reviewed", status.Text, StringComparison.Ordinal);
        Assert.Empty(document.Nodes.OfType<Card>());

        var expectedPairs = fixture.Paragraphs
            .Select(paragraph => new BilingualPair(
                paragraph.SourceText,
                Assert.IsType<string>(paragraph.TargetText),
                fixture.SourceLocale,
                Assert.IsType<string>(fixture.TargetLocale)))
            .ToList();
        expectedPairs.Add(new BilingualPair(
            fixture.RequestedAction,
            Assert.IsType<string>(fixture.TargetRequestedAction),
            fixture.SourceLocale,
            Assert.IsType<string>(fixture.TargetLocale)));
        if (!string.IsNullOrWhiteSpace(fixture.Deadline))
        {
            expectedPairs.Add(new BilingualPair(
                fixture.Deadline,
                Assert.IsType<string>(fixture.TargetDeadline),
                fixture.SourceLocale,
                Assert.IsType<string>(fixture.TargetLocale)));
        }

        expectedPairs.Add(new BilingualPair(
            fixture.Contact,
            Assert.IsType<string>(fixture.TargetContact),
            fixture.SourceLocale,
            Assert.IsType<string>(fixture.TargetLocale)));
        Assert.Equal(expectedPairs, document.Nodes.OfType<BilingualPair>());
    }

    private static List<DocumentNode> ExpectedDocumentNodes(Fixture fixture)
    {
        var nodes = new List<DocumentNode> { new Heading(1, fixture.Title) };
        if (fixture.Disposition == FixtureDisposition.SourceValid)
        {
            nodes.AddRange(fixture.Paragraphs.Select(paragraph => new Paragraph(paragraph.SourceText)));
            nodes.Add(new Card("What we ask", fixture.RequestedAction));
            if (!string.IsNullOrWhiteSpace(fixture.Deadline))
            {
                nodes.Add(new Card("By when", fixture.Deadline));
            }

            nodes.Add(new Card("Questions? Contact", fixture.Contact));
        }
        else
        {
            var targetLocale = Assert.IsType<string>(fixture.TargetLocale);
            nodes.AddRange(fixture.Paragraphs.Select(paragraph => new BilingualPair(
                paragraph.SourceText,
                Assert.IsType<string>(paragraph.TargetText),
                fixture.SourceLocale,
                targetLocale)));
            nodes.Add(new Heading(2, "What we ask"));
            nodes.Add(new BilingualPair(
                fixture.RequestedAction,
                Assert.IsType<string>(fixture.TargetRequestedAction),
                fixture.SourceLocale,
                targetLocale));
            if (!string.IsNullOrWhiteSpace(fixture.Deadline))
            {
                nodes.Add(new Heading(2, "By when"));
                nodes.Add(new BilingualPair(
                    fixture.Deadline,
                    Assert.IsType<string>(fixture.TargetDeadline),
                    fixture.SourceLocale,
                    targetLocale));
            }

            nodes.Add(new Heading(2, "Questions? Contact"));
            nodes.Add(new BilingualPair(
                fixture.Contact,
                Assert.IsType<string>(fixture.TargetContact),
                fixture.SourceLocale,
                targetLocale));
        }

        nodes.Add(new TeacherOnlyNotice(LockedFieldValidator.FormatInventorySummary(fixture.LockedFields)));
        if (fixture.Disposition == FixtureDisposition.RenderableUnreviewedDraft)
        {
            nodes.Add(new TeacherOnlyNotice(
                $"Working glossary {fixture.Glossary.Version} (not approved by this application). " +
                "Translation status: drafted - NOT yet language-reviewed by a qualified reviewer."));
        }

        nodes.Add(new TeacherOnlyNotice(
            "This application holds no recipient list and sends nothing; addressing and delivery are yours, under your school's rules."));
        return nodes;
    }

    private static async Task AssertGenericApprovalAndRenderingAreDeterministicAsync(
        Fixture fixture,
        FamilyBridgeResult result)
    {
        // This is generic structural approval. For bilingual drafts it is not
        // language review, and the teacher-only status remains unreviewed.
        var approved = ApprovalGate.Approve(
            DraftArtifact.New(result.Document, DataLane.Green),
            "synthetic-corpus@example.invalid",
            result.Issues,
            SomeInstant);
        var renderer = new AccessibleHtmlRenderer();
        var learnerRequest = new RenderRequest(RenderTarget.AccessibleHtml);
        var teacherRequest = new RenderRequest(RenderTarget.AccessibleHtml, RenderAudience.Teacher);
        var printRequest = new RenderRequest(RenderTarget.PrintHtml);
        var learnerFirst = await renderer.RenderAsync(approved, learnerRequest, CancellationToken.None);
        var learnerSecond = await renderer.RenderAsync(approved, learnerRequest, CancellationToken.None);
        var teacherFirst = await renderer.RenderAsync(approved, teacherRequest, CancellationToken.None);
        var teacherSecond = await renderer.RenderAsync(approved, teacherRequest, CancellationToken.None);
        var printFirst = await renderer.RenderAsync(approved, printRequest, CancellationToken.None);
        var printSecond = await renderer.RenderAsync(approved, printRequest, CancellationToken.None);

        Assert.True(learnerFirst.Content.Span.SequenceEqual(learnerSecond.Content.Span), $"{fixture}: learner rendering drifted.");
        Assert.True(teacherFirst.Content.Span.SequenceEqual(teacherSecond.Content.Span), $"{fixture}: teacher rendering drifted.");
        Assert.True(printFirst.Content.Span.SequenceEqual(printSecond.Content.Span), $"{fixture}: print rendering drifted.");

        var learner = Encoding.UTF8.GetString(learnerFirst.Content.Span);
        var teacher = Encoding.UTF8.GetString(teacherFirst.Content.Span);
        var print = Encoding.UTF8.GetString(printFirst.Content.Span);
        Assert.Contains($"<html lang=\"{fixture.SourceLocale}\"", learner, StringComparison.Ordinal);
        Assert.Contains(WebUtility.HtmlEncode(fixture.Title), learner, StringComparison.Ordinal);
        Assert.Contains(WebUtility.HtmlEncode(fixture.RequestedAction), learner, StringComparison.Ordinal);
        Assert.Contains(WebUtility.HtmlEncode(fixture.Contact), learner, StringComparison.Ordinal);
        if (!string.IsNullOrWhiteSpace(fixture.Deadline))
        {
            Assert.Contains(WebUtility.HtmlEncode(fixture.Deadline), learner, StringComparison.Ordinal);
        }

        foreach (var paragraph in fixture.Paragraphs)
        {
            Assert.Contains(WebUtility.HtmlEncode(paragraph.SourceText), learner, StringComparison.Ordinal);
            if (fixture.Disposition == FixtureDisposition.RenderableUnreviewedDraft)
            {
                Assert.Contains(
                    WebUtility.HtmlEncode(Assert.IsType<string>(paragraph.TargetText)),
                    learner,
                    StringComparison.Ordinal);
            }
        }

        Assert.DoesNotContain("Fact-lock summary", learner, StringComparison.Ordinal);
        Assert.DoesNotContain("no recipient list", learner, StringComparison.Ordinal);
        Assert.DoesNotContain("Fact-lock summary", print, StringComparison.Ordinal);
        Assert.DoesNotContain("no recipient list", print, StringComparison.Ordinal);
        Assert.Contains("Fact-lock summary", teacher, StringComparison.Ordinal);
        Assert.Contains("no recipient list", teacher, StringComparison.Ordinal);
        Assert.Contains("@page", print, StringComparison.Ordinal);

        if (fixture.Disposition == FixtureDisposition.RenderableUnreviewedDraft)
        {
            Assert.Contains($"lang=\"{fixture.TargetLocale}\"", learner, StringComparison.Ordinal);
            Assert.Contains("dir=\"auto\"", learner, StringComparison.Ordinal);
            Assert.Contains(WebUtility.HtmlEncode(Assert.IsType<string>(fixture.TargetRequestedAction)), learner, StringComparison.Ordinal);
            Assert.Contains(WebUtility.HtmlEncode(Assert.IsType<string>(fixture.TargetContact)), learner, StringComparison.Ordinal);
            if (!string.IsNullOrWhiteSpace(fixture.TargetDeadline))
            {
                Assert.Contains(WebUtility.HtmlEncode(fixture.TargetDeadline), learner, StringComparison.Ordinal);
            }

            Assert.DoesNotContain("NOT yet language-reviewed", learner, StringComparison.Ordinal);
            Assert.DoesNotContain("NOT yet language-reviewed", print, StringComparison.Ordinal);
            Assert.Contains("NOT yet language-reviewed", teacher, StringComparison.Ordinal);
        }
        else
        {
            Assert.DoesNotContain("NOT yet language-reviewed", teacher, StringComparison.Ordinal);
        }
    }

    private static string[] Sorted(IEnumerable<string>? codes)
        => [.. (codes ?? [])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)];

    private static Fixture F(
        string id,
        FixtureDisposition disposition,
        BridgeParagraph[] paragraphs,
        string requestedAction = "Read NOTICE-1.",
        string contact = "OFFICE-1",
        string? title = null,
        Glossary? glossary = null,
        LockedField[]? lockedFields = null,
        bool lockedFieldInventoryReviewed = true,
        string? deadline = null,
        string sourceLocale = "en",
        string? targetLocale = null,
        string? targetRequestedAction = null,
        string? targetContact = null,
        string? targetDeadline = null,
        string[]? expectedBlockingCodes = null,
        string[]? expectedWarningCodes = null)
        => new(
            id,
            disposition,
            title ?? $"Synthetic fixture {id}",
            paragraphs,
            requestedAction,
            contact,
            glossary ?? Glossary.Empty,
            lockedFields ?? [],
            lockedFieldInventoryReviewed,
            deadline,
            sourceLocale,
            targetLocale,
            targetRequestedAction,
            targetContact,
            targetDeadline,
            expectedBlockingCodes,
            expectedWarningCodes);

    private static BridgeParagraph P(string source, string? target = null) => new(source, target);

    private static LockedField L(LockedFieldKind kind, string exactValue) => new(kind, exactValue);

    private static Glossary G(string version, string source, string target)
        => new(version, [new GlossaryEntry(source, target)]);
}
