# SPDX-License-Identifier: GPL-3.0-or-later
# Reproducible structural/coverage audit for this synthetic advisory packet.
# This cannot authenticate a person, assess pedagogy, or close a human gate.
[CmdletBinding()]
param([switch]$EmitManifest)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../../.."))
$baseline = "965a59abb80a4f1671f34d13ee34f82cb7f5a624"
$packet = "docs/reviews/2026-09-05-synthetic-council"
$utf8 = [Text.UTF8Encoding]::new($false, $true)

function Require([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}
function Read-Local([string]$RelativePath) {
    $absolute = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $RelativePath))
    Require ($absolute.StartsWith($repositoryRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) "Path escaped repository: $RelativePath"
    return [IO.File]::ReadAllText($absolute, $utf8).Replace("`r`n", "`n")
}
function Read-Baseline([string]$RelativePath) {
    $lines = & git -C $repositoryRoot show ("${baseline}:" + $RelativePath)
    Require ($LASTEXITCODE -eq 0) "Missing baseline source: $RelativePath"
    return ($lines -join "`n")
}
function Check-Ids($Found, $Expected, [string]$Label) {
    $actual = @($Found)
    $wanted = @($Expected)
    Require ($actual.Count -eq $wanted.Count) "$Label count: $($actual.Count), expected $($wanted.Count)"
    Require (($actual -join ",") -ceq ($wanted -join ",")) "$Label identifiers or order differ"
}
function Check-SourceRows([string]$Report, [string]$Source, [string]$Pattern, [string]$Label) {
    $actual = [regex]::Matches($Report, $Pattern)
    $wanted = [regex]::Matches($Source, $Pattern)
    Check-Ids @($actual | ForEach-Object { $_.Groups[1].Value }) @($wanted | ForEach-Object { $_.Groups[1].Value }) $Label
    for ($i = 0; $i -lt $actual.Count; $i++) {
        Require ($actual[$i].Groups[2].Value.Trim() -ceq $wanted[$i].Groups[2].Value.Trim()) "$Label source name differs at row $i"
    }
}

$readme = Read-Local "$packet/README.md"
$teacher = Read-Local "$packet/teacher-practice-and-improvements.md"
$atlas = Read-Local "$packet/atlas-dispositions.md"
$evidence = Read-Local "$packet/evidence-and-completion.md"
$atlasSource = Read-Baseline "docs/idea-atlas.md"
$planSource = Read-Baseline "docs/implementation-plan.md"
$gateSource = Read-Baseline "docs/governance/stage-gate-disposition-register.md"
$traceSource = Read-Baseline "docs/release/release-requirement-test-traceability.md"

Check-Ids @([regex]::Matches($readme, '(?m)^## Session (\d+) ') | ForEach-Object { $_.Groups[1].Value }) @(13..20) "Sessions"
$habitSection = ($readme -split 'The persona carries twelve explicit habits\.', 2)[1] -split 'The persona cannot assess', 2
Require ([regex]::Matches($habitSection[0], '(?m)^\| (?!Habit)(?!-)[^|]+ \|').Count -eq 12) "Expected twelve habits"
$lensSection = ($readme -split '## Council lenses and deliberation rule', 2)[1] -split 'No votes or quorum', 2
Require ([regex]::Matches($lensSection[0], '(?m)^\| (?!Fictional lens)(?!-)[^|]+ \|').Count -eq 10) "Expected ten fictional lenses"

$proposalMatches = [regex]::Matches($teacher, '(?m)^### (I\d{2}) — ')
$proposalIds = @($proposalMatches | ForEach-Object { $_.Groups[1].Value })
Check-Ids $proposalIds @(1..40 | ForEach-Object { "I{0:D2}" -f $_ }) "Proposals"
$proposalSections = [regex]::Split($teacher, '(?m)^### I\d{2} — ')
foreach ($section in @($proposalSections | Select-Object -Skip 1)) {
    foreach ($field in @("**Need/change:**", "**Proof:**", "**Owner/dependency:**")) {
        Require ($section.Contains($field)) "A proposal lacks $field"
    }
}
Require ([regex]::Matches($teacher, '(?m)^\| [IVX]+ — ').Count -eq 25) "Expected twenty-five studios"
Check-Ids @([regex]::Matches($teacher, '(?m)^\| (10\.\d+) ') | ForEach-Object { $_.Groups[1].Value }) @([regex]::Matches($planSource, '(?m)^## (10\.\d+) ') | ForEach-Object { $_.Groups[1].Value }) "Starting modules"

