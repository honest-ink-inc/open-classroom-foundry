# SPDX-License-Identifier: GPL-3.0-or-later
#Requires -Version 7.2
# Builds unsigned, pre-sign inputs only. It never signs, tags, installs, uploads,
# distributes, or publishes. The typist must first authorize one coherent
# version in source and assembly metadata; this script fails closed otherwise.
[CmdletBinding()]
param(
    [ValidateSet("Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if (-not [string]::Equals($Configuration, "Release", [StringComparison]::Ordinal)) {
    throw "Unsigned pre-sign packaging accepts only the exact Release configuration."
}

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$packageRoot = Join-Path $repositoryRoot "out"
$packageLockPath = Join-Path $packageRoot ".package.lock"
$operationId = [Guid]::NewGuid().ToString("N")
$stagingRoot = Join-Path $packageRoot ".package-stage-$operationId"
$backupRoot = Join-Path $packageRoot ".package-backup-$operationId"
$stagedBuildArtifacts = Join-Path $stagingRoot "build-artifacts"
$stagedBundle = Join-Path $stagingRoot "unsigned-pre-sign"
$stagedPayload = Join-Path $stagedBundle "payload"
$stagedSymbols = Join-Path $stagedBundle "symbols"
$stagedManifest = Join-Path $stagedBundle "pre-sign-manifest.json"
$stagedSums = Join-Path $stagedBundle "SHA256SUMS.pre-sign.txt"
$stagedZip = Join-Path $stagedBundle "honest-ink-win-x64-unsigned-pre-sign.zip"
$stagedZipHash = Join-Path $stagedBundle "honest-ink-win-x64-unsigned-pre-sign.zip.sha256"
$stagedSymbolsZip = Join-Path $stagedBundle "honest-ink-win-x64-unsigned-pre-sign-symbols.zip"
$stagedSymbolsZipHash = Join-Path $stagedBundle "honest-ink-win-x64-unsigned-pre-sign-symbols.zip.sha256"
$finalBundle = Join-Path $packageRoot "unsigned-pre-sign"
$backupBundle = Join-Path $backupRoot "unsigned-pre-sign"

$packageLock = $null
$promoted = $false
$backedUp = $false
$preserveRecoveryData = $false

function Invoke-RepositoryGit {
    param([Parameter(Mandatory)][string[]]$Arguments)

    $output = @(& git -C $repositoryRoot @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "Repository identity verification failed."
    }

    $result = @($output | ForEach-Object { ([string]$_).Trim() } | Where-Object { $_.Length -gt 0 })
    return ,$result
}

function Get-EngineVersion {
    $identityPath = Join-Path $repositoryRoot "src\Foundry.Domain\EngineIdentity.cs"
    $identity = Get-Content -LiteralPath $identityPath -Raw
    $matches = [regex]::Matches(
        $identity,
        'public const string EngineVersion = "(?<version>[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?)";')
    if ($matches.Count -ne 1) {
        throw "EngineIdentity.cs did not contain one bounded engine version."
    }

    return $matches[0].Groups["version"].Value
}

function Assert-CleanSourceIdentity {
    param([Parameter(Mandatory)][string]$ExpectedCommit)

    $actualCommit = (Invoke-RepositoryGit @("rev-parse", "--verify", "HEAD"))[0]
    if (-not [string]::Equals($actualCommit, $ExpectedCommit, [StringComparison]::Ordinal)) {
        throw "Repository HEAD changed during unsigned pre-sign packaging."
    }

    $status = Invoke-RepositoryGit @("status", "--porcelain=v1", "--untracked-files=all")
    if ($status.Count -ne 0) {
        throw "Unsigned pre-sign packaging requires a clean committed source tree."
    }
}

function Assert-BuildIdentity {
    param(
        [Parameter(Mandatory)][string]$PayloadRoot,
        [Parameter(Mandatory)][string]$EngineVersion,
        [Parameter(Mandatory)][string]$SourceCommit
    )

    $appPath = Join-Path $PayloadRoot "Foundry.App.WinForms.exe"
    $depsPath = Join-Path $PayloadRoot "Foundry.App.WinForms.deps.json"
    if (-not (Test-Path -LiteralPath $appPath -PathType Leaf) -or
        -not (Test-Path -LiteralPath $depsPath -PathType Leaf)) {
        throw "Unsigned pre-sign output lacked the application or dependency identity."
    }

    $expectedProductVersion = "$EngineVersion+$SourceCommit"
    $firstParty = @(
        Get-ChildItem -LiteralPath $PayloadRoot -Recurse -File |
            Where-Object {
                $_.Name -eq "Foundry.App.WinForms.exe" -or
                ($_.Name -like "Foundry.*.dll")
            } |
            Sort-Object Name
    )
    if ($firstParty.Count -lt 2) {
        throw "Unsigned pre-sign output lacked the bounded first-party assembly set."
    }

    foreach ($file in $firstParty) {
        $productVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($file.FullName).ProductVersion
        if (-not [string]::Equals($productVersion, $expectedProductVersion, [StringComparison]::Ordinal)) {
            throw "Compiled ProductVersion does not equal EngineIdentity plus the exact source commit."
        }

        $signature = Get-AuthenticodeSignature -LiteralPath $file.FullName
        if ($signature.Status -ne [Management.Automation.SignatureStatus]::NotSigned) {
            throw "The build stage accepts only unsigned first-party inputs."
        }
    }

    $deps = Get-Content -LiteralPath $depsPath -Raw | ConvertFrom-Json -Depth 100
    $appIdentities = @(
        $deps.libraries.PSObject.Properties.Name |
            Where-Object { $_ -like "Foundry.App.WinForms/*" }
    )
    if ($appIdentities.Count -ne 1 -or
        -not [string]::Equals(
            $appIdentities[0],
            "Foundry.App.WinForms/$EngineVersion",
            [StringComparison]::Ordinal)) {
        throw "Compiled dependency identity does not equal EngineIdentity."
    }
}

function Assert-PortableSourceMetadata {
    param([Parameter(Mandatory)][IO.FileInfo[]]$Files)

    $canonicalSourceLink = "https://raw.githubusercontent.com/honest-ink-inc/open-classroom-foundry/"
    $legacySourceLink = "https://raw.githubusercontent.com/Spacejunk-io/open-classroom-foundry/"
    foreach ($file in $Files) {
        $text = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($file.FullName))
        if ($text.Contains($repositoryRoot, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Compiled release metadata exposed the local repository path."
        }
        if ($text.Contains($legacySourceLink, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Compiled release metadata retained the superseded source repository."
        }
        if ($file.Name -like "Foundry.*.pdb" -and
            -not $text.Contains($canonicalSourceLink, [StringComparison]::Ordinal)) {
            throw "A release PDB lacked the canonical Honest Ink SourceLink mapping."
        }
    }
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

    $legacyOutputs = @(
        (Join-Path $packageRoot "publish"),
        (Join-Path $packageRoot "SHA256SUMS.txt"),
        (Join-Path $packageRoot "honest-ink-win-x64.zip")
    )
    if (@($legacyOutputs | Where-Object { Test-Path -LiteralPath $_ }).Count -ne 0) {
        throw "Legacy ambiguously named package outputs exist; quarantine them before building new pre-sign inputs."
    }

    if (Test-Path -LiteralPath $stagingRoot) {
        throw "Refusing to reuse an unsigned pre-sign staging directory."
    }

    $headCommit = (Invoke-RepositoryGit @("rev-parse", "--verify", "HEAD"))[0]
    Assert-CleanSourceIdentity $headCommit
    $engineVersion = Get-EngineVersion

    New-Item -ItemType Directory -Path $stagedPayload | Out-Null
    New-Item -ItemType Directory -Path $stagedSymbols | Out-Null

    $applicationProject = Join-Path $repositoryRoot "src\Foundry.App.WinForms\Foundry.App.WinForms.csproj"
    dotnet restore $applicationProject --runtime win-x64 --locked-mode -p:NuGetLockFilePath=packages.win-x64.lock.json --configfile (Join-Path $repositoryRoot "NuGet.config") --artifacts-path $stagedBuildArtifacts
    if ($LASTEXITCODE -ne 0) {
        throw "Locked win-x64 restore failed."
    }

    dotnet publish $applicationProject -c Release --runtime win-x64 --self-contained false --no-restore --artifacts-path $stagedBuildArtifacts -o $stagedPayload --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "Unsigned pre-sign build failed."
    }

    $stagedPayloadFull = (Resolve-Path -LiteralPath $stagedPayload).Path
    $symbolFiles = @(Get-ChildItem -LiteralPath $stagedPayload -Recurse -File -Filter "*.pdb")
    if ($symbolFiles.Count -eq 0) {
        throw "Release compilation produced no segregated symbol evidence."
    }
    if (@($symbolFiles | Where-Object { $_.Name -like "Foundry.*.pdb" }).Count -lt 2) {
        throw "Release compilation produced no bounded first-party symbol set."
    }
    foreach ($symbol in $symbolFiles) {
        $relativePath = $symbol.FullName.Substring($stagedPayloadFull.Length + 1)
        $symbolDestination = Join-Path $stagedSymbols $relativePath
        $symbolParent = Split-Path -Parent $symbolDestination
        New-Item -ItemType Directory -Path $symbolParent -Force | Out-Null
        Move-Item -LiteralPath $symbol.FullName -Destination $symbolDestination
    }
    if (@(Get-ChildItem -LiteralPath $stagedPayload -Recurse -File -Filter "*.pdb").Count -ne 0) {
        throw "A PDB remained in the unsigned pre-sign payload."
    }

    Assert-BuildIdentity $stagedPayload $engineVersion $headCommit
    $metadataFiles = @(
        Get-ChildItem -LiteralPath $stagedPayload -Recurse -File |
            Where-Object { $_.Name -eq "Foundry.App.WinForms.exe" -or $_.Name -like "Foundry.*.dll" }
    ) + @(Get-ChildItem -LiteralPath $stagedSymbols -Recurse -File -Filter "*.pdb")
    Assert-PortableSourceMetadata $metadataFiles
    Assert-CleanSourceIdentity $headCommit

    $payloadFiles = @(Get-ChildItem -LiteralPath $stagedPayload -Recurse -File | Sort-Object FullName)
    if ($payloadFiles.Count -eq 0) {
        throw "Unsigned pre-sign build produced no files."
    }

    $inventory = @(
        foreach ($file in $payloadFiles) {
            $relativePath = $file.FullName.Substring($stagedPayloadFull.Length + 1).Replace("\", "/")
            $isFirstParty = $file.Name -eq "Foundry.App.WinForms.exe" -or $file.Name -like "Foundry.*.dll"
            [ordered]@{
                path = $relativePath
                sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
                role = if ($isFirstParty) { "first-party-authenticode" } else { "unchanged" }
            }
        }
    )

    $manifest = [ordered]@{
        schemaVersion = 1
        state = "unsigned-pre-sign"
        sourceCommit = $headCommit
        engineVersion = $engineVersion
        productVersion = "$engineVersion+$headCommit"
        files = $inventory
    }
    $manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $stagedManifest -Encoding utf8NoBOM
    $inventory | ForEach-Object { "$($_.sha256)  $($_.path.Replace('/', '\'))" } |
        Set-Content -LiteralPath $stagedSums -Encoding utf8NoBOM

    Compress-Archive -Path (Join-Path $stagedPayload "*") -DestinationPath $stagedZip
    $zipDigest = (Get-FileHash -LiteralPath $stagedZip -Algorithm SHA256).Hash
    "$zipDigest  $([IO.Path]::GetFileName($stagedZip))" |
        Set-Content -LiteralPath $stagedZipHash -Encoding ascii
    Compress-Archive -Path (Join-Path $stagedSymbols "*") -DestinationPath $stagedSymbolsZip
    $symbolsZipDigest = (Get-FileHash -LiteralPath $stagedSymbolsZip -Algorithm SHA256).Hash
    "$symbolsZipDigest  $([IO.Path]::GetFileName($stagedSymbolsZip))" |
        Set-Content -LiteralPath $stagedSymbolsZipHash -Encoding ascii

    foreach ($requiredOutput in @(
        $stagedPayload,
        $stagedSymbols,
        $stagedManifest,
        $stagedSums,
        $stagedZip,
        $stagedZipHash,
        $stagedSymbolsZip,
        $stagedSymbolsZipHash)) {
        if (-not (Test-Path -LiteralPath $requiredOutput)) {
            throw "Unsigned pre-sign packaging did not produce its complete bounded output set."
        }
    }

    Assert-CleanSourceIdentity $headCommit
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
        throw "Unsigned pre-sign packaging failed and rollback was incomplete. Recovery data remains in the package root."
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

Write-Host "Built unsigned pre-sign inputs under out\unsigned-pre-sign."
Write-Host "These files are not a release. Signing, finalization, installation, tagging, versioning, distribution, and publication remain the typist's acts."
