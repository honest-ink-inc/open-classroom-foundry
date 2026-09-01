# SPDX-License-Identifier: GPL-3.0-or-later
# Verifies the deliberately two-commit Option-A ratification record without
# treating a later merge commit as the record-only C2 itself.
[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot ".."),
    [string]$OutputPath = "out/recipe-identity-ratification/history-receipt.json",
    [switch]$RequireRatified
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$packetRelativePath = "docs/adr/recipe-identity-disposition-packet.md"
$expectedC2ChangedFiles = [string[]]@(
    "README.md",
    "docs/README.md",
    "docs/adr/README.md",
    "docs/adr/recipe-identity-disposition-packet.md",
    "docs/handover/2026-09-01-forge-integration-handover.md",
    "tests/Unit/RecipeIdentityDispositionPacketTests.cs"
)
[Array]::Sort($expectedC2ChangedFiles, [StringComparer]::Ordinal)

$repositoryFullPath = [IO.Path]::GetFullPath($RepositoryRoot)
$outRoot = [IO.Path]::GetFullPath((Join-Path $repositoryFullPath "out"))
$receiptPath = if ([IO.Path]::IsPathFullyQualified($OutputPath)) {
    [IO.Path]::GetFullPath($OutputPath)
}
else {
    [IO.Path]::GetFullPath((Join-Path $repositoryFullPath $OutputPath))
}
$pathComparison = if ($IsWindows) {
    [StringComparison]::OrdinalIgnoreCase
}
else {
    [StringComparison]::Ordinal
}
$outPrefix = $outRoot.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) +
    [IO.Path]::DirectorySeparatorChar
if (-not $receiptPath.StartsWith($outPrefix, $pathComparison)) {
    throw "The ratification receipt must be written beneath the repository's bounded out directory."
}

$receipt = [ordered]@{
    Schema = "honest-ink.recipe-identity-ratification-history-receipt.v1"
    Outcome = "failed"
    RequireRatified = [bool]$RequireRatified
    PacketPath = $packetRelativePath
    Head = $null
    CandidateFreezeCommit = $null
    RatificationCommit = $null
    CandidateFreezeIsImmediateSingleParent = $false
    CandidateFreezeIsAncestorOfHead = $false
    RatificationIsAncestorOfHead = $false
    ExpectedC2ChangedFiles = $expectedC2ChangedFiles
    ActualC2ChangedFiles = [string[]]@()
    FailureCode = $null
    FailureMessage = $null
}

function Write-RatificationReceipt {
    $parent = [IO.Path]::GetDirectoryName($receiptPath)
    [void][IO.Directory]::CreateDirectory($parent)
    $json = $receipt | ConvertTo-Json -Depth 5
    [IO.File]::WriteAllText(
        $receiptPath,
        $json + [Environment]::NewLine,
        [Text.UTF8Encoding]::new($false))
}

function Stop-RatificationVerification {
    param(
        [Parameter(Mandatory)][string]$Code,
        [Parameter(Mandatory)][string]$Message)

    $receipt.FailureCode = $Code
    $receipt.FailureMessage = $Message
    throw $Message
}

function Invoke-GitText {
    param(
        [Parameter(Mandatory)][string[]]$Arguments,
        [Parameter(Mandatory)][string]$Operation)

    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = "git"
    $startInfo.WorkingDirectory = $repositoryFullPath
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.StandardOutputEncoding = [Text.UTF8Encoding]::new($false)
    $startInfo.StandardErrorEncoding = [Text.UTF8Encoding]::new($false)
    [void]$startInfo.ArgumentList.Add("-C")
    [void]$startInfo.ArgumentList.Add($repositoryFullPath)
    foreach ($argument in $Arguments) {
        [void]$startInfo.ArgumentList.Add($argument)
    }

    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    try {
        if (-not $process.Start()) {
            Stop-RatificationVerification "git-command-failed" "Git did not start while $Operation."
        }
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(30000)) {
            try { $process.Kill($true) } catch { }
            Stop-RatificationVerification "git-command-timeout" "Git exceeded 30 seconds while $Operation."
        }
        $stdout = $stdoutTask.GetAwaiter().GetResult()
        [void]$stderrTask.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) {
            Stop-RatificationVerification "git-command-failed" "Git failed while $Operation."
        }

        return $stdout.TrimEnd("`r", "`n")
    }
    finally {
        $process.Dispose()
    }
}

function Test-GitAncestor {
    param(
        [Parameter(Mandatory)][string]$Ancestor,
        [Parameter(Mandatory)][string]$Descendant)

    & git -C $repositoryFullPath merge-base --is-ancestor $Ancestor $Descendant 2>$null
    if ($LASTEXITCODE -eq 0) {
        return $true
    }
    if ($LASTEXITCODE -eq 1) {
        return $false
    }

    Stop-RatificationVerification "git-command-failed" "Git failed while checking ratification ancestry."
}

