// SPDX-License-Identifier: GPL-3.0-or-later
namespace Foundry.Tests.Unit;

/// <summary>
/// Binds the console-signal load-reproduction instrument to its contract: the
/// exact named sightings, the real co-scheduled PDF exercise, bounded fresh
/// processes, retained content-free evidence, and no change to any deadline.
/// </summary>
public class ConsoleSignalLoadReproductionHarnessTests
{
    private const string ConsoleCase =
        "Foundry.Tests.Integration.ProjectUpgradeOperatorHostTests.Real_console_ctrl_c_cancels_the_process_and_cleans_the_synthetic_batch";
    private const string FlashCapLateStartCase =
        "Foundry.Tests.Integration.FlashCapCameraSourceTests.A_late_successful_start_is_stopped_and_disposed_again_after_immediate_cleanup";
    private const string FlashCapSharedLockCase =
        "Foundry.Tests.Integration.FlashCapCameraSourceTests.A_shared_lifecycle_lock_can_prevent_confirmed_shutdown_but_capture_still_settles_bounded";
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string Harness = File.ReadAllText(
        Path.Combine(RepositoryRoot, "tools", "run-console-signal-load-repro.ps1"));
    private static readonly string EvidenceModule = File.ReadAllText(
        Path.Combine(RepositoryRoot, "tools", "LoadReproEvidence.psm1"));
    private static readonly string OperatorHostTests = File.ReadAllText(
        Path.Combine(RepositoryRoot, "tests", "Integration", "ProjectUpgradeOperatorHostTests.cs"));
    private static readonly string FlashCapTests = File.ReadAllText(
        Path.Combine(RepositoryRoot, "tests", "Integration", "FlashCapCameraSourceTests.cs"));
    private static readonly string EdgePdfTests = File.ReadAllText(
        Path.Combine(RepositoryRoot, "tests", "Integration", "EdgePdfExporterTests.cs"));
    private static readonly string Sender = string.Concat(
        Directory.EnumerateFiles(Path.Combine(RepositoryRoot, "tools", "ConsoleControlSignalSender"), "*.cs", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.Ordinal)
            .Select(File.ReadAllText));

    [Fact]
    public void Harness_runs_exactly_the_three_bounded_native_lifecycle_sightings_in_one_fresh_process()
    {
        Assert.Contains(ConsoleCase, Harness, StringComparison.Ordinal);
        Assert.Contains(FlashCapLateStartCase, Harness, StringComparison.Ordinal);
        Assert.Contains(FlashCapSharedLockCase, Harness, StringComparison.Ordinal);
        Assert.Contains("Real_console_ctrl_c_cancels_the_process_and_cleans_the_synthetic_batch()", OperatorHostTests, StringComparison.Ordinal);
        Assert.Contains("A_late_successful_start_is_stopped_and_disposed_again_after_immediate_cleanup()", FlashCapTests, StringComparison.Ordinal);
        Assert.Contains("A_shared_lifecycle_lock_can_prevent_confirmed_shutdown_but_capture_still_settles_bounded()", FlashCapTests, StringComparison.Ordinal);
        Assert.Contains("[Collection(BoundedNativeLifecycleTestGroup.Name)]", OperatorHostTests, StringComparison.Ordinal);
        Assert.Contains("[Collection(BoundedNativeLifecycleTestGroup.Name)]", FlashCapTests, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(Harness, "\"test\", $testProject,"));
        Assert.Contains("\"--filter\", (Join-TestFilter -Cases $namedCases)", Harness, StringComparison.Ordinal);
        Assert.Contains("FreshProcessPerRole = $true", Harness, StringComparison.Ordinal);
        Assert.Contains("this harness has no", Harness, StringComparison.Ordinal);
        Assert.Contains("action-level or assertion retry path", Harness, StringComparison.Ordinal);
    }

