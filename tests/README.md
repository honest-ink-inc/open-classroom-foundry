# Test suites

Created during Days 16–30; directories exist now so the evidence structure is visible from the first commit.

| Suite | Proves |
|---|---|
| `Unit` | Domain and application behavior, including the 75 Writer's Kiosk characterization tests preserved through Release 0.0 |
| `Contract` | Schemas, recipe manifests, migrations, provider capability tests (fake provider: malformed JSON, refusal, truncation, timeouts, 401/403/429/5xx, cancellation) |
| `Integration` | End-to-end state machine paths, purge on completion and cancellation, storage round-trips |
| `Rendering` | Deterministic byte-identical rendering, dimensional-geometry fixtures (±0.2 mm), print regressions (Letter/A4, duplex, grayscale, low ink), imposition proofs |
| `Accessibility` | Keyboard operation, UI Automation exposure, zoom/contrast, reading order in digital outputs |
| `UiAutomation` | Scripted walkthroughs of capture→approve→print paths, including camera simulator and virtual print sink |
| `InstructionalEvals` | Module invariants on the seven fixture families (implementation plan §12); model self-ratings are never release evidence |

**Fixture rule, absolute:** all fixtures are synthetic, teacher-authored, public-domain, or openly licensed OER, marked as such with provenance. No student work or identifying classroom material — see CONTRIBUTING.md.
