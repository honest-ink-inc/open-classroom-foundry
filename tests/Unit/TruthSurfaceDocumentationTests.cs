// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;

namespace Foundry.Tests.Unit;

public class TruthSurfaceDocumentationTests
{
    [Fact]
    public void Public_status_uses_the_recorded_evidence_baseline_and_ruleset_state()
    {
        var readme = Read("README.md");

        Assert.Contains("Evidence is commit-scoped", readme, StringComparison.Ordinal);
        Assert.Contains("engineering-mode-complete as a prototype", readme, StringComparison.Ordinal);
        Assert.Contains("2,013/2,013", readme, StringComparison.Ordinal);
        Assert.Contains("active `main` ruleset", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("1,985/1,985", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("currently has no protected-branch enforcement", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void Implementation_plan_points_to_the_indexed_current_handover()
    {
        var index = Read("docs", "README.md");
        var currentLine = Assert.Single(
            index.Split('\n'),
            line => line.Contains("**Current repository state:**", StringComparison.Ordinal));
        var destinationStart = currentLine.IndexOf("](", StringComparison.Ordinal) + 2;
        var destinationEnd = currentLine.IndexOf(')', destinationStart);

        Assert.True(destinationStart > 1 && destinationEnd > destinationStart, "The current-state row has no readable Markdown destination.");
        var currentHandover = currentLine[destinationStart..destinationEnd];
        var planStatus = Assert.Single(
            Read("docs", "implementation-plan.md").Split('\n'),
            line => line.StartsWith("**Status (", StringComparison.Ordinal));

        Assert.Contains($"({currentHandover})", planStatus, StringComparison.Ordinal);
    }

    [Fact]
    public void Walkthrough_and_traceability_cover_preflight_before_fresh_gate_b()
    {
        var script = Read("docs", "accessibility", "nvda-walkthrough-script.md");
        var traceability = Read("docs", "accessibility", "uia-harness-traceability.md");
        var scriptStep = Assert.Single(
            script.Split('\n'),
            line => line.StartsWith("| 13 |", StringComparison.Ordinal));
        var traceabilityStep = Assert.Single(
            traceability.Split('\n'),
            line => line.StartsWith("| 13 |", StringComparison.Ordinal));

        Assert.Contains("fresh Gate B", scriptStep, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fresh Gate B", traceabilityStep, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-lane preflight", scriptStep, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-lane preflight", traceabilityStep, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("leave one box unchecked", scriptStep, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("incomplete Green classification refused", traceabilityStep, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("locked/approved state is audible", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Print_instrument_does_not_turn_mechanical_inspection_into_protected_review()
    {
        var checklist = Read("docs", "pilots", "print-inspection-checklist.md");

        Assert.Contains("Mechanical reviewer", checklist, StringComparison.Ordinal);
        Assert.Contains("Instrument operator", checklist, StringComparison.Ordinal);
        Assert.Contains("Protected judgments — separate records, separate owners", checklist, StringComparison.Ordinal);
        Assert.Contains("not reviewed", checklist, StringComparison.Ordinal);
        Assert.Contains("AAC user / SLP / special educator with AAC practice", checklist, StringComparison.Ordinal);
        Assert.Contains("Multilingual educator or family liaison", checklist, StringComparison.Ordinal);
        Assert.Contains("flip on short edge", checklist, StringComparison.Ordinal);
        Assert.DoesNotContain("flip on long edge", checklist, StringComparison.Ordinal);
        Assert.DoesNotContain("reads each symbol unambiguously", checklist, StringComparison.Ordinal);
    }

    [Fact]
    public void Historical_bundle_names_only_tracked_html_as_repository_samples()
    {
        var bundle = Read("docs", "evidence", "0.1-alpha", "README.md");
        var ignore = Read(".gitignore");

        Assert.Contains("HISTORICAL / MIXED REVISION", bundle, StringComparison.Ordinal);
        Assert.Contains("13-card", bundle, StringComparison.Ordinal);
        Assert.Contains("not qualified translations", bundle, StringComparison.Ordinal);
        Assert.Contains("No `.ocfproj` package is tracked in this directory", bundle, StringComparison.Ordinal);
        Assert.Contains("*.ocfproj", ignore, StringComparison.Ordinal);
        Assert.DoesNotContain("`task-strip-bilingual.ocfproj` — a real package", bundle, StringComparison.Ordinal);
        Assert.False(IsTracked("docs/evidence/0.1-alpha/samples/task-strip-bilingual.ocfproj"));
    }

    private static string Read(params string[] relativePath)
        => File.ReadAllText(Path.Combine([RepositoryRoot(), .. relativePath]));

    private static bool IsTracked(string relativePath)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = RepositoryRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in new[] { "ls-files", "--stage", "--", relativePath })
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Could not start git to verify the evidence-bundle inventory.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(process.ExitCode == 0, $"'git ls-files --stage' failed: {error}");
        return !string.IsNullOrWhiteSpace(output);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpenClassroomFoundry.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root for truth-surface documentation tests.");
    }
}
