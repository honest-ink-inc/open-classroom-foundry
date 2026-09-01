# ADR-009: StrandPlan replaces GridLesson as the provisional lesson-design display

**Status:** Accepted — product-owner-authorized naming correction enacted 31 Aug 2026

**Date:** 2026-08-31

**Ratified by:** Product owner through the standing direction to research, decide, and enact required module-name replacements

## Context

ADR-008 replaced the directly conflicting **Lesson Loom** display with
**GridLesson — Lesson Design Studio** after a bounded 30 August screen. That
screen looked for the exact composite but did not surface a reversed-word use.
A fresh 31 August screen found [LessonGrid](https://lessongrid.app/), an active
product that describes itself as visual curriculum browsing and lesson planning
for United States teachers. It offers weekly and quarterly planning, standards
visibility, lesson materials, exports, and sharing. The words are reversed, but
the audience, function, and software category substantially overlap this
module's lesson-design surface.

An exact-string result is therefore not enough to keep GridLesson. This is a
newly measured common-law/product fact, not a trademark conclusion. ADR-008
remains the historical record of what the 30 August screen found and is
superseded only for its lesson-design display row.

## Decision

Use **StrandPlan — Lesson Design Studio** as the provisional public display for
the lesson-design module. Preserve all stable compatibility identities exactly:

- module and recipe id `lesson-loom`;
- schema id `schema.lesson-loom.v1`;
- source namespace, type and filename tokens;
- localization ids, diagnostic codes, fixture identities, and saved-project
  bindings.

Use `strandplan` only as the public output filename stem. Add both **Lesson
Loom** and **GridLesson** to the retired-display guard so neither can re-enter
current UI chrome accidentally.

The load-bearing subtitle remains mandatory. StrandPlan is a working display,
not a claim of availability or registrability. Counsel must repeat registry,
common-law, marketplace, and international screening before any release,
distribution, publication, paid promotion, or other use that treats it as a
cleared mark.

## Alternatives considered

- **Keep GridLesson.** Rejected because LessonGrid now occupies nearly the same
  words, audience, and lesson-planning category.
- **LessonSprout.** Rejected because current Sprout-branded lesson and
  curriculum products make the education field crowded.
- **The other supplied candidates.** Current screening found direct or strong
  software/education uses for most; ArborPlan and PlanWeaver also have existing
  software uses. PlotOutline is generic and subject-narrow. None warrants a
  clearance claim.
- **No coined display, only “Lesson Design Studio.”** Safer as description, but
  inconsistent with the established name-plus-load-bearing-subtitle system and
  unnecessary for an internal alpha when a plainly provisional display can be
  recorded honestly.
- **Invent a new mark and call it clear.** Rejected. A keyboard cannot perform
  professional clearance, and novelty in a short web search is not legal
  availability.

Of the supplied candidates, StrandPlan had the least relevant surfaced product
conflict. Its words are descriptive and the matching `.com` is occupied; neither
fact prevents use as an in-product provisional module label, and neither is
misstated as domain availability or trademark clearance.

## Consequences

Current UI, current product documentation, accessibility names, and future
output filenames use **StrandPlan — Lesson Design Studio**. Historical ADRs,
handovers, receipts, and screenshots may retain GridLesson where it was true at
the recorded time. The deterministic neutral UI-catalog source digest changes
and must be re-exported and remeasured.

The correction is reversible at the display registry without changing a
project, recipe, schema, or diagnostic identity. It grants no curriculum,
multilingual, AAC/SLP, accessibility, rights, district, safeguarding, or council
authority and performs no filing, registration, account creation, domain
purchase, publication, or release act.
