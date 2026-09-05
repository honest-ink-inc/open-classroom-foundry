# Source-role integrity and predecessor evidence

**Prepared 5 September 2026**, from `8857a6474cc17cdfc7357dc56665b45dcced4fac`
on `codex/source-role-integrity`.

This continuation repairs a measured source-role comparison contract and retains
predecessor failures. The [complete work register](accepted-improvement-register.md)
remains active. Earlier frozen records and manifests are unchanged.

## SourceLens role integrity

Constitution #10 and plan section 10.12 require preserving load-bearing source
facts and quotation fidelity. The existing catalog validator searched all
document strings by substring. A literal anywhere—including a teacher-only
notice or a different source field—could satisfy that check. Fresh review
acknowledgements and typed approval did not compensate for the weak recipe
validator. The defect is measured in catalog-to-review controls, not inferred
from text inspection alone.

The original frozen nine-case baseline is
`out/source-role-integrity/20260905T211936Z-43641aee01224a12a4c78140e938ebbd/`.
Each run executed nine: five passed and four failed. Four altered revisions
returned typed approval for the exact attempted revision after a fresh
acknowledgement, with no blocker: excerpt `25` expanded to `125`; excerpt
hidden in a TeacherOnlyNotice; excerpt moved under Interpretation; and Date/
Place values swapped. The passes include four permitted controls and the
already-correct refusal of `25` changed to `26`.

Original summary SHA-256:
`B016451552379A4D91EBA3F18BAB47AC30D8352E86785F3EB3A057A3368A613A`;
45-file inventory:
`70F57E05008B9150A1AAB9F5CA5C551DB59B78DB3E3DC703B8626BF23E1A8382`.
The initial preparatory build failed with xUnit2031 at the helper's
`Assert.Single` after `Where`. Its actual message and source are retained;
correcting that test-helper idiom produced the subsequent build/format exits 0.
That analyzer failure is not a runtime approval counterexample. An abbreviated
capture display also projected null names/messages; original TRXs, full separate
streams and failure arrays remain intact and were read directly.

The expanded proof root is separate:
`out/source-role-integrity/20260905T214242Z-repair-f6702a00d8694d88980a1d06c366f35f/`.
Nothing replaces the original baseline.

| Phase | Executed | Passed | Failed | Native exit | Actual TRX SHA-256 |
| --- | ---: | ---: | ---: | ---: | --- |
| Expanded unchanged-production red 1 | 22 | 11 | 11 | 1 | `5DA362ABAE20B23F2A1787676058759305A0446A0DC820713FCE997207E6AD54` |
| Expanded unchanged-production red 2 | 22 | 11 | 11 | 1 | `9D048CC29E5558E22DE7FD9B64767668FB1C542D18C51456DB0946612A4C9D28` |
| First-candidate red 1 | 26 | 24 | 2 | 1 | `CBDBA705E3B19D0BA9A3B13B0E1421A2D45D32309346C6F7C317A8B67266A76C` |
| First-candidate red 2 | 26 | 24 | 2 | 1 | `AD8036CD11779E1A9BD34F883B28553A37D329FD0EC23B630D146FB352209C20` |
| Final scoped green 1 | 93 | 93 | 0 | 0 | `20FBFB80945CC7B673A9E0128C316A225AFC83224BE4D738F4E7B0E05512F49B` |
| Final scoped green 2 | 93 | 93 | 0 | 0 | `72E0B34046AA450479E2C20381AE57C45262611804C6CFA34D71843A7CB780B4` |

Each expanded red has **ten permitted controls, one already-correct refusal and
11 typed-approval counterexamples**, not 11 permitted edits. The additional
counterexamples cover an altered citation with its original in a teacher note,
conflicting duplicate Date rows, duplicate excerpt anchors/cards, altered title
with the old title still in the citation, displaced metadata, and invention
over a rendered optional `not recorded` value.

During review of the first repair candidate, four further controls were added
and measured twice against that candidate, not the untouched parent. Duplicate
exact source titles and extra invented metadata rows still returned approval;
an altered designated card could not be rescued by an original copy under a
different title, and independently titled preceding notes passed. The two
candidate defects were then corrected. The full failure messages are retained
by exact case name, using the measured message form:

