# Fixture-disposal follow-up observations

**Prepared:** 5 September 2026. **Branch:** `codex/fixture-disposal-follow-up`.
**Starting source:** `77bb88f3e07b5d888ce0e5f0cc2601a96c8a9dee`.

This continuation addresses a diagnostic gap in I33, not a demonstrated
scheduler or native-disposal cause. The [complete work register](accepted-improvement-register.md)
remains active. The predecessor [Board record](board-review-integrity.md),
handover and detached manifest remain frozen at their own cutoffs.

## Exact PR23 hosted evidence

The [ledger](../evidence/evidence-ledger.json) records PR23 head
`77bb88f3e07b5d888ce0e5f0cc2601a96c8a9dee` separately from this later
candidate. Both attempt-1 runs are pull-request observations: CI
`34001098197` completed/failure; CodeQL `34001098268` completed/success.
They were created at `2026-09-06T00:22:04Z` and `00:22:05Z`, respectively.
Both checkout logs bind synthetic merge
`02be37f64bb52546a3c082a214136d04a7508df9`, merging that head into
`aa7410085a02359988ac70d9f5a85e2e79968295`, not main.

Seven actual TRXs contain 2,595/2,619 passes and 24 failures: Unit 1,323/1,347;
Accessibility 26, Contract 175, InstructionalEvals 336, Integration 325,
Rendering 121 and UiAutomation 289 all passed. No skipped/error/aborted tests
were recorded. The first failed selector was
`Foundry.Tests.Unit.CiTestRunnerContractTests.Evidence_snapshot_rejects_balanced_duplicates_and_omitted_suite_artifacts`,
starting `2026-09-06T00:25:53.1909088Z`, duration 4.6613171 seconds.
Its decoded Message is reproduced below, omitting only terminal empty lines:

```text
Foundry.Tests.Unit.FixtureProcessException : Primary: DisposalFailure: fixture ownership did not settle cleanly.
NativeExit: 0; RootExitObserved: True; DescendantExit: NotEstablished
CleanupSettled: True; CaptureSettled: True; DisposalSettled: False; SafeToStartAnotherFixture: False
DisposalObservation: Stage: Queued; DeferredReasons: None; SharedBudgetMs: 500; DecisionElapsedMs: 0; RemainingAtDecisionMs: 500; WaitElapsedMs: 0; RemainingAtWaitMs: 500; CallbackEntryElapsedMs: NotObserved; CallbackExitElapsedMs: NotObserved; SnapshotElapsedMs: 508; TaskCompletionAtSnapshot: False; TaskFaultAtSnapshot: False; WaitReturnedSettled: False; TimelySettlement: NotEstablishedByObservations
Secondary outcomes:
DisposalDeadline: the owned disposal operation remains unsettled.
--- stdout (Eof; truncated=False) ---
{"InventoryCount":2,"Evidence":[{"SuiteName":"Alpha","TrxIsCurrent":true,"TrxValid":true,"TrxRetainable":true,"TrxOutcome":"Completed","TrxError":null,"TrxTotal":1,"TrxExecuted":1,"TrxPassed":1,"TrxFailed":0,"TrxFailures":[],"NewCoverageCount":2,"CoverageValid":[true,true]},{"SuiteName":"Beta","TrxIsCurrent":true,"TrxValid":true,"TrxRetainable":true,"TrxOutcome":"Completed","TrxError":null,"TrxTotal":1,"TrxExecuted":1,"TrxPassed":1,"TrxFailed":0,"TrxFailures":[],"NewCoverageCount":0,"CoverageValid":[]}],"Errors":["Alpha: expected one current TRX and one new direct coverage file; found 1 TRX and 2 coverage files.","Beta: expected one current TRX and one new direct coverage file; found 1 TRX and 0 coverage files."],"Snapshot":null,"RunnerExit":null}


--- stderr (Eof; truncated=False) ---
```

The next 23 failures have identical complete Message text, with this prefix:

```text
System.InvalidOperationException : A prior fixture has uncertain cleanup/capture; no new process was created. A fresh test host is required after ownership is resolved.
```

