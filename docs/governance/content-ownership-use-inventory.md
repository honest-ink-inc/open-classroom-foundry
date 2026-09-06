# Content ownership and intended-use inventory

**Prepared:** 5 September 2026 for proposal I01.

**Status at initial preparation: PREPARATORY INVENTORY — NO CONTENT LICENSE SELECTED, NO CONTRIBUTOR
ASSENT OBTAINED, NO RIGHTS OR PROTECTED-SEAT APPROVAL.** This is owner-directed
factual repository maintenance under the existing documentation default, not an
outbound license, a copyright assignment, an enacted contribution agreement, or
a release decision.

**Later 5 September 2026:** the typist expressly selected **CC BY-SA 4.0 as the
proposal to carry forward**, with the [exact question, answer and reason
recorded separately](2026-09-05-content-license-selection.md). This resolves
the proposal choice, not an operative grant. Earlier unchosen/UNANSWERED
statements below retain their original cutoff except where expressly updated;
material coverage, licensing authority, rights review, notices, temporal scope
and independent assent remain unresolved. No class is relicensed here.

The [contribution rules](../../CONTRIBUTING.md),
[current licensing boundary](../../README.md#licensing),
[notices](../../NOTICE.md), and [implementation plan §9](../implementation-plan.md#9-licensing-and-commons-contract)
control. This inventory prepares the class-by-class decision requested by
[I01](../reviews/2026-09-05-synthetic-council/teacher-practice-and-improvements.md#i01--choose-an-exact-content-license-and-contribution-route);
it does not resolve that decision by accepting its own proposal.

## How to read the inventory

A repository path locates evidence; it does not identify a copyright owner or
prove permission. A source declaration, a recorded license, an integrity hash,
a teacher's statement, an authorized rights finding, and a permission for one
particular use are different things. No ownership is inferred from a directory,
file extension, generated output, contributor label, or the proposed entity's
name. Unknown ownership or scope stays unknown.

These are class-level observations of the named source files, not an exhaustive
file-by-file clearance or an immutable distribution manifest. Before a new
license applies, its accountable decision must enumerate the exact covered
materials and the evidence of authority to license them. Existing third-party
terms and the existing symbol dedication are preserved; nothing is silently
relicensed. “Available in source” below is not permission to deploy, publish a
site, distribute a release, or use participant instruments.

| Class | Observed material and source authority | Current use and unresolved boundary |
|---|---|---|
| C01 — Application code and engineering tooling | [NOTICE application-code declaration](../../NOTICE.md#application-code), [COPYING](../../COPYING), and [source layout](../../src/README.md); source files such as [Assets.cs](../../src/Foundry.Contracts/Assets.cs) carry `GPL-3.0-or-later` notices. NOTICE records the Writer's Kiosk ancestry separately. | Code maintenance and contributions follow the existing GPL route. The declaration is not a finding that the proposed foundation owns every contributor's copyright. Preserve authorship, dependency notices, and corresponding-source obligations; a new content license does not replace the code license. |
| C02 — First-party recipe implementation and declarations | NOTICE and CONTRIBUTING include first-party recipes in the GPL class; examples are [Scaffold Smith](../../src/Foundry.Modules.BuiltIn/ScaffoldSmith.cs) and [Inquirywright's legacy SourceLens implementation](../../src/Foundry.Modules.BuiltIn/SourceLens.cs). The [recipe identity disposition](../adr/recipe-identity-disposition-packet.md) separately freezes admitted contracts. | Existing code/recipe contribution terms apply. A recipe path or the fact that a program emitted a page does not resolve the rights in every source quotation, teacher-entered value, printable passage, or embedded asset on that page. License choice does not authorize in-place contract changes. |
| C03 — Test code and synthetic fixtures | [Test-suite boundary](../../tests/README.md), [staged-task fixture source](../../tests/InstructionalEvals/StagedTaskFixtureTests.cs), and [frozen compatibility fixture provenance](../../tests/Integration/Fixtures/upgrade/README.md). The compatibility README distinguishes a constructed synthetic historical-shape fixture from an untouched prior-main generator output. | Test code is in the declared GPL class. Fixture admission still requires the current CONTRIBUTING route and marked synthetic/public-domain/compatible-OER provenance. A fixture's test location does not authorize separately authored replacement content or prove teaching, language, or rights review. Frozen evidence is not regenerated to erase a discrepancy. |
| C04 — Factual governance, status, and maintenance prose | The narrow first-party maintenance exception is explicit in [CONTRIBUTING](../../CONTRIBUTING.md). Examples include this inventory, current-state corrections, and evidence indexes. | An explicit bounded owner instruction permits that factual maintenance under the current all-rights-reserved documentation default. It neither opens documentation generally nor transfers participant-authored expression. Changes to governing law still follow [GOVERNANCE](../../GOVERNANCE.md#changing-this-document). |
| C05 — Expressive documentation and original printable content | [README licensing](../../README.md#licensing), plan §9, and the [historical four-HTML-sample index](../evidence/0.1-alpha/README.md). Printable furniture is separately identified in the [artifact-language contract](../localization/artifact-language-contract.md). | The repository's stated default is all rights reserved outside the separately licensed classes; no free-culture content license has been chosen. Do not infer an output's license from its renderer's GPL notice. Existing availability and synthetic provenance do not prove ownership of every expressive element, semantic approval, or a new distribution permission. Exact new-material scope and any proposed treatment of existing material need separate decisions. |
| C06 — Existing original symbol artwork and metadata | [The symbol manifest](../../assets/symbols/manifest.json) contains 13 records for 13 SVG files. Every record declares `source: original`, creator `Open Classroom Foundry contributors`, license `CC0-1.0`, a content hash, meaning, alt text, redistribution flag, and ambiguity note. [NOTICE](../../NOTICE.md#bundled-assets) records the dedication. | Preserve the existing CC0 declaration; this audit does not independently establish the contributor ownership chain or supply AAC/SLP approval. All 13 records currently omit `requiredAttribution` and `modifications`. The open-pack route requires explicit dispositions rather than interpreting those omissions as “none.” Existing catalog availability is not completed H1/H4/H5 evidence. |
| C07 — Other images, local imports, and candidate symbol families | [SymbolSubmission](../../src/Foundry.Contracts/Assets.cs) records teacher-stated rights separately from its privacy-preflight capability. The [Mulberry/OpenMoji candidate packet](../council/symbol-source-candidate-packet.md) is explicitly held reconnaissance, not an admitted asset set. | A local capability or a license label is not authority to redistribute an import. No candidate artwork, mapping, family default, or expanded symbol vocabulary is admitted by this inventory. Exact source, acquired bytes, transformations, permissions, attribution, and applicable rights/AAC/AT findings are still required for the proposed use. Do not turn a private-use question into an unsupported global declaration of illegality. |
| C08 — Fonts | The [artifact-language contract](../localization/artifact-language-contract.md) says no font is bundled. [Native PDF code](../../src/Foundry.Rendering/VectorPdfWriter.cs) references Standard-14 Courier/WinAnsi; [HTML rendering](../../src/Foundry.Rendering/AccessibleHtmlRenderer.cs) uses installed-system-font stacks. | Naming a font in output does not establish ownership of font software or permission to bundle it. No font file was found in the inspected `assets/` inventory. Any future bundled font needs exact bytes, source, license, embedding/modification conditions, notices, and script/AT evidence. Installed-font availability is not universal glyph coverage. |
| C09 — Application UI text and future translation catalogs | [UiStrings](../../src/Foundry.App.WinForms/Localization/UiStrings.cs) and catalog machinery carry GPL code notices. The [catalog JSON schema](../localization/ui-catalog.schema.json) separately requires translation creator, source, license, modification history, review assertion, and neutral-source digest. The [reviewed-catalog contract](../localization/reviewed-ui-catalogs.md) separates assertion from exact-byte admission. | Neutral source code is not a blanket license for a future translator's work. The inspected production `UiCatalogDeployment` allowlist is empty. Pseudo text and synthetic LTR/RTL test catalogs remain engineering fixtures, not reviewed translations. A real catalog needs its own contributor terms, multilingual review, provenance, and authorized exact-file build pin. |
| C10 — Printed language furniture and bilingual examples | The artifact-language contract separates built-in English furniture from teacher values and bilingual segments. The [historical sample index](../evidence/0.1-alpha/README.md) explicitly labels its Spanish text as unreviewed synthetic fixture content. | A UI catalog does not translate printed content. A second artifact language needs an exact furniture/source inventory, applicable contribution rights, language-pair/script review, fonts, and reviewed bytes. Existing sample declarations do not establish translation quality or permission for newly supplied text. |
| C11 — Outside or member-authored material | [CONTRIBUTING's content hold](../../CONTRIBUTING.md#what-contributions-are-welcome-now), [proposed operating terms §8](../council/draft-first-cohort-operating-terms.md#8-participation-credit-and-contribution-rights), and [compensation-policy correction](../council/compensation-policy.md) separate contributions from attendance, notes, credit, and payment. | Outside/member-authored documentation, printables, fixtures, quotations, translations, and illustrations are not presently accepted. A reviewer may report a finding without transferring replacement expression. Selecting an outbound license would not itself provide an author's matching assent, employer permission, third-party rights, or participant publication consent. |
| C12 — Third-party dependencies and legal notices | [NOTICE's dependency section](../../NOTICE.md#third-party-dependencies), locked project dependencies, and the [rights/openness traceability rows](../release/release-requirement-test-traceability.md#rights-and-openness) describe separate commit-scoped inventory/SBOM routes. [COPYING](../../COPYING) is the preserved GPL license text, not project-authored curriculum. | Preserve each dependency's actual terms and applicable notices. This class inventory neither reruns a dependency audit nor licenses an OS/framework, dependency, or release package. A future release manifest must bind the exact distributed set and its own evidence; the new educational-content decision cannot absorb third-party legal texts or rights. |

Student work, student data, identifying classroom material, credentials, and
blind-study definitions/keys remain forbidden repository content under
[AGENTS](../../AGENTS.md#never-commit), regardless of any license. Private
participant identity, consent, payment, or employment records do not belong in
this inventory. The [pilot-kit index](../evidence/pilot-kit/README.md) permits
only its explicitly fictional schema example in source; it does not authorize
generation or use of a real instrument.

## What the present metadata can and cannot establish

The inspected [schema-1 AssetProvenance record](../../src/Foundry.Contracts/Assets.cs)
carries identity, concept, version, filename, MIME type, source, creator, license,
content SHA-256, meaning, alt text, redistribution flag, ambiguity, and the two
optional export dispositions. It does not contain the full plan §9 license-text,
commercial-use, consent/release, or authenticated rights-review ledger.
[AssetManifestReader](../../src/Foundry.Storage/AssetManifestReader.cs) checks
bounded strict JSON; [AssetRightsPolicy](../../src/Foundry.Storage/AssetRightsPolicy.cs)
recognizes a narrow set of open-license identifiers and requires explicit
open-export dispositions. Neither proves a declared licensor's authority.

Accordingly, the next material-level inventory needs both exact bytes and the
intended operation: source contribution, local private use, classroom print,
editable export, open-pack redistribution, public site, or release bundle.
Record permission evidence, modifications, notices, scope limits, and the
accountable disposition for that operation. A Green lane, teacher approval,
successful parser, or matching digest supplies none of the missing rights.
Durable schema changes remain separately governed; this document adds no
unversioned fields to existing packages or asset records.

## Bounded comparison for the unanswered content-license decision

The following summaries were checked against Creative Commons' official pages
on 5 September 2026. They compare options; they do not recommend applying either
to an unexamined work. Both options permit sharing and adaptation, including
commercial uses, subject to their terms. Both require attribution, license
information, and modification indications, and prohibit additional restrictions
on licensed freedoms. [CC BY 4.0 deed](https://creativecommons.org/licenses/by/4.0/),
[CC BY-SA 4.0 deed](https://creativecommons.org/licenses/by-sa/4.0/).

| Option | Consequence to discuss with the rights steward and counsel |
|---|---|
| CC BY 4.0 | Does not impose a ShareAlike condition. For shared adaptations, the adapter's license must still allow recipients to comply with the original BY license. This leaves a different downstream licensing choice from BY-SA, not an absence of attribution or other obligations. [Official legal code §3](https://creativecommons.org/licenses/by/4.0/legalcode.en#s3). |
| CC BY-SA 4.0 | Shared adaptations must use an adapter's license with the same license elements at version 4.0 or later, or a CC-listed BY-SA Compatible License. The exact adaptation/collection and compatibility questions must be reviewed for the actual combined resource. [Official legal code §3](https://creativecommons.org/licenses/by-sa/4.0/legalcode.en#s3). |

Neither option supplies rights the licensor lacks, other people's privacy or
publicity permissions, or trademark clearance. CC's guidance asks licensors to
secure the necessary rights and mark excluded material. Stopping distribution
does not terminate an existing CC license; contribution licensing therefore
must not be described as an erasure promise or collapsed into withdrawal from
future participation. The repository's participant withdrawal route remains a
separate matter. [CC BY legal code, licensor considerations and §6](https://creativecommons.org/licenses/by/4.0/legalcode.en),
[CC BY-SA legal code, licensor considerations and §6](https://creativecommons.org/licenses/by-sa/4.0/legalcode.en).

This short comparison is not project-specific legal advice, a compatibility
finding, or counsel review. No CC license text has been added as the selected
repository content license.

## Unanswered decision fields

These rows are a requirements checklist for a later accountable record, **not
an authority form, a signature, or operative blank inputs**. Every listed
decision is unresolved by this inventory. Do not fill an authority assertion
from the table's existence, a broad implementation instruction, or a test.

| Decision field | Exact answer/evidence still required | Current state |
|---|---|---|
| License identity | Full selected license name, version, canonical legal text/URL, and reason for selecting it over the alternative | ~~UNANSWERED~~ **Later 5 September 2026:** CC BY-SA 4.0 selected as the proposal; [exact references and stated copyleft reason](2026-09-05-content-license-selection.md). No operative material grant |
| Authorized licensor and ownership basis | Who can license each covered class; supporting authorship, grant, or other authority evidence; unresolved/third-party exclusions | UNANSWERED — no foundation-wide copyright assignment inferred |
| Covered material | Enumerated material/classes with exact source revisions and byte bindings; explicit distinction between code, prose, printable expression, assets, and translations | UNANSWERED |
| Temporal scope | Whether the decision covers only newly admitted material or also named existing material; separate authority for any existing material | UNANSWERED — no retrospective relicensing |
| Exclusions and intended uses | Preserved third-party/GPL/CC0 terms; every excluded element; exact public, printable, editable, asset-pack, and release uses to evaluate | UNANSWERED |
| Project-specific review | Product-owner and rights-steward disposition, required counsel findings, open conflicts/limitations, and protected-seat boundaries | NOT PERFORMED by this inventory |
| Notices and inbound route | Exact matching contribution-control version, rights assertions, attribution/modification requirements, notice placement, and acceptance process | NOT ENACTED; existing contribution hold remains |
| Decision record identity | Accountable decision-maker, actual decision instant, exact approved wording, source/material bindings, and supersession relationship | NOT SUPPLIED; this inventory is not that record |

## Separate contributor-assent fields

The following evidence belongs to a future exact-material admission process
after the license decision. It has not been obtained. Keep actual identity,
signatures, consent records, employment details, and other private supporting
records outside public Git; any permitted public record uses only its authorized
scope, factual disposition, and opaque custodian references.

| Separate field | Evidence needed before the stated use | Present disposition |
|---|---|---|
| Contribution license assent | The actual contributor's affirmative assent to the exact applicable license/control and exact submitted material; unresolved third-party/employer rights stay held | NOT OBTAINED |
| Authorship and permission basis | Source and modifications, the contributor's authority to submit, excluded elements, and any needed separate permission evidence | NOT AUTHENTICATED by this inventory |
| Required license attribution | Exact attribution and modification disposition for the submitted material, reconciled with the separately chosen public-credit boundary before publication | NOT SUPPLIED |
| Public-credit choice | The person's separate public-credit preference; no default real-name disclosure and no inference from required license attribution | NOT OBTAINED |
| Participation choice | Its own applicable affirmative choice, independent of submitting copyrightable material | NOT OBTAINED |
| Private/de-identified note-collection choice | Its own applicable choice and stated collection scope; participation alone does not supply it | NOT OBTAINED |
| Public-record publication | Separate consent for the exact participant-reviewed final public record; contribution assent does not supply it | NOT OBTAINED |
| Recording choice | A separate recording choice and exact recording/use scope; no inference from participation or note collection | NOT OBTAINED |
| Within-cohort identity/affiliation disclosure | Its own choice for the exact information and recipients, with recipient confidentiality/no-contact acknowledgement | NOT OBTAINED |
| Withdrawal explanation | Acknowledgement of the applicable future-use/correction route and its public-history limits, without promising license revocation or universal erasure | NOT OBTAINED |
| Compensation election | A separate receive/decline choice under the applicable enacted compensation control; no finding or license is purchased | NOT OBTAINED |
| Bounded-role acceptance | Separate acceptance of the exact offered role and scope; attendance or contribution does not appoint a seat | NOT OBTAINED |
| Maintainer appointment | A separately authorized and accepted maintainer role; it does not follow from a code or content contribution | NOT OBTAINED |
| Copyright stewardship | Any proposed transfer requires its own exact authority and acceptance; no assignment to the foundation is inferred | NOT OBTAINED |

The initial license/assent route precedes requesting member-authored material.
Later H5 reviews exact material and uses; it is not a circular prerequisite for
preparing this initial decision. Council enactment and H0–H7 remain governed by
the [ordered review ledger](../council/bounded-commission-review-ledger.md).
This inventory clears no publication, participant-use, or release hold.
