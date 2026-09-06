# Board review integrity and predecessor evidence

**Prepared 5 September 2026**, from `aa7410085a02359988ac70d9f5a85e2e79968295`
on `codex/board-review-integrity` (stacked from the unchanged prior head).

This is a bounded engineering continuation, not a real council record. The
[complete work register](accepted-improvement-register.md) remains active.
Earlier frozen evidence and changed-file manifests are unchanged.

## Board lock occurrence boundary

Constitution #10 and plan section 10.2 require declared exact facts to survive
review. Board-to-Brief deliberately maps supplied Note lines to
`TeacherOnlyNotice`; some legitimate source locks therefore originate only in
notes. Its previous catalog validator checked one H1 and exact lock occurrence
anywhere in the document. A learner-facing locked date could disappear while a
teacher-only copy satisfied the validator, even after fresh acknowledgement and
typed approval of the exact altered revision.

The original four-case unchanged-production baseline is retained at
`out/board-role-integrity/20260905T232101Z-8fc9316468d74a0390ece4b50333ad0e/`.
Both runs returned native **1**, with **3/4 passed**. The hidden-date case
returned typed approval; unchanged content and ordinary note editing passed,
and complete date removal was already refused. Summary SHA-256:
`22260473C2AC06E8F388A6075E0067BCB12F2D192E3D3FBEDB557FF7C5834939`;
84-file inventory:
`CA9F5701CE6FFAFFDDD623B2874F5C8DDFB9F3C321086362FD177D9D38CA2A3A`.
The main agent rehashed all 84 entries without mismatch and read the actual
TRXs and emitted approval observations. A compact capture display projected
null names/messages; the original full TRX, failure arrays and separate native
streams retain them. No abbreviated display is the primary evidence.

The expanded unchanged-production baseline is a different root:
`out/board-role-integrity/20260905T233812Z-expanded-49ebee5a038741a5bca75385114d4be8/`.
Release Unit build returned **0**, with **0 warnings and 0 errors**. Both test
runs returned **1**, with **7/10 passed**:

| Phase | Passed / total | Failed | Actual TRX SHA-256 |
| --- | ---: | ---: | --- |
| Expanded red 1 | 7/10 | 3 | `93D5DDCD9FBBE7CDB2378889B065D302DED75F3BDAB5FFEE2A69AD8B71354CD0` |
| Expanded red 2 | 7/10 | 3 | `40E49E598727240D6A0475058E86A687B6731286D62F78D3F9E35346B1131F62` |
| First candidate 1 | 198/198 | 0 | `B7FA95A5F04AFAC66DDFFBE8DC3C61D393B5B76B662701EEF19CB8A98BA08D6C` |
| First candidate 2 | 198/198 | 0 | `C5A4DA7E3E242B9D4C514203A07EA0D63BC2747DBF9F4ED3244E7904F7B36701` |

Each expanded red contains **five permitted controls, two already-correct
refusals and three erroneous typed approvals**, not seven permitted edits.
The three counterexamples are `LockedDateHiddenInTeacherOnlyNotice`,
`DuplicateLockVisibleCopyRemoved` and
`VisibleLockExtendedWithTeacherCopy`. Each reports no blocking issue and
typed approval of the exact attempted revision after fresh acknowledgement.
The full failure message has this measured form:

```text
Expected a blocking issue when the exact lock is absent altogether or its original non-teacher occurrence is lost. Synthetic edit <case>: blocking codes=[]; CanApprove after fresh acknowledgement=True; typed approval returned=True; refusal=None.
```

Here `<case>` is a displayed pattern; actual TRXs retain each complete message,
stack, semantic document, node-role and exact-token-occurrence observations.
No fixture reaches a render, save, print or export sink. The main agent read all
20 expanded observations directly. Baseline summary:
`91FB7F10521DC2B8B2DC0ABE74F531A2A9CD675854F7E8C0432AA299F85DE96F`;
84-file inventory:
`9738A7E1745B4E68BC3BA4A80DDA92AE0E0086E4FCCFFDA54691C31DD742F96E`.
Independent rereading found 84 files and zero mismatches.

