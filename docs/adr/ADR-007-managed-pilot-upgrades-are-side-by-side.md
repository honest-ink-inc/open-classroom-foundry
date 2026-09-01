# ADR-007: Managed pilot upgrades are side-by-side and project preparation is copy-on-write

**Status:** Proposed — advisory only; implementation and operator seams supplied, while district deployment choices and ratification remain open
**Date:** 2026-08-29
**Ratified by:** Pending product owner; District IT owns each deployment plan

Until ratified, this record and its companion runbook describe a design to
evaluate. They do not authorize an upgrade, direct District IT, or replace any
typist, district, accessibility/AAC, multilingual, privacy, or records decision.

## Context

The six-week pilot is expected to receive fixes, but the repository previously
defined neither how one engine build hands a project library to the next nor how
an operator returns to the prior build. That absence is a stop condition, not
permission to improvise on a pilot device.

Three existing decisions bound the answer:

- The implementation plan makes self-updating managed installations a 1.x
  non-goal and assigns Intune, signing, uninstall, and rollback evidence to
  District IT at Gate 3.
- ADR-001 forbids downloaded code and keeps accepted recipe versions installed
  side by side. Opening a project must not silently exchange its pinned recipe.
- ADR-003 makes `.ocfproj` the portable source of truth and promises that every
  released schema remains readable through explicit migration tooling.

There is currently one real project schema: schema 1. The recorded prior engine
identity `0.1.0-dev` and the current engine identity both use it. Therefore the
truthful route today is deterministic schema-1 compatibility preparation. A
package that already carries the current persisted validation and render-profile
context may remain byte-identical. A legacy schema-1 package may instead gain
those exact-document context entries while retaining schema 1; that is a
reported compatibility transform, not a fabricated schema migration.

## Decision

1. **Separate application deployment from project preparation.** The engine may
   validate and prepare project packages. It must not discover, download,
   install, uninstall, replace, launch, sign, version, distribute, or publish an
   application build. Those remain operator or typist acts under repository
   governance.

2. **Address every input and both roots exactly.** A
   `ProjectUpgradeBatchRequest` names one existing `SourceLibraryRoot`, one
   existing `CandidateLibraryRoot`, the exact `TargetEngineVersion`, and an
   ordered, closed inventory of `ProjectUpgradeItem` values. Each item names a
   source-relative path, destination-relative path, source engine version,
   source schema version, and source SHA-256. Both roots must be canonical,
   non-reparse, distinct, and non-overlapping; each item must remain inside its
   declared root after resolution, and the candidate root must already be empty
   when preparation begins. “Current,” “latest,” a glob, an inferred root, a
   path alias, or a directory-wide implicit conversion is not an address. The
   executing binary must reject a target version other than its own identity.

3. **Treat the exact opened source as immutable.** Open each addressed source
   once through a read-only, non-writing handle. Hashing, manifest routing, full
   input validation, compatible transformation, and the final source check must all use that same
   held stream; no validation stage may reopen the path and accidentally inspect
   different bytes. Never replace or rename the source. Write only to a new
   destination through a uniquely named partial in the candidate root, flush it,
   validate the held staged output, and only then atomically rename it to the
   requested destination. A single-item failure deletes only that item's partial
   and never overwrites a completed file.

4. **Validate the whole package before and after writing.** Before copying,
   verify the SHA-256 and manifest engine/schema against the request, then run
   the complete hostile-package-aware validator against the already-open source
   stream. It must enforce normalized safe entry names; entry-count, per-entry,
   total-size, and inflation ceilings; the admitted entry set; required manifest
   fields and `ArtifactPath`; Green-only lane; semantic and module invariants;
   exact agreement among document asset references, manifest asset IDs, asset
   bytes, and provenance records; provenance identity, rights, and content
   hashes; and a safe, self-contained snapshot corresponding to the semantic
   document. Validate the held staged output to the same depth, verify its byte
   hash and version fields, and re-hash the same held source stream before
   completion. Any mismatch produces no completed destination.

