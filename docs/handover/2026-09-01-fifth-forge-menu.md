# The fifth forge menu — the register, the load, the ledger, and the rule of merge

**Prepared:** 1 September 2026
**Branch:** `claude/fifth-forge-menu-2026-09-01`
**Base:** `56f69006739e1d36302f836ed7f915803392d1c9` (`main`), whose exact-main CI run `33528622567` and CodeQL run `33528622566` both concluded success
**State:** current repository-state handover. It inherits the evidence and boundaries of the [forge integration handover](2026-09-01-forge-integration-handover.md) except where this document expressly changes them. Hosted evidence for this menu is recorded in the [evidence ledger](../evidence/evidence-ledger.json) as it is measured, never before.

## Provenance

On 1 September 2026 the governance council review, run in a Claude Code session on the typist's machine, verified the stated repository position against the repository itself and against GitHub, found no drift, and recommended a five-item zero-gate forge menu together with typist-only settings changes. The typist ratified with the standing phrase, **"Proceed with the fifth forge menu, items 1 through 4"**, the same day. Item 5 was not ratified and stays held. No protected seat, district, or typist act is part of this menu.

## The menu

| # | Item | Source | State |
|---|---|---|---|
| 1 | **The sightings register.** One governed table naming every recorded test-failure sighting with its signature, class, first observation, retained observations, instrument, and status, guarded by a Unit test that fails when a record names a sighting the register lacks. | Forge integration handover, future work 6 and 7; traceability P-07 | **Struck 1 Sep 2026.** [docs/evidence/sightings-register.md](../evidence/sightings-register.md) carries ten open rows and one deterministic row; [SightingsRegisterTests](../../tests/Unit/SightingsRegisterTests.cs) guards it. Evidence below. |
| 2 | **The console-signal load-reproduction instrument.** A bounded harness for the console-signal and FlashCap rows that restores the hosted overlap with the real Edge PDF exercise across two fresh processes under CPU and memory contention, retains content-free receipts, and changes no deadline. | Future work 7; the moved calendar §V | **Struck 1 Sep 2026.** [tools/run-console-signal-load-repro.ps1](../../tools/run-console-signal-load-repro.ps1) with [ConsoleSignalLoadReproductionHarnessTests](../../tests/Unit/ConsoleSignalLoadReproductionHarnessTests.cs). Evidence below. |
| 3 | **The evidence ledger as data.** A committed JSON ledger of measured hosted conclusions and regular merges that handovers cite instead of restating, validated structurally and re-measurable against GitHub and the Git graph. | Council finding: the current-state record was outgrowing its readers | **Struck 1 Sep 2026.** [docs/evidence/evidence-ledger.json](../evidence/evidence-ledger.json), [EvidenceLedgerTests](../../tests/Unit/EvidenceLedgerTests.cs), and [tools/measure-evidence-ledger.ps1](../../tools/measure-evidence-ledger.ps1). Evidence below. |
| 4 | **The AGENTS.md refresh.** Strike the single-toolchain note with a dated record of the first Codex session, and write the merge-method rule down. | AGENTS.md's own instruction to fix wording that practice exposes | **Struck 1 Sep 2026.** [AGENTS.md](../../AGENTS.md) gains **How changes reach `main`** and a sixth closing rite; [CLAUDE.md](../../CLAUDE.md) and [CONTRIBUTING.md](../../CONTRIBUTING.md) carry the one-line mirror. |
| 5 | **A Proposed ADR for project schema 2 carrying `recipeHash`,** with frozen schema-1 fixtures and explicit migration tooling per ADR-003. | Recipe identity disposition packet, the independent `recipeHash` gap | **HELD.** Not ratified. The product owner has not authorized a schema-2 route; schema 1 stays deliberately hash-less and the release stop stands. |

## What the council measured and left to the typist

These are settings of the hosted repository, measured on 1 September 2026 through the GitHub API, and every one of them is a typist's act to change. They are recorded here so the record carries them; nothing in this menu changes them.

