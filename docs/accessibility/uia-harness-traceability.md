# UIA harness ⇄ walkthrough script traceability

**Basis:** ADR-002 ("standard controls only until the accessibility test harness exists") · Handover 2026-08-29, forge item 3 · Script: [nvda-walkthrough-script.md](nvda-walkthrough-script.md)
**Harness:** `tests/UiAutomation` — two layers. The **in-process** layer asserts what WinForms will hand to assistive technology (accessible names, roles, tab order, mnemonics, the standard-controls rule) as data. The **headed** layer launches the real app in deterministic `--uia-harness review|capture|pressroom|allaboard|modules` fixture modes and reads it back through UI Automation — the same tree NVDA and Narrator consume. Every storage-capable mode additionally requires its exact empty disposable `--project-library-root`; no harness may inherit a teacher library. Headed tests run on any interactive desktop and are **visibly skipped** (never silently passed) where none exists or `OCF_SKIP_HEADED=1`.

**What the harness does NOT claim:** what speech actually *sounds like*, NVDA/Narrator divergences, and first-time-user comprehension remain the human walkthrough's evidence. The harness multiplies the AT reviewer's hour by catching structural regressions between sessions; it closes no human gate.

## Script steps → automated coverage

| Step | Script expectation | Automated by | Remaining human judgment |
|---|---|---|---|
| 1 | Title announces product; focus lands announced | `Part1_Step1` (in-proc), `Part1_Steps1and2` (headed) | What NVDA actually says at launch |
| 2 | Every control announces role + name; no unnamed pane; order matches visual logic | `Part1_Step2_every_focusable…`, `Part1_Step2_the_action_buttons…` (in-proc); `Part1_Steps1and2` (headed, full document-order assertion) | Speech phrasing; visible focus indication |
| 3 | `Shift+Tab` reverses without traps | — | Human only: focus traps are a runtime interaction property the harness does not simulate |
| 4 | Reach the module/recipe choice by keyboard; state change discoverable | `Part2_Step4` (in-proc, PressRoom): choosing a press regenerates its labeled parameter controls; headed: press selection over SelectionItemPattern | Whether the regeneration is announced |
| 5 | Enter a title and steps by keyboard; each field announces its label | `Part2_Step5` (in-proc, AllAboardForm): title + four typed steps reach typed approval; headed `Part2_Steps5to7`: the same entry over real UIA ValuePatterns | Typing echo behavior |
| 6 | Symbol picker announces names, never "image" | `Part2_Step6` (in-proc): every picker item is a provenance `IntendedMeaning`; no ".svg", no "image" anywhere AT can hear | The actual utterance on focus |
| 7 | Validation announced, offending field reachable | `Part2_Step7` (in-proc, ReviewForm): blank step surfaces a named issue, approval disables; `Part2_Step7` (PressRoom): a press refusal lands in the status line, which now speaks its message | Whether NVDA announces the change unprompted |
| 8 | Review announced as a review of a **draft** | `Part1_Step1` (title carries "draft") | Audibility in practice |
| 9 | Position announced; new position after move | `Part3_Step9` (in-proc), `Part3_Steps9to12` (headed): selection follows the moved element, so the announced position is the new one | The actual "step 3 of 5" utterance |
| 10 | Edit field reachable, labeled, reads back | `Part3_Step10` (in-proc), `Part3_Steps9to12` (headed, via ValuePattern) | Typing echo behavior |
| 11 | Approval states what approval means | `Part3_Step11` (in-proc, `AccessibleDescription`) | **Known gap:** the WinForms UIA provider does not surface `AccessibleDescription` as UIA `HelpText`; whether NVDA/Narrator speak the description must be confirmed by ear in the walkthrough |
| 12 | Approve → state change announced | `Part3_Step12` (in-proc, typed `ApprovedArtifact` produced), `Part3_Steps9to12` (headed, review completes) | The announcement itself |
| 13 | Reopen saved project → keyboard-readable data-lane preflight; incomplete Green classification refused; completion reaches fresh Gate B draft review; prior approval not inherited | `LoadedProjectPreflightTests.Exact_semantic_content_is_read_only_and_inspectable_before_Green_can_be_confirmed`, `An_incomplete_teacher_classification_cannot_mint_a_Green_capability`, and `Preflight_uses_only_named_roled_standard_controls` (in-proc); `PilotDay_dress_rehearsal…` plus `CompleteLoadedProjectPreflight` (headed): the exact document and three statements precede fresh Gate B, and re-approval alone unlocks outputs | What NVDA/Narrator actually speak for the preflight explanation, document, checkbox states, refusal, reset, unavailable outputs, and required re-approval; whether a first-time user understands the distinction |
| 14 | Lane radios: name, role, checked state; meaning in the name | `Part4_Step14` (in-proc), `Part4_Steps14and15` (headed, checked state over SelectionItemPattern) | Speech order and clarity |
| 15 | Safety pause reachable in ≤ a few tabs, names its purpose | `Part4_Step15` (in-proc: within the first six tab stops), `Part4_Steps14and15` (headed) | Whether "a few tabs" feels findable to a real user |
| 16 | Pause procedure announced, blocked state audible | — | Human only: MessageBox flow |

