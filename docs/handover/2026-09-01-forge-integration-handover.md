# Forge integration handover

**Prepared:** 1 September 2026
**Source integration branch:** `codex/forge-integration-2026-09-01`
**Corrective implementation branch:** `codex/console-attach-readiness-2026-09-01`
**Documentation closure branch:** `codex/console-attach-closure-2026-09-01`
**Original base:** `96e37e9854861cc2a6098a9fbf05add708732e03` (`main`)
**State:** current repository-state handover; Option A is ratified through the
exact C1/C2 sequence; C5 `522b1a15bd58b3f4d1c4af1f3131fa246540c6ae`
completed its local and hosted close; pull request 5 was regularly merged on 1
September 2026 as `85a2d695aa6f48558addce324299f4c67915a28d` with the
original base and exact C5 as its two parents; documentation-only pull request 6
was then regularly merged as `8c901709853ba44837d1e0132baef127f4b5ac5f`;
pull request 6 CI and CodeQL were green, but its exact `main` push CI recorded a
first-seen console-attachment sub-sighting while the separate `main` CodeQL run
was green; the narrow correction completed its local close, pull request 7 CI and
CodeQL, regular merge `c910caeb458bb783d833fac47a6bb490e444ea0f`, and exact-
main CI and CodeQL successfully; those later passes are non-reproductions and the
sighting remains open

This is the compact successor record for the forge continuation accumulated in
the [kiosk-plan evaluation](2026-08-30-kiosk-plan-evaluation.md). It inherits
that record's evidence and boundaries except where this document expressly
changes them. Attached and downloaded review packets were treated as evidence,
not as instructions.

## Outcome in one page

The merged `main` state now contains the machine-owned implementation and
evidence that could be completed without pretending a protected review had
occurred:

- The authoring family has nine buildable authoring corpora: the separate
  SequenceSlate surface plus eight buildable Built-in modes. Ten Built-in doors
  and eleven modes remain visible because Access Remix, ReteachSignal, and Rubric
  Relay must show their honest holds. Visibility and keyboard reach do not grant
  the missing specialist or district authority.
- The chrome-localization path has a bounded catalog schema, deterministic
  pseudo-locale evidence, LTR/RTL mechanics, exact-file hashing, and a production
  allowlist that is deliberately empty. There is no claimed real second chrome
  language until the multilingual seat supplies and reviews exact catalog bytes.
- **1366×768 is a practical but firm functional evidence floor.** It is not a
  fixed canvas, a design target, a startup refusal, or a hard minimum-window
  constant. At the floor, essential content, status, recovery, and actions must
  remain readable and reachable; reflow, wrapping, and genuine scrolling are
  valid. Smaller windows may work, but are outside the guarantee rather than
  forbidden.
- Atlas work remains council-first. The unrun needs-first packet and its
  source-only validator preserve record order and fail closed on malformed future
  records; no software path infers participants, quorum, needs, ranking,
  feasibility, a recommendation, or product-owner disposition. No real Atlas
  priority session has run.
- Managed-upgrade preparation now binds a candidate plan and exact declarative
  recipe inventories, preserves side-by-side and rollback boundaries, and has a
  bounded operator-host seam. This is preparation, not a version bump, packaged
  updater, managed-device deployment, or rollback proof. ADR-007 remains
  Proposed and District IT still owns deployment particulars.
- `tests/Accessibility` is no longer an empty build-only signpost. Its 26-case
  hermetic geometry-and-font-scaled floor suite covers reachability, essential
  visual viewports, long status paths, and layout mechanics while explicitly
  declining to claim physical DPI/device, keyboard, screen-reader, contrast,
  print, or protected-seat evidence.
- CI actions are pinned and inventoried on supported runtimes. The solution-wide
  test process is bounded and retains streams, TRX, coverage, identity hashes,
  and a machine-readable receipt. The always-upload path also includes hidden
  files so `.active-or-stranded.json` survives when it is the only interrupted
  runner receipt.
- The two headed-UIA timeouts and one image-test failure remain **sightings, not
  diagnoses**. Bounded reproduction instruments retain names, messages, source
  identity, process-tree cleanup, and partial evidence. Passing alone or in a
  later broad run does not prove a cause or a cure.
- Hosted PR runs `33497314978` and `33504105140` reproduced the same
  console-control lock-observation timeout. The second run left that test as the
  only failure and the hardened C3 collector retained its exact failed TRX. A
  forensic comparison identified a test scheduling/readiness candidate rather
  than evidence of failed product cancellation; C4 isolated the real console
  exercise from parallel Integration collections without extending its deadline
  and added content-free lock-probe diagnostics.
- C4-head PR CodeQL run `33507671111` completed successfully. C4-head PR CI run
  `33507671099` passed the console-control case in 1.7700168 seconds but stopped
  on one newly named FlashCap lifecycle timeout: the test expected cancellation
  and its five-second outer observation instead reported `TimeoutException`.
  Both run records name C4 as `headSha` and checked out PR merge ref
  `291b048c052de3b5cc9713b5dc470c091e56035d`; neither is mislabeled as a
  direct C4 checkout. The retained receipt and byte-identical raw/curated TRX
  make the failure a sighting, not a diagnosis of FlashCap or cleanup. C5
  generalizes the nonparallel collection to both bounded native-lifecycle
  classes without changing product code or any deadline. Exact-C5 PR CI run
  `33511155971` and CodeQL run `33511155996` both completed successfully; their
  metadata names C5 as `headSha` and their jobs checked out synthetic PR merge
  ref `e55bdd929fea261e5910ba7e352d4a8a2c7e7fc8`. The green CI test receipt is one
  non-reproduction in the later hosted PR run, not a diagnosis or proof of cure;
  CodeQL is static-analysis evidence, not a test reproduction.
