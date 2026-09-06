# Layout and console continuation — hosted evidence

**Measured 5 September 2026.** This record applies to implementation head
`453e87b80ea0abc3e1a022de6289a0e58aac67e9` on
`codex/layout-and-console-evidence`, pushed in
[PR17](https://github.com/honest-ink-inc/open-classroom-foundry/pull/17).
It is stacked on PR16. No pull request was merged by this work.

The [local closing record](layout-console-evidence.md) and its detached
[13-file manifest](layout-console-files.json) remain historical at that exact
implementation commit. Actual staged Git blobs matched every canonical entry
before commit; the manifest's raw SHA-256 remains
`171A07ECE8F448375EB3D221C6DF49779EF26C187993F92C4F52C95D17D539E3`.
This later ledger/record addition does not rewrite that manifest.

## Exact hosted outcomes

Both runs' own records were read as `completed` / `success` for the exact head,
not inferred from a watcher's exit code. Both were created at
`2026-09-05T15:32:01Z`:

| Workflow | Exact run | Measured conclusion |
|---|---|---|
| CI | [33975087048](https://github.com/honest-ink-inc/open-classroom-foundry/actions/runs/33975087048) | Success; build-and-test, secret-scan and portable-samples all succeeded |
| CodeQL SAST | [33975087039](https://github.com/honest-ink-inc/open-classroom-foundry/actions/runs/33975087039) | Success; retained SARIF inspected directly |

The [ledger](../evidence/evidence-ledger.json) binds the exact metadata, source,
checkout and receipt to this record; equal timestamps use its ID tie-break.
The hosted test receipt is
`20260905T153519Z-8db06563297046768fc89c3cfe351cea`, from synthetic merge checkout
`7b95f975afe4c4c1f531626d1cd8e6a5dd454543`, not `main`. Its 21,223-byte summary
has SHA-256 `EF6D8017BD6133E2885E9AEDA2F8C91B96ADCA5700CCEED31769FDA56D7BBB42`.

All seven retained TRX files were inspected: **2,442 executed, 2,442 passed,
zero skipped or failed**. Suite counts are Accessibility 26, Contract 175,
InstructionalEvals 336, Integration 320, Rendering 121, UiAutomation 284 and
Unit 1,180. Native and runner exits are 0, elapsed 362289 ms, no timeout,
stable pre/post identities, clean repository states, and empty identity and
evidence-completeness errors. Source stability is not, by itself, a claim that
`--no-build` establishes source-to-binary provenance.

The actual hosted log, rather than echoed command text, reports:

- `40 files compared byte-for-byte.`
- `40 files compared byte-for-byte on Linux.`
- `40 Windows and Linux sample files matched byte-for-byte.`
- `Core + module line coverage: 92.7%`.
- `128 commits scanned.` and `no leaks found`.

The downloaded Windows/Linux inventories were also independently compared
directly: all **40/40 files** match byte-for-byte and all case-exact relative
paths/hashes match the original first-admission manifest. Its SHA-256 remains
`DEF10A3258A2F2ABA922DF8F1BC38FC3A3209065B36F81F44C41B4FE047F4A90`.
The retained `ci.log` SHA-256 is
`77CD3FF03C65EBB65655516DA4079699D07D29B34A5C2CF46E5D5152AD0B9B32`.
The secret scan does not clear historical blind-study exposure.

The hosted ratification receipt is `verified`, preserving original recipe C1
`5cae09dcb40628265d51912aea98304557abfda6`, record-only recipe C2
`94f128cddd5cdbd00a6f7097b470e4defdebaa47`, decision blob
`349b8c10eae3018acc0d80b8d3ce52c0ed5ec74a` and sample-manifest blob
`07ec9f48d13a87826fc5eb784eb985b8eace5065`; both preservation flags are true,
with no failure. Receipt SHA-256:
`B1D8D5FC946993670D89C0FA5EF83DB7AD787E4CCCE0F4EFF4243A1F8E24286B`.
The committed local HEAD verifier also ran twice and produced identical
receipts, SHA-256
`228369AC78FCB1724EB4D6033ED8AA1A13C115F2FD2239E6263221F1E92739A7`.

The uncompressed `csharp.sarif` is SARIF 2.1.0, one analysis run and **zero
results**, counted inside that run. It is 636,237 bytes, SHA-256
`BC785470156A9B03F8A065DF783AEDFD2B0F3A3EDCCC073482119954FE3BEF4E`.
This is not the uploaded ZIP digest. Artifacts remain locally under
`out/layout-console-hosted/33975087048/` and
`out/layout-console-hosted/33975087039/codeql/`; this is not a funded permanent
archive or a physical-print, AT or classroom evidence claim.

## Record-only continuation and remaining scope

The separate [record-only manifest](layout-console-hosted-files.json) binds
this record and the ledger without replacing the implementation manifest.
This continuation changes no executable source. Its own verification must be
reported separately; the preceding hosted runs do not prove a later head.

The record-only Release build, formatting fix, independent formatting
verification and post-format rebuild all exited 0; both builds reported
`Build succeeded.`, `0 Warning(s)` and `0 Error(s)`. Separate logs are under
`out/layout-console-hosted/local-record-close/`.

Two subsequent unfiltered full runs each retained seven current complete TRX
files, **2,442/2,442**, zero skipped/failed, native/runner exit 0, stable source
and assembly identities, no timeout and no identity/completeness errors:

| Local record-only run | Elapsed milliseconds | Summary SHA-256 |
|---|---:|---|
| `20260905T155523Z-2313da7a5007479c9addf01ab19bfb05` | 194915 | `DD21164F16B57DBB385B2F0A25B0E313F17EFFBEB86B60D6AB591A52F24B5527` |
| `20260905T155901Z-26ce67d6c71d4bf38d777e3d0c06df32` | 193569 | `DD539C5269A902A555397E654DAEFC49E4E238BF75247AD490CD4F8732506AF4` |

Both summaries are under `out/ci-test-run/`. They bind the same three-file
dirty record state atop `453e87b80ea0abc3e1a022de6289a0e58aac67e9`, source digest
`FBE0FE7C9D1EF5DD048E9BC55F6DCEE6BE8B91251F852BCB114CBD727ED0190B`,
and identical Release test-assembly inventories. The current paragraph and
detached manifest update only record those completed measurements afterward;
they do not change executable source or mislabel the earlier run identities.

Live ledger measurement said `Measured 73 ledger entries; 0 mismatch(es).`
Receipt:
`out/evidence-ledger-measurement/20260905T155308Z-53db8b0d0ac0463ea70a60179e1d7486.json`,
SHA-256 `1B82DCDA7FEBBA2ACB79266B39B8F3BE50EBEDC126BB9020E675527EB0649661`.

The [current handover](../handover/2026-09-05-layout-and-console-evidence.md)
and [complete work register](accepted-improvement-register.md) remain active.
The new console instrument is still a local draft, not a diagnosis. No actual
council, participant consent, protected-seat review, license choice, ADR-007 or
ADR-010 ratification, site dispatch, publication, release, tagging/versioning,
signing, installation, distribution, filing or correspondence occurred.
