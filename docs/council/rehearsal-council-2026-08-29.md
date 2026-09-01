# Rehearsal educator council — six working sessions

**Date:** 29 August 2026 · **Convened by:** Da Vinci, at the product owner's direction
**Standing — read first:** These are *rehearsal* sessions. Every participant below is a persona constructed by the generator as a stand-in for a council seat; none is one of the seven invited educators, and no finding here substitutes for the human council's release-gated evidence (think-alouds, assistive-technology walkthroughs, seeded-error studies, physical print inspection). The plan's rule stands: **model self-ratings are not release evidence.** The rehearsal's purpose is humbler and useful — to arrive at the human sessions with the obvious defects already found, argued, and fixed.

**Personas (fictional, role-titled):** V. — AAC specialist/SLP · A. — multilingual services & family liaison · K. — accessibility/AT reviewer · M. — curriculum reviewer (math), with literacy and science hats · P. — privacy/records & OER steward · T. — classroom teacher, pilot council chair. Da Vinci records and does not vote.

**Materials before the council:** the 0.1-alpha evidence bundle and samples; the seven-symbol CC0 pack; the All Aboard, Board to Brief, Directions Duet, Scaffold Smith, Lesson Loom, and Talk Moves builders and their invariants; the eight presses; the review surface presenter; the governing documents.

---

## Session 1 — Visual supports and AAC (V. chairing)

V. opened with the samples printed and scissors on the table, which is where visual supports are judged.

**Verified:** the agency set exists as first-class content; no PECS language anywhere; symbols wordless with meaning held as metadata; the "a symbol is proposed representation, not universal meaning" invariant lives in the ambiguity notes. V. called the ambiguity registry "the most professionally honest thing I've seen a tool do."

**Findings:**
- **RC-1 (defect, fix now).** `AgencyCards` prints the catalog's *ambiguity notes as the learner card body*. Ambiguity notes are curator-to-teacher craft — "may read as a wheel or target" must never sit under a child's help card. Move them to teacher-only notices.
- **RC-2 (refinement, fix now).** The learner-facing card label is the catalog's English `IntendedMeaning` with no override. A bilingual classroom needs "Alto," not "Stop." Add per-card label overrides; the catalog meaning stays the default.
- **RC-3 (design, schedule 0.4 — top priority).** In a task strip with symbols, the images are emitted as a block before the steps: **symbol-step adjacency is lost**, and adjacency is the entire point of a visual task strip. The document model needs a semantic step-row (step text + its symbol as one unit) that the renderer lays out together. Until then the limitation is documented, not denied.
- **RC-4 (pack v2, schedule).** The set lacks **yes** and **no** — foundational in practice — plus *more*, *do-not-know*, and *consent* from the atlas's own Agency Deck list. The life-ring "help" is metaphorical; commission an open-hand variant and keep both. The clock-slash "not-now" needs its teaching note honored in the teacher guide.
- **RC-5 (verified, no change).** First/Then and Now/Next/Done labels are already parameters — translatable. Good.

## Session 2 — Multilingual learners and family partnership (A. chairing)

**Verified:** locked facts required verbatim in *both* languages is exactly right — "the date is the same in every language, and when it isn't, someone's child misses the trip." Glossary versions stamped on artifacts; one-to-one step alignment enforced; RTL rendering tested.

**Findings:**
- **RC-6 (accuracy defect, fix now).** The Duet status notice says **"machine-drafted"** even when the teacher typed every translation themselves — a false claim in a tool named for honesty. The truthful statement is about *review status only*: "drafted — not yet language-reviewed by a qualified reviewer."
- **RC-7 (defect, fix now).** Glossary matching is case-sensitive: "Folder" at sentence start escapes the "folder → carpeta" rule. Match case-insensitively on both sides; determinism is unharmed.
- **RC-8 (option, schedule 0.4).** Pairs always render source-first. A classroom whose room language is Arabic may want target-first on the learner page. Make pair order a teacher choice.
- **RC-9 (advisory for 0.5).** Family Bridge should measure its plain-language claim deterministically (sentence length, syllable proxies) and lint for more than one requested action — already in the plan's refinements; the fictional rehearsal reinforced both.

