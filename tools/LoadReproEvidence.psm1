# SPDX-License-Identifier: GPL-3.0-or-later
Set-StrictMode -Version Latest

Import-Module -Name (Join-Path $PSScriptRoot "CiTestEvidence.psm1") -Force

function Get-LoadReproStringSha256 {
    param(
        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [string]$Value
    )

    $bytes = [Text.UTF8Encoding]::new($false).GetBytes($Value)
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
}

function Wait-LoadReproProcessExit {
    param(
        [Parameter(Mandatory)][Diagnostics.Process]$Process,
        [Parameter(Mandatory)][long]$LimitMilliseconds
    )

    $clock = [Diagnostics.Stopwatch]::StartNew()
    while ($true) {
        $remaining = $LimitMilliseconds - $clock.ElapsedMilliseconds
        if ($remaining -le 0) {
            return $Process.WaitForExit(0)
        }

        $slice = [int][Math]::Min([long]250, $remaining)
        if ($Process.WaitForExit($slice)) {
            return $true
        }
    }
}

function Invoke-LoadReproBoundedTaskKill {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateRange(1, [int]::MaxValue)]
        [int]$TargetProcessId,

        [ValidateRange(1, 60000)]
        [int]$LimitMilliseconds = 10000,

        [ValidateRange(1, 10000)]
        [int]$CleanupLimitMilliseconds = 2000
    )

    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = Join-Path $env:SystemRoot "System32\taskkill.exe"
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    foreach ($argument in @("/PID", $TargetProcessId.ToString(), "/T", "/F")) {
        [void]$startInfo.ArgumentList.Add($argument)
    }

    $killProcess = [Diagnostics.Process]::new()
    $killProcess.StartInfo = $startInfo
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $started = $false
    $timedOut = $false
    $exitCode = $null
    $helperExitObserved = $false
    $startError = $null
    $cleanupError = $null
    try {
        try {
            $started = $killProcess.Start()
        }
        catch {
            $startError = $_.Exception.Message
        }

        if ($started) {
            $remainingMilliseconds = [long]$LimitMilliseconds - $clock.ElapsedMilliseconds
            if ($remainingMilliseconds -lt 0) {
                $remainingMilliseconds = 0
            }
            $helperExitObserved = Wait-LoadReproProcessExit `
                -Process $killProcess `
                -LimitMilliseconds $remainingMilliseconds
            if (-not $helperExitObserved) {
                $timedOut = $true
                try {
                    $killProcess.Kill($true)
                }
                catch {
                    $cleanupError = $_.Exception.Message
                }
                $helperExitObserved = Wait-LoadReproProcessExit `
                    -Process $killProcess `
                    -LimitMilliseconds $CleanupLimitMilliseconds
            }
            if ($helperExitObserved) {
                $killProcess.WaitForExit()
                $exitCode = $killProcess.ExitCode
            }
        }

        [pscustomobject]@{
            Started = $started
            TimedOut = $timedOut
            ExitCode = $exitCode
            HelperExitObserved = $helperExitObserved
            StartError = $startError
            CleanupError = $cleanupError
            ElapsedMilliseconds = $clock.ElapsedMilliseconds
        }
    }
    finally {
        $killProcess.Dispose()
    }
}

