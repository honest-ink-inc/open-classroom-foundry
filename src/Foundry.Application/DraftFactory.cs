using Foundry.Domain;

namespace Foundry.Application;

/// <summary>
/// Council finding RC-18, binding: a draft born of captured sources inherits the
/// highest lane among them — computed, never asserted. A requested lane may
/// escalate the result and can never lower it: a photographed board bearing a
/// student's name is Amber, and every derivative of it is born Amber no matter
/// what the caller hoped.
/// </summary>
public static class DraftFactory
{
    public static DraftArtifact CreateFromSources(
        ArtifactDocument document,
        IReadOnlyList<SourceEnvelope> sources,
        DataLane requestedLane = DataLane.Green)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(sources);

        var inherited = LanePolicy.Inherit(sources.Select(s => s.Lane));
        return DraftArtifact.New(document, LanePolicy.Inherit(inherited, requestedLane));
    }
}
