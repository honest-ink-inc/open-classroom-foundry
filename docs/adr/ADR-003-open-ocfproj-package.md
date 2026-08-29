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
- Green packages embed `snapshot.html` and a plain-text manifest summary, so a project remains human-legible a decade hence with no Foundry installed. Amber projects are not saved with content and are unaffected.
- Required manifest and SourceEnvelope fields are as specified in implementation plan §6.5; original filenames and paths are discarded unless explicitly required.

## Alternatives considered

1. **SQLite or similar as primary storage** — rejected: fails the liberation test's portability and legibility clauses; acceptable only as a rebuildable index.
2. **Bare folder of files instead of ZIP** — rejected: fragile to partial copies and path attacks; the ZIP boundary is where safe-path handling and integrity checks live.
3. **Model-generated HTML as the artifact format** — rejected: unverifiable, unescapable, and contradicts ADR-001's structured-output rule.

## Consequences

Format documentation becomes a release deliverable and schema versions must migrate forever — accepted as the price of the promise. Hostile-package tests (ZIP traversal, decompression) are mandatory. Reversible only by migration tooling that reads every prior version.
