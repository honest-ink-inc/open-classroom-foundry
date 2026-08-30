using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Foundry.Domain;
using Foundry.Tools.ProjectUpgradeHost;

namespace Foundry.Tests.Integration;

public sealed class ProjectUpgradeOperatorHostTests : IDisposable
{
    private const string PriorEngineVersion = "0.1.0-dev";
    private const string FixtureSha256 = "9B592AA00C2CCB31C5678D82C1685DBE99E023FA482AE25F103AE0D50D9A13FD";
    private const string RelativeSource = "prior/assets.ocfproj";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "ocf-operator-host-tests", Guid.NewGuid().ToString("N"));
    private readonly string _sourceRoot;
    private readonly string _candidateRoot;
    private readonly string _planPath;
    private readonly byte[] _fixture;

    public ProjectUpgradeOperatorHostTests()
    {
        var repository = FindRepositoryRoot();
        _fixture = Convert.FromBase64String(File.ReadAllText(Path.Combine(
            repository,
            "tests",
            "Integration",
            "Fixtures",
            "upgrade",
            "0.1.0-dev-schema-1-assets.ocfproj.base64")));
        Assert.Equal(FixtureSha256, Sha256(_fixture));

        _sourceRoot = Path.Combine(_root, "source-library");
        _candidateRoot = Path.Combine(_root, "candidate-library");
        _planPath = Path.Combine(_root, "exact-plan.json");
        var sourcePath = Path.Combine(_sourceRoot, RelativeSource.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        Directory.CreateDirectory(_candidateRoot);
        File.WriteAllBytes(sourcePath, _fixture);
    }

    [Fact]
    public async Task Reviewed_exact_plan_prepares_once_and_prints_only_content_free_receipts()
    {
        WritePlan();
        var reviewOutput = new StringWriter();
        var reviewError = new StringWriter();

        var reviewExit = await ProjectUpgradeOperatorHost.RunAsync(
            ["review", "--plan", _planPath],
            reviewOutput,
            reviewError,
            CancellationToken.None);
        var exactPlanHash = Sha256(await File.ReadAllBytesAsync(_planPath));

        Assert.Equal(0, reviewExit);
        Assert.Empty(reviewError.ToString());
        Assert.Contains(exactPlanHash, reviewOutput.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(_root, reviewOutput.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(RelativeSource, reviewOutput.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_candidateRoot));

        var receiptOutput = new StringWriter();
        var receiptError = new StringWriter();
        var prepareExit = await ProjectUpgradeOperatorHost.RunAsync(
            ["prepare", "--plan", _planPath, "--confirm-plan-sha256", exactPlanHash],
            receiptOutput,
            receiptError,
            CancellationToken.None);

        Assert.Equal(0, prepareExit);
        Assert.Empty(receiptError.ToString());
        Assert.Contains("Prepared project count: 1", receiptOutput.ToString(), StringComparison.Ordinal);
        Assert.Contains("Package transformed: True", receiptOutput.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(_root, receiptOutput.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(RelativeSource, receiptOutput.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Synthetic asset compatibility fixture", receiptOutput.ToString(), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(_candidateRoot, "prepared", "assets.ocfproj")));
        Assert.Equal(_fixture, await File.ReadAllBytesAsync(Path.Combine(_sourceRoot, RelativeSource.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Theory]
    [InlineData("{", OperatorUpgradeFailureCodes.PlanInvalid)]
    [InlineData("extra", OperatorUpgradeFailureCodes.PlanInvalid)]
    [InlineData("duplicate", OperatorUpgradeFailureCodes.PlanInvalid)]
    [InlineData("target", OperatorUpgradeFailureCodes.TargetVersionMismatch)]
    [InlineData("content-in-version", OperatorUpgradeFailureCodes.PlanInvalid)]
    public async Task Malformed_extra_duplicate_and_wrong_target_plans_fail_closed(
        string mutation,
        string expectedCode)
    {
        WritePlan(mutation);

        var (exitCode, output, error) = await RunReviewAsync();

        Assert.Equal(2, exitCode);
        Assert.Empty(output);
        Assert.Contains($"Code: {expectedCode}", error, StringComparison.Ordinal);
        Assert.DoesNotContain(_root, error, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(RelativeSource, error, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("teacher-authored phrase", error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_candidateRoot));
    }

    [Fact]
    public async Task Partial_source_inventory_is_refused_before_preparation()
    {
        File.WriteAllBytes(Path.Combine(_sourceRoot, "unaddressed.ocfproj"), _fixture);
        WritePlan();

        var (exitCode, output, error) = await RunReviewAsync();

        Assert.Equal(2, exitCode);
        Assert.Empty(output);
        Assert.Contains($"Code: {OperatorUpgradeFailureCodes.InventoryNotClosed}", error, StringComparison.Ordinal);
        Assert.DoesNotContain("unaddressed.ocfproj", error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_candidateRoot));
    }

    [Fact]
    public async Task Root_and_package_address_failures_remain_content_free()
    {
        WritePlan(sourceRoot: "relative-source");
        var (ExitCode, _, Error) = await RunReviewAsync();
        Assert.Equal(2, ExitCode);
        Assert.Contains($"Code: {OperatorUpgradeFailureCodes.RootInvalid}", Error, StringComparison.Ordinal);
        Assert.DoesNotContain("relative-source", Error, StringComparison.Ordinal);

        WritePlan(sourceSha256: new string('0', 64));
        var planSha = Sha256(await File.ReadAllBytesAsync(_planPath));
        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = await ProjectUpgradeOperatorHost.RunAsync(
            ["prepare", "--plan", _planPath, "--confirm-plan-sha256", planSha],
            output,
            error,
            CancellationToken.None);

        Assert.Equal(3, exitCode);
        Assert.Empty(output.ToString());
        Assert.Contains("Code: upgrade.source-address-mismatch", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(_root, error.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(RelativeSource, error.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_candidateRoot));
    }

    [Fact]
    public async Task Plan_sha_confirmation_must_bind_the_exact_file_bytes()
    {
        WritePlan();
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await ProjectUpgradeOperatorHost.RunAsync(
            ["prepare", "--plan", _planPath, "--confirm-plan-sha256", new string('0', 64)],
            output,
            error,
            CancellationToken.None);

        Assert.Equal(2, exitCode);
        Assert.Empty(output.ToString());
        Assert.Contains($"Code: {OperatorUpgradeFailureCodes.PlanNotConfirmed}", error.ToString(), StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_candidateRoot));
    }

    [Fact]
    public async Task Reparse_source_root_is_refused_without_following_it()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var target = Path.Combine(_root, "reparse-target");
        var link = Path.Combine(_root, "reparse-source");
        Directory.CreateDirectory(target);
        File.WriteAllBytes(Path.Combine(target, "asset.ocfproj"), _fixture);
        Directory.CreateSymbolicLink(link, target);
        WritePlan(sourceRoot: link, sourceRelativePath: "asset.ocfproj");
        var (ExitCode, _, Error) = await RunReviewAsync();

        Assert.Equal(2, ExitCode);
        Assert.Contains($"Code: {OperatorUpgradeFailureCodes.RootInvalid}", Error, StringComparison.Ordinal);
        Assert.DoesNotContain(link, Error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_candidateRoot));
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
            // Disposable synthetic evidence only; cleanup is best-effort.
        }
    }

    private async Task<(int ExitCode, string Output, string Error)> RunReviewAsync()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = await ProjectUpgradeOperatorHost.RunAsync(
            ["review", "--plan", _planPath],
            output,
            error,
            CancellationToken.None);
        return (exitCode, output.ToString(), error.ToString());
    }

    private void WritePlan(
        string? mutation = null,
        string? sourceRoot = null,
        string? sourceRelativePath = null,
        string? sourceSha256 = null)
    {
        if (mutation == "{")
        {
            File.WriteAllText(_planPath, "{");
            return;
        }

        var plan = $$"""
            {
              "schemaVersion": "1",
              "sourceLibraryRoot": {{JsonSerializer.Serialize(sourceRoot ?? _sourceRoot)}},
              "candidateLibraryRoot": {{JsonSerializer.Serialize(_candidateRoot)}},
              "targetEngineVersion": {{JsonSerializer.Serialize(mutation == "target" ? "not-this-build" : EngineIdentity.EngineVersion)}},
              "projects": [
                {
                  "sourceRelativePath": {{JsonSerializer.Serialize(sourceRelativePath ?? RelativeSource)}},
                  "destinationRelativePath": "prepared/assets.ocfproj",
                  "sourceEngineVersion": {{JsonSerializer.Serialize(mutation == "content-in-version" ? "teacher-authored phrase" : PriorEngineVersion)}},
                  "sourceSchemaVersion": "1",
                  "sourceSha256": {{JsonSerializer.Serialize(sourceSha256 ?? FixtureSha256)}}
                }
              ]{{(mutation == "extra" ? ",\n  \"extra\": true" : string.Empty)}}
            }
            """;
        if (mutation == "duplicate")
        {
            plan = plan.Replace(
                "\"schemaVersion\": \"1\",",
                "\"schemaVersion\": \"1\",\n  \"schemaVersion\": \"1\",",
                StringComparison.Ordinal);
        }

        File.WriteAllText(_planPath, plan, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpenClassroomFoundry.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root for frozen fixture.");
    }

    private static string Sha256(byte[] bytes)
        => Convert.ToHexString(SHA256.HashData(bytes));
}
