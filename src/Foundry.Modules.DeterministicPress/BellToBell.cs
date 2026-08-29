// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Domain;

namespace Foundry.Modules.DeterministicPress;

// Bell-to-Bell (atlas #44; fourth forge menu, item 3): the schedule that does
// its own arithmetic. Clock times are computed cumulatively — activity by
// activity, transitions counted — the closure row is PROTECTED at the end of
// the period, and a plan that overruns is refused in ink before it ever
// reaches a classroom. The activities are the teacher's; the press does only
// the clock arithmetic.

/// <summary>One planned row: the teacher's minutes and label, verbatim.</summary>
public sealed record PlannedActivity(int Minutes, string Label);

public static class BellToBell
{
    /// <summary>Parses a start time like "8:30" — hours 0-23, minutes 0-59, refused loudly otherwise.</summary>
    public static int ParseStartMinutes(string start)
    {
        ArgumentNullException.ThrowIfNull(start);

        var parts = start.Split(':', 2);
        if (parts.Length != 2
            || !int.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var hour)
            || !int.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var minute)
            || hour is < 0 or > 23 || minute is < 0 or > 59)
        {
            throw new ArgumentException($"'{start}' is not a start time; write it like 8:30.", nameof(start));
        }

        return hour * 60 + minute;
    }

    /// <summary>Parses teacher lines of "minutes | activity" — the plan is the teacher's, placed, never reordered.</summary>
    public static IReadOnlyList<PlannedActivity> Parse(IReadOnlyList<(string Left, string? Right)> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var activities = new List<PlannedActivity>();
        foreach (var (left, right) in lines)
        {
            if (string.IsNullOrWhiteSpace(right))
            {
                throw new ArgumentException($"'{left}' has no activity; write minutes | activity.", nameof(lines));
            }

            if (!int.TryParse(left, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var minutes))
            {
                throw new ArgumentException($"'{left}' is not a number of minutes for '{right}'.", nameof(lines));
            }

            if (minutes < 1)
            {
                throw new ArgumentException($"'{right}' needs at least one minute.", nameof(lines));
            }

            activities.Add(new PlannedActivity(minutes, right));
        }

        return activities;
    }

    public static ArtifactDocument Plan(
        string title,
        string start,
        IReadOnlyList<PlannedActivity> activities,
        int periodMinutes,
        int transitionMinutes,
        string closureLabel,
        int closureMinutes)
    {
        ArgumentNullException.ThrowIfNull(activities);

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("The plan needs a title.", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(closureLabel))
        {
            throw new ArgumentException("The protected closure needs a label.", nameof(closureLabel));
        }

        if (activities.Count is < 1 or > 12)
        {
            throw new ArgumentException("Between one and twelve activities.", nameof(activities));
        }

        if (periodMinutes < 1)
        {
            throw new ArgumentException("The period must hold at least one minute.", nameof(periodMinutes));
        }

        if (transitionMinutes < 0)
        {
            throw new ArgumentException("Transitions cannot take negative time.", nameof(transitionMinutes));
        }

        if (closureMinutes < 1)
        {
            throw new ArgumentException("The protected closure needs at least one minute.", nameof(closureMinutes));
        }

        var startMinutes = ParseStartMinutes(start);

        // THE invariant: cumulative clock arithmetic, refused loudly on overrun.
        var planned = activities.Sum(a => a.Minutes) + activities.Count * transitionMinutes + closureMinutes;
        if (planned > periodMinutes)
        {
            throw new ArgumentException(
                $"The plan needs {planned} minutes but the period holds {periodMinutes}; trim {planned - periodMinutes} minute(s).",
                nameof(activities));
        }

        var transition = transitionMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var rows = new List<IReadOnlyList<string>>();
        var clock = startMinutes;
        foreach (var activity in activities)
        {
            rows.Add([Clock(clock), activity.Minutes.ToString(System.Globalization.CultureInfo.InvariantCulture), activity.Label, transition]);
            clock += activity.Minutes + transitionMinutes;
        }

        // The closure holds its ground at the end of the period, whatever the
        // rows before it leave open.
        var closureStart = startMinutes + periodMinutes - closureMinutes;
        rows.Add([Clock(closureStart), closureMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture), closureLabel, ""]);

        var open = closureStart - clock;
        var summary = open == 0
            ? $"{planned} of {periodMinutes} minutes planned; the bell at {Clock(startMinutes + periodMinutes)} is met exactly."
            : $"{planned} of {periodMinutes} minutes planned; {open} minute(s) open before the closure at {Clock(closureStart)}.";

        return new ArtifactDocument(
        [
            new Heading(1, title),
            new Paragraph($"{Clock(startMinutes)} to {Clock(startMinutes + periodMinutes)} — {periodMinutes} minutes bell to bell."),
            new TableNode(["Clock", "Minutes", "Activity", "Transition"], rows),
            new Paragraph(summary),
        ]);
    }

    private static string Clock(int minutesSinceMidnight)
    {
        var wrapped = minutesSinceMidnight % (24 * 60);
        return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{wrapped / 60}:{wrapped % 60:00}");
    }
}
