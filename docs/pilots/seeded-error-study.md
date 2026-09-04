# Seeded-error study — protocol

> **NOT READY FOR PARTICIPANT USE.** The typist has not issued and approved an exact
> corrective proposal, and the initial cohort has not validly enacted the same
> consent, withdrawal-right, credit, compensation, contribution, and private-record terms. Do not
> schedule either pass, hand out packets, collect responses, or file a public
> record until those terms are validly enacted, each participant's applicable choices
> are recorded, the withdrawal right and route are separately acknowledged, and
> the compensation policy's counsel/accountant, authorized-route, private-ledger,
> exact-rate, and quarterly-cap prerequisites are satisfied.
> This protocol and the private seed materials supply no consent.

**Constitutional basis:** plan §14 risk table, automation bias row — *"Teachers must detect defined seeded errors before pilot."* This study gates the classroom-output rung of Gate 4, and its logic is the project's whole argument made measurable: every machine gate passed these artifacts; only a teacher can catch what's wrong with them. If teachers don't, the review surface has failed — not the teachers.

## Materials

- The [pilot kit](../evidence/pilot-kit/): eight printable review packets, A–H, generated deterministically by `SampleGenerator --seeded <definitions.json>`. Six carry exactly one planted defect each; two are clean controls. Letters carry no information.
- The packet definitions and the facilitator key: **kept with the facilitator, outside this repository** (29 Aug 2026 — a blind study cannot define its seeds in a repository meant to be public; the reasoning is in the [pilot kit README](../evidence/pilot-kit/README.md)). **Facilitator's eyes only, never printed for participants, never discussed until both passes are done.** Only an obviously-fictional example is committed, and a unit test fails if real definitions or a key ever land in the tree.

## Design

This H7 activity begins only after H0–H6 records are frozen and every
predecessor hold applicable to the exact H7 build, packets, and instrument is
explicitly closed by its accountable authority. Frozen `HOLD` evidence never
authorizes participant use. Two passes, ≥3 educator participants, at least a
week apart:

1. **Paper pass (H7, first pass):** each participant receives the eight printed packets with the framing below, works alone, 20 minutes.
2. **In-app pass (H7, at least one week later):** the same artifacts encountered as drafts in the Gate B review surface — the setting where catching them actually matters. Different packet order.

**Framing read to participants (verbatim — the deception is disclosed as part of the design):**

> Some of these eight are fine and some contain one planted error each — wrong order, wrong words, wrong picture, something missing, something self-contradictory. We planted them; the software could not catch them, by design. For each packet, mark it "I'd hand this out" or "something's wrong," and if wrong, circle where. There's no trick beyond what I just said. You may stop at any time. The lawful-payment route was reverified before this session. If you separately elected compensation, the full rate disclosed to you fits your remaining quarterly cap and attaches once this scheduled session begins; stopping or withdrawing does not forfeit it. If you declined compensation, the amount is zero and will not become retroactive.

## Scoring

Per participant, per packet: hit (defect found and located), miss (seeded packet passed), false alarm (control flagged). The facilitator keeps that keyed detection matrix with the key, outside the repository. Publish only aggregate, non-keyed measures against these thresholds:

| Threshold | Bar | Rationale |
|---|---|---|
| Detection rate, easy+medium seeds | **100% across the group** (every seed caught by at least one reviewer; each participant ≥ 4 of 6) | The plan's word is "must detect"; a group that misses an easy seed entirely fails the gate |
| Detection, hard seed | Reported, no bar | Measures the ceiling, not the gate |
| False-alarm rate on controls | Reported | High false alarms = the review surface breeds distrust; that's a design defect too |
| Paper vs. in-app delta | Reported | If in-app detection is *worse* than paper, the review surface is actively harmful and blocks release regardless of totals |

## After both passes

Reveal the key privately and keep the packet-keyed matrix and analysis with the
facilitator. A group debrief occurs only among participants whose separate
within-cohort identity/affiliation disclosure choices permit the exact named
recipients and after every recipient has acknowledged the session-
confidentiality and no-outside-contact boundary. Otherwise use the accepted
seat-only, pseudonymous, or one-to-one format; public-credit consent is not a
substitute.

Prepare a de-identified component aggregate containing only non-keyed measures,
the threshold verdict, and sanitized surface-defect facts that do not identify
packet letters, disclose seed definitions, or reconstruct the control key. Then
use this exact-byte flow before `docs/evidence/pilot/seeded-error-verdict.md` can
become public evidence:

1. through the private custodian, give every contributor whose material remains
   in the proposed record the exact proposed public bytes for review;
2. honor each correction, removal, or withdrawal request, regenerate the exact
   bytes, and repeat review whenever any byte changes;
3. only after exact-byte review is complete, separately re-confirm that each
   applicable public-record publication choice permits those exact bytes; and
4. freeze the resulting versioned component record and its detached manifest
   under the bounded-commission ledger, then bind both digests into the H7
   aggregate root. Neither frozen file is edited afterward.

A refusal or unresolved withdrawal leaves the proposed public-evidence row open
and cannot be converted into consent. A later correction or withdrawal is a new
append-only linked event and must be resolved by a fresh pre-use chain audit.
This protocol does not publish the record.

If the gate fails: fix the surface, regenerate packets with **new** seeds
(participants are now trained on these), and run again. The study is repeatable
by construction; the private artifacts are one `--seeded` run away.

## Honesty notes

- Participants who took part may not be the sole reviewers of the fix — trained eyes overestimate the surface.
- The generated packets are burned for blind-study purposes once revealed. They may remain private training material afterward, but neither packets, definitions, the keyed matrix, nor the facilitator key may enter this repository.
- Nothing in this study touches Amber: all packets are Green, synthetic, teacher-authored fixtures.
