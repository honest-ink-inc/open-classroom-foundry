# Pilot kit — seeded-error review packets

**Regenerate:** `dotnet run --project tools/SampleGenerator -- <repoRoot> <outDir> --seeded <definitions.json>` (deterministic) · **Protocol:** [seeded-error-study.md](../../pilots/seeded-error-study.md)

`packets/` holds eight print-ready task strips, **A through H**. Six contain exactly one planted defect that passed every machine gate — lane rules, validation, approval, rendering — because the defects are semantic, the kind only a practicing teacher can catch. Two are clean controls. The letters are meaningless by design.

**Print all eight at actual size, single-sided. Hand participants the packets and the framing script from the protocol — nothing else.**

## Where the answers live, and why not here

**The definitions and the facilitator key are not in this repository, and must never be.** They were, until 29 August 2026: the packet array sat in `tools/SampleGenerator/Program.cs` under a comment asserting the packet-to-defect mapping "lives only in the facilitator key," and `FACILITATOR-KEY.md` sat in this directory. The comment was false. Semantic defects written in plain language are legible on their face — packet A's unexecutable order and packet C's *once*/"dos veces" mistranslation could be read straight out of the source by anyone. **A blind study cannot define its seeds in a repository meant to be public**, and this one is.

So the packets are now an **input**: the generator reads them from a file you pass it. Keep the real definitions and the key with the facilitator, beside the correspondence ledger and outside this repository. Only [`seeded-packets.example.json`](seeded-packets.example.json) is committed — obviously fictional, defect-free, enough to document the shape and keep the loader under test. `.gitignore` refuses `seeded-packets.json` and any `FACILITATOR-KEY.md`, and a unit test fails if either appears in the tree, so the guard is machinery rather than memory.

The rule the key used to carry still stands wherever it now lives: **facilitators only.** Do not print it, do not open it in view of participants, and do not discuss its contents until both study passes are complete. **A participant who has seen the key is trained, not testing.**

## Two open questions for the facilitator

1. ~~**The committed `packets/` were generated from the old, now-removed definitions.** They are stale by construction. Regenerate from your own definitions before the study runs, and do not assume A–H still mean what any older note says they mean.~~

   *Done 30 Aug 2026: a **second generation** exists.* Eight new packets from all-new scenarios, six seeded and two controls, with the class-to-tier mapping deliberately unchanged so results stay comparable to the first generation's design. Definitions and key were written **outside this repository** and handed to the facilitator; generation was verified byte-identical across two runs, and every packet passed structural validation and the approval gate — which is the study's premise proved rather than asserted, since a structural defect would have been refused. **The committed `packets/` are still the burned first generation and are no longer the study instrument.** Printing them would run the study on trained material: **delete the directory, or replace it with your second-generation output only after the reveal.** With six seeds among eight packets at least four letters must coincide with any previous set — the letters were never the signal, the content is.
2. **These packets were briefly public** (29 Aug 2026, roughly 15:17–23:55 UTC, alongside the key). No forks, stars, or watchers resulted, and no participant had the URL — the letters were unsent and nothing linked to the repository — so the realistic risk is very low. But traffic statistics do not survive a repository transfer, so that window cannot be measured after the fact. The protocol's own remedy is one command: regenerate with new seeds. Prefer it to a judgment call.

After the reveal, packets are burned as study instruments (participants know them) but remain useful as review-surface training materials and as the honest exhibit of what the software cannot do alone.
