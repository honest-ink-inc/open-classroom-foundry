# SPDX-License-Identifier: GPL-3.0-or-later
# One-process, solution-wide CI test runner with an outer process cap. The
# runner preserves ordinary suite selection and scheduling while retaining a
# fail-closed, hash-bound receipt even when the test process stalls.
[CmdletBinding()]
param(
    [ValidateRange(60, 7200)]
    [int]$ProcessLimitSeconds = 900
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not $IsWindows) {
    throw "The bounded CI test runner requires Windows taskkill /T semantics."
}

function New-CiCreateNewFileStream {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$ContainmentRoot,
        [Parameter(Mandatory)][string]$Description)

    Assert-CiPathContained -ContainmentRoot $ContainmentRoot -Path $Path -Description $Description
    if (Test-Path -LiteralPath $Path) {
        throw "$Description already exists: $Path"
    }
    $options = [IO.FileOptions]([IO.FileOptions]::Asynchronous -bor [IO.FileOptions]::WriteThrough)
    $stream = [IO.FileStream]::new(
        $Path,
        [IO.FileMode]::CreateNew,
        [IO.FileAccess]::Write,
        [IO.FileShare]::Read,
        4096,
        $options)
    try {
        Assert-CiPathContained -ContainmentRoot $ContainmentRoot -Path $Path -Description $Description
        $stream
    }
    catch {
        $stream.Dispose()
        throw
    }
}

function Write-CiJsonFile {
    param(
        [Parameter(Mandatory)][object]$Value,
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$ContainmentRoot,
        [Parameter(Mandatory)][string]$Description)

    $stream = New-CiCreateNewFileStream `
        -Path $Path `
        -ContainmentRoot $ContainmentRoot `
        -Description $Description
    try {
        $json = $Value | ConvertTo-Json -Depth 10
        $bytes = [Text.UTF8Encoding]::new($false).GetBytes($json + [Environment]::NewLine)
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Flush($true)
    }
    finally {
        $stream.Dispose()
    }
    Assert-CiPathContained -ContainmentRoot $ContainmentRoot -Path $Path -Description $Description
}

function Update-CiJsonFileAtomically {
    param(
        [Parameter(Mandatory)][object]$Value,
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$ContainmentRoot,
        [Parameter(Mandatory)][string]$Description)

    Assert-CiPathContained -ContainmentRoot $ContainmentRoot -Path $Path -Description $Description
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Description cannot be atomically updated because it does not exist: $Path"
    }

    $temporaryPath = Join-Path `
        ([IO.Path]::GetDirectoryName($Path)) `
        ("." + [IO.Path]::GetFileName($Path) + "." + [Guid]::NewGuid().ToString("N") + ".tmp")
    $backupPath = $temporaryPath + ".bak"
    try {
        Write-CiJsonFile `
            -Value $Value `
            -Path $temporaryPath `
            -ContainmentRoot $ContainmentRoot `
            -Description "$Description replacement"
        Assert-CiPathContained -ContainmentRoot $ContainmentRoot -Path $Path -Description $Description
        Assert-CiPathContained `
            -ContainmentRoot $ContainmentRoot `
            -Path $backupPath `
            -Description "$Description replacement backup"
        [IO.File]::Replace($temporaryPath, $Path, $backupPath, $true)
        Assert-CiPathContained -ContainmentRoot $ContainmentRoot -Path $Path -Description $Description
        if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
            throw "$Description disappeared during atomic update: $Path"
        }
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath) {
            Assert-CiPathContained `
                -ContainmentRoot $ContainmentRoot `
                -Path $temporaryPath `
                -Description "$Description replacement"
            [IO.File]::Delete($temporaryPath)
        }
        if (Test-Path -LiteralPath $backupPath) {
            Assert-CiPathContained `
                -ContainmentRoot $ContainmentRoot `
                -Path $backupPath `
                -Description "$Description replacement backup"
            [IO.File]::Delete($backupPath)
        }
    }
}

function Wait-CiProcessExit {
    param(
        [Parameter(Mandatory)][Diagnostics.Process]$Process,
        [Parameter(Mandatory)][ValidateRange(0, [long]::MaxValue)][long]$LimitMilliseconds)

    $waitClock = [Diagnostics.Stopwatch]::StartNew()
    while ($true) {
        if ($Process.WaitForExit(0)) {
            return $true
        }

        $remaining = $LimitMilliseconds - $waitClock.ElapsedMilliseconds
        if ($remaining -le 0) {
            return $Process.WaitForExit(0)
        }

        $slice = [int][Math]::Min([long]250, $remaining)
        if ($Process.WaitForExit($slice)) {
            return $true
        }
    }
}

