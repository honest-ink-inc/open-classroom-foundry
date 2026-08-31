# Mulberry and OpenMoji symbol-source candidate packet

**Status:** PROPOSED / HELD — product-owner direction recorded; no source,
asset, mapping, default, fallback, or AAC meaning adopted

**Measured:** 30 August 2026

**Decision owners:** AAC/SLP seat for meaning, recognizability, mapping, and
fallbacks; OER/licence-steward seat for rights, attribution, modification, and
distribution; accessibility seat for AT and cognitive-access evidence; product
owner for disposition after those reviews

## Purpose and exact boundary

This packet makes the proposed two-family direction reviewable without
pretending that a protected review has occurred. It is evidence and a worksheet,
not an asset allowlist or a semantic registry. The repository still ships only
the existing 13 Honest Ink original symbols. Those originals are machine-checked
for their current provenance and bytes; they are **not** described here as
council-approved or AAC/SLP-approved.

No Mulberry or OpenMoji artwork, metadata corpus, licence text, API credential,
network dependency, concept mapping, family preference, or production source
descriptor is admitted by this packet. OpenSymbols remains an optional discovery
and identifier-interoperability candidate, not a runtime dependency or blanket
licence.

The accurate proposed product language is:

> **Symbol-supported, AAC-aware directions**

Honest Ink must not claim to translate, convert, or generate AAC. A pictogram is
never presumed to have a universal meaning.

## Product-owner source-role hypothesis

These are roles to test, not defaults to encode.

| Candidate source | Proposed strength | Present disposition |
|---|---|---|
| Mulberry | Primary candidate for actions, routines, communication, sequencing, and symbol-supported directions | Held for exact-asset AAC/SLP, rights, and accessibility review |
| OpenMoji | Supplemental candidate for concrete objects, curriculum topics, activities, technology, places, and general interface icons | Held for exact-asset AAC/SLP, rights, and accessibility review |
| Honest Ink originals | Reserved route for agency and safety-critical concepts when neither external family is adequate | Existing 13-symbol pack remains available under its present status; no new original or “council-approved” claim is authorized |

“Primary” and “supplemental” describe the product owner's intended division of
labour. They do not rank a search result, select a symbol, establish a fallback,
or authorize an educator-facing default. Final mappings and fallback rules remain
the AAC/SLP seat's territory.

## Proposed authoring contract

If the protected reviews admit a later implementation, the authoring flow must:

1. Break teacher-written directions into explicit, editable steps.
2. Suggest only reviewed concept-to-symbol mappings.
3. Prefer a currently selected family for visual consistency only after that
   preference mechanism is approved.
4. Identify alternatives from another admitted family explicitly rather than
   switching silently.
5. Require the educator to confirm every word, image, order, and meaning.
6. Preserve exact source, upstream release and key, acquired-byte hash, local
   transformation, licence, attribution, and ambiguity evidence.
7. Never replace, rearrange, or claim authority over a learner's established
   AAC vocabulary.

That contract may support lesson directions, task strips, First/Then boards,
choice supports, classroom procedures, content-area vocabulary, and printable
cards. Choice Board and Change Preview remain separately held interaction
patterns; naming them here does not activate them.

Model- or parser-produced steps remain symbol-less. A future reviewed mapping may
be shown as a suggestion, but no symbol enters the authored document until the
educator explicitly chooses it. Editing or reordering must never cause a silent
remap.

## Exact upstream reconnaissance pins

These pins make the 30 August measurement reproducible. They are **not** a
deployment allowlist and must be re-measured at review time.

