# Human gates — coordination plan

**Date:** 29 August 2026 · **Engine at:** v0.7.0-alpha · **Owner:** typist (director, Honest Ink, Inc.), with the educator council and district partners

Every machine row of the [hardening checklist](../release/hardening-checklist.md) is closed. What remains between 0.7.0-alpha and 1.0 is human by design, and this plan coordinates it. The constitution's rule holds throughout: **model self-ratings are not release evidence** (plan §14), the two rehearsal council cycles were preparation and never proof, and nothing below can be closed by the software or its generator — only by named people doing named work.

## The gates, their owners, and their evidence

| # | Gate | Owner | Prerequisite | Evidence artifact (lands in repo) | Materials |
|---|---|---|---|---|---|
| 1 | Six-week staff pilot: think-alouds, time-to-artifact, edit burden | Council educator seats + typist as facilitator | Compensation policy stated; ≥2 acceptances (have) | De-identified session notes + measures table → `docs/evidence/pilot/` | [Think-aloud protocol](think-aloud-protocol.md) |
| 2 | NVDA/Narrator + keyboard walkthrough | Typist first pass, then the AT reviewer seat | Build installed on a machine with NVDA | Completed script with pass/defect per step → `docs/evidence/pilot/` | [Walkthrough script](../accessibility/nvda-walkthrough-script.md) |
| 3 | Physical print inspection | Any educator seat + typist | A real printer and a metric ruler | Signed checklist + measured values → `docs/evidence/pilot/` | [Print inspection checklist](print-inspection-checklist.md) |
| 4 | Seeded-error study (**gates the classroom pilot itself** — plan §14 risk table) | ≥3 educator seats, facilitated | Packets printed; key sealed with facilitator | Detection matrix + threshold verdict → `docs/evidence/pilot/` | [Study protocol](seeded-error-study.md) + [pilot kit](../evidence/pilot-kit/) |
| 5 | Gate 3 district readiness + **written approval before any real Amber artifact** | District IT, privacy, safeguarding seats | Packet delivered; entity contact channels live | Signed instrument (retained by both parties; fact-of-signature recorded) | [Readiness packet](../district/gate-3-readiness-packet.md) |
| 6 | Second maintainer | Typist recruiting | None — begins now | Merged PR reviewed by them; name in GOVERNANCE | §Second maintainer below |
| 7 | Authenticode signing | Honest Ink, Inc. (org certificate) or district IT | Entity track below (EIN → OV certificate), or district cert | Signed release + timestamped signature verified | `tools/publish.ps1` output |
| 8 | Counsel confirmation of the Honest Ink name | Typist + counsel | Entity exists (strengthens the claim) | Counsel letter noted in `docs/trademark-screening.md` | Screening memo (exists) |
| 9 | Local inference weights | District/teacher provisioning | Hardware + license-clean weights | Capability-kit run recorded against the real provider | Spike + capability kit (exist) |

No gate's owner can waive another gate (GOVERNANCE). Gates 1–4 constitute the staff pilot; gate 5 runs in parallel; gates 6–9 are unscheduled but open now.

## Calendar — six-week staff pilot, opening date pending

**The opening date moved (29 Aug 2026).** It is downstream of Maryland's acceptance of the articles and is deliberately not invented here — see [the moved calendar](../handover/2026-08-29-the-moved-calendar.md). The six-week *structure* below is unchanged and is written relative to the opening week; only the anchor is missing. Substitute real dates once SDAT answers and the accepted seats confirm.

The pilot occupies Gate 4's first two rungs only: **synthetic/teacher-authored verification and the staff-only Green pilot.** No classroom output, no student presence, no Amber material of any kind. The Amber pilot is not in this window and cannot be until the written instrument of gate 5 is signed.

