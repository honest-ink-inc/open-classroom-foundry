// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Tools.AtlasCouncilRecords;
using System.Text.RegularExpressions;

namespace Foundry.Tests.Unit;

/// <summary>
/// Keeps the Atlas register, the council session template, and the repository's
/// governance record from silently collapsing into one source of authority.
/// The test intentionally protects process boundaries, not a council outcome.
/// </summary>
public partial class AtlasCouncilGovernanceTests
{
    [Fact]
    public void Atlas_priority_route_remains_needs_first_and_separates_each_authority()
    {
        var root = RepositoryRoot();
        var template = File.ReadAllText(Path.Combine(root, "docs", "council", "atlas-priority-session.md"));
        var freezeManifest = File.ReadAllText(Path.Combine(
            root,
            "docs",
            "council",
            "atlas-priority-session-freeze-manifest.md"));
        var feasibilityRecord = File.ReadAllText(Path.Combine(
            root,
            "docs",
            "council",
            "atlas-priority-session-feasibility-record.md"));
        var dispositionRecord = File.ReadAllText(Path.Combine(
            root,
            "docs",
            "council",
            "atlas-priority-session-product-owner-disposition.md"));
        var atlas = File.ReadAllText(Path.Combine(root, "docs", "idea-atlas.md"));
        var governance = File.ReadAllText(Path.Combine(root, "GOVERNANCE.md"));
        var councilCharter = File.ReadAllText(Path.Combine(root, "docs", "educator-council.md"));
        var namingDecision = File.ReadAllText(Path.Combine(
            root,
            "docs",
            "adr",
            "ADR-008-public-module-display-names.md"));

        Assert.Contains("DRAFT TEMPLATE — **UNRUN AND NOT READY TO CONVENE**", template, StringComparison.Ordinal);
        Assert.Contains("not a council finding, a roadmap decision, an ADR, or permission to build", template, StringComparison.Ordinal);
        Assert.Contains("Silence or absence means **not reviewed**, never assent", template, StringComparison.Ordinal);
        Assert.Contains(
            "requires the first cohort's enacted decision procedure and quorum rule",
            template,
            StringComparison.Ordinal);
        Assert.Contains("A session recommendation authorizes nothing", template, StringComparison.Ordinal);
        Assert.Contains(
            "The amounts described on 29 August remain held pending a corrective proposal and valid first-cohort enactment",
            template,
            StringComparison.Ordinal);

        var needCapture = HeadingOffset(template, "### Need card — complete before opening the atlas");
        var mapping = HeadingOffset(template, "### Need-to-possibility mapping — complete only after need capture");
        var recommendation = HeadingOffset(template, "## Council recommendation record");
        var closure = HeadingOffset(template, "## Close the session record; freeze only through a detached manifest");
        var completion = HeadingOffset(template, "## Completion check");

        Assert.True(
            needCapture < mapping
            && mapping < recommendation
            && recommendation < closure
            && closure < completion,
            "The H0 bytes must remain need capture → mapping → recommendation → immutable closure.");
        Assert.DoesNotContain("## Separate feasibility appendix", template, StringComparison.Ordinal);
        Assert.DoesNotContain("## Product-owner disposition — intentionally", template, StringComparison.Ordinal);
        Assert.Contains("field for its own SHA-256", freezeManifest, StringComparison.Ordinal);
        Assert.Contains("H0 freeze-manifest SHA-256", feasibilityRecord, StringComparison.Ordinal);
        Assert.Contains("Feasibility record SHA-256", dispositionRecord, StringComparison.Ordinal);

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
        var canonicalTemplate = File.ReadAllText(Path.Combine(
            councilDirectory,
            "atlas-priority-session.md"));
        var templateNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "atlas-priority-session.md",
            "atlas-priority-session-freeze-manifest.md",
            "atlas-priority-session-feasibility-record.md",
            "atlas-priority-session-product-owner-disposition.md",
        };
        var candidatePaths = Directory.EnumerateFiles(
                councilDirectory,
                "atlas-priority-session*.md",
                SearchOption.TopDirectoryOnly)
            .Where(path => !templateNames.Contains(Path.GetFileName(path)))
            .ToArray();

        Assert.All(
            candidatePaths,
            path => Assert.True(
                DatedH0RecordName().IsMatch(Path.GetFileName(path))
                || DatedH0ManifestName().IsMatch(Path.GetFileName(path))
                || DatedH0FeasibilityName().IsMatch(Path.GetFileName(path))
                || DatedH0DispositionName().IsMatch(Path.GetFileName(path)),
                $"Unrecognized Atlas H0 near-match must not bypass the lifecycle guard: {Path.GetFileName(path)}"));

        foreach (var path in candidatePaths.Where(path => DatedH0RecordName().IsMatch(Path.GetFileName(path))))
        {
            var validation = AtlasCouncilRecordValidator.ValidateAgainstCanonicalTemplate(
                Path.GetFileName(path),
                File.ReadAllText(path),
                canonicalTemplate);
            Assert.True(
                validation.IsValid,
                $"{Path.GetFileName(path)} failed the mechanics-only Atlas lifecycle guard:{Environment.NewLine}"
                + string.Join(
                    Environment.NewLine,
                    validation.Issues.Select(issue => $"{issue.Code}: {issue.Message}")));
        }