5. **Route by schema; never infer a conversion.** Schema 1 to schema 1 retains
   the manifest's writer engine identity and semantic document. It may be a
   byte-identical compatibility copy when the required persisted context is
   already present, or a deterministic compatibility transform that adds the
   versioned exact-document `validation.json` envelope and default
   `render-profile.json` when a legacy package lacks them. The receipt must say
   which occurred. Those entries are compatibility assertions inside a mutable
   package, not signatures: they do not authenticate recipe, lane, purpose,
   protected-seat review, warnings, or output preferences. The application must
   perform its ordinary exact-content review and data-lane preflight and must
   not silently apply those assertions after reopen. Any other source schema fails with “no admitted migration
   route.” A future route must be added deliberately, write a new package,
   validate both schemas, preserve the original, and carry a frozen fixture from
   every source schema it accepts. Changing a schema label without transforming
   its data is forbidden.

6. **Stage the whole library side by side, as one batch.** The old build keeps
   its old project library. `PrepareCompatibleBatchAsync` holds the batch lock
   and processes the ordered inventory sequentially into the initially empty,
   target-version candidate root; item preparation is never concurrent. The
   batch succeeds only if every item succeeds. On any failure or cancellation,
   it stops, removes every destination and partial created by that batch, and
   verifies that the candidate root is empty. If cleanup cannot establish that
   state, preparation fails with a distinct cleanup-residue code and
   content-free residue details, and the candidate root is quarantined rather
   than used. A missing pinned recipe, failed validation, changed hash,
   unavailable migration route, or unaddressed candidate entry therefore stops
   preparation before any application install. A one-project
   `PrepareCompatibleCopyAsync` wrapper retains the same explicit-root,
   relative-path, lock, and cleanup contract. The candidate build, if later
   authorized, opens only the complete prepared root and never writes to the
   rollback library. The operator host supplies an exact candidate-recipe
   inventory derived from the recipe catalogs compiled into its executing
   build. After fully validating each held source, preparation requires an
   ordinal match for both the pinned recipe ID and version; absence fails the
   batch without changing or inferring a recipe identity. That compatibility
   service and its request records are internal to the storage assembly; the
   operator host is the only production assembly in this repository granted
   friend access, so the application has no direct path to self-assert an
   inventory or bypass the host's version-address gate. This is an architectural
   API boundary, not a hostile-code security boundary: the assemblies are
   unsigned, and full-trust reflection or a same-simple-name assembly is outside
   the claim.

7. **Make recovery boring.** Before installation, retain the prior approved
   application package, its deployment metadata, the untouched prior library,
   and the preparation inventory. A failed preparation leaves no completed
   destination. A failed candidate smoke test returns the device to the prior
   application and points it at the untouched prior library; the candidate
   library is evidence, not the rollback source.

8. **Retain content-free evidence and failures.** A successful in-process
   preparation returns only source/target versions, schemas, SHA-256 values, and
   whether bytes were transformed. A failure crosses the preparation boundary
   only as `ProjectUpgradeException` with a stable code and fixed, content-free
   message. Raw filesystem, ZIP, JSON, and package-validation exception text —
   including paths, entry names, asset IDs, or content-derived values — must not
   escape into application diagnostics. Cleanup-residue details may report only
   content-free state such as counts. District IT owns the device-to-inventory
   mapping in its approved system of record.

