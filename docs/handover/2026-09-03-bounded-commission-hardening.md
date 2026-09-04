# Bounded commission hardening — prepared law, exact seams, and the unopened council

**Prepared:** 3 September 2026

**Branch:** `codex/bounded-commission-hardening`

**Base:** `d851c6c2611147a34243b38b874831ccded54f3e` (`main`)

**State:** current local repository-state handover. The product tree may be
committed under the commission's later instruction if its audit closes, but it
has not been pushed, published, released, tagged, signed, installed,
distributed, filed, or sent to anyone. Hosted evidence does not exist for this
tree.

## Disposition of the commission

| Commission element | Present disposition |
|---|---|
| Licensing, consent, withdrawal, quorum, conflict/recusal, and seat authority | **Prepared, not enacted.** The unsupported content-license grant is withdrawn; the code/content boundary is corrected; proposed first-cohort operating terms separate participation, private/de-identified note collection, public-record publication, recording, credit, compensation election, contribution, appointment, and copyright choices; treat withdrawal as an independently explained right; and define constituted-seat authority, quorum, matter-specific recusal, protected-seat limits, vacancies, records, and enactment. The content license is still an accountable human choice, no participant has assented, and the proposed terms create no seat or quorum. |
| Public/current-state claims, NVDA and print instruments, historical evidence bundle | **Repository repairs implemented; no publication.** The public status now names the exact evidence baseline and open human holds. NVDA step 13 begins with a fresh Green preflight and Gate B; its public evidence route permits only participant-reviewed de-identified factual paraphrases after separate publication consent, while exact wording and originals remain private absent the separate content-license and contribution assent. The print instrument uses short-edge duplex for the imposed booklet and separates mechanical observations from protected judgments. The historical bundle names only the tracked HTML samples and no longer claims a tracked `.ocfproj`. |
| `ApprovedArtifact`, lanes and sinks, cloud structured output | **Implemented and locally testable.** Details are under “Engine hardening.” |
| Schema/version/migration | **Decision-ready, not ratified.** ADR-010 is Proposed with its four authority decision fields intentionally blank and three fixed deferred/non-effect rows deferring the exact first schema-2 engine version and preserving district/records and release holds. It keeps schema 1 immutable and hash-less, proposes a closed schema 2 with mandatory recipe-contract hash and copy-on-write re-admission, and makes version alignment a separate exact act after implementation evidence. No schema-2 writer or migration was built. |
| Needs-first council and protected reviews | **Not begun.** A blank H0–H7 control ledger fixes the required order and exact freeze fields. It records no appointment, attendance, quorum, consent, finding, recommendation, pilot, or review. |
| Republishing and release rites | **Held for the typist.** The ledger permits consideration only after all eight records are honestly frozen, holds and applicable Gate 0–5 rows are dispositioned, schema/version is ratified, and the pre-publication history check is complete. This commission performed none of those outward acts. |

## Governance and truth repairs

The [proposed operating terms](../council/draft-first-cohort-operating-terms.md)
are deliberately non-operative. Initial enactment cannot borrow the quorum rule
it is trying to create: each proposed initial seat needs an exact product-owner
offer, candidate acceptance, bounded scope, and recorded
qualification/authentication basis; all candidates then accept one exact terms
version in the same product-owner enactment record that constitutes the seats.
That record executes and binds the existing `GOVERNANCE.md` council-formation
clause and canonical blob; a term that changes or conflicts with governance is
held for a separately ratified numbered ADR and any then-required
second-maintainer concurrence.
A later seat follows the enacted recommendation-and-appointment route. Absence,
vacancy, silence, abstention, recusal, automation, bare self-acceptance, or
another person's expertise never supplies protected-seat assent.

