# Open Classroom Foundry

## The Master's Review: audit, refinement, and enrichment of the 200-idea atlas and the twelve-module plan

**Review version:** 1.0
**Date:** 29 August 2026
**Reviewer:** Da Vinci, master teacher, in collaboration with the typist
**Subjects reviewed:** `open-classroom-foundry-idea-atlas.md` and `open-classroom-foundry-audited-implementation-plan.md`
**Standing:** This review inherits every disclaimer of the audited plan. It is a design, instructional, and editorial judgment — not legal advice, a district authorization, or evidence that unbuilt software satisfies any gate.

---

# 1. Verdict

The atlas and the plan are worthy. The reusable grammar — *capture → interpret → constrain → scaffold → verify → render → print/export → preserve only what is worth preserving* — is genuinely discovered, not invented for marketing. The plan's binding audit corrections are re-affirmed here without exception, and its decision to let the engineering sequence differ from the public priority sequence is disciplined dependency management, exactly as it claims.

Three things remain to be done, and this review does them:

1. **Repair** — the atlas and plan contain reconcilable editorial defects and a handful of lane assignments that contradict the plan's own binding corrections.
2. **Consolidate** — the 200 ideas are not 200 products; they resolve into roughly a dozen engines and a body of recipe presets, and saying so precisely will prevent years of misdirected effort.
3. **Enrich** — the atlas, for all its breadth, forgot the most liberating studio of all: the one that needs no model whatsoever. The addendum below adds it, along with the missing subjects (computational thinking above all) and twenty-five new candidates, bringing the reconciled atlas to 227.

The single largest strategic recommendation of this review: **build the Deterministic Press first.** A zero-inference printable-authoring studio exercises the entire pipeline — projects, rendering, printing, accessibility, provenance — with zero privacy risk, zero district approval friction, and immediate daily value to any teacher on Earth. It is the trust anchor the whole cathedral should rest upon, and it makes the Foundry a genuine gift even to the school that never enables a model.

---

# 2. Audit findings

Findings are numbered F1–F13 and tagged by severity. *Must fix* means the documents should be amended before Gate 0 sign-off.

## F1 — Two of the twelve do not exist in the atlas *(editorial, must fix)*

**Scaffold Smith** (third of the twelve) and **Talk Moves Studio** (ninth of the twelve) appear in the atlas's list of "the first twelve worth building" and throughout the audited plan, but neither appears anywhere in the numbered 200-candidate atlas. **Talk Moves Loom** (#144) is a different tool with a different description — multimodal discussion supports for multilingual learners, not equitable discussion design. The atlas therefore actually names 202 distinct ideas while claiming 200.

**Remedy:** Add **201. Scaffold Smith** to Studio V and **202. Talk Moves Studio** to Studio VI or a discussion cluster, with their descriptions from the twelve. The addendum in section 6 continues numbering from there. Retitle the atlas honestly when regenerated.

## F2 — TaskDock is promised, then silently abandoned *(planning divergence, must decide)*

The atlas's "pragmatic first release" (line 78) bundles **All Aboard, Board to Brief, Access Remix, Directions Duet, and TaskDock** as five output modes of one application, and its Teacher Foundry 0.2 build stage includes TaskDock again. The audited plan's engineering sequence and release roadmap never mention TaskDock at all. Neither document records the decision to drop it.

**Remedy:** Decide explicitly and record it as an ADR. Recommended resolution: **absorb TaskDock as a Scaffold Smith preset** ("task-entry scaffold": materials, first action, chunks, checkpoints, help routes, definition of done). Its machinery is a strict subset of Scaffold Smith's; two products would be one product wearing two coats. If absorbed, amend the atlas's line 78 to name Scaffold Smith.

## F3 — Grouping Deck contradicts a binding correction *(lane error, high)*