## Session 3 — Accessibility and assistive technology (K. chairing)

**Verified:** escaping universal; audience separation tested rather than promised; bilingual `lang`/`dir` semantics; millimeter-true SVG with accessible sheet descriptions; the review surface built as presenter-plus-standard-controls exactly as ADR-002 demands.

**Findings:**
- **RC-10 (defect, fix now).** A document whose language is Arabic renders `<html lang="ar">` with **no `dir="rtl"`** — the page direction is wrong for the whole document even though each pair isolates correctly. Set `dir="rtl"` from the document language.
- **RC-11 (structure defect, fix now).** `Card` titles render as `<h3>` regardless of context, producing heading jumps (h1 → h3) that wreck the screen-reader outline. A card is a physical object, not a section: render its title as a bold paragraph, not a heading.
- **RC-12 (needs human, standing).** The NVDA/Narrator walkthrough of the review surface cannot be simulated and is the first human gate. K.'s advice for that session: script it around the uncertain-token keyboard grammar, which is where blind and low-vision teachers will live.
- **RC-13 (verified, no change).** SVG text labels are covered by the figure's accessible description for print artifacts; acceptable for paper-first outputs.

## Session 4 — Curriculum: mathematics, literacy, science (M. chairing)

**Verified:** fraction-strip proportions proven as arithmetic ("finally, a tool that can't print a wrong third"); Lesson Loom's timing law and check-response law; Scaffold Smith's fade criteria; the imposition proof.

**Findings:**
- **RC-14 (refinement, fix now).** Fraction strips always print labels. The *unlabeled* variant is where the reasoning lives — students name the parts. Add a `labeled` parameter, default on.
- **RC-15 (schedule).** Number lines: fractions and decimals between the ticks; clock faces: minute ticks. Worthy, not urgent.
- **RC-16 (verified with advisory).** Transitions-as-phases is carried by the recipe warning; the human think-alouds should watch whether teachers actually enter transition phases, and if not, the builder should ask for them.
- **RC-17 (verified, no change).** The hint ladder's "cut apart, take one at a time" heading resolves the printed-all-at-once tension honestly for paper.

## Session 5 — Privacy, records, rights, safeguarding (P. chairing)

**Verified:** provenance-or-nothing enforced at build and at save; the CC0 dedication clean; the canary suite; policy failing closed to offline; the Amber refusals.

**Findings:**
- **RC-18 (product-owner-enacted engineering constraint for 0.4, surfaced by the fictional rehearsal).** Builders currently receive their lane from the caller; the tests pass Green by hand. When the capture-to-brief session flow is wired, **the draft's lane must be computed as `LanePolicy.Inherit` over every source envelope in the flow** — a photographed board with a student's name on it is Amber, and its brief must be born Amber. The rehearsal records the requirement; its fictional personas did not grant council, privacy, or records authority. The acceptance test proves that a hand-passed Green lane cannot underride inherited Amber.
- **RC-19 (schedule with capture UI).** Gate C (direct-source adult safety review) exists as architecture and prose but not yet as code; it lands with the capture workflow, before any real classroom photograph.
- **RC-20 (advisory).** For decision 6: keep CC0 for geometric basics; adopt CC BY-SA when the design system grows expressive original art worth copyleft. Counsel confirms at pre-release.

## Session 6 — Teacher usability and synthesis (T. chairing, all present)

**Verified:** the samples "look like things I would actually cut out"; the press studio "is the first thing I'd install for the graph paper alone" — which was the Deterministic Press thesis.