9. **Require an exact reviewed operator plan and a version-addressed application
   root.** `Foundry.Tools.ProjectUpgradeHost` accepts only a strict schema-1 JSON
   plan containing the two explicit roots, this binary's exact target engine
   identity, and the ordered closed source-package inventory. `review` prints a
   content-free summary, the exact plan SHA-256, a deterministic SHA-256 of the
   executing build's sorted candidate-recipe ID/version inventory, and a
   separate deterministic SHA-256 of that inventory's declarative manifest
   contracts without preparing anything. The declarative fingerprint uses
    `recipe-contract-fingerprint.v2` and binds every `RecipeManifest` field,
    including output schema; local-preprocessing, validator, recipe-owned
    localization-resource, and migration identity lists; editor; renderer;
    supported exports; the ordered §6.6 “Warnings and confirmations” declaration;
    and evaluation version. That combined concern is represented once: every
    manifest warning becomes a fresh required-acknowledgement warning in each
    review through both the shared review-notice path and Module Studio. It is
    not a second declarative confirmation-text list, and acknowledgement is not
    protected-seat approval. The surrounding
    `candidate-recipe-contract-inventory.v2` envelope also binds the fingerprint
   framing identity and every constituent. The engine-owned portable-semantic-
   editor identity is explicitly represented as
   identity-only because it has no `RecipeManifest`. The contract inventory is
   itself length-framed and refuses one ID/version identity that maps to
   different declarative fingerprints. `prepare` requires all three digests
   back, then invokes the whole-batch service once and prints only content-free
   receipt fields or stable failures. Echoing those digests proves review/prepare
   consistency, not approval or provenance. The present first-admission recipe
   manifests declare honest empty lists for local preprocessing, recipe-owned
   localization resources, and migrations; application-wide chrome catalogs do
   not become recipe resources by implication. Real use also requires an immutable,
   independently approved inventory record bound to the exact candidate package
   or source commit. Neither recipe digest binds executable builder, editor,
   renderer, schema, validator, or evaluation implementations or bytes. The host does
   not infer packages or builds and has no install, launch, delete, signing,
   versioning, distribution, or publication operation. The WinForms application
   accepts `--project-library-root <exact-existing-version-addressed-root>` only
   when the absolute root and every ancestor are non-reparse and one literal
   path segment equals the executing engine version. The default local library
   is version-addressed too. Project opens are confined to the configured root;
   save paths already derive exclusively from that root.

An advisory operator sequence accompanying this proposal is
[the managed pilot upgrade runbook](../release/managed-pilot-upgrade-runbook.md).

## Alternatives considered

1. **In-place project rewrite** — rejected. A crash, validation defect, or schema
   error would destroy the only rollback source and contradict ADR-003.
2. **Let the application self-update** — rejected. It is an explicit 1.x
   non-goal, crosses signing and deployment authority, and makes rollback depend
   on application code that may be the failing component.
3. **Copy the library with Explorer or a generic file command and call that a
   migration** — rejected. Copying preserves bytes but supplies no schema,
   structural, provenance, or content-address validation.
4. **Rewrite the schema-1 manifest to name the target engine** — rejected. The
   field records the writer, and changing it would turn a compatible copy into a
   needless mutation while erasing provenance.
5. **Wait until schema 2** — rejected. Schema transformation can wait; a safe
   mid-pilot handoff and rollback boundary cannot.

## Consequences

The engine now has a small, network-free compatibility boundary that can be
tested without performing a release or touching a live library. Frozen,
synthetic schema-1 fixtures must represent every package shape the admitted
prior build wrote — including assets, provenance, and the self-contained
snapshot — rather than only the smallest artifact. Hostile-package, cancellation
cleanup, same-destination concurrency, path-alias, and whole-batch tests are part
of the evidence. Unknown routes are coded failures rather than guessed
successes.

The cost is temporary disk duplication and an intentionally conservative
all-project stop. The seam does not make deployment ready by itself: District IT
must still decide application assignment/detection rules, approved storage,
backup retention, concurrency controls, smoke criteria, and rollback timing;
the privacy/records authority must approve any retention change for real
teacher-managed libraries. Signing, versioning, installation, distribution, and
release remain outside this ADR's executable scope.

The storage upgrade service, request, and receipt types were introduced as
public after the alpha tag and remained public through public-main commit
`96e37e9854861cc2a6098a9fbf05add708732e03`; they are now internal. That
narrowing is an intentional source- and binary-compatibility break from that
public source baseline under the present assumption that
`Foundry.Storage` is an application implementation assembly, not a supported
external SDK. If that assumption changes, an explicit versioned public-API
compatibility decision and evidence are required; this ADR does not silently
claim backward compatibility for external library consumers.

The earlier operator-host, version-addressed-root, and executing-build pinned-
recipe availability implementation stops are closed in code and synthetic
tests. That does not select a real root, review a real inventory, prepare a live
library, install a build, or grant deployment authority. District IT must still
approve and supply the exact plan and storage roots, maintain the external
exclusive-write boundary, run the reviewed host, configure the application
switch, retain rollback evidence, and decide whether the proposal is fit for a
pilot device.

If ratified, this decision remains reversible by a superseding ADR. A particular
upgrade remains reversible until the operator deliberately retires the prior
package and prior library under the district retention decision.
