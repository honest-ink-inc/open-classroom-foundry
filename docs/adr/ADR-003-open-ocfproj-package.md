# ADR-003: The open .ocfproj package is the portable source of truth

**Status:** Accepted — by adoption of implementation plan 2.0 (29 August 2026); recorded here as the standing decision record
**Date:** 2026-08-29
**Ratified by:** Product owner / master teacher

## Context

The liberation test requires that existing projects remain openable and editable without a cloud connection or subscription, in an open documented format, migrating cleanly to editable formats outside the application. A proprietary database as primary storage would fail every clause. The Master's Review (finding F10) added that JSON legibility is programmer legibility — the strongest form of the promise is a project that carries its own readable rendering.

## Decision

Projects are stored as an openly documented **`.ocfproj`** ZIP/JSON package with safe path handling:

    manifest.json      schema/module/recipe versions, lanes, locales, provenance references
    artifact.json      the semantic ArtifactDocument
    assets/            referenced assets with provenance
    provenance/        rights and history records
    previews/          optional, Green only
    snapshot.html      Green only: accessible, self-contained rendering of the approved artifact

- The package — never a database — is the portable source of truth. A rebuildable local database may index Green projects and Symbol Commons assets.
- The ArtifactDocument is a semantic tree (headings, steps, tables, cards, bilingual pairs, evidence links, teacher-only notices) — never model-authored HTML or Markdown. Renderers escape all strings; scripts, remote resources, commands, and filesystem paths are prohibited in documents.
- Green packages embed `snapshot.html` and a plain-text manifest summary, so a
  project remains human-legible a decade hence with no Foundry installed. The
  snapshot is the learner-audience derivative: approval identity and
  teacher-only material do not travel in the portable package. Its bytes must
  match the exact admitted, version-routed semantic renderer for the package's
  `artifact.json` and stored render profile (or the defined default profile).
  Safe HTML syntax alone is insufficient: a harmless but unrelated snapshot is
  corruption and is refused. Historical renderer variants are a finite frozen
  compatibility set; an unknown variant is not guessed. Amber projects are not
  saved with content and are unaffected.
- A normal save is copy-on-write. The writer creates one uniquely named sibling
  stage, closes and flushes the ZIP, runs the complete hostile-package and exact
  snapshot validator against that stage, and only then atomically moves it into
  a new destination or replaces an existing package. Cancellation or any
  rendering, write, flush, or validation failure deletes only that save's stage
  and cannot truncate or otherwise damage the prior valid package.
- Required manifest and SourceEnvelope fields are as specified in implementation plan §6.5; original filenames and paths are discarded unless explicitly required.
- A reopened mutable package cannot authenticate its own module, recipe,
  purpose, review notices, protected-seat review, or output settings. The host
  therefore shows the exact semantic document, requires a fresh Green
  classification and Gate B review, and treats purpose as unknown. Teachers may
  edit the semantic tree offline: a changed revision receives a non-dismissable
  portable-edit notice and any deliberate re-save uses the engine-owned
  `portable-semantic-document` / `portable-semantic-editor` identity rather than
  copying package-authored selectors as fabricated provenance. The new package
  remains open, exact-document-bound, and editable; it does not claim the
  originating typed recipe or a protected seat's acceptance.

## Alternatives considered

1. **SQLite or similar as primary storage** — rejected: fails the liberation test's portability and legibility clauses; acceptable only as a rebuildable index.
2. **Bare folder of files instead of ZIP** — rejected: fragile to partial copies and path attacks; the ZIP boundary is where safe-path handling and integrity checks live.
3. **Model-generated HTML as the artifact format** — rejected: unverifiable, unescapable, and contradicts ADR-001's structured-output rule.

## Consequences

Format documentation becomes a release deliverable and schema versions must migrate forever — accepted as the price of the promise. Hostile-package tests (ZIP traversal, decompression) are mandatory. Reversible only by migration tooling that reads every prior version.
