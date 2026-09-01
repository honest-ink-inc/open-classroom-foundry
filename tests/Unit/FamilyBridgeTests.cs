using Foundry.Domain;
using Foundry.Modules.BuiltIn.DirectionsDuet;
using Foundry.Modules.BuiltIn.FamilyBridge;

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
            lockedFieldInventoryReviewed: true,
            deadline: "Wednesday, October 7",
            targetLocale: "es",
            targetRequestedAction: "Firme el permiso.",
            targetContact: "Ms. Rivera, room 12, 555-0140",
            targetDeadline: "Wednesday, October 7");

        Assert.False(DocumentValidator.HasBlockingIssues(result.Issues));
        Assert.Contains(result.Document.Nodes.OfType<Heading>(), heading => heading.Level == 2 && heading.Text == "What we ask");
        Assert.Contains(result.Document.Nodes.OfType<Heading>(), heading => heading.Level == 2 && heading.Text == "By when");
        Assert.Contains(result.Document.Nodes.OfType<Heading>(), heading => heading.Level == 2 && heading.Text == "Questions? Contact");
        Assert.Contains(result.Document.Nodes.OfType<BilingualPair>(), pair =>
            pair.SourceText == "Sign the permission slip."
            && pair.TargetText == "Firme el permiso.");
        Assert.Contains(result.Document.Nodes.OfType<TeacherOnlyNotice>(),
            n => n.Text.Contains("no recipient list", StringComparison.Ordinal));
        Assert.Contains(result.Document.Nodes.OfType<TeacherOnlyNotice>(),
            n => n.Text.Contains("Fact-lock summary", StringComparison.Ordinal)
                && n.Text.Contains("kind=Date", StringComparison.Ordinal)
                && n.Text.Contains("exact=\"October 9\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Long_sentences_block_with_the_average_shown()
    {
        var winding = new BridgeParagraph(
            "As you may already be aware from previous communications sent home earlier in the marking period, our class will be taking part in an educational excursion to the science center which requires several forms.");

        var result = FamilyBridgeBuilder.Build(
            "Trip", [winding], "Sign the slip.", "Ms. Rivera", Glossary.Empty, [],
            lockedFieldInventoryReviewed: true);

        var issue = Assert.Single(result.Issues, i => i.Code == "bridge.readability");
        Assert.Contains("20", issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Two_asks_in_one_letter_are_flagged()
    {
        var result = FamilyBridgeBuilder.Build(
            "Trip", [new BridgeParagraph("The trip is Friday.")],
            "Sign the slip and send four dollars.", "Ms. Rivera", Glossary.Empty, [],
            lockedFieldInventoryReviewed: true);

        Assert.Contains(result.Issues, i => i.Code == "bridge.actions" && i.Severity == ValidationSeverity.Warning);
    }

    [Fact]
    public void The_ask_and_the_contact_are_never_optional()
    {
        var result = FamilyBridgeBuilder.Build(
            "Trip", [new BridgeParagraph("The trip is Friday.")], "  ", "  ", Glossary.Empty, [],
            lockedFieldInventoryReviewed: true);

        Assert.Contains(result.Issues, i => i.Code == "bridge.action");
        Assert.Contains(result.Issues, i => i.Code == "bridge.contact");
    }

    [Fact]
    public void Bilingual_letters_enforce_alignment_glossary_and_locked_facts()
    {
        var missingTranslation = FamilyBridgeBuilder.Build(
            "Trip", [new BridgeParagraph("The bus leaves at 8:15.")],
            "Sign the slip.", "Ms. Rivera", SchoolGlossary,
            [new LockedField(LockedFieldKind.Number, "8:15")],
            lockedFieldInventoryReviewed: true,
            targetLocale: "es",
            targetRequestedAction: "Firme el permiso.",
            targetContact: "Ms. Rivera");
        Assert.Contains(missingTranslation.Issues, i => i.Code == "bridge.target-missing");

        var wrongTerm = FamilyBridgeBuilder.Build(
            "Trip", [new BridgeParagraph("Our field trip is Friday.", "Nuestro paseo es el viernes.")],
            "Sign the slip.", "Ms. Rivera", SchoolGlossary, [],
            lockedFieldInventoryReviewed: true,
            targetLocale: "es",
            targetRequestedAction: "Firme el permiso.",
            targetContact: "Ms. Rivera");
        Assert.Contains(wrongTerm.Issues, i => i.Code == "bridge.glossary");

        var droppedTime = FamilyBridgeBuilder.Build(
            "Trip", [new BridgeParagraph("The bus leaves at 8:15.", "El autobús sale temprano.")],
            "Sign the slip.", "Ms. Rivera", SchoolGlossary,
            [new LockedField(LockedFieldKind.Number, "8:15")],
            lockedFieldInventoryReviewed: true,
            targetLocale: "es",
            targetRequestedAction: "Firme el permiso.",
            targetContact: "Ms. Rivera");
        Assert.Contains(droppedTime.Issues, i => i.Code == "bridge.locked");
    }

    [Fact]
    public void Bilingual_letters_allow_a_locked_fact_to_repeat_in_another_target_paragraph()
    {
        var result = FamilyBridgeBuilder.Build(
            "One synthetic time",
            [
                new BridgeParagraph("Station A opens at 8:15.", "La estación A abre a las 8:15."),
                new BridgeParagraph("The office closes later.", "Recuerde la hora: 8:15."),
            ],
            "Read the time.",
            "School office",
            Glossary.Empty,
            [new LockedField(LockedFieldKind.Number, "8:15")],
            lockedFieldInventoryReviewed: true,
            targetLocale: "es",
            targetRequestedAction: "Lea la hora.",
            targetContact: "School office");

        Assert.DoesNotContain(result.Issues, issue => issue.Code == "bridge.locked");
        Assert.False(DocumentValidator.HasBlockingIssues(result.Issues));
    }

    [Fact]
    public void Bilingual_action_deadline_and_contact_are_explicit_role_bound_pairs()
    {
        var result = FamilyBridgeBuilder.Build(
            "Synthetic notice",
            [new BridgeParagraph("The event is soon.", "El evento es pronto.")],
            "Return FORM-7.",
            "OFFICE-9",
            Glossary.Empty,
            [
                new LockedField(LockedFieldKind.Condition, "FORM-7"),
                new LockedField(LockedFieldKind.Date, "DATE-10"),
                new LockedField(LockedFieldKind.ProperName, "OFFICE-9"),
            ],
            lockedFieldInventoryReviewed: true,
            deadline: "DATE-10",
            targetLocale: "es",
            targetRequestedAction: "Devuelva FORM-7.",
            targetContact: "OFFICE-9",
            targetDeadline: "DATE-10");

        Assert.False(DocumentValidator.HasBlockingIssues(result.Issues));
        Assert.Equal(4, result.Document.Nodes.OfType<BilingualPair>().Count());

        var changed = FamilyBridgeBuilder.Build(
            "Synthetic notice",
            [new BridgeParagraph("The event is DATE-10.", "El evento es DATE-10.")],
            "Return FORM-7.",
            "OFFICE-9",
            Glossary.Empty,
            [new LockedField(LockedFieldKind.Date, "DATE-10")],
            lockedFieldInventoryReviewed: true,
            deadline: "DATE-10",
            targetLocale: "es",
            targetRequestedAction: "Devuelva FORM-7.",
            targetContact: "OFFICE-9",
            targetDeadline: "DATE-11");

        Assert.Contains(changed.Issues, issue =>
            issue.Code == "bridge.locked"
            && issue.Severity == ValidationSeverity.Blocking);
    }

    [Fact]
    public void A_message_fact_cannot_move_into_the_target_contact_role()
    {
        var result = FamilyBridgeBuilder.Build(
            "Synthetic notice",
            [new BridgeParagraph("Event DATE-10.", "El evento es pronto.")],
            "Return the form.",
            "Office",
            Glossary.Empty,
            [new LockedField(LockedFieldKind.Date, "DATE-10")],
            lockedFieldInventoryReviewed: true,
            targetLocale: "es",
            targetRequestedAction: "Devuelva el formulario.",
            targetContact: "Office DATE-10");

        Assert.Contains(result.Issues, issue =>
            issue.Code == "bridge.locked"
            && issue.Severity == ValidationSeverity.Blocking
            && issue.Message.Contains("target message paragraphs", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_title_only_lock_fails_closed_until_bilingual_titles_are_supported()
    {
        var result = FamilyBridgeBuilder.Build(
            "Event CODE-7",
            [new BridgeParagraph("Source message.", "Target message.")],
            "Return the form.",
            "Office",
            Glossary.Empty,
            [new LockedField(LockedFieldKind.Condition, "CODE-7")],
            lockedFieldInventoryReviewed: true,
            targetLocale: "es",
            targetRequestedAction: "Return target.",
            targetContact: "Office");

        Assert.Contains(result.Issues, issue =>
            issue.Code == "bridge.locked"
            && issue.Severity == ValidationSeverity.Blocking
            && issue.Message.Contains("supported source", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_generated_family_notice_cannot_satisfy_a_teacher_declared_lock()
    {
        var result = FamilyBridgeBuilder.Build(
            "Synthetic notice",
            [new BridgeParagraph("Source message.")],
            "Return the form.",
            "Office",
            Glossary.Empty,
            [new LockedField(LockedFieldKind.Condition, "no recipient list")],
            lockedFieldInventoryReviewed: true);

        Assert.Contains(result.Issues, issue =>
            issue.Code == "locked.missing"
            && issue.Severity == ValidationSeverity.Blocking);
    }

    [Theory]
    [InlineData("What we ask")]
    [InlineData("By when")]
    [InlineData("Questions? Contact")]
    public void A_generated_card_title_cannot_satisfy_a_teacher_declared_lock(string generatedTitle)
    {
        var result = FamilyBridgeBuilder.Build(
            "Synthetic notice",
            [new BridgeParagraph("Source message.")],
            "Return the form.",
            "School office",
            Glossary.Empty,
            [new LockedField(LockedFieldKind.ProperName, generatedTitle)],
            lockedFieldInventoryReviewed: true,
            deadline: "Friday");

        Assert.Contains(result.Issues, issue =>
            issue.Code == "locked.missing"
            && issue.Severity == ValidationSeverity.Blocking);
    }

    [Fact]
    public void Bilingual_explicit_fields_refuse_missing_target_versions()
    {
        var result = FamilyBridgeBuilder.Build(
            "Synthetic notice",
            [new BridgeParagraph("The event is soon.", "El evento es pronto.")],
            "Return the form.",
            "School office",
            Glossary.Empty,
            [],
            lockedFieldInventoryReviewed: true,
            deadline: "June 10",
            targetLocale: "es");

        Assert.Contains(result.Issues, issue => issue.Code == "bridge.target-action-missing");
        Assert.Contains(result.Issues, issue => issue.Code == "bridge.target-deadline-missing");
        Assert.Contains(result.Issues, issue => issue.Code == "bridge.target-contact-missing");
    }

    [Fact]
    public void Target_content_without_a_target_language_blocks_instead_of_being_discarded()
    {
        var result = FamilyBridgeBuilder.Build(
            "Synthetic notice",
            [new BridgeParagraph("Source message.", "Unreviewed target message.")],
            "Return the form.",
            "School office",
            Glossary.Empty,
            [],
            lockedFieldInventoryReviewed: true,
            targetRequestedAction: "Unreviewed target action.");

        Assert.Contains(result.Issues, issue =>
            issue.Code == "bridge.target-without-locale"
            && issue.Severity == ValidationSeverity.Blocking);
    }

    [Fact]
    public void Bilingual_numeric_substrings_and_blank_declarations_do_not_satisfy_locks()
    {
        var result = FamilyBridgeBuilder.Build(
            "Synthetic prices",
            [new BridgeParagraph("Station 13 costs $4.50.", "La estación 13 cuesta $4.50.")],
            "Read the price.",
            "School office",
            Glossary.Empty,
            [
                new LockedField(LockedFieldKind.Number, "3"),
                new LockedField(LockedFieldKind.Number, "$4.5"),
                new LockedField(LockedFieldKind.Number, " "),
            ],
            lockedFieldInventoryReviewed: true,
            targetLocale: "es",
            targetRequestedAction: "Lea el precio.",
            targetContact: "School office");

        Assert.Equal(2, result.Issues.Count(issue => issue.Code == "bridge.locked"));
        Assert.Contains(result.Issues, issue => issue.Code == "locked.empty");
    }

    [Fact]
    public void A_monolingual_letter_still_enforces_its_declared_locked_facts()
    {
        var result = FamilyBridgeBuilder.Build(
            "Synthetic notice",
            [new BridgeParagraph("The synthetic event is Friday.")],
            "Read the notice.",
            "School office",
            Glossary.Empty,
            [new LockedField(LockedFieldKind.Date, "DATE-ABSENT")],
            lockedFieldInventoryReviewed: true);

        Assert.Contains(result.Issues, issue =>
            issue.Code == "locked.missing"
            && issue.Severity == ValidationSeverity.Blocking);
    }

    [Fact]
    public void Translation_status_is_honest_exactly_like_the_duet()
    {
        var result = FamilyBridgeBuilder.Build(
            "Trip", Letter(), "Sign the slip.", "Ms. Rivera", SchoolGlossary, [],
            lockedFieldInventoryReviewed: true,
            targetLocale: "es",
            targetRequestedAction: "Firme el permiso.",
            targetContact: "Ms. Rivera");

        var status = result.Document.Nodes.OfType<TeacherOnlyNotice>()
            .Single(n => n.Text.Contains("Translation status", StringComparison.Ordinal));
        Assert.Contains("Working glossary 2026-fall.2", status.Text, StringComparison.Ordinal);
        Assert.Contains("not approved by this application", status.Text, StringComparison.Ordinal);
        Assert.Contains("NOT yet language-reviewed", status.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("machine", status.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Caller_text_cannot_self_attest_a_language_review()
    {
        var error = Assert.Throws<ArgumentException>(() => FamilyBridgeBuilder.Build(
            "Trip", Letter(), "Sign the slip.", "Ms. Rivera", SchoolGlossary, [],
            lockedFieldInventoryReviewed: true,
            targetLocale: "es", reviewedBy: "M. Alvarez, bilingual liaison"));

        Assert.Equal("reviewedBy", error.ParamName);
        Assert.Contains("cannot be self-attested", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Keyboard_language_tags_fail_clearly_before_a_family_letter_is_built()
    {
        var error = Assert.Throws<ArgumentException>(() => FamilyBridgeBuilder.Build(
            "Trip", Letter(), "Sign the slip.", "Ms. Rivera", SchoolGlossary, [],
            lockedFieldInventoryReviewed: true,
            sourceLocale: "en", targetLocale: "not_a_tag"));

        Assert.Equal("targetLocale", error.ParamName);
        Assert.Contains("structurally valid language tag", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Approval_requires_an_explicit_source_locked_fact_inventory_review()
    {
        var notReviewed = FamilyBridgeBuilder.Build(
            "Synthetic notice",
            [new BridgeParagraph("The synthetic event is Friday.")],
            "Read the notice.",
            "School office",
            Glossary.Empty,
            [],
            lockedFieldInventoryReviewed: false);
        var reviewedEmpty = FamilyBridgeBuilder.Build(
            "Synthetic notice",
            [new BridgeParagraph("The synthetic event is Friday.")],
            "Read the notice.",
            "School office",
            Glossary.Empty,
            [],
            lockedFieldInventoryReviewed: true);

        var issue = Assert.Single(notReviewed.Issues,
            candidate => candidate.Code == "locked.inventory-review-required");
        Assert.Equal(ValidationSeverity.Blocking, issue.Severity);
        Assert.DoesNotContain(reviewedEmpty.Issues,
            candidate => candidate.Code == "locked.inventory-review-required");
    }

    [Fact]
    public void Legacy_builder_signature_remains_reachable_but_fails_closed_on_inventory_review()
    {
        var result = FamilyBridgeBuilder.Build(
            "Synthetic notice",
            [new BridgeParagraph("Source message.")],
            "Read it.",
            "School office",
            Glossary.Empty,
            []);

        Assert.Contains(result.Issues, issue =>
            issue.Code == "locked.inventory-review-required"
            && issue.Severity == ValidationSeverity.Blocking);
    }

    [Fact]
    public void The_recipe_forbids_everything_the_module_must_never_become()
    {
        Assert.Contains(FamilyBridgeBuilder.Recipe.ProhibitedPurposes, p => p.Contains("recipient lists", StringComparison.Ordinal));
        Assert.Contains(FamilyBridgeBuilder.Recipe.ProhibitedPurposes, p => p.Contains("IEP/504", StringComparison.Ordinal));
        Assert.Equal(DataLane.Green, FamilyBridgeBuilder.Recipe.MaximumLane);
    }
}