An earlier expanded preparation build failed with CA1859 at
`BoardToBriefReviewIntegrityTests.cs(275,61)`: the private helper should return
`LockOccurrenceObservation[]`, not `IReadOnlyList<LockOccurrenceObservation>`.
Its 26-file evidence root
`out/board-role-integrity/20260905T233439Z-expanded-edc754eb84f84382aa2f3cdd2ebd6ef7/`
is unchanged. No tests ran there. The single return-type correction precedes
the successful expanded baseline; it is not an approval counterexample.

The measured repair captures an eager private subset of declared locks having
an exact occurrence in the original document outside `TeacherOnlyNotice`.
It uses the existing token-aware `ContainsExactOccurrence`, not a substring
test. Review retains one-H1 and all-document lock validation and additionally
checks that captured subset outside teacher-only notes. Complete-deletion
findings are deduplicated. Originally teacher-only locks remain supported and
their deletion remains refused. Note editing, paragraph reordering and changes
to unlocked prose remain possible.

The first candidate root is
`out/board-role-integrity/20260905T234756Z-candidate-b2bd30142ece444dbe28176c42410cb7/`.
Its Release Unit build returned **0**, with **0 warnings and 0 errors**.
The exact filter ORs `BoardToBriefReviewIntegrityTests`,
`BoardToBriefTests`, `ModuleStudioCatalogTests`, `ReviewSessionTests` and
`LockedFieldValidatorTests`, all under `Foundry.Tests.Unit`.
Both actual TRXs passed 198/198. Within the ten new cases, five permitted edits
returned exact typed approval and five removals/obscurations were refused.

First-candidate summary SHA-256:
`53848A098725029253CD72BF6616520183A23E47ABCBAC298626A93EB3C957FE`;
84-file inventory:
`727FBF9C08E78A51CEB9C1C2F66BF81FF0F8EDBF7FA28FEED26BE88B8B0CD872`.
The main agent independently rehashed all entries without mismatch and read
both actual TRXs and all twenty emitted role/approval observations. The 198
cases comprise 10 Board role, 5 Board builder, 42 catalog, 19 ReviewSession
and 122 locked-field controls.

Each expanded/candidate capture pins 485 source/build/control/document/root
inputs and 119 Unit output files, including before/after source and DLL copies.
The 484 inputs other than the authorized catalog change match the expanded
baseline in the first candidate. Inputs stayed stable across the three phases;
outputs changed at build, then remained stable within and between both
`--no-build --no-restore` runs. This scoped inventory is not exhaustive
asset/history coverage or independent source-to-binary proof.

The initial candidate's reused `locked.missing` message did not identify the
new non-teacher scope. **Later 5 September 2026:** that wording is clarified by
prefixing audience-only findings with `Outside teacher-only notes: `; the
same ten controls now require exactly one blocker in each of the five refusal
cases: the three audience-loss cases require the prefix, while the two complete
deletions forbid it. The five permitted cases require no blocker. The earlier candidate's bytes and successful
results are not relabeled as proof of this later wording. Final verification is
pending at this preparation cutoff.

## Exact PR22 hosted evidence

The [ledger](../evidence/evidence-ledger.json) appends the exact predecessor
head `aa7410085a02359988ac70d9f5a85e2e79968295`, sole parent
`8857a6474cc17cdfc7357dc56665b45dcced4fac`. Draft PR22's attempt-1
CI run `33997448266` concluded **failure**; its paired CodeQL run
`33997448198` concluded **success**. Both were created at
`2026-09-05T22:59:58Z`. Checkout logs bind the synthetic merge
`3182de76c7943cfa707dabaec6daa719fa442a32`, not main.