- `main` has no branch protection and no ruleset. The repository allows merge commits, squash merges, and rebase merges alike. Pull request 4 was in fact rebase-merged, which is why `origin/q1q4` still reads as unmerged. The recipe-identity ratification guard states that squash and rebase destroy its proof.
- GitHub's own secret scanning, push protection, and Dependabot alerts are disabled. The repository's gitleaks scan and NuGet vulnerability audit run in CI, so this is defense in depth rather than an unguarded gap; push protection is the one control that stops a burned key before it reaches history, and it is free for public repositories.
- Four merged `codex/*` branches and `q1q4` remain on the remote; delete-branch-on-merge is off.
- Engine identity, assembly metadata, and the alpha tag still disagree; the publish script fails closed on that, correctly. Choosing the version remains the typist's.

The council's recommendation to the typist, verbatim in substance: allow merge commits only; add a ruleset on `main` requiring a pull request and the CI and CodeQL checks, blocking force pushes and deletion, with required approvals at zero until a second maintainer is seated; enable secret scanning, push protection, and Dependabot alerts but not automated version updates, which would fight the locked restores; delete the merged branches and turn on delete-branch-on-merge. Send the three council letters this week. Begin the documented second-maintainer recruitment attempt. Decide ADR-007 and the `recipeHash` forward route.

Later the same day the typist reported acting on the settings and the letters; the measurements, and the one defect they found, are under **The typist's acts, reported and measured** below.

## Item 1 — the sightings register

Before this menu, the sightings lived in four handovers and the traceability matrix, each phrased for its own day. The register puts them in one table under one vocabulary. It carries ten open rows: the two headed UIA element-wait timeouts from 29 August, the burned-region image failure whose message was lost, both observations of the console-control case, the two FlashCap lifecycle timeouts, the local same-process UI suite stall, the hosted six-hour cancellation, and the one-time `0x8000FFFF` approval return. It carries one row in a second table for a deterministic failure that a record names on a sighting line, so the guard stays exhaustive without a reader mistaking it for a sighting.

The guard reads every Markdown file under `docs/` except the register. On every line containing the word *sighting*, it extracts each test identifier written as a class name ending in *Tests*, a dot, and a method name containing an underscore, together with each `console-signal.` token, and fails if the register lacks it. It also fails if a register identifier names no method in a matching test source, if a token is absent from the console-control sender, if the row tables lose their sequence or vocabulary, or if a relative link does not resolve. What the guard cannot judge is whether a row is honest; a hand still reads the cited record.

## Item 2 — the console-signal instrument

Both hosted observations of `console-signal.lock-observation-timeout` and the FlashCap late-start timeout overlapped the real two-export Edge PDF exercise while xUnit ran up to four Integration tests concurrently. C4 and C5 answered by placing the operator-host and FlashCap classes in one non-parallel collection, which is correct for the product's evidence but removed the very overlap that reproduced the failures from every ordinary run. The instrument restores that overlap from outside the process: each repetition starts one fresh `dotnet test` process for the real PDF exports and, at once, a second fresh process for the three named cases, while controlled CPU and memory workers stay live. It records whether the exercise process was alive when the named process started, measures the overlap of every named case against every exercise case from the TRX start and end times, and treats a repetition without live workers, without a valid exercise, or without measured overlap as an invalid load condition rather than a pass. It refuses to run at all when Edge is absent, because the PDF tests would silently skip and the exercise would be empty.

Its boundaries are stated in its summary and bound by its contract test. Overlap across two processes on one machine approximates the hosted intra-process overlap; it does not reproduce shared thread-pool or xUnit scheduling. The sender's 15-second readiness budget, the test's 15-second host exit wait, and the five-second FlashCap bounds belong to the tests and are unchanged; the harness contains no deadline and no retry. A passing batch is a non-reproduction, not a diagnosis, cure, or closure.

## Item 3 — the evidence ledger

The ledger is a JSON file of entries in measured-time order. A hosted entry carries the run identifier, workflow, event, branch, exact head, the synthetic merge checkout when one is known, the measured conclusion, the bounded runner receipt and test counts when the record carries them, the SARIF hash when one was recorded, the document that narrates it, and content-free notes. A merge entry carries the pull request, the merge commit, both parents, and the merge time. It is seeded with every hosted run and regular merge the forge integration handover narrates and with the 31 August run the register cites.

Its Unit test validates format, vocabulary, hashes, receipts, dates, coherence of test counts with conclusions, the existence of every cited record, the absence of absolute paths, uniqueness, and order. It also reads the ledger-bound records, this handover and the register, and fails if either cites a hosted run the ledger does not carry. The measurement tool re-reads every hosted entry from the run's own record and every merge from the Git graph and exits nonzero on any mismatch, so the ledger is measured rather than trusted.

