# SPDX-License-Identifier: GPL-3.0-or-later
# Points git at the repository's versioned hooks and reports honestly on whether
# the secret scanner they depend on is actually present. Hooks are per-clone
# configuration, never inherited from a clone or a pull, so every working copy —
# every human, every agent, every machine — runs this once.
param([switch]$Uninstall)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
Push-Location $repo
try {
    if ($Uninstall) {
        git config --unset core.hooksPath
        Write-Host "Hooks disabled: core.hooksPath unset. The pre-commit secret scan no longer runs."
        return
    }

    git config core.hooksPath .githooks
    Write-Host "Hooks enabled: core.hooksPath = .githooks"

    # Resolve exactly as the hook does: PATH first, then the git-ignored
    # local drop. Reporting "not installed" for a binary the hook will
    # happily use would send someone chasing a problem they do not have.
    $gitleaks = Get-Command gitleaks -ErrorAction SilentlyContinue
    $localDrop = Join-Path $repo "tools/bin/gitleaks.exe"
    if (-not $gitleaks -and (Test-Path $localDrop)) {
        $gitleaks = [pscustomobject]@{ Source = $localDrop }
    }
    if ($gitleaks) {
        Write-Host "gitleaks found: $($gitleaks.Source)"
        Write-Host "The pre-commit hook will refuse any commit whose staged changes contain a secret."
    }
    else {
        Write-Warning "gitleaks is NOT installed, so the hook will REFUSE every commit until it is."
        Write-Host "  Install with one of:  winget install gitleaks  |  scoop install gitleaks  |  brew install gitleaks"
        Write-Host "  Or place the binary at tools/bin/gitleaks.exe (that path is git-ignored)."
        Write-Host "  This refusal is deliberate: a scan that silently does not run reads as protection it is not providing."
    }
}
finally {
    Pop-Location
}
