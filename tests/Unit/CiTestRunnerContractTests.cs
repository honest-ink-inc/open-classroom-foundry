// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;
using System.Text.Json;

namespace Foundry.Tests.Unit;

public sealed class CiTestRunnerContractTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string RunnerPath = Path.Combine(
        RepositoryRoot,
        "tools",
        "run-ci-tests.ps1");
    private static readonly string EvidenceModulePath = Path.Combine(
        RepositoryRoot,
        "tools",
        "CiTestEvidence.psm1");
    private static readonly string WorkflowPath = Path.Combine(
        RepositoryRoot,
        ".github",
        "workflows",
        "ci.yml");

    [Fact]
    public void Runner_preserves_one_exact_solution_wide_test_process_and_its_evidence_switches()
    {
        var runner = File.ReadAllText(RunnerPath);

        Assert.Equal(1, CountOccurrences(runner, "$process.Start()"));
        Assert.Contains("[Diagnostics.ProcessStartInfo]::new()", runner, StringComparison.Ordinal);
        Assert.Contains("$startInfo.ArgumentList.Add($argument)", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("Start-Process", runner, StringComparison.Ordinal);
        Assert.Contains("\"test\"", runner, StringComparison.Ordinal);
        Assert.Contains("\"OpenClassroomFoundry.slnx\"", runner, StringComparison.Ordinal);
        Assert.Contains("\"--no-build\"", runner, StringComparison.Ordinal);
        Assert.Contains("\"--configuration\", \"Release\"", runner, StringComparison.Ordinal);
        Assert.Contains("\"console;verbosity=normal\"", runner, StringComparison.Ordinal);
        Assert.Contains("$trxFileName = \"test-results-$runId.trx\"", runner, StringComparison.Ordinal);
        Assert.Contains("\"trx;LogFileName=$trxFileName\"", runner, StringComparison.Ordinal);
        Assert.Contains("\"XPlat Code Coverage\"", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("--filter", runner, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--blame", runner, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("foreach ($project", runner, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Runner_has_a_bounded_fail_closed_lifecycle_and_durable_streams()
    {
        var runner = File.ReadAllText(RunnerPath);

        Assert.Contains("[ValidateRange(60, 7200)]", runner, StringComparison.Ordinal);
        Assert.Contains("[int]$ProcessLimitSeconds = 900", runner, StringComparison.Ordinal);
        Assert.Contains("Wait-CiProcessExit", runner, StringComparison.Ordinal);
        Assert.Contains("[long]250", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("$process.WaitForExit($ProcessLimitSeconds * 1000)", runner, StringComparison.Ordinal);
        Assert.Contains("taskkill.exe", runner, StringComparison.Ordinal);
        Assert.Contains("Invoke-CiBoundedTaskKill", runner, StringComparison.Ordinal);
        Assert.Contains("$taskkillResult.ExitCode -eq 0", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("ReadToEndAsync", runner, StringComparison.Ordinal);
        var taskkillStart = runner.IndexOf("function Invoke-CiBoundedTaskKill", StringComparison.Ordinal);
        var taskkillEnd = runner.IndexOf("function Complete-CiStreamCapture", taskkillStart, StringComparison.Ordinal);
        var taskkill = runner[taskkillStart..taskkillEnd];
        Assert.DoesNotContain("RedirectStandardOutput", taskkill, StringComparison.Ordinal);
        Assert.DoesNotContain("RedirectStandardError", taskkill, StringComparison.Ordinal);
        var taskkillClock = taskkill.IndexOf(
            "$killClock = [Diagnostics.Stopwatch]::StartNew()",
            StringComparison.Ordinal);
        var taskkillProcessStart = taskkill.IndexOf("$killProcess.Start()", StringComparison.Ordinal);
        var taskkillRemaining = taskkill.IndexOf(
            "$remainingKillMilliseconds = [long]$LimitMilliseconds - $killClock.ElapsedMilliseconds",
            StringComparison.Ordinal);
        var taskkillExpiredGuard = taskkill.IndexOf(
            "if ($remainingKillMilliseconds -lt 0)",
            taskkillRemaining,
            StringComparison.Ordinal);
        var taskkillWait = taskkill.IndexOf(
            "-LimitMilliseconds $remainingKillMilliseconds",
            StringComparison.Ordinal);
        Assert.True(
            taskkillClock < taskkillProcessStart
            && taskkillProcessStart < taskkillRemaining
            && taskkillRemaining < taskkillExpiredGuard
            && taskkillExpiredGuard < taskkillWait);
        Assert.Contains(".active-or-stranded.json", runner, StringComparison.Ordinal);
        Assert.Contains("Update-CiJsonFileAtomically", runner, StringComparison.Ordinal);
        Assert.Contains("State = \"preflight\"", runner, StringComparison.Ordinal);
        Assert.Contains("$activeMarker[\"State\"] = \"starting\"", runner, StringComparison.Ordinal);
        Assert.Contains("$activeMarker[\"State\"] = \"running\"", runner, StringComparison.Ordinal);
        Assert.Contains("$activeMarker[\"State\"] = \"stranded\"", runner, StringComparison.Ordinal);
        Assert.Contains("$activeMarker[\"ChildProcessId\"]", runner, StringComparison.Ordinal);
        Assert.Contains("$processStarted = $process.Start()", runner, StringComparison.Ordinal);
        Assert.Contains("SafeToStartAnotherRunner = $safeToStartAnotherRunner", runner, StringComparison.Ordinal);
        Assert.Contains("SourceToBinaryProvenance", runner, StringComparison.Ordinal);
        Assert.Contains("stability is established only when post-state exists", runner, StringComparison.Ordinal);
        Assert.Contains("BaseStream.CopyToAsync", runner, StringComparison.Ordinal);
        Assert.Contains("[IO.FileMode]::CreateNew", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("$process.StandardOutput.ReadToEndAsync()", runner, StringComparison.Ordinal);
        Assert.Contains("summary.json", runner, StringComparison.Ordinal);
        Assert.Contains("test.stdout.log", runner, StringComparison.Ordinal);
        Assert.Contains("test.stderr.log", runner, StringComparison.Ordinal);

        var streamDrainStart = runner.IndexOf(
            "function Complete-CiStreamCapture",
            StringComparison.Ordinal);
        var streamDrainEnd = runner.IndexOf(
            "function Copy-CiFileToConsole",
            streamDrainStart,
            StringComparison.Ordinal);
        var streamDrain = runner[streamDrainStart..streamDrainEnd];
        var remainingTime = streamDrain.IndexOf(
            "$remaining = $LimitMilliseconds - $drainClock.ElapsedMilliseconds",
            StringComparison.Ordinal);
        var expiredGuard = streamDrain.IndexOf(
            "if ($remaining -le 0)",
            remainingTime,
            StringComparison.Ordinal);
        var boundedWait = streamDrain.IndexOf(
            "$combined.Wait",
            expiredGuard,
            StringComparison.Ordinal);
        Assert.True(remainingTime < expiredGuard && expiredGuard < boundedWait);

        var markerObject = runner.IndexOf("$activeMarker = [ordered]@{", StringComparison.Ordinal);
        var preflightState = runner.IndexOf("State = \"preflight\"", markerObject, StringComparison.Ordinal);
        var marker = runner.IndexOf("Write-CiJsonFile `", markerObject, StringComparison.Ordinal);
        var markerCreated = runner.IndexOf("$activeMarkerCreated = $true", marker, StringComparison.Ordinal);
        var repositorySnapshot = runner.IndexOf(
            "$repositoryStateBefore = Get-CiRepositoryState",
            markerCreated,
            StringComparison.Ordinal);
        var sdkSnapshot = runner.IndexOf(
            "$dotnetSdk = (& dotnet --version",
            repositorySnapshot,
            StringComparison.Ordinal);
        var suiteInventory = runner.IndexOf(
            "$testSuites = @(Get-CiTestSuiteInventory",
            sdkSnapshot,
            StringComparison.Ordinal);
        var assemblySnapshot = runner.IndexOf(
            "$testAssembliesBefore = @(Get-CiTestAssemblySnapshot",
            suiteInventory,
            StringComparison.Ordinal);
        var evidenceBaseline = runner.IndexOf(
            "$evidenceBaseline = @(Get-CiTestEvidenceBaseline",
            assemblySnapshot,
            StringComparison.Ordinal);
        var startingState = runner.IndexOf(
            "$activeMarker[\"State\"] = \"starting\"",
            evidenceBaseline,
            StringComparison.Ordinal);
        var processClock = runner.IndexOf(
            "$clock = [Diagnostics.Stopwatch]::StartNew()",
            StringComparison.Ordinal);
        var process = runner.IndexOf("$process.Start()", StringComparison.Ordinal);
        var runningState = runner.IndexOf(
            "$activeMarker[\"State\"] = \"running\"",
            process,
            StringComparison.Ordinal);
        var processRemaining = runner.IndexOf(
            "$remainingProcessMilliseconds = ([long]$ProcessLimitSeconds * 1000) - $clock.ElapsedMilliseconds",
            process,
            StringComparison.Ordinal);
        var processExpiredGuard = runner.IndexOf(
            "if ($remainingProcessMilliseconds -lt 0)",
            processRemaining,
            StringComparison.Ordinal);
        var parentWait = runner.IndexOf(
            "-LimitMilliseconds $remainingProcessMilliseconds",
            processRemaining,
            StringComparison.Ordinal);
        var receipt = runner.IndexOf("$receipt = [ordered]@{", StringComparison.Ordinal);
        var cleanup = runner.IndexOf("finally {", receipt, StringComparison.Ordinal);
        Assert.DoesNotContain(
            -1,
            new[]
            {
                markerObject,
                preflightState,
                marker,
                markerCreated,
                repositorySnapshot,
                sdkSnapshot,
                suiteInventory,
                assemblySnapshot,
                evidenceBaseline,
                startingState,
                processClock,
                process,
                runningState,
                processRemaining,
                processExpiredGuard,
                parentWait,
                receipt,
                cleanup,
            });
        Assert.True(
            markerObject < preflightState
            && preflightState < marker
            && marker < markerCreated
            && markerCreated < repositorySnapshot
            && repositorySnapshot < sdkSnapshot
            && sdkSnapshot < suiteInventory
            && suiteInventory < assemblySnapshot
            && assemblySnapshot < evidenceBaseline
            && evidenceBaseline < startingState
            && startingState < processClock
            && processClock < process
            && process < runningState
            && process < processRemaining
            && processRemaining < processExpiredGuard
            && processExpiredGuard < parentWait
            && parentWait < receipt
            && receipt < cleanup);

        var markerCleanup = runner.IndexOf(
            "if ($activeMarkerCreated -and -not $activeMarkerCleared)",
            cleanup,
            StringComparison.Ordinal);
        var preStartBranch = runner.IndexOf(
            "if (-not $processStarted)",
            markerCleanup,
            StringComparison.Ordinal);
        var preStartDelete = runner.IndexOf(
            "[IO.File]::Delete($activeMarkerPath)",
            preStartBranch,
            StringComparison.Ordinal);
        var strandedBranch = runner.IndexOf(
            "$activeMarker[\"State\"] = \"stranded\"",
            preStartDelete,
            StringComparison.Ordinal);
        Assert.DoesNotContain(-1, new[] { markerCleanup, preStartBranch, preStartDelete, strandedBranch });
        Assert.True(
            markerCleanup < preStartBranch
            && preStartBranch < preStartDelete
            && preStartDelete < strandedBranch);
    }

    [Fact]
    public void Workflow_uses_the_bounded_runner_and_immediately_retains_partial_evidence()
    {
        var workflowLines = File.ReadAllLines(WorkflowPath);
        var activeWorkflow = string.Join(
            Environment.NewLine,
            workflowLines.Where(line => !line.TrimStart().StartsWith('#')));
        var testStep = GetNamedWorkflowStep(
            workflowLines,
            "Test with coverage (bounded outer process)");
        var evidenceStep = GetNamedWorkflowStep(workflowLines, "Retain bounded test evidence");
        var rawStep = GetNamedWorkflowStep(
            workflowLines,
            "Retain raw TestResults failure diagnostics");
        var determinismStep = GetNamedWorkflowStep(
            workflowLines,
            "Determinism gate (SampleGenerator twice, every byte hash-compared)");
        var coverageStep = GetNamedWorkflowStep(
            workflowLines,
            "Coverage threshold (core + module assemblies >= 80%)");

        Assert.Equal(evidenceStep.StartIndex, GetNextWorkflowStepStart(workflowLines, testStep));
        Assert.Equal(rawStep.StartIndex, GetNextWorkflowStepStart(workflowLines, evidenceStep));
        Assert.Equal(determinismStep.StartIndex, GetNextWorkflowStepStart(workflowLines, rawStep));

        Assert.Contains("timeout-minutes: 20", testStep.Text, StringComparison.Ordinal);
        Assert.Contains(
            "run: pwsh -NoProfile -File tools/run-ci-tests.ps1",
            testStep.Text,
            StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(activeWorkflow, "tools/run-ci-tests.ps1"));
        var activeTokens = activeWorkflow.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.DoesNotContain(
            Enumerable.Range(0, Math.Max(0, activeTokens.Length - 1)),
            index => string.Equals(activeTokens[index], "dotnet", StringComparison.OrdinalIgnoreCase)
                && string.Equals(activeTokens[index + 1], "test", StringComparison.OrdinalIgnoreCase));

        Assert.Contains("if: always()", evidenceStep.Text, StringComparison.Ordinal);
        Assert.Contains(
            "uses: actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a",
            evidenceStep.Text,
            StringComparison.Ordinal);
        Assert.Contains("name: bounded-test-evidence", evidenceStep.Text, StringComparison.Ordinal);
        Assert.Contains("path: out/ci-test-run/**", evidenceStep.Text, StringComparison.Ordinal);
        Assert.Contains("include-hidden-files: true", evidenceStep.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("TestResults", evidenceStep.Text, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("if: failure()", rawStep.Text, StringComparison.Ordinal);
        Assert.Contains(
            "uses: actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a",
            rawStep.Text,
            StringComparison.Ordinal);
        Assert.Contains("name: raw-testresults-failure-diagnostics", rawStep.Text, StringComparison.Ordinal);
        Assert.Contains("**/TestResults/*.trx", rawStep.Text, StringComparison.Ordinal);
        Assert.Contains("**/TestResults/**/coverage.cobertura.xml", rawStep.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("out/ci-test-run", rawStep.Text, StringComparison.Ordinal);

        Assert.Contains(
            "-reports:\"out/ci-test-run/*/coverage/*.cobertura.xml\"",
            coverageStep.Text,
            StringComparison.Ordinal);
        Assert.DoesNotContain("TestResults", coverageStep.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, CountOccurrences(activeWorkflow, "**/TestResults/*.trx"));
        Assert.Equal(1, CountOccurrences(activeWorkflow, "**/TestResults/**/coverage.cobertura.xml"));
    }

    [Fact]
    public void Runner_snapshots_only_solution_inventoried_current_test_evidence()
    {
        var runner = File.ReadAllText(RunnerPath);
        var evidenceModule = File.ReadAllText(EvidenceModulePath);

        Assert.Contains("Import-Module -Name $evidenceModulePath -Force", runner, StringComparison.Ordinal);
        Assert.Contains("Get-CiTestSuiteInventory", runner, StringComparison.Ordinal);
        Assert.Contains("-RepositoryRoot $repositoryRoot", runner, StringComparison.Ordinal);
        Assert.Contains("Get-CiTestEvidenceBaseline -Suites $testSuites", runner, StringComparison.Ordinal);
        Assert.Contains("Get-CiTestEvidenceDelta -Suites $testSuites", runner, StringComparison.Ordinal);
        Assert.Contains("Get-CiTestEvidenceCompletenessErrors", runner, StringComparison.Ordinal);
        Assert.Contains("[IO.FileShare]::None", runner, StringComparison.Ordinal);
        Assert.Contains("BoundRunId = $runId", runner, StringComparison.Ordinal);
        Assert.Contains("-TrxFileName $trxFileName", runner, StringComparison.Ordinal);
        Assert.Contains("$trxEvidenceRoot = Join-Path $evidenceRoot \"trx\"", runner, StringComparison.Ordinal);
        Assert.Contains("$coverageEvidenceRoot = Join-Path $evidenceRoot \"coverage\"", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("GetLastWriteTimeUtc", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("Get-ChildItem -LiteralPath $testsRoot -Directory", runner, StringComparison.Ordinal);
        Assert.Contains("Copy-CiEvidenceFile", runner, StringComparison.Ordinal);
        Assert.Contains("ExpectedTestProjects = @($testSuites.ProjectPath)", runner, StringComparison.Ordinal);
        Assert.Contains("TrxEvidenceFiles = $trxEvidenceFiles", runner, StringComparison.Ordinal);
        Assert.Contains("CoverageEvidenceFiles = $coverageEvidenceFiles", runner, StringComparison.Ordinal);
        Assert.Contains("EvidenceCopies = $evidenceCopies", runner, StringComparison.Ordinal);
        Assert.Contains("RepositoryStateBefore = $repositoryStateBefore", runner, StringComparison.Ordinal);
        Assert.Contains("TestAssembliesBefore = $testAssembliesBefore", runner, StringComparison.Ordinal);
        Assert.Contains("TestAssembliesAfter = $testAssembliesAfter", runner, StringComparison.Ordinal);
        Assert.Contains("SourceContentSha256", runner, StringComparison.Ordinal);
        Assert.Contains("SourceSha256 = $trxCopy.SourceSha256", runner, StringComparison.Ordinal);
        Assert.Contains("CopiedSha256 = $trxCopy.CopiedSha256", runner, StringComparison.Ordinal);
        Assert.Contains("CopyMatchesSource = $trxCopy.CopyMatchesSource", runner, StringComparison.Ordinal);
        Assert.Contains("SuiteEvidence = @($suiteEvidence", runner, StringComparison.Ordinal);

        Assert.Contains("$solution.SelectNodes(\"//*[local-name()='Project']\")", evidenceModule, StringComparison.Ordinal);
        Assert.Contains("$declaresTestsPath = $normalizedPath.StartsWith(", evidenceModule, StringComparison.Ordinal);
        Assert.Contains("GetRelativePath($testsRoot, $projectPath)", evidenceModule, StringComparison.Ordinal);
        Assert.Contains("Get-EvaluatedProjectClassification", evidenceModule, StringComparison.Ordinal);
        Assert.Contains("Get-CiProjectFilesUnderTests", evidenceModule, StringComparison.Ordinal);
        Assert.Contains("function Get-CiRepositoryState", evidenceModule, StringComparison.Ordinal);
        Assert.Contains("function Get-CiTestAssemblySnapshot", evidenceModule, StringComparison.Ordinal);
        Assert.Contains("function Get-CiTestAssemblyIdentityErrors", evidenceModule, StringComparison.Ordinal);
        Assert.DoesNotContain("function Get-CiRepositoryState", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("function Get-CiTestAssemblySnapshot", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("function Get-CiTestAssemblyIdentityErrors", runner, StringComparison.Ordinal);
        Assert.Contains("-getProperty:IsTestProject", evidenceModule, StringComparison.Ordinal);
        Assert.Contains("-getProperty:Configuration", evidenceModule, StringComparison.Ordinal);
        Assert.Contains("-getProperty:TargetFramework", evidenceModule, StringComparison.Ordinal);
        Assert.Contains("-getProperty:TargetFrameworks", evidenceModule, StringComparison.Ordinal);
        Assert.Contains("-getProperty:TargetDir", evidenceModule, StringComparison.Ordinal);
        Assert.Contains("-getProperty:TargetPath", evidenceModule, StringComparison.Ordinal);
        Assert.Contains("omitted from the solution inventory", evidenceModule, StringComparison.Ordinal);
        Assert.Contains("unsupported project kind", evidenceModule, StringComparison.Ordinal);
        Assert.Contains("[IO.FileAttributes]::ReparsePoint", evidenceModule, StringComparison.Ordinal);
        Assert.Contains("Test-CiTrxFile", evidenceModule, StringComparison.Ordinal);
        Assert.Contains("Test-CiCoberturaFile", evidenceModule, StringComparison.Ordinal);
        Assert.Contains("CopyMatchesSource", evidenceModule, StringComparison.Ordinal);
        Assert.Contains("TrxSha256 = Get-Sha256", evidenceModule, StringComparison.Ordinal);
        Assert.Contains("DirectCoveragePaths = @(Get-DirectCoveragePaths", evidenceModule, StringComparison.Ordinal);
        Assert.Contains("$trxCount -ne 1 -or $coverageCount -ne 1", evidenceModule, StringComparison.Ordinal);
        Assert.DoesNotContain("GetLastWriteTimeUtc", evidenceModule, StringComparison.Ordinal);
        Assert.DoesNotContain("-Recurse", evidenceModule, StringComparison.Ordinal);

        var lockAcquisition = runner.IndexOf("[IO.FileShare]::None", StringComparison.Ordinal);
        var baseline = runner.IndexOf("$evidenceBaseline =", StringComparison.Ordinal);
        var processStart = runner.IndexOf("$process.Start()", StringComparison.Ordinal);
        var snapshot = runner.IndexOf("$suiteEvidence = @(Get-CiTestEvidenceDelta", StringComparison.Ordinal);
        var receipt = runner.IndexOf("$receipt = [ordered]@{", StringComparison.Ordinal);
        Assert.True(
            lockAcquisition < baseline
            && baseline < processStart
            && processStart < snapshot
            && snapshot < receipt);
    }

    [Fact]
    public void Evidence_snapshot_accepts_one_changed_trx_and_one_new_direct_coverage_per_suite()
    {
        var repository = CreateSyntheticEvidenceRepository("Alpha", "Beta");
        try
        {
            using var result = RunEvidenceScenario(
                repository,
                """
                foreach ($suite in $inventory) {
                    Write-ValidTrx -Path $suite.TrxPath -SuiteName $suite.SuiteName -AssemblyPath $suite.TestAssemblyPath
                    [IO.File]::SetLastWriteTimeUtc($suite.TrxPath, [datetime]"2000-01-01T00:00:00Z")

                    $coverageDirectory = Join-Path $suite.TestResultsRoot ([Guid]::NewGuid().ToString("D"))
                    New-Item -ItemType Directory -Path $coverageDirectory | Out-Null
                    $coveragePath = Join-Path $coverageDirectory "coverage.cobertura.xml"
                    Write-ValidCoverage -Path $coveragePath
                    [IO.File]::SetLastWriteTimeUtc($coveragePath, [datetime]"2000-01-01T00:00:00Z")

                    $nestedDirectory = Join-Path $suite.TestResultsRoot (
                        "timestamp-" + $suite.SuiteName + "/In/HOST")
                    New-Item -ItemType Directory -Path $nestedDirectory | Out-Null
                    [IO.File]::WriteAllText(
                        (Join-Path $nestedDirectory "coverage.cobertura.xml"),
                        "nested-duplicate")
                }
                """);

            Assert.Equal(2, result.RootElement.GetProperty("InventoryCount").GetInt32());
            Assert.Empty(result.RootElement.GetProperty("Errors").EnumerateArray());
            var evidence = result.RootElement.GetProperty("Evidence").EnumerateArray().ToArray();
            Assert.Equal(2, evidence.Length);
            Assert.All(evidence, suite =>
            {
                Assert.True(suite.GetProperty("TrxIsCurrent").GetBoolean());
                Assert.True(suite.GetProperty("TrxValid").GetBoolean());
                Assert.Equal(1, suite.GetProperty("NewCoverageCount").GetInt32());
                Assert.True(suite.GetProperty("CoverageValid")[0].GetBoolean());
            });
        }
        finally
        {
            Directory.Delete(repository, recursive: true);
        }
    }

    [Fact]
    public void Evidence_snapshot_rejects_balanced_duplicates_and_omitted_suite_artifacts()
    {
        var balancedRepository = CreateSyntheticEvidenceRepository("Alpha", "Beta");
        try
        {
            using var balanced = RunEvidenceScenario(
                balancedRepository,
                """
                foreach ($suite in $inventory) {
                    Write-ValidTrx -Path $suite.TrxPath -SuiteName $suite.SuiteName -AssemblyPath $suite.TestAssemblyPath
                }

                foreach ($index in 1..2) {
                    $coverageDirectory = Join-Path $inventory[0].TestResultsRoot (
                        [Guid]::NewGuid().ToString("D"))
                    New-Item -ItemType Directory -Path $coverageDirectory | Out-Null
                    Write-ValidCoverage -Path (Join-Path $coverageDirectory "coverage.cobertura.xml")
                }
                """);

            var balancedEvidence = balanced.RootElement.GetProperty("Evidence")
                .EnumerateArray()
                .ToDictionary(item => item.GetProperty("SuiteName").GetString()!);
            Assert.Equal(2, balancedEvidence["Alpha"].GetProperty("NewCoverageCount").GetInt32());
            Assert.Equal(0, balancedEvidence["Beta"].GetProperty("NewCoverageCount").GetInt32());
            var balancedErrors = balanced.RootElement.GetProperty("Errors")
                .EnumerateArray()
                .Select(error => error.GetString()!)
                .ToArray();
            Assert.Equal(2, balancedErrors.Length);
            Assert.Contains(
                balancedErrors,
                error => error.StartsWith("Alpha:", StringComparison.Ordinal)
                    && error.Contains("found 1 TRX and 2 coverage files", StringComparison.Ordinal));
            Assert.Contains(
                balancedErrors,
                error => error.StartsWith("Beta:", StringComparison.Ordinal)
                    && error.Contains("found 1 TRX and 0 coverage files", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(balancedRepository, recursive: true);
        }

        var omittedRepository = CreateSyntheticEvidenceRepository("Alpha", "Beta");
        try
        {
            using var omitted = RunEvidenceScenario(
                omittedRepository,
                """
                $completed = $inventory[0]
                Write-ValidTrx -Path $completed.TrxPath -SuiteName $completed.SuiteName -AssemblyPath $completed.TestAssemblyPath
                $coverageDirectory = Join-Path $completed.TestResultsRoot ([Guid]::NewGuid().ToString("D"))
                New-Item -ItemType Directory -Path $coverageDirectory | Out-Null
                Write-ValidCoverage -Path (Join-Path $coverageDirectory "coverage.cobertura.xml")

                $omitted = $inventory[1]
                $nestedDirectory = Join-Path $omitted.TestResultsRoot "new-timestamp/In/HOST"
                New-Item -ItemType Directory -Path $nestedDirectory | Out-Null
                [IO.File]::WriteAllText(
                    (Join-Path $nestedDirectory "coverage.cobertura.xml"),
                    "nested-only")
                """);

            var omittedEvidence = omitted.RootElement.GetProperty("Evidence")
                .EnumerateArray()
                .ToDictionary(item => item.GetProperty("SuiteName").GetString()!);
            Assert.False(omittedEvidence["Beta"].GetProperty("TrxIsCurrent").GetBoolean());
            Assert.Equal(0, omittedEvidence["Beta"].GetProperty("NewCoverageCount").GetInt32());
            var errors = omitted.RootElement.GetProperty("Errors").EnumerateArray().ToArray();
            var error = Assert.Single(errors);
            Assert.Contains("Beta", error.GetString(), StringComparison.Ordinal);
            Assert.Contains("found 0 TRX and 0 coverage", error.GetString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(omittedRepository, recursive: true);
        }
    }

    [Fact]
    public void Evidence_snapshot_rejects_malformed_or_semantically_failed_xml()
    {
        var repository = CreateSyntheticEvidenceRepository("Alpha", "Beta", "Gamma");
        try
        {
            using var result = RunEvidenceScenario(
                repository,
                """
                $alpha = $inventory | Where-Object SuiteName -eq "Alpha"
                [IO.File]::WriteAllText($alpha.TrxPath, "<TestRun>")
                $alphaCoverageDirectory = Join-Path $alpha.TestResultsRoot ([Guid]::NewGuid().ToString("D"))
                New-Item -ItemType Directory -Path $alphaCoverageDirectory | Out-Null
                Write-ValidCoverage -Path (Join-Path $alphaCoverageDirectory "coverage.cobertura.xml")

                $beta = $inventory | Where-Object SuiteName -eq "Beta"
                Write-FailedTrx -Path $beta.TrxPath -SuiteName $beta.SuiteName -AssemblyPath $beta.TestAssemblyPath
                $betaCoverageDirectory = Join-Path $beta.TestResultsRoot ([Guid]::NewGuid().ToString("D"))
                New-Item -ItemType Directory -Path $betaCoverageDirectory | Out-Null
                [IO.File]::WriteAllText(
                    (Join-Path $betaCoverageDirectory "coverage.cobertura.xml"),
                    "<coverage lines-valid='0' lines-covered='0' line-rate='0' />")

                $gamma = $inventory | Where-Object SuiteName -eq "Gamma"
                Write-ValidTrx -Path $gamma.TrxPath -SuiteName $gamma.SuiteName -AssemblyPath $gamma.TestAssemblyPath
                $gammaCoverageDirectory = Join-Path $gamma.TestResultsRoot ([Guid]::NewGuid().ToString("D"))
                New-Item -ItemType Directory -Path $gammaCoverageDirectory | Out-Null
                [IO.File]::WriteAllText(
                    (Join-Path $gammaCoverageDirectory "coverage.cobertura.xml"),
                    "<coverage>")
                """);

            var evidence = result.RootElement.GetProperty("Evidence")
                .EnumerateArray()
                .ToDictionary(item => item.GetProperty("SuiteName").GetString()!);
            Assert.False(evidence["Alpha"].GetProperty("TrxValid").GetBoolean());
            Assert.True(evidence["Alpha"].GetProperty("CoverageValid")[0].GetBoolean());
            Assert.False(evidence["Beta"].GetProperty("TrxValid").GetBoolean());
            Assert.False(evidence["Beta"].GetProperty("CoverageValid")[0].GetBoolean());
            Assert.True(evidence["Gamma"].GetProperty("TrxValid").GetBoolean());
            Assert.False(evidence["Gamma"].GetProperty("CoverageValid")[0].GetBoolean());

            var errors = result.RootElement.GetProperty("Errors")
                .EnumerateArray()
                .Select(error => error.GetString()!)
                .ToArray();
            Assert.Equal(3, errors.Length);
            Assert.Contains(
                errors,
                error => error.StartsWith("Alpha:", StringComparison.Ordinal)
                    && error.Contains("TRX validation failed", StringComparison.Ordinal));
            Assert.Contains(
                errors,
                error => error.StartsWith("Beta:", StringComparison.Ordinal)
                    && error.Contains("TRX validation failed", StringComparison.Ordinal)
                    && error.Contains("Cobertura validation failed", StringComparison.Ordinal));
            Assert.Contains(
                errors,
                error => error.StartsWith("Gamma:", StringComparison.Ordinal)
                    && error.Contains("Cobertura validation failed", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(repository, recursive: true);
        }
    }

    [Fact]
    public void Evidence_semantics_bind_the_trx_assembly_and_cobertura_root_counters()
    {
        var repository = CreateSyntheticEvidenceRepository("Alpha");
        try
        {
            using var result = RunEvidenceScenario(
                repository,
                """
                $suite = $inventory[0]
                Write-ValidTrx -Path $suite.TrxPath -SuiteName $suite.SuiteName -AssemblyPath $suite.TestAssemblyPath
                $trx = [IO.File]::ReadAllText($suite.TrxPath)
                $wrongAssembly = [Security.SecurityElement]::Escape(
                    (Join-Path $env:OCF_TEST_REPOSITORY_ROOT "wrong/Foundry.Tests.Alpha.dll"))
                $expectedAssembly = [Security.SecurityElement]::Escape($suite.TestAssemblyPath)
                [IO.File]::WriteAllText(
                    $suite.TrxPath,
                    $trx.Replace($expectedAssembly, $wrongAssembly))

                $coverageDirectory = Join-Path $suite.TestResultsRoot ([Guid]::NewGuid().ToString("D"))
                New-Item -ItemType Directory -Path $coverageDirectory | Out-Null
                [IO.File]::WriteAllText(
                    (Join-Path $coverageDirectory "coverage.cobertura.xml"),
                    "<coverage line-rate='1' lines-covered='1' lines-valid='1'>" +
                    "<packages><package><classes><class><lines>" +
                    "<line number='1' hits='0' />" +
                    "</lines></class></classes></package></packages></coverage>")
                """);

            var evidence = Assert.Single(result.RootElement.GetProperty("Evidence").EnumerateArray());
            Assert.False(evidence.GetProperty("TrxValid").GetBoolean());
            Assert.False(evidence.GetProperty("CoverageValid")[0].GetBoolean());
            var error = Assert.Single(result.RootElement.GetProperty("Errors").EnumerateArray());
            Assert.Contains("TRX validation failed", error.GetString(), StringComparison.Ordinal);
            Assert.Contains("Cobertura validation failed", error.GetString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(repository, recursive: true);
        }
    }

    [Fact]
    public void Evidence_inventory_rejects_a_solution_test_path_that_escapes_tests()
    {
        var repository = Path.Combine(
            Path.GetTempPath(),
            "ocf-ci-evidence-escape-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(repository, "tests"));
            Directory.CreateDirectory(Path.Combine(repository, "outside"));
            File.WriteAllText(
                Path.Combine(repository, "outside", "Escaped.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\" />");
            File.WriteAllText(
                Path.Combine(repository, "OpenClassroomFoundry.slnx"),
                """
                <Solution>
                  <Folder Name="/tests/">
                    <Project Path="tests/../outside/Escaped.csproj" />
                  </Folder>
                </Solution>
                """);

            var process = RunPowerShell(
                repository,
                "Get-CiTestSuiteInventory -RepositoryRoot $env:OCF_TEST_REPOSITORY_ROOT");

            Assert.NotEqual(0, process.ExitCode);
            Assert.Contains("escapes the repository tests root", process.StandardError, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(repository, recursive: true);
        }
    }

    [Fact]
    public void Evidence_inventory_rejects_an_import_classified_test_project_outside_tests()
    {
        var repository = Path.Combine(
            Path.GetTempPath(),
            "ocf-ci-evidence-outside-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(repository, "tests"));
            Directory.CreateDirectory(Path.Combine(repository, "tools", "OutsideTests"));
            File.WriteAllText(
                Path.Combine(repository, "tools", "OutsideTests", "test-classification.props"),
                "<Project><PropertyGroup><IsTestProject>true</IsTestProject></PropertyGroup></Project>");
            File.WriteAllText(
                Path.Combine(repository, "tools", "OutsideTests", "OutsideTests.csproj"),
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <Import Project="test-classification.props" />
                </Project>
                """);
            File.WriteAllText(
                Path.Combine(repository, "OpenClassroomFoundry.slnx"),
                """
                <Solution>
                  <Folder Name="/tools/">
                    <Project Path="tools/OutsideTests/OutsideTests.csproj" />
                  </Folder>
                </Solution>
                """);

            var process = RunPowerShell(
                repository,
                "Get-CiTestSuiteInventory -RepositoryRoot $env:OCF_TEST_REPOSITORY_ROOT");

            Assert.NotEqual(0, process.ExitCode);
            Assert.Contains("test project is outside", process.StandardError, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(repository, recursive: true);
        }
    }

    [Fact]
    public void Evidence_inventory_rejects_unsupported_solution_project_kinds()
    {
        var repository = Path.Combine(
            Path.GetTempPath(),
            "ocf-ci-evidence-kind-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(repository, "tests", "FSharp"));
            File.WriteAllText(
                Path.Combine(repository, "tests", "FSharp", "Foundry.Tests.FSharp.fsproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\" />");
            File.WriteAllText(
                Path.Combine(repository, "OpenClassroomFoundry.slnx"),
                """
                <Solution>
                  <Folder Name="/tests/">
                    <Project Path="tests/FSharp/Foundry.Tests.FSharp.fsproj" />
                  </Folder>
                </Solution>
                """);

            var process = RunPowerShell(
                repository,
                "Get-CiTestSuiteInventory -RepositoryRoot $env:OCF_TEST_REPOSITORY_ROOT");

            Assert.NotEqual(0, process.ExitCode);
            Assert.Contains("unsupported project kind", process.StandardError, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(repository, recursive: true);
        }
    }

    [Fact]
    public void Evidence_inventory_rejects_a_project_file_omitted_from_the_solution()
    {
        var repository = CreateSyntheticEvidenceRepository("Alpha");
        try
        {
            var omittedDirectory = Path.Combine(repository, "tests", "Omitted");
            Directory.CreateDirectory(omittedDirectory);
            File.WriteAllText(
                Path.Combine(omittedDirectory, "Foundry.Tests.Omitted.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>" +
                "<IsTestProject>true</IsTestProject></PropertyGroup></Project>");

            var process = RunPowerShell(
                repository,
                "Get-CiTestSuiteInventory -RepositoryRoot $env:OCF_TEST_REPOSITORY_ROOT");

            Assert.NotEqual(0, process.ExitCode);
            Assert.Contains(
                "omitted from the solution inventory",
                process.StandardError,
                StringComparison.Ordinal);
            Assert.Contains("Foundry.Tests.Omitted.csproj", process.StandardError, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(repository, recursive: true);
        }
    }

    [Fact]
    public void Evidence_inventory_records_release_single_tfm_and_project_local_output()
    {
        var repository = CreateSyntheticEvidenceRepository("Alpha");
        try
        {
            var process = RunPowerShell(
                repository,
                """
                $suite = @(Get-CiTestSuiteInventory -RepositoryRoot $env:OCF_TEST_REPOSITORY_ROOT)[0]
                [ordered]@{
                    Configuration = $suite.Configuration
                    TargetFramework = $suite.TargetFramework
                    ReleaseOutputRoot = $suite.ReleaseOutputRoot
                    TestAssemblyOutputRoot = $suite.TestAssemblyOutputRoot
                    TestAssemblyPath = $suite.TestAssemblyPath
                } | ConvertTo-Json -Compress
                """);

            Assert.Equal(0, process.ExitCode);
            using var result = JsonDocument.Parse(process.StandardOutput);
            Assert.Equal("Release", result.RootElement.GetProperty("Configuration").GetString());
            Assert.Equal("net10.0", result.RootElement.GetProperty("TargetFramework").GetString());
            Assert.Equal(
                Path.GetFullPath(Path.Combine(repository, "tests", "Alpha", "bin", "Release")),
                Path.TrimEndingDirectorySeparator(
                    result.RootElement.GetProperty("ReleaseOutputRoot").GetString()!));
            Assert.Equal(
                Path.GetFullPath(Path.Combine(repository, "tests", "Alpha", "bin", "Release", "net10.0")),
                Path.TrimEndingDirectorySeparator(
                    result.RootElement.GetProperty("TestAssemblyOutputRoot").GetString()!));
            Assert.Equal(
                Path.GetFullPath(Path.Combine(
                    repository,
                    "tests",
                    "Alpha",
                    "bin",
                    "Release",
                    "net10.0",
                    "Foundry.Tests.Alpha.dll")),
                result.RootElement.GetProperty("TestAssemblyPath").GetString());
        }
        finally
        {
            Directory.Delete(repository, recursive: true);
        }
    }

    [Theory]
    [InlineData("net10.0")]
    [InlineData("net10.0;net9.0")]
    public void Evidence_inventory_rejects_outer_build_target_frameworks(string targetFrameworks)
    {
        var repository = CreateSyntheticEvidenceRepository("Alpha");
        try
        {
            File.WriteAllText(
                Path.Combine(repository, "tests", "Alpha", "Foundry.Tests.Alpha.csproj"),
                $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <IsTestProject>true</IsTestProject>
                    <TargetFrameworks>{targetFrameworks}</TargetFrameworks>
                  </PropertyGroup>
                </Project>
                """);

            var process = RunPowerShell(
                repository,
                "Get-CiTestSuiteInventory -RepositoryRoot $env:OCF_TEST_REPOSITORY_ROOT");

            Assert.NotEqual(0, process.ExitCode);
            Assert.Contains("uses TargetFrameworks", process.StandardError, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(repository, recursive: true);
        }
    }

    [Fact]
    public void Evidence_inventory_rejects_a_missing_target_framework()
    {
        var repository = CreateSyntheticEvidenceRepository("Alpha");
        try
        {
            File.WriteAllText(
                Path.Combine(repository, "tests", "Alpha", "Foundry.Tests.Alpha.csproj"),
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <IsTestProject>true</IsTestProject>
                  </PropertyGroup>
                </Project>
                """);

            var process = RunPowerShell(
                repository,
                "Get-CiTestSuiteInventory -RepositoryRoot $env:OCF_TEST_REPOSITORY_ROOT");

            Assert.NotEqual(0, process.ExitCode);
            Assert.Contains(
                "omitted the single Release TargetFramework",
                process.StandardError,
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(repository, recursive: true);
        }
    }

    [Fact]
    public void Evidence_inventory_rejects_debug_configuration()
    {
        var repository = CreateSyntheticEvidenceRepository("Alpha");
        try
        {
            File.WriteAllText(
                Path.Combine(repository, "tests", "Alpha", "Foundry.Tests.Alpha.csproj"),
                """
                <Project Sdk="Microsoft.NET.Sdk" TreatAsLocalProperty="Configuration">
                  <PropertyGroup>
                    <Configuration>Debug</Configuration>
                    <IsTestProject>true</IsTestProject>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);

            var process = RunPowerShell(
                repository,
                "Get-CiTestSuiteInventory -RepositoryRoot $env:OCF_TEST_REPOSITORY_ROOT");

            Assert.NotEqual(0, process.ExitCode);
            Assert.Contains("did not evaluate Configuration=Release", process.StandardError, StringComparison.Ordinal);
            Assert.Contains("Debug", process.StandardError, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(repository, recursive: true);
        }
    }

    [Fact]
    public void Evidence_inventory_rejects_an_escaped_release_output()
    {
        var repository = CreateSyntheticEvidenceRepository("Alpha");
        try
        {
            File.WriteAllText(
                Path.Combine(repository, "tests", "Alpha", "Foundry.Tests.Alpha.csproj"),
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <IsTestProject>true</IsTestProject>
                    <TargetFramework>net10.0</TargetFramework>
                    <OutputPath>..\..\escaped-output\</OutputPath>
                  </PropertyGroup>
                </Project>
                """);

            var process = RunPowerShell(
                repository,
                "Get-CiTestSuiteInventory -RepositoryRoot $env:OCF_TEST_REPOSITORY_ROOT");

            Assert.NotEqual(0, process.ExitCode);
            Assert.Contains("Release TargetDir escapes its containment root", process.StandardError, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(repository, recursive: true);
        }
    }

    [Fact]
    public void Evidence_inventory_rejects_a_target_path_outside_its_target_directory()
    {
        var repository = CreateSyntheticEvidenceRepository("Alpha");
        try
        {
            File.WriteAllText(
                Path.Combine(repository, "tests", "Alpha", "Foundry.Tests.Alpha.csproj"),
                """
                <Project>
                  <Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk" />
                  <PropertyGroup>
                    <IsTestProject>true</IsTestProject>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                  <Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk" />
                  <PropertyGroup>
                    <TargetPath>$(MSBuildProjectDirectory)\bin\Release\rogue\Foundry.Tests.Alpha.dll</TargetPath>
                  </PropertyGroup>
                </Project>
                """);

            var process = RunPowerShell(
                repository,
                "Get-CiTestSuiteInventory -RepositoryRoot $env:OCF_TEST_REPOSITORY_ROOT");

            Assert.NotEqual(0, process.ExitCode);
            Assert.Contains(
                "Release test assembly TargetDir binding escapes its containment root",
                process.StandardError,
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(repository, recursive: true);
        }
    }

    [Fact]
    public void Evidence_inventory_rejects_duplicate_normalized_test_assembly_paths()
    {
        var repository = Path.Combine(
            Path.GetTempPath(),
            "ocf-ci-evidence-duplicate-assembly-" + Guid.NewGuid().ToString("N"));
        try
        {
            var sharedDirectory = Path.Combine(repository, "tests", "Shared");
            Directory.CreateDirectory(sharedDirectory);
            const string project = """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <IsTestProject>true</IsTestProject>
                    <TargetFramework>net10.0</TargetFramework>
                    <AssemblyName>SharedTests</AssemblyName>
                  </PropertyGroup>
                </Project>
                """;
            File.WriteAllText(Path.Combine(sharedDirectory, "One.csproj"), project);
            File.WriteAllText(Path.Combine(sharedDirectory, "Two.csproj"), project);
            File.WriteAllText(
                Path.Combine(repository, "OpenClassroomFoundry.slnx"),
                """
                <Solution>
                  <Folder Name="/tests/">
                    <Project Path="tests/Shared/One.csproj" />
                    <Project Path="tests/Shared/Two.csproj" />
                  </Folder>
                </Solution>
                """);

            var processResult = RunPowerShell(
                repository,
                "Get-CiTestSuiteInventory -RepositoryRoot $env:OCF_TEST_REPOSITORY_ROOT");

            Assert.NotEqual(0, processResult.ExitCode);
            var compactError = string.Concat(
                processResult.StandardError.Where(character => !char.IsWhiteSpace(character)));
            Assert.Contains(
                "samenormalizedReleaseTargetPath",
                compactError,
                StringComparison.Ordinal);
            Assert.Contains("One.csproj", processResult.StandardError, StringComparison.Ordinal);
            Assert.Contains("Two.csproj", processResult.StandardError, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(repository, recursive: true);
        }
    }

    [Fact]
    public void Evidence_snapshot_rejects_a_reparse_point_collector_directory_on_windows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var repository = CreateSyntheticEvidenceRepository("Alpha");
        var collectorLink = Path.Combine(
            repository,
            "tests",
            "Alpha",
            "TestResults",
            "linked-collector");
        try
        {
            var outsideCollector = Path.Combine(repository, "outside-collector");
            Directory.CreateDirectory(outsideCollector);
            File.WriteAllText(
                Path.Combine(outsideCollector, "coverage.cobertura.xml"),
                "outside-evidence");

            var process = RunPowerShell(
                repository,
                """
                $collectorLink = Join-Path $env:OCF_TEST_REPOSITORY_ROOT "tests/Alpha/TestResults/linked-collector"
                $outsideCollector = Join-Path $env:OCF_TEST_REPOSITORY_ROOT "outside-collector"
                New-Item -ItemType Junction -Path $collectorLink -Target $outsideCollector | Out-Null
                $inventory = @(Get-CiTestSuiteInventory -RepositoryRoot $env:OCF_TEST_REPOSITORY_ROOT)
                Get-CiTestEvidenceBaseline -Suites $inventory
                """);

            Assert.NotEqual(0, process.ExitCode);
            Assert.Contains(
                "coverage collector directory crosses a reparse point",
                process.StandardError,
                StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(collectorLink))
            {
                Directory.Delete(collectorLink);
            }

            Directory.Delete(repository, recursive: true);
        }
    }

    [Fact]
    public void Evidence_copy_rejects_source_and_destination_paths_outside_their_roots()
    {
        var repository = Path.Combine(
            Path.GetTempPath(),
            "ocf-ci-evidence-copy-escape-" + Guid.NewGuid().ToString("N"));
        try
        {
            var sourceRoot = Path.Combine(repository, "tests", "Synthetic", "TestResults");
            var destinationRoot = Path.Combine(repository, "out", "ci-test-run", "synthetic");
            Directory.CreateDirectory(sourceRoot);
            Directory.CreateDirectory(destinationRoot);
            File.WriteAllText(Path.Combine(sourceRoot, "inside.trx"), "inside-source");
            File.WriteAllText(Path.Combine(repository, "outside-source.trx"), "outside-source");

            var sourceEscape = RunPowerShell(
                repository,
                """
                $sourceRoot = Join-Path $env:OCF_TEST_REPOSITORY_ROOT "tests/Synthetic/TestResults"
                $destinationRoot = Join-Path $env:OCF_TEST_REPOSITORY_ROOT "out/ci-test-run/synthetic"
                $source = Join-Path $env:OCF_TEST_REPOSITORY_ROOT "outside-source.trx"
                $destination = Join-Path $destinationRoot "source-escape.trx"
                $expected = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
                Copy-CiEvidenceFile `
                    -SourcePath $source `
                    -SourceContainmentRoot $sourceRoot `
                    -DestinationPath $destination `
                    -DestinationContainmentRoot $destinationRoot `
                    -ExpectedSourceSha256 $expected
                """);
            Assert.NotEqual(0, sourceEscape.ExitCode);
            Assert.Contains(
                "source evidence file escapes its containment root",
                sourceEscape.StandardError,
                StringComparison.Ordinal);

            var destinationEscape = RunPowerShell(
                repository,
                """
                $sourceRoot = Join-Path $env:OCF_TEST_REPOSITORY_ROOT "tests/Synthetic/TestResults"
                $destinationRoot = Join-Path $env:OCF_TEST_REPOSITORY_ROOT "out/ci-test-run/synthetic"
                $source = Join-Path $sourceRoot "inside.trx"
                $destination = Join-Path $env:OCF_TEST_REPOSITORY_ROOT "outside-destination.trx"
                $expected = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
                Copy-CiEvidenceFile `
                    -SourcePath $source `
                    -SourceContainmentRoot $sourceRoot `
                    -DestinationPath $destination `
                    -DestinationContainmentRoot $destinationRoot `
                    -ExpectedSourceSha256 $expected
                """);
            Assert.NotEqual(0, destinationEscape.ExitCode);
            Assert.Contains(
                "curated evidence file escapes its containment root",
                destinationEscape.StandardError,
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(repository, recursive: true);
        }
    }

    [Fact]
    public void Evidence_copy_records_and_enforces_equal_source_and_destination_hashes()
    {
        var repository = Path.Combine(
            Path.GetTempPath(),
            "ocf-ci-evidence-copy-" + Guid.NewGuid().ToString("N"));
        try
        {
            var sourceRoot = Path.Combine(repository, "tests", "Synthetic", "TestResults");
            var destinationRoot = Path.Combine(repository, "out", "ci-test-run", "synthetic");
            Directory.CreateDirectory(sourceRoot);
            Directory.CreateDirectory(destinationRoot);
            var source = Path.Combine(sourceRoot, "test-results.trx");
            var destination = Path.Combine(destinationRoot, "Synthetic.trx");
            File.WriteAllText(source, "hash-bound-evidence");

            var script = """
                $sourceRoot = Join-Path $env:OCF_TEST_REPOSITORY_ROOT "tests/Synthetic/TestResults"
                $destinationRoot = Join-Path $env:OCF_TEST_REPOSITORY_ROOT "out/ci-test-run/synthetic"
                $source = Join-Path $sourceRoot "test-results.trx"
                $destination = Join-Path $destinationRoot "Synthetic.trx"
                $expected = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
                Copy-CiEvidenceFile `
                    -SourcePath $source `
                    -SourceContainmentRoot $sourceRoot `
                    -DestinationPath $destination `
                    -DestinationContainmentRoot $destinationRoot `
                    -ExpectedSourceSha256 $expected |
                    ConvertTo-Json -Compress
                """;
            var process = RunPowerShell(repository, script);

            Assert.Equal(0, process.ExitCode);
            using var result = JsonDocument.Parse(process.StandardOutput);
            Assert.True(result.RootElement.GetProperty("CopyMatchesSource").GetBoolean());
            Assert.Equal(
                result.RootElement.GetProperty("SourceSha256").GetString(),
                result.RootElement.GetProperty("CopiedSha256").GetString());
            Assert.Equal(File.ReadAllText(source), File.ReadAllText(destination));

            var rejectedCopy = RunPowerShell(
                repository,
                """
                $sourceRoot = Join-Path $env:OCF_TEST_REPOSITORY_ROOT "tests/Synthetic/TestResults"
                $destinationRoot = Join-Path $env:OCF_TEST_REPOSITORY_ROOT "out/ci-test-run/synthetic"
                $source = Join-Path $sourceRoot "test-results.trx"
                $destination = Join-Path $destinationRoot "rejected.trx"
                Copy-CiEvidenceFile `
                    -SourcePath $source `
                    -SourceContainmentRoot $sourceRoot `
                    -DestinationPath $destination `
                    -DestinationContainmentRoot $destinationRoot `
                    -ExpectedSourceSha256 ("0" * 64)
                """);
            Assert.NotEqual(0, rejectedCopy.ExitCode);
            Assert.Contains("source evidence hash changed", rejectedCopy.StandardError, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(repository, recursive: true);
        }
    }

    [Fact]
    public void Repository_state_hashes_tracked_and_untracked_bytes_but_excludes_ignored_outputs()
    {
        var repository = Path.Combine(
            Path.GetTempPath(),
            "ocf-ci-repository-state-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repository);
        try
        {
            var process = RunPowerShell(
                repository,
                """
                $root = $env:OCF_TEST_REPOSITORY_ROOT
                & git -C $root init --quiet --object-format=sha1
                if ($LASTEXITCODE -ne 0) { throw "git init failed" }
                & git -C $root config user.name "CI Evidence Test"
                & git -C $root config user.email "ci-evidence@example.invalid"
                [IO.File]::WriteAllText((Join-Path $root ".gitignore"), "ignored/`n")
                [IO.File]::WriteAllText((Join-Path $root "tracked.txt"), "0000")
                & git -C $root add -- .
                if ($LASTEXITCODE -ne 0) { throw "git add failed" }
                & git -C $root commit --quiet -m "fixture"
                if ($LASTEXITCODE -ne 0) { throw "git commit failed" }

                [IO.File]::WriteAllText((Join-Path $root "tracked.txt"), "1111")
                $trackedBefore = Get-CiRepositoryState -RepositoryRoot $root
                [IO.File]::WriteAllText((Join-Path $root "tracked.txt"), "2222")
                $trackedAfter = Get-CiRepositoryState -RepositoryRoot $root

                [IO.File]::WriteAllText((Join-Path $root "untracked.txt"), "aaaa")
                $untrackedBefore = Get-CiRepositoryState -RepositoryRoot $root
                [IO.File]::WriteAllText((Join-Path $root "untracked.txt"), "bbbb")
                $untrackedAfter = Get-CiRepositoryState -RepositoryRoot $root

                $ignoredBefore = $untrackedAfter
                [void][IO.Directory]::CreateDirectory((Join-Path $root "ignored"))
                [IO.File]::WriteAllText((Join-Path $root "ignored/output.bin"), "first")
                $ignoredCreated = Get-CiRepositoryState -RepositoryRoot $root
                [IO.File]::WriteAllText((Join-Path $root "ignored/output.bin"), "second")
                $ignoredAfter = Get-CiRepositoryState -RepositoryRoot $root

                [ordered]@{
                    TrackedBefore = $trackedBefore
                    TrackedAfter = $trackedAfter
                    UntrackedBefore = $untrackedBefore
                    UntrackedAfter = $untrackedAfter
                    IgnoredBefore = $ignoredBefore
                    IgnoredCreated = $ignoredCreated
                    IgnoredAfter = $ignoredAfter
                } | ConvertTo-Json -Depth 4 -Compress
                """);

            Assert.Equal(0, process.ExitCode);
            using var result = JsonDocument.Parse(process.StandardOutput);
            var trackedBefore = result.RootElement.GetProperty("TrackedBefore");
            var trackedAfter = result.RootElement.GetProperty("TrackedAfter");
            Assert.True(trackedBefore.GetProperty("Dirty").GetBoolean());
            Assert.Equal(
                trackedBefore.GetProperty("StatusSha256").GetString(),
                trackedAfter.GetProperty("StatusSha256").GetString());
            Assert.NotEqual(
                trackedBefore.GetProperty("SourceContentSha256").GetString(),
                trackedAfter.GetProperty("SourceContentSha256").GetString());

            var untrackedBefore = result.RootElement.GetProperty("UntrackedBefore");
            var untrackedAfter = result.RootElement.GetProperty("UntrackedAfter");
            Assert.Equal(
                untrackedBefore.GetProperty("StatusSha256").GetString(),
                untrackedAfter.GetProperty("StatusSha256").GetString());
            Assert.NotEqual(
                untrackedBefore.GetProperty("SourceContentSha256").GetString(),
                untrackedAfter.GetProperty("SourceContentSha256").GetString());

            var ignoredBefore = result.RootElement.GetProperty("IgnoredBefore");
            foreach (var ignoredStateName in new[] { "IgnoredCreated", "IgnoredAfter" })
            {
                var ignoredState = result.RootElement.GetProperty(ignoredStateName);
                Assert.Equal(
                    ignoredBefore.GetProperty("StatusSha256").GetString(),
                    ignoredState.GetProperty("StatusSha256").GetString());
                Assert.Equal(
                    ignoredBefore.GetProperty("SourceContentSha256").GetString(),
                    ignoredState.GetProperty("SourceContentSha256").GetString());
                Assert.Equal(
                    ignoredBefore.GetProperty("SourceFileCount").GetInt32(),
                    ignoredState.GetProperty("SourceFileCount").GetInt32());
            }
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(
                         repository,
                         "*",
                         SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(repository, recursive: true);
        }
    }

    [Fact]
    public void Test_assembly_snapshot_detects_same_length_byte_mutation()
    {
        var repository = Path.Combine(
            Path.GetTempPath(),
            "ocf-ci-assembly-state-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repository);
        try
        {
            var process = RunPowerShell(
                repository,
                """
                $root = $env:OCF_TEST_REPOSITORY_ROOT
                $outputRoot = Join-Path $root "tests/Alpha/bin/Release/net10.0"
                [void][IO.Directory]::CreateDirectory($outputRoot)
                $assemblyPath = Join-Path $outputRoot "Alpha.Tests.dll"
                [IO.File]::WriteAllBytes($assemblyPath, [byte[]](1, 2, 3, 4))
                $suite = [pscustomobject]@{
                    SuiteName = "Alpha"
                    ProjectPath = "tests/Alpha/Alpha.Tests.csproj"
                    TestAssemblyOutputRoot = $outputRoot
                    TestAssemblyPath = $assemblyPath
                }
                $before = @(Get-CiTestAssemblySnapshot -Suites @($suite) -RepositoryRoot $root)
                [IO.File]::WriteAllBytes($assemblyPath, [byte[]](4, 3, 2, 1))
                $after = @(Get-CiTestAssemblySnapshot -Suites @($suite) -RepositoryRoot $root)
                [ordered]@{
                    Before = $before[0]
                    After = $after[0]
                    Errors = @(Get-CiTestAssemblyIdentityErrors -Before $before -After $after)
                } | ConvertTo-Json -Depth 4 -Compress
                """);

            Assert.Equal(0, process.ExitCode);
            using var result = JsonDocument.Parse(process.StandardOutput);
            Assert.Equal(
                result.RootElement.GetProperty("Before").GetProperty("Length").GetInt64(),
                result.RootElement.GetProperty("After").GetProperty("Length").GetInt64());
            Assert.NotEqual(
                result.RootElement.GetProperty("Before").GetProperty("Sha256").GetString(),
                result.RootElement.GetProperty("After").GetProperty("Sha256").GetString());
            var error = Assert.Single(result.RootElement.GetProperty("Errors").EnumerateArray());
            Assert.Contains("changed during execution", error.GetString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(repository, recursive: true);
        }
    }

    [Fact]
    public void Test_assembly_snapshot_fails_closed_when_the_dll_is_missing()
    {
        var repository = Path.Combine(
            Path.GetTempPath(),
            "ocf-ci-assembly-missing-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repository);
        try
        {
            var process = RunPowerShell(
                repository,
                """
                $root = $env:OCF_TEST_REPOSITORY_ROOT
                $outputRoot = Join-Path $root "tests/Alpha/bin/Release/net10.0"
                [void][IO.Directory]::CreateDirectory($outputRoot)
                $suite = [pscustomobject]@{
                    SuiteName = "Alpha"
                    ProjectPath = "tests/Alpha/Alpha.Tests.csproj"
                    TestAssemblyOutputRoot = $outputRoot
                    TestAssemblyPath = Join-Path $outputRoot "Alpha.Tests.dll"
                }
                Get-CiTestAssemblySnapshot -Suites @($suite) -RepositoryRoot $root
                """);

            Assert.NotEqual(0, process.ExitCode);
            Assert.Contains("does not exist; build before running tests", process.StandardError, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(repository, recursive: true);
        }
    }

    [Fact]
    public void Test_assembly_snapshot_rejects_a_reparse_output_path_on_windows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var repository = Path.Combine(
            Path.GetTempPath(),
            "ocf-ci-assembly-reparse-" + Guid.NewGuid().ToString("N"));
        var linkedOutput = Path.Combine(repository, "tests", "Alpha", "bin", "Release", "net10.0");
        Directory.CreateDirectory(Path.GetDirectoryName(linkedOutput)!);
        try
        {
            var process = RunPowerShell(
                repository,
                """
                $root = $env:OCF_TEST_REPOSITORY_ROOT
                $realOutput = Join-Path $root "real-output"
                [void][IO.Directory]::CreateDirectory($realOutput)
                [IO.File]::WriteAllBytes((Join-Path $realOutput "Alpha.Tests.dll"), [byte[]](1, 2, 3, 4))
                $linkedOutput = Join-Path $root "tests/Alpha/bin/Release/net10.0"
                New-Item -ItemType Junction -Path $linkedOutput -Target $realOutput | Out-Null
                $suite = [pscustomobject]@{
                    SuiteName = "Alpha"
                    ProjectPath = "tests/Alpha/Alpha.Tests.csproj"
                    TestAssemblyOutputRoot = $linkedOutput
                    TestAssemblyPath = Join-Path $linkedOutput "Alpha.Tests.dll"
                }
                Get-CiTestAssemblySnapshot -Suites @($suite) -RepositoryRoot $root
                """);

            Assert.NotEqual(0, process.ExitCode);
            Assert.Contains("reparse point", process.StandardError, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(linkedOutput))
            {
                Directory.Delete(linkedOutput);
            }

            Directory.Delete(repository, recursive: true);
        }
    }

    private static JsonDocument RunEvidenceScenario(string repository, string mutation)
    {
        var script = $$"""
            $validTrx = @'
            <?xml version="1.0" encoding="utf-8"?>
            <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
              <Results>
                <UnitTestResult testId="00000000-0000-0000-0000-000000000001" testName="__SUITE__.Synthetic_pass" outcome="Passed" />
              </Results>
              <TestDefinitions>
                <UnitTest id="00000000-0000-0000-0000-000000000001" storage="__ASSEMBLY__" name="__SUITE__.Synthetic_pass" />
              </TestDefinitions>
              <ResultSummary outcome="Completed">
                <Counters total="1" executed="1" passed="1" failed="0" error="0" timeout="0" aborted="0" inconclusive="0" passedButRunAborted="0" notRunnable="0" notExecuted="0" disconnected="0" warning="0" completed="0" inProgress="0" pending="0" />
              </ResultSummary>
            </TestRun>
            '@
            $failedTrx = @'
            <?xml version="1.0" encoding="utf-8"?>
            <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
              <Results>
                <UnitTestResult testId="00000000-0000-0000-0000-000000000001" testName="__SUITE__.Synthetic_failure" outcome="Failed" />
              </Results>
              <TestDefinitions>
                <UnitTest id="00000000-0000-0000-0000-000000000001" storage="__ASSEMBLY__" name="__SUITE__.Synthetic_failure" />
              </TestDefinitions>
              <ResultSummary outcome="Completed">
                <Counters total="1" executed="1" passed="0" failed="1" error="0" timeout="0" aborted="0" inconclusive="0" passedButRunAborted="0" notRunnable="0" notExecuted="0" disconnected="0" warning="0" completed="0" inProgress="0" pending="0" />
              </ResultSummary>
            </TestRun>
            '@
            $validCoverage = @'
            <?xml version="1.0" encoding="utf-8"?>
            <coverage line-rate="1" branch-rate="1" lines-covered="1" lines-valid="1">
              <packages>
                <package name="Synthetic" line-rate="1" branch-rate="1">
                  <classes>
                    <class name="Synthetic.Type" filename="Synthetic.cs" line-rate="1" branch-rate="1">
                      <lines><line number="1" hits="1" /></lines>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            '@
            function Write-ValidTrx([string]$Path, [string]$SuiteName, [string]$AssemblyPath) {
                $xmlAssembly = [Security.SecurityElement]::Escape($AssemblyPath)
                [IO.File]::WriteAllText(
                    $Path,
                    $validTrx.Replace("__SUITE__", $SuiteName).Replace("__ASSEMBLY__", $xmlAssembly))
            }
            function Write-FailedTrx([string]$Path, [string]$SuiteName, [string]$AssemblyPath) {
                $xmlAssembly = [Security.SecurityElement]::Escape($AssemblyPath)
                [IO.File]::WriteAllText(
                    $Path,
                    $failedTrx.Replace("__SUITE__", $SuiteName).Replace("__ASSEMBLY__", $xmlAssembly))
            }
            function Write-ValidCoverage([string]$Path) {
                [IO.File]::WriteAllText($Path, $validCoverage)
            }

            $inventory = @(Get-CiTestSuiteInventory -RepositoryRoot $env:OCF_TEST_REPOSITORY_ROOT)
            $baseline = @(Get-CiTestEvidenceBaseline -Suites $inventory)

            {{mutation}}

            $evidence = @(Get-CiTestEvidenceDelta -Suites $inventory -Baseline $baseline)
            $errors = @(Get-CiTestEvidenceCompletenessErrors -EvidenceDelta $evidence)
            [ordered]@{
                InventoryCount = $inventory.Count
                Evidence = @($evidence | ForEach-Object {
                        [ordered]@{
                            SuiteName = $_.SuiteName
                            TrxIsCurrent = $_.TrxIsCurrent
                            TrxValid = if ($null -eq $_.TrxValidation) {
                                $null
                            } else {
                                $_.TrxValidation.Valid
                            }
                            NewCoverageCount = @($_.NewCoveragePaths).Count
                            CoverageValid = @($_.NewCoverageEvidence | ForEach-Object {
                                    $_.Validation.Valid
                                })
                        }
                    })
                Errors = $errors
            } | ConvertTo-Json -Depth 6 -Compress
            """;
        var process = RunPowerShell(repository, script);
        Assert.True(
            process.ExitCode == 0,
            $"PowerShell evidence scenario failed with exit {process.ExitCode}:{Environment.NewLine}" +
            process.StandardError);
        return JsonDocument.Parse(process.StandardOutput);
    }

    private static PowerShellResult RunPowerShell(string repository, string script)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "pwsh",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add(
            "$ErrorActionPreference = 'Stop'; " +
            "Import-Module -Name $env:OCF_TEST_EVIDENCE_MODULE -Force; " +
            script);
        startInfo.Environment["OCF_TEST_EVIDENCE_MODULE"] = EvidenceModulePath;
        startInfo.Environment["OCF_TEST_REPOSITORY_ROOT"] = repository;

        using var process = new Process { StartInfo = startInfo };
        Assert.True(process.Start(), "The PowerShell evidence-fixture process did not start.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        var exited = process.WaitForExit(30_000);
        if (!exited)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }

        var output = standardOutput.GetAwaiter().GetResult();
        var error = standardError.GetAwaiter().GetResult();
        Assert.True(exited, "The PowerShell evidence-fixture process exceeded 30 seconds.");
        return new PowerShellResult(process.ExitCode, output, error);
    }

    private static string CreateSyntheticEvidenceRepository(params string[] suiteNames)
    {
        var repository = Path.Combine(
            Path.GetTempPath(),
            "ocf-ci-evidence-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repository);

        var projectLines = new List<string>();
        foreach (var suiteName in suiteNames)
        {
            var projectDirectory = Path.Combine(repository, "tests", suiteName);
            var testResultsRoot = Path.Combine(projectDirectory, "TestResults");
            Directory.CreateDirectory(testResultsRoot);
            File.WriteAllText(
                Path.Combine(projectDirectory, $"Foundry.Tests.{suiteName}.csproj"),
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <IsTestProject>true</IsTestProject>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);
            projectLines.Add(
                $"    <Project Path=\"tests/{suiteName}/Foundry.Tests.{suiteName}.csproj\" />");

            var staleTrx = Path.Combine(testResultsRoot, "test-results.trx");
            File.WriteAllText(staleTrx, "stale-trx-" + suiteName);
            var staleCoverageDirectory = Path.Combine(testResultsRoot, "stale-" + suiteName);
            Directory.CreateDirectory(staleCoverageDirectory);
            var staleCoverage = Path.Combine(staleCoverageDirectory, "coverage.cobertura.xml");
            File.WriteAllText(staleCoverage, "stale-coverage-" + suiteName);
            var future = DateTime.UtcNow.AddDays(7);
            File.SetLastWriteTimeUtc(staleTrx, future);
            File.SetLastWriteTimeUtc(staleCoverage, future);
        }

        File.WriteAllText(
            Path.Combine(repository, "OpenClassroomFoundry.slnx"),
            "<Solution>" + Environment.NewLine +
            "  <Folder Name=\"/tests/\">" + Environment.NewLine +
            string.Join(Environment.NewLine, projectLines) + Environment.NewLine +
            "  </Folder>" + Environment.NewLine +
            "</Solution>" + Environment.NewLine);
        return repository;
    }

    private static WorkflowStep GetNamedWorkflowStep(string[] lines, string name)
    {
        var marker = "      - name: " + name;
        var start = Assert.Single(
            Enumerable.Range(0, lines.Length),
            index => string.Equals(lines[index], marker, StringComparison.Ordinal));
        var end = start + 1;
        while (end < lines.Length
               && !lines[end].StartsWith("      - ", StringComparison.Ordinal))
        {
            end++;
        }

        return new WorkflowStep(
            start,
            end,
            string.Join(Environment.NewLine, lines[start..end]));
    }

    private static int GetNextWorkflowStepStart(string[] lines, WorkflowStep step)
    {
        Assert.True(step.EndIndex < lines.Length, "The workflow step has no following step.");
        Assert.StartsWith("      - ", lines[step.EndIndex], StringComparison.Ordinal);
        return step.EndIndex;
    }

    private sealed record WorkflowStep(int StartIndex, int EndIndex, string Text);

    private sealed record PowerShellResult(int ExitCode, string StandardOutput, string StandardError);

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "OpenClassroomFoundry.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root for CI runner tests.");
    }
}
