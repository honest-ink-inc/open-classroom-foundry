# Layout and console evidence continuation

**5 September 2026. Engineering continuation from
`bb788aa8d74fc3e230b6712876a6ea6c3ccce9d5`; not a human review or release.**

The [full accepted-improvement register](accepted-improvement-register.md)
continues to govern all 40 proposals and 227 atlas dispositions. This record
does not reduce that objective to the two investigations below. The
[current handover](../handover/2026-09-05-layout-and-console-evidence.md)
and every H0–H7, licensing, consent, authority, schema and release hold remain
applicable. No operating term, participant record, recipe identity, schema or
version is ratified here.

## I25: measured layout failure, not a physical-printer finding

The original source-derived suspicion now has a retained actual-render
reproduction. `out/i25-layout-evidence/red-measured/i25-red.trx` records
**9 executed, 7 passed, 2 failed**, SHA-256
`659ECF86BA74DCA8EAD374EAE31AF8704F5297419EF1DFA9AB448C7CAC944D91`.
Six-prompt Letter/A4 landscape goal sheets put the third rule respectively
3.833333/4.816667 mm inside the next prompt's nominal 5 mm font envelope.
Actual SVG coordinates agree within decimal serialization rounding. The
retained PDF rasterizations visibly place rules through prompts 2–6.

The portrait/default and four portfolio controls passed. Each of the nine
PDFs and HTML files was generated twice and compared byte-for-byte before the
geometry assertions. Normal stdout/stderr, exact failure names/messages and
nine PDF/HTML/geometry-JSON triplets are retained in that local evidence root.
The two original diagnostic PNGs were generated once; Poppler emitted missing
display-font warnings for Symbol/ArialUnicode. This is not universal glyph
coverage, AT evidence, physical-print validation or a green repair proof.
Preliminary CA1869/CA1859 test-authoring failures remain separately retained
under `out/i25-layout-evidence/red/`; they are not product failures.

### Bounded repair and regression proof

**Implemented and measured 5 September 2026:** overflowing Goal Post prompt
groups paginate without shrinking the 5 mm labels, deleting any of the three
9 mm-spaced writing rules, changing wording/order, or losing the pledge on any
page. Six prompts on either landscape size become five plus one. The existing
learner-held recipe, document node list, HTML and PDF routes already admit
multiple vector pages; no new recipe, schema or version identity is introduced.
Already-fitting layouts still use the same page builder and arithmetic.

A first nominal-geometry repair left a second, measured defect: with Letter
six-prompt margins of 32.249 and 32.2499 mm, positive model-space gaps of
0.000333 and 0.000033 mm serialized to touching SVG ink. The retained
`rounding-red/i25-rounding-red.trx` has **35 executed, 33 passed, 2 failed**,
SHA-256 `8FBD2077A9490BC940AAF045902165F8FC45DE697B2CC58976B3228299DA5D84`.
Its failures are
`LearnerHeldKitLayoutTests.Goal_pagination_handles_just_fitting_touching_and_overflowing_ink`;
one exact message is:

> goal-margin-32.249 page 1, prompts 3->4: rule-to-next-label gap 0.000333 mm; rendered SVG gap -0 mm.

The final fit decision checks actual invariant-culture `0.###` coordinates as
decimals, matching SVG serialization. Touching is refused; the positive-gap
assertion is not relaxed and no arbitrary padding is added. The 32.247 mm
control still fits on one page. Non-finite, negative and zero-capacity margins
refuse before chunking.

The final focused set covers all 20 standard size/count combinations, six
near-boundary margins, six invalid margins and four portfolio controls. Actual
approval, document validation, PDF/HTML rendering, page counts, prompt order,
pledges, fixed rule/font sizes and serialized positive gaps are exercised.

| Local run under `out/i25-layout-evidence/` | Measured result | TRX SHA-256 |
|---|---|---|
| `resumed-red-measured/i25-red.trx` | 24 executed; 22 passed; the same two landscape failures | `B479EB698CE1F357390DC0276D3017D64DC4FAEE698A81515848A66191C44BFB` |
| `green-final/i25-green-final.trx` | 36/36 passed; zero skipped | `2AC4E278EDD481B0590CE25B29FB79E6BC6287D7811C521E29D7691D2FF7123C` |
| `stability/i25-stability.trx` | 36/36 passed; zero skipped | `3CF3A2E7B5A3A22ECECFE5CCB39BAE279EB128D41868EF2696FCA1802C4423E0` |
| `compatibility-final/i25-compatibility-final.trx` | 66/66 passed; zero skipped | `98CAC837A27B11616B3B4F7F7E264152BC534A82C97EC838D48F359A2401CD26` |

