# Enactment handover — how to build the fourth menu wisely

**Date:** 29 August 2026, end of the forge day · **Audience:** whichever hand — a future session, a second maintainer, the typist at a keyboard — enacts the [fourth forge menu](2026-08-29-fourth-forge-menu.md). This document assumes nothing about who reads it. The menu says WHAT; this says HOW, and carries the craft knowledge three exhausted menus paid for, so no trap already sprung once is sprung twice.

## I. Read before touching anything

1. The **standing laws**, verbatim in the [first menu](2026-08-29-waiting-window.md) §I. The short form: presses take parameters, never prose; nothing renders, prints, exports, or persists without a typed `ApprovedArtifact`; lanes escalate only, Amber never persists; model self-ratings are not release evidence; the AAC/SLP seat and the district instrument are not waivable from the keyboard.
2. The four menus in order — [one](2026-08-29-waiting-window.md), [two](2026-08-29-second-forge-menu.md), [three](2026-08-29-third-forge-menu.md), [four](2026-08-29-fourth-forge-menu.md) — struck items carry dated notes that double as design records.
3. The [UIA harness traceability](../accessibility/uia-harness-traceability.md): which walkthrough steps are machine-guarded, which are human, and the accessibility findings register.
4. The divination rite, as amended: menus draw from TWO sources — the atlas, and the compositions the enriched product affords — and each item names its source. "Proceed with …" adopts an item; completed items are struck with a dated note, never deleted.

## II. State of the repository (main @ `62e6cde`)

- **597 tests green** across six suites; full `dotnet format --verify-no-changes` gate green; CI runs the whole solution on windows-latest.
- **18 recipes, 48 Press Room catalog engines**, two authoring surfaces (Press Room, All Aboard — mode-complete), library door, native print, vector-first PDF, booklet imposition, landscape + low-ink, pseudo-locale, UIA harness.
- **The stale zip:** `out/honest-ink-win-x64.zip` was built 10:48 AM, BEFORE any menu — it contained the retired empty `Form1`, and menu-4 item 1 existed because of it. *Resolved 29 Aug 2026, item 1: `tools/publish.ps1` re-run against the current tree; the zip now carries the real app (verified by launching the published exe). Re-run the script after any change meant for the pilot machine — the artifact is only ever as fresh as its last run.*
- ~~**Open in parallel:** the `.ocfproj` WRITER nondeterminism task (zip-entry timestamps; found when `task-strip-bilingual.ocfproj` differed across identical runs). A worktree branch `claude/wizardly-grothendieck-017c73` sits at `af29ace` with no commits yet. Whoever lands it must rebase across everything since — including the READER hardening in the same file (`OcfprojProjectStore.cs`, menu-2 item 6) — then re-run the Integration suite. The reader and writer changes are in disjoint methods; the merge should be clean, but prove it, don't assume it.~~

  *Closed 29 Aug 2026, on `main`, not on the branch — and **the recorded diagnosis was wrong**, which is the part worth carrying forward. Measured before fixing: two generator runs differed in exactly one place, `manifest.json`'s `projectId`, because `SaveGreenProjectAsync` minted a fresh `Guid.NewGuid()` on every save. The zip-entry timestamps blamed in the original note were a **second, real cause that the measurement nearly hid** — back-to-back runs share a DOS two-second window and match by luck, but every stamp read the wall clock rather than the request's save instant, so any pair of runs straddling a boundary would differ. Both are fixed: the project id is now derived from the save's own identity (name-based over SHA-256, RFC 9562 version 8) and every entry is stamped from `SavedAtUtc`, clamped into the 1980–2107 DOS window so an instant-less request writes the epoch instead of throwing. Nothing reads `ProjectId`, which is exactly why a random value bought nothing and cost determinism. Four tests in `OcfprojStoreTests` pin it: the stamp itself (not a run comparison, so speed cannot hide it), the id's stability AND its distinctness across projects, byte-identical repeat saves, and the undated-request clamp. The CI exclusion is deleted — the samples gate now compares all 40 files with no exclusions. 657 tests green, twice; samples double-run byte-identical across a rebuild boundary; pilot zip re-pressed. **The stale worktree `claude/wizardly-grothendieck-017c73` at `af29ace` never received a commit and is now superseded — it can be retired.** The lesson generalizes: a defect note records a hypothesis, not a finding; measure before fixing, and check whether the cause you recorded is the cause you have.*
