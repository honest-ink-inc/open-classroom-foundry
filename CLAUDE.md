# Claude Code — read AGENTS.md

**[AGENTS.md](AGENTS.md) is canonical for every automated contributor, Claude included.** Read it before you commit, push, or publish anything. This file exists only because Claude Code loads `CLAUDE.md` while Codex and most other tools load `AGENTS.md`; the rules are the same rules, and if these two files ever disagree, **AGENTS.md wins and this file is the defect.**

Do not copy AGENTS.md's content here. One canonical file cannot drift out of step with itself.

## The short form, so it is in front of you

- **Never commit** student data, credentials, or blind-study instruments (the seeded-error definitions and facilitator key).
- **Never do without a human saying so in that session:** change repository visibility, publish, point a domain, tag or distribute a release, send correspondence, file anything public, or create accounts and accept terms.
- **Install the hook first:** `pwsh tools/install-hooks.ps1`. It refuses commits carrying secrets. If it refuses yours, fix the content, never the hook.
- **Merge commits only.** Never squash-merge, rebase-merge, or force-push `main`; CI's ratification-history guard depends on exact ancestry. AGENTS.md records why.
- **Report what you measured.** Read a CI run's `conclusion`, not a watcher's exit code. Keep failure messages, not just names. Never call something green without running it.

## Claude Code specifics

- The harness may block outward-facing actions such as changing repository visibility. **That block is correct.** Do not route around it; report it and hand the step to the typist.
- Some Bash tool patterns silently corrupt content in this repository — `\f` and other escapes in Python or Perl replacement strings, PowerShell `Get-Content | Set-Content` round-trips, control characters written into source. Verify bytes after any scripted edit, not just the tool's exit status.
- The closing rites for every item are in the handover marked **Current repository state** in [docs/README.md](docs/README.md). A date alone is not enough when handovers share one. Run the rites in order, every time.
