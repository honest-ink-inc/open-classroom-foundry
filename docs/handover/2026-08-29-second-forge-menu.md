# The second forge menu — zero-gate work after the first menu's exhaustion

**Date:** 29 August 2026 · **Status:** PROPOSED — drafted by the generator from atlas 2.0 against the repository as it stands (menu one fully struck at `06a745a`); **ratification is the typist's act.** Adopting this menu, striking items from it, or reordering it is done by reply; the first "Proceed with …" against an item adopts that item. Until then this document binds nothing.

Drawn under the [first menu's](2026-08-29-waiting-window.md) standing laws, which carry forward unchanged: presses take parameters, never prose; nothing renders, prints, exports, or persists without a typed `ApprovedArtifact`; lanes escalate only and Amber never persists; model self-ratings are not release evidence; the AAC/SLP seat and the district's written instrument gate what they gate, and neither is waivable from the keyboard.

## I. What the window looks like now

Everything on menu one is built: eleven press recipes, the calibration instrument, the UIA harness, the pseudo-locale, the full format gate, the hardened `.ocfproj` reader, the site artifact, the Grouping Deck. The letters still wait for Sep 1–4; the pilot starts Sep 8; the AT reviewer's hour is week 2; the multilingual seat and print inspection are week 3. The `.ocfproj` **writer** determinism fix is in flight in a separate session — nothing below touches the writer.

Two engineering truths dominate the ranking. First, **the app's main surface is still an empty `Form1`**: every press, the review surface, and the capture surface exist and are tested, but no teacher can reach them — and the pilot's three-minute time-to-artifact budget (constitution 14) is measured through that missing door. Second, **PDF today rides headless Edge**: HTML-derived, byte-nondeterministic, and dependent on a browser the minimum-hardware covenant cannot assume — while the spec (§7) promises *vector-first* Print PDF.

## II. The menu, ranked by value-per-risk in this window

### 1. ~~The Press Room — the main authoring surface~~ — **done 29 Aug 2026**
~~Replace the placeholder `Form1` with the real thing for Module Zero: a recipe list (the eleven manifests are the data), a parameter form per press (typed parameters only — the form IS the parameters-never-prose invariant made visible), an exact-scale preview, the Gate B hand-off into the existing `ReviewForm`, and export/save through the existing gates. Standard controls only; the UIA harness extends to it as it grows (walkthrough steps 4, 5, and 7 become automatable); every string through `UiStrings`; the pseudo-locale must render it clean; the three-minute budget displayed per recipe as the constitution requires. **DoD:** a teacher reaches paper from a cold start by keyboard alone, proven by the harness; budget visible; harness and pseudo tests green. *Large; naturally splits by press.*~~
> **Done 29 Aug 2026:** `Form1` is retired. The module now carries a declarative `PressRoomCatalog` — twenty-six engines described as typed parameters (the invariant made into data; the catalog's own well-formedness is tested, and it caught a defaults-out-of-bounds bug on first contact). `PressRoomForm` generates each press's labeled form, displays the three-minute budget, hands off to the real `ReviewForm` for Gate B, and keeps print view, export, and save-to-library structurally disabled until a typed approval exists. Exact-scale VISUAL preview deliberately deferred: ADR-004's gate means nothing renders pre-approval, and the review surface's element list is the honest pre-approval view — a preview path needs its own ratified design, noted for the council. Harness proof: in-process cold-start-to-approval, refusal-to-status, gate-visibility, budget, names/roles; headed cold-start → select → review → approve → unlock over real UIA; pseudo-locale smoke includes the room. Three harness findings fixed along the way (status labels masking their messages; modal-from-click wedging automation, resolved by deferring the modal one message-loop beat; the legacy UIA client's blindness to newborn windows, bridged via Win32 + FromHandle) — recorded in the traceability document. Big Print Shop joins the room when the project-library picker exists.

### 2. All Aboard MVP wiring — the ratified slice only
Wire the ALREADY-RATIFIED 0.1 flow into the surface: title-and-steps typed entry → the tested builders → `ReviewForm` → print/export/save, with symbol attachment from the shipped CC0 pack through a standard list picker (walkthrough step 6: every symbol announces its name, never "image"). **Boundary, stated plainly:** this wires existing, spec'd, tested capability to a door. No new visual-support interaction pattern, no symbol-set expansion, no co-design decision of any kind — that territory stays sealed for the AAC/SLP seat, and any temptation that arises mid-build is a stop, not a judgment call. **DoD:** the walkthrough's part 2 runs end to end on the real surface; harness steps encoded. *Medium.*

### 3. The vector-first PDF press
A native deterministic PDF writer for the artifact document — vector sheets as true PDF vector operators (the mm-exact geometry deserves better than a rasterized browser print), text in standard-14 fonts, no timestamps, byte-identical output for identical input, asserted like every other render. Frees print and export from the headless-Edge dependency on donated hardware; Edge remains the fallback for rich HTML nodes until parity. Booklet Binder re-imposition over real PDFs becomes possible on top. **DoD:** `RenderTarget.PrintPdf` served natively for vector documents; byte-determinism test; calibration proof page's 100 mm ruler measures true in the PDF. *Medium-large.*

### 4. The computational-thinking studio (atlas #211–#213, #215)
The subject the original atlas under-served, and every entry is zero-gate by construction: **Parsons Press** — a teacher-supplied working solution scrambled into seeded line-ordering puzzles, teacher-authored distractor lines only, same seed same puzzle; **Trace Table Tutor** — variable-trace tables and predict-the-output sheets where the teacher's code is preserved EXACTLY (a machine-assertable invariant, and the whole point); **Unplugged Algorithm Atelier** — sequencing/branching/loop cards from a teacher-typed routine; **Rubber Duck Deck** — self-explanation and debugging-protocol cards. **DoD per engine:** parameterized, validated, mm-asserted, verbatim-preservation asserted where teacher text is the cargo, recipes joined, samples rendered. *Medium; splits into four sub-proceeds.*

### 5. Retrieval Grid Generator (atlas #226)
Spaced-retrieval grids and mixed warm-ups drawn seeded-deterministically from a teacher's own prior-unit question bank — the teacher's questions verbatim, the selection and arrangement seeded, scheduling suggestions as printed teacher notes. Measurement craft with no measurement claims. **DoD:** parameterized, seeded, verbatim-asserted, recipe joined, samples rendered. *Small.*

### 6. Field Journal Forge (atlas #218)
Nature and field-learning kits, pure parameters: observation frames, specimen labels (Label Lathe's kin), weather and phenology log tables, and site-map pages over the existing grid/dot machinery. No model, no reviewer seat, immediate outdoor-education value. **DoD:** the usual press bar. *Small-medium.*

### 7. Manipulative Mint, third strike
Spec §5.4's named remainder: algebra tiles, base-ten blocks (units, rods, flats with true proportions), and tangrams (the classic seven-piece square, exact construction). Proportions as arithmetic, cut-efficient layouts. **DoD:** the usual press bar; proportion assertions exact. *Small-medium.*

### 8. ~~The machine rows of spec §9~~ — **done 29 Aug 2026**
~~Three structural gates the spec already promises: an **egress-freedom architecture test** (no networking API surface reachable from the press module or its dependencies — the "zero connections" claim made structural); a **rights-metadata hard-fail** (CI refuses any shipped asset without a complete provenance record); and a **measured-geometry sweep** (every press's key dimensions asserted within the spec's ±0.2 mm at 100 percent scale, as one parameterized fixture over the whole recipe book). *Small.*~~
> **Done 29 Aug 2026:** egress freedom — the press module, Domain, Contracts, and Application each proven to reference no networking assembly at all (an assembly that cannot name the network cannot call it); rights metadata — every shipped symbol must carry complete provenance, its recorded SHA-256 must match the bytes on disk, redistributability is asserted, and orphan files hard-fail; measured geometry — one parameterized sweep drives all twenty-six catalog entries at their defaults and asserts every primitive within ±0.2 mm of the declared physical page. All three run in CI, so failing IS the hard-fail.

## III. What must NOT be built, carried forward and extended

- All Aboard co-design of any kind — **AAC/SLP seat first**; item 2's boundary is a wall, not a guideline.
- Anything touching real classroom-derived data — **written instrument first.**
- `Foundry.Inference.Local` implementation — the spike and capability kit remain its contract.
- No generator output ever closes a HUMAN row of the hardening checklist.
- **PNG raster export** — deferred until a rasterizer with pinned deterministic bytes is chosen deliberately; a nondeterministic export would poison the byte-identical claim.
- **Handwriting dotted-to-faded glyphs** — an OFL face must be chosen and its provenance recorded by a human first; geometry follows the license, never precedes it.
- **Notation Bench (#217) pedagogy** — the deterministic engraving core may be prepared only after a specialist reviewer for music pedagogy exists; until then even the "easy" rhythm cards wait, because wrong notation teaches wrong.
- **Inference-dependent modules** (Lesson Loom, Board to Brief, and kin) — roadmap-ordered (0.2–0.3), pedagogy-reviewed, and not pull-forward-safe the way zero-inference presses are.
- The `.ocfproj` **writer** — in flight elsewhere; two hands on one file is how packages get corrupted.

Bench stock (worthy, unranked, ratify-in if desired): Portfolio Passport (#224) and My Strategy Shelf (#223) as learner-held paper kits; Print Queue Planner (#175); Print-and-Play Press (#166) beyond what Puzzle Press already covers.

## IV. Resuming

The convention is unchanged: the typist says **"Proceed with …"** and names an item — which also adopts it from this proposed menu. Completed items get struck through with a dated note, never deleted. When this menu exhausts, the next is divined the same way: atlas against window, drafted by the forge, ratified by the hand that owns the gates.
