# Stacked pull-request containment — pull requests 16 through 25

**6 September 2026.** This record is the account of how ten open pull requests reached `main` in one
merge commit, what each of them measured on its own head, and what this merge does **not** settle.

The evidence ledger has three kinds of entry — `hosted-ci`, `hosted-codeql` and `merge` — and no
vocabulary for *contained but not merged*. Nine of the ten requests are exactly that at the commit
level: their commits entered `main` inside another request's merge. This record carries that link in
prose because the ledger cannot, and because a reader who finds nine requests marked merged with a
single merge row is owed the explanation.

## What happened

The ten open requests were not ten changes. They were one linear chain of fifteen commits above
`main`, each request's head an ancestor of the next. Pull request 25's head
`113132957811377e7d217fa6561daeed2314a3b6` contained every other head.

Six of the nine earlier heads had concluded `failure` on their own hosted CI, and a recorded
conclusion is immutable, so those requests could never have been lawfully merged one at a time.
Merging the tip landed all fifteen commits with their exact hashes and the C1/C2 ratification
ancestry intact.

Before the merge, pull requests 17 through 24 were retargeted from their stacked `codex/*` bases to
`main` while all were open. That ordering was deliberate: GitHub's merged-detection is push-triggered,
and a base change arrives as an `edited` event, so retargeting after the merge would have left eight
requests to be closed by hand and read as abandoned. Pull request 16 already targeted `main` and was
not retargeted.

The merge is `98499fa5b5ddd9d646979019c8359baf888691ea`, merged 2026-09-06T22:44:21Z, first parent
`39897d2b58884d000398c5178e87e174f82f5402`, second parent the pull request 25 head. Its tree is
`3f94e3046e7caf9eec7f5ce17b7a018972a1974c`, byte-identical to the pull request 25 head's own tree, so
the merge introduced no content that had not already been tested.

**A measured platform fact, recorded because it had no precedent here.** All ten requests flipped to
`MERGED` on the merge push, including the seven that were still drafts. Whether push-triggered
merged-detection flips a draft request could not be established read-only before the fact; it does.
`delete_branch_on_merge` then removed all ten head branches, not only the merged request's own. Every
head was verified an ancestor of `main` before that was accepted; none was lost, and every head SHA
remains recorded below.

## The nine contained requests

Each head's own conclusion was read from that run's own record. Six are failures. They are not
withdrawn by this merge and were not cured by any rerun; every run in the chain is attempt 1.

| Request | Exact head | Own CI conclusion | Ledger row |
|---|---|---|---|
| 16 | `bb788aa8d74fc3e230b6712876a6ea6c3ccce9d5` | success | `ci-33969243572`, and the post-retarget generation `ci-34005378354` |
| 17 | `4970399bc1505b3bff453187891c1b72901c84fc` | **failure** — 4 of 2,442 | `ci-33977198151` |
| 18 | `483649cad1a8f3f233d6065aaac404dac553bdc0` | success | `ci-33978930548` |
| 19 | `94971ed806422c851ed4bdd137b9a37f17fe9875` | **failure** — 24 of 2,532 | `ci-33987933433` |
| 20 | `80f52b09571b4d190ffbb89f63cbe0f43f317fa7` | **failure** — 24 of 2,568 | `ci-33990242717` |
| 21 | `8857a6474cc17cdfc7357dc56665b45dcced4fac` | **failure** — 27 of 2,578 | `ci-33992504610` |
| 22 | `aa7410085a02359988ac70d9f5a85e2e79968295` | **failure** — 18 of 2,609 | `ci-33997448266` |
| 23 | `77bb88f3e07b5d888ce0e5f0cc2601a96c8a9dee` | **failure** — 24 of 2,619 | `ci-34001098197` |
| 24 | `7cb257e7df5dcc55a7414ef482bed035ee2b4fd8` | success — 2,651/2,651 | `ci-34012750436` |

Pull request 24's two earlier heads also concluded failure and belong in the account:
`db40e9b673bceb65cd60d0e0fb88653b02ce66c6` (26 of 2,625) and
`a73fe0d70cc30aa2d6fdbdf462cc5d4770d61875` (5 of 2,650). **Eight consecutive heads were red**, not six.

### Verbatim primary failures

Read from each run's own TRX, downloaded on 6 September 2026 before the artifacts' 12–13 September
expiry and recounted independently. Twenty-four failed tests are one defect, not twenty-four: a single
`unsafePriorResult` flag is set once and never cleared, so one missed 500 ms settlement refuses every
later caller in that class for the rest of the process with
`A prior fixture has uncertain cleanup/capture; no new process was created.`

