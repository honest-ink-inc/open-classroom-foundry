# Human gates — coordination plan

**Date:** 29 August 2026 · **Engine at:** v0.7.0-alpha · **Owner:** typist/project director (proposed Honest Ink, Inc.; incorporation pending), with the educator council and district partners

The repository now has code-owned guardrails across the hardening matrix, but the checklist deliberately keeps exact-release evidence, packaging, immutable-source correspondence, rights-ledger completeness, standalone snapshot visual continuity, managed deployment, and accountable human judgments open in their own rows. This plan coordinates the human-held work; it does not turn neighboring code evidence green by association. The [stage-gate disposition register](../governance/stage-gate-disposition-register.md) is a consolidated working Gate 0–5 inventory; this plan supplies routes and materials, not closure. The constitution's rule holds throughout: **model self-ratings are not release evidence** (plan §14), the two rehearsal council cycles were preparation and never proof, and nothing below can be closed by the software or its generator — only by authorized people doing role-bound work. The [proposed first-cohort operating terms](../council/draft-first-cohort-operating-terms.md) are **DRAFT — NOT ENACTED**; they create no quorum, recommendation, appointment, or protected-seat authority. The proposed consent, withdrawal, credit, compensation, and private-record terms are likewise not operative; the typist must issue and approve one exact corrective proposal and the initial cohort must validly enact that same version before any participant session or collection.

The [bounded-commission review ledger](../council/bounded-commission-review-ledger.md)
now controls the human dependency order: an accountable ADR-010 or superseding
schema/version/migration ratification; enacted terms and separately recorded
participant-choice dispositions; needs-first council record; AAC/SLP; curriculum; multilingual;
accessibility/AT; rights/OER; physical print; then the Green-only teacher pilot.
Each downstream activity waits for the preceding participant-reviewed record to
be frozen with its exact version and digest. No row has begun.

## The gates, their owners, and their evidence

| # | Gate | Owner | Prerequisite | Evidence artifact (lands in repo) | Materials |
|---|---|---|---|---|---|
| 1 | H7 six-week staff pilot: think-alouds, time-to-artifact, edit burden | Constituted council educator oversight + separately consented educator participants + typist as facilitator | Procedurally valid H0–H6 records frozen in order; all applicable holds dispositioned; H7 participant choices, withdrawal acknowledgement, role authentication, and private-record terms satisfied | De-identified session notes + measures table → `docs/evidence/pilot/` | [Think-aloud protocol](think-aloud-protocol.md) |
| 2 | H4 NVDA/Narrator + keyboard walkthrough | Typist private preparation pass, then the constituted AT reviewer seat | A typist-only dry run may prepare defects; accountable human review waits for frozen H0–H3, the exact build and instrument, constituted-seat evidence, participant choices, withdrawal acknowledgement, and private-record terms | De-identified completed script with pass/defect per step → `docs/evidence/pilot/`; any signed original stays private | [Walkthrough script](../accessibility/nvda-walkthrough-script.md) |
| 3 | H6 physical print inspection | Constituted print/educator reviewer + typist operating the instrument | Procedurally valid H0–H5 records frozen in order; exact printable bytes and instrument fixed; participant choices, withdrawal acknowledgement, and private-record terms satisfied; real printer and metric ruler available | De-identified completed checklist + measured values → `docs/evidence/pilot/`; signed original retained privately | [Print inspection checklist](print-inspection-checklist.md) |
| 4 | H7 seeded-error study (**gates the classroom pilot itself** — plan §14 risk table) | ≥3 separately consented educator participants under constituted council oversight, facilitated | Procedurally valid H0–H6 records frozen in order; every predecessor hold applicable to the exact H7 build, packets, and instrument explicitly closed by its accountable authority; participant choices, withdrawal acknowledgement, role authentication, and private-record terms satisfied; packets printed; key sealed with facilitator | Aggregate non-keyed measures + threshold verdict → `docs/evidence/pilot/`; packet matrix and key stay private | [Study protocol](seeded-error-study.md) + [pilot kit](../evidence/pilot-kit/) |
| 5 | Gate 3 district readiness + **written approval before any real Amber artifact** | District IT, privacy, safeguarding seats | Packet delivered; entity contact channels live | Signed instrument (retained by both parties; fact-of-signature recorded) | [Readiness packet](../district/gate-3-readiness-packet.md) |
| 6 | Second maintainer | Typist recruiting | None — begins now | Merged PR reviewed by them; name in GOVERNANCE | §Second maintainer below |
| 7 | Authenticode signing | Honest Ink, Inc. after incorporation (organization certificate) or district IT | Entity track below (acceptance → EIN → OV certificate), or district cert | Signed release + timestamped signature verified | The unsigned framework-dependent `win-x64` output to be produced by `tools/publish.ps1` after authorization; signing is a separate authorized act |
| 8 | Counsel confirmation of the Honest Ink name | Typist + counsel | Entity exists (strengthens the claim) | Counsel letter noted in `docs/trademark-screening.md` | Screening memo (exists) |
| 9 | Local inference weights | District/teacher provisioning | Hardware + license-clean weights | Capability-kit run recorded against the real provider | Spike + capability kit (exist) |

