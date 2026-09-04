# Rehearsal council, round two — sessions 7–12: the governance seats

**Date:** 29 August 2026 · **Convened by:** Da Vinci, at the product owner's direction
**Standing:** As in round one — every participant is a rehearsal persona, findings are advisory, and nothing here substitutes for engaged professionals: a real district IT office, real counsel, a real safeguarding lead. Those seats are marked *engaged per deployment* in GOVERNANCE.md precisely because no rehearsal can hold them. What a rehearsal *can* do is make sure the code they will one day inspect was built expecting inspection.

**Personas (fictional, role-titled):** D. — district IT/security · L. — privacy/legal/records · The Smith — lead developer, auditing their own work adversarially · O. — OER/license steward · S. — safeguarding lead · The Owner — product owner/master teacher, chairing synthesis.

---

## Session 7 — District IT and security (D. chairing)

**Verified:** policy fails closed to offline; the egress gate (Gate A) is structural; the provider surface is minimal and stateless; diagnostics content-free; no secrets anywhere in the tree.

**Findings:**
- **R2-1 (gap, fix now).** `DistrictPolicy.CloudInferenceEnabled` and `MaximumLane` exist but **nothing enforces them at the provider boundary** — a wired composition root could call a provider with policy disabled. Add a policy-gating decorator: with cloud inference disabled, or a payload lane above the district maximum, the call is refused *before any egress*, as a first-class `PolicyRefused` outcome. Policy becomes law, not configuration.
- **R2-7 (typist action).** The CI workflow and the gitleaks secret scan have **never actually run** — the repository has no remote. Push to GitHub (private is fine) so every guarantee in `.github/workflows/ci.yml` starts being earned instead of described.
- **R2-12 (binding for the adapter).** When `Foundry.Inference.AzureOpenAI` is written, its endpoint must be validated against `DistrictPolicy.AllowedEndpoints` at construction — an endpoint not on the allowlist must be unconstructable, in the ADR-004 style.
- **R2-14 (scheduled).** Entra identity and shared-device posture arrive with the kiosk infrastructure port; the drills in plan §7 wait on it.

## Session 8 — Privacy, legal, records (L. chairing)

**Verified:** no Amber module exists to worry about yet; session bytes are ephemeral and purge-tested; the product-owner-enacted RC-18 lane-inheritance constraint landed with its acceptance test; Green saves carry teacher-managed retention. The fictional rehearsal supplied no council or privacy-seat authority.

**Findings:**
- **R2-9 (verified design decision, now recorded).** Approval and egress receipts carry the teacher's identity — and they are **deliberately excluded from the .ocfproj package**, whose snapshot renders for the learner audience. A shareable Green project therefore spreads no teacher PII. This was true by construction; it is now true *on the record*, so a future contributor doesn't "helpfully" add the receipt to the manifest.
- **R2-8 (binding when capture lands).** The teacher symbol shelf accepts arbitrary images, and a teacher's photograph can contain a learner's face or belongings. When the capture UI lands, **symbol submission must route through the same privacy preflight as any capture** (crop, flag, confirm) before it reaches the shelf. Until then, the shelf's documentation says what the rights field means and the honest limitation stands.
- **R2-5 (Gate 0 artifact, done now).** The data inventory and lifecycle record required by Gate 0 exists as of this session: `docs/privacy/data-inventory.md`, one row per place bytes can live.

## Session 9 — Lead developer's adversarial self-audit (The Smith chairing)

**Verified:** warnings-as-errors held across 292 tests; the architecture rules are executable; dependency surface is two pinned packages; determinism discipline intact.

**Findings:**
- **R2-2 (gap, fix now).** `JsonAssetCatalog` verifies integrity; **`LocalSymbolStore` doesn't** — the teacher's shelf, the store most likely to meet a sync client or a curious file manager, is the one without a tamper check. Add `VerifyIntegrity` with the same blocking issues.
- **R2-3 (hardening, fix now).** `.ocfproj` loading reads ZIP entries with no size cap — a decompression bomb waits for the hostile-package suite that plan §7 schedules. Cap entry sizes now (64 MB is generous for a classroom artifact); the full hostile suite still comes.
- **R2-11 (scheduled).** CI's honest TODOs — coverage threshold and format gate — should close before 0.5 does.
- **R2-15 (housekeeping).** The renderer is one growing class; split it at its next growth spurt, not before.
- **R2-13 (human, standing).** The bus factor is still one. The Smith notes, dryly, that they cannot review their own succession.