function Invoke-CiBoundedTaskKill {
    param(
        [Parameter(Mandatory)][int]$TargetProcessId,
        [int]$LimitMilliseconds = 10000)

    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = "$env:SystemRoot\System32\taskkill.exe"
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    foreach ($argument in @("/PID", $TargetProcessId.ToString(), "/T", "/F")) {
        [void]$startInfo.ArgumentList.Add($argument)
    }

    $killProcess = [Diagnostics.Process]::new()
    $killProcess.StartInfo = $startInfo
    try {
        $killClock = [Diagnostics.Stopwatch]::StartNew()
        if (-not $killProcess.Start()) {
            return [pscustomobject]@{
                Started = $false; TimedOut = $false; ExitCode = $null
            }
        }
        $remainingKillMilliseconds = [long]$LimitMilliseconds - $killClock.ElapsedMilliseconds
        if ($remainingKillMilliseconds -lt 0) {
            $remainingKillMilliseconds = 0
        }
        $exited = Wait-CiProcessExit `
            -Process $killProcess `
            -LimitMilliseconds $remainingKillMilliseconds
        if (-not $exited) {
            try {
                $killProcess.Kill($true)
                [void](Wait-CiProcessExit -Process $killProcess -LimitMilliseconds 2000)
            }
            catch { }
            return [pscustomobject]@{
                Started = $true; TimedOut = $true; ExitCode = $null
            }
        }
        [pscustomobject]@{
            Started = $true
            TimedOut = $false
            ExitCode = $killProcess.ExitCode
        }
    }
    finally {
        $killProcess.Dispose()
    }
}

function Complete-CiStreamCapture {
    param(
        [Parameter(Mandatory)][Diagnostics.Process]$Process,
        [Parameter(Mandatory)][Threading.Tasks.Task]$StandardOutputTask,
        [Parameter(Mandatory)][Threading.Tasks.Task]$StandardErrorTask,
        [Parameter(Mandatory)][Threading.CancellationTokenSource]$Cancellation,
        [int]$LimitMilliseconds = 10000)

    $combined = [Threading.Tasks.Task]::WhenAll(
        [Threading.Tasks.Task[]]@($StandardOutputTask, $StandardErrorTask))
    $captureError = $null
    try {
        $drainClock = [Diagnostics.Stopwatch]::StartNew()
        $completed = $combined.IsCompleted
        while (-not $completed -and $drainClock.ElapsedMilliseconds -lt $LimitMilliseconds) {
            $remaining = $LimitMilliseconds - $drainClock.ElapsedMilliseconds
            if ($remaining -le 0) {
                break
            }
            $completed = $combined.Wait([int][Math]::Min([long]250, $remaining))
        }
        if ($completed) {
            $combined.GetAwaiter().GetResult()
            return [pscustomobject]@{ Completed = $true; Error = $null }
        }
    }
    catch {
        $captureError = $_.Exception.Message
    }
    $Cancellation.Cancel()
    try { $Process.StandardOutput.BaseStream.Dispose() } catch { }
    try { $Process.StandardError.BaseStream.Dispose() } catch { }
    try { [void]$combined.Wait(2000) } catch { }
    [pscustomobject]@{
        Completed = $false
        Error = if ($null -ne $captureError) {
            $captureError
        }
        else {
            "Standard-output/error pipes did not reach EOF within the bounded drain."
        }
    }
}

function Copy-CiFileToConsole {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][IO.Stream]$Destination)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return
    }
    $source = [IO.File]::OpenRead($Path)
    try {
        $source.CopyTo($Destination)
        $Destination.Flush()
    }
    finally {
        $source.Dispose()
    }
}

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$evidenceModulePath = Join-Path $PSScriptRoot "CiTestEvidence.psm1"
Import-Module -Name $evidenceModulePath -Force

$runId = (Get-Date).ToUniversalTime().ToString(
    "yyyyMMddTHHmmssZ",
    [Globalization.CultureInfo]::InvariantCulture)
$runId += "-" + [Guid]::NewGuid().ToString("N")
$trxFileName = "test-results-$runId.trx"
$relativeEvidenceParent = "out\ci-test-run"
$evidenceParent = Join-Path $repositoryRoot $relativeEvidenceParent
$runnerLockPath = Join-Path $evidenceParent ".runner.lock"
$activeMarkerPath = Join-Path $evidenceParent ".active-or-stranded.json"

