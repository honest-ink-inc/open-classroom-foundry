# Governance-record guards and exact hosted evidence

**Prepared:** 5 September 2026. **Starting source:**
`94971ed806422c851ed4bdd137b9a37f17fe9875`. **Branch:**
`codex/governance-record-guards`.

This continues the [fixture-process evidence](fixture-process-evidence.md)
without changing its frozen files or conclusions. This is synthetic engineering
verification, not participant consent, council enactment, authentication,
protected review, recipe admission or publication. The
[complete register](accepted-improvement-register.md) remains active.

## I37 — two measured record-validation gaps

The existing H0 instrument binds the date-only session to its printed UTC day;
operating terms must cover that whole day. The previous validator accepted a
positive Int32 duration without an upper bound, including 1,441 and
2,147,483,647 minutes. Separately, the existing forbidden-current-state scan
treated ASCII hyphens as part of an opaque token even in disposition prose.
Both feasibility and product-owner disposition therefore accepted such phrases
as `withdrawn-from-use` or `roster-stale` alongside the required effective
suffix.

~~These two counterexamples were accepted by the existing validator.~~
**5 September 2026:** the session duration now requires 1–1,440 minutes, and
the forbidden-state check treats hyphens as word boundaries only in current
disposition prose. The shared opaque-reference token grammar is unchanged.

The duration cap is necessary for the existing date-only convention; it does
not prove a real start time, actual interval coverage, attendance or authority.
The prose screen is lexical, not semantic understanding or authentication.
No council policy, recusal representation, completed record, schema/version,
recipe, output renderer or production timeout changed.

### Measured red controls and scoped repair

Evidence root:
`out/governance-record-guards/20260905T194653Z-c81e53de75454a459583e4cb3914960f/`.
Raw receipts are retained locally under ignored `out/`, not tracked here.

The first red run executed 314 controls: 287 passed and 27 failed. One extra
failure was in the newly written positive control, which called
`ReplaceRequired` with unchanged 60-minute text before reaching the validator:

```text
Assert.NotEqual() Failure: Strings are equal
Expected: Not "# Atlas 2.0 council priority session\r\n\r\n**Stat"···
Actual:       "# Atlas 2.0 council priority session\r\n\r\n**Stat"···
```

That test-construction error was corrected before the next two runs; production
was still unchanged. It is not counted as a product counterexample or a
load-related sighting. Both corrected red runs executed **314: 288 passed,
26 failed**, with identical failure names and messages. The failures comprise
two overlong durations and these 12 hyphenated forbidden states through each
of the two consumers:

```text
ambiguous-state
conflicting-event
missing-link
restricted-use
revoked-permission
stale-roster
superseded-record
unresolved-event
withdrawn-from-use
roster-stale
chain-withdrawn-from-use
WiThDrAwN-from-use
```

Each failed assertion names its exact duration or state. Examples, with no
paraphrase of the messages:

```text
The session validator accepted duration '1441' minutes outside the positive one-UTC-day bound.
The session validator accepted duration '2147483647' minutes outside the positive one-UTC-day bound.
The feasibility validator accepted explicit current-chain state 'withdrawn-from-use' as effective.
The disposition validator accepted explicit current-chain state 'withdrawn-from-use' as effective.
```

The 36 new cases also retain positive 1/60/1,440-minute controls, zero/negative/
overflow refusals, benign `final-byte records` prose and `CUST-NONE-01`
opaque references. After the narrow production repair, two scoped runs each
passed **316/316**: 314 validator cases plus two existing governance-document
cases. No failures or skips occurred. Four scoped Release builds and the two
project-specific format fix/verify pairs exited 0; the final build logged zero
warnings and errors. These are scoped gates, not full-solution or hosted proof.

| Actual evidence | SHA-256 |
|---|---|
| `red-01/red-01.trx` | `5161F8911691EAF7319C6562A0866789AF35346BA64E01230359FE19A51E9861` |
| `red-02/red-02.trx` | `E34A89B73B8BBF4D0191893404EBB9E225F014F07484E8C0D273549196550FDD` |
| `red-03/red-03.trx` | `EDCCF47D23A1480067E1A47CFE6A0AC4D7091859432435136E49493EC7344C77` |
| `green-01/green-01.trx` | `17FA8D4CBF3803F2DCD1929ECADE9615D720A343744DB2DCBED4C8D4454D8303` |
| `green-02/green-02.trx` | `E994B4BE3F46DEE9F12F761226D17FC3258CC0A35A54762367829EA71D55E382` |
| Thirteen-phase `summary.json` | `4BD9706C8C58F0244D3CB7612E3AE90A3B6A31429D653CD6F45615E1D8AF6CB1` |
| Detached 66-file `inventory.json` | `F39A227467D383ADC7D7CCF5A044E3B7F431D7D0091F9AD7928C9DA872310786` |

