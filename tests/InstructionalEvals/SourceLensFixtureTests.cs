// SPDX-License-Identifier: GPL-3.0-or-later
using System.Net;
using System.Text;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.BuiltIn.SourceLens;
using Foundry.Rendering;

namespace Foundry.Tests.InstructionalEvals;

/// <summary>
/// Wholly synthetic structural corpus for Inquirywright (stable recipe id
/// <c>source-lens</c>; plan §10.12). It proves only the current metadata,
/// caller-asserted transcript-review, prompt-presence, source-card,
/// observation/inference, audience-filtering, and deterministic-rendering
/// contracts. It does not prove rights clearance or sink enforcement, durable
/// transcript verification, quotation fidelity to an external source,
/// authoritative context, answerability, omissions, sensitivity/trauma
/// handling, disciplinary quality, language review, accessibility review,
/// or protected-seat review.
/// </summary>
public sealed class SourceLensFixtureTests
{
    public enum FixtureDisposition
    {
        Renderable,
        UnknownRightsObservedOnly,
        Blocking,
    }

    public sealed record Fixture(
        string Id,
        string Stratum,
        SourceMetadata Metadata,
        string Excerpt,
        bool TeacherAssertedTranscriptReviewed,
        InquiryPrompts Prompts,
        int ObservationRows = 4,
        string Language = "en",
        FixtureDisposition Disposition = FixtureDisposition.Renderable,
        string[]? ExpectedBlockingCodes = null,
        string[]? ExpectedWarningCodes = null)
    {
        public override string ToString() => $"{Id} [{Stratum}; {Disposition}]";
    }