- Documentation-only pull request 6 passed CI `33516013705` and CodeQL
  `33516013782` on synthetic merge ref
  `7d22a7e78310c1b4844aab9e242431a40807eff5`, then merged regularly as
  `8c901709853ba44837d1e0132baef127f4b5ac5f`. Its exact `main` push CodeQL run
  `33517101459` passed, but CI `33517101369` measured 1,989/1,990 when the real
  console-control test's sender reported `console-signal.attach-failed`. This is
  the first retained observation of that exact signature and an attachment-phase
  sub-sighting distinct from the earlier lock-observation timeouts. It did not
  deliver a signal or reach the product-cancellation assertions and supplies no
  diagnosis. The current test-support correction records native attach and
  target-watch results separately and retries only `ERROR_INVALID_HANDLE` (6)
  while the target remains alive and time remains in one unchanged, absolute
  15-second attachment-plus-lock-readiness budget. Its final local close, pull
  request 7 CI and CodeQL, and exact-merge main CI and CodeQL recorded below are
  green. Those later passes are non-reproductions, not a diagnosis, proof of cure,
  or closure of the sighting.
- Provider and district-policy boundaries now fail closed around target
  construction, bearer acquisition, redirects, deadlines, status handling,
  response size and shape, strict UTF-8 JSON, endpoint inventory, and cancellation
  precedence. These are local transport/parser proofs, not a district approval or
  configured-deployment attestation.
- Project and asset readers now apply the same bounded provenance policy, and the
  shipped 13-symbol catalog is fenced to its exact manifest digest. That digest is
  integrity evidence only; it is not AAC/SLP, accessibility, or rights approval.

## Public names and symbol-supported authoring

ADR-009 changes only Lesson Loom's public display name to **StrandPlan — Lesson
Design Studio**. Stable recipe, schema, namespace, diagnostic, localization, and
saved-project identities do not change. The wider public-name screen remains a
pre-release counsel task, not a trademark-clearance claim.

Mulberry and OpenMoji remain governed candidates, not admitted dependencies:

- Mulberry is the proposed primary family for actions, routines, communication,
  sequencing, and symbol-supported directions.
- OpenMoji is the proposed supplemental family for concrete objects, curriculum
  topics, activities, technology, places, and general interface concepts.
- Honest Ink originals remain the possible route for council-approved agency and
  safety-critical concepts that neither source expresses adequately.

The intended product phrase is **“symbol-supported, AAC-aware directions,”** not
“automatic AAC conversion.” A future authoring flow may break directions into
editable steps and suggest reviewed mappings, but the educator must confirm
every word, image, order, and meaning; source, license, version, attribution, and
ambiguity must remain attached; and Honest Ink must never replace or rearrange a
learner's established AAC vocabulary. Exact assets, mappings, family preference,
fallbacks, and safety-critical semantics remain with the AAC/SLP, rights, and
accessibility seats.

## The local two-commit ratification gate

The product owner explicitly selected **Option A for all 23 outgoing rows** at
`2026-09-01T07:55:28.9491461Z`: the unsigned `v0.7.0-alpha` tuples are
pre-admission development, the 15 candidate-only identities freeze in the same
exact commit, schema 1 remains deliberately hash-less with missing `recipeHash`
as a release stop pending a separately authorized schema-2 route, and ADR-007
remains Proposed.

The [recipe-identity disposition packet](../adr/recipe-identity-disposition-packet.md)
records every exact row and the executable/evidence freeze. Before either commit
was pushed, C1 `5cae09dcb40628265d51912aea98304557abfda6` froze the exact
candidate. Its immediately following record-only C2 named that full hash and
marked the packet `RATIFIED` without changing any frozen product or evidence
surface. C2 then passed the same close, and the exact C1/C2 pair was pushed
together. No version, tag, signing, installation, distribution, publication, or
deployment act is authorized.

Implementation-plan §6.6's combined “Warnings and confirmations” concern does
not require a second duplicate manifest list. The exact ordered `Warnings` text
is fingerprinted, and every entry becomes a fresh
`RequiresAcknowledgement=true` warning at every review through both the shared
review-notice and Module Studio paths. Focused exhaustive regressions bind that
mechanical behavior; acknowledgement is not specialist or protected-seat
approval, and no recipe/runtime hash changed to invent a second source of truth.

C1 also contains the executable Git-history guard for record-only C2.
Hosted CI requires full history, locates RATIFIED C2 even from a later regular
merge commit, requires C1 as its sole immediate parent, requires both commits in
`HEAD` ancestry, and requires exactly the governed six record files to differ.
C1's historical pending state produced only its permitted local skipped receipt;
CI invokes the guard with RATIFIED state required and retains its bounded JSON
receipt. Squash and rebase would destroy the proof and are not permitted.

## Local candidate closing evidence

The candidate implementation tree completed a warnings-as-errors Release build
across 26 projects with zero warnings and zero errors. `dotnet format` completed
its fixing pass, and the subsequent full `--verify-no-changes` pass exited zero.