Cross-cutting, script-independent: `Mnemonics_are_unique…` (one unambiguous access key per action) and `ADR002_standard_controls_only…` (every control on both surfaces is a stock `System.Windows.Forms` type — the structural form of ADR-002's rule).

**Instrument reconciliation, 3 September 2026.** The original walkthrough wording expected step 13 to announce an inherited locked/approved state. That contradicted the implemented and tested fresh-review boundary recorded in this table. The script now asks the human reviewer to hear the fresh Gate B draft state, the loss of prior approval authority, and the need to re-approve. This documentation repair is not a walkthrough result and closes no accessibility gate.

## Findings the harness produced on first contact (2026-08-29)

1. **Unnamed focusable splitters.** Both `SplitContainer`s were keyboard-focusable, unnamed panes — exactly step 2's failure. WinForms ignores `TabStop = false` on `SplitContainer` (splitters stay focusable for keyboard resize), so the fix is naming them; both now carry accessible names.
2. **Lane radios overrode their meaning away.** `AccessibleName` was set to "Confirm Green lane"/"Keep Amber lane", *replacing* the full visible sentence for AT users — step 14's failure. The overrides are removed; the full text is the accessible name.
3. **Approval said only "Approve".** Step 11's failure. It now carries an `AccessibleDescription` stating that approval is the named approval of this exact revision — with the HelpText caveat recorded above for the human walkthrough.
4. **The draft state was invisible to the ear.** The review window title now says "reviewing a draft — nothing prints before approval" (step 8).

## Findings from the Press Room build (29 Aug 2026, second forge menu item 1)

5. **Status labels masked their own messages.** Both status lines carried `AccessibleName = "Status"`, which *replaces* the text for AT — a screen reader would hear the word "Status" forever, never the message. The overrides are removed on both surfaces; the message is the name.
6. **A modal opened re-entrantly from its click wedges automation.** Opening the Gate B review dialog synchronously inside the click handler left any UI Automation client's call pending until the dialog closed — the whole provider became unqueryable. The surface now defers the modal to the next message-loop beat (`BeginInvoke`), which AT experiences identically and automation survives. Related, recorded for future harness authors: the legacy managed UIA client does not enumerate top-level windows created after it attaches — the headed tests locate new dialogs by Win32 title walk and bridge with `AutomationElement.FromHandle`.

## Findings from the pilot dress rehearsal (29 Aug 2026, fourth forge menu item 1)

7. **Export's modal was still click-synchronous.** The Save As dialog opened re-entrantly from the Export click — the same wedge as finding 6, unnoticed because no automation had ever driven export. It now defers one beat like every other modal-opener.
8. **The shell Save As dialog defeats cross-process automation; the Open dialog does not.** Four independent walls, each found the hard way: the legacy client's `FindAll`/`FindFirst` Descendants queries return EMPTY over the SAVE AS dialog's DirectUI tree while `TreeWalker` navigation sees every control there; the Vista-style dialog appends the file pattern to each filter's display name ("Booklet PDF (2-up saddle-stitch) **(*.pdf)**"); the name field pre-fills ASYNCHRONOUSLY after the window exists, and a programmatic `SetValue` — even one that reads back correctly — is never what Save commits; and the OS foreground lock stops a background test runner's synthetic keyboard from reaching the field at all. The Open dialog, by contrast, commits typed paths honestly and the rehearsal drives it for real. Export therefore gained the same ctor seam the library picker already had (`exportPicker`; harness switch `--export-to`), and the headed rehearsal asserts everything that is OURS — the structural gate, the render-target switch, the imposition, the bytes on disk, the speaking status — while the dialog itself remains the human walkthrough's territory.
9. **No lookup on the shell Open dialog earned trust under load.** The flake that recurred across three menu-4 items was always the Open dialog under full-suite load (green every time standalone), and it would not be pinned: a `TreeWalker` crawl timed out; reverting to `FindFirst` moved the failure to the Open button's lookup — in the same run whose `FindFirst` found the File name edit beside it. Rather than deepen a twenty-second roulette on Microsoft's chrome, the reopen leg now goes through the `libraryPicker` ctor seam the in-process tests always used — the harness resolves the newest fixture project at click time only after production has admitted its exact disposable `--project-library-root` — and the rehearsal's shell-dialog driving ends entirely. Both file dialogs belong to the human walkthrough; the rehearsal guards everything that is ours.

## Findings from the forge closeout (30 Aug 2026)

10. **A storage-capable fixture could inherit the real default library.** `pressroom`, `allaboard`, and `modules` formerly launched without a test root. Each now requires the exact empty `%TEMP%\ocf-rehearsal-{GUID}\{engine-version}\prepared-library` shape after the production no-reparse validator has admitted it.
11. **A malformed fixture command could become production.** Missing, repeated, or unknown `--uia-harness` modes returned no fixture, and `Program` interpreted that as an ordinary Press Room launch. Presence of the harness switch now requires exactly one known mode or exits through a content-free refusal.
12. **The direct export seam could overwrite an arbitrary PDF.** `--export-to` bypasses the shell's overwrite confirmation by design, so it now accepts only one new fully qualified `.pdf` directly inside the validated empty rehearsal root. Duplicates, missing values, irrelevant modes, occupied roots, alternate extensions, and outside paths fail closed; a sentinel regression proves outside bytes remain unchanged.

## Under-load sighting evidence boundary (31 Aug 2026)

The two historical headed timeouts remain sightings, not diagnoses. Their
bounded reproduction harness still runs only the exact `Part3_Steps9to12…`
and `PilotDay_dress_rehearsal…` tests, retains each TRX and full assertion
message, and leaves the shared 20-second UIA probe timeout unchanged. The
separate exact burned-region image-test instrument remains a fresh-process
probe for the third historical sighting; it cannot recover the lost assertion
message. A pass from either instrument is only a current-code
non-reproduction.

Both instruments now share one cooperative evidence lock from source
preflight through the durable completed-batch summary. Their evidence-root
creator validates the repository, `out`, harness base, and unique run path
before and after creation and rejects every observed reparse-point segment.
Each instrument refuses a dirty tree, performs a locked restore and forced
non-incremental Release build with no build-skipping path, then records
pre/post HEAD, status, and tracked-plus-untracked-nonignored source-content
fingerprints together with sorted SHA-256 manifests of every file under the
exact Release TFM output root. Any source/output drift, failed result, missing
result, or invalid contention boundary makes the batch non-evidence and exits
nonzero. The same unrestarted contention jobs must report `Running` immediately
before and after every repetition; this is job-state evidence, not a measure of
CPU utilization or scheduler pressure.

After an outer-cap timeout, a shared Process-based helper gives the
`taskkill /T /F` request ten seconds, then gives helper self-termination and
exit observation a bounded further two seconds. Another repetition is allowed
only when the request started, did not time out, exited 0, the helper exit was
observed, and the original `dotnet` launcher exit was observed within the
separate ten-second launcher wait. Those facts and any start/cleanup errors are
retained in the per-run receipt before an unsafe batch aborts. This is not a
Job Object or an independent enumeration or proof of descendant exit.

The cooperative lock excludes only the two participating harnesses; an
arbitrary builder does not honor it. Boundary manifests cannot detect a
transient mutate-and-restore between snapshots, and neither the forced build
nor matching manifests prove compiler/source correspondence or the complete
SDK/NuGet process closure. The deliberately dirty current tree therefore
prevented both reproduction batches from running. Only the shared helper and
source contracts were exercised here: all three PowerShell files parsed cleanly
and the focused tests measured **20/20 passed**. This does not retroactively
strengthen the earlier headed **6/6** receipt or close any sighting. The ordinary
hosted solution test remains a separate one-process instrument under
`tools/run-ci-tests.ps1`; its own evidence boundary must not be conflated with
these reproduction batches.

### Local same-process suite sighting — 31 August 2026

Two plain minimal-output executions of the complete UI Automation assembly produced no TRX and an idle testhost after more than five minutes; both were stopped locally. The same exact 243-test binary completed **243/243** with ordinary console output, with blame-hang enabled, and with VSTest diagnostic logging. The 234 nonheaded cases passed together and the nine headed cases passed together. Those timing-altered and partitioned passes narrow the observation but do not identify the active test, stack, or cause. The two stalls are therefore one newly recorded same-process suite sighting, not a diagnosis and not evidence that either historical headed timeout recurred.

The audit did establish separate lifecycle defects in the test harness: the headed pseudo-locale child was killed without observing exit, `HeadedApp` ignored an unconfirmed exit, and a constructor-time UIA failure could bypass fixture disposal. Every headed child now receives a bounded kill-and-observe shutdown; constructor failure owns the same cleanup; and simultaneous assertion/cleanup failures preserve both exceptions. Two direct lifecycle tests measured forced and already-complete child exits. This hardening is independently warranted and is not represented as the cause of the stalls. The shared UIA polling stopwatch is also not called a hard bound: each synchronous UIA/COM probe can itself block before the stopwatch is checked again. A diagnosis still requires a stalled-run process inventory and native/full dumps of testhost and any surviving app child.

After the lifecycle hardening, the exact plain minimal UI command completed **245/245** in 45 seconds. It then completed **245/245** in each of two full seven-suite closing runs. These are non-reproductions after a guard, not proof of causation or closure of the new sighting.

A private typist-only self-run may prepare defects, but it cannot close the gate. The accountable accessibility/AT review is H4, may begin only after H0–H3 are frozen, and remains the human evidence that closes the gate; this harness only keeps what the reviewers find fixed from regressing.
