// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Tools.AtlasCouncilRecords;

namespace Foundry.Tests.Unit;

/// <summary>
/// Keeps the Atlas register, the council session template, and the repository's
/// governance record from silently collapsing into one source of authority.
/// The test intentionally protects process boundaries, not a council outcome.
/// </summary>
public class AtlasCouncilGovernanceTests
{
    [Fact]
    public void Atlas_priority_route_remains_needs_first_and_separates_each_authority()
    {
        var root = RepositoryRoot();
        var template = File.ReadAllText(Path.Combine(root, "docs", "council", "atlas-priority-session.md"));
        var atlas = File.ReadAllText(Path.Combine(root, "docs", "idea-atlas.md"));
        var governance = File.ReadAllText(Path.Combine(root, "GOVERNANCE.md"));
        var councilCharter = File.ReadAllText(Path.Combine(root, "docs", "educator-council.md"));
        var namingDecision = File.ReadAllText(Path.Combine(
            root,
            "docs",
            "adr",
            "ADR-008-public-module-display-names.md"));

        Assert.Contains("READY TEMPLATE — **UNRUN**", template, StringComparison.Ordinal);
        Assert.Contains("not a council finding, a roadmap decision, an ADR, or permission to build", template, StringComparison.Ordinal);
        Assert.Contains("Silence or absence means **not reviewed**, never assent", template, StringComparison.Ordinal);
        Assert.Contains("The first cohort must enact a decision procedure and quorum rule", template, StringComparison.Ordinal);
        Assert.Contains("A session recommendation authorizes nothing", template, StringComparison.Ordinal);

        var needCapture = HeadingOffset(template, "### Need card — complete before opening the atlas");
        var mapping = HeadingOffset(template, "### Need-to-possibility mapping — complete only after need capture");
        var recommendation = HeadingOffset(template, "## Council recommendation record");
        var participantFreeze = HeadingOffset(template, "## Participant review and council-record freeze");
        var feasibility = HeadingOffset(template, "## Separate feasibility appendix — completed after the council record is frozen");
        var disposition = HeadingOffset(template, "## Product-owner disposition — intentionally blank in the template");

        Assert.True(
            needCapture < mapping
            && mapping < recommendation
            && recommendation < participantFreeze
            && participantFreeze < feasibility
            && feasibility < disposition,
            "The council-first route must remain need capture → mapping → council record → participant freeze → feasibility → product-owner disposition.");

        Assert.Contains("This atlas is a possibility register, not an engineering queue", atlas, StringComparison.Ordinal);
        Assert.Contains("real, needs-first educator-council session", atlas, StringComparison.Ordinal);
        Assert.Contains("separate feasibility record and written product-owner disposition", atlas, StringComparison.Ordinal);
        Assert.Contains("No next atlas priority has been selected", template, StringComparison.Ordinal);

        Assert.Contains("does not yet record the first cohort's cadence, voting or decision rule, quorum, or term limits", governance, StringComparison.Ordinal);
        Assert.Contains("must not be inferred by a facilitator or automation", governance, StringComparison.Ordinal);

        Assert.DoesNotContain("council selection", councilCharter, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No real council recommendation", councilCharter, StringComparison.Ordinal);
        Assert.Contains("participant-frozen record", councilCharter, StringComparison.Ordinal);
        Assert.Contains("product-owner disposition", councilCharter, StringComparison.Ordinal);

        Assert.DoesNotContain(
            "educator council still selects the next Atlas priority",
            namingDecision,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "educator council still recommends the next Atlas priority",
            namingDecision,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("participant review and record freeze", namingDecision, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "separate feasibility record and written product-owner disposition",
            namingDecision,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Every_future_dated_atlas_record_must_pass_the_mechanics_only_lifecycle_guard()
    {
        var councilDirectory = Path.Combine(RepositoryRoot(), "docs", "council");
        var datedRecords = Directory.EnumerateFiles(
                councilDirectory,
                "atlas-priority-session*.md",
                SearchOption.TopDirectoryOnly)
            .Where(path => !string.Equals(
                Path.GetFileName(path),
                "atlas-priority-session.md",
                StringComparison.Ordinal));

        foreach (var path in datedRecords)
        {
            var validation = AtlasCouncilRecordValidator.Validate(
                Path.GetFileName(path),
                File.ReadAllText(path));
            Assert.True(
                validation.IsValid,
                $"{Path.GetFileName(path)} failed the mechanics-only Atlas lifecycle guard:{Environment.NewLine}"
                + string.Join(
                    Environment.NewLine,
                    validation.Issues.Select(issue => $"{issue.Code}: {issue.Message}")));
        }
    }

    private static int HeadingOffset(string document, string heading)
    {
        var offset = document.IndexOf(heading, StringComparison.Ordinal);
        Assert.True(offset >= 0, $"The Atlas priority template is missing required heading '{heading}'.");
        return offset;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpenClassroomFoundry.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not find the repository root by walking up to OpenClassroomFoundry.slnx.");
    }
}
