# Fixture-disposal observations and predecessor evidence

**Prepared 5 September 2026**, from `80f52b09571b4d190ffbb89f63cbe0f43f317fa7`
on `codex/fixture-disposal-observations`.

This continuation closes a diagnostic gap, not the recurring hosted disposal
failure. The [complete work register](accepted-improvement-register.md) remains
active. The predecessor [governance-record evidence](governance-record-guard-evidence.md)
and every earlier frozen manifest remain historical and unchanged.

## I33 — diagnostic-only disposal observations

The prior helper could report disposal failure without distinguishing a queued
callback, entered callback, callback exit, deferred disposal, or consumption
of the shared settlement budget. The new immutable observation records these,
decision/wait budget readings, and separately observed task completion/fault and
wait return. Nullable timestamps distinguish absence from elapsed zero.
Completion/fault are observed before the atomically acquired immutable callback
entry/exit pair. These are sequential observations, not one atomic sample;
the final elapsed reading is not an exact task-completion timestamp.
Queued means entry was unobserved, not that a scheduler cause was diagnosed.

The unchanged safety expression and sticky refusal do not consume these new
fields. Default disposal scheduling remains `Task.Run`; the same shared clock
still starts before capture cancellation. Work/cleanup/drain/maximum settlement
caps remain 30,000/2,000/2,000/500 ms, and capture is capped at 1,048,576 characters.
The disposal-only scheduler and monotonic-clock seams are trusted process-free
test boundaries, not new hostile-input APIs. Cleanup and native start/wait
behavior are unchanged.

Callback exit is not task completion, which is not proof of timely settlement.
`WaitSettled` still checks `IsCompleted` before a nonpositive remaining budget:
the retained synthetic zero-wait-budget case reports Safe=true without proving
timeliness. Diagnostics explicitly say `TimelySettlement: NotEstablishedByObservations`.
No scheduler repair, timeout increase, late-completion pardon, descendant-exit
proof or hosted cause is asserted.

### Measured red/green controls

Ignored proof root:
`out/fixture-disposal-observations/20260905T203448Z-0412ec9e23b24dda92dbae11b0b855c5/`.

| Actual run | Executed | Passed | Failed | Native exit | TRX SHA-256 |
| --- | ---: | ---: | ---: | ---: | --- |
| red-01 | 30 | 21 | 9 | 1 | `438AF76E05E7450AB4EAD6761D43F1325123A95DAFDAACD8F465CA748968AE1E` |
| red-02 | 30 | 21 | 9 | 1 | `DC5BFE3707FAD23DDCB8105D26CFE1F94AE18CBF7D08E6413108E9D6BCDFCA21` |
| green-01 | 62 | 62 | 0 | 0 | `7C279AEB6EA70D734EA52F6F6B6BFEAA24400F05BD7E4DA624F7CBEB200CDE5C` |
| green-02 | 62 | 62 | 0 | 0 | `F80255BF2781139326BB15F80728055F04A7792C57E98771492D67A4969E8337` |

Both reds used the behavior-preserving seam extraction, not the untouched
parent. Each retains nine full failure names, stacks and the exact message:
`The fixture result does not retain disposal scheduling, callback progress, and shared-budget observations.`
The 21 unchanged process-free controls passed; the existing native childless
timeout control was excluded from red. Both greens select the fixture and
CiTestRunner contract classes, including that native control, not the solution.

Nine controls were red-measured twice: queued/late entry; entered/pending;
budget exhausted before scheduling; budget consumed before waiting; capture
unsettled without budget exhaustion; zero-elapsed entry/exit/completion;
faulted callback exit; completion with zero wait budget without a timeliness
claim; and no adapter versus deferred disposal. The additional
`Disposal_observation_does_not_promote_callback_exit_to_task_completion`
control was added afterward and is explicitly green-only. Finally blocks
release synthetic gates and await the exact owned tasks. Late completion leaves
the earlier description and refusal intact.

Scoped Release builds, format fix, verify and final build exited 0; actual
build logs report zero warnings/errors. Native stdout/stderr remain separate.
Every test receipt retains eight input and 119 Unit-output identities unchanged
before/after; red pairs and green pairs match separately. Build/format changes
are not represented as unchanged identities. The main agent and independent
reviewer rehashed all 48 inventory files without mismatch and read actual TRXs.
The independent read-only source/proof audit found no actionable defect within
this diagnostic scope; it supplies neither native-cause nor release clearance.