Two earlier full-close attempts remain part of the record because each exposed a
specific deterministic defect rather than one of the three historical under-load
sightings. Run `20260901T091412Z-ebae452752e4472abd60ce3dffa59dab`
reported 23 headed UIA failures after attachable reflow behavior had first been
implemented as custom controls, contrary to ADR-002's standard-controls-only
boundary. The behavior was moved onto exact stock `Label` and `CheckBox`
instances. Run `20260901T093327Z-d60b5b5929f2403d98de62a4fb932f9f`
then passed UIA 261/261 and Accessibility 25/25 but found one Unit localization
contract failure caused by literal diagnostic text in those helpers; the helpers
now throw parameter-only exceptions. Neither failed run is erased or relabeled as
a flake.

After those repairs, exact committed C1
`5cae09dcb40628265d51912aea98304557abfda6` completed two solution-wide runs
with exit 0, no outer timeout, stable source and Release-assembly identities,
complete coverage/TRX evidence, and **1,985/1,985 passed** each:

- `20260901T100347Z-fe5a557bb19f42808f8e9ce0a0e2cca1`
- `20260901T100741Z-c2db4365b28d4ecc979da76cb1315811`

Each run measured Accessibility 25, Contract 152, Instructional Evals 336,
Integration 298, Rendering 81, UI Automation 261, and Unit 832. The
SampleGenerator then ran twice from that exact C1: both runs
produced exactly 40 files and 773,759 bytes, their inventories and every file
hash matched, and the expected first-admission manifest matched SHA-256
`DEF10A3258A2F2ABA922DF8F1BC38FC3A3209065B36F81F44C41B4FE047F4A90`.
These are local C1 results, not a hosted CI conclusion, release, or protected-
seat act. At that point, record-only C2 still required its own close and governed
Git-history verification; those requirements were subsequently completed before
the exact pair was pushed.

## Hosted PR finding and post-C2 repair

C1 and record-only C2 `94f128cddd5cdbd00a6f7097b470e4defdebaa47`
were pushed together on `codex/forge-integration-2026-09-01`. At that checkpoint,
pull request 5 retained both exact commits and was eligible for a regular merge.
CodeQL run `33497315024` completed successfully; CI run `33497314978` was red and
therefore stopped the merge at that checkpoint.

The first reading of the bounded receipt saw only the Accessibility failure.
Inspection of the always-retained raw TRXs corrected that report: the hosted run
contained four failures across three suites—Accessibility 24/25, Integration
297/298, and Unit 830/832. Contract, Instructional Evals, Rendering, and UI
Automation all passed, including UI Automation 261/261. The runner did not time
out and its source/Release-assembly identity remained stable.

Three findings are deterministic and repaired after C2 without rewriting C1 or
C2:

- Board to Brief's default comparison viewport had only 13 px of local
  typography slack above its 350 px content minimum. A deterministic Segoe UI
  10.25 pt regression reproduced the hosted vertical scrollbar at 340 px. The
  repair reclaims 16 decorative bottom-margin pixels while preserving the stock
  controls, the 160 px roles-group minimum, the right scrollbar gutter, and the
  no-scroll oracle. Failure output now records exact font, row, viewport,
  display-rectangle, margin, and control geometry.
- The ratification packet is checked out as CRLF, but two line-anchored test
  regexes admitted LF only. Both now accept the optional carriage return and the
  regression evaluates an explicit CRLF copy. Recipe bytes, identities, C1, C2,
  and the ratified disposition are unchanged.
- Hosted PowerShell inserted ANSI reset/recolor sequences between `Release` and
  `TargetPath` in a diagnostic contract. The test subprocess now forces plain
  text while explicitly exercising a color-capable `TERM=xterm` host. The
  underlying duplicate-target-path refusal was correct.

The fourth failure,
`ProjectUpgradeOperatorHostTests.Real_console_ctrl_c_cancels_the_process_and_cleans_the_synthetic_batch`,
reported only `console-signal.lock-observation-timeout`: its helper did not
observe the exclusive batch lock within 15 seconds while the hosted solution ran
under load. That record does not establish whether acquisition was late,
transiently missed, or absent, and it does not establish a product cancellation
failure. Five isolated local reruns passed in 565–602 ms each; the item therefore
remains a new sighting, not a diagnosis.

The same run exposed an instrumentation defect: each red suite had a coherent,
current, identity-bound TRX with `ResultSummary outcome="Failed"`, but the curated
collector treated only `Completed` as retainable, nulled the counters, and copied
only four of seven TRXs. The post-C2 correction separates trustworthy retention
from an all-passed result: coherent failed TRXs retain exact counters and failure
names/messages while remaining invalid for success, coverage, release, or merge.
The raw failure upload remains the fallback for malformed, partial, or interrupted
evidence.

### Post-C2 local close

The corrected post-C2 working state completed a 26-project Release build with
zero warnings and errors, followed by `dotnet format` in fix mode and
`--verify-no-changes`, both at exit 0. The hardened runner contracts passed
30/30; the full Unit suite passed 835/835. Applying the stricter validator to
all seven raw hosted TRXs retained all seven, while the three red suites remained
success-invalid with their exact counters and definition-bound failure
names/messages.

