using Foundry.Domain;
using Xunit;

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
    public void Comparison_is_exact_not_fuzzy()
    {
        var issues = LockedFieldValidator.Validate(SomeDocument(),
            [new LockedField(LockedFieldKind.Number, "$4.5")]);

        // "$4.5" is a substring of "$4.50", so it is present verbatim — and "$4.51" is not.
        Assert.Empty(issues);
        Assert.Contains(
            LockedFieldValidator.Validate(SomeDocument(), [new LockedField(LockedFieldKind.Number, "$4.51")]),
            i => i.Code == "locked.missing");
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
}