No gate's owner can waive another gate (GOVERNANCE). A person holding multiple capacities counts once and must record each capacity; one presence does not become multiple independent reviews. Vacancy, absence, silence, abstention, recusal, elapsed time, or a green machine check never supplies protected-seat assent. Rows 1 and 4 are H7 activities, row 2's accountable review is H4, and row 3 is H6; they run only at their ordered ledger positions, not as a parallel bundle called a staff pilot. Row 5 is an independent district route after its own authority and prerequisites; rows 6–9 remain separately held by their named owners.

## Historical six-week work breakdown — not a current schedule

**The opening date moved (29 Aug 2026).** This preserved work breakdown mixed
protected reviews into pilot weeks and therefore is not executable under the
bounded commission's ordered review. H0–H6 in the review ledger must freeze
first; accepted participants then revise and date the H7 pilot schedule. Do not
substitute dates into this historical table or use it to invite participants.

The pilot occupies Gate 4's first two rungs only: **synthetic/teacher-authored verification and the staff-only Green pilot.** No classroom output, no student presence, no Amber material of any kind. The Amber pilot is not in this window and cannot be until the written instrument of gate 5 is signed.

| Week | When | Work | Needs |
|---|---|---|---|
| Prep | The 4–5 working days before week 1 | Have the typist issue and approve one exact corrective proposal for consent, withdrawal-right, credit, compensation, contribution, and private-record terms; have the initial cohort validly enact that same version with its operating terms; reverify payment-law prerequisites with counsel/accountant and establish the authorized disbursement route and private compensation ledger; only then record each participant's separate applicable choices, exact elected rate, per-natural-person quarter-to-date commitments across every covered role, remaining cap, district-time/no-double-payment attestation, and acknowledgement of the withdrawal right and route; have the typist reconcile the update-letter draft before any typist-controlled send; confirm schedule with constituted seats; print the pilot kit; deliver the district packet; typist self-runs the NVDA script once | No participant session or collection before the exact correction is validly enacted, lawful-payment route is operational, full elected rate fits that natural person's remaining cap or a zero/non-retroactive decline is recorded, the district-time disposition is recorded, applicable choices are recorded, and the withdrawal route is acknowledged; SPF/DKIM check before any send; stale compensation, credit, or withdrawal wording blocks correspondence; no member-authored material enters the repository while the content license and separate assent are pending |
| 1 | Opening week | Deterministic Press time-to-artifact think-alouds (2 sessions) — the recipes' stated budgets are on trial (constitution item 14) | 2 educator seats |
| 2 | Week 1 + 1 | SequenceSlate think-alouds **only if the AAC/SLP seat has been constituted under enacted terms** (governance requires the seat before co-design); otherwise continue Press + Board-to-Brief; AT reviewer walkthrough | Constituted AAC seat record exists |
| 3 | Week 1 + 2 | Physical print inspection incl. booklet imposition; bilingual fixture review with the multilingual seat (EN–ES, EN–AR right-to-left) | Printer, ruler, multilingual seat |
| 4 | Week 1 + 3 | Seeded-error study, paper pass (packets A–H) | ≥3 educators, sealed key |
| 5 | Week 1 + 4 | Seeded-error study, in-app Gate B pass; defect burn-down begins | Builds on participants' machines |
| 6 | Week 1 + 5 | Burn-down completes; council evidence-review session; 1.0-rc evidence bundle assembled. It may be called a council recommendation only if exact operating terms were first enacted and every applicable protected-seat review is separately recorded; participant review may freeze the meeting record but does not ratify an ADR or release | Enacted operating terms; matter-specific quorum after recusals; applicable protected seats present and reviewing the exact evidence |