- Version is `EngineIdentity.EngineVersion = "0.7.0-alpha"`. Bumping it, tagging, installing, distributing are the TYPIST's acts — never yours.

## III. The craft — idioms and sprung traps

### The closing rites (run for every item, in this order)
1. `dotnet build` Release — warnings are errors; zero tolerance.
2. `dotnet format OpenClassroomFoundry.slnx` (FIX first — new files land with LF and the repo wants CRLF), then `--verify-no-changes` and demand exit 0.
3. Full `dotnet test` on the solution, then AT LEAST one stability re-run — and filter test output so failure NAMES survive (a flake's name was once lost to a summary-only filter). The recurring under-load flake was caught by name during menu-4 item 4 and killed at its root: it was the dress rehearsal's shell Open dialog leg, and the cure was the `libraryPicker` seam, not a better lookup (traceability finding 9). If a NEW flake appears, capture the name first, then ask whether the flaky thing is ours at all.
4. If presses changed: run the SampleGenerator TWICE into scratch directories and hash-compare every `press-*` file. Byte-identical or it does not ship.
5. Strike the menu item with a dated note (never delete), update `docs/README.md` if a document was added, commit, push.

### Toolchain traps (each of these bit once)
- **Never round-trip source files through PowerShell `Get-Content | Set-Content`** — PS 5.1 reads BOM-less UTF-8 as ANSI and mojibakes every em-dash, ellipsis, and ★. Use the Edit/Write tools, or `[IO.File]::ReadAllText/WriteAllText` with `New-Object System.Text.UTF8Encoding($false)` (the `$false` matters: the format gate rejects BOMs — CHARSET error).
- **Commit messages via PS here-strings must contain no double quotes** — PS 5.1 native-arg mangling shreds them and git sees pathspecs. Rephrase; parentheses survive.
- Analyzers that WILL fire on new code: CA1859 (declare concrete `List<>`/`Dictionary<>` for private/internal params), CA1720 (no identifiers named `Int` etc.), CA1806 (consume or `_ =` P/Invoke returns), CA1305 (InvariantCulture on every format/parse), IDE0005 (dead usings), SYSLIB1062 (`LibraryImport` needs `<AllowUnsafeBlocks>` — already on in the UiAutomation csproj).
- The WPF-enabled UiAutomation test project drops the `System.IO` implicit using — add `using System.IO;` by hand there.
- Curated style: the `.editorconfig` silences taste rules WITH REASONS in comments. If the formatter demands a change that hurts the code (it tried to fold a dispatch chain into nested ternaries once), the fix may be curating the rule, not obeying it — but every silencing gets its reason written beside it.

### The localization gate (a source-scanning test; it WILL catch you)
- Any quoted literal containing a space, anywhere in `Foundry.App.WinForms` except `UiStrings.cs`, `UiaHarness.cs` (fixture document content), and `ProductIdentity.cs` (the ADR-006 name record), fails the Unit suite. Every chrome string goes in `UiStrings` as `T("…")`; module-supplied text renders through `UiStrings.Localize`; even a separator template (`"{0} — {1}"`) belongs in the catalog. Exceptions in the app assembly carry no prose message (type/name only).
- Every new chrome string is automatically pseudo-locale-covered; the pseudo tests assert every focusable control's name is ⟦bracketed⟩ under `UiLocale.Set(UiLocaleMode.Pseudo)`.

### WinForms + UIA (the harness findings register, operationalized)
- `SplitContainer` IGNORES `TabStop = false`; give splitters accessible names instead of fighting.
- Never set `AccessibleName` on a Label whose Text is the message — it masks the message from AT. Status labels speak their text; tests read `form.StatusText`.
- **A modal opened synchronously from a click wedges every UIA client.** House pattern: the click handler builds and validates, then `BeginInvoke` the modal (production) or runs the injected test runner synchronously. Every surface takes `reviewRunner` (and the Press Room a `libraryPicker`) as ctor seams — tests inject, production defaults to real dialogs.
- The legacy managed UIA client cannot enumerate top-level windows created after it attached: find new dialogs by Win32 title walk (`Win32WindowByTitle` in `HeadedUiaWalkTests`) and bridge with `AutomationElement.FromHandle`.
- In-process lookups: a parameter row holds a visual Label AND its named input (filter `c is not Label`); `NumericUpDown` children inherit its name (filter `c.Parent is not NumericUpDown`).
- Headed tests use `[HeadedFact]` (visibly skipped without a desktop, never silently passed); the UiAutomation assembly runs serialized; `--uia-harness review|capture|pressroom|allaboard` launches deterministic fixture surfaces, and `UiaHarness.FromArgs` wires temp-file exception traps for diagnosis.
- Mnemonics must stay unique per form (a test enforces it). Taken in the Press Room: R P O E S W L A I. In All Aboard: R P O E S.

### Building a press (the assembly line — a new engine is ~1 hour of honest work)
1. Engine in the module: static class, exact-arithmetic geometry from `BlankformsPress.Dimensions(size)`, validation that THROWS `ArgumentException` with a teacher-readable message (it surfaces verbatim in the speaking status), accessible `Description` on every `VectorGraphic`, teacher text VERBATIM, seeded behavior only via the module's own `SeededPrng` (never BCL `Random`), fit-checks so defaults land inside margins in EVERY offered orientation.
2. Recipe: add or reuse a `DeterministicPressRecipes` manifest; put each engine's inviolable invariant in `ProhibitedPurposes` (the house signature — "code is cargo", "the paper IS the record"). Bump the recipe-count assertions in `StudioPressTests` AND `SecondWavePressTests` (currently 18).
3. Catalog entry in `PressRoomCatalog.All`: typed parameters only; defaults in-bounds (the well-formedness test enforces it — it has caught real bugs) and page-fitting; `inputs.Whole/Mm/Bool/Lines/RawLines/IntList/SplitLines/Page()`. `RawLines` for anything code-shaped (indentation is content; it becomes exact geometry at 2.5 mm/space). A catalog entry buys the generated UI, the ±0.2 mm geometry sweep, and the determinism test FOR FREE — never bypass it.
4. Tests: the engine's load-bearing invariant as arithmetic (the Parsons key reconstructs the code; the timeline's decade is constant millimeters; the fade is `n − ⌈n·j/fades⌉`). The catalog covers the rest.
5. Samples: add to the SampleGenerator's press list; the double-run hash check then guards it forever.

