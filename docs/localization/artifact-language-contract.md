# Artifact language contract

**Status:** Current implementation boundary · **Date recorded:** 30 August 2026

The UI-catalog workflow localizes application chrome. It does not localize the words printed inside an artifact. Those are separate products with separate review, provenance, font, direction, and accessibility obligations.

## What the engine means by language

`ArtifactDocument.Language` is the BCP-47 primary language of the document. Bilingual nodes and bilingual step rows carry their own source and target locale tags. The domain validator rejects malformed non-null tags, and HTML/SVG renderers emit the language and right-to-left direction semantics.

The property remains nullable so older packages and programmatic drafts can be read honestly. Renderers currently use `en` as a compatibility fallback for a null document; that fallback is not evidence that unknown teacher-entered text is English. Every current Deterministic Press definition separately declares neutral English as the language of its built-in artifact furniture. That catalog metadata is deliberately not promoted to `ArtifactDocument.Language`: a builder's exact document language is preserved, and an unknown document language stays null through composers. This does not translate teacher input, infer a language from the operating system, or claim that mixed teacher-entered text is English.

The distinction is deliberate:

- press titles, parameter labels, and choice labels are application chrome and belong to the reviewed UI catalog;
- built-in headings, directions, legends, descriptions, prompts, answer-key furniture, and default printable text are artifact furniture;
- teacher-entered lists and values are carried verbatim; and
- per-segment bilingual text keeps its own locale metadata rather than inheriting a page-wide guess.

## Current rendering boundary

The deterministic native vector-PDF route uses standard-14 Courier with WinAnsi encoding. Its exact admitted repertoire is 218 Unicode code points: printable ASCII, U+00A0–U+00FF, and the 27 defined WinAnsi extension characters. A rendering regression counts that set from the production encoder. Anything outside it refuses rather than substituting a glyph, and the application selects the local HTML/Edge print route instead.

HTML and SVG retain Unicode text, but their CSS font stacks use installed system fonts. No font is bundled. Therefore script coverage depends on the managed machine, and the repository makes no universal glyph-coverage claim. Adding a font requires recorded source, license, embedding permission, modification status, script coverage, and the protected rights/accessibility/language review required by the implementation plan.

## What remains human-held

The current printable furniture is neutral English. A real additional artifact language requires a complete furniture inventory, source-version binding, translation provenance, multilingual-seat review, script-appropriate font evidence, bidirectional and reading-order tests, and exact reviewed bytes. Resolving the renderer's legacy `en` fallback for language-null mixed content belongs in that review too. A keyboard choice, UI locale, machine culture, or broad engineering instruction cannot manufacture that evidence. Until the protected seat sets the scope and supplies reviewed material, this document records the boundary; it does not invent the translation.

Machine evidence: [PressRoomCatalogTests](../../tests/Unit/PressRoomCatalogTests.cs), [BilingualRegressionTests](../../tests/Rendering/BilingualRegressionTests.cs), [VectorPdfTests](../../tests/Rendering/VectorPdfTests.cs), and [DocumentValidator](../../src/Foundry.Domain/DocumentValidator.cs).
