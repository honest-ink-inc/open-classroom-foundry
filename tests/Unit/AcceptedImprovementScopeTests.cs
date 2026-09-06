// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.RegularExpressions;

namespace Foundry.Tests.Unit;

/// <summary>
/// Structural coverage and navigation only. These checks neither assess the
/// proposals' implementation nor authenticate a council, decision or review.
/// </summary>
public partial class AcceptedImprovementScopeTests
{
    private static readonly string[] WorkStates = ["OPEN", "ACTIVE", "HELD", "VERIFIED"];

    [Fact]
    public void Work_register_retains_every_proposal_and_the_full_acceptance_envelope()
    {
        var proposals = Read("docs/reviews/2026-09-05-synthetic-council/teacher-practice-and-improvements.md");
        var register = Read("docs/governance/accepted-improvement-register.md");
        var expected = ProposalHeading().Matches(proposals).Select(match => match.Groups[1].Value).ToArray();
        var rows = RegisterRow().Matches(register).ToArray();

        Assert.Equal(Enumerable.Range(1, 40).Select(id => $"I{id:00}"), expected);
        Assert.Equal(expected, rows.Select(match => match.Groups[1].Value));
        Assert.All(rows, row =>
        {
            Assert.False(string.IsNullOrWhiteSpace(row.Groups[2].Value));
            Assert.False(string.IsNullOrWhiteSpace(row.Groups[3].Value));
            Assert.Contains(row.Groups[4].Value, WorkStates);
        });

        var atlas = Read("docs/reviews/2026-09-05-synthetic-council/atlas-dispositions.md");
        var candidateIds = AtlasRow().Matches(atlas).Select(match => int.Parse(
            match.Groups[1].Value,
            System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(Enumerable.Range(1, 227), candidateIds);
        foreach (Match reference in ProposalReference().Matches(atlas))
        {
            Assert.Contains(reference.Value, expected);
        }

        Assert.Contains("each proposal's original **Proof**", register, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("44 Gate 0–5 criteria", register, StringComparison.Ordinal);
        Assert.Contains("34 Definition of", register, StringComparison.Ordinal);
        Assert.Contains("13 stop-ship conditions", register, StringComparison.Ordinal);
        Assert.Contains("not evidence of real council attendance or findings", register, StringComparison.Ordinal);
        Assert.Contains("EIN, 1023-EZ,", register, StringComparison.Ordinal);
        Assert.Contains("other missing", register, StringComparison.Ordinal);
    }

    [Fact]
    public void Decision_navigation_resolves_files_and_keeps_authority_failures_explicit()
    {
        var root = RepositoryRoot();
        foreach (var relative in new[]
        {
            "docs/governance/decision-index.md",
            "docs/governance/accepted-improvement-register.md",
            "docs/governance/accepted-improvement-evidence.md",
            "docs/governance/content-ownership-use-inventory.md",
            "docs/development/source-build-and-verification.md",
            "docs/governance/sustainability-dependency-inventory.md",
        })
        {
            var path = Path.Combine(root, relative);
            foreach (Match link in MarkdownLink().Matches(File.ReadAllText(path)))
            {
                var target = link.Groups[1].Value;
                if (target.StartsWith("https://", StringComparison.Ordinal)
                    || target.StartsWith("http://", StringComparison.Ordinal)
                    || target.StartsWith('#'))
                {
                    continue;
                }

                var filePart = Uri.UnescapeDataString(target.Split('#')[0]);
                var absolute = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, filePart));
                Assert.StartsWith(root + Path.DirectorySeparatorChar, absolute, StringComparison.OrdinalIgnoreCase);
                Assert.True(File.Exists(absolute), $"Broken decision/work navigation in {relative}: {target}");
            }
        }

        var index = Read("docs/governance/decision-index.md");
        foreach (var example in new[] { "Conflicting claim", "Superseded record", "Withdrawal", "Vacant or expired seat" })
        {
            Assert.Contains($"**{example}:**", index, StringComparison.Ordinal);
        }

        Assert.Contains("fresh private-custodian attestation", index, StringComparison.Ordinal);
        Assert.Contains("**HOLD**, not a best guess", index, StringComparison.Ordinal);
        Assert.Contains("does not replace any controlling record", index, StringComparison.Ordinal);
    }

    [Fact]
    public void Derivative_lane_erratum_preserves_history_without_an_operative_Green_downgrade()
    {
        const string obsoleteClaim = "the summary is already teacher-approved Green output";
        var plan = Read("docs/implementation-plan.md");
        var review = Read("docs/reviews/masters-review-1.0.md");
        var currentPlan = StruckHistory().Replace(plan, "");
        var currentReview = StruckHistory().Replace(review, "");

        Assert.Contains("already teacher-approved Green output", plan, StringComparison.Ordinal);
        Assert.Contains(obsoleteClaim, review, StringComparison.Ordinal);
        Assert.DoesNotContain("already teacher-approved Green output", currentPlan, StringComparison.Ordinal);
        Assert.DoesNotContain(obsoleteClaim, currentReview, StringComparison.Ordinal);
        Assert.Contains("Corrected 5 September 2026 (I35)", currentPlan, StringComparison.Ordinal);
        Assert.Contains("approval does not change a data lane", currentPlan, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("A derivative inherits the highest lane of its inputs", currentPlan, StringComparison.Ordinal);
        Assert.Contains("Copying or paraphrasing a response-derived summary is not this independent authoring route", currentPlan, StringComparison.Ordinal);
        Assert.Contains("current production sinks admit Green only", currentPlan, StringComparison.Ordinal);
        Assert.Contains("Neither this correction", currentReview, StringComparison.Ordinal);
        Assert.Contains("or a legal finding", currentReview, StringComparison.Ordinal);

        var atlas = Read("docs/idea-atlas.md");
        Assert.Contains("60. **Misconception Atlas** `[A]`", atlas, StringComparison.Ordinal);
        Assert.Contains("151. **Teacher Logbook** `[G/A]`", atlas, StringComparison.Ordinal);
        Assert.Contains("no response-derived text, image, quotation, or per-check trace", atlas, StringComparison.Ordinal);
        Assert.Contains("Accumulation invariant as in #60", atlas, StringComparison.Ordinal);
    }

    private static string Read(string relativePath)
        => File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath)).Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpenClassroomFoundry.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root unavailable.");
    }

    [GeneratedRegex(@"^### (I\d{2}) — ", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex ProposalHeading();

    [GeneratedRegex(@"^\| (I\d{2}) \| ([^|]+) \| ([^|]+) \| ([^|]+) \|$", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex RegisterRow();

    [GeneratedRegex(@"^\| (\d+) \| ", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex AtlasRow();

    [GeneratedRegex(@"\bI\d{2}\b", RegexOptions.CultureInvariant)]
    private static partial Regex ProposalReference();

    [GeneratedRegex(@"\[[^\]]+\]\(([^)]+)\)", RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownLink();

    [GeneratedRegex(@"~~[^\r\n]*?~~", RegexOptions.CultureInvariant)]
    private static partial Regex StruckHistory();
}
