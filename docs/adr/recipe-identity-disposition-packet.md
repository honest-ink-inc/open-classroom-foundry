# Recipe identity disposition packet

**Status:** RATIFIED — OPTION A

**Measured:** 1 September 2026

**Decision owner:** product owner / typist

**Governing records:** [ADR-001](ADR-001-one-foundry-bounded-recipes.md),
[ADR-003](ADR-003-open-ocfproj-package.md), and the still-Proposed
[ADR-007](ADR-007-managed-pilot-upgrades-are-side-by-side.md)

This is not an ADR and does not reopen ADR-001. ADR-001 is already Accepted: it
requires bounded, versioned recipes, pins projects to exact recipe versions, and
keeps a new recipe version beside the old one until a project-specific migration
is accepted. This packet records the product owner's explicit Option-A
disposition: every recipe identity in the public alpha-tag snapshot is
pre-admission development and will be frozen for the first time at the exact
candidate commit recorded by the immediately following ratification-record
commit.

The written decision names every one of the 23 outgoing rows through the complete
record below; no broad forge instruction, elapsed time, test result, public-name
change, or absence of a GitHub Release supplied that choice. This local-only
transitional state exists solely because a Git commit cannot contain its own
SHA-1. It must not be pushed until the next record-only commit replaces the
pending marker with C1's exact hash and sets the status to `RATIFIED`.

## Why a decision is required

The exact outgoing snapshot invoked here is commit
`a64abead04e56085b82ac632180ca1a362eb8bc3`, reached by unsigned annotated tag
object `380a0e5c3b768bdaa655825b35a25307fe89c0e5` (`v0.7.0-alpha`). It contains 23
recipe manifests. The current executing tree contains the same 23 identities plus
15 candidate-only press identities, for 38 manifest-backed identities total; the
portable-semantic-editor compatibility identity has no manifest and is not a
recipe row. The candidate side is still a mutable working tree, so a ratified
record must replace it with the exact later freeze commit.

An exhaustive property comparison found direct same-ID/version manifest drift in
11 of the 23 outgoing identities. Known builder, validator, output, or evaluation
changes affect at least four more. The other rows remain in scope because a
matching manifest fingerprint does **not** establish executable equivalence: it
does not bind builder, editor, or renderer bytes, schema definitions, validator
implementations, or evaluation corpora, and shared infrastructure changed after
the tag. “No direct manifest delta measured” therefore is not a conclusion that
the effective contract is unchanged.

At the 1 September measurement, `RecipeManifest` did not represent three fields
required by implementation plan §6.6: local preprocessing, localization
resources, and migration IDs. The measured v1 fingerprint therefore did **not**
bind them. The explicit Option-A decision authorizes their addition before first
admission. The candidate now represents all three as exact lists, with honest
empty lists where no registered identity exists, and
`recipe-contract-fingerprint.v2` binds them. The historical v1 measurements below
remain evidence of the pre-disposition tree; they are not relabeled as v2.

Plan §6.6 says “Warnings and confirmations” as one combined recipe concern. The
manifest represents that concern once, through its ordered `Warnings` list:
every declared warning is materialized as a fresh `ValidationIssue` with
`Severity=Warning` and `RequiresAcknowledgement=true` for each new review. Both
the shared `ReviewNoticeValidator.RequiredRecipeWarnings` path and Module
Studio's `Outcome` path are exhaustively guarded against the complete production
manifest inventory. This is not an omitted second list of confirmation text;
duplicating the same declaration under a new field would create two sources of
truth. The v2 fingerprint binds the exact ordered warning text, while the
behavioral regressions bind its required-confirmation effect. Neither proves
that a teacher's acknowledgement is substantive specialist approval.

