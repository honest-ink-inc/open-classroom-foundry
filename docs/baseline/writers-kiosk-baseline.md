# Writer's Kiosk baseline freeze

**Frozen:** 29 August 2026
**Repository:** https://github.com/Spacejunk-io/writers-kiosk-csharp
**Commit:** `c2b670b4b6ef7ba9ba07160949efb0f4940778f0` — `v1.6.1: install-free silent printing; virtual-printer guard; frame-drop`
**Tag:** `foundry-baseline-0` — created in the local clone at `Desktop\writers-kiosk-csharp` (working tree verified clean at exactly this commit) and **pushed to GitHub 29 August 2026**, confirmed by remote read-back
**Test evidence:** all **75 tests pass** — run 29 August 2026 from a pristine clone at the frozen commit, .NET SDK 10.0.302 → [test-output-c2b670b.txt](test-output-c2b670b.txt)

The remote HEAD and the local clone were both at the audited commit at freeze time, so no divergence exists to reconcile.

## Characterization map

Target framework `net10.0-windows10.0.19041.0`; 2,950 lines of C# across 14 files plus one test project.

| File | Lines | Role | Foundry disposition (Release 0.0) |
|---|---:|---|---|
| KioskForm.cs | 1,087 | The large UI/orchestration class the plan flags: capture, prompt, parse, review, print flow in one form | Strangler target — behavior extracted behind seams; the form itself is never ported |
| Printing.cs | 391 | Windows printing, silent print, install-free path | → `Foundry.Infrastructure.Windows` behind `IPrinter` |
| LlmClient.cs | 302 | Provider call path, free-form Markdown output (Markdig) | → replaced by `IInferenceProvider` + strict schemas; Markdown parsing is not ported (ADR-003) |
| Subjects.cs | 222 | Subject/grade/band profiles | → `Foundry.Domain` profile types |
| KioskConfig.cs | 136 | Configuration, relative-file persistence | → ProgramData/LocalAppData split (plan §6.4); relative-file pattern is not ported |
| ImageOps.cs | 135 | Image correction | → `Foundry.Infrastructure.Windows` behind `IDocumentNormalizer` |
| Profiles.cs | 123 | Classroom profiles | → `Foundry.Domain` |
| PdfRasterPrinter.cs | 121 | PDF rasterization for print | → `Foundry.Rendering` / `Infrastructure.Windows` |
| LogWindow.cs | 103 | Log UI | → not ported; diagnostics become content-free `IDiagnosticsSink` |
| KioskLog.cs | 93 | Local activity counters | → content-free diagnostics schema |
| EntraAuth.cs | 88 | Keyless district authentication | → `Foundry.Infrastructure.Windows` |
| SafetyAlert.cs | 83 | Safety pause behavior | → Gate C implementation reference |
| FeedbackLog.cs | 42 | Feedback capture | → reviewed for lane compliance before any port |
| Program.cs | 24 | Entry point | — |

**Dependencies at baseline:** FlashCap 1.10.0 (camera), Markdig 0.41.0 (Markdown — retired with free-form output), DotNetEnv 3.1.1, Azure.Identity 1.13.2 (Entra).

## Rules of the freeze

1. The 75 tests are preserved as **characterization tests** through Release 0.0: extraction refactors must keep them passing against the kiosk, which remains independently runnable (plan §6.1).
2. Improvements are ported back to the kiosk only after equivalent behavior passes tests.
3. Whether the two repositories later share an extracted library repository is plan §20 decision 2 — still open, not needed before Release 0.0 completes.

## Open actions

None. The freeze is complete: commit audited, tag published (`git ls-remote --tags origin foundry-baseline-0` returns `c2b670b4b6ef7ba9ba07160949efb0f4940778f0`), and test evidence preserved. Any future work on the kiosk happens on new commits; the baseline is immutable by convention and citable by tag.
