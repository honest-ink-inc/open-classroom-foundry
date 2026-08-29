# Gate C, version 1 — design (implements council finding RC-19)

**Date:** 29 August 2026 · Reviewed by the rehearsal safeguarding seat (session 11); a real safeguarding lead reviews before any classroom deployment. Implementation lands with the capture UI, against the acceptance criteria below.

## The one decision that shapes everything

**Version 1 has no automated detection of any kind.** Gate C is *teacher-invoked*: a visible "safety concern" control on the capture and review surfaces. Rationale: a detector that exists gets trusted, and the plan (§5) forbids implying comprehensive detection; an advisory pause the adult chooses is honest, while a detector that misses is a false promise with a child on the other side of it. Automated flagging, if it ever comes, is a separately governed feature with its own review — not an increment.

## Behavior

1. The supervising adult activates the control at any point in a job.
2. The job's state machine transitions to `Blocked`; nothing renders, prints, exports, or transmits from that job afterward.
3. The application privately shows the adult the **district's own procedure text** (from district policy; a neutral built-in fallback tells the adult to follow their school's procedure and consult the appropriate staff — the application never authors safety instructions, per the constitution).
4. The application **stores nothing about the concern** — no flag, no note, no diagnostic event beyond the ordinary content-free `Blocked` state transition; the artifact and source purge exactly as any blocked job does.
5. The application **broadcasts nothing** — no alert, no notification, no network activity. Any future district alert workflow is a separately approved design (plan §5).
6. The pause directs the adult to the **original physical or local source**, not to a screenshot or excerpt.

## Non-goals (v1, restated from the plan)

No detection, no severity triage, no reporting workflow, no record of concerns, no substitute for mandated reporting, suicide response, or threat assessment — which remain distinct human procedures the application must never appear to perform.

## Acceptance criteria for the implementation

- The control is reachable by keyboard from capture and review surfaces, with an accessible name.
- Activation transitions any non-terminal job to `Blocked`; a blocked job can reach only `TransientSourcesPurged`.
- The procedure text renders from district policy when present; the fallback contains no invented safety instructions.
- A canary test proves no trace of the invocation persists: diagnostics show only the state transition; the session store purges; no file changes besides ordinary purge.
- The UI copy claims nothing about detection ("If you saw something concerning, pause here" — not "we detected").
