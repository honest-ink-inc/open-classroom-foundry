# Seeded-error study — protocol

**Constitutional basis:** plan §14 risk table, automation bias row — *"Teachers must detect defined seeded errors before pilot."* This study gates the classroom-output rung of Gate 4, and its logic is the project's whole argument made measurable: every machine gate passed these artifacts; only a teacher can catch what's wrong with them. If teachers don't, the review surface has failed — not the teachers.

## Materials

- The [pilot kit](../evidence/pilot-kit/): eight printable review packets, A–H, generated deterministically by `SampleGenerator --seeded <definitions.json>`. Six carry exactly one planted defect each; two are clean controls. Letters carry no information.
- The packet definitions and the facilitator key: **kept with the facilitator, outside this repository** (29 Aug 2026 — a blind study cannot define its seeds in a repository meant to be public; the reasoning is in the [pilot kit README](../evidence/pilot-kit/README.md)). **Facilitator's eyes only, never printed for participants, never discussed until both passes are done.** Only an obviously-fictional example is committed, and a unit test fails if real definitions or a key ever land in the tree.

## Design

Two passes, ≥3 educator participants, at least a week apart:

1. **Paper pass (week 4):** each participant receives the eight printed packets with the framing below, works alone, 20 minutes.
2. **In-app pass (week 5):** the same artifacts encountered as drafts in the Gate B review surface — the setting where catching them actually matters. Different packet order.

**Framing read to participants (verbatim — the deception is disclosed as part of the design):**

> Some of these eight are fine and some contain one planted error each — wrong order, wrong words, wrong picture, something missing, something self-contradictory. We planted them; the software could not catch them, by design. For each packet, mark it "I'd hand this out" or "something's wrong," and if wrong, circle where. There's no trick beyond what I just said.

## Scoring

Per participant, per packet: hit (defect found and located), miss (seeded packet passed), false alarm (control flagged). The facilitator keeps that keyed detection matrix with the key, outside the repository. Publish only aggregate, non-keyed measures against these thresholds:

| Threshold | Bar | Rationale |
|---|---|---|
| Detection rate, easy+medium seeds | **100% across the group** (every seed caught by at least one reviewer; each participant ≥ 4 of 6) | The plan's word is "must detect"; a group that misses an easy seed entirely fails the gate |
| Detection, hard seed | Reported, no bar | Measures the ceiling, not the gate |
| False-alarm rate on controls | Reported | High false alarms = the review surface breeds distrust; that's a design defect too |
| Paper vs. in-app delta | Reported | If in-app detection is *worse* than paper, the review surface is actively harmful and blocks release regardless of totals |

## After both passes

Reveal the key privately, debrief as a group (this is the most instructive hour of the pilot), and keep the packet-keyed matrix and analysis with the facilitator. File only sanitized surface-defect issues that do not identify packet letters, disclose seed definitions, or reconstruct the control key. Record aggregate non-keyed measures and the threshold verdict in `docs/evidence/pilot/seeded-error-verdict.md`. If the gate fails: fix the surface, regenerate packets with **new** seeds (participants are now trained on these), and run again. The study is repeatable by construction; the private artifacts are one `--seeded` run away.

## Honesty notes

- Participants who took part may not be the sole reviewers of the fix — trained eyes overestimate the surface.
- The generated packets are burned for blind-study purposes once revealed. They may remain private training material afterward, but neither packets, definitions, the keyed matrix, nor the facilitator key may enter this repository.
- Nothing in this study touches Amber: all packets are Green, synthetic, teacher-authored fixtures.
