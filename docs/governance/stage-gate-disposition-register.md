# Stage-gate disposition register

**Status: CONSOLIDATED WORKING INVENTORY — ALL ROWS OPEN.**

This register is a consolidated working inventory for implementation-plan §16
Gates 0–5. The implementation plan remains authoritative for what each
criterion means; this file records whether its evidence and accountable
decision exist.
It does not alter a criterion, ratify an ADR, convene the council, authenticate
a reviewer, or exercise a protected, district, counsel, or typist authority.

This initial 3 September 2026 register was prepared from repository artifacts.
No row was closed by its preparation and no cited test or human activity was
rerun for this record. Existing artifacts are described as documented or
machine-evidenced only where the repository already carries them. Every row
remains open until its named owner records the exact evidence and decision.

## Status and authority rules

- **OPEN — NO EVIDENCE:** no closing artifact was found.
- **OPEN — DOCUMENTED:** a design or policy exists, but no complete disposition
  or exact-release evidence exists.
- **OPEN — PARTIAL:** implementation or evidence covers part of the criterion;
  the named remainder is still required.
- **OPEN — MACHINE-EVIDENCED:** current-source machine evidence exists; an
  exact-release rerun and any named human decision remain open.
- **OPEN — HUMAN/PROTECTED:** only the named human or protected seat can supply
  the missing judgment.
- **OPEN — TYPIST/DISTRICT:** the remaining act changes external state or
  depends on district-controlled policy, identity, device, or signing material.
- **CLOSED:** reserved for a future dated disposition that identifies the exact
  criterion, artifact/revision, evidence, owner, and every required concurrence.
  A machine result alone never closes a human, protected, district, or typist
  row.

Authority classes do not substitute for one another. Where a row names more
than one class, each owns its stated part. Vacancy, silence, absence, recusal,
elapsed time, automation, or a favorable neighboring row leaves the criterion
open.

For every future update:

1. preserve the row and its history; do not delete an unmet or superseded row;
2. replace “None — open” only with a dated, attributable decision;
3. bind evidence to an exact repository revision and, for release claims, the
   exact artifact digest and hosted conclusions;
4. record a superseding row or record rather than silently rewriting the
   evidence used for an earlier decision; and
5. keep identities private unless each person separately chose public credit.

## Gate 0 — Charter

| ID | Criterion | Accountable owner | Authority class | Current status | Evidence presently recorded | Decision date | Dependency / open remainder | Supersession |
|---|---|---|---|---|---|---|---|---|
| G0-01 | Purpose, non-goals, and prohibited decisions | Product owner; applicable protected seats | Human/protected | **OPEN — DOCUMENTED** | Implementation plan constitution; traceability P-01 | None — open | Release-specific owner and protected-seat signatures | None recorded |
| G0-02 | Module lane matrix | Product owner; privacy/legal/records | Human/protected | **OPEN — DOCUMENTED** | Implementation plan module and lane records; privacy data inventory | None — open | Accountable review of every module and derivative lane | None recorded |
| G0-03 | Data inventory and lifecycle | Privacy/legal/records; lead maintainer | Human/protected + machine | **OPEN — PARTIAL** | [Data inventory](../privacy/data-inventory.md); [trust boundaries](../architecture/trust-boundaries.md); traceability PS-01 | None — open | Privacy/records disposition and deployment-specific validation | None recorded |
| G0-04 | Privacy impact assessment | Privacy/legal/records; district | Human/protected + district | **OPEN — NO EVIDENCE** | No completed PIA identified | None — open | Named privacy owner, exact scope, and district review | None recorded |
| G0-05 | Retention and disposal decision | Privacy/legal/records; district | Human/protected + district | **OPEN — HUMAN/PROTECTED** | Technical purge evidence is partial under traceability PS-03 | None — open | Written retention, disposal, litigation-hold, and device-residue decisions | None recorded |
| G0-06 | Accessibility requirements | Accessibility/AT seat; product owner | Human/protected | **OPEN — PARTIAL** | ADR-002, accessibility requirements, and traceability AL rows | None — open | Constituted AT seat with recorded scope and qualification/authentication basis, exact review, and release-specific finding | None recorded |
| G0-07 | AAC and visual-support terminology | AAC user/SLP/special-educator seat | Protected seat | **OPEN — HUMAN/PROTECTED** | Council charter and held symbol-source packet | None — open | Constituted seat with exact product-owner offer, accepted scope and enacted terms, recorded qualification/authentication basis, and exact terminology/artifact review | None recorded |
| G0-08 | Licensing policy | OER/license steward; product owner; counsel; contributors | Human/protected + counsel | **OPEN — HUMAN/PROTECTED** | CONTRIBUTING and README separate code from the still-pending content license; [compensation policy](../council/compensation-policy.md) now preserves that boundary | None — open | Content-license choice, counsel/OER review, provenance rules, and separate contributor assent | None recorded |
| G0-09 | RACI | Product owner; governance | Human | **OPEN — PARTIAL** | GOVERNANCE roles and implementation-plan §15 identify accountability only | None — open | Responsible, consulted, informed, deputies, recusals, and acceptance record | None recorded |

