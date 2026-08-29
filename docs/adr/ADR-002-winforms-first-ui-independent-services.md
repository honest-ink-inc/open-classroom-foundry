# ADR-002: WinForms first, UI-independent services, standard controls only

**Status:** Accepted — by adoption of implementation plan 2.0 (29 August 2026); recorded here as the standing decision record
**Date:** 2026-08-29
**Ratified by:** Product owner / master teacher

## Context

Writer's Kiosk (audited baseline `c2b670b`, 75 passing tests) is WinForms. A framework rewrite in the first months would spend the project's scarcest resource — one developer's time — on plumbing instead of the vertical slice that proves the whole grammar. But WinForms accessibility collapses exactly where teams customize: owner-drawn controls expose nothing to UI Automation, and the accessibility contract (implementation plan §8) is then unmeetable at any price of retrofit (Master's Review, finding F8).

## Decision

- Keep **WinForms** for the first vertical slices. Reassess WPF or another shell only after All Aboard field testing.
- The UI framework is never the architecture: dependency injection plus presenters/view-models keep all domain, application, and rendering logic UI-independent, so the shell can be replaced without touching the engine.
- **Standard controls only, no owner-drawing**, until the accessibility test harness exists — and thereafter any custom control ships with its own UI Automation peer and NVDA/Narrator evidence.
- The review surface — the most novel UI in the program — receives assistive-technology walkthroughs early, not late.

## Alternatives considered

1. **WPF or WinUI rewrite first** — rejected: months of framework work before any teacher value; contradicts the strangler-refactor strategy.
2. **Cross-platform UI (Avalonia, MAUI)** — deferred, not rejected: the UI-independent services requirement is precisely what keeps this future open; the initial operating environment is managed Windows.
3. **WinForms with free customization** — rejected: unmeetable accessibility contract (F8).

## Consequences

Fast path to the 0.1 vertical slice on proven components; a plainer-looking UI in early releases, accepted deliberately — trust is earned by behavior, not chrome. The standard-controls rule is enforceable in review from day one. Reversible per shell: the services survive any UI replacement by construction.