The main hand rehashed all 66 inventory files with zero mismatches and read
both actual green TRXs. An independent read-only review matched the final
source snapshots, both corrected red TRXs and both green TRXs; it found no
concrete blocker in the narrow diff.

| Input/output at scoped cutoff | Raw SHA-256 |
|---|---|
| `tools/AtlasCouncilRecords/AtlasCouncilRecordValidator.cs` | `D4992BC81318B64CF54F09BB65A2DE559B4D305C71888A31FF5012B26D5907B3` |
| `tests/Unit/AtlasCouncilRecordValidatorTests.cs` | `6ED84AAC2D4526AE08F60FD5B6CD589F302578F5117DA596B5808E025BD9400A` |
| Scoped Unit DLL | `FABFA8E56CBD9C30CD1305008CE842B1C9DFC3E19CC9433DE9006FCE12717864` |
| Scoped validator DLL | `84B88D7DA0A6BD8312EB53D12E19C161A0D16563865C18AE2524A09D250A59A3` |

## I33/I34 — exact PR19 head is CI red, CodeQL green

The [ledger](../evidence/evidence-ledger.json) records the directly read
attempt-1 conclusions for head
`94971ed806422c851ed4bdd137b9a37f17fe9875`:
CI `33987933433` failed; CodeQL `33987933434` succeeded.
The PR checkout was synthetic merge
`6e400f26734fa66ff038eec76010d0355fd64e29`, with base
`483649cad1a8f3f233d6065aaac404dac553bdc0`; it is not main.

All seven actual CI TRXs contain **2,508 passed / 2,532 executed**. The 24
failures are all in `CiTestRunnerContractTests`; all 22
`FixtureProcessRunnerTests` passed. The earliest failure was
`Evidence_snapshot_rejects_balanced_duplicates_and_omitted_suite_artifacts`,
lasting 5.9751540 seconds, starting at `2026-09-05T19:46:35.9335219Z`.
Its exact primary and ownership diagnostic reads:

```text
Foundry.Tests.Unit.FixtureProcessException : Primary: DisposalFailure: fixture ownership did not settle cleanly.
NativeExit: 0; RootExitObserved: True; DescendantExit: NotEstablished
CleanupSettled: True; CaptureSettled: True; DisposalSettled: False; SafeToStartAnotherFixture: False
Secondary outcomes:
DisposalDeadline: the owned disposal operation remains unsettled.
```

Both streams reached EOF without truncation; stderr was empty. Stdout retained
the complete synthetic Alpha/Beta JSON, including the expected two-Alpha/
zero-Beta coverage errors. The full message, JSON and stack remain in the
actual Unit TRX. Each following failure carries the same prior diagnostic,
preceded by this exact refusal:

```text
System.InvalidOperationException : A prior fixture has uncertain cleanup/capture; no new process was created. A fresh test host is required after ownership is resolved.
```

The main hand compared all 23 complete decoded refusal messages;
they are identical. These are 23 refused attempts after one observed unsettled
operation, not evidence of 24 independent native failures. All names, messages,
stacks and output are retained in the full TRX and the independent inspection
receipt. Why disposal missed its wait, whether it had started, how much of the
shared settlement budget remained, and whether it later completed are
**not established**. No ThreadPool, OS, S-13 or N-02 cause is inferred.

The outer runner/native exits were 1/1, no outer timeout, elapsed 340,871 ms
inside the unchanged 900-second cap. Source and assembly snapshots were stable;
root-exit/drain/safe flags were true and identity errors empty. The sole
completeness error names the coherent failed Unit run, not missing evidence.
All other six suites passed: Accessibility 26, Contract 175,
InstructionalEvals 336, Integration 320, Rendering 121 and UI Automation 289.
The secret scan succeeded. Sample, coverage-threshold, dependency,
vulnerability and SBOM gates were skipped, not passed.

