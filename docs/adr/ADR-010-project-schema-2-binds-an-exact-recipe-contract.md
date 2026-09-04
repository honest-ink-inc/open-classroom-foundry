# ADR-010: Project schema 2 binds a saved project to an exact recipe contract

**Status:** Proposed — decision-ready, not ratified; no schema-2 writer or migration is authorized
**Date:** 2026-09-03
**Ratified by:** Pending product owner; District IT and records/privacy retain their deployment and retention gates

Until the product owner supplies the exact disposition record at the end of this
document, this ADR is a design for review. It does not change the current
project schema, engine identity, assembly version, tag, package, or release. It
does not authorize migration of a real library or installation on a managed
device.

## Context

ADR-003 adopts the implementation plan's required `.ocfproj` manifest fields,
including `recipeId`, `recipeVersion`, and `recipeHash`. The only implemented
project schema is schema 1. Its closed manifest has recipe ID and version but no
recipe hash. The ratified recipe-identity disposition therefore keeps schema 1
as a deliberately legacy, hash-less format and treats the omission as a release
stop.

The engine already computes `recipe-contract-fingerprint.v2` for every
manifest-backed recipe. That fingerprint binds the complete declarative
`RecipeManifest`; it does not bind executable builder, editor, validator, or
renderer bytes. A project hash must make that limitation explicit instead of
claiming executable reproducibility it cannot prove.

Schema 1 cannot be backfilled safely. The same recipe ID/version and engine
version appeared in public development states before the first-admission freeze,
so a schema-1 manifest does not identify which effective recipe contract wrote
it. Relabeling or hashing it in place would manufacture provenance.

Version identities also currently disagree: `EngineIdentity.EngineVersion` is
`0.7.0-alpha`, while default assembly/file metadata is `1.0.0.0`, and the
unsigned `v0.7.0-alpha` tag points to an older development snapshot. A schema
decision must not silently pretend that mismatch is a release decision.

If no decision is made, schema 1 remains readable and the release stop remains
open. That is safer than an inferred migration.

## Proposed decision

If accepted, the decision is all of the following; accepting only a fragment is
not ratification.

1. **Keep schema 1 immutable, readable, and hash-less.** Its closed property set
   does not gain `recipeHash`. Every admitted reader continues to validate the
   entire package and its self-contained snapshot. No writer calls a schema-1
   package release-complete.

2. **Make `recipeHash` mandatory in project schema 2.** It is the uppercase
   64-hex SHA-256 emitted by `recipe-contract-fingerprint.v2` for the exact
   manifest-backed recipe selected at save time. The project also retains the
   exact recipe ID and version. The current `portable-semantic-editor` is
   identity-only and therefore cannot write schema 2. Before re-admission is
   implemented, that identity must become one ordinary compiled
   `RecipeManifest`, with its allowed inputs, maximum Green lane, semantic
   output schema, validator, editor, renderer, exports, warnings, and evaluation
   identity explicitly declared and first-admission-frozen. Its hash then uses
   the same framing as every other recipe; no special sentinel or unrecorded
   alternate hash is admitted. A portable edit never copies an untrusted
   package-authored recipe selector.

3. **State what the hash proves.** It binds the exact declarative recipe
   contract: identity, license, minimum engine, purposes, allowed input kinds,
   maximum lane, provider capabilities, output schema,
   validator/editor/renderer/export identities, warnings, preprocessing,
   localization resources, migrations, and evaluation identity. It does not
   attest implementation bytes, rights review,
   translation quality, protected-seat approval, teacher approval, or release
   status. Those claims require their own evidence.

4. **Replace schema 1's abbreviated manifest shape deliberately.** Schema 2
   requires this closed top-level set: `schemaVersion`, `projectId`, `moduleId`,
   `moduleVersion`, `recipeId`, `recipeVersion`, `recipeHash`, `createdUtc`,
   `modifiedUtc`, `dataLane`, `retentionMode`, `sourceLocales`, `outputLocales`,
   `engineVersion`, `artifactPath`, `assets`, and `purpose`.

   `sourceLocales` and `outputLocales` are non-null, bounded, duplicate-free
   arrays of normalized language tags in first semantic appearance order. A
   re-admitted portable document derives source locales from its document
   language and each bilingual source segment, and output locales from each
   bilingual target segment; it never copies the legacy singular locale fields
   as authority. `assets` is an ordinal asset-ID-sorted array whose closed
   records contain `assetId`, `assetPath`, `provenancePath`, `contentSha256`, and
   `provenanceSha256`. Every path and digest must agree with the fully validated
   package entries. Schema 2 does not retain the singular `sourceLocale`,
   `outputLocale`, or bare `assetIds` properties. Exact bounds and normalization
   vectors become part of the published schema and frozen fixtures before any
   writer is admitted.

   `moduleId` and `moduleVersion` are not package-authored labels and are not
   inferred by copying `recipeVersion`. Before a schema-2 writer is admitted,
   the engine must carry a versioned, first-admission-frozen module/recipe
   binding registry. Each binding names one exact `(moduleId, moduleVersion,
   recipeId, recipeVersion, recipeHash)` tuple. A writer resolves all five
   values from that registry through an opaque engine-owned binding; it does not
   accept them as independent caller strings. A reader requires the complete
   tuple to match one admitted current or historical binding before typed edit
   or re-save. An unavailable or mismatched binding retains the same
   snapshot-only, no-substitution behavior as an unavailable recipe. The future
   portable-semantic manifest requires an ordinary binding in this registry as
   well. The current schema-1 convention that silently writes `moduleVersion`
   from `recipeVersion` is legacy behavior, not the schema-2 authority rule.

