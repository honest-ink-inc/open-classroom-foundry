using System.IO.Compression;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.BuiltIn;
using Foundry.Modules.DeterministicPress;
using Foundry.Storage;
using Foundry.Tools.ProjectUpgradeHost;
using Xunit.Abstractions;

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
    private readonly ITestOutputHelper _testOutput;

    public ProjectUpgradeOperatorHostTests(ITestOutputHelper testOutput)
    {
        _testOutput = testOutput;
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
        _candidateRoot = Path.Combine(_root, EngineIdentity.EngineVersion, "candidate-library");
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
        var sourcePath = Path.Combine(
            _sourceRoot,
            RelativeSource.Replace('/', Path.DirectorySeparatorChar));
        var planBeforeReview = Sha256(await File.ReadAllBytesAsync(_planPath));
        var sourceBeforeReview = Sha256(await File.ReadAllBytesAsync(sourcePath));
        var reviewOutput = new StringWriter();
        var reviewError = new StringWriter();

        var reviewExit = await ProjectUpgradeOperatorHost.RunAsync(
            ["review", "--plan", _planPath],
            reviewOutput,
            reviewError,
            CancellationToken.None);
        var exactPlanHash = Sha256(await File.ReadAllBytesAsync(_planPath));
        var reviewText = reviewOutput.ToString();

        Assert.Equal(0, reviewExit);
        Assert.Empty(reviewError.ToString());
        Assert.Equal(planBeforeReview, exactPlanHash);
        Assert.Equal(planBeforeReview, Sha256(await File.ReadAllBytesAsync(_planPath)));
        Assert.Equal(sourceBeforeReview, Sha256(await File.ReadAllBytesAsync(sourcePath)));
        Assert.Contains(exactPlanHash, reviewText, StringComparison.Ordinal);
        Assert.Contains(
            $"Candidate recipe inventory framing: {ProjectUpgradeOperatorHost.CandidateRecipeIdentityInventoryFramingVersion}",
            reviewText,
            StringComparison.Ordinal);
        Assert.Contains(
            ProjectUpgradeOperatorHost.ExecutingCandidateRecipesSha256(),
            reviewText,
            StringComparison.Ordinal);
        Assert.Contains(
            $"Candidate recipe-contract inventory framing: {ProjectUpgradeOperatorHost.CandidateRecipeContractInventoryFramingVersion}",
            reviewText,
            StringComparison.Ordinal);
        Assert.Contains(
            $"Recipe contract fingerprint framing: {RecipeContractFingerprint.FramingVersion}",
            reviewText,
            StringComparison.Ordinal);
        Assert.Contains(
            ProjectUpgradeOperatorHost.ExecutingCandidateRecipeContractsSha256(),
            reviewText,
            StringComparison.Ordinal);
        var expectedConstituents = ProjectUpgradeOperatorHost.ExecutingCandidateRecipeContracts();
        Assert.Contains(
            $"Candidate recipe constituent count: {expectedConstituents.Count.ToString(CultureInfo.InvariantCulture)}",
            reviewText,
            StringComparison.Ordinal);
        var constituentLines = reviewText.Split(
                ["\r\n", "\n"],
                StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.StartsWith("Candidate recipe constituent: ", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(expectedConstituents.Count, constituentLines.Length);
        Assert.Equal(
            expectedConstituents,
            expectedConstituents
                .OrderBy(recipe => recipe.RecipeId, StringComparer.Ordinal)
                .ThenBy(recipe => recipe.RecipeVersion, StringComparer.Ordinal));
        for (var index = 0; index < expectedConstituents.Count; index++)
        {
            var json = constituentLines[index]["Candidate recipe constituent: ".Length..];
            using var document = JsonDocument.Parse(json);
            var constituent = document.RootElement;
            var expected = expectedConstituents[index];
            Assert.Equal(expected.RecipeId, constituent.GetProperty("RecipeId").GetString());
            Assert.Equal(expected.RecipeVersion, constituent.GetProperty("RecipeVersion").GetString());
            Assert.Equal(expected.ManifestSha256, constituent.GetProperty("ManifestSha256").GetString());
            Assert.Equal(expected.ManifestSha256 is null, constituent.GetProperty("IdentityOnly").GetBoolean());
        }

        Assert.DoesNotContain(_root, reviewText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(RelativeSource, reviewText, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_candidateRoot));

        var receiptOutput = new StringWriter();
        var receiptError = new StringWriter();
        var prepareExit = await ProjectUpgradeOperatorHost.RunAsync(
            [
                "prepare", "--plan", _planPath,
                "--confirm-plan-sha256", exactPlanHash,
                "--confirm-candidate-recipes-sha256", ProjectUpgradeOperatorHost.ExecutingCandidateRecipesSha256(),
                "--confirm-candidate-recipe-contracts-sha256", ProjectUpgradeOperatorHost.ExecutingCandidateRecipeContractsSha256(),
            ],
            receiptOutput,
            receiptError,
            CancellationToken.None);

        Assert.Equal(0, prepareExit);
        Assert.Empty(receiptError.ToString());
        Assert.Contains("Prepared project count: 1", receiptOutput.ToString(), StringComparison.Ordinal);
        Assert.Contains(
            $"Candidate recipe-contract inventory framing: {ProjectUpgradeOperatorHost.CandidateRecipeContractInventoryFramingVersion}",
            receiptOutput.ToString(),
            StringComparison.Ordinal);
        Assert.Contains(
            $"Recipe contract fingerprint framing: {RecipeContractFingerprint.FramingVersion}",
            receiptOutput.ToString(),
            StringComparison.Ordinal);
        Assert.Contains("Package transformed: True", receiptOutput.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(_root, receiptOutput.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(RelativeSource, receiptOutput.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Synthetic asset compatibility fixture", receiptOutput.ToString(), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(_candidateRoot, "prepared", "assets.ocfproj")));
        Assert.Equal(_fixture, await File.ReadAllBytesAsync(sourcePath));
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
            [
                "prepare", "--plan", _planPath,
                "--confirm-plan-sha256", planSha,
                "--confirm-candidate-recipes-sha256", ProjectUpgradeOperatorHost.ExecutingCandidateRecipesSha256(),
                "--confirm-candidate-recipe-contracts-sha256", ProjectUpgradeOperatorHost.ExecutingCandidateRecipeContractsSha256(),
            ],
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
    public async Task Candidate_root_without_the_exact_engine_version_segment_is_refused_before_preparation()
    {
        var unversionedCandidate = Path.Combine(_root, "candidate-without-version");
        Directory.CreateDirectory(unversionedCandidate);
        WritePlan(candidateRoot: unversionedCandidate);

        var (exitCode, output, error) = await RunReviewAsync();

        Assert.Equal(2, exitCode);
        Assert.Empty(output);
        Assert.Contains(
            $"Code: {OperatorUpgradeFailureCodes.CandidateVersionSegmentMissing}",
            error,
            StringComparison.Ordinal);
        Assert.DoesNotContain(unversionedCandidate, error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.EnumerateFileSystemEntries(unversionedCandidate));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_candidateRoot));
    }

    [Fact]
    public async Task Candidate_build_inventory_refuses_an_exact_pinned_recipe_it_does_not_contain()
    {
        const string MissingRecipe = "synthetic.recipe.not-in-candidate";
        var sourceSha256 = await RewriteSourceRecipeAsync(MissingRecipe, "0.1.0");
        WritePlan(sourceSha256: sourceSha256);
        var planSha256 = Sha256(await File.ReadAllBytesAsync(_planPath));
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await ProjectUpgradeOperatorHost.RunAsync(
            [
                "prepare", "--plan", _planPath,
                "--confirm-plan-sha256", planSha256,
                "--confirm-candidate-recipes-sha256", ProjectUpgradeOperatorHost.ExecutingCandidateRecipesSha256(),
                "--confirm-candidate-recipe-contracts-sha256", ProjectUpgradeOperatorHost.ExecutingCandidateRecipeContractsSha256(),
            ],
            output,
            error,
            CancellationToken.None);

        Assert.Equal(3, exitCode);
        Assert.Empty(output.ToString());
        Assert.Contains(
            $"Code: {ProjectUpgradeFailureCodes.CandidateRecipeUnavailable}",
            error.ToString(),
            StringComparison.Ordinal);
        Assert.DoesNotContain(MissingRecipe, error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(_root, error.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_candidateRoot));
    }

    [Fact]
    public async Task Candidate_build_inventory_includes_the_persisted_big_print_recipe()
    {
        var sourceSha256 = await RewriteSourceRecipeAsync(
            DeterministicPressRecipes.BigPrint.Id,
            DeterministicPressRecipes.BigPrint.Version);
        WritePlan(sourceSha256: sourceSha256);
        var planSha256 = Sha256(await File.ReadAllBytesAsync(_planPath));
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await ProjectUpgradeOperatorHost.RunAsync(
            [
                "prepare", "--plan", _planPath,
                "--confirm-plan-sha256", planSha256,
                "--confirm-candidate-recipes-sha256", ProjectUpgradeOperatorHost.ExecutingCandidateRecipesSha256(),
                "--confirm-candidate-recipe-contracts-sha256", ProjectUpgradeOperatorHost.ExecutingCandidateRecipeContractsSha256(),
            ],
            output,
            error,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Empty(error.ToString());
        Assert.Contains("Prepared project count: 1", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(DeterministicPressRecipes.BigPrint.Id, output.ToString(), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(_candidateRoot, "prepared", "assets.ocfproj")));
    }

    [Fact]
    public void Managed_upgrade_plan_schema_tracks_the_operator_shape_and_portable_token_guards()
    {
        var repository = FindRepositoryRoot();
        using var schema = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(
            repository,
            "docs",
            "release",
            "managed-pilot-upgrade-plan.schema.json")));
        var root = schema.RootElement;
        var properties = root.GetProperty("properties");
        var projectItems = properties.GetProperty("projects").GetProperty("items");
        var projectProperties = projectItems.GetProperty("properties");

        Assert.Equal("object", root.GetProperty("type").GetString());

        Assert.False(root.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            ["schemaVersion", "sourceLibraryRoot", "candidateLibraryRoot", "targetEngineVersion", "projects"],
            root.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(
            ["candidateLibraryRoot", "projects", "schemaVersion", "sourceLibraryRoot", "targetEngineVersion"],
            properties.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(ProjectUpgradeOperatorHost.PlanSchemaVersion, properties.GetProperty("schemaVersion").GetProperty("const").GetString());
        AssertStringBounds(properties.GetProperty("sourceLibraryRoot"), 1, 1024);
        AssertStringBounds(properties.GetProperty("candidateLibraryRoot"), 1, 1024);
        AssertStringBounds(properties.GetProperty("targetEngineVersion"), 1, 64);
        Assert.Equal("array", properties.GetProperty("projects").GetProperty("type").GetString());
        Assert.Equal(1, properties.GetProperty("projects").GetProperty("minItems").GetInt32());
        Assert.Equal(512, properties.GetProperty("projects").GetProperty("maxItems").GetInt32());
        Assert.Equal("object", projectItems.GetProperty("type").GetString());
        Assert.False(projectItems.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            ["sourceRelativePath", "destinationRelativePath", "sourceEngineVersion", "sourceSchemaVersion", "sourceSha256"],
            projectItems.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(
            ["destinationRelativePath", "sourceEngineVersion", "sourceRelativePath", "sourceSchemaVersion", "sourceSha256"],
            projectProperties.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));

        AssertStringBounds(projectProperties.GetProperty("sourceRelativePath"), 1, 512);
        AssertStringBounds(projectProperties.GetProperty("destinationRelativePath"), 1, 512);
        AssertStringBounds(projectProperties.GetProperty("sourceEngineVersion"), 1, 64);
        AssertStringBounds(projectProperties.GetProperty("sourceSchemaVersion"), 1, 32);
        Assert.Equal("string", projectProperties.GetProperty("sourceSha256").GetProperty("type").GetString());
        Assert.Equal("^[0-9A-Fa-f]{64}$", projectProperties.GetProperty("sourceSha256").GetProperty("pattern").GetString());

        var targetVersionPattern = properties.GetProperty("targetEngineVersion").GetProperty("pattern").GetString();
        Assert.Equal(targetVersionPattern, projectProperties.GetProperty("sourceEngineVersion").GetProperty("pattern").GetString());
        Assert.Equal("^[0-9A-Za-z._+\\-]{1,64}$", targetVersionPattern);
        Assert.Equal("^[0-9]{1,32}$", projectProperties.GetProperty("sourceSchemaVersion").GetProperty("pattern").GetString());

        var sourcePathPattern = projectProperties.GetProperty("sourceRelativePath").GetProperty("pattern").GetString();
        Assert.Equal(sourcePathPattern, projectProperties.GetProperty("destinationRelativePath").GetProperty("pattern").GetString());
        var portablePath = new Regex(
            Assert.IsType<string>(sourcePathPattern),
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));

        foreach (var path in new[] { "asset.ocfproj", "prior/assets.OCFPROJ", "unit plans/week 1.ocfproj" })
        {
            Assert.Matches(portablePath, path);
        }

        foreach (var path in new[]
            {
                "/absolute/assets.ocfproj",
                "C:/absolute/assets.ocfproj",
                "../assets.ocfproj",
                "prior/../assets.ocfproj",
                "prior\\assets.ocfproj",
                "prior/*.ocfproj",
                "prior/assets?.ocfproj",
                "prior//assets.ocfproj",
                "prior./assets.ocfproj",
                "prior /assets.ocfproj",
                "prior/<assets>.ocfproj",
                "prior/assets.pdf",
            })
        {
            Assert.DoesNotMatch(portablePath, path);
        }

        Assert.Contains(
            "platform filesystem semantics",
            root.GetProperty("description").GetString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Console_entrypoint_wires_cooperative_cancellation_into_the_operator_host()
    {
        var repository = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            repository,
            "tools",
            "ProjectUpgradeHost",
            "Program.cs"));

        Assert.DoesNotContain("CancellationToken.None", source, StringComparison.Ordinal);
        Assert.Contains("Console.CancelKeyPress += cancelHandler;", source, StringComparison.Ordinal);
        Assert.Contains("eventArgs.Cancel = true;", source, StringComparison.Ordinal);
        Assert.Contains("cancellation.Cancel();", source, StringComparison.Ordinal);
        Assert.Contains("cancellation.Token", source, StringComparison.Ordinal);
        Assert.Contains("Console.CancelKeyPress -= cancelHandler;", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unsupported_platform_is_refused_before_command_or_plan_processing()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await ProjectUpgradeOperatorHost.RunOnPlatformAsync(
            [],
            output,
            error,
            isWindows: false,
            CancellationToken.None);

        Assert.Equal(2, exitCode);
        Assert.Empty(output.ToString());
        Assert.Contains(
            $"Code: {OperatorUpgradeFailureCodes.PlatformUnsupported}",
            error.ToString(),
            StringComparison.Ordinal);
        Assert.DoesNotContain(OperatorUpgradeFailureCodes.UsageInvalid, error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Supplied_cancellation_is_content_free_and_prepares_nothing()
    {
        WritePlan();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await ProjectUpgradeOperatorHost.RunAsync(
            ["review", "--plan", _planPath],
            output,
            error,
            cancellation.Token);

        Assert.Equal(3, exitCode);
        Assert.Empty(output.ToString());
        Assert.Contains(
            $"Code: {ProjectUpgradeFailureCodes.Canceled}",
            error.ToString(),
            StringComparison.Ordinal);
        Assert.DoesNotContain(_root, error.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_candidateRoot));
    }

    [Fact]
    public async Task Real_console_ctrl_c_cancels_the_process_and_cleans_the_synthetic_batch()
    {
        if (!OperatingSystem.IsWindows())
        {
            var output = new StringWriter();
            var error = new StringWriter();
            var exitCode = await ProjectUpgradeOperatorHost.RunOnPlatformAsync(
                [],
                output,
                error,
                isWindows: false,
                CancellationToken.None);

            Assert.Equal(2, exitCode);
            Assert.Contains(
                $"Code: {OperatorUpgradeFailureCodes.PlatformUnsupported}",
                error.ToString(),
                StringComparison.Ordinal);
            return;
        }

        const int ProjectCount = 512;
        var projects = new List<object>(ProjectCount)
        {
            new
            {
                sourceRelativePath = RelativeSource,
                destinationRelativePath = "prepared/000.ocfproj",
                sourceEngineVersion = PriorEngineVersion,
                sourceSchemaVersion = "1",
                sourceSha256 = FixtureSha256,
            },
        };
        var bulkSourceRoot = Path.Combine(_sourceRoot, "bulk");
        Directory.CreateDirectory(bulkSourceRoot);
        for (var index = 1; index < ProjectCount; index++)
        {
            var fileName = $"{index.ToString("D3", CultureInfo.InvariantCulture)}.ocfproj";
            File.WriteAllBytes(Path.Combine(bulkSourceRoot, fileName), _fixture);
            projects.Add(new
            {
                sourceRelativePath = $"bulk/{fileName}",
                destinationRelativePath = $"prepared/{fileName}",
                sourceEngineVersion = PriorEngineVersion,
                sourceSchemaVersion = "1",
                sourceSha256 = FixtureSha256,
            });
        }

        var plan = new
        {
            schemaVersion = ProjectUpgradeOperatorHost.PlanSchemaVersion,
            sourceLibraryRoot = _sourceRoot,
            candidateLibraryRoot = _candidateRoot,
            targetEngineVersion = EngineIdentity.EngineVersion,
            projects,
        };
        File.WriteAllText(
            _planPath,
            JsonSerializer.Serialize(plan),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var planSha256 = Sha256(await File.ReadAllBytesAsync(_planPath));
        var outputPath = Path.Combine(_root, "console-process.stdout.txt");
        var errorPath = Path.Combine(_root, "console-process.stderr.txt");
        var inheritanceProbePath = Path.Combine(_root, "must-not-be-inherited.probe");
        var hostPath = Path.Combine(AppContext.BaseDirectory, "Foundry.Tools.ProjectUpgradeHost.exe");
        var senderPath = Path.Combine(AppContext.BaseDirectory, "Foundry.Tools.ConsoleControlSignalSender.exe");
        Assert.True(File.Exists(hostPath), "The referenced operator host executable was not copied to the test output.");
        Assert.True(File.Exists(senderPath), "The console-control sender executable was not copied to the test output.");

        using var inheritanceProbe = WindowsConsoleProcess.CreateInheritanceProbe(inheritanceProbePath);
        using var host = WindowsConsoleProcess.Start(
            hostPath,
            [
                "prepare", "--plan", _planPath,
                "--confirm-plan-sha256", planSha256,
                "--confirm-candidate-recipes-sha256", ProjectUpgradeOperatorHost.ExecutingCandidateRecipesSha256(),
                "--confirm-candidate-recipe-contracts-sha256", ProjectUpgradeOperatorHost.ExecutingCandidateRecipeContractsSha256(),
            ],
            outputPath,
            errorPath,
            FindRepositoryRoot());

        var batchLockPath = Path.Combine(_candidateRoot, ".ocf-upgrade-batch.lock");
        Assert.False(host.HasExited, "The host exited before the inheritance-isolation probe.");
        inheritanceProbe.Dispose();
        File.Delete(inheritanceProbePath);
        Assert.False(
            File.Exists(inheritanceProbePath),
            "The child retained an inheritable handle outside its exact standard-stream handle list.");
        Assert.False(host.HasExited, "The host exited during the inheritance-isolation probe.");

        var senderStart = new ProcessStartInfo
        {
            FileName = senderPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        senderStart.ArgumentList.Add(host.ProcessId.ToString(CultureInfo.InvariantCulture));
        senderStart.ArgumentList.Add(batchLockPath);
        using var sender = Assert.IsType<Process>(Process.Start(senderStart));
        using var senderTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            await sender.WaitForExitAsync(senderTimeout.Token);
        }
        catch (OperationCanceledException)
        {
            await KillAndObserveExitAsync(sender, TimeSpan.FromSeconds(5));
            throw new TimeoutException("The bounded console-control sender did not exit.");
        }

        var senderOutput = await sender.StandardOutput.ReadToEndAsync();
        var senderError = await sender.StandardError.ReadToEndAsync();
        Assert.Empty(senderOutput);
        Assert.Empty(senderError);
        Assert.Equal(0, sender.ExitCode);
        Assert.True(host.Wait(TimeSpan.FromSeconds(15)), "The cooperatively canceled operator host did not exit within 15 seconds.");

        var processOutput = await File.ReadAllTextAsync(outputPath);
        var processError = await File.ReadAllTextAsync(errorPath);
        Assert.Empty(processOutput);
        Assert.Contains($"Code: {ProjectUpgradeFailureCodes.Canceled}", processError, StringComparison.Ordinal);
        Assert.DoesNotContain(_root, processError, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(RelativeSource, processError, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Synthetic asset compatibility fixture", processError, StringComparison.Ordinal);
        Assert.Equal(3, host.ExitCode);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_candidateRoot));

        var sourcePackages = Directory
            .EnumerateFiles(_sourceRoot, "*.ocfproj", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(ProjectCount, sourcePackages.Length);
        Assert.All(
            sourcePackages,
            sourcePackage => Assert.Equal(FixtureSha256, Sha256(File.ReadAllBytes(sourcePackage))));
        _testOutput.WriteLine(
            $"upgrade-console-cancel: signal=CTRL_C_EVENT; lockObservation=exclusive-sharing-violation; inheritanceProbeDeletedWhileHostAlive=true; senderExit=0; hostExit=3; refusal={ProjectUpgradeFailureCodes.Canceled}; candidateEntries=0; sourceCount={ProjectCount}; sourceSha256={FixtureSha256}");
    }

    [Fact]
    public void Executing_candidate_inventory_covers_every_compiled_recipe_catalog()
    {
        var expected = DiscoverDeclaredRecipeContracts()
            .Select(recipe => new ProjectUpgradeRecipeIdentity(recipe.RecipeId, recipe.RecipeVersion))
            .Append(new ProjectUpgradeRecipeIdentity(
                PortableProjectIdentity.RecipeId,
                PortableProjectIdentity.RecipeVersion))
            .Distinct()
            .OrderBy(recipe => recipe.RecipeId, StringComparer.Ordinal)
            .ThenBy(recipe => recipe.RecipeVersion, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, ProjectUpgradeOperatorHost.ExecutingCandidateRecipes());
        Assert.Contains(
            new ProjectUpgradeRecipeIdentity(
                DeterministicPressRecipes.BigPrint.Id,
                DeterministicPressRecipes.BigPrint.Version),
            expected);
    }

    [Fact]
    public void Executing_candidate_contract_inventory_fingerprints_every_manifest_backed_recipe()
    {
        var expected = DiscoverDeclaredRecipeContracts()
            .Append(new ProjectUpgradeRecipeContract(
                PortableProjectIdentity.RecipeId,
                PortableProjectIdentity.RecipeVersion,
                ManifestSha256: null))
            .Distinct()
            .OrderBy(recipe => recipe.RecipeId, StringComparer.Ordinal)
            .ThenBy(recipe => recipe.RecipeVersion, StringComparer.Ordinal)
            .ToArray();

        var actual = ProjectUpgradeOperatorHost.ExecutingCandidateRecipeContracts();

        Assert.Equal(expected, actual);
        Assert.Equal(ProjectUpgradeOperatorHost.ExecutingCandidateRecipes().Count, actual.Count);
        Assert.All(
            actual.Where(contract => contract.RecipeId != PortableProjectIdentity.RecipeId),
            contract => Assert.Matches("^[0-9A-F]{64}$", contract.ManifestSha256));
        var identityOnly = Assert.Single(actual, contract => contract.ManifestSha256 is null);
        Assert.Equal(PortableProjectIdentity.RecipeId, identityOnly.RecipeId);
        Assert.Matches("^[0-9A-F]{64}$", ProjectUpgradeOperatorHost.ExecutingCandidateRecipeContractsSha256());
    }

    private static IReadOnlyList<ProjectUpgradeRecipeContract> DiscoverDeclaredRecipeContracts()
    {
        var manifests = new List<RecipeManifest>();
        var assemblies = new[]
        {
            typeof(ModuleStudioCatalog).Assembly,
            typeof(DeterministicPressRecipes).Assembly,
        };

        foreach (var type in assemblies.SelectMany(assembly => assembly.GetTypes()))
        {
            foreach (var property in type.GetProperties(
                         BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (IsManifestCarrier(property.PropertyType))
                {
                    Collect(property.PropertyType, property.GetValue(null));
                }
            }

            foreach (var field in type.GetFields(
                         BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (IsManifestCarrier(field.FieldType))
                {
                    Collect(field.FieldType, field.GetValue(null));
                }
            }
        }

        return [.. manifests
            .Select(recipe => new ProjectUpgradeRecipeContract(
                recipe.Id,
                recipe.Version,
                RecipeContractFingerprint.ComputeSha256(recipe)))
            .Distinct()
            .OrderBy(recipe => recipe.RecipeId, StringComparer.Ordinal)
            .ThenBy(recipe => recipe.RecipeVersion, StringComparer.Ordinal)];

        void Collect(Type declaredType, object? value)
        {
            if (declaredType == typeof(RecipeManifest) && value is RecipeManifest manifest)
            {
                manifests.Add(manifest);
            }
            else if (typeof(IEnumerable<RecipeManifest>).IsAssignableFrom(declaredType)
                     && value is IEnumerable<RecipeManifest> sequence)
            {
                manifests.AddRange(sequence);
            }
        }

        static bool IsManifestCarrier(Type declaredType)
        {
            return declaredType == typeof(RecipeManifest)
                        || typeof(IEnumerable<RecipeManifest>).IsAssignableFrom(declaredType);
        }
    }

    [Fact]
    public void Candidate_recipe_contract_inventory_digest_is_sorted_and_length_framed()
    {
        var contract = new string('A', 64);
        var first = new[]
        {
            new ProjectUpgradeRecipeContract("ab", "c", contract),
            new ProjectUpgradeRecipeContract("second", "1", ManifestSha256: null),
        };
        var reversed = first.Reverse().ToArray();
        var regrouped = new[]
        {
            new ProjectUpgradeRecipeContract("a", "bc", contract),
            new ProjectUpgradeRecipeContract("second", "1", ManifestSha256: null),
        };

        Assert.Equal(
            "candidate-recipe-contract-inventory.v2",
            ProjectUpgradeOperatorHost.CandidateRecipeContractInventoryFramingVersion);
        Assert.Equal("recipe-contract-fingerprint.v2", RecipeContractFingerprint.FramingVersion);
        Assert.Equal(
            "6A844C46D70D16866C0234B84B3518E58CF6FE8D172CAA550A2055518635EA66",
            ProjectUpgradeOperatorHost.CandidateRecipeContractsSha256(first));
        Assert.Equal(
            ProjectUpgradeOperatorHost.CandidateRecipeContractsSha256(first),
            ProjectUpgradeOperatorHost.CandidateRecipeContractsSha256(reversed));
        Assert.NotEqual(
            ProjectUpgradeOperatorHost.CandidateRecipeContractsSha256(first),
            ProjectUpgradeOperatorHost.CandidateRecipeContractsSha256(regrouped));
    }

    [Fact]
    public void Candidate_recipe_identity_inventory_digest_is_versioned_sorted_and_length_framed()
    {
        var first = new[]
        {
            new ProjectUpgradeRecipeIdentity("ab", "c"),
            new ProjectUpgradeRecipeIdentity("second", "1"),
        };
        var reversed = first.Reverse().ToArray();
        var regrouped = new[]
        {
            new ProjectUpgradeRecipeIdentity("a", "bc"),
            new ProjectUpgradeRecipeIdentity("second", "1"),
        };

        Assert.Equal(
            "candidate-recipe-identity-inventory.v1",
            ProjectUpgradeOperatorHost.CandidateRecipeIdentityInventoryFramingVersion);
        Assert.Equal(
            "067538B22611516220E7DEDC8D8685B3ADC501BE02909CD27AB6396E5784A826",
            ProjectUpgradeOperatorHost.CandidateRecipesSha256(first));
        Assert.Equal(
            ProjectUpgradeOperatorHost.CandidateRecipesSha256(first),
            ProjectUpgradeOperatorHost.CandidateRecipesSha256(reversed));
        Assert.NotEqual(
            ProjectUpgradeOperatorHost.CandidateRecipesSha256(first),
            ProjectUpgradeOperatorHost.CandidateRecipesSha256(regrouped));
        Assert.Single(ProjectUpgradeOperatorHost.NormalizeCandidateRecipes([first[0], first[0]]));
        Assert.Throws<InvalidOperationException>(() =>
            ProjectUpgradeOperatorHost.NormalizeCandidateRecipes(
                [first[0] with { RecipeId = " " }]));
        Assert.Throws<InvalidOperationException>(() =>
            ProjectUpgradeOperatorHost.NormalizeCandidateRecipes(
                [first[0] with { RecipeVersion = "not a version" }]));
    }

    [Fact]
    public void Candidate_recipe_contract_inventory_refuses_one_identity_with_different_contracts()
    {
        var duplicate = new ProjectUpgradeRecipeContract("synthetic.recipe", "1.0.0", new string('a', 64));
        var normalized = ProjectUpgradeOperatorHost.NormalizeCandidateRecipeContracts([duplicate, duplicate]);

        Assert.Single(normalized);
        Assert.Equal(new string('A', 64), normalized[0].ManifestSha256);
        Assert.Throws<InvalidOperationException>(() =>
            ProjectUpgradeOperatorHost.NormalizeCandidateRecipeContracts(
            [
                duplicate,
                duplicate with { ManifestSha256 = new string('B', 64) },
            ]));
        Assert.Throws<InvalidOperationException>(() =>
            ProjectUpgradeOperatorHost.NormalizeCandidateRecipeContracts(
                [duplicate with { RecipeId = " " }]));
        Assert.Throws<InvalidOperationException>(() =>
            ProjectUpgradeOperatorHost.NormalizeCandidateRecipeContracts(
                [duplicate with { ManifestSha256 = "not-a-sha-256" }]));
    }

    [Fact]
    public void Compatibility_service_and_requests_are_internal_to_the_operator_boundary()
    {
        Assert.False(typeof(OcfprojUpgradeService).IsVisible);
        Assert.False(typeof(ProjectUpgradeItem).IsVisible);
        Assert.False(typeof(ProjectUpgradeRecipeIdentity).IsVisible);
        Assert.False(typeof(ProjectUpgradeBatchRequest).IsVisible);
        Assert.False(typeof(ProjectUpgradeRequest).IsVisible);
        Assert.False(typeof(ProjectUpgradeReceipt).IsVisible);
        Assert.False(typeof(ProjectUpgradeBatchReceipt).IsVisible);

        var friends = typeof(OcfprojUpgradeService).Assembly
            .GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(attribute => attribute.AssemblyName.Split(',')[0])
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            ["Foundry.Tests.Integration", "Foundry.Tools.ProjectUpgradeHost"],
            friends);
    }

    [Fact]
    public async Task Plan_sha_confirmation_must_bind_the_exact_file_bytes()
    {
        WritePlan();
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await ProjectUpgradeOperatorHost.RunAsync(
            [
                "prepare", "--plan", _planPath,
                "--confirm-plan-sha256", new string('0', 64),
                "--confirm-candidate-recipes-sha256", ProjectUpgradeOperatorHost.ExecutingCandidateRecipesSha256(),
                "--confirm-candidate-recipe-contracts-sha256", ProjectUpgradeOperatorHost.ExecutingCandidateRecipeContractsSha256(),
            ],
            output,
            error,
            CancellationToken.None);

        Assert.Equal(2, exitCode);
        Assert.Empty(output.ToString());
        Assert.Contains($"Code: {OperatorUpgradeFailureCodes.PlanNotConfirmed}", error.ToString(), StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_candidateRoot));
    }

    [Fact]
    public async Task Candidate_recipe_inventory_confirmation_must_bind_the_reviewed_executing_catalogs()
    {
        WritePlan();
        var planSha256 = Sha256(await File.ReadAllBytesAsync(_planPath));
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await ProjectUpgradeOperatorHost.RunAsync(
            [
                "prepare", "--plan", _planPath,
                "--confirm-plan-sha256", planSha256,
                "--confirm-candidate-recipes-sha256", new string('0', 64),
                "--confirm-candidate-recipe-contracts-sha256", ProjectUpgradeOperatorHost.ExecutingCandidateRecipeContractsSha256(),
            ],
            output,
            error,
            CancellationToken.None);

        Assert.Equal(2, exitCode);
        Assert.Empty(output.ToString());
        Assert.Contains(
            $"Code: {OperatorUpgradeFailureCodes.CandidateRecipesNotConfirmed}",
            error.ToString(),
            StringComparison.Ordinal);
        Assert.DoesNotContain(_root, error.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_candidateRoot));
    }

    [Fact]
    public async Task Candidate_recipe_contract_confirmation_must_bind_the_reviewed_declarative_manifests()
    {
        WritePlan();
        var planSha256 = Sha256(await File.ReadAllBytesAsync(_planPath));
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await ProjectUpgradeOperatorHost.RunAsync(
            [
                "prepare", "--plan", _planPath,
                "--confirm-plan-sha256", planSha256,
                "--confirm-candidate-recipes-sha256", ProjectUpgradeOperatorHost.ExecutingCandidateRecipesSha256(),
                "--confirm-candidate-recipe-contracts-sha256", new string('0', 64),
            ],
            output,
            error,
            CancellationToken.None);

        Assert.Equal(2, exitCode);
        Assert.Empty(output.ToString());
        Assert.Contains(
            $"Code: {OperatorUpgradeFailureCodes.CandidateRecipeContractsNotConfirmed}",
            error.ToString(),
            StringComparison.Ordinal);
        Assert.DoesNotContain(_root, error.ToString(), StringComparison.OrdinalIgnoreCase);
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
        string? candidateRoot = null,
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
              "candidateLibraryRoot": {{JsonSerializer.Serialize(candidateRoot ?? _candidateRoot)}},
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

    private static async Task KillAndObserveExitAsync(Process process, TimeSpan timeout)
    {
        if (!process.HasExited)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException) when (process.HasExited)
            {
                // The process crossed the exit boundary after the liveness
                // observation; the bounded wait below still proves settlement.
            }
        }

        using var forcedExitTimeout = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(forcedExitTimeout.Token);
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException("The console-control sender did not settle after forced termination.");
        }
    }

    private static void AssertStringBounds(JsonElement property, int minimumLength, int maximumLength)
    {
        Assert.Equal("string", property.GetProperty("type").GetString());
        Assert.Equal(minimumLength, property.GetProperty("minLength").GetInt32());
        Assert.Equal(maximumLength, property.GetProperty("maxLength").GetInt32());
    }

    private async Task<string> RewriteSourceRecipeAsync(string recipeId, string recipeVersion)
    {
        var sourcePath = Path.Combine(
            _sourceRoot,
            RelativeSource.Replace('/', Path.DirectorySeparatorChar));
        using (var archive = ZipFile.Open(sourcePath, ZipArchiveMode.Update))
        {
            var manifestEntry = Assert.IsType<ZipArchiveEntry>(archive.GetEntry("manifest.json"));
            string json;
            using (var reader = new StreamReader(manifestEntry.Open(), Encoding.UTF8))
            {
                json = reader.ReadToEnd();
            }

            var stamp = manifestEntry.LastWriteTime;
            var manifest = Assert.IsType<JsonObject>(JsonNode.Parse(json));
            manifest["recipeId"] = recipeId;
            manifest["recipeVersion"] = recipeVersion;
            manifestEntry.Delete();
            var replacement = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
            replacement.LastWriteTime = stamp;
            using var writer = new StreamWriter(
                replacement.Open(),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            writer.Write(manifest.ToJsonString());
        }

        return Sha256(await File.ReadAllBytesAsync(sourcePath));
    }
}