The independent audit checked downloaded archives against the hosting API's
size and SHA-256, all retained TRX/coverage copies and duplicate bounded
artifacts. Remote assembly bytes were not retained, so pre/post hashes are
not independent source-to-binary provenance. The complete audit, extraction
and test-inspection receipts are in:
`out/governance-record-hosted/33987933433/`.

CodeQL's actual 637,937-byte SARIF is version 2.1.0, CodeQL 2.26.4, one
successful invocation, zero results and 443 execution notifications all at
level `none`. Head binding comes from API/archive identity and checkout logs,
not an absent SARIF provenance field. This is not general security proof and
does not override red CI.

| Actual evidence | SHA-256 |
|---|---|
| CI bounded `summary.json` | `85A432CEC1AA7A3E2E7194C965ADF756C97BF5FD3C7567BBA0064374AE996D61` |
| CI bounded `trx/Unit.trx` | `30297CB8E4A9E11C8CF7A60FB92DA5A3D2778229667734DC05A775F037AD6CE2` |
| CI independent `test-inspection-receipt.json` | `C058EA92183546D1D014F00FE008C1ED2AB7351CA5A49EBE66FB635BD464E25B` |
| Independent `AUDIT.md` | `BABA596F769DB469F6BFCBC8FA418E884E9196C0FF8FC5D491BE8055844FDC70` |
| CodeQL uncompressed `csharp.sarif` | `7250982C47C4BC19AC167B93FE3CFABF1DEC5A72A3BA468FC90AFCA381981BF3` |

### Isolated local non-reproduction

The exact first failed selector passed locally 1/1 in 3.2021563 seconds before
any further rebuild, native exit 0. Caller/helper/control-source and Unit-DLL
hashes were captured before and after and agreed; the Unit DLL was the scoped
`FABFA8E5...` output above, **not** retained hosted binary bytes. Both native
fixtures in that one test recorded safe settled ownership and separate EOF
streams. This is not equivalent hosted load, diagnosis or closure of the new
[S-15 sighting](../evidence/sightings-register.md).

Root:
`out/governance-record-close/isolated-fixture-20260905T200429Z-22637565946449418ac30b938a89b0e0/`.

- Actual `isolated.trx`: `6C9D6C52E28C567BE6D424638E53C86838C3AF5373D0B42EE259CCFA4EC7FDB8`.
- Generated `receipt.json`: `55411F84CAA99CAEFFAAB13B882D8C4B43CEA12ACDA5E24DA8BB3185572CA4B4`.

No fixture helper or deadline is changed by this governance-record repair.
The next diagnostic must distinguish queued disposal from begun execution
and prior settlement-budget consumption, without upgrading uncertain ownership
to success.

## Historical truth correction and remaining close

The RepositoryHygieneTests documentation comment previously said “Nothing was
lost” after the study-key exposure. Its dated correction now agrees with the
[hardening checklist](../release/hardening-checklist.md): the unavailable
traffic interval cannot support that claim; no participant was known to have
the URL. This changes no hygiene test behavior or historical event bytes.

Full Release build, format fix/verify, rebuild and two complete solution runs
remain pending at this preparation cutoff. No new press or renderer changes
are made, so no new SampleGenerator comparison is claimed or required for this
diff. Later closing evidence must be appended, not backdated.

The five identified recipe-behavior changes in the earlier stack remain held
for exact compatibility disposition; the scoped inventories are not an
exhaustive C1-to-present comparison. Calibration visual QA still fails.
CC BY-SA 4.0 remains the selected proposal, not an operative grant.
ADR-007 and ADR-010 remain Proposed; schema 1 remains hash-less. H0–H7 remain
NOT BEGUN, with participant consent/custody, real appointments/quorum/recusal,
protected review and human freeze records unsupplied. No main merge, site
dispatch, version/tag, signing, installation, distribution, filing or
correspondence is performed or inferred.

## Full closing — appended after both terminal receipts

~~Full closing remained pending at the preparation cutoff above.~~
**Later 5 September 2026:** the Release build, format fix, independent format
verification and post-format Release rebuild all exited 0. Both build logs
record zero warnings and errors; warnings were errors. Diff-check exited 0;
its stderr contains only seven Git CRLF checkout advisories, not a hidden
build/test failure. All other captured gate stderr streams are empty.