$runnerLock = $null
$process = $null
$streamCancellation = $null
$stdoutFile = $null
$stderrFile = $null
$stdoutTask = $null
$stderrTask = $null
$processStartAttempted = $false
$processStarted = $false
$parentExitObserved = $false
$streamDrainCompleted = $false
$streamCaptureError = $null
$taskkillResult = $null
$activeMarker = $null
$activeMarkerCreated = $false
$activeMarkerCleared = $false
$processId = $null
$timedOut = $false
$nativeTestProcessExitCode = $null
$taskkillSucceeded = $false
$fatalError = $null
$cleanupErrors = [Collections.Generic.List[string]]::new()
$runnerExitCode = 127
$evidenceRoot = $null
$stdoutPath = $null
$stderrPath = $null

try {
    try {
    Assert-CiPathContained -ContainmentRoot $repositoryRoot -Path $evidenceParent -Description "CI evidence parent"
    [void][IO.Directory]::CreateDirectory($evidenceParent)
    Assert-CiPathContained -ContainmentRoot $repositoryRoot -Path $evidenceParent -Description "CI evidence parent"
    Assert-CiPathContained -ContainmentRoot $repositoryRoot -Path $runnerLockPath -Description "CI runner lock"
    try {
        $runnerLock = [IO.FileStream]::new(
            $runnerLockPath,
            [IO.FileMode]::OpenOrCreate,
            [IO.FileAccess]::ReadWrite,
            [IO.FileShare]::None)
    }
    catch {
        throw "Another repository-scoped CI test runner owns '$runnerLockPath'; " +
            "no baseline or test process was started. $($_.Exception.Message)"
    }

    $lockAcquiredUtc = (Get-Date).ToUniversalTime()
    $lockBinding = [ordered]@{
        RunId = $runId
        ProcessId = $PID
        AcquiredUtc = $lockAcquiredUtc.ToString("O", [Globalization.CultureInfo]::InvariantCulture)
    }
    $lockBytes = [Text.UTF8Encoding]::new($false).GetBytes(($lockBinding | ConvertTo-Json -Compress))
    $runnerLock.SetLength(0)
    $runnerLock.Write($lockBytes, 0, $lockBytes.Length)
    $runnerLock.Flush($true)

    Assert-CiPathContained `
        -ContainmentRoot $evidenceParent `
        -Path $activeMarkerPath `
        -Description "active-or-stranded runner marker"
    if (Test-Path -LiteralPath $activeMarkerPath) {
        throw "A prior runner left '$activeMarkerPath'. Inspect its process/evidence state and remove " +
            "that exact marker only after human resolution; no new baseline or test process was started."
    }

    $relativeEvidenceRoot = Join-Path $relativeEvidenceParent $runId
    $activeMarker = [ordered]@{
        State = "preflight"
        RunId = $runId
        RunnerProcessId = $PID
        ChildProcessId = $null
        EvidenceRoot = $relativeEvidenceRoot
        CreatedUtc = (Get-Date).ToUniversalTime().ToString("O", [Globalization.CultureInfo]::InvariantCulture)
        UpdatedUtc = $null
        Resolution = "A later runner must not start until this exact marker is inspected and, if safe, removed by a human."
    }
    Write-CiJsonFile `
        -Value $activeMarker `
        -Path $activeMarkerPath `
        -ContainmentRoot $evidenceParent `
        -Description "active-or-stranded runner marker"
    $activeMarkerCreated = $true

    $repositoryStateBefore = Get-CiRepositoryState -RepositoryRoot $repositoryRoot
    $dotnetSdk = (& dotnet --version 2>$null)
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($dotnetSdk)) {
        throw "The .NET SDK identity could not be measured; the test process was not started."
    }
    $dotnetSdk = $dotnetSdk.Trim()

    $testSuites = @(Get-CiTestSuiteInventory -RepositoryRoot $repositoryRoot -TrxFileName $trxFileName)
    $testAssembliesBefore = @(Get-CiTestAssemblySnapshot -Suites $testSuites -RepositoryRoot $repositoryRoot)
    $evidenceBaseline = @(Get-CiTestEvidenceBaseline -Suites $testSuites)

    $evidenceRoot = Join-Path $repositoryRoot $relativeEvidenceRoot
    Assert-CiPathContained -ContainmentRoot $repositoryRoot -Path $evidenceRoot -Description "run evidence root"
    [void][IO.Directory]::CreateDirectory($evidenceRoot)
    Assert-CiPathContained -ContainmentRoot $repositoryRoot -Path $evidenceRoot -Description "run evidence root"

    $stdoutPath = Join-Path $evidenceRoot "test.stdout.log"
    $stderrPath = Join-Path $evidenceRoot "test.stderr.log"
    $summaryPath = Join-Path $evidenceRoot "summary.json"
    $stdoutFile = New-CiCreateNewFileStream `
        -Path $stdoutPath -ContainmentRoot $evidenceRoot -Description "test standard-output stream"
    $stderrFile = New-CiCreateNewFileStream `
        -Path $stderrPath -ContainmentRoot $evidenceRoot -Description "test standard-error stream"

    $activeMarker["State"] = "starting"
    $activeMarker["UpdatedUtc"] = (Get-Date).ToUniversalTime().ToString(
        "O",
        [Globalization.CultureInfo]::InvariantCulture)
    Update-CiJsonFileAtomically `
        -Value $activeMarker `
        -Path $activeMarkerPath `
        -ContainmentRoot $evidenceParent `
        -Description "active-or-stranded runner marker"

    $testArguments = @(
        "test"
        "OpenClassroomFoundry.slnx"
        "--no-build"
        "--configuration", "Release"
        "--logger", "console;verbosity=normal"
        "--logger", "trx;LogFileName=$trxFileName"
        "--collect", "XPlat Code Coverage"
    )
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = "dotnet"
    $startInfo.WorkingDirectory = $repositoryRoot
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($argument in $testArguments) {
        [void]$startInfo.ArgumentList.Add($argument)
    }

    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $startedUtc = (Get-Date).ToUniversalTime()
    $processStartAttempted = $true
    $processStarted = $process.Start()
    if (-not $processStarted) {
        throw "The solution-wide dotnet test process could not be started."
    }
    $processId = $process.Id
    $markerRunningUtc = (Get-Date).ToUniversalTime().ToString(
        "O",
        [Globalization.CultureInfo]::InvariantCulture)
    $activeMarker["State"] = "running"
    $activeMarker["ChildProcessId"] = $processId
    $activeMarker["UpdatedUtc"] = $markerRunningUtc
    Update-CiJsonFileAtomically `
        -Value $activeMarker `
        -Path $activeMarkerPath `
        -ContainmentRoot $evidenceParent `
        -Description "active-or-stranded runner marker"
    $streamCancellation = [Threading.CancellationTokenSource]::new()
    $stdoutTask = $process.StandardOutput.BaseStream.CopyToAsync($stdoutFile, 81920, $streamCancellation.Token)
    $stderrTask = $process.StandardError.BaseStream.CopyToAsync($stderrFile, 81920, $streamCancellation.Token)

    $remainingProcessMilliseconds = ([long]$ProcessLimitSeconds * 1000) - $clock.ElapsedMilliseconds
    if ($remainingProcessMilliseconds -lt 0) {
        $remainingProcessMilliseconds = 0
    }
    $parentExitObserved = Wait-CiProcessExit `
        -Process $process `
        -LimitMilliseconds $remainingProcessMilliseconds
    $timedOut = -not $parentExitObserved
    if ($timedOut) {
        $taskkillResult = Invoke-CiBoundedTaskKill -TargetProcessId $process.Id
        $parentExitObserved = Wait-CiProcessExit -Process $process -LimitMilliseconds 10000
    }

    if ($parentExitObserved) {
        $nativeTestProcessExitCode = $process.ExitCode
    }

    $streamResult = Complete-CiStreamCapture `
        -Process $process `
        -StandardOutputTask $stdoutTask `
        -StandardErrorTask $stderrTask `
        -Cancellation $streamCancellation
    $streamDrainCompleted = $streamResult.Completed
    $streamCaptureError = $streamResult.Error

    foreach ($capture in @(
            [pscustomobject]@{ Stream = $stdoutFile; Path = $stdoutPath; Description = "test standard-output stream" },
            [pscustomobject]@{ Stream = $stderrFile; Path = $stderrPath; Description = "test standard-error stream" })) {
        try { $capture.Stream.Flush($true) } finally { $capture.Stream.Dispose() }
        Assert-CiPathContained `
            -ContainmentRoot $evidenceRoot `
            -Path $capture.Path `
            -Description $capture.Description
    }
    $stdoutFile = $null
    $stderrFile = $null
    $clock.Stop()
    $finishedUtc = (Get-Date).ToUniversalTime()

    $repositoryStateAfter = $null
    $testAssembliesAfter = @()
    $identityErrors = [Collections.Generic.List[string]]::new()
    try {
        $repositoryStateAfter = Get-CiRepositoryState -RepositoryRoot $repositoryRoot
        if (-not [string]::Equals(
                $repositoryStateBefore.Commit,
                $repositoryStateAfter.Commit,
                [StringComparison]::OrdinalIgnoreCase)) {
            $identityErrors.Add("Repository HEAD changed during test execution.")
        }
        if (-not [string]::Equals(
                $repositoryStateBefore.StatusSha256,
                $repositoryStateAfter.StatusSha256,
                [StringComparison]::OrdinalIgnoreCase) -or
            -not [string]::Equals(
                $repositoryStateBefore.SourceContentSha256,
                $repositoryStateAfter.SourceContentSha256,
                [StringComparison]::OrdinalIgnoreCase)) {
            $identityErrors.Add("Tracked or untracked source state changed during test execution.")
        }
        $testAssembliesAfter = @(Get-CiTestAssemblySnapshot -Suites $testSuites -RepositoryRoot $repositoryRoot)
        foreach ($assemblyError in @(Get-CiTestAssemblyIdentityErrors `
                -Before $testAssembliesBefore `
                -After $testAssembliesAfter)) {
            $identityErrors.Add($assemblyError)
        }
    }
    catch {
        $identityErrors.Add("Post-test source/binary identity measurement failed: $($_.Exception.Message)")
    }

    $taskkillSucceeded = $null -ne $taskkillResult -and
        -not $taskkillResult.TimedOut -and
        $taskkillResult.ExitCode -eq 0
    $safeToStartAnotherRunner = $parentExitObserved -and
        $streamDrainCompleted -and
        (-not $timedOut -or $taskkillSucceeded)

    $trxEvidenceFiles = @()
    $coverageEvidenceFiles = @()
    $evidenceCopies = @()
    $suiteEvidence = @()
    $completenessErrors = @()
    $evidenceSnapshotError = $null
    if ($parentExitObserved) {
        try {
            $trxEvidenceRoot = Join-Path $evidenceRoot "trx"
            $coverageEvidenceRoot = Join-Path $evidenceRoot "coverage"
            foreach ($snapshotRoot in @($trxEvidenceRoot, $coverageEvidenceRoot)) {
                Assert-CiPathContained `
                    -ContainmentRoot $evidenceRoot `
                    -Path $snapshotRoot `
                    -Description "curated evidence directory"
                [void][IO.Directory]::CreateDirectory($snapshotRoot)
                Assert-CiPathContained `
                    -ContainmentRoot $evidenceRoot `
                    -Path $snapshotRoot `
                    -Description "curated evidence directory"
            }

            $suiteEvidence = @(Get-CiTestEvidenceDelta -Suites $testSuites -Baseline $evidenceBaseline)
            $completenessErrors = @(Get-CiTestEvidenceCompletenessErrors -EvidenceDelta $suiteEvidence)
            if (-not $timedOut -and $nativeTestProcessExitCode -eq 0 -and $completenessErrors.Count -gt 0) {
                throw "A successful test process did not retain exactly one valid current TRX and one " +
                    "valid new direct coverage report per expected suite: " +
                    ($completenessErrors -join " ")
            }

            foreach ($currentSuite in $suiteEvidence) {
                if ($currentSuite.TrxIsCurrent -and $currentSuite.TrxValidation.Valid) {
                    $trxName = $currentSuite.EvidenceStem + ".trx"
                    $trxDestination = Join-Path $trxEvidenceRoot $trxName
                    $trxCopy = Copy-CiEvidenceFile `
                        -SourcePath $currentSuite.TrxPath `
                        -SourceContainmentRoot $repositoryRoot `
                        -DestinationPath $trxDestination `
                        -DestinationContainmentRoot $evidenceRoot `
                        -ExpectedSourceSha256 $currentSuite.TrxSha256
                    $trxRelativePath = Join-Path "trx" $trxName
                    $trxEvidenceFiles += $trxRelativePath
                    $evidenceCopies += [pscustomobject]@{
                        SuiteName = $currentSuite.SuiteName; Kind = "TRX"; EvidenceFile = $trxRelativePath
                        SourceSha256 = $trxCopy.SourceSha256; CopiedSha256 = $trxCopy.CopiedSha256
                        CopyMatchesSource = $trxCopy.CopyMatchesSource
                    }
                }

                foreach ($coverageEvidence in @($currentSuite.NewCoverageEvidence |
                        Where-Object { $_.Validation.Valid })) {
                    $coveragePath = $coverageEvidence.Path
                    $coverageDirectoryName = [IO.Path]::GetFileName([IO.Path]::GetDirectoryName($coveragePath))
                    $coverageName = $currentSuite.EvidenceStem + "-" + $coverageDirectoryName + ".cobertura.xml"
                    $coverageDestination = Join-Path $coverageEvidenceRoot $coverageName
                    $coverageCopy = Copy-CiEvidenceFile `
                        -SourcePath $coveragePath `
                        -SourceContainmentRoot $repositoryRoot `
                        -DestinationPath $coverageDestination `
                        -DestinationContainmentRoot $evidenceRoot `
                        -ExpectedSourceSha256 $coverageEvidence.Sha256
                    $coverageRelativePath = Join-Path "coverage" $coverageName
                    $coverageEvidenceFiles += $coverageRelativePath
                    $evidenceCopies += [pscustomobject]@{
                        SuiteName = $currentSuite.SuiteName; Kind = "Cobertura"; EvidenceFile = $coverageRelativePath
                        SourceSha256 = $coverageCopy.SourceSha256; CopiedSha256 = $coverageCopy.CopiedSha256
                        CopyMatchesSource = $coverageCopy.CopyMatchesSource
                    }
                }
            }
            $trxEvidenceFiles = @($trxEvidenceFiles | Sort-Object)
            $coverageEvidenceFiles = @($coverageEvidenceFiles | Sort-Object)
            $evidenceCopies = @($evidenceCopies | Sort-Object -Property SuiteName, Kind, EvidenceFile)
        }
        catch {
            $evidenceSnapshotError = $_.Exception.Message
        }
    }

    $runnerExitCode = if (-not $safeToStartAnotherRunner) { 125 }
    elseif ($timedOut) { 124 }
    elseif ($null -ne $evidenceSnapshotError -or $identityErrors.Count -gt 0) { 126 }
    elseif ($null -eq $nativeTestProcessExitCode) { 125 }
    else { $nativeTestProcessExitCode }

    $receipt = [ordered]@{
        RunId = $runId
        Statement = "One solution-wide dotnet test process was bounded externally; no test filter, retry, blame mode, or test-level timeout was added."
        SourceToBinaryProvenance = "Records pre/post source state and Release test-assembly hashes; stability is established only when post-state exists and IdentityErrors is empty. --no-build does not prove source-to-binary provenance."
        SourceAndAssemblyIdentityStable = $null -ne $repositoryStateAfter -and $identityErrors.Count -eq 0
        RunnerLock = [ordered]@{
            RelativePath = Join-Path $relativeEvidenceParent ".runner.lock"
            BoundRunId = $runId; OwnerProcessId = $PID
            AcquiredUtc = $lockAcquiredUtc.ToString("O", [Globalization.CultureInfo]::InvariantCulture)
            HeldThroughReceipt = $true
        }
        ActiveOrStrandedMarker = [ordered]@{
            RelativePath = Join-Path $relativeEvidenceParent ".active-or-stranded.json"
            PresentThroughReceipt = $true
            ClearEligibleAfterReceipt = $safeToStartAnotherRunner
        }
        RepositoryCommit = $repositoryStateBefore.Commit
        RepositoryStateBefore = $repositoryStateBefore
        RepositoryStateAfter = $repositoryStateAfter
        DotnetSdk = $dotnetSdk
        TrxFileName = $trxFileName
        Command = [ordered]@{ FileName = "dotnet"; Arguments = $testArguments; WorkingDirectory = $repositoryRoot }
        ProcessId = $processId
        StartedUtc = $startedUtc.ToString("O", [Globalization.CultureInfo]::InvariantCulture)
        FinishedUtc = $finishedUtc.ToString("O", [Globalization.CultureInfo]::InvariantCulture)
        ElapsedMilliseconds = $clock.ElapsedMilliseconds
        ProcessLimitSeconds = $ProcessLimitSeconds
        ExitCode = $runnerExitCode
        TestProcessExitCode = $nativeTestProcessExitCode
        TimedOut = $timedOut
        Taskkill = if ($null -eq $taskkillResult) { $null } else {
            [ordered]@{
                Started = $taskkillResult.Started; TimedOut = $taskkillResult.TimedOut
                ExitCode = $taskkillResult.ExitCode; Succeeded = $taskkillSucceeded
            }
        }
        ParentExitObserved = $parentExitObserved
        OutputDrainCompleted = $streamDrainCompleted
        OutputDrainError = $streamCaptureError
        SafeToStartAnotherRunner = $safeToStartAnotherRunner
        DescendantExitGuarantee = "taskkill /T success, parent exit, and pipe EOF are required after timeout. This is not a Job Object or adversarial descendant-exit proof."
        StandardOutputFile = "test.stdout.log"
        StandardErrorFile = "test.stderr.log"
        ExpectedTestProjects = @($testSuites.ProjectPath)
        TestAssembliesBefore = $testAssembliesBefore
        TestAssembliesAfter = $testAssembliesAfter
        IdentityErrors = @($identityErrors)
        TrxEvidenceFiles = $trxEvidenceFiles
        CoverageEvidenceFiles = $coverageEvidenceFiles
        EvidenceCopies = $evidenceCopies
        EvidenceCompletenessErrors = $completenessErrors
        SuiteEvidence = @($suiteEvidence | ForEach-Object {
                [ordered]@{
                    SuiteName = $_.SuiteName; ProjectPath = $_.ProjectPath; CurrentTrx = $_.TrxIsCurrent
                    TrxSourceSha256 = $_.TrxSha256
                    TrxValidation = if ($null -eq $_.TrxValidation) { $null } else {
                        [ordered]@{
                            Valid = $_.TrxValidation.Valid; Error = $_.TrxValidation.Error
                            Total = $_.TrxValidation.Total; Executed = $_.TrxValidation.Executed
                            Passed = $_.TrxValidation.Passed
                        }
                    }
                    NewDirectCoverageCount = @($_.NewCoverageEvidence).Count
                    NewDirectCoverage = @($_.NewCoverageEvidence | ForEach-Object {
                            [ordered]@{
                                Path = [IO.Path]::GetRelativePath($repositoryRoot, $_.Path)
                                SourceSha256 = $_.Sha256; Valid = $_.Validation.Valid; Error = $_.Validation.Error
                                LinesValid = $_.Validation.LinesValid; LinesCovered = $_.Validation.LinesCovered
                                LineRate = $_.Validation.LineRate
                            }
                        })
                }
            })
        EvidenceSnapshotError = $evidenceSnapshotError
    }
    Assert-CiPathContained `
        -ContainmentRoot $evidenceParent `
        -Path $activeMarkerPath `
        -Description "active-or-stranded runner marker"
    if (-not (Test-Path -LiteralPath $activeMarkerPath -PathType Leaf)) {
        throw "The active-or-stranded marker disappeared before receipt creation: $activeMarkerPath"
    }
    Write-CiJsonFile `
        -Value $receipt `
        -Path $summaryPath `
        -ContainmentRoot $evidenceRoot `
        -Description "CI test summary"
    Assert-CiPathContained `
        -ContainmentRoot $evidenceParent `
        -Path $activeMarkerPath `
        -Description "active-or-stranded runner marker"
    if (-not (Test-Path -LiteralPath $activeMarkerPath -PathType Leaf)) {
        throw "The active-or-stranded marker disappeared while the receipt was written: $activeMarkerPath"
    }

    if ($safeToStartAnotherRunner) {
        Assert-CiPathContained `
            -ContainmentRoot $evidenceParent `
            -Path $activeMarkerPath `
            -Description "active-or-stranded runner marker"
        [IO.File]::Delete($activeMarkerPath)
        if (Test-Path -LiteralPath $activeMarkerPath) {
            throw "The completed runner could not clear its active marker: $activeMarkerPath"
        }
        $activeMarkerCleared = $true
    }
    }
    catch {
        $fatalError = $_
    }
    finally {
        $associatedProcessId = $null
        if ($processStartAttempted -and $null -ne $process) {
            try {
                $associatedProcessId = [int]$process.Id
                $processId = $associatedProcessId
                $processStarted = $true
            }
            catch [InvalidOperationException] {
                $processStarted = $false
            }
            catch {
                $cleanupErrors.Add("Child-process association probe failed: $($_.Exception.Message)")
            }
        }

        if ($processStarted -and -not $parentExitObserved) {
            try {
                $parentExitObserved = Wait-CiProcessExit -Process $process -LimitMilliseconds 0
                if (-not $parentExitObserved -and $null -eq $taskkillResult) {
                    $taskkillResult = Invoke-CiBoundedTaskKill -TargetProcessId $associatedProcessId
                }
                if (-not $parentExitObserved) {
                    $parentExitObserved = Wait-CiProcessExit -Process $process -LimitMilliseconds 10000
                }
                if ($parentExitObserved) {
                    $nativeTestProcessExitCode = $process.ExitCode
                }
            }
            catch {
                $cleanupErrors.Add("Process cleanup failed: $($_.Exception.Message)")
            }
        }
        $taskkillSucceeded = $null -ne $taskkillResult -and
            -not $taskkillResult.TimedOut -and
            $taskkillResult.ExitCode -eq 0

        if (-not $streamDrainCompleted -and
            $null -ne $stdoutTask -and
            $null -ne $stderrTask -and
            $null -ne $streamCancellation) {
            try {
                $cleanupStreamResult = Complete-CiStreamCapture `
                    -Process $process `
                    -StandardOutputTask $stdoutTask `
                    -StandardErrorTask $stderrTask `
                    -Cancellation $streamCancellation
                $streamDrainCompleted = $cleanupStreamResult.Completed
                $streamCaptureError = $cleanupStreamResult.Error
            }
            catch {
                $streamCaptureError = $_.Exception.Message
                $cleanupErrors.Add("Output-capture cleanup failed: $streamCaptureError")
            }
        }

        if ($null -ne $streamCancellation) {
            try { $streamCancellation.Cancel() } catch {
                $cleanupErrors.Add("Output-capture cancellation failed: $($_.Exception.Message)")
            }
        }
        if ($processStarted -and $null -ne $process) {
            try { $process.StandardOutput.BaseStream.Dispose() } catch {
                $cleanupErrors.Add("Standard-output pipe disposal failed: $($_.Exception.Message)")
            }
            try { $process.StandardError.BaseStream.Dispose() } catch {
                $cleanupErrors.Add("Standard-error pipe disposal failed: $($_.Exception.Message)")
            }
        }
        foreach ($task in @($stdoutTask, $stderrTask)) {
            if ($null -ne $task) {
                try { [void]$task.Wait(2000) } catch { }
            }
        }
        foreach ($stream in @($stdoutFile, $stderrFile)) {
            if ($null -ne $stream) {
                try { $stream.Flush($true) } catch {
                    $cleanupErrors.Add("Partial output flush failed: $($_.Exception.Message)")
                }
                try { $stream.Dispose() } catch {
                    $cleanupErrors.Add("Partial output stream disposal failed: $($_.Exception.Message)")
                }
            }
        }
        if ($null -ne $streamCancellation) {
            try { $streamCancellation.Dispose() } catch {
                $cleanupErrors.Add("Output-capture token disposal failed: $($_.Exception.Message)")
            }
        }
        if ($null -ne $process) {
            try { $process.Dispose() } catch {
                $cleanupErrors.Add("Test-process handle disposal failed: $($_.Exception.Message)")
            }
        }

        if ($activeMarkerCreated -and -not $activeMarkerCleared) {
            if (-not $processStarted) {
                try {
                    Assert-CiPathContained `
                        -ContainmentRoot $evidenceParent `
                        -Path $activeMarkerPath `
                        -Description "active-or-stranded runner marker"
                    [IO.File]::Delete($activeMarkerPath)
                    $activeMarkerCleared = -not (Test-Path -LiteralPath $activeMarkerPath)
                }
                catch {
                    $cleanupErrors.Add("Pre-start marker cleanup failed: $($_.Exception.Message)")
                }
            }
            else {
                try {
                    $activeMarker["State"] = "stranded"
                    $activeMarker["ChildProcessId"] = $associatedProcessId
                    $activeMarker["UpdatedUtc"] = (Get-Date).ToUniversalTime().ToString(
                        "O",
                        [Globalization.CultureInfo]::InvariantCulture)
                    $activeMarker["Taskkill"] = if ($null -eq $taskkillResult) { $null } else {
                        [ordered]@{
                            Started = $taskkillResult.Started
                            TimedOut = $taskkillResult.TimedOut
                            ExitCode = $taskkillResult.ExitCode
                            Succeeded = $taskkillSucceeded
                        }
                    }
                    $activeMarker["ParentExitObserved"] = $parentExitObserved
                    $activeMarker["OutputDrainCompleted"] = $streamDrainCompleted
                    $activeMarker["OutputDrainError"] = $streamCaptureError
                    $activeMarker["CleanupFindings"] = @($cleanupErrors)
                    Update-CiJsonFileAtomically `
                        -Value $activeMarker `
                        -Path $activeMarkerPath `
                        -ContainmentRoot $evidenceParent `
                        -Description "active-or-stranded runner marker"
                }
                catch {
                    $cleanupErrors.Add("Stranded marker update failed: $($_.Exception.Message)")
                }
            }
        }
    }
}
finally {
    if ($null -ne $runnerLock) {
        try { $runnerLock.Dispose() } catch {
            $cleanupErrors.Add("Runner-lock disposal failed: $($_.Exception.Message)")
        }
    }
}

if ($null -ne $fatalError) {
    $cleanupSuffix = if ($cleanupErrors.Count -eq 0) { "" } else {
        " Cleanup findings: " + ($cleanupErrors -join " ")
    }
    throw ($fatalError.Exception.Message + $cleanupSuffix)
}
if ($cleanupErrors.Count -gt 0) {
    throw ($cleanupErrors -join " ")
}

if ($streamDrainCompleted) {
    Copy-CiFileToConsole -Path $stdoutPath -Destination ([Console]::OpenStandardOutput())
    Copy-CiFileToConsole -Path $stderrPath -Destination ([Console]::OpenStandardError())
}
Write-Host "Bounded test evidence retained at $evidenceRoot"
if (-not $activeMarkerCleared) {
    [Console]::Error.WriteLine(
        "The active-or-stranded marker remains at $activeMarkerPath; no later runner may start until human resolution.")
}
exit $runnerExitCode