```text
Expected blocking lens.structure for a changed source value or role. Synthetic edit <case>: blocking codes=[]; CanApprove after fresh acknowledgement=True; typed approval returned=True; refusal=None.
```

Here `<case>` is a displayed pattern, not a substituted primary record. Actual
TRXs preserve each complete message, stack and emitted semantic document.
The main agent read those messages and actual typed-approval observations.

The final SourceLens-specific guard captures the immutable builder-rendered
title, metadata card, excerpt heading, literal paragraph and formatted citation.
It requires one designated title/section association, exact source-body roles,
and the exact metadata field/value set with no extra or duplicate rows.
Metadata row order and absolute document indices are not frozen. Independent
notes remain possible outside the protected source body; teacher-only notices
and page breaks cannot stand in for the learner-visible excerpt/citation.
The shared substring validator and other recipe callers are unchanged.

All 26 source-role controls pass twice: 15 require blocking `lens.structure`,
no typed approval and state AwaitingTeacherReview; 11 permit approval of the
exact attempted revision. Both 93-case batches additionally cover 67 existing
SourceLens/catalog/review controls, not the full solution. Every edited case
requires fresh acknowledgements. Appended notes use an edited immutable draft
and a new ReviewSession because no UI append operation is asserted. No test
sends an approved fixture to a render, save, print or export sink.

The first expanded preparatory build also failed CA1861 at
`SourceLensReviewIntegrityTests.cs(294,47)`:
`Prefer 'static readonly' fields over constant array arguments if the called method is called repeatedly and is not mutating the passed array`.
That constant-array test arrangement was corrected before the two expanded
runtime measurements. Its failed build and original bytes remain retained;
it is separate from the initial baseline's xUnit2031 and the typed-approval
counterexamples.

All six scoped receipts preserve 366 source/build-control inputs and 119 Unit
output files unchanged within each run and within each same-stage pair. The
main agent compared the complete ordered inventories directly; null
`Compare-Object` outputs in the summary are not treated as invented arrays.
Build stages intentionally change outputs and are not called identity-stable.
Only the owned catalog and control source differ between the expanded baseline
and final source inventories. Scoped Unit and production format fix/verify
exited 0; the final warnings-as-errors rebuild records zero warnings/errors.
All native stdout/stderr streams are retained separately.

The main agent rehashed all 91 non-self evidence files with no mismatch.
Final summary SHA-256:
`2FABF3803B9BA83B2807C0D3CF02F806B835D1D71D9C970C02ABD1BB90BF777E`;
inventory:
`AE5128B9DDA71855838304B2A600414CCFBFB42AAA03BDF234B9AF33BB59F50A`.
Final raw catalog:
`DC604C1597FF7AB21B2AC5E39A37F9DB91DF5AF4188DE8D99E5C512D73AF43DE`;
new control source:
`726BD6D9BAAF1FAE58A320D4162B0B9F3D092F44E7EB8AC4DE1EEF807297B755`;
focused Unit DLL:
`169918CE76B8A87FFACD672140C833CC6BF9C52653473367C6BDF4A82283CA67`;
its BuiltIn dependency:
`D4651D07F7B51CACD56EFF8299D1F6647301EFA78DA14604424CA0E8A34A2E27`.
These are the focused cutoff, not prospective full-suite or hosted identities.

## Compatibility and authority boundary

This changes the admitted `source-lens@0.1.0` validator contract. It is a sixth
direct changed identity in the scoped inventory, alongside Lesson Loom,
charts, learner-held goal sheets, calibration and flashcards—not a complete
C1-to-present equivalence audit. Unchanged default builder bytes or recipe
manifest fields do not exempt an editor/validator change. Original C1/C2
records and samples remain frozen; no replacement version, routing choice,
schema-2 writer, migration or new admission is inferred.

The comparison is revision-local preservation, not source authenticity,
disciplinary or standards accuracy, rights clearance, durable verification of
the caller's Boolean assertion, or qualified human review. Unknown rights
retain their existing warning semantics; no new rights-to-sink policy is
invented. Source corrections need a new build from corrected source inputs and
fresh review, with compatibility disposition still held.

Independent read-only review found no actionable defect or evidence mismatch
within this bounded comparison. It checked all eight original/expanded/candidate/
final TRXs, actual semantic documents, both inventories and current scoped input/
output identities. SourceLens.cs and shared validation remain unchanged. This is
neither full-suite closure nor recipe admission.

