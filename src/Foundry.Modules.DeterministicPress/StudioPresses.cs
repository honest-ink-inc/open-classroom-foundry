using Foundry.Domain;

namespace Foundry.Modules.DeterministicPress;

/// <summary>
/// Manipulative Mint (spec §5.4): mathematical proportions are exact above all —
/// a strip labeled one-third IS one-third, asserted in tests as arithmetic.
/// </summary>
public static class ManipulativeMint
{
    public static ArtifactDocument FractionStrips(IReadOnlyList<int> denominators, PageSize size = PageSize.Letter, double marginMm = BlankformsPress.DefaultMarginMm)
    {
        ArgumentNullException.ThrowIfNull(denominators);
        if (denominators.Count == 0 || denominators.Any(d => d is < 2 or > 16))
        {
            throw new ArgumentException("Denominators between 2 and 16.", nameof(denominators));
        }

        var (width, height) = BlankformsPress.Dimensions(size);
        var stripWidth = width - 2 * marginMm;
        const double stripHeight = 18;
        const double rowGap = 8;

        var primitives = new List<VectorPrimitive>();
        var rows = new List<int> { 1 };
        rows.AddRange(denominators);

        for (var row = 0; row < rows.Count; row++)
        {
            var top = marginMm + row * (stripHeight + rowGap);
            var denominator = rows[row];
            var cellWidth = stripWidth / denominator;

            for (var cell = 0; cell < denominator; cell++)
            {
                primitives.Add(new RectShape(marginMm + cell * cellWidth, top, cellWidth, stripHeight, 0.5));
                primitives.Add(new TextLabel(
                    marginMm + cell * cellWidth + cellWidth / 2,
                    top + stripHeight / 2 + 2,
                    denominator == 1 ? "1" : $"1/{denominator}",
                    5));
            }
        }

        return new ArtifactDocument([new VectorGraphic(width, height, primitives,
            $"Fraction strips: one whole and {string.Join(", ", denominators.Select(d => $"1/{d}"))}")]);
    }

    public static ArtifactDocument DiceNet(double edgeMm = 30, PageSize size = PageSize.Letter)
    {
        var (width, height) = BlankformsPress.Dimensions(size);
        var originX = (width - 4 * edgeMm) / 2;
        var originY = (height - 3 * edgeMm) / 2;

        // Cross layout: face -> (column, row). Opposite faces sum to seven.
        (int Face, int Col, int Row)[] layout = [(1, 1, 0), (2, 0, 1), (3, 1, 1), (5, 2, 1), (4, 3, 1), (6, 1, 2)];

        var primitives = new List<VectorPrimitive>();
        foreach (var (face, col, row) in layout)
        {
            var x = originX + col * edgeMm;
            var y = originY + row * edgeMm;
            primitives.Add(new RectShape(x, y, edgeMm, edgeMm, 0.5));
            primitives.AddRange(Pips(face, x, y, edgeMm));
        }

        // Shared edges are folds, not cuts.
        primitives.Add(new LineSeg(originX + edgeMm, originY + edgeMm, originX + 2 * edgeMm, originY + edgeMm, 0.35, Dashed: true));
        primitives.Add(new LineSeg(originX + edgeMm, originY + 2 * edgeMm, originX + 2 * edgeMm, originY + 2 * edgeMm, 0.35, Dashed: true));
        primitives.Add(new LineSeg(originX + edgeMm, originY + edgeMm, originX + edgeMm, originY + 2 * edgeMm, 0.35, Dashed: true));
        primitives.Add(new LineSeg(originX + 2 * edgeMm, originY + edgeMm, originX + 2 * edgeMm, originY + 2 * edgeMm, 0.35, Dashed: true));
        primitives.Add(new LineSeg(originX + 3 * edgeMm, originY + edgeMm, originX + 3 * edgeMm, originY + 2 * edgeMm, 0.35, Dashed: true));

        return new ArtifactDocument([new VectorGraphic(width, height, primitives,
            "A cut-and-fold die net: six faces in a cross, opposite faces summing to seven; solid lines cut, dashed lines fold")]);
    }

    private static IEnumerable<VectorPrimitive> Pips(int face, double x, double y, double edge)
    {
        var c = edge / 2;
        var q = edge / 4;
        var radius = edge / 12;

        (double X, double Y)[] positions = face switch
        {
            1 => [(c, c)],
            2 => [(q, q), (edge - q, edge - q)],
            3 => [(q, q), (c, c), (edge - q, edge - q)],
            4 => [(q, q), (edge - q, q), (q, edge - q), (edge - q, edge - q)],
            5 => [(q, q), (edge - q, q), (c, c), (q, edge - q), (edge - q, edge - q)],
            _ => [(q, q), (edge - q, q), (q, c), (edge - q, c), (q, edge - q), (edge - q, edge - q)],
        };

        return positions.Select(p => (VectorPrimitive)new CircleShape(x + p.X, y + p.Y, radius, 0.3, Filled: true));
    }
}

