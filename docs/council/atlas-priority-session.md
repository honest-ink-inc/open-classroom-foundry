# Atlas 2.0 council priority session

**Status:** READY TEMPLATE — **UNRUN** as of 30 August 2026. This file is not a council finding, a roadmap decision, an ADR, or permission to build. When a real session is scheduled, copy it to `atlas-priority-session-YYYY-MM-DD.md`. Keep this template unchanged. The dated copy moves through `UNRUN` → `SESSION HELD — REVIEW PENDING` → `COUNCIL RECORD FROZEN` only when those events actually occur; feasibility and product-owner disposition follow the frozen council record.

The [idea atlas](../idea-atlas.md) is a register of possibilities, not a queue. This session lets practicing educators state the work that matters before anyone maps that work to an atlas entry or a newly possible composition. The product owner still owns module priorities. Every specialist keeps their own gate, and no absent or longer-delayed seat is treated as having agreed.

## Non-negotiable boundaries

- Use synthetic or staged examples only. Record no student work, identifying classroom detail, credentials, or blind-study material.
- Record participants by seat and count in the public copy. Names and scheduling remain in the typist's private ledger unless a participant has explicitly chosen public credit.
- Confirm withdrawal, credit, notes-only-by-default, and the [ratified compensation policy](compensation-policy.md) before starting. Compensation attaches to the session, never its conclusion.
- After the needs-first round, rehearsal findings may inform follow-up questions, but rehearsal personas are not participants, votes, specialist evidence, or ratification.
- Capture classroom needs before showing candidate names. Atlas numbering, previous forge menus, implementation ease, and the facilitator's preferences must not seed the first round.
- A member may speak from their own practice, not for an unfilled seat. Silence or absence means **not reviewed**, never assent.
- A candidate touching AAC, accessibility/AT, multilingual/family communication, district, privacy/legal/records, safeguarding, curriculum, or rights territory stays held for every applicable seat that was absent. One role cannot waive another role's critical gate.
- A session recommendation authorizes nothing. Engineering begins only after a separate feasibility record and a written product-owner disposition. Any architectural change still follows the ADR process.
- The first cohort must enact a decision procedure and quorum rule before an ordering is recorded as a council recommendation. If those terms are not yet in force, the session may capture and map needs, but its output remains an unranked consultation record.

## Prepare without pre-ranking

The facilitator prepares these items before the session:

1. An exact repository snapshot: commit, build identifier, date, and a factual list of what teachers can reach in the running application.
2. A one-page statement of current debts, unavailable or gated modules, and known evidence gaps. Describe sightings as sightings unless diagnosed.
3. Blank copies of the need card below. Do not distribute a shortlist.
4. The complete atlas, made available only after the needs-first round.
5. Samples held back until after need capture, then shown only when they help a member judge a named need; samples are not evidence that the need is already solved.

### Session header

| Field | Record |
|---|---|
| Session date and duration | `[not run]` |
| Repository commit/build inspected | `[not run]` |
| Facilitator (non-voting) | `[not run]` |
| Product owner present? | `[not run]` |
| Seats present (seat + count, no names by default) | `[not run]` |
| Seats absent | `[not run]` |
| Materials actually inspected | `[not run]` |
| Withdrawal right confirmed | `[not run]` |
| Compensation terms confirmed | `[not run]` |
| Note-taking choice confirmed | `[not run]` |
| Public-credit choice confirmed | `[not run]` |
| Decision procedure and quorum rule applied (exact governing record) | `[not enacted / not run]` |

## Sixty-minute needs-first agenda

1. **Boundaries and read-back — 5 minutes.** Read the non-negotiable boundaries aloud. Confirm that the software, not the educator, is under review.
2. **Silent need capture — 10 minutes.** Each member completes up to three need cards without seeing candidate names. One card describes one recurring production burden or missing support.
3. **Clarify, do not solve — 10 minutes.** Members ask only enough to understand frequency, current workaround, classroom consequence, and what a useful artifact would let the teacher or learner do.
4. **Cluster needs — 8 minutes.** Group duplicates while preserving materially different contexts and dissent. Keep the original cards.
5. **Open the atlas — 10 minutes.** Map each surviving need to an existing entry, an already-built capability, or a genuinely new composition. “No match” is valid and must remain visible.
6. **Test value and boundaries — 10 minutes.** For each leading need, record the first useful proof, unacceptable failure, data lane, and possibly implicated seats. An absent applicable seat creates a hold.
7. **Recommend and read back — 7 minutes.** Record an ordered recommendation only if the present members actually make one. Read the wording, dissent, uncertainty, and holds back to them before closing.