## Item 4 — the rule of merge

AGENTS.md said Claude Code had been the only generator through 30 August and asked that the first other-agent session be treated as a test of its wording. Codex was that session on 1 September; the note is struck in place with the dated record. The session exposed no ambiguity in the written rules, but it followed one that the file had never written: merge with a merge commit, because the ratification guard depends on exact ancestry. That rule now stands under **How changes reach `main`**, with the pull request 4 rebase merge as its recorded lesson, together with reading exact-main runs after a merge, deleting merged branches, and citing the ledger and register rather than restating them. A sixth closing rite records the hosted conclusions in the ledger and any new sighting in the register. CLAUDE.md and CONTRIBUTING.md carry the one-line mirror so a human or a Claude session meets the rule before opening AGENTS.md.

## Standing laws, unchanged

Presses take parameters, never prose. Nothing renders, prints, exports, or persists without a typed approved artifact. Lanes escalate only; Amber never persists. The AAC/SLP seat, the multilingual seat, and the district instrument are not waivable from the keyboard, and a longer window is not permission. Option A remains ratified for all 23 rows with the 15 candidate identities frozen at exact C1; the `v0.7.0-alpha` tuples remain pre-admission; schema 1's missing `recipeHash` remains a release stop; ADR-007 remains Proposed. Versioning, tagging, signing, installing, distributing, publishing, filing, and correspondence remain the typist's acts. Every recorded sighting remains open.

## Closing evidence

*Recorded after the rites ran, in order; nothing here was written before its measurement.*

**Environment.** The rites ran on the typist's machine on the exact `global.json` SDK, 10.0.302. PowerShell 7 is not installed system-wide on that machine; the bounded runner, the instruments, and the Unit tests that launch `pwsh` used PowerShell 7.6.4 from the Codex runtime's dependency directory, placed on the session PATH. That is the binary the 1 September Codex closes also used. It is recorded because a hand without it sees the runner and those Unit tests fail before any product code runs.

1. **Release build.** Locked restore of the solution, the `win-x64` application graph, and the pinned tools; `dotnet build OpenClassroomFoundry.slnx --no-restore --configuration Release` across all 26 projects measured **0 warnings, 0 errors**.
2. **Format.** The fix pass exited 0 and changed no content; `--verify-no-changes` exited 0. The pass was repeated after the register rows S-11 and N-02 and the guard change were added: the fix pass again exited 0 and changed no content, and `--verify-no-changes` again exited 0. The document-governance Unit batch on that final state measured **37/37 passed**: the register guard, the ledger tests, the three harness contracts, the traceability matrix, hygiene, and Atlas governance.
3. **Bounded solution runs.** Three runs through `tools/run-ci-tests.ps1`, each binding commit `56f69006739e1d36302f836ed7f915803392d1c9` with the uncommitted menu as a 14-entry dirty tree, each with stable source and Release-assembly identity, no outer timeout, seven TRXs, and seven coverage files. Suite counts in every run: Accessibility 26, Contract 152, Instructional Evals 336, Integration 307, Rendering 81, UI Automation 261, and Unit 850, which is the prior 835 plus the 15 tests this menu adds.
   - Run 1, receipt `20260901T202153Z-a8f63cde12b6455aad54179b2834b9f5`, launched from the session's Git Bash tool context: **red at 2,011/2,013**, exit 1, in 207,143 ms. The two failures are retained with names and messages and are not erased. `ProjectUpgradeOperatorHostTests.Real_console_ctrl_c_cancels_the_process_and_cleans_the_synthetic_batch` failed in 3.4964040 s with `Assert.Empty() Failure: String was not empty` on the host's standard output, which began `Managed project-upgrade preparation recei…`: the sender exited 0 yet the host completed its batch. `HeadedUiaWalkTests.Part3_Steps9to12_move_edit_and_approve_operate_through_uia_patterns` failed in 1.5213343 s with `System.Windows.Automation.ElementNotAvailableException : Unrecognized error.` from the UI Automation core's `FindAll`.
   - Measurement before any conclusion: the console case alone failed 3/3 in fresh processes from the same Git Bash context at about 2 s each, and passed 1/1 at 0.59 s from a Windows PowerShell context on the same tree. It is therefore deterministic in one parent context and not a load sighting; it is recorded as **N-02** in the register, undiagnosed, with the difference between contexts unmeasured. Part3 alone passed 2/2 at about 1 s from the same Git Bash context; its failure is a new-signature load sighting, recorded as **S-11**.
   - Run 2, receipt `20260901T202905Z-39073627a7e8443cabc303286874a1b5`, launched from the PowerShell context: **2,013/2,013**, exit 0, 205,314 ms.
   - Run 3, receipt `20260901T203420Z-219ecff4e62a4afb803ab227b4cb3323`, same context: **2,013/2,013**, exit 0, 219,274 ms.
   - Runs 2 and 3 are the close. Run 1 stays in the record because a red measurement is evidence, and because it found N-02. Neither green run diagnoses or closes S-11, N-02, or any other row.
