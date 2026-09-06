# Accepted-improvement execution evidence

**Local engineering close, 5 September 2026 — not a release close.**

Scope is the complete [implementation register](accepted-improvement-register.md).
The initial source is `554ec87b256c5cbd8f6efba453070f27941c9257`; implementation
uses `codex/accepted-improvement-implementation`. No real council or protected
review, filing, signing, installation, distribution or deployment is asserted.

## Bounded repairs and retained scope

These repairs enforce existing contracts, with no recipe/schema/version or
first-admission baseline replacement:

- **I18:** pre-cancelled Green printing does not render or open PDF input; Amber
  remains refused before cancellation. Three save surfaces retain exact approval,
  offer localized recovery on expected I/O/access/cancellation failures, allow a
  real retry, and do not swallow programming errors or bypass lane authorization.
- **I14:** chart ceiling/axis arithmetic uses Int64 across the admitted Int32
  input range. A zero category keeps its label/value without invalid zero-area
  rectangle geometry. Positive/default sample output must remain unchanged.
- **I15/I06:** transcript callers receive a live read-only token view, not the
  mutable list; explicit resolution remains necessary. Lesson generation and
  review use wide time sums and refuse absent required learning evidence.
- **I36:** HEAD, stage-zero index and working bytes must preserve the original
  C2 decision and C1 sample manifest. Packet normalization permits only strict
  UTF-8 literal CRLF-to-LF; samples are raw. Mutable attributes, clean filters,
  `assume-unchanged`, later commits and same-file appendices cannot rebaseline
  evidence. A future superseding disposition needs a separate deliberate change.
- **I01/I02/I31/I34/I35/I40:** ownership/use and sustainability inventories,
  decision navigation, the pinned source-build guide, retained derivative-lane
  errata and historical hosted-ledger completion. No license alternative,
  participant assent, seat, funding amount or service promise is invented.

All 40 proposal outcomes, 227 atlas dispositions and the full release envelope
remain in the register. These are partial implementations, not blanket closure.

## Measured red-to-green checks

All test content is synthetic. Failed attempts preceded the repairs and remain
failures in the record. Retained TRX paths below are local ignored evidence, not
files admitted to the public repository. SHA-256 identifies the exact local
receipt. Chart and transcript/lesson focused runs used console output only;
**no focused TRX was produced for those runs**, and none is claimed.

| Scope | Before repair | After repair / related control |
|---|---|---|
| Print cancellation | 2 failed / 1 passed | 12/12 including sink-authorization controls |
| Save recovery | 12 failed / 9 passed | 21/21, then 153/153 related UI contracts and 21/21 sink/localization Unit contracts |
| First identity guard repair | 12 failed / 7 passed | 19/19 |
| Attributes/filter counterexamples | 3 failed / 0 passed | Complete history scope 22/22, zero failed/skipped |
| Chart boundary (console only) | 7 failed / 2 passed | 83/83 including all catalog defaults and StudioPress validation |
| Transcript/lesson boundary (console only) | 6 failed / 2 passed | 78/78 including existing transcript/catalog/lesson Unit contracts; separate LessonLoom corpus 37/37 |

The parent independently inspected the final code diffs and retained TRX
failure messages. Console-only chart/lesson outcomes were supplied by the
implementing agent with exact names/messages; integrated retained full-suite
proof is a separate requirement below. Initial CA1861 and xUnit2032 test-authoring
compile errors were corrected before the affected tests could run; those are
not product failures or instability sightings.

### Failure signatures preserved

Print failures in `WindowsPdfPrinterCancellationTests`:

- `A_precanceled_raster_print_does_not_open_the_input_pdf`:
  `Assert.ThrowsAny() Failure: Exception type was not compatible`, expected
  `System.OperationCanceledException`, actual `System.IO.FileNotFoundException`;
  `Unable to find the specified file.`
- `A_precanceled_Green_print_does_not_call_the_renderer`: same assertion and
  expected type, actual `System.InvalidOperationException`;
  `Synthetic renderer must not run after cancellation.`

