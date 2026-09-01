// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Domain;

namespace Foundry.Tests.Unit;

public class LockedFieldValidatorTests
{
    private static ArtifactDocument SomeDocument() => new(
    [
        new Heading(1, "Field trip on Friday, October 9"),
        new Paragraph("Do not bring peanuts. The bus leaves at 8:15 AM and costs $4.50."),
        new BilingualPair("The bus leaves at 8:15 AM.", "El autobús sale a las 8:15 AM.", "en", "es"),
    ]);

    [Fact]
    public void Source_inventory_review_is_explicit_and_reviewed_empty_is_valid()
    {
        var issue = Assert.Single(LockedFieldValidator.ValidateInventoryReview(reviewed: false));

        Assert.Equal("locked.inventory-review-required", issue.Code);
        Assert.Equal(ValidationSeverity.Blocking, issue.Severity);
        Assert.Contains("source inventory only", issue.Message, StringComparison.Ordinal);
        Assert.Empty(LockedFieldValidator.ValidateInventoryReview(reviewed: true));
    }

    [Fact]
    public void Present_locked_values_pass()
    {
        var issues = LockedFieldValidator.Validate(SomeDocument(),
        [
            new LockedField(LockedFieldKind.Date, "Friday, October 9"),
            new LockedField(LockedFieldKind.Number, "$4.50"),
            new LockedField(LockedFieldKind.Negation, "Do not bring peanuts"),
            new LockedField(LockedFieldKind.Number, "8:15 AM"),
        ]);

        Assert.Empty(issues);
    }

    [Fact]
    public void A_dropped_date_blocks()
    {
        var issues = LockedFieldValidator.Validate(SomeDocument(),
            [new LockedField(LockedFieldKind.Date, "Friday, October 16")]);

        var issue = Assert.Single(issues);
        Assert.Equal("locked.missing", issue.Code);
        Assert.Equal(ValidationSeverity.Blocking, issue.Severity);
    }

    [Fact]
    public void A_softened_negation_blocks()
    {
        var document = new ArtifactDocument([new Paragraph("Bringing peanuts is discouraged.")]);

        var issues = LockedFieldValidator.Validate(document,
            [new LockedField(LockedFieldKind.Negation, "Do not bring peanuts")]);

        Assert.Contains(issues, i => i.Code == "locked.missing");
    }

    [Fact]
    public void A_shorter_numeric_token_does_not_satisfy_an_exact_lock()
    {
        var document = new ArtifactDocument(
            [new Paragraph("Synthetic stations 13 and 3.5 cost $4.50 and $14.50.")]);

        var issues = LockedFieldValidator.Validate(document,
        [
            new LockedField(LockedFieldKind.Number, "3"),
            new LockedField(LockedFieldKind.Number, "$4.5"),
        ]);

        Assert.Equal(2, issues.Count(issue => issue.Code == "locked.missing"));
    }