| Head | Primary failures | Message |
|---|---|---|
| `4970399` | `KioskPortTests.The_raster_core_prints_a_real_pdf_to_file_when_the_inbox_printer_exists` and `EdgePdfExporterTests.Two_exports_complete_concurrently_with_isolated_edge_profiles` | `System.InvalidOperationException : Edge did not produce a complete PDF within 90 seconds. The Edge launcher was still running.` |
| `4970399` | `HeadedUiaWalkTests.Part3_Steps9to12_move_edit_and_approve_operate_through_uia_patterns` | `System.TimeoutException : Timed out after 70559 ms and 1 probes waiting for the top-level window for harness mode 'review' (process 12864).` |
| `4970399` | `CiTestRunnerContractTests.Evidence_snapshot_rejects_malformed_xml_but_retains_a_coherent_failed_trx_as_red` | `The PowerShell evidence-fixture process exceeded 30 seconds.` |
| `94971ed`, `80f52b0`, `77bb88f` | `CiTestRunnerContractTests.Evidence_snapshot_rejects_balanced_duplicates_and_omitted_suite_artifacts` | `FixtureProcessException : Primary: DisposalFailure: fixture ownership did not settle cleanly.` |
| `8857a64` | `EdgePdfExporterTests.A_completed_launcher_gets_a_bounded_handoff_for_its_pdf_child` | `A child-owned partial PDF was mistaken for launcher failure or completion.` |
| `8857a64` | `FixtureProcessRunnerTests.Disposal_observation_keeps_zero_elapsed_entry_exit_and_task_completion_distinct` | `Assert.True() Failure` |
| `8857a64` | `CiTestRunnerContractTests.Repository_state_hashes_tracked_and_untracked_bytes_but_excludes_ignored_outputs` | `FixtureProcessException : Primary: DisposalFailure: fixture ownership did not settle cleanly.` |
| `aa74100` | `CiTestRunnerContractTests.Evidence_copy_rejects_source_and_destination_paths_outside_their_roots` | `FixtureProcessException : Primary: DisposalFailure: fixture ownership did not settle cleanly.` |
| `db40e9b` | `CiTestRunnerContractTests.Evidence_inventory_rejects_a_target_path_outside_its_target_directory` | `FixtureProcessException : Primary: StreamDrainTimeout: redirected streams did not finish within the separate drain budget.` |
| `a73fe0d` | `CiTestRunnerContractTests.Evidence_copy_records_and_enforces_equal_source_and_destination_hashes` | `FixtureProcessException : Primary: DisposalFailure: fixture ownership did not settle cleanly.` |
| `a73fe0d` | `FixtureProcessRunnerTests.Root_exit_does_not_stand_in_for_stream_eof_or_descendant_exit` | `Assert.StartsWith() Failure: String start does not match` |
| `a73fe0d` | `FixtureProcessRunnerTests.Nonoverlap_cleanup_preserves_primary_failure_and_observes_owned_aggregates` | `Assert.Equal() Failure: Collections differ` |

## The green at the tip is masked, not cured

This is the single most important sentence in this record and it must not be softened in any later
summary. The chain is green at the merged head. It is green in an environment from which the one
condition every red observation shared has been removed **by construction**.

`git diff a73fe0d 7cb257e` — last red head to first green head — changes three code files, and all
three do nothing but place two test classes into one
`[CollectionDefinition(DisableParallelization = true)]` collection.
`git diff a73fe0d 1131329 -- tests/Unit/FixtureProcessRunner.cs tools/run-ci-tests.ps1` returns
**empty**: the runner carrying the whole failing mechanism and the bounded CI runner are byte-identical
from the last red head to the merged head. `WorkMilliseconds = 30_000` and
`SettlementMilliseconds = 500` are unchanged and still hard-capped. No assertion was weakened, no test
renamed or skipped, and the new membership guard was red-tested before the attributes were added.

The removal is measurable. Counting other Unit-class results interleaved inside the fixture cohort's
span: 764 in the red run at `94971ed`, 977 at `77bb88f`, 503 at `4970399`, and **zero** in the green run
at the merged head, where the cohort runs as a contiguous block.

One genuine forward step exists and is not a diagnosis: separate progress observations at 1,772/1,777 ms
retained callback entry and exit at 1,757 ms with completed disposal. Disposal does finish — roughly
1.75 seconds after a 500 ms budget. That converts the primary from "never settles" to "settles about
three and a half times past its budget under load." No instrument reproduces it under equivalent load;
none exists.

`483649c` ran green in the middle of the red band, so the failure was already intermittent before any
isolation, and three greens after it bound the residual rate only loosely.

## Open sightings

This merge takes `main` from eleven open sightings to twenty-one. **None is closed by it.** A merge is
not a diagnosis, and a passing rerun is a non-reproduction that never closes a row.

S-12 through S-20 arrive with the chain. S-21 is new, first observed during this integration's local
preparation and recorded in the register.

## What this merge does not settle

