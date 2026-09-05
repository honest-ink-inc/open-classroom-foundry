# Honest Ink — the classroom foundry

## Enriched, refined, and audited implementation plan for Module Zero and the twelve-module starting sequence

**Plan version:** 2.0 — regenerated 29 August 2026, applying The Master's Review 1.0 (amendments 1–7; findings F2, F5–F11 incorporated)  
**Date:** 29 August 2026  
**Status (5 September 2026):** Foundation and the deterministic press are implemented; SequenceSlate and the reachable Green built-in studios are engineering prototypes, not protected-seat or classroom-validated products. SequenceSlate's constitutionally required AAC/SLP co-design, every other protected-seat review, pilot evidence, packaging, release, and the remaining roadmap stay open. ADR-008 fixes current public display names and ADR-009 corrects the lesson-design display to StrandPlan, while preserving all stable internal identifiers. The bounded commission prepared non-operative governance terms, hardened approval and output seams, and filed ADR-010 as Proposed without convening or ratifying any human decision. The accepted-improvement continuations repair existing recovery, quantitative, source-integrity, layout, access and evidence contracts while retaining the complete proposal scope and those human holds. The changed Lesson Loom, chart, learner-held, calibration and flashcard contracts remain unadmitted pending exact compatibility disposition; shared failure and transcript-mutation semantics require separate classification. See the [current handover](handover/2026-09-05-fixture-process-evidence.md).
**License intention:** Application code and first-party recipes under GNU GPL-3.0-or-later; other content and assets governed by explicit, compatible licenses  
**Initial operating environment:** .NET 10 on managed Windows 10/11 devices, local-first authoring, optional district-governed Azure OpenAI inference  

The current runtime taxonomy is narrower than the twelve-item product roadmap:
Built-in Studios exposes **10 doors and 11 modes**, because Scaffold Smith owns
two modes. Eight modes are mechanically buildable from synthetic Green
starters; Access Remix and the two district-governed modes remain held.
SequenceSlate is a separate authoring surface, while Module Zero and Symbol
Commons are outside the Built-in Studios door count.

This is a design, engineering, instructional, privacy, accessibility, licensing, and deployment audit of a proposed implementation. It is not legal advice, a district authorization, a completed security assessment, an accessibility certification, or a claim that unbuilt software has passed the gates described here.

---

# 1. Executive verdict

The twelve ideas are viable as one coherent free-software program, but they should not be built as twelve executables and should not be exposed as an unbounded “generate anything” chat surface.

The viable unit is:

> **A stable local engine + narrowly bounded and versioned recipes + module-specific editors and validators + an architectural teacher-approval gate.**

The public-facing priority sequence remains inspiring:

1. SequenceSlate — Visual Support Studio
2. Board to Brief
3. Scaffold Smith
4. Access Remix
5. Directions Duet
6. ReteachSignal — Formative Evidence
7. StrandPlan — Lesson Design Studio
8. Rubric Relay
9. Forumwright — Discussion Design
10. KinDispatch — Bilingual & Family Press
11. Symbol Commons
12. Inquirywright — Source & Inquiry

Preceding the twelve as **Module Zero** is the **Deterministic Press** — the zero-inference printable studio specified in section 10.0. It carries no privacy risk, needs no district AI approval, exercises the entire pipeline except the inference provider, and establishes the trust every later module is measured against.

The **engineering and release sequence must differ**:

1. Shared Green-lane foundation
2. Deterministic Press module zero — its first presses serve as the rendering pipeline's real cargo
3. Minimal asset/provenance kernel
4. SequenceSlate vertical slice
5. Board to Brief and Directions Duet
6. Access Remix
7. Scaffold Smith (including its task-entry scaffold), StrandPlan, and Forumwright
8. Full Symbol Commons and the complete Deterministic Press studio
9. Inquirywright and Green-only KinDispatch
10. ReteachSignal and Rubric Relay only after the complete Amber architecture and written district approval

This difference is not demotion. It is disciplined dependency management. Symbol provenance is a SequenceSlate dependency; anonymous formative-response processing and work-sample feedback introduce risks that must not be forced into the foundation.

## Audit corrections accepted as binding

- No PECS alignment, equivalence, certification, training, or protocol claim.
- “Teacher-facing” is never used as a privacy exemption.
- The data lane follows the content and every derivative, not the operator, module, or device.
- Removing a name is not assumed to make an artifact anonymous or de-identified.
- Restricted content is structurally excluded from every MVP.
- ReteachSignal reports reasoning clusters and matched instructional routes; it does not form named groups.
- Rubric Relay offers no grade, score, ranking, authorship judgment, or final evaluation.
- KinDispatch exports generic communication drafts but holds no recipient list and sends nothing.
- SequenceSlate begins with staged materials and empty environments, not learner photographs or named individualized profiles.
- Model output never prints, exports, sends, or saves as approved content without teacher inspection.
- “In memory only,” “zero retention,” “FERPA compliant,” and “de-identified” are not used as absolute marketing claims without corresponding forensic, contractual, and district evidence.
- Scaffold Smith includes the task-entry scaffold ratified by ADR-005; the former TaskDock title is a historical Atlas/ADR reference, not a separate runtime or public product.
- Accumulating stores persist only teacher-authored pattern descriptions, never response-derived content (review finding F5).

---

# 2. Program purpose and liberation test

Honest Ink exists to remove repetitive production labor between a teacher’s perception and a learner’s next useful support. It is an authoring, transformation, and analysis instrument. The teacher remains the accountable author, editor, witness, and decision-maker. Open Classroom Foundry remains the repository and internal engineering title.

The program advances educational liberation only if:

- Its source is available, modifiable, and redistributable.
- Existing projects remain openable and editable without a cloud connection or continuing subscription.
- It uses an open documented project format.
- It does not require a proprietary symbol set.
- It can support more than one inference provider and can perform deterministic authoring without any model.
- It records rights and provenance rather than laundering assets through generation.
- It gives teachers and communities control over recipes, language, layout, storage, export, and retention.
- It does not convert access support into compliance, surveillance, diagnosis, grading, or placement.
- Its outputs can migrate to accessible, editable formats outside the application.

## Explicit non-goals through version 1.x

- Student profiles, rosters, longitudinal records, IEP/504 data, diagnoses, medical or disciplinary records
- PECS protocol automation or claims
- Replacement or rearrangement of an established AAC system
- Automated grading, scoring, ranking, placement, discipline, or authorship detection
- Autonomous safety, threat, self-harm, or mandated-reporting decisions
- Direct email, messaging, LMS, SIS, gradebook, or family-recipient integration
- Arbitrary executable plugins, downloaded DLLs, scripts, or recipe-defined network calls
- Open-web research, web scraping, or automatic copyright clearance
- Public community symbol upload or synchronization
- Braille, tactile graphics, or claimed accessible PDF conformance without qualified specialists and formal evidence
- Proprietary symbol redistribution
- Self-updating managed district installations

---

# 3. Shared pedagogical constitution

Every module and recipe shall inherit these requirements.

1. **Preserve the target and construct.** Support may alter representation, sequence, pacing, or response mode; it may not silently remove the intellectual work.
2. **Treat the teacher as accountable author.** The generator proposes. The teacher inspects, edits, rejects, approves, publishes, and may see why a proposal was made.
3. **Separate epistemic layers.** Observation, transcription, interpretation, recommendation, and teacher decision must remain visibly distinct.
4. **Protect learner agency.** Where relevant, designs preserve help, wait, repair, stop, different, not now, do not know, and pass.
5. **Use non-deficit language.** No inference of diagnosis, intelligence, ability, motivation, effort, emotion, character, home support, or future performance.
6. **Add access without infantilizing.** Visuals, plain language, symbols, frames, and translations remain age-respectful and optional.
7. **Preserve productive difficulty.** The tool removes irrelevant load; it does not supply the conclusion or turn inquiry into copying.
8. **Make uncertainty actionable.** Illegible, ambiguous, unsupported, untranslated, or unverified content is marked rather than plausibly completed.
9. **Minimize data.** Green inputs are the default; Amber inputs are separately governed and ephemeral; Restricted inputs are blocked.
10. **Lock truth-bearing fields.** Dates, numbers, names, negation, quotations, citations, units, URLs, conditions, and rights metadata receive deterministic comparison.
11. **Keep outputs reversible.** Teachers can edit, undo, export, reopen, and migrate work without the model.
12. **Avoid inflated role claims.** The software is not an SLP, interpreter, historian, special educator, evaluator, mandated reporter, or master teacher.
13. **Guarantee paper first.** Every module's primary output must be fully usable in a classroom with zero learner devices; screens may enrich, paper must suffice.
14. **Declare the time-to-artifact budget.** Every recipe states a target time from intake to approved artifact; the interface displays it, pilots measure it, and a recipe that cannot meet its budget is redesigned rather than excused.

---

# 4. Data-lane contract

## Green

**Qualifying content**

- Teacher-created directions, lessons, standards references, rubrics, templates, and generic communications
- Public-domain, openly licensed, or otherwise authorized materials
- Staged objects and empty classroom environments
- Generic subject, grade, language, layout, and access settings

