# Access boundaries and retained console evidence

**Measured 5 September 2026.** Engineering continuation on
`codex/accessibility-and-console-boundaries`, starting at
`4970399bc1505b3bff453187891c1b72901c84fc`. This record appends evidence; it does
not replace the earlier implementation or record-only manifests. The complete
[work register](accepted-improvement-register.md) remains active.

**Admission status: tested proposed repairs only.** The later exact-C1 audit
below identifies changed admitted behavior. This branch is not a new admitted
recipe baseline; no merge, unchanged-contract equivalence, or release authority
follows from the tests or default sample matches.

## I20: two existing-path defects, not a new access design

Two fresh runs through the actual Press Room review path each executed four
synthetic controls: two passed and two failed. The full 90-character answer
was still present, but its existing builder warning was absent from Gate B.
Low-ink calibration retained its instruction to darken left to right while
emptying the solid endpoint, leaving no hatch lines in that patch. The exact
failed messages were:

```text
The actual flashcards catalog/review path lost its overflow warning: answer length=90; builder flashcard.overflow=True; review flashcard.overflow=False; review issues=[recipe.warning.1: Verify your printer is at 100 percent scale; fit-to-page changes dimensions.]. The full synthetic answer remains in the reviewed document.
```

```text
The actual calibration review silently emptied its solid density endpoint: low ink=True; final patch Filled=False; final-patch hatch lines=0; blocking issue=False; review issues=[recipe.warning.1: Verify your printer is at 100 percent scale; fit-to-page changes dimensions.]. The unchanged instruction still says: 5. The density ramp must darken evenly left to right; jumps or banding are driver or toner trouble.
```

Both red TRX files, their exact names/stacks, logs and measurements remain at
`out/i20-boundary-evidence/red-a/` and `red-b/`. Their SHA-256 values are
`12C15FDD87AB4BBDB195AB2C1788A5A9E72675006FCB7EADA976BE03B8D945EF`
and `A55D2E8DD5AD5EE7F8DBB308F5BCDC204A76D03A1B8EE9AFB62A7F48547FE877`.
The paired scoped 17-file source inventories matched at
`D50760A7770E7D001767FCC5745AC2CD7BED901702A5E16C4FE8F033BE75F89B`;
the complete 148-file UIA Release-output inventories matched at
`934F410652454DB6A770073364E341F52EC753BD6545FC5619ADD020498BD89A`.
The parent receipt SHA-256 is
`EC51D32479CB3183F397A6F62CA569631AAB6385F145AC6FA4B2A5210958BF6F`.

The candidate repair adds `PressBuildResult` and `BuildForReview`: one builder invocation
returns the exact document and a copied, read-only issue collection. The
document-only catalog entry point remains. Gate B receives the existing
flashcard warning unchanged, with its original severity and acknowledgement
flag; manifest acknowledgements and exact-revision approval are not bypassed.
Low-ink still thins strokes and outlines filled circles, but preserves filled
rectangles so the density endpoint remains meaningful. Geometry, wording,
artifact language, recipe declarations, schema and version identities do not
change.

The focused repaired UIA run passed **5/5**, including warning retention after
editing and re-review. Its TRX SHA-256 is
`CDA686E0248C81A59DCE194349FDDE94B879155CC41CD943737276BF24FE043B`.
The focused Unit run passed **63/63**, TRX SHA-256
`1664C5935F654681BF6228B46E8D72B6ECDC68697EE80C2E71047952047C6C81`.
Its 56 default-catalog comparisons exercise two current entry points sharing
the builder; they are not historical byte-compatibility evidence.

An earlier bounded read-only code audit found no concrete implementation defect
in current catalog paths; it did not compare the C1 admission contract. The
later exact-C1 audit below found an admission blocker. A future builder returning a Blocking issue would need a
separate validator/error-path disposition; no current builder does so.
This repair does not resolve the 40-character overflow heuristic, vector
text-scale behavior, physical print, real screen-reader use, or I20's full
H1/H2/H3/H4 acceptance.

