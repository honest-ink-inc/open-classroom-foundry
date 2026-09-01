using Foundry.Domain;
using Foundry.Modules.BuiltIn.SourceLens;

namespace Foundry.Tests.Unit;

public class SourceLensTests
{
    private static SourceMetadata Letter() => new(
        Creator: "Hannah Ropes",
        Title: "Letter from a Union Army nurse to her daughter",
        Date: "November 1862",
        Type: "Personal letter",
        Rights: "Public domain",
        Provenance: "Library of Congress, Ropes family papers");

    private static InquiryPrompts Prompts() => new(
        Sourcing: ["Who wrote this, and how do we know?", "What was her position to see what she describes?"],
        Contextualization: ["What was happening near Washington in November 1862?"],
        CloseReading: ["Underline the sentence that surprised you; what word carries it?"],
        Corroboration: ["What other account from this hospital could confirm or complicate this?"],
        BoundedInterpretation: ["What can this one letter support saying - and what can it not?"]);

    [Fact]
    public void A_disciplined_inquiry_builds_with_the_source_card_and_separate_observation_table()
    {
        var result = SourceLensBuilder.Build(Letter(), "The wounded came in by the hundred...", true, Prompts());

        Assert.False(DocumentValidator.HasBlockingIssues(result.Issues));

        var card = result.Document.Nodes.OfType<TableNode>().First();
        Assert.Contains(card.Rows, r => r[0] == "Creator" && r[1] == "Hannah Ropes");
        Assert.Contains(card.Rows, r => r[0] == "Place" && r[1] == SourceLensBuilder.NotRecorded);

        var observe = result.Document.Nodes.OfType<TableNode>().Last();
        Assert.Contains("observe", observe.HeaderRow![0], StringComparison.Ordinal);
        Assert.Contains("infer", observe.HeaderRow![1], StringComparison.Ordinal);
        Assert.All(observe.Rows, row =>
        {
            Assert.Equal(SourceLensBuilder.ObservationPrompt, row[0]);
            Assert.Equal(SourceLensBuilder.InferencePrompt, row[1]);
        });
    }

    [Fact]
    public void Blank_metadata_blocks_but_an_explicit_unknown_is_scholarship()
    {
        var blank = SourceLensBuilder.Build(Letter() with { Date = "  " }, "Excerpt.", true, Prompts());
        Assert.Contains(blank.Issues, i => i.Code == "lens.metadata");

        var unknown = SourceLensBuilder.Build(Letter() with { Date = "unknown" }, "Excerpt.", true, Prompts());
        Assert.DoesNotContain(unknown.Issues, i => i.Code == "lens.metadata");
    }

    [Fact]
    public void Unknown_rights_emits_a_warning_but_this_builder_does_not_prove_sink_enforcement()
    {
        var result = SourceLensBuilder.Build(Letter() with { Rights = "unknown" }, "Excerpt.", true, Prompts());

        Assert.Contains(result.Issues, i => i.Code == "lens.rights-unknown" && i.Severity == ValidationSeverity.Warning);
    }

    [Fact]
    public void A_false_transcript_assertion_emits_a_blocking_issue()
    {
        var result = SourceLensBuilder.Build(Letter(), "Excerpt.", transcriptVerifiedByTeacher: false, Prompts());

        Assert.Contains(result.Issues, i => i.Code == "lens.transcript");
    }

    [Fact]
    public void Sourcing_and_corroboration_are_never_optional()
    {
        var noSourcing = Prompts() with { Sourcing = [] };
        Assert.Contains(SourceLensBuilder.Build(Letter(), "Excerpt.", true, noSourcing).Issues,
            i => i.Code == "lens.sourcing");

        var noCorroboration = Prompts() with { Corroboration = [] };
        Assert.Contains(SourceLensBuilder.Build(Letter(), "Excerpt.", true, noCorroboration).Issues,
            i => i.Code == "lens.corroboration");
    }

    [Fact]
    public void The_citation_formatter_is_honest_about_unknowns_and_never_invents()
    {
        Assert.Equal(
            "Hannah Ropes. Letter from a Union Army nurse to her daughter. November 1862. Personal letter. Library of Congress, Ropes family papers.",
            SourceLensBuilder.FormatCitation(Letter()));

        Assert.Equal(
            "unknown. Broadside. unknown. Printed notice.",
            SourceLensBuilder.FormatCitation(new SourceMetadata("", "Broadside", "", "Printed notice", "Public domain")));
    }
}