**Permitted early-release handling**

- Explicit local save in a teacher-controlled project library
- Approved cloud inference through an allowlisted district endpoint
- Ordinary export and printing after approval

**Required evidence**

- Data inventory and lifecycle
- Rights/provenance record
- Metadata stripping
- Content-free diagnostics
- Synthetic PII-canary tests

Green means save-friendly, not automatically public.

## Amber

**Qualifying content**

- Student work, handwriting, faces, voices, response batches, or teacher feedback about a student
- Artifacts that could be linked back to a learner through context
- Personalized family communication
- Any derivative that retains identifying or linkable information

**Permitted early-release handling**

- Local crop and redaction assistance
- Raw capture retained only for the active session to the extent technically possible
- No autosave, recovery journal, thumbnails, recent-file list, raw-source save, or content logging
- Stateless inference only through an approved district path
- Export only after exact-payload preview, explicit approval, and district authorization

**Required evidence**

- Amber-specific data-flow review
- Provider and deployment attestation
- Retention decision
- Disk, memory, crash, network, and print-spool residue tests
- Egress trace
- District privacy/security approval

## Restricted

**Qualifying content**

- IEP/504 or individualized accommodation information
- Diagnosis, medical, counseling, behavioral, disciplinary, or custody information
- Private schedules or individualized AAC/communication profiles
- Disclosures, threat, self-harm, abuse, or neglect material
- Recipient/contact databases and other highly sensitive records

**Early-release handling**

- Blocked. A user may not relabel Restricted material to bypass policy.

## Lane inheritance rules

- Unknown input defaults to Amber.
- Automated detection may escalate a lane but may never certify content as Green.
- A derivative inherits the highest lane of its inputs.
- Lane reduction requires a district-approved irreversible redaction/de-identification process; a teacher checkbox alone is insufficient.
- Visual black boxes are not redaction unless underlying OCR, metadata, layers, and embedded objects are removed.
- The interface says **redaction-assisted**, not **certified de-identified**.
- Accumulating stores (for example a misconception atlas or teacher logbook) persist only teacher-authored pattern descriptions: no response-derived text, image, quotation, or per-check trace; no date-to-roster linkage; district-defined small-cluster suppression applies. Verified with fixtures.

---

# 5. The three mandatory human gates

## Gate A — Privacy preflight before egress

The teacher sees the exact outbound derivative, not merely the original source or a generic assurance. Local controls provide crop, rotation, metadata stripping, page selection, redaction assistance, and lane confirmation. The preflight shows:

- Pages and regions included
- OCR/transcription proposed for transmission
- Remaining possible names, faces, codes, screens, handwriting, schedules, and indirect identifiers
- Provider, deployment, region/policy label supplied by IT
- Retention mode and what the application will purge

## Gate B — Pedagogical approval after generation

Approval is field-level inspection, not a ceremonial checkbox. The review surface shows:

- Source or verified transcription
- Generated draft
- Evidence links
- Locked facts and mismatches
- Uncertainty and validation issues
- Rights/provenance
- Accessibility and translation status
- Every teacher edit

The teacher may accept, edit, reorder, replace, remove, or reject every consequential element.

## Gate C — Direct-source adult safety review

If a source appears to contain a safety concern, the program pauses normal output and privately directs the supervising adult to inspect the original physical or local source and follow the applicable district procedure. The program does not claim comprehensive detection and does not replace mandated reporting, suicide response, threat assessment, or other distinct human procedures.

No early release automatically broadcasts content or metadata. Any future district alert workflow requires separate recipient, minimum-necessary-content, retention, escalation, failure-handling, and human-confirmation approval.

---

# 6. Technical architecture

## 6.1 Migration strategy

Writer’s Kiosk at audited commit **c2b670b** provides proven components and all **75 existing tests pass**. Useful inherited capabilities include camera capture, image correction, Entra authentication, profiles, bilingual layout, Edge/PDF rendering, Windows printing, rate limiting, and local activity counters.

The current kiosk is not itself the Foundry architecture. It has a large UI/orchestration class, static services, free-form Markdown model output, string sentinels, automatic printing, relative-file persistence, and tightly coupled provider, prompt, parser, renderer, log, and UI paths.

Use a **strangler refactor with characterization tests**:

1. Keep Writer’s Kiosk independently runnable.
2. Place tested behavior behind interfaces.
3. Extract domain and application services without changing kiosk behavior.
4. Build the Foundry teacher shell against those services.
5. Port improvements back only after equivalent behavior passes tests.

Keep WinForms for the first vertical slices. Use dependency injection and presenters/view-models so the UI framework is not the architecture. Reassess WPF or another shell after SequenceSlate field testing; do not spend the first months on a framework rewrite.

One accessibility rule binds from day one: standard controls only, with no owner-drawing, until the accessibility test harness exists — and thereafter any custom control ships with its own UI Automation peer and NVDA/Narrator evidence. The review surface, as the most novel UI in the program, receives assistive-technology walkthroughs early, not late.

## 6.2 Proposed solution boundaries

    src/
      Foundry.App.WinForms
      Foundry.Domain
      Foundry.Contracts
      Foundry.Application
      Foundry.Infrastructure.Windows
      Foundry.Inference.Abstractions
      Foundry.Inference.AzureOpenAI
      Foundry.Inference.Local          (post-0.3 target; same capability-test kit)
      Foundry.Rendering
      Foundry.Storage
      Foundry.Modules.DeterministicPress
      Foundry.Modules.BuiltIn
    tests/
      Unit
      Contract
      Integration
      Rendering
      Accessibility
      UiAutomation
      InstructionalEvals

Core service seams:

- ICaptureSource
- IDocumentNormalizer
- IOcrService
- IRedactionAssistant
- IDataPolicyEvaluator
- IRecipeRegistry
- IRecipeRunner
- IInferenceProvider
- IStructuredOutputValidator
- IArtifactValidator
- IApprovalGate
- IAssetCatalog
- IProjectStore
- IRenderer
- IExporter
- IPrinter
- IDiagnosticsSink

A module may not directly reach the camera, network, filesystem, printer, or diagnostics sink.

## 6.3 Mandatory state machine

    New
      → Imported
      → Normalized
      → Data lane confirmed
      → Outbound payload previewed
      → Draft generated
      → Schema validated
      → Module invariants validated
      → Awaiting teacher review
      → Teacher edited
      → Approved
      → Rendered
      → Printed, exported, or deliberately saved
      → Transient sources purged

Blocked, declined, cancelled, provider-failed, validation-failed, and purge-incomplete are explicit states.

Approval is architectural. Render, export, save-as-final, and print APIs accept an **ApprovedArtifact**, never a DraftArtifact. Approval belongs to the exact revision; any later edit invalidates it.

## 6.4 Local-first meaning

Capture, manual authoring, editing, project opening, rendering, printing, and export work offline. Model suggestions are optional enrichment. The teacher can build an artifact manually and reopen an existing project without authentication or inference.

Storage locations:

- **ProgramData/OpenClassroomFoundry/policy** — IT-controlled tenant, provider, module, lane, export, retention, and endpoint policy
- **LocalAppData/OpenClassroomFoundry** — teacher preferences, rebuildable caches, content-free diagnostics
- **Teacher-selected or district-approved location** — deliberately saved Green projects and exports

Projects and secrets never live beside the executable.

“The application does not intentionally persist the source capture” is the correct claim. Pagefiles, crash dumps, preview systems, endpoint tools, camera drivers, temporary rendering, and print spooling require documented boundaries and forensic tests.

## 6.5 Open project package

Use an openly documented **.ocfproj** ZIP/JSON package with safe path handling:

    manifest.json
    artifact.json
    assets/
    provenance/
    previews/      optional and Green only
    snapshot.html  Green only: accessible self-contained rendering of the approved artifact

A rebuildable local database may index Green projects and Symbol Commons assets, but the package—not a proprietary database—is the portable source of truth. Green packages additionally embed the accessible, self-contained HTML snapshot and a plain-text manifest summary, so a project remains human-legible a decade hence with no Foundry installed; Amber projects, which are not saved with content, are unaffected.

### Required ProjectManifest fields

- schemaVersion
- projectId
- moduleId and moduleVersion
- recipeId, recipeVersion, and recipeHash
- createdUtc and modifiedUtc
- dataLane and retentionMode
- sourceLocales and outputLocales
- engineVersion
- artifactPath
- assets and provenance references

### Required SourceEnvelope fields

- Source kind and MIME type
- Page count
- Crop, rotation, enhancement, and redaction geometry
- Metadata-stripped state
- Teacher-stated rights/provenance
- Lane and disposal policy
- Session-scoped byte reference, never a module-readable filesystem path

Original filenames and paths are discarded unless explicitly required.

### ArtifactDocument

Use a semantic document tree, not model-created HTML or Markdown. Allowed nodes include heading, paragraph, ordered step, list, table, card, image reference, bilingual pair, choice, evidence link, citation, and teacher-only notice.

All strings are escaped by renderers. Scripts, arbitrary HTML, remote resources, commands, filenames, and paths are prohibited.

## 6.6 Recipe model

