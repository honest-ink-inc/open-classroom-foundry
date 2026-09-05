# Fixture-process evidence and hosted continuation

**Prepared:** 5 September 2026. **Source baseline:**
`483649cad1a8f3f233d6065aaac404dac553bdc0`. **Branch:**
`codex/fixture-process-evidence`.

This is factual engineering maintenance for I33/I34 and a read-only I20/I36
compatibility inventory. It continues, rather than rewrites, the
[previous evidence](accessibility-console-evidence.md). The complete
[work register](accepted-improvement-register.md) remains active. No real
participant, learner, credential, blind-study instrument or private record is
used. Raw diagnostic receipts identified below are retained locally under
ignored `out/`; their hashes are not a claim that those files are tracked.

## I33 — preserve the fixture failure before diagnosing it

The original private `RunPowerShell` in `CiTestRunnerContractTests` retained a
30,000 ms work wait, but followed timeout with synchronous tree-kill,
parameterless `WaitForExit`, and unbounded redirected-stream result waits.
Its eventual generic timeout assertion omitted both captured streams; a kill
or stream exception could replace that primary result. These are independently
inspected instrument defects, not a finding about what caused S-13.

A behavior-preserving adapter extraction was tested before the repair.
Two six-control runs each executed **1 passed / 5 failed**. The controls inject
process/reader outcomes; they do not reproduce a native OS timeout, blocked
pipe, or the original hosted load. Their actual TRX counters and messages were
read directly. The second run also retains the complete injected exceptions
and stacks in its test output, beyond xUnit's abbreviated assertion rendering.

Evidence root: `out/fixture-process-evidence/fixture-helper-20260905T182050Z/`.

| Retained red TRX | SHA-256 |
|---|---|
| `red-controls-01/scope.trx` | `CD03EC09E606DB7C711E477726B68A3EA62A8FDE994152F9A5363E2063FE29E0` |
| `red-controls-02/scope.trx` | `4D3D9DFDD510704F6E2893C31D1E6956C4C479311451F38574809C4C2DE02A25` |

All five failing names below have prefix
`Foundry.Tests.Unit.FixtureProcessRunnerTests.`. The assertion messages are
retained verbatim; the first two ellipses are xUnit's own abbreviated strings,
not a claim that the full inner message was unavailable in the second TRX.

```text
Timeout_retains_available_standard_output
Assert.Contains() Failure: Sub-string not found
String:    "The PowerShell evidence-fixture process e"···
Not found: "synthetic-standard-output"

Timeout_retains_available_standard_error
Assert.Contains() Failure: Sub-string not found
String:    "The PowerShell evidence-fixture process e"···
Not found: "synthetic-standard-error"

Timeout_cleanup_failure_does_not_replace_primary_timeout
Assert.Contains() Failure: Sub-string not found
String:    "synthetic-kill-failure"
Not found: "Primary: WorkTimeout"

Faulted_stdout_retains_the_separate_stderr_and_capture_failure
Assert.Contains() Failure: Sub-string not found
String:    "synthetic-stdout-read-failure"
Not found: "synthetic-standard-error"

Startup_failure_identifies_the_start_phase
Assert.Contains() Failure: Sub-string not found
String:    "synthetic-start-failure"
Not found: "Primary: StartupFailure"
```

The full timeout exception in the second red TRX is
`Xunit.Sdk.TrueException: The PowerShell evidence-fixture process exceeded 30 seconds.`
The injected kill, reader and start exceptions are respectively
`System.InvalidOperationException: synthetic-kill-failure`,
`System.IO.IOException: synthetic-stdout-read-failure`, and
`System.InvalidOperationException: synthetic-start-failure`.

### Bounded repair and its limits

~~The fixture's primary failure and captured streams can be discarded by its
own reporting path.~~ **5 September 2026:** the test-only repair carries a
structured result/exception with the primary outcome, separately retained
stdout/stderr, partial/EOF/fault/truncation state, secondary cleanup failures,
native exit when available, and observed ownership state. Successful and
expected-nonzero native results are written to xUnit output before caller
assertions. The assertion meanings and original work budget are unchanged.

