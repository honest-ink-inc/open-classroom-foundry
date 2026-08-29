// SPDX-License-Identifier: GPL-3.0-or-later
using System.Net;
using System.Text;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.DeterministicPress;
using Foundry.Rendering;

namespace Foundry.Tools.SiteGenerator;

/// <summary>
/// The samples gallery (fourth forge menu, item 8): a curated set of press
/// sheets rendered as inline SVG by the SAME deterministic engine that prints
/// them — the gift made visible before anyone installs anything. Every sample
/// is a catalog entry built at its own defaults; the standalone-SVG render
/// target is single-sheet only, so the curation holds only single-sheet
/// presses. Byte-identical on every build, like the rest of the site.
/// </summary>
public static class SampleGallery
{
    public const string Slug = "samples";

    public const string NavLabel = "Samples";

    /// <summary>Fixed approval instant, as the SampleGenerator uses: determinism over freshness.</summary>
    private static readonly DateTimeOffset ApprovedAt = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Curated single-sheet catalog entries, one per instructional seam.</summary>
    public static IReadOnlyList<string> CuratedPressIds { get; } =
    [
        "graph-paper",
        "hundred-chart",
        "timeline",
        "bar-chart",
        "trace-table",
        "fluency-rehearsal",
        "one-point-rubric",
        "peer-feedback",
    ];

    public static string BodyHtml()
    {
        var renderer = new AccessibleHtmlRenderer();
        var builder = new StringBuilder();
        builder.Append("<h1>Press samples</h1>\n");
        builder.Append("<p>Eight of the studio&#39;s presses, rendered by the same deterministic engine that prints them — byte-identical on every build. Each sheet below is a catalog entry built at its own defaults; in the app, every parameter is the teacher&#39;s. Nothing here was drawn by hand, and nothing changes between builds unless the presses themselves do.</p>\n");

        foreach (var id in CuratedPressIds)
        {
            var definition = PressRoomCatalog.ById(id);
            var document = definition.Build(new PressInputs(PressRoomCatalog.Defaults(definition)));
            var approved = ApprovalGate.Approve(
                DraftArtifact.New(document, DataLane.Green),
                "site-gallery@honest-ink.org",
                DocumentValidator.Validate(document),
                ApprovedAt);
            var svg = renderer.RenderAsync(
                    approved, new RenderRequest(RenderTarget.Svg, RenderAudience.Learner), CancellationToken.None)
                .GetAwaiter().GetResult();

            var description = document.Nodes.OfType<VectorGraphic>().Single().Description;
            builder.Append("<figure class=\"sample\">\n")
                .Append(Encoding.UTF8.GetString(svg.Content.ToArray()))
                .Append("\n<figcaption><strong>")
                .Append(WebUtility.HtmlEncode(definition.Title))
                .Append("</strong> — ")
                .Append(WebUtility.HtmlEncode(description))
                .Append("</figcaption>\n</figure>\n");
        }

        return builder.ToString();
    }
}
