# Handover — the waiting window

**Date:** 29 August 2026 · **State:** v0.7.0-alpha tagged and pushed; 361 tests green; CI green with whitespace and coverage gates; compensation policy ratified; letters cleared to send; pilot scheduled 8 Sep – 16 Oct. This document assumes nothing about who reads it next; it is the map of what waits, what moves, and what must not move.

## I. The standing laws (read before building anything)

1. **Presses take parameters, never prose** (Deterministic Press governing invariant).
2. Nothing renders, prints, exports, or persists without a typed `ApprovedArtifact` — the gate is structural, not procedural (ADR-004).
3. Lanes escalate only; unknown provenance is Amber; Amber never persists.
4. **Model self-ratings are not release evidence** — the generator's work is machine-verifiable engineering or it is a draft for humans.
5. All Aboard **co-design** waits for the AAC/SLP seat; the district's **written instrument** precedes any real Amber artifact; both by governance, neither waivable from the keyboard.

## II. The typist's watch (calendar-bound, reply-dependent)

| When | Action | Notes |
|---|---|---|
| Sep 1–4 | SPF/DKIM/DMARC check, then **send the three letters** individually from contact@honest-ink.org | Drafts: [council/correspondence/2026-09-invitation-updates.md](../council/correspondence/2026-09-invitation-updates.md) — ready verbatim |
| Sep 1–4 | Print the pilot kit; deliver the [Gate 3 packet](../district/gate-3-readiness-packet.md); self-run the [NVDA script](../accessibility/nvda-walkthrough-script.md) once | Kit: `docs/evidence/pilot-kit/` — key stays sealed |
| As replies arrive | Acceptances → schedule against the [coordination plan](../pilots/human-gates-coordination-plan.md) grid; open the Phase 1 session ledger (private, not in repo). Declines → thank, ask for a name, re-invite the seat | AAC/SLP acceptance unlocks week 2's All Aboard sessions |
| SDAT watch | On acceptance of the articles: retry the **online** EIN assistant first; else fax the **corrected** SS-4 | Corrections from the 29 Aug review: line 1 = "Honest Ink, Inc." exactly; line 6 = "Baltimore County, MD"; line 10 specify = nonprofit educational organization; line 17 filled; signature carries a **title** |
| Meanwhile | Counsel: 501(c)(3) purpose + dissolution clauses present in the articles (1023-EZ attests to them); name confirmation (gate 8) | Amend-while-pending is cheap |
| Ongoing | Second-maintainer conversations (coordination plan §Second maintainer) | The pitch: a well-lit house, not a rescue |

## III. Da Vinci's forge — engineering that no gate blocks, ranked

Each item is startable with a single "Proceed with …". Ordered by value-per-risk in this window; none touches a human gate's territory.

### 1. ~~Calibration & Proof Press~~ *(newly divined — not in the atlas)* — **done 29 Aug 2026**
~~One deterministic page that turns the print-inspection checklist's worst trap into an instrument: a 100 mm horizontal and vertical ruler pair, margin probes at the declared print boundary, duplex registration marks (print twice, flip, hold to light), and a grayscale step ramp. Week 3's inspectors print it first; if the driver scaled the page, the ruler confesses before any artifact is blamed. **DoD:** ruler spans measure true at 100% scale; mm-true SVG asserted like the existing presses; joins `DeterministicPressRecipes`. *Small.*~~
> **Done 29 Aug 2026:** `CalibrationPress.ProofPage` — both 100 mm rulers tick-asserted at exact millimeter multiples, margin frame probes the declared boundary, three duplex ring targets proven mirror-symmetric about the page center, and a six-step ink-density ramp built as hatch density (pure vector; no new domain primitives). Recipe `press.calibration` joined the book; mm-true SVG asserted in `CalibrationRenderingTests`; rendered into the samples run.

### 2. ~~Deterministic Press, second wave~~ *(the atlas's densest zero-gate value)* — **done 29 Aug 2026**
~~The best remaining printable engines, all parameters-never-prose: ten-frames and hundred charts; fraction strips and circles; coordinate grids (quadrant-configurable) and dot/isometric paper; music staff paper; spinner faces and dice/box nets; bingo boards and deterministic word searches **from a teacher-supplied word list** (placement seeded by a teacher-chosen seed — reproducible, never random-at-print). **DoD per press:** parameterized, validated, mm-asserted, rendered into the samples run. *Medium; naturally splits into sub-proceeds.*~~
> **Done 29 Aug 2026:** ten-frames, fraction strips, music staves, and dice nets already stood from earlier waves. Newly forged: hundred charts (labeled and blank), square and isometric dot paper, quadrant-configurable coordinate grids (`GridQuadrants.First`), fraction circles with radii proven to land on the circle, spinner faces with exact equal sectors, box nets in the cut/fold line language, and the seeded Puzzle Press — bingo boards and word searches from a teacher's list, driven by the module's own PRNG so determinism rests on our arithmetic, not the base library's. Recipe `press.puzzles` joined the book (ten recipes stand). Every engine parameterized, validated, mm-asserted (including a test-side finder proving every hidden word findable), and rendered into the samples run — proven byte-identical across two consecutive runs.