Every previously fitting PDF/HTML file from the 24-case red run remains
byte-identical: **44/44 files**. The final and stability outputs match exactly:
**60/60 PDF/HTML files**, and the two Poppler passes match **48/48 PNGs**.
The main agent inspected both pages of both repaired six-prompt landscape
PDFs: the original rule/label crossings are absent. This is scoped ASCII
synthetic visual QA, not long-text/glyph coverage, physical-print or AT proof.
The Symbol/ArialUnicode display-font warnings remain disclosed.

Two independent executions of the retained local verifier produced identical
33,706-byte receipts at `out/layout-console-close/i25-verification-a.json`
and `i25-verification-b.json`, SHA-256
`9849A1E07E534B2B53A5D7DC466ED68F410DD8D4C6B99288E85FDA22AB86A96F`.
They bind the focused TRX files, failures, output hashes, source hashes and
unchanged original pause checkpoint. Direct byte comparisons separately
confirmed the output equality, rather than treating equal hashes alone as a
byte-comparison command. The independent read-only engineering audit found
no additional defect in the final precision/pagination source; it did not
claim council, source-to-binary or physical-printer approval.

## I33: correcting the scope of retained evidence

Direct reads of the two later S-08 summary files distinguish their actual
configured caps; an unchanged default is not proof that every invocation used
that default:

| Receipt | Configured cap | Elapsed milliseconds | Result | Summary SHA-256 |
|---|---:|---:|---|---|
| `20260904T035103Z-906577fa2b024bd9859cdc1e00936a7f` | 900 seconds | 900220 | Timed out; exit 124 | `6A7DFD696B076E41F4BBEDDFD4E690B6E6996CEAD442C09E98FFF7FE4881FCCC` |
| `20260904T081350Z-c5320441d5ba40f0bef1fdae213c54ea` | 1,800 seconds | 1800227 | Timed out; exit 124 | `E1A972EB9D7D32CC6711AD95D8D3542500BA6113E80315916AD0A7C17818C08C` |

These are historical measurements, not permission to raise a timeout now.
Neither cap produced a diagnosis. The current runner's default remains 900
seconds; it does not yet retain timed live descendant/thread snapshots before
termination.