| Week | When | Work | Needs |
|---|---|---|---|
| Prep | The 4–5 working days before week 1 | ~~State the compensation policy in writing~~ **ratified 29 Aug**; send the update letters; confirm schedule with accepted seats; print the pilot kit; deliver the district packet; typist self-runs the NVDA script once | SPF/DKIM check before the first send |
| 1 | Opening week | Deterministic Press time-to-artifact think-alouds (2 sessions) — the recipes' stated budgets are on trial (constitution item 14) | 2 educator seats |
| 2 | Week 1 + 1 | All Aboard think-alouds **if the AAC/SLP seat has accepted** (governance requires it before co-design); otherwise continue Press + Board-to-Brief; AT reviewer walkthrough | AAC seat status known |
| 3 | Week 1 + 2 | Physical print inspection incl. booklet imposition; bilingual fixture review with the multilingual seat (EN–ES, EN–AR right-to-left) | Printer, ruler, multilingual seat |
| 4 | Week 1 + 3 | Seeded-error study, paper pass (packets A–H) | ≥3 educators, sealed key |
| 5 | Week 1 + 4 | Seeded-error study, in-app Gate B pass; defect burn-down begins | Builds on participants' machines |
| 6 | Week 1 + 5 | Burn-down completes; **real council evidence-ratification session** (the first non-rehearsal); 1.0-rc evidence bundle assembled | Quorum of accepted seats |

Crash-free session measurement (≥99.5%, plan §17) accumulates across all six weeks from the pilot machines' local, content-free diagnostics.

## Session hygiene — binding for every gate

- **Never student work, never students.** Synthetic and staged materials only; the CONTRIBUTING.md absolute rule applies to pilot sessions.
- **Notes, not recordings**, unless a participant asks for and consents to more; findings recorded de-identified; participants may withdraw at any time.
- Participants are advisors, not test subjects: the artifact under trial is always the software.
- Evidence lands in the repo de-identified; names appear only in release-note credits, and only by each person's stated preference.

## Organizational track — Honest Ink, Inc.

As of 29 August 2026: **Honest Ink, Inc. has applied for incorporation in Maryland**; **honest-ink.org** is purchased (no site yet); mailboxes live — director `honest.ink.edu@gmail.com` (internal), `contact@honest-ink.org` (public-facing: council invitations, district correspondence, security/takedown channel — SECURITY.md now points there). **IRS documentation is not yet filed.**

Sequence, in dependency order (items marked *counsel/accountant* are their calls, not this document's — this is coordination, not legal advice):

1. Maryland acceptance of the articles — awaiting the state. *While waiting:* verify the articles carry the 501(c)(3) purpose and dissolution clauses Form 1023-EZ attests to — amending before approval is cheap, after is not (*counsel confirms*).
2. **EIN** (IRS Form SS-4, drafted 29 Aug 2026; typist's decision to file by mail or fax) — file **after** SDAT accepts, so line 1 matches the approved legal name exactly; honoraria remain at zero until then by the compensation policy's own phase structure. Then 1023-EZ, which cannot precede the EIN.
3. Bank account in the entity's name (needs EIN).
4. *Counsel/accountant:* the tax-exemption path (whether and which 501(c)(3) filing) and whether Honest Ink, Inc. becomes the **copyright steward** for contributions — the license stays GPL-3.0-or-later regardless; stewardship is a governance decision the typist owns and should not decide alone or in a hurry.
5. **OV code-signing certificate in the entity's name** (needs completed incorporation, EIN, and verifiable presence — the domain helps). This gives gate 7 a second path that does not wait on the district's certificate.
6. Counsel confirmation of the name (gate 8) — the Maryland filing and the domain strengthen the claim recorded in the screening memo.
7. Domain hygiene now that invitations flow from it: SPF, DKIM, and DMARC on honest-ink.org so council and district mail is not quarantined.
8. Website later — the repository's documents are the content waiting for it.

## Second maintainer

The bus factor's only cure, and recruitment starts before the pilot ends. The honest pitch: a GPL codebase with 361 tests, warnings-as-errors, CI gates, and a written constitution — a well-lit house, not a rescue. Channels: the council's own networks (an educator-adjacent developer is ideal), the district's IT staff (post-Gate-3 rapport), and .NET OSS communities. The gate closes when a second person has merged a change they reviewed and holds emergency access; the sustainability DoD wants their name in GOVERNANCE.

## What "done" looks like

When gates 1–4 close with thresholds met (≥95% moderated keyboard completion, ≥99.5% crash-free, seeded-error detection at the study's bar, budgets met or recipes redesigned), the evidence bundle goes to the council for ratification — the real one. Gate 5's instrument then governs the classroom-output rung, and 1.0 is tagged only when the checklist's human rows are as green as its machine rows. If a threshold fails, the release waits and the defect burns down; the version number is patient.