    [Fact]
    public void Harness_co_schedules_the_real_pdf_exercise_and_measures_overlap_from_trx_intervals()
    {
        Assert.Contains(
            "Foundry.Tests.Integration.EdgePdfExporterTests.Two_exports_complete_concurrently_with_isolated_edge_profiles",
            Harness,
            StringComparison.Ordinal);
        Assert.Contains(
            "Foundry.Tests.Integration.EdgePdfExporterTests.The_real_edge_pipeline_resolves_an_asset_and_produces_a_pdf",
            Harness,
            StringComparison.Ordinal);
        Assert.Contains(
            "Foundry.Tests.Integration.EdgePdfExporterTests.An_approved_artifact_becomes_a_real_pdf",
            Harness,
            StringComparison.Ordinal);
        Assert.Contains("Two_exports_complete_concurrently_with_isolated_edge_profiles()", EdgePdfTests, StringComparison.Ordinal);
        Assert.Contains("The_real_edge_pipeline_resolves_an_asset_and_produces_a_pdf()", EdgePdfTests, StringComparison.Ordinal);
        Assert.Contains("An_approved_artifact_becomes_a_real_pdf()", EdgePdfTests, StringComparison.Ordinal);
        Assert.Contains("if (EdgePdfExporter.FindEdge() is null)", EdgePdfTests, StringComparison.Ordinal);
        Assert.Contains("function Get-EdgeExecutablePath", Harness, StringComparison.Ordinal);
        Assert.Contains("\"Microsoft\\Edge\\Application\\msedge.exe\"", Harness, StringComparison.Ordinal);
        Assert.Contains("the co-scheduled PDF exercise would silently skip", Harness, StringComparison.Ordinal);
        Assert.Contains("\"--filter\", (Join-TestFilter -Cases $exerciseCases)", Harness, StringComparison.Ordinal);
        Assert.Contains("$exerciseAliveAtNamedStart = -not $exerciseStarted.Process.HasExited", Harness, StringComparison.Ordinal);
        Assert.Contains("function Get-IntervalOverlapMilliseconds", Harness, StringComparison.Ordinal);
        Assert.Contains("OverlapWithExerciseMilliseconds = $overlap", Harness, StringComparison.Ordinal);
        Assert.Contains("CoScheduled = ($null -ne $overlap -and $overlap -gt 0)", Harness, StringComparison.Ordinal);
        Assert.Contains("-and $exerciseAliveAtNamedStart", Harness, StringComparison.Ordinal);
        Assert.Contains("-and $exerciseValid", Harness, StringComparison.Ordinal);
        Assert.Contains("-and $allNamedCoScheduled", Harness, StringComparison.Ordinal);
        Assert.Contains("does not reproduce shared thread-pool or xUnit scheduling", Harness, StringComparison.Ordinal);

        var exerciseStart = Harness.IndexOf("$exerciseStarted = Start-BoundedProcess", StringComparison.Ordinal);
        var namedStart = Harness.IndexOf("$namedStarted = Start-BoundedProcess", StringComparison.Ordinal);
        var aliveCheck = Harness.IndexOf("$exerciseAliveAtNamedStart = -not $exerciseStarted.Process.HasExited", StringComparison.Ordinal);
        var namedWait = Harness.IndexOf("$namedRun = Complete-BoundedProcess -Started $namedStarted", StringComparison.Ordinal);
        var exerciseWait = Harness.IndexOf("$exerciseRun = Complete-BoundedProcess -Started $exerciseStarted", StringComparison.Ordinal);
        Assert.True(
            exerciseStart > 0 && exerciseStart < namedStart && namedStart < aliveCheck && aliveCheck < namedWait && namedWait < exerciseWait,
            "The exercise must start first, the named process at once, liveness be observed, and both processes be waited for.");
    }

