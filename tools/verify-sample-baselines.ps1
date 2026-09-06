# SPDX-License-Identifier: GPL-3.0-or-later
# Read-only comparison of trusted, locally generated evidence, not a hostile-
# package reader or recipe-admission instrument. Dot-sourcing exposes the same
# comparison functions for synthetic controls without running the file gate.
[CmdletBinding()]
param(
    [string]$SamplesRoot,
    [string]$HistoricalManifestPath = (Join-Path $PSScriptRoot '../tests/Rendering/Fixtures/recipe-first-admission-samples.sha256'),
    [string]$CandidateManifestPath = (Join-Path $PSScriptRoot '../tests/Rendering/Fixtures/engine-0.8.0-alpha-candidate-samples.sha256'),
    [string]$HistoricalPackagePath = (Join-Path $PSScriptRoot '../tests/Integration/Fixtures/upgrade/c1-first-admission-task-strip.ocfproj.base64')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Test-SampleBytesEqual {
    param([byte[]]$First, [byte[]]$Second)
    return [string]::Equals(
        [Convert]::ToBase64String($First),
        [Convert]::ToBase64String($Second),
        [StringComparison]::Ordinal)
}

function Read-SampleManifest {
    param([byte[]]$Bytes, [string]$Label)
    $text = [Text.UTF8Encoding]::new($false, $true).GetString($Bytes)
    if ($Bytes -contains 13 -or -not $text.EndsWith("`n", [StringComparison]::Ordinal)) {
        throw "[sample.manifest-framing] $Label must retain UTF-8, LF and a final newline."
    }
    $lines = $text.Substring(0, $text.Length - 1).Split("`n")
    if ($lines.Count -ne 40) {
        throw "[sample.manifest-count] $Label must contain exactly 40 rows."
    }
    $hashes = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)
    $paths = [Collections.Generic.List[string]]::new()
    foreach ($line in $lines) {
        if ($line -cnotmatch '^(?<path>[a-z0-9][a-z0-9.-]*) (?<sha>[0-9A-F]{64})$') {
            throw "[sample.manifest-row] $Label contains a malformed row."
        }
        $path = $Matches.path
        if (-not $hashes.TryAdd($path, $Matches.sha)) {
            throw "[sample.manifest-duplicate] $Label repeats an exact path."
        }
        $paths.Add($path)
    }
    $ordered = $paths.ToArray()
    [Array]::Sort($ordered, [StringComparer]::Ordinal)
    for ($index = 0; $index -lt $ordered.Length; $index++) {
        if (-not [string]::Equals($ordered[$index], $paths[$index], [StringComparison]::Ordinal)) {
            throw "[sample.manifest-order] $Label must use exact ordinal path order."
        }
    }
    return [pscustomobject]@{ Paths = $paths.ToArray(); Hashes = $hashes }
}

function Assert-SampleBaselineContract {
    param([byte[]]$HistoricalManifest, [byte[]]$CandidateManifest)
    $historicalSha = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($HistoricalManifest))
    if ($historicalSha -cne 'DEF10A3258A2F2ABA922DF8F1BC38FC3A3209065B36F81F44C41B4FE047F4A90') {
        throw '[sample.historical-manifest] First-admission sample baseline drifted: immutable C1 manifest bytes differ.'
    }
    $historical = Read-SampleManifest -Bytes $HistoricalManifest -Label 'C1 historical manifest'
    $candidate = Read-SampleManifest -Bytes $CandidateManifest -Label 'Engine 0.8 candidate manifest'
    for ($index = 0; $index -lt $historical.Paths.Length; $index++) {
        $path = $historical.Paths[$index]
        if (-not [string]::Equals($path, $candidate.Paths[$index], [StringComparison]::Ordinal)) {
            throw '[sample.manifest-paths] Candidate and C1 must contain the same exact 40 paths.'
        }
        if ($path -ceq 'task-strip-bilingual.ocfproj') {
            if ($historical.Hashes[$path] -ceq $candidate.Hashes[$path]) {
                throw '[sample.candidate-package-unchanged] The 0.8 writer package cannot claim the C1 0.7 bytes.'
            }
        }
        elseif ($historical.Hashes[$path] -cne $candidate.Hashes[$path]) {
            throw "[sample.nonpackage-drift] First-admission sample baseline drifted: $path must retain its exact C1 bytes."
        }
    }
    return $candidate
}

