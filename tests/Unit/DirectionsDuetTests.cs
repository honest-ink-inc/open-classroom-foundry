using Foundry.Domain;
using Foundry.Modules.BuiltIn.DirectionsDuet;

namespace Foundry.Tests.Unit;

public class DirectionsDuetTests
{
    private static readonly Glossary SchoolGlossary = new("2026-fall.2",
        [new GlossaryEntry("folder", "carpeta"), new GlossaryEntry("hallway", "pasillo")]);

    private static IReadOnlyList<DuetStep> Steps() =>
    [
        new DuetStep("Put your folder in the bin.", "Pon tu carpeta en la caja."),
        new DuetStep("Line up in the hallway at 8:15.", "Haz fila en el pasillo a las 8:15."),
        new DuetStep("Do not run.", "No corras."),
    ];

    [Fact]
    public void An_aligned_duet_builds_with_pairwise_steps_and_a_stamped_status()
    {
        var result = DirectionsDuetBuilder.Build(
            "Morning routine", Steps(), "en", "es", SchoolGlossary,
            [new LockedField(LockedFieldKind.Number, "8:15")],
            lockedFieldInventoryReviewed: true,
            comprehensionCheck: "Point to where the folders go.");

        Assert.False(DocumentValidator.HasBlockingIssues(result.Issues));
        Assert.Equal(3, result.Document.Nodes.OfType<BilingualPair>().Count());

        var status = result.Document.Nodes.OfType<TeacherOnlyNotice>()
            .Single(notice => notice.Text.Contains("Translation status", StringComparison.Ordinal));
        Assert.Contains("Working glossary 2026-fall.2", status.Text, StringComparison.Ordinal);
        Assert.Contains("not approved by this application", status.Text, StringComparison.Ordinal);
        Assert.Contains("NOT yet language-reviewed", status.Text, StringComparison.Ordinal);

        // RC-6: the status speaks only to review, never to origin.
        Assert.DoesNotContain("machine", status.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.Document.Nodes.OfType<TeacherOnlyNotice>(), notice =>
            notice.Text.Contains("Fact-lock summary", StringComparison.Ordinal)
            && notice.Text.Contains("kind=Number", StringComparison.Ordinal)
            && notice.Text.Contains("exact=\"8:15\"", StringComparison.Ordinal)
            && notice.Text.Contains("not language or specialist review", StringComparison.Ordinal));
    }

