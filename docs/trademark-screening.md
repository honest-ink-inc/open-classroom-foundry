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

## Public-name verdict and the three finalists (29 August 2026)

**Verdict on "Open Classroom (Foundry)": not the best public name, and it need not be.** Four reasons, all grounded in the governing documents: (1) OpenClassrooms occupies the adjacent composite in our exact class; (2) "open classroom" is a generic 1970s pedagogy term — a weak mark by construction; (3) "Open ___" is the most crowded shelf in free-software naming, and GPL-3.0-or-later already does legally everything the word "open" is doing rhetorically; (4) the atlas and plan's own identity register is *craft* — foundry, press, studio, bench, smith, loom, forge — and the public name should come from that register. It remains an excellent working and repository title indefinitely, at zero switching cost: all namespaces are `Foundry.*` and public-name-neutral by design.

The three most adroit finalists, each screened first-pass and each anchored in the documents themselves:

| Rank | Name | Document anchor | Screen result (informal) | Character |
|---|---|---|---|---|
| 1 | **Honest Ink** | The review's own line — "good geometry and honest ink" — and the constitution's deepest differentiator: locked truth-bearing fields, uncertainty marked rather than plausibly completed, nothing invented on paper | **Cleanest screen of every name tested**: no education/software collision surfaced (nearest: Honestech video tools, Ink Labs, Inkling — all distinct) | The daring choice; a distinctive, protectable mark; the standing subtitle policy ("— the classroom foundry") supplies the function the name withholds |
| 2 | **Schoolhouse Foundry** | Atlas #200, One-Room Schoolhouse — the liberation ideal — plus full continuity with the Foundry identity | Two adjacent brands need counsel: Schoolhouse.world (Khan's tutoring nonprofit, house mark "Schoolhouse") and Schoolhouse Technologies (teacher-productivity software) | The warm, legible choice; composite likely distinguishable but screen-heavier |
| 3 | **Inkwright** | The craft register (wheelwright, playwright) fused with the lineage — the atlas is "descended from Writer's Kiosk," and wright/write is that descent said aloud | Claimable but not empty: Inkwright LLC (screen printing) and Inkwright, Inc. (technical-writing services) exist outside education software; Microsoft's old INKWRITER mark is cancelled | The invented-compound choice; one word, strong app-name ergonomics; minor misspelling risk ("Inkright") |

Eliminated by this pass: **The Classroom Press** (Classroom Complete Press, K-12 curriculum publisher, sits too close in class), **Chalk Foundry** (Chalk.com, K-12 software), **Chalkline Press** (the Chalkline root is claimed twice, including a UK education company), **The Apprentice Press** (Apprentice House Press, book publisher). The pattern across every test: in education naming, *descriptive names are all taken* — distinctiveness is not a luxury but the only clear ground remaining, which is itself an argument for finalist 1.

The decision remains the typist's, at pre-release leisure (plan §20, decision 1). Whichever survives counsel, the subtitle policy holds: the name names, the subtitle explains.

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
- Finalist screening: [Schoolhouse.world](https://schoolhouse.world/) · [Schoolhouse.world — Crunchbase](https://www.crunchbase.com/organization/schoolhouse-world) · [Schoolhouse Technologies — ZoomInfo](https://www.zoominfo.com/c/schoolhouse-technologies-inc/59490436) · [Classroom Complete Press — PublishersGlobal](https://www.publishersglobal.com/directory/publisher-profile/19942) · [Chalk — LinkedIn](https://ca.linkedin.com/company/chalk-com) · [Chalkline Education & Support Ltd — Companies House](https://find-and-update.company-information.service.gov.uk/company/14221471) · [Chalkline, Inc.](https://www.crunchbase.com/organization/chalkline) · [Inkwright, Inc.](https://inkwright.inc/) · [Inkwright LLC — ZoomInfo](https://www.zoominfo.com/c/inkwright-llc/398698117) · [INKWRITER (cancelled Microsoft mark) — USPTO report](https://uspto.report/TM/74364351) · [Honestech — Wikipedia](https://en.wikipedia.org/wiki/Honestech) · [Ink Labs — LinkedIn](https://www.linkedin.com/company/inklabsedu)
