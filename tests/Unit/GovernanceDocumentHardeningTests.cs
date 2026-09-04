// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.RegularExpressions;

namespace Foundry.Tests.Unit;

public sealed partial class GovernanceDocumentHardeningTests
{
    private static readonly string[] GovernedDocuments =
    [
        "GOVERNANCE.md",
        Path.Combine("docs", "educator-council.md"),
        Path.Combine("docs", "council", "atlas-priority-session.md"),
        Path.Combine("docs", "council", "atlas-priority-session-freeze-manifest.md"),
        Path.Combine("docs", "council", "atlas-priority-session-feasibility-record.md"),
        Path.Combine("docs", "council", "atlas-priority-session-product-owner-disposition.md"),
        Path.Combine("docs", "council", "compensation-policy.md"),
        Path.Combine("docs", "council", "correspondence", "2026-09-invitation-updates.md"),
        Path.Combine("docs", "council", "draft-first-cohort-operating-terms.md"),
        Path.Combine("docs", "council", "bounded-commission-review-ledger.md"),
        Path.Combine("docs", "governance", "stage-gate-disposition-register.md"),
        Path.Combine("docs", "accessibility", "nvda-walkthrough-script.md"),
        Path.Combine("docs", "pilots", "human-gates-coordination-plan.md"),
        Path.Combine("docs", "pilots", "print-inspection-checklist.md"),
        Path.Combine("docs", "pilots", "seeded-error-study.md"),
        Path.Combine("docs", "pilots", "think-aloud-protocol.md"),
    ];

