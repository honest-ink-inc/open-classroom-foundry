# 1.0 hardening checklist

**Date opened:** 29 August 2026 · **Engine version:** 0.7.0-alpha · Maps implementation plan Release 1.0 to evidence. "Done" means machine-verified or committed; human items are named as such and gate the release exactly as the plan intends. **0.7.0-alpha is not 1.0**: every open human row below is why.

| Item | Status | Notes |
|---|---|---|
| Signed package and checksum | **Partial** | `tools/publish.ps1` produces the publish, SHA256SUMS, and zip; **Authenticode signing needs the district's certificate or Honest Ink, Inc.'s own OV certificate** (entity track: `docs/pilots/human-gates-coordination-plan.md`). An unsigned zip is a build, not a release |
| Installer / Intune package | **Pending** | `.intunewin` wrapping via Microsoft Win32 Content Prep over the publish output; documented here, executed with district IT |
| Staged rollout / rollback | **Partial** | Git tags + immutable releases give rollback-by-version; the runtime kill switch exists (`DistrictPolicy.CloudInferenceEnabled=false` disables all egress; removing the policy file fails closed to Offline). Rollout staging is a deployment-time procedure with IT |
| Migration and compatibility tests | **Partial** | One project schema version exists (`ProjectSchemaVersion=1`); the migration harness becomes real at schema version 2, and the reversibility tests (save → reopen) already guard v1 |
| Documentation, contribution, localization workflows | **Done / growing** | README, GOVERNANCE, CONTRIBUTING, council charter, data inventory, Gate C design; localization workflow follows the council's multilingual stewardship |
| SBOM, source archive, asset ledger | **Partial** | SBOM-lite + coverage in CI; full CycloneDX blocked on .slnx support upstream; source archive is the GitHub repository; asset ledger complete and CI-verified (13 pack assets + teacher-shelf provenance) |
| SPDX per-file notices (R2-10) | **Done** | 74 source files headed; enforced by an architecture test from this commit forward |
| CI gates (R2-11) | **Done** | Warnings-as-errors, whitespace format gate, 80% core-coverage threshold (88.6% at enablement), secret scan, dependency inventory |
| Hostile-package suite | **Done (first pass)** | Entry-size ceiling, zip-slip inertness (nothing is ever extracted), corrupt/malformed loud failures; deeper fuzzing welcome |
| Six-week staff pilot and defect burn-down | **HUMAN — six weeks, opening date pending** (moved 29 Aug 2026; downstream of Maryland's acceptance — see [the moved calendar](../handover/2026-08-29-the-moved-calendar.md)) | Coordination plan, protocols, walkthrough script, print checklist, and the seeded-error pilot kit all stand ready in `docs/pilots/` and `docs/evidence/pilot-kit/` |
| District readiness (Gate 3) | **HUMAN — packet ready** | `docs/district/gate-3-readiness-packet.md` incl. the written-approval instrument; delivery target 4 Sep 2026 |
| Second maintainer | **HUMAN** | The sustainability DoD item; recruitment plan in the coordination plan; still the bus factor's only cure |

## Rollback drill (documented procedure)

1. Every release is a signed tag; `git checkout <previous-tag>` + `tools/publish.ps1` reproduces the prior build (deterministic builds, pinned SDK).
2. The kill switch is policy, not code: IT sets `CloudInferenceEnabled=false` (or deletes `policy.json` — absence fails closed) and every device refuses egress at the provider boundary on next launch, no redeploy needed.
3. Green projects (`.ocfproj`) open in any prior or later engine that speaks schema version 1 — reversibility is the constitution's word, tested.
