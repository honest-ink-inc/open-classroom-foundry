// SPDX-License-Identifier: GPL-3.0-or-later
namespace Foundry.Tests.Unit;

public class ImageLoadReproductionHarnessTests
{
    private const string ExactTestName =
        "Foundry.Tests.Integration.ImageNormalizerTests.A_burned_region_destroys_the_pixels_beneath_it";
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string Harness = File.ReadAllText(
        Path.Combine(RepositoryRoot, "tools", "run-image-load-repro.ps1"));
    private static readonly string EvidenceModule = File.ReadAllText(
        Path.Combine(RepositoryRoot, "tools", "LoadReproEvidence.psm1"));
    private static readonly string ImageTests = File.ReadAllText(
        Path.Combine(RepositoryRoot, "tests", "Integration", "ImageNormalizerTests.cs"));

    [Fact]
    public void Harness_runs_only_the_exact_historical_image_sighting_once_per_fresh_process()
    {
        Assert.Contains(ExactTestName, Harness, StringComparison.Ordinal);
        Assert.Contains(
            "public async Task A_burned_region_destroys_the_pixels_beneath_it()",
            ImageTests,
            StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(Harness, "\"test\", $testProject,"));
        Assert.Contains("\"--filter\", \"FullyQualifiedName=$testName\"", Harness, StringComparison.Ordinal);
        Assert.Contains("for ($repetition = 1; $repetition -le $Repetitions; $repetition++)", Harness, StringComparison.Ordinal);
        Assert.Contains("FreshProcess = $true", Harness, StringComparison.Ordinal);
        Assert.Contains("FreshProcessPerRepetition = $true", Harness, StringComparison.Ordinal);
        Assert.Contains("The exact assertion executes once in that process", Harness, StringComparison.Ordinal);
    }

    [Fact]
    public void Harness_is_bounded_without_adding_a_test_timeout_or_retry()
    {
        Assert.Contains("[ValidateRange(1, 64)]", Harness, StringComparison.Ordinal);
        Assert.Contains("[ValidateRange(1, 8)]", Harness, StringComparison.Ordinal);
        Assert.Contains("[ValidateRange(32, 1024)]", Harness, StringComparison.Ordinal);
        Assert.Contains("[ValidateRange(30, 600)]", Harness, StringComparison.Ordinal);
        Assert.Contains("$process.WaitForExit($LimitSeconds * 1000)", Harness, StringComparison.Ordinal);
        Assert.Contains("$exitCode = 124", Harness, StringComparison.Ordinal);
        Assert.Contains("no assertion retry issued", Harness, StringComparison.Ordinal);
        Assert.DoesNotContain("timeoutMs", Harness, StringComparison.OrdinalIgnoreCase);
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
        var summaryWrite = Harness.IndexOf("-Description \"image load-reproduction summary\"", StringComparison.Ordinal);
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
    public void Every_result_retains_trx_console_assertion_timing_and_process_evidence()
    {
        Assert.Contains("FullyQualifiedName = $testName", Harness, StringComparison.Ordinal);
        Assert.Contains("Repetition = $repetition", Harness, StringComparison.Ordinal);
        Assert.Contains("ExitCode = $boundedRun.ExitCode", Harness, StringComparison.Ordinal);
        Assert.Contains("TimedOut = $boundedRun.TimedOut", Harness, StringComparison.Ordinal);
        Assert.Contains("ElapsedMilliseconds = $boundedRun.ElapsedMilliseconds", Harness, StringComparison.Ordinal);
        Assert.Contains("FailureMessage = $failureMessage", Harness, StringComparison.Ordinal);
        Assert.Contains("StandardOutputFile", Harness, StringComparison.Ordinal);
        Assert.Contains("StandardErrorFile", Harness, StringComparison.Ordinal);
        Assert.Contains(
            "*[local-name()='Output']/*[local-name()='ErrorInfo']/*[local-name()='Message']",
            Harness,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Every_repetition_requires_content_free_load_liveness_at_both_boundaries()
    {
        Assert.Contains("ExpectedWorkerCount = $ExpectedWorkerCount", Harness, StringComparison.Ordinal);
        Assert.Contains("ObservedWorkerCount = $states.Count", Harness, StringComparison.Ordinal);
        Assert.Contains("RunningWorkerCount = $running", Harness, StringComparison.Ordinal);
        Assert.Contains("NonRunningWorkerCount = $states.Count - $running", Harness, StringComparison.Ordinal);
        Assert.Contains("AllWorkersRunning = $ExpectedWorkerCount -gt 0", Harness, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(Harness, "$contentionBefore = Get-ControlledContentionSnapshot"));
        Assert.Equal(1, CountOccurrences(Harness, "$contentionAfter = Get-ControlledContentionSnapshot"));
        Assert.Contains("LoadConditionVerified = $contentionBefore.AllWorkersRunning", Harness, StringComparison.Ordinal);
        Assert.Contains("ContentionLiveness = $contentionLiveness", Harness, StringComparison.Ordinal);
        Assert.Contains("Where-Object { -not $_.LoadConditionVerified }", Harness, StringComparison.Ordinal);
        Assert.Contains("$missingResults = $results.Count -ne $Repetitions", Harness, StringComparison.Ordinal);
    }

    [Fact]
    public void Passing_evidence_is_explicitly_non_diagnostic_and_cannot_recover_the_lost_signature()
    {
        Assert.Contains("non-reproductions", Harness, StringComparison.Ordinal);
        Assert.Contains("not a diagnosis or closure", Harness, StringComparison.Ordinal);
        Assert.Contains("original assertion message was not retained", Harness, StringComparison.Ordinal);
        Assert.Contains("cannot be recovered by this current-code harness", Harness, StringComparison.Ordinal);
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
            ?? throw new InvalidOperationException("Could not locate repository root for image-load harness tests.");
    }
}