4. **SampleGenerator.** No press, recipe, fixture, or sample input changed; the rite does not apply and no earlier sample proof is relabeled.
5. **Ledger measurement.** `tools/measure-evidence-ledger.ps1` compared all 28 seeded entries with `gh run view` and the local Git graph: **0 mismatches**; its receipt is under `out/evidence-ledger-measurement`, local and uncommitted like every other receipt.
6. **Instrument.** The console-signal instrument requires a clean tree, so its first batch runs after the implementation commit; its result is recorded with that commit below.

## The typist's acts, reported and measured

On the evening of 1 September 2026 the typist reported sending the three council letters from the contact address, individually, and changing the repository settings. The letters are recorded as **reported**: SPF and DKIM proof of sending is to be supplied later, and this record carries no measurement of them. The settings were **measured** through the GitHub API at about 20:34 UTC:

- Merge commits are the only allowed method; squash and rebase merges are off. Delete-branch-on-merge is on.
- Secret scanning, push protection, and vulnerability alerts are enabled. Dependabot security updates are disabled and no version-update configuration exists, as recommended, so nothing fights the locked restores.
- One active ruleset named `main` applies to the default branch: deletion blocked, non-fast-forward pushes blocked, a pull request required with zero approvals, and required status checks.
- **Defect found by measurement, for the typist.** The ruleset's required check contexts are `CI` and `CodeQL`, but GitHub reports the check runs on `main` as `build-and-test`, `secret-scan`, `portable-samples`, and `analyze-csharp`. Contexts named after workflows are never reported, so as written no pull request can merge into `main`. Replacing the two contexts with those four names makes the rule enforce what it means. The ruleset's own allowed-merge-method list still names squash and rebase; the repository-level setting closes them, and tightening the list to merge alone would make the rule self-contained.
- Branches: `q1q4` had already been deleted by the typist. With the typist's authorization the council deleted the four merged `codex/*` branches, remotely and locally, after measuring that each head is an ancestor of `main`.
- Entity track: verifying that the articles carry the 501(c)(3) purpose and dissolution clauses, and filing the SS-4 after acceptance, are not the council's acts. The articles are not in this repository, and a filing is a typist's or counsel's act. Nothing was done and nothing is claimed.

## Future work, in authority order

1. Typist: replace the ruleset's required check contexts with the four check-run names above and tighten its merge-method list; supply the SPF and DKIM proof for the letters; begin the second-maintainer attempt; and carry the entity track as Maryland answers.
2. Product owner: ratify or reject ADR-007; decide the `recipeHash` forward route, which alone unlocks the held item 5.
3. Multilingual seat: supply and review one exact real chrome catalog.
4. Council and product owner: run the needs-first Atlas session and record the written disposition.
5. AAC/SLP, rights, and accessibility seats: review the Mulberry and OpenMoji candidates asset by asset.
6. Forge, when the typist next says so: run the console-signal instrument against equivalent load whenever a hosted receipt records a new observation, and seek reproduction before any causal fix, timeout change, or closure claim; keep the ledger appended and the register exhaustive.
7. Everyone: complete human keyboard and AT evidence, print inspection, the six-week staff pilot, rights review, and every remaining release gate.
8. Forge candidate for the next menu, not authorized here: measure what differs between the two parent contexts in N-02 before proposing any change to the console exercise, its sender, or its deadlines.