| Retained identity | SHA-256 |
| --- | --- |
| `summary.json` | `2531D16FD3ADBACF7783459086604D33B1E24DCF112C4E5603C7E6241FF8FB01` |
| `inventory.json` | `EDFE732EBAE6D2FE3630E900F03FD33E70ADEF9CBF335234A95DFB081D70F803` |
| Raw formatted helper source | `987DC8C82AE532D2761EB9D0AF7BE5B785A351A4FC8428639332F1F8955499E4` |
| Raw formatted control source | `1C17CAF658D1BF206056F25EDEF87AF8A306F75113ADF2FE551D9BFAFB7E7259` |
| Focused Unit DLL | `375EE74D04A3D60A0CAF04FC8A08890E07FCFE799F060D68A7A87F43E9663B21` |

## I34 — exact PR20 head is CI red, CodeQL green

The [ledger](../evidence/evidence-ledger.json) is the hosted-conclusion authority.
Its two PR20 rows bind head `80f52b09571b4d190ffbb89f63cbe0f43f317fa7`,
attempt 1 and creation time `2026-09-05T20:29:07Z`. Only the CI row records
PR synthetic checkout `58a1afb89ad3bf7d5058d737b4cd48fba51b3e05`;
CodeQL's checkout field remains null. The retained checkout audit supplies the
relationship to base `94971ed806422c851ed4bdd137b9a37f17fe9875`.
This is not main or this later
diagnostic candidate's hosted proof. No rerun was dispatched.

Actual seven TRXs: 2,544 passed / 2,568 executed, 24 failed; Unit 1,277/1,301
and all six other suites passing. All 22 prior fixture-helper controls passed.
The earliest failure is
`Foundry.Tests.Unit.CiTestRunnerContractTests.Evidence_snapshot_rejects_balanced_duplicates_and_omitted_suite_artifacts`,
20:33:20.3668967Z to 20:33:25.5637602Z, reported duration 5.1883983 seconds:

```text
Foundry.Tests.Unit.FixtureProcessException : Primary: DisposalFailure: fixture ownership did not settle cleanly.
NativeExit: 0; RootExitObserved: True; DescendantExit: NotEstablished
CleanupSettled: True; CaptureSettled: True; DisposalSettled: False; SafeToStartAnotherFixture: False
Secondary outcomes:
DisposalDeadline: the owned disposal operation remains unsettled.
```

Stdout retains complete synthetic Alpha/Beta JSON, EOF/nontruncated; stderr is
EOF/nontruncated and empty. The next 23 failure messages retain that prior
result, prefixed by:

```text
System.InvalidOperationException : A prior fixture has uncertain cleanup/capture; no new process was created. A fresh test host is required after ownership is resolved.
```

All 24 decoded Message fields match PR19 by exact test name, not timestamps,
durations or stacks. These 23 refusals are not independent native failures.
[S-15](../evidence/sightings-register.md) remains open: callback entry, remaining
budget, later completion and scheduling/OS cause were unmeasured in both
predecessors. Synthetic controls do not retroactively provide them or equivalent
hosted load.

Outer receipt `20260905T203223Z-a83e1d17ac8b43ac9bd59d08b3cc18d1` records
native/runner 1/1, no outer timeout, 350,990 ms within the unchanged 900-second
cap, root exit, drain completion and safe outer ownership. Clean 540-source
and seven-assembly pre/post identities match; identity errors are empty.
The sole completeness error identifies the complete failed Unit run. This
outer settlement does not settle the inner fixture.

TRXs, coverage XML and duplicate copies were independently checked; archives
match API digests. Equal snapshots are not independent source-to-binary proof;
remote DLL bytes were not retained. Secret scan passed. Sample generation,
comparison, hosted coverage threshold, dependency/vulnerability and SBOM steps
were skipped, not passed. No proof is borrowed from earlier green runs.

Ignored CI root:
`out/fixture-disposal-hosted/33990242717/20260905T203914Z-21c5eb730f0d4c7698df5ec324345668/`.
The full independent `AUDIT.md` SHA-256 is
`B10ACCE528B5B44C5E692FA7ED172F176280622058F45EE59CC4FB74624BD9C6`;
actual bounded summary
`5888B4182948659641292A3FD67E391510DA48D3E0F52AD99AC7DE4E5ABC6CDC`;
actual Unit TRX
`FD9597675DC79AB8AE61CC6ED7D9667DF4B319D68039EB827ED3E6AFD928E8D2`.

The paired successful CodeQL run's actual `csharp.sarif` is 637,937 bytes,
SHA-256 `C2A2CFBD4F7A7C6FDD7863AE9E43EE1D236D8C3255CAB0532FB577589454B967`:
SARIF 2.1.0, CodeQL 2.26.4, one successful invocation, zero results, 443
execution notifications all level none. No embedded version-control provenance
exists; run/archive/checkout evidence supplies the limited binding.
This is not general security assurance and does not override paired red CI.

## Closing cutoff and unchanged holds

