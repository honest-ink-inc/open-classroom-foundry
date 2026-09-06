// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;
using System.Text.Json;

namespace Foundry.Tests.Unit;

public sealed class RecipeRatificationHistoryVerifierTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string VerifierPath = Path.Combine(
        RepositoryRoot,
        "tools",
        "verify-recipe-identity-ratification.ps1");
    private static readonly string WorkflowPath = Path.Combine(
        RepositoryRoot,
        ".github",
        "workflows",
        "ci.yml");
    private const string PacketRelativePath = "docs/adr/recipe-identity-disposition-packet.md";
    private const string SampleManifestRelativePath = "tests/Rendering/Fixtures/recipe-first-admission-samples.sha256";
    private const string SyntheticSampleManifest = "synthetic.html AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\n";

    private static readonly string[] ExactC2Files =
    [
        "README.md",
        "docs/README.md",
        "docs/adr/README.md",
        "docs/adr/recipe-identity-disposition-packet.md",
        "docs/handover/2026-09-01-forge-integration-handover.md",
        "tests/Unit/RecipeIdentityDispositionPacketTests.cs",
    ];

    [Fact]
    public void Workflow_fetches_full_history_enforces_ratification_and_retains_one_bounded_receipt()
    {
        var workflow = File.ReadAllText(WorkflowPath);
        var buildStart = workflow.IndexOf("  build-and-test:", StringComparison.Ordinal);
        var buildEnd = workflow.IndexOf("  portable-samples:", buildStart, StringComparison.Ordinal);
        Assert.True(buildStart >= 0 && buildEnd > buildStart);
        var buildJob = workflow[buildStart..buildEnd];

        Assert.Contains("fetch-depth: 0", buildJob, StringComparison.Ordinal);
        Assert.Contains("- name: Verify recipe-identity ratification history", buildJob, StringComparison.Ordinal);
        Assert.Contains(
            "run: pwsh -NoProfile -File tools/verify-recipe-identity-ratification.ps1 -RequireRatified",
            buildJob,
            StringComparison.Ordinal);
        Assert.Contains("- name: Retain recipe-identity ratification history evidence", buildJob, StringComparison.Ordinal);
        Assert.Contains("if: always()", buildJob, StringComparison.Ordinal);
        Assert.Contains("name: recipe-identity-ratification-history", buildJob, StringComparison.Ordinal);
        Assert.Contains(
            "path: out/recipe-identity-ratification/history-receipt.json",
            buildJob,
            StringComparison.Ordinal);
        Assert.Contains("if-no-files-found: error", buildJob, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(buildJob, "tools/verify-recipe-identity-ratification.ps1"));
    }

    [Fact]
    public void Explicit_pending_C1_skips_only_locally_and_is_refused_when_ratification_is_required()
    {
        using var fixture = new GitFixture();
        fixture.WriteFile(
            "docs/adr/recipe-identity-disposition-packet.md",
            PendingPacket());
        fixture.CommitAll("pending C1");

        var local = RunVerifier(fixture.Root, requireRatified: false);
        Assert.Equal(0, local.ExitCode);
        using (var receipt = ReadReceipt(fixture.Root))
        {
            Assert.Equal("skipped-pending-c1", receipt.RootElement.GetProperty("Outcome").GetString());
            Assert.False(receipt.RootElement.GetProperty("RequireRatified").GetBoolean());
        }

        var required = RunVerifier(fixture.Root, requireRatified: true);
        Assert.NotEqual(0, required.ExitCode);
        using var failedReceipt = ReadReceipt(fixture.Root);
        Assert.Equal("failed", failedReceipt.RootElement.GetProperty("Outcome").GetString());
        Assert.Equal("ratification-required", failedReceipt.RootElement.GetProperty("FailureCode").GetString());
    }

    [Fact]
    public void Ratified_C2_is_verified_from_a_later_regular_merge_commit()
    {
        using var fixture = new GitFixture();
        fixture.WriteFile("base.txt", "base\n");
        fixture.CommitAll("base");
        fixture.Git("checkout", "-b", "integration");
        WriteC1Files(fixture);
        var c1 = fixture.CommitAll("C1 candidate freeze");
        WriteC2Files(fixture, c1);
        var c2 = fixture.CommitAll("C2 ratification record");

        fixture.Git("checkout", "main");
        fixture.WriteFile("main-only.txt", "main advanced\n");
        fixture.CommitAll("main advanced");
        fixture.Git("merge", "--no-ff", "integration", "-m", "regular integration merge");

        var result = RunVerifier(fixture.Root, requireRatified: true);
        AssertSuccessful(result);
        using var receipt = ReadReceipt(fixture.Root);
        var root = receipt.RootElement;
        Assert.Equal("verified", root.GetProperty("Outcome").GetString());
        Assert.Equal(c1, root.GetProperty("CandidateFreezeCommit").GetString());
        Assert.Equal(c2, root.GetProperty("RatificationCommit").GetString());
        Assert.True(root.GetProperty("CandidateFreezeIsImmediateSingleParent").GetBoolean());
        Assert.True(root.GetProperty("CandidateFreezeIsAncestorOfHead").GetBoolean());
        Assert.True(root.GetProperty("RatificationIsAncestorOfHead").GetBoolean());
        Assert.True(root.GetProperty("OriginalDecisionRecordPreserved").GetBoolean());
        Assert.True(root.GetProperty("FirstAdmissionSampleManifestPreserved").GetBoolean());
        Assert.Equal(
            fixture.Git("rev-parse", $"{c2}:{PacketRelativePath}").Trim(),
            root.GetProperty("OriginalDecisionRecordGitBlob").GetString());
        Assert.Equal(
            fixture.Git("rev-parse", $"{c1}:{SampleManifestRelativePath}").Trim(),
            root.GetProperty("FirstAdmissionSampleManifestC1GitBlob").GetString());
        Assert.Equal(
            ExactC2Files,
            root.GetProperty("ActualC2ChangedFiles").EnumerateArray().Select(item => item.GetString()));
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, "docs/adr/README.md")]
    public void Record_only_C2_requires_the_exact_six_file_set(bool addUnexpectedFile, string? omitExpectedFile)
    {
        using var fixture = new GitFixture();
        fixture.WriteFile("base.txt", "base\n");
        fixture.CommitAll("base");
        WriteC1Files(fixture);
        var c1 = fixture.CommitAll("C1 candidate freeze");
        WriteC2Files(fixture, c1, omitExpectedFile);
        if (addUnexpectedFile)
        {
            fixture.WriteFile("unexpected.txt", "not record-only\n");
        }
        fixture.CommitAll("invalid C2");

        var result = RunVerifier(fixture.Root, requireRatified: true);
        Assert.NotEqual(0, result.ExitCode);
        using var receipt = ReadReceipt(fixture.Root);
        Assert.Equal("c2-file-set-mismatch", receipt.RootElement.GetProperty("FailureCode").GetString());
    }

    [Fact]
    public void Recorded_C1_must_be_C2s_immediate_single_parent()
    {
        using var fixture = new GitFixture();
        fixture.WriteFile("base.txt", "base\n");
        fixture.CommitAll("base");
        WriteC1Files(fixture);
        var c1 = fixture.CommitAll("C1 candidate freeze");
        fixture.WriteFile("intermediate.txt", "intervening commit\n");
        fixture.CommitAll("intervening commit");
        WriteC2Files(fixture, c1);
        fixture.CommitAll("non-immediate C2");

        var result = RunVerifier(fixture.Root, requireRatified: true);
        Assert.NotEqual(0, result.ExitCode);
        using var receipt = ReadReceipt(fixture.Root);
        Assert.Equal("ratification-child-count", receipt.RootElement.GetProperty("FailureCode").GetString());
    }

    [Fact]
    public void A_merge_commit_cannot_substitute_for_the_single_parent_record_only_C2()
    {
        using var fixture = new GitFixture();
        fixture.WriteFile("base.txt", "base\n");
        fixture.CommitAll("base");
        WriteC1Files(fixture);
        var c1 = fixture.CommitAll("C1 candidate freeze");

        fixture.Git("checkout", "-b", "side");
        fixture.WriteFile("side.txt", "side parent\n");
        fixture.CommitAll("side parent");
        fixture.Git("checkout", "main");
        fixture.Git("merge", "--no-ff", "side", "-m", "pending merge");
        WriteC2Files(fixture, c1);
        fixture.Git("add", "--all");
        fixture.Git("commit", "--amend", "--no-edit");

        var result = RunVerifier(fixture.Root, requireRatified: true);
        Assert.NotEqual(0, result.ExitCode);
        using var receipt = ReadReceipt(fixture.Root);
        Assert.Equal("ratification-child-count", receipt.RootElement.GetProperty("FailureCode").GetString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Later_packet_rewrites_and_same_file_continuations_cannot_replace_the_original_decision(bool appendOnly)
    {
        using var fixture = new GitFixture();
        WriteC1Files(fixture);
        var c1 = fixture.CommitAll("C1 candidate freeze");
        WriteC2Files(fixture, c1);
        fixture.CommitAll("C2 ratification record");
        var packet = RatifiedPacket(c1);
        fixture.WriteFile(PacketRelativePath, appendOnly
            ? packet + "\n\n## Subsequent factual notes\n\nSynthetic later note belongs in a separate record.\n"
            : packet.Replace("Synthetic immutable decision.", "Synthetic changed decision.", StringComparison.Ordinal));
        fixture.CommitAll("later record changed without rewriting C1 or C2");

        var result = RunVerifier(fixture.Root, requireRatified: true);

        AssertFailure(fixture.Root, result, "decision-record-drift");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_later_commit_cannot_rebaseline_sample_bytes_or_their_line_endings(bool lineEndingsOnly)
    {
        using var fixture = new GitFixture();
        WriteC1Files(fixture);
        var c1 = fixture.CommitAll("C1 candidate freeze");
        WriteC2Files(fixture, c1);
        fixture.CommitAll("C2 ratification record");
        fixture.WriteFile(SampleManifestRelativePath, lineEndingsOnly
            ? SyntheticSampleManifest.ReplaceLineEndings("\r\n")
            : SyntheticSampleManifest.Replace('A', 'B'));
        // A changed current-tree expected value is not independent evidence.
        fixture.WriteFile("tests/Unit/RecipeIdentityDispositionPacketTests.cs", "Synthetic replacement expected hash.\n");
        fixture.CommitAll("coordinated current-tree baseline replacement");

        var result = RunVerifier(fixture.Root, requireRatified: true);

        AssertFailure(fixture.Root, result, "sample-baseline-drift");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Missing_original_or_current_sample_evidence_cannot_be_called_verified(bool removeOriginal)
    {
        using var fixture = new GitFixture();
        WriteC1Files(fixture);
        if (removeOriginal)
        {
            File.Delete(Path.Combine(fixture.Root, SampleManifestRelativePath));
        }
        var c1 = fixture.CommitAll("C1 candidate freeze");
        WriteC2Files(fixture, c1);
        fixture.CommitAll("C2 ratification record");
        if (!removeOriginal)
        {
            File.Delete(Path.Combine(fixture.Root, SampleManifestRelativePath));
            fixture.CommitAll("removed current baseline");
        }

        var result = RunVerifier(fixture.Root, requireRatified: true);

        AssertFailure(fixture.Root, result, "sample-baseline-missing");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Dirty_sample_bytes_are_refused_even_before_commit(bool staged)
    {
        using var fixture = new GitFixture();
        WriteC1Files(fixture);
        var c1 = fixture.CommitAll("C1 candidate freeze");
        WriteC2Files(fixture, c1);
        fixture.CommitAll("C2 ratification record");
        fixture.WriteFile(SampleManifestRelativePath, SyntheticSampleManifest.Replace('A', 'B'));
        if (staged)
        {
            fixture.Git("add", "--", SampleManifestRelativePath);
        }

        var result = RunVerifier(fixture.Root, requireRatified: true);

        AssertFailure(fixture.Root, result, staged ? "sample-baseline-dirty" : "sample-baseline-working-copy-drift");
    }

    [Theory]
    [InlineData(PacketRelativePath, "decision-record-working-copy-drift")]
    [InlineData(SampleManifestRelativePath, "sample-baseline-working-copy-drift")]
    public void An_index_assume_unchanged_flag_cannot_hide_changed_working_copy_bytes(string path, string failureCode)
    {
        using var fixture = new GitFixture();
        WriteC1Files(fixture);
        var c1 = fixture.CommitAll("C1 candidate freeze");
        WriteC2Files(fixture, c1);
        fixture.CommitAll("C2 ratification record");
        fixture.Git("update-index", "--assume-unchanged", "--", path);
        fixture.WriteFile(path, path == PacketRelativePath
            ? RatifiedPacket(c1).Replace("Synthetic immutable decision.", "Synthetic hidden decision.", StringComparison.Ordinal)
            : SyntheticSampleManifest.Replace('A', 'B'));
        Assert.Equal(string.Empty, fixture.Git("status", "--porcelain=v1", "--", path).Trim());

        var result = RunVerifier(fixture.Root, requireRatified: true);

        AssertFailure(fixture.Root, result, failureCode);
    }

    [Fact]
    public void A_separate_factual_continuation_preserves_both_immutable_records()
    {
        using var fixture = new GitFixture();
        WriteC1Files(fixture);
        var c1 = fixture.CommitAll("C1 candidate freeze");
        WriteC2Files(fixture, c1);
        fixture.CommitAll("C2 ratification record");
        fixture.WriteFile("docs/evidence/synthetic-continuation.md", "Synthetic factual continuation; no amendment or authority.\n");
        fixture.CommitAll("separate factual continuation");

        var result = RunVerifier(fixture.Root, requireRatified: true);

        AssertSuccessful(result);
    }

    [Fact]
    public void Decision_packet_accepts_only_its_explicit_CRLF_to_LF_checkout_normalization()
    {
        using var fixture = new GitFixture();
        fixture.WriteFile(".gitattributes", $"{PacketRelativePath} text eol=lf\n{SampleManifestRelativePath} -text\n");
        WriteC1Files(fixture);
        var c1 = fixture.CommitAll("C1 candidate freeze");
        WriteC2Files(fixture, c1);
        fixture.CommitAll("C2 ratification record");
        fixture.WriteFile(PacketRelativePath, RatifiedPacket(c1).ReplaceLineEndings("\r\n"));

        var result = RunVerifier(fixture.Root, requireRatified: true);

        AssertSuccessful(result);
    }

    [Fact]
    public void Mutable_attributes_cannot_normalize_changed_raw_sample_bytes_back_to_C1()
    {
        using var fixture = new GitFixture();
        fixture.WriteFile(".gitattributes", $"{PacketRelativePath} text eol=lf\n{SampleManifestRelativePath} text eol=lf\n");
        WriteC1Files(fixture);
        var c1 = fixture.CommitAll("C1 candidate freeze");
        WriteC2Files(fixture, c1);
        fixture.CommitAll("C2 ratification record");
        fixture.WriteFile(".gitattributes", $"{PacketRelativePath} text eol=lf\n{SampleManifestRelativePath} text eol=crlf\n");
        fixture.WriteFile(SampleManifestRelativePath, SyntheticSampleManifest.ReplaceLineEndings("\r\n"));
        fixture.CommitAll("later attributes hide changed raw sample line endings");
        var admittedBlob = fixture.Git("rev-parse", $"{c1}:{SampleManifestRelativePath}").Trim();
        Assert.Equal(admittedBlob, fixture.Git("rev-parse", $"HEAD:{SampleManifestRelativePath}").Trim());
        Assert.NotEqual(admittedBlob, fixture.Git("hash-object", "--no-filters", "--", SampleManifestRelativePath).Trim());
        Assert.Equal(string.Empty, fixture.Git("status", "--porcelain=v1", "--", SampleManifestRelativePath).Trim());

        var result = RunVerifier(fixture.Root, requireRatified: true);

        AssertFailure(fixture.Root, result, "sample-baseline-working-copy-drift");
    }

    [Theory]
    [InlineData(PacketRelativePath, "decision-record-working-copy-drift")]
    [InlineData(SampleManifestRelativePath, "sample-baseline-working-copy-drift")]
    public void A_clean_filter_cannot_substitute_original_bytes_for_a_changed_working_copy(string path, string failureCode)
    {
        using var fixture = new GitFixture();
        fixture.WriteFile(".gitattributes", $"{PacketRelativePath} text eol=lf\n{SampleManifestRelativePath} -text\n");
        WriteC1Files(fixture);
        var c1 = fixture.CommitAll("C1 candidate freeze");
        WriteC2Files(fixture, c1);
        var c2 = fixture.CommitAll("C2 ratification record");
        fixture.Git("config", "filter.synthetic-record.clean", $"git show {c2}:{path}");
        fixture.Git("config", "filter.synthetic-record.required", "true");
        fixture.WriteFile(".gitattributes", $"{PacketRelativePath} text eol=lf\n{SampleManifestRelativePath} -text\n{path} filter=synthetic-record -text\n");
        fixture.WriteFile(path, path == PacketRelativePath
            ? RatifiedPacket(c1).Replace("Synthetic immutable decision.", "Synthetic filtered decision.", StringComparison.Ordinal)
            : SyntheticSampleManifest.Replace('A', 'B'));
        fixture.CommitAll("clean filter conceals a raw working-copy replacement");
        var admittedBlob = fixture.Git("rev-parse", $"{c2}:{path}").Trim();
        Assert.Equal(admittedBlob, fixture.Git("rev-parse", $"HEAD:{path}").Trim());
        Assert.NotEqual(admittedBlob, fixture.Git("hash-object", "--no-filters", "--", path).Trim());
        Assert.Equal(string.Empty, fixture.Git("status", "--porcelain=v1", "--", path).Trim());

        var result = RunVerifier(fixture.Root, requireRatified: true);

        AssertFailure(fixture.Root, result, failureCode);
    }

    private static void WriteC1Files(GitFixture fixture)
    {
        fixture.WriteFile(SampleManifestRelativePath, SyntheticSampleManifest);
        foreach (var path in ExactC2Files)
        {
            fixture.WriteFile(path, path.EndsWith("recipe-identity-disposition-packet.md", StringComparison.Ordinal)
                ? PendingPacket()
                : $"C1 {path}\n");
        }
    }

    private static void WriteC2Files(GitFixture fixture, string c1, string? omit = null)
    {
        foreach (var path in ExactC2Files.Where(path => !string.Equals(path, omit, StringComparison.Ordinal)))
        {
            fixture.WriteFile(path, path.EndsWith("recipe-identity-disposition-packet.md", StringComparison.Ordinal)
                ? RatifiedPacket(c1)
                : $"C2 {path}\n");
        }
    }

    private static string PendingPacket()
        => """
            # Recipe identity disposition packet

            **Status:** DECIDED — OPTION A; candidate freeze hash pending in local C1; do not push this transitional state

            | Record field | Explicit disposition |
            |---|---|
            | Status | `DECIDED — OPTION A; candidate freeze hash pending in local C1; do not push this transitional state` |
            | Exact candidate freeze state | `PENDING-C1-COMMIT-HASH` — local-only transitional marker |
            """.ReplaceLineEndings("\n");

    private static string RatifiedPacket(string c1)
        => $$"""
            # Recipe identity disposition packet

            **Status:** RATIFIED — OPTION A

            Record-only C2 replaced `PENDING-C1-COMMIT-HASH`; this historical
            explanation is not a pending-state field.

            | Record field | Explicit disposition |
            |---|---|
            | Status | `RATIFIED — OPTION A` |
            | Exact candidate freeze state | `{{c1}}` — C1 exact candidate freeze |
            | Exact written disposition | Synthetic immutable decision. |
            """.ReplaceLineEndings("\n");

    private static VerifierResult RunVerifier(string repository, bool requireRatified)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "pwsh",
            WorkingDirectory = repository,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(VerifierPath);
        startInfo.ArgumentList.Add("-RepositoryRoot");
        startInfo.ArgumentList.Add(repository);
        if (requireRatified)
        {
            startInfo.ArgumentList.Add("-RequireRatified");
        }

        using var process = new Process { StartInfo = startInfo };
        Assert.True(process.Start(), "The recipe-ratification verifier did not start.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(30_000), "The recipe-ratification verifier exceeded 30 seconds.");
        Task.WaitAll(outputTask, errorTask);
        return new VerifierResult(process.ExitCode, outputTask.Result, errorTask.Result);
    }

    private static JsonDocument ReadReceipt(string repository)
        => JsonDocument.Parse(File.ReadAllText(Path.Combine(
            repository,
            "out",
            "recipe-identity-ratification",
            "history-receipt.json")));

    private static void AssertSuccessful(VerifierResult result)
        => Assert.True(
            result.ExitCode == 0,
            $"Verifier failed with exit {result.ExitCode}:{Environment.NewLine}" +
            $"stdout:{Environment.NewLine}{result.StandardOutput}{Environment.NewLine}" +
            $"stderr:{Environment.NewLine}{result.StandardError}");

    private static int CountOccurrences(string value, string pattern)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }

    private static void AssertFailure(string repository, VerifierResult result, string failureCode)
    {
        Assert.True(
            result.ExitCode != 0,
            $"The changed record was accepted.{Environment.NewLine}{result.StandardOutput}{Environment.NewLine}{result.StandardError}");
        using var receipt = ReadReceipt(repository);
        Assert.Equal("failed", receipt.RootElement.GetProperty("Outcome").GetString());
        Assert.Equal(failureCode, receipt.RootElement.GetProperty("FailureCode").GetString());
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "OpenClassroomFoundry.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Repository root not found.");
    }

    private sealed class GitFixture : IDisposable
    {
        public GitFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "ocf-ratification-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            Git("init", "--initial-branch=main");
            Git("config", "core.autocrlf", "false");
            Git("config", "user.name", "Synthetic Ratification Test");
            Git("config", "user.email", "synthetic-ratification@example.invalid");
        }

        public string Root { get; }

        public void WriteFile(string relativePath, string content)
        {
            var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        public string CommitAll(string message)
        {
            Git("add", "--all");
            Git("commit", "-m", message);
            return Git("rev-parse", "HEAD").Trim();
        }

        public string Git(params string[] arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = Root,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo)
                ?? throw new Xunit.Sdk.XunitException("Synthetic Git process did not start.");
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            Assert.True(process.WaitForExit(30_000), "Synthetic Git process exceeded 30 seconds.");
            Assert.True(
                process.ExitCode == 0,
                $"git {string.Join(' ', arguments)} failed with exit {process.ExitCode}:" +
                $"{Environment.NewLine}{output}{Environment.NewLine}{error}");
            return output;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root) &&
                Root.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase))
            {
                foreach (var path in Directory.EnumerateFileSystemEntries(
                             Root,
                             "*",
                             SearchOption.AllDirectories))
                {
                    File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
                }
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private sealed record VerifierResult(int ExitCode, string StandardOutput, string StandardError);
}
