# Honest Ink — the classroom foundry

## An atlas of 227 free teacher-tool possibilities descended from Writer's Kiosk

**Atlas version:** 2.0 — regenerated 29 August 2026, applying The Master's Review 1.0 (findings F1–F5, F12–F13; addendum entries 201–227; build order harmonized with implementation plan 2.0)

**Selection status (30 August 2026):** This atlas is a possibility register, not an engineering queue. After the standing forge work, the next entry or newly possible composition must come from a real, needs-first educator-council session using the [Atlas 2.0 priority-session packet](council/atlas-priority-session.md), followed by a separate feasibility record and written product-owner disposition. Atlas order, rehearsal personas, and generator-created menus do not substitute for that human record.

Working vision: **GNU GPL-3.0-or-later software, local-first by instinct, district-governable when cloud inference is used, bounded in purpose, editable by the teacher, and capable of turning the physical classroom into useful instructional artifacts.**

The central discovery is that Writer's Kiosk is not merely one application. It establishes a reusable educational grammar:

> **Capture or import → interpret → constrain → scaffold → let the teacher verify → render → print/export → preserve only what is worth preserving.**

Writer's Kiosk already supplies much of the difficult machinery: live camera capture, image correction, subject/grade/band profiles, assignment focus, aligned bilingual rendering, bounded generation, PDF creation, silent printing, local logging, keyless district authentication, and an in-memory image path. The teacher-facing family should reuse those strengths while adding an indispensable **edit-and-approve studio** between generation and publication.

Most names in this atlas remain working titles, not trademark clearances. ADR-008 fixes six public display identities and ADR-009 corrects the lesson-design display: SequenceSlate, StrandPlan, Forumwright, ReteachSignal, Inquirywright, and KinDispatch. Their legacy internal identifiers remain unchanged. Every product keeps a load-bearing subtitle as its functional identity, and counsel screening remains a pre-release checkpoint.

## The three data lanes

Teacher-facing does not by itself make student-data duties disappear. The freedom to save and reuse products comes chiefly from designing most tools around teacher-created, public-domain, openly licensed, or genuinely de-identified inputs.

| Lane | Typical material | Default behavior |
|---|---|---|
| **Green (G)** | Standards, lesson plans, teacher-created directions, staged materials, empty classroom spaces, generic access settings | May be saved to a teacher-controlled project library; source and output remain editable |
| **Amber (A)** | Student work, class response sets, photographs that may include learners, indirect identifiers | Preview/crop/redact first; raw capture ephemeral; save only a teacher-approved de-identified derivative in district-approved storage |
| **Restricted (R)** | IEP/504 information, diagnoses, communication profiles, medical or behavioral records, private schedules, disclosures | Do not collect in an early release; require an explicitly district-governed design, minimum-necessary access, retention rules, and specialist review |

The lane tags below describe the safest ordinary implementation, not a legal conclusion. `H` means that a human specialist or instructional team should verify the product.

## Flagship concept: SequenceSlate

**SequenceSlate — Visual Support Studio** is the public display name selected by ADR-008 and the clearest next descendant of Writer's Kiosk. Its promise is simple:

> Lay out a real classroom task, photograph it, confirm the steps, and print the visual support the learner can use immediately.

### Teacher flow

1. Photograph a staged activity, workstation, worksheet, route, set of choices, or classroom materials; alternatively import images or type the task.
2. Crop the frame. The program locally flags possible faces, names, screens, barcodes, and background details for removal.
3. Choose an output mode: **First/Then**, **Now/Next/Done**, **task strip**, **visual schedule**, **choice board**, **station directions**, **change preview**, **social narrative**, or **context communication board**.
4. The program proposes objects and steps. The teacher adds, deletes, reorders, or replaces every item and selects symbol density, wording, reading level, home language, print size, and access mode.
5. A verification screen requires teacher approval. The program then prints or exports the selected packet.
6. The reusable teacher-created product may be saved; the source photograph disappears by default.

### A useful default packet

- Learner-facing visual task strip with three to eight one-action steps
- First/Then and Now/Next/Done variants
- Materials, help, break, different, stop, and finished cards
- Cut lines, optional hook-and-loop placement guides, and large-display layout
- Aligned bilingual wording
- Teacher-only prompt-and-fade guide that aims toward independence
- Editable PDF plus an open project file with asset provenance

### Boundaries that make it worthy

- It should be described as a **visual-support and picture-based communication authoring tool**, not as a PECS® application. PECS is a specific six-phase protocol, not a generic name for picture cards.
- It must preserve refusal, repair, help, stop, and "different" as valid communication—not make every board a compliance device.
- It must not infer diagnosis, emotion, intent, or a learner's preferred vocabulary from an image.
- It should supplement, never silently replace or rearrange, an established AAC system.
- Hazardous procedures must use teacher-entered or locked school-approved safety language, not invented safety instructions.
- Bundled symbols must be original or openly licensed with recorded provenance. Educators may import symbol sets they are entitled to use, but the program must not redistribute proprietary libraries.

The load-bearing subtitle remains essential because the coined name does not reveal the product by itself. The stable module, recipe, schema, localization, and project identifiers retain `all-aboard` by ADR-008; that token is compatibility identity, not current branding.

## The first twelve worth building

1. **SequenceSlate — Visual Support Studio** — the flagship; closest architectural sibling to Writer's Kiosk and a genuine unmet daily need.
2. **Board to Brief** — camera-to-clean-handout utility with exceptionally broad use.
3. **Scaffold Smith** — turns an existing task into temporary supports without changing the learning target.
4. **Access Remix** — produces large-print, reduced-clutter, chunked, symbol-supported, and bilingual variants from one teacher-created source.
5. **Directions Duet** — exploits the existing aligned bilingual renderer for classroom directions.
6. **ReteachSignal — Formative Evidence** — converts anonymous exit tickets into tomorrow's reteach and extension moves.
7. **StrandPlan — Lesson Design Studio** — the high-frequency teacher-planning studio.
8. **Rubric Relay** — makes evidence, criteria, and revision moves visible before the teacher approves feedback.
9. **Forumwright — Discussion Design** — designs equitable, intellectually productive discussion.
10. **KinDispatch — Bilingual & Family Press** — transforms teacher-authored information into clear, parallel family communications.
11. **Symbol Commons** — supplies the open, provenance-aware image foundation for all visual-support products.
12. **Inquirywright — Source & Inquiry** — a disciplined social-studies inquiry maker and a natural first subject-specific module.