## Gate 1 — Shared foundation

| ID | Criterion | Accountable owner | Authority class | Current status | Evidence presently recorded | Decision date | Dependency / open remainder | Supersession |
|---|---|---|---|---|---|---|---|---|
| G1-01 | Trust-boundary and data-flow diagrams | Lead maintainer; privacy/legal/records; district | Machine + human/district | **OPEN — PARTIAL** | [Trust boundaries](../architecture/trust-boundaries.md) and data inventory | None — open | Accountable privacy/district review against exact deployment | None recorded |
| G1-02 | Signed recipe schema | Lead maintainer; product owner | Machine + human | **OPEN — PARTIAL** | Versioned recipe manifests and traceability P-02/P-03 | None — open | Exact release inventory, coherent version decision, and authorized signature route | None recorded |
| G1-03 | Restricted-content block | Lead maintainer; privacy/safeguarding | Machine + human/protected | **OPEN — MACHINE-EVIDENCED** | Traceability stop-ships SS-01/SS-02 and regression paths | None — open | Exact-release rerun and review of every new intake/sink | None recorded |
| G1-04 | Local preflight and outbound preview | Lead maintainer; privacy/district | Machine + human/district | **OPEN — MACHINE-EVIDENCED** | Traceability PS-01/PS-02 and SS-03 | None — open | Exact configured deployment and Gate 3 review | None recorded |
| G1-05 | Stateless provider adapter and egress allowlist | Lead maintainer; district IT/security | Machine + district | **OPEN — PARTIAL** | Provider-boundary and egress evidence in traceability PS-02 | None — open | Exact provider configuration, safe refusal probe, and district authorization | None recorded |
| G1-06 | Content-free diagnostics | Lead maintainer; privacy/legal/records | Machine + human | **OPEN — MACHINE-EVIDENCED** | Traceability PS-05 and content-free evidence contracts | None — open | Production monitoring configuration and privacy review | None recorded |
| G1-07 | Open project format | Lead maintainer; product owner | Machine + human | **OPEN — PARTIAL** | ADR-003 and traceability P-02/RO-03/OP-04 | None — open | Schema-1 recipeHash stop, migration decision, and exact release proof | None recorded |
| G1-08 | Signed packaging and rollback design | Lead maintainer; typist; district IT | Machine + typist/district | **OPEN — PARTIAL** | [Hardening checklist](../release/hardening-checklist.md) and proposed ADR-007 | None — open | Ratified route, coherent version/tag, executed signing, installation, and rollback evidence | None recorded |
| G1-09 | Kill switch | Lead maintainer; district IT/security | Machine + district | **OPEN — MACHINE-EVIDENCED** | Policy kill switch described in hardening and traceability | None — open | Exact managed-device policy and district exercise | None recorded |
| G1-10 | SBOM and asset ledger | Lead maintainer; OER/license steward; typist | Machine + human/typist | **OPEN — PARTIAL** | Current-source CI inventories, hardening checklist, NOTICE, and traceability RO-01/RO-02 | None — open | Exact release-artifact correspondence, complete rights fields, and rights review | None recorded |

## Gate 2 — Verification

