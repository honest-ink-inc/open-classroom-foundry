# Bounded commission hardening — prepared law, exact seams, and the unopened council

**Prepared:** 3 September 2026

**Branch:** `codex/bounded-commission-hardening`

**Base:** `d851c6c2611147a34243b38b874831ccded54f3e` (`main`)

**State:** current local repository-state handover. The implementation is local
commit `576e19148862df0540fbdf6235e7896832674d7e`; this document's completed
evidence is the following local record-only change. Neither commit has been
pushed, published, released, tagged, signed, installed, distributed, filed, or
sent to anyone. Hosted evidence does not exist for this tree.

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

H0 now has exact, machine-checkable seat-authority, attendance, and decision
arithmetic without claiming that any human supplied it. Each seat entry binds
one person, appointing authority, one-calendar-year term, session scope,
acceptance, qualification, and custodian. A disputed recusal is its own
15-part read-back record for exactly one present affected person. Resolved
RECUSED and NOT-RECUSED outcomes reconcile to the same need's recommendation;
an honest no-quorum or no-majority HELD outcome cannot be converted into a
recommendation. Immediate resignation or term expiry stops the sitting and
requires a fresh roster cutoff and denominator. H1–H7 carry the same discrete
recusal-record and freeze-manifest requirement.

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

The ordered close completed on 4 September 2026:

- `dotnet format` fix and `--verify-no-changes` both exited 0. The post-format
  Release solution build with warnings as errors succeeded at **0 warnings, 0
  errors**. `git diff --check` exited 0; its only console notices were Git's
  working-copy line-ending warnings.
- Complete serial solution run
  `20260904T084603Z-d1f60a4ba32041fca9818097f4e141ca` passed
  **2,344/2,344**, zero failed and zero skipped: Accessibility 26, Contract 175,
  Instructional Evals 336, Integration 317, Rendering 84, UI Automation 263,
  and Unit 1,143. Its seven sorted path/hash/length TRX entries have manifest
  SHA-256 `86651624C62DA2051A07165D61E9E8303A5A5021427B6192AF47B8A1E4186DBD`.
- The unfiltered stability run
  `20260904T085154Z-b316e1a2c35949bfaaeac7eaf498cdd0`, run serially against
  exact implementation commit `576e19148862df0540fbdf6235e7896832674d7e`,
  repeated **2,344/2,344** with the same suite counts and seven coverage files.
  Its seven-TRX manifest SHA-256 is
  `F08C08DF4B4A73658943043A05A6E1183E0345D7DDE2862001B6209E8E2E52A5`.
- The required two-producer SampleGenerator comparison at that exact commit
  used run id `20260904T085706Z-0d1b1d57924b4e22b8f87ecdd56af374`.
  Both fresh directories held **40 files** and **769,403 bytes**; all 40 names,
  lengths, and SHA-256 values matched. The canonical sorted
  path/length/hash-manifest SHA-256 is
  `00FA10352A8CD5F765E34E646E23D47B3F7E18A1A00D0E71EC2F29D2F30FF3E7`.
- Independent audits closed the governance documents at 278/278 Atlas record
  tests, 6/6 governance-hardening tests, and 2/2 Atlas governance tests; the
  final cross-scope audit reran 81/81 cloud contracts and 77/77 focused
  governance/domain tests and found no remaining P0/P1 issue. The audits found
  and repaired semantic duplicate enums, malformed-Unicode pre-auth escapes,
  the SampleGenerator's missing exact review catalog, and a formatter-induced
  exact-number compile failure before the close.
- The installed pre-commit hook scanned approximately **867.88 KB**, reported
  no leaks, and admitted the implementation commit.

### Retained non-green measurements

These measurements remain red or incomplete even though the final isolated and
serial passes are green:

