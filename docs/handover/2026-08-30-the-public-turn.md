# The public turn — the project acquired a public face, and guardrails to deserve one

> **Historical state.** On 30 August 2026 this handover was superseded for current repository state and closing evidence by [the forge closeout](2026-08-30-forge-closeout.md). It remains the record of the earlier public turn; do not infer current test counts or open forge work from it.

**Date:** 30 August 2026, early hours · **Audience:** the typist, the second maintainer when one is seated, and any agent — Claude, Codex, or another — opening this repository next. Read [AGENTS.md](../../AGENTS.md) before you commit anything.

The [moved calendar](2026-08-29-the-moved-calendar.md) records why the pilot left 8 September and what the long window is for. This records what happened in the window's first night: the repository became public under an organization, the site went live, a study instrument was rebuilt after a near-miss, and the project acquired machine guardrails it had been relying on care to provide.

## I. State of the repository (main @ `32d8407`)

- **671 tests green** across six suites. `0.7.0-alpha`, unchanged — **version bumps remain the typist's act.**
- **56 Press Room engines, 23 press recipes**, two composers, two authoring surfaces. Unchanged this session; none of the work below touched the presses.
- **Public**, at `honest-ink-inc/open-classroom-foundry`, owned by the organization with **two owners**.
- **Site live** at `https://honest-ink-inc.github.io/open-classroom-foundry/` — seven pages, published by a `workflow_dispatch`-only workflow that never runs itself.
- **CI green on both jobs**, with **no exclusions anywhere**: warnings-as-errors, format gate, 93% core coverage, byte-for-byte determinism over 40 sample files, and a secret scan over full history.

## II. What was done, and the reason each thing was not what it looked like

**The `.ocfproj` writer.** The one open thread from the fourth menu, closed — and the recorded diagnosis was wrong. Measured before fixing: the runs differed on `manifest.json`'s `projectId`, a fresh `Guid.NewGuid()` on every save. The zip timestamps the note blamed were a *second, real* cause that a fast measurement had nearly hidden. Both fixed; the last CI exclusion deleted with them.

**The move to an organization.** `honest-ink` was unavailable, so the org is `honest-ink-inc`. The reason matters and is recorded in [the trademark screening](../trademark-screening.md): one blocker is the typist's own account, the other is a party outside education. The namespace is not contested in this field.

**The repository was already public once.** From 14:13 to roughly 23:55 UTC on 29 August, with the seeded-error answer key in it. No forks, stars, or watchers resulted and no participant had the URL. Traffic statistics do not survive a transfer, so that window cannot be measured after the fact — the absence of data is not evidence of absence.

**The secret scan had been red for four commits.** The transfer made `gitleaks-action`'s organization licence binding. `build-and-test` was green throughout; only the scan job failed. **It went unnoticed because a watcher's exit code was trusted instead of the run's own conclusion.** The fix was not a licence: the scanner is MIT and only the Action wrapper carries the org terms, so the wrapper is gone and the pinned CLI runs with its checksum verified before execution. Going public did **not** restore the free tier — measured, not assumed.

**The README front page was false.** It said "No application code exists yet" while 665 tests passed — and it is the site's index. Status now states what runs *and*, at equal length, what does not: nine doorless modules, English-only chrome, unsigned build, no pilot run, bus factor of one. The roadmap gained a state column, which turned up that the local-inference "spike" is a written assessment in front of an empty project.

## III. The seeded-error study, rebuilt

The generator's own comment claimed the packet-to-defect mapping "lives only in the facilitator key." **It was false** — the defects are semantic and written in plain language, so anyone reading `Program.cs` could reconstruct the key. Deleting the key file would have achieved nothing; the source was the leak.

So the packets became an **input**: `--seeded <definitions.json>`, with definitions and key kept by the facilitator outside this repository. Only a fictional example is committed. A **second generation** was then written — eight all-new scenarios, six seeded and two controls, class-to-tier mapping deliberately unchanged so results stay comparable — and the burned first generation was deleted from the tree. It remains in already-public history; the current tree does not provide a retrieval pointer.

