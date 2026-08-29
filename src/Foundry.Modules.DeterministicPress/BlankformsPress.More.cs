using Foundry.Domain;

namespace Foundry.Modules.DeterministicPress;

// The remaining Blankforms of spec §5.1: calendar, Cornell notes, lab table.

public static partial class BlankformsPress
{
    public static ArtifactDocument MonthCalendar(IReadOnlyList<string> weekdayLabels, int weeks = 5, PageSize size = PageSize.Letter, double marginMm = DefaultMarginMm)
    {
        ArgumentNullException.ThrowIfNull(weekdayLabels);
        if (weekdayLabels.Count != 7)
        {
            throw new ArgumentException("A week has seven day labels.", nameof(weekdayLabels));
        }

        if (weeks is < 4 or > 6)
        {
            throw new ArgumentException("Between four and six week rows.", nameof(weeks));
        }

        var (width, height) = Dimensions(size);
        var left = marginMm;
        var top = marginMm + 10;
        var cellWidth = (width - 2 * marginMm) / 7;
        var cellHeight = (height - top - marginMm) / weeks;

        var primitives = new List<VectorPrimitive>();
        for (var day = 0; day < 7; day++)
        {
            primitives.Add(new TextLabel(left + day * cellWidth + cellWidth / 2, marginMm + 6, weekdayLabels[day], 5));
            for (var week = 0; week < weeks; week++)
            {
                primitives.Add(new RectShape(left + day * cellWidth, top + week * cellHeight, cellWidth, cellHeight, 0.4));
            }
        }

        return new ArtifactDocument([new VectorGraphic(width, height, primitives,
            $"A blank month calendar: seven labeled weekday columns, {weeks} week rows")]);
    }

    public static ArtifactDocument CornellNotes(PageSize size = PageSize.Letter, double marginMm = DefaultMarginMm, double cueColumnMm = 55, double summaryMm = 50)
    {
        var (width, height) = Dimensions(size);
        var right = width - marginMm;
        var bottom = height - marginMm;
        var summaryY = bottom - summaryMm;

        var primitives = new List<VectorPrimitive>
        {
            new RectShape(marginMm, marginMm, right - marginMm, bottom - marginMm, 0.5),
            new LineSeg(marginMm + cueColumnMm, marginMm, marginMm + cueColumnMm, summaryY, 0.5),
            new LineSeg(marginMm, summaryY, right, summaryY, 0.5),
        };

        return new ArtifactDocument([new VectorGraphic(width, height, primitives,
            "A Cornell notes page: cue column at the left, notes at the right, summary strip at the foot")]);
    }

    public static ArtifactDocument LabTable(IReadOnlyList<string> columnHeaders, int dataRows = 8, PageSize size = PageSize.Letter, double marginMm = DefaultMarginMm)
    {
        ArgumentNullException.ThrowIfNull(columnHeaders);
        if (columnHeaders.Count is < 2 or > 8 || columnHeaders.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Between two and eight named columns.", nameof(columnHeaders));
        }

        if (dataRows is < 2 or > 20)
        {
            throw new ArgumentException("Between two and twenty data rows.", nameof(dataRows));
        }

        var (width, height) = Dimensions(size);
        var left = marginMm;
        var top = marginMm;
        var tableWidth = width - 2 * marginMm;
        var columnWidth = tableWidth / columnHeaders.Count;
        const double headerHeight = 14;
        var rowHeight = Math.Min(14, (height - 2 * marginMm - headerHeight) / dataRows);

        var primitives = new List<VectorPrimitive>();
        for (var column = 0; column < columnHeaders.Count; column++)
        {
            var x = left + column * columnWidth;
            primitives.Add(new RectShape(x, top, columnWidth, headerHeight, 0.6));
            primitives.Add(new TextLabel(x + columnWidth / 2, top + headerHeight / 2 + 2, columnHeaders[column], 4.5));

            for (var row = 0; row < dataRows; row++)
            {
                primitives.Add(new RectShape(x, top + headerHeight + row * rowHeight, columnWidth, rowHeight, 0.35));
            }
        }

        return new ArtifactDocument([new VectorGraphic(width, height, primitives,
            $"A blank lab data table: {columnHeaders.Count} named columns, {dataRows} rows")]);
    }
}
