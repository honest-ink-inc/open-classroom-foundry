# ADR-004: Approval is structural — render, export, save-as-final, and print accept only an ApprovedArtifact

**Status:** Accepted — by adoption of implementation plan 2.0 (29 August 2026); recorded here as the standing decision record
**Date:** 2026-08-29
**Ratified by:** Product owner / master teacher

## Context

The constitution's second requirement makes the teacher the accountable author: the generator proposes, the teacher inspects, edits, and approves. A checkbox the UI happens to show is a promise; a type the compiler enforces is a property. The binding audit correction reads: *model output never prints, exports, sends, or saves as approved content without teacher inspection.*

## Decision

- The engine's render, export, save-as-final, and print APIs accept an **`ApprovedArtifact`**, never a `DraftArtifact`. There is no cast, no bypass constructor, and no reflection-friendly back door; the type boundary is the gate.
- Approval belongs to the exact artifact revision. Any subsequent edit invalidates the approval and returns the artifact to the draft state; re-approval covers the new revision.
- The mandatory state machine (implementation plan §6.3) makes Blocked, declined, cancelled, provider-failed, validation-failed, and purge-incomplete explicit states — failure is never silent passage.
- Gate B (pedagogical approval) is field-level inspection on the review surface; for Module Zero's parameter-only presses the same gate applies in its lightest form, so every teacher learns one approval rhythm on artifacts where approval is effortless.
- Architecture tests verify that no code path reaches a sink without the approved type, and the stop-ship conditions (§19) treat any unapproved render/export/print path as a release blocker.

## Alternatives considered

1. **UI-level confirmation dialogs** — rejected: unenforceable against future code paths; approval must survive refactoring by construction.
2. **A boolean `IsApproved` flag on one artifact type** — rejected: flags get set; types get proven. A flag also cannot bind approval to a revision.
3. **Approval at save time only** — rejected: printing and exporting are the consequential acts; each sink needs the gate.

## Consequences

Every module pays a small ceremony cost — and that ceremony is the product's central promise made mechanical. Test harnesses use the same gate as production (fixtures approve artifacts explicitly), so tests cannot drift from the real path. Effectively irreversible: removing the gate is removing the constitution's enforcement.
