# ADR-001: One Foundry with bounded recipes, never per-idea executables or an open generation surface

**Status:** Accepted — by adoption of implementation plan 2.0 (29 August 2026); recorded here as the standing decision record
**Date:** 2026-08-29
**Ratified by:** Product owner / master teacher

## Context

The idea atlas names 227 candidate tools. The Master's Review's consolidation shows they resolve into roughly a dozen engines plus one shared kernel, with everything else expressible as data-only presets. Two failure modes threaten a project of this breadth: maintaining hundreds of codebases (a lifetime of unfinished work), or collapsing everything into an unbounded "generate anything" chat surface (which forfeits every instructional, privacy, and evidence guarantee the constitution demands).

## Decision

Build **one engine** — capture, normalization, lane policy, recipes, inference boundary, validation, approval, rendering, export, projects, diagnostics — and expose every product as a **narrowly bounded, versioned recipe** running on it.

- Two trust tiers only: first-party built-in modules compiled with the application, and data-only recipe packs (schemas, prompts, allowlisted validators, editor/renderer template references, warnings, evaluation-suite identity). No downloaded code, DLLs, scripts, arbitrary HTML, or recipe-defined network calls — ever, in managed deployments.
- Every recipe declares its instructional purpose, prohibited purposes, allowed inputs, maximum data lane, strict output schema, and evaluation suite. Structured output only; no free-form chat surface exists in the product.
- Modules may not directly reach the camera, network, filesystem, printer, or diagnostics sink; all access flows through the engine's service seams (implementation plan §6.2).
- Projects pin recipe versions; new recipe versions install alongside old ones until a project-specific migration is accepted.

## Alternatives considered

1. **Separate applications per tool** — rejected: duplicates every gate, contract, and test harness hundreds of times; guarantees abandonment.
2. **One open chat/agent surface with tool prompts** — rejected: unverifiable instructional purpose, unenforceable lanes and invariants, and an unbounded prompt-injection surface; contradicts the constitution's separation of epistemic layers.
3. **Plugin architecture with downloadable executable modules** — rejected: unacceptable supply-chain and district-security posture; explicitly listed as a non-goal through 1.x.

## Consequences

Shared gates, accessibility work, and evaluation harnesses are amortized across every product; a new tool becomes cheap exactly when it deserves to be. The cost is discipline: ideas that want prose-shaped freedom must be reshaped into bounded recipes or not built. Reversible only by superseding ADR; reversal would reopen every security and governance gate.