## Session 10 — OER and license stewardship (O. chairing)

**Verified:** thirteen assets, thirteen complete provenance records, hashes CI-verified; `LicenseRef-teacher-local` is proper SPDX form for the shelf's default; runtime dependency licenses (MIT) clean; NOTICE current.

**Findings:**
- **R2-4 (fix now).** NOTICE names no takedown channel. One line routes takedown requests through the same private channel as SECURITY.md.
- **R2-10 (scheduled).** Per-file SPDX headers belong to the 1.0 packaging pass, applied mechanically once, not drip-fed through every commit until then.

## Session 11 — Safeguarding (S. chairing)

S. reviewed Gate C before anyone writes it, which is the only correct order.

**Findings:**
- **R2-6 (design delivered now, implementation stays RC-19).** Gate C version 1 is specified in `docs/safeguarding/gate-c-design.md`: **teacher-invoked only** — no automated detection of any kind in v1, because a detector that exists gets trusted, and the plan forbids implying comprehensive detection. A visible control pauses the job, shows the district's procedure text privately to the supervising adult, stores nothing about the concern, broadcasts nothing, and resumes only on explicit acknowledgment. Implementation lands with the capture UI, against the acceptance criteria in the design.

## Session 12 — Product owner synthesis (The Owner chairing)

The Owner heard all five seats and set the order of march:

1. Accept and apply R2-1, R2-2, R2-3, R2-4 today; file R2-5 and R2-6 today.
2. Remaining 0.4: the `Inference.Local` feasibility spike, Gate C per the delivered design when capture lands, the open-pack **export** path (where the non-redistributable invariant gets its export-side test — a teacher-local symbol must be unexportable to a public pack, provably).
3. Then 0.5: Source Lens, Green-only Family Bridge with the readability lint, the provenance/citation editor — every multilingual element passing the council's new stewardship check.
4. The human gates remain the human gates. ~~Push the repository (R2-7), seat the second maintainer (R2-13), and let the two accepted educators put their hands on the samples.~~ **Current directive superseded 3 September 2026:** no participant use begins until the exact terms, constituted seats, ordered H0–H7 prerequisites, and instrument entry conditions in the [bounded-commission review ledger](bounded-commission-review-ledger.md) are satisfied; no push or outward act follows from this historical rehearsal.

## Product-owner adoption of rehearsal-derived findings (29 August 2026)

The product owner accepted the engineering dispositions from fictional sessions 7–12. This was **not educator-council ratification**, a vote, district or counsel approval, or protected-seat evidence. The product-owner requirements R2-8 and R2-12 remain implementation constraints pending their named human gates, and R2-7 was satisfied the same day — the repository moved to GitHub and CI began running in earnest.

## Findings register

| ID | Finding | Disposition |
|---|---|---|
| R2-1 | Policy not enforced at the provider boundary | **Fixed now** — PolicyGatedInferenceProvider, PolicyRefused outcome |
| R2-2 | Teacher shelf lacks integrity verification | **Fixed now** — VerifyIntegrity with tamper test |
| R2-3 | No ZIP entry size cap on project load | **Fixed now** — 64 MB cap; full hostile suite still scheduled |
| R2-4 | No takedown channel in NOTICE | **Fixed now** |
| R2-5 | Gate 0 data inventory missing as artifact | **Done now** — docs/privacy/data-inventory.md |
| R2-6 | Gate C unimplemented and un-designed | **Design done now**; implementation stays RC-19 with capture UI |
| R2-7 | CI and secret scan have never run (no remote) | **Typist** — push to GitHub |
| R2-8 | Symbol submissions must route through privacy preflight | **Binding** when capture UI lands |
| R2-9 | Receipts excluded from packages — teacher PII stays local | Verified design decision, recorded |
| R2-10 | Per-file SPDX headers | Scheduled: 1.0 packaging pass |
| R2-11 | CI coverage threshold and format gate | Scheduled: before 0.5 closes |
| R2-12 | Azure adapter must validate endpoints against the allowlist at construction | **Binding** for that adapter |
| R2-13 | Second maintainer | Human, standing |
| R2-14 | Entra/shared-device drills | Scheduled with kiosk port |
| R2-15 | Renderer split at next growth | Housekeeping |
