# Existing-PR audit and asynchronous fixture-wait repair

**Prepared:** 5 September 2026 (America/New_York).
**Working branch:** `codex/fixture-disposal-follow-up`.
**Starting source:** `db40e9b673bceb65cd60d0e0fb88653b02ce66c6`.

The typist confirmed the update succeeded and explicitly resumed work, asking
that prior improvements be perfected before new improvements. This supersedes
the [update checkpoint](2026-09-05-fixture-disposal-follow-up.md) for current
navigation only. Its record and detached manifest remain frozen; its pause
instruction described that completed checkpoint, not a continuing prohibition.
The [complete work register](../governance/accepted-improvement-register.md)
remains authoritative for unfinished engineering and human dependencies.

## Measured integration, not an inference from open-item badges

The initial live refresh confirmed ten dependent open PRs, three with successful
CI and seven with failed CI. Open status alone does not establish a defect:
the stack includes advisory maintenance, compatibility-held production changes,
drafts, and genuine hosted failures. Independent engineering audits are
assistant reviews, not a real educator council or protected-seat approval.

PR15, advisory-only at `554ec87b256c5cbd8f6efba453070f27941c9257`, was merged
normally at `2026-09-06T02:00:01Z`. The resulting exact main is
`39897d2b58884d000398c5178e87e174f82f5402`, with prior main
`965a59abb80a4f1671f34d13ee34f82cb7f5a624` as first parent and the exact PR15
head as second parent. Its own-head hosted conclusions were read before merge;
the [evidence ledger](../evidence/evidence-ledger.json) retains those runs.
~~The resulting exact-main runs are pending at this preparation cutoff.~~
**Struck later 5 September 2026:** both exact-main workflows succeeded; actual
seven-suite results are 2,346/2,346 and actual CodeQL SARIF has zero findings.
The ledger retains the directly read conclusions and separate artifact hashes.

Deleting PR15's merged base closed its dependent PR16. A base change while
closed was refused. Recovery restored the exact old base, reopened PR16,
retargeted it to `main`, then deleted the obsolete base. PR16 is open with
unchanged head `bb788aa8d74fc3e230b6712876a6ea6c3ccce9d5`. No production
compatibility hold was removed. The newly explicit stacked-PR sequence in
[AGENTS.md](../../AGENTS.md) records this previously unspecified ordering trap.
The original capture and separate recovery observations remain under
`out/pr-stack-resumption/`; no failed observation was overwritten.

## Bounded repair and pre-commit correction

PR24's failed CI and successful CodeQL have been retained in the ledger and
the new S-18 [sighting](../evidence/sightings-register.md). Its helper correctly
refused later fixtures after unresolved capture ownership. The additional
follow-up observations do not establish later capture completion or an OS cause.

A controlled synchronous-facade experiment now reproduces a caller-yield
failure twice: the caller cannot release its exact queued disposal work before
the synchronous wait consumes its budget. The actual failure and exact owned
task cleanup are retained under `out/fixture-async-waits/`. This is a local
measurement of the controlled scheduling dependency, not a diagnosis of PR24.

The bounded candidate will make cleanup, drain, cancellation-settlement and
disposal waits genuinely asynchronous, share one non-overlap gate across
synchronous and asynchronous entry points, and await the native fixture caller.
The capture worker isolation and synchronous native root wait remain unchanged.
No deadline increase, discarded failure, unsafe reuse, or production recipe
change belongs to this repair. ~~Implementation and ordered closing evidence
are not yet complete at this preparation cutoff.~~ **Struck later 5 September
2026:** the implemented repair passed its focused pair, 81/81 twice. ~~Full
ordered solution closing remains pending.~~ **Struck later 5 September 2026:**
Release build, format fix/verify, post-format Rebuild, another focused pair and
two full runs all exited 0; the actual full results were 2,638/2,638 each, with
source/assembly identity stable. Final record guards and own-head hosted
verification remain separate. The [new evidence record](../governance/fixture-async-waits.md)
retains the actual red/green measurement, initial build failure and limitations.

**Later 5 September 2026, before committing:** independent review found that a
queued refusal in two new tests' cleanup could mask the original assertion and
skip aggregate observation. The entire candidate was retained; no commit or
push occurred. Four controlled cases then failed in each of two runs. The
narrow test-only correction passed 93/93 focused controls twice and independent
source review; ~~a fresh full closing is pending.~~ **Struck later 5 September
2026:** the corrected ordered solution gates passed, including another 93/93
focused pair and 2,650/2,650 full tests twice, with stable source/assembly identity.
Final record guards and new own-head CI/CodeQL remain separate. Earlier full results remain
historical, not certification of this later test source. The evidence record
retains both additional gate failures and the test-output durability limit.

## Boundaries retained

Seven changed recipe identities and separately classified shared semantics
remain unadmitted. A later test-only PR inherits those held production commits;
its title or a green check cannot authorize a wholesale merge. The next
production integration needs the exact version/compatibility disposition and
outgoing-contract retention required by the ratified C1/C2 record.

**Later 5 September 2026:** the typist [authorized exact compatibility
implementation](../governance/2026-09-05-recipe-replacement-authorization.md):
candidate engine `0.8.0-alpha`, the seven replacement recipes `0.2.0`, evaluation
suites `0.2`, and the calibration instruction-layout repair, while preserving
outgoing contracts. This supplies implementation choices, not final admission
or proof. Finish the independent fixture repair's closing before enacting that
next production slice; no engine/recipe version has changed in this cutoff.

The read-only press census additionally found a booklet-guide LONG-to-SHORT
duplex instruction change under its old identity. The seven-row decision does
not authorize an eighth replacement. That exact disposition is separately
awaiting the typist; no blanket equivalence or version waiver is inferred.

CC BY-SA 4.0 remains a selected proposal, not an operative scope-specific grant.
ADR-007 and ADR-010 remain Proposed, schema 1 remains hash-less, calibration
visual QA is not closed, and native-console evidence remains unadmitted.
Actual consent, custody, withdrawal, appointments, quorum, recusal, and
protected-seat records remain absent; H0–H7 remain NOT BEGUN. No simulated
committee can supply those acts. No site dispatch, version/tag, signing,
installation, distribution, filing or correspondence is enacted here.