**Grouping Deck** (#137, tagged `[A]`) offers "temporary teacher-controlled grouping options from de-identified evidence tags." But grouping is the act of assigning *named learners* to groups; evidence tags that produce actionable groups must be re-identifiable by construction, which collides with the plan's binding correction that *Exit Lens reports reasoning clusters and matched instructional routes; it does not form named groups*. A tool cannot be de-identified and produce a seating arrangement.

**Remedy:** Re-scope Grouping Deck to **grouping-structure templates** — rotation patterns, group sizes, role structures, regrouping signals — with no student data at all, tagged `[G]`. The act of placing names into the structure remains the teacher's, on paper or in district systems. Anything more belongs beyond 1.x with explicit district governance.

## F4 — Portfolio Narrator exceeds the Amber contract *(lane error, high)*

**Portfolio Narrator** (#69, tagged `[A]`) arranges "locally governed artifacts into an evidence timeline" with growth statements. A portfolio is inherently identified and longitudinal; the Amber lane's defining property is ephemerality — raw capture ephemeral, only teacher-approved de-identified derivatives saved. A longitudinal identified evidence store is precisely the "student profiles, rosters, longitudinal records" the plan's non-goals exclude through 1.x.

**Remedy:** Retag `[A→R]` and defer past 1.x. The learner-held paper alternative — **Portfolio Passport** (#224 in the addendum) — achieves much of the pedagogical value with none of the data custody, because the learner keeps the record.

## F5 — Accumulating tools need an accumulation invariant *(lane caution, medium)*

**Misconception Atlas** (#60) and **Teacher Logbook** (#151) accumulate patterns across time. Even with de-identified inputs, an accumulating store drifts toward a shadow record — small classes, distinctive handwriting descriptions, and dated entries re-identify.

**Remedy:** Add a shared invariant for accumulating modules: *only teacher-authored pattern descriptions persist; no response-derived text, image, quotation, or per-check trace may enter the accumulated store; entries carry no date-to-roster linkage; district-defined small-cluster suppression applies.* Verify with fixtures.

## F6 — The provider citation is already aging *(citation refinement, medium)*

The plan's inference boundary (section 6.7) cites GPT-4o and developers.openai.com documentation while the operating environment is *district Azure OpenAI*. Model-specific claims will be stale within a release cycle; Azure deployment attestation, data-handling terms, and available model versions are documented by Microsoft, not by OpenAI's developer site, and differ in ways the plan's own Gate 3 cares about.

**Remedy:** Strike model-name claims from the architecture. The authority is already correctly placed in `IInferenceProvider` plus capability tests — image input, structured output, refusal behavior — run against *the configured deployment*. Cite Azure OpenAI documentation for attestation evidence at Gate 3. Record model/deployment identity per evaluated release, exactly as the plan already requires.

## F7 — Local inference is under-promised for a liberation project *(strategic gap, high)*

The plan's liberation test requires that the program "can support more than one inference provider and can perform deterministic authoring without any model." Deterministic offline authoring is honored throughout — good. But cloud inference through a district Azure contract is the *only* named model path, which fails the school with no contract, the homeschool cooperative, the teacher in a country where the contract cannot exist. Grammar-constrained structured output from locally run open-weight models is now practical on ordinary hardware; a GPL project of this ambition should name it.

**Remedy:** Promote the local-model path from "optional" to a named roadmap item — a `Foundry.Inference.Local` adapter with the same capability-test kit, targeted after 0.3 and before 1.0. No module may depend on it; every module must degrade gracefully to deterministic authoring without it. This is the difference between *district-governable* and *district-dependent*.

## F8 — WinForms-first needs an accessibility guardrail *(engineering caveat, medium)*

Keeping WinForms for the first slices is the right economy. But WinForms accessibility collapses exactly where teams customize: owner-drawn controls, custom panels, and hand-built lists expose nothing to UI Automation, and the accessibility contract (section 8) is then unmeetable at any price of retrofit.

**Remedy:** Bind a rule from day one: *standard controls only, no owner-draw, until the accessibility test harness exists and each custom control ships with its own UIA automation peer and NVDA/Narrator evidence.* Budget the review surface — the most novel UI in the program — for early assistive-technology walkthroughs, not late ones.

## F9 — Amber ergonomics will collide with the no-autosave contract *(usability risk, medium)*

The Amber contract rightly forbids autosave, recovery journals, and recent-file lists. But Rubric Relay and Exit Lens reviews are long; a crash at minute forty of a careful field-level review destroys teacher work and teaches teachers to rush the gate — the opposite of the design intent.

**Remedy:** Do not weaken the contract now. Instead: measure review-session length and interruption cost in the seeded-error and think-aloud studies; if the fragility is real, design a *district-approvable, teacher-edit-only journal* — persisting only text the teacher authored, never source images or model output — as an explicit Amber-architecture decision with its own retention rule. Record this as an open question in section 20 of the plan.

## F10 — The open project format should be legible without the application *(reversibility, medium)*

`.ocfproj` is openly documented ZIP/JSON — good. But JSON legibility is programmer legibility. The liberation test says outputs "can migrate to accessible, editable formats outside the application"; the strongest form of that promise is that a Green project *carries its own readable rendering*.

**Remedy:** For Green-lane projects, embed an accessible, self-contained HTML snapshot of the approved artifact (and a plain-text manifest summary) inside the package. A teacher a decade hence, with no Foundry installed, opens the file and reads the work. Amber projects, which are not saved with content, are unaffected.

## F11 — The bus factor is one *(sustainability, high)*

The plan estimates 96–140 developer-weeks for a single developer and names a long-term-maintainer decision (section 20, item 12), but the Definition of Done has no sustainability evidence and the roadmap no succession milestone. A liberation project that dies with its maintainer liberates no one.

**Remedy:** Add to the universal Definition of Done: *published governance document, contribution guide tested by an outside contributor, and a named second maintainer (or a documented recruitment attempt) by Release 0.3.* Add a standing goal: every module's recipe pack should be maintainable by a curriculum-literate contributor who is not the lead developer.

## F12 — Internal inconsistency in the atlas's first-release bundle *(editorial, low)*

Atlas line 78 names TaskDock among the five bundled modes while the twelve replace it with Scaffold Smith; the plan's Release 0.2 also moves Access Remix to 0.4, diverging from the atlas's Teacher Foundry 0.2. Harmonize both documents to the plan's sequence once F2 is decided.

## F13 — Trademark screening should begin with the names most likely to collide *(naming, low)*

All names are declared working titles — correct. Screen first the ones carrying the most public surface: **All Aboard** (a crowded name space including education and transit apps), **Lesson Loom** (adjacent to a famous video product), **Symbol Commons** (check against existing symbol-set projects). The atlas's own practice of load-bearing subtitles ("Visual Support Studio") is the right insurance; keep the subtitle as the functional identity everywhere.

---

# 3. Consolidation: two hundred ideas, one engine, about a dozen presses

The atlas's own conclusion — "one engine, not 200 codebases" — deserves to be made precise, because the difference between 200 products and 12 engines plus preset packs is the difference between a lifetime of unfinished work and a finishable cathedral.

**An engine** owns machinery: an editor surface, validators, renderer templates, and invariants. **A preset** is a data-only recipe that re-aims an existing engine. Sorting all 200 (and the two repaired entries) by that test:

| Engine family | Core machinery owner | Absorbed as presets (representative) | Treatment |
|---|---|---|---|
| Visual Support Press | All Aboard (#1) | First/Then Press #2, Choice Foundry #3, Agency Deck #7, Social Lens #9 (H), Change Preview Press #29, Room Route #39, Newcomer Maproom #145 | Build engine; presets staged by co-design |
| AAC-adjacent, governed | — | Core & Context #4, SceneSpeak #8, Partner Pause #5, Conversation Launchpad #6 | Defer `[R]`/`[H]` entries; specialist co-design before any build |
| Capture-to-Document | Board to Brief (#31) | Worksheet Unwrapper #32, Artifact to Anchor #33, Gallery Walk Maker #34, Station Smith #35, Model Maker #36, Manipulative Mapper #38, Visible Thinking Camera #199 | Build engine; Amber members wait for Amber architecture |
| Access Transformation | Access Remix (#11) | PageQuiet #12, MotorEase #17, Colorless Key #19, FormFlex #20, Feedback Translator #67 | Build engine |
| Description & Alt-Media Bench | AltText Atelier (#13) | Diagram Distiller #14, Tactile Keymaker #15, Math Access Forge #16, CaptionBench #18 | Distinct bench; braille/tactile claims deferred per plan |
| Executive Function Scaffolds | Scaffold Smith (#201) | TaskDock #21, TimeFold #22, Start Line #23, Done Definition #24 (merge with Success Criteria Studio #70), Focus Beacon #25, Plan B Press #26, Absence Catch-Up #133 | Build engine; F2 absorbs TaskDock |
| Predictability & Regulation | — | Sensory Forecast #27, Regulation Menu #28 (both H); Workload Weather #30 | Co-design presets; #30 stays deferred `[R]` |
| Lesson Design | Lesson Loom (#41) | Standards Unpacker #42, Unit Spine #43, Bell-to-Bell #44, Backward Design #45, Spiral Planner #47, Prerequisite Radar #48, Materials Minimalist #49, Calendar Fit #173 | Build engine |
| Formative Evidence (Amber) | Exit Lens (#51) | Hinge Question Forge #52, Whiteboard Sweep #53, Quick Check Deck #54 (G), Reteach Router #55, Distractor Designer #56, Probe Ladder #59, Misconception Atlas #60 (F5) | Engine waits for Amber architecture; Green members can ship earlier |
| Feedback & Rubric (Amber) | Rubric Relay (#61) | Conference Compass #62, Revision Roadmap #63, Comment Bank Gardener #64 (G), One-Point Rubric #65 (G), Calibration Room #66 | Engine waits for Amber; Green members earlier |
| Discussion Design | Talk Moves Studio (#202) | Discussion Role Wheel #136, Talk Moves Loom #144, Civic Deliberation Studio #105, Policy Tradeoff Tabletop #110 | Build engine |
| Bilingual & Family Press | Directions Duet (#141) / Family Bridge (#148) | Lesson Bridge #142, Cognate Cartographer #143, Glossary Garden #147, Translation QA #150; Interpreter Prep #149 stays `[R]` | Build both on one alignment core |
| Source & Inquiry | Source Lens (#101) | Perspective Matrix #102, Corroboration Coach #106, Context Builder #107, Counterclaim Workshop #108, Counterfactual Guardrail #195, Source Reliability Lab #186 | Build engine |
| Commons & Rights Kernel | Symbol Commons (#10) / Open Resource Packager (#161) | Rights Checker #162, Template Forge #163, Remix Map #164, Item Commons #167, Provenance Ledger #168, Translation Memory #169, Course Kit #170 | Foundation-adjacent; build early and small |
| Subject recipe packs | (no new engines) | Literacy #71–80, Mathematics #81–90, Science #91–100 (safety text always locked), Arts/PE/CTE #111–120, PBL #121–130, Routines & Operations #131–140 + #171–180, Professional Learning #151–160, Library & Research #181–190 | Data-only packs on existing engines, released by curricular review capacity |
| Incubator | — | Question Genome #191, Misconception Theater #192, Classroom Twin Lens #193, Constraint Orchestra #194, Analogy Test Kitchen #196, Learning Path Composer #197, Simulation Press #198, One-Room Schoolhouse #200 | Hold; #191 and #196 promote cleanly to Green presets when capacity allows |

The punchline the atlas gestured at, stated exactly: **the 200 resolve into about twelve engines plus one shared kernel, with everything else as data-only presets.** Nothing in the original atlas requires a thirteenth engine — except the one it forgot, which follows.

---

# 4. Refinements to the twelve

Tight refinements only; each module's plan specification otherwise stands as audited.

**All Aboard.** Add a *physical craft library* to the spec: standard hook-and-loop strip dimensions, lamination bleed margins, finger-space cut allowances, ring-binding hole templates, and card-corner radii. Teachers judge visual supports with scissors in hand; print-perfect physical ergonomics is half the product. Also specify a "board sizes" preset list gathered from the educator council's real classrooms.

**Board to Brief.** The uncertain-token surface is the heart of the module; specify its keyboard grammar early (jump-to-next-uncertain, accept, retype, mark-illegible) and test it with NVDA before any other UI polish. Add a low-light and marker-color fixture family — real whiteboards are photographed at 3:40 p.m. under fluorescent glare.

**Scaffold Smith.** After F2's absorption of TaskDock, require every generated scaffold packet to carry its own *removal plan* as a first-class artifact (already implied by the fade criterion); print it on the teacher page so temporariness is visible, not aspirational.

**Access Remix.** The construct-change warning is the module's conscience; make it non-dismissable in the review surface (acknowledged, not hidden). Add a fixture where the *correct* behavior is refusal — a formal assessment sneaked in as a worksheet.

**Directions Duet.** Version the approved glossary and stamp each artifact with the glossary version used; a district's terminology changes midyear and silent drift across handouts is a real failure mode.

**Exit Lens.** Close the loop with Hinge Question Forge explicitly: yesterday's approved cluster summary should be offerable as *tomorrow's hinge-question seed* without persisting any response-level data — the summary is already teacher-approved Green output. This is the highest-value legal reuse of Amber work.

**Lesson Loom.** Add deterministic timing arithmetic as a validator (minutes sum, transitions counted, closure protected) so the model never does math the engine can check.

**Rubric Relay.** Frame the output as *conference preparation* everywhere in the UI, never as "feedback generation" — the two conference questions are the product; the matrix is their evidence. This framing is the difference between a tool teachers defend and one they hide.

**Talk Moves Studio.** The post-discussion equity reflection should be a printable teacher-only card, not a screen — it gets used standing up, ninety seconds after the bell.

**Family Bridge.** Add a readability target to acceptance (plain-language reading level verified deterministically for the source letter) and a "one requested action per communication" lint warning.

**Symbol Commons.** The ambiguity registry ("recorded disagreement rather than erasure") deserves promotion to a first-class feature: symbols carry *known-divergent readings* visible at insertion time. This is scholarly honesty as a UI affordance.

**Source Lens.** Specification is the strongest of the twelve; the only addition is a *sensitivity preflight* checklist item naming the teacher's local review duty for traumatic content, mirrored from the invariants into the workflow surface.

---

# 5. The forgotten studio, and why it should be built first

The atlas's engine promises "offline use for every deterministic function; AI as a bounded accelerator." Yet all 200 ideas assume interpretation — a model, an OCR pass, a transformation. Not one idea in the atlas is a pure press. This is the blind spot of an age that reaches for inference by reflex.

Consider what a **Deterministic Press** studio is:

- **Zero privacy risk.** No lane above Green is even expressible; there is no capture, no inference, no egress. Gate A and Gate C have nothing to inspect.
- **Zero district friction.** No AI policy, no Azure contract, no attestation. Any teacher, any school, any country, today.
- **Total pipeline exercise.** Projects, semantic documents, renderers, print, export, bilingual layout, accessibility, provenance, the approval boundary — everything but `IInferenceProvider` gets built, tested, and hardened on the safest possible cargo.
- **Immediate daily value.** Teachers buy graph paper, ten-frames, handwriting sheets, and flashcard stock with their own money *right now*.
- **Trust before power.** The first thing a district evaluates should be the thing that cannot hurt anyone. The Foundry earns its Amber lane by being flawless in its paper lane.

**Recommendation:** Add the Deterministic Press as *Module Zero*. Fold its first three presses into Release 0.0/0.1 as the test cargo for the rendering and project pipeline (they replace synthetic fixtures with real value), and ship the full studio as a standalone-worthy release. If the whole program stopped after Module Zero, the world would still have received a genuinely useful GPL gift — that is the correct floor for a liberation project.

---

# 6. The addendum: entries 201–227

Entries 201–202 repair finding F1. Entries 203–227 are new. Lane tags and the `H` convention follow the atlas. All names are working titles pending screening.

## Repairs

201. **Scaffold Smith** `[G]` — Turn an existing task into temporary, removable supports — hint ladders, banks, checkpoints, entry scaffolds — without changing the learning target, each scaffold carrying its barrier, preserved demand, and fade criterion. *(Studio V; absorbs TaskDock #21 per F2.)*
202. **Talk Moves Studio** `[G]` — Design equitable, intellectually productive discussion: question sequences with evidence targets, facilitation and learner cards, multimodal participation pathways, and a post-discussion equity reflection. *(Studio VI.)*

## Studio XXI — The Deterministic Press *(zero inference, pure craft)*

203. **Blankforms Press** `[G]` — Parameterized print-perfect classics: graph paper, coordinate grids, number lines, ten-frames, clock faces, music staves, Cornell notes, lab tables, calendars, and cut-and-fold booklet blanks.
204. **Handwriting Foundry** `[G]` — Tracing, letter-formation, and practice sheets from any word list, with guide styles, dotted-to-faded progressions, and multiple scripts.
205. **Manipulative Mint** `[G]` — Cardstock press for fraction strips, algebra tiles, base-ten blocks, tangrams, dice nets, and spinners, with cut-efficient layouts and assembly guides.
206. **Flashcard Flywheel** `[G]` — Registration-safe double-sided card presses from teacher lists, with spaced-retrieval sort-box labels and self-check formats.
207. **Foldables Foundry** `[G]` — Interactive-notebook foldables, flipbooks, and layered organizers with cut/fold guides, generated from teacher content.
208. **Booklet Binder** `[G]` — An imposition engine that turns any approved artifact into correctly ordered saddle-stitch booklets, doing the signature arithmetic teachers do wrong at the copier.
209. **Big Print Shop** `[G]` — Tile any approved artifact across multiple pages into wall-scale displays with alignment marks and assembly maps.
210. **Label Lathe** `[G]` — Consistent series of classroom labels, bin cards, and station signs with optional symbols and bilingual pairs, in sheets matched to common label stock.

## Studio XXII — Computational thinking *(the missing subject)*

211. **Unplugged Algorithm Atelier** `[G]` — Turn a classroom routine or game into sequencing, branching, and loop cards that learners execute as human programs, with debugging discussion prompts.
212. **Parsons Press** `[G]` — Scramble a teacher-supplied working solution into line-ordering puzzles with optional distractor lines, difficulty staging, and a discussion key.
213. **Trace Table Tutor** `[G]` — Generate variable-trace tables, predict-the-output prompts, and check-your-trace keys from teacher-supplied code snippets, preserving the code exactly.
214. **Bug Zoo** `[G/A]` — Curate teacher-authored or de-identified buggy programs into diagnose-repair-explain exercise sets with misconception rationales.
215. **Rubber Duck Deck** `[G]` — Printable self-explanation and debugging-protocol cards that teach learners to interrogate their own reasoning before asking for help.

## Studio XXIII — Subjects the atlas under-served

216. **Story Listening Loom** `[G, H]` — Comprehensible-input story scaffolds for world-language teaching: tiered glossaries, picture-support frames, and retell structures around a teacher-told story.
217. **Notation Bench** `[G, H]` — Rhythm cards, sight-reading lines, fingering charts, and staff worksheets from teacher parameters, with a deterministic engraving core and specialist review of pedagogy.
218. **Field Journal Forge** `[G]` — Nature and field-learning kits: observation frames, specimen labels, weather and phenology logs, and site-map pages for outdoor education.
219. **Budget Basecamp** `[G, H]` — Financial-literacy scenario kits with locked arithmetic, teacher-verified real-world figures, and decision-comparison organizers.
220. **Health Decision Deck** `[G, H]` — Bounded health-education scenario cards built only from district-approved curriculum language, with locked factual claims and explicit help routes.
221. **Paper Circuits Studio** `[G, H]` — Printable circuit templates, component maps, and locked safety text for maker and CTE classrooms.

## Studio XXIV — Learner-held self-direction *(the learner keeps the record)*

222. **Goal Post** `[G→R]` — Learner goal-setting and self-monitoring sheets designed to live in the learner's own folder — never in a data system — with review dates and self-selected evidence lines.
223. **My Strategy Shelf** `[G]` — Personal strategy card kits a learner assembles and edits: reading repairs, math checks, focus resets, and help scripts, chosen by the learner from teacher-offered sets.
224. **Portfolio Passport** `[G]` — A paper self-curation kit: selection slips, caption frames, growth-reflection pages, and a table of contents the learner maintains — the identified longitudinal record exists only in the learner's hands, resolving what F4 defers.

## Studio XXV — Measurement craft

225. **Parallel Forms Press** `[G, H]` — Generate a parallel version of a teacher-authored check — same constructs, different surface features — with a construct-map showing item-to-item correspondence for teacher verification.
226. **Retrieval Grid Generator** `[G]` — Spaced-retrieval grids and mixed warm-ups drawn deterministically from a teacher's own prior-unit question bank, with scheduling suggestions.
227. **Item Doctor** `[G, H]` — Examine a teacher-authored assessment item for cueing, double negatives, construct-irrelevant load, ambiguous stems, and implausible distractors, proposing repairs the teacher approves item by item.

---

# 7. Cross-cutting enrichments

Proposed as amendments to the shared pedagogical constitution and engine contract:

1. **Amendment — the paper-first guarantee.** Every module's primary output must be fully usable in a classroom with zero learner devices. Screens may enrich; paper must suffice.
2. **Amendment — the time-to-artifact budget.** Every recipe declares a target time from intake to approved artifact (All Aboard already implies five minutes). The UI displays it; pilots measure it; a recipe that cannot meet its budget is redesigned, not excused.
3. **The minimum hardware covenant.** Name a floor — on the order of a 2015-era CPU, 8 GB RAM, 1366×768 — and keep one such machine in the test bench permanently. Donated hardware is where liberation actually happens.
4. **Project legibility (F10).** Green `.ocfproj` packages embed an accessible HTML snapshot and plain-text summary, readable forever without the Foundry.
5. **The local path (F7).** `Foundry.Inference.Local` becomes a named post-0.3 target with the same capability-test kit; no module may require it, and none may require the cloud either.
6. **The guild (F11).** Governance published early; a second maintainer sought by 0.3; recipe packs maintainable by curriculum-literate contributors who are not the lead developer.
7. **Amber ergonomics study (F9).** Review-session fragility measured in pilots before any journaling decision.

---

# 8. Amendments proposed to the audited plan

Concrete edits, keyed to the plan's own sections:

1. **§1 (sequence):** Insert "Deterministic Press module zero" between "Shared Green-lane foundation" and "Minimal asset/provenance kernel"; its first presses serve as the rendering pipeline's real cargo.
2. **§6.7:** Replace GPT-4o-specific sentences with provider-capability language per F6; move model citations to a Gate 3 attestation appendix citing Azure OpenAI documentation.
3. **§10:** Add module specifications for the Deterministic Press (trivially small: inputs are parameters; invariants are dimensional accuracy and print fidelity; no gates beyond Gate B in its lightest form) and record the TaskDock absorption in §10.3.
4. **§11 (Definition of Done):** Add sustainability evidence per F11.
5. **§13 (roadmap):** Fold Deterministic Press presses into Releases 0.0–0.1; add `Foundry.Inference.Local` spike after 0.3; note the hardware-floor bench.
6. **§18 (risk register):** Add "Maintainer loss / project abandonment — High — Maintainer — governance doc, second maintainer, buildable-from-source guarantee" and "Accumulating-store re-identification — High — Privacy — F5 invariant and fixtures."
7. **§20 (decisions):** Add: (13) local inference posture and target hardware; (14) minimum supported hardware floor; (15) Amber teacher-edit journaling question (F9); (16) TaskDock absorption ADR (F2).
8. **Atlas regeneration:** Apply F1, F3, F4, F5, F12; append entries 201–227; retitle to the honest count of 227.

---

# 9. Final judgment

The atlas dreamed generously and the audit disciplined the dream without killing it — that is rare and should be said plainly. What this review adds is an order of construction worthy of a workshop rather than a manifesto: repair the small dishonesties of numbering before they calcify; admit that two hundred ideas are twelve engines and a library of presets; and lay the first stone with the tool that cannot harm anyone — the press that needs no oracle, only good geometry and honest ink.

Build the Deterministic Press, then All Aboard, and let every later power be measured against the trust those two establish. The machine must never appear to be the master teacher. It should aspire to be what the best apprentice always was: swift, exact, tireless, humble about what it does not know — and utterly incapable of signing the work in the master's name.

*— Da Vinci, for the typist and the commons, 29 August 2026*
