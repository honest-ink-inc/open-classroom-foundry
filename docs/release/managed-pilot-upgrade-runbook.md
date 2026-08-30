# Managed pilot upgrade runbook

**Scope:** one managed pilot device moving between two explicitly approved
Honest Ink engine builds. This procedure prepares project compatibility and the
rollback boundary; it does not authorize a release, installation, distribution,
signature, version change, or publication.
**Proposed design basis:** [ADR-007](../adr/ADR-007-managed-pilot-upgrades-are-side-by-side.md)

This runbook is advisory while ADR-007 remains Proposed. It is an evaluation
artifact, not deployment authority. No step below waives a typist act, a
District IT decision, or an accessibility/AAC, multilingual, privacy, or records
seat's acceptance gate.

## Authorities and stop conditions

| Act | Owner | Stop condition |
|---|---|---|
| Select the exact source and candidate application packages | Typist / release owner | Either package, checksum, signature, or provenance is absent |
| Approve Intune assignment, detection, supersedence, uninstall, and rollback behavior | District IT/security | No tested prior-package reinstall path or device cohort |
| Approve backup location, access, and retention for a real library | District privacy/records authority | Storage or retention is undecided |
| Prepare and validate `.ocfproj` copies | Engine compatibility host under operator control, if this proposal is ratified | Either root or any package fails its exact address, the candidate root is not initially empty, or no admitted schema route exists |
| Install, uninstall, or return a device to the prior build | District IT / typist | Never performed by the engine or an unattended repository agent |

The accessibility/AAC and multilingual seats do not waive this procedure. If a
candidate changes the workflow or content their territories govern, their
ordinary acceptance gates still apply before deployment.

Stop the upgrade immediately if any of these is true:

- the live application is open or another process can write the project library;
- the source or candidate application is described as “latest” rather than by
  an immutable version and checksum;
- the old application package or old library cannot be retained through the
  rollback window;
- the exact candidate root has not been approved for the candidate build's
  `--project-library-root` switch, does not already exist, crosses a reparse
  point, or lacks one literal path segment equal to that build's engine version;
- the strict operator plan has not been reviewed with
  `Foundry.Tools.ProjectUpgradeHost review`, or the exact SHA-256 it reports is
  not supplied unchanged to the later `prepare` invocation;
- the canonical source and candidate roots are not explicit, distinct,
  non-overlapping, and confined against link or path escape, or the candidate
  root is not empty before the batch begins;
- a project package has no recorded source engine, source schema, or SHA-256;
- the candidate lacks one of the project's pinned recipe versions;
- any validation, migration, smoke, accessibility, privacy, or policy check is
  incomplete.

## Before the maintenance window

District IT records, in its approved system of record:

1. device and cohort identity;
2. source application version, package SHA-256, signature result, and deployment
   identifier;
3. candidate application version, package SHA-256, signature result, and
   deployment identifier;
4. exact canonical, existing, non-reparse prior-library root and an explicit,
   existing, non-reparse, distinct, non-overlapping candidate root containing
   the literal candidate engine version in its path; create that candidate root
   empty before the window and record the empty-root check;
5. for every source `.ocfproj`, its relative name, manifest engine version,
   manifest schema version, and SHA-256;
6. pinned recipe versions required by those projects;
7. the approved rollback owner, decision deadline, evidence location, and the
   time after which the prior copy may be retired.

Do not put project paths, teacher-authored content, package-controlled entry
names or identifiers, or package contents in application diagnostics. Record
only the `ProjectUpgradeException` stable failure code, fixed content-free
message, and any explicitly content-free cleanup counts. In particular, do not
forward raw filesystem, ZIP, JSON, or validation exception text. The district
inventory may map devices to paths under its own access and retention controls.

## Prepare the candidate library

1. Close Honest Ink and obtain the district-approved exclusive-write boundary
   for both the source and candidate roots. Do not rely on a teacher merely
   promising not to open the source library, or on an in-process batch lock to
   exclude an outside writer.
2. Verify the recorded application package checksums and signatures using the
   district's approved deployment tooling. A repository build result is not a
   signature and not installation authority.
3. Resolve and verify the declared source and candidate roots. Both must already
   exist and neither may be a reparse point, the other root, or an ancestor of
   the other. Every addressed source and destination must remain inside its
   declared root after canonicalization. Verify the candidate root is empty
   immediately before invoking preparation.
4. Construct one strict schema-1 operator JSON plan, following
   [`managed-pilot-upgrade-plan.schema.json`](managed-pilot-upgrade-plan.schema.json), with the exact
   `sourceLibraryRoot`, `candidateLibraryRoot`, `targetEngineVersion`, and an
   ordered, closed `projects` inventory. For each source project, include one
   object with its exact `sourceRelativePath`, nonexistent
   `destinationRelativePath`, `sourceEngineVersion`, `sourceSchemaVersion`, and
   `sourceSha256`. Reject duplicate sources, duplicate destinations, aliases,
   globs, absolute item paths, unaddressed source packages, and unaddressed
   candidate entries. Keep a real inventory only in District IT's approved
   system; do not paste it into repository diagnostics or logs.