Crash-free session measurement (≥99.5%, plan §17) accumulates across all six weeks from the pilot machines' local, content-free diagnostics.

## Binding repository boundary for every gate

- **Never student work, never students.** Synthetic and staged materials only; the CONTRIBUTING.md absolute rule applies to pilot sessions.

## Proposed session hygiene — not ratified and not operative

The terms below are drafting inputs for H0. They create no consent, appointment,
quorum, custody, recording, credit, contribution, or participation authority.
They become operative only after the typist issues and approves one exact
corrective proposal, the initial cohort validly enacts that same version in the
bootstrap record, and each participant separately records an accepted,
declined, or not-applicable disposition for every applicable choice before
collection. Participation, note collection, and any proposed public record
remain independently subject to their affirmative session prerequisites; an
optional recording, within-cohort disclosure, credit, compensation,
contribution, or role choice may be
declined without being silently converted into a participation bar.

- **Notes, not recordings**, unless a participant separately asks for and consents to more; findings recorded de-identified.
- Participants are advisors, not test subjects: the artifact under trial is always the software.
- Participation consent, private/de-identified note-collection consent, public-record publication consent, recording consent, within-cohort identity/affiliation disclosure, public credit, compensation election, content contribution, maintainer appointment, and copyright stewardship are separate choices. Agreement to one never supplies another. The withdrawal right and route are explained and acknowledged separately; withdrawal is a right, not an opt-in choice.
- Before a group session, every participant privately chooses the exact identity and affiliation information, if any, that may be disclosed to the named cohort recipients. Contact details are not shared by default, and every recipient first acknowledges the exact session-confidentiality and no-outside-contact boundary. A decline is honored through a seat-only or pseudonymous format where workable, or an offered one-to-one consultation outside H0–H7 that supplies no quorum or dependency. Public-credit consent never supplies within-cohort disclosure.
- Evidence lands in the repo de-identified; names appear only in future release-note credits or current editable acknowledgments after a separate affirmative choice.
- Participant identity, contact, participation consent, note-collection consent, public-record publication consent, recording consent, within-cohort disclosure scope, recipient acknowledgement, withdrawal, credit preference, and payment records are private records retained outside the public repository. Before collection, the typist states the private custodian, minimum fields, access boundary, retention rule, and correction/withdrawal route. A de-identified evidence artifact may record that the required consent was obtained, but must not carry the identity record itself.
- A participant may end future participation and future collection at any time. Before a proposed public record is committed or published, participant review is the point to remove or correct their material. Once a record enters public Git history or is distributed, erasure from clones and prior artifacts cannot be promised; the current record can carry an appended correction, withdrawal marker, or prospective credit change.
- Pilot consent is not contributor or maintainer consent. While the documentation/original-printable content license remains unchosen, member-authored fixtures, exact quotations, translations, illustrations, and other materials stay outside the repository and distribution artifacts. A later code or document contribution requires the author's separate assent to the then-applicable contribution terms. Naming someone as a maintainer, granting emergency access, or publishing maintainer credit likewise requires explicit acceptance of that role and preferred public identity.
- Conflicts and recusals are recorded by category, de-identified by default. A recused participant supplies neither matter-specific quorum nor a protected-seat finding. If quorum or the applicable seat is lost, the matter is held.

## Organizational track — Honest Ink, Inc.

As of 30 August 2026: **articles for the proposed Honest Ink, Inc. have been filed in Maryland; acceptance is pending**; **honest-ink.org** is purchased but is not configured as a custom domain; a historical deployment to the repository's default GitHub Pages address exists. That measured history does not authorize a future publication or domain change: each remains a separate typist act. Mailboxes live — director `honest.ink.edu@gmail.com` (internal), `contact@honest-ink.org` (public-facing: council invitations, district correspondence, security/takedown channel — SECURITY.md now points there). **IRS documentation is not yet filed.**

