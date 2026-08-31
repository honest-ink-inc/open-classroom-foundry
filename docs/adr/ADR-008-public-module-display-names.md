# ADR-008: Six public module display names change; stable identifiers do not

**Status:** Accepted — product-owner decision enacted 30 Aug 2026
**Date:** 2026-08-30
**Decided by:** Product owner

## Context

ADR-006 selected **Honest Ink — the classroom foundry** as the public product
name while deliberately leaving repository and internal identifiers unchanged.
The same separation is now required for six module names. Current common-law,
product, application, publishing, and registry reconnaissance found direct or
unacceptably close uses for **All Aboard**, **Lesson Loom**, **Talk Moves
Studio**, **Exit Lens**, **Source Lens**, and **Family Bridge** in education or
software. In particular, The Lesson Loom occupies the same lesson-planning
category and has two live United States applications, serial numbers
`99339738` and `99339743`.

The product owner asked for a researched decision, with Lesson Loom first in
importance. Candidate screening considered exact-name web, product, app-store,
code-forge, domain, and preliminary United States registry results, as well as
semantic fit and the established craft register. This is an engineering naming
decision, not a legal clearance opinion.

## Decision

The public display names and their load-bearing functional subtitles are:

| Prior public display | New public display | Load-bearing subtitle |
|---|---|---|
| All Aboard | **SequenceSlate** | Visual Support Studio |
| Lesson Loom | **GridLesson** | Lesson Design Studio |
| Talk Moves Studio | **Forumwright** | Discussion Design |
| Exit Lens | **ReteachSignal** | Formative Evidence |
| Source Lens | **Inquirywright** | Source & Inquiry |
| Family Bridge | **KinDispatch** | Bilingual & Family Press |

**GridLesson** is the selected Lesson Loom replacement. Of the product owner's
candidate list it had the best combination of functional legibility, short app
ergonomics, distance from the direct collision, and an initial exact-name
screen without a surfaced education-software product. Names that were more
descriptive or apparently more distinctive were already occupied: among them
Syllabuild, CurriculumCraft, Planly, Loominote, Lessoneer, Weavly, Tapestry,
SyncClass, and Planwright. StrandPlan and LessonSprout remained plausible, but
GridLesson better describes this module's visible planning surface without
claiming automatic curriculum authorship.

The subtitle is part of each public identity. UI, current product
documentation, release notes, screenshots, listings, and accessibility names
must not present a bare coined name when the functional subtitle can be shown.
Historical evidence may retain the name that was true when it was recorded.

## Compatibility contract: old identifiers remain exact

This decision changes display strings, not persisted or executable identity.
The following legacy identifiers remain stable. They must not be migrated,
aliased opportunistically, or rewritten in saved projects merely to match the
new display name.

| Public display | Stable door/module ID | Stable recipe or mode ID | Stable schema ID |
|---|---|---|---|
| SequenceSlate | `all-aboard` | `all-aboard.task-strip`, `all-aboard.first-then`, `all-aboard.now-next-done`, `all-aboard.agency-cards` | `schema.all-aboard.v1` |
| GridLesson | `lesson-loom` | `lesson-loom` | `schema.lesson-loom.v1` |
| Forumwright | `talk-moves` | `talk-moves-studio` | `schema.talk-moves.v1` |
| ReteachSignal | `exit-lens` | `exit-lens` | `schema.exit-lens.v1` |
| Inquirywright | `source-lens` | `source-lens` | `schema.source-lens.v1` |
| KinDispatch | `family-bridge` | `family-bridge` | `schema.family-bridge.v1` |

Namespaces, type names, localization keys, filenames, test selectors, fixture
names, diagnostic codes, project bindings, and package manifests may likewise
retain the legacy token where it is an internal contract. A public screen must
not expose those tokens as branding. Any future identifier migration requires
its own schema-aware ADR, backwards-compatibility proof, and project migration
route.

TaskDock remains the historical name of Atlas entry 21 and ADR-005. It is not a
runtime or public product name. Current product prose calls the shipped concept
the **Scaffold Smith task-entry scaffold**.

## Governance and release checkpoint

This decision is an informal product screen, **not a trademark clearance or a
claim that matching domains or handles are available**. Counsel must review the
six selected composites and their intended goods and services before release,
distribution, publication, paid promotion, or a visibility change that treats
them as marks. Registry status and common-law use can change; the screen must be
repeated at that checkpoint. Honest Ink's ADR-006 counsel checkpoint is
unchanged.

No name decision waives a protected seat or alters a data lane. In particular:

- AAC users, SLPs, special educators, accessibility/AT reviewers, and the
  rights seat still govern SequenceSlate's agency, terminology, symbol meaning,
  recognizability, and source admission.
- The multilingual seat still governs consequential translation, reviewed
  catalogs, and KinDispatch's parallel-language behavior.
- Written district authority remains required before ReteachSignal or any
  other Amber workflow uses real learner-linked evidence.
- The educator council still selects the next Atlas priority in a real
  needs-first session. A rename is neither adoption nor prioritization.

## Consequences

The names can change in the UI without invalidating a project, recipe, test,
localization key, or diagnostic record. The deliberate cost is that source code
and persisted data contain legacy tokens. Maintainers must distinguish public
display text from stable identity and must document both when debugging a
compatibility issue.

Historical ADRs, reviews, rehearsal records, evidence receipts, and handovers
are not rewritten. Current canonical documents use the new display names and
may carry a one-time “formerly” note where a reader needs to connect an old
record or stable ID.
