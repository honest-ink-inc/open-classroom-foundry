# SPDX-License-Identifier: GPL-3.0-or-later
#Requires -Version 7.2
# Verifies an already signed copy of the exact pre-sign inventory and assembles
# final local evidence. This script never signs, tags, installs, uploads,
# distributes, or publishes anything. Running it is a typist-authorized act.
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$SignedInputPath,

    [Parameter(Mandatory)]
    [string]$PreSignManifestPath,

    [Parameter(Mandatory)]
    [string[]]$AllowedSignerThumbprint
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$packageRoot = Join-Path $repositoryRoot "out"
$packageLockPath = Join-Path $packageRoot ".package.lock"
$operationId = [Guid]::NewGuid().ToString("N")
$stagingRoot = Join-Path $packageRoot ".signed-stage-$operationId"
$backupRoot = Join-Path $packageRoot ".signed-backup-$operationId"
$stagedBundle = Join-Path $stagingRoot "signed-final"
$stagedPayload = Join-Path $stagedBundle "payload"
$stagedSums = Join-Path $stagedBundle "SHA256SUMS.txt"
$stagedReleaseManifest = Join-Path $stagedBundle "signed-release-manifest.json"
$finalBundle = Join-Path $packageRoot "signed-final"
$backupBundle = Join-Path $backupRoot "signed-final"

$packageLock = $null
$promoted = $false
$backedUp = $false
$preserveRecoveryData = $false

function Invoke-RepositoryGit {
    param([Parameter(Mandatory)][string[]]$Arguments)

    $output = @(& git -C $repositoryRoot @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "Signed-package repository verification failed."
    }

    $result = @($output | ForEach-Object { ([string]$_).Trim() } | Where-Object { $_.Length -gt 0 })
    return ,$result
}

function Get-EngineVersion {
    $identity = Get-Content -LiteralPath (Join-Path $repositoryRoot "src\Foundry.Domain\EngineIdentity.cs") -Raw
    $matches = [regex]::Matches(
        $identity,
        'public const string EngineVersion = "(?<version>[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?)";')
    if ($matches.Count -ne 1) {
        throw "EngineIdentity.cs did not contain one bounded engine version."
    }

    return $matches[0].Groups["version"].Value
}