The helper retains the **30,000 ms work cap**, with separate maximum waits of
2,000 ms for cleanup, 2,000 ms for drain, and a shared 500 ms cancellation/
disposal settlement budget. Each stream retains at most 1,048,576 characters;
truncation is explicitly unsuccessful, not silent loss. The shared fixture
runner refuses further creation after uncertain ownership or incomplete
capture. A missed timed wait remains a failure even if task completion is
observed immediately afterwards. Capture/cancellation and disposal race
corrections came from static review; no native occurrence of those interleavings
is claimed.

This is bounded waiting, not a hard real-time or OS-call interruption guarantee.
`Start` and the native bounded root wait remain synchronous OS boundaries.
Stopping a task wait does not cancel its operation. A cancellation request and
its callback task are observed separately; root exit does not prove descendant
exit. These distinctions agree with Microsoft's documented
[task-wait semantics](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.wait?view=net-10.0),
[asynchronous cancellation](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtokensource.cancelasync?view=net-10.0), and
[process-tree termination limitations](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.kill?view=net-10.0),
checked 5 September 2026. The result says `DescendantExit: NotEstablished`.
No native inherited-pipe or stalled-OS-call experiment was performed.

Only three test sources changed; no product, recipe, renderer, provider,
project schema, production deadline or console-matrix instrument changed.

| Source at focused-run cutoff | Raw SHA-256 |
|---|---|
| `tests/Unit/CiTestRunnerContractTests.cs` | `8B04F9B5DF872BE6FCBF31B75175627D6C7F655473D81410C37468329F68D0CB` |
| `tests/Unit/FixtureProcessRunner.cs` | `68162E6686A16A7F71FB942A35CB130727035A07F4A30F5C6D37D7151251E93A` |
| `tests/Unit/FixtureProcessRunnerTests.cs` | `8CF5C79B467ECF5B3A1342410C661163FE75964A50C49157058125FE990D30E4` |

### Focused execution, not the full closing sequence

Scoped Release build, format fix and format verification each completed with
native exit 0. Preparatory compile/analyzer failures remain in the named
`red-build-*` and `green-build-*` directories; they are not erased or called
successful. Two final fresh test processes used the unchanged selector
`FullyQualifiedName~Foundry.Tests.Unit.FixtureProcessRunnerTests|FullyQualifiedName~Foundry.Tests.Unit.CiTestRunnerContractTests`.
Both actual TRXs contain **51/51 passed**, no failed/error/aborted/skipped cases,
and native exit 0. The paired inventories bind eight named inputs and 119 Unit
Release output files, not every repository dependency or the whole worktree.
Before/after identities match, and an independent read-only audit rehashed all
127 entries without a mismatch. Unit DLL SHA-256:
`E7599A1A5C2AE3F4278CFDFE88A9D831F6FB14ACFB4D042AFF229D2FB91CE0AB`.

| Focused run | Elapsed ms | TRX SHA-256 | Receipt SHA-256 |
|---|---:|---|---|
| `final-regression-01` | 32197 | `7D0776F0CF3F23AD7BDB071B772D32D3DA99928BA4853C0DDD1989AE6787EB14` | `0A6FF6DCD70940D8D35D202BE066B9F795D5FECFC0B8BE777CEBDCFA93A3D40B` |
| `final-regression-02` | 32174 | `F85463EF8A6FD20C59F47C14C41FF8C14E12E78B2229A10D1BDB7822C15A0D20` | `DAF5B901793D691D41238A7DA26564FC32D16AB17685D76170DE159BA361C502` |