Two trust tiers:

1. First-party built-in modules compiled with the application
2. Data-only recipe packs declaring schemas, prompts, allowed inputs, validators from an allowlist, warnings, editor/renderer templates, and evaluation-suite identity

A recipe manifest records:

- Stable ID, version, license, and minimum engine version
- Instructional purpose and prohibited purposes
- Allowed inputs and maximum data lane
- Required provider capabilities
- Strict output schema
- Local preprocessing
- Approved validators
- Editor and renderer IDs
- Supported exports
- Warnings and confirmations
- Localization resources
- Migration IDs
- Evaluation-suite version

No downloaded code, JavaScript, PowerShell, DLL, arbitrary HTML, or recipe-defined network call is permitted in managed deployments. Projects pin recipe versions. New versions install alongside the old until a project-specific migration is accepted.

## 6.7 Inference boundary

The inference provider receives a minimum, explicit payload and returns a strict structured object. It has:

- No tools
- No filesystem access
- No printer or export access
- No access to secrets
- No shared conversation or cross-job state
- No authority to choose a data lane
- No authority to approve an artifact

Source-page instructions are untrusted data and cannot override the recipe.

The architecture makes no model-specific claims. Authority rests in the IInferenceProvider abstraction plus capability tests — image input, structured output, refusal behavior — executed against the configured deployment. The plan uses strict schemas rather than free-form Markdown and records the configured provider, deployment/model identifier, recipe version, and, where available, pinned model version for every evaluated release. A provider must pass its capability tests before any module trusts it; no module assumes every OpenAI-compatible endpoint behaves identically. Deployment attestation evidence at Gate 3 cites the provider's own governing documentation — for district Azure OpenAI, Microsoft's Azure OpenAI documentation and contract terms.

A local-model path is a named target: a Foundry.Inference.Local adapter using grammar-constrained structured output from locally run open-weight models, held to the same capability-test kit, spiked after Release 0.3 and targeted before 1.0. No module may require it, and no module may require the cloud either; every module degrades gracefully to deterministic authoring with no model at all. This is the difference between district-governable and district-dependent.

Use a synthetic provider for ordinary CI. Live Azure smoke tests occur only in an explicitly authorized release environment with synthetic inputs.

---

# 7. Cross-cutting security and privacy controls

| Threat | Required control | Verification |
|---|---|---|
| Prompt injection in images, PDFs, OCR, metadata, QR codes, or sources | Treat input as data; no model tools or secrets; fixed recipes; strict schema; allowlisted transforms | Red-team corpus with visible, hidden, encoded, and metadata instructions |
| Hidden persistence | No content logs; restricted per-job temporary directory where unavoidable; purge; crash-upload disabled; documented spooler boundary | Synthetic canary search after success, failure, crash, reboot, print, and uninstall |
| Cloud retention/configuration drift | Stateless approved API path; district endpoint allowlist; deployment attestation; no thread/history features | Captured configuration and periodic re-attestation |
| Shared-device access | Individual Entra identity; least privilege; encrypted token cache; lock timeout; no generic account; no Amber recent list | Access, user-switch, lost-device, and role-removal drills |
| Cross-request leakage | Isolated requests and caches; cleared job state | Canary from job A must never appear in job B |
| Unsafe export or cosmetic redaction | Remove underlying text/metadata; sanitize filenames; hostile ZIP/PDF/DOCX tests | Reopen and inspect exports; malware, traversal, and decompression tests |
| Supply-chain compromise | Signed releases; pinned dependencies; SBOM; SAST/SCA; secret scan; rollback | CI evidence and signature/rollback drill |
| Model/recipe drift | Version record, eval gate, staged promotion, rapid rollback | Evaluation report tied to exact versions and hashes |
| Automation bias | Draft labeling; source/draft split; seeded-error usability studies | Teachers must detect defined seeded errors before pilot |
| Outage or cost attack | Explicit Generate; no background calls; rate and budget controls; idempotent retry; offline editing; kill switch | Network-loss, throttling, retry, and budget tests |

Diagnostics may record application, engine, module, recipe, provider/deployment identifiers; state transitions; durations; broad media class; token counts; and success/error categories.

Diagnostics never record source or output content, prompts, model responses, translations, filenames, paths, contact data, excerpts, thumbnails, or artifact hashes. Remote telemetry is off by default and, if enabled by district policy, uses an explicit content-free allowlist.

---

# 8. Accessibility contract

The interactive application targets complete keyboard operation, Windows UI Automation, screen-reader compatibility, high-contrast use, 200 percent zoom, predictable navigation, plain language, and WCAG 2.2 AA where applicable. A generated format is not called accessible until that format and template have corresponding evidence.

Required tests:

- Keyboard operation for capture, crop, reorder, edit, approve, save, export, and print
- No keyboard traps or single-key-only required paths
- Narrator and NVDA manual testing
- Visible focus and correct names, roles, states, progress, and errors
- Color never as the only signal
- Contrast targets of 4.5:1 for ordinary text and 3:1 for large text and essential non-text elements
- Text resize without clipping
- User-controlled large print, line spacing, margins, and pagination
- No forced timing; undo and clear error recovery
- Reduced motion and progressive disclosure
- Correct headings, lists, tables, alt text, reading order, Unicode, and language metadata in digital output
- Semantic bilingual-pair reading order
- Arabic/Hebrew bidirectional isolation and mixed-script number/punctuation tests
- Letter/A4, duplex, monochrome, low-ink, and enlarged-print regressions
- Cognitive-accessibility review of stable navigation, source/draft distinction, and text-plus-symbol labels

Accessible HTML is the first digital accessibility target. Edge-generated print PDF is treated as a paper-production format until tagged-PDF audits justify stronger claims.

Evidence for district review includes an ACR/VPAT, manual assistive-technology report, defect/remediation log, and accessible canonical samples.

---

# 9. Licensing and commons contract

## Code and recipes

- GNU GPL-3.0-or-later
- Full license text and SPDX identifiers
- Complete corresponding source and build scripts distributed or linked with binaries
- Modification notices and dependency-license inventory

## Documentation and original printables

Choose and declare a separate free-culture license, commonly CC BY or CC BY-SA, after project-specific review. **Later 5 September 2026:** the typist [selected CC BY-SA 4.0 as the proposal to carry forward](governance/2026-09-05-content-license-selection.md); its material scope, licensing authority, matching assent and operative declaration remain unresolved. Do not imply that GPL automatically governs every symbol, font, translation, photograph, or curriculum text.

## Asset manifest

Every shipped asset records:

- Source
- Creator
- Date/version
- SHA-256
- License or explicit LicenseRef
- License text
- Modifications
- Required attribution
- Redistribution and commercial-use status
- Consent/release where relevant

Prefer original, CC0, CC BY, or CC BY-SA artwork for the libre symbol core. Unknown rights block distribution. Restricted or noncommercial collections are not bundled as a universally free commons. Proprietary local imports remain technically isolated from public export.

Prefer OFL fonts and preserve required notices, naming obligations, embedding permissions, and script coverage. Translations require contributor rights, attribution, locale/script, review status, and the exact source-string version.

Public tests, documentation, issue reports, and CI use project-supplied synthetic, public-domain, or compatibly openly licensed material only. Outside or member-authored teacher material remains outside the repository until the exact content license is chosen and that author separately assents to its matching contribution terms; project-owner-directed factual governance, status, and repository-maintenance prose is the narrow first-party maintenance route under the current all-rights-reserved documentation default. No student page enters source control, a release, a crash report, or an evaluation fixture.

CI fails if a shipped file lacks provenance or rights status. Each release includes LICENSES, COPYING, NOTICE/ATTRIBUTIONS, an asset manifest, an SBOM, SECURITY.md, contribution terms, trademark policy, and a takedown process.

---

# 10. Module specifications

Each module below states the safe MVP, invariants, proof, and deferrals. Every module also inherits the shared constitution, lane contract, three human gates, accessibility contract, and universal Definition of Done.

## 10.0 Deterministic Press (Module Zero)

**Lane:** Structurally Green only — no capture, no inference, no egress is even expressible.  
**Problem:** Teachers buy graph paper, ten-frames, handwriting sheets, and flashcard stock with their own money; the atlas's engine promises offline deterministic function, yet every original idea assumed interpretation.

**Scope:** The eight presses of Studio XXI (atlas entries 203–210): Blankforms Press, Handwriting Foundry, Manipulative Mint, Flashcard Flywheel, Foldables Foundry, Booklet Binder, Big Print Shop, and Label Lathe. Inputs are parameters, never prose; a press that wants prose is a different module.

**Architecture position:** No dependency on IInferenceProvider, IOcrService, IRedactionAssistant, or the Amber machinery. Presses exercise IRenderer, IExporter, IPrinter, IProjectStore, and the ApprovedArtifact boundary — Gate B in its lightest form (parameter review plus exact print preview) still passes the architectural approval gate, preserving uniformity.

**MVP (folded into Releases 0.0–0.1):** Blankforms Press, Flashcard Flywheel, and Booklet Binder — chosen because together they exercise parameterized vector geometry, duplex registration with project save, and imposition over approved artifacts.