The complete solution was then tested twice through the unchanged bounded
runner, with no test filter, retry or altered timeout. Each actual set of seven
TRXs contains **2,568 passed / 2,568 executed**, with zero failed, skipped,
error or aborted results: Accessibility 26, Contract 175, InstructionalEvals
336, Integration 320, Rendering 121, UI Automation 289 and Unit 1,301.

| Full run | Native / runner exit | Elapsed ms; outer cap | Actual summary SHA-256 |
|---|---|---|---|
| `20260905T201259Z-d3495090b3e0430b8756f927c2ffb593` | 0 / 0 | 246,618; 900 s | `23B800935B835CC61A67918CEEE215105994D90634B66EDA0AD58C01ABAD400E` |
| `20260905T201735Z-e8eb5ed0466d409fbd296f9048fbb229` | 0 / 0 | 189,750; 900 s | `766820AAB0F81D834379A743C99565691B7CAF6A1CAF6A62F733F2001A865467` |

Neither run timed out. Both retain observed root exit, output drain and safe
outer-runner settlement; identity/completeness errors are empty and the
snapshot error is null. The same eleven-entry dirty tree at `94971ed...` and
all seven Release assembly identities were stable within and between runs.
The 539-source digest is
`FA38476C14823468DDB21C07AA753CE6076452A4266820B96F5E3186C96FB2C5`;
status digest is `16EA2DFDF8CAAE5E1BA743A3AF64D9AC3C4770F6620AEAB0D9CFFFBA7319F935`.
The full-run Unit DLL is
`DBAB4F0E3DE1DB75AA72BC26A5DAB05ACBCE8D7C4985E5E9CBFC172ECC8B368E`,
not the earlier focused DLL. The two I37 source hashes above are unchanged;
the formatted hygiene-comment source is
`E935B81987EDEAFD4418AA746807381D171220F9AD717A73B7E2EDF3831CF2CB`.

The main hand parsed all fourteen actual TRXs and rehashed all 28 retained
TRX/coverage copies against their recorded source and copied hashes, with no
mismatch. Source/index and externally changed test-visible inputs, including
ignored project files, remained frozen during both runs. The actual builds
preceded these `--no-build` tests; stability alone is not source-to-binary
provenance. These later factual paragraphs and the detached changed-file
manifest were prepared after the full-test cutoff, not represented as earlier
test inputs. No C# or tool behavior changes followed those full runs.

Local gate root:
`out/governance-record-close/closing-20260905T201129Z-8d17235c9ac9470fac789a67a809cbb6/`.
Its separate streams and per-command native receipts are retained. Generated
`gates.json` SHA-256 is
`7FCE5415B6ED5B3EC164040405A09CC947B9153BB0703F9CF09A2EDC13FFF0BD`;
main-derived `full-audit.json` is
`47F5F459D45BA25B6DCAA7C9F1581CA3391488C60380878CD2223EAC452476CB`.
No new press/sample comparison is claimed for this non-press diff, and no
local pass diagnoses S-15 or supplies a later hosted conclusion.

An independent read-only audit subsequently confirmed both actual result sets,
their definitions/execution identities/suite bindings, all 28 retained copy
hashes and all seven current assembly hashes against both snapshots. Integration
coverage records 6,445/13,521 lines in A and 6,447/13,521 in B; each agrees with
its own receipt. This difference is not an artifact mismatch or diagnosed cause.

The live ledger measurement at `2026-09-05T20:22:55.2730432Z` read each hosted
run and the local Git graph: **79 entries, zero mismatches**, native exit 0.
Receipt `out/evidence-ledger-measurement/20260905T202255Z-f3532c78fa2043a3a055de31ab575eec.json`
has SHA-256 `D5FC8D0D18152F813CB745A2868FF1206BA7B971AA4D82D0FCA11FD3ED1BA54A`.
It proves agreement at that time, not a later-head conclusion or approval.

The [detached changed-file manifest](governance-record-guard-files.json)
binds this first continuation commit's non-self changed files using strict
UTF-8 and literal CRLF-to-LF normalization only. It contains no self-hash and
must not be rewritten for a later continuation. All earlier frozen manifests,
the C1/C2 admission chain and the original sample baseline remain intact.