function Read-SampleZipEntry {
    param([IO.Compression.ZipArchiveEntry]$Entry)
    $inputStream = $Entry.Open()
    $outputStream = [IO.MemoryStream]::new()
    try {
        $inputStream.CopyTo($outputStream)
        return ,$outputStream.ToArray()
    }
    finally {
        $outputStream.Dispose()
        $inputStream.Dispose()
    }
}

function Assert-SamplePackageContract {
    param([byte[]]$HistoricalPackage, [byte[]]$CandidatePackage)
    if ($HistoricalPackage.Length -ne 3338 -or
        [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($HistoricalPackage)) -cne
        '9014BFFB477FC9F470E3901393AF1B6D34ACF697B5E0CD174A12BF8D76A74C82') {
        throw '[sample.historical-package] The exact 3,338-byte C1 package fixture has changed.'
    }
    $oldStream = [IO.MemoryStream]::new($HistoricalPackage, $false)
    $newStream = [IO.MemoryStream]::new($CandidatePackage, $false)
    $oldZip = $null
    $newZip = $null
    try {
        $oldZip = [IO.Compression.ZipArchive]::new($oldStream, [IO.Compression.ZipArchiveMode]::Read)
        $newZip = [IO.Compression.ZipArchive]::new($newStream, [IO.Compression.ZipArchiveMode]::Read)
        if ($oldZip.Entries.Count -ne $newZip.Entries.Count) {
            throw '[sample.package-entries] Candidate package entry count differs from C1.'
        }
        $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        for ($index = 0; $index -lt $oldZip.Entries.Count; $index++) {
            $oldEntry = $oldZip.Entries[$index]
            $newEntry = $newZip.Entries[$index]
            if (-not $names.Add($newEntry.FullName) -or
                -not [string]::Equals($oldEntry.FullName, $newEntry.FullName, [StringComparison]::Ordinal)) {
                throw '[sample.package-entries] Candidate package entry names, uniqueness or order differ from C1.'
            }
            if ($oldEntry.LastWriteTime -ne $newEntry.LastWriteTime -or
                $oldEntry.ExternalAttributes -ne $newEntry.ExternalAttributes) {
                throw '[sample.package-metadata] Candidate package entry stamp or attributes differ from C1.'
            }
            $oldBytes = Read-SampleZipEntry -Entry $oldEntry
            $newBytes = Read-SampleZipEntry -Entry $newEntry
            if ($oldEntry.FullName -cne 'manifest.json') {
                if (-not (Test-SampleBytesEqual -First $oldBytes -Second $newBytes)) {
                    throw "[sample.package-payload] Candidate $($oldEntry.FullName) differs from C1."
                }
                continue
            }
            $utf8 = [Text.UTF8Encoding]::new($false, $true)
            $oldText = $utf8.GetString($oldBytes)
            $newText = $utf8.GetString($newBytes)
            $oldJson = [Text.Json.JsonDocument]::Parse($oldText)
            $newJson = $null
            try {
                $newJson = [Text.Json.JsonDocument]::Parse($newText)
                $oldWriter = @($oldJson.RootElement.EnumerateObject() | Where-Object { $_.Name -ceq 'engineVersion' })
                $newWriter = @($newJson.RootElement.EnumerateObject() | Where-Object { $_.Name -ceq 'engineVersion' })
                if ($oldWriter.Count -ne 1 -or $newWriter.Count -ne 1 -or
                    $oldWriter[0].Value.GetString() -cne '0.7.0-alpha' -or
                    $newWriter[0].Value.GetString() -cne '0.8.0-alpha') {
                    throw '[sample.package-writer] Exactly one top-level 0.7 to 0.8 writer stamp is required.'
                }
                $oldStamp = '"engineVersion": "0.7.0-alpha"'
                if ($oldText.IndexOf($oldStamp, [StringComparison]::Ordinal) -lt 0 -or
                    $oldText.IndexOf($oldStamp, [StringComparison]::Ordinal) -ne
                    $oldText.LastIndexOf($oldStamp, [StringComparison]::Ordinal)) {
                    throw '[sample.package-manifest] C1 has no unique canonical writer-stamp expression.'
                }
                $expected = $oldText.Replace($oldStamp, '"engineVersion": "0.8.0-alpha"', [StringComparison]::Ordinal)
                if (-not (Test-SampleBytesEqual -First $utf8.GetBytes($expected) -Second $newBytes)) {
                    throw '[sample.package-manifest] Candidate manifest differs beyond the exact writer stamp.'
                }
            }
            finally {
                if ($null -ne $newJson) { $newJson.Dispose() }
                $oldJson.Dispose()
            }
        }
    }
    finally {
        if ($null -ne $newZip) { $newZip.Dispose() }
        if ($null -ne $oldZip) { $oldZip.Dispose() }
        $newStream.Dispose()
        $oldStream.Dispose()
    }
}

