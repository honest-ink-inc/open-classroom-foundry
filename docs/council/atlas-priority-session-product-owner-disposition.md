# Atlas H0 separate product-owner disposition

**Status:** DRAFT TEMPLATE — NOT A PRODUCT-OWNER DECISION. Copy this file to
`atlas-priority-session-YYYY-MM-DD-disposition-v1.md` only after the matching H0
record, detached freeze manifest, and separate feasibility record are complete.
The copy uses exactly one terminal status. Use `PRODUCT-OWNER DISPOSITION
RECORDED` only for an authorized decision. If an unresolved material
product-owner conflict prevents any decision, use `PRODUCT-OWNER DISPOSITION
HELD`; that is an immutable evidence record of the continuing hold, not a
completed disposition.

For each recommendation, the product owner records **adopt for a proposed forge
menu**, **defer**, or **decline**, with a reason and exact scope. Adoption does
not ratify an ADR, waive a specialist or district gate, authorize release, or
change the participant-reviewed council record.

For `PRODUCT-OWNER DISPOSITION RECORDED`, the completed table uses exactly
`ADOPT FOR PROPOSED FORGE MENU — YYYY-MM-DD`, `DEFER — YYYY-MM-DD`, or
`DECLINE — YYYY-MM-DD` in each disposition cell. For `PRODUCT-OWNER DISPOSITION
HELD`, retain only the canonical header and separator with no substantive rows;
the conflicted product owner records no action.
No row may authorize release or publication, ratify an ADR, or clear a protected
seat's hold. Immediately before this disposition, audit the complete H0 and
feasibility append-only chains at the exact candidate revision. Any later chain
event makes this disposition stale for downstream use until a new linked
disposition binds a fresh audit. Record the product owner's material conflict
category separately. An unresolved material conflict uses the terminal
`PRODUCT-OWNER DISPOSITION HELD` record and permits no product decision. The
current `OCF-COUNCIL-TERMS-v1` validator recognizes no substitute priority
authority. A future separately enacted governance/ADR route requires a
superseding record schema before use; an arbitrary delegation or authority
record cannot.

## Frozen H0 and feasibility binding

| Field | Record |
|---|---|
| Product-owner disposition record ID and version | `[not supplied]` |
| Predecessor disposition record path, version, byte length, and SHA-256; or NONE | `NONE — first disposition record` |
| H0 record ID and version | `[not supplied]` |
| Final H0 record repository path | `[not supplied]` |
| Final H0 record SHA-256 | `[not supplied]` |
| H0 freeze-manifest repository path | `[not supplied]` |
| H0 freeze-manifest SHA-256 | `[not supplied]` |
| Feasibility record repository path | `[not supplied]` |
| Feasibility record SHA-256 | `[not supplied]` |
| Enacted operating-terms exact file binding | `[not supplied]` |
| Operative compensation-policy exact file binding | `[not supplied]` |
| Upstream chain-audit ID | `[not supplied]` |
| Upstream chain-audit UTC instant | `[not supplied]` |
| Upstream chain-audit repository path, version, byte length, and SHA-256 | `[not supplied]` |
| Chain-audit exact candidate repository revision and dirty-tree disposition | `[not supplied]` |
| Public append-only event bindings, or NONE | `[not supplied]` |
| Private append-only event attestations, or NONE | `[not supplied]` |
| Current effective upstream dispositions and unresolved chain holds | `[not supplied]` |
| Product-owner conflict category and disposition | `[not supplied]` |
| Product-owner disposition UTC instant | `[not supplied]` |

## Product-owner disposition

| Recommendation | Disposition and date | Exact bounded scope | Reason | Outstanding seats/gates | Evidence required before completion |
|---|---|---|---|---|---|
| | | | | | |

## Authority boundary

This disposition is downstream of, and cannot alter, the final H0 record,
detached freeze manifest, or feasibility record. A correction or supersession
is a new linked version. Any architectural change still follows the ADR
process, and every protected-seat, district, rights, evidence, and typist hold
remains independently operative.

A completed disposition uses a fresh chain
audit made after the current feasibility record and before this disposition;
its current-disposition value ends `unresolved-chain-holds=NONE`. Any missing,
ambiguous, conflicting, withdrawn, restricted, or unresolved chain event is a
HOLD and forbids completion.

For a recorded disposition, the conflict field is exactly `NONE — <basis>`.
This validator version recognizes no substitute priority authority. A future
separately enacted governance/ADR route requires a superseding record schema
before use.
A held record instead uses `HELD — conflict-category=<de-identified category>; written-finding=<substantive finding>; adoption=NONE`, leaves the disposition table without substantive rows, and records no product decision. It is mechanically admissible evidence of a continuing hold, but it is not a completed disposition and cannot satisfy a downstream adoption, implementation, publication, or release dependency. Resolving it requires a new linked `-disposition-v<n>.md` record with fresh chain evidence; silence, self-appointment, or an informal delegation cannot resolve it.