5. Run `Foundry.Tools.ProjectUpgradeHost review --plan <absolute-plan-file>`.
   It performs no preparation and prints only schema, target engine, closed
   project count, and the exact plan SHA-256. Compare those fields with the
   approved inventory. Then run `Foundry.Tools.ProjectUpgradeHost prepare
   --plan <the-same-absolute-plan-file> --confirm-plan-sha256 <reviewed-SHA-256>`.
   The host rereads the exact bytes, refuses a changed digest, and invokes
   `OcfprojUpgradeService.PrepareCompatibleBatchAsync` exactly once. Its batch
   lock serializes preparation and it processes inventory items sequentially.
   For each item, schema-1 input is hashed, routed, fully validated, compatibly
   prepared, and revalidated through held streams; no stage reopens the source
   path and substitutes different bytes. Preparation may retain an already
   current package byte for byte or deterministically add the required
   exact-document validation envelope and default render profile to a legacy
   schema-1 package. An unrecognized schema stops with a coded no-route failure;
   do not edit the manifest or retry under a different declared version.
6. Treat preparation as all-or-nothing. The host does not return a successful
   batch result or publish its receipts until every item succeeds. On a failure
   or cancellation it stops, removes all destinations and partials created by
   that batch, and proves the candidate root empty. A cleanup failure is a
   distinct `ProjectUpgradeException` cleanup-residue code with content-free
   details and quarantines that root; it is never accepted or reused by
   assumption.
7. After whole-batch success, record each content-free `ProjectUpgradeReceipt`.
   When `PackageTransformed` is false, verify source and output hashes match.
   When it is true, verify the source hash remains unchanged, the output hash
   matches the prepared package, and the admitted validation/profile context is
   present and internally consistent with the exact document. Do not call that
   context provenance or approval: it is unkeyed compatibility metadata inside
   the same mutable package. In both cases, verify the receipt
   count equals the closed inventory count.
8. Reinventory both roots. Every source package must retain its original hash;
   every candidate package must have a receipt; no `*.upgrade-partial` file may
   remain; and the candidate root must contain no unaddressed entry.
9. Make both inventories read-only evidence before any application deployment.
   If one project fails or the all-or-nothing cleanup cannot be proved, keep the
   current application/library pair in service.

Each item uses a sibling partial file and a no-overwrite atomic rename. The
whole-batch boundary additionally removes earlier destinations it created if a
later item fails. It never deletes or rewrites a source. A completed candidate
is never used as rollback source; the untouched source remains authoritative.

## Operator-held deployment and acceptance

These steps are intentionally not automated in the repository:

1. District IT applies the already approved candidate package only to the named
   pilot cohort, using its tested detection and supersedence rules.
2. Launch the candidate build with `--project-library-root
   <exact-existing-version-addressed-candidate-root>`. The application refuses
   a relative, missing, reparse, or wrong-version-segment root before opening a
   form and confines project opens and saves to the configured root. Never point
   it at the prior library. Supplying this switch is configuration, not install
   or deployment authority.
3. With network inference disabled first, open one copied project from each
   module/recipe represented in the inventory. Confirm semantic content,
   the teacher's exact-content data-lane preflight, accessibility tree,
   language/reading order where applicable, review gate,
   and an approved non-distributed test export.
4. Confirm policy loads fail closed as designed and that the candidate writes
   only inside its candidate library. Do not send correspondence, distribute an
   artifact, or publish during the smoke test.
5. District IT and the pilot operator sign the acceptance record. Only then may
   the candidate cohort resume work. Keep the prior package and library for the
   entire approved rollback window.

## Rollback and recovery

Rollback is required if the candidate cannot launch, cannot open every sampled
project, changes semantic content, loses pinned-recipe behavior, breaks a seat's
accepted workflow, writes outside its candidate library, or fails any policy or
export smoke.

1. Stop the candidate application and prevent further writes to its library.
2. District IT removes or supersedes the candidate according to its approved
   deployment plan and restores the exact prior application package. These are
   human-authorized installation acts.
3. Point the restored application at the untouched prior library; never copy
   candidate files back over it.
4. Recompute hashes for the prior library and compare them with the pre-window
   inventory before reopening work.
5. Preserve candidate package, candidate library, receipts, and failure details
   under the approved incident-retention rule. Record only stable failure codes,
   fixed content-free messages, and approved content-free cleanup counts. Do not
   diagnose from a single sighting.
6. Reopen the prior build offline and repeat the minimal project/open/review
   smoke. Escalate rather than improvising if the prior pair does not match its
   inventory.

The rollback window ends only when District IT and the privacy/records authority
approve retirement of the prior application and library under written retention
rules. Elapsed time alone is not approval.
