// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Foundry.Tests.Unit;

/// <summary>
/// The evidence ledger records hosted conclusions and regular merges as data so
/// that handovers cite them instead of restating them. These tests keep every
/// entry well formed, content free, in measured order, and keep ledger-bound
/// records from citing a hosted run the ledger does not carry.
/// </summary>
public sealed partial class EvidenceLedgerTests
{
    private const string ExpectedFormat = "honest-ink-evidence-ledger.v1";
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string LedgerPath = Path.Combine(RepositoryRoot, "docs", "evidence", "evidence-ledger.json");
    private static readonly string MeasurementToolPath = Path.Combine(RepositoryRoot, "tools", "measure-evidence-ledger.ps1");
    private static readonly string[] LedgerBoundRecords =
    [
        "docs/handover/2026-09-01-fifth-forge-menu.md",
        "docs/evidence/sightings-register.md",
    ];
    private static readonly string[] HostedKinds = ["hosted-ci", "hosted-codeql"];
    private static readonly string[] Conclusions = ["success", "failure", "cancelled"];
    private static readonly string[] Events = ["push", "pull_request"];
    private static readonly Dictionary<string, string> WorkflowByKind = new(StringComparer.Ordinal)
    {
        ["hosted-ci"] = "CI",
        ["hosted-codeql"] = "CodeQL SAST",
    };

    [GeneratedRegex("^[0-9a-f]{40}$")]
    private static partial Regex CommitSha();

    [GeneratedRegex("^[0-9A-F]{64}$")]
    private static partial Regex Sha256();

    [GeneratedRegex(@"^\d{8}T\d{6}Z-[0-9a-f]{32}$")]
    private static partial Regex ReceiptId();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$")]
    private static partial Regex UtcStamp();

    [GeneratedRegex(@"(?:\bruns?\s+`(?<run>\d{11})`)|(?:actions/runs/(?<run>\d{11}))")]
    private static partial Regex RunCitation();

    [GeneratedRegex(@"[A-Za-z]:\\|[A-Za-z]:/|/Users/|/home/")]
    private static partial Regex AbsolutePath();

    [Fact]
    public void Ledger_declares_its_format_and_every_entry_is_well_formed_and_content_free()
    {
        using var ledger = Load();
        var root = ledger.RootElement;
        Assert.Equal(ExpectedFormat, root.GetProperty("format").GetString());
        var statement = RequiredString(root, "statement", "ledger");
        Assert.Contains("never inferred", statement, StringComparison.Ordinal);
        Assert.Contains("not a release, a diagnosis, a cure, or an approval", statement, StringComparison.Ordinal);

        var entries = root.GetProperty("entries").EnumerateArray().ToList();
        Assert.NotEmpty(entries);
        foreach (var entry in entries)
        {
            var id = RequiredString(entry, "id", "entry");
            var kind = RequiredString(entry, "kind", id);
            var record = RequiredString(entry, "record", id);
            var recordPath = Path.Combine(RepositoryRoot, record.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(recordPath), $"Ledger entry '{id}' cites a record that does not exist: {record}");
            var notes = RequiredString(entry, "notes", id);
            Assert.False(AbsolutePath().IsMatch(notes), $"Ledger entry '{id}' carries an absolute path in its notes.");

            if (HostedKinds.Contains(kind, StringComparer.Ordinal))
            {
                AssertHostedEntry(entry, id, kind);
            }
            else if (string.Equals(kind, "merge", StringComparison.Ordinal))
            {
                AssertMergeEntry(entry, id);
            }
            else
            {
                Assert.Fail($"Ledger entry '{id}' has an unknown kind '{kind}'.");
            }
        }
    }

