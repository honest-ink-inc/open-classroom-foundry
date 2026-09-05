# Source build and verification

**5 September 2026 — engineering instructions, not installation or release rites.**
This is the existing repository workflow, made explicit for I31. The current
candidate's measured results belong in the evidence record linked by the
[indexed current handover](../README.md).
A maintainer running these commands is not an independent stranger rebuild,
teacher evaluation, or acceptance by a second maintainer.

## Prerequisites and boundaries

- Use a Windows working copy with Git, PowerShell 7 (`pwsh`), and the exact .NET
  SDK in [global.json](../../global.json): currently `10.0.302`, roll-forward
  disabled. The full solution includes Windows infrastructure and WinForms;
  the portable sample graph in CI is not a cross-platform desktop application.
- Retain full Git history. The first-admission verifier must find the original
  C1 and record-only C2 in the selected commit's ancestry. A source ZIP or shallow
  clone alone cannot prove that history.
- Restore needs access to the sources in [NuGet.config](../../NuGet.config).
  The code's offline workflow does not imply an uncached build needs no network.
  Do not supply cloud credentials or connect a district service to run tests.
- Use only project-supplied synthetic fixtures. Never import a real classroom
  library, learner material, or a facilitator's blind-study key into this tree.
- An interactive Windows desktop is needed for headed UI Automation. Headed
  skips must remain visible and reported; a machine pass is not an NVDA speech
  finding, physical-print inspection, multilingual review, or teacher pilot.

Run commands from the repository root. Read [AGENTS.md](../../AGENTS.md),
[CONTRIBUTING.md](../../CONTRIBUTING.md), the current-state marker in the
[document index](../README.md), and the governing ADR before editing.
Do not copy credentials into commands or evidence logs.

## Restore and build

Install the per-clone hook before any commit:

```powershell
pwsh -NoProfile -File tools/install-hooks.ps1
dotnet --version
pwsh -NoProfile -File tools/verify-recipe-identity-ratification.ps1 -RequireRatified
dotnet restore OpenClassroomFoundry.slnx --locked-mode --configfile NuGet.config
dotnet restore src/Foundry.App.WinForms/Foundry.App.WinForms.csproj --runtime win-x64 --locked-mode -p:NuGetLockFilePath=packages.win-x64.lock.json --configfile NuGet.config
dotnet tool restore --configfile NuGet.config
dotnet build OpenClassroomFoundry.slnx --no-restore --configuration Release -warnaserror
```

Check each command's exit code before proceeding. `dotnet --version` must equal
the checked-in version, not merely a compatible major version. Do not regenerate
lock files to hide a failed locked restore. The solution locks and separate
`win-x64` application locks describe different graphs; neither replaces the other.
`Directory.Build.props` also makes warnings errors.

## Local closing sequence

After the Release build, apply formatting and independently verify it:

```powershell
dotnet format OpenClassroomFoundry.slnx --no-restore
dotnet format OpenClassroomFoundry.slnx --no-restore --verify-no-changes
dotnet build OpenClassroomFoundry.slnx --no-restore --configuration Release -warnaserror -t:Rebuild
pwsh -NoProfile -File tools/run-ci-tests.ps1
pwsh -NoProfile -File tools/run-ci-tests.ps1
```

The post-format rebuild is necessary if formatting rewrites source files: the
compiled-call-site audit refuses an assembly older than its source, even when
the edit only changes line endings. Both full test runs are required. The runner
retains stdout, stderr, TRX and a
bounded receipt under `out/ci-test-run/`. Read the receipt and suite outcomes;
retain every failing test's name and message. Do not edit source or change the
Git index while a run is active: the receipt binds source and build identities.
An under-load failure followed by a passing isolated run remains an open sighting,
not permission to raise its timeout. Follow the
[sightings register](../evidence/sightings-register.md).

When presses or their rendered output change, generate samples into two new,
unused directories (replace the example names if they already contain output):

```powershell
dotnet tools/SampleGenerator/bin/Release/net10.0/Foundry.Tools.SampleGenerator.dll . out/source-check-samples-a
dotnet tools/SampleGenerator/bin/Release/net10.0/Foundry.Tools.SampleGenerator.dll . out/source-check-samples-b
```

Compare complete recursive file inventories and every file's SHA-256, with no
exclusions. Then compare the relative-path/hash rows against the immutable
[first-admission baseline](../../tests/Rendering/Fixtures/recipe-first-admission-samples.sha256).
The exact comparison is in the **Determinism gate** in
[CI](../../.github/workflows/ci.yml). A mismatch is a finding; do not overwrite
the baseline. Do not use `--seeded` for this public evidence workflow.

## From local evidence to a reviewed change

Record source identity, actual commands, exit codes, failures, artifact hashes,
and remaining holds. Commit only reviewed paths on a branch and open a pull
request under session-specific authority. Read CI and CodeQL conclusions for
that exact head and inspect the retained SARIF results. Record them in the
[evidence ledger](../evidence/evidence-ledger.json). If a merge is authorized,
use a merge commit only, then read the exact-main runs; branch green is not main
green. No force push, squash merge, or rebase merge preserves this proof chain.

These commands do not run `tools/publish.ps1`, sign or install an application,
create a release, or deploy a site. Those acts have separate exact-source and
human gates. See the [decision index](../governance/decision-index.md).

## Independent rebuild still required

I31 remains open until an actual authorized outside builder records the exact
source/SDK/dependency inputs, missing steps, build and test results, a bounded
recipe-edit exercise with identity consequences, and a reviewed disposition.
Appointing a deputy also needs that person's actual acceptance and authority;
this guide appoints nobody. Any archive must identify its exact source,
toolchain, dependencies, licenses and hashes without bundling credentials or
private records.