`GOVERNANCE.md` retains the same canonical Git content and blob identity as the
inherited `main` baseline; checkout line-ending representation is not an
identity claim. Its amendment rule requires a ratified ADR, which this
commission cannot invent. Its historical sentence that
“a ratified compensation policy exists” must be read with the current
[compensation-policy audit](../council/compensation-policy.md): only the recorded
amount/phase history exists, while participant use and replacement terms are
held for a typist-approved corrective proposal and valid first-cohort enactment
of that same version. Changing that canonical sentence itself
remains a human-ratified governance act.

The [stage-gate register](../governance/stage-gate-disposition-register.md)
consolidates all 44 Gate 0–5 criteria without closing one. Each row carries an
accountable owner, authority class, evidence state, dependency, date, and
supersession field. Machine evidence cannot close a human, protected, district,
counsel, or typist remainder.

The [ordered human-review ledger](../council/bounded-commission-review-ledger.md)
requires H0 needs-first council, H1 AAC/SLP, H2 curriculum, H3 multilingual and
family communication, H4 accessibility/AT, H5 rights/OER, H6 physical print,
and H7 teacher pilot, in that order. Every row is **NOT BEGUN**. Each final
participant-reviewed record closes before a detached, non-self-hashing freeze
manifest binds its path, version, byte length, and SHA-256. Each downstream row
must bind both the preceding final-record digest and its completed-manifest
digest; a frozen HOLD remains a hold rather than becoming assent.

The participant-facing instruments remain held. They contain no real learner
material, participant identity, consent record, compensation record, private
contact detail, or blind-study key. The September invitation-update letters remain
drafts and were not sent during this commission; earlier invitations and
acceptances remain typist-reported historical facts, not independently
authenticated here.

## Engine hardening

### Approval and exact review evidence

`ApprovedArtifact` has one private constructor and one Domain-owned validating
factory. The sole production call into the mint is a private adapter nested in
`ReviewSession`; a compiled-IL inventory fails if a production friend assembly
adds another direct call, virtual call, or method-group reference. Approval:

- reruns and freezes both recipe and structural findings, refuses null,
  incomplete, undefined-severity, or blocking findings, and confirms that an
  injected approval gate returned the exact revision, reviewer identity,
  instant, findings, and reviewed asset bindings;
- snapshots every referenced image's owned bytes, MIME type, content SHA-256,
  and a length-framed digest over the full provenance record at Gate B; and
- requires every later renderer or project store to recapture the caller's
  catalog and match those exact bindings before output. Missing or duplicate
  reviewed bindings, or referenced assets with mutated bytes, MIME type, or
  provenance, fail closed; unrelated reusable-catalog entries are ignored.

The unapproved visual preview moved into the small `Foundry.ReviewPreview`
assembly. Only that assembly can call the raw review renderer, only the desktop
review surface can call the preview adapter, and compiled/source inventories
freeze both relationships. `PortableProjectSnapshot` is now a read-only exact
correspondence verifier; the public raw rewrite route was removed.

### Lane-to-sink enforcement

Undefined lane values and undefined or malformed classification bases fail at
capture and normalization. Only an explicitly provisional unknown Amber input
may be resolved by teacher confirmation; an established or automatically
escalated lane may be retained or raised but never lowered. Restricted capture or normalization blocks the session and
requests purge; cancellation, late completion, tentative references, failed
release, and incomplete purge retain explicit fail-closed states and tests.

Every renderer, exporter, printer, and project-library store accepts an
`ApprovedArtifact`. Green is the only lane admitted by the current production
build. Amber has an explicit opaque capability parameter but deliberately has
no production issuer or effective verifier; even a reflection-created instance
cannot authorize output until a separate request-bound district design is
ratified. Restricted and undefined lanes are refused.

Composite export and print implementations demand the caller's top-level
operation once and carry only a narrow internal render delegation. The raw
physical-print and print-fallback helpers have exactly one compiled production
caller. Each desktop export and injected print-view coordinator demands its own
operation before rendering, writing, changing in-progress state, or invoking
the supplied delegate. A negative UI contract proves an Amber print-view
delegate is never invoked.