**Findings:**
- **RC-21 (refinement, fix now).** The flashcard overflow threshold of 60 characters is generous past honesty: at 6 mm type in a ~96×63 mm cell, wrapping fails well before 60. Lower the flag to 40 characters; the text is still never truncated.
- **RC-22 (needs human, standing).** Time-to-artifact budgets are declared and unmeasured. The two accepted educators can begin think-alouds against the existing samples *now* — the rehearsal council's strongest recommendation to the product owner.
- **RC-23 (advisory).** When the human council convenes, hand each member this register and let them overturn any rehearsal disposition; the rehearsal is scaffolding and should fade like one.

---

## Findings register

| ID | Finding | Severity | Disposition |
|---|---|---|---|
| RC-1 | Ambiguity notes printed as learner card body | Defect | **Fixed now** — moved to teacher-only notices |
| RC-2 | Agency card labels not overridable/translatable | Refinement | **Fixed now** — per-card label overrides |
| RC-6 | "Machine-drafted" claim false for teacher-typed translations | Accuracy defect | **Fixed now** — status speaks only to review |
| RC-7 | Glossary matching case-sensitive | Defect | **Fixed now** — case-insensitive both sides |
| RC-10 | No `dir="rtl"` on RTL documents | Defect | **Fixed now** |
| RC-11 | Card titles as h3 break heading outline | Defect | **Fixed now** — bold paragraph, not heading |
| RC-14 | No unlabeled fraction strips | Refinement | **Fixed now** — `labeled` parameter |
| RC-21 | Flashcard overflow flag too generous | Refinement | **Fixed now** — 60 → 40 characters |
| RC-3 | Symbol-step adjacency lost in task strips | Design | **Scheduled: 0.4, top priority** — semantic step-row node |
| RC-4 | Pack v2: yes/no/more/do-not-know/consent; open-hand help | Content | Scheduled: pack v2 |
| RC-8 | Pair-order choice for RTL classrooms | Option | Scheduled: 0.4 |
| RC-15 | Fractional number lines; clock minute ticks | Enhancement | Scheduled |
| RC-18 | Draft lane must inherit from source envelopes in flow | **Engineering constraint** | Product-owner-enacted for 0.4 with an acceptance test; not council or privacy-seat approval |
| RC-19 | Gate C implementation | Safety | Scheduled: with capture UI, before real photographs |
| RC-9, RC-20 | Family Bridge readability lint; symbol-license posture | Advisory | Carried into 0.5 and decision 6 |
| RC-12, RC-22, RC-23 | NVDA walkthrough; think-alouds on samples; human council overturns rehearsal | **Human** | Standing — the release gates |

## Product-owner adoption of rehearsal-derived findings (29 August 2026)

The product owner accepted the following two rehearsal-derived directions for implementation. This was **not educator-council ratification**, a vote, protected-seat evidence, or authority for the fictional personas above:

1. **Symbol growth and teacher authorship.** Many more symbols must be encoded for All Aboard over time — and, more fundamentally, teachers must be empowered to create and add their own symbols to meet the individual needs of those they work with. *Enacted in Release 0.4:* the `LocalSymbolStore` gives every teacher a provenance-first symbol shelf (meaning, alt text, and teacher-stated rights required at submission; local-only by default, redistributable solely under an explicitly declared open license), resolved alongside the libre pack through one composite catalog. Pack v2 carries the product-owner-adopted rehearsal concepts: yes, no, more, do-not-know, consent, and the open-hand help variant. Their presence is not AAC/SLP approval.
2. **Multilingual stewardship.** The product owner recorded a proposal that a real council coordinate future multilingual audits and checks. Glossary governance, language-pair reviewer assignments (plan §20 decision 5), translation-status auditing, and RTL/script verification remain with the named human seats until those people enact an actual process; this rehearsal assigned none of that authority.

## Closing word

The rehearsal found eight things worth fixing today, one engineering constraint for the next release, and nothing rotten in the foundations — the gates, lanes, and invariants held under six adversarial readings. What it cannot find is what only real hands can: whether a four-year-old reaches for the life-ring, whether a teacher trusts the review screen at 3:40 p.m., whether the strips survive lamination. That evidence belongs to the seven who were invited. This rehearsal was built to be dismantled by them.