    private const string SyntheticRights = "Wholly synthetic repository fixture; GPL-3.0-or-later";
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
    public void The_corpus_is_thirty_six_distinct_synthetic_cases_with_unknown_rights_observed_without_exercising_sinks()
    {
        Assert.Equal(36, Fixtures.Count);
        Assert.Equal(Fixtures.Count, Fixtures.Select(fixture => fixture.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.True(Fixtures.Select(fixture => fixture.Stratum).Distinct(StringComparer.Ordinal).Count() >= 8);
        Assert.Equal(25, Fixtures.Count(fixture => fixture.Disposition == FixtureDisposition.Renderable));
        Assert.Single(Fixtures, fixture => fixture.Disposition == FixtureDisposition.UnknownRightsObservedOnly);
        Assert.Equal(10, Fixtures.Count(fixture => fixture.Disposition == FixtureDisposition.Blocking));
        Assert.True(Fixtures.Select(fixture => fixture.Language).Distinct(StringComparer.Ordinal).Count() >= 5);
        Assert.Contains(Fixtures, fixture => fixture.ObservationRows == 1 && fixture.Disposition == FixtureDisposition.Renderable);
        Assert.Contains(Fixtures, fixture => fixture.ObservationRows == 20 && fixture.Disposition == FixtureDisposition.Renderable);
        Assert.Contains(Fixtures, fixture => fixture.Prompts.Sourcing.Count >= 3 && fixture.Prompts.Corroboration.Count >= 3);

        var unknownRights = Assert.Single(
            Fixtures,
            fixture => fixture.Disposition == FixtureDisposition.UnknownRightsObservedOnly);
        Assert.Equal(SourceLensBuilder.Unknown, unknownRights.Metadata.Rights);
        var warningCodes = Assert.IsType<string[]>(unknownRights.ExpectedWarningCodes);
        Assert.Equal(["lens.rights-unknown"], warningCodes);

        Assert.Equal("source-lens", SourceLensBuilder.Recipe.Id);
        Assert.Equal("0.1.0", SourceLensBuilder.Recipe.Version);
        Assert.Equal("schema.source-lens.v1", SourceLensBuilder.Recipe.OutputSchemaId);
        Assert.Equal("0.1", SourceLensBuilder.Recipe.EvaluationSuiteVersion);
    }

    [Theory]
    [MemberData(nameof(FixtureIndexes))]
    public async Task Every_fixture_blocks_observes_unknown_rights_or_preserves_the_current_structure_and_renders_deterministically(int fixtureIndex)
    {
        var fixture = Fixtures[fixtureIndex];
        var result = SourceLensBuilder.Build(
            fixture.Metadata,
            fixture.Excerpt,
            fixture.TeacherAssertedTranscriptReviewed,
            fixture.Prompts,
            fixture.ObservationRows,
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

        if (fixture.Disposition == FixtureDisposition.UnknownRightsObservedOnly)
        {
            Assert.Empty(fixture.ExpectedBlockingCodes ?? []);
            Assert.Contains(
                result.Issues,
                issue => issue.Code == "lens.rights-unknown"
                    && issue.Severity == ValidationSeverity.Warning);

            // This is deliberately the end of the case. The current warning is
            // not a rights-specific sink guard. Corpus policy therefore does
            // not exercise ApprovalGate or a renderer; this must not be read as
            // evidence that the product itself enforces a hold.
            return;
        }

        Assert.Empty(fixture.ExpectedBlockingCodes ?? []);
        Assert.Empty(fixture.ExpectedWarningCodes ?? []);
        Assert.False(string.IsNullOrWhiteSpace(fixture.Metadata.Creator));
        Assert.False(string.IsNullOrWhiteSpace(fixture.Metadata.Title));
        Assert.False(string.IsNullOrWhiteSpace(fixture.Metadata.Date));
        Assert.False(string.IsNullOrWhiteSpace(fixture.Metadata.Type));
        Assert.False(string.IsNullOrWhiteSpace(fixture.Metadata.Rights));
        Assert.False(string.IsNullOrWhiteSpace(fixture.Excerpt));
        Assert.True(fixture.TeacherAssertedTranscriptReviewed);
        Assert.NotEmpty(fixture.Prompts.Sourcing);
        Assert.NotEmpty(fixture.Prompts.Corroboration);

        AssertImplementedDocumentShape(fixture, result.Document);
        await AssertApprovedRenderingIsDeterministicAsync(fixture, result);
    }

    private static List<Fixture> CreateFixtures()
    {
        var fixtures = new List<Fixture>
        {
            Form("form-notice", "Synthetic notice"),
            Form("form-letter", "Synthetic letter"),
            Form("form-diary", "Synthetic diary entry"),
            Form("form-minutes", "Synthetic meeting minutes"),
            Form("form-map-legend", "Synthetic map legend"),
            Form("form-object-label", "Synthetic object label"),
            Form("form-speech", "Synthetic speech transcript"),
            Form("form-poster", "Synthetic poster text"),
            Form("form-ledger", "Synthetic ledger excerpt"),
            Form("form-instructions", "Synthetic instruction sheet"),

            F(
                "language-arabic",
                "language-layout",
                M("AR-1", "نص تجريبي") with
                {
                    Creator = "منشئ تجريبي AR-1",
                    Title = "عنوان تجريبي AR-1",
                    Date = "تاريخ تجريبي AR-1",
                    Place = "مكان تجريبي AR-1",
                    Audience = "جمهور تجريبي AR-1",
                    Provenance = "مصدر تجريبي AR-1",
                },
                "مقتطف تجريبي AR-1",
                ScriptPrompts("AR-1", "من", "سياق", "قراءة", "دليل", "تفسير"),
                language: "ar"),
            F(
                "language-hebrew",
                "language-layout",
                M("HE-1", "טקסט ניסויי") with
                {
                    Creator = "יוצר ניסויי HE-1",
                    Title = "כותרת ניסויית HE-1",
                    Date = "תאריך ניסויי HE-1",
                    Place = "מקום ניסויי HE-1",
                    Audience = "קהל ניסויי HE-1",
                    Provenance = "מקור ניסויי HE-1",
                },
                "קטע ניסויי HE-1",
                ScriptPrompts("HE-1", "מקור", "הקשר", "קריאה", "אימות", "פרשנות"),
                language: "he"),
            F(
                "language-japanese",
                "language-layout",
                M("JA-1", "合成資料") with
                {
                    Creator = "合成作成者 JA-1",
                    Title = "合成題名 JA-1",
                    Date = "合成日付 JA-1",
                    Place = "合成場所 JA-1",
                    Audience = "合成読者 JA-1",
                    Provenance = "合成来歴 JA-1",
                },
                "合成抜粋 JA-1",
                ScriptPrompts("JA-1", "出典", "文脈", "精読", "照合", "解釈"),
                language: "ja"),
            F(
                "language-traditional-chinese",
                "language-layout",
                M("ZH-1", "合成資料") with
                {
                    Creator = "合成創作者 ZH-1",
                    Title = "合成標題 ZH-1",
                    Date = "合成日期 ZH-1",
                    Place = "合成地點 ZH-1",
                    Audience = "合成讀者 ZH-1",
                    Provenance = "合成來源 ZH-1",
                },
                "合成摘錄 ZH-1",
                ScriptPrompts("ZH-1", "來源", "脈絡", "細讀", "佐證", "詮釋"),
                language: "zh-Hant"),

            F(
                "metadata-optionals-omitted",
                "metadata",
                M("META-O", "Synthetic record") with { Place = "", Audience = " ", Provenance = "" },
                E("META-O"),
                P("META-O")),
            F(
                "metadata-explicit-unknowns",
                "metadata",
                M("META-U", "Synthetic record") with { Creator = SourceLensBuilder.Unknown, Date = SourceLensBuilder.Unknown },
                E("META-U"),
                P("META-U")),

            F(
                "prompts-minimum-current-validator",
                "prompt-breadth",
                M("PROMPT-MIN", "Synthetic record"),
                E("PROMPT-MIN"),
                new InquiryPrompts(
                    Sourcing: ["Synthetic sourcing prompt PROMPT-MIN"],
                    Contextualization: [],
                    CloseReading: [],
                    Corroboration: ["Synthetic corroboration prompt PROMPT-MIN"],
                    BoundedInterpretation: [])),
            F(
                "prompts-many",
                "prompt-breadth",
                M("PROMPT-MANY", "Synthetic record"),
                E("PROMPT-MANY"),
                new InquiryPrompts(
                    Sourcing: ["Synthetic sourcing PROMPT-MANY-A", "Synthetic sourcing PROMPT-MANY-B", "Synthetic sourcing PROMPT-MANY-C"],
                    Contextualization: ["Synthetic context PROMPT-MANY-A", "Synthetic context PROMPT-MANY-B"],
                    CloseReading: ["Synthetic close reading PROMPT-MANY-A", "Synthetic close reading PROMPT-MANY-B"],
                    Corroboration: ["Synthetic corroboration PROMPT-MANY-A", "Synthetic corroboration PROMPT-MANY-B", "Synthetic corroboration PROMPT-MANY-C"],
                    BoundedInterpretation: ["Synthetic interpretation PROMPT-MANY-A", "Synthetic interpretation PROMPT-MANY-B"])),

            F("rows-one", "observation-rows", M("ROWS-1", "Synthetic record"), E("ROWS-1"), P("ROWS-1"), observationRows: 1),
            F("rows-eight", "observation-rows", M("ROWS-8", "Synthetic record"), E("ROWS-8"), P("ROWS-8"), observationRows: 8),
            F("rows-twenty", "observation-rows", M("ROWS-20", "Synthetic record"), E("ROWS-20"), P("ROWS-20"), observationRows: 20),

            F(
                "citation-trimming",
                "citation",
                M("CITE-1", " Synthetic format. ") with
                {
                    Creator = " Synthetic creator CITE-1. ",
                    Title = " Synthetic title CITE-1. ",
                    Date = " Synthetic date CITE-1. ",
                    Provenance = " Synthetic provenance CITE-1. ",
                },
                E("CITE-1"),
                P("CITE-1")),

            F(
                "rendering-long-excerpt",
                "rendering",
                M("LONG-1", "Synthetic long record"),
                $"Synthetic excerpt LONG-1 {new string('x', 900)}",
                P("LONG-1")),
            F(
                "rendering-markup-like",
                "rendering",
                M("MARK-1", "Synthetic <format> & record") with
                {
                    Creator = "Synthetic <creator> & token MARK-1",
                    Title = "Synthetic <title> & token MARK-1",
                    Provenance = "Synthetic <provenance> & token MARK-1",
                },
                "Synthetic <source> & quoted \"token\" MARK-1",
                new InquiryPrompts(
                    ["Synthetic <sourcing> & token MARK-1"],
                    ["Synthetic <context> & token MARK-1"],
                    ["Synthetic <reading> & token MARK-1"],
                    ["Synthetic <corroboration> & token MARK-1"],
                    ["Synthetic <interpretation> & token MARK-1"])),
            F(
                "rendering-mixed-script",
                "rendering",
                M("MIX-1", "Synthetic α format") with { Title = "Synthetic α موضوع 合成 title MIX-1" },
                "Synthetic β مقتطف 合成 excerpt MIX-1",
                ScriptPrompts("MIX-1", "source α", "سياق β", "精読 γ", "佐證 δ", "interpretation ε")),

            F(
                "rights-explicit-unknown",
                "unknown-rights-observation",
                M("RIGHTS-U", "Synthetic record") with { Rights = SourceLensBuilder.Unknown },
                E("RIGHTS-U"),
                P("RIGHTS-U"),
                disposition: FixtureDisposition.UnknownRightsObservedOnly,
                expectedWarningCodes: ["lens.rights-unknown"]),

            F(
                "refusal-blank-creator",
                "refusal-metadata",
                M("R-CREATOR", "Synthetic record") with { Creator = " " },
                E("R-CREATOR"),
                P("R-CREATOR"),
                disposition: FixtureDisposition.Blocking,
                expectedBlockingCodes: ["lens.metadata"]),
            F(
                "refusal-blank-title",
                "refusal-metadata",
                M("R-TITLE", "Synthetic record") with { Title = " " },
                E("R-TITLE"),
                P("R-TITLE"),
                disposition: FixtureDisposition.Blocking,
                expectedBlockingCodes: ["doc.heading.empty", "lens.metadata"]),
            F(
                "refusal-blank-date",
                "refusal-metadata",
                M("R-DATE", "Synthetic record") with { Date = "" },
                E("R-DATE"),
                P("R-DATE"),
                disposition: FixtureDisposition.Blocking,
                expectedBlockingCodes: ["lens.metadata"]),
            F(
                "refusal-blank-type",
                "refusal-metadata",
                M("R-TYPE", "Synthetic record") with { Type = " " },
                E("R-TYPE"),
                P("R-TYPE"),
                disposition: FixtureDisposition.Blocking,
                expectedBlockingCodes: ["lens.metadata"]),
            F(
                "refusal-blank-rights",
                "refusal-metadata",
                M("R-RIGHTS", "Synthetic record") with { Rights = " " },
                E("R-RIGHTS"),
                P("R-RIGHTS"),
                disposition: FixtureDisposition.Blocking,
                expectedBlockingCodes: ["lens.metadata"]),
            F(
                "refusal-transcript-review-not-asserted",
                "refusal-transcript-review-assertion",
                M("R-TRANSCRIPT", "Synthetic record"),
                E("R-TRANSCRIPT"),
                P("R-TRANSCRIPT"),
                teacherAssertedTranscriptReviewed: false,
                disposition: FixtureDisposition.Blocking,
                expectedBlockingCodes: ["lens.transcript"]),
            F(
                "refusal-blank-excerpt",
                "refusal-excerpt",
                M("R-EXCERPT", "Synthetic record"),
                " ",
                P("R-EXCERPT"),
                disposition: FixtureDisposition.Blocking,
                expectedBlockingCodes: ["lens.excerpt"]),
            F(
                "refusal-no-sourcing",
                "refusal-prompts",
                M("R-SOURCING", "Synthetic record"),
                E("R-SOURCING"),
                P("R-SOURCING") with { Sourcing = [] },
                disposition: FixtureDisposition.Blocking,
                expectedBlockingCodes: ["lens.sourcing"]),
            F(
                "refusal-no-corroboration",
                "refusal-prompts",
                M("R-CORROBORATION", "Synthetic record"),
                E("R-CORROBORATION"),
                P("R-CORROBORATION") with { Corroboration = [] },
                disposition: FixtureDisposition.Blocking,
                expectedBlockingCodes: ["lens.corroboration"]),
            F(
                "refusal-blank-prompt",
                "refusal-prompts",
                M("R-PROMPT", "Synthetic record"),
                E("R-PROMPT"),
                P("R-PROMPT") with { Sourcing = [" "] },
                disposition: FixtureDisposition.Blocking,
                expectedBlockingCodes: ["doc.steps.blank-step"]),
        };

        return fixtures;
    }

    private static Fixture Form(string id, string sourceType)
        => F(id, "source-form", M(id, sourceType), E(id), P(id));

    private static SourceMetadata M(string token, string sourceType)
        => new(
            Creator: $"Synthetic creator {token}",
            Title: $"Synthetic source title {token}",
            Date: $"Synthetic date {token}",
            Type: sourceType,
            Rights: SyntheticRights,
            Place: $"Synthetic place {token}",
            Audience: $"Synthetic audience {token}",
            Provenance: $"Generated wholly for repository fixture {token}");

    private static string E(string token) => $"Synthetic source excerpt {token}; no external fact is asserted.";

    private static InquiryPrompts P(string token)
        => new(
            Sourcing: [$"Synthetic sourcing prompt {token}"],
            Contextualization: [$"Synthetic context prompt {token}"],
            CloseReading: [$"Synthetic close-reading prompt {token}"],
            Corroboration: [$"Synthetic corroboration prompt {token}"],
            BoundedInterpretation: [$"Synthetic bounded-interpretation prompt {token}"]);

    private static InquiryPrompts ScriptPrompts(
        string token,
        string sourcing,
        string context,
        string closeReading,
        string corroboration,
        string interpretation)
        => new(
            [$"{sourcing} {token}"],
            [$"{context} {token}"],
            [$"{closeReading} {token}"],
            [$"{corroboration} {token}"],
            [$"{interpretation} {token}"]);

    private static Fixture F(
        string id,
        string stratum,
        SourceMetadata metadata,
        string excerpt,
        InquiryPrompts prompts,
        int observationRows = 4,
        string language = "en",
        bool teacherAssertedTranscriptReviewed = true,
        FixtureDisposition disposition = FixtureDisposition.Renderable,
        string[]? expectedBlockingCodes = null,
        string[]? expectedWarningCodes = null)
        => new(
            id,
            stratum,
            metadata,
            excerpt,
            teacherAssertedTranscriptReviewed,
            prompts,
            observationRows,
            language,
            disposition,
            expectedBlockingCodes,
            expectedWarningCodes);

    private static string[] Sorted(IEnumerable<string>? values)
        => [.. (values ?? []).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal)];

    private static void AssertImplementedDocumentShape(Fixture fixture, ArtifactDocument document)
    {
        Assert.Equal(fixture.Metadata.Title, Assert.IsType<Heading>(document.Nodes[0]).Text);

        var tables = document.Nodes.OfType<TableNode>().ToArray();
        Assert.Equal(2, tables.Length);
        Assert.Equal(["Field", "Record"], tables[0].HeaderRow);
        Assert.Equal(
            [
                ["Creator", Recorded(fixture.Metadata.Creator)],
                ["Date", Recorded(fixture.Metadata.Date)],
                ["Type", Recorded(fixture.Metadata.Type)],
                ["Place", Recorded(fixture.Metadata.Place)],
                ["Audience", Recorded(fixture.Metadata.Audience)],
                ["Provenance", Recorded(fixture.Metadata.Provenance)],
                ["Rights", Recorded(fixture.Metadata.Rights)],
            ],
            tables[0].Rows.Select(row => row.ToArray()));

        Assert.Equal(fixture.Excerpt, Assert.Single(document.Nodes.OfType<Paragraph>()).Text);
        Assert.Equal(SourceLensBuilder.FormatCitation(fixture.Metadata), Assert.Single(document.Nodes.OfType<Citation>()).Text);

        var expectedPromptGroups = new[]
        {
            fixture.Prompts.Sourcing,
            fixture.Prompts.Contextualization,
            fixture.Prompts.CloseReading,
            fixture.Prompts.Corroboration,
            fixture.Prompts.BoundedInterpretation,
        }.Where(group => group.Count > 0).ToArray();
        var actualPromptGroups = document.Nodes.OfType<OrderedSteps>().Select(steps => steps.Steps).ToArray();
        Assert.Equal(expectedPromptGroups.Length, actualPromptGroups.Length);
        for (var index = 0; index < expectedPromptGroups.Length; index++)
        {
            Assert.Equal(expectedPromptGroups[index], actualPromptGroups[index]);
        }

        Assert.Equal(
            ["What I observe (I can point to it)", "What I infer (my thinking, and why)"],
            tables[1].HeaderRow);
        Assert.Equal(Math.Max(1, fixture.ObservationRows), tables[1].Rows.Count);
        Assert.All(tables[1].Rows, row => Assert.Equal(
            [SourceLensBuilder.ObservationPrompt, SourceLensBuilder.InferencePrompt],
            row));

        Assert.Single(document.Nodes.OfType<TeacherOnlyNotice>());
    }

    private static async Task AssertApprovedRenderingIsDeterministicAsync(Fixture fixture, SourceLensResult result)
    {
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
        var learner = Encoding.UTF8.GetString(learnerFirst.Content.Span);
        var teacher = Encoding.UTF8.GetString(teacherFirst.Content.Span);
        var print = Encoding.UTF8.GetString(printFirst.Content.Span);

        Assert.True(learnerFirst.Content.Span.SequenceEqual(learnerSecond.Content.Span), $"{fixture}: learner rendering drifted.");
        Assert.True(teacherFirst.Content.Span.SequenceEqual(teacherSecond.Content.Span), $"{fixture}: teacher rendering drifted.");
        Assert.True(printFirst.Content.Span.SequenceEqual(printSecond.Content.Span), $"{fixture}: print rendering drifted.");
        var rootTag = fixture.Language is "ar" or "he"
            ? $"<html lang=\"{fixture.Language}\" dir=\"rtl\">"
            : $"<html lang=\"{fixture.Language}\">";
        Assert.Contains(rootTag, learner, StringComparison.Ordinal);
        Assert.Contains(WebUtility.HtmlEncode(fixture.Metadata.Title), learner, StringComparison.Ordinal);
        Assert.Contains(WebUtility.HtmlEncode(fixture.Excerpt), learner, StringComparison.Ordinal);
        Assert.Contains($"<cite>{WebUtility.HtmlEncode(SourceLensBuilder.FormatCitation(fixture.Metadata))}</cite>", learner, StringComparison.Ordinal);
        Assert.Contains("<th scope=\"col\">Field</th>", learner, StringComparison.Ordinal);
        Assert.Contains(WebUtility.HtmlEncode(SourceLensBuilder.ObservationPrompt), learner, StringComparison.Ordinal);
        Assert.Contains(WebUtility.HtmlEncode(SourceLensBuilder.InferencePrompt), learner, StringComparison.Ordinal);
        Assert.DoesNotContain("A primary source is evidence", learner, StringComparison.Ordinal);
        Assert.Contains("A primary source is evidence", teacher, StringComparison.Ordinal);
        Assert.Contains("@page", print, StringComparison.Ordinal);

        foreach (var value in new[]
        {
            Recorded(fixture.Metadata.Creator),
            Recorded(fixture.Metadata.Date),
            Recorded(fixture.Metadata.Type),
            Recorded(fixture.Metadata.Place),
            Recorded(fixture.Metadata.Audience),
            Recorded(fixture.Metadata.Provenance),
            Recorded(fixture.Metadata.Rights),
        })
        {
            Assert.Contains(WebUtility.HtmlEncode(value), learner, StringComparison.Ordinal);
        }

        foreach (var prompt in AllPrompts(fixture.Prompts))
        {
            Assert.Contains(WebUtility.HtmlEncode(prompt), learner, StringComparison.Ordinal);
        }
    }

    private static IEnumerable<string> AllPrompts(InquiryPrompts prompts)
        => prompts.Sourcing
            .Concat(prompts.Contextualization)
            .Concat(prompts.CloseReading)
            .Concat(prompts.Corroboration)
            .Concat(prompts.BoundedInterpretation);

    private static string Recorded(string value)
        => string.IsNullOrWhiteSpace(value) ? SourceLensBuilder.NotRecorded : value;
}