### Cloud structured output

The Azure adapter refuses a request before token acquisition or network egress
unless the output-schema ID maps to a registered, locally supported strict JSON
Schema and a provider-safe schema name. It sends `response_format.type =
json_schema` with `strict: true`, accepts only the bounded supported subset, and
requires every object property, `additionalProperties: false`, bounded nesting,
and bounded total properties. Conservative cross-deployment ceilings also
refuse more than 500 enum values, more than 15,000 aggregate characters across
provider-counted property/definition names and string enum/constant values, or
more than 7,500 string-enum characters when one enum has over 250 values.
Unsupported keywords, malformed schemas, duplicates, invalid bounds, missing
registration, and over-limit schemas are capability refusals before token
acquisition or HTTP egress.

Successful provider bodies still pass independent bounded UTF-8 and JSON
parsing, envelope/finish/refusal checks, duplicate-name rejection, and complete
local validation against the same schema, including deterministic bounds that
the provider schema omits. A mismatch cannot become draft input.

## Schema and version disposition

[ADR-010](../adr/ADR-010-project-schema-2-binds-an-exact-recipe-contract.md)
is the bounded commission's implementable disposition artifact, not a
ratification. Its four required authority fields—the product-owner statement,
instant, acceptance or rejection of all ten clauses, and ADR-007
disposition—remain `[not supplied]`. The exact first schema-2 engine version is
deliberately deferred until implementation-candidate evidence can support a
separate exact version act. Schema 1 therefore remains the only implemented
format, readable but release-blocked by its absent `recipeHash`. Engine,
assembly, file, and tag identities remain intentionally unreconciled until that
separate authorized version act.

## Local evidence

This section is completed only from the final tree after the closing rites. It
must preserve any red run with its test name and message instead of rewriting
history around a later green run.

- Release build: `[pending final close]`
- Format fix and verification: `[pending final close]`
- Bounded full-suite run 1: `[pending final close]`
- Bounded full-suite stability run 2: `[pending final close]`
- Two-producer SampleGenerator comparison: `[pending final close]`
- Independent integrated and sink-inventory audit: `[pending final close]`

## Evidence manifest

These hashes will bind the decision and control artifacts after formatting and
the last audit correction. The manifest records files, not a release package.

| Artifact | SHA-256 |
|---|---|
| Proposed first-cohort operating terms | `[pending final close]` |
| Proposed corrective compensation policy | `[pending final close]` |
| Stage-gate disposition register | `[pending final close]` |
| Ordered H0–H7 review ledger | `[pending final close]` |
| Proposed ADR-010 | `[pending final close]` |
| NVDA walkthrough | `[pending final close]` |
| Physical-print inspection instrument | `[pending final close]` |

## Deviations and open authority

There is no substitution hidden in this close:

- no content/documentation license was selected and no contribution terms were
  accepted;
- no first cohort was seated and its proposed operating terms were not enacted;
- no consent was obtained, participant record frozen, council convened,
  protected-seat review performed, or teacher pilot begun;
- no schema or version decision was ratified and no migration was performed;
- no real managed device, district configuration, physical printer, NVDA user,
  or participant was used by the machine evidence;
- no public or hosted claim was created for this branch; and
- no push, merge, correspondence, publication, version, tag, signature,
  installation, distribution, filing, or release occurred.

The next lawful sequence is to finish the private entry conditions, enact the
operating terms and contribution boundaries through their accountable human
routes, ratify ADR-010 as written through its four authority fields or enact a
superseding exact schema/version/migration disposition, then freeze H0 and H1
through H6 in order. Rejection alone does not satisfy the pre-H0 disposition
dependency. H7 participant tasks begin only after
every applicable predecessor hold is explicitly closed; its record is then
frozen. Only after H0–H7 and all remaining holds are honestly dispositioned may
the typist be asked whether to begin the repository's pre-publication and
release rites.