    [Fact]
    public void Proposed_operating_terms_create_no_quorum_or_council_authority()
    {
        var draft = Read("docs", "council", "draft-first-cohort-operating-terms.md");
        var council = Read("docs", "educator-council.md");
        var session = Read("docs", "council", "atlas-priority-session.md");
        var coordination = Read("docs", "pilots", "human-gates-coordination-plan.md");

        Assert.Contains(
            "**DRAFT — NOT ENACTED — NO QUORUM OR AUTHORITY CREATED.**",
            draft,
            StringComparison.Ordinal);
        Assert.Contains("These rules have no effect until enacted.", draft, StringComparison.Ordinal);
        Assert.Contains(
            "If vacancies make general quorum mathematically impossible",
            draft,
            StringComparison.Ordinal);
        Assert.Contains(
            "fill only enough already-defined vacant seats",
            draft,
            StringComparison.Ordinal);
        Assert.Contains(
            "A material product-owner conflict in enacting or amending these terms holds",
            draft,
            StringComparison.Ordinal);
        Assert.Contains(
            "executing it does not itself amend",
            draft,
            StringComparison.Ordinal);
        Assert.Contains(
            "separate numbered ADR",
            draft,
            StringComparison.Ordinal);
        Assert.Contains(
            "If neither the named",
            draft,
            StringComparison.Ordinal);
        Assert.Contains("the appointment is held", draft, StringComparison.Ordinal);
        Assert.Contains("Renewal requires, before expiry", draft, StringComparison.Ordinal);
        Assert.Contains(
            "ceiling(2 × eligible non-recused seated natural persons / 3)",
            draft,
            StringComparison.Ordinal);
        Assert.Contains("AUTHENTICATOR CONFLICT — HELD", draft, StringComparison.Ordinal);
        Assert.Contains("PARTICIPANT SCOPE CONFLICT — HELD", draft, StringComparison.Ordinal);
        Assert.Contains("PARTICIPANT CONFLICT — LIMITED", draft, StringComparison.Ordinal);
        Assert.Contains("A non-conflicted authenticator or alternate cannot cure", draft, StringComparison.Ordinal);
        Assert.Contains("PRODUCT-OWNER DISPOSITION HELD", draft, StringComparison.Ordinal);
        Assert.Contains("matter-quorum denominator unless recused", draft, StringComparison.Ordinal);
        Assert.Contains(
            "the activity stops immediately and any council matter adjourns",
            draft,
            StringComparison.Ordinal);
        Assert.Contains("fresh roster/vacancy or participant-role cutoff", draft, StringComparison.Ordinal);
        Assert.Contains("stale denominator is invalid.", draft, StringComparison.Ordinal);
        Assert.Contains("A held dispute cannot be", draft, StringComparison.Ordinal);
        Assert.Contains("represented as resolved", draft, StringComparison.Ordinal);
        Assert.Contains("The record either constitutes a quorum-capable roster", draft, StringComparison.Ordinal);
        Assert.Contains(
            "Either control may retain its `v1` identity only if its final",
            draft,
            StringComparison.Ordinal);
        Assert.Contains(
            "**OPERATING TERMS NOT ENACTED — NO COUNCIL QUORUM OR DECISION AUTHORITY.**",
            draft,
            StringComparison.Ordinal);
        Assert.Contains("They create no quorum or authority now.", council, StringComparison.Ordinal);
        Assert.Contains("within-cohort identity/affiliation disclosure", council, StringComparison.Ordinal);
        Assert.Contains("no-outside-contact boundary", council, StringComparison.Ordinal);
        Assert.Contains("**UNRANKED CONSULTATION — NO QUORUM**", session, StringComparison.Ordinal);
        Assert.Contains(
            "Silence or absence means **not reviewed**, never assent",
            session,
            StringComparison.Ordinal);
        Assert.Contains(
            "## Proposed session hygiene — not ratified and not operative",
            coordination,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Compensation_and_participation_create_no_content_license_or_erasure_promise()
    {
        var policy = Read("docs", "council", "compensation-policy.md");
        var correspondence = Read("docs", "council", "correspondence", "2026-09-invitation-updates.md");
        var coordination = Read("docs", "pilots", "human-gates-coordination-plan.md");
        var protocol = Read("docs", "pilots", "think-aloud-protocol.md");
        var print = Read("docs", "pilots", "print-inspection-checklist.md");
        var seeded = Read("docs", "pilots", "seeded-error-study.md");
        var nvda = Read("docs", "accessibility", "nvda-walkthrough-script.md");
        var contributing = Read("CONTRIBUTING.md");
        var implementationPlan = Read("docs", "implementation-plan.md");

        Assert.Contains(
            "its separate license for documentation and original printable content has not been chosen",
            policy,
            StringComparison.Ordinal);
        Assert.Contains("PROPOSED — NOT RATIFIED OR OPERATIVE", policy, StringComparison.Ordinal);
        Assert.Contains("NOT READY FOR PARTICIPANT USE", protocol, StringComparison.Ordinal);
        Assert.Contains("NOT READY FOR PARTICIPANT OR REVIEWER USE", print, StringComparison.Ordinal);
        Assert.Contains("NOT READY FOR PARTICIPANT USE", seeded, StringComparison.Ordinal);
        Assert.Contains("NOT READY FOR MODERATED OR REVIEWER USE", nvda, StringComparison.Ordinal);
        Assert.Contains(
            "grant no GPL or other contribution license",
            policy,
            StringComparison.Ordinal);
        Assert.Contains(
            "the author separately assents to those exact contribution terms",
            policy,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Fixtures, findings, and materials created in council work enter the project under GPL-3.0-or-later",
            policy,
            StringComparison.Ordinal);
        Assert.Contains(
            "No participant session begins until that dated reverification is recorded",
            policy,
            StringComparison.Ordinal);
        Assert.Contains(
            "private ledger and an authorized disbursement route is operational",
            policy,
            StringComparison.Ordinal);
        Assert.Contains(
            "Do not schedule it when that reservation would exceed",
            policy,
            StringComparison.Ordinal);
        Assert.Contains(
            "A separately declined session is zero and non-retroactive",
            policy,
            StringComparison.Ordinal);
        Assert.Contains(
            "$300 per compensated natural person per UTC calendar quarter across all covered sessions and consultations",
            policy,
            StringComparison.Ordinal);
        Assert.Contains("HISTORICAL PRIORITY WORDING — HELD, NOT OPERATIVE", policy, StringComparison.Ordinal);
        Assert.Contains("NOT ON COMPENSATED EMPLOYER TIME", policy, StringComparison.Ordinal);
        Assert.Contains("HONORARIUM PAUSED — compensated employer time", policy, StringComparison.Ordinal);
        Assert.Contains("accepted and authenticated bounded", policy, StringComparison.Ordinal);
        Assert.Contains("| H0 needs-first council |", policy, StringComparison.Ordinal);
        Assert.Contains("| H1 AAC/SLP |", policy, StringComparison.Ordinal);
        Assert.Contains("| H2 curriculum |", policy, StringComparison.Ordinal);
        Assert.Contains("| H3 multilingual/family |", policy, StringComparison.Ordinal);
        Assert.Contains("| H4 accessibility/AT |", policy, StringComparison.Ordinal);
        Assert.Contains("| H5 rights/OER |", policy, StringComparison.Ordinal);
        Assert.Contains("| H6 physical print |", policy, StringComparison.Ordinal);
        Assert.Contains("| H7 teacher pilot |", policy, StringComparison.Ordinal);
        Assert.Contains(
            "The typist's facilitating or product-owner acts, and",
            policy,
            StringComparison.Ordinal);
        Assert.Contains(
            "counsel/accountant verification acts, are not participant-session roles",
            policy,
            StringComparison.Ordinal);

        Assert.Contains("> **NOT READY — DO NOT SEND.**", correspondence, StringComparison.Ordinal);
        Assert.Contains(
            "rewrite and approve the exact letter",
            correspondence,
            StringComparison.Ordinal);
        Assert.DoesNotContain("letters are ready to send as written", correspondence, StringComparison.Ordinal);
        Assert.DoesNotContain("changeable at any time", correspondence, StringComparison.Ordinal);

        Assert.Contains(
            "Once a record enters public Git history or is distributed, erasure from clones and prior artifacts cannot be promised",
            coordination,
            StringComparison.Ordinal);
        Assert.Contains(
            "A public Git history or an artifact already copied by others cannot honestly be promised erased",
            protocol,
            StringComparison.Ordinal);
        Assert.Contains(
            "Participation consent, private/de-identified note-collection consent, public-record publication consent, recording consent, within-cohort identity/affiliation disclosure, public credit, compensation election, content contribution, maintainer appointment, and copyright stewardship are separate choices",
            coordination,
            StringComparison.Ordinal);
        Assert.Contains("per-natural-person quarter-to-date commitments", coordination, StringComparison.Ordinal);
        Assert.Contains("no-outside-contact boundary", coordination, StringComparison.Ordinal);
        Assert.Contains("give every contributor whose material remains", seeded, StringComparison.Ordinal);
        Assert.Contains(
            "public-record publication choice permits those exact bytes",
            seeded,
            StringComparison.Ordinal);
        Assert.Contains("This protocol does not publish the record.", seeded, StringComparison.Ordinal);
        Assert.Contains(
            "print/educator reviewer alone owns and closes those mechanical findings",
            print,
            StringComparison.Ordinal);
        Assert.Contains("execution only; no review authority or veto", print, StringComparison.Ordinal);
        Assert.Contains("**Content-contribution hold:**", contributing, StringComparison.Ordinal);
        Assert.Contains(
            "Outside or member-authored documentation",
            contributing,
            StringComparison.Ordinal);
        Assert.Contains(
            "Project-owner-directed factual governance, status, and repository-maintenance prose",
            contributing,
            StringComparison.Ordinal);
        Assert.Contains(
            "Outside or member-authored teacher material remains outside the repository",
            implementationPlan,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Public tests, documentation, issue reports, and CI use synthetic, teacher-authored",
            implementationPlan,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Atlas_H0_uses_separate_choices_constituted_seats_and_a_non_circular_freeze_chain()
    {
        var session = Read("docs", "council", "atlas-priority-session.md");
        var manifest = Read("docs", "council", "atlas-priority-session-freeze-manifest.md");
        var feasibility = Read("docs", "council", "atlas-priority-session-feasibility-record.md");
        var disposition = Read("docs", "council", "atlas-priority-session-product-owner-disposition.md");
        var ledger = Read("docs", "council", "bounded-commission-review-ledger.md");
        var terms = Read("docs", "council", "draft-first-cohort-operating-terms.md");
        var compensation = Read("docs", "council", "compensation-policy.md");

        Assert.Contains(
            "| Private/de-identified note-collection consent recorded |",
            session,
            StringComparison.Ordinal);
        Assert.Contains(
            "| Participation consent recorded separately |",
            session,
            StringComparison.Ordinal);
        Assert.Contains(
            "| Public-record publication consent recorded |",
            session,
            StringComparison.Ordinal);
        Assert.Contains(
            "| Recording consent recorded, or no recording |",
            session,
            StringComparison.Ordinal);
        Assert.Contains(
            "| Operative compensation-policy version and effective date; election recorded |",
            session,
            StringComparison.Ordinal);
        Assert.Contains(
            "| Content-contribution choice and exact license/control identity, or none |",
            session,
            StringComparison.Ordinal);
        Assert.Contains(
            "| Role-acceptance choice and exact bounded role/control identity, or none |",
            session,
            StringComparison.Ordinal);
        Assert.Contains(
            "| Maintainer-appointment choice and exact role/control identity, or none |",
            session,
            StringComparison.Ordinal);
        Assert.Contains(
            "| Copyright-stewardship choice and exact transfer/control identity, or none |",
            session,
            StringComparison.Ordinal);
        Assert.Contains(
            "| Withdrawal right and route explained/acknowledged |",
            session,
            StringComparison.Ordinal);
        Assert.Contains(
            "Constituted-seat authority entries (stable seat/person refs, presence, scope, term, qualification-basis category, and private custodian reference)",
            session,
            StringComparison.Ordinal);
        Assert.Contains(
            "| Disputed-recusal resolution subrecords before affected matters, or NONE |",
            session,
            StringComparison.Ordinal);
        Assert.Contains("read-back=CONFIRMED", session, StringComparison.Ordinal);
        Assert.Contains("affected-person-practicing-educator=YES|NO", session, StringComparison.Ordinal);
        Assert.Contains("excluded-total=1", session, StringComparison.Ordinal);
        Assert.Contains("excluded-present=1", session, StringComparison.Ordinal);
        Assert.Contains("excluded-practicing-educators=0|1", session, StringComparison.Ordinal);
        AssertOrdered(
            session,
            "affected-person-excluded=YES",
            "affected-person-practicing-educator=YES|NO",
            "excluded-total=1",
            "excluded-present=1",
            "excluded-practicing-educators=0|1",
            "outcome=<outcome>",
            "eligible-total=<non-negative count>",
            "eligible-present=<non-negative count>",
            "practicing-educators=<non-negative",
            "quorum=<quorum>",
            "decision=<decision>",
            "read-back=CONFIRMED",
            "rationale=<substantive de-identified rationale>");
        Assert.Contains(
            "Eligible counts plus those one-person exclusions",
            session,
            StringComparison.Ordinal);
        Assert.Contains(
            "A mapped need may have at most one disputed-",
            session,
            StringComparison.Ordinal);
        Assert.Contains("basis=DISPUTE —", session, StringComparison.Ordinal);
        Assert.Contains("RECUSALS — total-persons=1; present-persons=1", session, StringComparison.Ordinal);
        Assert.Contains("dispute=<need ID>; category=<same category>", session, StringComparison.Ordinal);
        Assert.Contains("outcome=NOT-RECUSED", session, StringComparison.Ordinal);
        Assert.Contains(
            "Only `RECUSED` or `HELD` in a protected-seat category",
            ledger,
            StringComparison.Ordinal);
        Assert.Contains("outcome=HELD", session, StringComparison.Ordinal);
        Assert.Contains(
            "quorum=NOT-MET — OCF-COUNCIL-TERMS-v1; decision=NONE — quorum not met",
            session,
            StringComparison.Ordinal);
        Assert.Contains(
            "does not put more than half in favor",
            session,
            StringComparison.Ordinal);
        Assert.Contains(
            "cannot appear in the recommendation table",
            session,
            StringComparison.Ordinal);
        Assert.Contains(
            "| Activity withdrawal, council resignation/vacancy, and non-member role-closure dispositions |",
            session,
            StringComparison.Ordinal);
        Assert.Contains("| Repository commit and dirty-tree disposition |", session, StringComparison.Ordinal);
        Assert.Contains("| Build/artifact IDs and SHA-256 values |", session, StringComparison.Ordinal);
        Assert.Contains("| Instrument name, version, and SHA-256 |", session, StringComparison.Ordinal);
        Assert.Contains("| Exact material actually reviewed |", session, StringComparison.Ordinal);
        Assert.Contains("| Natural persons present (count) |", session, StringComparison.Ordinal);
        Assert.Contains(
            "| Eligible total natural persons after matter recusals |",
            session,
            StringComparison.Ordinal);
        Assert.Contains("| H0 freeze-manifest repository path |", manifest, StringComparison.Ordinal);
        Assert.Contains(
            "| Exact-final-byte public-record publication permission reconfirmed after participant review |",
            manifest,
            StringComparison.Ordinal);
        Assert.Contains(
            "CONFIRMED — all corrections resolved",
            manifest,
            StringComparison.Ordinal);
        Assert.Contains(
            "activity-withdrawal=<the exact H0 value>; council-resignation-vacancy=<the exact",
            manifest,
            StringComparison.Ordinal);
        Assert.Contains(
            "field reproduces the final H0 field exactly",
            manifest,
            StringComparison.Ordinal);
        Assert.Contains("`read-back=CONFIRMED`", manifest, StringComparison.Ordinal);
        Assert.Contains(
            "cannot relabel either one as resolved or recommended",
            manifest,
            StringComparison.Ordinal);
        Assert.Contains("withdrawal is a right", terms, StringComparison.Ordinal);
        Assert.Contains("withdrawal is a right, not an", terms, StringComparison.Ordinal);
        Assert.Contains("opt-in choice or a condition of participation", terms, StringComparison.Ordinal);
        Assert.Contains("manifest contains no self-hash", terms, StringComparison.Ordinal);
        Assert.Contains(
            "The amounts described on 29 August remain held pending a corrective proposal and valid first-cohort enactment",
            session,
            StringComparison.Ordinal);
        Assert.Contains(
            "HELD FOR A CORRECTIVE PROPOSAL AND FIRST-COHORT ENACTMENT",
            compensation,
            StringComparison.Ordinal);

        Assert.Contains(
            "The private custodian keeps original cards and wording",
            session,
            StringComparison.Ordinal);
        Assert.Contains(
            "Participant-reviewed de-identified factual paraphrase",
            session,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Council member's words", session, StringComparison.Ordinal);
        Assert.Contains(
            "Council outcome (`RECOMMENDATION RECORDED`, `NO RECOMMENDATION`, or `HOLD`)",
            session,
            StringComparison.Ordinal);
        Assert.Contains(
            "Vote/tally under the enacted procedure, or consensus/no vote",
            session,
            StringComparison.Ordinal);
        Assert.Contains(
            "Read-back confirmation (seat + count, no names by default)",
            session,
            StringComparison.Ordinal);
        Assert.Contains("Applicable seat holds after read-back", session, StringComparison.Ordinal);

        Assert.Contains("field for its own SHA-256", manifest, StringComparison.Ordinal);
        Assert.DoesNotContain("| H0 freeze-manifest SHA-256 |", manifest, StringComparison.Ordinal);
        Assert.Contains("| H0 freeze-manifest SHA-256 |", feasibility, StringComparison.Ordinal);
        Assert.Contains("| Feasibility record SHA-256 |", disposition, StringComparison.Ordinal);
        Assert.DoesNotContain("## Separate feasibility appendix", session, StringComparison.Ordinal);
        Assert.DoesNotContain("| Final H0 record SHA-256 |", session, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "## Product-owner disposition — intentionally blank in the template",
            session,
            StringComparison.Ordinal);
        Assert.Contains("A manifest never contains its own digest", ledger, StringComparison.Ordinal);
        Assert.Contains(
            "ratified as written through its four accountable authority",
            ledger,
            StringComparison.Ordinal);
        Assert.Contains(
            "a human Git-history audit must identify the first",
            ledger,
            StringComparison.Ordinal);
        Assert.Contains(
            "final records and their eight detached freeze manifests",
            ledger,
            StringComparison.Ordinal);
        Assert.Contains(
            "separate authenticator- and participant-conflict dispositions/limitations",
            ledger,
            StringComparison.Ordinal);
        Assert.Contains(
            "activity-specific withdrawal, full council resignation/vacancy, and non-member role closure dispositions distinguished",
            ledger,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Stage_gate_register_contains_every_unique_row_and_keeps_all_rows_open()
    {
        var register = Read("docs", "governance", "stage-gate-disposition-register.md");
        var rows = register
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line => StageGateRow().IsMatch(line))
            .ToArray();
        var identifiers = rows
            .Select(row => row.Split('|', StringSplitOptions.TrimEntries)[1])
            .ToArray();
        var uniqueIdentifiers = new HashSet<string>(identifiers, StringComparer.Ordinal);

        Assert.Equal(44, rows.Length);
        Assert.Equal(rows.Length, uniqueIdentifiers.Count);
        Assert.Equal(9, identifiers.Count(id => id.StartsWith("G0-", StringComparison.Ordinal)));
        Assert.Equal(10, identifiers.Count(id => id.StartsWith("G1-", StringComparison.Ordinal)));
        Assert.Equal(10, identifiers.Count(id => id.StartsWith("G2-", StringComparison.Ordinal)));
        Assert.Equal(9, identifiers.Count(id => id.StartsWith("G3-", StringComparison.Ordinal)));
        Assert.Equal(5, identifiers.Count(id => id.StartsWith("G4-", StringComparison.Ordinal)));
        Assert.Single(identifiers, id => id.StartsWith("G5-", StringComparison.Ordinal));
        Assert.All(
            rows,
            row => Assert.Contains("| **OPEN — ", row, StringComparison.Ordinal));
        Assert.Contains(
            "**Status: CONSOLIDATED WORKING INVENTORY — ALL ROWS OPEN.**",
            register,
            StringComparison.Ordinal);
        Assert.DoesNotContain("single status surface", register, StringComparison.Ordinal);
        Assert.Contains(
            "No Gate 0–5 criterion is closed by this initial register.",
            register,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Human_reviews_are_ordered_and_every_freeze_record_remains_open()
    {
        var ledger = Read("docs", "council", "bounded-commission-review-ledger.md");
        var rows = ledger.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line => ReviewLedgerRow().IsMatch(line))
            .ToArray();

        Assert.Equal(8, rows.Length);
        Assert.Equal(
            ["H0", "H1", "H2", "H3", "H4", "H5", "H6", "H7"],
            rows.Select(row => row.Split('|', StringSplitOptions.TrimEntries)[2]));
        Assert.Equal(
            ["0", "1", "2", "3", "4", "5", "6", "7"],
            rows.Select(row => row.Split('|', StringSplitOptions.TrimEntries)[1]));
        Assert.All(rows, row => Assert.Contains("| **NOT BEGUN** |", row, StringComparison.Ordinal));
        Assert.Contains("NO SESSION CONVENED", ledger, StringComparison.Ordinal);
        Assert.Contains("Only after all eight", ledger, StringComparison.Ordinal);
        Assert.Contains("may the typist", ledger, StringComparison.Ordinal);
        Assert.Contains(
            "not an H0 record",
            ledger,
            StringComparison.Ordinal);
        Assert.Contains(
            "An unranked consultation is never one of those eight records.",
            ledger,
            StringComparison.Ordinal);
        Assert.Contains("Before any downstream use or decision", ledger, StringComparison.Ordinal);
        Assert.Contains(
            "Bounded activity/matter ID and exact start-UTC/end-exclusive-UTC interval",
            ledger,
            StringComparison.Ordinal);
        Assert.Contains("Immediate pre-opening roster/vacancy cutoff UTC", ledger, StringComparison.Ordinal);
        Assert.Contains("opening-check=CONFIRMED", ledger, StringComparison.Ordinal);
        Assert.Contains(
            "Exact constituted-seat authority entries: stable seat/person refs",
            ledger,
            StringComparison.Ordinal);
        Assert.Contains(
            "One-calendar-year/29-Feb term validation and exact whole-activity/matter-interval authority coverage",
            ledger,
            StringComparison.Ordinal);
        Assert.Contains("effective-utc <= start-utc", ledger, StringComparison.Ordinal);
        Assert.Contains("expiry-exclusive-utc >= end-exclusive-utc", ledger, StringComparison.Ordinal);
        Assert.Contains("No stale roster or denominator carries forward.", ledger, StringComparison.Ordinal);
        Assert.Contains(
            "Disputed-recusal subrecords before affected council matters",
            ledger,
            StringComparison.Ordinal);
        Assert.Contains("read-back=CONFIRMED", ledger, StringComparison.Ordinal);
        Assert.Contains("affected-person-practicing-educator=YES|NO", ledger, StringComparison.Ordinal);
        Assert.Contains("excluded-total=1; excluded-present=1", ledger, StringComparison.Ordinal);
        AssertOrdered(
            ledger,
            "affected-person-excluded=YES",
            "affected-person-practicing-educator=YES|NO",
            "excluded-total=1",
            "excluded-present=1",
            "excluded-practicing-educators=0|1",
            "outcome=<outcome>",
            "eligible-total=<non-negative count>",
            "eligible-present=<non-negative count>",
            "practicing-educators=<non-negative count>",
            "quorum=<quorum>",
            "decision=<decision>",
            "read-back=CONFIRMED",
            "rationale=<substantive de-identified");
        Assert.Contains("The affected present person is the sole exclusion", ledger, StringComparison.Ordinal);
        Assert.Contains("basis=DISPUTE —", ledger, StringComparison.Ordinal);
        Assert.Contains("RECUSALS — total-persons=1; present-persons=1", ledger, StringComparison.Ordinal);
        Assert.Contains("NONE — dispute=<matter ID>", ledger, StringComparison.Ordinal);
        Assert.Contains(
            "the matter stays held until a revised",
            ledger,
            StringComparison.Ordinal);
        Assert.Contains("record grammar and validator can represent both", ledger, StringComparison.Ordinal);
        Assert.Contains(
            "decision=NONE — quorum not met",
            ledger,
            StringComparison.Ordinal);
        Assert.Contains(
            "independent protected-seat review; no council recusal",
            ledger,
            StringComparison.Ordinal);
        Assert.Contains("mechanical review; no council recusal decision", ledger, StringComparison.Ordinal);
        Assert.Contains("H7 participant activity; no council recusal decision", ledger, StringComparison.Ordinal);
        Assert.Contains(
            "aggregate/root binding; no council recusal decision",
            ledger,
            StringComparison.Ordinal);
        Assert.Contains(
            "H7 aggregate/root binding; no constituted council-seat authority exercised",
            ledger,
            StringComparison.Ordinal);
        Assert.Contains(
            "manifest reproduces the final record's exact interval",
            ledger,
            StringComparison.Ordinal);
        Assert.Contains(
            "entire disputed-recusal field byte-for-byte",
            ledger,
            StringComparison.Ordinal);
        Assert.Contains("roster-kind=<COUNCIL", ledger, StringComparison.Ordinal);
        Assert.Contains(
            "effective UTC, exclusive-expiry UTC, and whole-interval coverage",
            ledger,
            StringComparison.Ordinal);
        Assert.Equal(
            3,
            ledger.Split(
                "Exact operating-terms control ID, version, effective date",
                StringSplitOptions.None).Length);
        Assert.Equal(
            3,
            ledger.Split(
                "Exact compensation control ID, version, effective date",
                StringSplitOptions.None).Length);
        Assert.Contains(
            "A collapsed “choices recorded”",
            ledger,
            StringComparison.Ordinal);
        Assert.Contains(
            "statement is invalid.",
            ledger,
            StringComparison.Ordinal);
        Assert.Contains(
            "— independent protected-seat review; no council recommendation",
            ledger,
            StringComparison.Ordinal);
        Assert.Contains("### H7 session, aggregate, and council-record topology", ledger, StringComparison.Ordinal);
        Assert.Contains("they never count toward", ledger, StringComparison.Ordinal);
        Assert.Contains("Selecting an older commit cannot hide a known later event", ledger, StringComparison.Ordinal);
        Assert.DoesNotContain("SESSION HELD", ledger, StringComparison.Ordinal);
        Assert.DoesNotContain("COUNCIL RECORD FROZEN", ledger, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_relative_link_in_the_governance_packet_resolves()
    {
        var root = RepositoryRoot();

        foreach (var relativeDocument in GovernedDocuments)
        {
            var absoluteDocument = Path.Combine(root, relativeDocument);
            Assert.True(File.Exists(absoluteDocument), $"Governed document is missing: {relativeDocument}");

            var directory = Path.GetDirectoryName(absoluteDocument)
                ?? throw new InvalidOperationException($"Document has no parent directory: {relativeDocument}");
            var markdown = File.ReadAllText(absoluteDocument);
            foreach (Match match in MarkdownLink().Matches(markdown))
            {
                var target = match.Groups["target"].Value;
                if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                    || target.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                    || target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
                    || target.StartsWith('#'))
                {
                    continue;
                }

                var fragmentIndex = target.IndexOf('#');
                var pathPart = fragmentIndex >= 0 ? target[..fragmentIndex] : target;
                var resolved = Path.GetFullPath(Path.Combine(directory, pathPart));

                Assert.True(
                    File.Exists(resolved) || Directory.Exists(resolved),
                    $"{relativeDocument} has an unresolved relative link: {target}");
            }
        }
    }

    private static string Read(params string[] relativePath)
        => File.ReadAllText(Path.Combine([RepositoryRoot(), .. relativePath]));

    private static void AssertOrdered(string text, params string[] fragments)
    {
        var cursor = 0;
        foreach (var fragment in fragments)
        {
            var index = text.IndexOf(fragment, cursor, StringComparison.Ordinal);
            Assert.True(index >= 0, $"Expected ordered fragment was not found: {fragment}");
            cursor = index + fragment.Length;
        }
    }

    [GeneratedRegex(@"^\| G[0-5]-\d{2} \|", RegexOptions.CultureInvariant)]
    private static partial Regex StageGateRow();

    [GeneratedRegex(@"^\| [0-7] \| H[0-7] \|", RegexOptions.CultureInvariant)]
    private static partial Regex ReviewLedgerRow();

    [GeneratedRegex(@"\[[^\]]+\]\((?<target>[^)]+)\)", RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownLink();

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpenClassroomFoundry.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root for governance-document tests.");
    }
}
