# NVDA/Narrator walkthrough — script

**Constitutional basis:** ADR-002 — standard controls only until the accessibility harness exists; the review surface gets AT walkthroughs *early, not late*. Plan §17: ≥95% completion in the moderated keyboard workflow. · **Who:** typist self-run first (prep week), then the AT reviewer seat moderated (week 2). · **Setup:** NVDA (free, nvaccess.org) at default verbosity; repeat critical steps under Windows Narrator. **Mouse unplugged or pushed away — the whole script is keyboard-only.**

Record per step: **works / works-but-wrong-speech / fails**, plus what was actually announced. "Works" means a first-time NVDA user could proceed without sight of the screen.

## Part 1 — Launch and main surface

| # | Action | Expect to hear |
|---|---|---|
| 1 | Launch the app; wait | Window title announcing the product name; focus lands somewhere announced, not silence |
| 2 | `Tab` through the entire main surface, one full cycle | Every control announces role + name ("button", "list", …); no unnamed "pane"; order matches visual logic; focus visibly indicated |
| 3 | `Shift+Tab` back one full cycle | Same order reversed, no traps |

## Part 2 — Authoring a strip

| # | Action | Expect |
|---|---|---|
| 4 | Reach the module/recipe choice by keyboard; activate with `Enter`/`Space` | Choice announced; resulting state change announced or discoverable |
| 5 | Enter a title and four steps by keyboard only | Each field announces its label; typed text echoes per NVDA settings |
| 6 | Attach a symbol to a step | The symbol picker is keyboard-operable; each symbol announces its **name**, never "image" or a filename |
| 7 | Make a deliberate mistake (blank step), attempt to proceed | The validation message is announced (not just painted red somewhere) and focus moves to, or can trivially reach, the offending field |

## Part 3 — The review surface (the novel UI; the reason this script exists)

| # | Action | Expect |
|---|---|---|
| 8 | Open review with a draft present | Announced as a review of a **draft** — the draft/approved distinction must be audible, not just visual |
| 9 | Navigate the step list; move a step (the `MoveSelection` path) | Position announced ("step 2 of 4"); after moving, the **new** position announced |
| 10 | Edit a step's text within review | Edit field reachable, labeled, and the changed text readable back |
| 11 | Locate the approval control | It states what approval *means* (named approval of this exact revision), not just "OK" |
| 12 | Approve | State change announced; the artifact's approved status discoverable afterward by keyboard |
| 13 | Reopen review on an approved artifact | The locked/approved state is audible before any edit is attempted |

## Part 4 — Capture flow and Gate C

| # | Action | Expect |
|---|---|---|
| 14 | Walk the capture/import path to the file dialog | Standard dialog announces normally; lane choice radios announce name, role, **and checked state**; the lane's meaning is in the accessible name, not only in adjacent visual text |
| 15 | Find the safety pause control ("I saw something concerning — pause here") by keyboard alone, from anywhere in the flow | Reachable in ≤ a few tabs; announces its full purpose. An invisible Gate C is a failed Gate C |
| 16 | Invoke it | The procedure text is announced/readable; the blocked state is audible |

## Part 5 — Narrator spot-check

Repeat steps 8–12 and 15 under Narrator. Divergences are findings even when NVDA passed.

## Scoring

```
Date ____  Runner: typist self-run / AT reviewer (circle)  NVDA ver ____ Narrator: Win 11
Steps passed: __ /16   Works-but-wrong-speech: [step #s + what was said]
Fails: [step #s + what happened]
Completion for §17: moderated keyboard workflow completed end-to-end? Y/N
Three worst moments, in the runner's words:
```

File as `docs/evidence/pilot/nvda-walkthrough-<date>.md`, one issue per fail and per wrong-speech. The typist's self-run does not close the gate — it clears the underbrush so the AT reviewer's hour is spent on real findings, and their session is the evidence.
