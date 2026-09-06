# Existing-PR integration and asynchronous fixture waits

**Prepared:** 5 September 2026 (America/New_York).
**Source baseline:** `db40e9b673bceb65cd60d0e0fb88653b02ce66c6`.
**Status:** corrected local closing verified; new own-head hosted verification pending.

## Scope and measured predecessor

The resumed user instruction prioritizes perfection of existing work. The
attached open-PR image prompted a live stack audit; it was evidence, not an
instruction source. Three independent assistant engineering reviews examined
merge identity, fixture ownership and compatibility authority. These are not
real council sessions, protected-seat findings or independently recruited
educator evidence.

The [ledger](../evidence/evidence-ledger.json) retains PR24's own failed CI and
successful CodeQL at the baseline above. S-18 in the
[sightings register](../evidence/sightings-register.md) preserves its first
StreamDrainTimeout, empty Pending streams, deferred disposal and 25 later
exact-description refusals. Disposal follow-ups are not capture-completion
observations. No isolated run on that exact hosted head was performed here;
the candidate's passes do not diagnose or erase that failure.

Only three Unit sources change executable behavior in this continuation:
`FixtureProcessRunner.cs`, `FixtureProcessRunnerTests.cs` and
`CiTestRunnerContractTests.cs`. No production source, recipe, schema, renderer,
sample baseline, dependency, work cap or protected instrument is changed.

## Controlled red before the repair

The baseline experiment added one compatibility facade,
`RunAsync => Task.FromResult(Run(...))`, around the unchanged synchronous
runner. Removing that uniquely matched facade reproduces the baseline helper
source exactly after CRLF-to-LF normalization. A known-unstarted synthetic
adapter and controlled disposal scheduler require the caller to receive a
pending task, release its exact owned disposal, and then await the result.
The old synchronous wait prevents that release before its budget expires.

The actual regression failed 0/1 twice with native exit 1:

> RunAsync consumed the disposal wait before returning control; the caller could not release its queued owned disposal.

StartupFailure remained primary; disposal was Queued/unsettled and reuse was
refused. Each control's `finally` released the exact callback and awaited its
owned task aggregate. Complete messages, stacks and separate output streams
remain under `out/fixture-async-waits/`:

| Case | Actual TRX SHA-256 |
|---|---|
| `red-01-20260906t0201` | `578B2A16FD5CA7AAEC14BF80776890CE15C846A69F335E4CE49B8D75E030E4ED` |
| `red-02-20260906t0201` | `B40A7DBF901495FF598008B27910E5AF2048F01864CB294AD945048C61813BB4` |

Both runs had 367 source/build inputs and 256 output files stable before and
after, with stable repository identity. The red source snapshots and DLL hash
were retained; the red DLL itself was not separately archived before rebuild.
This proves the controlled caller-yield defect, not hosted thread starvation,
an OS cause, or equivalent-load reproduction of S-18.

## Bounded implementation

Cleanup, drain, cancellation-settlement and disposal waits now await their
owned task versus a private deadline task. The exact selected winner is kept;
an operation's own TimeoutException is a settled fault, not a wait deadline.
Fault/cancellation settlement is still distinct from successful completion.
Only the private timer is canceled. The owned fixture operation is not canceled
by a timed wait, and later completion cannot revise the frozen failure.

Synchronous compatibility entry points and asynchronous callers share one
lock-protected predecessor/release task chain. The short queue lock is never
held across a wait or fixture operation; release occurs in `finally`, including
refusal. Native caller tests await the actual asynchronous path, not a
`Task.Run` wrapper around the whole synchronous runner. Failure follow-up still
makes one diagnostic-write attempt and preserves the original exception even
if that output channel fails.

Native Start/root WaitForExit remain synchronous boundaries. Reader startup
stays off-thread: removing that isolation could put a blocking getter or read
prefix onto the caller. Cleanup, capture startup and default disposal still
use the shared task scheduler. This slice does not promise scheduler-independent
deadlines or complete cancellation of a stalled OS operation.

