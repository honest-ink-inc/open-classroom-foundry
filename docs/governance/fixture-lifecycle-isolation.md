# Fixture lifecycle collection isolation

**Prepared:** 6 September 2026 (America/New_York).
**Source baseline:** `a73fe0d70cc30aa2d6fdbdf462cc5d4770d61875`.
**Status:** corrected ordered local closing verified after the retained first failure; final record guards and new own-head hosted verification remain separate.

## Exact predecessor, not a transferred local pass

The [previous asynchronous-wait record](fixture-async-waits.md) and its
detached manifest remain frozen. That candidate was committed and pushed as
the baseline above. Its own conclusions are now appended to the
[evidence ledger](../evidence/evidence-ledger.json): CI failed, while CodeQL
succeeded. Root read the actual seven TRXs: 2,645/2,650, all five failures in
Unit (1,373/1,378), other six suites passed, no skipped results.

Both run APIs identify the exact head. Checkout is synthetic PR merge
`ef005959a7cfc75819564d8e83dfe2b8550e2f28` into
`77bb88f3e07b5d888ce0e5f0cc2601a96c8a9dee`, not main. Separate Git-object
reads confirm its ordered parents and the same tree as the head,
`c3309893d5e3a1d6fce31a5005a8f5588e161c26`. Tree equality is not independent
source-to-binary provenance. Complete native streams, actual artifacts,
messages/stacks and API captures remain under
`out/fixture-async-hosted-pr24/a73fe0d-20260906T034125Z/`.
The twice-derived retention manifest covers 204 non-self files / 99,864,529
bytes, SHA-256 `9C396A817FDECDEA943F5FBD156CC744D2264890696A993D280F64E1A5F68FA3`.
Actual summary: `E36594652D54210E394660759044EFD7C26FDDF9FBA723DD669A6348D5E5B94C`.
Actual Unit TRX: `06C14898D0B85D3C454E798637E8AC1965A81BC416E73A6C1D6F21D05EB3175F`.

The native copy test retained its expected changed-source rejection and
native exit 1, but fixture disposal did not settle in time. Primary:
`DisposalFailure: fixture ownership did not settle cleanly.` Disposal was
Queued at 1,752 ms against the unchanged shared 500-ms budget; entry/exit
were unobserved and the task incomplete. Root exit, both EOF streams,
cleanup and capture were observed/settled; disposal and safe reuse were
false. Two later tests correctly refused creation, preserving the entire
original description exactly. Separate later progress reports callback
entry/exit at 1,757 ms and completion at the 1,772/1,777-ms observation sites.
They do not revise the deadline, establish an exact task-completion instant
or permit reuse. This is a new S-15 observation, not S-18's capture failure.

Two synthetic failures remain distinct. The root-exit control's
PendingReader.EnsureReading did not observe its pending-read signal within
two seconds: `Synthetic reader did not reach its controlled pending read.`
The runner retained that as ExitObservationFailure instead of the expected
StreamDrainTimeout. Signal absence does not prove that no worker entered.
In one non-overlap case, the first aggregate's unchanged two-second await
timed out; the second was successfully observed. The original
`synthetic-original-nonoverlap-assertion` remained primary and the outer
finally subsequently awaited the exact released aggregate. A missing
successful-observation marker is not a skipped attempt, masked primary or
proof of surviving work. S-19/S-20 in the
[sightings register](../evidence/sightings-register.md) retain these signatures.

Build, format, history and secret scan passed. Samples, coverage threshold,
dependency/vulnerability/SBOM and portable gates were skipped. Actual CodeQL
SARIF has one successful invocation and zero results; it cannot override
red CI or admit the inherited production stack. Outer runner identity and
safety were stable, with one coherent failed-Unit completeness error. No
hosted rerun replaced the failure evidence.

## Unchanged-source isolated non-reproductions