function Enter-LoadReproEvidenceLock {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$RepositoryRoot,
        [Parameter(Mandatory)][string]$RunId,
        [Parameter(Mandatory)][string]$HarnessName
    )

    # This is deliberately a cooperative lock shared by the two load-reproduction
    # harnesses. An arbitrary builder does not honor it, so the harnesses also
    # compare source and output identities after every completed batch.
    $resolvedRepositoryRoot = [IO.Path]::GetFullPath($RepositoryRoot)
    $outRoot = Join-Path $resolvedRepositoryRoot "out"
    Assert-CiPathContained `
        -ContainmentRoot $resolvedRepositoryRoot `
        -Path $outRoot `
        -Description "load-reproduction evidence directory"
    [void][IO.Directory]::CreateDirectory($outRoot)
    Assert-CiPathContained `
        -ContainmentRoot $resolvedRepositoryRoot `
        -Path $outRoot `
        -Description "load-reproduction evidence directory"

    $lockPath = Join-Path $outRoot ".load-repro-evidence.lock"
    Assert-CiPathContained `
        -ContainmentRoot $outRoot `
        -Path $lockPath `
        -Description "shared load-reproduction evidence lock"
    try {
        $stream = [IO.FileStream]::new(
            $lockPath,
            [IO.FileMode]::OpenOrCreate,
            [IO.FileAccess]::ReadWrite,
            [IO.FileShare]::None)
    }
    catch {
        throw "Another load-reproduction evidence process owns '$lockPath'; " +
            "no source preflight, build, or repetition was started. $($_.Exception.Message)"
    }

    try {
        $binding = [ordered]@{
            RunId = $RunId
            Harness = $HarnessName
            OwnerProcessId = $PID
            AcquiredUtc = (Get-Date).ToUniversalTime().ToString(
                "O",
                [Globalization.CultureInfo]::InvariantCulture)
        }
        $bytes = [Text.UTF8Encoding]::new($false).GetBytes(
            (($binding | ConvertTo-Json -Compress) + [Environment]::NewLine))
        $stream.SetLength(0)
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Flush($true)

        [pscustomobject]@{
            Path = $lockPath
            RelativePath = [IO.Path]::GetRelativePath($resolvedRepositoryRoot, $lockPath)
            Stream = $stream
        }
    }
    catch {
        $stream.Dispose()
        throw
    }
}

function New-LoadReproEvidenceDirectory {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$RepositoryRoot,
        [Parameter(Mandatory)]
        [ValidateSet("uia-load-repro", "image-load-repro")]
        [string]$EvidenceBaseName,
        [Parameter(Mandatory)]
        [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]*$')]
        [string]$RunId
    )

    if ($RunId -in @(".", "..")) {
        throw "The load-reproduction run ID must be one non-relative path segment."
    }

    $resolvedRepositoryRoot = [IO.Path]::GetFullPath($RepositoryRoot)
    Assert-CiPathContained `
        -ContainmentRoot $resolvedRepositoryRoot `
        -Path $resolvedRepositoryRoot `
        -Description "load-reproduction repository root"

    $outRoot = Join-Path $resolvedRepositoryRoot "out"
    Assert-CiPathContained `
        -ContainmentRoot $resolvedRepositoryRoot `
        -Path $outRoot `
        -Description "load-reproduction out directory"
    [void][IO.Directory]::CreateDirectory($outRoot)
    Assert-CiPathContained `
        -ContainmentRoot $resolvedRepositoryRoot `
        -Path $outRoot `
        -Description "load-reproduction out directory"

    $baseRoot = Join-Path $outRoot $EvidenceBaseName
    Assert-CiPathContained `
        -ContainmentRoot $resolvedRepositoryRoot `
        -Path $baseRoot `
        -Description "load-reproduction evidence base"
    [void][IO.Directory]::CreateDirectory($baseRoot)
    Assert-CiPathContained `
        -ContainmentRoot $resolvedRepositoryRoot `
        -Path $baseRoot `
        -Description "load-reproduction evidence base"

    $runRoot = Join-Path $baseRoot $RunId
    Assert-CiPathContained `
        -ContainmentRoot $resolvedRepositoryRoot `
        -Path $runRoot `
        -Description "load-reproduction evidence run directory"
    if (Test-Path -LiteralPath $runRoot) {
        throw "The load-reproduction evidence run directory already exists: $runRoot"
    }

    [void][IO.Directory]::CreateDirectory($runRoot)
    Assert-CiPathContained `
        -ContainmentRoot $resolvedRepositoryRoot `
        -Path $runRoot `
        -Description "load-reproduction evidence run directory"

    [pscustomobject]@{
        Path = $runRoot
        RelativePath = [IO.Path]::GetRelativePath($resolvedRepositoryRoot, $runRoot)
    }
}

function Get-LoadReproRepositoryState {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$RepositoryRoot)

    Get-CiRepositoryState -RepositoryRoot $RepositoryRoot
}

function Assert-LoadReproCleanRepositoryState {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object]$State,
        [string]$Context = "repository tree"
    )

    if ($State.Dirty -or $State.StatusEntryCount -ne 0) {
        throw "The $Context is not clean; commit or set aside every change before collecting source-bound evidence."
    }
}