Two fresh solution-wide bounded runs then passed **1,989/1,989** each:
`20260901T113007Z-eaf877baf57744b9aa66db1077723039` and
`20260901T113357Z-f7cf21eb2a5e43019e2357463a31ddc3`. Each receipt records
Accessibility 26, Contract 152, Instructional Evals 336, Integration 298,
Rendering 81, UI Automation 261, and Unit 835; exactly seven current TRXs and
seven direct coverage files; both process exit codes 0; no timeout; stable source
and Release-assembly identity; no identity, completeness, snapshot, or stream
error; and a cleared active marker. The console-control sighting passed in both
full runs, which remains non-reproduction rather than diagnosis or cure.

The ratification history guard again verified exact C1 and single-parent,
record-only C2 in current ancestry. No deterministic-press source, recipe,
fixture, or sample input changed after C2, so the C1 byte-for-byte SampleGenerator
evidence was not rerun or repurposed as new C3 evidence. This closing note changes
only the governed handover record and is followed by the document-governance Unit
suite before commit.

### C3 hosted rerun and console-process scheduling-isolation candidate

C3 `4754637b6c98ccd8daf357bea7a0505a61b449ea` was committed with C2 as
its sole parent and pushed to pull request 5. CodeQL run `33504105104`
completed successfully. CI run `33504105140` completed red at the bounded test
step, so pull request 5 remained unmerged at the C3 checkpoint and the workflow
was not rerun blindly.
Release build, formatting, secret scan, and every suite except one Integration
case passed. The exact failure remained
`ProjectUpgradeOperatorHostTests.Real_console_ctrl_c_cancels_the_process_and_cleans_the_synthetic_batch`
with `Assert.Empty() Failure: String was not empty` and
`console-signal.lock-observation-timeout`; Integration measured 297/298.

C3's repaired failure collector worked as intended. Its receipt
`20260901T114837Z-73edf7b6ab8d42ec8ee3d26fac1c7bda` retained all seven
TRXs and all seven coverage files, recorded the failed Integration TRX as
coherent and diagnostically retainable but success-invalid, preserved its exact
298/298 executed and 297/298 passed counters, and kept the overall exit at 1.
The curated and raw Integration TRXs were byte-identical at SHA-256
`04DDB040A9C8BA4F620B65EE02CE95F0FA3B16EF7EA891992B9EAD3681C1D262`.
There was no outer timeout, source or assembly identity drift, stranded active
marker, or stream/snapshot error. This is instrumentation proof, not a green
test result.

Comparison of both hosted failures established the bounded distinction the
first record could not. In each, the sender had attached to the isolated console,
installed its own ignore handler, polled for at least 15 seconds without
observing the lock's exclusive sharing violation, and still observed the target
process alive at the deadline. The failures lasted 28.7749389 and 23.9197687
seconds and both overlapped the same two-export Edge PDF exercise while xUnit ran
up to four Integration tests concurrently. Ten retained isolated local passes
took 0.5643130–0.6035544 seconds; the same case took 1.4728911 and 1.1427937
seconds in the two full local C3 closes. The signal was never sent in either
hosted failure, so those records cannot diagnose the product's console handler,
cancellation, or cleanup behavior.

The C4 candidate therefore changes only the test scheduling and diagnostic
seams. `ProjectUpgradeOperatorHostTests` belongs to a dedicated xUnit collection
whose parallelization is disabled, keeping the real production executable,
isolated console, exclusive-lock observation, `CTRL_C_EVENT`, 15-second
observation boundary, cooperative exit, cleanup, and source-immutability checks
unchanged while preventing the measured intra-assembly PDF co-scheduling. A
future timeout retains only content-free state: target liveness, last lock-probe
classification and counts, attempt count, maximum inter-poll gap, elapsed
milliseconds, bounded candidate-entry count, lock existence, and empty/nonempty
host-stream state. It emits no path, PID, plan/package identity, or raw stream
content. A synthetic live-target/missing-lock exercise reached the unchanged
deadline at exit 5 and emitted only `lastLockState=missing`, 1,034 matching
missing observations, zero openable/access-refused/other-I/O observations, a
31 ms maximum poll gap, and 15,010 ms elapsed.

The exact real-signal test passed locally in 609 ms; the two direct
cancellation/cleanup tests passed 2/2; and the complete coverage-enabled
Integration assembly passed 298/298, with the console collection running after
the parallel-capable PDF tests.

### C4 local close

The final C4 working state completed a warnings-as-errors Release build across
all 26 projects with zero warnings and errors. `dotnet format` completed its fix
pass and the subsequent full `--verify-no-changes` pass exited 0. Two fresh,
unchanged-state solution-wide bounded runs then passed **1,989/1,989** each:

- `20260901T121158Z-cdfed2aec90a4c1d9ebbd22004ef8271`
- `20260901T121606Z-ed553cc107284325befeb4a4c4cded1f`

Each receipt records Accessibility 26, Contract 152, Instructional Evals 336,
Integration 298, Rendering 81, UI Automation 261, and Unit 835; seven current
TRXs and seven direct coverage files; runner and test-process exits 0; no outer
timeout; observed parent exit and drained streams; stable five-entry source and
Release-assembly identities; no identity, completeness, snapshot, or stream
error; permission for the next runner; and a cleared active marker. The
console-control exercise passed in both broad runs after the Integration
scheduler seam changed.

The ratification-history verifier again confirmed exact C1
`5cae09dcb40628265d51912aea98304557abfda6`, its immediately following
single-parent record-only C2
`94f128cddd5cdbd00a6f7097b470e4defdebaa47`, the exact governed six-file C2
diff, and both commits in current ancestry. C4 changes no deterministic-press
source, recipe, fixture, or sample input, so the C1 SampleGenerator evidence is
not rerun or relabeled. This final evidence note changes only the governed
handover record and is followed by the full document-governance Unit suite
before commit. C4 was then committed as
`5cf24a86249b35c44c557f3240455157e57e80e9` and pushed; its hosted outcome is
recorded immediately below. No merge claim is made for the C4 checkpoint; the
later C5 and merge closure is recorded below.

