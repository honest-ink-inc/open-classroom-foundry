# Trademark screening — first pass

**Date:** 29 August 2026 · **Status:** informal screening, not legal clearance and not legal advice. This satisfies the Days 1–15 "trademark-screening task" at the working-title level; professional screening (USPTO TESS, EUIPO, state registries, common-law search) is required before any public distribution, and counsel before 1.0.

All names remain working titles. The standing insurance policy holds: every product keeps a load-bearing subtitle ("Visual Support Studio") as its functional identity, so any single name can be replaced without losing the product's meaning.

## Findings by name, highest risk first

### Lesson Loom — direct collision found; rename before public release

**"The Lesson Loom"** (thelessonloom.com) is an existing product: an AI-powered lesson-planning "smart curriculum companion" for homeschool educators — the same product category as our module. "Loom" additionally collides with the well-known video product. **Recommendation: treat the rename as decided-in-principle; choose the replacement at public-naming time.** Candidate replacements to screen: **Lesson Bench**, **Lesson Wright**, **Planwright**, **Lesson Anvil**. The module identifier in code should stay neutral (`Modules.LessonPlanning`) so the rename is a resource change, not a refactor.

### Open Classroom Foundry — significant adjacent brand; plan a distinct public name

The typist found an "Open Classrooms" iOS app; research confirms the source: **OpenClassrooms** (openclassrooms.com) is a major France-based online vocational-education platform — founded 2013, ~2.5 million users, courses in English/French/Spanish, B Corp, institutionally funded — operating squarely in education services, our class of goods and services.

Assessment: the phrase "open classroom" is also a generic 1970s pedagogy term, which cuts both ways — descriptive terms make weak marks (limiting their reach against a different composite name), but an established international education brand this close in name and class is a real opposition and confusion risk for a *public-facing* software name. **Recommendation: keep "Open Classroom Foundry" as the internal working title and repository name (fine), and select a more distinctive public name before public distribution** — plan §20 decision 1, which remains the typist's. Candidates worth screening when that day comes: **Schoolhouse Foundry**, **Chalk Foundry**, **Teacher's Foundry**, **The Classroom Press**. No decision is needed now; nothing in the codebase should hard-code the public name (ship name lives in one resource).

### All Aboard — crowded phrase, no direct collision surfaced

First-pass search of the visual-schedule/autism app space (Goally, Lil Planner, Visual Schedule & Social Story, and similar) surfaced **no app named "All Aboard"** in the category — consistent with the typist's finding pattern. However, "All Aboard" is a common phrase with known uses in other app categories (transit, games), and common phrases in crowded fields need a proper USPTO/app-store screen before release. Risk: moderate. The subtitle "Visual Support Studio" carries the identity regardless.

### Symbol Commons — clear, and the search found something better than clearance

No product named "Symbol Commons" surfaced. The adjacent ecosystem is an implementation gift, not a conflict:

- **OpenSymbols** (opensymbols.org, open-aac/opensymbols on GitHub) aggregates 50,000+ open-licensed AAC symbols with an open API — prior art to learn from and interoperate with, not compete with.
- **Mulberry Symbols** (~3,000, CC BY-SA) and **OpenMoji** (~3,000, CC BY-SA) are license-compatible candidates for the bundled libre core (plan §9 prefers CC0/CC BY/CC BY-SA).
- **ARASAAC** (~13,000, CC BY-NC-SA) and **Sclera** (~11,000, CC BY-NC) are **noncommercial-licensed: never bundled** as a universally free commons — import-only for entitled educators, technically isolated from public export, exactly as the plan's invariants require.

**Recommendation:** record Mulberry + OpenMoji as the candidate bundled core for plan §20 decision 6, and add "interoperability with OpenSymbols identifiers" to Symbol Commons' design considerations.

## Method for the professional screen (pre-release checklist)

1. USPTO TESS search per name (word mark + design variants) in the relevant classes (education services; computer software).
2. EUIPO and national registries where distribution is expected (OpenClassrooms is EU-based — the EU screen matters).
3. App-store, domain, and common-law search per name.
4. Counsel review of the survivors before any public 1.0 distribution.

## Sources

- [OpenClassrooms — Wikipedia](https://en.wikipedia.org/wiki/OpenClassrooms) · [openclassrooms.com/en/about-us](https://openclassrooms.com/en/about-us) · [Crunchbase profile](https://www.crunchbase.com/organization/openclassrooms) · [B Corp listing](https://www.bcorporation.net/en-us/find-a-b-corp/company/open-classrooms/)
- [The Lesson Loom](https://thelessonloom.com/) · [AVID Open Access on Loom (video product)](https://avidopenaccess.org/resource/loom/)
- Visual-schedule category scan: [Goally](https://getgoally.com/blog/visual-schedule-apps/) · [Lil Planner (App Store)](https://apps.apple.com/us/app/lil-planner-visual-schedule/id6448482826) · [Adult Down Syndrome Center resource list](https://adscresources.advocatehealth.com/resources/visual-schedule-apps/)
- AAC symbol ecosystem: [OpenSymbols](https://www.opensymbols.org/) · [open-aac/opensymbols (GitHub)](https://github.com/open-aac/opensymbols) · [OpenAAC symbol libraries](https://www.openaac.org/symbols.html) · [Mulberry Symbols](https://mulberrysymbols.org/)
