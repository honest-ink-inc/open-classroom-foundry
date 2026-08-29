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

# Windows PowerShell 5.1 lacks [IO.Path]::GetRelativePath; substring the resolved root instead.
$outFull = (Resolve-Path $out).Path
$sums = Get-ChildItem $out -Recurse -File | Get-FileHash -Algorithm SHA256 | ForEach-Object {
    "$($_.Hash)  $($_.Path.Substring($outFull.Length + 1))"
}
$sums | Out-File (Join-Path $out "..\SHA256SUMS.txt") -Encoding utf8

Compress-Archive -Path "$out\*" -DestinationPath (Join-Path $out "..\honest-ink-win-x64.zip") -Force
Write-Host "Published to out\ from the current tree: publish\, SHA256SUMS.txt, honest-ink-win-x64.zip."
Write-Host "This script only builds. Signing, installing on the pilot machine, tagging, versioning, and distributing remain the typist's acts (fourth forge menu, section III)."
