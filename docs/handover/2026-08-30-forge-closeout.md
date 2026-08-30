# Forge closeout — doors opened, floor held, human gates kept

**Date:** 30 August 2026 · **Audience:** the typist, council, District IT, the next maintainer, and any automated contributor. Read [AGENTS.md](../../AGENTS.md) before acting.

This is the current repository state at the commit containing this file. It closes every forge-owned part of the standing directives in [the moved calendar](2026-08-29-the-moved-calendar.md), records what measurement changed, and names the two items that code must not counterfeit as complete. The engine remains `0.7.0-alpha`. No version, tag, signature, installation, distribution, publication, filing, correspondence, district decision, or protected-seat acceptance was performed.

## I. Exact measured state

- The Release solution builds with warnings as errors: **0 warnings, 0 errors**.
- Full `dotnet format` fix completed; the following `--verify-no-changes` exited **0**.
- **890/890 tests passed twice**, with no skip in the local closing runs:

  | Suite | Tests |
  |---|---:|
  | Unit | 493 |
  | Integration | 120 |
  | UI automation, including headed UIA | 125 |
  | Contract | 69 |
  | Rendering | 42 |
  | Instructional evaluations | 34 |
  | Accessibility minimum floor | 7 |
  | **Total** | **890** |

- SampleGenerator ran twice; **40 files** matched byte for byte, with no exclusion.
- The neutral UI-catalog packet contains **978 strings** after the Access hold, review-keyboard, escaped-ampersand, and malformed-Unicode corrections. Two exports were byte-identical: packet SHA-256 `752F0D1654A1C379D6DB52D8C5A40B9AAEDBF9BF33534FF625DD8FB2CDD84B51`, bound to source digest `35186157cfc0c64e94c912539fa0cc9c7370c83f5c335718ce23faf7a31b385c`.
- An intermediate Access-authority design first measured **888/889**, not green, because its new user-facing refusal text bypassed `UiStrings`. That route was then rejected entirely after an adversarial Spanish assessment fixture proved that typed prose cannot establish purpose across languages. The final design holds Access at the catalog and public-API boundaries; this red result and its assertion message were retained rather than rounded into an expectation.
- The first complete closing attempt was red, and its names and messages were retained. It found two real regressions introduced by hardening: old headed commands had not adopted the newly mandatory disposable library, and the supposedly frozen 0.1 snapshot renderer inherited CRLF from its source file. Both were fixed and rerun in isolation before the two complete green runs. A red measurement was not rounded into a green expectation.
- The first GitHub Actions run for commit `b32f062` was also red, in the pseudo-locale 125% floor case. Its trace proved that the hosted desktop had silently clamped the nominal 1366×728 top-level form to a 1044×728 outer window and 1028×689 client area. The exact geometry and failure reproduced locally at 1044×728; the same Press Room passed at the intended 1366×728 and at 1071×728. A first harness correction made the requested outer geometry explicit, and the next hosted run for `a3f71b5` honestly failed that assertion in both locales: WinForms applied the same maximum-track clamp even to the child Form. The final harness never scales, creates, or shows a Form window. It measures only each Form's non-client extent, moves its complete live control tree with z-order, inherited chrome, and mirrored RTL window flags preserved into an unclamped logical client canvas, lays it out at physical-client ÷ scale, scales that canvas, and corrects rounding to the exact physical client. Client plus measured frame must equal the requested outer floor. A local comparison put the key pseudo Press Room text box at the same rectangle as a real 1366×728 window, with only one-pixel internal rounding differences elsewhere; Module Studio also lost an erroneous over-tall list left by the old clamped path. This is a client-layout test-harness repair, not an application-layout diagnosis; headed UIA separately retains real-window coverage.

## II. The forge order, honestly closed

| Standing item | State | What is true now |
|---|---|---|
| Doorless modules | **Struck** | The old count was wrong: Family Bridge made **ten doors**, with **eleven modes** because Scaffold Smith has two. One generated standard-controls studio exposes all of them. Eight modes have synthetic Green starters. Access Remix is visible but has no catalog build delegate or public remixer API pending protected specialist purpose-authority review; Exit Lens and Rubric Relay remain visible and unavailable behind written district authority. |
| Real second UI chrome language | **Human hold — not struck** | The complete deterministic 978-string packet, strict reviewed-catalog loader, LTR/RTL chrome, provenance contract, pseudo stress path, CLI/environment selection, and exact-file compiled allowlist exist. Runtime tests exercise every press, built-in mode, All Aboard mode, Review tab, node editor, vector primitive, and the loaded-project preflight under pseudo RTL. The allowlist is empty because no multilingual-seat-reviewed translation exists. JSON cannot appoint its own reviewer, and a path cannot approve its own bytes. |
| 1366×768 minimum floor | **Struck** | Every shipped surface, all three Review views, every admitted node editor, and every vector primitive layout is exercised at an exact, host-independent 1366×728 working area in neutral 100% and stretched pseudo 125%. A reflection inventory fails if a future production Form lacks a deliberate scenario. Interactive and non-interactive chrome must remain unclipped; reachability through an owning scroll container is accepted only when a real `ScrollControlIntoView` exposes the target and preserves inspection state. The design surface remains 1180×720 and ordinary larger displays remain the intended experience. |
| Atlas, council-first | **Prepared — session hold** | The needs-first [priority-session packet](../council/atlas-priority-session.md) is ready and explicitly unrun. Need capture precedes candidate names; absent protected seats create holds; withdrawal and compensation confirmations, the cohort's still-unenacted decision/quorum record, participant review, and council-record freeze are explicit fields. Feasibility and product-owner disposition remain separate. No atlas rank or newly divined product goal was selected by automation. |

