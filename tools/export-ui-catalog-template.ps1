# SPDX-License-Identifier: GPL-3.0-or-later
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot 'src/Foundry.App.WinForms/Foundry.App.WinForms.csproj'
$resolvedOutput = [IO.Path]::GetFullPath($OutputPath, (Get-Location).Path)

dotnet run --project $projectPath --configuration Release --no-launch-profile -- --export-ui-catalog-template $resolvedOutput
if ($LASTEXITCODE -ne 0) {
    throw "UI catalog template export failed with exit code $LASTEXITCODE."
}

Write-Host "Wrote the neutral multilingual-seat review packet to $resolvedOutput"
