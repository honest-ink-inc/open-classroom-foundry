# Frozen project compatibility fixture

`0.1.0-dev-schema-1-assets.ocfproj.base64` freezes the historically
representative All Aboard shape: a heading, an `ImageReference`, an
ordered task, the actual `agency.help.v1` SVG, and its complete provenance
record. It decodes to 2,138 bytes with SHA-256
`9B592AA00C2CCB31C5678D82C1685DBE99E023FA482AE25F103AE0D50D9A13FD`.
The asset bytes and provenance are the blobs recorded at commit `1c41984`.
The package is deliberately synthetic: it contains no classroom, learner,
staff, district, or other identifying data. Freezing the bytes rather than
regenerating them under the current writer means the test detects a real
compatibility regression instead of merely agreeing with itself.

The asset-bearing fixture's snapshot is the exact learner HTML emitted by the
renderer at commit `1c41984`, not a hand-written approximation. Managed
preparation first checks those exact legacy bytes, then rewrites the snapshot
through the current canonical renderer alongside the compatibility context.

No historical `.ocfproj` blob was committed with the `0.1.0-dev` build. This
fixture was therefore constructed from that build's recorded schema-1 contract;
it does not claim to be an archived teacher package. Git commit `1c41984`
records `EngineVersion = "0.1.0-dev"` and `ProjectSchemaVersion = "1"`; commit
`4843359` changes only the engine identity here to `0.7.0-alpha`, retaining
schema 1.

`0.7.0-alpha-prior-main-task-strip.ocfproj.base64` is an untouched 3,323-byte
package emitted by `SampleGenerator` from prior main commit `1bded59`, before
the portable-snapshot verifier was added; its relevant renderer, store,
generator, and symbol trees are byte-identical to `32d8407`. Its SHA-256 is
`AE0C140FC4FCB1F9A4DF10FBDC32B2FE09384A5481016016B6223EB6DAAF0D5A`.
It freezes the important compatibility case where the engine identity did not
change while package validation became stricter; the suite proves this real
prior-main output still loads and receives a side-by-side prepared copy.