Before collection changes, the exact clean head was rebuilt in Release:
native 0, zero warnings/errors. Six separate local invocations then passed,
twice per failing area, with actual native 0, no failed/skipped results and
stable paired source/output/repository identities. The complete four-case
non-overlap theory was selected, not only its one hosted failing case.

| Selected area | A / B result | Actual A / B TRX SHA-256 |
|---|---|---|
| Copy hash enforcement | 1/1; 1/1 | `28E0504D37D3C8C88E231578F9931C9479E025324B0A46574851FF5028154BA5`; `5B22817918105B0C47171CF8554D68FD02DE45C0F89BC5DBEEBD9BF2CEF40D21` |
| Root exit versus EOF | 1/1; 1/1 | `7049BBEDF22DDF6C3788356FD6AB6D2FBA4B35DA634BE1A0DA0A88E9CE55E323`; `79B101BA8E6967080059BF87D23BB63CBD61911D6932BCC4DB2FEF23E3F1B6D3` |
| Non-overlap cleanup | 4/4; 4/4 | `DE03F7806128BDBEB9223E49FCAA2DF08D7107D0F7E421546C0A2E058CD4E4F5`; `0181BA5447548CD91ED5ADC06EC7B5F1660B9FFDE370C3467FB1B2D84D66ECD9` |

Driver receipts are under
`out/fixture-async-close/replay-a73-20260906T035317Z-3ebdd2d75f9443129b813f7c9d0a8ce2/`;
the six sibling result directories share that lowercase prefix and their
case/repeat suffix. Locally rebuilt assemblies are not retained hosted bytes
or equivalent load. Passing reruns are non-reproductions, not cures or proof
of a common scheduler/OS cause.

## Red-tested narrow implementation

Only two existing source lines change: Collection attributes on
FixtureProcessRunnerTests and CiTestRunnerContractTests. The new
BoundedFixtureLifecycleTestGroup.cs defines one named collection with
DisableParallelization=true and one structural guard. The guard is a third
member and verifies the unique definition, flag/name and exact membership.
It does not automatically discover future helper consumers.

Before either fixture class joined, the guard failed 0/1 twice, native 1:
`Assert.Equal() Failure: Collections differ`; expected the guard plus both
fixture classes, actual only the guard. Only after both failures were read
were the two attributes added. This measures guard sensitivity, not the
hosted cause. Full messages and separate streams remain under
`out/fixture-lifecycle-isolation/20260906T035841Z/`.

| Case | Actual result | Actual TRX SHA-256 |
|---|---|---|
| red-01 | 0/1; native 1 | `E6E372C3D78737BDC9C2B24E3014749ACCBFC92AB35A19B2637FAB5343D93EB2` |
| red-02 | 0/1; native 1 | `5F2A34716108AC5230BA15AD3C9EB677A01F83B2C7716D2FB9FF1B915FEA2998` |
| green-01 | 94/94; native 0 | `9C53530CF7753A772C1E22C5A3E990E4498D270344E6B5F20F4A350765CB397F` |
| green-02 | 94/94; native 0 | `19AB10A188AA02999C5743E365D21ADAE8A6CAF261CAD677C2EA9A4C3A811615` |

Unit Release, scoped format fix/verify and post-format Rebuild exited 0,
with zero build warnings/errors. All original 93 test names remain: 63
helper and 30 caller, plus one guard; none renamed, removed or skipped.
Final cutoff rehash at `2026-09-06T04:08:03.1590694Z` found zero mismatches
across 368 inputs and 256 outputs. Later records are not part of that snapshot.
The sealed summary is `4C0AE01FC2536CF0A17EA6D3E7D2BEE80DB55F6DD6627FE140DFBE13A3B338B3`;
93-file / 5,176,822-byte retention manifest:
`5ABA26469FC342A9712F1519CBE2CA4ECEF7D0D2FC985EC288B0906501DB18BC`.
Two independent derivations and a complete later retained-file rehash agree.
One local JavaScript authoring parse error happened before any command/file
creation; it is disclosed separately, not a failed measured gate.

