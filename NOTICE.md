# Notices and attributions

This file is the human-readable companion to the asset ledger. Every shipped asset — font, symbol, image, translation, template, or media file — must be recorded here (or in the machine-readable asset manifest it summarizes) with source, creator, license, modifications, and required attribution. **Unknown rights block distribution; CI hard-fails a shipped file without provenance** (implementation plan §9).

## Application code

- **Open Classroom Foundry** — © the Open Classroom Foundry contributors, licensed under GNU GPL-3.0-or-later ([COPYING](COPYING)).
- Descended from **Writer's Kiosk** (https://github.com/Spacejunk-io/writers-kiosk-csharp), audited baseline commit `c2b670b`; inherited components are refactored behind interfaces during Release 0.0 under the same authorship.

## Third-party dependencies

None yet. When dependencies are added, the SBOM and dependency-license report are generated in CI, and license-relevant notices are summarized here.

## Bundled assets

- **Agency symbol pack** (`assets/symbols/`, 13 SVGs: stop, help, help-hand, wait, break, different, not-now, finished, yes, no, more, do-not-know, consent) — original geometric artwork by the Open Classroom Foundry contributors, dedicated to the public domain under **CC0-1.0**. Per-asset core provenance, SHA-256 integrity hashes, intended meanings, and ambiguity notes live in `assets/symbols/manifest.json`; integrity is verified in CI by the asset-catalog tests. The manifest does not yet contain OER/license-steward-approved attribution and modification dispositions, so the open-pack exporter now refuses this catalog rather than interpreting absence as “none.” No words are baked into any graphic — language stays metadata, per the Symbol Commons invariant. *The license for the full symbol design system (implementation plan §20, decision 6) remains an open decision; this test pack's CC0 dedication stands regardless.*

The wider libre symbol core (Symbol Commons) will prefer original, CC0, CC BY, or CC BY-SA artwork; fonts will prefer OFL with notices, naming obligations, and embedding permissions preserved.

## Takedown

Rights concerns about any asset or file in this repository follow the same private channel as security reports (see [SECURITY.md](SECURITY.md)); good-faith reports are acted on with priority and credited unless the reporter prefers otherwise.

## Trademarks

All product and module names are working titles, not trademark clearances. PECS® is a registered trademark of Pyramid Educational Consultants; this project makes no PECS alignment, equivalence, certification, or protocol claim (implementation plan, binding corrections).