    [Fact]
    public void Harness_changes_no_test_or_product_deadline()
    {
        Assert.Contains("var readinessTimeout = TimeSpan.FromSeconds(15);", Sender, StringComparison.Ordinal);
        Assert.Contains("host.Wait(TimeSpan.FromSeconds(15))", OperatorHostTests, StringComparison.Ordinal);
        Assert.Contains("WaitAsync(TimeSpan.FromSeconds(5))", FlashCapTests, StringComparison.Ordinal);
        Assert.DoesNotContain("FromSeconds", Harness, StringComparison.Ordinal);
        Assert.DoesNotContain("readinessTimeout", Harness, StringComparison.Ordinal);
        Assert.DoesNotContain("timeoutMs", Harness, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OCF_CONSOLE", Harness, StringComparison.Ordinal);
        Assert.Contains("this harness sets no test or product deadline", Harness, StringComparison.Ordinal);
        Assert.Contains("[ValidateRange(1, 10)]", Harness, StringComparison.Ordinal);
        Assert.Contains("[ValidateRange(1, 8)]", Harness, StringComparison.Ordinal);
        Assert.Contains("[ValidateRange(32, 1024)]", Harness, StringComparison.Ordinal);
        Assert.Contains("[ValidateRange(60, 600)]", Harness, StringComparison.Ordinal);
    }

    [Fact]
    public void A_timeout_requires_a_successful_tree_request_and_observed_launcher_exit_before_continuing()
    {
        Assert.Contains("$timedOut = -not $process.WaitForExit([int]$remainingMilliseconds)", Harness, StringComparison.Ordinal);
        Assert.Contains("$exitCode = 124", Harness, StringComparison.Ordinal);
        Assert.Contains("$taskkillResult = Invoke-LoadReproBoundedTaskKill", Harness, StringComparison.Ordinal);
        Assert.Contains("$terminationRequestSucceeded = $taskkillResult.Started", Harness, StringComparison.Ordinal);
        Assert.Contains("-and -not $taskkillResult.TimedOut", Harness, StringComparison.Ordinal);
        Assert.Contains("-and $taskkillResult.HelperExitObserved", Harness, StringComparison.Ordinal);
        Assert.Contains("-and $taskkillResult.ExitCode -eq 0", Harness, StringComparison.Ordinal);
        Assert.Contains("$launcherExitObserved = $process.WaitForExit(10000)", Harness, StringComparison.Ordinal);
        Assert.Contains(
            "$safeToContinue = $terminationRequestSucceeded -and $launcherExitObserved",
            Harness,
            StringComparison.Ordinal);
        Assert.Contains("descendant exit is not independently enumerated", Harness, StringComparison.Ordinal);
        Assert.DoesNotContain("taskkill.exe", Harness, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("process tree was terminated", Harness, StringComparison.OrdinalIgnoreCase);

        var receipt = Harness.IndexOf("$record | ConvertTo-Json", StringComparison.Ordinal);
        var abort = Harness.IndexOf("if (-not $namedRun.SafeToContinue -or -not $exerciseRun.SafeToContinue)", StringComparison.Ordinal);
        Assert.True(receipt > 0 && receipt < abort, "The unconfirmed termination must be retained before the batch aborts.");

        var helperStart = EvidenceModule.IndexOf("function Invoke-LoadReproBoundedTaskKill", StringComparison.Ordinal);
        var helperEnd = EvidenceModule.IndexOf("function Enter-LoadReproEvidenceLock", helperStart, StringComparison.Ordinal);
        var helper = EvidenceModule[helperStart..helperEnd];
        Assert.Contains("[int]$LimitMilliseconds = 10000", helper, StringComparison.Ordinal);
        Assert.Contains("$killProcess.Kill($true)", helper, StringComparison.Ordinal);
    }

    [Fact]
    public void Evidence_is_locked_and_binds_stable_source_and_exact_built_output_around_the_batch()
    {
        Assert.Contains("Import-Module -Name $evidenceModulePath -Force", Harness, StringComparison.Ordinal);
        Assert.Contains("-HarnessName \"console-signal-load-repro\"", Harness, StringComparison.Ordinal);
        Assert.Contains("-EvidenceBaseName \"console-signal-load-repro\"", Harness, StringComparison.Ordinal);
        Assert.Contains("\"uia-load-repro\", \"image-load-repro\", \"console-signal-load-repro\"", EvidenceModule, StringComparison.Ordinal);
        Assert.Contains("Assert-LoadReproCleanRepositoryState", Harness, StringComparison.Ordinal);
        Assert.Contains("RepositoryCommit = $repositoryCommit", Harness, StringComparison.Ordinal);
        Assert.Contains("TreeStateBefore = \"clean\"", Harness, StringComparison.Ordinal);
        Assert.Contains("HarnessSha256 = $harnessSha256", Harness, StringComparison.Ordinal);
        Assert.Contains("TestAssemblySha256 = $testAssemblySha256", Harness, StringComparison.Ordinal);
        Assert.Contains("Get-LoadReproOutputIdentity", Harness, StringComparison.Ordinal);
        Assert.Contains("Get-LoadReproIdentityErrors", Harness, StringComparison.Ordinal);
        Assert.Contains("SourceAndOutputIdentityStable = $identityStable", Harness, StringComparison.Ordinal);
        Assert.Contains("-or -not $identityStable", Harness, StringComparison.Ordinal);
        Assert.Contains("HeldThroughDurableSummary = $true", Harness, StringComparison.Ordinal);
        Assert.Contains("do not prove compiler/source correspondence", Harness, StringComparison.Ordinal);
        Assert.Contains(
            "\"restore\", $testProject, \"--locked-mode\", \"--configfile\", \"NuGet.config\"",
            Harness,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"build\", $testProject, \"-c\", \"Release\", \"--no-restore\", \"--no-incremental\"",
            Harness,
            StringComparison.Ordinal);
        Assert.DoesNotContain("SkipBuild", Harness, StringComparison.Ordinal);

        var lockAcquisition = Harness.IndexOf("$evidenceLock = Enter-LoadReproEvidenceLock", StringComparison.Ordinal);
        var sourceBefore = Harness.IndexOf("$repositoryStateBefore =", StringComparison.Ordinal);
        var evidenceDirectory = Harness.IndexOf("$evidenceDirectory = New-LoadReproEvidenceDirectory", StringComparison.Ordinal);
        var build = Harness.IndexOf("\"build\", $testProject", StringComparison.Ordinal);
        var outputBefore = Harness.IndexOf("$outputIdentityBefore =", StringComparison.Ordinal);
        var repetitions = Harness.IndexOf("for ($repetition = 1", StringComparison.Ordinal);
        var outputAfter = Harness.IndexOf("$outputIdentityAfter =", StringComparison.Ordinal);
        var summaryWrite = Harness.IndexOf("-Description \"console-signal load-reproduction summary\"", StringComparison.Ordinal);
        var lockRelease = Harness.IndexOf("$evidenceLock.Stream.Dispose()", StringComparison.Ordinal);
        Assert.True(
            lockAcquisition > 0
            && lockAcquisition < sourceBefore
            && sourceBefore < evidenceDirectory
            && evidenceDirectory < build
            && build < outputBefore
            && outputBefore < repetitions
            && repetitions < outputAfter
            && outputAfter < summaryWrite
            && summaryWrite < lockRelease,
            "The shared lock must span clean-source preflight, build, repetitions, post identity, and durable summary.");
    }

    [Fact]
    public void Every_repetition_retains_content_free_results_and_refuses_missing_or_invalid_load()
    {
        Assert.Contains("$env:TEMP = $exerciseScratch", Harness, StringComparison.Ordinal);
        Assert.Contains("$env:TEMP = $namedScratch", Harness, StringComparison.Ordinal);
        Assert.Contains("*[local-name()='Output']/*[local-name()='ErrorInfo']/*[local-name()='Message']", Harness, StringComparison.Ordinal);
        Assert.Contains("FailureMessage = $failureMessage", Harness, StringComparison.Ordinal);
        Assert.Contains("DurationMilliseconds = $namedResult.DurationMilliseconds", Harness, StringComparison.Ordinal);
        Assert.Contains("ExpectedWorkerCount = $ExpectedWorkerCount", Harness, StringComparison.Ordinal);
        Assert.Contains("AllWorkersRunning = $ExpectedWorkerCount -gt 0", Harness, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(Harness, "$contentionBefore = Get-ControlledContentionSnapshot"));
        Assert.Equal(1, CountOccurrences(Harness, "$contentionAfter = Get-ControlledContentionSnapshot"));
        Assert.Contains("LoadConditionVerified = $contentionBefore.AllWorkersRunning", Harness, StringComparison.Ordinal);
        Assert.Contains("Where-Object { -not $_.LoadConditionVerified }", Harness, StringComparison.Ordinal);
        Assert.Contains("$missingResults = $results.Count -ne $Repetitions", Harness, StringComparison.Ordinal);
        Assert.Contains("this does not measure CPU-utilization magnitude", Harness, StringComparison.Ordinal);
        Assert.Contains("non-reproductions of the console-signal and FlashCap lifecycle sightings, not a diagnosis, cure, or closure", Harness, StringComparison.Ordinal);
        Assert.DoesNotContain("Documents", Harness, StringComparison.OrdinalIgnoreCase);
    }

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
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpenClassroomFoundry.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root for console-signal harness tests.");
    }
}