    [Theory]
    [InlineData(LockedFieldKind.Number, "3", "-3")]
    [InlineData(LockedFieldKind.Number, "3", "+3")]
    [InlineData(LockedFieldKind.Number, "3", "3%")]
    [InlineData(LockedFieldKind.Number, "3", "3\u0609")]
    [InlineData(LockedFieldKind.Number, "3", "3\u060A")]
    [InlineData(LockedFieldKind.Number, "3", "3\uFE6A")]
    [InlineData(LockedFieldKind.Number, "3", "3\uFF05")]
    [InlineData(LockedFieldKind.Number, "3", "3:30")]
    [InlineData(LockedFieldKind.Number, "3", "3/4")]
    [InlineData(LockedFieldKind.Number, "3", "3–5")]
    [InlineData(LockedFieldKind.Number, "3", "3..5")]
    [InlineData(LockedFieldKind.Number, "3", "3.,5")]
    [InlineData(LockedFieldKind.Number, "3", "3)4")]
    [InlineData(LockedFieldKind.Number, "3", "(3)")]
    [InlineData(LockedFieldKind.Number, "3", "( 3 )")]
    [InlineData(LockedFieldKind.Number, "3", "（3）")]
    [InlineData(LockedFieldKind.Number, "3", "［3］")]
    [InlineData(LockedFieldKind.Number, "3", "⦅3⦆")]
    [InlineData(LockedFieldKind.Number, "3", "﹙3﹚")]
    [InlineData(LockedFieldKind.Number, "3", "⁽3⁾")]
    [InlineData(LockedFieldKind.Number, "3", "₍3₎")]
    [InlineData(LockedFieldKind.Number, "3", "⟮3⟯")]
    [InlineData(LockedFieldKind.Number, "3", "︵3︶")]
    [InlineData(LockedFieldKind.Number, "3", "｟3｠")]
    [InlineData(LockedFieldKind.Number, "3", "﹇3﹈")]
    [InlineData(LockedFieldKind.Number, "3", "⁅3⁆")]
    [InlineData(LockedFieldKind.Number, "3", "⌈3⌉")]
    [InlineData(LockedFieldKind.Number, "3", "⌊3⌋")]
    [InlineData(LockedFieldKind.Number, "3", "⸨3⸩")]
    [InlineData(LockedFieldKind.Number, "$4.50", "($4.50)")]
    [InlineData(LockedFieldKind.Number, "€4.50", "(€4.50)")]
    [InlineData(LockedFieldKind.Number, "8:15 AM", "( 8:15 AM )")]
    [InlineData(LockedFieldKind.Number, "3", "- 3")]
    [InlineData(LockedFieldKind.Number, "3", "− 3")]
    [InlineData(LockedFieldKind.Number, "3", "+ 3")]
    [InlineData(LockedFieldKind.Number, "3", "$ 3")]
    [InlineData(LockedFieldKind.Number, "3", "3 %")]
    [InlineData(LockedFieldKind.Number, "3", "3\u202F%")]
    [InlineData(LockedFieldKind.Number, "3", "3 / 4")]
    [InlineData(LockedFieldKind.Number, "3", "3 – 5")]
    [InlineData(LockedFieldKind.Number, "3", "3 .5")]
    [InlineData(LockedFieldKind.Number, "3", "3 . 5")]
    [InlineData(LockedFieldKind.Number, "3", "3 ,000")]
    [InlineData(LockedFieldKind.Number, "3", "3 , 000")]
    [InlineData(LockedFieldKind.Number, "3", "3 €")]
    [InlineData(LockedFieldKind.Number, "3", "3 °C")]
    [InlineData(LockedFieldKind.Number, "3", "1 . 3")]
    [InlineData(LockedFieldKind.Number, "3", "1 , 3")]
    [InlineData(LockedFieldKind.Number, "3", "1 : 3")]
    [InlineData(LockedFieldKind.Number, "3", "1.. 3")]
    [InlineData(LockedFieldKind.Number, "3", "1 . . 3")]
    [InlineData(LockedFieldKind.Number, "3", "3. 5")]
    [InlineData(LockedFieldKind.Number, "3", "3.. 5")]
    [InlineData(LockedFieldKind.Number, "3", "3, 000")]
    [InlineData(LockedFieldKind.Number, "3", "3: 30")]
    [InlineData(LockedFieldKind.Number, "3", "3\u202F000")]
    [InlineData(LockedFieldKind.Number, "000", "3\u202F000")]
    [InlineData(LockedFieldKind.Number, "3", "3  000")]
    [InlineData(LockedFieldKind.Number, "3", "Values 1  3  5")]
    [InlineData(LockedFieldKind.Date, "June 10", "June 10..11")]
    [InlineData(LockedFieldKind.Number, "٣", "٣٫٥")]
    [InlineData(LockedFieldKind.Number, "٣", "٣٬٥٠٠")]
    [InlineData(LockedFieldKind.Url, "https://example.invalid/Path?q=A", "https://example.invalid/Path?q=A&next=B")]
    [InlineData(LockedFieldKind.Url, "https://example.invalid/Path?q=A", "https://example.invalid/Path?q=A#part")]
    [InlineData(LockedFieldKind.Url, "https://example.invalid/a", "https://example.invalid/a.?x=1")]
    [InlineData(LockedFieldKind.Url, "https://example.invalid/a", "https://example.invalid/a..evil")]
    [InlineData(LockedFieldKind.Url, "https://example.invalid/a", "https://example.invalid/a,,evil")]
    [InlineData(LockedFieldKind.Url, "https://example.invalid/a", "https://example.invalid/a?")]
    [InlineData(LockedFieldKind.Url, "https://example.invalid/a", "https://example.invalid/a;")]
    [InlineData(LockedFieldKind.Url, "https://example.invalid/a", "https://example.invalid/a:")]
    [InlineData(LockedFieldKind.Url, "https://example.invalid/a", "https://example.invalid/a.")]
    [InlineData(LockedFieldKind.ProperName, "𐐀", "𐐁𐐀𐐂")]
    public void Semantic_token_extensions_do_not_satisfy_exact_locks(
        LockedFieldKind kind,
        string exactValue,
        string changedValue)
    {
        var issues = LockedFieldValidator.Validate(
            new ArtifactDocument([new Paragraph(changedValue)]),
            [new LockedField(kind, exactValue)]);

        Assert.Contains(issues, issue => issue.Code == "locked.missing");
    }