**Invariants:** Dimensional accuracy within ±0.2 mm at 100 percent scale; print fidelity across Letter/A4, duplex, grayscale, and low ink; imposition page-order correctness proven deterministically for 4–64 pages; duplex term/answer registration never scrambled; declared time-to-artifact budget of three minutes per press.

**Acceptance proof:** Measured geometry fixtures; physical print inspection on the hardware bench including the minimum-hardware machine; keyboard-only creation of each MVP artifact; static verification that Module Zero references no inference seam and produces no network egress.

**Defer:** Non-Latin handwriting scripts pending qualified review; Label Lathe until the asset kernel exists; any generated decorative art; any press whose input is prose rather than parameters.

The complete specification is the companion document `open-classroom-foundry-deterministic-press-spec.md`.

## 10.1 SequenceSlate — Visual Support Studio

**Lane:** Green when using staged materials and empty environments; individualized or learner-containing material is excluded from the MVP.  
**Problem:** Teachers spend substantial time photographing, cropping, labeling, sequencing, and formatting real activities into visual supports.  

**Authorized inputs**

- Teacher-staged materials, empty work areas, routes, and genuinely available choices
- Teacher-entered task names and actions
- Original or verified libre symbols
- Generic language, layout, size, contrast, and symbol-density settings
- Locked school-approved safety text when a procedure has hazards

**Teacher workflow**

1. Choose Task Strip, First/Then, Now/Next/Done, Choice Board, or classwide Change Preview.
2. Capture/import, crop, rotate, and run the local privacy preflight.
3. Confirm literal objects and each proposed action.
4. Add, remove, replace, and reorder words and images.
5. Choose language, size, contrast, cut lines, and layout.
6. Inspect learner view, teacher-only notes, agency options, provenance, and privacy summary.
7. Approve export; discard source by default.

**Outputs**

- Three-to-eight-step task strip
- First/Then and Now/Next/Done variants
- Choice board containing only genuinely available options
- Optional help, wait, stop, different, not now, and finished cards
- Aligned bilingual labels
- Print PDF, SVG/PNG cards, and editable project
- Optional teacher-only prompt-and-fade notes, never mixed into learner output
- Physical craft library: standard hook-and-loop strip dimensions, lamination bleed margins, finger-space cut allowances, ring-binding hole templates, and card-corner radii, with board-size presets gathered from the educator council's real classrooms

**Invariants**

- No PECS claim
- No inference of diagnosis, emotion, preference, intent, capacity, or behavior
- No silent alteration of an established AAC layout
- No compliance-only board that omits agency
- No invented hazardous action
- A symbol is proposed representation, not universal meaning

**Acceptance proof**

- Every element editable, replaceable, reorderable, and removable
- No unobserved step survives without teacher confirmation
- Required agency options survive compatible templates
- Every distributable symbol has rights metadata
- Long bilingual and right-to-left rows remain aligned
- At least 80 percent of representative teachers create and verify a four-step strip from a staged photo within five minutes
- Thirty staged-task fixtures across subjects and environments
- Zero PECS/proprietary-protocol claims in UI, documentation, metadata, or samples

**Defer**

Learner profiles, people in photographs, individualized schedules, AAC-device synchronization, social narratives, automatic vocabulary rearrangement, PECS phases, and model-authored safety directions.

## 10.2 Board to Brief

**Lane:** Green for teacher-created boards captured without people or student work.  
**Problem:** Boards, slides, agendas, and anchor charts are transient and often visually crowded.

**Workflow and outputs**

- Capture/import; correct perspective, glare, rotation, and crop
- Produce a literal OCR/vision transcript with every uncertain token highlighted
- Let the teacher assign semantic roles such as title, sequence, vocabulary, date, material, example, and diagram
- Produce faithful editable transcription plus a structured one-page brief
- Export accessible HTML/DOCX/ODT and print PDF
- Present uncertainty and source-to-output comparison

**Invariants**

- Transcription and interpretation remain separate
- No invented date, objective, material, definition, or missing step
- Formulas, units, numbering, arrows, and diagram references are preserved
- No answer is added
- Unknown rights block redistribution

**Acceptance proof**

- Locked facts match the teacher-approved transcript exactly
- Every uncertainty is reviewable rather than guessed silently
- Every structured statement traces to the board or teacher entry
- Reading order is editable and keyboard-inspectable
- Usable at 200 percent zoom and in grayscale
- At least 80 percent of teachers complete a clean conversion in three minutes
- The uncertain-token surface has a specified keyboard grammar — jump-to-next-uncertain, accept, retype, mark-illegible — tested with NVDA before any other UI polish
- Low-light and marker-color fixtures reflect real end-of-day whiteboard photography under fluorescent glare

**Implementation status — 30 August 2026.** A bounded first source-intake slice is now reachable from the Board-to-Brief module door. It reuses the capture/normalization and teacher lane-attestation surface, advances only teacher-confirmed Green material, compares the normalized image with either local Windows OCR candidates or an explicitly labeled manual literal transcript, requires every uncertain token to be accepted, retyped, or marked illegible, and then requires a teacher role for every line plus exactly one title. The teacher can edit the verified line text and change the line reading order. The intake returns verified line/role values only after terminal source-byte purge; a purge failure exposes only retry and returns nothing. Imported lines deliberately clear the module's Green attestation and any earlier Gate B approval, and the intake itself creates no artifact or output.

The Windows path uses the installed `Windows.Media.Ocr` user-language recognizer and has no application network or credential path. Because that platform API supplies words and line boundaries but no trustworthy word confidence, every platform word enters the same explicit uncertainty grammar. The service aligns each platform word list to the platform's exact line text and retains the intervening/suffix separators, preserving forms such as `x=2`, `25 mL`, punctuation, and arrows even when a word is retyped; a line/word mismatch fails closed to the manual path. Candidate, partial-verification, and completed views share that reconstruction contract. Capture-surface failure, an unexpected OCR fault during terminal cancellation, and forced `Dispose()` also withhold results and converge on cancel → OCR settlement → session purge; disposal does not purge while OCR may still be borrowing the buffer.

This implemented slice does **not** close the section: local language-pack availability, real low-light/glare/marker-color photographs, perspective/glare correction, multi-page/PDF/slide input, human keyboard and NVDA/Narrator evidence, 200 percent zoom, grayscale/high-contrast inspection, the three-minute teacher measure, accessible DOCX/ODT, and native print-PDF evidence remain open. The current module outputs remain accessible HTML and print HTML.

**Defer**

Live lecture recording, arbitrary complex-table reconstruction, exact visual facsimiles, and textbook-digitization workflows.

## 10.3 Scaffold Smith

**Lane:** Green. Individual learner records and identifiable work are excluded.  
**Problem:** Teachers need temporary access supports that preserve grade-level reasoning.  
**Task-entry scaffold:** This module includes materials, first action, chunks, checkpoints, help routes, and a concrete definition of done, as ratified by ADR-005. The old TaskDock label remains only in that historical decision and stable record references.

**Authorized inputs**

Teacher-authored task, learning target, criteria/rubric, source material, time, and generic teacher-selected barrier categories.

**Outputs**

- Student scaffold packet
- Progressive hint ladder
- Vocabulary or representation bank
- Checkpoint card
- Teacher rationale linking each scaffold to a barrier, preserved demand, and fade criterion
- Source-versus-scaffold comparison
- Printed removal plan on the teacher page, making temporariness visible rather than aspirational

**Invariants**

- Same target, criteria, and core cognitive demand
- No completed thesis, conclusion, proof, computation, or source interpretation
- Sentence frames remain optional
- No diagnosis inference or automatic leveling
- Not an IEP/accommodation generator
- Multiple valid approaches survive

**Acceptance proof**

- Every scaffold states the barrier addressed and demand preserved
- All original criteria remain represented
- Zero answer leakage in gold/adversarial fixtures
- Two content experts approve target fidelity
- Every scaffold can be removed independently
- Fade plan uses an observable transfer criterion

**Defer**

Individualized accommodation plans, student mastery tracking, diagnosis-linked recommendations, and automatic differentiation by named learner.

## 10.4 Access Remix

**Lane:** Green for teacher-created or redistribution-authorized material.  
**Problem:** Sound content can be unusable because of density, print size, contrast, reading order, response space, or inflexible response mode.

**Outputs**

- Large-print version
- High-contrast/low-ink version
- Reduced-clutter or one-item-panel version
- Chunked version with preserved numbering
- Optional symbol-supported and bilingual directions
- Editable structured digital output plus paper PDF
- Transformation report showing authorized changes

**Invariants**

- The production MVP requires purpose authority established through protected specialist review and not grantable by typed content. The current build deliberately issues no such authority: SequenceSlate content remains purpose `Unknown`, the Access catalog mode has no build delegate, and the remixer is not a public API
- Preserve every prompt, condition, datum, reference, and criterion
- Do not alter item difficulty or cue the answer
- Keep indispensable graphics with their prompt
- Color is not the sole carrier of meaning
- No claim that a variant fulfills an individual plan or law
- Formal/high-stakes assessments are refused in the MVP

**Acceptance proof**

