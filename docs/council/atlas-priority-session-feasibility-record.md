# Atlas H0 separate feasibility record

**Status:** DRAFT TEMPLATE — NOT A FEASIBILITY FINDING. Copy this file to
`atlas-priority-session-YYYY-MM-DD-feasibility-v1.md` only after the matching H0
record and detached freeze manifest are complete. The completed copy uses the
exact status `FEASIBILITY RECORDED`.

The maintainer may add implementation facts here without rewriting council
evidence. This record cannot rank a need, change a recommendation, clear a
protected hold, or make a product-owner decision. Immediately before completing
this record, audit the full append-only H0 chain at the exact candidate revision.
Any later chain event makes this record stale for downstream use until a new
linked feasibility version binds a fresh audit.

## Frozen H0 binding

| Field | Record |
|---|---|
| Feasibility record ID and version | `[not supplied]` |
| Predecessor feasibility record path, version, byte length, and SHA-256; or NONE | `NONE — first feasibility record` |
| H0 record ID and version | `[not supplied]` |
| Final H0 record repository path | `[not supplied]` |
| Final H0 record SHA-256 | `[not supplied]` |
| H0 freeze-manifest repository path | `[not supplied]` |
| H0 freeze-manifest SHA-256 | `[not supplied]` |
| Enacted operating-terms exact file binding | `[not supplied]` |
| Operative compensation-policy exact file binding | `[not supplied]` |
| Upstream chain-audit ID | `[not supplied]` |
| Upstream chain-audit UTC instant | `[not supplied]` |
| Upstream chain-audit repository path, version, byte length, and SHA-256 | `[not supplied]` |
| Chain-audit exact candidate repository revision and dirty-tree disposition | `[not supplied]` |
| Public append-only event bindings, or NONE | `[not supplied]` |
| Private append-only event attestations, or NONE | `[not supplied]` |
| Current effective upstream dispositions and unresolved chain holds | `[not supplied]` |
| Feasibility record UTC instant | `[not supplied]` |

## Feasibility assessment

| Recommended possibility | Reusable engine/capability | Smallest bounded slice | Dependencies and migrations | Required automated and human evidence | Effort/risk range | Conflicts with ADR, plan, or gate |
|---|---|---|---|---|---|---|
| | | | | | | |

## Authority boundary

- Preserve the council's requested outcome even when the proposed implementation changes.
- Do not turn ease of implementation into a retroactive council priority.
- Mark uncertainty. Do not convert rehearsal findings or model judgment into human evidence.
- If a candidate enters Amber, Restricted, or a protected seat's territory, record the stop; do not design around it.
- Do not edit the bound H0 record, freeze manifest, or this completed record. A correction is the next `-feasibility-v<n>.md` and exactly binds its immediate predecessor path, version, byte length, and SHA-256. The fresh chain audit, not a self-hash inside this record, determines which linked version is current.
- A completed record uses a fresh chain audit made after the H0 freeze and before this record. The current-disposition value ends `unresolved-chain-holds=NONE`; any missing, ambiguous, conflicting, withdrawn, restricted, or unresolved chain event is a HOLD and forbids completion.