Direct inspection of all seven actual TRXs found **2,591/2,609 passed**,
18 Unit failures and no skipped/error/aborted tests. Unit passed 1,319/1,337;
Accessibility 26, Contract 175, InstructionalEvals 336, Integration 325,
Rendering 121 and UiAutomation 289 all passed. Native/runner exits were **1/1**,
with no outer timeout: 347,446 ms within the unchanged 900-second cap.
The clean 547-source and seven-assembly before/after records matched.
Identity errors were empty; the sole completeness error named the coherent
failed Unit suite. These records do not retain the remote DLL bytes or supply
independent source-to-binary proof.

The new first S-15 caller is
`Foundry.Tests.Unit.CiTestRunnerContractTests.Evidence_copy_rejects_source_and_destination_paths_outside_their_roots`,
duration 3.6799496 seconds. Its source-containment rejection first completed
safely; the second, destination-containment invocation retained this complete
primary error:

```text
Foundry.Tests.Unit.FixtureProcessException : Primary: DisposalFailure: fixture ownership did not settle cleanly.
NativeExit: 1; RootExitObserved: True; DescendantExit: NotEstablished
CleanupSettled: True; CaptureSettled: True; DisposalSettled: False; SafeToStartAnotherFixture: False
DisposalObservation: Stage: Queued; DeferredReasons: None; SharedBudgetMs: 500; DecisionElapsedMs: 0; RemainingAtDecisionMs: 500; WaitElapsedMs: 0; RemainingAtWaitMs: 500; CallbackEntryElapsedMs: NotObserved; CallbackExitElapsedMs: NotObserved; SnapshotElapsedMs: 519; TaskCompletionAtSnapshot: False; TaskFaultAtSnapshot: False; WaitReturnedSettled: False; TimelySettlement: NotEstablishedByObservations
Secondary outcomes:
DisposalDeadline: the owned disposal operation remains unsettled.
--- stdout (Eof; truncated=False) ---


--- stderr (Eof; truncated=False) ---
Exception: curated evidence file escapes its containment root:
C:\Users\runneradmin\AppData\Local\Temp\ocf-ci-evidence-copy-escape-6e48304407fe48cca47fcdba7f3d487e\outside-destination
.trx


```

The intentional native exit 1 is a containment refusal; the failing assertion
concerns unsettled disposal. The following 17 failures all refuse new process
creation and preserve that exact frozen description after their shared prefix.
They are not 17 new native failures. All complete names/messages/stacks and
identity comparisons remain in the actual Unit TRX and retained audit.
Neither later callback completion nor ThreadPool, scheduler or OS causation was
measured. The [sightings register](../evidence/sightings-register.md) keeps S-15
open; differences from earlier callers, exit outcomes, timing and refusal
counts are not concealed by a common-cause claim.

All 32 fixture-runner, 26 SourceLens and 325 Integration tests passed on this
same hosted run. The corrected zero-time control records a known-unstarted
adapter and exact owned-aggregate settlement, not live capture scheduling.
The PDF checkpoint records WaitingForActivation at 1,108.523–1,108.524 ms,
followed by successful waiter completion at 1,170.937 ms. Its separate
incomplete-file control retains a real TimeoutException at 5,004.412 ms.
No Edge child is launched by those controls. These own-run successes do not
close S-15, S-16 or S-17, diagnose equivalent load, or repair the previously
noted nested replacement-helper exception-precedence residual.

Hosted build and formatting passed; the secret-scan job passed with Gitleaks
8.30.1 reporting 134 commits and no leaks. Historical C1/C2 verification returned
`Outcome: verified`. Samples, coverage-threshold, dependency/vulnerability,
SBOM and portable gates were **skipped**; their success is not inferred.

The actual CodeQL SARIF is 638,214 bytes, SHA-256
`7C9D2C00E47FA02C00DC8932BE93C42F1EF95B8579FFCCF1DBCEDF7420F803F8`:
SARIF 2.1.0, CodeQL 2.26.4, one successful invocation, zero results and 444
execution notifications all level none. The gate says
`CodeQL SARIF clean across 1 file(s).` Configuration-notification and embedded
version-control provenance properties are absent; binding is supplied by
run/artifact/archive/checkout observations, not an invented SARIF field.
A clean analysis neither overrides CI failure nor proves broad security absence.