### Rendered calibration: endpoint repaired, overall visual QA red

The isolated rendering harness subsequently generated ordinary, repaired
low-ink and reconstructed-old-expression calibration pages for both Letter and
A4 portrait at 100% text scale. All 34 generated files matched their second
derivation by direct byte comparison, as did all six paired Poppler page PNGs.
All 33 native stages exited 0. The six landscape cases in each derivation
retained the existing explicit portrait-only refusal; no landscape page was
generated. The old-expression reference applies the retained earlier transform
expression using the current catalog and renderer: it is **not** execution of
a historical binary.

Artifacts and separate stage streams remain under
`out/i20-render-proof/runs/first-20260905/`. The immutable generation receipt
SHA-256 is `5E53BEE9AFBF525F501AB35F217BC4B1BF9E37DDF289628337472F499088A65E`;
its retained-files manifest SHA-256 is
`5703C45378749DA93ECE3BBEA114BEF270B756877071CC18ACCC4D27B91D497B`.
That receipt correctly records visual review as pending at generation time;
this later audit does not rewrite it. Source/reference/output inventories were
stable, but simultaneous source and binary hashes alone are not a compiler
provenance proof. The prior PDF-operation marker was acknowledged from the
main tool-call observation; no retained marker log or independent harness
validation is claimed.

Main inspected all six distinct rendered pages; each second-copy PNG has
identical bytes. The repaired low-ink page has a solid final ramp patch, while
the reconstructed old expression leaves it hollow. However, instructions 4
and 5 run beyond the right edge on **both ordinary and low-ink pages**, on
both paper sizes. Overall visual QA is therefore **FAILED**, not print-ready.
Each instruction has 99 characters at x=22 mm, size 3.5 mm, start anchored.
The native PDF writer's fixed Courier advance is 0.6 em, placing the endpoint
at `22 + 99 * 3.5 * 0.6 = 229.9 mm`: 19.9 mm past A4's 210 mm edge and 14.0 mm
past Letter's 215.9 mm edge. This is a measured existing layout defect, not
evidence that preserving the rectangle fill caused clipping.

| Inspected first-copy PNG | SHA-256 |
|---|---|
| A4 ordinary | `DBD353D18F7345DB64ABABF97DC9824A1294090F7C15F8FFC9F9C4F529717A0D` |
| A4 reconstructed old expression | `DAD0675275E98FA43BA7BDC3B831E0502140EABDD800C3CAD1CEFFF5DE03434F` |
| A4 repaired low-ink | `2EBD4F3FB6101EEF4576147E2FAF47A92C23FF92224971F27B84D90A8C2A06F8` |
| Letter ordinary | `E4ED98A74C7B505126E23C6FD1059D5C68D1BE30EAAF6545F64C97BA84BAB708` |
| Letter reconstructed old expression | `7790859B59D4C0D0BD5979B3CF2FA058569670557D8A3D49F6662F2AE8781DCC` |
| Letter repaired low-ink | `FD80068253971507674F798295A17C612662D836DF583AB91CC55F33AF5FAB25` |

All twelve Poppler PNG stderr files also retain `No display font for 'Symbol'`
and `No display font for 'ArialUnicode'` syntax warnings. Their cause is not
diagnosed here. HTML bytes were compared, but no browser HTML render or
physical-paper/assistive-technology acceptance was performed.

The ordinary Letter PDF and HTML still match the original first-admission
calibration bytes. Fixing this default layout would change that frozen sample
route. A further exact recipe/layout disposition must precede such a repair;
do not rewrite the original baseline or silently substitute a new layout
under its evidence. `press.calibration@0.1.0`, its schema and the original
sample manifest remain unchanged. The clipping, scaling and overflow-heuristic
work remains open alongside the required human reviews.

### Exact-C1 admission hold

