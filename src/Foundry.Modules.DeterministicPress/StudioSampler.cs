// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Domain;

namespace Foundry.Modules.DeterministicPress;

/// <summary>
/// The Studio Sampler (fourth forge menu, item 9 — not in the atlas; born of
/// the enriched product): the forge printing its own catalog. One page per
/// engine at its own defaults, behind a cover that names EVERY engine with
/// its recipe id — nothing is ever skipped silently. Engines whose default
/// sheet is not portrait Letter (the booklet's one uniform size) and engines
/// that set prose instead of geometry are named on the cover with an honest
/// marker instead of a page. Like Big Print Shop, this is a composer over the
/// catalog, not a catalog entry: its input is the whole studio.
/// </summary>
public static class StudioSampler
{
    /// <summary>Who prints and who is only named — the partition the cover and the tests both stand on.</summary>
    public sealed record SamplerPlan(
        IReadOnlyList<(PressDefinition Definition, VectorGraphic Sheet)> Included,
        IReadOnlyList<(PressDefinition Definition, string Reason)> ListedOnly);

    public static SamplerPlan Plan()
    {
        var (letterWidth, letterHeight) = BlankformsPress.Dimensions(PageSize.Letter);
        var included = new List<(PressDefinition, VectorGraphic)>();
        var listedOnly = new List<(PressDefinition, string)>();

        foreach (var definition in PressRoomCatalog.All)
        {
            var document = definition.Build(new PressInputs(PressRoomCatalog.Defaults(definition)));
            var first = document.Nodes.OfType<VectorGraphic>().FirstOrDefault();
            if (first is null)
            {
                listedOnly.Add((definition, "prose"));
            }
            else if (first.WidthMm == letterWidth && first.HeightMm == letterHeight)
            {
                included.Add((definition, first));
            }
            else
            {
                listedOnly.Add((definition, first.WidthMm > first.HeightMm ? "landscape" : "off-size"));
            }
        }

        return new SamplerPlan(included, listedOnly);
    }

    /// <summary>The sampler document: the cover, then one default sheet per included engine, all one page size for the imposer.</summary>
    public static ArtifactDocument Catalog()
    {
        var plan = Plan();
        var nodes = new List<DocumentNode> { Cover(plan) };
        nodes.AddRange(plan.Included.Select(entry => entry.Sheet));
        return new ArtifactDocument(nodes);
    }

    private static VectorGraphic Cover(SamplerPlan plan)
    {
        var (width, height) = BlankformsPress.Dimensions(PageSize.Letter);
        const double marginMm = BlankformsPress.DefaultMarginMm;

        var markerById = plan.ListedOnly.ToDictionary(
            entry => entry.Definition.Id,
            entry => entry.Reason == "prose" ? "†" : "*",
            StringComparer.Ordinal);

        var total = PressRoomCatalog.All.Count;
        var primitives = new List<VectorPrimitive>
        {
            new TextLabel(width / 2, marginMm + 7, "The Studio Sampler", 6),
            new TextLabel(width / 2, marginMm + 14,
                $"{total} presses from exact parameters - one page each, at its own defaults", 4),
            new LineSeg(marginMm, marginMm + 18, width - marginMm, marginMm + 18, 0.5),
        };

        var rows = (total + 1) / 2;
        const double rowHeight = 6.2;
        var listTop = marginMm + 25;
        for (var i = 0; i < total; i++)
        {
            var definition = PressRoomCatalog.All[i];
            var marker = markerById.TryGetValue(definition.Id, out var mark) ? mark : "";
            var x = i < rows ? marginMm : width / 2 + 4;
            var y = listTop + i % rows * rowHeight;
            primitives.Add(new TextLabel(x, y,
                $"{definition.Title}{marker} - {definition.Recipe.Id}", 3.2, TextAnchor.Start));
        }

        primitives.Add(new TextLabel(marginMm, height - marginMm - 6,
            "* not bound here (its page is not this booklet's portrait size) - press it yourself in the app", 3.2, TextAnchor.Start));
        primitives.Add(new TextLabel(marginMm, height - marginMm - 1,
            "† set in type rather than geometry - press it yourself in the app", 3.2, TextAnchor.Start));

        return new VectorGraphic(width, height, primitives,
            $"The Studio Sampler cover: all {total} press engines named with their recipe ids; {plan.Included.Count} bound as pages, {plan.ListedOnly.Count} honestly marked as not bindable here");
    }
}
