# Foundry.Inference.Local — feasibility spike (Release 0.4)

**Date:** 29 August 2026 · **Question:** can the liberation test's local-model path be met on the minimum hardware covenant — grammar-constrained structured output, fully offline, GPL-compatible, on a 2015-era CPU with 8 GB RAM?

## Requirements the adapter must meet

Same `IInferenceProvider` seam, same `ProviderCapabilityTestKit`, same Gate A and policy gate as every provider; no module may require it, and deterministic authoring stays independent of it. Strict structured output is non-negotiable (free-form is prohibited engine-wide).

## Candidates assessed

| Candidate | Structured output | License | Hardware floor | Verdict |
|---|---|---|---|---|
| **LLamaSharp** (llama.cpp bindings) | **True grammar-constrained decoding (GBNF)** — the schema is compiled to a grammar and malformed JSON becomes unrepresentable | MIT (bindings and llama.cpp) | 3–4B-parameter models at Q4 quantization run in ~3–4 GB RAM on CPU; slow but workable for suggestion-sized outputs | **Primary recommendation** |
| ONNX Runtime GenAI | No native grammar constraint; schema enforced by validate-and-retry | MIT | Comparable | Fallback |
| Ollama as a sidecar service | JSON mode; grammar support varies | MIT (server) | Comparable | Rejected for managed devices: an external service process contradicts the no-prerequisite posture and complicates Intune deployment |

## Recommendation

Build `Foundry.Inference.Local` on **LLamaSharp with GBNF grammars** when a slice needs live offline suggestions. Grammar-constrained decoding is the honest fit for this engine: the same strictness the schema validator enforces after the fact, enforced during generation.

## Constraints recorded now

1. **Model weights are separate assets** with their own licenses — never bundled by default; the district or teacher supplies a model file, and its provenance enters the asset ledger like any asset. Candidate families with permissive weights (Phi, Qwen, Gemma classes) are evaluated at implementation time, not promised now.
2. **Determinism is bounded, not absolute**: temperature 0 and a fixed seed give reproducibility within a build, not byte-stability across llama.cpp versions — the evaluation gate and version pinning (plan §12) apply to the local path exactly as to the cloud path.
3. **The capability test kit is already the acceptance bar** (`ProviderCapabilityTestKit`, shipped with this spike): the adapter passes it or no module sees it.
4. Suggestion latency on the hardware floor will be tens of seconds, not sub-second; the UI must show progress and remain cancellable (the kit tests cancellation).

**Verdict: feasible.** Implementation is scheduled work, not a research risk.
