# The moved calendar — the long window, and what it is for

**Date:** 29 August 2026, after the fourth menu was exhausted and the last CI exclusion deleted · **Audience:** the typist, the second maintainer when one is seated, whichever hand next opens this repository, and — for §II and §VI — counsel.

The pilot does not open on 8 September 2026. This document records why, what the critical path now is, what the delay unfreezes, and what must still not be built. It supersedes the September calendar in the [coordination plan](../pilots/human-gates-coordination-plan.md) and the [hardening checklist](../release/hardening-checklist.md); those documents' *substance* stands and only their dates are stale, which is itself a task in §V.

## I. What changed

Three things arrived at once, and they agree with each other.

1. **The entity track is longer than the pilot date.** Maryland must accept the articles; the EIN (Form SS-4) is filed after acceptance so line 1 matches the approved legal name exactly; Form 1023-EZ cannot precede the EIN. The typist has sequenced this correctly and it simply does not fit inside a fortnight.
2. **An educator wrote agreeing to join, and said 8 September does not fit them.** A council seat accepted on a date the seat-holder cannot make is not a seat.
3. **The typist reached the same conclusion independently.** 8 September was always the earliest honest date, not a comfortable one.

The new pilot date is **not yet set**, and this document deliberately does not invent one. It is downstream of Maryland's acceptance, which is downstream of the state's own queue. Setting a date before SDAT answers would be the kind of paper fact this project exists to refuse.

## II. The critical path is now legal, not technical

The forge is not the bottleneck and has not been since the fourth menu closed. The chain is:

> Maryland acceptance → EIN → 1023-EZ → (exemption recognized)

with three things hanging off it: the OV code-signing certificate (needs completed incorporation, EIN, and verifiable presence), the bank account (needs EIN), and whatever the counsel/accountant conversation concludes about tax-exemption path and copyright stewardship.

**Four questions belong to counsel, not to the keyboard.** They are recorded here so they are asked once, deliberately, rather than assumed:

1. **Is Form 1023-EZ actually available to Honest Ink, Inc.?** The whole schedule rests on it. The form has an eligibility worksheet with receipt and asset thresholds and category exclusions; failing it means the full Form 1023, a materially longer and more expensive path. This should be confirmed before the calendar is rebuilt on top of it.
2. **Is the pilot genuinely gated on exemption at all?** Incorporation → EIN → 1023-EZ → hosting is a clean chain, but only the last link was ever load-bearing for the *pilot*, and §III argues that link is optional. A six-week staff pilot of a locally installed, offline-capable, Green-lane-only tool may be independent of tax-exempt status. It is certainly gated on **Gate 3 district approval**, which is a separate instrument already drafted and ready. If these are two independent tracks that got braided together, the typist has a much freer hand than the calendar suggests.
3. **Does ADR-006's pre-release checkpoint reach a website?** [ADR-006](../adr/ADR-006-public-name-honest-ink.md) §4 requires counsel and formal screening to confirm the name "before any public distribution." A site is not distribution of software, but it is unmistakably public use of the name. See §III.
4. **Who are the directors, and where are the bylaws?** `GOVERNANCE.md` governs the *project* — product owner, council, maintainers, decision records. Nothing in this repository describes the *corporation's* governance. Maryland will want directors; the exemption filing will ask about organizational structure. This is the one entity-track item the coordination plan does not already name, and it is cheap to settle now and awkward to settle under time pressure.

## III. The website: the dependency was partly self-imposed

`honest-ink.org` is purchased. `tools/SiteGenerator` already builds the whole site — README, Governance, Contributing, Security, Notices, the Deterministic Press specification, and the samples gallery carrying the presses' own portraits — and its determinism is CI-tested. Nothing about publishing that requires an EIN or a recognized exemption.

