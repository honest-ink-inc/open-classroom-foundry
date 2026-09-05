# Reviewed UI catalogs

Honest Ink has production catalog plumbing, but it does **not** yet have a second UI language. The multilingual educator or family-liaison seat owns the translation, terminology, mnemonic choices, reading direction, and review decision. A developer can export and mechanically validate the packet; a keyboard cannot grant that review.

The mechanical loader validates a review **assertion** and binds it to the exact neutral source digest. It cannot authenticate the human identity typed into JSON. Production activation therefore adds a separate compiled exact-file SHA-256 allowlist: `--ui-catalog` can select only a catalog already pinned into that build after the protected-seat and provenance review. A path, environment variable, or typed `"status": "reviewed"` can never add that pin. The current allowlist is deliberately empty because no such seat-supplied catalog or approved deployment artifact exists, so the real-language part of the directive remains human-gated and fail-closed.

The catalog covers application chrome only: every static `UiStrings` entry, every dynamic Press Room title/field/choice label, every built-in module door/mode/field/table/choice label, and the SequenceSlate card labels. Artifact content, validation messages produced by the domain, teacher-entered text, symbol terminology, and the public name **Honest Ink** are outside this catalog. Submitted choice values are also outside it: a translated label never changes the stable value delivered to a builder. ADR-008 changes public display strings and load-bearing subtitles only; localization keys, submitted values, recipe IDs, schema IDs, and saved-project bindings deliberately retain their legacy tokens.

**Candidate measurement, 5 September 2026:** after adding the two path-free library-save recovery statuses, the neutral inventory contains **1,080 ids**. Two independent exports were byte-identical at **165,577 bytes**: packet SHA-256 `62EFC47E009FC6E581C09C0FB92194E35561636EC1A3847D74B8734BAA6E4D84`, bound to source digest `42a156dd667d317806f4b66a379391794a7b47b4af2d1582cdeb20a5866e5b8c`. The related save/library/catalog/pseudo-locale UI contracts passed **153/153**, separately from **21/21** sink/localization Unit contracts. This is local candidate evidence, not an exact hosted or release claim; the [implementation evidence record](../governance/accepted-improvement-evidence.md) retains its scope. The packet remains `draft` / `und` and the production allowlist remains empty. No second-language or protected-seat review is supplied.

*Historical measurement, before that recovery change:* the **1,078-id**, **164,925-byte** packet had SHA-256 `ADC01D0323AD6BBF766B1CC305A409165AD3D82118FF642A9B6576FE8CFF20C1` and source digest `13cab42111782b29bc1d048b75d66a45a5a195210fe284072d3323b2d63d0e3e`; its focused catalog contract passed **28/28**. Those values are retained as the earlier neutral packet, not relabeled as current bytes or human review.

The pseudo-locale expansion contract is inventory-wide, not a four-string sample. A 31 August 2026 audit projected all **1,078** neutral entries through the runtime and found that the earlier letter-count padding left **178** complete strings below the claimed 1.40× length. Runtime padding now retains that letter stress while also meeting the complete-string threshold, and a reflection guard requires all nine shipped forms to have a deliberate pseudo surface scenario. Board-to-Brief and its grid role choices are included. The repeated accented `ẋ` at the end of a bracketed string is therefore expected diagnostic padding while pseudo mode is deliberately active; it is not an AAC symbol or translated product text.

The reviewed-catalog path also has a full-surface mechanical projection. Complete synthetic catalogs with an exact test-only hash pin append a visible fixture marker, then traverse every shipped form, review tab, press, SequenceSlate mode, Built-in Studios door and mode, and node-editor variant in both LTR and RTL. The fixture identifies itself as **not protected-seat evidence**, never changes the production allowlist, and cannot establish translation quality. A separate structural guard binds the checked-in JSON schema, generated packet, runtime constants, exact neutral tables, and a successful strict-loader result so those three representations cannot drift silently.

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

The seat edits values in `strings` only, then reviews the whole packet in the intended UI. Placeholders such as `{0}` and the count of keyboard mnemonic markers such as `&P` must match the corresponding neutral source. In chrome strings, each single `&` must introduce a Unicode letter or digit; use `&&` for a literal ampersand. A dangling marker, whitespace, punctuation, or non-BMP surrogate pair after a single `&` is rejected. Every translated access key must also be unique among controls that can be simultaneously mnemonic-active on its actual surface under WinForms' native UTF-16 and current-culture matching semantics. Reuse across separate forms and mutually exclusive node-editor variants remains valid. The engine passes that marker syntax unchanged only to mnemonic-capable chrome; for a literal sink it decodes the static template before inserting any raw dynamic arguments, so a translated `R&&D: {0}` and an artifact value containing `&` cannot reinterpret one another. Dynamic press, module, list, status, and artifact text treats `&` literally because those values are not WinForms mnemonic-bearing chrome. Mnemonic-capable controls carrying those values explicitly opt out; dynamic GroupBox captions escape the native prefix while preserving the exact accessible name. A translation may reorder placeholders and choose a different mnemonic letter. It may not remove or add either contract, and the multilingual seat still chooses the translated keys.

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

Admission tooling refuses if JSON is non-strict or contains malformed UTF-8/Unicode, the schema or property set differs, the language tag/direction/review assertion is invalid, the source digest or neutral text differs, an id is missing or unknown, a translation is blank, placeholder/mnemonic parity fails, or a translated access key is duplicated within one simultaneously mnemonic-active surface context. Production startup additionally refuses any exact file hash absent from its compiled allowlist. Draft catalogs never activate. An unknown id encountered after activation falls back to its exact neutral source; known inventory ids cannot be omitted from the reviewed file. Mechanical checks and a byte pin still do not manufacture human review; the protected seat supplies that evidence before the pin is committed.

`--pseudo-locale` and `OCF_PSEUDO_LOCALE=1` remain the layout stress harness. They are not a language and confer no review. Expected pseudo output is accented, bracketed, mirrored text ending in repeated `ẋ` (U+1E8B) padding; the padding forces at least 40 percent expansion so clipping is visible. Headed local tests briefly show real windows in that state. OS language or culture cannot activate it. If the marker appears in an ordinary launch outside testing, inspect the exact process command line and inherited `OCF_PSEUDO_LOCALE` value as a configuration defect.
