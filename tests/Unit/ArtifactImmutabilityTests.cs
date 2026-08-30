// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Tests.Unit;

public sealed class ArtifactImmutabilityTests
{
    [Fact]
    public void Caller_owned_collections_cannot_mutate_an_approved_revision()
    {
        var ordered = new List<string> { "First synthetic action", "Second synthetic action" };
        var unordered = new List<string> { "Sample support" };
        var header = new List<string> { "Column" };
        var row = new List<string> { "Synthetic value" };
        var rows = new List<IReadOnlyList<string>> { row };
        var choices = new List<string> { "Help", "Wait" };
        var primitives = new List<VectorPrimitive>
        {
            new TextLabel(10, 10, "Synthetic label"),
        };
        var nodes = new List<DocumentNode>
        {
            new Heading(1, "Synthetic immutable artifact"),
            new OrderedSteps(ordered),
            new UnorderedList(unordered),
            new TableNode(header, rows),
            new ChoiceSet(choices),
            new VectorGraphic(100, 100, primitives, "Synthetic vector proof"),
        };
        var document = new ArtifactDocument(nodes, "en");
        var approved = ApprovalGate.Approve(
            DraftArtifact.New(document, DataLane.Green, ArtifactPurpose.ClassroomSupport),
            "Synthetic teacher",
            [],
            DateTimeOffset.UnixEpoch);
        var before = ArtifactDocumentFingerprint.Compute(approved.Revision.Document);

        nodes.Clear();
        ordered.Add("Unreviewed third action");
        unordered.Clear();
        header[0] = "Changed column";
        row[0] = "Changed value";
        rows.Add(["Injected row"]);
        choices.Add("Injected choice");
        primitives.Add(new TextLabel(20, 20, "Injected label"));

        Assert.Equal(before, ArtifactDocumentFingerprint.Compute(approved.Revision.Document));
        Assert.Equal(6, approved.Revision.Document.Nodes.Count);
        Assert.Equal(2, Assert.IsType<OrderedSteps>(approved.Revision.Document.Nodes[1]).Steps.Count);
        Assert.Single(Assert.IsType<UnorderedList>(approved.Revision.Document.Nodes[2]).Items);
        Assert.Single(Assert.IsType<TableNode>(approved.Revision.Document.Nodes[3]).Rows);
        Assert.Equal(2, Assert.IsType<ChoiceSet>(approved.Revision.Document.Nodes[4]).Options.Count);
        Assert.Single(Assert.IsType<VectorGraphic>(approved.Revision.Document.Nodes[5]).Primitives);
    }

    [Fact]
    public void Exposed_document_collections_are_read_only_at_every_nested_boundary()
    {
        var document = new ArtifactDocument(
        [
            new OrderedSteps(["Synthetic action"]),
            new UnorderedList(["Synthetic support"]),
            new TableNode(["Column"], [["Value"]]),
            new ChoiceSet(["Help"]),
            new VectorGraphic(10, 10, [new TextLabel(1, 1, "Label")], "Synthetic vector"),
        ]);

        Assert.Throws<NotSupportedException>(() => ((IList<DocumentNode>)document.Nodes).Clear());
        Assert.Throws<NotSupportedException>(() =>
            ((IList<string>)Assert.IsType<OrderedSteps>(document.Nodes[0]).Steps).Add("Injected"));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<string>)Assert.IsType<UnorderedList>(document.Nodes[1]).Items).Clear());
        var table = Assert.IsType<TableNode>(document.Nodes[2]);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)table.HeaderRow!).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<string>)table.Rows[0]).Clear());
        Assert.Throws<NotSupportedException>(() =>
            ((IList<string>)Assert.IsType<ChoiceSet>(document.Nodes[3]).Options).Clear());
        Assert.Throws<NotSupportedException>(() =>
            ((IList<VectorPrimitive>)Assert.IsType<VectorGraphic>(document.Nodes[4]).Primitives).Clear());
    }
}