### C4 hosted result and C5 bounded native-lifecycle isolation

CodeQL run `33507671111` completed successfully and CI run `33507671099`
completed red. Both PR run records identify C4
`5cf24a86249b35c44c557f3240455157e57e80e9` as `headSha` and checked out PR
merge ref `291b048c052de3b5cc9713b5dc470c091e56035d`. PR 5 therefore remained
unmerged at the C4 checkpoint and the CI run was not blindly retried.
Ratification history, secret scan, warnings-as-errors build, and format
succeeded. The bounded test step
executed every test, with
Accessibility 26/26, Contract 152/152, Instructional Evals 336/336, Rendering
81/81, UI Automation 261/261, Unit 835/835, and Integration 297/298. The sole
failure was
`FlashCapCameraSourceTests.A_late_successful_start_is_stopped_and_disposed_again_after_immediate_cleanup`:
the assertion expected `OperationCanceledException` but the outer five-second
`WaitAsync` produced `TimeoutException: The operation has timed out.` The
determinism, coverage-threshold, dependency, vulnerability, SBOM,
distributable, and portable-sample gates were consequently skipped and are not
inferred.

Receipt `20260901T122927Z-9ced1d172cc9405ba728cd2ea3fa7cb7`
records native and wrapper exits 1, no outer-process timeout, stable source and
Release-assembly identities, observed parent exit, drained streams, permission
for the next runner, all seven TRXs and all seven direct coverage files, and no
identity, snapshot, or stream error. The raw and curated Integration TRXs are
byte-identical at 463,583 bytes and SHA-256
`6D9C9163DA96762AFD63319F828B5321FED64855580510235437DD3A5CAFD519`.
The failing case lasted 11.6735845 seconds and overlapped a 38.3016974-second
real-PDF test, a 37.3553761-second floor test, and an 11.6050305-second capture
test. The exact C4 console-control case passed hosted in 1.7700168 seconds. This
is one hosted non-reproduction of its prior timeout, not proof of cure.

The FlashCap case passed in 20/20 fresh isolated local processes, passed within
the coverage-enabled Integration assembly at 298/298 when the invoking shell set
`DOTNET_PROCESSOR_COUNT=4`, and passed in 0.0055481 seconds within full bounded
receipt `20260901T124002Z-94cdd0b0c53e40a4a13e0ce31c4e1370` under the same
invocation context. That receipt measured clean C4, its recorded command and
identities, and 1,989/1,989 with complete evidence and no timeout or identity
error; it does **not** record environment variables or processor count, so it is
not processor-count evidence. These are non-reproductions; neither the overlap
nor the passes establish whether cancellation propagation, cleanup, or
continuation scheduling caused the hosted delay.

The C5 change makes the smallest evidence-led test change. It renames the
test-only collection to `BoundedNativeLifecycleTestGroup`, places both the
operator-host and FlashCap classes in that single disabled-parallelization
collection, and adds a reflection contract that fails if either class leaves it
or if parallelization is re-enabled. Production code and every lifecycle and
test deadline remain unchanged. The coverage-enabled focused batch passed
52/52. The complete coverage-enabled Integration assembly then passed 299/299;
direct TRX interval comparison counted all 52 native-lifecycle cases and zero
overlap with any of the other 247 Integration cases.

The final C5 source state completed a warnings-as-errors Release build across all
26 projects with zero warnings and errors. `dotnet format` completed its fixing
pass and the full `--verify-no-changes` pass exited 0. Two independent
solution-wide bounded runs then passed **1,990/1,990** each:

- `20260901T125323Z-04c131a7ec8f44b785d1040b4beaed84`
- `20260901T125714Z-c15a2b4114c545d9a1dbda10b562bc32`

Each receipt records Accessibility 26, Contract 152, Instructional Evals 336,
Integration 299, Rendering 81, UI Automation 261, and Unit 835; seven current
TRXs and seven direct coverage files; wrapper and native test exits 0; no outer
timeout; observed parent exit and drained streams; permission for the next
runner; stable six-entry working-state and Release-assembly identities; no
identity, completeness, snapshot, or stream error; and a cleared active marker.
The ratification verifier again confirmed exact C1 and single-parent record-only
C2 in current ancestry. No deterministic-press source, recipe, fixture, or sample
input changed, so the C1 SampleGenerator proof is neither rerun nor relabeled.
This final evidence note changes only governed documentation after the broad
receipts and is followed by the full Unit/document-governance suite and format
verification before commit.

### C5 hosted and merge closure

C5 was committed and pushed as
`522b1a15bd58b3f4d1c4af1f3131fa246540c6ae`. Pull-request CI run
`33511155971` completed successfully: build-and-test job `99866852723`, secret
scan job `99866852854`, and portable-samples job `99869905899` all passed. Its
bounded receipt `20260901T130655Z-ed2d7c0d8cf24f32842ba6acecb3918f` records
**1,990/1,990 passed**: Accessibility 26, Contract 152, Instructional Evals 336,
Integration 299, Rendering 81, UI Automation 261, and Unit 835. The raw-failure
diagnostics step was correctly skipped because no test failed. Windows and Linux
determinism, coverage threshold, dependency and vulnerability inventories, both
SBOM scopes, and distributable checks passed. Pull-request CodeQL run
`33511155996`, job `99866852704`, also completed successfully, including its
clean-SARIF requirement.

