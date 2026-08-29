# Open Classroom Foundry

> Free teacher tools for the liberation of the minds of all, for all time.

**Open Classroom Foundry** is a GNU GPL-3.0-or-later family of teacher-facing authoring tools descended from Writer's Kiosk: local-first by instinct, district-governable when cloud inference is used, bounded in purpose, editable by the teacher, and capable of turning the physical classroom into useful instructional artifacts.

The whole family lives on one reusable grammar:

> **Capture or import → interpret → constrain → scaffold → let the teacher verify → render → print/export → preserve only what is worth preserving.**

The machine never appears to be the master teacher. The teacher remains the accountable author, editor, witness, and final decision-maker; the software is the extraordinarily fast press, compositor, translator, accessibility bench, and apprentice.

*The public name is decided: **Honest Ink — the classroom foundry** (ADR-006, pending final counsel clearance pre-release). "Open Classroom Foundry" remains the working and repository title; the ship name lives in a single code resource. Module names are still working titles.*

## Status

**Pre-0.0 — charter phase (Days 1–15 of the implementation plan).** No application code exists yet. This repository currently holds the governing documents, architecture decision records, and the solution skeleton. The audited Writer's Kiosk baseline (commit `c2b670b`, 75 passing tests) lives at [Spacejunk-io/writers-kiosk-csharp](https://github.com/Spacejunk-io/writers-kiosk-csharp) and is refactored behind interfaces during Release 0.0, not copied here wholesale.

## What gets built, in order

| Release | Contents |
|---|---|
| 0.0 Foundation + Module Zero | Engine extraction from Writer's Kiosk; first Deterministic Press presses (Blankforms Press, Flashcard Flywheel, Booklet Binder) as the rendering pipeline's real cargo |
| 0.1 | All Aboard: Visual Support Studio (vertical slice) |
| 0.2 | Board to Brief, Directions Duet |
| 0.3 | Scaffold Smith (with absorbed TaskDock preset, ADR-005), Lesson Loom, Talk Moves Studio, full Deterministic Press studio |
| 0.4–0.5 | Symbol Commons, Access Remix, Source Lens, Green-only Family Bridge; Foundry.Inference.Local spike |
| 0.6 | Exit Lens and Rubric Relay — synthetic fixtures first, Amber lane only with complete Amber architecture and written district approval |
| 1.0 | Hardening, packaging, pilots, Open Commons |

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

See [CONTRIBUTING.md](CONTRIBUTING.md) and [GOVERNANCE.md](GOVERNANCE.md). One rule outranks all others: **no student work, student data, or identifying classroom material ever enters this repository** — not in code, fixtures, issues, or documentation. Security and privacy reports: [SECURITY.md](SECURITY.md).