function Test-PathAtOrBelow {
    param(
        [Parameter(Mandatory)][string]$Candidate,
        [Parameter(Mandatory)][string]$Root
    )

    $candidateFull = [IO.Path]::GetFullPath($Candidate).TrimEnd([IO.Path]::DirectorySeparatorChar)
    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd([IO.Path]::DirectorySeparatorChar)
    return [string]::Equals($candidateFull, $rootFull, [StringComparison]::OrdinalIgnoreCase) -or
        $candidateFull.StartsWith($rootFull + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
}

function Assert-ExactProperties {
    param(
        [Parameter(Mandatory)][object]$Value,
        [Parameter(Mandatory)][string[]]$Names,
        [Parameter(Mandatory)][string]$Context
    )

    $actual = @($Value.PSObject.Properties.Name | Sort-Object)
    $expected = @($Names | Sort-Object)
    if ($actual.Count -ne $expected.Count -or
        [string]::Join("`n", $actual) -cne [string]::Join("`n", $expected)) {
        throw "$Context did not have its exact closed property set."
    }
}

function Assert-CleanTaggedSource {
    param(
        [Parameter(Mandatory)][string]$ExpectedCommit,
        [Parameter(Mandatory)][string]$ExpectedTag
    )

    $actualCommit = (Invoke-RepositoryGit @("rev-parse", "--verify", "HEAD"))[0]
    if (-not [string]::Equals($actualCommit, $ExpectedCommit, [StringComparison]::Ordinal)) {
        throw "Repository HEAD does not equal the pre-sign source commit."
    }

    if ((Invoke-RepositoryGit @("status", "--porcelain=v1", "--untracked-files=all")).Count -ne 0) {
        throw "Signed-package finalization requires a clean committed source tree."
    }

    $tagType = (Invoke-RepositoryGit @("cat-file", "-t", $ExpectedTag))[0]
    if (-not [string]::Equals($tagType, "tag", [StringComparison]::Ordinal)) {
        throw "The exact version tag is absent or is not an annotated tag."
    }

    $tagCommit = (Invoke-RepositoryGit @("rev-parse", "$ExpectedTag^{}"))[0]
    if (-not [string]::Equals($tagCommit, $ExpectedCommit, [StringComparison]::Ordinal)) {
        throw "The exact version tag does not identify HEAD."
    }

    $null = @(& git -C $repositoryRoot verify-tag --raw $ExpectedTag 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "The exact version tag does not carry a valid trusted signature."
    }
}

function Resolve-InventoryPath {
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][string]$RelativePath
    )

    if ([string]::IsNullOrWhiteSpace($RelativePath) -or
        $RelativePath.Length -gt 512 -or
        [IO.Path]::IsPathFullyQualified($RelativePath) -or
        $RelativePath.Contains("\", [StringComparison]::Ordinal) -or
        $RelativePath -match '[\x00-\x1F\x7F<>:"|?*]') {
        throw "A pre-sign inventory path was not one normalized relative path."
    }

    $segments = $RelativePath.Split("/", [StringSplitOptions]::None)
    $invalidSegments = @(
        $segments |
            Where-Object {
                $_ -in @("", ".", "..") -or
                $_.Length -gt 255 -or
                $_.EndsWith(" ", [StringComparison]::Ordinal) -or
                $_.EndsWith(".", [StringComparison]::Ordinal) -or
                $_ -match '\A(?i:CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\..*)?\z'
            }
    )
    if ($segments.Count -eq 0 -or $invalidSegments.Count -ne 0) {
        throw "A pre-sign inventory path contained an unsafe segment."
    }

    $rootFull = [IO.Path]::GetFullPath($Root)
    $candidate = [IO.Path]::GetFullPath((Join-Path $rootFull ([string]::Join([IO.Path]::DirectorySeparatorChar, $segments))))
    $prefix = $rootFull.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $candidate.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "A pre-sign inventory path escaped the signed input root."
    }

    return $candidate
}

function Assert-CompiledIdentity {
    param(
        [Parameter(Mandatory)][string]$PayloadRoot,
        [Parameter(Mandatory)][string]$EngineVersion,
        [Parameter(Mandatory)][string]$SourceCommit
    )

    $expectedProductVersion = "$EngineVersion+$SourceCommit"
    $appPath = Join-Path $PayloadRoot "Foundry.App.WinForms.exe"
    $depsPath = Join-Path $PayloadRoot "Foundry.App.WinForms.deps.json"
    if (-not (Test-Path -LiteralPath $appPath -PathType Leaf) -or
        -not (Test-Path -LiteralPath $depsPath -PathType Leaf)) {
        throw "Signed input lacked the application or dependency identity."
    }

    $firstParty = @(
        Get-ChildItem -LiteralPath $PayloadRoot -Recurse -File |
            Where-Object { $_.Name -eq "Foundry.App.WinForms.exe" -or $_.Name -like "Foundry.*.dll" }
    )
    if ($firstParty.Count -lt 2) {
        throw "Signed input lacked the bounded first-party assembly set."
    }
    foreach ($file in $firstParty) {
        $productVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($file.FullName).ProductVersion
        if (-not [string]::Equals($productVersion, $expectedProductVersion, [StringComparison]::Ordinal)) {
            throw "A signed first-party ProductVersion did not equal the exact engine and source identity."
        }
    }

    $deps = Get-Content -LiteralPath $depsPath -Raw | ConvertFrom-Json -Depth 100
    $appIdentities = @($deps.libraries.PSObject.Properties.Name | Where-Object { $_ -like "Foundry.App.WinForms/*" })
    if ($appIdentities.Count -ne 1 -or
        -not [string]::Equals($appIdentities[0], "Foundry.App.WinForms/$EngineVersion", [StringComparison]::Ordinal)) {
        throw "Signed dependency identity did not equal EngineIdentity."
    }
}