Full closing is pending at this preparation cutoff. Only the test helper and
its controls change executable code; no press, renderer, recipe, schema or
version change is introduced. Earlier compatibility dispositions, failed
calibration visual QA, unadmitted console work, real participant/protected-seat
evidence, licensing scope/authority/assent and both Proposed ADRs remain open.
H0–H7 remain NOT BEGUN. No main merge, site dispatch, publication or release
is claimed. A later factual append must identify its tested source/assembly
cutoff and detached manifest without rewriting frozen predecessor evidence.

## Later 5 September 2026 — local closing completed

~~Full closing was pending at the preparation cutoff above.~~
Release build (warnings-as-errors), format fix, verify-no-changes, post-format
Release rebuild and both full runs exited 0 in the required order. The actual
build logs report zero warnings/errors. Gate streams remain separate and all
14 stream hashes/lengths were rechecked without mismatch; initial diff-check
stderr contains only six LF/CRLF advisories. Gate receipt root:
`out/fixture-disposal-close/closing-20260905T205931Z-9d1247d80c654ece9b8f7beefd26aac8/`;
`gates.json` SHA-256
`E06712CBD97DE39D03B41DFF67C45E3C38CA7527D0B3AE69C02ACC0736EB78B3`.

| Full run under `out/ci-test-run/` | Actual passed/executed | Native/runner | Elapsed ms | Summary SHA-256 | Unit TRX SHA-256 |
| --- | ---: | --- | ---: | --- | --- |
| `20260905T210026Z-d703129bf4f04451b62a76e686b566bf` | 2,578/2,578 | 0/0 | 213,113 | `A8790AAACDF7873426F4CB9CEA7981032D71074EBE14A85F6497FACF0DF4A048` | `0F6431455DD486DA1C3A354365D4DCE2D7A07CC843A87A790F564C437EBE007C` |
| `20260905T210424Z-412e8432a9f74e55b5929353d49a51e7` | 2,578/2,578 | 0/0 | 213,039 | `1C712E75B1E822BD45B5AE7CDB11BF394574F5A71652A22FF23D3A9475AA61D7` | `35F0709E7CF98EAD98B2AF64FAD0F9195B699B5CA34C5835D4AF2C1DC7792F2D` |

Each has 26 Accessibility, 175 Contract, 336 InstructionalEvals, 320 Integration,
121 Rendering, 289 UiAutomation and 1,311 Unit results; no skips, errors or
aborts. Both actual summaries and all 14 TRXs were read, with 28 retained
TRX/coverage copies rehashed. Independent review also checked result, definition,
entry, execution and suite-storage identities and coverage roots.

All four source snapshots match: 10 dirty entries at parent `80f52b0`,
542 source files, content digest
`4E67737389FBF39CA338F37DED65D55918731C35ED7D96340339F0F8BC7E04D1`,
status digest `FCF58A2A7C295B5770B2FB47953B133F1D31C88267F3795132283198CE123021`.
All seven assembly snapshots match within and between runs and the independently
read current DLLs. Unit is `375EE74D04A3D60A0CAF04FC8A08890E07FCFE799F060D68A7A87F43E9663B21`.
Both runs retain root/drain/safe=true, no outer timeout, empty identity and
completeness errors, and null snapshot error. This does not independently prove
source-to-binary provenance or diagnose S-15.

The final test process ended at `2026-09-05T21:08:12.3459227Z`; the gate wrapper
finished at `21:08:20.3890726Z`. Source/index and all externally changed
test-visible inputs stayed frozen during the full pair. The factual closing
append, corrected CI-versus-CodeQL ledger attribution and new detached manifest
follow that cutoff. Neither executable source changed after it.

The [detached changed-file manifest](fixture-disposal-observation-files.json)
binds ten non-self changed paths in this continuation's first commit, using
strict UTF-8 with literal CRLF-to-LF only. It does not replace a historical
manifest or contain its own hash. No press/renderer changed, so no new sample
generation is represented as part of this diagnostic slice. A local pass
neither erases predecessor failures nor supplies this candidate's hosted proof.

The later live-ledger measurement read 81 entries with zero mismatches at
`2026-09-05T21:11:07.9314466Z`; native exit 0. Receipt
`out/evidence-ledger-measurement/20260905T211107Z-1ddbe9c782c6473c92c04a7e2105bf03.json`
has SHA-256 `FE1FD6CEC65AF21A8B3964D2E4BEBE48AB867AEC660A72D6B01B27C5503BF2B4`.
This measures run metadata and local graph agreement, not a new test verdict.

The C1/C2 verifier exited 0 and its actual receipt says `Outcome: verified`,
with C1 as C2's sole parent, both in HEAD ancestry, exactly the original six C2
files, and unchanged decision/sample Git blobs. Its `history-receipt.json`
under the closing root has SHA-256
`E9C294EFE35FB27754B4795C18B7E09BC9D566BCBB0F754CFF3FA2C36704876C`.
This does not admit later changed recipe behavior.
