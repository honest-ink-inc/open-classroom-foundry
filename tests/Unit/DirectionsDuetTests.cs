using Foundry.Domain;
using Foundry.Modules.BuiltIn.DirectionsDuet;
using Xunit;

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
            comprehensionCheck: "Point to where the folders go.");

        Assert.False(DocumentValidator.HasBlockingIssues(result.Issues));
        Assert.Equal(3, result.Document.Nodes.OfType<BilingualPair>().Count());

        var status = Assert.Single(result.Document.Nodes.OfType<TeacherOnlyNotice>());
        Assert.Contains("Glossary 2026-fall.2", status.Text, StringComparison.Ordinal);
        Assert.Contains("NOT language-reviewed", status.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_recorded_reviewer_changes_the_status_honestly()
    {
        var result = DirectionsDuetBuilder.Build(
            "Morning routine", Steps(), "en", "es", SchoolGlossary, [], reviewedBy: "M. Alvarez, bilingual liaison");

        Assert.Contains("language-reviewed by M. Alvarez",
            Assert.Single(result.Document.Nodes.OfType<TeacherOnlyNotice>()).Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_glossary_violation_blocks_per_step()
    {
        var steps = new List<DuetStep>(Steps())
        {
            [0] = new DuetStep("Put your folder in the bin.", "Pon tu fólder en la caja."),
        };

        var result = DirectionsDuetBuilder.Build("Morning routine", steps, "en", "es", SchoolGlossary, []);

        Assert.Contains(result.Issues, i => i.Code == "duet.glossary" && i.Message.Contains("carpeta", StringComparison.Ordinal));
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
            [new LockedField(LockedFieldKind.Number, "8:15")]);

        Assert.Contains(result.Issues, i => i.Code == "duet.locked" && i.Message.Contains("translation", StringComparison.Ordinal));
    }

    [Fact]
    public void A_missing_translation_breaks_one_to_one_alignment()
    {
        var steps = new List<DuetStep>(Steps()) { [2] = new DuetStep("Do not run.", " ") };

        var result = DirectionsDuetBuilder.Build("Morning routine", steps, "en", "es", SchoolGlossary, []);

        Assert.Contains(result.Issues, i => i.Code == "duet.target-missing");
    }

    [Fact]
    public void A_right_to_left_duet_builds_cleanly()
    {
        var result = DirectionsDuetBuilder.Build(
            "Lining up",
            [new DuetStep("Stand behind the line at 8:15.", "قف خلف الخط الساعة 8:15.")],
            "en", "ar", Glossary.Empty,
            [new LockedField(LockedFieldKind.Number, "8:15")]);

        Assert.False(DocumentValidator.HasBlockingIssues(result.Issues));
        Assert.Equal("ar", Assert.Single(result.Document.Nodes.OfType<BilingualPair>()).TargetLocale);
    }

    [Fact]
    public void The_recipe_forbids_certified_claims_and_consequential_directions()
    {
        Assert.Contains(DirectionsDuetBuilder.Recipe.ProhibitedPurposes, p => p.Contains("certified-translation", StringComparison.Ordinal));
        Assert.Contains(DirectionsDuetBuilder.Recipe.ProhibitedPurposes, p => p.Contains("emergency", StringComparison.Ordinal));
    }
}