The complete retained root is `out/source-role-hosted-pr22/`. Its detached
229-file inventory SHA-256 is
`AAF40620A3FF3A4AF331725C5156E81349779F1215C9880F36107068D48EDDB7`;
both agents independently rehashed all 229 files without mismatch.
The primary bounded summary is
`362E3582F8FE708115E1F87467302423C6D74C9B89EE06E2CCCBC64CDE0CDDE8`;
Unit TRX:
`C5EECEDF431F277222442C9DF557730EADDE768FBA1F15C17D21D35AF97D0A30`;
complete `AUDIT.md`:
`2055DDA8DB6ABFF0DE8E648D60B7BF508D4CB728763EB9736AC5E633A1D266E2`.
All five artifact ZIP digests/sizes matched their API records. Seven archives
contained 101 files: 100 allowlisted evidence files were safely extracted;
the sole `.runner.lock` remained archive-only. Nothing was executed from an
archive. A read-only XML audit first failed because of a missing token space;
the retained corrected audit returned native 0 with no identity/copy errors.
That audit correction was not a CI rerun or replacement of primary evidence.

## I35 progress-row correction

The plan section 10.6 and historical review's Green-summary error was already
struck and corrected at ancestor
`4e7c6d7bfbd1d78e48646ea73e7a323c9cae5401`.
The existing `AcceptedImprovementScopeTests` erratum control and the six-row
[consumer inventory](accepted-improvement-register.md#i35-consumer-and-origin-audit)
already cover that factual correction. Only the stale I35 progress-row wording
is updated here. This does not establish authentic origins, legal clearance,
complete source-to-sink evidence, or a lane downgrade.

## Compatibility and authority boundary

This is a seventh direct changed identity in the scoped inventory:
`board-to-brief@0.1.0` catalog review behavior. Exact C1-to-candidate
compatibility/change disposition remains required before admission or merge,
alongside the six earlier direct changes and separately classified shared
semantics. Builder output, shared locked-field validation, engine/recipe
versions, schema-1 bytes and renderer routes are unchanged by this patch.
Default-sample equality cannot admit a changed validator.

The guard protects **at least one exact occurrence outside teacher-only nodes**
for each declared value originally occurring there. It does not preserve the
original semantic role, position, multiplicity or full prose; authenticate
source/rights; establish physical visibility; or confer human approval.
The movement control measures paragraph reordering, not arbitrary node-type
conversion. The broader cross-module audit remains open.

CC BY-SA 4.0 remains the selected proposal, not an operative grant. Exact owner,
covered material and contributor assent remain unsupplied. ADR-007 and ADR-010
remain Proposed. No recipe version, schema writer, migration or routing choice
is inferred. Real consent, custody/withdrawal, appointments, quorum, recusals and
protected-seat authority remain human prerequisites; H0–H7 remain NOT BEGUN.
Calibration visual QA still fails and the native-console matrix is unadmitted.
No main merge, site dispatch, version/tag, signing, installation, distribution,
filing or correspondence is performed by this continuation.

## Final local closing

~~Pending at this preparation cutoff.~~ **Later 5 September 2026 (local date;
the following execution timestamps are 6 September UTC):** the full ordered
local close completed. It is not hosted evidence for a later head or admission.

The root is
`out/board-review-close/closing-20260906T000146Z-372682f6bb2742faab66aa7fbcae2667/`.
Every command retains its arguments, start/end times, native exit, and separate
stdout/stderr hashes in its own receipt. No combined stream or watcher verdict
substitutes for the underlying measurement.

| Gate, in order | Measured verdict |
| --- | --- |
| `git diff --check` | Exit 0; only the expected Git LF/CRLF notices on stderr |
| Release solution build, `--no-restore -warnaserror` | Exit 0; `Build succeeded.` / `0 Warning(s)` / `0 Error(s)` |
| `dotnet format OpenClassroomFoundry.slnx --no-restore` | Exit 0 |
| Format `--verify-no-changes` | Exit 0 |
| Release `-t:Rebuild`, warnings as errors | Exit 0; `Build succeeded.` / `0 Warning(s)` / `0 Error(s)` |
| `tools/run-ci-tests.ps1`, full A | Native/runner 0/0; 2,619/2,619; no skipped/error/aborted tests |
| Same full command, stability B | Native/runner 0/0; 2,619/2,619; no skipped/error/aborted tests |

Full A is
`out/ci-test-run/20260906T000311Z-f3d60c979b64465b8a8834e4180ea7af/`,
summary SHA-256
`7363F45FDC72B4FA1EE1B0FF5CDC3D532C1400C70B62583CF62BB1857C1BD957`.
The test process ran 00:03:32.3604264–00:07:46.5700855 UTC, 254,209 ms.
Full B is
`out/ci-test-run/20260906T000755Z-f46f427caa94480c8f8af71c532a8436/`,
summary SHA-256
`A56631194EB92A5A361C7C7D696E4BF927421CDF6F33EBBE3257788E9104CE5C`.
It ran 00:08:10.9598056–00:11:32.8553576 UTC, 201,895 ms.
Neither reached the unchanged 900-second cap. Root exit and output drain were
observed, safe reuse was true, and identity/completeness/snapshot errors were
absent. This is not adversarial descendant-exit or independent
source-to-binary proof.

The main agent read all fourteen actual TRXs. Each run passed Accessibility 26,
Contract 175, InstructionalEvals 336, Integration 325, Rendering 121,
UiAutomation 289 and Unit 1,347. Actual Unit TRX hashes are
`D497B70EDE413E205D71A9C3DD2F87F5CA0237A2869B419019B57CAD745C2516`
and `6D7E10FD8127A7F62293A92EE569151522979A1D0703A924933240288693851D`.
All ten final Board cases are included, with five exact typed approvals and
five single-blocker refusals; audience-only messages carry the new prefix,
complete-deletion messages do not. The 198-case earlier filter is not reported
as a separately repeated final focused run.

Both runs record the same 550-source dirty candidate digest
`22FA7DF5B089817BF4EFB99B99C7FE4D3A377546DED78B089B6B08C840886CD7`
and seven before/after test-assembly identities. Unit DLL SHA-256 is
`EDB341627D35427A32551B2C7230F13BF8A7BF94E41A48072C9C7540CEFA14F1`.
The formatter-normalized catalog and Board-test raw hashes are respectively
`EB42B622F6E2C3E99228801D04FC5C1E4757F4DC2D9041B0FE8132B2502B9CF2`
and `43779E5632F756110D90255414BC9BE9DC625E497EFF7F06B7E6816C088A209D`.
These source/test bytes remained unchanged through both runs. This final
factual record and navigation are completed afterward; they do not replace
the earlier source-state cutoff or claim a clean release tree.

The default SampleGenerator then ran twice, without seeded mode, at
`out/board-review-close/samples-20260906T001254Z-c209748c51ef40528c4a8ced478ac07d/`.
Both invocations exited 0. The actual verdict was:
`Every byte of all 40 default sample files matched across two runs and the frozen first-admission baseline.`
Each run produced 773,759 bytes. Complete source/generator inventories remained
stable; no sample or baseline was overwritten. Receipt SHA-256:
`515F0D1A0B50ADDBA8225A99766B7813B783A48BE02B566BEFDE4F9931847113`;
87-file inventory:
`C14995D9736A0371C2F00B3720B07DDEFD4CF0B986010D04745107D5F7F71772`.
These are default-output byte checks, not Board review-validator coverage,
new visual/physical/AT evidence, rights clearance or compatibility admission.

The new [detached changed-file inventory](board-review-integrity-files.json)
binds this continuation's non-self files with strict UTF-8 and CRLF-to-LF-only
canonicalization. It is derived twice from complete independent reads. It
does not replace any historical manifest. Final record guards and the staged
file-set comparison are separate checks of this later factual closing state.