    [Theory]
    [InlineData(LockedFieldKind.Number, "٣", "Use ٣، then continue.")]
    [InlineData(LockedFieldKind.Number, "3", "Use 3 — then continue.")]
    [InlineData(LockedFieldKind.Number, "3", "Use 3 – then continue.")]
    [InlineData(LockedFieldKind.Unit, "25 mL", "Use 25 mL — then stop.")]
    [InlineData(LockedFieldKind.Number, "3", "Use 「3」 in the CJK example.")]
    [InlineData(LockedFieldKind.Number, "3", "Use 『3』 in the CJK example.")]
    [InlineData(LockedFieldKind.Number, "3", "使用「3」继续")]
    [InlineData(LockedFieldKind.Number, "3", "使用“3”继续")]
    [InlineData(LockedFieldKind.Number, "3", "使用｢3｣继续")]
    [InlineData(LockedFieldKind.Number, "3", "使用﹁3﹂继续")]
    [InlineData(LockedFieldKind.Number, "3", "使用﹃3﹄继续")]
    [InlineData(LockedFieldKind.Number, "3", "\u31053\u3105")]
    [InlineData(LockedFieldKind.Number, "3", "\u31F03\u31F0")]
    [InlineData(LockedFieldKind.Number, "3", "\U000300003\U00030000")]
    [InlineData(LockedFieldKind.Number, "3", "\U000313503\U00031350")]
    [InlineData(LockedFieldKind.Number, "3", "第3冊へ進む。")]
    [InlineData(LockedFieldKind.Number, "3", "3。次へ")]
    [InlineData(LockedFieldKind.Url, "https://example.invalid/a", "https://example.invalid/a。次へ")]
    [InlineData(LockedFieldKind.Number, "3", "Results — 3 students")]
    [InlineData(LockedFieldKind.Number, "3", "Results – 3 students")]
    [InlineData(LockedFieldKind.ProperName, "𐐀", "Use 𐐀.")]
    [InlineData(LockedFieldKind.Url, "https://example.invalid/Path?q=A", "See https://example.invalid/Path?q=A")]
    public void Unicode_and_url_values_still_pass_at_terminal_boundaries(
        LockedFieldKind kind,
        string exactValue,
        string text)
    {
        var issues = LockedFieldValidator.Validate(
            new ArtifactDocument([new Paragraph(text)]),
            [new LockedField(kind, exactValue)]);

        Assert.Empty(issues);
    }

    [Theory]
    [InlineData("3. Read the next instruction.")]
    [InlineData("3) Read the next instruction.")]
    [InlineData("  3: Read the next instruction.")]
    [InlineData("First line.\n3． Read the next instruction.")]
    public void A_numeric_list_marker_cannot_substitute_for_a_locked_quantity(string text)
    {
        var issues = LockedFieldValidator.Validate(
            new ArtifactDocument([new Paragraph(text)]),
            [new LockedField(LockedFieldKind.Number, "3")]);

        Assert.Contains(issues, issue => issue.Code == "locked.missing");
    }

    [Theory]
    [InlineData("Use 3. Read the next instruction.")]
    [InlineData("3 students read the next instruction.")]
    [InlineData("Use 3, then read the next instruction.")]
    public void A_quantity_outside_a_leading_list_marker_still_passes(string text)
    {
        var issues = LockedFieldValidator.Validate(
            new ArtifactDocument([new Paragraph(text)]),
            [new LockedField(LockedFieldKind.Number, "3")]);

        Assert.Empty(issues);
    }

    [Fact]
    public void Adjacent_numeric_document_nodes_do_not_change_each_others_boundaries()
    {
        var issues = LockedFieldValidator.Validate(
            new ArtifactDocument(
            [
                new Paragraph("Previous 2"),
                new Paragraph("3"),
                new Paragraph("4"),
            ]),
            [new LockedField(LockedFieldKind.Number, "3")]);

        Assert.Empty(issues);
    }

