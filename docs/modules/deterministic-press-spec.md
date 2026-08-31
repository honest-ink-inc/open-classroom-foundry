# Honest Ink — Deterministic Press module specification

**Module:** Deterministic Press (Module Zero; atlas Studio XXI, entries 203–210)
**Specification version:** 1.0
**Date:** 29 August 2026
**Status:** Draft for educator-council and product-owner review
**Companion to:** Implementation plan 2.0, section 10.0
**Lane:** Structurally Green only — no lane above Green is expressible in this module
**License intention:** Code GPL-3.0-or-later; original printable templates under the project's declared free-culture content license

This module inherits the shared pedagogical constitution, data-lane contract, accessibility contract, and universal Definition of Done of implementation plan 2.0. Where a gate below is described as vacuous, that is a structural property proven by tests, not a waiver.

---

## 1. Purpose and position

The Deterministic Press is the Foundry's zero-inference printable studio: parameterized, print-perfect classroom classics produced from good geometry and honest ink, with no model, no capture, no OCR, and no network egress anywhere in the module.

It is Module Zero for five reasons:

1. **Zero privacy risk.** There is no source material — inputs are parameters and teacher-typed lists. Gate A (privacy preflight) and Gate C (safety review) have nothing to inspect, and tests prove it structurally.
2. **Zero district friction.** No AI policy, no Azure contract, no attestation. Any teacher, any school, any country, from the first release.
3. **Total pipeline exercise.** Projects, the semantic ArtifactDocument, renderers, print, export, bilingual labels, accessibility, provenance, and the ApprovedArtifact boundary all get built and hardened on the safest possible cargo.
4. **Immediate daily value.** Teachers currently buy graph paper, ten-frames, handwriting sheets, and flashcard stock with their own money.
5. **Trust before power.** The first thing a district or teacher evaluates is the thing that cannot hurt anyone. The Foundry earns its later lanes by being flawless in its paper lane.

**Standalone worthiness is a requirement:** if the program stopped after Module Zero, the studio must still constitute a genuinely useful GPL gift.

## 2. The governing invariant

> **Presses take parameters, never prose.**

A press whose input wants to be sentences, sources, or captured images is a different module in a different studio. This single rule is the module's boundary against scope creep into content generation, and it is enforced by the recipe schema: every Deterministic Press recipe declares only enumerated, typed parameters (dimensions, counts, ranges, list items, toggles, page options).

Teacher-typed list content (word lists, term/answer pairs, label text) is data placed verbatim into the output — never interpreted, corrected, completed, or reordered by anything but the teacher.

## 3. Scope: the eight presses

| # | Press | Makes | MVP |
|---|---|---|---|
| 203 | Blankforms Press | Graph paper, coordinate grids, number lines, ten-frames, clock faces, Cornell notes, lab tables, calendars, music staves, booklet blanks | **Yes** |
| 206 | Flashcard Flywheel | Registration-safe double-sided cards from teacher lists, sort-box labels, self-check formats | **Yes** |
| 208 | Booklet Binder | Saddle-stitch imposition of any approved artifact, with correct signature arithmetic | **Yes** |
| 205 | Manipulative Mint | Fraction strips, algebra tiles, base-ten blocks, tangrams, dice nets, spinners on cardstock layouts | 0.3 |
| 207 | Foldables Foundry | Interactive-notebook foldables, flipbooks, layered organizers with cut/fold guides | 0.3 |
| 209 | Big Print Shop | Multi-page tiling of any approved artifact into wall-scale displays | 0.3 |
| 204 | Handwriting Foundry | Tracing, letter-formation, and practice sheets from any word list | 0.3, Latin script first |
| 210 | Label Lathe | Classroom label series with optional symbols and bilingual pairs | After asset kernel |

The three MVP presses are chosen deliberately: together they exercise **parameterized vector geometry** (Blankforms), **duplex registration plus list handling plus project save** (Flywheel), and **imposition over the ApprovedArtifact boundary** (Binder) — the three rendering capabilities every later module depends on.

## 4. Architecture position

**Package:** `Foundry.Modules.DeterministicPress`

**Uses:** IRecipeRegistry, IRecipeRunner (parameter-only recipes), IArtifactValidator, IApprovalGate, IProjectStore, IRenderer, IExporter, IPrinter, IDiagnosticsSink (content-free, as everywhere).

**Must not reference:** IInferenceProvider, IOcrService, ICaptureSource, IRedactionAssistant, or any Amber machinery. A static architecture test fails the build if the module's dependency graph reaches any of these seams. A network-egress trace during any press operation is a stop-ship defect.