Both pull-request runs have `event=pull_request` and C5 as `headSha`; their jobs
checked out synthetic merge ref
`e55bdd929fea261e5910ba7e352d4a8a2c7e7fc8`, which merged C5 into original base
`96e37e9854861cc2a6098a9fbf05add708732e03` for testing. That synthetic ref is
not the repository merge commit. Pull request 5 was regularly merged at
2026-09-01T13:14:26Z as
`85a2d695aa6f48558addce324299f4c67915a28d`, whose first parent is the original
base and whose second parent is exact C5. The merge tree is byte-identical to the
C5 tree; C1 and single-parent record-only C2 remain intact in `main` ancestry.

Main push CI run `33512198577` completed successfully on exact checkout and
`headSha` `85a2d695aa6f48558addce324299f4c67915a28d`. Build-and-test job
`99870367598`, secret-scan job `99870367946`, and portable-samples job
`99873604621` all passed. Bounded receipt
`20260901T131736Z-707a7823932b473abae84b5e0e70e57d` again records the same
seven suite counts and **1,990/1,990 passed**. The ratification-history gate
passed from the later regular merge, as did warnings-as-errors build, format,
Windows and Linux determinism, coverage threshold, dependency and vulnerability
inventories, both SBOM scopes, and distributable checks. Main push CodeQL run
`33512198619`, job `99870367005`, completed successfully on the same exact merge
commit, including its clean-SARIF requirement.

The PR and main CI test receipts are non-reproductions of the historical and
later hosted named under-load sightings; they do not diagnose their causes or
prove a cure. The two CodeQL conclusions are separate static-analysis evidence.
This integration
closure is not a version, tag, signature, installation, distribution,
publication, filing, protected-seat review, or release.

## Future work, in authority order

1. Have the multilingual seat supply and review one exact real chrome catalog;
   only a separate authorized build may pin its digest and activate it.
2. Run the real needs-first Atlas council session, freeze the participant record,
   keep feasibility separate, and obtain written product-owner disposition.
3. Review Mulberry/OpenMoji exact assets and mappings one by one across AAC/SLP,
   rights, and accessibility; admit nothing by family-wide assumption.
4. Resolve the documented module acceptance gaps under honest new or frozen
   recipe identities: SequenceSlate reorder and bilingual authoring UI; Board
   perspective/glare handling, multi-page PDF/slide intake, DOCX/ODT, and native
   print-PDF; Scaffold source comparison, checkpoint cards, and the task-entry
   blank-fade defect; Forumwright available-time fit and protected sensitive-topic
   evidence; and Inquirywright excerpt binding, role-bound exact facts, and
   unresolved-rights sink enforcement.
5. Ratify or reject the managed-upgrade design, then obtain District IT's exact
   roots, detection, retention, deployment, smoke, and rollback policy and prove
   it on managed devices.
6. Retain the exact pull-request 7 and post-merge receipts as non-reproductions,
   keep the console-attachment sub-sighting open, and require every later change
   to earn its own local and hosted closing evidence without a cure claim.
7. Keep all recorded sightings and their distinct signatures open as sightings
   and use retained
   receipts to seek equivalent-load reproduction before any causal fix, timeout
   increase, or closure claim.
8. Complete human keyboard/AT and physical-device evidence, print inspection,
   the six-week staff pilot, rights review, and all remaining release gates.

The protected seats' territory remains non-waivable from the keyboard. More time
does not grant authority. Versioning, tagging, signing, installing, distributing,
publishing, filing, correspondence, protected reviews, and human evidence remain
their named owners' acts unless that owner gives exact authority in the session.

## Integration closure evidence

C1's exact local build, format, two complete solution-wide receipts,
SampleGenerator comparison, hook result, and full commit ID are recorded above.
The record-only C2 working state then completed a second 26-project Release
build with zero warnings and errors, both formatting passes at exit 0, and two
unchanged-state solution-wide receipts at **1,985/1,985 passed** each:
`20260901T101453Z-e55ead923b494dbeb74a5853e22fa685` and
`20260901T101842Z-f43d3fe2886f48dea4bde251e77fcfa1`. Each receipt records
exactly six changed status entries, stable source and Release-assembly
identities, all seven suites, no outer timeout, no identity/evidence errors, and
a cleared active marker. The final pre-C2 closing note changed only the governed
handover record, and the document-governance Unit suite was rerun before C2 was
committed.

C1, exact C2 `94f128cddd5cdbd00a6f7097b470e4defdebaa47`, C3
`4754637b6c98ccd8daf357bea7a0505a61b449ea`, C4
`5cf24a86249b35c44c557f3240455157e57e80e9`, and C5
`522b1a15bd58b3f4d1c4af1f3131fa246540c6ae` were pushed in exact order on
`codex/forge-integration-2026-09-01`. The pull-request CI and CodeQL checks
recorded above passed, then pull request 5 merged regularly as
`85a2d695aa6f48558addce324299f4c67915a28d`. Main push CI `33512198577` and
CodeQL `33512198619` both passed on that exact merge commit. This closes the
authorized integration path measured in this handover while leaving the stated
release stops, future work, protected reviews, and all non-diagnostic under-load
sightings open.