function Get-LoadReproOutputIdentity {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$RepositoryRoot,
        [Parameter(Mandatory)][string]$OutputRoot,
        [Parameter(Mandatory)][string]$RequiredTestAssembly
    )

    $resolvedRepositoryRoot = [IO.Path]::GetFullPath($RepositoryRoot)
    $resolvedOutputRoot = [IO.Path]::GetFullPath($OutputRoot)
    $resolvedTestAssembly = [IO.Path]::GetFullPath($RequiredTestAssembly)
    Assert-CiPathContained `
        -ContainmentRoot $resolvedRepositoryRoot `
        -Path $resolvedOutputRoot `
        -Description "load-reproduction test output root"
    Assert-CiPathContained `
        -ContainmentRoot $resolvedOutputRoot `
        -Path $resolvedTestAssembly `
        -Description "load-reproduction test assembly"
    if (-not (Test-Path -LiteralPath $resolvedOutputRoot -PathType Container)) {
        throw "The exact built test-output root does not exist: $resolvedOutputRoot"
    }
    if (-not (Test-Path -LiteralPath $resolvedTestAssembly -PathType Leaf)) {
        throw "The exact built test assembly does not exist: $resolvedTestAssembly"
    }

    foreach ($directory in @(Get-ChildItem -LiteralPath $resolvedOutputRoot -Directory -Recurse -Force)) {
        Assert-CiPathContained `
            -ContainmentRoot $resolvedOutputRoot `
            -Path $directory.FullName `
            -Description "load-reproduction test-output directory"
        if (($directory.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "The load-reproduction test-output identity cannot include a reparse point: $($directory.FullName)"
        }
    }

    $identityFiles = [Collections.Generic.List[object]]::new()
    [string[]]$relativePaths = @(
        Get-ChildItem -LiteralPath $resolvedOutputRoot -File -Recurse -Force |
            ForEach-Object {
                ([IO.Path]::GetRelativePath($resolvedOutputRoot, $_.FullName)).Replace(
                    [IO.Path]::DirectorySeparatorChar,
                    '/')
            })
    [Array]::Sort($relativePaths, [StringComparer]::Ordinal)
    foreach ($relativePath in $relativePaths) {
        $fullPath = Join-Path $resolvedOutputRoot $relativePath
        Assert-CiPathContained `
            -ContainmentRoot $resolvedOutputRoot `
            -Path $fullPath `
            -Description "load-reproduction test-output file"
        $file = Get-Item -LiteralPath $fullPath -Force
        if (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "The load-reproduction test-output identity cannot include a reparse point: $fullPath"
        }
        $identityFiles.Add([pscustomobject][ordered]@{
                Path = $relativePath
                Length = $file.Length
                Sha256 = (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash
            })
    }

    if ($identityFiles.Count -eq 0) {
        throw "The exact built test-output root is empty: $resolvedOutputRoot"
    }
    $assemblyRelativePath = [IO.Path]::GetRelativePath(
        $resolvedOutputRoot,
        $resolvedTestAssembly).Replace([IO.Path]::DirectorySeparatorChar, '/')
    $assemblyEntry = @($identityFiles | Where-Object { $_.Path -ceq $assemblyRelativePath })
    if ($assemblyEntry.Count -ne 1 -or $assemblyEntry[0].Length -le 0) {
        throw "The exact built test assembly is not one non-empty member of the output identity: $resolvedTestAssembly"
    }

    $manifestLines = @($identityFiles | ForEach-Object {
            "$($_.Path)`0$($_.Length)`0$($_.Sha256)"
        })
    [pscustomobject][ordered]@{
        OutputRoot = [IO.Path]::GetRelativePath($resolvedRepositoryRoot, $resolvedOutputRoot)
        RequiredTestAssembly = [IO.Path]::GetRelativePath($resolvedRepositoryRoot, $resolvedTestAssembly)
        RequiredTestAssemblySha256 = $assemblyEntry[0].Sha256
        FileCount = $identityFiles.Count
        TotalBytes = [long](($identityFiles | Measure-Object -Property Length -Sum).Sum)
        ManifestSha256 = Get-LoadReproStringSha256 -Value ($manifestLines -join "`n")
        Files = @($identityFiles)
    }
}