function Get-RecordedCandidateFreeze {
    param([Parameter(Mandatory)][string]$PacketText)

    $match = [regex]::Match(
        $PacketText,
        '(?m)^\| Exact candidate freeze state \| `(?<hash>[0-9a-f]{40})`(?: [^|]*)?\|$')
    if (-not $match.Success) {
        return $null
    }

    return $match.Groups["hash"].Value
}

function Test-PendingPacket {
    param([Parameter(Mandatory)][string]$PacketText)

    return [regex]::IsMatch(
        $PacketText,
        '(?m)^\*\*Status:\*\* DECIDED — OPTION A; candidate freeze hash pending in local C1; do not push this transitional state\r?$') -and
        [regex]::IsMatch(
            $PacketText,
            '(?m)^\| Exact candidate freeze state \| `PENDING-C1-COMMIT-HASH` [^|]*\|\r?$')
}

function Test-RatifiedPacket {
    param([Parameter(Mandatory)][string]$PacketText)

    return [regex]::IsMatch(
        $PacketText,
        '(?m)^\*\*Status:\*\* RATIFIED — OPTION A\r?$') -and
        [regex]::IsMatch(
            $PacketText,
            '(?m)^\| Status \| `RATIFIED — OPTION A` \|\r?$')
}

try {
    $gitRoot = Invoke-GitText @("rev-parse", "--show-toplevel") "resolving the repository root"
    $gitRootFullPath = [IO.Path]::GetFullPath($gitRoot.Trim())
    if (-not [string]::Equals($gitRootFullPath, $repositoryFullPath, $pathComparison)) {
        Stop-RatificationVerification "repository-root-mismatch" "The supplied repository root was not the Git worktree root."
    }

    $head = (Invoke-GitText @("rev-parse", "HEAD") "resolving HEAD").Trim()
    if ($head -notmatch '^[0-9a-f]{40}$') {
        Stop-RatificationVerification "invalid-head" "HEAD did not resolve to one full Git commit ID."
    }
    $receipt.Head = $head

    $workingPacketPath = Join-Path $repositoryFullPath $packetRelativePath
    if (-not [IO.File]::Exists($workingPacketPath)) {
        Stop-RatificationVerification "packet-missing" "The recipe-identity disposition packet is missing."
    }
    $workingPacket = [IO.File]::ReadAllText($workingPacketPath)
    $hasPendingStatus = [regex]::IsMatch(
        $workingPacket,
        '(?m)^\*\*Status:\*\* DECIDED — OPTION A; candidate freeze hash pending in local C1; do not push this transitional state\r?$')
    $hasPendingHash = [regex]::IsMatch(
        $workingPacket,
        '(?m)^\| Exact candidate freeze state \| `PENDING-C1-COMMIT-HASH` [^|]*\|\r?$')
    if ($hasPendingStatus -or $hasPendingHash) {
        if (-not ($hasPendingStatus -and $hasPendingHash)) {
            Stop-RatificationVerification "pending-state-malformed" "The transitional packet did not carry both exact pending markers."
        }
        if ($RequireRatified) {
            Stop-RatificationVerification "ratification-required" "The workflow requires RATIFIED C2 history; the packet is still the local C1 transition."
        }

        $receipt.Outcome = "skipped-pending-c1"
        $receipt.FailureCode = $null
        $receipt.FailureMessage = $null
        Write-RatificationReceipt
        Write-Host "Recipe-identity history verification skipped for the explicit local C1 pending state."
        return
    }

    $packetStatus = (Invoke-GitText @("status", "--porcelain=v1", "--", $packetRelativePath) "checking the packet worktree state").Trim()
    if (-not [string]::IsNullOrEmpty($packetStatus)) {
        Stop-RatificationVerification "packet-dirty" "The RATIFIED packet must be verified from committed Git history, not working-tree bytes."
    }

    $headPacket = Invoke-GitText @("show", "HEAD:$packetRelativePath") "reading the packet at HEAD"
    if (-not (Test-RatifiedPacket $headPacket)) {
        Stop-RatificationVerification "ratified-status-missing" "The committed packet did not carry both exact RATIFIED Option-A status records."
    }

    $candidateFreeze = Get-RecordedCandidateFreeze $headPacket
    if ($null -eq $candidateFreeze) {
        Stop-RatificationVerification "candidate-freeze-missing" "The RATIFIED packet did not record one full lowercase C1 commit ID."
    }
    $receipt.CandidateFreezeCommit = $candidateFreeze

    $candidateExists = @(& git -C $repositoryFullPath cat-file -e "$candidateFreeze^{commit}" 2>$null)
    if ($LASTEXITCODE -ne 0) {
        Stop-RatificationVerification "candidate-freeze-unavailable" "The recorded C1 commit is unavailable; full Git history is required."
    }

    $receipt.CandidateFreezeIsAncestorOfHead = Test-GitAncestor $candidateFreeze $head
    if (-not $receipt.CandidateFreezeIsAncestorOfHead) {
        Stop-RatificationVerification "candidate-freeze-not-ancestor" "The recorded C1 commit is not an ancestor of HEAD."
    }

    $descendantText = Invoke-GitText @(
        "rev-list",
        "--ancestry-path",
        "$candidateFreeze..$head") "enumerating descendants of the recorded C1"
    $descendants = @($descendantText -split "`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    $ratificationCandidates = [Collections.Generic.List[string]]::new()
    foreach ($commit in $descendants) {
        $parentLine = (Invoke-GitText @("rev-list", "--parents", "-n", "1", $commit) "reading a candidate commit's parents").Trim()
        $parts = @($parentLine -split ' ' | Where-Object { $_.Length -gt 0 })
        if ($parts.Count -ne 2 -or
            -not [string]::Equals($parts[1], $candidateFreeze, [StringComparison]::Ordinal)) {
            continue
        }

        & git -C $repositoryFullPath cat-file -e "$commit`:$packetRelativePath" 2>$null
        if ($LASTEXITCODE -ne 0) {
            continue
        }
        $candidatePacket = Invoke-GitText @("show", "$commit`:$packetRelativePath") "reading a candidate ratification packet"
        if ((Test-RatifiedPacket $candidatePacket) -and
            [string]::Equals(
                (Get-RecordedCandidateFreeze $candidatePacket),
                $candidateFreeze,
                [StringComparison]::Ordinal)) {
            $ratificationCandidates.Add($commit)
        }
    }

    if ($ratificationCandidates.Count -ne 1) {
        Stop-RatificationVerification "ratification-child-count" "Exactly one single-parent RATIFIED C2 must immediately follow the recorded C1."
    }
    $ratificationCommit = $ratificationCandidates[0]
    $receipt.RatificationCommit = $ratificationCommit
    $receipt.CandidateFreezeIsImmediateSingleParent = $true

    $receipt.RatificationIsAncestorOfHead = Test-GitAncestor $ratificationCommit $head
    if (-not $receipt.RatificationIsAncestorOfHead) {
        Stop-RatificationVerification "ratification-not-ancestor" "The located RATIFIED C2 commit is not an ancestor of HEAD."
    }

    $changedText = Invoke-GitText @(
        "diff",
        "--no-renames",
        "--name-only",
        $candidateFreeze,
        $ratificationCommit,
        "--") "reading the exact C1-to-C2 changed-file set"
    $actualChangedFiles = [string[]]@(
        $changedText -split "`n" |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            ForEach-Object { $_.Trim().Replace('\', '/') })
    [Array]::Sort($actualChangedFiles, [StringComparer]::Ordinal)
    $receipt.ActualC2ChangedFiles = $actualChangedFiles

    if ($actualChangedFiles.Count -ne $expectedC2ChangedFiles.Count) {
        Stop-RatificationVerification "c2-file-set-mismatch" "Record-only C2 did not change the exact governed six-file set."
    }
    for ($index = 0; $index -lt $expectedC2ChangedFiles.Count; $index++) {
        if (-not [string]::Equals(
                $actualChangedFiles[$index],
                $expectedC2ChangedFiles[$index],
                [StringComparison]::Ordinal)) {
            Stop-RatificationVerification "c2-file-set-mismatch" "Record-only C2 did not change the exact governed six-file set."
        }
    }

    $c1Packet = Invoke-GitText @("show", "$candidateFreeze`:$packetRelativePath") "reading the transitional packet at C1"
    if (-not (Test-PendingPacket $c1Packet)) {
        Stop-RatificationVerification "c1-transition-missing" "Recorded C1 did not contain the exact local-only pending transition."
    }

    $receipt.Outcome = "verified"
    $receipt.FailureCode = $null
    $receipt.FailureMessage = $null
    Write-RatificationReceipt
    Write-Host "Verified recipe-identity C1 $candidateFreeze and record-only C2 $ratificationCommit in HEAD ancestry."
}
catch {
    if ($null -eq $receipt.FailureCode) {
        $receipt.FailureCode = "unexpected-verifier-failure"
        $receipt.FailureMessage = "The ratification history verifier failed unexpectedly."
    }
    Write-RatificationReceipt
    throw
}