The [31 August Board-to-Brief record](../handover/2026-08-30-kiosk-plan-evaluation.md#live-board-to-brief-stall-diagnosis-and-async-boundary-repair--31-august-2026)
does describe a separately captured live testhost dump and a bounded duplicate
OCR-fault diagnosis and repair. It explicitly does not retroactively diagnose
earlier undumped same-process stalls. The dump and temporary diagnostic tool
were deleted, according to that record; no reinspection of those missing bytes
is claimed here. The [sightings register](../evidence/sightings-register.md)
therefore narrows its former blanket "no run" assertion while leaving S-08 and
S-09 open. Passing reruns are still non-reproductions.

N-02's parent-context difference remains undiagnosed. An inherited Windows
ignore-Ctrl+C attribute is a hypothesis to measure, not an observed explanation
of the historical shell state. [Microsoft documents the inherited attribute](https://learn.microsoft.com/en-us/windows/console/setconsolectrlhandler)
and [why successful event generation need not invoke an ignoring process's handler](https://learn.microsoft.com/en-us/windows/console/generateconsolectrlevent).
No sender, host or test deadline changed as part of these factual corrections.

## Prior pushed head: exact hosted records added to the ledger

The [evidence ledger](../evidence/evidence-ledger.json) now carries
`ci-33969243572` and `codeql-33969243592` for the prior record-only head
`bb788aa8d74fc3e230b6712876a6ea6c3ccce9d5`. Both runs' own records were re-read
as completed/success. The retained clean synthetic-merge CI summary directly
measured seven suites, **2,405/2,405**, exit 0, stable source/assembly identity,
no timeout and no completeness/identity errors. Its SHA-256 is
`99134EE6CC8DA2329E465C7BE9A576C5DEF8B11F57A06FCB6DBD9DE5A39CB925`.
The paired retained SARIF is version 2.1.0, one run, zero results; SHA-256
`69477839E22AC092795C790DA166877CC995411F53AD48EF8C8BF063F526BF95`.

Those conclusions prove their exact historical source/evaluation scope. They
are not proof of this later working tree, a main merge, a human review or
publication. The 71-entry ledger was measured with zero mismatches; receipt
`out/evidence-ledger-measurement/20260905T143043Z-9e578eb114914fc7800a410b1a268c9d.json`,
SHA-256 `6CDD33D20DB56E85FB97235EFED69D581F84387B885387BA191C9F93A257F825`.

## Full local closing evidence

The hook installer, exact SDK check (`10.0.302`), both locked restores, local
tool restore, Release warnings-as-errors build, formatting fix, formatting
verification and post-format Release rebuild all exited 0. Both builds said
`Build succeeded.`, `0 Warning(s)` and `0 Error(s)`. Separate stdout/stderr are
retained under `out/layout-console-close/local-build-20260905T1520Z/` (the
directory label is not a claimed command-start timestamp). Release-build
stdout SHA-256 is
`D4BD21B935F743B75E15D7701CB99BA0F91D16500D26B2A9CDB57C04CBD8E116`;
post-format rebuild stdout is
`58976EE98CA76D6F3ADACAB5834C28C87E82BD6AF65CB45CADA747A30A62F3D5`.
Both formatting stdout and stderr files are empty; their zero exit codes were
read from the completed processes, not inferred from empty logs.

The unmodified full runner then executed twice on the same pre-record-update
working tree. Each has seven current, complete TRX files, **2,442 executed,
2,442 passed, zero failed or skipped**, native/runner exit 0, no timeout,
no identity/completeness errors, and stable pre/post source and Release
assembly inventories. Counts are Accessibility 26, Contract 175,
InstructionalEvals 336, Integration 320, Rendering 121, UiAutomation 284 and
Unit 1,180.

| Run under `out/ci-test-run/` | Elapsed milliseconds | Summary SHA-256 |
|---|---:|---|
| `20260905T150442Z-14e1afd185a842cca6552f73ee4e7c5e` | 209892 | `6637FA285C9DBD147841CF6A291BF78AF046AA66DB3FC8BC5AE9E4688B4B80D4` |
| `20260905T151023Z-686714879efb45cda1c2555ce1b52e1c` | 255182 | `19CC62C3888ACBE060D9172706DEE7C531974668FE46F91407889132E4C5EC88` |

Both retain commit `bb788aa8d74fc3e230b6712876a6ea6c3ccce9d5` plus the same
six-file dirty state and 522-file source digest
`5BBB20E67B2B3F67C3FDAEB4E89C8009612D28AC30D1FF4EC386DF5B2C1D6047`.
The configured process limit stayed 900 seconds. `--no-build` does not itself
prove source-to-binary provenance; the fresh build sequence and source-bound
audits are separate evidence. Normal complete streams and all failure
names/messages survive in the retained receipts. These passes do not diagnose
any prior undumped stall.

After the full runs, the fresh SampleGenerator ran twice into
`out/layout-console-close/verified-samples-a/` and `verified-samples-b/`, both
exit 0. Complete recursive inventories and direct bytes match **40/40**, with
all 40 hashes matching the unchanged first-admission manifest SHA-256
`DEF10A3258A2F2ABA922DF8F1BC38FC3A3209065B36F81F44C41B4FE047F4A90`.
A preceding shell argument-array mistake returned usage/exit 1, before any
sample output; its `final-samples-a.stdout.log` and stderr are retained. It
was an orchestration failure, not a product regression or a hidden passing run.

The original recipe C1/C2 ancestry verifier also passed twice with identical
2,048-byte `out/layout-console-close/history-a.json` and `history-b.json`,
SHA-256 `90F43DCCD7D77CCB9FE0046FCC7529740F85F7B4BC1A95D92AB9CAD8B41B2E4D`.
No historical evidence manifest was regenerated. The new detached
[file manifest](layout-console-files.json) binds this change's own files.
Final record/navigation edits postdate the two full runs; their focused
documentation/hygiene validation is separate from those full-run identities.
Exact later hosted conclusions belong to the ledger, not a forecast here.

The post-record Unit run subsequently said `Test Run Successful.`,
`Total tests: 1180`, `Passed: 1180`, native exit 0; all 1,180 executed with
zero skipped or failed. TRX:
`out/layout-console-close/record-validation/record-validation.trx`, SHA-256
`6DBB4E03FF1F93DD578E7ECDBA82E60826F5C13D5EA0EABF35C0A49F08A9E92F`.
It includes the current-state navigation, scope, ledger, sightings and hygiene
guards. A separate 32-link file-destination check passed; it does not assert
Markdown-fragment semantics. This receipt paragraph and its detached manifest
are the final record-only additions, not another unmeasured product change.
