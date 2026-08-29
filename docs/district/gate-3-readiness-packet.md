# Gate 3 — district readiness packet

**From:** Honest Ink, Inc. (Maryland incorporation in progress) · contact@honest-ink.org · **Date:** 29 August 2026 · **Covers:** Honest Ink (engine: Open Classroom Foundry, v0.7.0-alpha, GPL-3.0-or-later)

This packet is what the district's IT, privacy, and safeguarding reviewers need in one place. Every claim below is either machine-verified in the repository's test suite or explicitly marked as procedure. The ask at the end is specific: three named seats, one review meeting, and — later, separately — a signature that gates a capability we will not enable without it.

## 1. What the software is and is not

A Windows desktop application for **teachers only** — never students — that turns teacher intent into printable classroom materials. Its zero-AI module (Deterministic Press) makes no network connection at all. Its AI-assisted modules are **off by default and fail closed**: with no district policy file, or with `CloudInferenceEnabled=false`, the application cannot make an outbound call — the provider seam is structurally unreachable (tested, not configured).

## 2. Data posture (full detail: [data inventory](../privacy/data-inventory.md), [trust boundaries](../architecture/trust-boundaries.md))

- **Lanes:** Green (teacher-authored, synthetic) / Amber (may contain classroom-derived material) / Restricted (never enters the system). Lane inheritance is escalate-only; unknown provenance lands Amber.
- **Amber never persists**: no autosave, purge on completion *and* cancellation (tested); an Amber artifact cannot be saved to the library (tested).
- **Diagnostics are content-free by allowlist** — no prose, filenames, excerpts, or hashes can enter them (tested via canary); remote telemetry is off by default.
- **Egress**: only to district-allowlisted endpoints (a provider with an off-allowlist endpoint is unconstructable — tested); every outbound payload is shown to the teacher verbatim **before** sending (Gate A, structural).
- **Identity**: Entra ID interactive auth, bearer tokens only, **no API keys anywhere** (asserted by test); token cache DPAPI-encrypted per user; `DisableAutomaticAuthentication` respects Conditional Access.

## 3. Safeguarding

Gate C is a teacher-invoked pause ("I saw something concerning — pause here"), reachable from any in-flight state; it blocks the job, purges on exit, and surfaces **the district's own procedure text** verbatim from policy — the application performs no detection, assessment, recording, or reporting, and says so. Design: [gate-c-design.md](../safeguarding/gate-c-design.md). The district supplies its procedure text in the policy file.

## 4. Deployment and operations

- Install: xcopy-deployable publish; `.intunewin` wrapping planned **with** district IT (Win32 Content Prep over `tools/publish.ps1` output).
- Signing: Authenticode via the district's certificate or Honest Ink, Inc.'s own OV certificate (entity formation in progress) — an unsigned build is not distributed.
- Rollback: signed tags reproduce any prior build; the **kill switch is policy, not code** — set `CloudInferenceEnabled=false` or delete `policy.json` and every device refuses egress on next launch, no redeploy.
- Updates: staged rollout procedure agreed with IT at deployment time; supply chain: pinned SDK, secret scan and dependency inventory in CI, SHA-256 manifest per release.

## 5. The checklist we ask the district to complete (plan Gate 3)

| Item | District owner | Our part |
|---|---|---|
| Central instructional-software approval | Curriculum office | This packet + a demo on request |
| Provider contract/configuration review (Azure OpenAI, district tenant) | IT | The adapter's endpoint allowlist and auth posture, §2 above |
| Deployment geography / statefulness decision | IT + privacy | Documented: nothing server-side of ours exists; state is per-device |
| RBAC / Conditional Access / policy attestation | IT | `DisableAutomaticAuthentication` honors CA; policy file schema provided |
| Records-approved retention/disposal | Privacy/records | Amber-never-persists evidence; Green artifacts are teacher documents under existing rules |
| Incident + safeguarding playbooks | Safeguarding lead | Gate C procedure-text slot; security channel contact@honest-ink.org |
| Intune/signing/uninstall/rollback evidence | IT | Joint working session |
| Training (teacher, accessibility, privacy, admin) | Joint | Pilot materials in `docs/pilots/` are the training seeds |
| Support/patch/disclosure/EOL commitments | Honest Ink, Inc. | SECURITY.md; GPL guarantees the code outlives the entity |

## 6. The written-approval instrument (sign later, not now)

Nothing Amber runs in any classroom until this is signed. Presented now so its existence shapes the review:

> **Approval to process classroom-derived material (Amber lane) — Honest Ink**
>
> The district of ______________________, having completed the Gate 3 review dated __________, approves the use of Honest Ink's Amber-lane capability by consenting staff, subject to:
> the provider configuration reviewed and attached; retention rule: **no persistence** (purge-on-completion/cancellation as evidenced); the safeguarding procedure text attached as deployed; revocation at will via the policy kill switch, effective on next launch, no notice required.
>
> This approval does **not** extend to any Restricted-lane processing, which the software does not implement.
>
> District signatory (name, role) ______________________ Date ________
> For Honest Ink, Inc. ______________________ Date ________

Both parties retain the signed instrument; the repository records only its existence and date.

## 7. The ask, this month

1. Name the three seats (IT, privacy, safeguarding) — governance holds a place for each.
2. One review meeting against §5 in September 2026.
3. Staff-pilot awareness: the six-week pilot (8 Sep – 16 Oct) is **staff-only, synthetic-material, Green-lane, no student presence** — rungs 1–2 of our own gate ladder, and we welcome an observer.
