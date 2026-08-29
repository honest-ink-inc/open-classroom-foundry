# SPDX-License-Identifier: GPL-3.0-or-later
# Produces the distributable: framework-dependent win-x64 publish of the app
# shell, a SHA-256 checksum manifest, and a zip. Authenticode signing is the
# district's certificate step and happens after this script (see
# docs/release/hardening-checklist.md); an unsigned zip is a build, not a release.
param([string]$Configuration = "Release")

$ErrorActionPreference = "Stop"
$out = Join-Path $PSScriptRoot "..\out\publish"

dotnet publish (Join-Path $PSScriptRoot "..\src\Foundry.App.WinForms") -c $Configuration -o $out --nologo
if ($LASTEXITCODE -ne 0) { throw "Publish failed." }

$sums = Get-ChildItem $out -Recurse -File | Get-FileHash -Algorithm SHA256 | ForEach-Object {
    "$($_.Hash)  $([System.IO.Path]::GetRelativePath($out, $_.Path))"
}
$sums | Out-File (Join-Path $out "..\SHA256SUMS.txt") -Encoding utf8

Compress-Archive -Path "$out\*" -DestinationPath (Join-Path $out "..\honest-ink-win-x64.zip") -Force
Write-Host "Published to out\ with SHA256SUMS.txt. Sign before any distribution."
