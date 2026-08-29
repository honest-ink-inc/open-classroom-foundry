# ADR-006: The public name is Honest Ink

**Status:** Accepted — decided by the product owner/master teacher, 29 August 2026
**Date:** 2026-08-29
**Ratified by:** Product owner / master teacher

## Context

The Master's Review (finding F13) and the first-pass trademark screen (docs/trademark-screening.md) established that "Open Classroom Foundry" is a working title, not a viable public name: OpenClassrooms occupies the adjacent composite in our exact class, "open classroom" is a generic pedagogy term, and the crowded "Open ___" register adds nothing the GPL does not already guarantee. Three finalists were screened; **Honest Ink** returned the cleanest result of every name tested and carries the deepest anchor in the governing documents — the constitution's locked truth-bearing fields, uncertainty marked rather than plausibly completed, nothing invented on paper; in the review's words, "good geometry and honest ink."

## Decision

1. The public name of the product family is **Honest Ink**; the full public identity is **"Honest Ink — the classroom foundry"**, per the standing subtitle policy (the name names; the subtitle explains).
2. **"Open Classroom Foundry" remains the working and repository title.** All internal identifiers — `Foundry.*` namespaces, `OpenClassroomFoundry` solution and internal id, document titles — are unchanged; they were public-name-neutral by design and stay that way.
3. The ship name lives in exactly one code resource: `ProductIdentity` in `Foundry.App.WinForms`. Nothing else in code, schemas, project formats, or diagnostics carries the public name.
4. **Pre-release checkpoint:** professional counsel and formal USPTO/EUIPO screening must confirm the name before any public distribution (the informal screen is evidence, not clearance). If counsel fails it, the fallback order is Schoolhouse Foundry, then Inkwright, adopted by a superseding ADR.
5. Module names (All Aboard, Board to Brief, …) remain working titles under their own screening obligations; the Lesson Loom rename stands decided-in-principle per the screening document.

## Alternatives considered

Schoolhouse Foundry (warm and legible, but requires clearance against Schoolhouse.world and Schoolhouse Technologies) and Inkwright (strong craft compound, but the root is held by two existing companies including a technical-writing firm). Both survive as ordered fallbacks. Retaining "Open Classroom Foundry" publicly was rejected for the reasons in Context.

## Consequences

Documentation and future marketing surfaces adopt Honest Ink; the repository does not rename; the rename surface if counsel ever forces one is a single resource string plus prose — by design, not luck. The name commits the product to its own standard: a tool named Honest Ink that invented a fact on paper would be self-refuting, which is precisely the right pressure to live under.