/// <summary>
/// Foldables Foundry (spec §5.5): a consistent line language — solid cuts, dashed
/// folds — with a printed legend on every sheet. Physical assembly verification
/// belongs to the hardware bench, not to this code's claims.
/// </summary>
public static class FoldablesFoundry
{
    public static ArtifactDocument FlapBook(int flaps, IReadOnlyList<string>? flapLabels = null, PageSize size = PageSize.Letter, double marginMm = BlankformsPress.DefaultMarginMm)
    {
        if (flaps is < 2 or > 8)
        {
            throw new ArgumentException("Between two and eight flaps.", nameof(flaps));
        }

        if (flapLabels is not null && flapLabels.Count != flaps)
        {
            throw new ArgumentException("One label per flap, or none.", nameof(flapLabels));
        }

        var (width, height) = BlankformsPress.Dimensions(size);
        var foldY = height / 2;
        var left = marginMm;
        var right = width - marginMm;
        var flapWidth = (right - left) / flaps;

        var primitives = new List<VectorPrimitive>
        {
            new RectShape(left, marginMm, right - left, height - 2 * marginMm, 0.5),
            new LineSeg(left, foldY, right, foldY, 0.5, Dashed: true),
        };

        for (var i = 1; i < flaps; i++)
        {
            primitives.Add(new LineSeg(left + i * flapWidth, marginMm, left + i * flapWidth, foldY, 0.5));
        }

        for (var i = 0; i < flaps; i++)
        {
            if (flapLabels is not null)
            {
                primitives.Add(new TextLabel(left + i * flapWidth + flapWidth / 2, marginMm + 12, flapLabels[i], 5));
            }
        }

        // Legend: the line language, printed on every sheet.
        var legendY = height - marginMm / 2;
        primitives.Add(new LineSeg(left, legendY, left + 14, legendY, 0.5));
        primitives.Add(new TextLabel(left + 17, legendY + 1.5, "cut", 4, TextAnchor.Start));
        primitives.Add(new LineSeg(left + 34, legendY, left + 48, legendY, 0.5, Dashed: true));
        primitives.Add(new TextLabel(left + 51, legendY + 1.5, "fold", 4, TextAnchor.Start));

        return new ArtifactDocument([new VectorGraphic(width, height, primitives,
            $"A {flaps}-flap foldable: dashed center fold, solid cut lines between flaps, legend at the foot")]);
    }
}

/// <summary>
/// Big Print Shop (spec §5.6): tiles any vector sheet across pages at exactly 100
/// percent scale, with overlap strips, alignment marks, and an assembly note on
/// every tile. Out-of-window geometry clips naturally at the SVG boundary.
/// </summary>
public static class BigPrintShop
{
    public static ArtifactDocument Tile(VectorGraphic source, int columns, int rows, double overlapMm = 10, PageSize size = PageSize.Letter)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (columns < 1 || rows < 1 || columns * rows < 2)
        {
            throw new ArgumentException("Tiling needs at least two pages.", nameof(columns));
        }

        var (pageWidth, pageHeight) = BlankformsPress.Dimensions(size);

        var nodes = new List<DocumentNode>
        {
            new TeacherOnlyNotice(
                $"Wall display: {rows} rows × {columns} columns. Print every tile at exactly 100 percent scale, overlap the shaded strips by {Fmt(overlapMm)} mm, and align the corner marks."),
        };

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var offsetX = column * (pageWidth - overlapMm);
                var offsetY = row * (pageHeight - overlapMm);

                var primitives = source.Primitives.Select(p => Translate(p, -offsetX, -offsetY)).ToList();

                foreach (var (x, y) in new[] { (4.0, 4.0), (pageWidth - 4, 4.0), (4.0, pageHeight - 4), (pageWidth - 4, pageHeight - 4) })
                {
                    primitives.Add(new LineSeg(x - 3, y, x + 3, y, 0.3));
                    primitives.Add(new LineSeg(x, y - 3, x, y + 3, 0.3));
                }

                primitives.Add(new TextLabel(pageWidth / 2, pageHeight - 5,
                    $"Row {row + 1}, column {column + 1} of {rows}x{columns} - print at 100%", 3.5));

