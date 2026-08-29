# 0.1-alpha evidence bundle

**Date:** 29 August 2026 · **Commit basis:** the Days 76–90 vertical slice
**Standing:** This bundle records what is machine-verified and what remains human-owned. It claims nothing a test does not prove, and it is not a release: Release 0.1 requires the open human items below.

## What the slice proves end to end

The full grammar runs under test with no hardware and no cloud: *teacher intent → Gate A exact-payload preview → confirmed egress → (synthetic) provider → strict structured parse → deterministic builder → Gate B review with edit loop → typed approval → accessible and print rendering → provenance-carrying .ocfproj → reopen.* A model suggestion is a proposal into the same review surface a teacher-typed strip uses; nothing a provider returns can print, save, or export without the teacher's named approval of an exact revision.

## Machine-verified evidence (209 tests, 0 warnings, warnings-as-errors build)

| Suite | Tests | What it proves |
|---|---:|---|
| Unit | 94 | Lanes, approval gate, state machine, document validation, locked fields, diagnostics policy, review session, builders, architecture rules |
| Contract | 48 | Gate A structural enforcement, provider failure taxonomy, suggestion flow, prompt-injection red team |
| Rendering | 12 | Escaping, audience separation, bilingual lang/dir semantics, RTL, CJK, long-translation, determinism, print stylesheet |
| Integration | 24 | Image normalization (EXIF strip, crop, rotate, redaction burn), policy fail-closed, .ocfproj round trip, asset integrity, cancellation/purge, PII canary |
| InstructionalEvals | 31 | The 30 staged-task fixtures plus corpus breadth |

## The thirty staged-task fixtures (plan §10.1 acceptance)

Thirty strips across **ten subjects** (science, math, literacy, art, PE, music, CTE, library, routines, SEL) and real school environments; **four bilingual fixtures across three language pairs** (English–Spanish, English–Arabic right-to-left, English–Chinese); **two symbol-bearing fixtures** against the shipped CC0 pack. Every fixture builds, validates without blocking issues, approves, renders for learner screen and paper, and — where bilingual — carries both languages with correct `lang` attributes.

## Red team summary

- **Prompt injection** (7-string hostile corpus × full pipeline): instruction-shaped text in a suggested step survives as inert, visible, escaped data — the teacher sees the attack verbatim in review, and the rendering contains no executable tag, attribute, or link. Extra JSON fields ("toolCall", "admin") are malformed output, not obeyed instructions. The Gate A preview shows hostile payloads verbatim before any egress. The provider interface is structurally minimal: two methods, no tools, no state, no secrets.
- **PII canary**: a distinctive marker captured into the session is unrecoverable after purge; it cannot enter diagnostics through any field (the content-free policy rejects prose-shaped identifiers loudly); an Amber artifact carrying it is refused persistence.

## Canonical samples (in `samples/`)

- `task-strip-bilingual.learner.html` / `.teacher.html` — the same approved artifact for each audience; the teacher view alone carries prompt-fade-adjacent notes and the approval footer
- `task-strip-bilingual.ocfproj` — a real package: manifest, polymorphic artifact JSON, both referenced symbols with their provenance records, and the self-contained snapshot
- `agency-cards.learner.html` — the full seven-card agency deck from the CC0 pack
- `first-then.print.html` — print-ready output with the paper stylesheet

## Open items — human-owned, blocking Release 0.1

| Item | Owner | Notes |
|---|---|---|
| Keyboard and NVDA/Narrator walkthrough of the review surface | Typist, then AT reviewer seat | ReviewForm is a standard-controls prototype; the walkthrough is the first tripwire-adjacent human gate |
| Teacher think-aloud sessions (time-to-artifact, edit burden) | Typist + educator council | Council formation tabling ends here — recruitment is now due (GOVERNANCE.md tripwire 2) |
| Physical print inspection | Typist | Samples exist; paper is the judge |
| Symbol recognizability review | Educator council (AAC/SLP seat) | The seven CC0 symbols were delivered for eyeballing; ambiguity notes recorded |
| Seeded-error study | Typist + council | Required before any pilot (plan §7, automation-bias control) |
| Live provider capability test | Typist + district IT | Synthetic covers CI; a real deployment needs Gate 3 attestation |

## Go/no-go on All Aboard expansion

**Da Vinci's recommendation: GO**, conditioned on the human items above. Every architectural promise made since the review — structural approval, structural egress preview, lane fail-closed, content-free diagnostics, provenance-or-nothing, teacher-owned suggestions — now exists as passing tests rather than prose. The risks that remain are exactly the ones the plan assigned to humans, which is where they belong.
