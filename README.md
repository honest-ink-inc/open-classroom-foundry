# Open Classroom Foundry

> Free teacher tools for the liberation of the minds of all, for all time.

**Open Classroom Foundry** is a GNU GPL-3.0-or-later family of teacher-facing authoring tools descended from Writer's Kiosk: local-first by instinct, district-governable when cloud inference is used, bounded in purpose, editable by the teacher, and capable of turning the physical classroom into useful instructional artifacts.

The whole family lives on one reusable grammar:

> **Capture or import → interpret → constrain → scaffold → let the teacher verify → render → print/export → preserve only what is worth preserving.**

The machine never appears to be the master teacher. The teacher remains the accountable author, editor, witness, and final decision-maker; the software is the extraordinarily fast press, compositor, translator, accessibility bench, and apprentice.

*The public name is decided: **Honest Ink — the classroom foundry** (ADR-006, pending final counsel clearance pre-release). "Open Classroom Foundry" remains the working and repository title; the ship name lives in a single code resource. Module names are still working titles.*

## Status

**0.7.0-alpha — the software runs, and it is not 1.0.** The Windows desktop application works offline through three real doors: a **Press Room** of 56 deterministic printable engines across 23 recipes; **All Aboard**, mode-complete; and one generated **Built-in Studios** surface exposing ten module doors and eleven modes. Eight modes carry synthetic Green starters. Access Remix is visible but has no catalog build delegate or public remixer API until protected specialist review establishes a non-keyboard purpose authority; arbitrary typed All Aboard content remains purpose `Unknown` and cannot unlock it. The two district-governed modes are likewise visible but unavailable. Gate B now presents the exact semantic elements, exact source when one exists, and a visibly unapproved visual derivative; approval stays disabled until the embedded browser completes the exact current revision and render profile. Projects save atomically to the open `.ocfproj` format, reopen through a hostile-package-aware reader and explicit Green preflight, and have an advisory side-by-side compatibility-preparation host for a future managed version change. **890 automated tests** ran green twice across seven suites on 30 August 2026. CI gates warnings-as-errors, full formatting, an 80% core-and-module coverage floor, a full-history secret scan, direct and transitive vulnerability audit, a complete CycloneDX 1.7 SBOM normalized against NuGet's restored dependency graph, and byte-for-byte sample determinism.

**What is honestly not done.** No real second chrome language is shipped. A deterministic 978-string review packet, strict catalog validator, RTL layout path, and exact-file build allowlist are ready, but the allowlist is deliberately empty until the multilingual seat supplies and reviews an actual translation. Access Remix cannot build until the protected seats establish and review a purpose-authority route that cannot be granted by typed content. Exit Lens and Rubric Relay cannot build until written district authorization exists; visible doors do not waive either gate. The managed-upgrade ADR and runbook remain proposed; the repository has a tested compatibility-preparation seam, not a packaged or managed-device-proven upgrade and rollback path. It does not install, version, sign, distribute, or replace an application. The build is **unsigned**—an unsigned zip is a build, not a release—and there is no installer. **No pilot or Atlas priority session has run.** The six-week staff pilot is prepared and its opening date is pending; see the [forge closeout](docs/handover/2026-08-30-forge-closeout.md). The second maintainer's seat is open, and the bus factor is one.

The audited Writer's Kiosk baseline (commit `c2b670b`, 75 passing tests) lives at [Spacejunk-io/writers-kiosk-csharp](https://github.com/Spacejunk-io/writers-kiosk-csharp); its reusable components were refactored behind interfaces during Release 0.0 rather than copied here wholesale. Release-readiness is tracked honestly, item by item, in the [1.0 hardening checklist](docs/release/hardening-checklist.md) — every open human row there is a reason this is not 1.0.

## What gets built, in order

| Release | Contents | State, 29 Aug 2026 |
|---|---|---|
| 0.0 Foundation + Module Zero | Engine extraction from Writer's Kiosk; first Deterministic Press presses (Blankforms Press, Flashcard Flywheel, Booklet Binder) as the rendering pipeline's real cargo | **Delivered** — and the Press went well past its first three |
| 0.1 | All Aboard: Visual Support Studio (vertical slice) | **Delivered** — mode-complete, with its own authoring surface |
| 0.2 | Board to Brief, Directions Duet | **Reachable Green studios** — typed doors, Gate B, output sinks, and synthetic starters |
| 0.3 | Scaffold Smith (with absorbed TaskDock preset, ADR-005), Lesson Loom, Talk Moves Studio, full Deterministic Press studio | **Reachable** — both Scaffold modes, Lesson Loom, Talk Moves, and the 56-engine Press studio share typed review and output machinery |
| 0.4–0.5 | Symbol Commons, Access Remix, Source Lens, Green-only Family Bridge; Foundry.Inference.Local spike | **Part-built** — Access, Source, and Family doors are visible; Source and Family build from synthetic Green starters, while Access is held with no catalog delegate or public remixer API pending protected specialist purpose-authority review. The provenance catalog, local symbol store, and pack export exist, but no Symbol Commons surface; the local-inference adapter remains empty |
| 0.6 | Exit Lens and Rubric Relay — synthetic fixtures first, Amber lane only with complete Amber architecture and written district approval | **Visible, unavailable by design** — both doors state the written district gate; no UI or keyboard act can enable their builders |
| 1.0 | Hardening, packaging, pilots, Open Commons | **In progress** — the 1366×768 floor, Accessibility suite, project compatibility preparation, and package hardening exist; signing, installer, pilot, and Open Commons remain open |

“Reachable” means a teacher can open the typed studio in the running application; it does not mean a protected review, pilot, signed release, or district authorization has occurred. Release numbers still describe *shipped* releases, so reachability alone is not a release claim.

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