The first candidate build failed CA1001 because a SemaphoreSlim field made the
runner a disposable-resource owner. No tests ran on that failed build. It was
replaced by the task-chain gate, without analyzer suppression or a misleading
runner-disposal API. The mixed sync/async control also releases a callback again
after observing its owned worker, so a delayed scheduler cannot miss an early
cleanup release. All original limits, success/safety checks, immutable stream
snapshots, sticky refusal and completed-first/zero-budget semantics remain.

## Focused verification before full closing

Unit Release, scoped three-file format fix, format verification and post-format
Rebuild all exited 0; both builds reported zero warnings/errors. Thirteen new
cases cover yielding, serialized ownership, queued-follower refusal,
fault-versus-deadline outcomes, late completion, zero-budget behavior and
diagnostic failure preservation. The original 38 helper controls remain.

| Case | Actual result | Actual TRX SHA-256 |
|---|---|---|
| `green-01-20260906t0213` | 81/81: 51 helper + 30 caller | `39362CBB2975B3D8FF84F98BEAB745D79A3C2D1D05FFA64C12285FCD661D1831` |
| `green-02-20260906t0214` | 81/81: 51 helper + 30 caller | `98823A975F85E0F3B6418B4B30962C34D2FEE1F7E2C69494F2E911F75F59D34D` |

Both actual native exits were 0, with no failed/skipped cases. The 367 inputs
and 256 outputs were stable within and equal across the pair; a final 623-file
rehash found no mismatch. Native childless timeout controls retained the
unchanged 30-second work timeout, both EOFs and settled owned operations;
their PIDs and measured elapsed times are in the actual TRX output.

Raw source pins at this focused cutoff:

| File | SHA-256 |
|---|---|
| `FixtureProcessRunner.cs` | `CC98EB3AB5C66412361989CF841A8C7AF450D42141A9B076DD3848409FEE7F69` |
| `FixtureProcessRunnerTests.cs` | `ED4AAFE1DCA0F168CE632BC98EDFF67AAAB5F16D4877C2B6BA6B3095285F8DF3` |
| `CiTestRunnerContractTests.cs` | `92F8EC02BCE93F96DEAA7242BD2F1642EF29B75649A8B065956E4C5620FCFADC` |

The focused summary hash is
`3B9E96AEF554C10F415288EE2C37BDA443D231A957CBC42D64CE1536E6C2425B`.
The twice-derived 60-entry retained-files manifest hash is
`8D2D9EE101BC0F8DB84E502E49A4EC4FF6426B178923303338F79966FCD909E6`.
That ignored evidence root is frozen. ~~Full closing uses a separate
`out/fixture-async-close/` root and is pending at this preparation cutoff.~~
**Struck later 5 September 2026:** the separate ordered closing completed as
recorded below. The original focused evidence has not been rewritten.

## Pre-correction local closing — 5 September 2026

The root driver retained nine separate native command receipts and stdout/stderr
under `out/fixture-async-close/closing-20260906T022525Z-bae86e155b914368aaebfbca15a45935/`.
In order: diff check, Release solution build with warnings as errors, full format
fix, format verification, post-format solution Rebuild, focused A/B, full A/B.
All nine native exits were 0; both builds reported zero warnings/errors.
The full pair was one unfiltered solution-wide test process per run, bounded by
the unchanged external runner. No retry, blame mode or test timeout was added.

| Full run under `out/ci-test-run/` | Actual result | Summary SHA-256 |
|---|---|---|
| `20260906T022737Z-6ac94c806b8642e9a9712856a0143e5a` | 2,638/2,638; native/runner 0/0 | `06BCD6A1FEBB5649CE8CE395F20741AFCDF4C3F949287C7CF33F599C1E3C0D2B` |
| `20260906T023106Z-920421237c474c68993fd8073cb4da14` | 2,638/2,638; native/runner 0/0 | `2E95EBF8CB78F6A8F6F348D4BCC08F525C72FD5A66167A0EF7359E76E7D7ABEE` |