| Stable recipe ID | Version in outgoing snapshot | Direct manifest delta measured against the tag | Current-tree reason the row requires evidence |
|---|---:|---|---|
| `access-remix` | `0.1.0` | `Warnings` | The warning now records the protected-specialist purpose-authority hold; the production door remains held. |
| `all-aboard.agency-cards` | `0.1.0` | `SupportedExports` | PDF and SVG were added to HTML and print HTML, alongside builder/output-shape hardening. |
| `all-aboard.first-then` | `0.1.0` | `SupportedExports` | PDF and SVG were added to HTML and print HTML, alongside builder/output-shape hardening. |
| `all-aboard.now-next-done` | `0.1.0` | `SupportedExports` | PDF and SVG were added to HTML and print HTML, alongside builder/output-shape hardening. |
| `all-aboard.task-strip` | `0.1.0` | `SupportedExports` | PDF and SVG were added to HTML and print HTML, alongside builder/output-shape hardening. |
| `board-to-brief` | `0.1.0` | None directly measured | It adds language-tag validation and inherits the changed shared locked-field matcher. |
| `directions-duet` | `0.1.0` | `ProhibitedPurposes`, `AllowedInputKinds`, `Warnings` | It adds a false-default source-inventory act, stricter aligned-row matching, an authenticated-review refusal, a teacher-only inventory notice, and a 36-case corpus. |
| `exit-lens` | `0.1.0` | None directly measured | No complete builder/editor/schema/validator/evaluation/renderer equivalence proof exists for the tagged tuple. |
| `family-bridge` | `0.1.0` | `ProhibitedPurposes`, `AllowedInputKinds`, `Warnings` | It adds the source-inventory act, explicit bilingual action/deadline/contact roles, stricter role-aware matching, changed semantic node shape, and a 36-case corpus. |
| `lesson-loom` | `0.1.0` | `InstructionalPurpose` | “Weave” changed to “Arrange”; language validation and a 36-case corpus were also added. The StrandPlan display decision is separate. |
| `press.big-print` | `0.1.0` | None directly measured | No complete builder/editor/schema/validator/evaluation/renderer equivalence proof exists for the tagged tuple. |
| `press.blankforms` | `0.1.0` | `InstructionalPurpose` | The purpose and deterministic output family expanded to hundred charts, dot/isometric-dot paper, landscape pages, and first-quadrant grids. |
| `press.booklet-guide` | `0.1.0` | None directly measured | No complete builder/editor/schema/validator/evaluation/renderer equivalence proof exists for the tagged tuple. |
| `press.flashcards` | `0.1.0` | None directly measured | No complete builder/editor/schema/validator/evaluation/renderer equivalence proof exists for the tagged tuple. |
| `press.foldables` | `0.1.0` | None directly measured | No complete builder/editor/schema/validator/evaluation/renderer equivalence proof exists for the tagged tuple. |
| `press.handwriting` | `0.1.0` | None directly measured | No complete builder/editor/schema/validator/evaluation/renderer equivalence proof exists for the tagged tuple. |
| `press.labels` | `0.1.0` | None directly measured | No complete builder/editor/schema/validator/evaluation/renderer equivalence proof exists for the tagged tuple. |
| `press.manipulatives` | `0.1.0` | `InstructionalPurpose` | The purpose and deterministic output family expanded beyond strips and dice nets to circles, box nets, spinners, algebra tiles, base-ten blocks, and tangrams. |
| `rubric-relay` | `0.1.0` | None directly measured | No complete builder/editor/schema/validator/evaluation/renderer equivalence proof exists for the tagged tuple. |
| `scaffold-smith.packet` | `0.1.0` | None directly measured | It adds language validation and a distinct 36-case evaluation corpus. |
| `scaffold-smith.task-entry` | `0.1.0` | `InstructionalPurpose` | “Absorbed TaskDock” changed to “absorbed task-entry mode”; language validation and the shared-schema route also changed. |
| `source-lens` | `0.1.0` | None directly measured | It changes claims and learner/teacher-facing source labels and adds a 36-case corpus; further rights and durable-verification work remains held. |
| `talk-moves-studio` | `0.1.0` | None directly measured | It counts distinct teacher-authored participation modes, refuses the reserved automatic pass option, and adds a 38-case corpus. |

### Measured declarative fingerprints — evidence only

The table below records the two measurements used to prepare this packet. The
outgoing column is bound to the exact tagged snapshot named above. The
current-tree column describes the mutable working tree measured earlier on 1
September 2026; it was not then a candidate freeze, selected route, equivalence
finding, or approval. The ratified route preserves those v1 values as historical
evidence and records the complete candidate separately under v2 below.

