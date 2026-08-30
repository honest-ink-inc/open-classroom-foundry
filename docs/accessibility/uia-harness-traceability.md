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
| 13 | Reopen approved → locked state audible | `PilotDay_dress_rehearsal…` (headed): a saved project reopened from the library arrives as a FRESH Gate B review — by design there is no locked state to announce, only a new review; re-approval unlocks outputs again | What NVDA announces when the reopened review appears |
| 14 | Lane radios: name, role, checked state; meaning in the name | `Part4_Step14` (in-proc), `Part4_Steps14and15` (headed, checked state over SelectionItemPattern) | Speech order and clarity |
| 15 | Safety pause reachable in ≤ a few tabs, names its purpose | `Part4_Step15` (in-proc: within the first six tab stops), `Part4_Steps14and15` (headed) | Whether "a few tabs" feels findable to a real user |
| 16 | Pause procedure announced, blocked state audible | — | Human only: MessageBox flow |

Cross-cutting, script-independent: `Mnemonics_are_unique…` (one unambiguous access key per action) and `ADR002_standard_controls_only…` (every control on both surfaces is a stock `System.Windows.Forms` type — the structural form of ADR-002's rule).

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

The typist's self-run of the script (prep week) and the AT reviewer's moderated session (week 2) remain the evidence that closes the gate; this harness only keeps what they find fixed from regressing.