- One hundred percent item, numbering, data, and option parity
- Reading order passes keyboard and screen-reader inspection
- 200 percent zoom and grayscale use
- Pagination never separates a prompt from indispensable evidence
- Construct-change warnings are visible and non-dismissable — acknowledged, never hidden
- A refusal fixture verifies that a formal assessment disguised as a worksheet is refused
- Adversarial fixtures verify that assessment-shaped typed content in English and Spanish, keyboard declarations, generic approvals, edited artifacts, and reopened packages cannot acquire Access authority; no document enters the current held MVP
- No answer-key leakage

**Defer**

Formal-test conversion, braille/tactile output, arbitrary complex math/table reconstruction, and accessibility certification without evidence.

## 10.5 Directions Duet

**Lane:** Green generic classwide directions.  
**Problem:** Literal translation can preserve words while losing actions, order, conditions, or school-specific meaning.

**Workflow and outputs**

- Confirm exact action sequence
- Split into one-action microsteps
- Lock dates, numbers, names, links, materials, conditions, units, and negation
- Translate line by line using an approved glossary
- Show uncertainty and review status
- Add a nonverbal comprehension check
- Export aligned print and accessible digital formats

**Invariants**

- Same action count, order, conditions, and deadlines
- Source remains visible and authoritative
- No claim of certified translation
- Back-translation is a warning aid, not proof
- Safety, legal, emergency, disciplinary, or consequential directions require approved source language and qualified human review

**Acceptance proof**

- One-to-one step alignment
- Exact invariant fields
- Uncertainty always visible
- Right-to-left and long translations render correctly
- Glossary terms remain consistent
- The approved glossary is versioned and each artifact is stamped with the glossary version used, so midyear terminology changes never drift silently across handouts
- “Language-reviewed” requires a recorded human reviewer
- Either language can be edited without losing alignment

**Defer**

Unlimited unsupported languages, speech interpretation, dialect guarantees, personalized directions, and high-stakes machine-only translations.

## 10.6 ReteachSignal — Formative Evidence

**Lane:** Amber by design; synthetic inputs only until written district authorization.  
**Problem:** Teachers need fast visibility into reasoning patterns without turning a quick check into grading or surveillance.

**Authorized inputs**

- Teacher-defined target, expected evidence, and known misconception hypotheses
- Nameless response batch after Amber approval
- No roster, demographics, grades, behavior data, permanent history, or named groups

**Workflow**

1. Define target and evidence.
2. Capture/import a nameless batch.
3. Remove any identifying response.
4. Verify uncertain OCR.
5. Review proposed reasoning clusters and outlier/unclassified bin.
6. Merge, split, rename, or reject clusters.
7. Choose cluster-matched reteach, practice, and extension routes.
8. Approve a summary; purge response images and response-level text.

**Outputs**

- Counts and percentages by teacher-approved reasoning cluster
- Correct, partial, misconception, missing, unreadable, off-target, and novel/outlier distinctions
- Cluster-matched instructional routes
- Short reteach draft
- One or two hinge questions with conditional teacher responses
- ~~The approved cluster summary — already teacher-approved Green output — may seed tomorrow's Hinge Question Forge session without persisting any response-level data~~ **Corrected 5 September 2026 (I35):** approval does not change a data lane. A response-derived summary retains the highest input lane, including Amber; suppression, purging raw responses, a teacher checkbox, and the word "anonymous" do not authorize a downgrade or a persistent handoff.
- A separately authored generic pattern description may enter a new Green planning task only under §4's qualifying-content, rights, and accumulation rules. It must contain no response-derived text, image, quotation, per-check trace, or date-to-roster linkage. Copying or paraphrasing a response-derived summary is not this independent authoring route. Actual Amber operation and any exceptional lane reduction remain subject to the district/privacy/records process; the current production sinks admit Green only.
- No named group, ranking, or response-level export

**Invariants**

- Formative analysis only
- Anonymous artifacts cannot yield student-specific groups
- Wrong, partial, missing, and unreadable are distinct
- Preserve minority and unconventional valid reasoning
- No inference of ability, effort, motivation, personality, or demographics
- No invented quotation or rationale

**Acceptance proof**

- Every response accounted for, including unreadable/outlier bins
- Exact quotation only when verified and approved
- Two experts approve cluster placement on gold fixtures
- Every recommendation traces to a visible evidence pattern
- Raw artifacts and response text purge at session close
- Low-confidence OCR never produces high-confidence classification
- District-defined rare/small-cluster policy applied without claiming guaranteed de-identification

**Defer**

Named grouping, longitudinal dashboards, grading, SIS/LMS integration, student histories, subgroup comparison, and response-level export.

## 10.7 StrandPlan — Lesson Design Studio

**Lane:** Green.  
**Problem:** Plans often become lists of activities rather than coherent target-evidence-instruction-decision sequences.

**Outputs**

- One-page lesson map
- Full plan with objective, evidence, phases, timing, materials, teacher moves, anticipated responses, and access routes
- Formative “If X, then Y” decision table
- Preparation checklist
- Short-time, no-device, and absent-material contingencies
- Source/provenance list

**Invariants**

- Backward alignment from evidence
- Learners perform meaningful intellectual work
- Each check leads to a plausible response
- Timing and materials are feasible
- No invented standard text, source citation, or safety procedure
- Access preserves the target
- No unsupported “research-based” label

**Acceptance proof**

- Every phase maps to the target
- Minutes sum correctly and include transitions, enforced by a deterministic timing validator — the model never does math the engine can check
- At least two target-relevant checks have response plans
- Closure produces evidence
- Materials are declared or marked missing
- Facts are teacher-entered or source-traceable
- Cross-subject experts approve feasibility

**Defer**

Calendar/LMS automation, mandated-curriculum replacement, learner profiles, and unverified standards alignment.

## 10.8 Rubric Relay

**Lane:** Amber by design; synthetic inputs first.  
**Problem:** Rubric feedback is time-consuming, inconsistently evidenced, and often too voluminous to act upon.  
**Framing:** The interface presents this module as conference preparation everywhere, never as feedback generation — the two conference questions are the product, and the matrix is their evidence.

**Authorized inputs**

One de-identified artifact, teacher-approved assignment, target, and rubric. No roster, gradebook, history, ranking, or authorship detection.

**Outputs**

- Evidence-to-criterion matrix with source pointers
- One verified strength
- One prioritized revision move
- Two conference questions
- Teacher-only uncertainty/missing-evidence notes
- No score or grade

**Invariants**

- Every feedback claim has visible evidence
- Exact quotations are exact
- Absence, unreadability, and insufficient evidence are distinct
- Formative and criterion-referenced
- No effort, personality, disability, language-status, plagiarism, or AI-authorship inference
- No automated grade or batch comparison
- Feedback preserves authorial choice and remains bounded

**Acceptance proof**

- Every final claim links to a verified source location
- One hundred percent quotation fidelity
- No evidence means no evaluative claim
- Teacher approves each feedback element
- Zero score leakage
- Experts approve mapping for unconventional valid responses
- Artifact purges after approval

**Defer**

Scores, grades, ranking, batch use, persistent portfolios, authorship detection, and high-stakes decisions.

## 10.9 Forumwright — Discussion Design

**Lane:** Green, rosterless.  
**Problem:** Discussion plans may create prompts without equitable participation, evidence use, idea-building, or disciplined disagreement.

**Outputs**

- Teacher facilitation card
- Learner talk-move card
- Question sequence with purpose and evidence target
- Optional language supports
- Speaking, writing, pointing, drawing, AAC, partner-supported, wait, and pass pathways
- Non-ranking evidence/noticing tracker
- Opening, repair, disagreement, and synthesis language
- Post-discussion equity reflection as a printable teacher-only card — it gets used standing up, ninety seconds after the bell

**Invariants**

- Participation is not airtime
- Preserve wait, pass, and multimodal contribution
- No forced personal disclosure
- Challenge claims/evidence, not dignity
- Do not script learner opinions
- No fixed passive roles or false equivalence
- No AAC rearrangement

**Acceptance proof**

- Every major question maps to target and evidence
- At least three participation modes plus processing/pass
- Invite, build, press-for-evidence, repair, and synthesize moves present
- Sensitive-topic fixtures catch forced disclosure, reenactment, and false balance
- Frames clearly optional
- Plan fits available time
- No participation ranking or individual analytics

**Defer**

Recordings, transcripts, speaker analytics, emotion inference, participation prediction, and roster-based grouping.

## 10.10 KinDispatch — Bilingual & Family Press

**Lane:** Green general communications only.  
**Problem:** Teacher communication can hide actions inside jargon, mistranslation, inaccessible layout, or assumptions about devices, money, transportation, and time.

**Authorized inputs**

Generic class/school information, verified dates/contacts/links, target language, approved glossary, and locked district text. No learner-specific progress, grades, attendance, behavior, IEP/504, discipline, health, custody, immigration, legal notice, or recipient list.

**Outputs**

- Plain-language source letter or short message
- Aligned translation
- Mobile/SMS-length draft
- FAQ where useful
- Fact-lock summary
- Translation-status label
- Accessible print and digital files
- No address or delivery log

