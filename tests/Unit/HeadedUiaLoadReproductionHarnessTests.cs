// SPDX-License-Identifier: GPL-3.0-or-later
namespace Foundry.Tests.Unit;

public class HeadedUiaLoadReproductionHarnessTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string Harness = File.ReadAllText(
        Path.Combine(RepositoryRoot, "tools", "run-headed-uia-load-repro.ps1"));
    private static readonly string EvidenceModule = File.ReadAllText(
        Path.Combine(RepositoryRoot, "tools", "LoadReproEvidence.psm1"));
    private static readonly string HeadedTests = File.ReadAllText(
        Path.Combine(RepositoryRoot, "tests", "UiAutomation", "HeadedUiaWalkTests.cs"));
    private static readonly string PilotTest = File.ReadAllText(
        Path.Combine(RepositoryRoot, "tests", "UiAutomation", "HeadedUiaWalkTests.PilotRehearsal.cs"));

    [Fact]
    public void Harness_runs_only_the_two_named_sightings_without_changing_the_probe_timeout()
    {
        Assert.Contains(
            "Foundry.Tests.UiAutomation.HeadedUiaWalkTests.PilotDay_dress_rehearsal_cold_start_to_reopened_booklet_and_low_ink_over_real_uia",
            Harness,
            StringComparison.Ordinal);
        Assert.Contains(
            "Foundry.Tests.UiAutomation.HeadedUiaWalkTests.Part3_Steps9to12_move_edit_and_approve_operate_through_uia_patterns",
            Harness,
            StringComparison.Ordinal);
        Assert.Contains("int timeoutMs = 20000", HeadedTests, StringComparison.Ordinal);
        Assert.DoesNotContain("timeoutMs", Harness, StringComparison.Ordinal);
        Assert.Contains("ExistingProbeTimeoutMilliseconds = 20000", Harness, StringComparison.Ordinal);
    }

    [Fact]
    public void Harness_is_bounded_and_applies_controlled_cpu_and_memory_contention()
    {
        Assert.Contains("[ValidateRange(1, 10)]", Harness, StringComparison.Ordinal);
        Assert.Contains("[ValidateRange(1, 8)]", Harness, StringComparison.Ordinal);
        Assert.Contains("[ValidateRange(32, 1024)]", Harness, StringComparison.Ordinal);
        Assert.Contains("[ValidateRange(60, 600)]", Harness, StringComparison.Ordinal);
        Assert.Contains("Start-ControlledContention", Harness, StringComparison.Ordinal);
        Assert.Contains("$process.WaitForExit($LimitSeconds * 1000)", Harness, StringComparison.Ordinal);
        Assert.Contains("$exitCode = 124", Harness, StringComparison.Ordinal);
    }

    [Fact]
    public void A_timeout_requires_a_successful_tree_request_and_observed_launcher_exit_before_continuing()
    {
        Assert.Contains("$taskkillResult = Invoke-LoadReproBoundedTaskKill", Harness, StringComparison.Ordinal);
        Assert.Contains("$terminationRequestStarted = $taskkillResult.Started", Harness, StringComparison.Ordinal);
        Assert.Contains("$terminationRequestTimedOut = $taskkillResult.TimedOut", Harness, StringComparison.Ordinal);
        Assert.Contains("$terminationRequestExitCode = $taskkillResult.ExitCode", Harness, StringComparison.Ordinal);
        Assert.Contains(
            "$terminationHelperExitObserved = $taskkillResult.HelperExitObserved",
            Harness,
            StringComparison.Ordinal);
        Assert.Contains("$terminationRequestSucceeded = $taskkillResult.Started", Harness, StringComparison.Ordinal);
        Assert.Contains("-and -not $taskkillResult.TimedOut", Harness, StringComparison.Ordinal);
        Assert.Contains("-and $taskkillResult.HelperExitObserved", Harness, StringComparison.Ordinal);
        Assert.Contains("-and $taskkillResult.ExitCode -eq 0", Harness, StringComparison.Ordinal);
        Assert.Contains("$launcherExitObserved = $process.WaitForExit(10000)", Harness, StringComparison.Ordinal);
        Assert.Contains(
            "$safeToContinue = $terminationRequestSucceeded -and $launcherExitObserved",
            Harness,
            StringComparison.Ordinal);
        Assert.Contains("TerminationRequestStarted = $terminationRequestStarted", Harness, StringComparison.Ordinal);
        Assert.Contains("TerminationRequestTimedOut = $terminationRequestTimedOut", Harness, StringComparison.Ordinal);
        Assert.Contains("TerminationRequestExitCode = $terminationRequestExitCode", Harness, StringComparison.Ordinal);
        Assert.Contains(
            "TerminationHelperExitObserved = $terminationHelperExitObserved",
            Harness,
            StringComparison.Ordinal);
        Assert.Contains("TerminationRequestSucceeded = $terminationRequestSucceeded", Harness, StringComparison.Ordinal);
        Assert.Contains("LauncherExitObserved = $launcherExitObserved", Harness, StringComparison.Ordinal);
        Assert.Contains("SafeToContinue = $safeToContinue", Harness, StringComparison.Ordinal);
        Assert.Contains("TerminationRequestStarted = $boundedRun.TerminationRequestStarted", Harness, StringComparison.Ordinal);
        Assert.Contains("TerminationRequestTimedOut = $boundedRun.TerminationRequestTimedOut", Harness, StringComparison.Ordinal);
        Assert.Contains("TerminationRequestExitCode = $boundedRun.TerminationRequestExitCode", Harness, StringComparison.Ordinal);
        Assert.Contains(
            "TerminationHelperExitObserved = $boundedRun.TerminationHelperExitObserved",
            Harness,
            StringComparison.Ordinal);
        Assert.Contains("TerminationRequestSucceeded = $boundedRun.TerminationRequestSucceeded", Harness, StringComparison.Ordinal);
        Assert.Contains("LauncherExitObserved = $boundedRun.LauncherExitObserved", Harness, StringComparison.Ordinal);
        Assert.Contains("SafeToContinue = $boundedRun.SafeToContinue", Harness, StringComparison.Ordinal);
        Assert.Contains("if (-not $boundedRun.SafeToContinue)", Harness, StringComparison.Ordinal);
        Assert.Contains("descendant exit is not independently enumerated", Harness, StringComparison.Ordinal);
        Assert.DoesNotContain("process tree was terminated", Harness, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("process tree did not exit", Harness, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("taskkill.exe", Harness, StringComparison.OrdinalIgnoreCase);

        var helperStart = EvidenceModule.IndexOf(
            "function Invoke-LoadReproBoundedTaskKill",
            StringComparison.Ordinal);
        var helperEnd = EvidenceModule.IndexOf("function Enter-LoadReproEvidenceLock", helperStart, StringComparison.Ordinal);
        var helper = EvidenceModule[helperStart..helperEnd];
        Assert.Contains("[int]$LimitMilliseconds = 10000", helper, StringComparison.Ordinal);
        Assert.Contains("[int]$CleanupLimitMilliseconds = 2000", helper, StringComparison.Ordinal);
        Assert.Contains("$killProcess.Start()", helper, StringComparison.Ordinal);
        Assert.Contains("$killProcess.Kill($true)", helper, StringComparison.Ordinal);
        Assert.Contains("HelperExitObserved = $helperExitObserved", helper, StringComparison.Ordinal);
        Assert.DoesNotContain("RedirectStandardOutput", helper, StringComparison.Ordinal);
        Assert.DoesNotContain("RedirectStandardError", helper, StringComparison.Ordinal);

        var kill = Harness.IndexOf(
            "$taskkillResult = Invoke-LoadReproBoundedTaskKill",
            StringComparison.Ordinal);
        var requestSuccess = Harness.IndexOf(
            "$terminationRequestSucceeded = $taskkillResult.Started",
            StringComparison.Ordinal);
        var wait = Harness.IndexOf("$launcherExitObserved = $process.WaitForExit(10000)", StringComparison.Ordinal);
        var safe = Harness.IndexOf(
            "$safeToContinue = $terminationRequestSucceeded -and $launcherExitObserved",
            StringComparison.Ordinal);
        var dispose = Harness.IndexOf("$process.Dispose()", StringComparison.Ordinal);
        var receipt = Harness.IndexOf("$record | ConvertTo-Json", StringComparison.Ordinal);
        var abort = Harness.IndexOf("if (-not $boundedRun.SafeToContinue)", StringComparison.Ordinal);
        Assert.True(kill < requestSuccess && requestSuccess < wait && wait < safe && safe < dispose);
        Assert.True(receipt < abort, "The unconfirmed termination must be retained before the batch aborts.");
    }

    [Fact]
    public void Every_repetition_records_boundary_liveness_and_refuses_missing_load_evidence()
    {
        Assert.Contains("ExpectedWorkerCount = $ExpectedWorkerCount", Harness, StringComparison.Ordinal);
        Assert.Contains("ObservedWorkerCount = $states.Count", Harness, StringComparison.Ordinal);
        Assert.Contains("RunningWorkerCount = $running", Harness, StringComparison.Ordinal);
        Assert.Contains("NonRunningWorkerCount = $states.Count - $running", Harness, StringComparison.Ordinal);
        Assert.Contains("AllWorkersRunning = $ExpectedWorkerCount -gt 0", Harness, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(Harness, "$contentionBefore = Get-ControlledContentionSnapshot"));
        Assert.Equal(1, CountOccurrences(Harness, "$contentionAfter = Get-ControlledContentionSnapshot"));

        var before = Harness.IndexOf(
            "$contentionBefore = Get-ControlledContentionSnapshot",
            StringComparison.Ordinal);
        var namedRuns = Harness.IndexOf("foreach ($testCase in $testCases)", StringComparison.Ordinal);
        var after = Harness.IndexOf(
            "$contentionAfter = Get-ControlledContentionSnapshot",
            StringComparison.Ordinal);
        var stop = Harness.IndexOf("Stop-ControlledContention -Jobs $loadJobs", StringComparison.Ordinal);
        Assert.True(before < namedRuns && namedRuns < after && after < stop);

        Assert.Contains("LoadConditionVerified = $contentionBefore.AllWorkersRunning", Harness, StringComparison.Ordinal);
        Assert.Contains("ContentionLiveness = $contentionLiveness", Harness, StringComparison.Ordinal);
        Assert.Contains("Where-Object { -not $_.LoadConditionVerified }", Harness, StringComparison.Ordinal);
        Assert.Contains("$missingResults = $results.Count -ne ($Repetitions * $testCases.Count)", Harness, StringComparison.Ordinal);
        Assert.Contains("this does not measure CPU-utilization magnitude", Harness, StringComparison.Ordinal);
    }

    [Fact]
    public void Evidence_is_locked_and_binds_stable_source_and_exact_built_output_around_the_batch()
    {
        Assert.Contains("Import-Module -Name $evidenceModulePath -Force", Harness, StringComparison.Ordinal);
        Assert.Contains("Enter-LoadReproEvidenceLock", Harness, StringComparison.Ordinal);
        Assert.Contains("New-LoadReproEvidenceDirectory", Harness, StringComparison.Ordinal);
        Assert.Contains("Get-LoadReproRepositoryState", Harness, StringComparison.Ordinal);
        Assert.Contains("Assert-LoadReproCleanRepositoryState", Harness, StringComparison.Ordinal);
        Assert.Contains("RepositoryCommit = $repositoryCommit", Harness, StringComparison.Ordinal);
        Assert.Contains("TreeStateBefore = \"clean\"", Harness, StringComparison.Ordinal);
        Assert.Contains("RepositoryStateBefore = $repositoryStateBefore", Harness, StringComparison.Ordinal);
        Assert.Contains("RepositoryStateAfter = $repositoryStateAfter", Harness, StringComparison.Ordinal);
        Assert.Contains("DotnetSdk = $dotnetSdk", Harness, StringComparison.Ordinal);
        Assert.Contains("RestorePerformed = $true", Harness, StringComparison.Ordinal);
        Assert.Contains("BuildPerformed = $true", Harness, StringComparison.Ordinal);
        Assert.Contains("HarnessSha256 = $harnessSha256", Harness, StringComparison.Ordinal);
        Assert.Contains("TestAssemblySha256 = $testAssemblySha256", Harness, StringComparison.Ordinal);
        Assert.Contains("Get-LoadReproOutputIdentity", Harness, StringComparison.Ordinal);
        Assert.Contains("test-output-identity-before.json", Harness, StringComparison.Ordinal);
        Assert.Contains("test-output-identity-after.json", Harness, StringComparison.Ordinal);
        Assert.Contains("Get-LoadReproIdentityErrors", Harness, StringComparison.Ordinal);
        Assert.Contains("SourceAndOutputIdentityStable = $identityStable", Harness, StringComparison.Ordinal);
        Assert.Contains("-or -not $identityStable", Harness, StringComparison.Ordinal);
        Assert.Contains("HeldThroughDurableSummary = $true", Harness, StringComparison.Ordinal);
        Assert.Contains("Write-LoadReproJsonFile", Harness, StringComparison.Ordinal);
        Assert.Contains("do not prove compiler/source correspondence", Harness, StringComparison.Ordinal);
        Assert.DoesNotContain("SkipBuild", Harness, StringComparison.Ordinal);
        Assert.Contains(
            "\"restore\", $testProject, \"--locked-mode\", \"--configfile\", \"NuGet.config\"",
            Harness,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"build\", $testProject, \"-c\", \"Release\", \"--no-restore\", \"--no-incremental\"",
            Harness,
            StringComparison.Ordinal);

        Assert.Contains("[IO.FileShare]::None", EvidenceModule, StringComparison.Ordinal);
        Assert.Contains(".load-repro-evidence.lock", EvidenceModule, StringComparison.Ordinal);
        Assert.Contains("function New-LoadReproEvidenceDirectory", EvidenceModule, StringComparison.Ordinal);
        Assert.Contains(
            "-Description \"load-reproduction evidence base\"",
            EvidenceModule,
            StringComparison.Ordinal);
        Assert.Contains(
            "-Description \"load-reproduction evidence run directory\"",
            EvidenceModule,
            StringComparison.Ordinal);
        Assert.Contains("Get-CiRepositoryState -RepositoryRoot $RepositoryRoot", EvidenceModule, StringComparison.Ordinal);
        Assert.Contains("Get-ChildItem -LiteralPath $resolvedOutputRoot -File -Recurse -Force", EvidenceModule, StringComparison.Ordinal);
        Assert.Contains("ManifestSha256 = Get-LoadReproStringSha256", EvidenceModule, StringComparison.Ordinal);
        Assert.Contains("[IO.FileMode]::CreateNew", EvidenceModule, StringComparison.Ordinal);
        Assert.Contains("$stream.Flush($true)", EvidenceModule, StringComparison.Ordinal);

        var lockAcquisition = Harness.IndexOf("$evidenceLock = Enter-LoadReproEvidenceLock", StringComparison.Ordinal);
        var sourceBefore = Harness.IndexOf("$repositoryStateBefore =", StringComparison.Ordinal);
        var evidenceDirectory = Harness.IndexOf(
            "$evidenceDirectory = New-LoadReproEvidenceDirectory",
            StringComparison.Ordinal);
        var build = Harness.IndexOf("\"build\", $testProject", StringComparison.Ordinal);
        var outputBefore = Harness.IndexOf("$outputIdentityBefore =", StringComparison.Ordinal);
        var repetitions = Harness.IndexOf("for ($repetition = 1", StringComparison.Ordinal);
        var outputAfter = Harness.IndexOf("$outputIdentityAfter =", StringComparison.Ordinal);
        var summaryWrite = Harness.IndexOf("-Description \"headed UIA load-reproduction summary\"", StringComparison.Ordinal);
        var lockRelease = Harness.IndexOf("$evidenceLock.Stream.Dispose()", StringComparison.Ordinal);
        Assert.True(
            lockAcquisition < sourceBefore
            && sourceBefore < evidenceDirectory
            && evidenceDirectory < build
            && build < outputBefore
            && outputBefore < repetitions
            && repetitions < outputAfter
            && outputAfter < summaryWrite
            && summaryWrite < lockRelease,
            "The shared lock must span clean-source preflight, build, repetitions, post identity, and durable summary.");
        Assert.DoesNotContain(
            "New-Item -ItemType Directory -Path $evidenceRoot",
            Harness,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Each_repetition_retains_the_named_result_and_content_free_failure_evidence()
    {
        Assert.Equal(1, CountOccurrences(Harness, "\"test\", $testProject,"));
        Assert.Contains("FullyQualifiedName = $testCase.FullyQualifiedName", Harness, StringComparison.Ordinal);
        Assert.Contains("Repetition = $repetition", Harness, StringComparison.Ordinal);
        Assert.Contains("ExitCode = $boundedRun.ExitCode", Harness, StringComparison.Ordinal);
        Assert.Contains("FailureMessage = $failureMessage", Harness, StringComparison.Ordinal);
        Assert.Contains("StandardOutputFile", Harness, StringComparison.Ordinal);
        Assert.Contains("StandardErrorFile", Harness, StringComparison.Ordinal);
        Assert.Contains("*[local-name()='Output']/*[local-name()='ErrorInfo']/*[local-name()='Message']", Harness, StringComparison.Ordinal);
        Assert.Contains("Content-free diagnostic snapshot", HeadedTests, StringComparison.Ordinal);
        Assert.Contains("no action retry issued", Harness, StringComparison.Ordinal);
        Assert.Contains("non-reproductions, not a diagnosis", Harness, StringComparison.Ordinal);
    }

    [Fact]
    public void Headed_processes_inherit_only_disposable_test_storage()
    {
        Assert.Contains("$env:TEMP = $scratchRoot", Harness, StringComparison.Ordinal);
        Assert.Contains("$env:TMP = $scratchRoot", Harness, StringComparison.Ordinal);
        Assert.Contains("ocf-rehearsal-", PilotTest, StringComparison.Ordinal);
        Assert.Contains("ProjectLibraryRootConfiguration.Switch", PilotTest, StringComparison.Ordinal);
        Assert.Contains("The library lives in a disposable directory", PilotTest, StringComparison.Ordinal);
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
            ?? throw new InvalidOperationException("Could not locate repository root for headed-load harness tests.");
    }
}
