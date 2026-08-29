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
}