A pragmatic first release would place **SequenceSlate, Board to Brief, Access Remix, Directions Duet, and Scaffold Smith** inside one application as five output modes rather than maintaining five executables. Scaffold Smith includes the task-entry scaffold ratified by ADR-005; TaskDock survives only as the historical title of Atlas entry 21 and as a stable record reference.

Preceding the twelve is **Module Zero: the Deterministic Press** (entries 203–210) — a zero-inference printable studio that exercises the entire pipeline with no privacy risk, no district friction, and immediate daily value. It is built first, and every later module is measured against the trust it establishes.

---

# The 227-candidate atlas

## Studio I — Visual supports, picture-based communication, and AAC

1. **SequenceSlate — Visual Support Studio** `[G→R, H]` — Photograph a real activity and make teacher-confirmed task strips, First/Then cards, choice supports, finished markers, and bilingual visual directions.
2. **First/Then Press** `[G→R]` — Use two captures or two selected images to create First/Then/After strips with completion and transition cues.
3. **Choice Foundry** `[G→R]` — Photograph genuinely available options and make a choice board that always preserves none, not now, different, stop, and help.
4. **Core & Context** `[R, H]` — Draft a lesson-specific AAC board that preserves stable core-word placement and adds teacher-approved fringe vocabulary.
5. **Partner Pause** `[G→R, H]` — Print lanyard-size reminders for adults to model, ask once, wait, notice every response mode, affirm, and offer repair.
6. **Conversation Launchpad** `[G→R, H]` — Turn a book, artwork, experiment, or object into a board balanced across commenting, questioning, joking, rejecting, repairing, and requesting.
7. **Agency Deck** `[G]` — Generate consistent stop, help, different, wait, break, all-done, consent, and do-not-know cards in multiple formats and languages.
8. **SceneSpeak Studio** `[R, H]` — Place teacher-selected vocabulary hotspots onto a classroom scene and export a visual-scene display plus printable companion.
9. **Social Lens** `[G→R, H]` — Build factual, choice-respecting social narratives that describe possibilities and help routes without claiming to know anyone's thoughts.
10. **Symbol Commons** `[G]` — Maintain a local, searchable, provenance-aware library of teacher photographs and openly licensed symbols with open export formats.

## Studio II — Document, media, and sensory accessibility

11. **Access Remix** `[G]` — Convert a teacher-created worksheet into large-print, high-contrast, reduced-clutter, chunked, symbol-supported, and bilingual variants.
12. **PageQuiet** `[G]` — Reflow a dense page with whitespace, one item per panel, line-focus guides, cover windows, and optional illustration suppression.
13. **AltText Atelier** `[G, H]` — Draft identification-only, teaching-rich, and assessment-safe image descriptions for teacher verification.
14. **Diagram Distiller** `[G, H]` — Turn a diagram into a simplified rendering, numbered key, stepwise reveal, vocabulary list, and verbal-description script.
15. **Tactile Keymaker** `[G, H]` — Create a high-contrast vector draft, tactile-production plan, ordered key, and braille-label placeholders from a map or diagram.
16. **Math Access Forge** `[G, H]` — Synchronize teacher-created mathematics across print notation, LaTeX, MathML, spoken-math text, and a specialist-review braille draft.
17. **MotorEase** `[G→R, H]` — Produce equivalent keyboard, pointing, eye-gaze, switch-scan, oral, and scribe-friendly response formats.
18. **CaptionBench** `[G, H]` — Create an editable transcript, captions, speaker labels, key-term preview, and sign-language-inset placeholder from teacher media.
19. **Colorless Key** `[G]` — Detect meaning conveyed only by color and add labels, patterns, contrast, and monochrome-safe legends.
20. **FormFlex** `[G]` — Convert a paper handout into a labeled, logically tabbed accessible form without discarding the original print design.

## Studio III — Executive function, predictability, and regulation

