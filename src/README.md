# Solution boundaries

Projects are created during Days 16–30 (implementation plan §14). The directories exist now so the boundary map is visible from the first commit. Dependency rules are enforced by architecture tests once code lands.

| Project | Owns | May not |
|---|---|---|
| `Foundry.App.WinForms` | The teacher shell: forms, presenters wiring, navigation | Contain domain, rendering, or policy logic (ADR-002) |
| `Foundry.Domain` | Lane types, artifact revisions, approval receipts, validation issues, state machine | Reference any infrastructure or UI |
| `Foundry.Contracts` | Recipe/manifest/document schemas, service seam interfaces | Contain behavior |
| `Foundry.Application` | Use-case orchestration across the seams | Reach devices or network directly |
| `Foundry.Infrastructure.Windows` | Camera, printing, storage, Entra, OS integration | Be referenced by Domain or Contracts |
| `Foundry.Inference.Abstractions` | IInferenceProvider and the capability-test kit | Know any concrete provider |
| `Foundry.Inference.AzureOpenAI` | District Azure OpenAI adapter (stateless, allowlisted) | Hold secrets, tools, or cross-job state (§6.7) |
| `Foundry.Inference.Local` | Post-0.3 local-model adapter, same capability-test kit | Be required by any module |
| `Foundry.Inference.Synthetic` | Deterministic scripted provider for CI and offline development (plan §6.7) | Ship as a district inference path or use randomness |
| `Foundry.Rendering` | Escaped HTML and print renderers over the semantic ArtifactDocument | Accept a DraftArtifact at a sink (ADR-004) |
| `Foundry.Storage` | `.ocfproj` packages, safe path handling, rebuildable index | Treat the index as the source of truth (ADR-003) |
| `Foundry.Modules.DeterministicPress` | Module Zero: the eight presses | Reference inference, OCR, capture, or redaction seams — enforced by a build-failing architecture test |
| `Foundry.Modules.BuiltIn` | First-party modules (All Aboard onward) | Reach camera/network/filesystem/printer/diagnostics directly (ADR-001) |

Universal rule: **a module may not directly reach the camera, network, filesystem, printer, or diagnostics sink.** All access flows through the engine's seams (implementation plan §6.2).