5. **Fail closed when the exact contract is unavailable.** A schema-2 save
   requires one unambiguous compiled recipe ID/version whose computed hash
   equals the manifest hash. A schema-2 open may preserve and display the
   self-contained snapshot when editing support is unavailable, but it must not
   substitute a recipe or allow a typed-recipe re-save. Hash mismatch is
   corruption or an unsupported contract, never a warning that can be checked
   away.

6. **Do not auto-migrate schema 1.** A schema-1 project may enter schema 2 only
   through copy-on-write re-admission: retain the original package byte for
   byte; open it through the frozen schema-1 reader; create a fresh unapproved
   portable-semantic draft; run the current Green preflight and complete
   validation; require a new Gate B review of the exact document; and save a new
   schema-2 package under the newly manifest-backed and separately frozen
   portable-semantic contract. The receipt
   says `re-admitted`, not `upgraded in place`, and links only content-free
   source and destination hashes. If review, validation, hashing, or final
   package validation fails, no destination is admitted.

7. **Keep compatibility and deployment separate.** ADR-007's side-by-side,
   copy-on-write preparation remains the only proposed managed-library route.
   Ratifying this ADR does not ratify ADR-007. Ratifying ADR-007 does not
   authorize a real plan, retention change, installation, rollout, or rollback;
   District IT and records/privacy own those decisions.

8. **Align version identities only through a separate exact version act.** The
   first schema-2 writer must be bound to one exact engine version chosen in a
   release-candidate record. On that exact tree, package, assembly, file, and
   informational versions must derive from the same declared engine identity,
   and the tag must be created only after the release rites. This ADR chooses no engine version and moves no existing tag.

9. **Define the ADR relationship explicitly.** If accepted, this ADR partially
   supersedes only ADR-003's schema-1 manifest-field and migration-detail
   clauses. ADR-003's open-package, portable-source-of-truth, semantic-document,
   safe-path, self-contained-snapshot, atomic-save, and fresh-review decisions
   remain Accepted. The ADR index must record that partial supersession in the
   same ratification change; Proposed status changes nothing today.

10. **Prove both directions before removing the stop.** Required evidence is:
   frozen packages from every admitted schema-1 shape; schema-1 backward-open
   tests; schema-2 round trips; missing/unknown/malformed/hash-mismatch refusals;
   copy-on-write re-admission and cancellation cleanup; byte-identical source
   preservation; deterministic schema-2 output; exact snapshot correspondence;
   unavailable-recipe read-only behavior; and rollback to the untouched prior
   package/library. Human, district, signing, installer, and release rows remain
   independent.

## Alternatives considered

1. **Add `recipeHash` under the schema-1 label** — rejected. It changes a closed
   accepted schema without routing or migration evidence.
2. **Infer the hash from recipe ID/version** — rejected. Public development
   states reused tuples before first admission, so the inference can be false.
3. **Hash the whole application binary** — rejected as the manifest field's
   meaning. It would entangle platform-specific packaging with the portable
   declarative recipe contract and still would not prove protected review.
   Signed package provenance remains separate release evidence.
4. **Carry `legacy-unknown` as a schema-2 hash** — rejected. A sentinel would
   make a mandatory integrity field optional in disguise.
5. **Rewrite schema-1 packages automatically on open** — rejected. It destroys
   rollback provenance, bypasses fresh teacher review, and contradicts ADR-003.
6. **Leave projects permanently on schema 1** — safe for current reading but
   rejected as a release destination because it never satisfies the accepted
   manifest contract.

## Consequences

The route is deliberately conservative. Existing schema-1 packages remain
open and human-legible; none is falsely blessed with an inferred recipe hash.
Schema 2 gains a precise declarative-contract binding and a fail-closed editing
route. The cost is that legacy projects require a fresh teacher review before a
new editable package can claim the current recipe contract, and some projects
may remain snapshot-readable only until a compatible editor exists.

Implementation requires a version-routed manifest model rather than adding a
nullable property to the current record, an exact recipe-contract resolver at
save/open boundaries, new hostile-package fixtures, deterministic migration
receipts, and a frozen legacy reader. Those changes are not authorized by this
Proposed record.

The decision is reversible only by a superseding ADR. Individual re-admissions
remain reversible because the original schema-1 package is never changed.

## Ratification decision fields — intentionally blank; fixed deferred/non-effects recorded

The first four authority decision fields below must be completed by the named
authority for this ADR to be ratified. The final three rows are fixed rather
than delegated decisions: the exact first schema-2 engine version is deferred
until an implementation candidate supplies evidence for a separate exact
version act, and neither district/records nor release authority transfers
through this ADR. A test may protect the shape of this record, but no automation
may fill the authority decision fields from surrounding prose, elapsed time, or
passing tests.

| Field | Required disposition |
|---|---|
| Product-owner decision | `[not supplied]` |
| Decision instant | `[not supplied]` |
| Exact statement accepting or rejecting all ten clauses | `[not supplied]` |
| ADR-007 disposition | `[not supplied separately]` |
| Exact first schema-2 engine version | `Deferred — required by a separate exact version act after implementation evidence; no version is authorized here` |
| District/records effect | `None; each real deployment and retention plan remains separately held` |
| Release effect | `None; all release and publication stops remain open` |