All twelve save failures in `LibrarySaveRecoveryTests` reported
`Assert.Null() Failure: Value is not null`, `Expected: null`:

- `An_expected_save_failure_keeps_exact_approval_and_allows_a_real_retry` for
  each `surfaceKind` of `press`, `studio`, `sequence` and each `failureKind`
  `io`, `access`, `cancel` (nine cases): actual `System.IO.IOException:
  synthetic-private-detail: unavailable destination`,
  `System.UnauthorizedAccessException: synthetic-private-detail: denied destination`,
  or `System.OperationCanceledException: synthetic-private-detail: cancelled save`.
- `A_real_unavailable_library_keeps_the_approval_and_can_be_retried` for each
  of those three surfaces: actual `System.IO.IOException`,
  `Cannot create '[synthetic temporary projects path]' because a file or directory
  with the same name already exists.` The bracketed path is an explicit
  redaction of the local machine path, not a verbatim path claim. Original
  messages and stacks remain in the hash-bound TRX.

History guard failures in `RecipeRatificationHistoryVerifierTests`:

- Later packet replacement/append, sample rebaseline/line-ending rewrite,
  absent original/current sample, dirty staged/unstaged sample and hidden
  packet/sample edits reported `The changed record was accepted.`
- The prior positive receipt-contract test reported
  `System.Collections.Generic.KeyNotFoundException : The given key was not present in the dictionary.`
  The positive checkout-EOL test was refused with
  `The RATIFIED packet must be verified from committed Git history, not working-tree bytes.`
- The three subsequent attributes/clean-filter counterexamples again reported
  `The changed record was accepted.` The final repair refuses all three without
  invoking a configured clean filter to decide protected-file equivalence.

Console-only chart failures in `ChartPressBoundaryTests`:

- `Grid_step_covers_the_entire_admitted_nonnegative_integer_range` for maxima
  `2000000000`, `2147483646`, `2147483647`: `Assert.Equal() Failure: Values differ`,
  expected `500000000`, actual respectively `200000000`, `5`, `2`.
- `A_zero_category_remains_visible_without_an_unapprovable_zero_area_bar` and
  `The_largest_admitted_value_has_a_positive_bounded_axis_and_proportional_bars`,
  each with `horizontal: True` and `False`: `Assert.DoesNotContain() Failure:
  Filter matched in collection`, blocking `doc.vector.rectangle`:
  `A vector rectangle has non-finite coordinates or non-positive geometry.`

Console-only transcript/lesson failures:

- `TranscriptSessionBoundaryTests.A_token_view_cannot_remove_uncertainty_to_make_an_empty_session_complete`
  and `A_token_view_cannot_replace_an_uncertainty_with_an_unreviewed_resolution`:
  `Assert.Throws() Failure: No exception was thrown`, expected
  `typeof(System.NotSupportedException)`.
- `StrandPlanBoundaryTests.Large_phase_minutes_reach_a_timing_refusal_instead_of_overflowing_the_builder`:
  `System.OverflowException : Arithmetic operation resulted in an overflow.`
- `Reviewed_phase_minutes_cannot_wrap_around_to_the_original_available_time`
  and `A_required_learning_evidence_value_cannot_be_replaced_by_its_generated_heading`
  with both empty and whitespace evidence: `Assert.Contains() Failure: Filter
  not matched in collection`. Only warning `recipe.warning.1` remained:
  `Minutes sum exactly and include transitions; the engine checks the arithmetic, not the model.`

### Retained focused receipt hashes