No authorized seat-supplied catalog or council evidence was available through the user-provided sources. No protected material was inspected.

## III. What the teachers can actually reach

The running application now has three first-class doors:

1. **Press Room** — 56 deterministic engines across 23 recipes.
2. **All Aboard** — the complete visual-support studio.
3. **Built-in Studios** — Board to Brief; Access Remix; Directions Duet; Scaffold Smith packet and task-entry modes; Talk Moves Studio; Lesson Loom; Exit Lens; Rubric Relay; Source Lens; and Family Bridge.

The shared studio is catalog-driven, not ten copied forms. Stable submitted values are independent from translated labels. Every available mode owns typed field readers, its recipe validator and warning inventory, fresh Gate B review, audience/scale/language-order render profile, print, print view, declared export targets, and atomic project save. Any field, option, mode, or source change revokes approval and every sink.

Access Remix's deterministic layout engine remains internally testable, but the shipped type, result, and methods are non-public and the catalog mode has no build delegate. All Aboard approvals unlock only All Aboard outputs and remain purpose `Unknown`, including assessment-shaped English and Spanish fixtures. No raw document, draft, generic module approval, keyboard declaration, mutable package purpose field, saved package, or typed claim can enter Access through the application or its public engine surface. The hold remains until protected specialist review establishes a purpose-authority route that cannot be granted from typed content.

## IV. Gate B and package trust were made exact

Gate B now has three keyboard-reachable views: semantic elements, exact source comparison when source exists, and a visibly watermarked unapproved visual derivative made by the same HTML/SVG semantic core as final output. Assigning `WebBrowser.DocumentText` is not readiness. Approval remains disabled until `DocumentCompleted` exposes a browser-only SHA-256 marker binding the exact revision, render request, derivative bytes, and load generation. Edit, refresh, navigation, error, marker mismatch, and stale completion revoke readiness. The marker enters neither approved output nor the semantic derivative.

The `.ocfproj` reader now treats manifest, semantic artifact, snapshot, render profile, validation envelope, assets, provenance, entry inventory, and exact renderer identity as one hostile package. A safe but unrelated snapshot, active content, unknown renderer, forged warning text, metadata refresh, duplicate entry, traversal, size/inflation excess, asset mismatch, or half-context is refused. Saved validation and render profiles bind to the exact document SHA-256. Package saves stage to one unique sibling, flush through the file handle, fully revalidate, then atomically replace or move; failure and cancellation preserve the prior destination and remove the bounded stage.

The compatibility boundary keeps a frozen exact renderer for the admitted `0.1.0-dev` package and the current renderer for current packages. The first closing run proved why “frozen” must include source-line-ending independence: a raw CSS literal acquired CRLF under the repository checkout policy and no longer matched the historical LF bytes. The legacy writer now spells its newlines explicitly; 26 package-upgrade cases and 10 operator-host cases pass, **36/36** focused upgrade-related integration cases in all.

## V. The repository compatibility-preparation seam is complete; a managed upgrade path is not

[ADR-007](../adr/ADR-007-managed-pilot-upgrades-are-side-by-side.md) remains **Proposed**. The [runbook](../release/managed-pilot-upgrade-runbook.md), strict plan schema, and `Foundry.Tools.ProjectUpgradeHost` provide two operator-controlled operations:

- `review` canonicalizes and validates an exact closed plan and reports its SHA-256;
- `prepare` requires that unchanged digest and creates a new candidate library without changing the source.

Each source is addressed by root, relative path, engine, schema, and SHA-256 and held open read-only across address, hash, full validation, copy/compatible transform, and final source check. Source and candidate roots must exist, be canonical, non-reparse, separate and non-overlapping; the candidate begins empty. A batch lock excludes another engine batch, every partial is bounded, completed output is fully validated before atomic promotion, one batch failure removes every output created by that batch, and cleanup residue reports a content-free stable code.

`tools/publish.ps1` publishes only the WinForms application; no separately authorized operator package for the host exists. The runbook now gives an explicit source-tree evaluation invocation without representing that command as an installed tool. This code does **not** choose a candidate application, ratify the ADR, install or uninstall, assign Intune policy, sign, version, launch, replace the live library, retire the prior library, or decide rollback. Those remain typist and District IT acts, and a managed-device upgrade and rollback remain unratified and unproven.