The root read the actual seven TRXs in each run, not only the driver exit:
Accessibility 26, Contract 175, InstructionalEvals 336, Integration 325,
Rendering 121, UiAutomation 289 and Unit 1,366. Every result was Passed, with
no skipped cases. The Unit TRX hashes are respectively
`D896D3F69C72A52418CFE8CF9964A40C360A7B1FD9A25340C7A741AC1CD9E0D0`
and `CB4062D2A878E50BEC8A3AF6B99863FE23048C4E85295320E960ADBF019FE803`.
Both receipts report no timeout, safe runner reuse and stable before/after
source/assembly identity; the measured test-process durations were 185,648 and
187,466 ms. The 557-file source digest was
`BF7ED32A002FB0E2BB7D16EA16E50788E66A5BBBBE1389FCCFA029ED3824A9B6`
in both runs. The Unit DLL remained 1,274,368 bytes,
`81D30A082AA3487D1DB1D99BDABC4E6FED6FD89B94F9C612EB842C7037A923C5`.

The same three source hashes listed above survived full formatting and both
full runs. Final record-only edits follow this source-frozen proof and require
separate record guards; they are not silently attributed to the full-run
repository digest. No production press changed, so this test-only slice does
not regenerate or replace the frozen sample baseline. Hosted verification of
the new commit remains separate, and S-18 remains open.

The [detached changed-file manifest](fixture-async-waits-files.json) pins the
final non-self files with strict UTF-8 and literal CRLF-to-LF normalization
only. It describes this continuation from `db40e9b6`, not the contents of any
earlier frozen manifest. The ledger at this cutoff contains 91 hosted rows;
later own-head observations must be appended as later evidence, never swapped
into this cutoff. Separate final record guards and staged-byte comparisons
are retained under `out/fixture-async-close/`.

## Pre-commit review correction — preserve the primary during test cleanup

The independent source review found a test-only exception-precedence defect
after the successful runs above. In two newly added non-overlap controls, a
first disposal deadline can make an already queued follower correctly refuse
without creating a process. Their `finally` observer caught FixtureProcessException
but not that exact sticky InvalidOperationException. The refusal could replace
the original assertion and skip later aggregate-observation sites.

No commit or push occurred. The full fourteen-file staged candidate and its
binary Git diff were retained before an index-only unstage at
`out/fixture-async-close/pre-review-correction-2026-09-06T02-45-46-423Z-4ced2f7b-2e01-4ed7-80eb-b0264be8b103/`.
Its retention receipt is
`F61D6C7E74B4C4D6B62E6833E473E495B3DA4376ADAC166D1CECB7519721FEBC`.
The independent audit of the earlier successful-path evidence found zero
mismatches; its derived twins hash to
`7E84F1DFC529B53FBB424CDE2FF3C65B4A5DF643500F6A46DA10FA9419D7E661`.
That audit does not clear a failure path absent from those runs.

Before correcting the observer, the two original cleanup sequences were
extracted without changing their exception behavior. Four cases (two release
paths × original assertion/fixture failure) used the real synthetic runner,
known-unstarted adapter, withheld disposal, manual clock and queued factory
sentinel. Both runs were 0/4, native exit 1. All four actual failure messages
begin `Assert.Same() Failure: Values are not the same instance`: the expected
original XunitException or FixtureProcessException was replaced by the exact
sticky InvalidOperationException. Both inner aggregate-observation sites were
skipped; the outer harness released and awaited its exact owned aggregate in
every case. A skipped observation site is not itself proof of a surviving task.

The new evidence remains under `out/fixture-async-cleanup-correction/`:

| Case | Actual result | Actual TRX SHA-256 |
|---|---|---|
| `red-01` | 0/4; native 1 | `C387F670291376EB83C14F600B7DFF16E37A0F48FFC797A6CC62074380FDF21B` |
| `red-02` | 0/4; native 1 | `4D35B2E98C186AC5749E0335C953C0972BED3BDC81816794B4FEECBD2A367C2B` |
| `green-01` | 93/93; native 0 | `4C5A343640AA9E9A5A1D60F73FFAD9650A2E781ECDDF4309995D019F2D0B9CCD` |
| `green-02` | 93/93; native 0 | `2EE24BB3784FE51FB273790C0C6D8BB4E5349084AAA25E9FE7FD567AD9868FBA` |

