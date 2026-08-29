# Contributing

Thank you for helping build free tools for teachers. The project is in its charter phase — contributions right now are mostly review, co-design, and fixtures, not code.

## The one rule that outranks everything

**No student work, student data, or identifying classroom material ever enters this repository.** Not in code, tests, fixtures, issues, pull requests, screenshots, discussion, or documentation. All fixtures and examples must be synthetic, teacher-authored, public-domain, or openly licensed OER — and marked as such. If you accidentally include such material, report it privately per [SECURITY.md](SECURITY.md) immediately; do not push a "fix" commit that leaves it in history.

## What contributions are welcome now

- **Document review** — the [idea atlas](docs/idea-atlas.md), [implementation plan](docs/implementation-plan.md), and [module specifications](docs/modules/) all benefit from teacher, specialist, and engineer eyes. Open an issue per finding.
- **Educator council** — teachers willing to do think-aloud sessions and co-design (see [GOVERNANCE.md](GOVERNANCE.md) for the recruiting roles).
- **Fixture authoring** — synthetic classroom materials for the seven fixture families (implementation plan §12): gold, ambiguity, privacy-adversarial, pedagogical edges, language, accessibility/rendering, rights/provenance.
- **Specialist review** — accessibility, AAC, multilingual, privacy, and OER-licensing expertise, per the governance roles.

Code contributions begin with Release 0.0; watch the ADRs and the implementation plan's Days 16–30 milestones.

## Ground rules

1. **Licensing is inbound = outbound.** Code and first-party recipes are contributed under GPL-3.0-or-later. Documentation and printable content follow the project's declared content license once chosen (implementation plan §9). By contributing you certify you have the right to submit the work under those terms.
2. **Provenance or it doesn't ship.** Every asset (image, font, symbol, translation, template) carries source, creator, license, and modification history in the asset ledger. Unknown rights block distribution — CI will enforce this.
3. **Bounded modules, structured output.** No contribution may add a "generate anything" surface, an unapproved network call, executable plugin loading, or a path that renders, prints, exports, or saves unapproved draft content (ADR-001, ADR-004).
4. **Accessibility is not a later.** UI contributions use standard controls (ADR-002) and keep keyboard operation and UI Automation exposure intact.
5. **Honest claims only.** No "FERPA compliant," "de-identified," "certified translation," or "accessible" claims without the corresponding evidence artifacts.

## Practicalities

- Discuss substantive changes in an issue before a large pull request.
- Keep commits focused; reference the ADR or plan section your change serves.
- The implementation plan's stop-ship conditions (§19) are non-negotiable review criteria.