    [Fact]
    public void A_teacher_only_fact_lock_summary_cannot_satisfy_the_fact_it_reports()
    {
        const string summary = "Fact-lock summary: DATE-ABSENT";
        var issues = LockedFieldValidator.Validate(
            new ArtifactDocument(
            [
                new Paragraph("The source omits the declared date."),
                new TeacherOnlyNotice(summary),
            ]),
            [new LockedField(LockedFieldKind.Date, "DATE-ABSENT")],
            [summary]);

        Assert.Contains(issues, issue => issue.Code == "locked.missing");
    }

    [Fact]
    public void Verified_source_text_in_a_teacher_only_notice_remains_lock_bearing()
    {
        var issues = LockedFieldValidator.Validate(
            new ArtifactDocument([new TeacherOnlyNotice("Teacher deadline Monday")]),
            [new LockedField(LockedFieldKind.Date, "Monday")]);

        Assert.Empty(issues);
    }

    [Fact]
    public void Fact_lock_summary_frames_delimiters_and_line_breaks_unambiguously()
    {
        const string exactValue = "3; ProperName: Alice\nnext";

        var summary = LockedFieldValidator.FormatInventorySummary(
            [new LockedField(LockedFieldKind.Quotation, exactValue)]);

        Assert.Contains("1 declaration", summary, StringComparison.Ordinal);
        Assert.Contains($"utf16-length={exactValue.Length}", summary, StringComparison.Ordinal);
        Assert.Contains("exact=\"3; ProperName: Alice\\nnext\"", summary, StringComparison.Ordinal);
        Assert.DoesNotContain('\n', summary);
    }