$sourceEntries = [regex]::Matches($atlasSource, '(?m)^(\d+)\. \*\*(.+?)\*\* `\[([^\]]+)\]`')
$reportEntries = [regex]::Matches($atlas, '(?m)^\| (\d+) \| ([^|]+) \| ([^|]+) \| ([^|]+) \| ([^|]+) \| ([^|]+) \|$')
Check-Ids @($reportEntries | ForEach-Object { $_.Groups[1].Value }) @(1..227) "Atlas rows"
Require ($sourceEntries.Count -eq 227) "Baseline atlas count differs"
$refinements = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
for ($i = 0; $i -lt 227; $i++) {
    $row = $reportEntries[$i]
    $source = $sourceEntries[$i]
    Require ($row.Groups[2].Value.Trim() -ceq $source.Groups[2].Value.Trim()) "Atlas name differs at $($i + 1)"
    Require ($row.Groups[3].Value.Trim() -ceq $source.Groups[3].Value.Trim()) "Atlas lane differs at $($i + 1)"
    Require ($refinements.Add($row.Groups[5].Value.Trim())) "Repeated atlas refinement at $($i + 1)"
    Require ([regex]::Matches($row.Groups[5].Value, '\S+').Count -ge 12) "Atlas refinement too thin at $($i + 1)"
    Require ([regex]::Matches($row.Groups[6].Value, 'I\d{2}').Count -ge 1) "Atlas row has no proposal"
    if ($source.Groups[3].Value.StartsWith("R") -or $i -eq 68) {
        Require ($row.Groups[4].Value.Contains("Outside 1.x")) "Restricted posture changed at $($i + 1)"
    }
}

Check-SourceRows $evidence $gateSource '(?m)^\| (G\d-\d{2}) \| ([^|]+) \|' "Stage gates"
Check-SourceRows $evidence $traceSource '(?m)^\| ((?:P|PS|AL|RO|OP|SU)-\d{2}) \| ([^|]+) \|' "Definition of Done"
Check-SourceRows $evidence $traceSource '(?m)^\| (SS-\d{2}) \| ([^|]+) \|' "Stop-ship"
Require ($evidence.Contains("All eight remain NOT BEGUN.")) "Human review boundary missing"

$narratives = @("$packet/README.md", "$packet/teacher-practice-and-improvements.md", "$packet/atlas-dispositions.md", "$packet/evidence-and-completion.md")
$linkCount = 0
foreach ($path in $narratives) {
    $body = Read-Local $path
    foreach ($reference in [regex]::Matches($body, '\bI\d{2}\b')) {
        Require ($proposalIds -ccontains $reference.Value) "Unknown proposal $($reference.Value) in $path"
    }
    foreach ($link in [regex]::Matches($body, '\[[^\]]+\]\(([^)]+)\)')) {
        $target = $link.Groups[1].Value
        if ($target -match '^https?://' -or $target.StartsWith("#")) { continue }
        $fileTarget = [Uri]::UnescapeDataString(($target -split '#', 2)[0])
        $absolute = [IO.Path]::GetFullPath((Join-Path (Split-Path (Join-Path $repositoryRoot $path)) $fileTarget))
        Require ($absolute.StartsWith($repositoryRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) "Link escaped repository: $target"
        Require ([IO.File]::Exists($absolute)) "Broken relative link in ${path}: $target"
        $linkCount++
    }
}

$verdict = "Packet audit passed: 8 sessions; 12 habits; 10 fictional lenses; 40 proposals; 25 studios; 13 modules; 227 atlas rows; 44 gates; 34 DoD; 13 stop-ship rows; $linkCount relative links."
if (-not $EmitManifest) {
    Write-Output $verdict
    return
}

$manifestPaths = @($narratives) + @("$packet/verify-packet.ps1", "docs/README.md", "docs/evidence/evidence-ledger.json")
$manifestPaths = @($manifestPaths | Sort-Object -CaseSensitive)
$files = @(
    foreach ($path in $manifestPaths) {
        $bytes = $utf8.GetBytes((Read-Local $path))
        $header = $utf8.GetBytes("blob $($bytes.Length)`0")
        $blobBytes = [byte[]]::new($header.Length + $bytes.Length)
        [Buffer]::BlockCopy($header, 0, $blobBytes, 0, $header.Length)
        [Buffer]::BlockCopy($bytes, 0, $blobBytes, $header.Length, $bytes.Length)
        [ordered]@{
            path = $path
            canonicalBytes = $bytes.Length
            canonicalSha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
            canonicalGitBlob = [Convert]::ToHexString([Security.Cryptography.SHA1]::HashData($blobBytes)).ToLowerInvariant()
        }
    }
)
$sourceBindings = @(
    foreach ($path in @("AGENTS.md", "CONTRIBUTING.md", "GOVERNANCE.md", "docs/idea-atlas.md", "docs/implementation-plan.md", "docs/governance/stage-gate-disposition-register.md", "docs/release/release-requirement-test-traceability.md", "docs/council/bounded-commission-review-ledger.md")) {
        $blob = & git -C $repositoryRoot rev-parse ("${baseline}:" + $path)
        Require ($LASTEXITCODE -eq 0) "Cannot bind baseline source $path"
        [ordered]@{ path = $path; baselineGitBlob = "$blob".Trim() }
    }
)
[ordered]@{
    format = "honest-ink-synthetic-advisory-manifest.v1"
    baselineCommit = $baseline
    hashBasis = "UTF-8 without BOM, CRLF normalized to LF; canonical Git text content, not platform checkout bytes."
    scope = "Narrative and supporting maintenance files; excludes this detached manifest. Report integrity only, not authenticated human review, consent, a freeze manifest, or release approval."
    verdict = $verdict
    files = $files
    sourceBindings = $sourceBindings
} | ConvertTo-Json -Depth 8