## Exact PR21 hosted evidence

The [evidence ledger](../evidence/evidence-ledger.json) is the hosted-conclusion
authority. Both attempt-1 pull-request runs bind exact head
`8857a6474cc17cdfc7357dc56665b45dcced4fac`, created
`2026-09-05T21:15:26Z`. The CI receipt and CodeQL checkout log bind synthetic
merge `782e85ef901d62da06ecb789abc04e6e367e90db`, that head into
`80f52b09571b4d190ffbb89f63cbe0f43f317fa7`. This is not main or later local
source-role proof. No rerun was dispatched.

CI failed: seven actual TRXs retain **2,551/2,578 passed, 27 failed**,
with 1,285/1,311 Unit and 319/320 Integration; the other five suites pass.
No skips, errors or aborts. The first native caller failure was
`Foundry.Tests.Unit.CiTestRunnerContractTests.Repository_state_hashes_tracked_and_untracked_bytes_but_excludes_ignored_outputs`,
reported duration 3.4113523 seconds:

```text
Foundry.Tests.Unit.FixtureProcessException : Primary: DisposalFailure: fixture ownership did not settle cleanly.
NativeExit: 0; RootExitObserved: True; DescendantExit: NotEstablished
CleanupSettled: True; CaptureSettled: True; DisposalSettled: False; SafeToStartAnotherFixture: False
DisposalObservation: Stage: Queued; DeferredReasons: None; SharedBudgetMs: 500; DecisionElapsedMs: 0; RemainingAtDecisionMs: 500; WaitElapsedMs: 0; RemainingAtWaitMs: 500; CallbackEntryElapsedMs: NotObserved; CallbackExitElapsedMs: NotObserved; SnapshotElapsedMs: 532; TaskCompletionAtSnapshot: False; TaskFaultAtSnapshot: False; WaitReturnedSettled: False; TimelySettlement: NotEstablishedByObservations
Secondary outcomes:
DisposalDeadline: the owned disposal operation remains unsettled.
```

Stdout retains the complete synthetic repository-state JSON, EOF/nontruncated.
Stderr is EOF/nontruncated and retains the fixture `.gitignore` LF-to-CRLF
warning; it is not empty. The next **24** caller failures preserve that exact
description, prefixed by:

```text
System.InvalidOperationException : A prior fixture has uncertain cleanup/capture; no new process was created. A fresh test host is required after ownership is resolved.
```

The main agent independently compared all 24 complete decoded suffixes using
ordinal equality. These are refusals, not 24 new native process failures.
Queued means callback entry was unobserved in the immutable snapshot, not a
diagnosed scheduler state. The final elapsed reading and sequential task/progress
observations establish neither exact later completion nor a scheduling/OS cause.
All 24 formerly failing PR20 names recur, but **none** of their complete messages
equals PR20: the first result and synthetic output changed and diagnostics were
added. The earlier PR19/PR20 message-equality finding is not carried forward.

A distinct synthetic failure,
`Foundry.Tests.Unit.FixtureProcessRunnerTests.Disposal_observation_keeps_zero_elapsed_entry_exit_and_task_completion_distinct`,
lasted 2.2193163 seconds. The full message is
`Assert.True() Failure / Expected: True / Actual: False` at the safe-reuse
assertion (then lines 472/483). Actual stdout instead records
`Primary: StreamDrainTimeout: redirected streams did not finish within the separate drain budget.`
Root exit/cleanup are observed/settled, but capture/disposal/safe are false.
Disposal is Deferred for CaptureUnsettled, with clock 0 and 250 ms remaining;
no disposal wait or callback entry/exit was observed. Both stream snapshots
are Pending, empty and nontruncated. Secondary outcomes retain the drain
deadline, missed capture-settlement wait, unsettled capture and deferred disposal.
This is not the caller's Queued-disposal signature.

The separate Integration failure,
`Foundry.Tests.Integration.EdgePdfExporterTests.A_completed_launcher_gets_a_bounded_handoff_for_its_pdf_child`,
retains only
`A child-owned partial PDF was mistaken for launcher failure or completion.`
and the stack at then line 240. Its 5.3502240-second duration is whole-test
timing, not a measured waiter elapsed time at that assertion. The original
`IsCompleted` assertion cannot distinguish success, fault or cancellation;
the task outcome was not retained. No particular fault is retroactively supplied.

