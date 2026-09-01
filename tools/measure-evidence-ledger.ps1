# SPDX-License-Identifier: GPL-3.0-or-later
# Measures docs/evidence/evidence-ledger.json against the systems it describes.
# Every hosted entry is compared with the run's own record from `gh run view`
# (conclusion, headSha, event, headBranch, workflowName, createdAt); every merge
# entry is compared with the local Git graph (both parents, and ancestry of the
# current HEAD). The ledger is measured, not transcribed: a mismatch names the
# entry and the field and exits nonzero. Nothing here trusts a wrapper's exit
# code, a watcher, or a neighbouring run.
[CmdletBinding()]
param(
    [string]$LedgerPath = (Join-Path $PSScriptRoot "..\docs\evidence\evidence-ledger.json")
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $false

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$resolvedLedgerPath = [IO.Path]::GetFullPath($LedgerPath)
if (-not (Test-Path -LiteralPath $resolvedLedgerPath -PathType Leaf)) {
    throw "The evidence ledger does not exist: $resolvedLedgerPath"
}

$ledger = Get-Content -LiteralPath $resolvedLedgerPath -Raw | ConvertFrom-Json
if ($ledger.format -ne "honest-ink-evidence-ledger.v1") {
    throw "Unexpected ledger format '$($ledger.format)'; this tool measures honest-ink-evidence-ledger.v1."
}

$mismatches = [Collections.Generic.List[string]]::new()
$measured = [Collections.Generic.List[object]]::new()

function Compare-Field {
    param(
        [Parameter(Mandatory)][string]$EntryId,
        [Parameter(Mandatory)][string]$Field,
        [AllowNull()][AllowEmptyString()][object]$Expected,
        [AllowNull()][AllowEmptyString()][object]$Actual
    )

    if (-not [string]::Equals([string]$Expected, [string]$Actual, [StringComparison]::Ordinal)) {
        $mismatches.Add("$EntryId ${Field}: ledger='$Expected' measured='$Actual'")
    }
}

foreach ($entry in @($ledger.entries)) {
    switch ($entry.kind) {
        { $_ -in @("hosted-ci", "hosted-codeql") } {
            $runJson = & gh run view ([string]$entry.runId) --json databaseId,conclusion,headSha,event,headBranch,workflowName,createdAt 2>$null
            if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace(($runJson -join ""))) {
                $mismatches.Add("$($entry.id): gh run view exited $LASTEXITCODE; the run could not be measured")
                continue
            }

            $run = ($runJson -join "") | ConvertFrom-Json
            Compare-Field -EntryId $entry.id -Field "runId" -Expected $entry.runId -Actual $run.databaseId
            Compare-Field -EntryId $entry.id -Field "conclusion" -Expected $entry.conclusion -Actual $run.conclusion
            Compare-Field -EntryId $entry.id -Field "headSha" -Expected $entry.headSha -Actual $run.headSha
            Compare-Field -EntryId $entry.id -Field "event" -Expected $entry.event -Actual $run.event
            Compare-Field -EntryId $entry.id -Field "branch" -Expected $entry.branch -Actual $run.headBranch
            Compare-Field -EntryId $entry.id -Field "workflow" -Expected $entry.workflow -Actual $run.workflowName
            Compare-Field -EntryId $entry.id -Field "createdUtc" -Expected $entry.createdUtc -Actual $run.createdAt
            $measured.Add([ordered]@{
                    Id = $entry.id
                    RunId = [long]$run.databaseId
                    Conclusion = [string]$run.conclusion
                    HeadSha = [string]$run.headSha
                    Event = [string]$run.event
                    Branch = [string]$run.headBranch
                    Workflow = [string]$run.workflowName
                    CreatedUtc = [string]$run.createdAt
                })
        }
        "merge" {
            $firstParent = & git -C $repositoryRoot rev-parse --verify "$($entry.commit)^1" 2>$null
            $firstParentExit = $LASTEXITCODE
            $secondParent = & git -C $repositoryRoot rev-parse --verify "$($entry.commit)^2" 2>$null
            $secondParentExit = $LASTEXITCODE
            if ($firstParentExit -ne 0 -or $secondParentExit -ne 0) {
                $mismatches.Add("$($entry.id): the merge commit or one of its parents is not in the local repository")
                continue
            }

            Compare-Field -EntryId $entry.id -Field "firstParent" -Expected $entry.firstParent -Actual ([string]$firstParent).Trim()
            Compare-Field -EntryId $entry.id -Field "secondParent" -Expected $entry.secondParent -Actual ([string]$secondParent).Trim()
            & git -C $repositoryRoot merge-base --is-ancestor $entry.commit HEAD 2>$null
            $isAncestor = ($LASTEXITCODE -eq 0)
            if (-not $isAncestor) {
                $mismatches.Add("$($entry.id): merge commit is not an ancestor of the current HEAD")
            }

            $measured.Add([ordered]@{
                    Id = $entry.id
                    Commit = [string]$entry.commit
                    FirstParent = ([string]$firstParent).Trim()
                    SecondParent = ([string]$secondParent).Trim()
                    AncestorOfHead = $isAncestor
                })
        }
        default {
            $mismatches.Add("$($entry.id): unknown kind '$($entry.kind)'")
        }
    }
}

$receipt = [ordered]@{
    Statement = "Measured against gh run view and the local Git graph; agreement at measurement time is all this receipt proves. It is not a release, diagnosis, cure, or approval."
    MeasuredUtc = (Get-Date).ToUniversalTime().ToString("O", [Globalization.CultureInfo]::InvariantCulture)
    LedgerPath = [IO.Path]::GetRelativePath($repositoryRoot, $resolvedLedgerPath)
    LedgerSha256 = (Get-FileHash -LiteralPath $resolvedLedgerPath -Algorithm SHA256).Hash
    EntryCount = @($ledger.entries).Count
    MismatchCount = $mismatches.Count
    Mismatches = @($mismatches)
    Measured = @($measured)
}
$outputRoot = Join-Path $repositoryRoot "out\evidence-ledger-measurement"
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
$receiptName = "{0}-{1}.json" -f (Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssZ", [Globalization.CultureInfo]::InvariantCulture), [Guid]::NewGuid().ToString("N")
$receiptPath = Join-Path $outputRoot $receiptName
[IO.File]::WriteAllText(
    $receiptPath,
    (($receipt | ConvertTo-Json -Depth 6) + [Environment]::NewLine),
    [Text.UTF8Encoding]::new($false))

Write-Host "Measured $(@($ledger.entries).Count) ledger entries; $($mismatches.Count) mismatch(es). Receipt: $receiptPath"
foreach ($mismatch in $mismatches) {
    Write-Host "  $mismatch"
}

exit $(if ($mismatches.Count -eq 0) { 0 } else { 1 })