    [Fact]
    public void Entries_are_unique_and_appended_in_measured_time_order()
    {
        using var ledger = Load();
        var keys = ledger.RootElement.GetProperty("entries").EnumerateArray()
            .Select(entry => (
                Time: entry.TryGetProperty("createdUtc", out var created)
                    ? created.GetString() ?? string.Empty
                    : entry.GetProperty("mergedUtc").GetString() ?? string.Empty,
                Id: entry.GetProperty("id").GetString() ?? string.Empty))
            .ToList();

        Assert.Equal(keys.Count, keys.Select(key => key.Id).Distinct(StringComparer.Ordinal).Count());
        var ordered = keys
            .OrderBy(key => key.Time, StringComparer.Ordinal)
            .ThenBy(key => key.Id, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(ordered, keys);
    }

    [Fact]
    public void Every_hosted_run_cited_by_a_ledger_bound_record_is_in_the_ledger()
    {
        using var ledger = Load();
        var runIds = ledger.RootElement.GetProperty("entries").EnumerateArray()
            .Where(entry => HostedKinds.Contains(entry.GetProperty("kind").GetString(), StringComparer.Ordinal))
            .Select(entry => entry.GetProperty("runId").GetInt64().ToString(CultureInfo.InvariantCulture))
            .ToHashSet(StringComparer.Ordinal);
        Assert.NotEmpty(runIds);

        foreach (var record in LedgerBoundRecords)
        {
            var path = Path.Combine(RepositoryRoot, record.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(path), $"Ledger-bound record '{record}' does not exist.");
            var text = File.ReadAllText(path);
            var citations = RunCitation().Matches(text).Select(match => match.Groups["run"].Value).Distinct(StringComparer.Ordinal).ToList();
            Assert.NotEmpty(citations);
            var uncited = citations.Where(run => !runIds.Contains(run)).ToList();
            Assert.True(
                uncited.Count == 0,
                $"'{record}' cites hosted runs the evidence ledger does not carry: {string.Join(", ", uncited)}");
        }
    }

    [Fact]
    public void Measurement_tool_reads_each_conclusion_from_the_run_itself()
    {
        var tool = File.ReadAllText(MeasurementToolPath);
        Assert.StartsWith("# SPDX-License-Identifier: GPL-3.0-or-later", tool, StringComparison.Ordinal);
        Assert.Contains(ExpectedFormat, tool, StringComparison.Ordinal);
        Assert.Contains("gh run view", tool, StringComparison.Ordinal);
        Assert.Contains("--json databaseId,conclusion,headSha,event,headBranch,workflowName,createdAt", tool, StringComparison.Ordinal);
        Assert.Contains("& git -C $repositoryRoot rev-parse --verify", tool, StringComparison.Ordinal);
        Assert.Contains("merge-base --is-ancestor", tool, StringComparison.Ordinal);
        Assert.Contains("measured, not transcribed", tool, StringComparison.Ordinal);
        Assert.DoesNotContain("--exit-status", tool, StringComparison.Ordinal);
        Assert.DoesNotContain("gh run watch", tool, StringComparison.Ordinal);
        foreach (var field in new[] { "conclusion", "headSha", "event", "branch", "workflow", "createdUtc", "firstParent", "secondParent" })
        {
            Assert.Contains($"-Field \"{field}\"", tool, StringComparison.Ordinal);
        }
    }

    private static void AssertHostedEntry(JsonElement entry, string id, string kind)
    {
        var runIdElement = entry.GetProperty("runId");
        Assert.Equal(JsonValueKind.Number, runIdElement.ValueKind);
        Assert.True(runIdElement.TryGetInt64(out var runId) && runId > 0, $"Ledger entry '{id}' has no positive run identifier.");
        var prefix = string.Equals(kind, "hosted-ci", StringComparison.Ordinal) ? "ci" : "codeql";
        Assert.Equal(string.Create(CultureInfo.InvariantCulture, $"{prefix}-{runId}"), id);
        Assert.Equal(WorkflowByKind[kind], RequiredString(entry, "workflow", id));
        Assert.Contains(RequiredString(entry, "event", id), Events);
        RequiredString(entry, "branch", id);
        Assert.Matches(CommitSha(), RequiredString(entry, "headSha", id));

        var checkout = entry.GetProperty("checkoutSha");
        if (checkout.ValueKind != JsonValueKind.Null)
        {
            Assert.Matches(CommitSha(), checkout.GetString() ?? string.Empty);
        }

        var conclusion = RequiredString(entry, "conclusion", id);
        Assert.Contains(conclusion, Conclusions);
        Assert.Matches(UtcStamp(), RequiredString(entry, "createdUtc", id));

        var receipt = entry.GetProperty("receipt");
        if (receipt.ValueKind != JsonValueKind.Null)
        {
            Assert.Equal("hosted-ci", kind);
            Assert.Matches(ReceiptId(), receipt.GetString() ?? string.Empty);
        }

        var tests = entry.GetProperty("tests");
        if (tests.ValueKind != JsonValueKind.Null)
        {
            Assert.Equal("hosted-ci", kind);
            var passed = tests.GetProperty("passed").GetInt32();
            var total = tests.GetProperty("total").GetInt32();
            Assert.True(total > 0 && passed >= 0 && passed <= total, $"Ledger entry '{id}' has incoherent test counts.");
            if (string.Equals(conclusion, "success", StringComparison.Ordinal))
            {
                Assert.Equal(total, passed);
            }
            else
            {
                Assert.True(passed < total, $"Ledger entry '{id}' is not a success but reports every test passed.");
            }
        }

        if (entry.TryGetProperty("sarifSha256", out var sarif))
        {
            Assert.Equal("hosted-codeql", kind);
            Assert.Matches(Sha256(), sarif.GetString() ?? string.Empty);
        }
    }

    private static void AssertMergeEntry(JsonElement entry, string id)
    {
        Assert.True(entry.GetProperty("pullRequest").TryGetInt32(out var pullRequest) && pullRequest > 0, $"Ledger entry '{id}' has no pull request number.");
        Assert.Equal(string.Create(CultureInfo.InvariantCulture, $"merge-pr-{pullRequest}"), id);
        var commit = RequiredString(entry, "commit", id);
        var firstParent = RequiredString(entry, "firstParent", id);
        var secondParent = RequiredString(entry, "secondParent", id);
        Assert.Matches(CommitSha(), commit);
        Assert.Matches(CommitSha(), firstParent);
        Assert.Matches(CommitSha(), secondParent);
        Assert.Equal(3, new[] { commit, firstParent, secondParent }.Distinct(StringComparer.Ordinal).Count());
        Assert.Matches(UtcStamp(), RequiredString(entry, "mergedUtc", id));
    }

    private static string RequiredString(JsonElement element, string property, string owner)
    {
        Assert.True(element.TryGetProperty(property, out var value), $"'{owner}' lacks required property '{property}'.");
        Assert.Equal(JsonValueKind.String, value.ValueKind);
        var text = value.GetString();
        Assert.False(string.IsNullOrWhiteSpace(text), $"'{owner}' has an empty '{property}'.");
        return text;
    }

    private static JsonDocument Load()
        => JsonDocument.Parse(
            File.ReadAllBytes(LedgerPath),
            new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpenClassroomFoundry.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root for evidence-ledger tests.");
    }
}