No recipe admission. No schema ratification — schema 1 remains hash-less and release-blocked. ADR-007
and ADR-010 remain Proposed. No version tag, release, distribution, site publication, correspondence,
public filing, account creation, or council or protected-seat claim. H0 through H7 remain not begun.
Calibration visual QA still fails. CC BY-SA 4.0 remains a selected proposal, not an operative grant,
with ownership, scope and assent unresolved. The neutral UI packet remains `draft`/`und` with blank
reviewer identity and an empty production allowlist; no second-language or protected-seat review is
supplied.

### The seven replacement recipes are non-default and unadmitted, but reachable

They are not sealed away, and calling them "held" without qualification would overstate the boundary.
Engine `0.8.0-alpha` ships a **Recipe version** control in both the Press Room and Module Studio. Its
default selection is the historical `0.1.0` entry; the second entry is labelled as the replacement
candidate. A teacher can select it in one click and then review, approve, print, export and save with
it. The test suite asserts that control on every press and every module door and mode, not only on the
seven replaced identities.

What is preserved is the outgoing contract: the `0.1.0` routes were restored to their pre-stack
behaviour through frozen `Historical*` builders, every surviving `0.1.0` manifest fingerprint is pinned
by the literal minimum engine `0.7.0-alpha`, and the forty-row first-admission sample baseline is
unchanged. The compatibility holds recorded in the implementation register are **not** lifted by this
merge.

## Six findings from the union review, recorded as open items

The 174-file merge was reviewed as a single change before it landed — a pass no per-request review had
performed. Six changes reach `main` that no pull request body describes. None is settled here.

1. **The default project library moves.** `AppServices.DefaultLibraryRoot` is byte-identical on both
   sides, but it is version-addressed by design:
   `MyDocuments\OpenClassroomFoundry\{EngineVersion}\projects`. The engine constant moves
   `0.7.0-alpha` → `0.8.0-alpha`, so the resolved path moves with it and no migration code exists.
   `ProjectLibraryRootConfiguration.ValidateProductionRoot` additionally refuses a managed
   `--library-root` whose path lacks the current version segment. No teacher is exposed by this merge —
   no release is authorized and nobody runs `main` — but it becomes real at the first distribution and
   is an open question until then.
2. **A founding safety doctrine was retracted.** The Press Room class comment deleted "the
   parameters-never-prose invariant is therefore visible: there is nowhere to type prose" in favour of
   "Free text requires a fresh Green confirmation after every edit; deterministic layout is not privacy
   classification." The module specification carried the same retraction in place; it is restored to
   strike form by this commit.
3. **A mandatory attestation appears on the shipped `0.1.0` path.** Review and approve now require a
   ticked Green-input checkbox, auto-ticked on press load and auto-cleared by any text edit. This is
   not confined to the replacement candidates; it changes shipped Press Room behaviour.
4. **The version control is not scoped to the seven replaced recipes**, as described above.
5. **A CI gate changed shape and no pull request body mentions it.** `ci.yml` drops its inline forty-row
   comparison and delegates to `tools/verify-sample-baselines.ps1`. The new gate is stronger on several
   axes — it pins the historical manifest's own bytes by hardcoded SHA-256, closing a hole where output
   and manifest could have been edited in the same commit — and differently scoped, because two of the
   forty samples are now produced by frozen `Historical*` copies rather than the shipped presses.
6. **One document was overwritten where project law requires striking.**
   `docs/modules/deterministic-press-spec.md` took thirteen rewritten passages with no strikethrough.
   This commit repairs them.

### The one passage that could not be restored literally

`TruthSurfaceDocumentationTests` asserts that the string "Gate A and Gate C are structurally vacuous"
does **not** appear in `docs/modules/deterministic-press-spec.md`. Restoring that sentence in strike
form would have reintroduced the literal string and turned `build-and-test` red, and writing around a
guard would be a worse fault than the deletion being repaired. Its exact prior wording is preserved
here instead, struck, and remains in the file's own history at `39897d2`:

> ~~**Gates:** Gate A and Gate C are structurally vacuous (proven, not waived). Gate B applies in its
> lightest form: the teacher reviews parameters beside an exact-scale print preview and approves;
> render, export, and print still accept only an ApprovedArtifact. Architectural uniformity is the
> point — Module Zero teaches every teacher the approval rhythm on artifacts where approval is
> effortless.~~

## Where this record stops

This commit records the merged head's own hosted conclusions, the post-retarget generation for pull
request 16, the merge itself, and the exact-`main` conclusions after it. It cannot record its own.

Ending the chain here leaves the same gap `merge-pr-12` and `merge-pr-15` already leave — no merge row
and no exact-`main` pair for this record's own commit. That is the stopping rule this repository has
used before rather than an oversight, and a later commission may close it. Nothing here should be read
as claiming that this record's own pull request was measured.