The outer receipt `20260905T211837Z-1318b2f3976249b8bb7bbce55297d6d5`
records native/runner 1/1, no outer timeout, 346,558 ms within 900 seconds,
root/drain/safe=true, empty identity errors and two completeness errors naming
the coherent failed Integration and Unit suites. All clean 543-source and seven
assembly snapshots agree before/after. This does not settle the inner fixture,
recover remote DLL bytes or independently prove source-to-binary provenance.

Secret scan passed. Sample generation/comparison, portable samples, coverage
threshold, dependency/vulnerability and SBOM gates were skipped. Actual TRXs,
coverage roots, result/definition/entry/execution/storage identities and retained
copies were independently inspected. All 185 files in the retained manifest
were rehashed by the main agent with zero mismatch. No missing gate is borrowed
from another head.

CodeQL succeeded. Actual uncompressed `csharp.sarif`: 637,842 bytes,
SARIF 2.1.0, CodeQL 2.26.4, one successful invocation, zero results and 443
execution notifications all level none. No configuration-notification or
embedded `versionControlProvenance` property is invented. Run/artifact/archive/
checkout evidence supplies the limited binding; the native gate reports
`CodeQL SARIF clean across 1 file(s).` This does not override paired red CI.

Ignored hosted root: `out/source-role-hosted/`.

| Retained evidence | SHA-256 |
| --- | --- |
| CI complete independent audit | `95B86D5E11A92EA59E311C6E179C8D1034A4C0F72760806C84EAF15FE70682CF` |
| 185-file manifest | `55AF3AFEA246AAC0BD5AA2912FDC5B72E7DE79CA4081BD15782B81BC2E3D70E9` |
| Actual bounded summary | `0132E5823C5382D1A9DF8110C3D7DFEFA920300EE2A484C1573C9C7A26A0F7C2` |
| Actual Unit TRX | `7D61B9892390DE58F753A858FE1E09D758F32721121E570BF4C7045F7D9AAC68` |
| Actual Integration TRX | `497BAE9AD210501C388C860674972005987C09DB2E4F83BBA7534B8B36946B20` |
| Actual CodeQL SARIF, not ZIP | `8A8B4CC7B38FD9DF801EE825C941FDBE574A218F59BA768ABE1F4EE03CCFFF96` |

The audit lives under
`33992504610/20260905T212606Z-01c52afc3d3d422aa569e06a3dd8e9fd/AUDIT.md`.
The initial capture's false date-preflight rejection is retained separately:
PowerShell auto-converted a matching date before a string comparison. A
separately named corrected instrument preserved the original and read the
matching identity. This was not an actual head mismatch or a CI rerun.
Read-only display projections also needed corrected API field names and
explicit XML text fields; actual primary artifacts remained intact.

## Isolated local non-reproductions

Before the repair rebuild, three exact selectors ran serially with
`--no-build --no-restore`, native exit 0 and actual 1/1 each:

| Selector | Actual duration, seconds | TRX SHA-256 |
| --- | ---: | --- |
| Repository-state hashing caller | 1.4747742 | `9EF4D5E06CFE4D40874BFD99DE02E3678489A965826774320C2742A796C5CE47` |
| Zero-elapsed disposal control | 0.0108847 | `CD2162CC32D9B6BAD6950FDDE46E3545ACDB8F34005C320A31C1F99DEF1B1FF0` |
| Completed-launcher PDF handoff | 1.1845177 | `47AA53672961E629CE246BC72F4A76CD1173EDD2E9305D4870A217E6866A00A2` |

Every before/after pair, and all three pairs, retain identical 366 source/build
control inputs and 256 Release output files (119 Unit, 137 Integration).
The scoped source inventory is not a full repository asset/history inventory.
The main agent read all three actual TRXs, compared retained snapshots, and
rehashed all 16 non-self inventory files without mismatch. The auditor's final
622-input/output rehash occurred before releasing the freeze; later rebuilding
is not represented as unchanged bytes.