## VI. Recorded debts and sightings

- **Accessibility suite:** empty-suite debt struck. It now holds seven machine tests for the exact minimum-floor contract and passed twice. This is not NVDA/Narrator, contrast or high-contrast, 200% zoom, human reading-order, or ACR/VPAT evidence; those remain owned human accessibility work.
- **Deprecated Node 20 actions:** debt struck. Every workflow action is exact-commit pinned to its Node 24 generation. Repository tools are manifest-pinned, CI has read-only contents permission, and it now retains a direct/transitive vulnerability audit and full CycloneDX 1.7 JSON SBOM. The measured local SBOM contained 34 components, all 34 with license evidence, and an exact restored-assets graph of 35 nodes and 51 edges; all 34 dependency components were reachable from the application root. The site remains `workflow_dispatch` only.
- **Image flake:** diagnosed and fixed, no longer merely a sighting. Official .NET 10 source corroborates the unsynchronized encoder-inventory publication path: `Image.Save(stream, ImageFormat.*)` could observe a default encoder GUID and throw `ArgumentNullException (encoder)`. Production and integration fixtures now resolve one explicit codec through a thread-safe frozen cache, and the current regression passed 20/20 fresh testhost processes. The historical notes record **2/20** before and **0/50** after the fix, but their raw per-process logs were not retained; a 64-way regression remains.
- **Two historical headed UIA timeouts:** still sightings, not diagnoses. Both passed in isolation when recorded, never reproduced in the 57-run CI history inspected, and did not recur in either 890-test closing run. Their names remain in the moved calendar; the `PilotDay` timeout message was captured, while the `Part3` message was not retained. The shared timeout remains 20 seconds and was not increased.

The final adversarial pass found three different deterministic UIA-harness defects, which must not be mislabeled as explanations for those historical sightings:

1. storage-capable harness modes could inherit the real default teacher library when the disposable root was omitted;
2. missing or misspelled harness modes could fall through into the production Press Room; and
3. `--export-to` could bypass Save As and overwrite an arbitrary PDF path.

All three now fail closed. Press Room, All Aboard, and module harnesses require an exact empty `%TEMP%\ocf-rehearsal-{GUID}\0.7.0-alpha\prepared-library`; one exact known harness mode is mandatory; and direct export accepts only one new fully qualified `.pdf` directly inside that validated empty root. Diagnostics contain only the exception type, never a message, path, stack, or authored text.

## VII. Other hardening earned during the forge

- Every `ArtifactDocument` recursively freezes its node collections; edits create new revisions and cannot mutate an approved graph by alias.
- Language-bearing documents, bilingual pairs, StepRows, and builders share a bounded deterministic BCP-47 grammar covering language, extlang, script, region, unique variants, unique extensions, and terminal private use. Malformed keyboard tags block before Gate B.
- Directions Duet, Family Bridge, and Glossary Garden say **working glossary — not approved** and **not yet language-reviewed**. No text field manufactures review status.
- Reopened projects default to Amber until the teacher completes a three-part exact-document Green preflight. Package lane, recipe, module, notice, and purpose claims are not trusted provenance; origin warnings are engine-owned and require acknowledgement.
- Edge PDF export uses an isolated local profile, bounded stable-file completion, process-family cleanup, and retrying residue cleanup. It refuses remote or active content and preserves render options.
- The UI Automation harness now expects the Review tab control, selected tab, and all three tab items, and its content-free failures retain the test name and assertion message in the runner evidence.
- A real out-of-process UIA walk now enters Built-in Studios through the production Press Room modal, enumerates all ten doors and eleven modes, proves Access/Exit/Rubric cannot grant themselves authority, and opens real Gate B before the four output sinks unlock. No timeout was increased and no sink was invoked.

## VIII. What remains, and whose act it is

1. **Multilingual seat:** supply, provenance, review in context, and approve an actual second chrome catalog. Only then may an authorized source review add its exact SHA-256 to the build allowlist.
2. **Council and product owner:** run the needs-first Atlas session, record present and absent seats, then create a separate feasibility record and written disposition. The template is not the meeting.
3. **District IT / product owner:** evaluate and ratify ADR-007 for an exact managed deployment; test installation, rollback, detection, signatures, and device policy outside this repository.
4. **Typist:** version, tag, sign, install, distribute, publish, file, send correspondence, and perform every outward-facing release act.
5. **Accessibility/AAC, multilingual, district, privacy/records, safeguarding, curriculum, and rights seats:** retain every ordinary gate. Time does not waive territory.
6. **Project:** seat the second maintainer, run the pilot, and keep the two historical UIA sightings named until measurement earns a diagnosis.

The forge has no remaining code-owned standing item hidden behind those human holds. Honest Ink is more capable, but its strongest improvement is that it now has fewer ways to claim capability, review, provenance, readiness, or authority that it has not actually earned.
