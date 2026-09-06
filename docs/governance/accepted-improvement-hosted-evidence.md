# Accepted-improvement hosted evidence

**Measured 5 September 2026.** This separate record applies to implementation
commit `4e7c6d7bfbd1d78e48646ea73e7a323c9cae5401` on
`codex/accepted-improvement-implementation`, pushed in
[PR16](https://github.com/honest-ink-inc/open-classroom-foundry/pull/16).
It is stacked on advisory PR15; neither pull request was merged by this work.
The original [local close](accepted-improvement-evidence.md) and its
[36-file manifest](accepted-improvement-files.json) remain historical bytes at
that implementation commit. Do not regenerate the manifest to hide this later
evidence-ledger addition. All 36 entries were independently checked against
the committed Git blob bytes and matched. The manifest file's SHA-256 is
`9262D3C7CEFFF7848FE4B4CC73C6D371D44162B91A1D73E3B431FA47EF8A183A`.

## Exact hosted conclusions

Both workflows' own completed/success conclusions were read directly for the
exact implementation head, not inferred from a watcher's exit code:

| Workflow | Run | Conclusion |
|---|---|---|
| CI | [33968475826](https://github.com/honest-ink-inc/open-classroom-foundry/actions/runs/33968475826) | `completed` / `success`; secret-scan, build-and-test and portable-samples all succeeded |
| CodeQL SAST | [33968475822](https://github.com/honest-ink-inc/open-classroom-foundry/actions/runs/33968475822) | `completed` / `success`; the retained SARIF was inspected directly |

Both were created at `2026-09-05T13:17:41Z`. The
[evidence ledger](../evidence/evidence-ledger.json) binds these metadata rows to
this record; equal timestamps are ordered by the ledger's ID tie-break rule.
The live measurement reported `Measured 69 ledger entries; 0 mismatch(es).`
Its local receipt is
`out/evidence-ledger-measurement/20260905T133057Z-2eb46530ccaf444784bb0b63722a0cd7.json`,
SHA-256 `368CA4C10660614D4DF2992ACDD6B542902F859BA591B0A778CEF90532C61A93`.

The retained CI receipt is
`20260905T132034Z-1be970ab748f42bfbf3347c9a7951a97`, from GitHub's synthetic
merge checkout `2b0d2dcc43c972f7785749f30d6db5b7515ac7f1`, not `main`.
It records **2,405/2,405** across seven suites: Accessibility 26, Contract 175,
InstructionalEvals 336, Integration 320, Rendering 85, UI Automation 284 and
Unit 1,179. Every suite has zero failures. Exit and test-process exit are 0;
there is no timeout, source/assembly identity is stable, both repository states
are clean, and identity/completeness errors are empty. Its 21,234-byte
`summary.json` has SHA-256
`55EEE214555C9D0D37A5D401DAA41D3AEF4A53FBF792E354BBFB6BED51B92390`.

Direct hosted log measurements include:

- `40 files compared byte-for-byte.` The original first-admission manifest
  matched SHA-256 `DEF10A3258A2F2ABA922DF8F1BC38FC3A3209065B36F81F44C41B4FE047F4A90`.
- `40 files compared byte-for-byte on Linux.`
- `40 Windows and Linux sample files matched byte-for-byte.`
- `Core + module line coverage: 92.6%`.
- The pinned/checksum-verified secret scan reported `126 commits scanned.`
  and `no leaks found`. This does not clear historical study exposure.

The separate hosted ratification receipt is 2,048 bytes, SHA-256
`C59AACEBBA3605EC668F5F93E05EBC3684685C59EA65B92DED037B2D99121243`.
It verifies original recipe C1 `5cae09dcb40628265d51912aea98304557abfda6`
and record-only recipe C2 `94f128cddd5cdbd00a6f7097b470e4defdebaa47`,
including the unchanged decision blob
`349b8c10eae3018acc0d80b8d3ce52c0ed5ec74a` and raw sample-manifest blob
`07ec9f48d13a87826fc5eb784eb985b8eace5065`. Its outcome is `verified`;
the two preservation flags are true, with no failure code/message.

The uncompressed CodeQL artifact is SARIF 2.1.0, one analysis run and **zero
results**, counted within that run rather than counting the surrounding array.
`csharp.sarif` is 635,785 bytes, SHA-256
`5D1D79094B44A16155EBBD14D24426798858463B869FC599A114894BB70746C2`.
This is a file hash, not the uploaded ZIP artifact digest.

Local copies are retained under
`out/accepted-improvement-hosted/33968475826/` (`tests`, `ratification`) and
`out/accepted-improvement-hosted/33968475822/codeql/`. Their existence is not a
funded permanent archive claim. After the local-closing navigation update,
16/16 focused scope/truth/ledger/hygiene checks also passed; the retained local
`accepted-improvement-final-records.trx` SHA-256 is
`7DDE930BC7D377519F7B816E65C9EABCEEB0C9ABEC839773509AF9A91E57332F`.

The evidence-only follow-up also passed a Release build with zero warnings and
errors, format verification at exit 0, and the 16/16 scope/truth/ledger/hygiene
checks. Its `accepted-improvement-hosted-records.trx` SHA-256 is
`B72E50784DBB836F86CB69E430520E9CBED4AF776580BEAFC07E307A87C7A0CC`.
The separate [record-only file manifest](accepted-improvement-hosted-files.json)
binds this record and the updated ledger without replacing the implementation
manifest. No executable source changed in this follow-up.

## This is not publication or release evidence

These runs verify the exact implementation head, not a future record-only head,
merge, release, installed application, or any actual participant review. Later
heads need their own hosted conclusions. No main merge, site workflow, naming
disposition, correspondence, filing, tag/version, signing, installation or
distribution was performed. The [decision index](decision-index.md), active
[work register](accepted-improvement-register.md), unresolved schema/license
choices, real consent/seat authority and all H0–H7 freezes still govern.
