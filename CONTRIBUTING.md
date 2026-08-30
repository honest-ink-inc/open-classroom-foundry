# Contributing

Thank you for helping build free tools for teachers. The project is in its charter phase — contributions right now are mostly review, co-design, and fixtures, not code.

## The rules that outrank everything

Three kinds of thing must never enter this repository. The first is the oldest and still the gravest; the other two were learned here, the hard way.

**1. No student work, student data, or identifying classroom material.** Not in code, tests, fixtures, issues, pull requests, screenshots, discussion, or documentation. All fixtures and examples must be synthetic, teacher-authored, public-domain, or openly licensed OER — and marked as such. If you accidentally include such material, report it privately per [SECURITY.md](SECURITY.md) immediately; do not push a "fix" commit that leaves it in history.

**2. No credentials.** API keys, tokens, passwords, connection strings, private keys, certificates, `.env` files — none of it, not even briefly, not even in a branch. Git history is permanent and **this repository is public**: a committed key is a burned key even if the next commit removes it. Rotate it; do not merely delete it.

**3. No blind-study instruments.** The seeded-error study’s definitions and its facilitator key stay with the facilitator, outside this repository (see [the pilot kit](docs/evidence/pilot-kit/README.md)). These are not secrets in a security sense; they are secrets in a *research* sense, and the harm is quieter: **a participant who has seen the key is trained, not testing.** The study gates the pilot, so leaking it costs the evidence, not the data.

### And one question that is not about what enters

Before anything becomes **newly visible** — repository visibility, a release, the site, a public filing — run the pre-publication check in the [1.0 hardening checklist](docs/release/hardening-checklist.md). Content that is harmless in a private repository can be harmful the moment it is public, and **publishing publishes the whole history, not the working tree**. That check exists because this project once made a repository public with a study answer key inside it.

### These are guarded by machinery, not goodwill

`.gitignore` refuses the obvious filenames; a **pre-commit hook** scans staged changes and refuses the commit before history exists; `RepositoryHygieneTests` fails the build if a credential or answer key is ever tracked; and CI scans the full history on every push. Install the hook in every working copy — hooks are per-clone and are never inherited:

```
pwsh tools/install-hooks.ps1
```

If the hook refuses your commit, it is working. Fix the content, never the hook.

**Automated contributors** — LLM agents committing on the project’s behalf — are bound by all of the above and by [AGENTS.md](AGENTS.md), which additionally names the acts an agent must never perform without a human: changing repository visibility, publishing, tagging or distributing a release, sending correspondence, and filing anything public.

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