Only `FixtureProcessRunnerTests.cs` changed in this correction. Both original
callers preserve their propagating primary. The observer captures results
before fallible diagnostic output; exact sticky refusal additionally requires
the first unsafe result and an unentered follower factory. It independently
attempts each release, outcome and available owned aggregate. Unexpected errors
remain separate when a primary is already propagating; otherwise the first is
rethrown with its stack after all attempts. The four measured red cases plus
six unrelated/mismatched/factory-entered refusal controls and two actual-owned-
aggregate diagnostic-writer controls account for the twelve added cases.

The first correction build failed CA2219, “Do not raise an exception from
within a finally clause,” on the injected throwing delegate declaration.
Moving that declaration outside the clause preserved the injected behavior.
The next format verification returned native 2, IDE0061, “Use block body for
local function,” after formatting had converted the delegate. No tests ran
against either unsuccessful gate sequence. Fresh sequence `03` passed Unit
Release, single-file format fix/verify and post-format Rebuild with zero
warnings/errors, followed by the 93/93 pair (63 helper + 30 caller controls).
No analyzer suppression, runner change, deadline increase or unsafe reuse was
introduced. All complete failures and separate native streams remain retained.

The final test source is
`27441B220D6E716F967AEF01FB8DE0094969B82245904CF09D88AEB9D5C25824`;
runner and caller source pins above are unchanged. The Unit DLL is 1,299,968
bytes, `5B6F013B6465B1462F5FA9E037A6FF2145FFF96E7ABF45239DE56A8D74DBED8B`.
Both green receipts retain stable source/output/repository identities, and the
final 367-input / 256-output rehash found zero mismatches. Sources and binary
identity receipts are retained, not complete red/green binary archives.
The correction summary is
`18541582CE1833D8B671117E94632FDFBBE2C5E54382A256FD3436DAE781FEEC`;
the twice-derived 121-entry non-self retention manifest is
`83BE077AE7E8B020C99A6990AF8A79BEADA928C5517B4D1C9E596398BD9BD0F5`.

Independent review found the original P2 repaired, while retaining a reporting
limit: the two callers discard the returned secondary-error list. If the test
output channel itself fails, those secondary logs are not independently
durable. The injected writer controls prove cleanup continuity and returned
evidence, not successful durable emission. ~~Corrected full closing is pending;~~
**Struck later 5 September 2026:** the corrected ordered closing passed below;
the earlier full pair, final-record guards and detached candidate snapshot
remain evidence of their own pre-correction cutoff, not this later source.

## Corrected local closing — 5 September 2026

The fresh root driver retained all nine ordered native receipts and separate
streams under `out/fixture-async-close/closing-20260906T030705Z-f285ee4eb41f430b91958c742ed5e470/`.
Diff check, Release solution build with warnings as errors, full format fix,
format verification, post-format solution Rebuild, focused A/B and full A/B
all exited 0. Both builds reported zero warnings/errors. No retry, blame mode,
test filter in the full runs, or deadline change was introduced.

| Full run under `out/ci-test-run/` | Actual result | Summary SHA-256 |
|---|---|---|
| `20260906T030922Z-1a60c93cb78f4dcca8180438a778d4ef` | 2,650/2,650; native/runner 0/0 | `D980B7F77521DD41F74D238A6F3F4EA0AB0F26CC753E83430768AFD26F076921` |
| `20260906T031253Z-596bd07340a24c42a6177537b73cc0bc` | 2,650/2,650; native/runner 0/0 | `B2E68B2B00791FC4B4B9CE59BC7747187BE4D163FFA39AEB4B29971FD22ADDDB` |

Root read the seven actual TRXs in each run: Accessibility 26, Contract 175,
InstructionalEvals 336, Integration 325, Rendering 121, UiAutomation 289 and
Unit 1,378. All results were Passed, with no failed or skipped cases. The Unit
TRX hashes are `759538E3C955863729BF8F5B736A8B6E6548F7391F1542B0C9B34AF1C6AD9277`
and `708C44510B6E45CBDC480B625BA199725E4973A0EBCD50AEEEFA044F3ADE3D6B`.
Both full receipts report no timeout, completed output drain, safe runner reuse,
and no identity, completeness or snapshot errors. Their measured test-process
durations are 188,679 and 187,793 ms. Before/after and cross-run source/assembly
identities match: 558 source files, fourteen dirty entries, source digest
`5BEE497337DEFADE4C0C91418DEBFDDAFD1765425D141A4E9B2E10332DD68902`.
The corrected test source and Unit DLL retain the correction pins above;
the runner and caller sources are unchanged from their original async pins.