        foreach (var path in candidatePaths.Where(path => DatedH0ManifestName().IsMatch(Path.GetFileName(path))))
        {
            var recordPath = path.Replace("-freeze-manifest.md", ".md", StringComparison.Ordinal);
            Assert.True(File.Exists(recordPath), $"{Path.GetFileName(path)} is orphaned: its H0 record is missing.");
            AssertValid(
                Path.GetFileName(path),
                AtlasCouncilRecordValidator.ValidateFreezeManifest(
                    Path.GetFileName(path),
                    File.ReadAllBytes(path),
                    Path.GetFileName(recordPath),
                    File.ReadAllBytes(recordPath)));
        }

        foreach (var path in candidatePaths.Where(path => DatedH0FeasibilityName().IsMatch(Path.GetFileName(path))))
        {
            var versionMarker = path.LastIndexOf("-feasibility-v", StringComparison.Ordinal);
            var recordPath = path[..versionMarker] + ".md";
            var manifestPath = path[..versionMarker] + "-freeze-manifest.md";
            Assert.True(File.Exists(recordPath), $"{Path.GetFileName(path)} is orphaned: its H0 record is missing.");
            Assert.True(File.Exists(manifestPath), $"{Path.GetFileName(path)} is orphaned: its freeze manifest is missing.");
            AssertValid(
                Path.GetFileName(path),
                AtlasCouncilRecordValidator.ValidateFeasibilityRecord(
                    Path.GetFileName(path),
                    File.ReadAllBytes(path),
                    Path.GetFileName(recordPath),
                    File.ReadAllBytes(recordPath),
                    Path.GetFileName(manifestPath),
                    File.ReadAllBytes(manifestPath)));
        }

        foreach (var path in candidatePaths.Where(path => DatedH0DispositionName().IsMatch(Path.GetFileName(path))))
        {
            var versionMarker = path.LastIndexOf("-disposition-v", StringComparison.Ordinal);
            var recordPath = path[..versionMarker] + ".md";
            var manifestPath = path[..versionMarker] + "-freeze-manifest.md";
            var feasibilityFileName = ReadBoundRepositoryFileName(path, "Feasibility record repository path");
            var feasibilityPath = Path.Combine(Path.GetDirectoryName(path)!, feasibilityFileName);
            Assert.True(File.Exists(recordPath), $"{Path.GetFileName(path)} is orphaned: its H0 record is missing.");
            Assert.True(File.Exists(manifestPath), $"{Path.GetFileName(path)} is orphaned: its freeze manifest is missing.");
            Assert.True(File.Exists(feasibilityPath), $"{Path.GetFileName(path)} is orphaned: its feasibility record is missing.");
            AssertValid(
                Path.GetFileName(path),
                AtlasCouncilRecordValidator.ValidateDispositionRecord(
                    Path.GetFileName(path),
                    File.ReadAllBytes(path),
                    Path.GetFileName(recordPath),
                    File.ReadAllBytes(recordPath),
                    Path.GetFileName(manifestPath),
                    File.ReadAllBytes(manifestPath),
                    Path.GetFileName(feasibilityPath),
                    File.ReadAllBytes(feasibilityPath)));
        }
    }

    private static void AssertValid(
        string fileName,
        AtlasCouncilArtifactValidation validation)
        => Assert.True(
            validation.IsValid,
            $"{fileName} failed the mechanics-only Atlas lifecycle guard:{Environment.NewLine}"
            + string.Join(
                Environment.NewLine,
                validation.Issues.Select(issue => $"{issue.Code}: {issue.Message}")));

    private static int HeadingOffset(string document, string heading)
    {
        var offset = document.IndexOf(heading, StringComparison.Ordinal);
        Assert.True(offset >= 0, $"The Atlas priority template is missing required heading '{heading}'.");
        return offset;
    }

    private static string ReadBoundRepositoryFileName(string artifactPath, string field)
    {
        var prefix = $"| {field} |";
        var row = File.ReadLines(artifactPath)
            .Single(line => line.StartsWith(prefix, StringComparison.Ordinal));
        var cells = row.Split('|', StringSplitOptions.TrimEntries);
        Assert.True(cells.Length >= 3, $"The '{field}' row is malformed in {Path.GetFileName(artifactPath)}.");
        return Path.GetFileName(cells[2]);
    }

    [GeneratedRegex(
        "^atlas-priority-session-[0-9]{4}-[0-9]{2}-[0-9]{2}\\.md$",
        RegexOptions.CultureInvariant)]
    private static partial Regex DatedH0RecordName();

    [GeneratedRegex(
        "^atlas-priority-session-[0-9]{4}-[0-9]{2}-[0-9]{2}-freeze-manifest\\.md$",
        RegexOptions.CultureInvariant)]
    private static partial Regex DatedH0ManifestName();

    [GeneratedRegex(
        "^atlas-priority-session-[0-9]{4}-[0-9]{2}-[0-9]{2}-feasibility-v[1-9][0-9]*\\.md$",
        RegexOptions.CultureInvariant)]
    private static partial Regex DatedH0FeasibilityName();

    [GeneratedRegex(
        "^atlas-priority-session-[0-9]{4}-[0-9]{2}-[0-9]{2}-disposition-v[1-9][0-9]*\\.md$",
        RegexOptions.CultureInvariant)]
    private static partial Regex DatedH0DispositionName();

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