### PDF (all in `VectorPdfWriter`)
`Fmt` is `0.###` invariant; every char ≤0xFF so string length IS byte count; characters outside WinAnsi throw (never substitute — Edge is the fallback path); Courier because 600/1000 em makes anchoring exact; test streams split on `">>\nstream\n"` (plain `"stream\n"` also matches `endstream`). Imposition layering is a wall: signature arithmetic in `BookletImposition.PdfSides`, placement in `WriteImposed`, composition in the app — do not let the layers leak.

### Gate discipline
Transforms happen BEFORE Gate B so the teacher reviews what prints (low-ink is the model). A derived document (tiling; a future class-set) is a NEW document and passes Gate B itself — the gate is structural, not hereditary. Reopened projects re-review and re-approve. `AppServices` holds the shared plumbing: `SessionOver`, `Render`, `OpenPrintView`, `Print`, `SaveToLibrary`/`OpenFromLibrary` with the `LibraryRoot` test seam.

### Test placement map
Unit (net10.0; no Storage, no App refs — but it DOES reference the SiteGenerator tool) · Integration (has Storage; NOT the press module) · Rendering (has the press module) · UiAutomation (has App + WPF-for-UIA; serialized; STA via `Sta.Run`) · source-scanning architecture tests live in Unit and find the repo root by walking to `OpenClassroomFoundry.slnx`.

