# SPDX-License-Identifier: GPL-3.0-or-later
# Bounded cross-process reproduction harness for the bounded-native-lifecycle
# sightings (sightings register S-04 to S-07): the real console-control case,
# which has reported console-signal.lock-observation-timeout and
# console-signal.attach-failed, and the two FlashCap lifecycle timeouts. Every
# hosted observation overlapped the real Edge PDF exercise, and the C4/C5
# isolation removed that overlap from ordinary runs. Each repetition therefore
# starts one fresh test process for the three named cases while a second fresh
# test process runs the real PDF exports and controlled CPU/memory contention
# stays live, then measures the overlap from the TRX intervals. Passing
# repetitions are non-reproductions, not a diagnosis, cure, or closure; no test
# or product deadline is changed.
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
$PSNativeCommandUseErrorActionPreference = $false

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$testProject = "tests\Integration\Foundry.Tests.Integration.csproj"
$runId = (Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssZ", [Globalization.CultureInfo]::InvariantCulture)
$runId += "-" + [Guid]::NewGuid().ToString("N")
$evidenceModulePath = Join-Path $PSScriptRoot "LoadReproEvidence.psm1"
Import-Module -Name $evidenceModulePath -Force
$evidenceLock = Enter-LoadReproEvidenceLock `
    -RepositoryRoot $repositoryRoot `
    -RunId $runId `
    -HarnessName "console-signal-load-repro"
$finalExitCode = 1

try {
    # This cooperative lock prevents the other load harnesses from entering their
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
        -EvidenceBaseName "console-signal-load-repro" `
        -RunId $runId
    $relativeEvidenceRoot = $evidenceDirectory.RelativePath
    $evidenceRoot = $evidenceDirectory.Path
    # An exception before the durable summary fails the process and leaves no
    # completed batch summary from which evidence could be claimed.

$namedCases = @(
    [pscustomobject]@{
        Id = "console-ctrl-c"
        FullyQualifiedName = "Foundry.Tests.Integration.ProjectUpgradeOperatorHostTests.Real_console_ctrl_c_cancels_the_process_and_cleans_the_synthetic_batch"
    }
    [pscustomobject]@{
        Id = "flashcap-late-start"
        FullyQualifiedName = "Foundry.Tests.Integration.FlashCapCameraSourceTests.A_late_successful_start_is_stopped_and_disposed_again_after_immediate_cleanup"
    }
    [pscustomobject]@{
        Id = "flashcap-shared-lock"
        FullyQualifiedName = "Foundry.Tests.Integration.FlashCapCameraSourceTests.A_shared_lifecycle_lock_can_prevent_confirmed_shutdown_but_capture_still_settles_bounded"
    }
)

# The co-scheduled exercise is the same real Edge PDF work that overlapped every
# hosted observation. It runs in its own fresh test process so the named cases
# keep their production non-parallel collection unchanged.
$exerciseCases = @(
    [pscustomobject]@{
        Id = "pdf-two-exports"
        FullyQualifiedName = "Foundry.Tests.Integration.EdgePdfExporterTests.Two_exports_complete_concurrently_with_isolated_edge_profiles"
    }
    [pscustomobject]@{
        Id = "pdf-asset-pipeline"
        FullyQualifiedName = "Foundry.Tests.Integration.EdgePdfExporterTests.The_real_edge_pipeline_resolves_an_asset_and_produces_a_pdf"
    }
    [pscustomobject]@{
        Id = "pdf-approved-artifact"
        FullyQualifiedName = "Foundry.Tests.Integration.EdgePdfExporterTests.An_approved_artifact_becomes_a_real_pdf"
    }
)

function Join-TestFilter {
    param([Parameter(Mandatory)][object[]]$Cases)

    return (($Cases | ForEach-Object { "FullyQualifiedName=$($_.FullyQualifiedName)" }) -join "|")
}

function Get-EdgeExecutablePath {
    # Mirrors the exporter's two candidate locations. The PDF tests return early
    # without Edge, which would silently remove the co-scheduled load; the
    # harness therefore refuses to run rather than record an empty exercise.
    $candidates = @(
        (Join-Path ([Environment]::GetFolderPath([System.Environment+SpecialFolder]::ProgramFilesX86)) "Microsoft\Edge\Application\msedge.exe"),
        (Join-Path ([Environment]::GetFolderPath([System.Environment+SpecialFolder]::ProgramFiles)) "Microsoft\Edge\Application\msedge.exe")
    )
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return $candidate
        }
    }

    return $null
}