function Invoke-SampleBaselineVerification {
    param([string]$Root, [string]$HistoricalPath, [string]$CandidatePath, [string]$PackagePath)
    $contract = Assert-SampleBaselineContract `
        -HistoricalManifest ([IO.File]::ReadAllBytes($HistoricalPath)) `
        -CandidateManifest ([IO.File]::ReadAllBytes($CandidatePath))
    $resolvedRoot = (Resolve-Path -LiteralPath $Root).Path
    $files = @(Get-ChildItem -LiteralPath $resolvedRoot -Recurse -File)
    if ($files.Count -ne $contract.Paths.Length) {
        throw '[sample.output-inventory] Generated samples must contain the exact 40 candidate paths.'
    }
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($file in $files) {
        $path = [IO.Path]::GetRelativePath($resolvedRoot, $file.FullName).Replace([IO.Path]::DirectorySeparatorChar, '/')
        if (-not $seen.Add($path) -or -not $contract.Hashes.ContainsKey($path)) {
            throw '[sample.output-inventory] Generated sample paths differ from the exact candidate inventory.'
        }
        if ((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -cne $contract.Hashes[$path]) {
            throw "[sample.output-hash] Candidate sample baseline drifted: $path."
        }
    }
    $historicalPackage = [Convert]::FromBase64String([IO.File]::ReadAllText($PackagePath))
    Assert-SamplePackageContract -HistoricalPackage $historicalPackage `
        -CandidatePackage ([IO.File]::ReadAllBytes((Join-Path $resolvedRoot 'task-strip-bilingual.ocfproj')))
    Write-Host 'First-admission sample baseline matched: 39 unchanged outputs; all 40 historical manifest rows preserved.'
    Write-Host 'Engine 0.8 candidate sample baseline matched: all 40 outputs; exact C1 package relation permits only the writer stamp.'
    Write-Host "Historical manifest SHA256: $((Get-FileHash -LiteralPath $HistoricalPath -Algorithm SHA256).Hash)"
    Write-Host "Candidate manifest SHA256: $((Get-FileHash -LiteralPath $CandidatePath -Algorithm SHA256).Hash)"
}

if ($MyInvocation.InvocationName -ne '.') {
    Invoke-SampleBaselineVerification -Root $SamplesRoot -HistoricalPath $HistoricalManifestPath `
        -CandidatePath $CandidateManifestPath -PackagePath $HistoricalPackagePath
}