**Geometry engine:** millimeter-exact vector primitives composed into the semantic ArtifactDocument (extended with a `geometry` node family: line, grid, arc, path, registration mark). Layout is deterministic — identical parameters produce byte-identical vector output, which CI compares as a rendering regression.

**Current rendering and language boundary (recorded 30 August 2026):** each catalog definition separately declares neutral English for its built-in furniture; that metadata never guesses the language of teacher-entered content. Builders and composers preserve an exact or null whole-document language. The native vector-PDF path uses standard-14 Courier and admits exactly 218 WinAnsi-encodable Unicode code points. Unsupported characters refuse without substitution and select the Unicode HTML/Edge path. HTML/SVG use installed system fonts; no bundled font or universal script coverage is claimed. The complete boundary and protected remainder are in the [artifact language contract](../localization/artifact-language-contract.md).

**Gates:** Gate A and Gate C are structurally vacuous (proven, not waived). Gate B applies in its lightest form: the teacher reviews parameters beside an exact-scale print preview and approves; render, export, and print still accept only an ApprovedArtifact. Architectural uniformity is the point — Module Zero teaches every teacher the approval rhythm on artifacts where approval is effortless.

## 5. Press specifications

### 5.1 Blankforms Press (MVP)

**Parameters (representative):** form type; grid pitch in millimeters or inches; line weights (major/minor); margin set; page size (Letter/A4) and orientation; header/footer fields (teacher-typed); duplex mirroring; low-ink variant.

**Forms at MVP:** square graph paper; coordinate grids (one or four quadrants, axis labels, tick intervals); number lines (range, interval, blank variants); ten-frames and five-frames; analog clock faces (with or without numerals and hands); Cornell note pages; lab data tables (columns, rows, units row); month and year calendars (configurable week start); single, grand, and multi-staff music staves; folded booklet blanks.

**Press invariants:** dimensional accuracy within ±0.2 mm at 100 percent scale; a printed calibration rule on request so teachers can verify their driver did not scale; explicit warning whenever a driver's fit-to-page would alter dimensions; contrast and low-ink variants for every form.

### 5.2 Flashcard Flywheel (MVP)

**Parameters:** teacher list of term/answer pairs (optional bilingual pairs); card dimensions (including 3×5 and A7 presets); cards per sheet; font and size; duplex flip mode (long edge or short edge); stock template; optional Leitner sort-box label sheet; self-check layout.

**Press invariants:** term-to-answer pairing is never scrambled — a deterministic pairing test covers every layout and flip mode; back-side columns are mirrored correctly for the chosen flip edge; a one-sheet registration test page is offered before large runs; text overflow is flagged to the teacher, never silently truncated or shrunk below the declared minimum size; bilingual pairs follow the engine's semantic bilingual-pair node, including right-to-left correctness.

### 5.3 Booklet Binder (MVP)

**Input:** any ApprovedArtifact from the Foundry, or an imported teacher-authorized PDF (Green, teacher-stated rights; the import is imposed, never parsed or interpreted).

**Parameters:** signature size; blank-page padding placement (explicit, shown to the teacher); optional creep allowance; folio marks; duplex flip mode.

**Press invariants:** page-order correctness proven by deterministic tests for every count from 4 to 64 pages; padding pages are explicit in the preview, never surprising; imposition only — no content reflow, rescale beyond the declared imposition scale, or alteration of the source artifact.

### 5.4 Manipulative Mint (0.3)

Fraction strips, algebra tiles, base-ten blocks, tangrams, dice nets, and spinners with cut-efficient cardstock layouts and assembly guides. **Press invariant above all:** mathematical proportions are exact — a fraction strip labeled one-third is one-third to the module's dimensional tolerance, with labels optional so blank versions preserve the reasoning work.

### 5.5 Foldables Foundry (0.3)

Foldables, flipbooks, and layered organizers from teacher-typed panel text. **Press invariants:** a consistent cut/fold line language (solid cuts, dashed folds) with a printed legend on every sheet; each shipped template is verified by physically assembling a printed copy on the hardware bench and photographing the result as release evidence.

### 5.6 Big Print Shop (0.3)

Tiles any ApprovedArtifact across multiple pages for wall display. **Press invariants:** alignment marks and overlap strips; an assembly map on its own page; the scale factor stated on every tile so a partial reprint matches.

### 5.7 Handwriting Foundry (0.3, Latin script first)

Tracing, letter-formation, and practice sheets from any word list, with guide styles (three-line, box, baseline-only) and dotted-to-faded progressions. **Press invariants:** guide faces are OFL or original with recorded provenance — no "tracing fonts" of unclear rights; dotted and faded variants are generated geometrically from the licensed face, keeping provenance singular; additional scripts ship only with a qualified reviewer for that script's pedagogy, per the plan's language contract.