    [Fact]
    public void Fact_lock_summary_escapes_directional_and_invisible_format_controls()
    {
        var summary = LockedFieldValidator.FormatInventorySummary(
            [new LockedField(LockedFieldKind.Condition, "SAFE\u202Etxt\u200B\U000E0061")]);

        Assert.Contains("SAFE\\u202Etxt\\u200B\\U000E0061", summary, StringComparison.Ordinal);
        Assert.DoesNotContain('\u202E', summary);
        Assert.DoesNotContain('\u200B', summary);
        Assert.DoesNotContain("\U000E0061", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Exact_numeric_tokens_still_pass_at_sentence_punctuation_boundaries()
    {
        var document = new ArtifactDocument([new Paragraph("Use 3, then pay $4.5.")]);

        var issues = LockedFieldValidator.Validate(document,
        [
            new LockedField(LockedFieldKind.Number, "3"),
            new LockedField(LockedFieldKind.Number, "$4.5"),
        ]);

        Assert.Empty(issues);
    }

    [Fact]
    public void An_exact_delimited_coordinate_is_not_extended_by_a_preceding_equation_operator()
    {
        var document = new ArtifactDocument([new Paragraph("Plot A = (−3, 4).")]);

        var issues = LockedFieldValidator.Validate(document,
            [new LockedField(LockedFieldKind.Number, "(−3, 4)")]);

        Assert.Empty(issues);
    }

    [Fact]
    public void Exact_ordinal_phrases_and_urls_still_pass_without_normalization()
    {
        var document = new ArtifactDocument(
            [new Paragraph("Use Synthetic Phrase; then see https://example.invalid/Path?q=A")]);

        var issues = LockedFieldValidator.Validate(document,
        [
            new LockedField(LockedFieldKind.Condition, "Synthetic Phrase"),
            new LockedField(LockedFieldKind.Url, "https://example.invalid/Path?q=A"),
        ]);

        Assert.Empty(issues);
        Assert.Contains(
            LockedFieldValidator.Validate(document,
                [new LockedField(LockedFieldKind.Url, "https://example.invalid/path?q=A")]),
            issue => issue.Code == "locked.missing");
    }

    [Fact]
    public void A_combining_mark_is_a_lexical_continuation_not_a_boundary()
    {
        var document = new ArtifactDocument([new Paragraph("Synthetic Cafe\u0301 token.")]);

        var issues = LockedFieldValidator.Validate(document,
            [new LockedField(LockedFieldKind.ProperName, "Cafe")]);

        Assert.Contains(issues, issue => issue.Code == "locked.missing");
    }

    [Fact]
    public void Locked_values_are_found_in_both_sides_of_bilingual_pairs()
    {
        var issues = LockedFieldValidator.Validate(SomeDocument(),
            [new LockedField(LockedFieldKind.Number, "las 8:15 AM")]);

        Assert.Empty(issues);
    }

    [Fact]
    public void An_empty_locked_value_is_itself_a_blocking_issue()
    {
        var issues = LockedFieldValidator.Validate(SomeDocument(),
            [new LockedField(LockedFieldKind.Url, "  ")]);

        Assert.Contains(issues, i => i.Code == "locked.empty");
    }

    [Fact]
    public void Aligned_locked_values_must_use_the_same_row_indexes()
    {
        IReadOnlyList<(string SourceText, string? TargetText)> pairs =
        [
            ("Source A keeps LOCK-A.", "Target A keeps LOCK-B."),
            ("Source B keeps LOCK-B.", "Target B keeps LOCK-A."),
        ];

        var issues = LockedFieldValidator.ValidateAlignedPairs(
            pairs,
            [new LockedField(LockedFieldKind.Number, "LOCK-A")],
            "duet.locked",
            "Step");

        var issue = Assert.Single(issues);
        Assert.Equal("duet.locked", issue.Code);
        Assert.Contains("Step 1", issue.Message, StringComparison.Ordinal);
        Assert.Contains("may not move", issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Aligned_row_parity_does_not_impose_a_linguistic_repetition_count()
    {
        IReadOnlyList<(string SourceText, string? TargetText)> pairs =
        [
            ("LOCK-A then LOCK-A.", "LOCK-A."),
            ("No protected value here.", "No protected value here."),
        ];

        var issues = LockedFieldValidator.ValidateAlignedPairs(
            pairs,
            [new LockedField(LockedFieldKind.Number, "LOCK-A")],
            "duet.locked",
            "Step");

        Assert.Empty(issues);
    }

    [Fact]
    public void Aligned_validation_refuses_target_only_and_blank_declarations()
    {
        IReadOnlyList<(string SourceText, string? TargetText)> pairs =
        [
            ("Authoritative source.", "Target adds LOCK-A."),
        ];

        var issues = LockedFieldValidator.ValidateAlignedPairs(
            pairs,
            [
                new LockedField(LockedFieldKind.Number, "LOCK-A"),
                new LockedField(LockedFieldKind.Url, " "),
            ],
            "duet.locked",
            "Step");

        Assert.Contains(issues, issue => issue.Code == "duet.locked" && issue.Message.Contains("Step 1", StringComparison.Ordinal));
        Assert.Contains(issues, issue => issue.Code == "locked.empty");
    }

    [Fact]
    public void Aligned_validation_does_not_accept_numeric_substrings_on_both_sides()
    {
        IReadOnlyList<(string SourceText, string? TargetText)> pairs =
        [
            ("Synthetic station 13 costs $4.50.", "Synthetic target 13 costs $4.50."),
        ];

        var issues = LockedFieldValidator.ValidateAlignedPairs(
            pairs,
            [
                new LockedField(LockedFieldKind.Number, "3"),
                new LockedField(LockedFieldKind.Number, "$4.5"),
            ],
            "duet.locked",
            "Step");

        Assert.Equal(2, issues.Count(issue => issue.Code == "duet.locked"));
    }

    [Fact]
    public void Bilingual_validation_requires_global_exact_presence_but_not_row_parity()
    {
        IReadOnlyList<(string SourceText, string? TargetText)> pairs =
        [
            ("Source keeps LOCK-A.", "Target keeps LOCK-A."),
            ("Source has no repeated fact.", "Target repeats LOCK-A."),
        ];

        var valid = LockedFieldValidator.ValidateBilingualPairs(
            pairs,
            [new LockedField(LockedFieldKind.Number, "LOCK-A")],
            "bridge.locked",
            "Paragraph");
        var invalid = LockedFieldValidator.ValidateBilingualPairs(
            pairs,
            [
                new LockedField(LockedFieldKind.Number, "LOCK-B"),
                new LockedField(LockedFieldKind.Number, " "),
            ],
            "bridge.locked",
            "Paragraph");

        Assert.Empty(valid);
        Assert.Contains(invalid, issue => issue.Code == "bridge.locked");
        Assert.Contains(invalid, issue => issue.Code == "locked.empty");
    }
}
