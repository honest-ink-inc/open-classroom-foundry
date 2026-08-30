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

- **OpenSymbols** (opensymbols.org, open-aac/opensymbols on GitHub) indexes 50,000+ AAC symbols from multiple repositories and returns source-specific licence and attribution metadata — prior art to learn from and interoperate with, not compete with.
- **Mulberry Symbols** (~3,000, CC BY-SA) and **OpenMoji** (~3,000, CC BY-SA) are license-compatible candidates for the bundled libre core (plan §9 prefers CC0/CC BY/CC BY-SA).
- **ARASAAC** (~13,000, CC BY-NC-SA) and **Sclera** (~11,000, CC BY-NC) are **noncommercial-licensed: never bundled** as a universally free commons — import-only for entitled educators, technically isolated from public export, exactly as the plan's invariants require.

**Recommendation:** record Mulberry + OpenMoji as the candidate bundled core for plan §20 decision 6, and add "interoperability with OpenSymbols identifiers" to Symbol Commons' design considerations.

### OpenSymbols candidate note — 30 August 2026

The typist has expressly noted that OpenSymbols may be used for AAC features. That makes it a recorded candidate, not a blanket asset approval or a substitute for the AAC/SLP and rights seats.

The current [catalog](https://www.opensymbols.org/search) aggregates repositories under different licences; each admitted symbol therefore needs its own provider, repository key, symbol key, source/detail URL, acquisition date, author, exact licence metadata, attribution, and content hash carried into Honest Ink's existing rights ledger. Remote identifiers alone are not durable provenance. The API's fields may be absent and do not supply Honest Ink's required modification history, attribution disposition, commercial-use decision, or release/consent record. The MIT licence on the [OpenSymbols server source](https://github.com/open-aac/opensymbols) licenses that server code, not every indexed image.

The current [API documentation](https://www.opensymbols.org/api) requires an application-issued shared secret, short-lived access tokens, and compliance with service usage guidance. The secret must not be exposed in browser JavaScript or a compiled application, requests may be throttled, and returned image URLs are not promised to remain permanent. Search accepts only two-letter lowercase locales, and `hc=1` is a relevance preference rather than proof that a result satisfies Honest Ink's accessibility standard. Any future importer must allowlist and bound admitted fields, sanitize downloaded PNG/SVG content, avoid retaining unneeded search phrases or query logs, and hash the exact acquired bytes. No account was created, terms accepted, secret requested, network egress added, symbol downloaded, or AAC meaning selected here.

The safe future route is exact, curated, locally vendored assets selected only after protected AAC/SLP recognizability and meaning review plus OER/licence-steward approval, with per-asset licence, attribution, provenance, and hash evidence. Identifier interoperability remains a design candidate. A live runtime dependency on OpenSymbols is not admitted by this note and would additionally require a governed server-side secret boundary, offline/failure behaviour, and an authorized service-account decision.

## Public-name verdict and the three finalists (29 August 2026)

**Verdict on "Open Classroom (Foundry)": not the best public name, and it need not be.** Four reasons, all grounded in the governing documents: (1) OpenClassrooms occupies the adjacent composite in our exact class; (2) "open classroom" is a generic 1970s pedagogy term — a weak mark by construction; (3) "Open ___" is the most crowded shelf in free-software naming, and GPL-3.0-or-later already does legally everything the word "open" is doing rhetorically; (4) the atlas and plan's own identity register is *craft* — foundry, press, studio, bench, smith, loom, forge — and the public name should come from that register. It remains an excellent working and repository title indefinitely, at zero switching cost: all namespaces are `Foundry.*` and public-name-neutral by design.

The three most adroit finalists, each screened first-pass and each anchored in the documents themselves:

| Rank | Name | Document anchor | Screen result (informal) | Character |
|---|---|---|---|---|
| 1 | **Honest Ink** | The review's own line — "good geometry and honest ink" — and the constitution's deepest differentiator: locked truth-bearing fields, uncertainty marked rather than plausibly completed, nothing invented on paper | **Cleanest screen of every name tested**: no education/software collision surfaced (nearest: Honestech video tools, Ink Labs, Inkling — all distinct) | The daring choice; a distinctive, protectable mark; the standing subtitle policy ("— the classroom foundry") supplies the function the name withholds |
| 2 | **Schoolhouse Foundry** | Atlas #200, One-Room Schoolhouse — the liberation ideal — plus full continuity with the Foundry identity | Two adjacent brands need counsel: Schoolhouse.world (Khan's tutoring nonprofit, house mark "Schoolhouse") and Schoolhouse Technologies (teacher-productivity software) | The warm, legible choice; composite likely distinguishable but screen-heavier |
| 3 | **Inkwright** | The craft register (wheelwright, playwright) fused with the lineage — the atlas is "descended from Writer's Kiosk," and wright/write is that descent said aloud | Claimable but not empty: Inkwright LLC (screen printing) and Inkwright, Inc. (technical-writing services) exist outside education software; Microsoft's old INKWRITER mark is cancelled | The invented-compound choice; one word, strong app-name ergonomics; minor misspelling risk ("Inkright") |

Eliminated by this pass: **The Classroom Press** (Classroom Complete Press, K-12 curriculum publisher, sits too close in class), **Chalk Foundry** (Chalk.com, K-12 software), **Chalkline Press** (the Chalkline root is claimed twice, including a UK education company), **The Apprentice Press** (Apprentice House Press, book publisher). The pattern across every test: in education naming, *descriptive names are all taken* — distinctiveness is not a luxury but the only clear ground remaining, which is itself an argument for finalist 1.

**Decided 29 August 2026: Honest Ink** (ADR-006). Counsel confirmation remains a pre-release checkpoint; Schoolhouse Foundry and Inkwright stand as ordered fallbacks. The subtitle policy holds: the name names, the subtitle explains.

## Namespace observation — GitHub, 29 August 2026 (evidence, not clearance)

Recorded because creating the organization forced the question, and because a bare "the name was taken" would mislead counsel. Two `honest-ink*` handles were unavailable, for two **unrelated** reasons:

- **`honest-ink-edu` is the typist's own personal account.** Self-inflicted; evidence of nothing.
- **`honest-ink` is held by a third party** — a *personal user account*, not an organization: created 13 Aug 2025, last activity 26 Jan 2026, six public repositories.

The organization was therefore created as **`honest-ink-inc`**. The namespace is not contested by anyone in this field.

### What that account publishes

| Repository | Its own description | Language |
|---|---|---|
| `editorial-desk` | "a GPT that lets you test out story ideas against a media editor" | TypeScript |
| `HotSeat`, `TheHotSeat2` | "the HotSeat Gameshow" | TypeScript |
| `SG-Example-RAG` | "A blog site that uses RAG" | TypeScript |
| `N8N-backup2` | "N8N backup JSONs" | — |
| `Concierge` | "Files for Concierge import" | — |

**Relatedness of goods and services:** the account's field is AI/LLM experimentation with a media-and-broadcast flavor — GPT wrappers, a RAG demo, workflow-automation backups, a gameshow project. Not education, not teacher tools, not accessibility, not classroom printing. Against a GPL classroom authoring-and-printing tool the overlap is essentially nil, and relatedness of goods and services is the axis on which confusion is judged.

**No commercial signal at all:** empty bio, company, website, location, and email; zero followers; zero stars across all six repositories; a User account rather than an Organization. This reads as an individual's scratch account, not a business trading under a brand.

**One adjacency, named rather than hidden:** `editorial-desk` is editorial/publishing-flavored, and "Honest Ink" is publishing-flavored too. That is not a conflict — their goods are AI chat experiments — but it is a small sign that the name reads as publishing-adjacent to other people, which is precisely the register a genuine conflict would come from. Screen the printing, tattoo, publishing, and journalism trades hardest.

**Three limits, stated so this is not over-read:**

1. **A GitHub username is not a trademark.** It is a namespace reservation on one platform. Rights arise from use in commerce, and no use in commerce is visible here.
2. **This is one platform, not a search.** It says nothing about registered marks in the relevant classes, nor about common-law users who are not on GitHub. It supplements the professional screen below; it does not substitute for any step of it.
3. **Inspection stopped at public repository metadata.** Identifying the individual behind the account is a different activity from screening a name, and is not needed for it.

**Net: nothing here supports a goods-and-services conflict.** ADR-006 §4's counsel checkpoint is unchanged — this is a fact for counsel, not a clearance.

## Method for the professional screen (pre-release checklist)

1. USPTO TESS search per name (word mark + design variants) in the relevant classes (education services; computer software).
2. EUIPO and national registries where distribution is expected (OpenClassrooms is EU-based — the EU screen matters).
3. App-store, domain, and common-law search per name.
4. Code-forge and package-registry namespaces (GitHub, GitLab, npm, NuGet, PyPI) per name — a handle is not a mark, but an occupied one is a fact worth carrying to counsel, and it constrains the project’s own naming (see the namespace observation above).
5. Counsel review of the survivors before any public 1.0 distribution.

## Sources

- [OpenClassrooms — Wikipedia](https://en.wikipedia.org/wiki/OpenClassrooms) · [openclassrooms.com/en/about-us](https://openclassrooms.com/en/about-us) · [Crunchbase profile](https://www.crunchbase.com/organization/openclassrooms) · [B Corp listing](https://www.bcorporation.net/en-us/find-a-b-corp/company/open-classrooms/)
- [The Lesson Loom](https://thelessonloom.com/) · [AVID Open Access on Loom (video product)](https://avidopenaccess.org/resource/loom/)
- Visual-schedule category scan: [Goally](https://getgoally.com/blog/visual-schedule-apps/) · [Lil Planner (App Store)](https://apps.apple.com/us/app/lil-planner-visual-schedule/id6448482826) · [Adult Down Syndrome Center resource list](https://adscresources.advocatehealth.com/resources/visual-schedule-apps/)
- AAC symbol ecosystem: [OpenSymbols](https://www.opensymbols.org/) · [open-aac/opensymbols (GitHub)](https://github.com/open-aac/opensymbols) · [OpenAAC symbol libraries](https://www.openaac.org/symbols.html) · [Mulberry Symbols](https://mulberrysymbols.org/)
- Namespace observation (29 Aug 2026): GitHub REST API, `users/honest-ink` and `users/honest-ink/repos` — public account and repository metadata, read directly rather than from a third-party profile.
- Finalist screening: [Schoolhouse.world](https://schoolhouse.world/) · [Schoolhouse.world — Crunchbase](https://www.crunchbase.com/organization/schoolhouse-world) · [Schoolhouse Technologies — ZoomInfo](https://www.zoominfo.com/c/schoolhouse-technologies-inc/59490436) · [Classroom Complete Press — PublishersGlobal](https://www.publishersglobal.com/directory/publisher-profile/19942) · [Chalk — LinkedIn](https://ca.linkedin.com/company/chalk-com) · [Chalkline Education & Support Ltd — Companies House](https://find-and-update.company-information.service.gov.uk/company/14221471) · [Chalkline, Inc.](https://www.crunchbase.com/organization/chalkline) · [Inkwright, Inc.](https://inkwright.inc/) · [Inkwright LLC — ZoomInfo](https://www.zoominfo.com/c/inkwright-llc/398698117) · [INKWRITER (cancelled Microsoft mark) — USPTO report](https://uspto.report/TM/74364351) · [Honestech — Wikipedia](https://en.wikipedia.org/wiki/Honestech) · [Ink Labs — LinkedIn](https://www.linkedin.com/company/inklabsedu)
