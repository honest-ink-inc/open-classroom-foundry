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
    [int]$PerRunProcessLimitSeconds = 240,

    [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$testProject = "tests\UiAutomation\Foundry.Tests.UiAutomation.csproj"
$runId = (Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssZ", [Globalization.CultureInfo]::InvariantCulture)
$runId += "-" + [Guid]::NewGuid().ToString("N")
$relativeEvidenceRoot = Join-Path "out\uia-load-repro" $runId
$evidenceRoot = Join-Path $repositoryRoot $relativeEvidenceRoot
New-Item -ItemType Directory -Path $evidenceRoot | Out-Null

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
    if ($timedOut) {
        # Kill exactly this dotnet process tree. A stranded headed testhost can
        # otherwise collide with the next named repetition and corrupt evidence.
        & "$env:SystemRoot\System32\taskkill.exe" /PID $process.Id /T /F 2>&1 | Out-Null
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

if (-not $SkipBuild) {
    $buildOutput = Join-Path $evidenceRoot "build.stdout.log"
    $buildError = Join-Path $evidenceRoot "build.stderr.log"
    $build = Invoke-BoundedProcess -Arguments @(
        "build", $testProject, "-c", "Release", "--no-restore"
    ) -StandardOutputPath $buildOutput -StandardErrorPath $buildError -LimitSeconds 300
    if ($build.ExitCode -ne 0) {
        throw "The headed UIA project build failed with exit code $($build.ExitCode); see the retained build logs."
    }
}

$loadJobs = @()
$results = @()
try {
    $loadJobs = @(Start-ControlledContention -WorkerCount $CpuWorkers -TotalMemoryMiB $MemoryMiB)

    for ($repetition = 1; $repetition -le $Repetitions; $repetition++) {
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
                $outcome = "ProcessTimedOut"
                $failureMessage = "The per-run process cap was reached; the exact test process tree was terminated with no action retry issued."
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
        }
    }
}
finally {
    Stop-ControlledContention -Jobs $loadJobs
}

$summary = [ordered]@{
    RunId = $runId
    Statement = "Passing repetitions are non-reproductions, not a diagnosis of either historical sighting."
    Configuration = [ordered]@{
        Repetitions = $Repetitions
        CpuWorkers = $CpuWorkers
        MemoryMiB = $MemoryMiB
        PerRunProcessLimitSeconds = $PerRunProcessLimitSeconds
        ExistingProbeTimeoutMilliseconds = 20000
    }
    Results = $results
}
$summaryPath = Join-Path $evidenceRoot "summary.json"
$summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $summaryPath -Encoding utf8

$failed = @($results | Where-Object { $_.Outcome -ne "Passed" -or $_.ExitCode -ne 0 })
Write-Host "Evidence retained at $evidenceRoot"
if ($failed.Count -gt 0) {
    exit 1
}
exit 0