Each suffix exactly matches the original frozen description without its
exception-type prefix. These are 23 refusals to create another process, not
23 new native failures or fresh callback observations. The retained actual
Unit TRX and independent audit preserve every full name, message and stack.
The primary Output contains ErrorInfo only; no per-test StdOut or prior
same-test success report is retained. This absence is not proof that no
unrecorded activity occurred.

All ten Board, 32 fixture-runner and 26 SourceLens controls passed on this same
hosted run, as did all 325 Integration cases. Those results do not diagnose or
close S-15, S-16 or S-17. The first selector also appears in older S-15 records,
but changed instrumentation and observations are not byte-equal historical
messages, equivalent load or a common-cause finding.

The outer receipt `20260906T002503Z-e58d31644b7144fb93aaf066cb8daadf`
records native/runner 1/1, no timeout, 325,090 ms within the unchanged
900-second cap, root exit, complete drain and safe outer ownership. Clean
551-file source snapshots and all seven assembly records match pre/post;
identity errors are empty and the sole completeness error names the coherent
failed Unit suite. This outer settlement does not settle the inner fixture,
independently prove source-to-binary provenance or establish descendant exit.
All 14 curated TRX/coverage copies were independently rehashed without mismatch.

| Primary retained file | SHA-256 |
| --- | --- |
| Bounded `summary.json` | `64B69A91E11346220BCB0F42EE1C293CB4E213F02E3B0C86C0D347E107416E52` |
| Actual `Unit.trx` | `E962DD87B1D991D6A6BB412A70E0ADB325CF50E1122D6548D5488C5ADC7EABCF` |
| Actual `csharp.sarif` | `0186A3AA60237F54CC61098535C8EECA65E97CDFD77D76EEF6E7969EEC58F07B` |

The primary CI root is
`out/board-review-hosted-pr23/34001098197/20260906T003244Z-3635f41f294f44118e74686b25f385aa/artifact-bounded-test-evidence/20260906T002503Z-e58d31644b7144fb93aaf066cb8daadf/`.
The paired CodeQL root is
`out/board-review-hosted-pr23/34001098268/20260906T003042Z-6a238f42a5e6435a98383a2617390697/`.
The independent `out/board-review-hosted-pr23/AUDIT.md` records the retained
archive and actual-XML checks. No archive code was executed.

The final detached `out/board-review-hosted-pr23/retained-files.json`
contains 208 non-self files totaling 100,391,608 bytes. Two independent complete
derivations agreed; root and independent reviewers rehashed the exact path set,
lengths and bytes with zero mismatches. The manifest SHA-256 is
`ABA63199603C107AE8CF2031A7721D72D53144493CBED6B49C507010F333DD90`;
the 15,983-byte `AUDIT.md` SHA-256 is
`CD0E6913E4285F8E53F597A6A1C2500D453DB95C0B6AF8B61BC8BA9DFFC68551`.
The separate capture-byte audit covers 11 observations, 40 native captures,
80 separate streams and 17 duplicate build-evidence files with no errors.
All original archives remain retained; the sole archive-only entry is the
inert runner-lock file. Retained authoring/display issues are not CI failures
or successful test reruns. This is read-only evidence, not a workflow dispatch.

Hosted Release build and format passed; actual build output reports zero
warnings/errors. Gitleaks 8.30.1 scanned 135 commits with no leaks. Sample
determinism, coverage threshold, dependency/vulnerability inventories, both
SBOM scopes and portable samples were skipped, not passed. No successful
dependent gate is borrowed from local or earlier hosted runs.

The actual 638,658-byte SARIF is version 2.1.0, CodeQL 2.26.4, with one
successful invocation, zero results and 445 execution notifications all
level none. Configuration-notification and embedded version-control fields
are absent; run/artifact/archive/checkout evidence supplies the binding.
Its uploaded ZIP hash is
`AAF4CA733FE7E3006AB6540525053BACACD80E948F7C93196A634A938098662F`,
not the uncompressed SARIF hash. Successful analysis does not override red CI.

## I33 — missing later-state observation

At the preparation cutoff, `Run` repeats the saved unsafe result whenever a
later caller is refused. Its original immutable snapshot is correct to remain
frozen. What is missing is a separately labeled observation of the exact
retained task and callback progress at a later refusal.

