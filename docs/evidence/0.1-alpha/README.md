# 0.1-alpha evidence bundle — historical index

**Original date:** 29 August 2026 · **Original slice commit:** `843cf670664823c5b68b0b232d997820775050e3` (Days 76–90 vertical slice)

**Tracked HTML sample refresh:** `ca3f3d6a1f6c40aca5521a81ec5e5e22cba8dc59` (later Release 0.4 step-row and 13-symbol regeneration)

**Standing:** HISTORICAL / MIXED REVISION. The 209-test table below records the original slice; it is not the current test count. The tracked HTML files in `samples/` were regenerated later and are not immutable bytes from the original slice. This directory is not a release, a current-state dashboard, a protected-seat review, or council ratification. Current requirement status lives in the [release traceability matrix](../../release/release-requirement-test-traceability.md), and current repository state lives in the handover marked as such in [the governing-documents index](../../README.md).

## What the original slice proved end to end

The original tests exercised the grammar with no hardware and no cloud: *teacher intent → Gate A exact-payload preview → confirmed egress → synthetic provider → strict structured parse → deterministic builder → Gate B review with edit loop → typed approval → accessible and print rendering → provenance-carrying `.ocfproj` → reopen.* A model suggestion entered the same review surface as a teacher-typed strip; the tested structural gate did not permit provider output to print, save, or export without named approval of an exact revision.

That was machine evidence about the original implementation and synthetic inputs. It did not establish instructional quality, translation correctness, symbol recognizability, assistive-technology usability, physical print quality, or classroom value.

## Historical machine evidence at the original slice (209 tests)

The original record reported 209 tests, a warnings-as-errors build, and these suite counts:

| Suite | Tests | What the original slice tested |
|---|---:|---|
| Unit | 94 | Lanes, approval gate, state machine, document validation, locked fields, diagnostics policy, review session, builders, architecture rules |
| Contract | 48 | Gate A structural enforcement, provider failure taxonomy, suggestion flow, prompt-injection red team |
| Rendering | 12 | Escaping, audience separation, bilingual `lang`/`dir` semantics, RTL, CJK, long fixture strings, determinism, print stylesheet |
| Integration | 24 | Image normalization (EXIF strip, crop, rotate, redaction burn), policy fail-closed, `.ocfproj` round trip, asset integrity, cancellation/purge, PII canary |
| Instructional Evals | 31 | The 30 staged-task fixtures plus corpus breadth |

Do not transpose those counts or conclusions onto a later tree. The current handover and evidence ledger carry later commit-scoped measurements.

## The thirty staged-task fixtures

The original corpus contained thirty synthetic strips across ten subject labels and school contexts; four bilingual fixtures used English–Spanish, English–Arabic, and English–Chinese strings; two fixtures referenced the shipped original symbol pack. The automated path built, validated, approved, and rendered the fixtures and checked structural language metadata. The multilingual strings were fixture content, not qualified translations or evidence from the multilingual seat. Generic Gate B approval was not language review.

## Historical red-team summary

- **Prompt injection** (seven hostile strings through the original pipeline): instruction-shaped fixture text survived as inert, visible, escaped data; extra JSON fields were malformed output rather than commands. This was structural test evidence, not an external security assessment.
- **PII canary:** the original tests reported that a distinctive synthetic marker was unrecoverable after purge, could not enter content-free diagnostics, and caused Amber persistence refusal. This was synthetic test evidence, not a claim about operating-system residue or a district deployment.

## What a repository clone actually carries in `samples/`

- `task-strip-bilingual.learner.html` and `task-strip-bilingual.teacher.html` — tracked HTML outputs later regenerated with semantic step rows. Their Spanish strings are synthetic fixture content and have **not** been recorded as reviewed by the multilingual seat. The legacy teacher HTML has no unreviewed-language notice; its approval footer proves only the generic sample approval boundary.
- `agency-cards.learner.html` — a later-regenerated **13-card** learner sample. It uses semantic placeholders and labels; it does not establish that the symbol visuals or meanings are recognizable, AAC/SLP-approved, or suitable for every context.
- `first-then.print.html` — a tracked print-HTML fixture; physical output remains subject to the print-inspection instrument.

No `.ocfproj` package is tracked in this directory. The generator and the checked-in sample-hash contract exercise a file named `task-strip-bilingual.ocfproj`, but `.gitignore` deliberately excludes generated `*.ocfproj` files. A local ignored copy is not part of the published repository evidence and must not be described as though a clone contains it. A future evidence bundle may link a retained CI artifact and exact digest, or may adopt a separately reviewed safe fixture encoding; this historical index does neither.

## Open human and protected-seat items

This historical bundle closes none of these rows. A later artifact may close one only through the current traceability and release-evidence process.

| Item | Owner | Boundary |
|---|---|---|
| Keyboard and NVDA/Narrator walkthrough of the review surface | Typist preparation, then AT reviewer seat | Machine UIA structure does not prove actual speech or first-use comprehension |
| Teacher think-aloud sessions | Typist + educator council | Time-to-artifact, edit burden, and classroom value remain human evidence |
| Physical print inspection | Educator seat + typist for mechanical checks | Paper, printer, geometry, and handling must be measured physically |
| Symbol meaning and recognizability | AAC/SLP/special-educator seat | The current 13 originals have provenance and byte checks, not protected meaning approval |
| Bilingual fixture review | Multilingual educator or family-liaison seat for the exact pair/script | Synthetic strings and structural `lang`/`dir` tests do not prove translation quality |
| Seeded-error study | Typist + council | Required before the classroom-output rung; private definitions and key stay outside the repository |
| Live provider capability test | Typist + district IT | Synthetic provider coverage does not establish a deployment attestation |

## Historical recommendation status

The original bundle ended with a generator-authored “GO, conditioned on the human items” recommendation. It was not a real educator-council recommendation, vote, protected-seat finding, product-owner release decision, or permission to publish. The current honest disposition is the one in the release traceability matrix: substantial machine structure exists, while every named human and protected-seat remainder stays open until the authorized people produce its evidence.