**The 501(c)(3) promotional pricing and the host decision are orthogonal.** Nonprofit programs discount *paid infrastructure* — compute, seat licenses, ad grants. A generated static site needs none of it and is free at this scale regardless of status; a discount on free is nothing. The decision also forecloses nothing: a static site is a directory of files, regenerated deterministically from this repository, and changing hosts is a DNS record. Nonprofit pricing becomes genuinely relevant only when something dynamic is needed — donation processing, a symbol-commons upload endpoint, forms — and that conversation is better had against real requirements than hypothetical ones.

**Decision recorded: choose a static host now; do not buy a paid tier or sign an annual contract.** The recommendation is GitHub Pages, on grounds specific to this repository rather than general preference — the repo, the CI, and the generator are already here, so publishing is one workflow step with no new vendor, account, or secret, and custom domain plus HTTPS are included. Keep the site generated from the repository so the host stays interchangeable.

**But standing up the host and switching DNS are two decisions, not one.** The infrastructure carries no name risk and can proceed immediately. Pointing `honest-ink.org` at it is public use of the Honest Ink name and touches ADR-006's checkpoint (§II question 3). The delay has bought exactly the time to get counsel's read first, which is the cheapest available ordering. The cost of getting this wrong is not the HTML — that regenerates — it is that every educator, council member, and district contact who learns the name makes a later rename more painful, and ADR-006's fallback order (Schoolhouse Foundry, then Inkwright) exists precisely because that possibility is real.

What the wait is currently costing, recorded plainly because it lands on the very things the delay is meant to buy: the educator who accepted has nothing to look at; second-maintainer recruitment (R2-13, the only *standing* council finding) is much harder without a public face, and "a well-lit house, not a rescue" is a poor pitch when there is no house to see.

## IV. What the delay unfreezes, in order

The pilot freeze argued for on 29 August is void — it was a five-day argument, not a principle. The window is now weeks, and the following is the honest order. Per the amended divination rite, each item names its source.

**1. The doorless modules — nine of them.** *(Source: atlas entries 11, 31, 41, 51, 61, 101, 141, 148, 201, 202, and the composition the Press Room affords.)* Board to Brief, Access Remix, Directions Duet, Scaffold Smith, Talk Moves Studio, Lesson Loom, Exit Lens, Rubric Relay, and Source Lens all exist as tested, deterministic builders in `Foundry.Modules.BuiltIn`. Only All Aboard has a form; the sole app reference to the module namespace is `AllAboardForm.cs`. A teacher cannot reach any of the others from the running executable, though the atlas build order lists five of them as shipped 0.2 and 0.3 features. **This is the largest gap in the product.** The Press Room is already fully generic over typed parameters (`PressDefinition` → generated UI → Gate B → print/export/library/tile), so the work is generalizing that machinery into a shared module door, not writing nine bespoke forms. Five of these are pure-Green atlas entries that would otherwise be "enacted" by building something new when they are already built.

**2. A real second UI language.** *(Source: the composition the pseudo-locale affords.)* `UiLocaleMode` has exactly two members, Neutral and Pseudo; the chrome is English-only. For a product whose flagship differentiators are Directions Duet, Glossary Garden, and Family Bridge, an English-only shell around bilingual content is an asymmetry the multilingual seat will notice at once. The pseudo-locale already proved every string is externalized — 81 of them in one catalog — and `UiStrings`' own comment says real translations arrive as additional catalogs and the mechanism is ready. It is ready and unscheduled. **The typography and the translations themselves answer to the multilingual seat; that blessing is not ours to give from a keyboard.**

**3. The minimum-hardware floor, correctly framed.** *(Source: atlas, "One engine, not 227 codebases" — the hardware covenant.)* **1366×768 is a floor that must not break, not a design target.** Honest Ink is a teacher's tool on a teacher's machine, and 1920×1080 is the sensible design surface; most student and Chromebook devices are lower, but they are not this product's target. The test at the floor asserts that nothing is unreachable, clipped, or off-screen — not that the layout is optimized there. That honors the covenant correctly: the donated machine still works, the ordinary machine gets the good experience. Nothing in the repository currently references that resolution at all.