                nodes.Add(new VectorGraphic(pageWidth, pageHeight, primitives,
                    $"Wall-display tile, row {row + 1} of {rows}, column {column + 1} of {columns}"));
            }
        }

        return new ArtifactDocument(nodes);
    }

    private static VectorPrimitive Translate(VectorPrimitive primitive, double dx, double dy) => primitive switch
    {
        LineSeg l => l with { X1 = l.X1 + dx, Y1 = l.Y1 + dy, X2 = l.X2 + dx, Y2 = l.Y2 + dy },
        CircleShape c => c with { CenterX = c.CenterX + dx, CenterY = c.CenterY + dy },
        RectShape r => r with { X = r.X + dx, Y = r.Y + dy },
        TextLabel t => t with { X = t.X + dx, Y = t.Y + dy },
        _ => throw new NotSupportedException($"Unknown vector primitive {primitive.GetType().Name}."),
    };

    private static string Fmt(double value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// Handwriting Foundry (spec §5.7), Latin script first: three-line practice rows
/// with the dashed midline convention and optional model words to copy. Dotted and
/// faded glyph tracing requires font-outline geometry from an OFL face and stays
/// deferred; model-word copying rows are the honest MVP.
/// </summary>
public static class HandwritingFoundry
{
    public static ArtifactDocument PracticeRows(int rows = 8, double xHeightMm = 8, IReadOnlyList<string>? modelWords = null, PageSize size = PageSize.Letter, double marginMm = BlankformsPress.DefaultMarginMm)
    {
        if (rows is < 1 or > 12)
        {
            throw new ArgumentException("Between one and twelve rows fit a page.", nameof(rows));
        }

        var (width, height) = BlankformsPress.Dimensions(size);
        var left = marginMm;
        var right = width - marginMm;
        var rowHeight = 2 * xHeightMm;
        var gap = (height - 2 * marginMm - rows * rowHeight) / Math.Max(1, rows + 1);

        var primitives = new List<VectorPrimitive>();
        for (var row = 0; row < rows; row++)
        {
            var top = marginMm + gap + row * (rowHeight + gap);
            primitives.Add(new LineSeg(left, top, right, top, 0.35));
            primitives.Add(new LineSeg(left, top + xHeightMm, right, top + xHeightMm, 0.3, Dashed: true));
            primitives.Add(new LineSeg(left, top + rowHeight, right, top + rowHeight, 0.5));

            if (modelWords is { Count: > 0 })
            {
                primitives.Add(new TextLabel(left + 2, top + rowHeight - 1, modelWords[row % modelWords.Count], xHeightMm * 1.4, TextAnchor.Start));
            }
        }

        return new ArtifactDocument([new VectorGraphic(width, height, primitives,
            $"{rows} handwriting practice rows with dashed midlines" + (modelWords is { Count: > 0 } ? " and model words to copy" : ""))]);
    }
}

public sealed record LabelSpec(string Primary, string? Secondary = null);

/// <summary>
/// Label Lathe (spec §5.8): consistent label series on dimensionally described
/// sheets. Symbol-bearing labels wait for SVG compositing of catalog artwork;
/// text labels, optionally bilingual, are the MVP.
/// </summary>
public static class LabelLathe
{
    public const int Columns = 2;
    public const int Rows = 5;

    public static ArtifactDocument Sheets(IReadOnlyList<LabelSpec> labels, PageSize size = PageSize.Letter, double marginMm = BlankformsPress.DefaultMarginMm)
    {
        ArgumentNullException.ThrowIfNull(labels);
        if (labels.Count == 0 || labels.Any(l => string.IsNullOrWhiteSpace(l.Primary)))
        {
            throw new ArgumentException("Every label needs primary text.", nameof(labels));
        }

        var (width, height) = BlankformsPress.Dimensions(size);
        var labelWidth = (width - 2 * marginMm) / Columns;
        var labelHeight = (height - 2 * marginMm) / Rows;
        var perSheet = Columns * Rows;

        var nodes = new List<DocumentNode>();
        for (var sheet = 0; sheet * perSheet < labels.Count; sheet++)
        {
            var primitives = new List<VectorPrimitive>();
            var first = sheet * perSheet;
            var last = Math.Min(first + perSheet, labels.Count);

            for (var i = first; i < last; i++)
            {
                var slot = i - first;
                var x = marginMm + slot % Columns * labelWidth;
                var y = marginMm + slot / Columns * labelHeight;
                var centerX = x + labelWidth / 2;

                primitives.Add(new RectShape(x, y, labelWidth, labelHeight, 0.4));
                primitives.Add(new TextLabel(centerX, y + labelHeight / 2, labels[i].Primary, 8));
                if (!string.IsNullOrWhiteSpace(labels[i].Secondary))
                {
                    primitives.Add(new TextLabel(centerX, y + labelHeight / 2 + 10, labels[i].Secondary!, 5.5));
                }
            }

            nodes.Add(new VectorGraphic(width, height, primitives,
                $"Label sheet {sheet + 1}: {last - first} classroom labels, {Columns} columns by {Rows} rows"));
        }

        return new ArtifactDocument(nodes);
    }
}