## Pull request 6, exact-main failure, and locally closed correction

Documentation-only pull request 6 had head
`43f155374459cf260fe604a9f600b4df29d88e0a` and base
`85a2d695aa6f48558addce324299f4c67915a28d`. Pull-request CI run
`33516013705` completed successfully: build-and-test job `99883098460`, secret
scan job `99883098948`, and portable-samples job `99886204673` all passed. Its
bounded receipt `20260901T135527Z-1e6adbf35dd04c2fa61c8a3f59ac0eb2` binds the
checked-out synthetic merge ref
`7d22a7e78310c1b4844aab9e242431a40807eff5` and records **1,990/1,990 passed**:
Accessibility 26, Contract 152, Instructional Evals 336, Integration 299,
Rendering 81, UI Automation 261, and Unit 835. Pull-request CodeQL run
`33516013782`, job `99883098596`, also passed on that synthetic merge ref. Those
`pull_request` runs name exact pull-request head
`43f155374459cf260fe604a9f600b4df29d88e0a` as `headSha`; the synthetic checkout
is not the later repository merge commit.

Pull request 6 merged regularly at 2026-09-01T14:02:57Z as
`8c901709853ba44837d1e0132baef127f4b5ac5f`, with exact first parent
`85a2d695aa6f48558addce324299f4c67915a28d` and exact second parent
`43f155374459cf260fe604a9f600b4df29d88e0a`. Its tree differs from the first
parent only in the three governed documentation files named by the pull request.
Main push CodeQL run `33517101459`, job `99886767781`, completed successfully on
exact `headSha` and checkout `8c901709853ba44837d1e0132baef127f4b5ac5f`,
including the clean-SARIF requirement. Main
push CI run `33517101369` did not: build-and-test job `99886767578` failed,
secret-scan job `99886767909` passed, and dependent portable-samples job
`99889067410` was skipped. The green pull-request runs and green main CodeQL run
do not override the red main CI conclusion.

The failed bounded receipt
`20260901T140524Z-8ff4cc52be4044b3a1021b7165ad5e95` binds clean exact commit
`8c901709853ba44837d1e0132baef127f4b5ac5f`, stable source and Release-assembly
identities, no outer timeout, and **1,989/1,990 passed**. Accessibility measured
26/26, Contract 152/152,
Instructional Evals 336/336, Integration 298/299, Rendering 81/81, UI Automation
261/261, and Unit 835/835. The retained Integration TRX SHA-256 is
`E85F112C74467AE09DEB1B020D158477046ED626F282B53007E4EE119B0B0AAD`. The exact
failure was
`ProjectUpgradeOperatorHostTests.Real_console_ctrl_c_cancels_the_process_and_cleans_the_synthetic_batch`
at 0.6326650 seconds with `Assert.Empty() Failure: String was not empty` and
`console-signal.attach-failed`. Its content-free snapshot was
`senderExit=3; hostExited=False; candidateEntries=0; lockExists=False;
hostStdoutEmpty=True; hostStderrEmpty=True`.

That token existed as a fail-closed sender branch from C1 onward, but no earlier
repository record, retained local evidence, supplied review bundle, issue or pull
request comment, or hosted CI log since C1 records it as an observed failure.
The two prior failures of this same test in runs `33497314978` and `33504105140`
were later-phase `console-signal.lock-observation-timeout` sightings after the
sender had attached and installed its ignore handler. In the new record,
`AttachConsole` returned false before handler installation, lock observation,
signal delivery, or product-cancellation and cleanup assertions. It is therefore
a first-seen, distinct attachment-phase sub-sighting—not a recurrence of the
lock timeout, one of the original three sightings (two headed UIA and one
burned-region image), a diagnosis, or evidence that product cancellation failed.
The green PR receipt followed by the red exact-main receipt also does not identify
the host, scheduler, load, or documentation merge as a cause.

At this checkpoint the corrective implementation in this change affects only the
Windows test-support sender, its test visibility, and deterministic Integration
tests. Its content-free receipt separately retains the inherited-console detach
result/error, `lastAttachError`, whether any attachment succeeded, the most recent
target-wait classification and `targetWaitError`, and bounded attempt/poll timing.
The readiness helper consumes its caller's absolute shared-stopwatch elapsed value
and 15-second deadline; it does not rebase a fresh attachment allowance. After an
attachment error it re-observes the already-opened target handle before assigning
any terminal timeout or nontransient-attach classification. It retries only exact
`ERROR_INVALID_HANDLE` (6), and only while the target still reports running and
budget remains; every other attach error fails after that same target observation.
Target exit or target-watch failure stops the path. A successful attachment at
the deadline is not admitted as ready, remains recorded as attached, and is
detached by the outer `finally`. The existing exclusive-lock observation consumes
whatever remains of the same stopwatch and 15-second total budget, so no product
or test deadline is extended. Because the failed hosted receipt did not retain a
native error code, this correction does not claim that the observed failure was
`ERROR_INVALID_HANDLE` or that its cause is known.

Measured local evidence on the revised corrective diff is bounded accordingly.
A clean focused Release build completed with zero warnings and zero errors. Eight
deterministic attachment-readiness cases plus the real
console-control case passed **9/9**, with the real case at 577 ms. The eight cases
cover bounded `ERROR_INVALID_HANDLE` retry, a running-target nontransient error,
the existing readiness bound, the caller's absolute shared budget, target-state
reobservation at the deadline, a successful attachment exactly at the deadline,
target exit, and target-watch failure with its separate native error. Ten
fresh-process executions of the real case on the same code passed **10/10** at
571–669 ms. Full Integration passed **307/307**, with the real case at 549 ms.
These are deterministic seam evidence and local non-reproductions, not a
diagnosis or proof of cure.

