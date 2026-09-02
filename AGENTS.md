# For automated contributors

**Audience: any LLM agent or automation committing, pushing, or publishing on behalf of Honest Ink.** Humans should read [CONTRIBUTING.md](CONTRIBUTING.md) — it says the same things at more length. This file exists because an agent given a task may never open CONTRIBUTING.md, and because an agent commits faster than a person hesitates.

Nothing here is hypothetical. Every rule below was learned from something that happened in this repository.

## Which tool reads what

This file is canonical. It is wired to the tools that look for different names, and **whoever adds a new agent to this project is responsible for wiring it too** — an agent that reads nothing is the risk this file exists to reduce.

| Tool | Loads | Status |
|---|---|---|
| Codex, and most agents following the cross-tool convention | `AGENTS.md` | This file — canonical |
| Claude Code | `CLAUDE.md` | A thin pointer here; it must never duplicate this content |
| Anything else | — | Point it here before you let it commit |

If a pointer file ever disagrees with this one, **this one wins and the pointer is the defect.**

~~Note that Claude Code has been the only generator used on this repository through 30 Aug 2026, so nothing below has been exercised by a second toolchain. Treat the first Codex or other-agent session as a test of these instructions as much as of the task — and if something here proves ambiguous in practice, fix the wording rather than working around it.~~

*Struck 1 Sep 2026.* Codex became the second toolchain on 1 September 2026: pull requests 5 through 8 were prepared on `codex/*` branches under these rules, closed with local and hosted evidence, and merged regularly. That session recorded no ambiguity in this file. The one rule it followed that this file had never written down is the merge-method rule under **How changes reach `main`** below, added the same day. The standing instruction survives the strike: when a rule proves ambiguous in practice, fix the wording rather than working around it, and write down what was ambiguous.

## Read before you act

1. [CONTRIBUTING.md](CONTRIBUTING.md) §"The rules that outrank everything" — the three categories that must never enter this repository.
2. The handover marked **Current repository state** in [docs/README.md](docs/README.md) — repository state, the closing rites, and the traps already sprung. A date alone is not enough: when handovers share a date, follow their supersession notices and the index's explicit current-state marker.
3. Whatever governing document your task touches: an ADR outranks the implementation plan, which outranks the atlas.

## Never commit

- **Student work, student data, or identifying classroom material.** Nothing about a real learner, ever, in any file, including tests and fixtures.
- **Credentials** — API keys, tokens, passwords, connection strings, private keys, certificates, `.env` files. Git history is permanent and this repository is **public**. A committed key is a burned key even if the next commit removes it: rotate it, do not merely delete it.
- **Blind-study instruments** — the seeded-error definitions and facilitator key. These are not secrets in a security sense; they are secrets in a research sense. A participant who has seen the key is trained, not testing.

The pre-commit hook, `.gitignore`, `RepositoryHygieneTests`, and the CI scan enforce these. **Install the hook before your first commit:** `pwsh tools/install-hooks.ps1`. If the hook refuses your commit, it is doing its job — fix the content, never the hook.

## Never do without a human saying so, in that session

These are the typist's acts. They are outward-facing, hard or impossible to reverse, and they are not yours:

- Changing **repository visibility**, or making anything public.
- **Publishing** — running the site workflow, pointing a domain, deploying anything.
- **Tagging, versioning, signing, installing, or distributing** a release.
- **Sending correspondence** — council letters, district packets, any email.
- **Filing anything public** — a trademark application, a registration, a form.
- Creating accounts or organizations, or accepting terms on behalf of the entity.
- Anything the AAC/SLP seat, the multilingual seat, or the district instrument governs. A longer schedule is not permission; it is the opposite, because there is no deadline left to excuse it.

When a task seems to require one of these: **stop and say so.** Do the part that is yours, name the part that is not.

## Before anything becomes newly visible

Content that is harmless in a private repository can be harmful the moment it is public, and **publishing publishes the whole history, not the current tree**. Before a visibility change, a release, or a site publish, run the pre-publication check in [the hardening checklist](docs/release/hardening-checklist.md). It is short. It has already caught one thing.

## Report what you measured, not what you expect

Three defects in this repository were recorded as one thing and turned out to be another. The discipline that catches this:

- **Measure before fixing.** A defect note records a hypothesis, not a finding. The `.ocfproj` nondeterminism was recorded for a day as a timestamp bug; it was a random `Guid`, *and* a timestamp bug — a fast measurement had nearly hidden the second cause.
- **Read the measurement, not a wrapper around it.** CI was reported green twice while it was red, because a watcher's exit code was trusted instead of the run's own conclusion.
- **Keep the failure message, not just the name.** A flake's assertion message was lost to an output filter that kept only pass/fail lines.
- **Never report green without running it.** "Tests should pass" is not evidence.

## The closing rites

Run these for every item, in order — the full text is in the enactment handover:

1. Release build; warnings are errors.
2. `dotnet format` (fix), then `--verify-no-changes` demanding exit 0.
3. Full `dotnet test`, plus at least one stability re-run, with failure **names and messages** surviving your output filter.
4. If presses changed: the SampleGenerator twice, hash-compared byte for byte.
5. Strike the item with a dated note — never delete one — commit, push, and **read the CI run's conclusion**.
6. Record the hosted conclusions in the [evidence ledger](docs/evidence/evidence-ledger.json). If a test failed under load and passed alone, add the row to the [sightings register](docs/evidence/sightings-register.md); never raise its timeout as part of the same change.

## How changes reach `main`

- Work on a branch and open a pull request. Read the CI **and** CodeQL conclusions for the exact head before merging, and read the exact-`main` runs after the merge; a green pull-request run does not stand in for either.
- **Merge with a merge commit only. Never squash-merge, never rebase-merge, never force-push `main`.** CI's recipe-identity ratification guard requires the exact C1 and record-only C2 commits to remain in `main`'s ancestry with C1 as C2's sole parent; squashing or rebasing rewrites hashes and destroys that proof. Pull request 4 was rebase-merged on 31 Aug 2026, before the guard existed, which is why its `q1q4` branch still reads as unmerged.
- Delete your branch after its pull request merges. A stale merged branch reads as unfinished work, and a stale rebase-merged one reads as unmerged work.
- Cite hosted runs from the evidence ledger and sightings from the register. A handover explains decisions and boundaries; it does not restate what those two files already record.

## If you are unsure

Say so plainly and stop. This project would rather have an honest gap than a confident mistake — its name is an argument about exactly that, and a tool called Honest Ink that overstated its own state would be self-refuting.