| ID | Criterion | Accountable owner | Authority class | Current status | Evidence presently recorded | Decision date | Dependency / open remainder | Supersession |
|---|---|---|---|---|---|---|---|---|
| G2-01 | Unit, integration, and render evidence | Lead maintainer | Machine | **OPEN — MACHINE-EVIDENCED** | Seven-suite evidence in the [evidence ledger](../evidence/evidence-ledger.json) and traceability matrix | None — open | Closing rites and stability rerun on exact release commit/artifact | None recorded |
| G2-02 | Disk, network, and privacy-canary report | Lead maintainer; privacy/legal/records; district | Machine + human/district | **OPEN — PARTIAL** | Traceability PS-03/PS-06 and current PII-canary tests | None — open | Captured exact-deployment network trace and device-residue review | None recorded |
| G2-03 | Prompt-injection red team | Lead maintainer; independent threat reviewer | Machine + human/protected | **OPEN — PARTIAL** | Traceability PS-02/PS-07 and SS-09 regression evidence | None — open | Independent release-specific threat-model review | None recorded |
| G2-04 | SAST, SCA, and secret scan | Lead maintainer | Machine | **OPEN — MACHINE-EVIDENCED** | CI/CodeQL entries in the evidence ledger and traceability OP-05 | None — open | Exact release-head hosted conclusions and retained artifacts | None recorded |
| G2-05 | Accessibility report and ACR/VPAT draft | Accessibility/AT seat | Protected seat | **OPEN — HUMAN/PROTECTED** | Machine accessibility evidence under traceability AL-01/AL-02 | None — open | Constituted AT seat with recorded scope and qualification/authentication basis; human screen-reader, keyboard, reading-order, and conformance review | None recorded |
| G2-06 | Golden instructional corpus and independent ratings | Curriculum/content reviewers; educator council | Human/protected | **OPEN — HUMAN/PROTECTED** | Synthetic fixture corpora under traceability P-04 | None — open | Independent ratings; machine fixtures are not expert judgment | None recorded |
| G2-07 | Localization and RTL report | Multilingual/family liaison; accessibility/AT | Protected seats | **OPEN — HUMAN/PROTECTED** | Structural localization evidence under traceability AL-03/AL-04 | None — open | Constituted exact-language/script and AT seats with recorded scopes and qualification/authentication bases; reviewed real catalog, language quality, RTL, and font review | None recorded |
| G2-08 | License report | OER/license steward; counsel; typist | Human/protected + typist | **OPEN — PARTIAL** | NOTICE, current dependency evidence, and traceability RO rows | None — open | Content-license decision, complete asset rights, and exact-release rights report | None recorded |
| G2-09 | Model and recipe cards | Product owner; lead maintainer; applicable protected seats | Machine + human/protected | **OPEN — PARTIAL** | Recipe manifests, identity packet, and traceability P-01/P-03 | None — open | Release-specific owner statements and protected review | None recorded |
| G2-10 | Seeded-error teacher study | Constituted educator council oversight; at least three separately consented educator participants; facilitator | Human | **OPEN — HUMAN/PROTECTED** | Protocol and private-kit boundary exist; no study result | None — open | Authenticated participant roles, separately recorded choices and withdrawal acknowledgement, sealed instrument, completed study, aggregate verdict, and council review under enacted terms | None recorded |

## Gate 3 — District readiness