Unit DLL was `936EA0D8039A52E94967F9EBFBAC8A7BB5E036BCF63EEF9F744B7AEECFA5342D`;
Integration was `6B2301FFE98011418695B1F32031193D8F0063B3B055D1428DE78B0A2375376C`.
These local bytes/load are not equivalent hosted replay, a diagnosis or a cure.
[S-15, S-16 and S-17](../evidence/sightings-register.md) remain open.

Ignored root: `out/source-role-isolated/20260905T213219Z-recovery-audit/`.
Detached `inventory.json` SHA-256:
`571125A18D306029B35AAF1FE3F37F808C85D80C5E957389493FCC80B8FE3850`.
No rebuild, changed deadline or additional experiment occurred in that window.

## Test-instrument correction and repeated local verification

**Later 5 September 2026:** two test-only corrections preserve the original
identifiers and all production/helper timing policy.

The zero-elapsed disposal control now retains a known-unstarted synthetic
adapter. It asserts the exact startup-failure result, unavailable streams, no
root wait/exit, exactly one disposal, and distinct zero decision/wait/callback/
snapshot observations. It captures the exact StartedOperations aggregate before
assertions and awaits that aggregate under the existing two-second control
bound before any fallback adapter cleanup. The absence control still requires
null callback observations; its decision/snapshot values are explicitly zero.
This isolates one observation contract, not started-process capture scheduling.
FixtureProcessRunner, its default scheduler and all limits are unchanged.

The PDF handoff test records sequential phase/status/elapsed observations,
awaits an observed terminal task so its actual fault or cancellation survives,
and rejects successful completion while output is partial. A pending checkpoint
is not a full-test pass: the exact waiter is still awaited after complete output
is written. Four synthetic task-state controls preserve pending, success,
fault-instance and cancellation-token distinctions. A separate real waiter
with pre-created incomplete output records the actual timeout, Faulted status,
IsCompleted=true and IsCompletedSuccessfully=false. It is a current counterexample
to conflating completion with success, not the missing historical S-17 outcome.

The added cleanup awaits the exact owned waiter before deleting its owned
directory and preserves the exception caught by the outer test. Cleanup-only
failures still fail. Independent source review found no actionable defect in
that bounded correction. The pre-existing ReplaceTextWithRetryAsync helper
can still mask its final move exception if subsequent temporary-file deletion
also fails; this residual is outside the new cleanup guarantee and is not an
observed hosted cause. No outer-clock acceptance gate, native Edge child,
longer timeout, production exporter or policy change is introduced.

The first common preparation build stopped at native exit 1 with two xUnit2032
analyzer errors at EdgePdfExporterTests.cs then lines 361 and 368:

```text
The naming of Assert.IsAssignableFrom can be confusing. An overload of Assert.IsType is available with an exact match flag which can be set to false to perform the same operation.
```

The failed build's complete separate streams and pre-correction source remain
under `out/source-role-close/preparation-20260905T223007Z-1a05f80000314a49ac935790e8cd1730/`.
The original source SHA-256 is
`FE626C45A017EE81DA44E709BF225D5F4227C83A50E56AFAEAF315C90AC8FC4E`.
Only the two assertion spellings changed to IsType with exactMatch=false.
The following preparation root,
`preparation-20260905T223051Z-34f2881995ec457695beaa7b88e2dc2c/`,
records diff check, warnings-as-errors Release build, full format fix,
verify-no-changes and post-format rebuild all native 0, zero build warnings/
errors. No runtime test was executed by the failed preparation.

Four subsequent scoped runs used --no-build --no-restore, serially:

| Actual batch | Passed/executed | Native exit | Actual TRX SHA-256 |
| --- | ---: | ---: | --- |
| Unit A | 91/91 | 0 | `F2DE4A8A6BBBE8DF95B5440B9A2AF38A79F737D781B02D8D4A75D27F4537B646` |
| Integration A | 16/16 | 0 | `3D7ED072685EF58DE709A24B4907E6147DBB9446AF14DF9F93F4904F91B1F10D` |
| Unit B | 91/91 | 0 | `8295C69CCCF87ED349E73238A8A1ACC89EC1FF94928DB4AA26F526EA1DB108BF` |
| Integration B | 16/16 | 0 | `91D2BB3802F11BC4CDCE7175E6C5A3150895451A902FDA60F446C01A274797C9` |

