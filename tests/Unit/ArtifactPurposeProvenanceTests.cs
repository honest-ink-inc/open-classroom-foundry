// SPDX-License-Identifier: GPL-3.0-or-later
using System.Reflection;
using Foundry.Domain;

namespace Foundry.Tests.Unit;

public sealed class ArtifactPurposeProvenanceTests
{
    private static ArtifactDocument Document(string text = "Synthetic classroom support")
        => new([new Paragraph(text)], "en");

    [Fact]
    public void Public_callers_can_create_only_unknown_purpose_drafts_and_cannot_construct_revisions()
    {
        var publicFactories = typeof(DraftArtifact).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == nameof(DraftArtifact.New))
            .ToList();

        var factory = Assert.Single(publicFactories);
        Assert.Equal([typeof(ArtifactDocument), typeof(DataLane)],
            factory.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.Empty(typeof(DraftArtifact).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(typeof(ArtifactRevision).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Equal(ArtifactPurpose.Unknown, DraftArtifact.New(Document(), DataLane.Green).Revision.Purpose);
    }

    [Fact]
    public void Purpose_evidence_is_bound_to_one_exact_immutable_document_and_lane()
    {
        var exact = Document();
        var evidence = ArtifactPurposeEvidence.ClassroomSupport(
            exact,
            DataLane.Green,
            ArtifactPurposeAuthority.TestFixture);

        var draft = DraftArtifact.NewClassified(exact, DataLane.Green, evidence);

        Assert.Equal(ArtifactPurpose.ClassroomSupport, draft.Revision.Purpose);
        Assert.True(draft.Revision.HasAuthenticatedPurpose(ArtifactPurpose.ClassroomSupport));
        Assert.Throws<InvalidOperationException>(() =>
            DraftArtifact.NewClassified(Document(), DataLane.Green, evidence));
        Assert.Throws<InvalidOperationException>(() =>
            DraftArtifact.NewClassified(exact, DataLane.Amber, evidence));
    }

    [Fact]
    public void Generic_edits_clear_exact_document_purpose_evidence()
    {
        var exact = Document();
        var classified = DraftArtifact.New(
            exact,
            DataLane.Green,
            ArtifactPurpose.ClassroomSupport);

        var edited = classified.WithEditedDocument(Document("Edited classroom support"));

        Assert.Equal(classified.Revision.Id, edited.Revision.Id);
        Assert.Equal(classified.Revision.Number + 1, edited.Revision.Number);
        Assert.Equal(ArtifactPurpose.Unknown, edited.Revision.Purpose);
        Assert.False(edited.Revision.HasAuthenticatedPurpose(ArtifactPurpose.ClassroomSupport));
    }

    [Fact]
    public void Invalid_revision_lane_number_identity_and_purpose_metadata_are_refused()
    {
        var document = Document();

        Assert.Throws<ArgumentException>(() =>
            new ArtifactRevision(new ArtifactId(Guid.Empty), 1, document, DataLane.Green));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ArtifactRevision(ArtifactId.NewId(), 0, document, DataLane.Green));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ArtifactRevision(ArtifactId.NewId(), 1, document, (DataLane)99));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ArtifactPurposeEvidence.ForTest(document, DataLane.Green, ArtifactPurpose.Unknown));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ArtifactPurposeEvidence.ForTest(document, DataLane.Green, (ArtifactPurpose)99));
    }
}