    [Fact]
    public void Caller_text_cannot_self_attest_a_language_review()
    {
        var error = Assert.Throws<ArgumentException>(() => DirectionsDuetBuilder.Build(
            "Morning routine", Steps(), "en", "es", SchoolGlossary, [],
            lockedFieldInventoryReviewed: true,
            reviewedBy: "M. Alvarez, bilingual liaison"));

        Assert.Equal("reviewedBy", error.ParamName);
        Assert.Contains("cannot be self-attested", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("e n", "es")]
    [InlineData("en", "not_a_tag")]
    public void Keyboard_language_tags_fail_clearly_before_a_duet_is_built(string sourceLocale, string targetLocale)
    {
        var error = Assert.Throws<ArgumentException>(() => DirectionsDuetBuilder.Build(
            "Morning routine", Steps(), sourceLocale, targetLocale, SchoolGlossary, [],
            lockedFieldInventoryReviewed: true));

        Assert.Contains("structurally valid language tag", error.Message, StringComparison.Ordinal);
        Assert.True(error.ParamName is "sourceLocale" or "targetLocale");
    }

    [Fact]
    public void A_glossary_violation_blocks_per_step()
    {
        var steps = new List<DuetStep>(Steps())
        {
            [0] = new DuetStep("Put your folder in the bin.", "Pon tu fólder en la caja."),
        };

        var result = DirectionsDuetBuilder.Build(
            "Morning routine", steps, "en", "es", SchoolGlossary, [],
            lockedFieldInventoryReviewed: true);

        Assert.Contains(result.Issues, i => i.Code == "duet.glossary"
            && i.Message.Contains("carpeta", StringComparison.Ordinal)
            && i.Message.Contains("not approved by this application", StringComparison.Ordinal));
    }

    [Fact]
    public void Glossary_matching_ignores_case_so_sentence_starts_cannot_escape()
    {
        var steps = new List<DuetStep>
        {
            new("Folder goes in the bin.", "El fólder va en la caja."),
        };

        var result = DirectionsDuetBuilder.Build(
            "Routine", steps, "en", "es", SchoolGlossary, [],
            lockedFieldInventoryReviewed: true);

        // RC-7: "Folder" still triggers the folder -> carpeta rule.
        Assert.Contains(result.Issues, i => i.Code == "duet.glossary");
    }

    [Fact]
    public void A_locked_time_missing_from_the_translation_blocks()
    {
        var steps = new List<DuetStep>(Steps())
        {
            [1] = new DuetStep("Line up in the hallway at 8:15.", "Haz fila en el pasillo."),
        };

        var result = DirectionsDuetBuilder.Build(
            "Morning routine", steps, "en", "es", SchoolGlossary,
            [new LockedField(LockedFieldKind.Number, "8:15")],
            lockedFieldInventoryReviewed: true);

        Assert.Contains(result.Issues, i => i.Code == "duet.locked" && i.Message.Contains("translation", StringComparison.Ordinal));
    }

    [Fact]
    public void Locked_values_cannot_be_swapped_between_aligned_steps()
    {
        var result = DirectionsDuetBuilder.Build(
            "Two timed steps",
            [
                new DuetStep("Open station A at 8:15.", "Abra la estación A a las 9:30."),
                new DuetStep("Open station B at 9:30.", "Abra la estación B a las 8:15."),
            ],
            "en",
            "es",
            Glossary.Empty,
            [
                new LockedField(LockedFieldKind.Number, "8:15"),
                new LockedField(LockedFieldKind.Number, "9:30"),
            ],
            lockedFieldInventoryReviewed: true);

        Assert.Contains(result.Issues, issue =>
            issue.Code == "duet.locked"
            && issue.Severity == ValidationSeverity.Blocking
            && issue.Message.Contains("Step 1", StringComparison.Ordinal));
    }

    [Fact]
    public void A_whitespace_only_aligned_lock_blocks_instead_of_matching_sentence_spaces()
    {
        var result = DirectionsDuetBuilder.Build(
            "Morning routine",
            Steps(),
            "en",
            "es",
            SchoolGlossary,
            [new LockedField(LockedFieldKind.Number, " ")],
            lockedFieldInventoryReviewed: true);

        Assert.Contains(result.Issues, issue =>
            issue.Code == "locked.empty"
            && issue.Severity == ValidationSeverity.Blocking
            && issue.Message.Contains("no value", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Numeric_substrings_do_not_satisfy_declared_exact_locks()
    {
        var result = DirectionsDuetBuilder.Build(
            "Synthetic prices",
            [new DuetStep("Station 13 costs $4.50.", "La estación 13 cuesta $4.50.")],
            "en",
            "es",
            Glossary.Empty,
            [
                new LockedField(LockedFieldKind.Number, "3"),
                new LockedField(LockedFieldKind.Number, "$4.5"),
            ],
            lockedFieldInventoryReviewed: true);

        Assert.Equal(2, result.Issues.Count(issue => issue.Code == "duet.locked"));
    }

    [Fact]
    public void A_missing_translation_breaks_one_to_one_alignment()
    {
        var steps = new List<DuetStep>(Steps()) { [2] = new DuetStep("Do not run.", " ") };

        var result = DirectionsDuetBuilder.Build(
            "Morning routine", steps, "en", "es", SchoolGlossary, [],
            lockedFieldInventoryReviewed: true);

        Assert.Contains(result.Issues, i => i.Code == "duet.target-missing");
    }

    [Fact]
    public void A_right_to_left_duet_builds_cleanly()
    {
        var result = DirectionsDuetBuilder.Build(
            "Lining up",
            [new DuetStep("Stand behind the line at 8:15.", "قف خلف الخط الساعة 8:15.")],
            "en", "ar", Glossary.Empty,
            [new LockedField(LockedFieldKind.Number, "8:15")],
            lockedFieldInventoryReviewed: true);

        Assert.False(DocumentValidator.HasBlockingIssues(result.Issues));
        Assert.Equal("ar", Assert.Single(result.Document.Nodes.OfType<BilingualPair>()).TargetLocale);
    }

    [Fact]
    public void Approval_requires_an_explicit_source_locked_fact_inventory_review()
    {
        var notReviewed = DirectionsDuetBuilder.Build(
            "Synthetic direction",
            [new DuetStep("Open the synthetic card.", "Abra la tarjeta sintética.")],
            "en",
            "es",
            Glossary.Empty,
            [],
            lockedFieldInventoryReviewed: false);
        var reviewedEmpty = DirectionsDuetBuilder.Build(
            "Synthetic direction",
            [new DuetStep("Open the synthetic card.", "Abra la tarjeta sintética.")],
            "en",
            "es",
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
        var result = DirectionsDuetBuilder.Build(
            "Synthetic directions",
            [new DuetStep("Source.", "Target.")],
            "en",
            "es",
            Glossary.Empty,
            []);

        Assert.Contains(result.Issues, issue =>
            issue.Code == "locked.inventory-review-required"
            && issue.Severity == ValidationSeverity.Blocking);
    }

    [Fact]
    public void The_recipe_forbids_certified_claims_and_consequential_directions()
    {
        Assert.Contains(DirectionsDuetBuilder.Recipe.ProhibitedPurposes, p => p.Contains("certified-translation", StringComparison.Ordinal));
        Assert.Contains(DirectionsDuetBuilder.Recipe.ProhibitedPurposes, p => p.Contains("emergency", StringComparison.Ordinal));
    }
}