**The definitions and key are deliberately not located in this repository, and their storage path is deliberately not written down here.** A public repository naming the place the answer key lives would be gratuitous reconnaissance. The facilitator knows where it is; a future hand who needs it should ask, not search. **Do not "helpfully" record the path.**

Guards, so this is machinery rather than memory: `.gitignore` refuses `seeded-packets.json` and any `FACILITATOR-KEY.md`; `RepositoryHygieneTests` fails if either is tracked, or if any generated packet is.

## IV. The guardrails, and why prose was the weakest layer

Every rule in the project governed what **enters** the repository. The near-miss was a **visibility change**. Three layers now exist, and they are listed weakest-last on purpose:

1. **Machinery.** A `pre-commit` hook refusing any commit whose staged changes carry a secret — it *refuses* rather than warns when the scanner is absent, because a scan that silently does not run reads as protection. `.gitignore` patterns for credential filenames. `RepositoryHygieneTests`, asserting over what git **tracks** rather than what sits in the working directory.
2. **Procedure.** A five-step [pre-publication check](../release/hardening-checklist.md): scan full history not the tree; list what becomes newly readable; check the *claims* are true today; name what cannot be undone; read a gate's conclusion rather than a wrapper's exit code.
3. **Prose.** [CONTRIBUTING.md](../../CONTRIBUTING.md) now carries three rules, not one. [AGENTS.md](../../AGENTS.md) is canonical for automated contributors and names the acts an agent must never perform without a human.

**The agent wiring had a gap running opposite to expectation.** `AGENTS.md` is what Codex reads, so Codex was covered on arrival. Claude Code reads `CLAUDE.md`, which did not exist — the generator that has made every commit here had no in-repo guidance at all. `CLAUDE.md` is now a thin pointer, never a copy; if the two disagree, AGENTS.md wins and the pointer is the defect. A test guards the wiring so a tidy-up cannot leave a toolchain reading nothing.

`gitleaks` is installed system-wide at 8.30.1, matching the version CI pins. **Hooks are per-clone and are never inherited** — every working copy, every machine, every agent runs `pwsh tools/install-hooks.ps1` once. A fresh shell is needed after installing the scanner, or the hook will refuse you for a stale `PATH`; it refused this session's own author exactly that way.

## V. The lesson this session paid for four times

*A defect note records a hypothesis, not a finding.* It was learned on the `.ocfproj` writer and then sprung three more times in different clothing:

| Sighting | What was trusted | What was true |
|---|---|---|
| `.ocfproj` nondeterminism | A recorded diagnosis | Two causes, one of them nearly hidden by a fast measurement |
| A flake in `ImageNormalizerTests` | An output filter keeping pass/fail lines | The assertion **message** was lost and the cause is still unknown |
| CI "green under the org" | `gh run watch --exit-status` | Red for four commits |
| A CRLF hook, a stale `PATH`, an `echo`'s exit code | Tooling reports | The bytes, the run's `conclusion`, and HEAD |

**Read the measurement. Not a wrapper around it, not a note about it, not what you expect it to say.** This is now in AGENTS.md, where the next agent will meet it.

## VI. Open, and whose it is

**Counsel's, answered:** wait for SDAT before the trademark filing. The applicant must be an entity that exists.

**The typist's, waiting on Maryland:** the filing itself, the pilot's opening date, and pointing `honest-ink.org` at the live site — the loudest remaining use of the name, and the one ADR-006's checkpoint most clearly reaches. Also outstanding: the four counsel questions in the moved calendar §II, of which the fourth — *who are the directors, and where are the bylaws* — is still unanswered anywhere in this repository.

**The forge's, in order:** the nine doorless modules; a real second UI chrome language; the 1366×768 floor **as a floor that must not break, not a design target**; then the atlas, council-first.

**Debts, all recorded:** no upgrade path for a mid-pilot version bump; `tests/Accessibility` builds but holds no tests; CI actions on deprecated Node 20; and three under-load flakes logged as *sightings, not diagnoses* — two headed UIA timeouts and one image test, each passing in isolation, none reproduced in CI.

**Standing and unchanged:** the seats' territory is not waivable from a keyboard, and a longer schedule is not permission — it is the opposite, because no deadline remains to excuse it. Version, tag, sign, install, distribute, publish, and file remain the typist's acts.