Root also read the actual fresh focused pair, 93/93 each, with no failures or
skips. Their TRX hashes are
`DA8487261A876DDD94F9326A9B4137D55A255D9452100A78F9D7E8CBEA43BDDC`
and `6E95B8273ED92B15D6E65973AFDE939BEC64AD3BFCB6F7321210B2DA4DE0B2DE`.
Final documentation and the detached manifest follow this frozen executable
proof and receive separate record guards; they are not part of its historical
source digest. New own-head CI/CodeQL evidence remains post-push work. These
local passes do not close S-18 or certify the inherited production stack.

## PR15 integration and the dependent-branch correction

The advisory-only PR15 was independently audited and normally merged at exact
main `39897d2b58884d000398c5178e87e174f82f5402`, preserving C1/C2 ancestry.
Its own-head and resulting exact-main CI/CodeQL conclusions are in the ledger;
the resulting main runs were independently read, not inferred from PR green.
Retained main evidence is under `out/pr15-main-hosted/`. It certifies that
advisory main tree, not the later fixture candidate or inherited production
changes.

The main retention manifest pins 203 non-self files / 52,434,667 bytes,
SHA-256 `4094FC134E6A5B4415DCF0C8B442FCD97C6A52801A1D7397B833A55880CC106C`.
Root independently rehashed that exact path set and the focused 60-file set,
with zero mismatches or extra/missing files. A secondary main-audit projection
initially reported sample bytes as the 40-entry array length; a separately
retained correction records the actual 773,759-byte sum. No primary artifact
changed and no CI failure is implied by that projection correction.

Deleting PR15's merged base unexpectedly closed PR16. Changing a closed PR's
base was refused. Restoring the exact deleted base, reopening PR16, retargeting
it to main and then deleting the obsolete base recovered open review state
with its exact head unchanged. Both merge and recovery observations are
retained separately under `out/pr-stack-resumption/`. AGENTS now explicitly
requires retargeting and verifying open dependents before deleting their base.
No held production PR was merged or described as approved by this recovery.

## Remaining boundaries

The seven direct recipe changes still need exact outgoing/replacement
compatibility disposition; inherited held commits prevent wholesale main
integration of this descendant branch. A concrete versioned-replacement
proposal is prepared under `out/pr-stack-resumption/`; it is non-operative
unless the owner explicitly chooses it, and any later decision must be
recorded separately rather than backdated into this evidence cutoff.

**Later 5 September 2026:** the typist answered the exact versioned-replacement
question with “Authorized.” The [separate authorization record](2026-09-05-recipe-replacement-authorization.md)
permits the seven candidate replacements and calibration layout work; it does
not admit them or change production source in this test-only repair.

Read-only reinspection of four retained calibration PDF renders confirms
instruction 4/5 clipping on ordinary/low-ink Letter/A4. Their PNG hashes match
the frozen [I20 record](accessibility-console-evidence.md#rendered-calibration-endpoint-repaired-overall-visual-qa-red).
No regeneration or production layout change occurred. The existing exact
layout/compatibility hold precedes that repair; physical-print and AT evidence
remain separate.

A further read-only C1-to-baseline press census found a separate direct
`press.booklet-guide@0.1.0` drift: `BookletImposition.Guide` changed its first
instruction from LONG-edge to SHORT-edge duplex at commit `576e191`. The
imposition arithmetic and separate imposed-PDF instructions are unchanged;
the frozen 40-file sample set contains no booklet-guide sample. Existing
print-instrument correction authority does not supply an exact replacement,
retention and evaluation disposition. The seven-row authorization does not
include it. A separate eighth-tuple question has been submitted to the typist;
no replacement is assigned or admitted in this repair.

ADR-007/010 remain Proposed; schema 1 is hash-less; CC BY-SA 4.0 remains a scope
proposal without an operative ownership grant or separate assent. Real consent,
custody, withdrawal, appointments, quorum/recusal and H0–H7 records remain
absent. No real council, protected review, site dispatch, release version/tag,
signing, installation, distribution, filing or correspondence is enacted here.
