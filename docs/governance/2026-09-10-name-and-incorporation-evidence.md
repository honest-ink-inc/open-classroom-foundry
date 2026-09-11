# Name change and incorporation — measured hosted evidence

**Date:** 10 September 2026 · **Covers:** pull requests 26, 27 and 28 · **Kind:** record only

This record closes rite 6 for three merges. It adds nothing to the working tree but the [evidence ledger](../evidence/evidence-ledger.json) rows it explains, and it decides nothing. Every conclusion below was read from the run's own record through the GitHub API, never from a neighbouring run, a green pull-request run, or a watcher's exit code — the failure mode this repository has already paid for twice.

## Why pull request 26 appears here, late

Pull request 26 was itself the record-only closure for pull request 25, and it merged on 6 September 2026 without its own rows ever being written. A record-only commission that does not record itself is the recursion this repository keeps walking into: the closure earns checks, and those checks then need a closure. Leaving the gap open would have put a silent hole in an append-only ledger between pull request 25 and pull request 27, so its four rows are added now. They are dated by measurement, not by the date they were typed, which is why they sort before the September 10 rows rather than at the end.

## What was measured

Seventeen rows. For each pull request: the head's own conclusions, the regular merge, and the exact-`main` conclusions after it. Every CI conclusion carries the run's own receipt identifier and its own test counts, extracted from the run log rather than assumed from the suite size.

| Pull request | Head | Head CI / CodeQL | Merge | Exact-`main` CI / CodeQL |
|---|---|---|---|---|
| 26 — stacked integration record | `af719e6` | `34066644809` / `34066644811` | `eca454d` | `34067570994` / `34067570935` |
| 27 — the working title retired | `a951960` | `34554207308` / `34554207247` | `d9d928c` | `34555179347` / `34555179322` |
| 28 — incorporation recorded | `18faa8d`, then `5075644` | `34556185887` / `34556185933`, then `34557360113` / `34557360135` | `3494e75` | `34558192387` / `34558192443` |

All seventeen concluded success. Every CI run reported **3,078 of 3,078 tests passed, zero failed**.

Pull request 28 carries two heads because a fact arrived mid-review: `18faa8d` recorded the incorporation, and `5075644` corrected it after the typist reported that Form 1023-EZ had been filed — the earlier commit asserted it had not been. Both heads' conclusions are recorded. The first is superseded as a head, not withdrawn as a measurement; a green run on a head that no longer exists certifies nothing about the head that replaced it, which is precisely why both are here and why the merge was read separately.

## The local gap, stated rather than papered over

`pwsh` is absent from the workstation these changes were written on, so 78 of the 3,078 tests could not run locally: 50 in `CiTestRunnerContractTests`, 21 in `RecipeRatificationHistoryVerifierTests`, 6 in `LoadReproEvidenceTests`, and 1 in `FixtureProcessRunnerTests`, each failing on a `Win32Exception` starting the process. A 79th test failed locally — `ProjectUpgradeOperatorHostTests.Real_console_ctrl_c_cancels_the_process_and_cleans_the_synthetic_batch`, matched by test name, assertion message and source line to open sighting **S-21** in the [sightings register](../evidence/sightings-register.md). Local runs therefore read 2,999 passed and 79 failed, six times, with identical failure sets.

The hosted runs above are the measurement of record for those 78, and they passed there. S-21 again did not reproduce hosted, which is the register's standing observation about it and is not a repair. No timeout was raised, no test was rewritten to call `powershell.exe`, and no sighting was closed by a passing rerun.

## The site

The repository was renamed on 10 September 2026, and GitHub does not redirect project GitHub Pages URLs. The old project address stopped resolving at the rename; the typist dispatched `site.yml` — a `workflow_dispatch`-only workflow that no agent runs — as run `34555212626` on `d9d928c`, which concluded success, and `https://honest-ink-inc.github.io/honest-ink/` was then measured returning 200 with the correct heading and no occurrence of the retired slug. That run has no ledger row because the ledger's format carries hosted CI, hosted CodeQL and merges only; inventing a fourth kind to hold one run would change the format rather than record a fact, so it is recorded here instead.

Pull request 28 was not republished, and does not need to be. `SiteBuilder.Pages` publishes six documents — `README.md`, `GOVERNANCE.md`, `CONTRIBUTING.md`, `SECURITY.md`, `NOTICE.md` and the Deterministic Press specification — and pull request 28 changed none of them. The site was rebuilt from `3494e75` and all seven generated pages compared **byte-identical by SHA-256** to the live bytes, so the published site and `main` agree without a further dispatch.

## This record's own first head failed, and that is recorded too

The pull request carrying these rows concluded **failure** on its own first head, `5fb6907`, in CI run `34559521423` attempt 1: 3,077 of 3,078 passed. One test failed in teardown rather than in assertion — `RecipeRatificationHistoryVerifierTests.A_merge_commit_cannot_substitute_for_the_single_parent_record_only_C2` threw `System.IO.IOException` from `GitFixture.Dispose()` because another process still held the temporary Git repository the fixture was removing. Every assertion in that test passed; the verifier behaved correctly.

That is a new signature, opened as **S-22** in the [sightings register](../evidence/sightings-register.md), and its CI and CodeQL rows are in the ledger alongside the seventeen above. The run was deliberately **not** re-run: re-running mutates a run's own conclusion, and a measurement is not rewritten to make a page read better. Whether the signature reproduces is left to the next head's own run, which is the only honest instrument available for it. No timeout was raised and no disposal was made more forgiving in this change, per the standing rule.

It cannot be reproduced locally at all. PowerShell 7 is absent from the authoring workstation, so all twenty-one cases in that class fail during process start and never reach teardown. This is the second time in one commission that the hosted runner was the only instrument that could see something, and it argues for installing PowerShell 7 before the next one.

## What this record is not

It is not a release, a tag, a distribution, or an admission. It confers no recipe admission, schema ratification, or protected-seat finding, and it moves no gate on the pilot ladder. It does not close S-21, which remains open. It does not establish that Honest Ink, Inc. is tax-exempt: Form 1023-EZ is filed and undetermined, and no determination letter exists. It does not clear the name — ADR-006's counsel checkpoint stands — and it does not resolve the attribution question ADR-011 clause 6 referred to that same checkpoint.