Each final run includes one native childless PowerShell smoke: owned PIDs
39476 and 35976, measured helper durations 30,353 and 30,362 ms, primary
`WorkTimeout` at 30,000 ms, native exit -1, matching stdout PID marker and
separate stderr marker, observed root exit and completed owned operations.
The other helper cases use injected adapters; the existing caller regressions
also run native synthetic PowerShell evidence fixtures. Native childless
success does not establish descendant cleanup, S-13 causation or N-02.
The focused wrapper has no independent outer process supervisor; the full
solution runner's separate cap and receipt remain necessary below.

## I34 — exact predecessor hosted evidence

The [ledger](../evidence/evidence-ledger.json) now records the own-run conclusions
for draft PR18 head `483649cad1a8f3f233d6065aaac404dac553bdc0`, attempt 1:
[CI 33983187473](https://github.com/honest-ink-inc/open-classroom-foundry/actions/runs/33983187473)
and [CodeQL 33983187527](https://github.com/honest-ink-inc/open-classroom-foundry/actions/runs/33983187527)
both completed successfully. Neither is evidence for this later helper tree.

CI's three jobs succeeded. Its clean synthetic merge
`153bb8bb0acfced42b2e861ba2cb2f4b29b66e6d` has parents `4970399...` and
`483649c...`; the full parent identities were read from GitHub and retained in
the local inspection receipt. Both that merge and PR head have tree
`2b589e5f2bd949277a9e5f56235833f5ece5790c`. This is not a merge into `main`.
The retained summary and all seven actual TRXs measured **2,510/2,510**,
zero non-passes, native/runner exit 0, no timeout at 343,395 ms of 900 seconds,
stable source/assembly identities, and no identity/completeness errors.
The duplicate summary and TRX copies in both downloaded artifacts match
directly. Summary SHA-256:
`A302C4E6E7AA39A7B40CA2D277C5DD496E7736C562B0F669E8ADDE03C61EA769`.

Actual logs record 92.7% core/module coverage and a 130-commit secret scan with
no leaks. The Windows and Linux producers each passed their 40-file twin gate;
the downloaded Windows/Linux files were independently compared by complete
bytes, all 40 matched, and both inventories matched the original baseline
`DEF10A3258A2F2ABA922DF8F1BC38FC3A3209065B36F81F44C41B4FE047F4A90`.
Those downloaded producer files do not independently re-execute either hosted
twin generator. Dependency/vulnerability and scoped SBOM gates passed; the
SBOMs describe repository/build and application-project dependencies, not the
whole OS/framework or a release artifact. A nonfatal artifact-download Node
Buffer deprecation warning remains in the log; the workflow is not described
as having no warnings of any kind.

The inspected CodeQL SARIF is 2.1.0, one run, zero results, a successful
invocation, and 441 tool notifications all at level `none` (zero warnings or
errors). Its uncompressed 637,120-byte file hashes to
`F6E864577D9FA1ECCCE80B7C88880986C13B65D70483E64C56834CE0A6CF617C`.
The uploaded archive digest is separately
`0dc06e2a1939b2a9f4cd7b344d26ce85c71698f780706e3e1bed9e0d3ef264ef`;
these hashes name different objects. Two matching reads are not two analyses.

Local hosted receipt root: `out/fixture-process-hosted/`.

| Retained inspection file | Raw SHA-256 |
|---|---|
| `33983187473/inspection-receipt.json` | `19D8FA9D4E0B45587F8102DFB4811918C2EE86256FF26BA02F12C15E14285267` |
| `33983187473/cross-os-inspection.json` | `2FD7E4AFA35A706B9AF10C769C430982AF1428DE4264B3876E65ACB537937609` |
| `33983187473/full-ci.stdout.log` | `3595E10E8221D30851D88AB8FAB580341579D43300960A260976107421074059` |
| `33983187527/inspection-receipt.json` | `CD8CFAA50AB85A7E9FE5FA51728FC638AC0E4D413A7AFAAA3657CFA5453D73D8` |

## I20/I36 — affected contracts, not transitive version assignments

A read-only audit of the predecessor against ratified C1
`5cae09dcb40628265d51912aea98304557abfda6` confirms two direct same-input
differences for PR18's two I20 behavioral changes. This inventory is not an
exhaustive audit of every other change since C1 or of the earlier stacked
implementation proposals:

| Admitted identity | Differing surface | What unchanged defaults do not prove |
|---|---|---|
| `press.calibration@0.1.0` | The selected low-ink transform now preserves the solid F rectangle; C1 emptied it. Ordinary calibration producer source is unchanged. | Default parameters with low ink selected still differ. Ordinary default samples do not exercise that toggle. |
| `press.flashcards@0.1.0` | For entered terms/answers beyond the existing `Length > 40` condition, Gate B now receives the builder's existing overflow issue; C1 discarded it. | Default pairs do not trigger the heuristic. Two current-code catalog paths are not historical equivalence evidence. |

The catalog's 56 entries across 22 recipe identities share the review/low-ink
path in `PressRoomForm`. Static producer inspection found no other built-in
filled rectangle. For documents with no filled rectangle this particular
expression change is a no-op; that is not exhaustive cross-version contract
equivalence. Flashcards alone currently supplies catalog builder issues.

Big Print can receive a freshly approved low-ink calibration document. Its
unchanged translator preserves that different input; different composed output
does not establish a changed same-input `press.big-print` contract. Saving and
reopening likewise expose the changed document/warning code, but reopening
does not rerun low ink or trust stored notices. Fresh portable editing uses
`portable-semantic-editor@1.0.0`. Export/print consume reviewed content; booklet
export is not the separately named booklet-guide recipe. Public `LowInkPress`
can accept other filled-rectangle documents, but no shipped low-ink-on-reopened
or edited-document call was found. StudioSampler uses ordinary defaults;
ClassSets requires a seed absent from calibration/flashcards.

The required disposition therefore starts with calibration and flashcards,
retains shared-transform and downstream dependencies, and must preserve the
outgoing admitted contracts while choosing exact replacement identities and
routing where needed. It must not automatically bump every reached identity.
No replacement recipe/engine/schema/evaluation tuple was selected here. The
existing calibration instructions still fail visual QA; vector scaling and
flashcard heuristic questions remain unmeasured beyond the previous record.

### Earlier accepted-improvement stack — separately scoped

A second static comparison covers advisory baseline
`554ec87b256c5cbd8f6efba453070f27941c9257` through
`4970399bc1505b3bff453187891c1b72901c84fc`, excluding the two later I20
changes above. It identifies three further direct same-input differences.
This is source inspection, not a new runtime reproduction or an exhaustive
C1-to-present compatibility proof.

| Item / admitted identity | Source-confirmed change | Admission boundary |
|---|---|---|
| I06 / `lesson-loom@0.1.0` | `LessonLoom.cs` adds blocking `loom.evidence` for missing learning evidence and widens phase sums. `ModuleStudioCatalog.cs` also widens the edited-table review sum, preventing wraparound to the available minutes. | Builder and validator behavior differ, including Gate B after edits. Default output preservation is not equivalence. |
| I14 / `press.charts@0.1.0` (`bar-chart`) | `ChartPress.cs` omits the zero-area rectangle while retaining its label/value and widens grid-step/axis arithmetic for large admitted integers in both orientations. | Builder geometry differs. The unchanged structural validator receives a different document. |
| I25 / `press.learner-held@0.1.0` (`goal-post`) | `LearnerHeldKit.cs` paginates overflowing and precision-sensitive prompt layouts and refuses nonfinite, negative or zero-capacity margins. | Page count, geometry and refusal behavior differ. No same-input difference was established for the sibling `portfolio-passport` or `strategy-shelf` modes; they share one declared recipe identity. |

Two other changed surfaces require explicit classification without treating
every consuming recipe as automatically replaced:

- I15's `TranscriptSession.Tokens` now exposes a live read-only wrapper,
  preventing cast-based removal/replacement of unresolved tokens. This is a
  public mutation-boundary repair. The shipped Board-to-Brief intake only reads
  that view and uses explicit resolution; the diff does not demonstrate a
  changed builder result for the same verified lines.
- I18's three library-save hosts retain approval and report expected I/O,
  access or cancellation failures, with Green-save authorization before the
  injected operation. This is observable shared failure handling, not
  test-only work. Successful persistence/build/render algorithms are unchanged
  by that diff. The Windows print seam now rejects pre-cancellation before
  temporary output or rasterization; the actual `AppServices.Print` caller
  still supplies `CancellationToken.None`, so no new UI print-cancel workflow
  or changed same-input paper result is established.

The save hosts reach the 23 `press.*` identities (including Big Print), the
reopened `portable-semantic-editor@1.0.0`, four `all-aboard.*` recipes, and the
eight available Green studio modes: Board to Brief, Directions Duet, the two
Scaffold Smith modes, Talk Moves, Lesson Loom, Source Lens and Family Bridge.
Unavailable Access Remix and the two unavailable Amber modes are not
additional demonstrated Green-save routes. Changed documents can change later
Big Print/export/package bytes by supplying different inputs, without proving
changed same-input downstream algorithms. No `Foundry.Rendering` source or
unavailable-provider production behavior changed in this comparison.

The exact owner disposition must therefore cover these three direct identities
as well as calibration and flashcards, and separately classify transcript and
shared failure semantics. It must name any replacement engine/recipe/output-
schema/evaluation identities and preservation/selection/migration treatment.
C1 supplies no blanket invariant-repair exception. The held stack must not be
merged merely because these inventories or ordinary samples exist.

## Initial full run — retained red

The first full closing attempt passed Release build, format fix/verification
and the post-format rebuild (all native exit 0; both builds zero warnings and
errors), then retained **2,530/2,531** in
`out/ci-test-run/20260905T185848Z-526fb07be46944d28283bec835ca6dd4/`.
Native test and runner exits are both 1. All seven actual TRXs are complete;
Unit is 1,263/1,264 and the other six suites pass. No case is skipped, errored
or aborted. The one completeness error identifies the coherently failed Unit
suite, not a missing TRX. At 192,872 ms of the unchanged 900-second cap there
was no outer timeout. Root exit, output drain and eligibility for another
runner were observed; identity errors are empty. The 11-entry dirty tree at
`483649c...` has 536 measured source files, unchanged before/after digest
`EEB3ED33ED8D96EEF4BF57691B2300CB57AE51BFE4ED658E0AB3E408236ECD85`.
The Unit DLL still equals the focused-run hash above.

Summary SHA-256:
`366095109DE2A77E988332A74C22F88A9803C1BBDFC91D167671B92E24520B00`.
Failed Unit TRX SHA-256:
`B5B7741FF207B53AA2C2B510EF558EAE33459B34E9D245320F183EE5E795E9C9`.
The sole failure, duration **1.2398866 seconds**, is:

```text
Foundry.Tests.Unit.FixtureProcessRunnerTests.Root_exit_does_not_stand_in_for_stream_eof_or_descendant_exit
Assert.True() Failure
Expected: True
Actual:   False
```

The retained stack points to `FixtureProcessRunnerTests.cs:149`, the
unconditional `Assert.True(result.CaptureSettled)`. The complete diagnostic
distinguishes the real observed states rather than losing them:

```text
Primary: StreamDrainTimeout: redirected streams did not finish within the separate drain budget.
NativeExit: 0; RootExitObserved: True; DescendantExit: NotEstablished
CleanupSettled: True; CaptureSettled: False; DisposalSettled: False; SafeToStartAnotherFixture: False
DrainDeadline: output below is a partial snapshot, not claimed EOF.
CaptureSettlementDeadline: later task completion does not undo the missed settlement budget.
CaptureSettlement: at least one read or cancellation operation remains unsettled.
DisposalDeferred: ownership is retained because a root or operation is unresolved, or no settlement budget remains.
```

The later stdout snapshot in that same report is `Canceled`, with
`synthetic-pending-prefix` and the full `TaskCanceledException` stack; stderr
is `Eof` with `synthetic-standard-error`. Those separately observed milestones
do not establish that capture/cancellation had already settled when its earlier
flag was recorded. The helper retained the missed 250 ms control-settlement
wait and refused reuse. The test instead required immediate settlement.

Before any edit or rebuild, an exact-selector isolated replay with coverage
passed **1/1**, case duration **0.0762768 seconds**. The same Unit DLL, eight
input files and 119 output files were unchanged before/after. Its report has
capture/disposal settled but still refuses reuse after the drain timeout.
This is a same-built-bytes non-reproduction, not equivalent load or a
scheduling-cause diagnosis. It is retained under
`out/fixture-process-evidence/s14-20260905T190449Z/isolated-01/`:
TRX `FC2693C655F7C8FFE4C2DAF86AF856A302DF32CD507A298A9AD0A4803895D43D`,
receipt `35B8A96104D829ABAD6B37D5A56FEC4E63A6ADF1B9C91C707090FE007E6D714D`.
The [S-14 row](../evidence/sightings-register.md) retains this separate failure.
Neither it nor any later assertion correction is S-13 or N-02 causation.
No deadline is increased. Further correction and full closing remain pending
at this appended cutoff.

### Narrow control correction after the retained failure

**Later 5 September 2026:** only `FixtureProcessRunnerTests.cs` changed from
the first focused cutoff. Its unconditional immediate-settlement assertion
was replaced with state-dependent obligations: a settled capture must report
cancellation; an unsettled capture must retain the settlement/deferred-disposal
diagnostics and remain unsafe. Root exit, partial prefix, non-EOF, primary
drain timeout, descendant-unknown and refusal checks remain. Both variants
release and await the exact `StartedOperations` aggregate under the existing
two-second control bound, then verify that refusal remains sticky. A separate
process-free control explicitly holds read completion through cancellation;
it does not manufacture the original full-run timing or establish its cause.

No helper, caller or deadline changed. Revised test source: 23,740 bytes,
raw SHA-256 `ADBEBAE0347141F720E28701A87181D83A271D830C2B567616C4210DEC738D9B`.
Final scoped Release build and format fix/verify each exited 0. A fresh focused
coverage run passed **52/52**, with eight input and 119 output files unchanged
before/after; revised Unit DLL SHA-256:
`14BD6F4A5BA8F1EFE6A9D18987F5846AA8057960BE035FFB183A7E72163E9C60`.
At `out/fixture-process-evidence/s14-20260905T190449Z/focused-01/`, TRX SHA-256
is `A8E39A5CAD20E638A69BEFA951B5CE8D5F7C1EC2F82F4E8DBAF6AFED9EF7090A`
and receipt SHA-256 is
`AD536B5E75D13E93D453F9FC7E2315B773315561B16C11300815E68D5B43406A`.
The actual held-control result has capture unsettled, output `Pending`,
disposal deferred and reuse refused; its exact started tasks finish only after
the control releases them. This is bounded test-instrument proof, not S-14
scheduling causation, native inherited-pipe behavior or full-suite closure.

## Full closing and unchanged holds

~~Full solution closing for this continuation is pending at the preparation
cutoff.~~ **Later 5 September 2026:** after the narrow S-14 control correction,
Release build, `dotnet format` fix, `dotnet format --verify-no-changes`, and a
post-format Release rebuild each exited 0. Both builds reported zero warnings
and errors; repository warnings-as-errors remained enabled. Separate logs are
retained under `out/fixture-process-close/revised-20260905/`. Diff check also
exited 0; its Git LF-to-CRLF advisories are not compiler warnings.

Two fresh, serial full runs of `pwsh -NoProfile -File tools/run-ci-tests.ps1`
then each passed **2,532/2,532**. All fourteen actual TRXs were read: each run
contains Accessibility 26, Contract 175, Instructional Evals 336, Integration
320, Rendering 121, UI Automation 289 and Unit 1,265, with zero non-passes,
skips, errors or aborts. Native test and runner exits are 0. Neither run reached
the unchanged 900-second outer cap. Root exit, output drain and eligibility for
another runner were observed; identity/completeness errors are empty and the
snapshot error is null.

| Full receipt under `out/ci-test-run/` | Elapsed ms | `summary.json` SHA-256 |
|---|---:|---|
| `20260905T192247Z-c240e732134542ce863670cf447ab0f2` | 196572 | `999F41A35A290FB913D05B23417663951A8B4DFF10DB78C320D1CA3BF8D68C50` |
| `20260905T192726Z-64f6d847a9ff4159879a813328bbd2f4` | 249626 | `D58858C8DF5685E35D9E35F942BA9806B5D71F687F4661C1D67B67B164A88E91` |

Both runs bind the same 11-entry dirty tree at `483649c...`, with 536 measured
source files and before/after source-content digest
`FB4BA319D551EDF7FDC29177BA5ABA54A222CD69AA56546DEEA5B362E19D3558`.
All seven test-assembly identities are stable within and between runs. The
Unit DLL is the revised `14BD6F4A...` identified above. Source/index and all
externally changed test-visible inputs, including ignored project files, were
held fixed during both runs. The recorded build/rebuild preceded execution;
`--no-build` alone would not establish source-to-binary provenance.

The live ledger measurement inspected **77 entries / zero mismatches**, receipt
`out/evidence-ledger-measurement/20260905T185740Z-5110972c3e7c4a2e9c156db5eba4e657.json`,
SHA-256 `6EF68AEA6E17D69BCC88FB15E14523691AA219EA0767A8ED67CC0289B3B33F89`.
No ledger entry changed after that measurement. The final factual closure and
broader static inventory were appended after the full runs; they are not
silently represented as the earlier test-time document bytes. Final record
guards and the detached changed-file manifest are a separate closing step.
No press source changed in this test-only continuation, so the predecessor's
actual sample proof remains historical rather than a newly executed generator.

**Final record guard cutoff, 5 September 2026:** the eight selected Unit guard
classes for current truth surfaces, ledger, sightings, complete work scope,
repository hygiene, schema hold, governance terms and the first-admission
packet passed **33/33**, with no non-passes and native exit 0. The actual TRX
under `out/fixture-process-close/record-guards-20260905/` hashes to
`63D5AF624FD8E7CCD1B463A45E68EF512DA183B43953C0B03314FA67E20CC32A`.
The separate `-RequireRatified` history check exited 0 and its actual receipt
says `verified`: exact C1/C2 ancestry, six C2 paths, decision-record blob and
original sample-manifest blob are preserved. Receipt SHA-256:
`2FB5FE25E51D56B98287E737E6E97C5221C1ABC8FEEEEE4240C5019DDE7F42F2`.
These guards do not authenticate humans or establish whole-contract equivalence.
This paragraph records their later outcome; it is not claimed as their input.

The [detached changed-file manifest](fixture-process-files.json) binds the
first continuation commit's eleven non-self paths using strict UTF-8 with
literal CRLF-to-LF normalization only. Its self-hash is deliberately excluded.
Ignored raw receipts remain separately named evidence, not staged publication
content. No hosted conclusion is yet assigned to this later candidate.

All original historical manifests, C1/C2 and the sample baseline remain
untouched. S-13 and all other undiagnosed sightings remain open. The v3 console
pure controls remain a scoped pass, not native matrix admission or N-02 proof.
CC BY-SA 4.0 is the [selected proposal](2026-09-05-content-license-selection.md),
not an operative grant; exact material scope, rights and separate assent are
unsupplied. ADR-007/010 remain Proposed pending exact owner responses. All real
H0–H7 records remain NOT BEGUN. No site dispatch, publication, main merge,
version/tag, signing, installation, distribution, filing or correspondence is
performed by this continuation.
