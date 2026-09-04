# Append-only governance record event

**Status:** DRAFT TEMPLATE — NOT AN EVENT. Copy this file to
`docs/governance/record-events/EVT-<opaque-root-id>-v<positive-integer>.md`.
Do not use an `atlas-priority-session*` filename for an event: those names are
reserved for mechanically validated H0 lifecycle artifacts.

This public record contains no participant name, contact detail, signature,
exact private wording, payment detail, or student/classroom data. Private facts
are cited only through opaque custodian attestations.

## Event binding

| Field | Record |
|---|---|
| Event ID and version | `[not supplied]` |
| Event type (`CORRECTION`, `WITHDRAWAL`, `CREDIT CHANGE`, `RESTRICTION`, or `SUPERSESSION`) | `[not supplied]` |
| Root final-record ID and version | `[not supplied]` |
| Root final-record repository path, byte length, and SHA-256 | `[not supplied]` |
| Immediate predecessor event path, version, byte length, and SHA-256; or NONE | `[not supplied]` |
| Effective UTC instant | `[not supplied]` |
| Accountable authority or opaque private-custodian attestation | `[not supplied]` |
| Public de-identified change or restriction | `[not supplied]` |
| Earlier claims or uses affected | `[not supplied]` |
| Current-use disposition (`EFFECTIVE`, `RESTRICTED`, `WITHDRAWN`, or `SUPERSEDED`) | `[not supplied]` |
| Unresolved ambiguity, conflict, or hold | `[not supplied]` |

## Append-only boundary

Complete and close an event; never edit a frozen predecessor. Version 1 binds
the frozen root and states `NONE — first event` for the immediate predecessor.
Every later event binds the immediately preceding event's exact repository path,
version, byte length, and SHA-256. A chain audit—not this event by itself—must
enumerate every branch, reconcile private attestations, and decide which
disposition governs a proposed downstream use. Missing or conflicting links are
a HOLD.