| Local path | SHA-256 |
|---|---|
| `tests/Integration/TestResults/print-cancellation-before.trx` | `C3BA5C4B93C4FCD9B38BFF4E115B7954763B72F95DF7E64EA97DA7C8CCAC16D8` |
| `tests/Integration/TestResults/print-cancellation-after.trx` | `27D9F647787B996BD8123505963DC40DCEB4E32BE126291B3979D2DE30FDF039` |
| `tests/UiAutomation/TestResults/library-save-recovery-before.trx` | `03E5D8E8B6B507C8A469438C21AFEE5AD9593B10120BF371225A05D73D43CADA` |
| `tests/UiAutomation/TestResults/library-save-recovery-after.trx` | `463394C6F4E38217722CD3AE52DE0431312C6A5E1C2459857DF3D02C363F3938` |
| `tests/UiAutomation/TestResults/library-save-related-contracts.trx` | `93540679F0E19DE7F4C8537E8AD49B9DFEA5201E02A70E048364A9F6D8B13E64` |
| `tests/Unit/TestResults/save-recovery-sink-localization.trx` | `DF066635E4B74C65BB38C59CE2E2F6012CA2BF5628C4BEAEC5BEE937069869DA` |
| `out/identity-history-audit/red/identity-history-red.trx` | `56A832D2A049681E4C99931DAA531E010B2C2C8B25460BD69CB29CCBBEB2D4CD` |
| `out/identity-history-audit/filter-red/identity-filter-red.trx` | `5A85E15301686420EE4D8A99B4EFAC24AB44642976532D027AAE91FDB39B7D5C` |
| `out/identity-history-audit/filter-green/identity-filter-green.trx` | `121F600B0A8A984CE4CE0063B09E994A30C796E47F609CC31C41EB70B227AFF0` |

## Integrated closure

The initial Release build passed with `0 Warning(s)` and `0 Error(s)`; format
fix and independent verify both exited 0. The first full run
`20260905T125514Z-4ce813fb221f433ebc962c0e6a1839b2` was **2,402 passed / 3 failed
of 2,405**, exit 1, no timeout. Its retained `summary.json` SHA-256 is
`A1C53E8FF3DC48F65CD2CE643378931A5AB64EFE59F9ABB914CC0A963B8707AC`.
The exact failing names and messages were read from the Unit TRX:

- `SinkContractTests.Compiled_production_friends_expose_only_the_application_review_adapter_as_an_approval_mint_caller`:
  `The inspected Release assembly for Foundry.Application is older than its source; rebuild before auditing approval call sites.`
- `SinkContractTests.Print_internal_export_delegation_has_exactly_one_compiled_caller`:
  `The inspected Release assembly for Foundry.Infrastructure.Windows is older than its source; rebuild before auditing approval call sites.`
- `EvidenceLedgerTests.Entries_are_unique_and_appended_in_measured_time_order`:
  `Assert.Equal() Failure: Collections differ`, position 65; CodeQL created
  `2026-09-05T10:14:57Z` must precede CI created `2026-09-05T10:14:58Z`.

Measured source modification instants were `12:53:58Z` after formatting; the
Application and Windows infrastructure DLLs were still timestamped `12:48:06Z`
and `12:15:32Z`. The corrective action is a real full Release rebuild, not
timestamp manipulation or weakening the audit. The two newly added ledger rows
were reordered without changing their evidence. These are deterministic closing
defects, not under-load/alone instability findings; no timeout was changed.

~~Two retained full-solution passes after correction and deterministic
sample/neutral-catalog comparisons pending.~~ **Closed locally 5 September
2026:** the real full Release rebuild again reported `0 Warning(s)` and
`0 Error(s)`, and independent format verification exited 0. Both complete
solution runs then passed **2,405/2,405** with zero failures, no timeout, stable
source/assembly identity, no identity or completeness errors, and safe-runner
release. The source digest was
`9D93365886877B57672B8023EEA5C912410CF564E18F0210AEBA7F3AB5EA3755`
across the same 516-file source inventory and unchanged source/index state.

| Retained full-run directory under `out/ci-test-run/` | `summary.json` SHA-256 |
|---|---|
| `20260905T130145Z-173b3add74434c4e939da12ea6734bf7` | `AC4B9CEC5992C7B09A9231CBC2178562A5658644E8E5A8D0871BBFA52643BE86` |
| `20260905T130546Z-27851f154fdb4e498f2292639ac58106` | `118F62C2E39489699F1CE7FF96785B5F704FEA94AA7DEC15938949E58969BBF3` |