Each Unit batch contains 32 fixture-runner, 26 SourceLens-role and 33 document/
record controls; each Integration batch is the full EdgePdfExporterTests class.
These are not full-solution results. The main agent read the actual counters,
class/case identities and new-control output in both rounds. Both real timeout
controls retain the exact message
`The PDF did not become complete before the export timeout.`
Both handoff checkpoints retain WaitingForActivation before complete replacement
and successful final await. Those measured local outcomes do not substitute for
PR21 or close S-15/S-16/S-17.

All eight before/after repository digest/count snapshots match: parent
`8857a6474cc17cdfc7357dc56665b45dcced4fac`, 12 dirty entries, 546 source files,
source-content digest
`E3CE910BACBB09E3425F23622571E983EAFD53625AB453B8F9777A32F7A20E10`.
These repository objects are digest/count snapshots, not retained per-file
lists. The separate 366-input and 256-output inventories are full scoped
per-file lists; all within-run and cross-run comparisons match. An independent
current-byte rehash checked all 622 scoped files with zero mismatch before
documentation edits were released.

The retained scoped bundle is
`out/source-role-close/scoped-20260905t223212z-0aa4377fb19147f29e1fab47ce1973f6/`.
It preserves actual TRXs/streams/receipts, source and three assembly copies,
not just a summary. Its 39-file non-self inventory SHA-256 is
`9A6764040CD458F34368956D249F3EDDD2E9B6475532B2D0761B8012153A4E47`;
summary SHA-256:
`7A33B34D10E3BFB3767AB3299274A5D7BF9D57364FE8B8FEFDA473C3139FD7E6`.

At this scoped cutoff, raw FixtureProcessRunnerTests.cs is
`D5B19CAFDE8176AC0E8EDE1FFAF4F488FBD32C48859342CFA634F5CC49B0F131`;
EdgePdfExporterTests.cs is
`10F1E46CDEA9CD0DA3CCAFE2476355203D9767BB9AD321A2DED725730174C22C`.
Unit DLL is
`F325491139931BA7BA727D501E126AB5CCF319FC361166E5168D69F8742CEA5D`;
Integration DLL is
`F2BB93FA2740FD9B7AD1F84A3BE59288B9A191F0D2B28FE50F92BA1D40C4CA65`.
The catalog/control source and BuiltIn DLL equal the earlier final SourceLens
cutoff. The unchanged helper is
`987DC8C82AE532D2761EB9D0AF7BE5B785A351A4FC8428639332F1F8955499E4`;
unchanged production exporter:
`F140D550E81D84880CEC208FF6FC892C3CF197D7D7A5DD86E54A236DF34E1091`.

The live ledger measurement at `2026-09-05T22:15:13.6314433Z` read all 83
records with zero mismatches, native exit 0. Ignored receipt:
`out/evidence-ledger-measurement/20260905T221513Z-dd9aff2526a546df9968f7a8ccae46cc.json`,
SHA-256 `6C5C2BC0187A5E5D7610319AA5885795AA6D9E814E249BE3BA9820B557693354`.
It does not make red CI green or establish future hosted conclusions.

## Full local closing — 5 September 2026

~~Full closing was pending at the earlier preparation cutoff.~~ **Later 5
September 2026:** the common closing root
`out/source-role-close/closing-20260905T223849Z-1a7dc4c961a145439eb94a7170d46f83/`
records all seven gates at native exit 0: diff check, warnings-as-errors Release
build, full format fix, verify-no-changes, post-format rebuild, full A and
full B. Build logs retain zero warnings/errors. Both complete test runs report
`Test Run Successful.` for every suite.

| Local full run | Passed/executed | Native/runner exits | Process elapsed ms | Summary SHA-256 |
| --- | ---: | --- | ---: | --- |
| `20260905T223938Z-1c0cd03335bd4b34b8e1d5c2fade1620` | 2,609/2,609 | 0/0 | 211,616 | `BBFDB35A0BEF469073038C87330F5D61ADB82E493276820562C5369220E95EB9` |
| `20260905T224335Z-becee626fc5e4ff98610d49eef357b33` | 2,609/2,609 | 0/0 | 212,696 | `7AA1B095F7A26C26D5DF4876EC75505FC72FD8FDDBCAED3C80D0D8A5D20E5D0F` |

