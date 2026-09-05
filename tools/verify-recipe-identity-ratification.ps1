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
$sampleManifestRelativePath = "tests/Rendering/Fixtures/recipe-first-admission-samples.sha256"
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
    OriginalDecisionRecordGitBlob = $null
    HeadDecisionRecordGitBlob = $null
    OriginalDecisionRecordPreserved = $false
    DecisionRecordWorkingCopyGitBlob = $null
    DecisionRecordWorkingCopyNormalization = "UTF-8; literal CRLF to LF only; no Git filters"
    FirstAdmissionSampleManifestPath = $sampleManifestRelativePath
    FirstAdmissionSampleManifestC1GitBlob = $null
    FirstAdmissionSampleManifestHeadGitBlob = $null
    FirstAdmissionSampleManifestWorkingCopyGitBlob = $null
    FirstAdmissionSampleManifestWorkingCopyNormalization = "None; raw bytes with --no-filters"
    FirstAdmissionSampleManifestPreserved = $false
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

function Get-RequiredGitBlobId {
    param(
        [Parameter(Mandatory)][string]$Revision,
        [Parameter(Mandatory)][string]$RelativePath,
        [Parameter(Mandatory)][string]$FailureCode)

    $entry = Invoke-GitText @("ls-tree", $Revision, "--", $RelativePath) "resolving a protected record's Git blob"
    $pattern = '^(?:100644|100755) blob (?<hash>[0-9a-f]{40})\t' + [regex]::Escape($RelativePath) + '$'
    $match = [regex]::Match($entry, $pattern)
    if (-not $match.Success) {
        Stop-RatificationVerification $FailureCode "A protected ratification artifact is missing or is not one regular Git blob."
    }

    return $match.Groups["hash"].Value
}

function Get-RequiredIndexBlobId {
    param(
        [Parameter(Mandatory)][string]$RelativePath,
        [Parameter(Mandatory)][string]$FailureCode)

    $entry = Invoke-GitText @("ls-files", "--stage", "--", $RelativePath) "checking a protected record's staged Git bytes"
    $pattern = '^(?:100644|100755) (?<hash>[0-9a-f]{40}) 0\t' + [regex]::Escape($RelativePath) + '$'
    $match = [regex]::Match($entry, $pattern)
    if (-not $match.Success) {
        Stop-RatificationVerification $FailureCode "A protected ratification artifact has no single regular stage-zero index entry."
    }

    return $match.Groups["hash"].Value
}

