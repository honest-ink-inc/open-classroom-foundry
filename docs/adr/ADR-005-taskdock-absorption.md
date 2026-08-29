# ADR-005: TaskDock is absorbed into Scaffold Smith as its task-entry preset

**Status:** Accepted — drafted 29 August 2026 by Da Vinci per Master's Review finding F2; ratified 29 August 2026 by the product owner/master teacher, resolving implementation plan 2.0, section 20, decision 16
**Series note:** ADR-001 through ADR-004 are chartered in the implementation plan (Days 1–15) and not yet written. This record is drafted out of sequence because the divergence it resolves affects both source documents and blocked their reconciliation.

---

## Context

The idea atlas and the audited implementation plan diverged silently on TaskDock (atlas entry #21: *turn an assignment into materials, first action, chunks, checkpoints, help routes, and a concrete definition of done*):

- The atlas's "pragmatic first release" bundled **All Aboard, Board to Brief, Access Remix, Directions Duet, and TaskDock** as five output modes of one application, and its Teacher Foundry 0.2 build stage listed TaskDock again.
- The audited plan's engineering sequence and release roadmap never mentioned TaskDock at all, and its twelve-module sequence carried Scaffold Smith in the equivalent position.
- Neither document recorded the decision to drop it. The Master's Review flagged this as finding F2: *promised, then silently abandoned*.

Comparing the two ideas' machinery:

| | TaskDock (#21) | Scaffold Smith (#201) |
|---|---|---|
| Input | A teacher-authored assignment | A teacher-authored task, target, criteria, and barrier categories |
| Output | Materials list, first action, chunks, checkpoints, help routes, definition of done | Scaffold packets, hint ladders, banks, checkpoint cards, rationale, fade plan |
| Editor surface | Ordered support elements over a source task | Ordered support elements over a source task |
| Invariants | Preserve the task; add no answers | Preserve target, criteria, and demand; add no answers; every support removable |
| Lane | Green (individualized use excluded) | Green (individualized use excluded) |

TaskDock's machinery is a strict subset of Scaffold Smith's. Every TaskDock output is a scaffold in Scaffold Smith's sense — a temporary, removable support with a barrier addressed (task initiation and executive load), a preserved demand (the assignment itself), and a natural fade criterion (independent task entry). Two products would be one product wearing two coats, each needing its own editor, validators, renderer templates, fixtures, evaluation suite, and documentation.

## Decision

**Absorb TaskDock into Scaffold Smith as a first-party preset named the "task-entry scaffold."**

1. The preset produces TaskDock's full output set: materials, first action, chunks, checkpoints, help routes, and a concrete definition of done.
2. It runs on Scaffold Smith's engine — same editor surface, validators, renderer templates, approval gate, and Green lane — and ships when Scaffold Smith ships (Release 0.3).
3. It inherits Scaffold Smith's invariants without exception: no diagnosis inference, no automatic leveling, not an IEP/accommodation generator, no answer leakage, every element independently removable, and the printed removal plan on the teacher page.
4. The preset carries its own recipe identity (stable ID, version, evaluation-suite membership) so it is discoverable by teachers searching for "task breakdown," "first step," "chunking," or "definition of done" — the capability must not hide inside an unfamiliar module name.
5. Atlas entry #21 is retained with an absorption note as the idea's historical record; the atlas's first-release bundle names Scaffold Smith in its place.

## Alternatives considered

1. **Keep TaskDock as a separate module.** Rejected: duplicates an editor, validator set, renderer, fixture corpus, and evaluation suite for a strict subset of another module's machinery, in a program whose central discipline is "one engine, not two hundred codebases."
2. **Restore TaskDock to the roadmap as its own later module.** Rejected: the same duplication, merely postponed, plus a naming collision with Scaffold Smith's checkpoint outputs that would confuse teachers.
3. **Drop the idea entirely.** Rejected: task entry and executive-function unpacking is a real, high-frequency classroom need, and the atlas's Studio III depends on it as an anchor. The idea survives; only the separate executable dies.

## Consequences

**Positive**
- One engine fewer to build, test, document, and evaluate; the capability ships on Scaffold Smith's schedule with Scaffold Smith's evidence.
- The atlas and plan are reconciled (resolves findings F2 and F12); Studio III's TimeFold, Start Line, Done Definition, Focus Beacon, and Plan B Press now have an unambiguous engine home as future presets of the same family.
- The definition-of-done checklist and first-action card gain Scaffold Smith's fade discipline for free — TaskDock alone had no fade concept, which the review would otherwise have flagged.

**Negative, accepted**
- The name TaskDock disappears from the product surface. Mitigation: recipe naming, search keywords, and documentation route teachers to the preset (consequence of point 4 above).
- Scaffold Smith's Release 0.3 scope grows by one preset. Mitigation: the preset defines no new machinery; growth is fixtures and templates only.

**Follow-through required**
- Ratification recorded 29 August 2026; implementation plan section 20, decision 16 is resolved.
- Scaffold Smith's fixture corpus (30–50 stratified fixtures) must include task-entry cases: multi-day assignments, materials-heavy labs, and single-period tasks.
- If field evidence later shows the preset deserves independent life (usage patterns, divergent editor needs), a future ADR may split it back out; this decision is reversible at the cost of the duplication it currently avoids.