The final closing sequence then completed on one unchanged corrective state. The
full 26-project Release build measured zero warnings and zero errors. Full
`dotnet format` fix and `dotnet format --verify-no-changes` both exited 0. Two
unchanged-state solution-wide bounded runs passed **1,998/1,998** each:

- `20260901T144210Z-5cd7272a1847462fad734ec398f606c1` in 193,342 ms;
- `20260901T144618Z-168807b0baa84e90a9fa6841a3c74138` in 188,530 ms.

Each receipt records wrapper and native test-process exits 0, no outer timeout,
observed parent exit, complete output drain, permission for another runner,
stable source and Release-assembly identities, no identity or evidence-
completeness errors, seven current TRXs, and seven direct coverage files. Each
measured Accessibility 26, Contract 152, Instructional Evals 336, Integration
307, Rendering 81, UI Automation 261, and Unit 835. These complete local passes
remain non-reproductions, not a diagnosis or proof of cure. Exact pull-request 7,
regular-merge, and post-merge evidence is recorded below. No deterministic-press
source, recipe, fixture, or sample input changed, so the conditional
SampleGenerator rite did not apply, was not rerun, and no earlier sample proof is
relabeled.

Nothing in pull request 6, the main failure, or this correction changes the
ratified Option-A disposition for all 23 outgoing rows, the pre-admission status
of the `v0.7.0-alpha` tuples, the exact-C1 freeze of all 15 candidate-only
identities, schema 1's missing project `recipeHash` release stop, or ADR-007's
Proposed status. It is not a version, tag, signature, installation, distribution,
publication, filing, protected-seat review, or release. The typist's acts and all
AAC/SLP, multilingual, district, rights, safeguarding, and other protected-seat
territory remain unchanged.

## Pull request 7 and exact-main hosted evidence

Pull request 7 had exact base
`8c901709853ba44837d1e0132baef127f4b5ac5f`, exact head
`d2a04cdf5ddebba67867987db4a2a9c083b4b4f2`, and synthetic merge checkout
`783357a799b8f5dde48f806f5f856659b539760c`. Pull-request CI run
`33522404031` completed successfully: build-and-test job `99904669394`, secret-
scan job `99904669716`, and portable-samples job `99907918147` all passed. Its
bounded receipt `20260901T145716Z-deb8b124b5334f3599f839a7a2051bf8` binds that
synthetic merge, completed in 302,296 ms, and records stable source and Release-
assembly identities, no timeout or identity/evidence-completeness error, seven
current TRXs, seven direct coverage files, and **1,998/1,998 passed**:
Accessibility 26, Contract 152, Instructional Evals 336, Integration 307,
Rendering 81, UI Automation 261, and Unit 835.

Pull-request CodeQL run `33522404004`, job `99904669102`, completed successfully
on the same synthetic merge. Its clean SARIF contained zero results at SHA-256
`539E2E1D15922A418F8C184914650707F935244F8C16B9871A4DCF3358AF73A1`.
That static-analysis conclusion is separate from the test receipt.

Pull request 7 merged regularly at 2026-09-01T15:05:02Z as
`c910caeb458bb783d833fac47a6bb490e444ea0f`, with exact first parent
`8c901709853ba44837d1e0132baef127f4b5ac5f` and exact second parent
`d2a04cdf5ddebba67867987db4a2a9c083b4b4f2`. The head and regular merge share
exact tree object `ad0866c8119c06dc0219b25c0b167db60a4092e9`; the recorded
base-relative patch hashes for base-to-head and base-to-merge are likewise equal
at `6d21e64019949eda60df464b378c68ccfd5e3d16`. The pull-request merge ref,
head commit, and regular merge commit remain distinct identities even though the
measured implementation trees agree.

Main push CI run `33523542939` completed successfully on exact repository commit
`c910caeb458bb783d833fac47a6bb490e444ea0f`: build-and-test job `99908503980`,
secret-scan job `99908503633`, and portable-samples job `99912082705` all passed.
Bounded receipt `20260901T150823Z-4d6b213977bc495197300c64aacb7ec7` binds exact
`c910caeb458bb783d833fac47a6bb490e444ea0f`, completed in 339,792 ms, and records
the same stable source/Release-
assembly identities, no timeout or identity/evidence-completeness error, seven
TRXs, seven coverage files, suite counts, and **1,998/1,998 passed** as the
pull-request receipt. Main push CodeQL run `33523542863`, job `99908501902`, also
completed successfully on exact
`c910caeb458bb783d833fac47a6bb490e444ea0f`; its clean SARIF contained zero results
at SHA-256
`BA23496F56F37FF30990FF3D15C3E01F42C8DEC97D3BED14C469E989A64EBB62`.

These later hosted passes are non-reproductions of the first-seen
`console-signal.attach-failed` sub-sighting. They do not identify its cause,
diagnose a product failure, prove the correction cured it, or close the sighting.
They do not change Option A, schema 1's missing project `recipeHash` release stop,
ADR-007's Proposed status, or any typist or protected-seat boundary. This
documentation-only closure change must earn its own applicable local and hosted
closing evidence; no such later result is predeclared or inferred in this record.