function Get-WorkingCopyGitBlobId {
    param(
        [Parameter(Mandatory)][string]$RelativePath,
        [Parameter(Mandatory)][string]$FailureCode)

    $workingPath = Join-Path $repositoryFullPath $RelativePath
    if (-not [IO.File]::Exists($workingPath) -or
        (([IO.File]::GetAttributes($workingPath) -band [IO.FileAttributes]::ReparsePoint) -ne 0)) {
        Stop-RatificationVerification $FailureCode "A protected working-copy record is missing or is not a regular local file."
    }

    if ($RelativePath -ceq $sampleManifestRelativePath) {
        # The sample baseline pins raw LF bytes. Neither mutable attributes nor
        # a clean filter may transform a changed checkout back to the C1 blob.
        $blobId = (Invoke-GitText @("hash-object", "--no-filters", "--", $workingPath) "hashing raw first-admission sample bytes").Trim()
        if ($blobId -notmatch '^[0-9a-f]{40}$') {
            Stop-RatificationVerification $FailureCode "The raw sample bytes did not resolve to one Git blob identity."
        }
        return $blobId
    }

    if ($RelativePath -cne $packetRelativePath) {
        Stop-RatificationVerification $FailureCode "No working-copy normalization is admitted for this protected record."
    }

    # Only ordinary UTF-8 CRLF checkout representation is allowed for the
    # decision packet. Read bounded owned bytes and normalize literal CRLF, not
    # arbitrary Unicode line separators, encodings, attributes, or clean filters.
    $stream = [IO.File]::OpenRead($workingPath)
    try {
        if ($stream.Length -gt 4 * 1024 * 1024) {
            Stop-RatificationVerification $FailureCode "The working-copy decision record exceeds its bounded byte limit."
        }
        $bytes = [byte[]]::new([int]$stream.Length)
        $stream.ReadExactly($bytes, 0, $bytes.Length)
        if ($stream.ReadByte() -ne -1) {
            Stop-RatificationVerification $FailureCode "The working-copy decision record changed while its bytes were read."
        }
    }
    finally {
        $stream.Dispose()
    }
    try {
        $strictUtf8 = [Text.UTF8Encoding]::new($false, $true)
        $normalized = $strictUtf8.GetBytes($strictUtf8.GetString($bytes).Replace("`r`n", "`n"))
    }
    catch {
        Stop-RatificationVerification $FailureCode "The working-copy decision record must contain valid UTF-8 bytes."
    }

    # Git's blob framing is computed directly so no repository-configured
    # transformation runs. SHA-1 here identifies a Git object, not an approval
    # signature; C2 supplies the original admitted object to compare against.
    $header = [Text.Encoding]::ASCII.GetBytes(
        "blob " + $normalized.Length.ToString([Globalization.CultureInfo]::InvariantCulture) + [char]0)
    $hash = [Security.Cryptography.IncrementalHash]::CreateHash([Security.Cryptography.HashAlgorithmName]::SHA1)
    try {
        $hash.AppendData($header)
        $hash.AppendData($normalized)
        return [Convert]::ToHexString($hash.GetHashAndReset()).ToLowerInvariant()
    }
    finally {
        $hash.Dispose()
    }
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

    # Ancestry proves the original act, not that a later commit preserved its
    # decision or baseline. Compare immutable Git blobs, never current-tree hash
    # constants that could be replaced in the same change. Factual continuation
    # belongs in a separate record; a future superseding ADR must deliberately
    # revise this guard rather than rewriting the first-admission evidence.
    $originalPacketBlob = Get-RequiredGitBlobId $ratificationCommit $packetRelativePath "decision-record-missing"
    $headPacketBlob = Get-RequiredGitBlobId $head $packetRelativePath "decision-record-missing"
    $receipt.OriginalDecisionRecordGitBlob = $originalPacketBlob
    $receipt.HeadDecisionRecordGitBlob = $headPacketBlob
    if ($headPacketBlob -cne $originalPacketBlob) {
        Stop-RatificationVerification "decision-record-drift" "The ratified decision packet differs from the immutable original C2 Git bytes."
    }
    $indexPacketBlob = Get-RequiredIndexBlobId $packetRelativePath "packet-dirty"
    if ($indexPacketBlob -cne $headPacketBlob) {
        Stop-RatificationVerification "packet-dirty" "The staged decision packet differs from the immutable original C2 Git bytes."
    }
    $workingPacketBlob = Get-WorkingCopyGitBlobId $packetRelativePath "decision-record-working-copy-drift"
    $receipt.DecisionRecordWorkingCopyGitBlob = $workingPacketBlob
    if ($workingPacketBlob -cne $headPacketBlob) {
        Stop-RatificationVerification "decision-record-working-copy-drift" "The decision packet's canonical working-copy Git content differs from the original C2 record."
    }
    $receipt.OriginalDecisionRecordPreserved = $true

    $originalSampleBlob = Get-RequiredGitBlobId $candidateFreeze $sampleManifestRelativePath "sample-baseline-missing"
    $headSampleBlob = Get-RequiredGitBlobId $head $sampleManifestRelativePath "sample-baseline-missing"
    $receipt.FirstAdmissionSampleManifestC1GitBlob = $originalSampleBlob
    $receipt.FirstAdmissionSampleManifestHeadGitBlob = $headSampleBlob
    if ($headSampleBlob -cne $originalSampleBlob) {
        Stop-RatificationVerification "sample-baseline-drift" "The first-admission sample manifest differs from the immutable C1 Git bytes."
    }
    $indexSampleBlob = Get-RequiredIndexBlobId $sampleManifestRelativePath "sample-baseline-dirty"
    if ($indexSampleBlob -cne $headSampleBlob) {
        Stop-RatificationVerification "sample-baseline-dirty" "The staged sample manifest differs from the immutable C1 Git bytes."
    }
    $workingSampleBlob = Get-WorkingCopyGitBlobId $sampleManifestRelativePath "sample-baseline-working-copy-drift"
    $receipt.FirstAdmissionSampleManifestWorkingCopyGitBlob = $workingSampleBlob
    if ($workingSampleBlob -cne $headSampleBlob) {
        Stop-RatificationVerification "sample-baseline-working-copy-drift" "The sample manifest's raw working-copy bytes differ from the immutable C1 evidence."
    }
    $receipt.FirstAdmissionSampleManifestPreserved = $true

    $receipt.Outcome = "verified"
    $receipt.FailureCode = $null
    $receipt.FailureMessage = $null
    Write-RatificationReceipt
    Write-Host "Verified recipe-identity C1 $candidateFreeze and record-only C2 $ratificationCommit in HEAD ancestry; original decision and first-admission sample bytes are preserved."
}
catch {
    if ($null -eq $receipt.FailureCode) {
        $receipt.FailureCode = "unexpected-verifier-failure"
        $receipt.FailureMessage = "The ratification history verifier failed unexpectedly."
    }
    Write-RatificationReceipt
    throw
}