21. **TaskDock** `[G→R]` — Turn an assignment into materials, first action, chunks, checkpoints, help routes, and a concrete definition of done. *(Absorbed as the Scaffold Smith task-entry preset per ADR-005; retained here as the idea's record.)*
22. **TimeFold** `[G→R]` — Add a deadline and create a backward plan with dependencies, estimates, buffers, and teacher-adjustable milestones.
23. **Start Line** `[G→R]` — Reduce a task to a 30-second entry action, prepared-workspace card, and minimal first screen.
24. **Done Definition** `[G]` — Combine an assignment, rubric, and exemplar into a completion checklist with examples, nonexamples, and final self-check.
25. **Focus Beacon** `[G→R]` — Present only Now, Next, Ask, and Done as a stable printable or full-screen anchor.
26. **Plan B Press** `[G→R]` — Print what changed, what stayed, available choices, and where to get help when a routine shifts.
27. **Sensory Forecast** `[G→R, H]` — Describe teacher-selected noise, light, movement, texture, smell, and crowding features and list available supports without predicting feelings.
28. **Regulation Menu** `[G→R, H]` — Turn approved spaces and tools into a self-advocacy menu for quiet, movement, headphones, water, pressure, help, or another option.
29. **Change Preview Press** `[G→R]` — Rapidly compare old and new room, schedule, person, or event images and make a just-in-time change preview.
30. **Workload Weather** `[R, H]` — Show a privacy-controlled calendar heat map of estimated demands, collisions, buffers, and recovery time across courses.

## Studio IV — Camera and physical-artifact transformation

31. **Board to Brief** `[G]` — Turn a whiteboard, slide, agenda, or anchor chart into clean sequenced directions, vocabulary, due-date callouts, and accessible handouts.
32. **Worksheet Unwrapper** `[G]` — Reconstruct a crowded teacher-created worksheet as chunked pages with enlarged response areas, checkboxes, and alternate response modes.
33. **Artifact to Anchor** `[A]` — Convert a de-identified work sample into an annotated exemplar, success-criteria display, and noticing questions.
34. **Gallery Walk Maker** `[A]` — Prepare de-identified photographed artifacts as numbered displays, observation prompts, feedback cards, and a rotation plan.
35. **Station Smith** `[G]` — Photograph laid-out station materials and produce visual directions, timing, cleanup, rotation, and troubleshooting cards.
36. **Model Maker** `[G/A]` — Turn a completed product into an annotated model, partially faded practice version, blank version, and reflection sequence.
37. **Lab Bench Coach** `[G, H]` — Document a correct apparatus setup and produce labels, preparation, approved safety checks, and cleanup sequence.
38. **Manipulative Mapper** `[G]` — Connect photographed manipulatives to mathematical language, representations, notation, questions, and extensions.
39. **Room Route** `[G→R]` — Build visual movement routes, location cards, transition steps, and environmental signs from approved empty-room photographs.
40. **Book Page Builder** `[G, H]` — Turn an authorized excerpt into a teacher guide with preview vocabulary, stopping points, comprehension prompts, and a response task.

## Studio V — Lesson and unit design

41. **StrandPlan — Lesson Design Studio** `[G]` — Arrange an objective, time, materials, and constraints into launch, modeling, practice, checks, closure, and access supports.
42. **Standards Unpacker** `[G]` — Translate a standard into concepts, skills, vocabulary, prerequisites, success criteria, and acceptable evidence.
43. **Unit Spine** `[G]` — Build a conceptual unit arc, lesson sequence, checkpoints, flex days, and culminating evidence from standards and a calendar.
44. **Bell-to-Bell** `[G]` — Produce a realistic minute-by-minute lesson with transitions, contingency cuts, and protected closure.
45. **Backward Design Board** `[G]` — Begin with transfer and evidence, then draft assessments, learning experiences, and prerequisite checks.
46. **Co-Teaching Choreographer** `[G→R, H]` — Plan purposeful educator roles across station, parallel, alternative, team, and one-teach/one-observe structures.
47. **Spiral Planner** `[G]` — Reintroduce priority learning through a retrieval calendar, mixed warm-ups, and cumulative checks.
48. **Prerequisite Radar** `[G]` — Map likely foundations, create a short diagnostic, and propose bridge activities before a new target.
49. **Materials Minimalist** `[G]` — Redesign a lesson around the inventory and low-cost materials the teacher actually has.
50. **Lesson Repair Bench** `[G/A]` — Analyze a plan plus de-identified response evidence and generate revised sequencing, checks, and a retry experiment.

## Studio VI — Formative assessment and responsive teaching

51. **ReteachSignal — Formative Evidence** `[A]` — Read anonymous exit tickets and return misconception clusters, instructional routes, a reteach mini-lesson, and next-day hinge questions.
52. **Hinge Question Forge** `[G/A]` — Create one mid-lesson question whose options expose distinct reasoning paths and connect each path to an immediate response.
53. **Whiteboard Sweep** `[A]` — Analyze anonymous mini-whiteboard images into response distributions, error clusters, and discussion prompts.
54. **Quick Check Deck** `[G]` — Generate oral, written, gesture, diagram, selected-response, and demonstration checks for the same target.
55. **Reteach Router** `[A]` — Match an anonymous evidence summary to the smallest useful conference, small-group, whole-class, practice, or extension move.
56. **Distractor Designer** `[G/A]` — Turn observed misconceptions into diagnostic multiple-choice options with rationales and follow-up questions.
57. **Confidence × Correctness Grid** `[A]` — Separate confident errors, uncertain success, confident success, and guesses to guide different instructional responses.
58. **Concept Sort Studio** `[G]` — Produce examples, nonexamples, ambiguous cases, sorting rules, and a teacher debrief around a concept boundary.
59. **Probe Ladder** `[G/A]` — Build a short ordered sequence that identifies the first breakdown in a prerequisite chain.
60. **Misconception Atlas** `[A]` — Accumulate de-identified patterns across checks and map misconception families, prerequisites, trends, and diagnostic probes. *(Accumulation invariant: only teacher-authored pattern descriptions persist — no response-derived text, image, quotation, or per-check trace; no date-to-roster linkage; small-cluster suppression applies.)*

## Studio VII — Feedback, rubrics, and revision

61. **Rubric Relay** `[A]` — Connect evidence in a work sample to rubric criteria and draft strengths, revision priorities, and conference questions for teacher approval.
62. **Conference Compass** `[A]` — Prepare one focused conference with an opening question, evidence to notice, one teaching point, and an agreed next step.
63. **Revision Roadmap** `[A]` — Turn teacher comments into an ordered, estimated, checkpointed revision plan.
64. **Comment Bank Gardener** `[G]` — Organize a teacher's own feedback language by criterion, stage, misconception, tone, and next move.
65. **One-Point Rubric Maker** `[G]` — Center a rubric on concise proficiency criteria with room for evidence below, meeting, and beyond.
66. **Calibration Room** `[A, H]` — Package de-identified anchors, scoring protocols, disagreement notes, and clarified rubric guidance for educator calibration.
67. **Feedback Translator** `[A]` — Recast approved teacher feedback in plain-language, bilingual, audio-script, symbol-supported, and checklist forms.
68. **Peer Feedback Builder** `[G]` — Create a bounded peer-response protocol, sentence stems, evidence rules, and an author decision sheet.
69. **Portfolio Narrator** `[A→R, H]` — Arrange locally governed artifacts into an evidence timeline and draft growth statements, questions, and next goals. *(A portfolio is inherently identified and longitudinal, exceeding the Amber lane's ephemeral contract; deferred past 1.x. See Portfolio Passport, #224, for the learner-held paper alternative.)*
70. **Success Criteria Studio** `[G]` — Convert an objective and product into observable criteria, a student checklist, and a quality continuum.

## Studio VIII — Literacy and language arts

71. **Text Set Weaver** `[G, H]` — Organize teacher-supplied texts around an inquiry question with reading order, complexity notes, connections, and synthesis task.
72. **Close Reading Cartographer** `[G]` — Plan purposeful rereads, annotation focuses, text-dependent questions, and a culminating response.
73. **Vocabulary Ladder** `[G]` — Build repeated encounters using student-friendly meaning, morphology, examples, retrieval, and application.
74. **Writing Conference Coach** `[A]` — Narrow a draft conference to one high-leverage move, mini-lesson, practice sentence, and follow-up check.
75. **Genre Mentor Mapper** `[G, H]` — Extract teachable craft moves from an authorized mentor excerpt and create noticing, imitation, and transfer tasks.
76. **Phonics Pattern Studio** `[G, H]` — Build teacher-verified word chains, controlled phrases, dictation, and cumulative review from selected sound-spelling patterns.
77. **Fluency Rehearsal Builder** `[G]` — Create phrase-marked text, modeling cues, partner practice, repeated-reading purposes, and reflection.
78. **Argument Mapper** `[G/A]` — Make claims, evidence, warrants, counterclaims, rebuttals, and missing links visible in a source set or draft.
79. **Source Synthesis Table** `[G]` — Produce a claim-by-source matrix, agreements, conflicts, provenance fields, and synthesis frames.
80. **Sentence Combining Studio** `[G]` — Generate meaning-preserving syntax choices, discussions of rhetorical effect, models, and transfer prompts.

## Studio IX — Mathematics

81. **Worked Example Fader** `[G]` — Produce a full worked example, progressively faded examples, independent practice, and a self-check.
82. **Error Analysis Lab** `[G/A]` — Turn an authentic or teacher-authored error into diagnosis prompts, correction, explanation, and a prevention rule.
83. **Number Talk Forge** `[G]` — Plan a problem string, anticipated strategies, representation sequence, recording plan, and talk moves.
84. **Representation Bridge** `[G]` — Align concrete, visual, verbal, symbolic, and contextual forms and ask learners to translate among them.
85. **Word Problem Revoicer** `[G, H]` — Clarify or vary language and context while explicitly preserving the mathematical structure.
86. **Practice Set Balancer** `[G, H]` — Sequence practice through purposeful variation, interleaving, nonexamples, explanation, and spaced return.
87. **Estimation First** `[G]` — Insert magnitude estimates, reasonable ranges, exact computation, and comparison before answer checking.
88. **Data Story Maker** `[G]` — Turn a teacher dataset into questions, graph choices, analysis prompts, and an evidence-based conclusion task.
89. **Proof Scaffold Studio** `[G]` — Provide givens/claims organizers, hint ladders, proof frames, and comparison of multiple valid approaches.
90. **Unit Sense Checker** `[G]` — Add dimension checks, conversions, plausible ranges, and unit-based error cases to quantitative tasks.

## Studio X — Science and engineering

91. **Lab Lantern** `[G, H]` — Build visual procedures, approved safety checkpoints, data tables, role cards, access supports, and analysis prompts.
92. **Phenomenon Launcher** `[G]` — Turn an observable event into noticing, wondering, initial models, an investigation path, and model revision.
93. **CER Builder** `[G]` — Scaffold claim-evidence-reasoning with evidence sorting, reasoning prompts, language supports, and a rubric without supplying the conclusion.
94. **Safety Scenario Deck** `[G, H]` — Transform official lab rules into decision scenarios, choices, consequence discussions, and a teacher guide.
95. **Variable Detective** `[G]` — Identify variables, controls, confounds, measurement needs, and revision questions in an experimental plan.
96. **Data Table Doctor** `[G]` — Repair headings, units, trials, uncertainty fields, missing values, and graph readiness in a planned table.
97. **Model Revision Cycle** `[G/A]` — Compare initial models with new evidence and document what changed, why, and what uncertainty remains.
98. **Investigation Planner** `[G, H]` — Convert a testable question into materials, procedure, controls, data plan, access supports, and failure contingencies.
99. **Graph-to-Claim Coach** `[G/A]` — Guide pattern description, evidence selection, uncertainty, exceptions, and claim boundaries from a graph or table.
100. **Engineering Constraint Canvas** `[G]` — Structure users, criteria, constraints, research, alternatives, decision matrices, tests, and iteration evidence.

## Studio XI — Social studies, history, geography, and civics

101. **Inquirywright — Source & Inquiry** `[G, H]` — Turn a primary or secondary source into sourcing, context, close reading, corroboration, and bounded interpretation prompts.
102. **Perspective Matrix** `[G, H]` — Compare viewpoints, evidence, position, context, power, omissions, and uncertainty without manufacturing false equivalence.
103. **Timeline Weaver** `[G]` — Display chronology, parallel developments, duration, causation, contingency, turning points, and significance together.
104. **Map Inquiry Maker** `[G]` — Create a disciplined observation-to-inference sequence, spatial questions, contextual checks, and a map-production task.
105. **Civic Deliberation Studio** `[G, H]` — Build stakeholder briefs, shared facts, disputed claims, options, tradeoffs, norms, and a deliberation protocol.
106. **Corroboration Coach** `[G]` — Test a claim across sources with agreement, contradiction, independence, reliability, and bounded-conclusion fields.
107. **Context Builder** `[G, H]` — Supply the minimum chronology, institutions, vocabulary, geography, and anti-presentism prompts needed to approach a source.
108. **Counterclaim Workshop** `[G]` — Generate plausible counterclaims from supplied evidence and test concession, rebuttal, and revision.
109. **Oral History Kit** `[G→R, H]` — Prepare ethical questions, consent steps, recording boundaries, transcript workflow, archive fields, and reflection.
110. **Policy Tradeoff Tabletop** `[G, H]` — Produce role briefs, evidence cards, constraints, decision rounds, events, and a debrief for a public-policy simulation.

## Studio XII — Arts, physical education, and career/technical education

111. **Studio Critique Cards** `[G]` — Create observation, interpretation, question, evidence, and suggestion cards matched to an artistic medium and purpose.
112. **Composition Constraint Deck** `[G]` — Generate productive creative limitations, variations, wildcards, and reflection prompts for visual, musical, or written composition.
113. **Rehearsal Mapper** `[G]` — Plan warm-up, sectional priorities, transitions, checkpoints, recording, reflection, and a contingency cut for performance rehearsal.
114. **Theater Blocking Board** `[G]` — Combine a script excerpt and stage diagram into beats, entrances, movements, sightline checks, and rehearsal notes.
115. **Dance Sequence Cards** `[G]` — Turn teacher-entered movement, counts, formations, and transitions into visual cards and practice chunks.
116. **Movement Station Maker** `[G→R, H]` — Build safe, inclusive PE stations with progressions, rotations, equipment, access options, and challenge levels.
117. **Game Modification Studio** `[G→R, H]` — Adjust equipment, space, rule, role, tempo, and communication while preserving a game's learning purpose.
118. **Design Brief Generator** `[G]` — Frame an authentic artistic, technical, or maker problem with audience, need, criteria, constraints, research, and critique milestones.
119. **Shop Safety Cards** `[G, H]` — Convert official manuals and district rules into point-of-use precheck, operating, stop-condition, and cleanup cards.
120. **Culinary Sequence Planner** `[G, H]` — Coordinate mise en place, equipment, roles, synchronized timing, sanitation, access supports, and cleanup.

## Studio XIII — Project-based, inquiry, and experiential learning

121. **Project Compass** `[G]` — Turn a driving question, calendar, standards, resources, and audience into milestones, critique, evidence, exhibition, and recovery routes.
122. **Driving Question Tuner** `[G]` — Test whether a question is open, rigorous, feasible, locally meaningful, and capable of yielding several defensible products.
123. **Milestone Mapper** `[G]` — Convert a final deadline into visible interim products, quality gates, evidence requirements, flex points, and recovery checkpoints.
124. **Team Contract Studio** `[G→R]` — Draft roles, communication norms, decision rules, conflict repair, access commitments, and review dates.
125. **Critique Cycle Builder** `[G]` — Plan iterative feedback rounds, protocols, revision evidence, quality gates, and author decisions around prototypes or drafts.
126. **Community Partner Brief** `[G→R, H]` — Create a bounded partnership brief, meeting agenda, student preparation, safeguarding boundaries, and follow-up checklist.
127. **Research Sprint Planner** `[G]` — Break inquiry into short question, search, source-evaluation, note, synthesis, and next-question cycles.
128. **Exhibition Night Pack** `[G→R]` — Produce a program, exhibit labels, rehearsal plan, accessibility check, family communication, feedback form, and logistics runbook.
129. **Prototype Test Planner** `[G]` — Translate design criteria into test cases, observation fields, thresholds, user feedback, and redesign decisions.
130. **Project Risk Radar** `[G]` — Maintain risks, early-warning signals, mitigations, fallback products, material dependencies, and schedule buffers.

## Studio XIV — Classroom routines and organization

131. **Routine Cards** `[G]` — Convert a teacher-described routine into model language, visual steps, practice, completion cues, and neutral correction prompts.
132. **Station Rotation Board** `[G→R]` — Build a readable rotation schedule, group cards, timing, transition cues, and a rapid contingency version.
133. **Absence Catch-Up Pack** `[G→R]` — Produce an essential summary, must-do path, optional practice, next-target preview, and useful help questions.
134. **Morning Launch** `[G]` — Combine agenda, announcements, warm-up, materials, and first action into a predictable projected screen and desk card.
135. **Closing Loop** `[G]` — Align synthesis, self-check, exit evidence, cleanup, and next-lesson preview with the day's target.
136. **Discussion Role Wheel** `[G]` — Create rotating roles with accountable actions, language supports, access routes, and safeguards against permanently passive assignments.
137. **Grouping Deck** `[G]` — Offer grouping-structure templates — rotation patterns, group sizes, role structures, and regrouping signals — with no student data; placing names into the structure remains the teacher's act, on paper or in district systems. *(Re-scoped per review finding F3: a tool cannot be de-identified and produce a seating arrangement.)*
138. **Help Protocol Builder** `[G]` — Make stuck routines explicit through a help ladder, peer-support boundaries, teacher signals, and repair language.
139. **Early Finisher Menu** `[G]` — Generate worthwhile application, explanation, creation, retrieval, and community-contribution extensions instead of filler.
140. **Homework Clarity Check** `[G]` — Flag missing directions, materials, examples, access barriers, unrealistic timing, and absent help routes before work goes home.

## Studio XV — Multilingual learners and family partnership

141. **Directions Duet** `[G]` — Turn board directions into line-aligned English/home-language microsteps, icons, key verbs, and a nonverbal comprehension check.
142. **Lesson Bridge** `[G]` — Create a bilingual preview/review packet with vocabulary, visuals, sentence frames, and a concise concept synopsis around grade-level learning.
143. **Cognate Cartographer** `[G, H]` — Map likely cognates, false friends, roots, and affixes for teacher verification across a selected language pair.
144. **Talk Moves Loom** `[G→R]` — Produce discussion supports across language proficiency and speech, writing, pointing, partner-supported, and AAC response modes.
145. **Newcomer Maproom** `[G→R]` — Use approved school images to build an orientation booklet with essential phrases, routines, landmarks, and help routes.
146. **Background Builder** `[G, H]` — Identify cultural, institutional, geographic, or school-specific assumptions in a task and draft brief neutral primers.
147. **Glossary Garden** `[G]` — Maintain an illustrated, bilingual unit glossary with consistent terminology, morphology, examples, and teacher-recorded pronunciation.
148. **KinDispatch — Bilingual & Family Press** `[G→R, H]` — Transform teacher-authored information into plain-language letters, short messages, aligned translations, FAQs, and print/mobile layouts.
149. **Interpreter Prep Pack** `[R, H]` — Prepare terminology, context, acronyms, planned pauses, and questions for a human interpreter without trying to replace one.
150. **Translation QA Companion** `[G→R, H]` — Align source and translation, check dates/numbers/names, show back-translation warnings, and mark uncertainty for human review.

## Studio XVI — Professional learning, collaboration, and curriculum stewardship

151. **Teacher Logbook** `[G/A]` — Maintain a searchable instructional timeline of plans, decisions, de-identified evidence, outcomes, questions, and follow-up experiments. *(Accumulation invariant as in #60: only teacher-authored descriptions persist.)*
152. **Lesson Study Binder** `[G/A]` — Organize a research question, joint plan, observation evidence, revisions, reteaching, and findings for collaborative lesson study.
153. **Observation-to-Reflection** `[G/A]` — Turn non-evaluative observation notes into evidence patterns, reflective questions, and a small next-lesson experiment.
154. **PLC Protocol Builder** `[G/A]` — Select and prepare a timed protocol, roles, norms, evidence display, decision record, and follow-up for a defined meeting purpose.
155. **Work Sample Calibration** `[A, H]` — Package de-identified samples, a rubric, independent scoring, comparison, ambiguity notes, and rubric revision.
156. **Standards Coverage Map** `[G]` — Show where standards are introduced, developed, assessed, revisited, omitted, duplicated, or addressed only superficially.
157. **Curriculum Gap Finder** `[G]` — Compare intended outcomes, materials, tasks, and assessments to surface missing concepts, weak evidence, and sequence problems.
158. **Policy-to-Practice Translator** `[G, H]` — Convert an official policy into teacher actions, decision points, examples, nonexamples, records, and unresolved questions.
159. **Professional Learning Rehearsal** `[G]` — Turn a new instructional strategy into a microteaching script, anticipated learner responses, peer feedback, and revision round.
160. **Meeting-to-Action** `[G→R]` — Convert approved meeting notes into decisions, owners, deadlines, dependencies, open questions, and a follow-up agenda.

## Studio XVII — Open educational resource creation and stewardship

161. **Open Resource Packager** `[G]` — Assemble editable source, README, license notices, attribution, metadata, previews, and release archive for a teacher-created resource.
162. **Resource Rights Checker** `[G, H]` — Record source and license information, flag unclear or incompatible assets, draft attribution, and suggest replacement tasks.
163. **Template Forge** `[G]` — Turn a teacher's recurring handout, organizer, card set, or report into an accessible reusable template with named fields.
164. **Remix Map** `[G]` — Document what changed between a source resource and an adaptation, preserving authorship, license, version, and rationale.
165. **Open Textbook Trailhead** `[G]` — Transform openly licensed chapters into teacher guides, reading paths, vocabulary, checks, and local adaptations while retaining attribution.
166. **Print-and-Play Press** `[G]` — Generate classroom games with cards, boards, rules, answer keys, accessible variants, and cut-efficient layouts from teacher content.
167. **Assessment Item Commons** `[G, H]` — Store teacher-reviewed open items with standards, cognitive demand, accessibility, misconception rationale, and revision history.
168. **Prompt Provenance Ledger** `[G]` — Record recipe version, model/provider, input type, output checks, teacher edits, and license data without storing sensitive source content.
169. **Translation Memory Garden** `[G, H]` — Preserve teacher-approved translations of recurring school and subject phrases to increase consistency and reduce repeated inference.
170. **Open Course Kit Builder** `[G]` — Package syllabi, unit maps, lesson sources, slides, handouts, assessments, accessible variants, and contribution guidance as a forkable course.

## Studio XVIII — Teacher operations and school-life logistics

171. **Substitute Capsule** `[G→R]` — Make a lesson executable by an unfamiliar adult through a concise script, materials map, timing, routines, access notes, and backups.
172. **Coverage Handoff** `[G→R]` — Distill a day's essential instructional and operational information using minimum-necessary disclosure and a separate secured addendum.
173. **Calendar Fit** `[G]` — Place a unit against assemblies, testing, closures, field experiences, and flex days and propose realistic sequence options.
174. **Supply Steward** `[G]` — Convert a materials list and inventory into preparation bins, substitutions, reuse, replenishment thresholds, and cleanup routes.
175. **Print Queue Planner** `[G]` — Combine handouts, class counts, duplex/color needs, cut/fold work, and due times into a low-waste production plan.
176. **Permission Packet Builder** `[G→R, H]` — Assemble teacher-authored trip or activity information into checklists, accessible family versions, and return tracking without inventing policy language.
177. **Event Runbook** `[G]` — Produce a minute-by-minute school event plan with roles, spaces, accessibility, communications, contingencies, and closure.
178. **Trip Chaperone Pack** `[G→R, H]` — Create a minimum-necessary route, role, contact procedure, accessibility, emergency, and student-count packet under school rules.
179. **Club Charter Studio** `[G]` — Draft a purpose, membership access, meeting cadence, roles, decision process, safeguarding boundaries, and first-month plan.
180. **Advisory Circle Maker** `[G→R, H]` — Build bounded check-ins, community prompts, opt-out paths, discussion agreements, and referral reminders without acting as therapy.

## Studio XIX — Libraries, media centers, research, and community memory

181. **Media Center Pathways** `[G]` — Turn a research assignment into staged library routes, source types, checkpoints, help stations, and offline alternatives.
182. **Source Pack Curator** `[G, H]` — Organize teacher-supplied sources by perspective, format, complexity, provenance, rights, and use in an inquiry sequence.
183. **Book Talk Builder** `[G]` — Create a concise, spoiler-conscious book introduction, read-aloud excerpt plan, access options, and invitation questions.
184. **Research Conference Coach** `[A]` — Prepare a five-minute conference around a learner's question, search trail, source evaluation, synthesis obstacle, and next action.
185. **Citation Apprentice** `[G/A]` — Turn supplied source metadata into editable citations, explain each field, flag missing information, and create citation-repair exercises.
186. **Source Reliability Lab** `[G]` — Generate comparison tasks that distinguish authority, evidence, currency, purpose, independence, and fitness for a particular claim.
187. **Museum Case Maker** `[G]` — Convert artifacts and sources into exhibit labels, object questions, curatorial choices, accessibility descriptions, and a visitor trail.
188. **Community Archive Kit** `[G→R, H]` — Prepare consent, metadata, scanning, transcription, rights, description, redaction, preservation, and access workflows for local history.
189. **Reading Path Studio** `[G→R]` — Build teacher-curated thematic reading pathways with formats, access notes, invitations, and learner choice rather than opaque recommendation scoring.
190. **Information Detour** `[G]` — Create a no-internet research packet that models catalog, index, encyclopedia, primary-source, note, citation, and synthesis moves.

## Studio XX — Wild inventions at the edge of the possible

191. **Question Genome** `[G]` — Display how a question changes when audience, evidence demand, abstraction, openness, uncertainty, and cognitive operation change.
192. **Misconception Theater** `[G/A]` — Turn de-identified reasoning patterns into short dialogues among plausible thinkers that students can diagnose and revise.
193. **Classroom Twin Lens** `[G→R, H]` — Photograph an empty room and prototype alternate layouts for sightlines, mobility, noise, glare, collaboration, and rapid reset.
194. **Constraint Orchestra** `[G]` — Enter time, space, materials, staffing, access, language, and technology limits and receive several genuinely different lesson designs.
195. **Counterfactual Guardrail** `[G, H]` — Build disciplined historical “what if” investigations that preserve chronology, constraints, causation, evidence, and explicit uncertainty.
196. **Analogy Test Kitchen** `[G, H]` — Generate, stretch, and break candidate analogies, making useful correspondences and dangerous mismatches visible.
197. **Learning Path Composer** `[G/A]` — Arrange the same target as direct instruction, inquiry, workshop, station, seminar, project, and independent routes with shared evidence.
198. **Classroom Simulation Press** `[G, H]` — Turn teacher-provided systems, roles, rules, resources, and shocks into printable tabletop simulations with debriefs.
199. **Visible Thinking Camera** `[A]` — Capture an anonymous physical model or board and create a sequence of observation, explanation, challenge, and revision prompts around it.
200. **One-Room Schoolhouse** `[G]` — Given one printer, one camera, scarce devices, mixed readiness, and intermittent internet, generate a complete parallel station ecosystem that remains teachable offline.

## Repairs to the numbered atlas (per review finding F1)

201. **Scaffold Smith** `[G]` — Turn an existing task into temporary, removable supports — hint ladders, banks, checkpoints, entry scaffolds — without changing the learning target, each scaffold carrying its barrier, preserved demand, and fade criterion. *(Belongs with Studio V; absorbs TaskDock #21 per ADR-005.)*
202. **Forumwright — Discussion Design** `[G]` — Design equitable, intellectually productive discussion: question sequences with evidence targets, facilitation and learner cards, multimodal participation pathways, and a post-discussion equity reflection. *(Belongs with Studio VI; distinct from Talk Moves Loom #144.)*

## Studio XXI — The Deterministic Press (zero inference, pure craft)

Not one idea in the original 200 was a pure press. This studio needs no model, no capture, no egress — only good geometry and honest ink — and is the trust anchor built before every other studio.

203. **Blankforms Press** `[G]` — Parameterized print-perfect classics: graph paper, coordinate grids, number lines, ten-frames, clock faces, music staves, Cornell notes, lab tables, calendars, and cut-and-fold booklet blanks.
204. **Handwriting Foundry** `[G]` — Tracing, letter-formation, and practice sheets from any word list, with guide styles, dotted-to-faded progressions, and multiple scripts.
205. **Manipulative Mint** `[G]` — Cardstock press for fraction strips, algebra tiles, base-ten blocks, tangrams, dice nets, and spinners, with cut-efficient layouts and assembly guides.
206. **Flashcard Flywheel** `[G]` — Registration-safe double-sided card presses from teacher lists, with spaced-retrieval sort-box labels and self-check formats.
207. **Foldables Foundry** `[G]` — Interactive-notebook foldables, flipbooks, and layered organizers with cut/fold guides, generated from teacher content.
208. **Booklet Binder** `[G]` — An imposition engine that turns any approved artifact into correctly ordered saddle-stitch booklets, doing the signature arithmetic teachers do wrong at the copier.
209. **Big Print Shop** `[G]` — Tile any approved artifact across multiple pages into wall-scale displays with alignment marks and assembly maps.
210. **Label Lathe** `[G]` — Consistent series of classroom labels, bin cards, and station signs with optional symbols and bilingual pairs, in sheets matched to common label stock.

## Studio XXII — Computational thinking (the missing subject)

211. **Unplugged Algorithm Atelier** `[G]` — Turn a classroom routine or game into sequencing, branching, and loop cards that learners execute as human programs, with debugging discussion prompts.
212. **Parsons Press** `[G]` — Scramble a teacher-supplied working solution into line-ordering puzzles with optional distractor lines, difficulty staging, and a discussion key.
213. **Trace Table Tutor** `[G]` — Generate variable-trace tables, predict-the-output prompts, and check-your-trace keys from teacher-supplied code snippets, preserving the code exactly.
214. **Bug Zoo** `[G/A]` — Curate teacher-authored or de-identified buggy programs into diagnose-repair-explain exercise sets with misconception rationales.
215. **Rubber Duck Deck** `[G]` — Printable self-explanation and debugging-protocol cards that teach learners to interrogate their own reasoning before asking for help.

## Studio XXIII — Subjects the original atlas under-served

216. **Story Listening Loom** `[G, H]` — Comprehensible-input story scaffolds for world-language teaching: tiered glossaries, picture-support frames, and retell structures around a teacher-told story.
217. **Notation Bench** `[G, H]` — Rhythm cards, sight-reading lines, fingering charts, and staff worksheets from teacher parameters, with a deterministic engraving core and specialist review of pedagogy.
218. **Field Journal Forge** `[G]` — Nature and field-learning kits: observation frames, specimen labels, weather and phenology logs, and site-map pages for outdoor education.
219. **Budget Basecamp** `[G, H]` — Financial-literacy scenario kits with locked arithmetic, teacher-verified real-world figures, and decision-comparison organizers.
220. **Health Decision Deck** `[G, H]` — Bounded health-education scenario cards built only from district-approved curriculum language, with locked factual claims and explicit help routes.
221. **Paper Circuits Studio** `[G, H]` — Printable circuit templates, component maps, and locked safety text for maker and CTE classrooms.

## Studio XXIV — Learner-held self-direction (the learner keeps the record)

222. **Goal Post** `[G→R]` — Learner goal-setting and self-monitoring sheets designed to live in the learner's own folder — never in a data system — with review dates and self-selected evidence lines.
223. **My Strategy Shelf** `[G]` — Personal strategy card kits a learner assembles and edits: reading repairs, math checks, focus resets, and help scripts, chosen by the learner from teacher-offered sets.
224. **Portfolio Passport** `[G]` — A paper self-curation kit: selection slips, caption frames, growth-reflection pages, and a table of contents the learner maintains — the identified longitudinal record exists only in the learner's hands, resolving what finding F4 defers.

## Studio XXV — Measurement craft

225. **Parallel Forms Press** `[G, H]` — Generate a parallel version of a teacher-authored check — same constructs, different surface features — with a construct-map showing item-to-item correspondence for teacher verification.
226. **Retrieval Grid Generator** `[G]` — Spaced-retrieval grids and mixed warm-ups drawn deterministically from a teacher's own prior-unit question bank, with scheduling suggestions.
227. **Item Doctor** `[G, H]` — Examine a teacher-authored assessment item for cueing, double negatives, construct-irrelevant load, ambiguous stems, and implausible distractors, proposing repairs the teacher approves item by item.

---

## One engine, not 227 codebases

A shared **Honest Ink** shell could expose each candidate as a recipe or module. The common architecture would contain:

- Camera, scanner, clipboard, typed-text, image, PDF, and document intake
- Local crop, rotation, enhancement, OCR, metadata stripping, and redaction assistance
- Provider-neutral inference adapters: district Azure OpenAI and a named local-model path (Foundry.Inference.Local) held to one shared capability-test kit; no module may require either
- Named **classroom profiles** for subject, grade, language pair, layout, and generic access settings—never student names by default
- A declarative recipe format defining allowed inputs, instructional purpose, schema, refusal conditions, validation, output choices, and required warnings
- Structured output rather than free-form chat
- A mandatory teacher edit/approve surface
- PDF, print, DOCX/ODT, PNG/SVG, HTML, and open-project export as appropriate
- Two-column bilingual rendering, large print, high contrast, screen-reader structure, and keyboard operation
- A teacher-controlled project library for Green-lane products
- Content-free operational logs plus optional provenance records
- Test fixtures for instructional accuracy, formatting, refusal behavior, privacy boundaries, and rendering
- Offline use for every deterministic function; AI as a bounded accelerator rather than a prerequisite for opening or editing a project
- A paper-first guarantee: every module's primary output fully usable with zero learner devices
- A declared time-to-artifact budget per recipe, displayed in the interface and measured in pilots
- A minimum hardware covenant on the order of a 2015-era CPU, 8 GB RAM, and 1366×768, kept permanently on the test bench — donated hardware is where liberation actually happens
- Green project packages that embed an accessible HTML snapshot, readable forever without the application

The modules should be small and opinionated. “Generate anything” is not the product. The product is a trustworthy transformation whose educational purpose, teacher decision points, and output shape are obvious before the camera key is pressed.

## Licensing pattern

- License the application code and recipe engine **GNU GPL-3.0-or-later**.
- Keep the full license text, per-file notices where appropriate, source availability, modification notices, and an About-page license display.
- Treat fonts, icons, symbols, translations, model weights, sample student work, curriculum texts, and media as separate assets with their own provenance and compatible permissions.
- Prefer original artwork and clearly open assets. Do not assume that code licensing grants rights to a communication protocol, symbol library, textbook page, logo, or font.
- Consider a free-culture license such as CC BY-SA for original printable curriculum content while retaining GPL-3.0-or-later for software; obtain project-specific licensing advice before combining or distributing third-party assets.

## Suggested build order

Harmonized with the audited implementation plan (version 2.0), whose roadmap is authoritative.

### Foundation and Module Zero (0.0)

Refactor the reusable Writer's Kiosk components into a shared engine — capture, profiles, bilingual layout, render/print, keyless district authentication, provider interface, privacy gates, and test harness — and build the first Deterministic Press presses (Blankforms Press, Flashcard Flywheel, Booklet Binder) as the rendering pipeline's real cargo.

### SequenceSlate 0.1

Ship material-only capture, teacher confirmation, open symbol import, text cards, First/Then, Now/Next/Done, three-to-eight-step task strips, aligned bilingual output, and explicit save/export. Avoid learner records, auto-generated safety instructions, and claims of PECS alignment.

### Capture and language utilities 0.2

Add Board to Brief and Directions Duet with the OCR-uncertainty workflow and hardened right-to-left rendering.

### Green planning studio 0.3

Add Scaffold Smith (with its task-entry scaffold), StrandPlan, and Forumwright; complete the full Deterministic Press studio; begin second-maintainer recruitment.

### Accessibility, commons, and sources 0.4–0.5

Add full Symbol Commons, Access Remix, Inquirywright, and Green-only KinDispatch; spike Foundry.Inference.Local.

### Amber research pilot 0.6

Add ReteachSignal and Rubric Relay on synthetic fixtures first, entering the Amber lane only with the complete Amber architecture and written district approval.

### Open Commons 1.0

Add Open Resource Packager, recipe packs, provenance, localization contributions, and district-selectable inference backends so communities can fork the whole schoolhouse.

## Final north star

The family should not attempt to replace a teacher's judgment. Its great public purpose is to remove the repetitive production labor between a teacher's perception and a learner's next useful support. The master teacher remains the author, editor, witness, and final decision-maker; the software becomes the extraordinarily fast press, compositor, translator, accessibility bench, and apprentice.