~~The bounded diagnostic addition is under implementation and verification.~~
**Struck later 5 September 2026:** implementation and repeated local closing
are complete below. The preparation scope was:
retain access to that exact task/progress, take a separate immutable
observation without waiting or starting work, and log it separately while
preserving the original exception and streams. Controls must distinguish
still queued, later callback entry/exit, task completion/fault and
absent/deferred disposal. They must prove sticky refusal, zero additional
process creation, immutable prior observations and settlement of owned
synthetic tasks after release.

No late completion may rewrite the earlier snapshot, claim the deadline was
met or clear unsafe reuse. Sequential reads are not an atomic lifecycle
sample or an exact task-completion timestamp. Even a later queued observation
does not diagnose ThreadPool starvation, host descheduling or a native cause.
Only an actual later under-load capture could supply native later-state
evidence; these proposed controls do not manufacture it.

## Closing and authority boundary

~~New focused and full verification remain pending at this preparation cutoff.~~
**Struck later 5 September 2026:** the repeated local results below supersede
only that pending status; predecessor and own-head hosted evidence stay separate.
The prior Board full pair of 2,619/2,619 and its default-sample equality are
historical proof for that code, not this diagnostic addition. Only test-helper,
caller and test-control code are in this new scope; presses, renderers,
recipes, schema and versions are not to change.

Seven earlier direct recipe changes remain held for exact compatibility
disposition. Calibration visual QA still fails; the native-console matrix
remains unadmitted. CC BY-SA 4.0 is the selected proposal only: exact licensor,
covered materials, rights and separate assent are not supplied. ADR-007 and
ADR-010 remain Proposed, schema 1 hash-less, and H0–H7 NOT BEGUN.
No real participant, consent, custody, withdrawal, appointment, quorum,
recusal, protected-seat review or council decision is invented.

The 85-row ledger committed at `77bb88f` remains its historical cutoff.
The two later own-head rows and this new continuation do not rewrite the
frozen Board manifest. No main merge, publication, release/version/tag,
signing, installation, distribution, filing or correspondence is performed.
The overall objective is not complete.

## Local closing — 5 September 2026

This is the finite engineering checkpoint requested before the Codex update,
not completion of I33 or the overall commission. The new test-helper record
retains a separate immutable observation of the exact owned disposal task,
callback-entry/exit pair and settlement clock. Task status is read before the
callback pair; the attempt counter is not emission order, an atomic sample,
or an exact completion timestamp. A stored observation can be printed later
than a newer attempt. Completion observed later does not establish timely
settlement or clear the original unsafe-reuse refusal.

The native fixture caller makes one diagnostic-write attempt on failure or
refusal and rethrows the original exception. Observation or logging failure
cannot replace that exception. There is no added explicit wait, worker,
disposal retry, timeout, safety-predicate change or new process on refusal.
Clock reads, formatting and the writer are synchronous diagnostics, not a
hard-interruptibility guarantee. Six net new helper cases plus extended
controls cover queued/entered/exited/completed/faulted transitions, callback
exit preceding terminal task state, absent/deferred ownership, immutable
old observations, sticky refusal and observer/logger failure. Owned
synthetic tasks are released and awaited. No new failing baseline was run
for this instrumentation feature; PR23 establishes the missing retained
later-state evidence, not a reproducer of a newly proven native cause.

Root's closing driver completed nine gates with native exit 0 under
`out/fixture-follow-up-close/closing-20260906T005808Z-e4ddbb8db1134274a153e9abf5e4c0c9/`:
diff check; Release warnings-as-errors build; format fix; format
`--verify-no-changes`; post-format Rebuild; two focused runs; two unfiltered
full runs. Both build logs report `0 Warning(s)` and `0 Error(s)`.
Separate native stdout/stderr and per-gate receipts are retained.
Formatting completed before the test-input freeze. No source, index,
build or test-visible input was changed during either repeated pair.
The driver completed at `2026-09-06T01:08:11.704309Z`; only then was
the freeze released for these factual closing records.

The focused pair each passed **68/68**: 38 fixture-helper and 30 runner-contract
cases. All 367 declared source/build inputs and 256 Unit/Integration Release
output files matched pre/post and between runs; independent current-byte
rehashes found zero mismatches. Thirteen new/extended control outputs matched
exactly. The childless native timeout controls measured 30,354/30,370 ms
against the unchanged 30,000-ms work limit; this is not descendant-exit or
scheduler-cause proof. These focused roots are the closing-driver identifier
in lowercase with `-a/` and `-b/` under `out/fixture-follow-up-close/`.