**Invariants**

- Preserve dates, conditions, costs, permissions, contacts, links, and negation
- No invented policy, promise, resource, or deadline
- No sensitive learner communication
- No automated distribution
- No certified-translation claim without evidence
- Do not replace an interpreter
- Avoid family-deficit assumptions

**Acceptance proof**

- Locked facts identical across versions
- Uncertainty remains visible
- Human review records reviewer and time
- Right-to-left, CJK, long-word, print, and mobile layouts remain legible
- Requested action, deadline, support route, and contact are explicit
- The source letter meets a deterministically verified plain-language readability target, and a lint warning flags more than one requested action per communication
- High-stakes and student-specific categories are blocked
- Application cannot address or send

**Defer**

Recipient databases, direct messaging, emergency/legal/discipline/medical/consent/special-education communications, and machine-only consequential translation.

## 10.11 Symbol Commons

**Lane:** Green libre core; private local assets remain segregated.  
**Problem:** Visual-support tools require a trustworthy asset foundation with stable meaning notes, rights, versions, and export controls.

**Curator workflow**

1. Import and hash for duplicates.
2. Complete privacy and rights check.
3. Assign stable concept ID, source, creator, license, and change history.
4. Record intended meaning and known ambiguity.
5. Add language labels, tags, related/contrasting concepts, and alt text.
6. Review access, culture, line weight, and variants.
7. Publish locally or to an approved open pack.
8. Version/deprecate without silently altering existing projects.

**Outputs**

- Searchable local library
- SVG and standard PNG sizes
- Stable concept/version identifiers
- Labels, tags, meaning/ambiguity notes, and alt text
- Ambiguity registry as a first-class feature: known-divergent readings visible at insertion time, recording disagreement rather than erasing it
- High-contrast and low-ink variants where appropriate
- Complete rights and modification metadata
- Machine-readable manifest and human-readable provenance report

**Invariants**

- No universal-meaning claim
- No unknown-source redistribution
- Local proprietary assets cannot enter open export
- Language remains metadata, not permanently baked into the graphic
- Existing projects retain selected asset versions
- No silent replacement
- Generated distributable art deferred until policy approval

**Acceptance proof**

- One hundred percent of distributable assets have verified provenance
- Unknown rights hard-block public export
- Proprietary fixtures never enter bundles
- Deterministic packaging and complete attribution
- Stable IDs resolve after updates
- Contrast, line weight, small-size, alt-text, and print tests pass
- Ambiguity studies record disagreement rather than erasing it

**Defer**

Community upload, public synchronization, web scraping, unclear generated-image rights, and proprietary-pack redistribution.

## 10.12 Inquirywright — Source & Inquiry

**Lane:** Green for public-domain, libre, or teacher-authorized sources.  
**Problem:** Source activities often collapse into generic comprehension, fabricated intent, decontextualized quotation, presentism, or false equivalence.

**Workflow and outputs**

- Verify creator, title, date, place, type, audience, provenance, rights, and transcription
- Define disciplinary target
- Separate literal observation from inference
- Draft sourcing, contextualization, close-reading, corroboration, and bounded-interpretation prompts
- Review contextual claims and author-intent language
- Add vocabulary, accessibility, sensitivity, and omission decisions
- Complete a sensitivity preflight naming the teacher's local review duty for traumatic content, mirrored from the invariants into the workflow surface
- Pair corroborating/complicating evidence
- Export source card, marked excerpt, verified transcript, prompt set, observation/inference table, sourced context, teacher guide, and rights record

**Invariants**

- Never fabricate quotation, metadata, context, intent, or corroboration
- A primary source is evidence, not transparent truth
- Separate perspective, limitation, context, and present interpretation
- Avoid teleology, presentism, and unsupported symmetry
- Harmful language is not silently sanitized
- No reenactment of enslavement, genocide, dispossession, or comparable trauma
- Assessment descriptions do not reveal answers
- Rights govern redistribution

**Acceptance proof**

- Missing critical metadata blocks publication or is labeled unknown
- One hundred percent transcript/quotation fidelity
- Context claims have an authoritative basis
- Every inquiry set includes genuine sourcing and corroboration
- Questions are answerable from supplied evidence or clearly identified outside knowledge
- Observation and inference remain structurally separate
- Omissions are marked
- Harmful-content and traumatic-roleplay fixtures trigger warning/refusal

**Defer**

Open-web research, current-events discovery, automatic rights clearance, student responses, and unsupported historical fact generation.

---

# 11. Universal Definition of Done

A module is not release-ready until all of the following exist:

## Product and instruction

- Signed purpose/non-purpose statement
- Versioned input and output schemas
- Module invariants encoded where deterministic
- Thirty to fifty stratified fixtures
- Independent expert ratings
- Teacher usability study
- Documented known failure modes

## Privacy and security

- Lane/data-flow record
- Exact outbound preview
- Retention and purge tests
- Prompt-injection red team
- No-content diagnostics proof
- Network egress trace
- Threat-model review

## Accessibility and language

- Keyboard and assistive-technology tests
- Zoom, contrast, grayscale, high-DPI, low-ink, duplex, Letter/A4 tests
- Pseudo-localization, right-to-left, and text-expansion tests
- Qualified reviewer for each initially claimed language pair
- Canonical accessible sample outputs

## Rights and openness

- Source and asset ledger
- SBOM and dependency-license report
- Open project-schema documentation
- Source/build correspondence for the release
- Attribution-complete package

## Operations

- Signed package and checksum
- Intune installation/uninstallation test
- Rollback and kill-switch drill
- Migration test
- Support and vulnerability process
- Release-to-requirement-to-test traceability matrix

## Sustainability

- Published governance document
- Contribution guide tested by an outside contributor
- Named second maintainer, or a documented recruitment attempt, by Release 0.3
- Recipe packs maintainable by a curriculum-literate contributor who is not the lead developer

---

# 12. Evaluation and test program

## Shared fixture families

1. **Gold:** clean teacher-created examples with expert-agreed expectations
2. **Ambiguity:** blur, poor handwriting, contradiction, incomplete pages, missing metadata, uncertain language
3. **Privacy adversarial:** synthetic names, faces, schedules, screens, barcodes, indirect identifiers, disclosures, hidden metadata
4. **Pedagogical edges:** unconventional valid reasoning, multiple answers, answer-leak temptations, construct-preservation challenges
5. **Language:** several language families, right-to-left, long translations, negation, idiom, units, proper nouns, mixed scripts
6. **Accessibility/rendering:** keyboard, screen reader, 200 percent zoom, grayscale, large print, duplex, low ink, page overflow
7. **Rights/provenance:** public domain, CC BY, CC BY-SA, proprietary local-only, unknown source, prohibited redistribution

## Pull-request CI

- Build on pinned .NET 10 with warnings as errors
- Formatting and analyzers
- Unit, contract, schema, and migration tests
- Recipe-manifest validation
- Dependency/license audit and SBOM
- Secret scan and static security analysis
- Deterministic rendering comparisons
- Coverage threshold for Domain, Application, and Contracts

## Provider tests

Fake-provider cases cover valid output, malformed JSON, schema mismatch, refusal, content filtering, truncation, timeout, 401/403, 429, 5xx, cancellation, and unsupported capability.

Live Azure smoke tests use synthetic inputs in an authorized release environment only.

## Hardware bench

The 1366×768 covenant is a **practical functional floor**, not a fixed layout
canvas, design target, or startup cutoff. At that screen size, after ordinary
operating-system chrome, every supported core authoring, review, status, and
recovery path must remain readable and reachable. Reflow, wrapping, and genuine
scrolling are valid. Smaller displays may still work, but they are outside the
current support evidence rather than mechanically blocked.

“Practical” describes permitted adaptation, not optionality: a supported core
path that becomes unreadable, unreachable, or unrecoverable at either reference
working-area profile fails the floor.

- Camera simulator in CI and physical camera before release
- Virtual print sink and physical printers
- Camera loss/reconnect, low light, rotation, and multi-page capture
- Printer missing, jam/reprint, duplex, and virtual-printer refusal
- Windows 10/11, standard/high DPI, offline start, expired Entra token

## Instructional evaluation

Use deterministic checks, double-blind expert review, teacher think-aloud sessions, representative-user co-design, print inspection, privacy red-teaming, and classroom simulation. Model self-ratings are not release evidence.

Think-aloud and seeded-error studies also measure Amber review-session length and interruption cost; if no-autosave fragility proves real, a district-approvable teacher-edit-only journal — persisting only text the teacher authored, never source images or model output — becomes an explicit Amber-architecture decision (section 20, item 15).

The cross-module human rubric scores ten dimensions from 0 to 3:

| Dimension | Release question |
|---|---|
| Target fidelity | Is the intended knowledge, skill, construct, and evidence preserved? |
| Evidence fidelity | Can every claim and recommendation be traced to authorized input? |
| Teacher control | Can the teacher inspect, edit, reject, reorder, undo, and approve? |
| Learner agency | Are refusal, repair, alternate participation, and meaningful choice preserved? |
| Access/language | Is the output age-respectful, multimodal, structurally accessible, and honest about translation? |
| Privacy minimization | Is the least-sensitive input used and the lane enforced? |
| Rights/provenance | Are source, license, attribution, modification, and redistribution status explicit? |
| Actionability | Can a teacher use the product without deciphering generic advice? |
| Error containment | Are ambiguity and low confidence visible and fail-closed? |
| Reversibility | Can the artifact survive outside the application in editable open formats? |