Each actual seven-TRX set contains Accessibility 26, Contract 175,
Instructional Evals 336, Integration 325, Rendering 121, UI Automation 289,
and Unit 1,337, all passed. The main agent read each actual TRX's counters,
result/definition/entry counts and empty failure set, and rehashed all 14
retained TRX/coverage copies per run without mismatch. Unit TRXs are
`302D3AD69921BF18543CF7078A26D6D4E9A15C0B09C7EA7288E46F98908F018E`
and
`48FD1F5B21E1EC714762D752FD41F1B95860C50245820F5DC5C47BB159A190BF`;
Integration TRXs are
`645C3BB67DDF54B6BFF10B28F7BCE2821573E6424E8C97C78805A87CF42B109A`
and
`E07CF1DC1582E4B0FFAB1A91045DB759BD0D092012396328363E719E95FB6852`.
All actual primary evidence remains beneath the respective
`out/ci-test-run/<run-id>/` root.

Neither run hit the unchanged 900-second outer cap. Both record observed
parent exit, completed output drain, safe continuation, no taskkill, empty
identity/completeness errors and no evidence-snapshot error. All four
repository digest/count snapshots match: 546 files, 12 dirty entries,
parent `8857a6474cc17cdfc7357dc56665b45dcced4fac`, content digest
`FB0F58A694F3648FD47377D5061EEF4867F13B17AE9248C4AAA03637B1F32DB1`.
All four seven-test-assembly inventories match. Unit and Integration DLLs
equal the preceding common scoped cutoff. These are observed local identities,
not independent source-to-binary provenance or equivalent hosted load.

Full B's test process finished at `2026-09-05T22:47:23.2675736Z`; the
closing wrapper finished at `22:47:31.330136Z`. The following ordinary
SampleGenerator double run used the unchanged source snapshot and 22 pinned
generator-output files. Both native exits were 0. All 40 files and 773,759
bytes per run matched, with no exclusions or first-admission baseline
difference. Frozen baseline SHA-256 remains
`DEF10A3258A2F2ABA922DF8F1BC38FC3A3209065B36F81F44C41B4FE047F4A90`.
Ignored sample root:
`out/source-role-close/samples-20260905T224746Z-09905e367c0746f6ba1aee12bc16b7bc/`.
Receipt SHA-256:
`35C5EE397A9AF0501C12652FE4335930CD8FB9999DC9D370640B76245FBD0883`;
87-file non-self inventory:
`60E3D729F19220C1AC2254E668C1183C4DA0944B1BDB52F23DEB6628AA951E2E`.
The main agent rehashed every inventory entry without mismatch.

This ordinary sample producer does not exercise the SourceLens recipe
validator and was never invoked in seeded-study mode. Byte determinism is
not visual QA, an AT/physical-print finding or compatibility admission;
the already-recorded calibration clipping remains failed. No Linux run,
coverage-threshold gate, new dependency/SBOM gate or hosted conclusion is
claimed by this local sequence.

These factual closing paragraphs, the README's correction from an internal
property label to the already-adopted Inquirywright — Source & Inquiry
display, and the detached changed-file manifest follow that full-test cutoff.
They are not retroactively represented as the tested source snapshot. Final
record checks and canonical staged-byte verification must bind the later
record-only delta before commit. Earlier frozen manifests remain unchanged.
Read-only result displays briefly needed corrected dictionary aggregation and
plain-text selection instead of serializing recursive match objects; no actual
TRX, failure message, gate verdict or retained primary record was replaced.

## Closing cutoff and unchanged holds

~~Full closing is pending at this preparation cutoff.~~ ~~The two separate test-only
instrument corrections are under implementation; no result is anticipated here.~~
**Later 5 September 2026:** their bounded implementation and repeated scoped
results are recorded above; ~~full closing is not yet claimed.~~ **Later 5
September 2026:** the completed local closing is separately recorded above.
A later factual append must state actual source/assembly cutoffs and verification.
All licensing scope/authority/assent, both Proposed ADRs, exact compatibility,
failed calibration visual QA, unadmitted console work and real human evidence
remain held. H0–H7 remain NOT BEGUN. No main merge, site dispatch, publication or
release is claimed. The House Covenant governs retained reds and exact-byte
separation; it does not confer human authority.