| Retained focused file | SHA-256 |
| --- | --- |
| A `receipt.json` | `DD55A085DCD37DF420B0108B194CBA5EA750F288A20E27731EEFD9F669A04727` |
| A actual TRX | `C5CDA2C716746C334EEE0F7259226D3D6C5EAA0C91272DEFA67534C72C7FF766` |
| B `receipt.json` | `B2569281B457CC49186BACDEA23A3715146CE0CEFB5970F1855B01C1AE523600` |
| B actual TRX | `2B7069A59B3F448A21881ACAFB712ED78CE727990770CEC872683BE266C41B1A` |

Both full runs passed **2,625/2,625**, read from all seven actual TRXs:
Unit 1,353; Accessibility 26; Contract 175; InstructionalEvals 336;
Integration 325; Rendering 121; UiAutomation 289. Actual result, definition
and entry counts agree; no failed/skipped/error/aborted tests were recorded.
Each root's 14 curated TRX/coverage copies were independently rehashed with
zero mismatches. Full A ran `01:00:59.5264655Z–01:04:21.0601760Z`
(201,533 ms), and B `01:04:46.2672645Z–01:08:03.4595800Z`
(197,192 ms), on 6 September UTC / 5 September local time.

| Full root under `out/ci-test-run/` | `summary.json` SHA-256 | Actual Unit TRX SHA-256 |
| --- | --- | --- |
| `20260906T010042Z-d8837caea73045e68c75a6526e7c0900/` | `BD0D6CCC3EB0E880123195AE3F3B7E2DC89335CBBDDE7CEA5FB82E65A2064E12` | `794FF37891B3170AC27CCC583ABFE65D3756FDDA347B63571B5037AE84255CDA` |
| `20260906T010430Z-1245fb200b234f0f86280c4ef30d10c6/` | `E98A916265150F44BA5EF16A55AE2A97DC3AECA0778092A2C93CD0EB1DA5BD3A` | `A196DC13D1B9CFC8EE48972A8657F0E766E058E90A0C4E2F6875E2885162E153` |

Both outer receipts record native/runner 0/0, no timeout or taskkill, root
exit, full drain and safe outer ownership, within the unchanged 900-second
cap. Identity/completeness errors are empty and snapshot errors null.
All four full source snapshots agree with the focused cutoffs: dirty source
at starting HEAD, 11 status entries, 553 source files, source-content SHA-256
`E0364D8EFB1E235C340A59620643C5B95799D3B741405BA17E20C36F214B18A3`
and status SHA-256
`C5E994480F87210537F8FCC79879A138C818128C6008261BBA2078A1C3701BA6`.
All seven assembly arrays match before/after and across the pair. The Unit
DLL is 1,224,192 bytes, SHA-256
`2374F927ED22D4732A9FC6B81826CA65D9FC2FC0C410C5A9D483629212A1C30A`.
Stable `--no-build` inputs are measured identity evidence, not independent
source-to-binary provenance.

The [detached changed-file manifest](fixture-disposal-follow-up-files.json)
covers eleven non-self paths in the first continuation commit containing it;
later records cannot replace that cutoff. Final documentation-only guard
runs, hook/history/secret checks, exact staged-byte checks and this candidate's
own CI/CodeQL conclusions remain post-record checks, not pre-claimed passes.
Their separate retained receipts belong under
`out/fixture-follow-up-close/`; hosted records belong in the evidence ledger.
The intended pull request is stacked on `codex/board-review-integrity`,
not merged into main. After recording the checkpoint's actual hosted status,
stop owned work for the update, without starting another repair.

This slice changes three test files and factual maintenance records only.
No presses changed and no new SampleGenerator run was needed; the older
Board sample comparison is historical. No real AT, physical print, human
review, native-console matrix, publication or release act was performed.
S-15/S-16/S-17 and the earlier nested replacement-helper residual remain open.
The house-covenant audit kept prior failures and separate cutoffs intact and
required independent byte and actual-result checks. No fictional council
decision, operative license or release admission follows from these passes.
