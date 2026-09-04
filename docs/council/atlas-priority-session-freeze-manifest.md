# Atlas H0 detached freeze manifest

**Status:** DRAFT TEMPLATE — NOT A FREEZE MANIFEST. Copy this file to
`atlas-priority-session-YYYY-MM-DD-freeze-manifest.md` only after participants
have reviewed the exact final bytes of the matching dated H0 session record.
The completed copy uses the exact status `H0 FREEZE MANIFEST COMPLETE`.

This template records mechanics, not authenticity. It does not infer quorum,
authenticate a participant or seat, turn an unranked consultation into H0, or
approve a recommendation. Private identities, review attestations, consent
records, payment records, original need cards, and exact participant wording
stay with the named private custodian.

## H0 freeze binding

| Field | Record |
|---|---|
| H0 record ID and version | `[not supplied]` |
| Final H0 record repository path | `[not supplied]` |
| Final H0 record SHA-256 | `[not supplied]` |
| Final H0 record byte length | `[not supplied]` |
| Upstream final-record and detached-manifest bindings (H0: NONE — no predecessor) | `NONE — H0 has no predecessor` |
| Repository commit and dirty-tree disposition | `[not supplied]` |
| Build/artifact IDs and SHA-256 values | `[not supplied]` |
| Instrument name, version, and SHA-256 | `[not supplied]` |
| Current enacted roster record, version, and SHA-256 | `[not supplied]` |
| Total seated, non-vacant natural persons (count) | `[not supplied]` |
| Seats present (seat + count, no names by default) | `[not supplied]` |
| Natural persons present (count) | `[not supplied]` |
| Practicing-educator natural persons present (count) | `[not supplied]` |
| Seats absent | `[not supplied]` |
| Multi-capacity disclosures | `[not supplied]` |
| Documentation/original-printable content license and accountable decision record | `[not supplied]` |
| Enacted operating-terms exact file binding | `[not supplied]` |
| Constituted-seat authority entries (stable seat/person refs, presence, scope, term, qualification-basis category, and private custodian reference) | `[not supplied]` |
| Participation consent recorded separately | `[not supplied]` |
| Session-opening general quorum result before matter-specific recusals | `[not supplied]` |
| Conflict categories and recusals (de-identified by default) | `[not supplied]` |
| Disputed-recusal resolution subrecords before affected matters, or NONE | `[not supplied]` |
| Withdrawal right and route explained/acknowledged | `[not supplied]` |
| Operative compensation-policy version and effective date; election recorded | `[not supplied]` |
| Private compensation-ledger attestation for rate, UTC quarter, cap reservation, and district-time status | `[not supplied]` |
| Operative compensation-policy exact file binding | `[not supplied]` |
| Private/de-identified note-collection consent recorded | `[not supplied]` |
| Public-record publication consent recorded | `[not supplied]` |
| Recording consent recorded, or no recording | `[not supplied]` |
| Within-cohort identity/affiliation disclosure scope honored; confidentiality/no-contact boundary acknowledged | `[not supplied]` |
| Public-credit choice confirmed | `[not supplied]` |
| Content-contribution choice and exact license/control identity, or none | `[not supplied]` |
| Role-acceptance choice and exact bounded role/control identity, or none | `[not supplied]` |
| Maintainer-appointment choice and exact role/control identity, or none | `[not supplied]` |
| Copyright-stewardship choice and exact transfer/control identity, or none | `[not supplied]` |
| Activity withdrawal, council resignation/vacancy, and non-member role-closure dispositions | `[not supplied]` |
| Decision procedure and quorum rule applied (exact governing record) | `[not supplied]` |
| Per-recommendation matter counts, conflicts/recusals, quorum, and tally denominators | `[not supplied]` |
| Exact material actually reviewed | `[not supplied]` |
| Findings, measurements, holds, dissent, and limitations | `[not supplied]` |
| Requested corrections and accountable owners | `[not supplied]` |
| Participant read-back/review of those exact bytes completed (seat + count, no names by default) | `[not supplied]` |
| Exact-final-byte public-record publication permission reconfirmed after participant review | `[not supplied]` |
| Corrections and dissent incorporated before final hashing | `[not supplied]` |
| Pre-freeze withdrawal/removal requests honored; unresolved requests | `[not supplied]` |
| Applicable protected seats vacant, absent, or recused; each marked NOT REVIEWED — HELD | `[not supplied]` |
| Frozen UTC instant | `[not supplied]` |
| H0 freeze-manifest repository path | `[not supplied]` |
| Append-only correction, withdrawal, credit-change, or supersession record paths; or none at freeze | `[not supplied]` |

## Non-circular and immutable boundary

Close the final H0 record before computing its byte length and SHA-256. Complete
this manifest afterward and then close it too. This manifest deliberately has no
field for its own SHA-256: a self-hash would change the bytes it claims to hash.
Every downstream record computes the SHA-256 of this completed manifest and
records that value alongside the final H0 record SHA-256.

The `Disputed-recusal resolution subrecords before affected matters, or NONE`
field reproduces the final H0 field exactly, in the same order, including every
table-escaped subrecord separator, outcome, count, quorum result, decision,
`read-back=CONFIRMED`, and rationale. It may say `NONE — no disputed recusal`
only when the bound H0 field says exactly that. An `outcome=HELD` subrecord and
its underlying matter remain explicitly held and absent from the recommendation
table; this manifest cannot relabel either one as resolved or recommended.

Before hashing, offer the exact proposed public bytes for participant review,
honor every correction and withdrawal/removal request, and separately
reconfirm public-record publication permission for those exact bytes as
`RECONFIRMED — <exact seats present>`. Changed bytes restart review and
reconfirmation; a refusal leaves the public record open. Record requested
corrections exactly as `NONE — no correction requested` or `RESOLVED — <opaque
or de-identified correction references>; unresolved=NONE`, and confirm the
adjacent incorporation field exactly as `CONFIRMED — all corrections resolved
and dissent preserved in final H0 bytes`.

When both H0 withdrawal-disposition values are `NONE`, record exactly `HONORED
— NONE RECEIVED; unresolved=NONE`. Otherwise record `HONORED —
activity-withdrawal=<the exact H0 value>; council-resignation-vacancy=<the exact
H0 value>; unresolved=NONE`; this makes every non-`NONE` H0 reference explicit
at freeze. Any unresolved correction, withdrawal, removal, resignation, or
vacancy request prevents freeze. Only a request received after freeze becomes
an append-only event.

After completion, never edit either bound file. A correction, withdrawal
marker, prospective credit change, or supersession is a new append-only linked
record. It may govern current use without pretending to erase or mutate the
historical bytes.

The at-freeze correction-path field is a historical snapshot, not an evergreen
inventory. Before any downstream review or release consideration, a fresh
chain audit at the exact candidate repository revision must enumerate and bind
every later public event, reconcile opaque private-custodian attestations, and
state the current effective disposition. A missing, ambiguous, conflicting, or
unresolved event is a HOLD; no downstream record may choose an older convenient
state.

The mechanics validator proves only the linkage among the current bytes
supplied to it. It does not establish the first-complete Git history, prevent a
coordinated rewrite, or choose which later correction is current. Before
publication or release consideration, a human history audit must verify those
claims. Until a versioned correction schema and resolver are adopted,
append-only correction paths remain a manual governance protocol.
