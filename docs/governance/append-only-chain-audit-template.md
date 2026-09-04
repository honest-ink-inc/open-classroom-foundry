# Append-only governance chain audit

**Status:** DRAFT TEMPLATE — NOT AN AUDIT. Copy this file to
`docs/governance/chain-audits/AUDIT-<opaque-scope-id>-v<positive-integer>.md`.
The completed audit is closed before a dependent record or act binds its path,
version, byte length, and SHA-256; it therefore contains no circular self-hash.

## Audit header

| Field | Record |
|---|---|
| Chain-audit ID and version | `[not supplied]` |
| Audit UTC instant | `[not supplied]` |
| Exact candidate repository revision and dirty-tree disposition | `[not supplied]` |
| Proposed dependent record, decision, use, or outward act | `[not supplied]` |
| Frozen roots and detached manifests in scope, each path/version/byte length/SHA-256 | `[not supplied]` |
| Exact enacted operating-terms path/version/byte length/SHA-256/source commit | `[not supplied]` |
| Exact operative compensation-policy path/version/byte length/SHA-256/source commit | `[not supplied]` |
| Fresh opaque private-custodian attestation and UTC instant | `[not supplied]` |
| Human first-complete/history audit evidence | `[not supplied]` |

## Public event enumeration

Use one row per discovered event, in causal order. If there is no public event,
record one `NONE` row whose search boundary is the exact candidate revision.

| Root ID | Event ID/version or NONE | Type | Repository path | Byte length | SHA-256 | Immediate predecessor binding | Effective UTC | Disposition |
|---|---|---|---|---:|---|---|---|---|
| | | | | | | | | |

## Private event reconciliation

| Root ID | Opaque custodian attestation or NONE | Effective UTC | Disposition/hold |
|---|---|---|---|
| | | | |

## Resolved current state

| Root ID | Current effective record/event | Current-use disposition | Unresolved chain holds |
|---|---|---|---|
| | | | |

Every root must resolve to exactly one current effective disposition and
`unresolved-chain-holds=NONE` before the proposed dependent use may proceed.
A missing link, branch, later event, stale private attestation, ambiguity,
conflict, restriction, or withdrawal is a HOLD. A later public or private event,
or a relevant source change after the recorded cutoff, stales this audit even
when the proposed actor selects an older commit.