### Need card — complete before opening the atlas

| Prompt | Council member's words |
|---|---|
| Need ID | `N-__` |
| Recurring teacher work or learner-facing barrier | |
| Who encounters it (generic role/context only) | |
| How often it occurs | |
| Current workaround and its time/material cost | |
| What a useful paper/offline artifact would make possible | |
| What must remain under teacher control | |
| Unacceptable failure or harm | |
| First classroom proof that would earn trust | |
| Seat speaking | |

Do not force a number where the member has none. Mark estimates as estimates and preserve “unknown.”

### Need-to-possibility mapping — complete only after need capture

| Need ID | Atlas entry / existing capability / new composition / no match | Why it fits or fails to fit | Likely lane (`G`, `A`, `R`, uncertain) | Possibly implicated seats |
|---|---|---|---|---|
| | | | | |

Mapping is not selection. A Green label does not erase an `H` specialist marker, release evidence, accessibility work, rights review, or the [implementation plan's](../implementation-plan.md) stop-ship conditions.

## Council recommendation record

Record the council's language, not a facilitator-generated score. A split recommendation is more honest than manufactured consensus.

| Order, if any | Need ID and mapped possibility | Why now, in council members' words | First proof requested | Holds / seats still needed | Dissent or alternative |
|---|---|---|---|---|---|
| | | | | | |

Also record:

- **Needs deliberately not advanced, and why:**
- **Useful possibilities with no atlas match:**
- **Questions the session could not answer:**
- **Corrections members made during read-back:**
- **Whether members reached consensus, split, or made no ordering:**

“Consensus” means only that the members present stated no unresolved objection to this recommendation. It never implies consent from an absent seat or satisfaction of a later gate.

## Participant review and council-record freeze

Do not begin feasibility while any field below is incomplete. A session having occurred is not evidence that its record was reviewed or frozen.

| Field | Record |
|---|---|
| Session occurred; dated copy status changed from `UNRUN` | `[not run]` |
| Participant read-back/review completed (seat + count, no names by default) | `[not run]` |
| Corrections and dissent incorporated without facilitator rewriting | `[not run]` |
| Applicable absent-seat holds rechecked and retained | `[not run]` |
| Council record frozen (date, repository path, commit, and record version) | `[not run]` |

## Separate feasibility appendix — completed after the council record is frozen

The maintainer may add facts without rewriting the council's record:

| Recommended possibility | Reusable engine/capability | Smallest bounded slice | Dependencies and migrations | Required automated and human evidence | Effort/risk range | Conflicts with ADR, plan, or gate |
|---|---|---|---|---|---|---|
| | | | | | | |

Rules for the appendix:

- Preserve the council's requested outcome even when the proposed implementation changes.
- Do not turn ease of implementation into a retroactive council priority.
- Mark uncertainty. Do not convert rehearsal findings or model judgment into human evidence.
- If a candidate enters Amber, Restricted, or a protected seat's territory, record the stop; do not design around it.

## Product-owner disposition — intentionally blank in the template

For each recommendation, the product owner records **adopt for a proposed forge menu**, **defer**, or **decline**, with a reason and the exact scope. Adoption does not ratify an ADR and does not waive any specialist or district gate.

| Recommendation | Disposition and date | Exact bounded scope | Reason | Outstanding seats/gates | Evidence required before completion |
|---|---|---|---|---|---|
| `[not run]` | `[not decided]` | | | | |

## Completion check

A real session record is complete only when it contains the session header, original need cards, mapping, recommendation (including dissent), read-back corrections, seat holds, participant review and freeze evidence, the separate feasibility appendix, and the product-owner disposition. Until then, the honest Atlas 2.0 status is:

> **No next atlas priority has been selected. Awaiting real council input and the product owner's recorded disposition.**