**4. Then the atlas, council-first.** *(Source: atlas.)* Seventy-four pure-Green entries remain unenacted of the 111 that carry no specialist gate, no Amber lane, and no Restricted escalation. But with a council seated and a pilot ahead, the order is educators first and register second: build what they ask for, not what the numbering happens to offer next.

**5. Seat the second maintainer.** *(Source: R2-13, standing; implementation plan Definition of Done.)* The Smith noted dryly that they cannot review their own succession. This window is the best chance the project will get, and items 1–3 make the house worth joining.

## V. Debts and hygiene the window should also pay

- **An upgrade path exists nowhere.** The only reference is a single line in the plan about managed deployments installing alongside. During a six-week pilot the project *will* ship fixes — that is what a pilot is for — and a longer runway means more versions before it even opens. How a pilot teacher moves from 0.7.0-alpha to the next build without losing their library is undefined. The reversibility guarantee covers `.ocfproj` projects across schema versions; it does not cover the executable.
- **The date is baked into sixteen places**, including the hardening checklist's "scheduled 8 Sep–16 Oct," the coordination plan's six-week calendar, and the default press seed `20260908`. Stale dates in governing documents are how documents stop being trusted. The seed is harmless and deterministic either way; the prose is not.
- **`tests/Accessibility` builds but contains no tests** — `dotnet test` reports "No test is available" for it. The real coverage lives in `tests/UiAutomation`, so nothing is unguarded, but a suite named Accessibility holding nothing reads as coverage that is not there. Either give it the tests its name promises or fold it into the suite that has them.
- **CI actions are on deprecated Node 20.** `actions/checkout@v4` and `gitleaks/gitleaks-action@v2` are being force-migrated to Node 24 by the runner. Not urgent, worth a version bump next time the workflow is touched.

## VI. What has not changed

The standing laws of all four menus remain binding, verbatim and without exception:

- Presses take parameters, never prose. Nothing renders, prints, exports, or persists without a typed `ApprovedArtifact`.
- Lanes escalate only; Amber never persists. Model self-ratings are not release evidence.
- The AAC/SLP seat and the district instrument are not waivable from the keyboard, and no additional time makes them so. **A longer runway is not a licence to build into the seats' territory while they are unseated** — it is the opposite, because there is now no schedule pressure to excuse it.
- No printing without approval. No auto-approving CLI.
- **Version bumps, tags, installs, signing, and distribution remain the typist's acts.** The forge builds; the typist ships.
- Every human gate in the [coordination plan](../pilots/human-gates-coordination-plan.md) survives the schedule change intact. Only the dates moved.

## VII. Resuming

The closing rites in the [enactment handover](2026-08-29-forge-enactment-handover.md) §III are unchanged and apply to every item: Release build with warnings as errors; `dotnet format` fix then `--verify-no-changes` at exit 0; the full suite plus at least one stability re-run with failure names surviving the filter; the SampleGenerator twice with every byte hash-compared if presses changed; strike with a dated note; commit and push. The determinism gate now runs with **no exclusions** — the `.ocfproj` writer was made a pure function of its inputs on 29 August and the last standing exception was deleted with it.

The typist says **"Proceed with …"** and names an item; that adopts it. Items completed here are struck with dated notes, never deleted.

One caution earned the same day this was written, and it outranks the craft notes: **a defect note records a hypothesis, not a finding.** The `.ocfproj` nondeterminism was recorded for a day as a timestamp bug; it was measured before it was fixed and turned out to be a random `Guid`, with the timestamps a real second cause that a fast measurement had nearly hidden. Measure before fixing, and check whether the cause you recorded is the cause you have. The same discipline applies to a calendar: this document records what is known on 29 August 2026 and no date beyond what Maryland has actually answered.