An independent later read-only comparison against the ratified C1
`5cae09dcb40628265d51912aea98304557abfda6` found a concrete compatibility
disposition requirement. C1's `LowInkPress` explicitly set rectangular fills
to false; the candidate preserves them, changing the calibration low-ink
output. C1's flashcard catalog discarded the builder issues and its Press Room
review used the default validator before manifest notices; the candidate
adds the existing overflow issue to Gate B. Thus at least
`press.calibration@0.1.0` output and `press.flashcards@0.1.0` review behavior
change, even though the manifest fields and forty default samples match.
Shared-transform impact must be enumerated rather than assuming these two
rows are the exhaustive set.

The [ratified first-admission decision](../adr/recipe-identity-disposition-packet.md#selected-option-a--explicit-first-admission-freeze)
freezes builder, output, editor, validator, evaluation and renderer behavior,
not merely names or default hashes. It contains no bug-fix or unchanged-default
exception. The proposed fixes may be retained and reviewed on an unmerged
branch, but must not be admitted as unchanged 0.1.0 contracts. Before admission,
an accountable exact disposition must cover every affected identity and any
required candidate engine, recipe, output-schema and evaluation identities,
retained outgoing behavior, selection/refusal/rollback proof, and applicable
project migration route. No semantic-version increment or waiver is inferred.

This hold also applies to a future clipping, scaling or overflow-policy repair;
fixing an undesirable frozen behavior is still a contract change. Preserve C1,
C2 and their original manifests, and distinguish a safe proposed-code commit
from admission, merge or publication. Earlier scoped code-audit findings above
are not a governing-authority approval.

## I33/I34: retain the exact red hosted head

The later record-only PR17 head
`4970399bc1505b3bff453187891c1b72901c84fc` did **not** repeat its predecessor's
hosted green. Both own run records were read directly, with attempt 1 and
creation time `2026-09-05T16:13:36Z`:

| Workflow | Exact run | Own conclusion |
|---|---|---|
| CI | [33977198151](https://github.com/honest-ink-inc/open-classroom-foundry/actions/runs/33977198151) | Completed / failure |
| CodeQL SAST | [33977198085](https://github.com/honest-ink-inc/open-classroom-foundry/actions/runs/33977198085) | Completed / success |

The [ledger](../evidence/evidence-ledger.json) appends both rows. CI's secret
scan passed; build-and-test failed; portable-samples was skipped. Receipt
`20260905T161637Z-4d2d59b2c32947769c7767959f5a0f5c` binds clean synthetic merge
checkout `b202612cd2b159f90600bb5b4c64b4e6eb7fd072`, not `main`. The 23,379-byte
summary SHA-256 is
`40C074618EE04384D3AB6301D53EC64818D6CFA72B6A14664C2F757C50D90981`.

All seven retained TRXs are complete: **2,438/2,442 passed, four failed**, zero
skipped, error or aborted. Accessibility is 26/26, Contract 175/175,
InstructionalEvals 336/336, Integration 318/320, Rendering 121/121,
UiAutomation 283/284 and Unit 1,179/1,180. Native and runner exits are 1,
elapsed 319650 ms against the unchanged 900-second outer cap, without an outer
timeout. Pre/post source and assembly identities are stable; both source
states are clean. Identity errors are empty. The **three** completeness-error
entries identify coherent complete failed suites, not missing/malformed TRXs
or a snapshot failure. Parent exit and output drain were observed, and the
receipt permits another runner.

The four exact failures and messages are retained:

1. `Foundry.Tests.Integration.KioskPortTests.The_raster_core_prints_a_real_pdf_to_file_when_the_inbox_printer_exists`
   took 106.9104569 seconds and failed at `ExportAsync`, before raster/printing:

   ```text
   System.InvalidOperationException : Edge did not produce a complete PDF within 90 seconds. The Edge launcher was still running.
   ---- System.TimeoutException : The PDF did not become complete before the export timeout.
   ```

2. `Foundry.Tests.Integration.EdgePdfExporterTests.Two_exports_complete_concurrently_with_isolated_edge_profiles`
   took 96.4902196 seconds with the same exact outer and inner messages. The
   intervals overlap; this does not establish a common cause or printer fault.

3. `Foundry.Tests.UiAutomation.HeadedUiaWalkTests.Part3_Steps9to12_move_edit_and_approve_operate_through_uia_patterns`
   took 72.6121737 seconds:

   ```text
   System.TimeoutException : Timed out after 70559 ms and 1 probes waiting for the top-level window for harness mode 'review' (process 12864). Content-free diagnostic snapshot: last transition=process launched; process=running; expected=ControlType.Window; candidates=not measured; matches=0.
   ```

   This is the first recorded hosted recurrence of S-01's element-wait class,
   not the distinct S-10/S-11 messages or the missing-TRX S-08/S-09 signatures.
   One synchronous probe's elapsed time cannot distinguish a blocking query
   from scheduling delay.

4. `Foundry.Tests.Unit.CiTestRunnerContractTests.Evidence_snapshot_rejects_malformed_xml_but_retains_a_coherent_failed_trx_as_red`
   took 30.5951791 seconds:

   ```text
   The PowerShell evidence-fixture process exceeded 30 seconds.
   ```

   The stack reaches `RunPowerShell`, `RunEvidenceScenario`, then the test.
   No inner fixture stdout/stderr survived that assertion. Independent source
   findings about cleanup and stream waits do not explain this hosted timeout.

The failing Integration, UiAutomation and Unit TRX hashes respectively are
`4DFDFC0104E03D2597A303764A18DA5E5FED3D8352A93CCA5C31A9104C2A3DA4`,
`5BFD18FD7ADE74E41B4CBE503E67169C8055AEBCB22E42D37FE54A2A613A2CB2`
and `62823FF502F1FF777E54C93B4554209A41FAE8AF85B05C5A37E59F5D4F519F4D`.
Raw and curated copies matched byte-for-byte. Full logs, stacks, all other
TRXs and receipts remain under `out/layout-console-hosted/33977198151/`.
The retained UTF-8 CI log SHA-256 is
`BEB85E83CC99517ACAEFFC392B67D1678AA30A152E21E0CC569968793427A649`.
It reports 129 commits scanned and no leaks. Determinism, cross-platform
samples, coverage threshold, dependency/vulnerability and SBOM gates were
skipped after the failure; none is inferred green.

The paired uncompressed CodeQL `csharp.sarif` is SARIF 2.1.0 with one run and
zero results, 636,237 bytes, SHA-256
`00C82D4C26F30F380E52375A526FE8B298DF77F63F0B6C748FBC9A8BBA89A77D`.
It remains under `out/layout-console-hosted/33977198085/codeql-sarif/`.
This is a file digest, not the uploaded artifact digest.

### Isolated local non-reproductions

Four later serial, one-test local runs each passed 1/1 with native exit 0.
These used the available local Release outputs, with the Unit/UIA outputs
already rebuilt for I20. They are **not** the hosted machine/build or a
source-to-binary provenance proof; no paired pre/post identity snapshots were
collected for these counterchecks.

| Case | TRX test duration (seconds) | TRX SHA-256 |
|---|---:|---|
| Unit fixture | 2.0881127 | `D790DCCFA70D78DAE455FC3B69DFA8C8ADF5C4230835B4D6073F3DFD0C7FACBE` |
| Concurrent Edge export | 1.0860555 | `F60AB06954D6A2D46677753EF3B1F86C39F8280EC352D5724FF3B5A8EBCE24B7` |
| Kiosk export/raster test | 2.1399711 | `915A04DC3CF1CBD89AEB38E573291A7214D53F2A4DD85267BE41827983373699` |
| Part3 | 1.1158310 | `FFDD18E50B92EE29AA39E553DB9CE66134BFD18E36F0BB76642BD0FEF04FDCA9` |

TRXs and unfiltered logs are in
`out/layout-console-hosted/4970399-local-counterchecks/`. Local Edge
152.0.4191.66 and the Microsoft Print to PDF printer were present. This is
not physical-paper evidence. S-01 remains open; S-12 and S-13 retain the new
export and fixture signatures in the [register](../evidence/sightings-register.md).
No deadline was raised and no passing replay closes or diagnoses a row.

## Console instrument admission remains separate

The local v2 pure-control run
`20260905T164841Z-95ccd8542b2e4957896285773154d63c` executed 30 controls and
correctly reported **red**, exit 1: six invalid controller receipts were
admitted (duplicate root and cleanup fields, replay, string setting error,
Boolean setting error and string helper exit). The other 24 expected
accept/reject outcomes matched; no test child or console experiment ran.
Receipt SHA-256:
`BF6C104D106FB7AB768D7BC40383631ED15E5826590D68D4018C34E1B50D11CD`.

All v2 instrument, fixture and receipt bytes remain in
`out/layout-console-investigation/v2/`. A separately named v3 draft may repair
these trust-boundary defects; it cannot rewrite the failed instrument's
evidence. Pure fixture tests alone do not admit a native shell/console matrix,
prove cleanup/marker behavior, or diagnose N-02. No console/product behavior
or existing test deadline changed in this continuation.

### Later v3 process-free measurement

After main read all six final v3 files and independently parsed the four
PowerShell files with zero parse errors, one fresh PowerShell process ran the
scoped pure controls. Receipt
`out/layout-console-investigation/v3/pure-controls-20260905T173935Z-be2fc9ef6aa9495b806df5517af9fc5f/pure-controls-receipt.json`
has SHA-256 `A1D08DD19E3DF659546DDA180EBC5EE66E20DC4B5D39FE25E25620A620699CA6`.
All **45/45** expected acceptance/rejection outcomes matched, with zero defects,
zero unproven fixtures, no fatal code and native exit 0. Before/after input
inventories match. In particular, all six receipt shapes improperly admitted
by v2 were rejected. v2's red evidence remains unchanged.

The receipt's verdict is deliberately `PURE_CONTROLS_PASS_ONLY`, with
`PreMatrixGateComplete=false`, `NativeOutcome=NOT_RUN` and
`N02Diagnosis=NOT_ESTABLISHED`. These synthetic checks do not exercise a full
identity collector, real shell startup/topology, native console inheritance,
process/pipe cleanup or durable trial-write failure. The harness contains no
child-launch calls; OS process creation was not independently measured.
Separate stdout/stderr remain under `out/accessibility-console-close/`.
No native console matrix is admitted by this result.
Independent read-only audit confirmed all 45 case records, all nine input
identities and all 68 v2 regular files unchanged. Four negative controls retain
only `non-context-exception`; their precise exception causes cannot be recovered
from those receipts. This limitation is not a claim that their specific intended
cause was independently established.

## Closing evidence and authority limits

Release build, formatting fix, independent formatting verification and
post-format Release rebuild all exited 0; both builds reported `Build succeeded.`,
`0 Warning(s)` and `0 Error(s)`. Logs remain under
`out/accessibility-console-close/`.

The first unfiltered full run passed **2,510/2,510**. The stability attempt
retained a complete **2,509/2,510** result, not a green close:

| Local run | Native / runner exit | Elapsed ms | Summary SHA-256 |
|---|---|---:|---|
| `20260905T170955Z-ae2b299e656443a0be5499bdd2b7a4f0` | 0 / 0 | 192553 | `1EF053C9413B8CF364D40360C858BB59D148FFC61EB717378F5FD3C889A527D6` |
| `20260905T171420Z-7b597a71110f48ceb357e1c6c0f72eef` | 1 / 1 | 192312 | `9D0ACBF72EFD07BDD473470D7F7D02D29BE71B6FA7A97F20A6594A9666E3FA1E` |

Both summaries are under `out/ci-test-run/`. Both bind the same 14-file dirty
tracked/nonignored source state atop the starting commit, digest
`B5AB258CD7333DC3373AB26981E50097155BCF4557022CA975D3A44633A55F13`,
and stable pre/post Release test assemblies. All seven TRXs are complete, no
outer timeout, parent exit and output drain observed, and no identity errors.
The first has no completeness errors; the second has one coherent failed Unit
suite, not missing evidence.

The second run's exact failed test was
`Foundry.Tests.Unit.CiSupplyChainContractTests.NuGet_resolution_is_one_source_mapped_and_every_project_has_a_lock`,
at 0.1556080 seconds, with this retained message:

```text
Every project must retain a version-1 packages.lock.json beside the project: out\i20-render-proof\packages.lock.json
```

Unit TRX SHA-256 is
`7576481C15B4FA6ABBDEFAE17800CA0B69EB4426558EF106B2B06324BD4B4BB7`.
The source check enumerates every `.csproj` beneath the repository except
`bin`/`obj`, including ignored output. Main had permitted a parallel agent to
prepare an ignored rendering harness without execution; its new project was
present without a generated lock when the check ran. This is a preparation
interference finding, not an unexplained under-load failure or a product
regression. The source identity collector excludes ignored files, so stable
source/assembly hashes did not establish an unchanged **test-visible** workspace.
Keep all test-visible preparation stopped during subsequent full runs. No
product test, scheduling policy or deadline is weakened to accommodate it.

All native test streams remain separately retained by the bounded runner.
The first two outer console transcripts merged stdout/stderr; this deviated
from the House Covenant's gate-stream rule. Their own TRXs and separately
retained native streams, not a console filter, establish these outcomes.
Subsequent gate invocations keep the outer streams separate as well.

The live ledger measurement checked **75 entries, 0 mismatches**. Receipt
`out/evidence-ledger-measurement/20260905T171117Z-f4446bacba54412ab89e318974e5f7b4.json`
has SHA-256 `A15F7D65792139D7C27BD442B9AD89122E2AFD2E8C4996B5F814C06E25D6DF61`.
Rendered calibration checks, sample comparisons and the complete unchanged-input
closing runs remained under way at that paragraph's initial cutoff. Their later
results are recorded separately below. No earlier green is assigned to a later
changed tree.

### Later supplied licensing proposal choice

After the initial full-run source snapshot, the typist expressly selected
**CC BY-SA 4.0** as the educational-content proposal. The [separate exact
question/answer record](2026-09-05-content-license-selection.md) preserves the
stated copyleft reason and the remaining scope, ownership, notices and assent
requirements. No operative grant or retrospective/future material coverage is
inferred. Earlier no-choice statements retain their earlier cutoff; current
navigation now records this supplied choice.

### Later frozen-input engineering close

After the factual license selection and rendering/console records were added,
the ordered Release build, formatting fix, independent formatting verification
and post-format Release rebuild again exited 0. Both builds reported zero
warnings and zero errors. Hooks were confirmed enabled with gitleaks present.
All outer stdout/stderr streams were retained separately under
`out/accessibility-console-close/final-*`. All test-visible preparation,
including ignored harness inputs, remained frozen through both full runs.

| Full solution run | Passed / total | Native / runner exit | Elapsed ms | Summary SHA-256 |
|---|---:|---|---:|---|
| `20260905T174448Z-0b14503c895f446d8cbe464be18bfc7f` | 2510 / 2510 | 0 / 0 | 196272 | `9EF636008AF5E9F327C58E81C13D6972D6BC0F6D7C5C075F34BC4FA37E8BB2D1` |
| `20260905T174928Z-11de00a877f74a6e803b5c36622bdcf1` | 2510 / 2510 | 0 / 0 | 194358 | `DE7A8CEB641F2465BFE4949863E683EB7A8409D6A28E7A6370EFABD1BBBFCE2A` |

Main read both own summaries and all fourteen actual TRXs. Each run has
Accessibility 26, Contract 175, InstructionalEvals 336, Integration 320,
Rendering 121, UiAutomation 289 and Unit 1243, all passed; no skipped, error,
aborted or non-passing result. The unchanged 900-second cap was not reached.
Both report stable pre/post source and assemblies, empty identity and
completeness errors, no snapshot error, observed parent exit and completed
output drain, and permission to start another runner. Both bind the same
18-entry dirty state atop `4970399bc1505b3bff453187891c1b72901c84fc`, with
531 tracked/nonignored source files and source digest
`37180715EEB12B47BC8174E9025FD2E95A27CF498288E264F3D9BCAEB85482EF`.
These runs precede this result transcription and the later explicit admission
hold wording; they do not claim an identical later documentation tree.

Two fresh SampleGenerator invocations then exited 0. Every one of the 40 output
pairs matched by complete-byte comparison independently of hash comparison;
two independent inventory reads per directory also matched paths, lengths and
hashes. All forty matched the original first-admission manifest, whose SHA-256
remains `DEF10A3258A2F2ABA922DF8F1BC38FC3A3209065B36F81F44C41B4FE047F4A90`.
The outputs and separate streams remain under
`out/accessibility-console-close/samples-final-a` and `samples-final-b`.
Generated `sample-byte-proof.json` in that parent directory has SHA-256
`95BC2D0912D84A523F7A5D9BD0E5B993DBA3195AC5E2E296D43AAEF43852B395`.
This proves default sample compatibility only, not complete behavioral
equivalence or the missing admission disposition. Visual calibration QA stays
red, and the earlier interference/hosted failures remain retained.

The subsequent record-only checks initially passed 29/30, native exit 1:
`AcceptedImprovementScopeTests.Work_register_retains_every_proposal_and_the_full_acceptance_envelope`
rejected main's new status text `ACTIVE — ADMISSION HELD` because its permitted
states are exactly `OPEN`, `ACTIVE`, `HELD`, `VERIFIED`. The exact assertion,
full offending row and stack remain in
`out/accessibility-console-close/record-validation/record-validation.trx`,
SHA-256 `C7E1C329B38FEACFE98B33F90C4B6A3645CE224FD76AE85D58BDB79BC2EA6C27`.
The row was corrected to `ACTIVE`, retaining the explicit admission hold in its
description; no test or permitted state was changed. A separate run then passed
30/30 with native exit 0, no non-passing result. Its retained
`record-validation-b/record-validation-b.trx` SHA-256 is
`908B61DA32A32E1F9C085CF7DDFABBEBA6E103D9A2D332E07017187010154C2C`.
This is a corrected documentation-format defect, not a load sighting or a
replacement for either full-run record. The final detached-manifest audit and
exact proposed-code commit remain separately scoped.

The history guard also ran twice with `-RequireRatified`, both native exits 0.
Its complete receipts at `out/accessibility-console-close/history-a.json` and
`history-b.json` match directly byte-for-byte, SHA-256
`07821229A6303E7C857D02DBF387175C79612ED912F6F8D44565C219D0D6F4A7`.
They verify original C1/C2 ancestry, the exact six-file record-only transition,
and preserved decision/sample blobs at starting HEAD. This guard proves that
historical relationship, **not** equivalence of later changed recipe behavior;
the separate admission hold remains.

The [detached changed-file manifest](accessibility-console-files.json) binds
the exact candidate commit's changed source and factual records using two
independent strict-UTF-8 reads with CRLF normalized to LF. It excludes itself;
ignored artifacts have the separate receipts and limits above. Retaining the
proposal in Git does not admit its changed recipe behavior.

No human council, participant consent, protected-seat acceptance, operative
content-license grant, ADR-007/ADR-010 ratification, site dispatch, publication, release,
version/tag, signing, installation, distribution, filing or correspondence
occurred. No recipe admission or merge occurred. All real H0–H7 records remain **NOT BEGUN**. Synthetic code and
rendered tests do not supply those records or complete the full goal.