$normalizedThumbprints = @(
    $AllowedSignerThumbprint |
        ForEach-Object { ([string]$_).Replace(" ", "").ToUpperInvariant() } |
        Sort-Object -Unique
)
if ($normalizedThumbprints.Count -eq 0 -or
    @($normalizedThumbprints | Where-Object { $_ -notmatch '\A[0-9A-F]{40}\z' }).Count -ne 0) {
    throw "Allowed signer thumbprints must be one or more exact SHA-1 certificate thumbprints."
}

$resolvedInput = (Resolve-Path -LiteralPath $SignedInputPath -ErrorAction Stop).Path
$resolvedManifest = (Resolve-Path -LiteralPath $PreSignManifestPath -ErrorAction Stop).Path
if (-not (Test-Path -LiteralPath $resolvedInput -PathType Container) -or
    -not (Test-Path -LiteralPath $resolvedManifest -PathType Leaf)) {
    throw "Signed input and pre-sign manifest must already exist."
}
if ((Get-Item -LiteralPath $resolvedInput).Attributes -band [IO.FileAttributes]::ReparsePoint) {
    throw "Signed input root may not be a reparse point."
}

$inputPrefix = $resolvedInput.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$packagePrefix = [IO.Path]::GetFullPath($packageRoot).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if ((Test-PathAtOrBelow $resolvedInput (Join-Path $packageRoot "unsigned-pre-sign")) -or
    (Test-PathAtOrBelow $resolvedInput (Join-Path $packageRoot "signed-final")) -or
    $packagePrefix.StartsWith($inputPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Signed input must be a separate copy outside unsigned evidence, final output, and package-root ancestors."
}

$manifest = Get-Content -LiteralPath $resolvedManifest -Raw | ConvertFrom-Json -Depth 100
Assert-ExactProperties $manifest @("schemaVersion", "state", "sourceCommit", "engineVersion", "productVersion", "files") "Pre-sign manifest"
if ($manifest.schemaVersion -ne 1 -or $manifest.state -cne "unsigned-pre-sign") {
    throw "Pre-sign manifest did not identify the supported unsigned state."
}

$engineVersion = Get-EngineVersion
$sourceCommit = ([string]$manifest.sourceCommit).Trim()
$expectedProductVersion = "$engineVersion+$sourceCommit"
if ($sourceCommit -notmatch '\A[0-9a-f]{40}\z' -or
    -not [string]::Equals([string]$manifest.engineVersion, $engineVersion, [StringComparison]::Ordinal) -or
    -not [string]::Equals([string]$manifest.productVersion, $expectedProductVersion, [StringComparison]::Ordinal)) {
    throw "Pre-sign manifest did not match the exact engine and source identity."
}

$expectedTag = "v$engineVersion"
Assert-CleanTaggedSource $sourceCommit $expectedTag

$manifestEntries = @($manifest.files)
if ($manifestEntries.Count -eq 0 -or $manifestEntries.Count -gt 4096) {
    throw "Pre-sign manifest file inventory was empty or exceeded its bound."
}

$manifestByPath = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
foreach ($entry in $manifestEntries) {
    Assert-ExactProperties $entry @("path", "sha256", "role") "Pre-sign file entry"
    $relativePath = [string]$entry.path
    $null = Resolve-InventoryPath $resolvedInput $relativePath
    if (-not $manifestByPath.TryAdd($relativePath, $entry) -or
        [string]$entry.sha256 -notmatch '\A[0-9A-F]{64}\z' -or
        [string]$entry.role -notin @("first-party-authenticode", "unchanged")) {
        throw "Pre-sign manifest contained a duplicate or invalid file entry."
    }
}

$inputItems = @(Get-ChildItem -LiteralPath $resolvedInput -Recurse -Force)
if (@($inputItems | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count -ne 0) {
    throw "Signed input may not contain reparse points."
}
$inputFiles = @($inputItems | Where-Object { -not $_.PSIsContainer })
if ($inputFiles.Count -ne $manifestEntries.Count -or $inputFiles.Count -gt 4096 -or
    ($inputFiles | Measure-Object Length -Sum).Sum -gt 2147483648) {
    throw "Signed input file inventory did not match its bounded manifest."
}

try {
    New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
    try {
        $packageLock = [IO.File]::Open(
            $packageLockPath,
            [IO.FileMode]::OpenOrCreate,
            [IO.FileAccess]::ReadWrite,
            [IO.FileShare]::None)
    }
    catch {
        $lockError = $_.Exception
        while ($null -ne $lockError.InnerException) {
            $lockError = $lockError.InnerException
        }
        if ($lockError -isnot [IO.IOException]) {
            throw
        }

        throw "Another packaging process already holds the package-root lock."
    }

    if (Test-Path -LiteralPath $stagingRoot) {
        throw "Refusing to reuse a signed-final staging directory."
    }
    New-Item -ItemType Directory -Path $stagedPayload | Out-Null
    foreach ($entry in $manifestEntries) {
        $sourcePath = Resolve-InventoryPath $resolvedInput ([string]$entry.path)
        $destinationPath = Join-Path $stagedPayload ([string]$entry.path).Replace("/", [IO.Path]::DirectorySeparatorChar)
        $destinationParent = Split-Path -Parent $destinationPath
        New-Item -ItemType Directory -Path $destinationParent -Force | Out-Null
        Copy-Item -LiteralPath $sourcePath -Destination $destinationPath
    }

    $stagedPayloadFull = (Resolve-Path -LiteralPath $stagedPayload).Path
    $stagedFiles = @(Get-ChildItem -LiteralPath $stagedPayload -Recurse -File | Sort-Object FullName)
    if ($stagedFiles.Count -ne $manifestEntries.Count -or
        ($stagedFiles | Measure-Object Length -Sum).Sum -gt 2147483648 -or
        @($stagedFiles | Where-Object { $_.Extension -ieq ".pdb" }).Count -ne 0) {
        throw "Copied signed input changed inventory or contained PDBs in its final payload."
    }

    $signedInventory = @()
    $observedSignerThumbprints = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($file in $stagedFiles) {
        $relativePath = $file.FullName.Substring($stagedPayloadFull.Length + 1).Replace("\", "/")
        $entry = $null
        if (-not $manifestByPath.TryGetValue($relativePath, [ref]$entry)) {
            throw "Signed input contained a file absent from the pre-sign manifest."
        }

        $expectedRole = if ($file.Name -eq "Foundry.App.WinForms.exe" -or $file.Name -like "Foundry.*.dll") {
            "first-party-authenticode"
        }
        else {
            "unchanged"
        }
        if (-not [string]::Equals([string]$entry.role, $expectedRole, [StringComparison]::Ordinal)) {
            throw "A pre-sign inventory role did not match the bounded first-party rule."
        }

        $digest = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        if ($expectedRole -eq "unchanged") {
            if (-not [string]::Equals($digest, [string]$entry.sha256, [StringComparison]::Ordinal)) {
                throw "A non-signable file changed after the pre-sign build."
            }
        }
        else {
            if ([string]::Equals($digest, [string]$entry.sha256, [StringComparison]::Ordinal)) {
                throw "A required first-party file remained byte-identical to its unsigned input."
            }

            $signature = Get-AuthenticodeSignature -LiteralPath $file.FullName
            if ($signature.Status.ToString() -cne "Valid" -or
                $signature.SignatureType.ToString() -cne "Authenticode" -or
                $null -eq $signature.SignerCertificate -or
                $null -eq $signature.TimeStamperCertificate -or
                [string]::IsNullOrWhiteSpace([string]$signature.SignerCertificate.Thumbprint)) {
                throw "A first-party file lacked a valid timestamped signature from an authorized certificate."
            }
            $thumbprint = ([string]$signature.SignerCertificate.Thumbprint).Replace(" ", "").ToUpperInvariant()
            if ($thumbprint -notin $normalizedThumbprints) {
                throw "A first-party file was signed by a certificate outside the authorized thumbprint set."
            }
            [void]$observedSignerThumbprints.Add($thumbprint)
        }

        $signedInventory += [ordered]@{
            path = $relativePath
            sha256 = $digest
            role = $expectedRole
        }
    }

    Assert-CompiledIdentity $stagedPayload $engineVersion $sourceCommit
    Assert-CleanTaggedSource $sourceCommit $expectedTag

    $signedInventory | ForEach-Object { "$($_.sha256)  $($_.path.Replace('/', '\'))" } |
        Set-Content -LiteralPath $stagedSums -Encoding utf8NoBOM
    $zipName = "honest-ink-win-x64-$engineVersion-signed.zip"
    $stagedZip = Join-Path $stagedBundle $zipName
    $stagedZipHash = Join-Path $stagedBundle "$zipName.sha256"
    Compress-Archive -Path (Join-Path $stagedPayload "*") -DestinationPath $stagedZip
    $zipDigest = (Get-FileHash -LiteralPath $stagedZip -Algorithm SHA256).Hash
    "$zipDigest  $zipName" | Set-Content -LiteralPath $stagedZipHash -Encoding ascii

    $releaseManifest = [ordered]@{
        schemaVersion = 1
        state = "signed-final"
        sourceCommit = $sourceCommit
        engineVersion = $engineVersion
        productVersion = $expectedProductVersion
        signedTag = $expectedTag
        signerThumbprints = @($observedSignerThumbprints | Sort-Object)
        files = $signedInventory
        zip = [ordered]@{
            path = $zipName
            sha256 = $zipDigest
        }
    }
    $releaseManifest | ConvertTo-Json -Depth 10 |
        Set-Content -LiteralPath $stagedReleaseManifest -Encoding utf8NoBOM

    foreach ($requiredOutput in @($stagedPayload, $stagedSums, $stagedZip, $stagedZipHash, $stagedReleaseManifest)) {
        if (-not (Test-Path -LiteralPath $requiredOutput)) {
            throw "Signed finalization did not produce its complete local evidence set."
        }
    }

    Assert-CleanTaggedSource $sourceCommit $expectedTag
    New-Item -ItemType Directory -Path $backupRoot | Out-Null
    if (Test-Path -LiteralPath $finalBundle) {
        Move-Item -LiteralPath $finalBundle -Destination $backupBundle
        $backedUp = $true
    }
    Move-Item -LiteralPath $stagedBundle -Destination $finalBundle
    $promoted = $true
}
catch {
    $operationError = $_
    try {
        if ($promoted -and (Test-Path -LiteralPath $finalBundle)) {
            Move-Item -LiteralPath $finalBundle -Destination $stagedBundle
        }
        if ($backedUp -and (Test-Path -LiteralPath $backupBundle)) {
            Move-Item -LiteralPath $backupBundle -Destination $finalBundle
        }
    }
    catch {
        $preserveRecoveryData = $true
        throw "Signed finalization failed and rollback was incomplete. Recovery data remains in the package root."
    }

    throw $operationError
}
finally {
    try {
        if (-not $preserveRecoveryData) {
            if (Test-Path -LiteralPath $stagingRoot) {
                Remove-Item -LiteralPath $stagingRoot -Recurse -Force
            }
            if (Test-Path -LiteralPath $backupRoot) {
                Remove-Item -LiteralPath $backupRoot -Recurse -Force
            }
        }
    }
    finally {
        if ($null -ne $packageLock) {
            $packageLock.Dispose()
            $packageLock = $null
        }
    }
}

Write-Host "Verified signed bytes and assembled local final evidence under out\signed-final."
Write-Host "No signing, installation, upload, distribution, or publication was performed."