- Receipt `20260904T032401Z-2959235ccdb247238f179c189f753cff`
  completed red at 28 failures. The two
  `StagedTaskFixtureTests.Every_fixture_builds_validates_approves_and_renders_for_screen_and_paper`
  cases (fixture indexes 28 and 29) and 23 named `HostilePackageDepthTests`
  cases each reported
  `System.InvalidOperationException : Approval is blocked: every referenced image requires exact Gate B asset evidence, with no missing or unrelated binding.`
  Three UI cases—
  `LibraryDoorTests.Reopened_All_Aboard_content_routes_pdf_to_the_semantic_exporter_with_its_assets`,
  `ReviewSurfaceContractTests.GateB_selected_content_exposes_every_consequential_field_of_every_nonparagraph_node`,
  and
  `LoadedProjectPreflightTests.Exact_semantic_content_is_read_only_and_inspectable_before_Green_can_be_confirmed`—reported either that same message or
  `System.InvalidOperationException : Gate B review refused: every referenced image requires an exact local asset catalog; a placeholder is not review evidence.`
  Those failures measured missing test/fixture evidence and were repaired by
  supplying the exact reviewed catalogs, not by weakening approval.
- Receipt `20260904T035103Z-906577fa2b024bd9859cdc1e00936a7f`
  timed out at the unchanged 900-second bound. UI Automation produced no TRX;
  Unit recorded 889/890 with
  `AtlasCouncilGovernanceTests.Atlas_priority_route_remains_needs_first_and_separates_each_authority`:
  `Assert.Contains() Failure: Sub-string not found`; the retained expected
  prefix was `The first cohort must enact a decision pr...`. The stale
  documentation assertion was corrected; the UI signature remains open S-08.
- A later attempted run directory,
  `20260904T054920Z-9517248cc7b04a1a85d7d4c185f14657`, has streams but no
  summary because its runner was interrupted. On resumption, both recorded
  processes were absent; the exact stale marker was removed after inspection,
  while the incomplete directory was preserved.
- Receipt `20260904T081350Z-c5320441d5ba40f0bef1fdae213c54ea`
  timed out with stable source and assembly identities. Accessibility 26,
  Contract 175, Instructional Evals 336, Integration 317, Rendering 84, and
  Unit 1,143 all passed—**2,081/2,081**—but UI Automation produced no TRX.
  The same UI project then passed alone **263/263** in 50.6758 seconds, and both
  complete serial passes above repeated 263/263. These are non-reproductions,
  not a diagnosis or a reason to raise the suite's deadlines.
- Before those retained receipts, focused pre-close runs exposed only assertion
  drift in
  `GovernanceDocumentHardeningTests.Compensation_and_participation_create_no_content_license_or_erasure_promise`,
  `ReviewSessionTests.Restricted_session_never_reports_approval_ready_and_still_refuses_approval`,
  `TruthSurfaceDocumentationTests.Print_instrument_does_not_turn_mechanical_inspection_into_protected_review`,
  `GovernanceDocumentHardeningTests.Atlas_H0_uses_separate_choices_constituted_seats_and_a_non_circular_freeze_chain`,
  and
  `GovernanceDocumentHardeningTests.Human_reviews_are_ordered_and_every_freeze_record_remains_open`.
  Their messages were `Assert.Contains() Failure: Sub-string not found` against
  the newly separated or line-wrapped truth text; the restricted-session case
  expected `Review cannot be approved` and observed
  `Approval is not available: the review is not awaiting the teacher, or blocking issues remain.`
  The assertions were aligned to the already-correct current language and all
  later complete passes are green.

## Evidence manifest

These hashes will bind the decision and control artifacts after formatting and
the last audit correction. The manifest records files, not a release package.

| Artifact | SHA-256 |
|---|---|
| Proposed first-cohort operating terms | `3BD04151B0783D9551FCD8E77C82396E082D42248EB962D63E2708C93A604A76` |
| Proposed corrective compensation policy | `B4E3D0B33DCF97C1692E64087987C6E99B3C253FB91F1D35D67C205097DCF3A4` |
| Stage-gate disposition register | `AAE50D216F64B8EA59B134FF734CB2296C3944F516CB1FE829CDAEF3B0F2CC38` |
| Ordered H0–H7 review ledger | `6658773119D9CBAAF6378A6FF8C2DF3B6BE48345DD6B41C33B67D93500F0A09C` |
| Proposed ADR-010 | `B1FE5C9A00CB595EB143F3809B23F7A3450A97E656CBDF742AA2394E329D2E79` |
| NVDA walkthrough | `AE89473529B10BBAC8572F466FBD5A307D3BD270CE9DAF207236B4B9E4D2D304` |
| Physical-print inspection instrument | `209DC55FBB5F6729E651B154300A2B72B17CDCB8B0A0BC2CFC553E0144FD4F8B` |

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