## IV. Enactment notes, per menu-4 item

1. **Pilot build.** Study `out/publish` + `SHA256SUMS.txt` for the shape the typist's earlier process produced (sums cover dlls AND pdbs); a `tools/` script or PowerShell entry running `dotnet publish` on the app, zipping, hashing. The dress rehearsal belongs in `HeadedUiaWalkTests` using every established idiom (deferred modals, Win32 dialog find, `LibraryRoot` temp seam). Print step: assert the gate and the status path, not actual paper — CI has no printer.
2. **Chart Press.** Sibling of `TimelineWeaver` — proportionality asserted the same way. `SplitLines` for "label | value"; parse values like `IntList` parses (loud). Landscape-friendly like the timeline (`Page("Letter landscape")` default is available).
3. **Bell-to-Bell.** The invariant is cumulative clock arithmetic + loud overrun refusal. Times formatted invariantly; start time as a Text parameter parsed loudly ("8:30" → validate).
4. **Bug Zoo / Fluency.** Bug Zoo is `TraceTableTutor`'s sibling (RawLines, indent geometry, teacher misconception note as `TeacherOnlyNotice`). Fluency: text verbatim, `|` marks become visible breaks — assert reconstruction like the Parsons key.
5. **Card trio.** `AlgorithmAtelier.Deck` is private; either widen it or follow `LearnerHeldKit.StrategyShelf`'s loop. Shape-distinguish card kinds (double border precedent), never color alone.
6. **Glossary Garden.** Use the ENGINE's bilingual nodes (`StepRow`/`BilingualPair`) so `lang` semantics come from the tested renderer; note in the strike that typography answers to the week-3 seat.
7. **CI determinism gate + coverage.** Two SampleGenerator runs + hash diff as a ci.yml step (pwsh, mirroring the local rite). Re-MEASURE module coverage before adding to the threshold filter (the merged-linerate computation from menu-2 item 6's session works from the cobertura files); record numbers in the workflow checklist comment, per house habit.
8. **Site gallery.** Extend `SiteBuilder.Pages` + embed sample SVGs inline (render via `AccessibleHtmlRenderer` Svg target — single-sheet only, so curate single-sheet samples). Determinism test already patterns off `SiteBuilderTests`.
9. **Studio Sampler.** Iterate `PressRoomCatalog.All` (skip nothing silently — assert every engine appears, so a future engine can't be missed), build each at defaults, take each document's FIRST sheet, force one uniform page size for imposition (portrait Letter; skip-and-list landscape-only engines honestly on the cover), compose via `PdfSides` + `WriteImposed`. Cover page lists all 48 with recipe ids.
10. **Class Sets.** A composer, not a press: derive variant seeds deterministically (`baseSeed + variantIndex` is honest and reprintable-by-number), build N documents via the catalog entry's own `Build`, concatenate their vector pages into one document, stamp variant number + seed per page (most seeded engines already print their seed — assert the variant stamp separately). The composed document passes Gate B as itself.

## V. The humans' calendar, and what must not move

Sep 1–4: SPF/DKIM/DMARC check, the three letters, pilot kit printing, the typist's NVDA self-run. Sep 8: the pilot opens. SDAT→EIN sequencing with the corrected SS-4 (details in the [first menu](2026-08-29-waiting-window.md) §II). None of that is forge work. The must-not-build lists of all four menus remain binding — most especially: the seat's territory, the instrument's territory, no printing without approval, no auto-approving CLI, and no version/tag/install/distribute from the keyboard.

## VI. Resuming

The typist says **"Proceed with …"** and names a menu-4 item; that adopts it. Run the closing rites every time. Strike with dated notes. If the menu exhausts before the letters answer, the honest next act may be rest. And carry the amended rite forward: possibility flows from the atlas AND from what has been built — name the source of everything you forge.