| Source at focused cutoff | Raw SHA-256 |
|---|---|
| Collection/guard | `01D576BC63353081DD3426CEB6C42C29A95D1F1783A65EB29410F7BD8BF9D93C` |
| Fixture tests | `3E5EBF9926FE586F566DCAA3FF53CFCE49A96E4024EC20000B46B0768436268C` |
| Caller tests | `6D0AA1BDCDFD1AE802CABFED0905C8AF849022A694BFCAC352E43C9FBBF6A39B` |
| Unchanged runner | `CC98EB3AB5C66412361989CF841A8C7AF450D42141A9B076DD3848409FEE7F69` |

Unit DLL: 1,302,528 bytes,
`1FA8DD3CAB2780F7926955EF27B9003546A88B28C55396F787071B78A9DB9462`.
Independent source review found no actionable defect. Under
[xUnit's documented semantics](https://xunit.net/docs/running-tests-in-parallel),
this isolates the collection from other Unit collections, not other
assemblies, processes or work surviving a completed test. Internal schedulers,
fixture bodies, caps, sticky refusal and safety decisions are unchanged.
No global parallelism setting, retry, pool adjustment or dedicated worker
was added. Shared-pool pressure remains a hypothesis without a causal trace.

## Closing boundary

~~Ordered full solution closing and stability rerun are pending at this
preparation cutoff.~~ **Struck later 6 September 2026:** the corrected
ordered pair passed at the exact cutoff recorded below, using a new
`out/fixture-lifecycle-close/` root. The
[detached changed-file manifest](fixture-lifecycle-isolation-files.json)
pins this continuation separately; earlier manifests are not regenerated.
Final record guards and new own-head CI/CodeQL remain separate. No production
press changes here, so this slice does not regenerate the sample baseline
as a substitute for its actual test evidence.

## First full closing — rejected, retained and corrected

The ordered driver under
`out/fixture-lifecycle-close/closing-20260906T041652Z-4e53fa88551f453589706aa518a33db5/`
passed diff check, Release build, format fix/verify, post-format Rebuild and
94/94 focused A/B. Both builds had zero warnings/errors. The first full run
then failed, native/runner 1/1, and full B did not run. Actual seven TRXs were
2,649/2,651, with two Unit failures (1,377/1,379), no skipped results and all
94 fixture/collection cases passed. Root read both complete failure messages.

`EvidenceLedgerTests.Entries_are_unique_and_appended_in_measured_time_order`
failed `Assert.Equal() Failure: Collections differ` at position 91: both
new runs share `2026-09-06T03:37:14Z`, and the required ordinal ID tie-break
places the CI row before CodeQL. Only those two new rows were reordered;
their evidence values and all original 91 entries remain unchanged.

`CiSupplyChainContractTests.Repository_project_files_do_not_declare_a_weaker_build_contract`
reported that the historical nested C1 `source/Directory.Build.props`
`does not import the repository build contract`. This is a separate,
independent checkout, not a new current-repository build exception. The
guard was not weakened and no C1 source was edited. After resolving and
checking the exact source and new dedicated temporary destination, a normal
`git worktree move` relocated the owned live checkout outside this repository.
All 661 non-metadata files / 15,810,875 bytes match before and after; exact
C1 HEAD and clean status are retained, as are the active repository's HEAD
and status. Only the worktree registration/pointer is excluded from that
byte comparison. The old seal already excluded live `source/` and is intact.

The actual failed full receipt is
`out/ci-test-run/20260906T041956Z-2f9f2f9cd320487bba30b90eacd2cfb1/`:
summary SHA-256 `9AFDBFA5FA890B371D537568C96D3479B9367288AF9ED9733A522CBDAC024856`;
Unit TRX `3E1EEE92ADB1C916AE213C57B0441FD7B6CD366B0F1D9DEC539274048E0A43B8`.
No timeout, safe outer reuse, stable 562-file source/seven-assembly identity,
empty identity errors and one coherent failed-Unit completeness error were
recorded. Those guarantees exclude ignored workspace contents such as the
historical checkout; they do not mean the filesystem guard's scope was empty.

Before correction, all twelve candidate files were retained at
`out/fixture-lifecycle-close/failed-closing-cutoff-2026-09-06T04-27-51-821Z-e59df217-0b7a-489b-8fe9-611055108da2/`,
receipt `1EDF59532E95EA69941F972CF5A60B9C27BD1C3E37AA9F78A0EC92B0DCD101F8`.
Relocation proof is separately retained under
`out/fixture-lifecycle-close/c1-relocation-20260906T042918Z-9daaa29d9c384fd6a97a65dd7d377f8f/`,
receipt `9D80C88546605A7B68727CE600426978DA1F25C6DB5ACCA6111F436A56B3163A`.
Its exact destination is a new `ocf-c1-compatibility-4af1dcfbcc2244f3a492487625efc6e5/source`
directory under the local temporary directory, not a deletion of evidence.

Independent record review initially reported no defect but did not catch
the ledger's equal-time ordering rule. Executed guards outrank that review;
the first failed cutoff remains retained. These deterministic corrections
do not diagnose historical fixture failures. ~~Fresh full closing is pending.~~
**Struck later 6 September 2026:** the corrected ordered pair below passed.

## Corrected full closing — measured local pass

The two exact record/environment controls first passed 2/2, native 0, with
stable inputs and outputs, under
`out/fixture-async-close/lifecycle-record-environment-correction-20260906T0431/`.
Actual TRX: `0F2D265A73F5D4481E89F4DC290C16F9CB4008DC8B10A939D2007E1007BFA705`;
receipt: `74C906DE31949E7DD43ED18F688F78885B11E5D7E23132DD0D82B576A96AC2F0`.

Fresh ordered closing under
`out/fixture-lifecycle-close/closing-20260906T043214Z-e845a9a980554905beeb2f5260ba3c7c/`
completed all nine gates with native 0: diff check, Release build with
warnings as errors, format fix, format verify, post-format Rebuild, focused
A/B, and full A/B. Root read the actual build streams: both builds had zero
warnings/errors. Their stdout SHA-256 values are
`BD0F49A33F086DD78DE51E6B7E13DA155C31F3B761CB6412CD82E5F4E64914ED`
and `6F526544548B3443CC0DA07154014B08EF4A72FA01D38539B1834FB910315CD0`.
Both stderr files were empty.

The new focused pair passed 94/94 each, with no failed or skipped results.
Actual TRX SHA-256 values are
`6A6E220C5139A9E9727884CF1057BFCCEDCDCE25170762118E5211AB72631940`
and `3F81C2403F62150153AF6D1375064D9F29B9CDE46E579D8FECF2FF56D0D00A2B`;
their directories under `out/fixture-async-close/` share the prefix
`isolation-closing-20260906t043214z-e845a9a980554905beeb2f5260ba3c7c-`
and suffixes `a` / `b`.

Root also read all fourteen actual full-run TRXs. Each full run passed
2,651/2,651: Accessibility 26, Contract 175, Evals 336, Integration 325,
Rendering 121, UI 289 and Unit 1,379. No result was failed or skipped.

| Full run under `out/ci-test-run/` | Actual summary SHA-256 | Actual Unit TRX SHA-256 |
|---|---|---|
| A: `20260906T043514Z-ef2245b9c27646b38c8b061e97ef1ac6` | `FBF364A5127828CFEFEFDEA2F871D53BAFA6CDF51AC71108422976ABE4FA4C64` | `E03B4977CC76A901E2DED095A7587D73044A4FFB69A7540620EADAE853654A3E` |
| B: `20260906T043850Z-5327aead293948b399a96c1a03911850` | `882F32D5597E07D356847E47EC94380D8C72B3253617ABAADD999892C1D4A341` | `A52FE391C42766B79FCE3D1B73DBAF9AD0EB2F0F14B89B581FCD62DEAF4AFAAD` |

Native/runner exits were 0/0 in both runs, with elapsed runner times
191,611 and 189,539 ms, no timeout, completed output drains and safe outer
reuse. Both identity/completeness error lists were empty and snapshot errors
were null. Before/after and cross-run comparisons agree on the same dirty
baseline HEAD, twelve changed paths, 562 source files and source digest
`5DA250222E1D85C2C9CBB5634D160D0F57F5705AB5E365A8773C9AE14F9FCE07`.
The seven-assembly identities also match across both runs, including the
Unit DLL pinned above. This is local repeated verification, not proof of
the historical hosted cause, stability under every load or compatibility
admission for inherited production changes.

Before writing this closing account, all twelve candidate files and the
tracked binary diff were retained at
`out/fixture-lifecycle-close/verified-closing-cutoff-2026-09-06T04-44-55-850Z-f1e25b69-6037-409f-af2e-1152434e8439/`,
receipt `21D1A27C62DFD14601EDACC781D05AE99110DEE83090188A6C3F58EAE48D321B`.
Those bytes matched the then-current changed-file manifest. This paragraph
and the later final record updates were not part of the full-run source
digest. Separate final record guards bind the final records to the unchanged
Unit DLL; a future commit and its own hosted conclusions cannot be inferred
from either local pair.

An independent retained-evidence audit under
`out/fixture-lifecycle-final-audit/audit-20260906T044510Z-c0e17aeec74b4d4988ddc642ea4039a3/`
read all sixteen TRXs, 5,490 result bindings, nine gate receipts and paired
source/output records. Both audit executions exited 0 with zero errors;
196,339-byte report twins have SHA-256
`94D8BC0B9CF57EB130A603ED91A613A42FBE2B8A3172B7FA0D7B09F95DF8B6A9`.
The audit retained 111 original/copy pairs / 99,487,847 bytes, including the
complete five terminal roots and eleven pinned executable files. Its initial
copy script exited 1 at a final console-only `Measure-Object bytes` summary
after all copies and both inventories had been written. That script and its
complete native failure remain unchanged; it was not rerun. Both subsequent
independent audit passes rehashed every original/copy pair successfully.
This audit-only print failure is not a product-test failure or a substitute
for the underlying native verdicts. The audit README also discloses a
tool-level authoring parse error before any tool call or file creation.

## Compatibility and human holds

Separately, fresh C1 execution reproduced all 40 original samples (773,759
bytes), twice, exactly matching both old sets and the first-admission
manifest. Its sealed root is
`out/recipe-compatibility/c1-reconstruction-20260906T034042Z-49c3daf3a5e6471192ad2c172f3d5ae9/`;
144-file non-self manifest twins:
`E17D2AC5D50247AAEF0D0F73E0B8BC7AAE72E3C8F2E9507EC36BEED5E71E0485`.
The seal excludes live source but retains captured inventories and 17
archived producer files. This was a fresh reconstruction, not archived
September execution or complete seven-recipe proof. Calibration clipping,
font diagnostics and visual-provenance limits remain. Embedded default
GoalPost/first flashcard-front pages are not complete standalone/boundary
coverage or typed-review/package evidence for those recipes.

The [seven-replacement authority](2026-09-05-recipe-replacement-authorization.md)
remains exact and unexpanded; no production version or admission changes
here. The eighth booklet decision awaits the typist. Schema 1 is hash-less;
ADR-007/010 remain Proposed; CC BY-SA 4.0 is a selected proposal, not an
operative grant. Real rights, consent, custody, withdrawal, appointments,
quorum/recusal and H0–H7 records remain absent. No assistant committee can
supply those acts. No site dispatch, release, tag, signing, installation,
distribution or correspondence is enacted.
