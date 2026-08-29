// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Domain;

namespace Foundry.Modules.DeterministicPress;

/// <summary>
/// Field Journal Forge (atlas #218; second forge menu, item 6): nature and
/// field-learning kits from pure parameters — observation frames, specimen
/// labels, and site-map pages. The weather and phenology log rides the
/// existing lab-table press with field defaults; no new machinery for what
/// already exists.
/// </summary>
public static class FieldJournalForge
{
    public static ArtifactDocument ObservationFrame(
        IReadOnlyList<string> prompts,
        double sketchHeightMm = 110,
        PageSize size = PageSize.Letter,
        double marginMm = BlankformsPress.DefaultMarginMm)
    {
        ArgumentNullException.ThrowIfNull(prompts);
        if (prompts.Count is < 1 or > 6 || prompts.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Between one and six non-blank prompts.", nameof(prompts));
        }

        if (sketchHeightMm is < 40 or > 160)
        {
            throw new ArgumentException("A sketch box between 40 and 160 millimeters tall.", nameof(sketchHeightMm));
        }

        var (width, height) = BlankformsPress.Dimensions(size);
        const double lineSpacing = 8;
        const int linesPerPrompt = 3;
        var promptBlock = 7 + linesPerPrompt * lineSpacing;

        if (marginMm + sketchHeightMm + 6 + prompts.Count * promptBlock > height - marginMm)
        {
            throw new ArgumentException("Sketch box plus prompts must fit one page.", nameof(prompts));
        }

        var primitives = new List<VectorPrimitive>
        {
            new RectShape(marginMm, marginMm, width - 2 * marginMm, sketchHeightMm, 0.5),
        };

        var y = marginMm + sketchHeightMm + 6;
        foreach (var prompt in prompts)
        {
            primitives.Add(new TextLabel(marginMm, y + 5, prompt, 5, TextAnchor.Start));
            for (var line = 1; line <= linesPerPrompt; line++)
            {
                primitives.Add(new LineSeg(marginMm, y + 7 + line * lineSpacing, width - marginMm, y + 7 + line * lineSpacing, 0.3));
            }

            y += promptBlock;
        }

        return new ArtifactDocument([new VectorGraphic(width, height, primitives,
            $"A field observation frame: a sketch box above {prompts.Count} teacher-prompted, ruled writing sections")]);
    }

    public static ArtifactDocument SpecimenLabels(
        IReadOnlyList<string> fields,
        PageSize size = PageSize.Letter,
        double marginMm = BlankformsPress.DefaultMarginMm)
    {
        ArgumentNullException.ThrowIfNull(fields);
        if (fields.Count is < 2 or > 6 || fields.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Between two and six non-blank field names.", nameof(fields));
        }

        const int columns = 2;
        const int rows = 4;
        var (width, height) = BlankformsPress.Dimensions(size);
        var cardWidth = (width - 2 * marginMm) / columns;
        var cardHeight = (height - 2 * marginMm) / rows;
        var fieldSpacing = (cardHeight - 8) / fields.Count;

        var primitives = new List<VectorPrimitive>();
        for (var card = 0; card < columns * rows; card++)
        {
            var x = marginMm + card % columns * cardWidth;
            var y = marginMm + card / columns * cardHeight;
            primitives.Add(new RectShape(x, y, cardWidth, cardHeight, 0.5));

            for (var f = 0; f < fields.Count; f++)
            {
                var lineY = y + 8 + (f + 0.5) * fieldSpacing;
                primitives.Add(new TextLabel(x + 4, lineY, fields[f], 4.5, TextAnchor.Start));
                primitives.Add(new LineSeg(x + 8 + fields[f].Length * 2.5, lineY + 1, x + cardWidth - 4, lineY + 1, 0.3));
            }
        }

        return new ArtifactDocument([new VectorGraphic(width, height, primitives,
            $"Eight specimen labels, {fields.Count} write-in fields each, cut-ready in a {columns} by {rows} grid")]);
    }

    /// <summary>A gridded site-map page with a north arrow and a true scale bar; the scale is the teacher's declaration, printed.</summary>
    public static ArtifactDocument SiteMapPage(
        double pitchMm = 10,
        double metersPerSquare = 1,
        PageSize size = PageSize.Letter,
        double marginMm = BlankformsPress.DefaultMarginMm)
    {
        if (metersPerSquare is <= 0 or > 1000)
        {
            throw new ArgumentException("Between zero (exclusive) and a thousand meters per square.", nameof(metersPerSquare));
        }

        var baseDocument = BlankformsPress.GraphPaper(size, pitchMm, marginMm, majorEvery: 0);
        var graphic = (VectorGraphic)baseDocument.Nodes[0];
        var primitives = new List<VectorPrimitive>(graphic.Primitives);
        var width = graphic.WidthMm;
        var height = graphic.HeightMm;

        // North arrow, top-right, drawn in shape: shaft, two head strokes, "N".
        var ax = width - marginMm - 10;
        primitives.Add(new LineSeg(ax, marginMm + 22, ax, marginMm + 6, 0.7));
        primitives.Add(new LineSeg(ax - 3, marginMm + 10, ax, marginMm + 6, 0.7));
        primitives.Add(new LineSeg(ax + 3, marginMm + 10, ax, marginMm + 6, 0.7));
        primitives.Add(new TextLabel(ax, marginMm + 30, "N", 6));

        // Scale bar: five squares long, ticked per square, labeled in meters.
        var barY = height - marginMm - 6;
        var barLength = 5 * pitchMm;
        primitives.Add(new LineSeg(marginMm, barY, marginMm + barLength, barY, 0.7));
        for (var tick = 0; tick <= 5; tick++)
        {
            primitives.Add(new LineSeg(marginMm + tick * pitchMm, barY - 2, marginMm + tick * pitchMm, barY + 2, 0.5));
        }

        primitives.Add(new TextLabel(
            marginMm + barLength + 4, barY + 1.5,
            (5 * metersPerSquare).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " m",
            4, TextAnchor.Start));

        return new ArtifactDocument([graphic with
        {
            Primitives = primitives,
            Description = $"A site-map page: {pitchMm.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)} millimeter grid squares each representing {metersPerSquare.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)} meters, with a north arrow and scale bar",
        }]);
    }
}