Release requires:

- No dimension below 2
- At least 26 of 30 overall
- No critical privacy, rights, locked-fact, answer-leak, or learner-agency failure
- Separate written approval for any Amber module

---

# 13. Roadmap and realistic effort

Assumptions:

- One developer with approximately 30 productive engineering hours per week
- Educator council contributing 8–15 aggregate hours weekly
- Estimates include code, tests, documentation, packaging, and ordinary correction
- Estimates exclude district review queues, legal/contract review, translation procurement, and hardware acquisition
- At 12–15 developer hours weekly, calendar duration roughly doubles

## Release 0.0 — Foundation extraction

**Estimate:** 12–16 developer-weeks

- Preserve all 75 Writer’s Kiosk tests as characterization tests
- Extract capture, Entra, profiles, rendering, and printing behind interfaces
- Add dependency wiring, state machine, strict contracts, lane policy, project format, diagnostics, and synthetic provider
- Move settings into AppData/ProgramData
- Enforce approval at render/export/print boundaries
- Create camera simulator and virtual print sink
- Build the first Deterministic Press presses — Blankforms Press, Flashcard Flywheel, Booklet Binder — as the rendering pipeline's real cargo, replacing equivalent synthetic-fixture effort
- Stand up the minimum-hardware bench machine (on the order of a 2015-era CPU, 8 GB RAM, 1366×768)
- Complete Gate 0 governance artifacts

## Release 0.1 — SequenceSlate vertical slice

**Estimate:** 10–14 weeks

- Staged-material-only intake
- Manual crop/rotate/redact
- Text-first cards and optional suggestions
- Minimal original/libre symbol pack and provenance kernel
- First/Then, Now/Next/Done, and task strips
- Bilingual alignment
- Edit, approve, print, PDF/PNG/SVG export, and Green project save

An honest educator alpha is plausible five to seven months after foundation work begins.

## Release 0.2 — Capture and language utilities

**Estimate:** 10–14 weeks

- Board to Brief
- Directions Duet
- OCR comparison and uncertainty workflow
- Reusable bilingual alignment
- Right-to-left and multi-script rendering hardening

## Release 0.3 — Green planning studio

**Estimate:** 12–18 weeks

- Scaffold Smith, including its task-entry scaffold (ADR-005)
- StrandPlan — Lesson Design Studio
- Forumwright — Discussion Design
- Complete Deterministic Press studio (all eight presses)
- Shared target, evidence, access-route, and instructional-decision contracts
- Instructional evaluation harness
- Second-maintainer recruitment (sustainability Definition of Done)

## Release 0.4 — Accessibility and commons

**Estimate:** 16–24 weeks

- Full local Symbol Commons and pack exchange
- Access Remix for simple semantic documents
- Keyboard, contrast, zoom, screen-reader, accessible HTML, and specialist verification
- Complete bundled-asset audit
- Foundry.Inference.Local feasibility spike with the shared capability-test kit

## Release 0.5 — Sources and communication

**Estimate:** 10–16 weeks

- Inquirywright — Source & Inquiry for teacher-provided OER/public-domain/authorized sources
- KinDispatch — Bilingual & Family Press for generic informational communication
- Provenance/citation editor
- Translation uncertainty and invariant preservation

## Release 0.6 — Amber research pilot

**Estimate:** 18–26 weeks

- Batch/evidence kernel
- ReteachSignal — Formative Evidence and Rubric Relay using synthetic fixtures first
- De-identification review, no-autosave sessions, evidence tracing, district-controlled export
- Independent privacy, bias/error, assessment, and security review
- No real student artifact until written authorization

## Release 1.0 — Hardening

**Estimate:** 8–12 weeks, partly overlapping

- Installer, Intune package, signing, staged rollout, uninstall, rollback
- Migration and compatibility tests
- Documentation, contribution and localization workflows
- SBOM, source archive, asset ledger
- Six-week staff pilot and defect burn-down

**Program estimate:** approximately 96–140 developer-weeks, or 24–34 full-time months for a reliable suite. Nights/weekends make four to six years more credible. Specialist and educator participation is likely 500–800 hours. District review may add independent calendar time.

---

# 14. First ninety days

## Days 1–15 — Charter and evidence baseline

- Approve product constitution, data lanes, prohibited uses, and stop-ship rules
- Establish working project/repository name and trademark-screening task
- Recruit educator council and accessibility/AAC, multilingual, privacy, and OER reviewers
- Freeze audited Writer’s Kiosk baseline commit
- Preserve test output and create architecture characterization map
- Write ADR-001: one Foundry, bounded recipes
- Write ADR-002: WinForms first, UI-independent services
- Write ADR-003: open .ocfproj package
- Write ADR-004: structural ApprovedArtifact gate
- Ratify ADR-005: TaskDock absorption into Scaffold Smith (ratified 29 August 2026)
- Draft data-flow and trust-boundary diagrams

## Days 16–30 — Skeleton

- Create solution/projects and dependency rules
- Introduce dependency injection and synthetic provider
- Implement domain lane types, artifact revisions, validation issues, and approval receipt
- Implement state machine
- Define semantic ArtifactDocument and JSON schemas
- Create content-free diagnostic schema
- Add license/SBOM/secret/static-analysis CI

## Days 31–45 — Intake and policy

- Wrap existing camera and import pipeline
- Implement session-scoped SourceEnvelope
- Add metadata stripping and crop/rotate
- Prototype redaction-assistance UI with honest limitations
- Implement exact outbound payload preview
- Add ProgramData district policy and LocalAppData teacher preferences
- Add fake printer/export sinks

## Days 46–60 — Approval and rendering

- Implement DraftArtifact versus ApprovedArtifact type boundary
- Build source/draft/uncertainty review surface
- Implement locked-field validators
- Refactor bilingual pairs into semantic nodes
- Build escaped HTML and print renderers
- Test cancellation and purge paths

## Days 61–75 — Asset kernel and SequenceSlate thin slice

- Define asset/concept/provenance manifest
- Curate a tiny original/libre test pack
- Implement Task Strip with teacher-entered steps
- Add First/Then and Now/Next/Done
- Add agency cards
- Add Green project save/export

## Days 76–90 — First audited vertical slice

- Add optional structured model suggestions
- Run 30 staged-task fixtures
- Keyboard and screen-reader walkthrough
- Print and bilingual regressions
- Prompt-injection and PII-canary red team
- Teacher think-aloud sessions
- Produce 0.1-alpha evidence bundle
- Decide whether SequenceSlate merits continued expansion before starting the next module

---

# 15. Governance and roles

| Role | Accountable work |
|---|---|
| Product owner/master teacher | Purpose, instructional constitution, module priorities, classroom usability |
| Lead developer/maintainer | Architecture, implementation, CI, releases, vulnerabilities, open-source stewardship |
| District IT/security | Tenant, endpoint, RBAC, egress, Intune, signing, monitoring, incident and rollback |
| Privacy/legal/records | Lane decisions, FERPA/Maryland analysis, retention, deletion, contracts, litigation hold |
| Curriculum/content reviewers | Target fidelity, facts, standards, subject fixtures, pedagogical evaluation |
| Accessibility/AT reviewers | Keyboard, screen reader, outputs, cognitive access, ACR/VPAT |
| AAC users/SLP/special educators | Visual-support agency, terminology, symbol/AAC boundaries |
| Multilingual services/family liaisons | Translation review, family clarity, localization, interpreter boundaries |
| OER/license steward | Asset/dependency rights, provenance, attribution, takedown |
| Safeguarding leads | Direct-source procedure, alert boundaries, training, incident response |
| Teacher pilot council | Think-alouds, time-on-task, edit burden, output usefulness, classroom fit |

No single role may waive another role’s critical gate.

---

# 16. Stage gates and evidence

## Gate 0 — Charter

- Purpose/non-goals and prohibited decisions
- Module lane matrix
- Data inventory and lifecycle
- Privacy impact assessment
- Retention/disposal decision
- Accessibility requirements
- AAC/visual-support terminology
- Licensing policy
- RACI

## Gate 1 — Shared foundation

- Trust-boundary and data-flow diagrams
- Signed recipe schema
- Restricted-content block
- Local preflight and outbound preview
- Stateless provider adapter and egress allowlist
- Content-free diagnostics
- Open project format
- Signed packaging/rollback design
- Kill switch
- SBOM and asset ledger

## Gate 2 — Verification

- Unit/integration/render evidence
- Disk/network/privacy-canary report
- Prompt-injection red team
- SAST/SCA/secret scan
- Accessibility report and ACR/VPAT draft
- Golden instructional corpus and independent ratings
- Localization/RTL report
- License report
- Model/recipe cards
- Seeded-error teacher study

## Gate 3 — District readiness