| Stable recipe ID | Outgoing SHA-256 (`recipe-contract-fingerprint.v1`) | Measured current-tree SHA-256 (`recipe-contract-fingerprint.v1`) |
|---|---|---|
| `access-remix` | `162C5A2BE668A8340F2A850F1B2F304A1036332DD69B61836FD2E535F7BF04FD` | `B657CC7D4DB7976E1F385D581A811975951CBA382D92AB6BB0994D4A5B55605C` |
| `all-aboard.agency-cards` | `E21E4E21EA165EBBAD218B98AE9AB11F83F5AD82244AE77B872D27875CDBA21F` | `E0C3D180512DE80D04166A3AA0BFA44352BF77C946B90F88C512B8C534553536` |
| `all-aboard.first-then` | `78956CDFEE8D7D74701F545AAB5535BC9D2BCB69BA72C634A0F6197465D0B8E5` | `C1450269643BA899679A577E1789ED717B6F03D9BBBED1DE97997732FB4548C6` |
| `all-aboard.now-next-done` | `48142E0FBBB86EEB9CAD1D9DC0172D696C047BA1E691D22D6BEC42DD60AC67AD` | `16995ADD60752602CC6A6C5627754FCFCD4E47754868FB323E19FA1D671F8AFD` |
| `all-aboard.task-strip` | `AB4F5222F52829BAAD8F79CE9163F95DBA56614B92A5FE50B7D139C7F0E42585` | `080F0411281588B5A44994502DD4200582DCC68726274A1AFE61D03AF254A520` |
| `board-to-brief` | `20873B30065DF05F62A4AF728D881F1D367312FEB3286A14D4DE4F7CB57C861A` | `20873B30065DF05F62A4AF728D881F1D367312FEB3286A14D4DE4F7CB57C861A` |
| `directions-duet` | `6DA01F0C7B8779420C7D76E737284119906FDEB409864F71CCC4D167E02A2929` | `C7F07EAE06055D3A1DBEC49F4A2E4A04F345DE7E7CF22621D3DA60F3B87FD7CC` |
| `exit-lens` | `5BDEE8FB0A55BDDF1231408B5A17382D29B3F54419CBD0E8D6B6DFF136789359` | `5BDEE8FB0A55BDDF1231408B5A17382D29B3F54419CBD0E8D6B6DFF136789359` |
| `family-bridge` | `7F927525D384FC7798F83092E5CC6552D2BC25FA4E6E1170F6D362DF04B208E4` | `E412A7A9D9DE717533CC1280F311513A3D7DB04D00A2A0BCDDBB1BC031E62BE5` |
| `lesson-loom` | `513448B0B369058F72BF1072B0EE47AC547B5433F1BD46B8629FCA136F26487D` | `E3B1E39BC8069E4F6DA2A5C8605DE63B9FA217FD89D540EFFAA122B3811ECE1F` |
| `press.big-print` | `4CB47B3D5A39E29C6CFA2C02928D4E6CF8B16DBB77D4472549B2CEFC8B04050D` | `4CB47B3D5A39E29C6CFA2C02928D4E6CF8B16DBB77D4472549B2CEFC8B04050D` |
| `press.blankforms` | `8807543DADC43916EC036FE79146D7E07B831BC8CB6841D57659CE4F8C7DAA81` | `419CF59E3227C0A57B9302AD47F603B6905C76F13ABAFD4A865DE250DAC79177` |
| `press.booklet-guide` | `01A03046EE2BCED7A87EA46CD152A735639EBD8C2465D831BD758F10F9E186D8` | `01A03046EE2BCED7A87EA46CD152A735639EBD8C2465D831BD758F10F9E186D8` |
| `press.flashcards` | `156ED6344D3BBA57425FEB4A98B27E59F86E1784EAEE5FAA600991D53A90EE84` | `156ED6344D3BBA57425FEB4A98B27E59F86E1784EAEE5FAA600991D53A90EE84` |
| `press.foldables` | `D9DE114FF77EBF3D855355EF5A048DDC82D7DF24B2FAA648E0B7BC405935C92D` | `D9DE114FF77EBF3D855355EF5A048DDC82D7DF24B2FAA648E0B7BC405935C92D` |
| `press.handwriting` | `273C5A162D814E5E3D3BF3227AB59C32A9261ECD477B828E63D7CD807721F08B` | `273C5A162D814E5E3D3BF3227AB59C32A9261ECD477B828E63D7CD807721F08B` |
| `press.labels` | `3C7AFB53CD7027AE9FA609F4E21D427C2B308843862776945CF83EF689D2EF1D` | `3C7AFB53CD7027AE9FA609F4E21D427C2B308843862776945CF83EF689D2EF1D` |
| `press.manipulatives` | `212C9F559ECDF46D97501B1802226C583F68BFF9B295AC881D786F82D67E06E6` | `913580FE097A4735955D02BCF6AD2F5562E83A158B23221F6E635F2AE82DF97E` |
| `rubric-relay` | `61BA6CBB6BED1D6A83F9452E9E81DB08A20CFD00FBC2FD9BA1A24823D50777D9` | `61BA6CBB6BED1D6A83F9452E9E81DB08A20CFD00FBC2FD9BA1A24823D50777D9` |
| `scaffold-smith.packet` | `1CD8900489CF9AA78463328BEE31277521CF0A3736D382BDFF559FF90914F2BF` | `1CD8900489CF9AA78463328BEE31277521CF0A3736D382BDFF559FF90914F2BF` |
| `scaffold-smith.task-entry` | `67D51E4082988D0B1E2F6ADFBCC4119A2C8DE3C22EBEB10D1BD197DDA3B7C42D` | `22D63BCC3CB78F982091452E1A2E13FAC7B6FF04FF544E94E09202F2804FEC02` |
| `source-lens` | `7CF1220A987AF7F2052AB1B489E79549E6683809B6B1F293A81B8E69347DA16E` | `7CF1220A987AF7F2052AB1B489E79549E6683809B6B1F293A81B8E69347DA16E` |
| `talk-moves-studio` | `826E14F062F180F403D7C07E78EE54EF6AF4E943F67523AB974AE54924DBFD76` | `826E14F062F180F403D7C07E78EE54EF6AF4E943F67523AB974AE54924DBFD76` |

The 15 candidate-only identities are `press.calibration`, `press.charts`,
`press.computational`, `press.field-journal`, `press.fluency`, `press.glossary`,
`press.grouping`, `press.history`, `press.learner-held`, `press.math-scaffolds`,
`press.protocols`, `press.puzzles`, `press.retrieval`, `press.rubrics`, and
`press.schedules`. They have no outgoing tag tuple to preserve, but a later
release record must still freeze their first admitted manifests and executable
evidence at the same exact candidate commit; public development history does not
silently admit them.

The repository had no GitHub Release object when measured, and no archived
outgoing application package is present. The public tag and source identities
therefore make “first admission” a product-policy disposition, not a fact an
automated contributor may infer.

## Display names are already decided and are not this choice