Each receipt records Accessibility 26, Contract 175, InstructionalEvals 336,
Integration 320, Rendering 85, UI Automation 284, and Unit 1,179, all passed.
Both are dirty-candidate local evidence based on `554ec87…`, not clean hosted
commit evidence. This closing prose and the navigation continuation were added
after those stable runs; no executable source was changed by that record step.

Two fresh SampleGenerator executions under
`out/accepted-improvement-close/samples-a` and `samples-b` gave the verbatim
comparison verdict: `40 files compared byte-for-byte; 40 first-admission baseline
entries matched.` Complete inventories, actual byte sequences and every SHA-256
were compared, with no exclusions. The untouched C1 manifest remains
`DEF10A3258A2F2ABA922DF8F1BC38FC3A3209065B36F81F44C41B4FE047F4A90`.

Two fresh neutral UI exports in the same evidence root matched byte-for-byte:
1,080 ids, 165,577 bytes, SHA-256
`62EFC47E009FC6E581C09C0FB92194E35561636EC1A3847D74B8734BAA6E4D84`,
source digest `42a156dd667d317806f4b66a379391794a7b47b4af2d1582cdeb20a5866e5b8c`.
Both remain `draft` / `und`; the production allowlist remains empty. The
identity verifier's two matching scoped receipts have SHA-256
`F58D048B3ED9AFBCE111DF179999ECF920C862FA52E4D899D420738B1BF38D98`;
the parent independently reran the actual-tree verifier and matched that hash.

The independent read-only staged audit returned a bounded no-findings verdict,
not a human review or a substitute for tests. The installed gitleaks 8.30.1
whole-history scan reported `125 commits scanned` and `no leaks found`. That
local executable's archive provenance was not re-established here; hosted CI's
pinned-archive scan is separate. Deleted study instruments remain a known
historical exposure, not cleared by a credential scan. The per-clone hook was
installed and remains enabled.

The [changed-file manifest](accepted-improvement-files.json) binds canonical
UTF-8/LF source bytes at this local close; its declared exclusions avoid a
circular self-hash. Hosted conclusions for the implementation belong in the
[evidence ledger](../evidence/evidence-ledger.json) and a separately dated record.
No new implementation commit's hosted conclusion is asserted at this cutoff.

## Historical hosted ledger completion

Two previously omitted final-head PR15 rows were independently read and appended
to the [evidence ledger](../evidence/evidence-ledger.json). They bind
`554ec87b256c5cbd8f6efba453070f27941c9257`, **not the new implementation**:
CI `33960134199` and CodeQL `33960133982`, both completed/success. The retained
CI receipt contained 2,346/2,346 passes across seven suites, stable source/build
identity and no errors or timeout. The retained SARIF contained one CodeQL run,
zero results. The live ledger measurement reported verbatim:
`Measured 67 ledger entries; 0 mismatch(es).`

## Explicitly unresolved

The remaining engineering audit found a specific next I25 measurement: six
prompts in `LearnerHeldKit.PromptedLinesPage` on A4 landscape leave only about
one-third millimetre between a ruled line and the following 5 mm label baseline.
This is a source-derived collision candidate, not rendered or physical-print
evidence; reproduce it before choosing a fit/refusal or pagination repair. I20's
semantic scaling does not enlarge every fixed-size vector label. I39's locked
text and teacher-declared source metadata do not authenticate a standards version
or specialist judgment. The inspected I37 authority checks revealed no further
substantiated defect, but cannot authenticate real people or represent every
multi-person recusal. None of these proposals is silently closed.

I33's console-signal and test-host sightings remain undiagnosed. Fresh successful
runs do not close S01–S11 or N02; no timeout was increased and no console-delivery
or live-descendant evidence was invented. New contracts and atlas selections
still need exact identities and the relevant real reviews. ADR-010 and ADR-007
remain Proposed, engine version remains deferred, and schema 1 remains hash-less.
Content-license alternatives remain unchosen; participant consent and withdrawal,
cohort enactment/quorum/recusal/seat authority and all real H0–H7 records remain
unsupplied. Push/publication direction is not any of those missing records. No
site workflow, release, correspondence, filing, signing or installation was run.
