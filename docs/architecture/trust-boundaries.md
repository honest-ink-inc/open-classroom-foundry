# Data-flow and trust boundaries

**Date:** 29 August 2026 · Companion to implementation plan §4 (lanes), §5 (gates), §6 (architecture), §7 (controls). These diagrams are the Days 1–15 deliverable; they are revised whenever a flow or boundary changes (Gate 5 change control).

## The artifact data-flow, gates inline

Every artifact walks the mandatory state machine (plan §6.3). The three human gates and the purge are structural, not procedural.

```mermaid
flowchart TD
    subgraph UNTRUSTED["Untrusted content zone"]
        SRC["Source material: photos, PDFs, pasted or typed text.<br/>Always data, never instructions - injection corpus tested"]
    end

    subgraph LOCAL["Local machine - authenticated teacher session"]
        CAP["Capture / import<br/>(ICaptureSource)"] --> ENV["SourceEnvelope<br/>session-scoped bytes, no filesystem path"]
        ENV --> NORM["Normalize: crop, rotate,<br/>metadata strip, redaction assist"]
        NORM --> LANE{"Lane policy<br/>(IDataPolicyEvaluator)<br/>unknown defaults to Amber"}
        LANE -->|"Restricted"| BLOCK["Blocked state"]
        LANE -->|"Green / Amber"| GATEA["GATE A - privacy preflight:<br/>teacher sees the exact outbound derivative"]
        DRAFT["DraftArtifact"] --> VAL["Schema + locked-field +<br/>module-invariant validators"]
        VAL --> GATEB["GATE B - field-level review:<br/>accept, edit, reorder, replace, reject"]
        GATEB --> APPR["ApprovedArtifact<br/>(the only type sinks accept - ADR-004)"]
        APPR --> REND["Renderer (escaped HTML / print)"]
        GATEB -.-> PURGE[("Purge transient sources<br/>on completion AND cancellation")]
    end

    subgraph CLOUD["District cloud boundary - allowlisted endpoints only"]
        ENTRA["Entra ID<br/>individual identity"]
        LLM["Inference deployment<br/>stateless, no tools, no secrets,<br/>no cross-job state"]
    end

    subgraph SINKS["Output boundary"]
        PRN["Printer + spooler<br/>(documented residue boundary)"]
        EXP["Exports + Green .ocfproj projects<br/>(teacher-selected location)"]
    end

    SRC --> CAP
    GATEA -->|"minimum explicit payload"| LLM
    LLM -->|"strict structured object"| DRAFT
    REND --> PRN
    REND --> EXP
    GATEA -.->|"offline / deterministic path:<br/>teacher authors manually"| DRAFT
```

**Gate C** (direct-source adult safety review) is orthogonal to this flow: on an apparent safety concern the program pauses normal output and privately directs the supervising adult to the original source and the district procedure. It broadcasts nothing.

## Trust zones and what may cross each boundary

```mermaid
flowchart LR
    subgraph Z1["Z1 - Untrusted content"]
        U["Source pages, images,<br/>OCR text, QR codes, metadata"]
    end
    subgraph Z2["Z2 - Engine core (local)"]
        E["Lane policy, recipes, validators,<br/>gates, state machine, renderers"]
    end
    subgraph Z3["Z3 - Module sandbox"]
        M["Modules: no camera, network,<br/>filesystem, printer, or diagnostics access"]
    end
    subgraph Z4["Z4 - District cloud"]
        C["Entra ID + inference deployment"]
    end
    subgraph Z5["Z5 - OS residue surface"]
        O["Print spooler, pagefile,<br/>crash dumps, temp rendering"]
    end

    U -->|"data only - can never override a recipe"| E
    M <-->|"service seams only (plan 6.2)"| E
    E -->|"minimum payload, egress allowlist,<br/>Gate A preview first"| C
    C -->|"schema-validated draft only -<br/>no lane authority, no approval authority"| E
    E -->|"documented + forensically tested<br/>residue boundary (canary suite)"| O
```

## Boundary register

| Boundary | Crosses it | Never crosses it | Verification |
|---|---|---|---|
| Z1 → Z2 | Source bytes as data | Instructions, tool calls, lane decisions | Prompt-injection red-team corpus |
| Z3 ↔ Z2 | Seam calls (`IRecipeRunner`, `IRenderer`, …) | Direct device/network/file access | Architecture tests (tests/Unit) fail the build on a forbidden reference |
| Z2 → Z4 | Gate-A-previewed minimum payload to allowlisted endpoints | Amber content without preview; anything without explicit Generate | Egress trace; no-background-calls test |
| Z4 → Z2 | Strict structured object | Free-form output, tool use, cross-job state | Provider capability tests; fake-provider suite |
| Z2 → Z5 | Unavoidable OS artifacts within documented limits | Intentional persistence of Amber content; content in logs | Synthetic canary search after success, failure, crash, reboot, print, uninstall |
| Z2 → storage | ProgramData = IT policy (read); LocalAppData = preferences + content-free diagnostics; teacher-selected = Green projects only | Secrets or projects beside the executable; Amber autosave | Storage-location tests; residue suite |

## Standing honesty rule

"The application does not intentionally persist the source capture" is the claim these diagrams support. Pagefiles, crash dumps, spoolers, camera drivers, and endpoint tools are the OS residue surface — bounded and tested, never denied (plan §6.4).
