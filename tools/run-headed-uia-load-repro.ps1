# SPDX-License-Identifier: GPL-3.0-or-later
# Bounded reproduction harness for the two historical headed UIA timeout
# sightings. A passing batch is evidence of non-reproduction, not a diagnosis.
[CmdletBinding()]
param(
    [ValidateRange(1, 10)]
    [int]$Repetitions = 3,

    [ValidateRange(1, 8)]
    [int]$CpuWorkers = [Math]::Min(2, [Math]::Max(1, [Environment]::ProcessorCount - 1)),

    [ValidateRange(32, 1024)]
    [int]$MemoryMiB = 128,

    [ValidateRange(60, 600)]
    [int]$PerRunProcessLimitSeconds = 240
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$testProject = "tests\UiAutomation\Foundry.Tests.UiAutomation.csproj"
$runId = (Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssZ", [Globalization.CultureInfo]::InvariantCulture)
$runId += "-" + [Guid]::NewGuid().ToString("N")
$evidenceModulePath = Join-Path $PSScriptRoot "LoadReproEvidence.psm1"
Import-Module -Name $evidenceModulePath -Force
$evidenceLock = Enter-LoadReproEvidenceLock `
    -RepositoryRoot $repositoryRoot `
    -RunId $runId `
    -HarnessName "headed-uia-load-repro"
$finalExitCode = 1

try {
    # This cooperative lock prevents the other load harness from entering its
    # preflight/build/run boundary; unrelated builders do not honor this lock.
    $repositoryStateBefore = Get-LoadReproRepositoryState -RepositoryRoot $repositoryRoot
    Assert-LoadReproCleanRepositoryState -State $repositoryStateBefore
    $repositoryCommit = $repositoryStateBefore.Commit

    $dotnetSdk = (& dotnet --version 2>$null)
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($dotnetSdk)) {
        throw "The .NET SDK identity could not be measured; no source-bound evidence was collected."
    }
    $dotnetSdk = $dotnetSdk.Trim()

    $evidenceDirectory = New-LoadReproEvidenceDirectory `
        -RepositoryRoot $repositoryRoot `
        -EvidenceBaseName "uia-load-repro" `
        -RunId $runId
    $relativeEvidenceRoot = $evidenceDirectory.RelativePath
    $evidenceRoot = $evidenceDirectory.Path
    # An exception before the durable summary fails the process and leaves no
    # completed batch summary from which evidence could be claimed.

$testCases = @(
    [pscustomobject]@{
        Id = "pilot-day"
        FullyQualifiedName = "Foundry.Tests.UiAutomation.HeadedUiaWalkTests.PilotDay_dress_rehearsal_cold_start_to_reopened_booklet_and_low_ink_over_real_uia"
    }
    [pscustomobject]@{
        Id = "part3-review"
        FullyQualifiedName = "Foundry.Tests.UiAutomation.HeadedUiaWalkTests.Part3_Steps9to12_move_edit_and_approve_operate_through_uia_patterns"
    }
)

function Invoke-BoundedProcess {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments,

        [Parameter(Mandatory)]
        [string]$StandardOutputPath,

        [Parameter(Mandatory)]
        [string]$StandardErrorPath,

        [Parameter(Mandatory)]
        [int]$LimitSeconds
    )

    $clock = [Diagnostics.Stopwatch]::StartNew()
    $process = Start-Process -FilePath "dotnet" -ArgumentList $Arguments `
        -WorkingDirectory $repositoryRoot -NoNewWindow -PassThru `
        -RedirectStandardOutput $StandardOutputPath -RedirectStandardError $StandardErrorPath
    $timedOut = -not $process.WaitForExit($LimitSeconds * 1000)
    $terminationRequestStarted = $null
    $terminationRequestTimedOut = $null
    $terminationRequestExitCode = $null
    $terminationHelperExitObserved = $null
    $terminationRequestStartError = $null
    $terminationRequestCleanupError = $null
    $terminationRequestSucceeded = $null
    $launcherExitObserved = $true
    $safeToContinue = $true
    if ($timedOut) {
        # taskkill /T requests termination of the launcher and its current
        # descendants. Its success plus observed launcher exit are required to
        # continue, but neither is an independent enumeration of descendant exit.
        $taskkillResult = Invoke-LoadReproBoundedTaskKill -TargetProcessId $process.Id
        $terminationRequestStarted = $taskkillResult.Started
        $terminationRequestTimedOut = $taskkillResult.TimedOut
        $terminationRequestExitCode = $taskkillResult.ExitCode
        $terminationHelperExitObserved = $taskkillResult.HelperExitObserved
        $terminationRequestStartError = $taskkillResult.StartError
        $terminationRequestCleanupError = $taskkillResult.CleanupError
        $terminationRequestSucceeded = $taskkillResult.Started `
            -and -not $taskkillResult.TimedOut `
            -and $taskkillResult.HelperExitObserved `
            -and $taskkillResult.ExitCode -eq 0
        $launcherExitObserved = $process.WaitForExit(10000)
        if ($launcherExitObserved) {
            $process.WaitForExit()
        }
        $safeToContinue = $terminationRequestSucceeded -and $launcherExitObserved
        $exitCode = 124
    }
    else {
        $process.WaitForExit()
        $exitCode = $process.ExitCode
    }

    $process.Dispose()
    $clock.Stop()
    return [pscustomobject]@{
        ExitCode = $exitCode
        TimedOut = $timedOut
        TerminationRequestStarted = $terminationRequestStarted
        TerminationRequestTimedOut = $terminationRequestTimedOut
        TerminationRequestExitCode = $terminationRequestExitCode
        TerminationHelperExitObserved = $terminationHelperExitObserved
        TerminationRequestStartError = $terminationRequestStartError
        TerminationRequestCleanupError = $terminationRequestCleanupError
        TerminationRequestSucceeded = $terminationRequestSucceeded
        LauncherExitObserved = $launcherExitObserved
        SafeToContinue = $safeToContinue
        ElapsedMilliseconds = $clock.ElapsedMilliseconds
    }
}

function Start-ControlledContention {
    param(
        [Parameter(Mandatory)]
        [int]$WorkerCount,

        [Parameter(Mandatory)]
        [int]$TotalMemoryMiB
    )

    $memoryPerWorker = [Math]::Max(1, [Math]::Floor($TotalMemoryMiB / $WorkerCount))
    $jobs = @()
    for ($worker = 1; $worker -le $WorkerCount; $worker++) {
        $jobs += Start-Job -Name "ocf-uia-contention-$runId-$worker" -ScriptBlock {
            param($MemoryPerWorkerMiB)

            $blocks = [Collections.Generic.List[byte[]]]::new()
            for ($blockIndex = 0; $blockIndex -lt $MemoryPerWorkerMiB; $blockIndex++) {
                $block = [byte[]]::new(1MB)
                for ($offset = 0; $offset -lt $block.Length; $offset += 4096) {
                    $block[$offset] = [byte](($blockIndex + $offset) % 251)
                }
                $blocks.Add($block)
            }

            Write-Output "OCF_LOAD_READY"
            $counter = 0
            while ($true) {
                foreach ($block in $blocks) {
                    for ($offset = 0; $offset -lt $block.Length; $offset += 4096) {
                        $counter = ($counter + 1) % 251
                        $block[$offset] = [byte](($block[$offset] + $counter) % 251)
                    }
                }
            }
        } -ArgumentList $memoryPerWorker
    }

    try {
        $readyJobIds = [Collections.Generic.HashSet[int]]::new()
        $readyDeadline = [DateTime]::UtcNow.AddSeconds(20)
        while ($readyJobIds.Count -lt $jobs.Count -and [DateTime]::UtcNow -lt $readyDeadline) {
            foreach ($job in $jobs) {
                if ($job.State -eq "Failed") {
                    throw "Controlled contention worker $($job.Id) failed before the batch began."
                }

                foreach ($message in @(Receive-Job -Job $job)) {
                    if ($message -eq "OCF_LOAD_READY") {
                        [void]$readyJobIds.Add($job.Id)
                    }
                }
            }

            if ($readyJobIds.Count -lt $jobs.Count) {
                Start-Sleep -Milliseconds 100
            }
        }

        if ($readyJobIds.Count -ne $jobs.Count) {
            throw "Controlled contention did not become ready within 20 seconds."
        }
    }
    catch {
        foreach ($job in $jobs) {
            Stop-Job -Job $job -ErrorAction SilentlyContinue
            Remove-Job -Job $job -Force -ErrorAction SilentlyContinue
        }
        throw
    }

    return $jobs
}

function Stop-ControlledContention {
    param([System.Management.Automation.Job[]]$Jobs)

    foreach ($job in $Jobs) {
        Stop-Job -Job $job -ErrorAction SilentlyContinue
        Remove-Job -Job $job -Force -ErrorAction SilentlyContinue
    }
}

function Get-ControlledContentionSnapshot {
    param(
        [System.Management.Automation.Job[]]$Jobs,
        [int]$ExpectedWorkerCount
    )

    $states = @($Jobs |
        Sort-Object -Property Id |
        ForEach-Object { [string]$_.State })
    $running = @($states | Where-Object { $_ -eq "Running" }).Count
    return [pscustomobject][ordered]@{
        ExpectedWorkerCount = $ExpectedWorkerCount
        ObservedWorkerCount = $states.Count
        RunningWorkerCount = $running
        NonRunningWorkerCount = $states.Count - $running
        AllWorkersRunning = $ExpectedWorkerCount -gt 0 `
            -and $states.Count -eq $ExpectedWorkerCount `
            -and $running -eq $ExpectedWorkerCount
        States = $states
    }
}

function Read-TrxResult {
    param(
        [Parameter(Mandatory)]
        [string]$TrxPath,

        [Parameter(Mandatory)]
        [string]$ExpectedTestName
    )

    if (-not (Test-Path -LiteralPath $TrxPath)) {
        return [pscustomobject]@{
            Outcome = "NotRecorded"
            FailureMessage = "No TRX result was recorded for the named test."
        }
    }

    try {
        [xml]$trx = Get-Content -LiteralPath $TrxPath -Raw
        $matches = @($trx.TestRun.Results.UnitTestResult | Where-Object { $_.testName -eq $ExpectedTestName })
        if ($matches.Count -ne 1) {
            return [pscustomobject]@{
                Outcome = "NotRecorded"
                FailureMessage = "The TRX did not contain exactly one result for the named test."
            }
        }

        $failureMessage = $null
        $messageNode = $matches[0].SelectSingleNode(
            "*[local-name()='Output']/*[local-name()='ErrorInfo']/*[local-name()='Message']")
        if ($null -ne $messageNode) {
            $failureMessage = [string]$messageNode.InnerText
        }

        return [pscustomobject]@{
            Outcome = [string]$matches[0].outcome
            FailureMessage = $failureMessage
        }
    }
    catch {
        return [pscustomobject]@{
            Outcome = "NotRecorded"
            FailureMessage = "The TRX result could not be parsed ($($_.Exception.GetType().Name))."
        }
    }
}

if (-not [Environment]::UserInteractive) {
    throw "An interactive desktop is required; no headed evidence was collected."
}
if ($env:OCF_SKIP_HEADED -eq "1") {
    throw "OCF_SKIP_HEADED=1 would skip the named tests; no headed evidence was collected."
}

$restoreOutput = Join-Path $evidenceRoot "restore.stdout.log"
$restoreError = Join-Path $evidenceRoot "restore.stderr.log"
$restore = Invoke-BoundedProcess -Arguments @(
    "restore", $testProject, "--locked-mode", "--configfile", "NuGet.config"
) -StandardOutputPath $restoreOutput -StandardErrorPath $restoreError -LimitSeconds 300
if (-not $restore.SafeToContinue) {
    throw "The timed-out restore did not produce both a successful process-tree termination request and an observed launcher exit; the batch cannot continue safely."
}
if ($restore.ExitCode -ne 0) {
    throw "The headed UIA project locked restore failed with exit code $($restore.ExitCode); see the retained restore logs."
}

$buildOutput = Join-Path $evidenceRoot "build.stdout.log"
$buildError = Join-Path $evidenceRoot "build.stderr.log"
$build = Invoke-BoundedProcess -Arguments @(
    "build", $testProject, "-c", "Release", "--no-restore", "--no-incremental"
) -StandardOutputPath $buildOutput -StandardErrorPath $buildError -LimitSeconds 300
if (-not $build.SafeToContinue) {
    throw "The timed-out build did not produce both a successful process-tree termination request and an observed launcher exit; the batch cannot continue safely."
}
if ($build.ExitCode -ne 0) {
    throw "The headed UIA project forced build failed with exit code $($build.ExitCode); see the retained build logs."
}

$testAssembly = Join-Path $repositoryRoot `
    "tests\UiAutomation\bin\Release\net10.0-windows10.0.19041.0\Foundry.Tests.UiAutomation.dll"
$testOutputRoot = [IO.Path]::GetDirectoryName($testAssembly)
$outputIdentityBefore = Get-LoadReproOutputIdentity `
    -RepositoryRoot $repositoryRoot `
    -OutputRoot $testOutputRoot `
    -RequiredTestAssembly $testAssembly
$outputIdentityBeforePath = Join-Path $evidenceRoot "test-output-identity-before.json"
Write-LoadReproJsonFile `
    -Value $outputIdentityBefore `
    -Path $outputIdentityBeforePath `
    -ContainmentRoot $evidenceRoot `
    -Description "pre-run test-output identity"
$testAssemblySha256 = $outputIdentityBefore.RequiredTestAssemblySha256
$harnessSha256 = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash

$loadJobs = @()
$results = @()
$contentionLiveness = @()
try {
    $loadJobs = @(Start-ControlledContention -WorkerCount $CpuWorkers -TotalMemoryMiB $MemoryMiB)

    for ($repetition = 1; $repetition -le $Repetitions; $repetition++) {
        $contentionBefore = Get-ControlledContentionSnapshot `
            -Jobs $loadJobs -ExpectedWorkerCount $CpuWorkers
        try {
            if (-not $contentionBefore.AllWorkersRunning) {
                Write-Host "[$repetition/$Repetitions] controlled contention was not live; neither named test was started."
                continue
            }

            foreach ($testCase in $testCases) {
                $fileStem = "r{0:D2}-{1}" -f $repetition, $testCase.Id
                $relativeRunDirectory = Join-Path $relativeEvidenceRoot $fileStem
                $runDirectory = Join-Path $repositoryRoot $relativeRunDirectory
                $scratchRoot = Join-Path $runDirectory "scratch"
                New-Item -ItemType Directory -Path $scratchRoot | Out-Null

                $stdoutPath = Join-Path $runDirectory "$fileStem.stdout.log"
                $stderrPath = Join-Path $runDirectory "$fileStem.stderr.log"
                $trxPath = Join-Path $runDirectory "$fileStem.trx"

                $previousTemp = $env:TEMP
                $previousTmp = $env:TMP
                $previousSkip = $env:OCF_SKIP_HEADED
                try {
                    # The named tests inherit only this disposable temp root. The
                    # PilotDay fixture then supplies its exact empty library root
                    # to production; no default teacher library can be reached.
                    $env:TEMP = $scratchRoot
                    $env:TMP = $scratchRoot
                    Remove-Item Env:OCF_SKIP_HEADED -ErrorAction SilentlyContinue

                    # One process invocation per named test and repetition. The
                    # tests themselves issue approval/output actions once only;
                    # this harness has no action-level retry path.
                    $boundedRun = Invoke-BoundedProcess -Arguments @(
                        "test", $testProject,
                        "-c", "Release", "--no-build", "--no-restore",
                        "--filter", "FullyQualifiedName=$($testCase.FullyQualifiedName)",
                        "--results-directory", $relativeRunDirectory,
                        "--logger", "console;verbosity=normal",
                        "--logger", "trx;LogFileName=$fileStem.trx"
                    ) -StandardOutputPath $stdoutPath -StandardErrorPath $stderrPath `
                        -LimitSeconds $PerRunProcessLimitSeconds
                }
                finally {
                    $env:TEMP = $previousTemp
                    $env:TMP = $previousTmp
                    if ($null -eq $previousSkip) {
                        Remove-Item Env:OCF_SKIP_HEADED -ErrorAction SilentlyContinue
                    }
                    else {
                        $env:OCF_SKIP_HEADED = $previousSkip
                    }
                }

                $trxResult = Read-TrxResult -TrxPath $trxPath -ExpectedTestName $testCase.FullyQualifiedName
                $outcome = $trxResult.Outcome
                $failureMessage = $trxResult.FailureMessage
                if ($boundedRun.TimedOut) {
                    if ($boundedRun.SafeToContinue) {
                        $outcome = "ProcessTimedOut"
                        $failureMessage = "The per-run process cap was reached; the process-tree termination request succeeded and launcher exit was observed, with no action retry issued. Descendant exit was not independently enumerated."
                    }
                    else {
                        $outcome = "ProcessTerminationUnconfirmed"
                        $failureMessage = "The per-run process cap was reached, but the process-tree termination request did not succeed or launcher exit was not observed within 10 seconds; the batch was aborted."
                    }
                }
                elseif ($boundedRun.ExitCode -ne 0 -and $outcome -eq "NotRecorded") {
                    $outcome = "ProcessFailed"
                }

                $record = [ordered]@{
                    TestId = $testCase.Id
                    FullyQualifiedName = $testCase.FullyQualifiedName
                    Repetition = $repetition
                    Outcome = $outcome
                    ExitCode = $boundedRun.ExitCode
                    TimedOut = $boundedRun.TimedOut
                    TerminationRequestStarted = $boundedRun.TerminationRequestStarted
                    TerminationRequestTimedOut = $boundedRun.TerminationRequestTimedOut
                    TerminationRequestExitCode = $boundedRun.TerminationRequestExitCode
                    TerminationHelperExitObserved = $boundedRun.TerminationHelperExitObserved
                    TerminationRequestStartError = $boundedRun.TerminationRequestStartError
                    TerminationRequestCleanupError = $boundedRun.TerminationRequestCleanupError
                    TerminationRequestSucceeded = $boundedRun.TerminationRequestSucceeded
                    LauncherExitObserved = $boundedRun.LauncherExitObserved
                    SafeToContinue = $boundedRun.SafeToContinue
                    ElapsedMilliseconds = $boundedRun.ElapsedMilliseconds
                    FailureMessage = $failureMessage
                    TrxFile = "$fileStem\$fileStem.trx"
                    StandardOutputFile = "$fileStem\$fileStem.stdout.log"
                    StandardErrorFile = "$fileStem\$fileStem.stderr.log"
                }
                $record | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $runDirectory "$fileStem.result.json") -Encoding utf8
                $results += [pscustomobject]$record

                Write-Host ("[{0}/{1}] {2}: {3}; exit={4}; elapsedMs={5}" -f `
                    $repetition, $Repetitions, $testCase.Id, $outcome, $boundedRun.ExitCode, $boundedRun.ElapsedMilliseconds)
                if (-not [string]::IsNullOrWhiteSpace($failureMessage)) {
                    Write-Host "  $failureMessage"
                }

                if (Test-Path -LiteralPath $scratchRoot) {
                    Remove-Item -LiteralPath $scratchRoot -Recurse -Force
                }

                if (-not $boundedRun.SafeToContinue) {
                    throw "The timed-out test did not produce both a successful process-tree termination request and an observed launcher exit; the receipt was retained and the batch cannot continue safely."
                }
            }
        }
        finally {
            $contentionAfter = Get-ControlledContentionSnapshot `
                -Jobs $loadJobs -ExpectedWorkerCount $CpuWorkers
            $liveness = [ordered]@{
                Repetition = $repetition
                Before = $contentionBefore
                After = $contentionAfter
                LoadConditionVerified = $contentionBefore.AllWorkersRunning `
                    -and $contentionAfter.AllWorkersRunning
            }
            $liveness | ConvertTo-Json -Depth 5 | Set-Content `
                -LiteralPath (Join-Path $evidenceRoot ("r{0:D2}-contention.json" -f $repetition)) `
                -Encoding utf8
            $contentionLiveness += [pscustomobject]$liveness
        }
    }
}
finally {
    Stop-ControlledContention -Jobs $loadJobs
}