### 5.8 Label Lathe (after the asset kernel)

Label series, bin cards, and station signs with optional Symbol Commons symbols and bilingual pairs, on sheets matched to common label-stock pitches (described dimensionally, not by trademarked stock names). Depends on the asset/provenance kernel; sequenced accordingly.

## 6. Teacher workflow (common to all presses)

1. Choose a press and form.
2. Set parameters beside a live, exact-scale preview; type any list or label content.
3. Review: parameters, preview at print scale, and — where relevant — overflow and scaling warnings.
4. Approve (Gate B, lightweight; produces the ApprovedArtifact).
5. Print, export, or both.
6. Optionally save as a Green project (with embedded accessible HTML snapshot per plan section 6.5).

**Declared time-to-artifact budget: three minutes per press** (constitution requirement 14). The budget appears in the UI and is measured in pilots.

## 7. Outputs and formats

- Print PDF (vector-first; treated as a paper-production format per the accessibility contract)
- SVG; PNG at a declared DPI
- `.ocfproj` Green project with `snapshot.html`
- Accessible HTML for forms where a digital rendering is meaningful (tables, calendars, note pages); paper-only geometry (graph paper, staves) is exempt from digital-accessibility claims and says so

## 8. Accessibility

The authoring UI meets the full engine accessibility contract: complete keyboard operation, UI Automation exposure with standard controls only, NVDA/Narrator verification, 200 percent zoom, visible focus, and no color-only signals — including in previews, where grid-line weight and pattern, not hue alone, carry meaning.

Output claims stay honest: printed geometry is paper; the module claims high-contrast and low-ink variants, large-format options, and correct structure in its digital exports — nothing more, until evidence exists.

## 9. Acceptance proof

- Measured-geometry fixtures: rendered vectors compared to specification within ±0.2 mm at 100 percent scale, for every form and preset
- Byte-identical rendering for identical parameters across two consecutive CI runs (determinism proof)
- Print regressions: Letter/A4, duplex both flip modes, grayscale, low ink, enlarged formats
- Flywheel pairing and registration proofs across all layouts and flip modes
- Binder page-order proofs for 4–64 pages, padding placements included
- Physical print inspection on the hardware bench, including the minimum-hardware machine and at least two physical printers
- Keyboard-only creation of each MVP artifact, verified with NVDA and Narrator
- Teacher study: at least 80 percent of representative teachers produce a usable artifact within the three-minute budget on first use
- Static architecture test: no reference from `Foundry.Modules.DeterministicPress` to any inference, capture, OCR, or redaction seam
- Network egress trace during press operation shows zero connections
- Every shipped template and guide face carries complete rights metadata; CI hard-fails otherwise

## 10. Deferrals

- Non-Latin handwriting scripts pending a qualified reviewer per script
- Label Lathe until the asset/provenance kernel exists
- Any generated decorative art (engine-wide policy)
- ~~Isometric~~/polar graph variants and music-notation content beyond blank staves (Notation Bench, entry 217, is a separate future module) — *isometric dot paper joined the Press's second wave by handover directive, 29 Aug 2026; polar variants and notation content remain deferred*
- Any press whose input is prose rather than parameters — by definition, forever

## 11. Release placement

- **Releases 0.0–0.1:** Blankforms Press, Flashcard Flywheel, and Booklet Binder, built as the rendering pipeline's real cargo in place of equivalent synthetic fixtures.
- **Release 0.3:** the full studio (Manipulative Mint, Foldables Foundry, Big Print Shop, Handwriting Foundry).
- **After the asset kernel:** Label Lathe.

## 12. Risks and mitigations

| Risk | Mitigation |
|---|---|
| Printer-driver scaling silently destroys dimensional accuracy | Calibration rule page, fit-to-page warnings, stated tolerances, physical bench verification |
| Guide-face or template rights ambiguity | OFL-or-original policy, geometric derivation of variants, CI provenance hard-fail |
| Scope creep into content generation | "Parameters, never prose" enforced by recipe schema and review |
| Module Zero polish delays SequenceSlate | MVP is fixed at three presses; the remaining five wait for 0.3 by design |
| Teachers mistake the lightweight Gate B for the full review rhythm | Identical approval vocabulary and surface across all modules; the rhythm is the lesson |

## 13. Success measures specific to Module Zero

- Median parameter-to-print time per press (target: under the three-minute budget)
- Reprint rate attributable to dimensional or registration error (target: approaching zero after calibration)
- Proportion of pilot teachers who use a press again within two weeks unprompted
- Zero privacy, egress, or inference findings — structural, and verified every release
