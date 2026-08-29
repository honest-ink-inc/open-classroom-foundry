// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Domain;

namespace Foundry.Modules.DeterministicPress;

// The history presses (atlas #103, #79; third forge menu, item 5). The
// timeline is proportionally TRUE: a century is the same millimeters
// everywhere on the line, asserted as arithmetic.

/// <summary>One dated entry: a point year or a span, with the teacher's label verbatim.</summary>
public sealed record TimelineEvent(int Year, int? EndYear, string Label);

public static class TimelineWeaver
{
    /// <summary>Parses teacher lines of "year | label" or "start-end | label" — years are the teacher's claim, placed, never checked against history.</summary>
    public static IReadOnlyList<TimelineEvent> Parse(IReadOnlyList<(string Left, string? Right)> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var events = new List<TimelineEvent>();
        foreach (var (left, right) in lines)
        {
            if (string.IsNullOrWhiteSpace(right))
            {
                throw new ArgumentException($"'{left}' has no label; write year | label.", nameof(lines));
            }

            var parts = left.Split('-', 2);
            if (!int.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var year))
            {
                throw new ArgumentException($"'{parts[0].Trim()}' is not a year.", nameof(lines));
            }

            int? end = null;
            if (parts.Length == 2)
            {
                if (!int.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var endYear) || endYear <= year)
                {
                    throw new ArgumentException($"'{left}' is not a valid span; write start-end with end after start.", nameof(lines));
                }

                end = endYear;
            }

            events.Add(new TimelineEvent(year, end, right));
        }

        return events;
    }

    public static ArtifactDocument Sheet(
        IReadOnlyList<TimelineEvent> events,
        int fromYear,
        int toYear,
        PageSize size = PageSize.LetterLandscape,
        double marginMm = BlankformsPress.DefaultMarginMm)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (events.Count is < 2 or > 16)
        {
            throw new ArgumentException("Between two and sixteen events.", nameof(events));
        }

        if (toYear <= fromYear)
        {
            throw new ArgumentException("The range must run forward; 'to' after 'from'.", nameof(toYear));
        }

        if (events.Any(e => e.Year < fromYear || (e.EndYear ?? e.Year) > toYear))
        {
            throw new ArgumentException("Every event must lie inside the declared range.", nameof(events));
        }

        var (width, height) = BlankformsPress.Dimensions(size);
        var axisY = height / 2;
        var left = marginMm + 10;
        var right = width - marginMm - 10;
        var span = right - left;
        double X(int year)
        {
            return left + span * (year - fromYear) / (toYear - fromYear);
        }

        var primitives = new List<VectorPrimitive>
        {
            new LineSeg(left, axisY, right, axisY, 0.7),
            new TextLabel(left, axisY + 8, fromYear.ToString(System.Globalization.CultureInfo.InvariantCulture), 4),
            new TextLabel(right, axisY + 8, toYear.ToString(System.Globalization.CultureInfo.InvariantCulture), 4),
        };

        for (var i = 0; i < events.Count; i++)
        {
            var above = i % 2 == 0; // alternate so neighboring labels never collide
            var entry = events[i];
            var x = X(entry.Year);
            var labelY = above ? axisY - 14 - i % 4 * 7 : axisY + 18 + i % 4 * 7;

            if (entry.EndYear is int end)
            {
                primitives.Add(new LineSeg(x, axisY, X(end), axisY, 1.6)); // the span rides the axis, heavier
                primitives.Add(new TextLabel((x + X(end)) / 2, labelY, $"{entry.Label} ({entry.Year}-{end})", 4));
            }
            else
            {
                primitives.Add(new LineSeg(x, axisY - 3, x, axisY + 3, 0.5));
                primitives.Add(new TextLabel(x, labelY, $"{entry.Label} ({entry.Year})", 4));
            }
        }

        return new ArtifactDocument([new VectorGraphic(width, height, primitives,
            $"A proportionally true timeline from {fromYear} to {toYear} carrying {events.Count} teacher-dated events")]);
    }
}

/// <summary>
/// Source Synthesis Table (atlas #79): teacher-named sources across, teacher-
/// typed claims down, empty cells for the learner's marks, a provenance row
/// at the foot — the synthesis is the learner's work, never the press's.
/// </summary>
public static class SourceSynthesisTable
{
    public static ArtifactDocument Sheet(
        IReadOnlyList<string> claims,
        IReadOnlyList<string> sources,
        string legend,
        string provenanceLabel,
        PageSize size = PageSize.Letter,
        double marginMm = BlankformsPress.DefaultMarginMm)
    {
        ArgumentNullException.ThrowIfNull(claims);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentException.ThrowIfNullOrWhiteSpace(legend);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenanceLabel);

        if (claims.Count is < 2 or > 8 || claims.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Between two and eight non-blank claims.", nameof(claims));
        }

        if (sources.Count is < 2 or > 5 || sources.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Between two and five non-blank sources.", nameof(sources));
        }

        var (width, height) = BlankformsPress.Dimensions(size);
        var labelWidth = (width - 2 * marginMm) * 0.4;
        var cellWidth = (width - 2 * marginMm - labelWidth) / sources.Count;
        const double headerHeight = 12;
        var rowHeight = Math.Min(22, (height - 2 * marginMm - headerHeight - 26) / (claims.Count + 1));

        var primitives = new List<VectorPrimitive>
        {
            new TextLabel(marginMm, marginMm + 4, legend, 4, TextAnchor.Start),
        };

        var top = marginMm + 8;
        for (var s = 0; s < sources.Count; s++)
        {
            var x = marginMm + labelWidth + s * cellWidth;
            primitives.Add(new RectShape(x, top, cellWidth, headerHeight, 0.6));
            primitives.Add(new TextLabel(x + cellWidth / 2, top + headerHeight / 2 + 1.5, sources[s], 4));
        }

        // Claim rows plus the provenance row at the foot.
        var rows = claims.Append(provenanceLabel).ToList();
        for (var r = 0; r < rows.Count; r++)
        {
            var y = top + headerHeight + r * rowHeight;
            primitives.Add(new RectShape(marginMm, y, labelWidth, rowHeight, 0.4));
            primitives.Add(new TextLabel(marginMm + 3, y + 6, rows[r], 4, TextAnchor.Start));
            for (var s = 0; s < sources.Count; s++)
            {
                primitives.Add(new RectShape(marginMm + labelWidth + s * cellWidth, y, cellWidth, rowHeight, 0.4));
            }
        }

        return new ArtifactDocument([new VectorGraphic(width, height, primitives,
            $"A claim-by-source synthesis table: {claims.Count} claims across {sources.Count} sources, provenance row at the foot")]);
    }
}
