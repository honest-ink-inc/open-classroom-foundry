# Notices and attributions

This file is the human-readable companion to the asset ledger. Every shipped asset — font, symbol, image, translation, template, or media file — must be recorded here (or in the machine-readable asset manifest it summarizes) with source, creator, license, modifications, and required attribution. **Unknown rights block distribution; CI hard-fails a shipped file without provenance** (implementation plan §9).

## Application code

- **Open Classroom Foundry application code, tests, and first-party recipes** — © the Open Classroom Foundry contributors, licensed under GNU GPL-3.0-or-later ([COPYING](COPYING)). Documentation, original printable content, and assets follow the separate [README licensing boundaries](README.md#licensing); this notice does not extend the GPL to them.
- Descended from **Writer's Kiosk** (https://github.com/Spacejunk-io/writers-kiosk-csharp), audited baseline commit `c2b670b`; inherited components are refactored behind interfaces during Release 0.0 under the same authorship.

## Third-party dependencies

Third-party NuGet dependencies are present and version-locked. The [CI workflow](.github/workflows/ci.yml) generates, verifies, and retains separate repository/build and `Foundry.App.WinForms` `win-x64` dependency-license inventories and CycloneDX SBOMs; the [release traceability matrix](docs/release/release-requirement-test-traceability.md#rights-and-openness) records their measured scope and limits. Those inventories are commit-scoped. Any dependency notice required for distribution must also be summarized here before release.

## Bundled assets

- **Agency symbol pack** (`assets/symbols/`, 13 SVGs: stop, help, help-hand, wait, break, different, not-now, finished, yes, no, more, do-not-know, consent) — original geometric artwork by the Open Classroom Foundry contributors, dedicated to the public domain under **CC0-1.0**. Per-asset core provenance, SHA-256 integrity hashes, intended meanings, and ambiguity notes live in `assets/symbols/manifest.json`; integrity is verified in CI by the asset-catalog tests. The manifest does not yet contain OER/license-steward-approved attribution and modification dispositions, so the open-pack exporter now refuses this catalog rather than interpreting absence as “none.” No words are baked into any graphic — language stays metadata, per the Symbol Commons invariant. *The license for the full symbol design system (implementation plan §20, decision 6) remains an open decision; this test pack's CC0 dedication stands regardless.*

The wider libre symbol core (Symbol Commons) will prefer original, CC0, CC BY, or CC BY-SA artwork; fonts will prefer OFL with notices, naming obligations, and embedding permissions preserved.

## Takedown

Rights concerns about any asset or file in this repository follow the same private channel as security reports (see [SECURITY.md](SECURITY.md)); good-faith reports are acted on with priority and credited unless the reporter prefers otherwise.

## Trademarks

Honest Ink and the current public module display names are ratified identities under ADR-006, ADR-008, and ADR-009, but those decisions are not trademark clearances; counsel review remains a pre-release checkpoint. Open Classroom Foundry remains the working repository and engineering title. PECS® is a registered trademark of Pyramid Educational Consultants; this project makes no PECS alignment, equivalence, certification, or protocol claim (implementation plan, binding corrections).
