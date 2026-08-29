using Foundry.Domain;
using Foundry.Modules.BuiltIn.DirectionsDuet;
using Foundry.Modules.BuiltIn.FamilyBridge;
using Xunit;

namespace Foundry.Tests.Unit;

public class FamilyBridgeTests
{
    private static readonly Glossary SchoolGlossary = new("2026-fall.2",
        [new GlossaryEntry("field trip", "excursión")]);

    private static IReadOnlyList<BridgeParagraph> Letter() =>
    [
        new BridgeParagraph("Our class has a field trip on Friday, October 9.", "Nuestra clase tiene una excursión el viernes, October 9."),
        new BridgeParagraph("The bus leaves at 8:15 AM.", "El autobús sale a las 8:15 AM."),
    ];

    [Fact]
    public void A_clear_letter_builds_with_ask_deadline_contact_and_the_no_recipient_statement()
    {
        var result = FamilyBridgeBuilder.Build(
            "Field trip on October 9", Letter(),
            requestedAction: "Sign the permission slip.",
            contact: "Ms. Rivera, room 12, 555-0140",
            SchoolGlossary,
            [new LockedField(LockedFieldKind.Date, "October 9"), new LockedField(LockedFieldKind.Number, "8:15")],
            deadline: "Wednesday, October 7",
            targetLocale: "es");

        Assert.False(DocumentValidator.HasBlockingIssues(result.Issues));
        Assert.Contains(result.Document.Nodes.OfType<Card>(), c => c.Title == "What we ask");
        Assert.Contains(result.Document.Nodes.OfType<Card>(), c => c.Title == "By when");
        Assert.Contains(result.Document.Nodes.OfType<Card>(), c => c.Title == "Questions? Contact");
        Assert.Contains(result.Document.Nodes.OfType<TeacherOnlyNotice>(),
            n => n.Text.Contains("no recipient list", StringComparison.Ordinal));
    }

    [Fact]
    public void Long_sentences_block_with_the_average_shown()
    {
        var winding = new BridgeParagraph(
            "As you may already be aware from previous communications sent home earlier in the marking period, our class will be taking part in an educational excursion to the science center which requires several forms.");

        var result = FamilyBridgeBuilder.Build(
            "Trip", [winding], "Sign the slip.", "Ms. Rivera", Glossary.Empty, []);

        var issue = Assert.Single(result.Issues, i => i.Code == "bridge.readability");
        Assert.Contains("20", issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Two_asks_in_one_letter_are_flagged()
    {
        var result = FamilyBridgeBuilder.Build(
            "Trip", [new BridgeParagraph("The trip is Friday.")],
            "Sign the slip and send four dollars.", "Ms. Rivera", Glossary.Empty, []);

        Assert.Contains(result.Issues, i => i.Code == "bridge.actions" && i.Severity == ValidationSeverity.Warning);
    }

    [Fact]
    public void The_ask_and_the_contact_are_never_optional()
    {
        var result = FamilyBridgeBuilder.Build(
            "Trip", [new BridgeParagraph("The trip is Friday.")], "  ", "  ", Glossary.Empty, []);

        Assert.Contains(result.Issues, i => i.Code == "bridge.action");
        Assert.Contains(result.Issues, i => i.Code == "bridge.contact");
    }

    [Fact]
    public void Bilingual_letters_enforce_alignment_glossary_and_locked_facts()
    {
        var missingTranslation = FamilyBridgeBuilder.Build(
            "Trip", [new BridgeParagraph("The bus leaves at 8:15.")],
            "Sign the slip.", "Ms. Rivera", SchoolGlossary,
            [new LockedField(LockedFieldKind.Number, "8:15")], targetLocale: "es");
        Assert.Contains(missingTranslation.Issues, i => i.Code == "bridge.target-missing");

        var wrongTerm = FamilyBridgeBuilder.Build(
            "Trip", [new BridgeParagraph("Our field trip is Friday.", "Nuestro paseo es el viernes.")],
            "Sign the slip.", "Ms. Rivera", SchoolGlossary, [], targetLocale: "es");
        Assert.Contains(wrongTerm.Issues, i => i.Code == "bridge.glossary");

        var droppedTime = FamilyBridgeBuilder.Build(
            "Trip", [new BridgeParagraph("The bus leaves at 8:15.", "El autobús sale temprano.")],
            "Sign the slip.", "Ms. Rivera", SchoolGlossary,
            [new LockedField(LockedFieldKind.Number, "8:15")], targetLocale: "es");
        Assert.Contains(droppedTime.Issues, i => i.Code == "bridge.locked");
    }

    [Fact]
    public void Translation_status_is_honest_exactly_like_the_duet()
    {
        var result = FamilyBridgeBuilder.Build(
            "Trip", Letter(), "Sign the slip.", "Ms. Rivera", SchoolGlossary, [], targetLocale: "es");

        var status = result.Document.Nodes.OfType<TeacherOnlyNotice>()
            .Single(n => n.Text.Contains("Translation status", StringComparison.Ordinal));
        Assert.Contains("NOT yet language-reviewed", status.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("machine", status.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_recipe_forbids_everything_the_module_must_never_become()
    {
        Assert.Contains(FamilyBridgeBuilder.Recipe.ProhibitedPurposes, p => p.Contains("recipient lists", StringComparison.Ordinal));
        Assert.Contains(FamilyBridgeBuilder.Recipe.ProhibitedPurposes, p => p.Contains("IEP/504", StringComparison.Ordinal));
        Assert.Equal(DataLane.Green, FamilyBridgeBuilder.Recipe.MaximumLane);
    }
}
