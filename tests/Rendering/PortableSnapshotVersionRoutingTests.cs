// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Rendering;

namespace Foundry.Tests.Rendering;

public sealed class PortableSnapshotVersionRoutingTests
{
    [Theory]
    [InlineData("0.1.0-dev", true)]
    [InlineData("0.7.0-alpha", true)]
    [InlineData("0.8.0-alpha", true)]
    [InlineData("0.8.0-ALPHA", false)]
    [InlineData("0.8.0-alpha ", false)]
    [InlineData(" 0.7.0-alpha", false)]
    [InlineData("0.9.0-alpha", false)]
    [InlineData("latest", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Snapshot_routes_are_finite_and_exact(string? version, bool supported)
        => Assert.Equal(supported, PortableProjectSnapshot.IsAdmittedRendererVersion(version!));

    [Theory]
    [InlineData(100, false)]
    [InlineData(150, true)]
    public void First_admission_and_candidate_routes_validate_the_same_exact_snapshot_contract(double scale, bool targetFirst)
    {
        var document = new ArtifactDocument([
            new Heading(1, "Synthetic version-route fixture"),
            new Paragraph("No learner, external source, or real review."),
        ]);
        var request = new RenderRequest(RenderTarget.AccessibleHtml, RenderAudience.Learner, scale, targetFirst);
        var expected = AccessibleHtmlRenderer.RenderPortableSnapshot(document, request);
        Assert.True(PortableProjectSnapshot.MatchesExact(document, "0.7.0-alpha", true, request, expected));
        Assert.True(PortableProjectSnapshot.MatchesExact(document, "0.8.0-alpha", true, request, expected));
        var changed = expected.ToArray();
        changed[^1] ^= 1;
        Assert.False(PortableProjectSnapshot.MatchesExact(document, "0.7.0-alpha", true, request, changed));
        Assert.False(PortableProjectSnapshot.MatchesExact(document, "0.8.0-alpha", true, request, changed));
        Assert.Throws<NotSupportedException>(() => PortableProjectSnapshot.MatchesExact(document, "0.9.0-alpha", true, request, expected));
    }
}