| Source | Exact release and commit | Measured release artifacts | Graphics/licence observation |
|---|---|---|---|
| [Mulberry Symbols](https://mulberrysymbols.org/) | [`v3.6.1`](https://github.com/mulberrysymbols/mulberry-symbols/releases/tag/v3.6.1), commit `9cbab9f400c5de44e2bc58839cca07294aadb086` | `mulberry-symbols.zip`: 52,347,398 bytes, SHA-256 `9DA3F23A17BD71AEC3C94EAE9A3367E977E7B65D3D7FEEAADB3F4690182AA05B`; 3,436 English SVGs. `symbol-info.csv`: 302,110 bytes, SHA-256 `7AB9C0A3A964334C4F8D38FB2D49BD3E08A3BE652A1ECA36AC9036BA7ADC568C` | The tagged [`LICENSE.txt`](https://github.com/mulberrysymbols/mulberry-symbols/blob/v3.6.1/LICENSE.txt) identifies the graphics as CC BY-SA 4.0. Repository tooling separately declares ISC; that does not relicense the artwork. |
| [OpenMoji](https://openmoji.org/) | [`17.0.0`](https://github.com/hfg-gmuend/openmoji/releases/tag/17.0.0), commit `f9fc506a3f913be9897ab0181d611d4c910a4104` | `openmoji-svg-color.zip`: 5,383,430 bytes, SHA-256 `59B0CD9F6FE033818FC02585CEA42BEA9FBA5D68A4D3A3639BFE5D5CC3805689`; 4,495 colour and 4,495 black SVG exports. `data/openmoji.json`: 2,193,231 bytes, SHA-256 `AB181DD523021DFBE2B98A13D96AB384B7B3014D17815261BC267FB39D9A211B` | The upstream [licence notice](https://github.com/hfg-gmuend/openmoji/blob/17.0.0/LICENSE.txt) identifies graphics as CC BY-SA 4.0 and helper/test code separately as LGPL-3.0. The colour release ZIP itself carries artwork only, so any later vendoring must deliberately carry the reviewed notice and attribution evidence. |

The licence identifiers above are facts about upstream releases, not an Honest
Ink compatibility conclusion. Whether an exact curated package, its
modifications, notices, attribution, ShareAlike obligations, and export routes
are acceptable belongs to the rights seat. Runtime retrieval does not make those
obligations disappear, and the GPL licence on Honest Ink does not automatically
relicense separately licensed artwork.

## Measured blockers in the current engine

1. Each loose manifest is intentionally bounded at 512 records. Either complete
   candidate collection exceeds that hostile-input limit; raising the limit is
   not a catalog design.
2. SequenceSlate's current picker is a small, eager list. Feeding it thousands of
   symbols would be unusable. Agency Cards also represents the present small
   catalog as an agency set; a general corpus must never expand that set.
3. The current strict schema-1 provenance record lacks the complete source-key,
   acquisition, licence-text/URI, commercial-use, consent/release, and reviewed
   mapping topology needed for this adoption. It must not be extended in place
   under the old schema label.
4. Representative upstream SVGs fail the current narrow self-contained-image
   policy: Mulberry `EN/drink.svg` carries `overflow="visible"`; OpenMoji
   `color/svg/1F600.svg` carries `id` attributes outside the allowlist. Broadening
   the sanitizer silently would weaken the boundary. A future deterministic
   normalization must retain before/after hashes, record the modification, and
   pass corpus visual-equivalence review.
5. The application project copies `assets/symbols/**/*`, while the active loader
   owns one top-level manifest. The repository test now scans that shipped tree
   recursively so an inactive or nested candidate file cannot ride into the
   application without a provenance record. A future multi-pack topology must
   declare and close every shipped partition explicitly.
6. Upstream labels, filenames, Unicode names, and coincidentally shared words are
   not reviewed Honest Ink concepts. Cross-family equivalence needs a separate,
   exact-digest-reviewed semantic registry.

## Exact-asset review worksheet

One row is required per proposed asset. A family-wide approval is insufficient.

| Field | Required evidence |
|---|---|
| Source identity | Family, exact release/commit, upstream immutable key and source URL |
| Honest Ink identity | Proposed version-stable namespaced ID; collision check across every admitted source |
| Acquired bytes | Exact file SHA-256, media type, dimensions/view box, acquisition date |
| Transformation | Original hash, deterministic transformation revision, resulting hash, human visual-equivalence disposition |
| Meaning | Narrow intended meaning and classroom contexts; never merely the upstream English label |
| Ambiguity | Alternate readings, cultural or script assumptions, abstraction level, and teaching/context note |
| AAC/SLP disposition | Admit, revise, or reject; reviewer, date, rationale, and evidence reference |
| Rights disposition | Exact licence text/URI, creator, attribution wording and placement, modification statement, ShareAlike/export effect, commercial-use decision, reviewer/date |
| Accessibility disposition | Alt text, colour/low-ink/print findings, cognitive-recognizability evidence, keyboard and AT presentation, reviewer/date |
| Educator workflow | How the proposed choice is distinguished from alternatives and explicitly confirmed without changing established AAC vocabulary |

Blank, self-declared, filename-derived, or automated fields do not approve a
row. A path named `reviewed`, a JSON boolean, or possession of upstream bytes is
not protected-seat evidence.

## Admission gates for a later implementation

- The AAC/SLP and rights seats sign an exact, curated asset allowlist and a
  separate exact mapping packet; accessibility review covers the resulting
  interaction and output.
- A ratified, versioned topology defines bounded/sharded catalogs, complete
  rights records, source-stable identities, upgrade behaviour, and closed
  shipped-file ownership without weakening schema 1.
- Search and family presentation remain keyboard-operable, source-labelled, and
  non-silent at 1366×768, pseudo 125%, 200% zoom, print, low-ink, NVDA, and
  Narrator checks.
- The current model/parser boundary stays symbol-less. Suggestions, preferences,
  and alternatives cannot authorize selection or mutate an existing choice.
- Save/reopen embeds the exact selected asset bytes and provenance, never a live
  mutable URL, and proves no silent replacement across source updates.
- Corpus normalization, attribution/export, deterministic rendering, collision,
  performance, package-hostility, and visual-regression suites pass for the
  admitted subset.

## Questions that remain genuinely open

- Which exact assets, if any, are recognizable and suitable for each reviewed
  classroom concept?
- What source-family preference, alternative ordering, and no-match behaviour
  does the AAC/SLP seat approve?
- What complete rights-ledger schema and attribution presentation does the
  rights seat approve for CC BY-SA artwork and local transformations?
- What bounded subset and search/index topology performs well without turning a
  small agency surface into a general symbol browser?
- Which educator confirmation and reconfirmation interactions pass cognitive and
  AT review?

Until those questions are answered by their owners, the honest result is a
prepared proposal and stronger neutral machinery—not a shipped symbol-system
claim.