function Get-LoadReproIdentityErrors {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object]$RepositoryBefore,
        [Parameter(Mandatory)][object]$RepositoryAfter,
        [Parameter(Mandatory)][object]$OutputBefore,
        [Parameter(Mandatory)][object]$OutputAfter
    )

    if ($RepositoryBefore.Dirty -or $RepositoryAfter.Dirty -or
        $RepositoryBefore.StatusEntryCount -ne 0 -or $RepositoryAfter.StatusEntryCount -ne 0) {
        "The repository tree was not clean at both load-reproduction identity boundaries."
    }
    if (-not [string]::Equals(
            [string]$RepositoryBefore.Commit,
            [string]$RepositoryAfter.Commit,
            [StringComparison]::OrdinalIgnoreCase)) {
        "Repository HEAD changed during the load-reproduction batch."
    }
    if (-not [string]::Equals(
            [string]$RepositoryBefore.StatusSha256,
            [string]$RepositoryAfter.StatusSha256,
            [StringComparison]::OrdinalIgnoreCase) -or
        -not [string]::Equals(
            [string]$RepositoryBefore.SourceContentSha256,
            [string]$RepositoryAfter.SourceContentSha256,
            [StringComparison]::OrdinalIgnoreCase) -or
        $RepositoryBefore.SourceFileCount -ne $RepositoryAfter.SourceFileCount) {
        "Tracked or untracked nonignored source identity changed during the load-reproduction batch."
    }
    if (-not [string]::Equals(
            [string]$OutputBefore.OutputRoot,
            [string]$OutputAfter.OutputRoot,
            [StringComparison]::Ordinal) -or
        -not [string]::Equals(
            [string]$OutputBefore.RequiredTestAssembly,
            [string]$OutputAfter.RequiredTestAssembly,
            [StringComparison]::Ordinal) -or
        -not [string]::Equals(
            [string]$OutputBefore.ManifestSha256,
            [string]$OutputAfter.ManifestSha256,
            [StringComparison]::OrdinalIgnoreCase) -or
        $OutputBefore.FileCount -ne $OutputAfter.FileCount -or
        $OutputBefore.TotalBytes -ne $OutputAfter.TotalBytes) {
        "The exact built test-output/dependency identity changed during the load-reproduction batch."
    }
}

function Write-LoadReproJsonFile {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object]$Value,
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$ContainmentRoot,
        [string]$Description = "load-reproduction evidence file"
    )

    $resolvedContainmentRoot = [IO.Path]::GetFullPath($ContainmentRoot)
    $resolvedPath = [IO.Path]::GetFullPath($Path)
    Assert-CiPathContained `
        -ContainmentRoot $resolvedContainmentRoot `
        -Path $resolvedPath `
        -Description $Description
    if (Test-Path -LiteralPath $resolvedPath) {
        throw "$Description already exists: $resolvedPath"
    }

    $options = [IO.FileOptions]([IO.FileOptions]::WriteThrough)
    $stream = [IO.FileStream]::new(
        $resolvedPath,
        [IO.FileMode]::CreateNew,
        [IO.FileAccess]::Write,
        [IO.FileShare]::Read,
        4096,
        $options)
    try {
        $json = $Value | ConvertTo-Json -Depth 12
        $bytes = [Text.UTF8Encoding]::new($false).GetBytes($json + [Environment]::NewLine)
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Flush($true)
    }
    finally {
        $stream.Dispose()
    }
    Assert-CiPathContained `
        -ContainmentRoot $resolvedContainmentRoot `
        -Path $resolvedPath `
        -Description $Description
}

Export-ModuleMember -Function @(
    "Invoke-LoadReproBoundedTaskKill",
    "Enter-LoadReproEvidenceLock",
    "New-LoadReproEvidenceDirectory",
    "Get-LoadReproRepositoryState",
    "Assert-LoadReproCleanRepositoryState",
    "Get-LoadReproOutputIdentity",
    "Get-LoadReproIdentityErrors",
    "Write-LoadReproJsonFile"
)