function Start-BoundedProcess {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments,

        [Parameter(Mandatory)]
        [string]$StandardOutputPath,

        [Parameter(Mandatory)]
        [string]$StandardErrorPath
    )

    $clock = [Diagnostics.Stopwatch]::StartNew()
    $process = Start-Process -FilePath "dotnet" -ArgumentList $Arguments `
        -WorkingDirectory $repositoryRoot -NoNewWindow -PassThru `
        -RedirectStandardOutput $StandardOutputPath -RedirectStandardError $StandardErrorPath
    return [pscustomobject]@{
        Process = $process
        Clock = $clock
        StartedUtc = [DateTime]::UtcNow
    }
}

function Complete-BoundedProcess {
    param(
        [Parameter(Mandatory)]
        [object]$Started,

        [Parameter(Mandatory)]
        [int]$LimitSeconds
    )

    $process = $Started.Process
    $clock = $Started.Clock
    $remainingMilliseconds = [Math]::Max(0, ([long]$LimitSeconds * 1000) - $clock.ElapsedMilliseconds)
    $timedOut = -not $process.WaitForExit([int]$remainingMilliseconds)
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
        StartedUtc = $Started.StartedUtc.ToString("O", [Globalization.CultureInfo]::InvariantCulture)
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
        $jobs += Start-Job -Name "ocf-console-contention-$runId-$worker" -ScriptBlock {
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

function Read-TrxResults {
    param(
        [Parameter(Mandatory)]
        [string]$TrxPath,

        [Parameter(Mandatory)]
        [string[]]$ExpectedTestNames
    )

    $results = @()
    $document = $null
    $parseFailure = $null
    if (Test-Path -LiteralPath $TrxPath) {
        try {
            [xml]$document = Get-Content -LiteralPath $TrxPath -Raw
        }
        catch {
            $parseFailure = "The TRX result could not be parsed ($($_.Exception.GetType().Name))."
        }
    }

    foreach ($expectedTestName in $ExpectedTestNames) {
        if ($null -eq $document) {
            $results += [pscustomobject]@{
                FullyQualifiedName = $expectedTestName
                Outcome = "NotRecorded"
                FailureMessage = if ($null -ne $parseFailure) { $parseFailure } else { "No TRX result was recorded for the named test." }
                StartUtc = $null
                EndUtc = $null
                DurationMilliseconds = $null
            }
            continue
        }

        $matches = @($document.TestRun.Results.UnitTestResult | Where-Object { $_.testName -eq $expectedTestName })
        if ($matches.Count -ne 1) {
            $results += [pscustomobject]@{
                FullyQualifiedName = $expectedTestName
                Outcome = "NotRecorded"
                FailureMessage = "The TRX did not contain exactly one result for the named test."
                StartUtc = $null
                EndUtc = $null
                DurationMilliseconds = $null
            }
            continue
        }

        $failureMessage = $null
        $messageNode = $matches[0].SelectSingleNode(
            "*[local-name()='Output']/*[local-name()='ErrorInfo']/*[local-name()='Message']")
        if ($null -ne $messageNode) {
            $failureMessage = [string]$messageNode.InnerText
        }

        $start = [DateTimeOffset]::Parse([string]$matches[0].startTime, [Globalization.CultureInfo]::InvariantCulture).ToUniversalTime()
        $end = [DateTimeOffset]::Parse([string]$matches[0].endTime, [Globalization.CultureInfo]::InvariantCulture).ToUniversalTime()
        $results += [pscustomobject]@{
            FullyQualifiedName = $expectedTestName
            Outcome = [string]$matches[0].outcome
            FailureMessage = $failureMessage
            StartUtc = $start.ToString("O", [Globalization.CultureInfo]::InvariantCulture)
            EndUtc = $end.ToString("O", [Globalization.CultureInfo]::InvariantCulture)
            DurationMilliseconds = [long]($end - $start).TotalMilliseconds
        }
    }

    return $results
}

function Get-IntervalOverlapMilliseconds {
    param(
        [Parameter(Mandatory)][object]$Named,
        [Parameter(Mandatory)][object[]]$Exercise
    )

    if ($null -eq $Named.StartUtc -or $null -eq $Named.EndUtc) {
        return $null
    }

    $namedStart = [DateTimeOffset]::Parse($Named.StartUtc, [Globalization.CultureInfo]::InvariantCulture)
    $namedEnd = [DateTimeOffset]::Parse($Named.EndUtc, [Globalization.CultureInfo]::InvariantCulture)
    $overlap = [long]0
    foreach ($exerciseResult in $Exercise) {
        if ($null -eq $exerciseResult.StartUtc -or $null -eq $exerciseResult.EndUtc) {
            continue
        }

        $exerciseStart = [DateTimeOffset]::Parse($exerciseResult.StartUtc, [Globalization.CultureInfo]::InvariantCulture)
        $exerciseEnd = [DateTimeOffset]::Parse($exerciseResult.EndUtc, [Globalization.CultureInfo]::InvariantCulture)
        $latestStart = if ($namedStart -gt $exerciseStart) { $namedStart } else { $exerciseStart }
        $earliestEnd = if ($namedEnd -lt $exerciseEnd) { $namedEnd } else { $exerciseEnd }
        $slice = [long]($earliestEnd - $latestStart).TotalMilliseconds
        if ($slice -gt 0) {
            $overlap += $slice
        }
    }

    return $overlap
}

$edgeExecutable = Get-EdgeExecutablePath
if ($null -eq $edgeExecutable) {
    throw "Microsoft Edge was not found at either exporter candidate path; the co-scheduled PDF exercise would silently skip, so no console-signal load evidence was collected."
}

$restoreOutput = Join-Path $evidenceRoot "restore.stdout.log"
$restoreError = Join-Path $evidenceRoot "restore.stderr.log"
$restore = Complete-BoundedProcess -Started (Start-BoundedProcess -Arguments @(
    "restore", $testProject, "--locked-mode", "--configfile", "NuGet.config"
) -StandardOutputPath $restoreOutput -StandardErrorPath $restoreError) -LimitSeconds 300
if (-not $restore.SafeToContinue) {
    throw "The timed-out restore did not produce both a successful process-tree termination request and an observed launcher exit; the batch cannot continue safely."
}
if ($restore.ExitCode -ne 0) {
    throw "The Integration test project locked restore failed with exit code $($restore.ExitCode); see the retained restore logs."
}

$buildOutput = Join-Path $evidenceRoot "build.stdout.log"
$buildError = Join-Path $evidenceRoot "build.stderr.log"
$build = Complete-BoundedProcess -Started (Start-BoundedProcess -Arguments @(
    "build", $testProject, "-c", "Release", "--no-restore", "--no-incremental"
) -StandardOutputPath $buildOutput -StandardErrorPath $buildError) -LimitSeconds 300
if (-not $build.SafeToContinue) {
    throw "The timed-out build did not produce both a successful process-tree termination request and an observed launcher exit; the batch cannot continue safely."
}
if ($build.ExitCode -ne 0) {
    throw "The Integration test project build failed with exit code $($build.ExitCode); see the retained build logs."
}

$testAssembly = Join-Path $repositoryRoot `
    "tests\Integration\bin\Release\net10.0-windows10.0.19041.0\Foundry.Tests.Integration.dll"
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
        $exerciseAliveAtNamedStart = $false
        $exerciseValid = $false
        $allNamedCoScheduled = $false
        try {
            if (-not $contentionBefore.AllWorkersRunning) {
                Write-Host "[$repetition/$Repetitions] controlled contention was not live; no named case was started."
                continue
            }

            $fileStem = "r{0:D2}" -f $repetition
            $relativeRunDirectory = Join-Path $relativeEvidenceRoot $fileStem
            $runDirectory = Join-Path $repositoryRoot $relativeRunDirectory
            $namedScratch = Join-Path $runDirectory "named-scratch"
            $exerciseScratch = Join-Path $runDirectory "exercise-scratch"
            New-Item -ItemType Directory -Path $namedScratch | Out-Null
            New-Item -ItemType Directory -Path $exerciseScratch | Out-Null

            $namedTrxName = "$fileStem-named.trx"
            $exerciseTrxName = "$fileStem-exercise.trx"
            $namedTrxPath = Join-Path $runDirectory $namedTrxName
            $exerciseTrxPath = Join-Path $runDirectory $exerciseTrxName

            $previousTemp = $env:TEMP
            $previousTmp = $env:TMP
            try {
                # Each fresh process inherits only its own disposable temp root.
                # The exercise starts first; the named process starts at once so
                # that its cases run while the real PDF exports are in flight.
                $env:TEMP = $exerciseScratch
                $env:TMP = $exerciseScratch
                $exerciseStarted = Start-BoundedProcess -Arguments @(
                    "test", $testProject,
                    "-c", "Release", "--no-build", "--no-restore",
                    "--filter", (Join-TestFilter -Cases $exerciseCases),
                    "--results-directory", $relativeRunDirectory,
                    "--logger", "console;verbosity=normal",
                    "--logger", "trx;LogFileName=$exerciseTrxName"
                ) -StandardOutputPath (Join-Path $runDirectory "$fileStem-exercise.stdout.log") `
                    -StandardErrorPath (Join-Path $runDirectory "$fileStem-exercise.stderr.log")

                $env:TEMP = $namedScratch
                $env:TMP = $namedScratch
                # One named-case process per repetition. The tests own every
                # deadline and issue their one signal once; this harness has no
                # action-level or assertion retry path.
                $namedStarted = Start-BoundedProcess -Arguments @(
                    "test", $testProject,
                    "-c", "Release", "--no-build", "--no-restore",
                    "--filter", (Join-TestFilter -Cases $namedCases),
                    "--results-directory", $relativeRunDirectory,
                    "--logger", "console;verbosity=normal",
                    "--logger", "trx;LogFileName=$namedTrxName"
                ) -StandardOutputPath (Join-Path $runDirectory "$fileStem-named.stdout.log") `
                    -StandardErrorPath (Join-Path $runDirectory "$fileStem-named.stderr.log")
            }
            finally {
                $env:TEMP = $previousTemp
                $env:TMP = $previousTmp
            }

            $exerciseAliveAtNamedStart = -not $exerciseStarted.Process.HasExited
            $namedRun = Complete-BoundedProcess -Started $namedStarted -LimitSeconds $PerRunProcessLimitSeconds
            $exerciseRun = Complete-BoundedProcess -Started $exerciseStarted -LimitSeconds $PerRunProcessLimitSeconds

            $exerciseResults = @(Read-TrxResults -TrxPath $exerciseTrxPath -ExpectedTestNames @($exerciseCases.FullyQualifiedName))
            $namedResults = @(Read-TrxResults -TrxPath $namedTrxPath -ExpectedTestNames @($namedCases.FullyQualifiedName))
            $exerciseValid = $exerciseRun.ExitCode -eq 0 `
                -and -not $exerciseRun.TimedOut `
                -and @($exerciseResults | Where-Object { $_.Outcome -ne "Passed" }).Count -eq 0

            $namedRecords = @()
            foreach ($namedCase in $namedCases) {
                $namedResult = @($namedResults | Where-Object { $_.FullyQualifiedName -eq $namedCase.FullyQualifiedName })[0]
                $outcome = $namedResult.Outcome
                $failureMessage = $namedResult.FailureMessage
                if ($namedRun.TimedOut) {
                    if ($namedRun.SafeToContinue) {
                        $outcome = "ProcessTimedOut"
                        $failureMessage = "The per-run process cap was reached; the process-tree termination request succeeded and launcher exit was observed, with no action or assertion retry issued. Descendant exit was not independently enumerated."
                    }
                    else {
                        $outcome = "ProcessTerminationUnconfirmed"
                        $failureMessage = "The per-run process cap was reached, but the process-tree termination request did not succeed or launcher exit was not observed within 10 seconds; the batch was aborted."
                    }
                }
                elseif ($namedRun.ExitCode -ne 0 -and $outcome -eq "NotRecorded") {
                    $outcome = "ProcessFailed"
                }

                $overlap = Get-IntervalOverlapMilliseconds -Named $namedResult -Exercise $exerciseResults
                $namedRecords += [pscustomobject][ordered]@{
                    TestId = $namedCase.Id
                    FullyQualifiedName = $namedCase.FullyQualifiedName
                    Outcome = $outcome
                    FailureMessage = $failureMessage
                    StartUtc = $namedResult.StartUtc
                    EndUtc = $namedResult.EndUtc
                    DurationMilliseconds = $namedResult.DurationMilliseconds
                    OverlapWithExerciseMilliseconds = $overlap
                    CoScheduled = ($null -ne $overlap -and $overlap -gt 0)
                }
            }
            $allNamedCoScheduled = $namedRecords.Count -eq $namedCases.Count `
                -and @($namedRecords | Where-Object { -not $_.CoScheduled }).Count -eq 0

            $record = [ordered]@{
                Repetition = $repetition
                FreshProcessPerRole = $true
                ExerciseAliveAtNamedStart = $exerciseAliveAtNamedStart
                ExerciseValid = $exerciseValid
                AllNamedCoScheduled = $allNamedCoScheduled
                Named = $namedRecords
                NamedProcess = [ordered]@{
                    ExitCode = $namedRun.ExitCode
                    TimedOut = $namedRun.TimedOut
                    TerminationRequestStarted = $namedRun.TerminationRequestStarted
                    TerminationRequestTimedOut = $namedRun.TerminationRequestTimedOut
                    TerminationRequestExitCode = $namedRun.TerminationRequestExitCode
                    TerminationHelperExitObserved = $namedRun.TerminationHelperExitObserved
                    TerminationRequestStartError = $namedRun.TerminationRequestStartError
                    TerminationRequestCleanupError = $namedRun.TerminationRequestCleanupError
                    TerminationRequestSucceeded = $namedRun.TerminationRequestSucceeded
                    LauncherExitObserved = $namedRun.LauncherExitObserved
                    SafeToContinue = $namedRun.SafeToContinue
                    StartedUtc = $namedRun.StartedUtc
                    ElapsedMilliseconds = $namedRun.ElapsedMilliseconds
                    TrxFile = "$fileStem\$namedTrxName"
                    StandardOutputFile = "$fileStem\$fileStem-named.stdout.log"
                    StandardErrorFile = "$fileStem\$fileStem-named.stderr.log"
                }
                Exercise = @($exerciseResults | ForEach-Object {
                        [ordered]@{
                            FullyQualifiedName = $_.FullyQualifiedName
                            Outcome = $_.Outcome
                            FailureMessage = $_.FailureMessage
                            StartUtc = $_.StartUtc
                            EndUtc = $_.EndUtc
                            DurationMilliseconds = $_.DurationMilliseconds
                        }
                    })
                ExerciseProcess = [ordered]@{
                    ExitCode = $exerciseRun.ExitCode
                    TimedOut = $exerciseRun.TimedOut
                    TerminationRequestStarted = $exerciseRun.TerminationRequestStarted
                    TerminationRequestTimedOut = $exerciseRun.TerminationRequestTimedOut
                    TerminationRequestExitCode = $exerciseRun.TerminationRequestExitCode
                    TerminationHelperExitObserved = $exerciseRun.TerminationHelperExitObserved
                    TerminationRequestStartError = $exerciseRun.TerminationRequestStartError
                    TerminationRequestCleanupError = $exerciseRun.TerminationRequestCleanupError
                    TerminationRequestSucceeded = $exerciseRun.TerminationRequestSucceeded
                    LauncherExitObserved = $exerciseRun.LauncherExitObserved
                    SafeToContinue = $exerciseRun.SafeToContinue
                    StartedUtc = $exerciseRun.StartedUtc
                    ElapsedMilliseconds = $exerciseRun.ElapsedMilliseconds
                    TrxFile = "$fileStem\$exerciseTrxName"
                    StandardOutputFile = "$fileStem\$fileStem-exercise.stdout.log"
                    StandardErrorFile = "$fileStem\$fileStem-exercise.stderr.log"
                }
            }
            $record | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $runDirectory "$fileStem.result.json") -Encoding utf8
            $results += [pscustomobject]$record

            foreach ($namedRecord in $namedRecords) {
                Write-Host ("[{0}/{1}] {2}: {3}; overlapMs={4}; durationMs={5}" -f `
                    $repetition, $Repetitions, $namedRecord.TestId, $namedRecord.Outcome, $namedRecord.OverlapWithExerciseMilliseconds, $namedRecord.DurationMilliseconds)
                if (-not [string]::IsNullOrWhiteSpace($namedRecord.FailureMessage)) {
                    Write-Host "  $($namedRecord.FailureMessage)"
                }
            }
            Write-Host ("[{0}/{1}] exercise: exit={2}; valid={3}; aliveAtNamedStart={4}; elapsedMs={5}" -f `
                $repetition, $Repetitions, $exerciseRun.ExitCode, $exerciseValid, $exerciseAliveAtNamedStart, $exerciseRun.ElapsedMilliseconds)

            foreach ($scratch in @($namedScratch, $exerciseScratch)) {
                if (Test-Path -LiteralPath $scratch) {
                    Remove-Item -LiteralPath $scratch -Recurse -Force
                }
            }

            if (-not $namedRun.SafeToContinue -or -not $exerciseRun.SafeToContinue) {
                throw "A timed-out test process did not produce both a successful process-tree termination request and an observed launcher exit; the receipt was retained and the batch cannot continue safely."
            }
        }
        finally {
            $contentionAfter = Get-ControlledContentionSnapshot `
                -Jobs $loadJobs -ExpectedWorkerCount $CpuWorkers
            $liveness = [ordered]@{
                Repetition = $repetition
                Before = $contentionBefore
                After = $contentionAfter
                ExerciseAliveAtNamedStart = $exerciseAliveAtNamedStart
                ExerciseValid = $exerciseValid
                AllNamedCoScheduled = $allNamedCoScheduled
                LoadConditionVerified = $contentionBefore.AllWorkersRunning `
                    -and $contentionAfter.AllWorkersRunning `
                    -and $exerciseAliveAtNamedStart `
                    -and $exerciseValid `
                    -and $allNamedCoScheduled
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
    Statement = "Passing repetitions are non-reproductions of the console-signal and FlashCap lifecycle sightings, not a diagnosis, cure, or closure."
    CoSchedulingStatement = "Overlap is measured from the TRX start and end times of two separate fresh test processes on one machine; it approximates the hosted intra-process overlap and does not reproduce shared thread-pool or xUnit scheduling."
    DeadlineStatement = "The sender's 15-second readiness budget, the test's 15-second host exit wait, and the five-second FlashCap bounds are owned by the tests and are unchanged; this harness sets no test or product deadline."
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
        EdgePresent = $true
    }
    Configuration = [ordered]@{
        Repetitions = $Repetitions
        CpuWorkers = $CpuWorkers
        MemoryMiB = $MemoryMiB
        PerRunProcessLimitSeconds = $PerRunProcessLimitSeconds
        FreshProcessPerRole = $true
    }
    NamedCases = @($namedCases | ForEach-Object { [ordered]@{ Id = $_.Id; FullyQualifiedName = $_.FullyQualifiedName } })
    ExerciseCases = @($exerciseCases | ForEach-Object { [ordered]@{ Id = $_.Id; FullyQualifiedName = $_.FullyQualifiedName } })
    ContentionStatement = "Worker job states were sampled at repetition boundaries; this does not measure CPU-utilization magnitude."
    ContentionLiveness = $contentionLiveness
    Results = $results
}
$summaryPath = Join-Path $evidenceRoot "summary.json"
Write-LoadReproJsonFile `
    -Value $summary `
    -Path $summaryPath `
    -ContainmentRoot $evidenceRoot `
    -Description "console-signal load-reproduction summary"

$failedNamed = @($results | ForEach-Object { $_.Named } | Where-Object { $_.Outcome -ne "Passed" })
$failedProcesses = @($results | Where-Object { $_.NamedProcess.ExitCode -ne 0 })
$invalidLoad = @($contentionLiveness | Where-Object { -not $_.LoadConditionVerified })
$missingResults = $results.Count -ne $Repetitions
Write-Host "Evidence retained at $evidenceRoot"
$finalExitCode = if ($failedNamed.Count -gt 0 `
    -or $failedProcesses.Count -gt 0 `
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