Sequence, in dependency order (items marked *counsel/accountant* are their calls, not this document's — this is coordination, not legal advice):

1. Maryland acceptance of the articles — awaiting the state. *While waiting:* verify the articles carry the 501(c)(3) purpose and dissolution clauses Form 1023-EZ attests to — amending before approval is cheap, after is not (*counsel confirms*).
2. **EIN** (IRS Form SS-4, drafted 29 Aug 2026; typist's decision to file by mail or fax) — file **after** SDAT accepts, so line 1 matches the approved legal name exactly. Participant sessions remain held until counsel/accountant reverify the lawful-payment prerequisites and an authorized disbursement route and private ledger are operational; an EIN alone proves none of those conditions. Then 1023-EZ, which cannot precede the EIN.
3. Bank account in the entity's name (needs EIN).
4. *Counsel/accountant:* the tax-exemption path (whether and which 501(c)(3) filing) and whether Honest Ink, Inc. becomes the **copyright steward** for contributions. Code and first-party recipes remain GPL-3.0-or-later; the separate documentation/original-printable content license is still pending. Stewardship, content-license selection, and each contributor's assent are distinct decisions and should not be collapsed.
5. **OV code-signing certificate in the entity's name** (needs completed incorporation, EIN, and verifiable presence — the domain helps). This gives gate 7 a second path that does not wait on the district's certificate.
6. Counsel confirmation of the name (gate 8) — the Maryland filing and the domain strengthen the claim recorded in the screening memo.
7. Domain hygiene now that invitations flow from it: SPF, DKIM, and DMARC on honest-ink.org so council and district mail is not quarantined.
8. Website state and future acts — retain the historical GitHub Pages deployment record and the measured absence of a custom domain. Republishing, changing the Pages configuration, or pointing `honest-ink.org` remains a fresh typist act after the applicable counsel and pre-publication checks; the existence of an earlier deployment is not standing permission.

## Second maintainer

The bus factor's only cure, and recruitment starts before the pilot ends. The honest pitch: a GPL codebase with a seven-suite automated test gate, warnings-as-errors, CI gates, and a written constitution — a well-lit house, not a rescue. Channels: the council's own networks (an educator-adjacent developer is ideal), the district's IT staff (post-Gate-3 rapport), and .NET OSS communities. Pilot participation or pilot consent cannot silently recruit a maintainer. The gate closes only when a second person has contributed under the repository's contribution terms, explicitly accepted the maintainer role and emergency-access responsibility, stated how they may be named publicly, merged a change they reviewed, and holds that access; the sustainability DoD then records the accepted public identity in GOVERNANCE.

## What "done" looks like

“Done” is conjunctive, not representative. Gates 1–4 must close with thresholds met (≥95% moderated keyboard completion, ≥99.5% crash-free, seeded-error detection at the study's bar, budgets met or recipes redesigned), and the evidence bundle then goes to the council for review. No participant review begins before the exact operating terms and every applicable participant prerequisite are satisfied. With those predicates satisfied, the review becomes a council recommendation only with matter-specific quorum after recusals, participant-reviewed records, and separate evidence from every applicable protected seat; if only matter-specific quorum is missing, it remains an unranked consultation outside H0–H7. Participant review may freeze the exact meeting record; it does not ratify an ADR, schema, release, or publication. For the proposed release, **every applicable row** in the release traceability matrix and hardening checklist must carry its required exact-release evidence, every applicable stop-ship veto must be closed, and the council must compare the human findings with the corresponding machine evidence across every applicable evidence family, recording agreements, disagreements, and dispositions. One favorable human–machine comparison, one green neighboring row, or an aggregate score cannot stand in for the rest. Gate 5's instrument then governs the classroom-output rung, and 1.0 is tagged only after all of those conditions and every protected-seat, district, and typist hold are satisfied. If a threshold or comparison fails, the release waits and the defect burns down; the version number is patient.
