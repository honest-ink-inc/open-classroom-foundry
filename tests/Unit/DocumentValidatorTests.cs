using Foundry.Domain;

namespace Foundry.Tests.Unit;

public class DocumentValidatorTests
{
    [Fact]
    public void A_sound_document_has_no_blocking_issues()
    {
        var document = new ArtifactDocument(
        [
            new Heading(1, "Watering the class plants"),
            new OrderedSteps(["Pick up the watering can.", "Fill it to the line.", "Water each plant once."]),
            new ImageReference(new AssetId("symbols.watering-can.v1"), "A green watering can"),
            new BilingualPair("Water each plant once.", "Riega cada planta una vez.", "en", "es"),
            new ChoiceSet(["Water the plants", "Not now"]),
        ]);

        var issues = DocumentValidator.Validate(document);

        Assert.False(DocumentValidator.HasBlockingIssues(issues));
    }

    [Fact]
    public void Heading_levels_outside_one_to_six_block()
    {
        var issues = DocumentValidator.Validate(new ArtifactDocument([new Heading(7, "Too deep")]));

        Assert.Contains(issues, i => i.Code == "doc.heading.level" && i.Severity == ValidationSeverity.Blocking);
    }

    [Fact]
    public void An_image_without_alternative_text_blocks()
    {
        var issues = DocumentValidator.Validate(
            new ArtifactDocument([new ImageReference(new AssetId("symbols.stop.v1"), "  ")]));

        Assert.Contains(issues, i => i.Code == "doc.image.alt-text");
    }

    [Fact]
    public void A_single_option_is_not_a_choice()
    {
        var issues = DocumentValidator.Validate(new ArtifactDocument([new ChoiceSet(["Comply"])]));

        Assert.Contains(issues, i => i.Code == "doc.choice.options");
    }

    [Fact]
    public void An_empty_step_sequence_blocks()
    {
        var issues = DocumentValidator.Validate(new ArtifactDocument([new OrderedSteps([])]));

        Assert.Contains(issues, i => i.Code == "doc.steps.empty");
    }

    [Fact]
    public void A_bilingual_pair_without_locales_blocks()
    {
        var issues = DocumentValidator.Validate(
            new ArtifactDocument([new BilingualPair("Hello", "Hola", "", "es")]));

        Assert.Contains(issues, i => i.Code == "doc.bilingual.locale");
    }

    [Theory]
    [InlineData("e n")]
    [InlineData("not_a_tag")]
    [InlineData("en--US")]
    [InlineData("en-a")]
    [InlineData("en-US-ab")]
    [InlineData("sl-rozaj-ROZAJ")]
    [InlineData("en-a-foo-a-bar")]
    [InlineData("en-Latn-US-Latn")]
    public void Malformed_document_and_bilingual_language_tags_block(string invalidTag)
    {
        var issues = DocumentValidator.Validate(new ArtifactDocument(
        [
            new BilingualPair("Open.", "Abre.", invalidTag, "es-MX"),
        ], invalidTag));

        Assert.Contains(issues, issue => issue.Code == "doc.language.tag");
        Assert.Contains(issues, issue => issue.Code == "doc.bilingual.locale-tag");
        Assert.True(DocumentValidator.HasBlockingIssues(issues));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("es-MX")]
    [InlineData("zh-Hant")]
    [InlineData("de-DE-1996")]
    [InlineData("zh-cmn-Hans-CN")]
    [InlineData("sl-rozaj-biske-1994")]
    [InlineData("en-US-u-ca-gregory")]
    [InlineData("en-US-u-ca-gregory-x-test")]
    [InlineData("x-synth")]
    public void Portable_structurally_valid_language_tags_are_accepted(string languageTag)
    {
        var issues = DocumentValidator.Validate(new ArtifactDocument(
            [new Paragraph("Synthetic text")],
            languageTag));

        Assert.DoesNotContain(issues, issue => issue.Code == "doc.language.tag");
    }

    [Fact]
    public void Malformed_optional_step_locale_pairs_block_when_entered()
    {
        var issues = DocumentValidator.Validate(new ArtifactDocument(
        [
            new StepRow(
                "Open the folder.",
                TargetText: "Abre la carpeta.",
                SourceLocale: "e n",
                TargetLocale: "not_a_tag"),
        ], "en"));

        Assert.Equal(2, issues.Count(issue => issue.Code == "doc.step-row.locale-tag"));
        Assert.True(DocumentValidator.HasBlockingIssues(issues));
    }

