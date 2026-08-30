# Reviewed UI catalogs

Honest Ink has production catalog plumbing, but it does **not** yet have a second UI language. The multilingual educator or family-liaison seat owns the translation, terminology, mnemonic choices, reading direction, and review decision. A developer can export and mechanically validate the packet; a keyboard cannot grant that review.

The mechanical loader validates a review **assertion** and binds it to the exact neutral source digest. It cannot authenticate the human identity typed into JSON. Production activation therefore adds a separate compiled exact-file SHA-256 allowlist: `--ui-catalog` can select only a catalog already pinned into that build after the protected-seat and provenance review. A path, environment variable, or typed `"status": "reviewed"` can never add that pin. The current allowlist is deliberately empty because no such seat-supplied catalog or approved deployment artifact exists, so the real-language part of the directive remains human-gated and fail-closed.

The catalog covers application chrome only: every static `UiStrings` entry, every dynamic Press Room title/field/choice label, every built-in module door/mode/field/table/choice label, and the All Aboard card labels. Artifact content, validation messages produced by the domain, teacher-entered text, symbol terminology, and the public name **Honest Ink** are outside this catalog. Submitted choice values are also outside it: a translated label never changes the stable value delivered to a builder.

The neutral inventory measured on 30 August 2026 contains **978 ids**. Two independent exports were byte-identical: packet SHA-256 `752F0D1654A1C379D6DB52D8C5A40B9AAEDBF9BF33534FF625DD8FB2CDD84B51`, bound to source digest `35186157cfc0c64e94c912539fa0cc9c7370c83f5c335718ce23faf7a31b385c`. Re-export after any catalog-affecting source change; these values describe this repository state, not a permanent allowlist entry.

## 1. Export the neutral review packet

From the repository root:

```powershell
pwsh tools/export-ui-catalog-template.ps1 -OutputPath C:\safe-review-location\ui-catalog.json
```

The output is deterministic for one source tree. Its portable shape is recorded in [ui-catalog.schema.json](ui-catalog.schema.json); runtime validation is stricter because it also requires the exact generated id set, neutral sources, digest, placeholders, and mnemonics. It contains:

- `schemaVersion`: currently `1`;
- `languageTag`: replace `und` with one canonical .NET/BCP-47 tag such as `es-MX`;
- `direction`: exactly `ltr` or `rtl`, chosen by the seat rather than guessed from the tag;
- `review`: a deliberately non-activatable `draft` record and the SHA-256 digest of the neutral source inventory;
- `provenance`: the catalog id, translation creator/source/license, and modification history required by the repository's asset rule;
- `neutralStrings`: immutable source text for review; and
- `strings`: the working translation table, initially copied from the neutral source.

Keep `neutralStrings`, every string id, and `sourceDigestSha256` unchanged. String ids are contracts, not English text. Re-export when the digest changes; do not transplant an old review stamp onto a new packet.

## 2. Translate and review

The seat edits values in `strings` only, then reviews the whole packet in the intended UI. Placeholders such as `{0}` and the count of keyboard mnemonic markers such as `&P` must match the corresponding neutral source. In chrome strings, each single `&` must introduce a Unicode letter or digit; use `&&` for a literal ampersand. A dangling marker, whitespace, or punctuation after a single `&` is rejected. Dynamic press, module, list, and artifact text treats `&` literally because those values are not WinForms mnemonic-bearing chrome. A translation may reorder placeholders and choose a different mnemonic letter. It may not remove or add either contract.

Fill every provenance field and add at least one truthful modification-history entry. After the actual seat review, record:

```json
{
  "status": "reviewed",
  "reviewerName": "the named reviewer",
  "reviewerRole": "multilingual-educator-or-family-liaison",
  "reviewedAtUtc": "2026-08-30T12:00:00Z"
}
```

That example describes the shape only; it is not a review record and must not be copied as one. Use the actual reviewer and instant. Catalogs remain outside the repository until their text, rights/provenance, and distribution path are separately approved under the project rules.

## 3. Admit, then select explicitly

After the actual protected-seat review, an authorized source change must pin the SHA-256 of the **exact completed catalog bytes** in the build's `UiCatalogDeployment` allowlist. Review and pinning are separate evidence: JSON records what was asserted; the source-reviewed pin records which immutable artifact this build admits. Any byte change requires a new review and a new pin. The current build pins none.

Once a build contains that pin, select the already-admitted artifact with either the command line:

```powershell
Foundry.App.WinForms.exe --ui-catalog C:\approved-location\ui-catalog.json
```

or set `OCF_UI_CATALOG` to the approved file path. A command-line path takes precedence over the environment path. The pseudo-locale and a reviewed catalog are mutually exclusive. Neither selector can approve a new file: an unpinned catalog is refused before its self-declared review fields are used.

Admission tooling refuses if JSON is non-strict or contains malformed UTF-8/Unicode, the schema or property set differs, the language tag/direction/review assertion is invalid, the source digest or neutral text differs, an id is missing or unknown, a translation is blank, or placeholder/mnemonic parity fails. Production startup additionally refuses any exact file hash absent from its compiled allowlist. Draft catalogs never activate. An unknown id encountered after activation falls back to its exact neutral source; known inventory ids cannot be omitted from the reviewed file. Mechanical checks and a byte pin still do not manufacture human review; the protected seat supplies that evidence before the pin is committed.

`--pseudo-locale` and `OCF_PSEUDO_LOCALE=1` remain the layout stress harness. They are not a language and confer no review.
