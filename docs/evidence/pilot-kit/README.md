# Pilot kit — seeded-error review packets

> **NOT READY FOR GENERATION, PRINTING, OR PARTICIPANT USE.** The command below
> documents deterministic machinery; it is not authorization to create or use a
> live instrument. First require a typist-approved exact corrective proposal and
> valid first-cohort enactment of the same
> participation terms and a separate accepted, declined, or not-applicable
> disposition for every choice applicable to each participant. Keep every
> real definition and key facilitator-held outside this repository.

**Regenerate:** `dotnet run --project tools/SampleGenerator -- <repoRoot> <outDir> --seeded <definitions.json>` (deterministic) · **Protocol:** [seeded-error-study.md](../../pilots/seeded-error-study.md)

**This directory holds no packets, and that is deliberate.** A study kit is eight print-ready task strips, **A through H** — six carrying exactly one planted defect that passed every machine gate (lane rules, validation, approval, rendering) because the defects are semantic, the kind only a practicing teacher can catch, plus two clean controls. The letters are meaningless by design. But **printable packets are output, not source**: they are one command away from the facilitator's own definitions, and committing them would let any participant study the instrument before sitting the study. The first generation was committed here and was deleted on 30 Aug 2026 once it had been burned twice over — by its definitions having lived in the generator's source, and by the repository's public window of 29 August. It remains in already-public history; the current tree deliberately provides no retrieval pointer.

**Only after the typist-approved exact corrective proposal and the same validly enacted first-cohort participation terms exist, the participant separately records an accepted, declined, or not-applicable disposition for every applicable choice, the affirmative participation and collection prerequisites are met, and the protocol's owners authorize that session:** generate and print all eight at actual size, single-sided; hand that participant the packets and the framing script from the protocol — nothing else. Declining an optional recording, credit, compensation, contribution, or role choice does not bar participation. The packets exist only where an authorized facilitator generates them, which is also where they must stay.

## Where the answers live, and why not here

**The definitions and the facilitator key are not in this repository, and must never be.** Earlier repository versions embedded protected study design, and later tracked wording retained descriptive fragments of it; those current-tree fragments have now been removed. **A blind study cannot define its seeds in a repository meant to be public**, and this one is.

So the packets are now an **input**: the generator reads them from a file you pass it. Keep the real definitions and key facilitator-held outside this repository, and do not record their storage location here. Only [`seeded-packets.example.json`](seeded-packets.example.json) is committed — obviously fictional, defect-free, enough to document the shape and keep the loader under test. `.gitignore` refuses `seeded-packets.json` and any `FACILITATOR-KEY.md`, and a unit test fails if either appears in the tree, so the guard is machinery rather than memory.

The rule the key used to carry still stands wherever it now lives: **facilitators only.** Do not print it, do not open it in view of participants, and do not discuss its contents until both study passes are complete. **A participant who has seen the key is trained, not testing.**

## Two questions for the facilitator, both now answered

1. ~~**The committed `packets/` were generated from the old, now-removed definitions.** They are stale by construction. Regenerate from your own definitions before the study runs, and do not assume A–H still mean what any older note says they mean.~~

   *Done 30 Aug 2026: a **second generation** exists.* Eight new packets from all-new scenarios, six seeded and two controls, with the class-to-tier mapping deliberately unchanged so results stay comparable to the first generation's design. Definitions and key were written **outside this repository** and handed to the facilitator; generation was verified byte-identical across two runs, and every packet passed structural validation and the approval gate — which is the study's premise proved rather than asserted, since a structural defect would have been refused. The burned first generation was **deleted from this directory the same day**, so no one can print trained material by reaching for the obvious folder. With six seeds among eight packets at least four letters must coincide with any previous set — the letters were never the signal, the content is.
2. **These packets were briefly public** (29 Aug 2026, roughly 15:17–23:55 UTC, alongside the key). No forks, stars, or watchers resulted, and no participant had the URL — the letters were unsent and nothing linked to the repository — so the realistic risk is very low. But traffic statistics do not survive a repository transfer, so that window cannot be measured after the fact. The protocol's own remedy is one command: regenerate with new seeds. Prefer it to a judgment call.

After the reveal, packets are burned as study instruments — participants know them — but may remain useful as review-surface training material and as an honest exhibit of what the software cannot do alone. Completion of the passes does **not** itself authorize publication. Publication remains held until the project chooses the applicable content license, each author separately assents to the exact contribution terms, affected participants review the proposed record, the required council and protected-seat records are frozen, and the typist explicitly performs the pre-publication check and publication act.
