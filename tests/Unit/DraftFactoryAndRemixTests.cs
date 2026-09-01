using System.Reflection;
using Foundry.Application;
using Foundry.Domain;
using Foundry.Modules.BuiltIn.AccessRemix;

namespace Foundry.Tests.Unit;

public class DraftFactoryTests
{
    private static SourceEnvelope Source(DataLane lane)
        => new("camera", "image/png", 1, lane, true, string.Empty, SessionByteReference.NewReference());

    [Fact]
    public void A_hand_passed_green_cannot_underride_inherited_amber()
    {
        // Product-owner-enacted RC-18 lane test: the caller asks for Green; the
        // sources say Amber. The fictional rehearsal supplied no council authority.
        var draft = DraftFactory.CreateFromSources(
            ArtifactDocument.Empty, [Source(DataLane.Green), Source(DataLane.Amber)], DataLane.Green);

        Assert.Equal(DataLane.Amber, draft.Revision.Lane);
    }

    [Fact]
    public void A_restricted_source_makes_a_restricted_draft_no_matter_what()
    {
        var draft = DraftFactory.CreateFromSources(
            ArtifactDocument.Empty, [Source(DataLane.Restricted)], DataLane.Green);

        Assert.Equal(DataLane.Restricted, draft.Revision.Lane);
    }

    [Fact]
    public void A_requested_lane_may_escalate_but_never_lower()
    {
        var escalated = DraftFactory.CreateFromSources(
            ArtifactDocument.Empty, [Source(DataLane.Green)], DataLane.Amber);

        Assert.Equal(DataLane.Amber, escalated.Revision.Lane);
    }

    [Fact]
    public void No_sources_means_pure_parameters_and_green()
    {
        Assert.Equal(DataLane.Green, DraftFactory.CreateFromSources(ArtifactDocument.Empty, []).Revision.Lane);
    }
}

public class AccessRemixTests
{
    private static ArtifactDocument Strip(int steps) => new(
    [
        new Heading(1, "Cleaning the paint station"),
        .. Enumerable.Range(1, steps).Select(DocumentNode (i) => new StepRow($"Step {i}.")),
    ]);

    [Fact]
    public void The_held_remixer_is_not_a_shipped_public_API()
    {
        Assert.True(typeof(AccessRemixer).IsNotPublic);
        Assert.Empty(typeof(AccessRemixer).GetMethods(
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));
        Assert.True(typeof(RemixResult).IsNotPublic);
    }

    [Fact]
    public void Chunking_inserts_page_breaks_between_groups_and_changes_no_text()
    {
        var result = AccessRemixer.Chunk(Strip(7), chunkSize: 3);

        Assert.Equal(2, result.Document.Nodes.OfType<PageBreak>().Count());
        Assert.Equal(
            DocumentText.CollectStrings(Strip(7)),
            DocumentText.CollectStrings(result.Document));
        Assert.Contains(result.TransformationReport, r => r.Contains("groups of 3", StringComparison.Ordinal));
    }

    [Fact]
    public void One_step_per_panel_breaks_after_every_step()
    {
        var result = AccessRemixer.OneStepPerPanel(Strip(4));

        Assert.Equal(4, result.Document.Nodes.OfType<PageBreak>().Count());
        Assert.Equal(
            DocumentText.CollectStrings(Strip(4)),
            DocumentText.CollectStrings(result.Document));
    }

    [Fact]
    public void The_remix_recipe_is_layout_only_by_declaration()
    {
        Assert.Contains(AccessRemixer.Recipe.ProhibitedPurposes, p => p.Contains("layout only", StringComparison.Ordinal));
        Assert.Equal(DataLane.Green, AccessRemixer.Recipe.MaximumLane);
    }
}
