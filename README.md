# Open Classroom Foundry

> Free teacher tools for the liberation of the minds of all, for all time.

**Open Classroom Foundry** is a GNU GPL-3.0-or-later family of teacher-facing authoring tools descended from Writer's Kiosk: local-first by instinct, district-governable when cloud inference is used, bounded in purpose, editable by the teacher, and capable of turning the physical classroom into useful instructional artifacts.

The whole family lives on one reusable grammar:

> **Capture or import → interpret → constrain → scaffold → let the teacher verify → render → print/export → preserve only what is worth preserving.**

The machine never appears to be the master teacher. The teacher remains the accountable author, editor, witness, and final decision-maker; the software is the extraordinarily fast press, compositor, translator, accessibility bench, and apprentice.

*The public name is decided: **Honest Ink — the classroom foundry** (ADR-006, pending final counsel clearance pre-release). "Open Classroom Foundry" remains the working and repository title; the ship name lives in a single code resource. Module names are still working titles.*

## Status

**0.7.0-alpha — the software runs, and it is not 1.0.** A Windows desktop application builds from this repository and does real work offline: a **Press Room** of 56 deterministic printable engines across 23 recipes, and **All Aboard**, the visual-support studio, mode-complete. Every output passes a teacher's approve gate before anything renders, prints, exports, or is saved; projects save to the open `.ocfproj` format and reopen without the application that made them. **665 automated tests** run green across six suites, and CI gates warnings-as-errors, formatting, 93% core coverage, a secret scan, and byte-for-byte determinism — the sample outputs are generated twice on a machine nobody tuned and compared with no exclusions.

**What is honestly not done.** Nine module builders — Board to Brief, Access Remix, Directions Duet, Scaffold Smith, Talk Moves Studio, Lesson Loom, Exit Lens, Rubric Relay, Source Lens — are written and tested but have **no door**: no teacher can reach them from the running application yet. The application's own chrome is English-only (the pseudo-locale proves every string is externalized; no second language is shipped). The build is **unsigned** — an unsigned zip is a build, not a release — and there is no installer. **No pilot has run.** The six-week staff pilot is prepared in full and its opening date is pending; see the [moved calendar](docs/handover/2026-08-29-the-moved-calendar.md). The second maintainer's seat is open, and the bus factor is one.

The audited Writer's Kiosk baseline (commit `c2b670b`, 75 passing tests) lives at [Spacejunk-io/writers-kiosk-csharp](https://github.com/Spacejunk-io/writers-kiosk-csharp); its reusable components were refactored behind interfaces during Release 0.0 rather than copied here wholesale. Release-readiness is tracked honestly, item by item, in the [1.0 hardening checklist](docs/release/hardening-checklist.md) — every open human row there is a reason this is not 1.0.

## What gets built, in order

| Release | Contents | State, 29 Aug 2026 |
|---|---|---|
| 0.0 Foundation + Module Zero | Engine extraction from Writer's Kiosk; first Deterministic Press presses (Blankforms Press, Flashcard Flywheel, Booklet Binder) as the rendering pipeline's real cargo | **Delivered** — and the Press went well past its first three |
| 0.1 | All Aboard: Visual Support Studio (vertical slice) | **Delivered** — mode-complete, with its own authoring surface |
| 0.2 | Board to Brief, Directions Duet | **Builders only** — written and tested; no door |
| 0.3 | Scaffold Smith (with absorbed TaskDock preset, ADR-005), Lesson Loom, Talk Moves Studio, full Deterministic Press studio | **Mixed** — the Press studio is delivered (56 engines, 23 recipes); the three builders are written and tested, no door |
| 0.4–0.5 | Symbol Commons, Access Remix, Source Lens, Green-only Family Bridge; Foundry.Inference.Local spike | **Part-built** — the provenance catalog, local symbol store and pack export exist, but no Symbol Commons surface; three builders written, no door; the spike is a written feasibility assessment and the adapter is an empty project |
| 0.6 | Exit Lens and Rubric Relay — synthetic fixtures first, Amber lane only with complete Amber architecture and written district approval | **Builders only, synthetic** — no door; the Amber lane has not been entered and cannot be until the district instrument is signed |
| 1.0 | Hardening, packaging, pilots, Open Commons | **In progress** — unsigned, no installer, no pilot run, Open Commons not begun |

**Builders only** means the module’s logic is written and covered by tests but no teacher can reach it from the running application — the gap named under Status above. Release numbers describe *shipped* releases, so a row is not “delivered” until a teacher can use it.

The authoritative roadmap, module specifications, data-lane contract, human gates, and stop-ship conditions are in [docs/implementation-plan.md](docs/implementation-plan.md). The full 227-idea atlas is [docs/idea-atlas.md](docs/idea-atlas.md).

## The liberation test

This program advances educational liberation only if its source is available, modifiable, and redistributable; existing projects remain openable and editable without a cloud connection or subscription; it uses an open documented project format; it requires no proprietary symbol set; it supports more than one inference provider and performs deterministic authoring without any model; it records rights and provenance rather than laundering assets through generation; and it never converts access support into compliance, surveillance, diagnosis, grading, or placement.

Three commitments beyond the code: a **paper-first guarantee** (every module's primary output is fully usable with zero learner devices), a **time-to-artifact budget** declared per recipe and measured in pilots, and a **minimum hardware covenant** (a 2015-era machine stays on the test bench permanently — donated hardware is where liberation actually happens).

## Data lanes, in one breath

**Green** — teacher-created, public-domain, openly licensed, staged, or generic content: save-friendly. **Amber** — anything that could link back to a learner: ephemeral by contract, separately governed, structurally gated. **Restricted** — IEP/504, medical, disciplinary, disclosure, and recipient data: blocked through 1.x. The lane follows the content and every derivative, never the operator, module, or device. Details: implementation plan §4.

## Licensing

- **Code and first-party recipes:** GNU GPL-3.0-or-later — see [COPYING](COPYING).
- **Documentation and original printable content:** a separate free-culture license will be declared after project-specific review (implementation plan §9); until then, all-rights-reserved by default outside the code.
- **Assets** (fonts, symbols, translations, media): governed individually with recorded provenance — see [NOTICE.md](NOTICE.md). Unknown rights block distribution.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) and [GOVERNANCE.md](GOVERNANCE.md); automated contributors also read [AGENTS.md](AGENTS.md). Three rules outrank all others — no credentials, no blind-study instruments, and above all **no student work, student data, or identifying classroom material ever enters this repository** — not in code, fixtures, issues, or documentation. Security and privacy reports: [SECURITY.md](SECURITY.md).
