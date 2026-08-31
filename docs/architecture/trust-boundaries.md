# Data-flow and trust boundaries

**Date:** 29 August 2026 · **Last revised:** 30 August 2026 · Companion to implementation plan §4 (lanes), §5 (gates), §6 (architecture), §7 (controls). These diagrams are the Days 1–15 deliverable; they are revised whenever a flow or boundary changes (Gate 5 change control).

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
        LANE -->|"teacher-confirmed Green;<br/>Board intake only"| LOCR["Local Windows OCR<br/>installed user-language recognizer;<br/>no network call"]
        LOCR --> VERIFY["Literal transcript review:<br/>exact line separators retained;<br/>every platform word uncertain;<br/>teacher resolves text, order, and roles"]
        VERIFY --> DRAFT
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
        PV["Bounded print-view handoff<br/>leased file → hash-verified child copy<br/>one tokenized loopback GET"]
        BRW["Default browser<br/>(rendering/cache residue boundary)"]
        EXP["Exports + Green .ocfproj projects<br/>(teacher-selected location)"]
    end

    SRC --> CAP
    GATEA -->|"minimum explicit payload"| LLM
    LLM -->|"strict structured object"| DRAFT
    REND --> PRN
    REND --> PV --> BRW
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
        O["Print spooler, pagefile, crash dumps,<br/>print-view jobs, browser cache"]
    end
    subgraph Z6["Z6 - local Windows OCR capability"]
        W["Windows.Media.Ocr<br/>installed recognizer languages;<br/>no application credential or network service"]
    end

    U -->|"data only - can never override a recipe"| E
    M <-->|"service seams only (plan 6.2)"| E
    E -->|"minimum payload, egress allowlist,<br/>Gate A preview first"| C
    C -->|"schema-validated draft only -<br/>no lane authority, no approval authority"| E
    E -->|"documented + forensically tested<br/>residue boundary (canary suite)"| O
    E -->|"owned copy of one normalized,<br/>metadata-stripped PNG"| W
    W -->|"language tag + exact line text + words;<br/>no trustworthy word confidence"| E
```

## Boundary register

| Boundary | Crosses it | Never crosses it | Verification |
|---|---|---|---|
| Z1 → Z2 | Source bytes as data | Instructions, tool calls, lane decisions | Prompt-injection red-team corpus |
| Z3 ↔ Z2 | Seam calls (`IRecipeRunner`, `IRenderer`, …) | Direct device/network/file access | Architecture tests (tests/Unit) fail the build on a forbidden reference |
| Z2 → Z4 | Gate-A-previewed minimum payload to the exact allowlisted HTTPS origin, through a provider-owned transport with redirects disabled | Amber content without preview; anything without explicit Generate; redirects or replay to another origin | Egress trace; no-background-calls and no-redirect tests |
| Z4 → Z2 | Strict structured object | Free-form output, tool use, cross-job state | Provider capability tests; fake-provider suite |
| Z2 ↔ Z6 | An owned in-memory copy of one normalized, metadata-stripped PNG crosses to the local Windows OCR capability; exact line text, its word list, and the installed recognizer's language tag return. The service aligns words ordinally to the line and retains exact leading/inter-word/trailing text, so `x=2`, `25 mL`, punctuation, and arrows do not become invented spaces; candidate and verified views use the same projection | A filesystem path, credential, remote request, lane or role decision, approval, or trustworthy word-confidence claim. `Windows.Media.Ocr.OcrWord` exposes no confidence, so every returned word remains uncertain until the teacher acts. Any line/word mismatch fails closed to the manual transcript path | [WindowsOcrServiceTests](../../tests/Integration/WindowsOcrServiceTests.cs); [TranscriptSessionTests](../../tests/Unit/TranscriptSessionTests.cs) |
| Z2 → Z5 | Unavoidable OS artifacts within documented limits. Print view crosses only after approval through an immutable leased job, a hash-verified child copy, and one exact tokenized loopback response write; the parent accepts only child exit 0 within its bound. | An unapproved artifact; a mutable or reused print-view pathname; content in logs. Browser receipt/render, cache erasure, pagefile erasure, and spool erasure are not claimed. | [PrintViewPathTests](../../tests/UiAutomation/PrintViewPathTests.cs); synthetic canary search after success, failure, crash, reboot, print, uninstall |
| Z2 → storage | ProgramData = IT policy (read); LocalAppData = preferences + content-free diagnostics; teacher-selected = Green projects only | Secrets or projects beside the executable; Amber autosave | Storage-location tests; residue suite |

## Standing honesty rule

"The application does not intentionally persist the source capture" is the claim these diagrams support. Approved print views do use one bounded temporary job and a copy-owning loopback helper before immediate cleanup is attempted. Pagefiles, crash dumps, filesystem remanence, browser caches, spoolers, camera drivers, and endpoint tools remain the OS residue surface — bounded and tested where named, never denied (plan §6.4).