[ADR-008](ADR-008-public-module-display-names.md) and
[ADR-009](ADR-009-strandplan-display-name.md) change public chrome while
expressly preserving module, recipe, schema, localization, diagnostic, and saved-
project identities. SequenceSlate, StrandPlan, Forumwright, ReteachSignal,
Inquirywright, and KinDispatch therefore do not acquire new executable identities
merely because their display names changed. Counsel's pre-release name screen
also remains separate from this packet.

## The independent `recipeHash` / project-schema gap

The [implementation plan §6.5](../implementation-plan.md#65-open-project-package)
requires `recipeId`, `recipeVersion`, **and `recipeHash`** in every
`ProjectManifest`; ADR-003 incorporates the plan's required manifest fields.
The current strict project schema 1 stores recipe ID and version but has no
`recipeHash`. The upgrade host retains its sorted recipe-ID/version inventory
digest and now requires a second, length-framed declarative-contract digest that
binds every field in each compiled `RecipeManifest`, including output schema,
validator IDs, editor, renderer, exports, warnings, and evaluation identity. The
engine-owned portable-semantic-editor identity is explicitly identity-only
because it has no manifest. Neither digest is persisted in a project or binds
executable builder/editor/renderer bytes, so this improvement does not supply the
missing project `recipeHash` or make an in-place identity change safe.

This gap cannot be repaired by adding a property under the existing schema-1
label: the package validator owns a closed property set, and ADR-003 requires
explicit migration for released schemas. Every route below must therefore
record schema 1 as a deliberately legacy, hash-less format pending a defined
forward route, or authorize and prove a project schema 2 migration. A recipe
output schema such as `schema.family-bridge.v1` is distinct from the `.ocfproj`
container's project schema version; changing one does not silently change the
other.

## Selected Option A — explicit first-admission freeze

The product owner has declared, row by row, that the first local commit made
after this disposition and the complete closing rites is the first admitted
contract for the listed recipe identity, manifest, builder behavior, output
contract, editor behavior, evaluation route, and renderer behavior.

The selected Option A requires all of the following:

1. Name every one of the 23 outgoing rows; an omitted row remains held.
2. State expressly that earlier public development commits and the unsigned
   `v0.7.0-alpha` tag do not constitute an admitted recipe-compatibility baseline
   for that row.
3. Freeze the exact resulting commit, recipe manifest, builder behavior, output
   contract, editor and validator behavior, evaluation corpus, and required
   renderer evidence.
4. Freeze the 15 candidate-only identities in the same exact commit and add exact
   regression assertions that make later in-place drift fail closed.
5. Record the project-schema-1 `recipeHash` exception and its forward route; do
   not call the migration/version debt closed.

Option A does not move or replace the existing tag, cohere engine and assembly
versions, ratify ADR-007, authorize a package, or perform any release act.

## Option B — new identities with outgoing contracts retained

The product owner may instead treat a public/tagged tuple as a compatibility
contract and authorize a new identity for each row whose effective contract
changed.

Option B requires all of the following:

1. Select an exact candidate engine version and an exact new recipe version,
   recipe output-schema disposition, and evaluation version for every changed
   row. “Next,” “current,” or an inferred semantic-version increment is not a
   decision.
2. Preserve each admitted outgoing manifest, builder/editor/validator behavior,
   schema, evaluation route, and required renderer behavior alongside the replacement.
   Replacing `0.1.0` in place is forbidden.
3. Extend the executing candidate catalog so it contains both exact outgoing and
   new recipe identities, and prove that an old pinned project is never silently
   substituted with the new recipe.
4. Freeze representative outgoing packages and behavior fixtures for every
   admitted source shape; add exact selection, compatibility, refusal, and
   rollback tests.
5. Decide whether `recipeHash` enters a deliberately migrated project schema 2;
   if it does not, retain the schema-1 gap as an explicit release stop.

The current version-addressed application-root and upgrade host identify a
candidate build by `EngineIdentity.EngineVersion`, so any Option-B route needs an
explicit candidate engine identity. Choosing or enacting that version remains a
typist act. ADR-007 must be ratified separately before its design governs a real
managed upgrade, and District IT still owns every deployment plan.

## A per-identity split is permitted only when exhaustive

The product owner may choose Option A for some rows and Option B for others. A
third route, `U` (tagged contract admitted and candidate proven unchanged), is
permitted only when exact evidence establishes equivalence of the manifest,
builder and editor behavior, schema and validator behavior, evaluation route,
and renderer behavior. A matching manifest fingerprint alone cannot support `U`; a row with a
measured manifest delta may use `U` only after the outgoing manifest is restored
and every other equivalence surface is proved.

Every one of the 23 rows below must be completed. Shared evidence may be cited by
more than one row, but shared implementation does not make a disposition
transitive: Board to Brief must still be named when its change arrives through a
shared matcher, and both Scaffold Smith identities must be considered before
changing their shared schema. An unlisted or ambiguous identity remains held. If
any row selects Option B, one exact candidate engine identity applies to the
whole build.

## Disposition record — Option A ratified through C1/C2

A commit cannot contain its own Git object ID. Ratification therefore used the
following deliberately two-step route:

1. The exact written owner decision and all fields below were completed first.
2. The ordered closing rites ran on that exact tree, which was committed once as
   local, unpushed C1. C1 is the candidate freeze.
3. This immediately following record-only C2 names C1's full commit ID and
   changes the status to `RATIFIED`. C2 may update
   only `README.md`, `docs/README.md`, `docs/adr/README.md`, this packet,
   `docs/handover/2026-09-01-forge-integration-handover.md`, and
   `tests/Unit/RecipeIdentityDispositionPacketTests.cs`; all six, and exactly
   those six, must change. Its two status records become exactly `RATIFIED —
   OPTION A`. C2 may not change a recipe, builder, schema, validator, editor,
   renderer, evaluation, fixture, sample baseline, dependency, or other frozen
   executable/evidence surface.
4. The ordered closing rites run again on C2. Only then may C1 and C2 be pushed
   together and the verified branch be merged with history preserved (never
   squash or rebase).

`tools/verify-recipe-identity-ratification.ps1` makes that Git-history contract
executable. On the explicit pending C1 worktree it may emit only a bounded local
`skipped-pending-c1` receipt; hosted CI requires RATIFIED state. From a later
branch or regular merge commit, it locates C2 in `HEAD` ancestry, requires C2 to
have exactly one parent and that parent to equal the full C1 recorded here,
requires both commits to be `HEAD` ancestors, compares the C1-to-C2 file set to
the exact six-file set above, checks C1's exact pending markers, and retains its
bounded JSON receipt. CI therefore requires full checkout history.

The outgoing column below retains the partial declarative fingerprint measured
with `recipe-contract-fingerprint.v1`. The candidate column uses the complete
`recipe-contract-fingerprint.v2`, which additionally binds local preprocessing,
localization resources, and migration IDs; its existing ordered `Warnings` field
binds the combined warning/required-confirmation declaration described above.
Neither is an assembly hash or, by itself, proof of executable behavior; the
exact Git commit, behavioral tests, semantic default-output pins, and rendered-
sample manifest supply those separate surfaces.

| Record field | Explicit disposition |
|---|---|
| Status | `RATIFIED — OPTION A` |
| Product owner / typist (private identity may be recorded outside the public repository) | Product owner / typist, this Codex session; private identity kept outside the public repository |
| Decision instant | `2026-09-01T07:55:28.9491461Z` |
| Exact written disposition | “Ratify Option A for all 23 outgoing recipe rows; treat `v0.7.0-alpha` tuples as pre-admission; freeze the 15 candidate-only identities with the same exact commit; retain schema 1’s missing `recipeHash` as a release stop pending a separately authorized schema-2 route; leave ADR-007 Proposed.” |
| Exact outgoing snapshot | `a64abead04e56085b82ac632180ca1a362eb8bc3` via tag object `380a0e5c3b768bdaa655825b35a25307fe89c0e5` |
| Exact candidate freeze state | `5cae09dcb40628265d51912aea98304557abfda6` — local C1, immediately followed by record-only C2 |
| Treatment of unsigned `v0.7.0-alpha` recipe tuples | Development/pre-admission under Option A for all 23 rows; none is an admitted compatibility baseline |
| Candidate engine identity, if any row uses Option B | Not applicable; no row uses Option B |
| Candidate-only 15-identity first-admission freeze | The exact v2 manifest table below; the exact identity-to-definition/default-semantic-output regression in `RecipeIdentityDispositionPacketTests`; and the sorted 40-file `tests/Rendering/Fixtures/recipe-first-admission-samples.sha256` manifest (SHA-256 `DEF10A3258A2F2ABA922DF8F1BC38FC3A3209065B36F81F44C41B4FE047F4A90`), all contained by C1 |
| Project schema 1 `recipeHash` disposition | Schema 1 remains a deliberately legacy, closed, hash-less format; no property is backfilled under the schema-1 label. Release remains stopped until a separately authorized ADR defines schema 2 with `recipeHash`, explicit schema routing, copy-on-write migration from every admitted schema-1 source shape, frozen fixtures, rollback evidence, and backward-open proof. This disposition does not close that debt. |
| ADR-007 disposition | Still Proposed; this record does not ratify it or authorize a live upgrade |

| Stable recipe ID | Route | Outgoing declarative-contract SHA-256 (`recipe-contract-fingerprint.v1`) | Candidate declarative-contract SHA-256 (`recipe-contract-fingerprint.v2`) | Exact recipe version after disposition | Exact output schema after disposition | Exact evaluation version after disposition | Freeze evidence and rationale |
|---|---|---|---|---|---|---|---|
| `access-remix` | `A` | `162C5A2BE668A8340F2A850F1B2F304A1036332DD69B61836FD2E535F7BF04FD` | `FA99DC89BA4AD6D220B099643D2C6121383FE15477ECAA50775EE56ADC10F74C` | `0.1.0` | `schema.access-remix.v1` | `0.1` | C1 first-admission freeze; exact v2/runtime and executable regressions plus the rendered-sample manifest. |
| `all-aboard.agency-cards` | `A` | `E21E4E21EA165EBBAD218B98AE9AB11F83F5AD82244AE77B872D27875CDBA21F` | `FDE8EB525834B2AA424CF14D372ADE58A4A3D3498095B025C8B11B4186812454` | `0.1.0` | `schema.all-aboard.v1` | `0.1` | C1 first-admission freeze; exact v2/runtime and executable regressions plus the rendered-sample manifest. |
| `all-aboard.first-then` | `A` | `78956CDFEE8D7D74701F545AAB5535BC9D2BCB69BA72C634A0F6197465D0B8E5` | `BE9B04C48002EE77DE197837C1DE1B6A4FC10EBAFEF9907A5D3F0AA029080AD8` | `0.1.0` | `schema.all-aboard.v1` | `0.1` | C1 first-admission freeze; exact v2/runtime and executable regressions plus the rendered-sample manifest. |
| `all-aboard.now-next-done` | `A` | `48142E0FBBB86EEB9CAD1D9DC0172D696C047BA1E691D22D6BEC42DD60AC67AD` | `6DA2D2CF5171C609EC49A9B97622F9B9899F4FC74D930D42E0560C03D7711A46` | `0.1.0` | `schema.all-aboard.v1` | `0.1` | C1 first-admission freeze; exact v2/runtime and executable regressions plus the rendered-sample manifest. |
| `all-aboard.task-strip` | `A` | `AB4F5222F52829BAAD8F79CE9163F95DBA56614B92A5FE50B7D139C7F0E42585` | `B37386D3E562E3E2F4CD5FC05065F9CBD41529AAAA70951D84AC26E03DB7F094` | `0.1.0` | `schema.all-aboard.v1` | `0.1` | C1 first-admission freeze; exact v2/runtime and executable regressions plus the rendered-sample manifest. |
| `board-to-brief` | `A` | `20873B30065DF05F62A4AF728D881F1D367312FEB3286A14D4DE4F7CB57C861A` | `A1E6B588E55021D60E1A9FFF1FBBE5CF60F8A8BFC7985B071E7D8B5FC63BB471` | `0.1.0` | `schema.board-to-brief.v1` | `0.1` | C1 first-admission freeze; exact v2/runtime and executable regressions plus the rendered-sample manifest. |
| `directions-duet` | `A` | `6DA01F0C7B8779420C7D76E737284119906FDEB409864F71CCC4D167E02A2929` | `CF86EDBB46B9DFBD7991E255B8920EFAEC9ACD1E608B27B71B5C4914DBBF7F5C` | `0.1.0` | `schema.directions-duet.v1` | `0.1` | C1 first-admission freeze; exact v2/runtime and executable regressions plus the rendered-sample manifest. |
| `exit-lens` | `A` | `5BDEE8FB0A55BDDF1231408B5A17382D29B3F54419CBD0E8D6B6DFF136789359` | `76054CDCA21C7CB0D6159A27ADB06180ECFD3BA6B7BC17A51A2FD9125DD9C82B` | `0.1.0` | `schema.exit-lens.v1` | `0.1` | C1 first-admission freeze; exact v2/runtime and executable regressions plus the rendered-sample manifest. |
| `family-bridge` | `A` | `7F927525D384FC7798F83092E5CC6552D2BC25FA4E6E1170F6D362DF04B208E4` | `A7A8A9E81C4ECD931A3B6B929E65B3059FCDEB28E2819C3EB784B8B9919BFA3B` | `0.1.0` | `schema.family-bridge.v1` | `0.1` | C1 first-admission freeze; exact v2/runtime and executable regressions plus the rendered-sample manifest. |
| `lesson-loom` | `A` | `513448B0B369058F72BF1072B0EE47AC547B5433F1BD46B8629FCA136F26487D` | `6644849B3EA513821EF81737DD87047DC8FF8DA816B55B69E69B9CD8C4D00289` | `0.1.0` | `schema.lesson-loom.v1` | `0.1` | C1 first-admission freeze; exact v2/runtime and executable regressions plus the rendered-sample manifest. |
| `press.big-print` | `A` | `4CB47B3D5A39E29C6CFA2C02928D4E6CF8B16DBB77D4472549B2CEFC8B04050D` | `F8786CEC1C0A48A382AA88D9E3CD479D6395C7110365C672963402A4B4FFC9F2` | `0.1.0` | `schema.deterministic-press.v1` | `0.1` | C1 first-admission freeze; exact v2/runtime and executable regressions plus the rendered-sample manifest. |
| `press.blankforms` | `A` | `8807543DADC43916EC036FE79146D7E07B831BC8CB6841D57659CE4F8C7DAA81` | `A4EA79276F62355419D11D2FB08CEE8EDC386680E8B002A9A4F6D3E7449A94D7` | `0.1.0` | `schema.deterministic-press.v1` | `0.1` | C1 first-admission freeze; exact v2/runtime and executable regressions plus the rendered-sample manifest. |
| `press.booklet-guide` | `A` | `01A03046EE2BCED7A87EA46CD152A735639EBD8C2465D831BD758F10F9E186D8` | `1DC219879D76C50128AF43E344398A3B52C25C30088B92D5D8E40405EF25EF51` | `0.1.0` | `schema.deterministic-press.v1` | `0.1` | C1 first-admission freeze; exact v2/runtime and executable regressions plus the rendered-sample manifest. |
| `press.flashcards` | `A` | `156ED6344D3BBA57425FEB4A98B27E59F86E1784EAEE5FAA600991D53A90EE84` | `18B4B7167D38FD941B65B1EC19D5E0C24F397974B9AC19AEC070ED4F54F9F5CF` | `0.1.0` | `schema.deterministic-press.v1` | `0.1` | C1 first-admission freeze; exact v2/runtime and executable regressions plus the rendered-sample manifest. |
| `press.foldables` | `A` | `D9DE114FF77EBF3D855355EF5A048DDC82D7DF24B2FAA648E0B7BC405935C92D` | `55A3E24663DA5051133292541F15439C33ED48C44F9B8E5254663A8661EAB861` | `0.1.0` | `schema.deterministic-press.v1` | `0.1` | C1 first-admission freeze; exact v2/runtime and executable regressions plus the rendered-sample manifest. |
| `press.handwriting` | `A` | `273C5A162D814E5E3D3BF3227AB59C32A9261ECD477B828E63D7CD807721F08B` | `85AED146613D967605ADE75AEC64E62FE4EC00B20BA37847942B262FFD0FC77C` | `0.1.0` | `schema.deterministic-press.v1` | `0.1` | C1 first-admission freeze; exact v2/runtime and executable regressions plus the rendered-sample manifest. |
| `press.labels` | `A` | `3C7AFB53CD7027AE9FA609F4E21D427C2B308843862776945CF83EF689D2EF1D` | `CF161F36F77394733896F6712873DCA727B59B5B7BD32B84F26014F7372F4D41` | `0.1.0` | `schema.deterministic-press.v1` | `0.1` | C1 first-admission freeze; exact v2/runtime and executable regressions plus the rendered-sample manifest. |
| `press.manipulatives` | `A` | `212C9F559ECDF46D97501B1802226C583F68BFF9B295AC881D786F82D67E06E6` | `C08B6CD5056A48046001F232166F31E9E8C9156045672F1C6BA4DFF78FE1D3C3` | `0.1.0` | `schema.deterministic-press.v1` | `0.1` | C1 first-admission freeze; exact v2/runtime and executable regressions plus the rendered-sample manifest. |
| `rubric-relay` | `A` | `61BA6CBB6BED1D6A83F9452E9E81DB08A20CFD00FBC2FD9BA1A24823D50777D9` | `9ACEF556796C706905F36F4225CF008280C1C1206FAA1B58E69C42996D14C17F` | `0.1.0` | `schema.rubric-relay.v1` | `0.1` | C1 first-admission freeze; exact v2/runtime and executable regressions plus the rendered-sample manifest. |
| `scaffold-smith.packet` | `A` | `1CD8900489CF9AA78463328BEE31277521CF0A3736D382BDFF559FF90914F2BF` | `AA4C6F6AC1440BE0E919DA95C3920AF1B7198F9EE296F05EAB97DAA22B10E5B6` | `0.1.0` | `schema.scaffold-smith.v1` | `0.1` | C1 first-admission freeze; exact v2/runtime and executable regressions plus the rendered-sample manifest. |
| `scaffold-smith.task-entry` | `A` | `67D51E4082988D0B1E2F6ADFBCC4119A2C8DE3C22EBEB10D1BD197DDA3B7C42D` | `1F433EC97A18BAC22BE35EEE694C72A9C135BBFF77A6FAB2B97DB5C2EBA027FD` | `0.1.0` | `schema.scaffold-smith.v1` | `0.1` | C1 first-admission freeze; exact v2/runtime and executable regressions plus the rendered-sample manifest. |
| `source-lens` | `A` | `7CF1220A987AF7F2052AB1B489E79549E6683809B6B1F293A81B8E69347DA16E` | `5A96F3ABA5531C17A0EE4B61525DA640A9DB6B5E0BF542E851EEF8FEA78CB557` | `0.1.0` | `schema.source-lens.v1` | `0.1` | C1 first-admission freeze; exact v2/runtime and executable regressions plus the rendered-sample manifest. |
| `talk-moves-studio` | `A` | `826E14F062F180F403D7C07E78EE54EF6AF4E943F67523AB974AE54924DBFD76` | `CAC76D811BF1F539B1AD3D5E6CA5D71891ECBBD746AEB7212F552B3FFF563297` | `0.1.0` | `schema.talk-moves.v1` | `0.1` | C1 first-admission freeze; exact v2/runtime and executable regressions plus the rendered-sample manifest. |

### Candidate-only identities frozen at the same C1

These rows have no outgoing tuple. Option A makes C1 their first admitted
identity and contract. The listed default definitions are separately pinned to
their semantic document hashes in `RecipeIdentityDispositionPacketTests`; the
rendered files are pinned by
`tests/Rendering/Fixtures/recipe-first-admission-samples.sha256`.

| Stable recipe ID | Candidate declarative-contract SHA-256 (`recipe-contract-fingerprint.v2`) | Version | Output schema | Evaluation | Exact default definitions |
|---|---|---|---|---|---|
| `press.calibration` | `D4EF35243CC43AB847A3FF7443826299968501795D44F6BC30E1007D53F8773E` | `0.1.0` | `schema.deterministic-press.v1` | `0.1` | `calibration-proof` |
| `press.charts` | `E5937D4D7B8494FF081A1E7657EBFABF5049952BE9DD6AF166FEB3D1A33B3518` | `0.1.0` | `schema.deterministic-press.v1` | `0.1` | `bar-chart` |
| `press.computational` | `D70950F6118EAD681D2565B0A45371919F72B4CDE4A925362607079735EAADE2` | `0.1.0` | `schema.deterministic-press.v1` | `0.1` | `algorithm-cards`, `bug-zoo`, `parsons-puzzle`, `rubber-duck-deck`, `trace-table` |
| `press.field-journal` | `9CC177EF0D3DE8E2C9BCCA500F5CCEE736E3557BBBB0AE0F15A6AFA7C1711C59` | `0.1.0` | `schema.deterministic-press.v1` | `0.1` | `field-log`, `observation-frame`, `site-map`, `specimen-labels` |
| `press.fluency` | `065DEA8D80616EEB63DBA935EDD36568416D50CFBC45181D210E1E004235860E` | `0.1.0` | `schema.deterministic-press.v1` | `0.1` | `fluency-rehearsal` |
| `press.glossary` | `B57D198C81DDE52B3759494FD5797E3059E84CE423A584BD7C8FA3DFB0996751` | `0.1.0` | `schema.deterministic-press.v1` | `0.1` | `glossary-garden` |
| `press.grouping` | `8856C92FB62BBEB0496B3FDA22E344E3EDAC791733ABBFAD3C4130D72BEC7FAC` | `0.1.0` | `schema.deterministic-press.v1` | `0.1` | `grouping-cards` |
| `press.history` | `33944CC852F4009FD0D9027B09D49D3173D8C5E2CD7C60AB696BB202912B4BE0` | `0.1.0` | `schema.deterministic-press.v1` | `0.1` | `synthesis-table`, `timeline` |
| `press.learner-held` | `363B309D85421A517A8530883A3AC63829EF3CBD9B6D1CE1450DBCA7966EA801` | `0.1.0` | `schema.deterministic-press.v1` | `0.1` | `goal-post`, `portfolio-passport`, `strategy-shelf` |
| `press.math-scaffolds` | `58792158AE76F7349CBEF6B7A639607135440E60DC3F9B6B0F1C213428F8DD8E` | `0.1.0` | `schema.deterministic-press.v1` | `0.1` | `estimation-first`, `worked-example-fader` |
| `press.protocols` | `068A87AE3D53CFF07EA0472CE6E7F9CF6CED97C0CB2C8F664BCF21C024293D2F` | `0.1.0` | `schema.deterministic-press.v1` | `0.1` | `concept-sort`, `peer-feedback`, `role-cards` |
| `press.puzzles` | `1B1313D13C89D6CCD803B33D4AA3F3243B8BC49C1043DFC22BC16A6E48DE5693` | `0.1.0` | `schema.deterministic-press.v1` | `0.1` | `bingo-cards`, `word-search` |
| `press.retrieval` | `4B7CABB7003B9BEE2C21388E216F7126841AE673134DF83EDAB1C09839517A9C` | `0.1.0` | `schema.deterministic-press.v1` | `0.1` | `retrieval-grids` |
| `press.rubrics` | `2803F649DE3CB59E1ECFECAAEFEE84B9CC37C54DD2C63C7730B66B7CADAECAC5` | `0.1.0` | `schema.deterministic-press.v1` | `0.1` | `done-definition`, `one-point-rubric`, `success-criteria` |
| `press.schedules` | `D1FED11C73659BE5936B2EFA74D126E09ACD2AE8D7F6B2F0DDDFD5E4344BBF71` | `0.1.0` | `schema.deterministic-press.v1` | `0.1` | `bell-to-bell` |

## Actions that remain separately held

Ratifying this packet does not authorize or supply any of the following:

- moving or replacing a tag; choosing/enacting a release version; signing,
  installing, distributing, publishing, filing, or sending correspondence;
- packaging an operator host, preparing a live library, or selecting District
  IT roots, inventory, retention, deployment, detection, smoke, or rollback
  policy under Proposed ADR-007;
- activating a real second UI language before the multilingual seat supplies and
  reviews exact catalog bytes and an authorized build pins them;
- admitting Mulberry, OpenMoji, OpenSymbols, or new Honest Ink mappings before
  exact-asset AAC/SLP, rights, and accessibility review;
- selecting an Atlas priority before the real needs-first council session,
  participant-frozen record, separate feasibility appendix, and written product-
  owner disposition;
- trademark clearance, human AT/physical-device evidence, pilot evidence,
  district approval, rights review, or any other protected-seat act.

The single candidate freeze is C1
`5cae09dcb40628265d51912aea98304557abfda6`; its ordered closing rites and
repository hook accepted the exact committed tree. This immediately following
record-only C2 names that commit and marks the packet `RATIFIED`. C1 and C2 may
be pushed only together after C2 passes the same closing rites. A green
neighboring test cannot substitute for either exact record.