    [Fact]
    public void Blank_list_items_block()
    {
        var issues = DocumentValidator.Validate(
            new ArtifactDocument([new UnorderedList(["One", "\t"])]));

        Assert.Contains(issues, issue => issue.Code == "doc.list.blank-item");
    }

    [Fact]
    public void Blank_and_ragged_table_cells_block()
    {
        var issues = DocumentValidator.Validate(
            new ArtifactDocument(
            [
                new TableNode(
                    ["First", " "],
                    [
                        ["one"],
                        ["two", ""],
                    ]),
            ]));

        Assert.Contains(issues, issue => issue.Code == "doc.table.blank-cell");
        Assert.Contains(issues, issue => issue.Code == "doc.table.ragged");
    }

    [Fact]
    public void A_table_without_columns_blocks()
    {
        var issues = DocumentValidator.Validate(
            new ArtifactDocument([new TableNode(null, [[]])]));

        Assert.Contains(issues, issue => issue.Code == "doc.table.columns");
    }

    [Fact]
    public void A_blank_card_body_blocks()
    {
        var issues = DocumentValidator.Validate(
            new ArtifactDocument([new Card("Synthetic title", "  ")]));

        Assert.Contains(issues, issue => issue.Code == "doc.card.body");
    }

    [Fact]
    public void Missing_bilingual_target_text_blocks_pairs_and_translation_marked_steps()
    {
        var issues = DocumentValidator.Validate(
            new ArtifactDocument(
            [
                new BilingualPair("Source", " ", "en", "es"),
                new StepRow("Source step", TargetText: null, SourceLocale: "en", TargetLocale: "es"),
            ]));

        Assert.Contains(issues, issue => issue.Code == "doc.bilingual.target");
        Assert.Contains(issues, issue => issue.Code == "doc.step-row.target");
    }

    [Fact]
    public void Blank_and_duplicate_choices_block()
    {
        var issues = DocumentValidator.Validate(
            new ArtifactDocument([new ChoiceSet(["Ready", " ", " ready "])]));

        Assert.Contains(issues, issue => issue.Code == "doc.choice.blank-option");
        Assert.Contains(issues, issue => issue.Code == "doc.choice.duplicate-option");
    }

    [Fact]
    public void Non_finite_and_invalid_vector_geometry_blocks_fail_closed()
    {
        var issues = DocumentValidator.Validate(
            new ArtifactDocument(
            [
                new VectorGraphic(
                    double.NaN,
                    double.PositiveInfinity,
                    [
                        new LineSeg(0, 0, 0, 0, double.NaN),
                        new CircleShape(double.NegativeInfinity, 1, 0, -1),
                        new RectShape(0, double.NaN, -1, 0, 0),
                        new TextLabel(double.PositiveInfinity, 0, "Label", 0, (TextAnchor)99),
                        null!,
                        new UnsupportedPrimitive(),
                    ],
                    "Synthetic invalid geometry"),
            ]));

        Assert.Contains(issues, issue => issue.Code == "doc.vector.size");
        Assert.Contains(issues, issue => issue.Code == "doc.vector.line");
        Assert.Contains(issues, issue => issue.Code == "doc.vector.circle");
        Assert.Contains(issues, issue => issue.Code == "doc.vector.rectangle");
        Assert.Contains(issues, issue => issue.Code == "doc.vector.label-geometry");
        Assert.Contains(issues, issue => issue.Code == "doc.vector.anchor");
        Assert.Equal(2, issues.Count(issue => issue.Code == "doc.vector.primitive"));
    }

    [Fact]
    public void Finite_positive_vector_geometry_remains_valid()
    {
        var document = new ArtifactDocument(
        [
            new VectorGraphic(
                210,
                297,
                [
                    new LineSeg(1, 2, 3, 4),
                    new CircleShape(5, 6, 2),
                    new RectShape(7, 8, 9, 10),
                    new TextLabel(11, 12, "Synthetic label"),
                ],
                "Synthetic vector sheet"),
        ]);

        var issues = DocumentValidator.Validate(document);

        Assert.False(DocumentValidator.HasBlockingIssues(issues));
    }

    private sealed record UnsupportedPrimitive : VectorPrimitive;
}