### 3. ~~Accessibility test harness (UI Automation)~~ — **done 29 Aug 2026**
~~ADR-002's standing debt: the harness that must exist before any custom control ever ships, and that multiplies the AT reviewer's hour by catching regressions between sessions. Automated UIA pass over the main surface and ReviewForm: every control named, roled, keyboard-reachable; tab-order asserted; the walkthrough script's parts 1–3 encoded as tests where automation honestly can. **DoD:** UiAutomation test project runs headed on the pilot machine and in CI where feasible; findings-to-script traceability. *Medium.*~~
> **Done 29 Aug 2026:** two layers in `tests/UiAutomation` — in-process contract tests (names, roles, tab order, mnemonics, ADR-002's standard-controls rule as a structural assertion) plus headed tests that launch the real app in a deterministic `--uia-harness` fixture mode and drive ReviewForm and CaptureForm through real UI Automation patterns (select, move, edit, approve). Headed tests ran green on this machine and are visibly skipped — never silently passed — where no desktop exists. Traceability: [accessibility/uia-harness-traceability.md](../accessibility/uia-harness-traceability.md), including the four findings the harness produced on first contact (unnamed focusable splitters, lane radios whose accessible names replaced their meaning, an approval that said only "Approve", a draft state invisible to the ear) — all fixed with standard-control properties only. Script steps 3, 13, and 16 and everything speech remain honestly human.

### 4. ~~Localization scaffolding~~ *(the council's multilingual-stewardship directive)* — **done 29 Aug 2026**
~~Extract UI strings to resources; add a pseudo-locale ("ẋẋ") build that lengthens strings 40% and forces RTL, so truncation and mirroring defects surface **before** the multilingual seat's week-3 review; wire the existing `TargetLanguageFirst`/RTL rendering knowledge into the app chrome. **DoD:** zero hard-coded user-facing strings (architecture test), pseudo-locale smoke render. *Medium.*~~
> **Done 29 Aug 2026:** every chrome string extracted to the `UiStrings` catalog; a source-scanning architecture test forbids any user-facing literal outside it (and caught `ProductIdentity` on its first run, exposing a subtitle that bypassed the transformer — fixed). The "ẋẋ" pseudo-locale (`--pseudo-locale` / `OCF_PSEUDO_LOCALE=1`) deterministically accents, brackets with ⟦…⟧ so truncation confesses, stretches ≥40%, preserves mnemonics and format placeholders, and mirrors the whole window (`RightToLeft` + `RightToLeftLayout`) — the renderer's forced-RTL discipline wired into the chrome. Smoke pass: both surfaces render in pseudo with every focusable control speaking the catalog; a headed test proves the switch end-to-end on the real window. The public name never localizes (ADR-006); domain text (validation messages, procedures, artifact content) localizes by its own contracts, not this catalog — recorded in the catalog's doc comment.

### 5. ~~Curated style pass → full format gate~~ — **done 29 Aug 2026**
~~The `.editorconfig` blanket severity currently drags IDE0008/IDE0058 into `dotnet format` verification; curate rule-by-rule to intended severities, fix the fallout, then widen the CI gate from whitespace-only to full `dotnet format --verify-no-changes`. **DoD:** gate green locally and in CI. *Small-medium, tedious, worthwhile.*~~
> **Done 29 Aug 2026:** 2002 diagnostics measured under the full gate; curated to intended severities with each silencing reasoned in the `.editorconfig` itself (var everywhere, fluent discards, parentheses/expression-body/conditional taste, flat namespaces, JSON-in-string fixtures; accessibility modifiers relaxed only for interface members). `dotnet format` applied the real fallout fixes across 65 files; CI's gate widened from whitespace-only to full `dotnet format --verify-no-changes`. Green locally; CI confirms on next push.

### 6. ~~Hostile-package depth + coverage breadth~~ — **done 29 Aug 2026**
~~Fuzz the `.ocfproj` reader harder (truncated central directories, hash-collision names, manifest/artifact disagreement); add `Foundry.Storage` and `Foundry.Rendering` to the CI coverage-threshold assemblies once their numbers are known honest. *Small.*~~
> **Done 29 Aug 2026:** the depth pass found the reader trusting where it should not, so the reader was hardened first: colliding entry names (exact or case-only — the two-manifests smuggling vector) refused; a manifest claiming a lane above Green, an unknown schema version, or assets the package does not carry refused; a tampered artifact failing structural validation refused. Nine new tests attack all of it — a six-point truncation sweep through the central directory, both collision forms, every disagreement class — and the untouched package still loads. Coverage measured honest first: `Foundry.Storage` 93.0%, `Foundry.Rendering` 92.5%; both joined the CI threshold assemblies.

### 7. honest-ink.org static site generator
The repository's documents are the content; a small deterministic generator (same spirit as SampleGenerator) renders README + governance + module pages to static HTML for the purchased domain. **Publishing is the typist's act** — hosting choice, DNS — the forge only makes the artifact. *Medium.*

### 8. Grouping Deck engine (atlas #137, lane-corrected)
Seeded-deterministic grouping cards from a teacher-typed roster of **synthetic or first-name-free labels** (the lane correction that v2 applied): teacher seed in, same groups out every time, printable deck. Green by construction. *Small.*

## IV. What must NOT be built while waiting

- All Aboard co-design features (new visual-support interaction patterns, symbol-set expansion strategy) — **AAC/SLP seat first.**
- Anything touching real classroom-derived data — **written instrument first.**
- `Foundry.Inference.Local` implementation — weights are provisioned by humans; the spike and capability kit remain its contract.
- Evidence rows: no generator output may close a HUMAN row of the [hardening checklist](../release/hardening-checklist.md), ever.

## V. Resuming

The convention that has carried the whole project: the typist says **"Proceed with …"** and names an item (e.g., "Proceed with the Calibration Press and the second Press wave"). Repository documents are canonical; this handover supersedes nothing, it only points. When items here complete, they are struck through with a dated note rather than deleted — the map should show where the road has been.
