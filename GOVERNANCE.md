# Governance

This document records how decisions are made in Open Classroom Foundry. It exists from the first commit because a liberation project that dies with its maintainer liberates no one (Master's Review, finding F11).

## Roles

| Role | Accountable work | Currently held by |
|---|---|---|
| Product owner / master teacher | Purpose, instructional constitution, module priorities, classroom usability | The founding maintainer |
| Lead developer / maintainer | Architecture, implementation, CI, releases, vulnerabilities, open-source stewardship | The founding maintainer |
| District IT/security | Tenant, endpoint, RBAC, egress, deployment, signing, incident and rollback | Vacant — engaged per deployment |
| Privacy/legal/records | Lane decisions, FERPA analysis, retention, deletion, contracts | Vacant — engaged before any Amber work |
| Curriculum/content reviewers | Target fidelity, facts, standards, subject fixtures, pedagogical evaluation | Recruiting (educator council) |
| Accessibility/AT reviewers | Keyboard, screen reader, outputs, cognitive access, ACR/VPAT | Recruiting |
| AAC users / SLP / special educators | Visual-support agency, terminology, symbol/AAC boundaries | Recruiting — required before All Aboard co-design |
| Multilingual services / family liaisons | Translation review, family clarity, localization | Recruiting |
| OER/license steward | Asset and dependency rights, provenance, attribution, takedown | The founding maintainer, until delegated |
| Safeguarding leads | Direct-source procedure, alert boundaries, training | Vacant — engaged per deployment |
| Teacher pilot council | Think-alouds, time-on-task, edit burden, classroom fit | Recruiting |

**No single role may waive another role's critical gate.** The full role contract is implementation plan §15; stage gates and their required evidence are §16; stop-ship conditions are §19.

## How decisions are recorded

Architectural and product decisions live in [docs/adr/](docs/adr/) as numbered architecture decision records. An ADR is Proposed until the product owner ratifies it; ratified ADRs bind until superseded by a later ADR. The governing documents (atlas, implementation plan, module specifications) are versioned; the copies in [docs/](docs/) are canonical for this repository from this commit forward.

## Sustainability commitments

- A contribution guide exists from the first commit and is tested by an outside contributor before Release 0.3.
- A second maintainer is named — or a recruitment attempt is documented — by Release 0.3 (Definition of Done, Sustainability).
- Recipe packs are designed to be maintainable by curriculum-literate contributors who are not the lead developer.
- Every release ships complete corresponding source, build scripts, an SBOM, and an asset ledger, so the project remains buildable by strangers.
- The end-of-life promise (implementation plan §20, decision 12) is decided before any district deployment: if the project is abandoned, the last release's projects remain openable, editable, and exportable forever.

## Changing this document

Changes to governance itself require an ADR ratified by the product owner and, once a second maintainer exists, their concurrence.