| ID | Criterion | Accountable owner | Authority class | Current status | Evidence presently recorded | Decision date | Dependency / open remainder | Supersession |
|---|---|---|---|---|---|---|---|---|
| G3-01 | Central instructional-software approval | District authority | District | **OPEN — TYPIST/DISTRICT** | [Gate 3 readiness packet](../district/gate-3-readiness-packet.md) is prepared; instrument unsigned | None — open | Written central approval | None recorded |
| G3-02 | Provider contract and configuration review | District IT/security; privacy/legal/records | District + human/protected | **OPEN — TYPIST/DISTRICT** | Capability kit and readiness packet | None — open | Exact contract, deployment, model, endpoint, terms, and reviewer probe | None recorded |
| G3-03 | Deployment geography and statefulness decision | District IT/security; privacy/legal/records | District + human/protected | **OPEN — TYPIST/DISTRICT** | Questions documented in readiness packet | None — open | Written geography, statefulness, and retention decision | None recorded |
| G3-04 | RBAC, Conditional Access, and policy attestation | District IT/security | District | **OPEN — TYPIST/DISTRICT** | Policy mechanisms exist; no district attestation | None — open | Exact tenant, identity, endpoint, and policy evidence | None recorded |
| G3-05 | Records-approved retention and disposal | Privacy/legal/records; district records owner | District + human/protected | **OPEN — HUMAN/PROTECTED** | Technical purge contracts only | None — open | Written records schedule, disposal, holds, and device/provider decisions | None recorded |
| G3-06 | Incident and safeguarding playbooks | Safeguarding; privacy/legal/records; district | District + human/protected | **OPEN — HUMAN/PROTECTED** | SECURITY and readiness boundaries exist | None — open | Named owners, escalation, direct-source procedure, and exercised playbook | None recorded |
| G3-07 | Intune, signing, uninstall, and rollback evidence | District IT/security; typist | District + typist | **OPEN — TYPIST/DISTRICT** | Packaging contracts and hardening plan only | None — open | Signed package and managed-device install/uninstall/rollback drill | None recorded |
| G3-08 | Teacher, accessibility, privacy, and administrator training | District; educator and protected seats | District + human/protected | **OPEN — NO EVIDENCE** | No completed training record identified | None — open | Reviewed materials, delivery, attendance, and comprehension/feedback record | None recorded |
| G3-09 | Support, patch, disclosure, and end-of-life commitments | Product owner; maintainer; district | Human + district | **OPEN — DOCUMENTED** | SECURITY, GOVERNANCE sustainability promises, and traceability OP-05 | None — open | Named sustainable owners, release policy, escalation, and pre-deployment EOL decision | None recorded |

## Gate 4 — Pilots

| ID | Criterion | Accountable owner | Authority class | Current status | Evidence presently recorded | Decision date | Dependency / open remainder | Supersession |
|---|---|---|---|---|---|---|---|---|
| G4-01 | Synthetic and teacher-authored verification | Educator seats; lead maintainer | Machine + human | **OPEN — PARTIAL** | Synthetic corpora and machine evidence exist; [human-gates plan](../pilots/human-gates-coordination-plan.md) remains unrun | None — open | Teacher sessions, separate contribution assent where authored material is retained, and participant-reviewed evidence | None recorded |
| G4-02 | Staff-only Green pilot | Educator council; facilitator | Human | **OPEN — HUMAN/PROTECTED** | Protocols and schedule structure prepared; no pilot evidence artifact | None — open | Confirmed participants, consent boundaries, executed sessions, thresholds, and review | None recorded |
| G4-03 | Supervised Green classroom-output pilot after central approval | District; educator council; applicable protected seats | District + human/protected | **OPEN — TYPIST/DISTRICT** | No classroom-output pilot authorized or run | None — open | G3-01 closure plus exact supervised protocol and protected reviews | None recorded |
| G4-04 | Amber pilot only after Amber architecture and written approval | District IT/security; privacy/legal/records; safeguarding | District + protected seats | **OPEN — TYPIST/DISTRICT** | Amber remains held; no complete architecture or approval | None — open | Complete Amber architecture, PIA/retention/incident decisions, and signed district instrument | None recorded |
| G4-05 | Restricted features remain absent | Lead maintainer; privacy/legal/records; safeguarding | Machine + human/protected | **OPEN — MACHINE-EVIDENCED** | Current stop-ship guards SS-01/SS-02 | None — open | Exact-release rerun and review of every new feature, intake, persistence, and sink | None recorded |

## Gate 5 — Release and change control

| ID | Criterion | Accountable owner | Authority class | Current status | Evidence presently recorded | Decision date | Dependency / open remainder | Supersession |
|---|---|---|---|---|---|---|---|---|
| G5-01 | Every provider, model, recipe, retention rule, symbol pack, translation engine, major dependency, or data-flow change triggers matching regression and re-review; production monitoring records configuration and health, never instructional content | Lead maintainer; product owner; every implicated protected/district owner | Machine + human/protected + district/typist | **OPEN — PARTIAL** | Versioned manifests, CI policy, traceability SS-12, content-free diagnostics, and governance boundaries | None — open | Exact change record, regression evidence, re-review by every implicated authority, release correspondence, and deployment monitoring decision | None recorded |

## Current disposition

No Gate 0–5 criterion is closed by this initial register. Rows with existing
machine evidence remain open for their exact-release rerun and any named human
remainder. Rows owned by a protected seat, district, counsel, council cohort, or
typist remain open until that owner acts and the evidence is recorded.