$repositoryStateAfter = $null
$outputIdentityAfter = $null
$postIdentityMeasurementError = $null
$identityErrors = [Collections.Generic.List[string]]::new()
try {
    $repositoryStateAfter = Get-LoadReproRepositoryState -RepositoryRoot $repositoryRoot
    $outputIdentityAfter = Get-LoadReproOutputIdentity `
        -RepositoryRoot $repositoryRoot `
        -OutputRoot $testOutputRoot `
        -RequiredTestAssembly $testAssembly
    $outputIdentityAfterPath = Join-Path $evidenceRoot "test-output-identity-after.json"
    Write-LoadReproJsonFile `
        -Value $outputIdentityAfter `
        -Path $outputIdentityAfterPath `
        -ContainmentRoot $evidenceRoot `
        -Description "post-run test-output identity"
    foreach ($identityError in @(Get-LoadReproIdentityErrors `
            -RepositoryBefore $repositoryStateBefore `
            -RepositoryAfter $repositoryStateAfter `
            -OutputBefore $outputIdentityBefore `
            -OutputAfter $outputIdentityAfter)) {
        $identityErrors.Add($identityError)
    }
}
catch {
    $postIdentityMeasurementError = $_.Exception.Message
    $identityErrors.Add("Post-run source/output identity measurement failed: $postIdentityMeasurementError")
}
$identityStable = $null -ne $repositoryStateAfter `
    -and $null -ne $outputIdentityAfter `
    -and $identityErrors.Count -eq 0

$summary = [ordered]@{
    RunId = $runId
    Statement = "Passing repetitions are non-reproductions, not a diagnosis of either historical sighting."
    TerminationEvidenceStatement = "Timeout continuation requires taskkill /T /F exit 0 and observed launcher exit; descendant exit is not independently enumerated."
    SourceToBinaryProvenance = "A forced build plus stable pre/post source and built-output manifests bind this batch to unchanged observed bytes; they do not prove compiler/source correspondence or the complete SDK/NuGet process closure."
    SourceAndOutputIdentityStable = $identityStable
    SharedEvidenceLock = [ordered]@{
        RelativePath = $evidenceLock.RelativePath
        BoundRunId = $runId
        HeldThroughDurableSummary = $true
    }
    Source = [ordered]@{
        RepositoryCommit = $repositoryCommit
        TreeStateBefore = "clean"
        RepositoryStateBefore = $repositoryStateBefore
        RepositoryStateAfter = $repositoryStateAfter
        DotnetSdk = $dotnetSdk
        RestorePerformed = $true
        BuildPerformed = $true
        HarnessSha256 = $harnessSha256
        TestAssemblySha256 = $testAssemblySha256
    }
    TestOutputIdentity = [ordered]@{
        BeforeFile = "test-output-identity-before.json"
        AfterFile = if ($null -eq $outputIdentityAfter) { $null } else { "test-output-identity-after.json" }
        BeforeManifestSha256 = $outputIdentityBefore.ManifestSha256
        AfterManifestSha256 = if ($null -eq $outputIdentityAfter) { $null } else {
            $outputIdentityAfter.ManifestSha256
        }
        BeforeFileCount = $outputIdentityBefore.FileCount
        AfterFileCount = if ($null -eq $outputIdentityAfter) { $null } else {
            $outputIdentityAfter.FileCount
        }
    }
    IdentityErrors = @($identityErrors)
    PostIdentityMeasurementError = $postIdentityMeasurementError
    Host = [ordered]@{
        OperatingSystem = [Environment]::OSVersion.VersionString
        ProcessorCount = [Environment]::ProcessorCount
        UserInteractive = [Environment]::UserInteractive
    }
    Configuration = [ordered]@{
        Repetitions = $Repetitions
        CpuWorkers = $CpuWorkers
        MemoryMiB = $MemoryMiB
        PerRunProcessLimitSeconds = $PerRunProcessLimitSeconds
        ExistingProbeTimeoutMilliseconds = 20000
    }
    ContentionStatement = "Worker job states were sampled at repetition boundaries; this does not measure CPU-utilization magnitude."
    ContentionLiveness = $contentionLiveness
    Results = $results
}
$summaryPath = Join-Path $evidenceRoot "summary.json"
Write-LoadReproJsonFile `
    -Value $summary `
    -Path $summaryPath `
    -ContainmentRoot $evidenceRoot `
    -Description "headed UIA load-reproduction summary"

$failed = @($results | Where-Object { $_.Outcome -ne "Passed" -or $_.ExitCode -ne 0 })
$invalidLoad = @($contentionLiveness | Where-Object { -not $_.LoadConditionVerified })
$missingResults = $results.Count -ne ($Repetitions * $testCases.Count)
Write-Host "Evidence retained at $evidenceRoot"
$finalExitCode = if ($failed.Count -gt 0 `
    -or $invalidLoad.Count -gt 0 `
    -or $missingResults `
    -or -not $identityStable) {
    1
}
else {
    0
}
}
finally {
    if ($null -ne $evidenceLock) {
        $evidenceLock.Stream.Dispose()
    }
}
exit $finalExitCode