- Central instructional-software approval
- Provider contract/configuration review
- Deployment geography and statefulness decision
- RBAC/Conditional Access/policy attestation
- Records-approved retention/disposal
- Incident and safeguarding playbooks
- Intune/signing/uninstall/rollback evidence
- Teacher, accessibility, privacy, and administrator training
- Support, patch, disclosure, and end-of-life commitments

## Gate 4 — Pilots

1. Synthetic/teacher-authored verification
2. Staff-only Green pilot
3. Supervised Green classroom-output pilot after central approval
4. Amber pilot only after Amber architecture and written approval
5. Restricted features remain absent

## Gate 5 — Release/change control

Every provider, model, recipe, retention rule, symbol pack, translation engine, major dependency, or data-flow change triggers corresponding regression and re-review. Production monitoring records configuration and health, never instructional content.

---

# 17. Success measures

## Universal engineering measures

- All inherited 75 tests remain passing
- Zero render/export/print paths for unapproved drafts
- Zero raw content in seeded diagnostics tests
- Network egress only to allowlisted endpoints
- Transient-source purge succeeds after completion and cancellation
- One hundred percent shipped-file provenance
- No open P0/P1 defects
- At least 95 percent completion in moderated keyboard workflow
- At least 99.5 percent crash-free sessions during six-week staff pilot

## Human-value measures

- Median capture/import-to-approved artifact time
- Percentage of proposed elements retained, edited, or rejected
- Critical factual/locked-field error rate
- Unsupported-claim rate
- Teacher correction burden versus manual creation time
- Teacher-rated immediate usability
- Learner/user agency and age-respectfulness review
- Accessibility defect count and severity
- Translation critical-meaning error count
- Successful project reopen/migration/export rate

Do not measure success through token volume, number of generated pages, teacher acceptance clicks, or model self-scores.

---

# 18. Priority risk register

| Risk | Severity | Primary owner | Required mitigation/evidence |
|---|---:|---|---|
| Reidentification of supposedly anonymous work | Critical | Privacy/records | Lane inheritance, contextual review, irreversible redaction, suppression policy |
| Hidden persistence | Critical | Security/IT | Forensic residue report, stateless path, documented spooler/OS boundary |
| Safety miss/false alert/substituted reporting | Critical | Safeguarding/legal | Advisory-only pause, direct-source adult review, separate procedures |
| Prompt injection or exfiltration | Critical | Engineering/security | No tools/secrets, schema, egress allowlist, hostile corpus |
| Inaccessible core workflow/output | High | Accessibility/AT | Manual keyboard/NVDA/Narrator testing, ACR/VPAT, structured formats |
| Unsafe directions or translation | High | Curriculum/localization | Locked official text, invariant checks, qualified review |
| Automated grading/grouping or biased feedback | High | Curriculum/privacy | No consequential automation, evidence links, independent evaluation |
| AAC harm or misleading terminology | High | AAC governance | Visual-support boundary, co-design, agency options, no rearrangement |
| Unlicensed asset/font/source | High | License steward | CI hard fail, complete manifest, takedown |
| Shared-device access | High | IT/security | Individual identity, least privilege, lock, secure print |
| Provider/model drift | High | AI governance | Versioning, evaluation gate, configuration monitoring, rollback |
| Records conflict | High | Records officer | Retention schedule, hold procedure, authorized disposal |
| Public support/CI leak | High | Maintainer | Synthetic-only fixtures, scrubbed diagnostics, issue warning |
| Malicious asset/pack | High | Commons maintainer | Curated packs, safe ZIP handling, sanitization, no executable content |
| Maintainer loss or project abandonment | High | Maintainer | Published governance, tested contribution guide, second maintainer by 0.3, buildable-from-source guarantee |
| Accumulating-store re-identification | High | Privacy/records | Teacher-authored-descriptions-only invariant, no per-check traces, small-cluster suppression, fixtures |

---

# 19. Stop-ship conditions

Do not release if:

- Restricted data can be uploaded, saved, or externally alerted in an early module.
- Raw Amber content persists by default or enters logs, telemetry, crash reports, recent files, or unapproved provider state.
- The teacher cannot inspect the exact outbound derivative before Amber egress.
- The application makes unsupported FERPA, anonymity, de-identification, zero-retention, regional-processing, or in-memory-only claims.
- AI output can auto-send, auto-print before approval, grade, place, group, discipline, modify AAC, or initiate a legal/safety report.
- The safety design implies complete detection or substitutes an internal notice for direct adult action.
- A critical keyboard/screen-reader path is blocked.
- Canonical learner output has incorrect reading order or inaccessible structure.
- Prompt injection can escape the recipe/schema, reach tools, reveal secrets, or leak another job.
- Any shipped code, symbol, font, translation, sample, or source has unknown/prohibited redistribution rights.
- Consequential translation, safety language, quotation, or historical claim can be invented without a human gate.
- Provider/model/recipe changes bypass regression evaluation.
- There is no signed package, rollback, kill switch, incident procedure, retention decision, or required central approval.

---

# 20. Decisions to make before coding beyond the skeleton

1. Public project and executable name after trademark screening — **decided 29 August 2026: the public name is Honest Ink (ADR-006); "Open Classroom Foundry" remains the working/repository title; counsel confirmation is a pre-release checkpoint**
2. Whether Writer’s Kiosk and Foundry remain separate repositories or later share an extracted library repository
3. Exact Green project save locations permitted by district policy
4. Local OCR implementation after a Windows/API feasibility spike — **bounded Board-to-Brief implementation added 30 August 2026; installed-language, field-image, human-AT, and release evidence remain open (see §10.2)**
5. First two supported language pairs and named reviewers
6. Original/libre symbol design system and content license
7. Accessible digital export target: structured HTML first, then DOCX/ODT
8. District policy format, signing, and configuration precedence
9. Exact Azure deployment/version attestation available to the client
10. District-approved definition and handling of an Amber session
11. Pilot success thresholds and observation period
12. Long-term maintainer, security contact, and end-of-life promise
13. Local inference posture: Foundry.Inference.Local target hardware and first supported local model family
14. Minimum supported hardware floor — **functional covenant decided 31 August
    2026: 1366×768 is the practical, firm floor defined in §12; acquiring and
    maintaining the permanent physical bench machine and recording physical-
    device evidence remain open**
15. Amber teacher-edit-only journaling: whether, and under what district-approved retention rule, pending pilot fragility data
16. Ratification of ADR-005: TaskDock absorption into Scaffold Smith — **decided: ratified 29 August 2026**
17. Public display names for the six screened modules — **decided 30 August 2026 by ADR-008 and corrected 31 August by ADR-009: SequenceSlate, StrandPlan, Forumwright, ReteachSignal, Inquirywright, and KinDispatch; legacy identifiers remain stable and counsel review remains a pre-release checkpoint**

---

# 21. Source basis

This plan draws upon:

- The supplied Office of Curriculum and Instruction / Department of IT email
- Writer’s Kiosk source and test suite at audited commit c2b670b
- The Honest Ink 227-Idea Atlas (version 2.0; repository document `docs/idea-atlas.md`)
- The Master's Review 1.0 (open-classroom-foundry-davinci-review.md), whose amendments this version applies
- Accepted ADR-005, ADR-006, ADR-008, and ADR-009, plus the Deterministic Press module specification
- U.S. Department of Education FERPA guidance on direct and indirect identifiers
- Official PECS description establishing it as a specific six-phase protocol
- Provider capability documentation, cited per configured deployment at Gate 3 (for district Azure OpenAI, Microsoft's Azure OpenAI documentation)
- GNU guidance for GPL-3.0-or-later
- WCAG 2.2 and established accessibility-testing practice
- Maryland and BCPS governance sources identified in the audit
- Architecture, instructional, accessibility, privacy, security, licensing, and delivery reviews conducted for this plan

Important references:

- FERPA PII: https://studentprivacy.ed.gov/content/personally-identifiable-information-education-records
- PECS protocol description: https://pecs.com/picture-exchange-communication-system-pecs/
- Writer’s Kiosk: https://github.com/Spacejunk-io/writers-kiosk-csharp
- Azure OpenAI documentation (deployment capability and attestation authority at Gate 3): https://learn.microsoft.com/azure/ai-services/openai/
- GNU license recommendation: https://www.gnu.org/licenses/license-recommendations.html
- WCAG 2.2: https://www.w3.org/TR/WCAG22/

---

# 22. Final implementation judgment

Begin with the foundation and one honest vertical slice. SequenceSlate should prove the full grammar:

> staged capture → privacy preflight → bounded structured suggestion → deterministic validation → teacher edit → explicit approval → accessible rendering → print/export → source purge → reopenable free project.

If that path is trustworthy, fast, accessible, and instructionally worthy, the remaining Green modules become disciplined extensions. ReteachSignal and Rubric Relay then become tests of whether the Foundry can enter the Amber lane without betraying its first principle.

The purpose is not to make the machine appear to be a master teacher. It is to give real teachers a transparent, extraordinarily capable press and apprentice—one that remains subordinate to truth, learner agency, human judgment, public ownership, and the verity supreme.
