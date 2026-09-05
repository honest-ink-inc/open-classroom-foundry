// SPDX-License-Identifier: GPL-3.0-or-later
using System.Security.Cryptography;
using System.Text;
using Foundry.Tools.AtlasCouncilRecords;

namespace Foundry.Tests.Unit;

public class AtlasCouncilRecordValidatorTests
{
    private const string RecordFile = "atlas-priority-session-2030-01-02.md";
    private const string ManifestFile = "atlas-priority-session-2030-01-02-freeze-manifest.md";
    private const string FeasibilityFile = "atlas-priority-session-2030-01-02-feasibility-v1.md";
    private const string DispositionFile = "atlas-priority-session-2030-01-02-disposition-v1.md";
    private const string SeatAuthorityField =
        "Constituted-seat authority entries (stable seat/person refs, presence, scope, term, qualification-basis category, and private custodian reference)";
    private const string SeatAuthorityEntryP01 =
        "CONSTITUTED — seat=general educator; person-ref=P-01; person-count=1; presence=PRESENT; practicing-educator=YES; appointing-authority=synthetic product owner; effective-utc=2029-01-03T00:00:00Z; expiry-exclusive-utc=2030-01-03T00:00:00Z; scope=synthetic general-educator review; acceptance-record=REC-P-01; qualification-basis=general educator; private-custodian=CUST-P-01";
    private const string SeatAuthorityEntryP02 =
        "CONSTITUTED — seat=general educator; person-ref=P-02; person-count=1; presence=PRESENT; practicing-educator=YES; appointing-authority=synthetic product owner; effective-utc=2029-01-03T00:00:00Z; expiry-exclusive-utc=2030-01-03T00:00:00Z; scope=synthetic general-educator review; acceptance-record=REC-P-02; qualification-basis=general educator; private-custodian=CUST-P-02";
    private const string SeatAuthorityEntryP03 =
        "CONSTITUTED — seat=general educator; person-ref=P-03; person-count=1; presence=PRESENT; practicing-educator=YES; appointing-authority=synthetic product owner; effective-utc=2029-01-03T00:00:00Z; expiry-exclusive-utc=2030-01-03T00:00:00Z; scope=synthetic general-educator review; acceptance-record=REC-P-03; qualification-basis=general educator; private-custodian=CUST-P-03";
    private const string CurriculumSeatAuthorityEntryP01 =
        "CONSTITUTED — seat=curriculum; person-ref=P-01; person-count=1; presence=PRESENT; practicing-educator=YES; appointing-authority=synthetic product owner; effective-utc=2029-01-03T00:00:00Z; expiry-exclusive-utc=2030-01-03T00:00:00Z; scope=synthetic curriculum review; acceptance-record=REC-P-01-CURRICULUM; qualification-basis=curriculum; private-custodian=CUST-P-01";
    private const string MarkdownEntrySeparator = " \\|\\| ";
    private const string CanonicalSeatAuthority =
        SeatAuthorityEntryP01 + MarkdownEntrySeparator + SeatAuthorityEntryP02 + MarkdownEntrySeparator + SeatAuthorityEntryP03;
    private const string CanonicalRecusalDispute =
        "DISPUTE — matter=N-SYNTHETIC; category=financial; affected-person-excluded=YES; affected-person-practicing-educator=YES; excluded-total=1; excluded-present=1; excluded-practicing-educators=1; outcome=RECUSED; eligible-total=3; eligible-present=3; practicing-educators=2; quorum=MET — OCF-COUNCIL-TERMS-v1; decision=CONSENSUS — denominator=3; read-back=CONFIRMED; rationale=bounded finding";
    private const string CanonicalNoQuorumHeldRecusalDispute =
        "DISPUTE — matter=N-SYNTHETIC; category=financial; affected-person-excluded=YES; affected-person-practicing-educator=YES; excluded-total=1; excluded-present=1; excluded-practicing-educators=1; outcome=HELD; eligible-total=2; eligible-present=2; practicing-educators=2; quorum=NOT-MET — OCF-COUNCIL-TERMS-v1; decision=NONE — quorum not met; read-back=CONFIRMED; rationale=recusal dispute remains held without quorum";
    private const string CanonicalFailedVoteHeldRecusalDispute =
        "DISPUTE — matter=N-SYNTHETIC; category=financial; affected-person-excluded=YES; affected-person-practicing-educator=YES; excluded-total=1; excluded-present=1; excluded-practicing-educators=1; outcome=HELD; eligible-total=3; eligible-present=3; practicing-educators=2; quorum=MET — OCF-COUNCIL-TERMS-v1; decision=VOTE — for=1,against=1,abstain=1,denominator=3; read-back=CONFIRMED; rationale=recusal dispute remains held after vote without majority";
    private const string CanonicalNotRecusedDispute =
        "DISPUTE — matter=N-SYNTHETIC; category=financial; affected-person-excluded=YES; affected-person-practicing-educator=YES; excluded-total=1; excluded-present=1; excluded-practicing-educators=1; outcome=NOT-RECUSED; eligible-total=3; eligible-present=3; practicing-educators=2; quorum=MET — OCF-COUNCIL-TERMS-v1; decision=CONSENSUS — denominator=3; read-back=CONFIRMED; rationale=bounded finding";
    private const string CanonicalRecusedRecommendationConflict =
        "RECUSALS — total-persons=1; present-persons=1; practicing-educators=1; basis=DISPUTE — matter=N-SYNTHETIC,category=financial,outcome=RECUSED";
    private const string CanonicalNotRecusedRecommendationConflict =
        "NONE — dispute=N-SYNTHETIC; category=financial; outcome=NOT-RECUSED";
    private const string CanonicalRecommendationRow =
        "| 1 | N-SYNTHETIC · Synthetic possibility | Synthetic rationale | Synthetic proof request | 3 | 3 | 3 | NONE — no affected conflict or recusal | MET — OCF-COUNCIL-TERMS-v1 — after recusals | CONSENSUS — denominator=3 | AAC/SLP hold retained | None recorded |";
    private static readonly string SyntheticBuildArtifact =
        $"synthetic build artifact · sha256:{new string('1', 64)}";
    private static readonly string DifferentSyntheticBuildArtifact =
        $"different build artifact · sha256:{new string('3', 64)}";
    private static readonly string SyntheticInstrument =
        $"Atlas H0 synthetic v1 · sha256:{new string('2', 64)}";

    [Fact]
    public void Boundary_expressly_refuses_every_human_or_protected_inference()
    {
        Assert.Equal(
            "This validator checks record mechanics, including count arithmetic for the one supported OCF-COUNCIL-TERMS-v1 rule. It does not authenticate supplied control digests against repository bytes, authenticate roster or attendance facts, declare that human quorum occurred, score needs, rank or recommend possibilities, select a priority, or perform a protected-seat act. Whether a public-credit identity may appear remains a human confirmation outside this validator.",
            AtlasCouncilRecordValidator.AuthorityBoundary);
    }

    [Fact]
    public void Unrun_record_accepts_only_placeholders()
    {
        var valid = AtlasCouncilRecordValidator.Validate(
            RecordFile,
            SyntheticRecord(AtlasCouncilRecordValidator.UnrunStatus));

        Assert.True(valid.IsValid, Describe(valid));
        Assert.Equal(AtlasCouncilRecordStatus.Unrun, valid.Status);

        var claimedSession = SyntheticRecord(AtlasCouncilRecordValidator.UnrunStatus)
            .Replace(
                "| Session date and duration | [not run] |",
                "| Session date and duration | 2030-01-02 · 60 minutes |",
                StringComparison.Ordinal);
        var refusal = AtlasCouncilRecordValidator.Validate(RecordFile, claimedSession);

        Assert.Contains(refusal.Issues, issue => issue.Code == "atlas.status.session-mismatch");
    }

    [Fact]
    public void Session_record_accepts_a_complete_needs_first_recommendation()
    {
        var result = AtlasCouncilRecordValidator.Validate(
            RecordFile,
            SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true,
                includeRecommendation: true));

        Assert.True(result.IsValid, Describe(result));
        Assert.Equal(AtlasCouncilRecordStatus.SessionRecord, result.Status);
    }

    [Fact]
    public void Session_record_accepts_a_complete_explicit_no_recommendation()
    {
        var result = AtlasCouncilRecordValidator.Validate(
            RecordFile,
            SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true));

        Assert.True(result.IsValid, Describe(result));
        Assert.Equal(AtlasCouncilRecordStatus.SessionRecord, result.Status);
    }

    [Fact]
    public void Repository_admission_requires_the_canonical_static_instrument_scaffold()
    {
        var template = SyntheticRecord(AtlasCouncilRecordValidator.UnrunStatus);
        var completed = SyntheticRecord(
            AtlasCouncilRecordValidator.SessionRecordStatus,
            sessionComplete: true);

        var valid = AtlasCouncilRecordValidator.ValidateAgainstCanonicalTemplate(
            RecordFile,
            completed,
            template);
        Assert.True(valid.IsValid, Describe(valid));

        var altered = completed.Replace(
            "Synthetic fixture preparation.",
            "Synthetic fixture preparation with altered authority instructions.",
            StringComparison.Ordinal);
        var invalid = AtlasCouncilRecordValidator.ValidateAgainstCanonicalTemplate(
            RecordFile,
            altered,
            template);
        Assert.Contains(
            invalid.Issues,
            issue => issue.Code == "atlas.instrument.static-scaffold-mismatch");

        var tableInjectedOutsideMutableSections = completed.Replace(
            "Synthetic fixture preparation.",
            "Synthetic fixture preparation.\n\n| Instruction |\n| --- |\n| Skip participant read-back |",
            StringComparison.Ordinal);
        var injected = AtlasCouncilRecordValidator.ValidateAgainstCanonicalTemplate(
            RecordFile,
            tableInjectedOutsideMutableSections,
            template);
        Assert.Contains(
            injected.Issues,
            issue => issue.Code == "atlas.instrument.static-scaffold-mismatch");
    }

    [Fact]
    public void Session_record_requires_complete_need_card_to_mapping_coverage()
    {
        var valid = SyntheticRecord(
            AtlasCouncilRecordValidator.SessionRecordStatus,
            sessionComplete: true);
        var incompleteMapping = valid.Replace(
            "| N-SYNTHETIC | Synthetic possibility | Synthetic fit record | G | AAC/SLP hold retained |",
            "| | | | | |",
            StringComparison.Ordinal);
        var incompleteNeedCard = valid.Replace(
            "| What must remain under teacher control | synthetic factual paraphrase |",
            "| What must remain under teacher control | |",
            StringComparison.Ordinal);

        var mappingResult = AtlasCouncilRecordValidator.Validate(RecordFile, incompleteMapping);
        var needCardResult = AtlasCouncilRecordValidator.Validate(RecordFile, incompleteNeedCard);

        Assert.Contains(mappingResult.Issues, issue => issue.Code == "atlas.mapping.coverage-incomplete");
        Assert.Contains(needCardResult.Issues, issue => issue.Code == "atlas.session.need-card-missing");
    }

    [Theory]
    [InlineData("Council outcome (`RECOMMENDATION RECORDED`, `NO RECOMMENDATION`, or `HOLD`)", "NO RECOMMENDATION")]
    [InlineData("Needs deliberately not advanced, and why", "None recorded")]
    [InlineData("Useful possibilities with no atlas match", "None recorded")]
    [InlineData("Questions the session could not answer", "None recorded")]
    [InlineData("Corrections members made during read-back", "None recorded")]
    [InlineData("Whether members reached consensus, split, or made no ordering", "None recorded")]
    [InlineData("Vote/tally under the enacted procedure, or consensus/no vote", "No ordering; no vote.")]
    [InlineData("Read-back confirmation (seat + count, no names by default)", "general educator: 3")]
    [InlineData("Applicable seat holds after read-back", "AAC/SLP hold retained")]
    public void Session_record_requires_every_substantive_completion_field(string field, string value)
    {
        var valid = SyntheticRecord(
            AtlasCouncilRecordValidator.SessionRecordStatus,
            sessionComplete: true);
        var incomplete = valid.Replace(
            $"- **{field}:** {value}",
            $"- **{field}:**",
            StringComparison.Ordinal);

        Assert.NotEqual(valid, incomplete);
        var result = AtlasCouncilRecordValidator.Validate(RecordFile, incomplete);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "atlas.recommendation.supplemental-field-pending");
    }

    [Theory]
    [InlineData("Enacted operating-terms version and effective date", "ENACTED — OCF-COUNCIL-TERMS-v1 — 2030-01-01")]
    [InlineData("Documentation/original-printable content license and accountable decision record", "CHOSEN — Synthetic content license — REC-1")]
    [InlineData(SeatAuthorityField, CanonicalSeatAuthority)]
    [InlineData("Participation consent recorded separately", "ACCEPTED — general educator: 3")]
    [InlineData("Session-opening general quorum result before matter-specific recusals", "MET — OCF-COUNCIL-TERMS-v1 — before matter-specific recusals")]
    [InlineData("Conflict categories and recusals (de-identified by default)", "NONE — no conflict or recusal")]
    [InlineData("Disputed-recusal resolution subrecords before affected matters, or NONE", "NONE — no disputed recusal")]
    [InlineData("Multi-capacity disclosures", "NONE — one constituted capacity per natural person")]
    [InlineData("Applicable protected seats vacant, absent, or recused; each marked NOT REVIEWED — HELD", "HELD — AAC/SLP — NOT REVIEWED — HELD")]
    [InlineData("Private compensation-ledger attestation for rate, UTC quarter, cap reservation, and district-time status", "ATTESTED — private-ledger-ref=COMP-REC-1; rate=VERIFIED; utc-quarter-cap-reservation=VERIFIED; district-time-status=VERIFIED")]
    [InlineData("Content-contribution choice and exact license/control identity, or none", "NONE — general educator: 3")]
    [InlineData("Role-acceptance choice and exact bounded role/control identity, or none", "ACCEPTED — general educator: 3 — OCF-COUNCIL-TERMS-v1")]
    [InlineData("Maintainer-appointment choice and exact role/control identity, or none", "NONE — general educator: 3")]
    [InlineData("Copyright-stewardship choice and exact transfer/control identity, or none", "NONE — general educator: 3")]
    [InlineData("Activity withdrawal, council resignation/vacancy, and non-member role-closure dispositions", "RESOLVED — activity-withdrawal=NONE; council-resignation-vacancy=NONE; non-member-role-closure=NOT-APPLICABLE-H0; unresolved=NONE")]
    public void Session_record_requires_each_governance_and_separate_choice_field(string field, string value)
    {
        var valid = SyntheticRecord(
            AtlasCouncilRecordValidator.SessionRecordStatus,
            sessionComplete: true);
        var incomplete = valid.Replace(
            $"| {field} | {value} |",
            $"| {field} | [not run] |",
            StringComparison.Ordinal);

        Assert.NotEqual(valid, incomplete);
        var result = AtlasCouncilRecordValidator.Validate(RecordFile, incomplete);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.session.field-pending");
    }

    [Theory]
    [InlineData("RESOLVED — activity-withdrawal=NONE; council-resignation-vacancy=NONE; unresolved=NONE")]
    [InlineData("RESOLVED — activity-withdrawal=NONE; council-resignation-vacancy=NONE; non-member-role-closure=NOT-APPLICABLE-H0; unresolved=HOLD")]
    [InlineData("RESOLVED — activity-withdrawal=NONE; council-resignation-vacancy=NONE; non-member-role-closure=NONE; unresolved=NONE")]
    public void Session_record_refuses_malformed_or_unresolved_withdrawal_dispositions(
        string disposition)
    {
        var record = ReplaceRequired(
            SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true),
            "| Activity withdrawal, council resignation/vacancy, and non-member role-closure dispositions | RESOLVED — activity-withdrawal=NONE; council-resignation-vacancy=NONE; non-member-role-closure=NOT-APPLICABLE-H0; unresolved=NONE |",
            $"| Activity withdrawal, council resignation/vacancy, and non-member role-closure dispositions | {disposition} |");

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "atlas.session.withdrawal-disposition-invalid");
    }

    [Theory]
    [InlineData(
        "Content-contribution choice and exact license/control identity, or none",
        "NONE — general educator: 3",
        "NONE — general educator: 2",
        "atlas.session.content-contribution-choice-invalid")]
    [InlineData(
        "Role-acceptance choice and exact bounded role/control identity, or none",
        "ACCEPTED — general educator: 3 — OCF-COUNCIL-TERMS-v1",
        "ACCEPTED — general educator: 3 — OCF-COUNCIL-TERMS-v2",
        "atlas.session.role-acceptance-choice-invalid")]
    [InlineData(
        "Maintainer-appointment choice and exact role/control identity, or none",
        "NONE — general educator: 3",
        "APPOINTED — general educator: 3 — MAINTAINER-1",
        "atlas.session.maintainer-appointment-choice-invalid")]
    [InlineData(
        "Copyright-stewardship choice and exact transfer/control identity, or none",
        "NONE — general educator: 3",
        "TRANSFERRED — general educator: 3 — STEWARD-1",
        "atlas.session.copyright-stewardship-choice-invalid")]
    public void Session_record_requires_exact_separate_choice_dispositions(
        string field,
        string validValue,
        string invalidValue,
        string issueCode)
    {
        var record = ReplaceRequired(
            SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true),
            $"| {field} | {validValue} |",
            $"| {field} | {invalidValue} |");

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(result.Issues, issue => issue.Code == issueCode);
    }

    [Fact]
    public void Session_record_refuses_unenacted_operating_terms()
    {
        var record = SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true)
            .Replace(
                "| Enacted operating-terms version and effective date | ENACTED — OCF-COUNCIL-TERMS-v1 — 2030-01-01 |",
                "| Enacted operating-terms version and effective date | NOT ENACTED |",
                StringComparison.Ordinal);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.session.operating-terms-unenacted");
    }

    [Fact]
    public void Session_record_refuses_an_unsupported_operating_terms_rule_id()
    {
        var record = ReplaceRequired(
            SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true,
                includeRecommendation: true),
            "OCF-COUNCIL-TERMS-v1",
            "OCF-COUNCIL-TERMS-v2");

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "atlas.session.operating-terms-unsupported");
    }

    [Theory]
    [InlineData(6, 3, 3)]
    [InlineData(3, 2, 2)]
    [InlineData(3, 3, 1)]
    public void Session_opening_quorum_enforces_majority_person_and_educator_floors(
        int total,
        int present,
        int educators)
    {
        var record = WithSessionCounts(
            SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true),
            total,
            present,
            educators);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "atlas.session.quorum-arithmetic-invalid");
    }

    [Fact]
    public void Session_opening_quorum_accepts_the_exact_person_and_educator_floors()
    {
        var record = WithSessionCounts(
            SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true),
            total: 3,
            present: 3,
            educators: 2);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.True(result.IsValid, Describe(result));
    }

    [Theory]
    [InlineData(
        "| Enacted operating-terms version and effective date | ENACTED — OCF-COUNCIL-TERMS-v1 — 2030-01-01 |",
        "| Enacted operating-terms version and effective date | ENACTED — no terms — 2030-01-01 |",
        "atlas.session.operating-terms-unenacted")]
    [InlineData(
        "| Documentation/original-printable content license and accountable decision record | CHOSEN — Synthetic content license — REC-1 |",
        "| Documentation/original-printable content license and accountable decision record | CHOSEN — no license — no accountable record |",
        "atlas.session.content-license-reference-invalid")]
    [InlineData(
        "| Documentation/original-printable content license and accountable decision record | CHOSEN — Synthetic content license — REC-1 |",
        "| Documentation/original-printable content license and accountable decision record | CHOSEN — record states no license — REC-1 |",
        "atlas.session.content-license-reference-invalid")]
    [InlineData(
        "| Operative compensation-policy version and effective date; election recorded | RECORDED — OCF-COMPENSATION-v1 — 2030-01-01 — general educator: 3 |",
        "| Operative compensation-policy version and effective date; election recorded | RECORDED — no policy — 2030-01-01 — general educator: 3 |",
        "atlas.session.compensation-election-invalid")]
    [InlineData(
        "| Operative compensation-policy version and effective date; election recorded | RECORDED — OCF-COMPENSATION-v1 — 2030-01-01 — general educator: 3 |",
        "| Operative compensation-policy version and effective date; election recorded | RECORDED — record states no policy — 2030-01-01 — general educator: 3 |",
        "atlas.session.compensation-election-invalid")]
    public void Session_record_refuses_negated_governance_references(
        string validField,
        string negatedField,
        string issueCode)
    {
        var valid = SyntheticRecord(
            AtlasCouncilRecordValidator.SessionRecordStatus,
            sessionComplete: true);
        var record = valid.Replace(validField, negatedField, StringComparison.Ordinal);

        Assert.NotEqual(valid, record);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(result.Issues, issue => issue.Code == issueCode);
    }

    [Theory]
    [InlineData(
        "path=docs/council/draft-first-cohort-operating-terms.md",
        "path=docs/council/other-terms.md",
        "atlas.session.operating-terms-binding-invalid")]
    [InlineData(
        "path=docs/council/compensation-policy.md",
        "path=docs/council/other-policy.md",
        "atlas.session.compensation-binding-invalid")]
    [InlineData(
        "bytes=1234",
        "bytes=0",
        "atlas.session.operating-terms-binding-invalid")]
    public void Session_record_requires_exact_hashed_control_file_bindings(
        string validToken,
        string invalidToken,
        string issueCode)
    {
        var valid = SyntheticRecord(
            AtlasCouncilRecordValidator.SessionRecordStatus,
            sessionComplete: true);
        var record = valid.Replace(validToken, invalidToken, StringComparison.Ordinal);

        Assert.NotEqual(valid, record);
        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(result.Issues, issue => issue.Code == issueCode);
    }

    [Fact]
    public void Session_record_refuses_generic_tokens_in_identity_hash_and_seat_authority_fields()
    {
        var valid = SyntheticRecord(
            AtlasCouncilRecordValidator.SessionRecordStatus,
            sessionComplete: true);
        var record = valid
            .Replace("| H0 record ID and version | H0-SYNTHETIC v1 |", "| H0 record ID and version | x |", StringComparison.Ordinal)
            .Replace($"| Build/artifact IDs and SHA-256 values | {SyntheticBuildArtifact} |", "| Build/artifact IDs and SHA-256 values | x |", StringComparison.Ordinal);
        record = WithSeatAuthority(record, "CONSTITUTED — x");

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.session.record-identity-invalid");
        Assert.Contains(result.Issues, issue => issue.Code == "atlas.session.build-artifacts-invalid");
        Assert.Contains(result.Issues, issue => issue.Code == "atlas.session.seat-authority-incomplete");
    }

    [Fact]
    public void Session_record_refuses_negated_placeholders_in_seat_authority_components()
    {
        var valid = SyntheticRecord(
            AtlasCouncilRecordValidator.SessionRecordStatus,
            sessionComplete: true);
        var record = ReplaceRequired(
            valid,
            "appointing-authority=synthetic product owner",
            "appointing-authority=no authority");

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.session.seat-authority-incomplete");
    }

    [Fact]
    public void Session_record_refuses_embedded_negation_in_seat_authority_components()
    {
        var valid = SyntheticRecord(
            AtlasCouncilRecordValidator.SessionRecordStatus,
            sessionComplete: true);
        var record = valid.Replace(
            "appointing-authority=synthetic product owner",
            "appointing-authority=record says no authority",
            StringComparison.Ordinal);

        Assert.NotEqual(valid, record);
        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.session.seat-authority-incomplete");
    }

    [Theory]
    [InlineData("CONSTITUTED — seat=general educator; person-ref=P-01")]
    [InlineData(SeatAuthorityEntryP01 + MarkdownEntrySeparator + SeatAuthorityEntryP01)]
    public void Session_record_refuses_malformed_or_duplicate_seat_authority_assignments(
        string authority)
    {
        var record = WithSeatAuthority(
            SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true),
            authority);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.session.seat-authority-incomplete");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("2")]
    public void Session_record_requires_one_natural_person_per_constituted_assignment(
        string personCount)
    {
        var authority = ReplaceRequired(
            CanonicalSeatAuthority,
            "person-count=1",
            $"person-count={personCount}");
        var record = WithSeatAuthority(
            SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true),
            authority);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.session.seat-authority-incomplete");
    }

    [Theory]
    [InlineData(
        "| Total seated, non-vacant natural persons (count) | 3 |",
        "| Total seated, non-vacant natural persons (count) | 4 |",
        "atlas.session.seat-authority-total-person-mismatch")]
    [InlineData(
        "| Natural persons present (count) | 3 |",
        "| Natural persons present (count) | 2 |",
        "atlas.session.seat-authority-present-person-mismatch")]
    [InlineData(
        "| Practicing-educator natural persons present (count) | 3 |",
        "| Practicing-educator natural persons present (count) | 2 |",
        "atlas.session.seat-authority-educator-count-mismatch")]
    [InlineData(
        "| Seats present (seat + count, no names by default) | general educator: 3 |",
        "| Seats present (seat + count, no names by default) | general educator: 2 |",
        "atlas.session.seat-authority-present-count-mismatch")]
    public void Session_record_reconciles_constituted_authority_to_session_counts(
        string validField,
        string mismatchedField,
        string issueCode)
    {
        var record = ReplaceRequired(
            SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true),
            validField,
            mismatchedField);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(result.Issues, issue => issue.Code == issueCode);
    }

    [Theory]
    [InlineData("presence=PRESENT", "presence=ABSENT")]
    [InlineData("practicing-educator=YES", "practicing-educator=NO")]
    public void Session_record_refuses_inconsistent_person_facts_across_constituted_seats(
        string validFact,
        string inconsistentFact)
    {
        var inconsistentSecondCapacity = ReplaceRequired(
            CurriculumSeatAuthorityEntryP01,
            validFact,
            inconsistentFact);
        var record = WithSeatAuthority(
            SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true),
            $"{CanonicalSeatAuthority}{MarkdownEntrySeparator}{inconsistentSecondCapacity}");

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.session.seat-authority-incomplete");
    }

    [Fact]
    public void Session_record_refuses_an_invalid_authority_term_instant()
    {
        var authority = ReplaceRequired(
            CanonicalSeatAuthority,
            "effective-utc=2029-01-03T00:00:00Z",
            "effective-utc=not-a-utc-instant");
        var record = WithSeatAuthority(
            SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true),
            authority);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.session.seat-authority-incomplete");
    }

    [Theory]
    [InlineData("2030-01-02T00:00:01Z", "2031-01-02T00:00:01Z")]
    [InlineData("2028-12-31T00:00:00Z", "2029-12-31T00:00:00Z")]
    [InlineData("2029-01-02T23:59:59Z", "2030-01-02T23:59:59Z")]
    public void Session_record_requires_effective_authority_terms_covering_the_whole_session_day(
        string effective,
        string expiryExclusive)
    {
        var authority = ReplaceRequired(
            CanonicalSeatAuthority,
            "effective-utc=2029-01-03T00:00:00Z",
            $"effective-utc={effective}");
        authority = ReplaceRequired(
            authority,
            "expiry-exclusive-utc=2030-01-03T00:00:00Z",
            $"expiry-exclusive-utc={expiryExclusive}");
        var record = WithSeatAuthority(
            SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true),
            authority);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.session.seat-authority-incomplete");
    }

    [Fact]
    public void Session_record_refuses_a_two_year_constituted_seat_term()
    {
        var authority = ReplaceRequired(
            CanonicalSeatAuthority,
            "expiry-exclusive-utc=2030-01-03T00:00:00Z",
            "expiry-exclusive-utc=2031-01-03T00:00:00Z");
        var record = WithSeatAuthority(
            SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true),
            authority);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.session.seat-authority-incomplete");
    }

    [Theory]
    [InlineData("NONE — no absent seat")]
    [InlineData("ABSENT — general educator: 1")]
    public void Session_record_reconciles_absent_seats_to_constituted_authority(string absentSeats)
    {
        var record = ReplaceRequired(
            SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true),
            "| Seats absent | NONE — no absent constituted seat |",
            $"| Seats absent | {absentSeats} |");

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "atlas.session.seat-authority-absent-count-mismatch");
    }

    [Fact]
    public void Session_record_accepts_exact_multi_capacity_authority_reconciliation()
    {
        var record = SyntheticMultiCapacityRecord();

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.True(result.IsValid, Describe(result));
    }

    [Theory]
    [InlineData("NONE — one constituted capacity per natural person")]
    [InlineData("DISCLOSED — person-ref=P-01; seats=general educator + curriculum")]
    [InlineData("DISCLOSED — person-ref=P-02; seats=curriculum + general educator")]
    public void Session_record_requires_exact_canonical_multi_capacity_reconciliation(
        string disclosure)
    {
        var record = ReplaceRequired(
            SyntheticMultiCapacityRecord(),
            "| Multi-capacity disclosures | DISCLOSED — person-ref=P-01; seats=curriculum + general educator |",
            $"| Multi-capacity disclosures | {disclosure} |");

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "atlas.session.seat-authority-multi-capacity-mismatch");
    }

    [Fact]
    public void Session_record_accepts_a_canonical_disputed_recusal_resolution()
    {
        var session = WithSessionCounts(
            SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true),
            total: 4,
            present: 4,
            educators: 3);
        var record = ReplaceRequired(
            session,
            "| Disputed-recusal resolution subrecords before affected matters, or NONE | NONE — no disputed recusal |",
            $"| Disputed-recusal resolution subrecords before affected matters, or NONE | {CanonicalRecusalDispute} |");

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.True(result.IsValid, Describe(result));
    }

    [Theory]
    [InlineData("affected-person-excluded=YES", "affected-person-excluded=NO")]
    [InlineData("eligible-total=3", "eligible-total=6")]
    [InlineData("eligible-present=3", "eligible-present=2")]
    [InlineData("practicing-educators=2", "practicing-educators=1")]
    [InlineData("decision=CONSENSUS — denominator=3", "decision=CONSENSUS — denominator=2")]
    [InlineData("decision=CONSENSUS — denominator=3", "decision=VOTE — for=1,against=1,abstain=1,denominator=3")]
    public void Session_record_refuses_malformed_disputed_recusal_arithmetic_or_denominators(
        string validToken,
        string invalidToken)
    {
        var malformedDispute = ReplaceRequired(
            CanonicalRecusalDispute,
            validToken,
            invalidToken);
        var record = ReplaceRequired(
            WithSessionCounts(
                SyntheticRecord(
                    AtlasCouncilRecordValidator.SessionRecordStatus,
                    sessionComplete: true),
                total: 4,
                present: 4,
                educators: 3),
            "| Disputed-recusal resolution subrecords before affected matters, or NONE | NONE — no disputed recusal |",
            $"| Disputed-recusal resolution subrecords before affected matters, or NONE | {malformedDispute} |");

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "atlas.session.recusal-dispute-record-invalid");
    }

    [Fact]
    public void Session_record_refuses_an_unmapped_disputed_recusal_matter()
    {
        var dispute = ReplaceRequired(
            CanonicalRecusalDispute,
            "matter=N-SYNTHETIC",
            "matter=N-UNMAPPED");
        var record = ReplaceRequired(
            WithSessionCounts(
                SyntheticRecord(
                    AtlasCouncilRecordValidator.SessionRecordStatus,
                    sessionComplete: true),
                total: 4,
                present: 4,
                educators: 3),
            "| Disputed-recusal resolution subrecords before affected matters, or NONE | NONE — no disputed recusal |",
            $"| Disputed-recusal resolution subrecords before affected matters, or NONE | {dispute} |");

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "atlas.session.recusal-dispute-matter-unmapped");
    }

    [Fact]
    public void Session_record_refuses_duplicate_disputed_recusal_matters()
    {
        var record = ReplaceRequired(
            WithSessionCounts(
                SyntheticRecord(
                    AtlasCouncilRecordValidator.SessionRecordStatus,
                    sessionComplete: true),
                total: 4,
                present: 4,
                educators: 3),
            "| Disputed-recusal resolution subrecords before affected matters, or NONE | NONE — no disputed recusal |",
            $"| Disputed-recusal resolution subrecords before affected matters, or NONE | {CanonicalRecusalDispute}{MarkdownEntrySeparator}{CanonicalRecusalDispute} |");

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "atlas.session.recusal-dispute-matter-duplicate");
    }

    [Fact]
    public void Session_record_accepts_a_no_quorum_held_recusal_dispute()
    {
        var record = WithHeldRecusalDispute(
            SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true),
            CanonicalNoQuorumHeldRecusalDispute,
            replaceNoRecommendationOutcomeWithHold: true);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.True(result.IsValid, Describe(result));
    }

    [Fact]
    public void Session_record_accepts_a_majority_failure_held_recusal_dispute()
    {
        var dispute =
            "DISPUTE — matter=N-SYNTHETIC; category=financial; affected-person-excluded=YES; affected-person-practicing-educator=YES; excluded-total=1; excluded-present=1; excluded-practicing-educators=1; outcome=HELD; eligible-total=6; eligible-present=3; practicing-educators=2; quorum=NOT-MET — OCF-COUNCIL-TERMS-v1; decision=NONE — quorum not met; read-back=CONFIRMED; rationale=recusal dispute remains held because the eligible majority is absent";
        var record = WithHeldRecusalDispute(
            WithSessionCounts(
                SyntheticRecord(
                    AtlasCouncilRecordValidator.SessionRecordStatus,
                    sessionComplete: true),
                total: 7,
                present: 4,
                educators: 3),
            dispute,
            replaceNoRecommendationOutcomeWithHold: true);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.True(result.IsValid, Describe(result));
    }

    [Fact]
    public void Session_record_accepts_an_educator_minimum_failure_held_recusal_dispute()
    {
        var dispute = ReplaceRequired(
            CanonicalFailedVoteHeldRecusalDispute,
            "practicing-educators=2",
            "practicing-educators=1");
        dispute = ReplaceRequired(
            dispute,
            "quorum=MET — OCF-COUNCIL-TERMS-v1",
            "quorum=NOT-MET — OCF-COUNCIL-TERMS-v1");
        dispute = ReplaceRequired(
            dispute,
            "decision=VOTE — for=1,against=1,abstain=1,denominator=3",
            "decision=NONE — quorum not met");
        var record = WithHeldRecusalDispute(
            WithSessionCounts(
                SyntheticRecord(
                    AtlasCouncilRecordValidator.SessionRecordStatus,
                    sessionComplete: true),
                total: 4,
                present: 4,
                educators: 2),
            dispute,
            replaceNoRecommendationOutcomeWithHold: true);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.True(result.IsValid, Describe(result));
    }

    [Theory]
    [InlineData(
        "affected-person-practicing-educator=YES",
        "affected-person-practicing-educator=UNKNOWN")]
    [InlineData("excluded-total=1", "excluded-total=2")]
    [InlineData("excluded-present=1", "excluded-present=0")]
    [InlineData(
        "excluded-practicing-educators=1",
        "excluded-practicing-educators=0")]
    [InlineData(
        "excluded-total=1; excluded-present=1",
        "excluded-present=1; excluded-total=1")]
    public void Session_record_requires_exact_one_person_recusal_exclusion_grammar(
        string validToken,
        string invalidToken)
    {
        var dispute = ReplaceRequired(CanonicalRecusalDispute, validToken, invalidToken);
        var record = WithRecusalDispute(
            WithSessionCounts(
                SyntheticRecord(
                    AtlasCouncilRecordValidator.SessionRecordStatus,
                    sessionComplete: true),
                total: 4,
                present: 4,
                educators: 3),
            dispute);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "atlas.session.recusal-dispute-record-invalid");
    }

    [Fact]
    public void Session_record_accepts_a_one_person_non_educator_recusal()
    {
        var dispute = ReplaceRequired(
            CanonicalRecusalDispute,
            "affected-person-practicing-educator=YES",
            "affected-person-practicing-educator=NO");
        dispute = ReplaceRequired(
            dispute,
            "excluded-practicing-educators=1",
            "excluded-practicing-educators=0");
        dispute = ReplaceRequired(
            dispute,
            "practicing-educators=2",
            "practicing-educators=3");
        var record = WithRecusalDispute(
            WithSessionCounts(
                SyntheticRecord(
                    AtlasCouncilRecordValidator.SessionRecordStatus,
                    sessionComplete: true),
                total: 4,
                present: 4,
                educators: 3),
            dispute);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.True(result.IsValid, Describe(result));
    }

    [Theory]
    [InlineData(5, 4, 3)]
    [InlineData(4, 3, 3)]
    [InlineData(4, 4, 4)]
    public void Session_record_reconciles_each_dispute_count_to_the_session_counts(
        int total,
        int present,
        int educators)
    {
        var record = WithRecusalDispute(
            WithSessionCounts(
                SyntheticRecord(
                    AtlasCouncilRecordValidator.SessionRecordStatus,
                    sessionComplete: true),
                total,
                present,
                educators),
            CanonicalRecusalDispute);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "atlas.session.recusal-dispute-count-reconciliation-invalid");
    }

    [Fact]
    public void Session_record_accepts_a_failed_vote_held_recusal_dispute()
    {
        var record = WithHeldRecusalDispute(
            WithSessionCounts(
                SyntheticRecord(
                    AtlasCouncilRecordValidator.SessionRecordStatus,
                    sessionComplete: true),
                total: 4,
                present: 4,
                educators: 3),
            CanonicalFailedVoteHeldRecusalDispute,
            replaceNoRecommendationOutcomeWithHold: true);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.True(result.IsValid, Describe(result));
    }

    [Theory]
    [InlineData(
        "eligible-present=2",
        "eligible-present=3")]
    [InlineData(
        "decision=NONE — quorum not met",
        "decision=VOTE — for=1,against=1,abstain=0,denominator=2")]
    public void Session_record_refuses_a_false_or_mixed_no_quorum_hold(
        string validToken,
        string invalidToken)
    {
        var dispute = ReplaceRequired(
            CanonicalNoQuorumHeldRecusalDispute,
            validToken,
            invalidToken);
        var record = WithHeldRecusalDispute(
            SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true),
            dispute,
            replaceNoRecommendationOutcomeWithHold: true);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "atlas.session.recusal-dispute-record-invalid");
    }

    [Fact]
    public void Session_record_refuses_a_not_met_hold_when_the_counts_really_meet_quorum()
    {
        var dispute = ReplaceRequired(
            CanonicalFailedVoteHeldRecusalDispute,
            "quorum=MET — OCF-COUNCIL-TERMS-v1",
            "quorum=NOT-MET — OCF-COUNCIL-TERMS-v1");
        dispute = ReplaceRequired(
            dispute,
            "decision=VOTE — for=1,against=1,abstain=1,denominator=3",
            "decision=NONE — quorum not met");
        var record = WithHeldRecusalDispute(
            WithSessionCounts(
                SyntheticRecord(
                    AtlasCouncilRecordValidator.SessionRecordStatus,
                    sessionComplete: true),
                total: 4,
                present: 4,
                educators: 3),
            dispute,
            replaceNoRecommendationOutcomeWithHold: true);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "atlas.session.recusal-dispute-record-invalid");
    }

    [Theory]
    [InlineData(
        "decision=VOTE — for=1,against=1,abstain=1,denominator=3",
        "decision=VOTE — for=2,against=1,abstain=0,denominator=3")]
    [InlineData(
        "quorum=MET — OCF-COUNCIL-TERMS-v1",
        "quorum=NOT-MET — OCF-COUNCIL-TERMS-v1")]
    [InlineData(
        "decision=VOTE — for=1,against=1,abstain=1,denominator=3",
        "decision=CONSENSUS — denominator=3")]
    public void Session_record_refuses_a_false_or_mixed_failed_vote_hold(
        string validToken,
        string invalidToken)
    {
        var dispute = ReplaceRequired(
            CanonicalFailedVoteHeldRecusalDispute,
            validToken,
            invalidToken);
        var record = WithHeldRecusalDispute(
            WithSessionCounts(
                SyntheticRecord(
                    AtlasCouncilRecordValidator.SessionRecordStatus,
                    sessionComplete: true),
                total: 4,
                present: 4,
                educators: 3),
            dispute,
            replaceNoRecommendationOutcomeWithHold: true);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "atlas.session.recusal-dispute-record-invalid");
    }

    [Fact]
    public void Session_record_refuses_a_resolved_recusal_without_a_strict_vote_majority()
    {
        var dispute = ReplaceRequired(
            CanonicalRecusalDispute,
            "decision=CONSENSUS — denominator=3",
            "decision=VOTE — for=1,against=1,abstain=1,denominator=3");
        var record = WithRecusalDispute(
            WithSessionCounts(
                SyntheticRecord(
                    AtlasCouncilRecordValidator.SessionRecordStatus,
                    sessionComplete: true),
                total: 4,
                present: 4,
                educators: 3),
            dispute);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "atlas.session.recusal-dispute-record-invalid");
    }

    [Fact]
    public void Session_record_refuses_a_disputed_recusal_without_confirmed_read_back()
    {
        var dispute = ReplaceRequired(
            CanonicalRecusalDispute,
            "; read-back=CONFIRMED",
            string.Empty);
        var record = WithRecusalDispute(
            WithSessionCounts(
                SyntheticRecord(
                    AtlasCouncilRecordValidator.SessionRecordStatus,
                    sessionComplete: true),
                total: 4,
                present: 4,
                educators: 3),
            dispute);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "atlas.session.recusal-dispute-record-invalid");
    }

    [Fact]
    public void Session_record_accepts_exact_recused_recommendation_coupling()
    {
        var record = WithSessionCounts(
            SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true,
                includeRecommendation: true),
            total: 4,
            present: 4,
            educators: 3);
        record = WithRecommendation(
            record,
            eligibleTotal: 3,
            eligiblePresent: 3,
            educatorsPresent: 2,
            conflicts: CanonicalRecusedRecommendationConflict,
            tally: "CONSENSUS — denominator=3");
        record = WithRecusalDispute(record, CanonicalRecusalDispute);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.True(result.IsValid, Describe(result));
    }

    [Fact]
    public void Session_record_accepts_exact_not_recused_recommendation_coupling()
    {
        var record = WithSessionCounts(
            SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true,
                includeRecommendation: true),
            total: 4,
            present: 4,
            educators: 3);
        record = WithRecommendation(
            record,
            eligibleTotal: 4,
            eligiblePresent: 4,
            educatorsPresent: 3,
            conflicts: CanonicalNotRecusedRecommendationConflict,
            tally: "CONSENSUS — denominator=4");
        record = WithRecusalDispute(record, CanonicalNotRecusedDispute);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.True(result.IsValid, Describe(result));
    }

    [Theory]
    [InlineData(
        4,
        4,
        3,
        "RECUSALS — total-persons=1; present-persons=1; practicing-educators=1; basis=DISPUTE — matter=N-SYNTHETIC,category=financial,outcome=RECUSED")]
    [InlineData(
        3,
        3,
        2,
        "RECUSALS — total-persons=1; present-persons=1; practicing-educators=1; basis=DISPUTE — matter=N-SYNTHETIC,category=financial,outcome=NOT-RECUSED")]
    public void Session_record_refuses_inexact_recused_recommendation_coupling(
        int eligibleTotal,
        int eligiblePresent,
        int educatorsPresent,
        string conflicts)
    {
        var record = WithSessionCounts(
            SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true,
                includeRecommendation: true),
            total: 4,
            present: 4,
            educators: 3);
        record = WithRecommendation(
            record,
            eligibleTotal,
            eligiblePresent,
            educatorsPresent,
            conflicts,
            tally: $"CONSENSUS — denominator={eligiblePresent}");
        record = WithRecusalDispute(record, CanonicalRecusalDispute);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "atlas.session.recusal-dispute-recommendation-reconciliation-invalid");
    }

    [Fact]
    public void Session_record_refuses_inexact_not_recused_recommendation_coupling()
    {
        var record = WithSessionCounts(
            SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true,
                includeRecommendation: true),
            total: 4,
            present: 4,
            educators: 3);
        record = WithRecommendation(
            record,
            eligibleTotal: 4,
            eligiblePresent: 4,
            educatorsPresent: 3,
            conflicts: "NONE — no affected conflict or recusal",
            tally: "CONSENSUS — denominator=4");
        record = WithRecusalDispute(record, CanonicalNotRecusedDispute);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "atlas.session.recusal-dispute-recommendation-reconciliation-invalid");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Protected_seat_continuity_follows_the_resolved_dispute_outcome(bool holdRequired)
    {
        var dispute = ReplaceRequired(
            holdRequired ? CanonicalRecusalDispute : CanonicalNotRecusedDispute,
            "category=financial",
            "category=accessibility");
        var record = WithRecusalDispute(
            WithSessionCounts(
                SyntheticRecord(
                    AtlasCouncilRecordValidator.SessionRecordStatus,
                    sessionComplete: true),
                total: 4,
                present: 4,
                educators: 3),
            dispute);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Equal(
            holdRequired,
            result.Issues.Any(issue => issue.Code == "atlas.session.protected-seat-continuity"));
        if (!holdRequired)
        {
            Assert.True(result.IsValid, Describe(result));
        }
    }

    [Fact]
    public void Protected_seat_continuity_applies_to_a_held_dispute_category()
    {
        var dispute = ReplaceRequired(
            CanonicalNoQuorumHeldRecusalDispute,
            "category=financial",
            "category=accessibility");
        var record = WithHeldRecusalDispute(
            SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true),
            dispute,
            replaceNoRecommendationOutcomeWithHold: true);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "atlas.session.protected-seat-continuity");
    }

    [Fact]
    public void Session_record_refuses_a_recommendation_on_a_held_dispute_matter()
    {
        var session = WithSessionCounts(
            SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true,
                includeRecommendation: true),
            total: 4,
            present: 4,
            educators: 3);
        session = WithRecommendation(
            session,
            eligibleTotal: 4,
            eligiblePresent: 4,
            educatorsPresent: 3,
            conflicts: "NONE — no affected conflict or recusal",
            tally: "CONSENSUS — denominator=4");
        var record = WithHeldRecusalDispute(
            session,
            CanonicalFailedVoteHeldRecusalDispute,
            replaceNoRecommendationOutcomeWithHold: false);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "atlas.session.recusal-dispute-held-matter-recommended");
    }

    [Fact]
    public void Session_record_requires_hold_outcome_when_a_dispute_is_held_without_other_recommendations()
    {
        var record = WithHeldRecusalDispute(
            SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true),
            CanonicalNoQuorumHeldRecusalDispute,
            replaceNoRecommendationOutcomeWithHold: false);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "atlas.recommendation.outcome-mismatch");
    }

    [Fact]
    public void Session_record_allows_an_independent_recommendation_while_another_dispute_is_held()
    {
        var record = WithIndependentHeldNeed(
            SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true,
                includeRecommendation: true));
        var heldDispute = ReplaceRequired(
            CanonicalNoQuorumHeldRecusalDispute,
            "matter=N-SYNTHETIC",
            "matter=N-HELD");
        record = WithHeldRecusalDispute(
            record,
            heldDispute,
            replaceNoRecommendationOutcomeWithHold: false);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.True(result.IsValid, Describe(result));
    }

    [Theory]
    [InlineData("| Natural persons present (count) | 3 |", "| Natural persons present (count) | 0 |", "atlas.session.natural-person-count-invalid")]
    [InlineData("| Total seated, non-vacant natural persons (count) | 3 |", "| Total seated, non-vacant natural persons (count) | 0 |", "atlas.session.total-seated-count-invalid")]
    [InlineData("| Total seated, non-vacant natural persons (count) | 3 |", "| Total seated, non-vacant natural persons (count) | 2 |", "atlas.session.natural-person-count-inconsistent")]
    [InlineData("| Practicing-educator natural persons present (count) | 3 |", "| Practicing-educator natural persons present (count) | 0 |", "atlas.session.practicing-educator-count-invalid")]
    [InlineData("| Practicing-educator natural persons present (count) | 3 |", "| Practicing-educator natural persons present (count) | 4 |", "atlas.session.practicing-educator-count-inconsistent")]
    public void Session_record_requires_consistent_positive_natural_person_counts(
        string validField,
        string invalidField,
        string issueCode)
    {
        var record = SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true)
            .Replace(validField, invalidField, StringComparison.Ordinal);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(result.Issues, issue => issue.Code == issueCode);
    }

    [Fact]
    public void Session_record_requires_constituted_authority_for_every_present_capacity()
    {
        var record = SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true)
            .Replace(
                "| Seats present (seat + count, no names by default) | general educator: 3 |",
                "| Seats present (seat + count, no names by default) | general educator: 3; curriculum: 1 |",
                StringComparison.Ordinal);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "atlas.session.seat-authority-present-count-mismatch");
    }

    [Theory]
    [InlineData("AAC")]
    [InlineData("aac")]
    [InlineData("SLP")]
    [InlineData("slp")]
    [InlineData("AT")]
    [InlineData("accessibility")]
    [InlineData("multilingual")]
    [InlineData("privacy")]
    [InlineData("legal")]
    [InlineData("records")]
    [InlineData("safeguarding")]
    [InlineData("curriculum")]
    [InlineData("rights")]
    [InlineData("OER")]
    [InlineData("oer")]
    [InlineData("district")]
    public void Session_record_requires_canonical_hold_for_each_protected_seat_alias(string alias)
    {
        var record = SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true)
            .Replace(
                "| N-SYNTHETIC | Synthetic possibility | Synthetic fit record | G | AAC/SLP hold retained |",
                $"| N-SYNTHETIC | Synthetic possibility | Synthetic fit record | G | {alias} implicated |",
                StringComparison.Ordinal)
            .Replace("AAC/SLP hold retained", "None recorded", StringComparison.Ordinal)
            .Replace(
                "HELD — AAC/SLP — NOT REVIEWED — HELD",
                "NONE — no protected-seat hold",
                StringComparison.Ordinal);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.session.protected-seat-continuity");
    }

    [Fact]
    public void Session_record_does_not_treat_lowercase_preposition_at_as_the_AT_seat()
    {
        var record = SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true)
            .Replace(
                "| N-SYNTHETIC | Synthetic possibility | Synthetic fit record | G | AAC/SLP hold retained |",
                "| N-SYNTHETIC | Synthetic possibility | Synthetic fit record | G | translation at school |",
                StringComparison.Ordinal)
            .Replace("AAC/SLP hold retained", "None recorded", StringComparison.Ordinal)
            .Replace(
                "HELD — AAC/SLP — NOT REVIEWED — HELD",
                "NONE — no protected-seat hold",
                StringComparison.Ordinal);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.True(result.IsValid, Describe(result));
    }

    [Fact]
    public void Session_record_carries_an_explicit_session_hold_into_final_read_back()
    {
        var record = SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true)
            .Replace(
                "| N-SYNTHETIC | Synthetic possibility | Synthetic fit record | G | AAC/SLP hold retained |",
                "| N-SYNTHETIC | Synthetic possibility | Synthetic fit record | G | none implicated |",
                StringComparison.Ordinal)
            .Replace("AAC/SLP hold retained", "None recorded", StringComparison.Ordinal)
            .Replace(
                "HELD — AAC/SLP — NOT REVIEWED — HELD",
                "HELD — accessibility/AT — NOT REVIEWED — HELD",
                StringComparison.Ordinal);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.session.protected-seat-continuity");
    }

    [Theory]
    [InlineData(
        "HELD — AAC/SLP — NOT REVIEWED — HELD",
        "No AAC/SLP hold remains",
        "atlas.session.protected-seat-continuity")]
    [InlineData(
        "AAC/SLP hold retained",
        "No AAC/SLP hold remains",
        "atlas.session.protected-seat-continuity")]
    public void Session_record_refuses_protected_seat_labels_inside_clearance_language(
        string retainedText,
        string clearanceText,
        string issueCode)
    {
        var valid = SyntheticRecord(
            AtlasCouncilRecordValidator.SessionRecordStatus,
            sessionComplete: true,
            includeRecommendation: true);
        var record = valid.Replace(retainedText, clearanceText, StringComparison.Ordinal);

        Assert.NotEqual(valid, record);
        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(result.Issues, issue => issue.Code == issueCode);
    }

    [Theory]
    [InlineData("H0-SYNTHETIC v")]
    [InlineData("H0-SYNTHETIC v.")]
    [InlineData("H0-SYNTHETIC v1..2")]
    public void Session_record_requires_numeric_version_segments(string malformedIdentity)
    {
        var record = SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true)
            .Replace("H0-SYNTHETIC v1", malformedIdentity, StringComparison.Ordinal);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.session.record-identity-invalid");
    }

    [Theory]
    [InlineData("Session-opening general quorum result before matter-specific recusals", "MET — OCF-COUNCIL-TERMS-v1 — before matter-specific recusals", "NOT MET — UNRANKED", "atlas.session.quorum-not-met")]
    [InlineData("Participation consent recorded separately", "ACCEPTED — general educator: 3", "TBD", "atlas.session.participation-consent-invalid")]
    [InlineData("Private/de-identified note-collection consent recorded", "ACCEPTED — general educator: 3", "DECLINED — no notes", "atlas.session.note-consent-invalid")]
    [InlineData("Documentation/original-printable content license and accountable decision record", "CHOSEN — Synthetic content license — REC-1", "UNCHOSEN", "atlas.session.content-license-unresolved")]
    [InlineData("Public-record publication consent recorded", "ACCEPTED — general educator: 3", "DECLINED", "atlas.session.publication-consent-invalid")]
    [InlineData("Decision procedure and quorum rule applied (exact governing record)", "APPLIED — OCF-COUNCIL-TERMS-v1", "DRAFT", "atlas.session.procedure-unapplied")]
    public void Session_record_refuses_nonoperative_authority_or_consent_dispositions(
        string field,
        string validValue,
        string invalidValue,
        string expectedCode)
    {
        var valid = SyntheticRecord(
            AtlasCouncilRecordValidator.SessionRecordStatus,
            sessionComplete: true);
        var record = valid.Replace(
            $"| {field} | {validValue} |",
            $"| {field} | {invalidValue} |",
            StringComparison.Ordinal);

        Assert.NotEqual(valid, record);
        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(result.Issues, issue => issue.Code == expectedCode);
    }

    [Theory]
    [InlineData("<!--", "-->")]
    [InlineData("```markdown", "```")]
    [InlineData("<pre>", "</pre>")]
    [InlineData("<details><summary>Hidden", "</summary></details>")]
    public void H0_refuses_hidden_or_fenced_governance_structure(string opening, string closing)
    {
        var record = $"{opening}\n{SyntheticRecord(AtlasCouncilRecordValidator.SessionRecordStatus, sessionComplete: true)}\n{closing}";

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.h0.hidden-structure");
    }

    [Fact]
    public void H0_allows_placeholder_angle_text_inside_a_closed_multiline_code_span()
    {
        var record = ReplaceRequired(
            SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true),
            "Synthetic fixture preparation.",
            "Synthetic fixture preparation.\n\n`synthetic <opaque>\nreference`");

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.True(result.IsValid, Describe(result));
    }

    [Fact]
    public void H0_refuses_an_unclosed_multiline_inline_code_span()
    {
        var record = ReplaceRequired(
            SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true),
            "Synthetic fixture preparation.",
            "Synthetic fixture preparation.\n\n`synthetic <opaque>\nreference");

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.h0.hidden-structure");
    }

    [Fact]
    public void H0_refuses_setext_headings_that_evade_atx_heading_topology()
    {
        var record = SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true)
            .Replace(
                "## Completion check",
                "Shipping decision\n=================\n\n## Completion check",
                StringComparison.Ordinal);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.h0.hidden-structure");
    }

    [Fact]
    public void H0_refuses_indented_code_that_only_looks_like_a_visible_record()
    {
        var record = string.Join(
            "\n",
            SyntheticRecord(AtlasCouncilRecordValidator.SessionRecordStatus, sessionComplete: true)
                .Split('\n')
                .Select(line => $"    {line}"));

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.h0.hidden-structure");
    }

    [Fact]
    public void H0_refuses_a_title_that_relabels_the_record_authority()
    {
        var record = SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true)
            .Replace(
                "# Atlas 2.0 council priority session",
                "# RELEASE AUTHORIZATION",
                StringComparison.Ordinal);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.lifecycle.section-boundary");
    }

    [Fact]
    public void H0_refuses_alternate_later_authority_heading()
    {
        var record = SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true)
            + "\n## Engineering feasibility\n\nNot part of H0.\n";

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.lifecycle.section-boundary");
    }

    [Theory]
    [InlineData(true, "RECOMMENDATION RECORDED", "NO RECOMMENDATION")]
    [InlineData(false, "NO RECOMMENDATION", "RECOMMENDATION RECORDED")]
    public void Session_record_outcome_must_match_recommendation_presence(
        bool includeRecommendation,
        string validOutcome,
        string mismatchedOutcome)
    {
        var record = SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true,
                includeRecommendation: includeRecommendation)
            .Replace(
                $"- **Council outcome (`RECOMMENDATION RECORDED`, `NO RECOMMENDATION`, or `HOLD`):** {validOutcome}",
                $"- **Council outcome (`RECOMMENDATION RECORDED`, `NO RECOMMENDATION`, or `HOLD`):** {mismatchedOutcome}",
                StringComparison.Ordinal);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.recommendation.outcome-mismatch");
    }

    [Theory]
    [InlineData("SESSION HELD — REVIEW PENDING")]
    [InlineData("COUNCIL RECORD FROZEN")]
    [InlineData("PRIORITY SELECTED")]
    public void H0_record_refuses_statuses_that_claim_detached_lifecycle_events(string status)
    {
        var result = AtlasCouncilRecordValidator.Validate(RecordFile, SyntheticRecord(status));

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.status.unknown");
        Assert.Null(result.Status);
    }

    [Fact]
    public void Required_headings_must_appear_once_in_the_authoritative_order()
    {
        const string NeedHeading = "### Need card — complete before opening the atlas";
        const string MappingHeading = "### Need-to-possibility mapping — complete only after need capture";
        const string Swap = "### SYNTHETIC SWAP";
        var malformed = SyntheticRecord(AtlasCouncilRecordValidator.UnrunStatus)
            .Replace(NeedHeading, "### Missing need card", StringComparison.Ordinal)
            .Replace(
                "## Completion check",
                "## Close the session record; freeze only through a detached manifest\n\n## Completion check",
                StringComparison.Ordinal);
        var malformedResult = AtlasCouncilRecordValidator.Validate(RecordFile, malformed);
        Assert.Contains(malformedResult.Issues, issue => issue.Code == "atlas.heading.missing");
        Assert.Contains(malformedResult.Issues, issue => issue.Code == "atlas.heading.duplicate");

        var reordered = SyntheticRecord(AtlasCouncilRecordValidator.UnrunStatus)
            .Replace(NeedHeading, Swap, StringComparison.Ordinal)
            .Replace(MappingHeading, NeedHeading, StringComparison.Ordinal)
            .Replace(Swap, MappingHeading, StringComparison.Ordinal);
        var reorderedResult = AtlasCouncilRecordValidator.Validate(RecordFile, reordered);
        Assert.Contains(reorderedResult.Issues, issue => issue.Code == "atlas.heading.order");
    }

    [Fact]
    public void Current_consent_compensation_and_constituted_seat_fields_are_required()
    {
        var requiredFields = new[]
        {
            "H0 record ID and version",
            "Repository commit and dirty-tree disposition",
            "Build/artifact IDs and SHA-256 values",
            "Instrument name, version, and SHA-256",
            "Exact material actually reviewed",
            "Current enacted roster record, version, and SHA-256",
            "Total seated, non-vacant natural persons (count)",
            "Natural persons present (count)",
            "Practicing-educator natural persons present (count)",
            "Seats absent",
            "Enacted operating-terms exact file binding",
            "Operative compensation-policy exact file binding",
            SeatAuthorityField,
            "Participation consent recorded separately",
            "Disputed-recusal resolution subrecords before affected matters, or NONE",
            "Withdrawal right and route explained/acknowledged",
            "Operative compensation-policy version and effective date; election recorded",
            "Private/de-identified note-collection consent recorded",
            "Public-record publication consent recorded",
            "Recording consent recorded, or no recording",
            "Within-cohort identity/affiliation disclosure scope honored; confidentiality/no-contact boundary acknowledged",
            "Public-credit choice confirmed",
            "Content-contribution choice and exact license/control identity, or none",
            "Role-acceptance choice and exact bounded role/control identity, or none",
            "Maintainer-appointment choice and exact role/control identity, or none",
            "Copyright-stewardship choice and exact transfer/control identity, or none",
            "Activity withdrawal, council resignation/vacancy, and non-member role-closure dispositions",
        };
        var valid = SyntheticRecord(
            AtlasCouncilRecordValidator.SessionRecordStatus,
            sessionComplete: true);

        foreach (var field in requiredFields)
        {
            var result = AtlasCouncilRecordValidator.Validate(
                RecordFile,
                valid.Replace(field, $"Altered {field}", StringComparison.Ordinal));

            Assert.True(
                result.Issues.Any(issue => issue.Code == "atlas.session.field-missing"),
                $"The validator accepted a session without '{field}'.{Environment.NewLine}{Describe(result)}");
        }
    }

    [Fact]
    public void Public_record_requires_factual_paraphrase_tables_not_original_words()
    {
        var record = SyntheticRecord(AtlasCouncilRecordValidator.UnrunStatus)
            .Replace(
                "Participant-reviewed de-identified factual paraphrase",
                "Council member's words",
                StringComparison.Ordinal);
        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.need-card.header");
    }

    [Fact]
    public void Mapping_and_recommendation_must_link_exact_completed_need_identity()
    {
        var valid = SyntheticRecord(
            AtlasCouncilRecordValidator.SessionRecordStatus,
            sessionComplete: true,
            includeRecommendation: true);
        var missingNeed = valid.Replace(
            "| What must remain under teacher control | synthetic factual paraphrase |",
            "| What must remain under teacher control | |",
            StringComparison.Ordinal);
        var missingNeedResult = AtlasCouncilRecordValidator.Validate(RecordFile, missingNeed);
        Assert.Contains(missingNeedResult.Issues, issue => issue.Code == "atlas.need-card.incomplete");
        Assert.Contains(missingNeedResult.Issues, issue => issue.Code == "atlas.lifecycle.mapping-before-need");

        var mismatchedRecommendation = valid.Replace(
            "N-SYNTHETIC · Synthetic possibility",
            "N-SYNTHETIC · Different possibility",
            StringComparison.Ordinal);
        var mismatchResult = AtlasCouncilRecordValidator.Validate(RecordFile, mismatchedRecommendation);
        Assert.Contains(mismatchResult.Issues, issue => issue.Code == "atlas.recommendation.possibility-unmapped");
    }

    [Fact]
    public void Mapping_requires_a_canonical_lane_token()
    {
        var record = SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true)
            .Replace(
                "| N-SYNTHETIC | Synthetic possibility | Synthetic fit record | G | AAC/SLP hold retained |",
                "| N-SYNTHETIC | Synthetic possibility | Synthetic fit record | banana | AAC/SLP hold retained |",
                StringComparison.Ordinal);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.mapping.lane-invalid");
    }

    [Fact]
    public void Recommendation_requires_explicit_holds_and_canonical_width()
    {
        var valid = SyntheticRecord(
            AtlasCouncilRecordValidator.SessionRecordStatus,
            sessionComplete: true,
            includeRecommendation: true);
        const string RecommendationRow = "| 1 | N-SYNTHETIC · Synthetic possibility | Synthetic rationale | Synthetic proof request | 3 | 3 | 3 | NONE — no affected conflict or recusal | MET — OCF-COUNCIL-TERMS-v1 — after recusals | CONSENSUS — denominator=3 | AAC/SLP hold retained | None recorded |";
        var missingHold = valid.Replace(
            RecommendationRow,
            "| 1 | N-SYNTHETIC · Synthetic possibility | Synthetic rationale | Synthetic proof request | 3 | 3 | 3 | NONE — no affected conflict or recusal | MET — OCF-COUNCIL-TERMS-v1 — after recusals | CONSENSUS — denominator=3 | | None recorded |",
            StringComparison.Ordinal);
        var missingHoldResult = AtlasCouncilRecordValidator.Validate(RecordFile, missingHold);
        Assert.Contains(missingHoldResult.Issues, issue => issue.Code == "atlas.holds.recommendation-value-missing");

        var shifted = valid.Replace(
            RecommendationRow,
            "| 1 | N-SYNTHETIC · Synthetic possibility | Synthetic rationale | shifted | Synthetic proof request | 3 | 3 | 3 | NONE — no affected conflict or recusal | MET — OCF-COUNCIL-TERMS-v1 — after recusals | CONSENSUS — denominator=3 | AAC/SLP hold retained | None recorded |",
            StringComparison.Ordinal);
        var shiftedResult = AtlasCouncilRecordValidator.Validate(RecordFile, shifted);
        Assert.Contains(shiftedResult.Issues, issue => issue.Code == "atlas.table.recommendation-width");
    }

    [Fact]
    public void Recommendation_none_recusals_cannot_silently_shrink_the_matter_denominator()
    {
        var record = SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true,
                includeRecommendation: true)
            .Replace(
                "| 1 | N-SYNTHETIC · Synthetic possibility | Synthetic rationale | Synthetic proof request | 3 | 3 | 3 | NONE — no affected conflict or recusal | MET — OCF-COUNCIL-TERMS-v1 — after recusals | CONSENSUS — denominator=3 | AAC/SLP hold retained | None recorded |",
                "| 1 | N-SYNTHETIC · Synthetic possibility | Synthetic rationale | Synthetic proof request | 2 | 2 | 2 | NONE — no affected conflict or recusal | MET — OCF-COUNCIL-TERMS-v1 — after recusals | CONSENSUS — denominator=2 | AAC/SLP hold retained | None recorded |",
                StringComparison.Ordinal);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "atlas.recommendation.recusal-reconciliation-invalid");
    }

    [Fact]
    public void Recommendation_structured_recusals_reconcile_exactly_to_matter_counts()
    {
        var record = SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true,
                includeRecommendation: true)
            .Replace(
                "| 1 | N-SYNTHETIC · Synthetic possibility | Synthetic rationale | Synthetic proof request | 3 | 3 | 3 | NONE — no affected conflict or recusal | MET — OCF-COUNCIL-TERMS-v1 — after recusals | CONSENSUS — denominator=3 | AAC/SLP hold retained | None recorded |",
                "| 1 | N-SYNTHETIC · Synthetic possibility | Synthetic rationale | Synthetic proof request | 2 | 2 | 2 | RECUSALS — total-persons=1; present-persons=1; practicing-educators=1; basis=employment interest in exact matter | MET — OCF-COUNCIL-TERMS-v1 — after recusals | CONSENSUS — denominator=2 | AAC/SLP hold retained | None recorded |",
                StringComparison.Ordinal);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.DoesNotContain(
            result.Issues,
            issue => issue.Code is "atlas.recommendation.conflict-recusals-invalid"
                or "atlas.recommendation.recusal-reconciliation-invalid");
    }

    [Theory]
    [InlineData(
        "RECUSALS — none",
        "MET — OCF-COUNCIL-TERMS-v1 — after recusals",
        "CONSENSUS — denominator=3",
        "atlas.recommendation.conflict-recusals-invalid")]
    [InlineData(
        "NONE — no affected conflict or recusal",
        "NOT MET — OCF-COUNCIL-TERMS-v1 — after recusals",
        "CONSENSUS — denominator=3",
        "atlas.recommendation.quorum-invalid")]
    [InlineData(
        "NONE — no affected conflict or recusal",
        "MET — OCF-COUNCIL-TERMS-v1 — after recusals",
        "CONSENSUS — denominator=1",
        "atlas.recommendation.tally-invalid")]
    public void Recommendation_requires_exact_conflict_quorum_and_tally_mechanics(
        string conflicts,
        string quorum,
        string tally,
        string issueCode)
    {
        var record = SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true,
                includeRecommendation: true)
            .Replace(
                "NONE — no affected conflict or recusal | MET — OCF-COUNCIL-TERMS-v1 — after recusals | CONSENSUS — denominator=3",
                $"{conflicts} | {quorum} | {tally}",
                StringComparison.Ordinal);

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(result.Issues, issue => issue.Code == issueCode);
    }

    [Theory]
    [InlineData(3, 3, 3, 2, 2, 2, "RECUSALS — total-persons=1; present-persons=1; practicing-educators=1; basis=employment interest in exact matter")]
    [InlineData(7, 4, 3, 6, 3, 2, "RECUSALS — total-persons=1; present-persons=1; practicing-educators=1; basis=employment interest in exact matter")]
    [InlineData(4, 4, 2, 3, 3, 1, "RECUSALS — total-persons=1; present-persons=1; practicing-educators=1; basis=employment interest in exact matter")]
    public void Recommendation_matter_quorum_enforces_majority_person_and_educator_floors(
        int sessionTotal,
        int sessionPresent,
        int sessionEducators,
        int eligibleTotal,
        int eligiblePresent,
        int eligibleEducators,
        string recusals)
    {
        var record = WithSessionCounts(
            SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true,
                includeRecommendation: true),
            sessionTotal,
            sessionPresent,
            sessionEducators);
        record = WithRecommendation(
            record,
            eligibleTotal,
            eligiblePresent,
            eligibleEducators,
            recusals,
            $"CONSENSUS — denominator={eligiblePresent}");

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.DoesNotContain(
            result.Issues,
            issue => issue.Code == "atlas.recommendation.recusal-reconciliation-invalid");
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "atlas.recommendation.quorum-arithmetic-invalid");
    }

    [Fact]
    public void Recommendation_matter_quorum_accepts_the_exact_person_and_educator_floors()
    {
        var record = WithSessionCounts(
            SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true,
                includeRecommendation: true),
            total: 4,
            present: 4,
            educators: 2);
        record = WithRecommendation(
            record,
            eligibleTotal: 3,
            eligiblePresent: 3,
            educatorsPresent: 2,
            "RECUSALS — total-persons=1; present-persons=1; practicing-educators=0; basis=employment interest in exact matter",
            "CONSENSUS — denominator=3");

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.True(result.IsValid, Describe(result));
    }

    [Theory]
    [InlineData("VOTE — for=2; against=1; abstain=0; denominator=3", true)]
    [InlineData("VOTE — for=1; against=1; abstain=1; denominator=3", false)]
    public void Recommendation_vote_requires_a_strict_majority_of_the_exact_denominator(
        string tally,
        bool expectedValid)
    {
        var record = WithRecommendation(
            SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true,
                includeRecommendation: true),
            eligibleTotal: 3,
            eligiblePresent: 3,
            educatorsPresent: 3,
            "NONE — no affected conflict or recusal",
            tally);
        record = ReplaceRequired(
            record,
            "- **Vote/tally under the enacted procedure, or consensus/no vote:** Consensus under synthetic procedure; no vote.",
            $"- **Vote/tally under the enacted procedure, or consensus/no vote:** {tally}");

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        if (expectedValid)
        {
            Assert.True(result.IsValid, Describe(result));
        }
        else
        {
            Assert.Contains(result.Issues, issue => issue.Code == "atlas.recommendation.tally-invalid");
        }
    }

    [Theory]
    [InlineData(3, 2, 2)]
    [InlineData(2, 3, 2)]
    [InlineData(2, 2, 3)]
    public void Recommendation_structured_recusals_must_reconcile_each_count(
        int eligibleTotal,
        int eligiblePresent,
        int eligibleEducators)
    {
        var record = WithRecommendation(
            SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true,
                includeRecommendation: true),
            eligibleTotal,
            eligiblePresent,
            eligibleEducators,
            "RECUSALS — total-persons=1; present-persons=1; practicing-educators=1; basis=employment interest in exact matter",
            $"CONSENSUS — denominator={eligiblePresent}");

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.DoesNotContain(
            result.Issues,
            issue => issue.Code == "atlas.recommendation.conflict-recusals-invalid");
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "atlas.recommendation.recusal-reconciliation-invalid");
    }

    [Fact]
    public void Recommendation_protected_seat_conflict_activates_session_and_matter_holds()
    {
        var record = SyntheticRecord(
            AtlasCouncilRecordValidator.SessionRecordStatus,
            sessionComplete: true,
            includeRecommendation: true);
        record = ReplaceRequired(
            record,
            "| Applicable protected seats vacant, absent, or recused; each marked NOT REVIEWED — HELD | HELD — AAC/SLP — NOT REVIEWED — HELD |",
            "| Applicable protected seats vacant, absent, or recused; each marked NOT REVIEWED — HELD | NONE — no applicable protected-seat hold |");
        record = ReplaceRequired(
            record,
            "| N-SYNTHETIC | Synthetic possibility | Synthetic fit record | G | AAC/SLP hold retained |",
            "| N-SYNTHETIC | Synthetic possibility | Synthetic fit record | G | none implicated |");
        record = ReplaceRequired(
            record,
            "- **Applicable seat holds after read-back:** AAC/SLP hold retained",
            "- **Applicable seat holds after read-back:** NONE — no applicable protected-seat hold");
        record = WithRecommendation(
            record,
            eligibleTotal: 2,
            eligiblePresent: 2,
            educatorsPresent: 2,
            "RECUSALS — total-persons=1; present-persons=1; practicing-educators=1; basis=AAC reviewer authorship interest",
            "CONSENSUS — denominator=2",
            holds: "None recorded");

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.session.protected-seat-continuity");
        Assert.Contains(result.Issues, issue => issue.Code == "atlas.recommendation.protected-seat-hold-missing");
    }

    [Theory]
    [InlineData("## Participant review and council-record freeze")]
    [InlineData("## Separate feasibility appendix — completed after the council record is frozen")]
    [InlineData("## Product-owner disposition — intentionally blank in the template")]
    public void H0_record_refuses_sections_that_would_allow_post_review_mutation(string forbiddenHeading)
    {
        var record = SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true)
            .Replace(
                "## Completion check",
                $"{forbiddenHeading}{Environment.NewLine}{Environment.NewLine}Synthetic later-authority content{Environment.NewLine}{Environment.NewLine}## Completion check",
                StringComparison.Ordinal);
        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.lifecycle.detached-content-in-h0");
    }

    [Fact]
    public void H0_record_refuses_an_embedded_digest_field()
    {
        var record = SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true)
            .Replace(
                "| H0 record ID and version | H0-SYNTHETIC v1 |",
                "| H0 record ID and version | H0-SYNTHETIC v1 |\n| Final H0 record SHA-256 | circular |",
                StringComparison.Ordinal);
        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.session.field-unexpected");
    }

    [Fact]
    public void Detached_manifest_binds_final_record_bytes_without_self_hash()
    {
        var recordBytes = CompletedRecordBytes();
        var manifestBytes = Utf8(SyntheticManifest(recordBytes));

        var result = AtlasCouncilRecordValidator.ValidateFreezeManifest(
            ManifestFile,
            manifestBytes,
            RecordFile,
            recordBytes);

        Assert.True(result.IsValid, Describe(result));
    }

    [Fact]
    public void Detached_manifest_refuses_mechanically_incomplete_H0_record_even_when_binding_matches()
    {
        var incompleteRecord = Encoding.UTF8.GetString(CompletedRecordBytes()).Replace(
            "| N-SYNTHETIC | Synthetic possibility | Synthetic fit record | G | AAC/SLP hold retained |",
            "| | | | | |",
            StringComparison.Ordinal);
        var incompleteRecordBytes = Utf8(incompleteRecord);
        var matchingManifestBytes = Utf8(SyntheticManifest(incompleteRecordBytes));

        var result = AtlasCouncilRecordValidator.ValidateFreezeManifest(
            ManifestFile,
            matchingManifestBytes,
            RecordFile,
            incompleteRecordBytes);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.freeze.record-invalid");
        Assert.DoesNotContain(result.Issues, issue => issue.Code == "atlas.freeze.record-hash-mismatch");
        Assert.DoesNotContain(result.Issues, issue => issue.Code == "atlas.freeze.record-length-mismatch");
    }

    [Fact]
    public void Detached_manifest_detects_any_final_record_byte_change()
    {
        var originalBytes = CompletedRecordBytes();
        var manifestBytes = Utf8(SyntheticManifest(originalBytes));
        var changedBytes = Utf8(Encoding.UTF8.GetString(originalBytes).Replace("\n", "\r\n", StringComparison.Ordinal));

        var result = AtlasCouncilRecordValidator.ValidateFreezeManifest(
            ManifestFile,
            manifestBytes,
            RecordFile,
            changedBytes);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.freeze.record-hash-mismatch");
        Assert.Contains(result.Issues, issue => issue.Code == "atlas.freeze.record-length-mismatch");
    }

    [Fact]
    public void Detached_manifest_exactly_binds_review_packet_and_all_present_seat_counts()
    {
        var recordBytes = CompletedRecordBytes();
        var mismatched = SyntheticManifest(recordBytes)
            .Replace(
                $"| Build/artifact IDs and SHA-256 values | {SyntheticBuildArtifact} |",
                $"| Build/artifact IDs and SHA-256 values | {DifferentSyntheticBuildArtifact} |",
                StringComparison.Ordinal)
            .Replace(
                "| Participant read-back/review of those exact bytes completed (seat + count, no names by default) | general educator: 3 |",
                "| Participant read-back/review of those exact bytes completed (seat + count, no names by default) | general educator: 1 |",
                StringComparison.Ordinal);

        var result = AtlasCouncilRecordValidator.ValidateFreezeManifest(
            ManifestFile,
            Utf8(mismatched),
            RecordFile,
            recordBytes);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.freeze.build-artifact-mismatch");
        Assert.Contains(result.Issues, issue => issue.Code == "atlas.freeze.review-coverage-mismatch");
    }

    [Fact]
    public void Detached_manifest_refuses_a_circular_self_hash_field()
    {
        var recordBytes = CompletedRecordBytes();
        var manifest = SyntheticManifest(recordBytes)
            .Replace(
                "| Final H0 record byte length |",
                "| This freeze manifest SHA-256 | deadbeef |\n| Final H0 record byte length |",
                StringComparison.Ordinal);

        var result = AtlasCouncilRecordValidator.ValidateFreezeManifest(
            ManifestFile,
            Utf8(manifest),
            RecordFile,
            recordBytes);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.freeze.self-hash-field");
        Assert.Contains(result.Issues, issue => issue.Code == "atlas.freeze.field-unexpected");
    }

    [Theory]
    [InlineData("This freeze manifest SHA-256 is deadbeef.")]
    [InlineData("This manifest checksum is deadbeef.")]
    [InlineData("This manifest digest is deadbeef.")]
    public void Detached_manifest_refuses_own_digest_claims_in_its_preamble(string claim)
    {
        var recordBytes = CompletedRecordBytes();
        var manifest = SyntheticManifest(recordBytes).Replace(
            "## H0 freeze binding",
            $"{claim}\n\n## H0 freeze binding",
            StringComparison.Ordinal);

        var result = AtlasCouncilRecordValidator.ValidateFreezeManifest(
            ManifestFile,
            Utf8(manifest),
            RecordFile,
            recordBytes);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.freeze.self-hash-claim");
    }

    [Fact]
    public void Detached_manifest_refuses_an_own_digest_claim_in_a_free_value()
    {
        var recordBytes = CompletedRecordBytes();
        var manifest = SyntheticManifest(recordBytes).Replace(
            "| Requested corrections and accountable owners | NONE — no correction requested |",
            "| Requested corrections and accountable owners | This manifest checksum is deadbeef |",
            StringComparison.Ordinal);

        var result = AtlasCouncilRecordValidator.ValidateFreezeManifest(
            ManifestFile,
            Utf8(manifest),
            RecordFile,
            recordBytes);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.freeze.self-hash-claim");
    }

    [Fact]
    public void Detached_manifest_exactly_binds_each_duplicated_governance_fact()
    {
        var recordBytes = CompletedRecordBytes();
        var manifest = SyntheticManifest(recordBytes).Replace(
            "| Participation consent recorded separately | ACCEPTED — general educator: 3 |",
            "| Participation consent recorded separately | ACCEPTED — general educator: 1 |",
            StringComparison.Ordinal);

        var result = AtlasCouncilRecordValidator.ValidateFreezeManifest(
            ManifestFile,
            Utf8(manifest),
            RecordFile,
            recordBytes);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "atlas.freeze.participation-consent-mismatch");
    }

    [Theory]
    [InlineData(
        "| Content-contribution choice and exact license/control identity, or none | NONE — general educator: 3 |",
        "| Content-contribution choice and exact license/control identity, or none | NONE — general educator: 2 |",
        "atlas.freeze.content-contribution-choice-mismatch")]
    [InlineData(
        "| Role-acceptance choice and exact bounded role/control identity, or none | ACCEPTED — general educator: 3 — OCF-COUNCIL-TERMS-v1 |",
        "| Role-acceptance choice and exact bounded role/control identity, or none | ACCEPTED — general educator: 2 — OCF-COUNCIL-TERMS-v1 |",
        "atlas.freeze.role-acceptance-choice-mismatch")]
    [InlineData(
        "| Maintainer-appointment choice and exact role/control identity, or none | NONE — general educator: 3 |",
        "| Maintainer-appointment choice and exact role/control identity, or none | NONE — general educator: 2 |",
        "atlas.freeze.maintainer-appointment-choice-mismatch")]
    [InlineData(
        "| Copyright-stewardship choice and exact transfer/control identity, or none | NONE — general educator: 3 |",
        "| Copyright-stewardship choice and exact transfer/control identity, or none | NONE — general educator: 2 |",
        "atlas.freeze.copyright-stewardship-choice-mismatch")]
    [InlineData(
        "| Activity withdrawal, council resignation/vacancy, and non-member role-closure dispositions | RESOLVED — activity-withdrawal=NONE; council-resignation-vacancy=NONE; non-member-role-closure=NOT-APPLICABLE-H0; unresolved=NONE |",
        "| Activity withdrawal, council resignation/vacancy, and non-member role-closure dispositions | RESOLVED — activity-withdrawal=REQ-1; council-resignation-vacancy=NONE; non-member-role-closure=NOT-APPLICABLE-H0; unresolved=NONE |",
        "atlas.freeze.withdrawal-disposition-mismatch")]
    public void Detached_manifest_exactly_binds_each_separate_choice_and_withdrawal_disposition(
        string validField,
        string mismatchedField,
        string issueCode)
    {
        var recordBytes = CompletedRecordBytes();
        var manifest = ReplaceRequired(
            SyntheticManifest(recordBytes),
            validField,
            mismatchedField);

        var result = AtlasCouncilRecordValidator.ValidateFreezeManifest(
            ManifestFile,
            Utf8(manifest),
            RecordFile,
            recordBytes);

        Assert.Contains(result.Issues, issue => issue.Code == issueCode);
    }

    [Theory]
    [InlineData(
        "| Seats absent | NONE — no absent constituted seat |",
        "| Seats absent | ABSENT — general educator: 1 |",
        "atlas.freeze.absent-seats-mismatch")]
    [InlineData(
        "| Disputed-recusal resolution subrecords before affected matters, or NONE | NONE — no disputed recusal |",
        "| Disputed-recusal resolution subrecords before affected matters, or NONE | DISPUTE — matter=N-SYNTHETIC; category=financial; affected-person-excluded=YES; affected-person-practicing-educator=YES; excluded-total=1; excluded-present=1; excluded-practicing-educators=1; outcome=RECUSED; eligible-total=3; eligible-present=3; practicing-educators=2; quorum=MET — OCF-COUNCIL-TERMS-v1; decision=CONSENSUS — denominator=3; read-back=CONFIRMED; rationale=bounded finding |",
        "atlas.freeze.recusal-dispute-records-mismatch")]
    public void Detached_manifest_exactly_binds_absence_and_recusal_dispute_records(
        string validField,
        string mismatchedField,
        string issueCode)
    {
        var recordBytes = CompletedRecordBytes();
        var manifest = ReplaceRequired(
            SyntheticManifest(recordBytes),
            validField,
            mismatchedField);

        var result = AtlasCouncilRecordValidator.ValidateFreezeManifest(
            ManifestFile,
            Utf8(manifest),
            RecordFile,
            recordBytes);

        Assert.Contains(result.Issues, issue => issue.Code == issueCode);
    }

    [Fact]
    public void Detached_manifest_exactly_binds_the_constituted_seat_authority_entries()
    {
        var recordBytes = CompletedRecordBytes();
        var manifest = ReplaceRequired(
            SyntheticManifest(recordBytes),
            "private-custodian=CUST-P-03",
            "private-custodian=CUST-DIFFERENT");

        var result = AtlasCouncilRecordValidator.ValidateFreezeManifest(
            ManifestFile,
            Utf8(manifest),
            RecordFile,
            recordBytes);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "atlas.freeze.seat-authority-mismatch");
    }

    [Fact]
    public void Detached_manifest_requires_completed_review_fields_and_canonical_name()
    {
        var recordBytes = CompletedRecordBytes();
        var incomplete = SyntheticManifest(recordBytes).Replace(
            "| Participant read-back/review of those exact bytes completed (seat + count, no names by default) | general educator: 3 |",
            "| Participant read-back/review of those exact bytes completed (seat + count, no names by default) | [not supplied] |",
            StringComparison.Ordinal);

        var result = AtlasCouncilRecordValidator.ValidateFreezeManifest(
            "wrong-freeze-name.md",
            Utf8(incomplete),
            RecordFile,
            recordBytes);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.freeze.file-name");
        Assert.Contains(result.Issues, issue => issue.Code == "atlas.freeze.field-pending");
    }

    [Fact]
    public void Detached_manifest_requires_final_byte_publication_reconfirmation()
    {
        var recordBytes = CompletedRecordBytes();
        var manifest = ReplaceRequired(
            SyntheticManifest(recordBytes),
            "| Exact-final-byte public-record publication permission reconfirmed after participant review | RECONFIRMED — general educator: 3 |",
            "| Omitted exact-final-byte publication permission field | RECONFIRMED — general educator: 3 |");

        var result = AtlasCouncilRecordValidator.ValidateFreezeManifest(
            ManifestFile,
            Utf8(manifest),
            RecordFile,
            recordBytes);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.freeze.field-missing");
    }

    [Fact]
    public void Detached_manifest_reconfirmation_must_match_the_exact_present_seats()
    {
        var recordBytes = CompletedRecordBytes();
        var manifest = ReplaceRequired(
            SyntheticManifest(recordBytes),
            "| Exact-final-byte public-record publication permission reconfirmed after participant review | RECONFIRMED — general educator: 3 |",
            "| Exact-final-byte public-record publication permission reconfirmed after participant review | RECONFIRMED — general educator: 2 |");

        var result = AtlasCouncilRecordValidator.ValidateFreezeManifest(
            ManifestFile,
            Utf8(manifest),
            RecordFile,
            recordBytes);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "atlas.freeze.final-byte-publication-consent-invalid");
    }

    [Fact]
    public void Detached_manifest_accepts_resolved_correction_references()
    {
        var recordBytes = CompletedRecordBytes();
        var manifest = ReplaceRequired(
            SyntheticManifest(recordBytes),
            "| Requested corrections and accountable owners | NONE — no correction requested |",
            "| Requested corrections and accountable owners | RESOLVED — CORR-1; unresolved=NONE |");

        var result = AtlasCouncilRecordValidator.ValidateFreezeManifest(
            ManifestFile,
            Utf8(manifest),
            RecordFile,
            recordBytes);

        Assert.True(result.IsValid, Describe(result));
    }

    [Theory]
    [InlineData("none recorded")]
    [InlineData("RESOLVED — CORR-1; unresolved=HOLD")]
    [InlineData("RESOLVED — none; unresolved=NONE")]
    public void Detached_manifest_refuses_malformed_or_unresolved_correction_records(
        string correctionRecord)
    {
        var recordBytes = CompletedRecordBytes();
        var manifest = ReplaceRequired(
            SyntheticManifest(recordBytes),
            "| Requested corrections and accountable owners | NONE — no correction requested |",
            $"| Requested corrections and accountable owners | {correctionRecord} |");

        var result = AtlasCouncilRecordValidator.ValidateFreezeManifest(
            ManifestFile,
            Utf8(manifest),
            RecordFile,
            recordBytes);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "atlas.freeze.correction-resolution-invalid");
    }

    [Theory]
    [InlineData("confirmed")]
    [InlineData("NOT INCORPORATED")]
    [InlineData("CONFIRMED — unresolved corrections remain")]
    public void Detached_manifest_requires_exact_correction_and_dissent_incorporation(
        string incorporation)
    {
        var recordBytes = CompletedRecordBytes();
        var manifest = ReplaceRequired(
            SyntheticManifest(recordBytes),
            "| Corrections and dissent incorporated before final hashing | CONFIRMED — all corrections resolved and dissent preserved in final H0 bytes |",
            $"| Corrections and dissent incorporated before final hashing | {incorporation} |");

        var result = AtlasCouncilRecordValidator.ValidateFreezeManifest(
            ManifestFile,
            Utf8(manifest),
            RecordFile,
            recordBytes);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "atlas.freeze.correction-incorporation-invalid");
    }

    [Fact]
    public void Detached_manifest_accepts_exact_non_none_withdrawal_reconciliation()
    {
        var (recordBytes, manifest) = SyntheticManifestWithWithdrawalRequests();

        var result = AtlasCouncilRecordValidator.ValidateFreezeManifest(
            ManifestFile,
            Utf8(manifest),
            RecordFile,
            recordBytes);

        Assert.True(result.IsValid, Describe(result));
    }

    [Theory]
    [InlineData("HONORED — NONE RECEIVED; unresolved=NONE")]
    [InlineData("HONORED — activity-withdrawal=ACT-REQ-2; council-resignation-vacancy=VAC-REQ-1; unresolved=NONE")]
    [InlineData("HONORED — activity-withdrawal=ACT-REQ-1; council-resignation-vacancy=NONE; unresolved=NONE")]
    public void Detached_manifest_refuses_contradictory_or_mismatched_withdrawal_reconciliation(
        string disposition)
    {
        var (recordBytes, validManifest) = SyntheticManifestWithWithdrawalRequests();
        var manifest = ReplaceRequired(
            validManifest,
            "| Pre-freeze withdrawal/removal requests honored; unresolved requests | HONORED — activity-withdrawal=ACT-REQ-1; council-resignation-vacancy=VAC-REQ-1; unresolved=NONE |",
            $"| Pre-freeze withdrawal/removal requests honored; unresolved requests | {disposition} |");

        var result = AtlasCouncilRecordValidator.ValidateFreezeManifest(
            ManifestFile,
            Utf8(manifest),
            RecordFile,
            recordBytes);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "atlas.freeze.withdrawal-resolution-invalid");
    }

    [Theory]
    [InlineData("HONORED — NONE RECEIVED")]
    [InlineData("HONORED — REQ-1; unresolved=HOLD")]
    [InlineData("PENDING — NONE RECEIVED; unresolved=NONE")]
    public void Detached_manifest_refuses_malformed_or_unresolved_pre_freeze_withdrawals(
        string disposition)
    {
        var recordBytes = CompletedRecordBytes();
        var manifest = ReplaceRequired(
            SyntheticManifest(recordBytes),
            "| Pre-freeze withdrawal/removal requests honored; unresolved requests | HONORED — NONE RECEIVED; unresolved=NONE |",
            $"| Pre-freeze withdrawal/removal requests honored; unresolved requests | {disposition} |");

        var result = AtlasCouncilRecordValidator.ValidateFreezeManifest(
            ManifestFile,
            Utf8(manifest),
            RecordFile,
            recordBytes);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "atlas.freeze.withdrawal-resolution-invalid");
    }

    [Fact]
    public void Detached_manifest_refuses_later_authority_or_release_sections()
    {
        var recordBytes = CompletedRecordBytes();
        var manifest = SyntheticManifest(recordBytes)
            + "\n## Feasibility assessment\n\nNot part of the detached manifest.\n";

        var result = AtlasCouncilRecordValidator.ValidateFreezeManifest(
            ManifestFile,
            Utf8(manifest),
            RecordFile,
            recordBytes);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.freeze.section-boundary");
    }

    [Fact]
    public void Feasibility_is_a_separate_record_bound_to_record_and_manifest_bytes()
    {
        var recordBytes = CompletedRecordBytes();
        var manifestBytes = Utf8(SyntheticManifest(recordBytes));
        var feasibilityBytes = Utf8(SyntheticFeasibility(recordBytes, manifestBytes));

        var result = AtlasCouncilRecordValidator.ValidateFeasibilityRecord(
            FeasibilityFile,
            feasibilityBytes,
            RecordFile,
            recordBytes,
            ManifestFile,
            manifestBytes);

        Assert.True(result.IsValid, Describe(result));
    }

    [Theory]
    [InlineData(
        "| Upstream chain-audit ID | CHAIN-H0-SYNTHETIC-v1 |",
        "| Upstream chain-audit ID | CHAIN- |",
        "atlas.feasibility.chain-audit-id-invalid")]
    [InlineData(
        "| Upstream chain-audit UTC instant | 2030-01-03T12:30:00Z |",
        "| Upstream chain-audit UTC instant | 2030-01-03 12:30Z |",
        "atlas.feasibility.chain-audit-utc-invalid")]
    [InlineData(
        "| Upstream chain-audit repository path, version, byte length, and SHA-256 | BOUND — path=docs/council/atlas-priority-session-2030-01-02-chain-audit-v1.md; version=v1; bytes=3456; sha256:dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd |",
        "| Upstream chain-audit repository path, version, byte length, and SHA-256 | BOUND — path=docs/council/atlas-priority-session-2030-01-02-chain-audit-v1.md; version=v1; bytes=0; sha256:dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd |",
        "atlas.feasibility.chain-audit-binding-invalid")]
    [InlineData(
        "| Chain-audit exact candidate repository revision and dirty-tree disposition | 0000000 · clean synthetic tree |",
        "| Chain-audit exact candidate repository revision and dirty-tree disposition | not-a-revision · clean synthetic tree |",
        "atlas.feasibility.chain-audit-revision-invalid")]
    [InlineData(
        "| Public append-only event bindings, or NONE | NONE — no public event through candidate revision |",
        "| Public append-only event bindings, or NONE | NONE — [not supplied] |",
        "atlas.feasibility.public-event-bindings-invalid")]
    [InlineData(
        "| Private append-only event attestations, or NONE | NONE — no private event attested through audit instant |",
        "| Private append-only event attestations, or NONE | ATTESTED — REC-1 |",
        "atlas.feasibility.private-event-attestations-invalid")]
    public void Feasibility_refuses_malformed_chain_audit_evidence(
        string validField,
        string malformedField,
        string issueCode)
    {
        var recordBytes = CompletedRecordBytes();
        var manifestBytes = Utf8(SyntheticManifest(recordBytes));
        var feasibility = ReplaceRequired(
            SyntheticFeasibility(recordBytes, manifestBytes),
            validField,
            malformedField);

        var result = AtlasCouncilRecordValidator.ValidateFeasibilityRecord(
            FeasibilityFile,
            Utf8(feasibility),
            RecordFile,
            recordBytes,
            ManifestFile,
            manifestBytes);

        Assert.Contains(result.Issues, issue => issue.Code == issueCode);
    }

    [Theory]
    [InlineData("2030-01-03T12:00:00Z", "atlas.feasibility.chain-audit-chronology-invalid")]
    [InlineData("2030-01-03T13:00:00Z", "atlas.feasibility.chain-audit-stale")]
    public void Feasibility_requires_a_fresh_chain_audit_strictly_between_freeze_and_record(
        string auditInstant,
        string issueCode)
    {
        var recordBytes = CompletedRecordBytes();
        var manifestBytes = Utf8(SyntheticManifest(recordBytes));
        var feasibility = ReplaceRequired(
            SyntheticFeasibility(recordBytes, manifestBytes),
            "| Upstream chain-audit UTC instant | 2030-01-03T12:30:00Z |",
            $"| Upstream chain-audit UTC instant | {auditInstant} |");

        var result = AtlasCouncilRecordValidator.ValidateFeasibilityRecord(
            FeasibilityFile,
            Utf8(feasibility),
            RecordFile,
            recordBytes,
            ManifestFile,
            manifestBytes);

        Assert.Contains(result.Issues, issue => issue.Code == issueCode);
    }

    [Theory]
    [InlineData("CURRENT — H0 frozen bytes withdrawn effective; unresolved-chain-holds=NONE")]
    [InlineData("CURRENT — H0 frozen bytes restricted effective; unresolved-chain-holds=NONE")]
    [InlineData("CURRENT — H0 frozen bytes stale effective; unresolved-chain-holds=NONE")]
    [InlineData("CURRENT — H0 frozen bytes effective; unresolved-chain-holds=HOLD")]
    [InlineData("CURRENT — H0 frozen bytes; unresolved-chain-holds=NONE")]
    public void Feasibility_refuses_noncurrent_or_unresolved_chain_dispositions(string disposition)
    {
        var recordBytes = CompletedRecordBytes();
        var manifestBytes = Utf8(SyntheticManifest(recordBytes));
        var feasibility = ReplaceRequired(
            SyntheticFeasibility(recordBytes, manifestBytes),
            "| Current effective upstream dispositions and unresolved chain holds | CURRENT — H0 frozen bytes effective; unresolved-chain-holds=NONE |",
            $"| Current effective upstream dispositions and unresolved chain holds | {disposition} |");

        var result = AtlasCouncilRecordValidator.ValidateFeasibilityRecord(
            FeasibilityFile,
            Utf8(feasibility),
            RecordFile,
            recordBytes,
            ManifestFile,
            manifestBytes);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "atlas.feasibility.chain-disposition-invalid");
    }

    [Theory]
    [InlineData(false, "ambiguous-state")]
    [InlineData(false, "conflicting-event")]
    [InlineData(false, "missing-link")]
    [InlineData(false, "restricted-use")]
    [InlineData(false, "revoked-permission")]
    [InlineData(false, "stale-roster")]
    [InlineData(false, "superseded-record")]
    [InlineData(false, "unresolved-event")]
    [InlineData(false, "withdrawn-from-use")]
    [InlineData(false, "roster-stale")]
    [InlineData(false, "chain-withdrawn-from-use")]
    [InlineData(false, "WiThDrAwN-from-use")]
    [InlineData(true, "ambiguous-state")]
    [InlineData(true, "conflicting-event")]
    [InlineData(true, "missing-link")]
    [InlineData(true, "restricted-use")]
    [InlineData(true, "revoked-permission")]
    [InlineData(true, "stale-roster")]
    [InlineData(true, "superseded-record")]
    [InlineData(true, "unresolved-event")]
    [InlineData(true, "withdrawn-from-use")]
    [InlineData(true, "roster-stale")]
    [InlineData(true, "chain-withdrawn-from-use")]
    [InlineData(true, "WiThDrAwN-from-use")]
    public void Current_chain_dispositions_refuse_hyphenated_forbidden_states(
        bool productOwnerDisposition,
        string state)
    {
        var upstream = productOwnerDisposition ? "H0 frozen bytes and feasibility v1" : "H0 frozen bytes";
        var result = ValidateSyntheticDownstreamRecordWithChangedField(
            productOwnerDisposition,
            $"| Current effective upstream dispositions and unresolved chain holds | CURRENT — {upstream} effective; unresolved-chain-holds=NONE |",
            $"| Current effective upstream dispositions and unresolved chain holds | CURRENT — {upstream} {state} effective; unresolved-chain-holds=NONE |");
        var consumer = productOwnerDisposition ? "disposition" : "feasibility";

        Assert.False(
            result.IsValid,
            $"The {consumer} validator accepted explicit current-chain state '{state}' as effective.");
        Assert.Contains(result.Issues, issue => issue.Code == $"atlas.{consumer}.chain-disposition-invalid");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Current_chain_dispositions_preserve_benign_hyphenated_text(bool productOwnerDisposition)
    {
        var upstream = productOwnerDisposition ? "H0 frozen bytes and feasibility v1" : "H0 frozen bytes";
        var result = ValidateSyntheticDownstreamRecordWithChangedField(
            productOwnerDisposition,
            $"| Current effective upstream dispositions and unresolved chain holds | CURRENT — {upstream} effective; unresolved-chain-holds=NONE |",
            $"| Current effective upstream dispositions and unresolved chain holds | CURRENT — {upstream} final-byte records effective; unresolved-chain-holds=NONE |");

        Assert.True(result.IsValid, Describe(result));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Current_chain_dispositions_leave_opaque_reference_token_grammar_unchanged(bool productOwnerDisposition)
    {
        var result = ValidateSyntheticDownstreamRecordWithChangedField(
            productOwnerDisposition,
            "| Private append-only event attestations, or NONE | NONE — no private event attested through audit instant |",
            "| Private append-only event attestations, or NONE | ATTESTED — custodian-ref=CUST-NONE-01 |");

        Assert.True(result.IsValid, Describe(result));
    }

    [Fact]
    public void Feasibility_version_two_binds_its_immediate_predecessor()
    {
        var recordBytes = CompletedRecordBytes();
        var manifestBytes = Utf8(SyntheticManifest(recordBytes));
        var feasibility = SyntheticFeasibilityVersionTwo(recordBytes, manifestBytes);

        var result = AtlasCouncilRecordValidator.ValidateFeasibilityRecord(
            "atlas-priority-session-2030-01-02-feasibility-v2.md",
            Utf8(feasibility),
            RecordFile,
            recordBytes,
            ManifestFile,
            manifestBytes);

        Assert.True(result.IsValid, Describe(result));
    }

    [Theory]
    [InlineData(
        "| Feasibility record ID and version | H0-SYNTHETIC-FEASIBILITY v2 |",
        "| Feasibility record ID and version | H0-SYNTHETIC-FEASIBILITY v3 |",
        "atlas.feasibility.version-identity-mismatch")]
    [InlineData(
        "| Predecessor feasibility record path, version, byte length, and SHA-256; or NONE | BOUND — path=docs/council/atlas-priority-session-2030-01-02-feasibility-v1.md; version=v1; bytes=1234; sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff |",
        "| Predecessor feasibility record path, version, byte length, and SHA-256; or NONE | BOUND — path=docs/council/atlas-priority-session-2030-01-02-feasibility-v0.md; version=v0; bytes=1234; sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff |",
        "atlas.feasibility.predecessor-invalid")]
    public void Feasibility_version_two_refuses_identity_or_predecessor_drift(
        string validField,
        string invalidField,
        string issueCode)
    {
        var recordBytes = CompletedRecordBytes();
        var manifestBytes = Utf8(SyntheticManifest(recordBytes));
        var feasibility = ReplaceRequired(
            SyntheticFeasibilityVersionTwo(recordBytes, manifestBytes),
            validField,
            invalidField);

        var result = AtlasCouncilRecordValidator.ValidateFeasibilityRecord(
            "atlas-priority-session-2030-01-02-feasibility-v2.md",
            Utf8(feasibility),
            RecordFile,
            recordBytes,
            ManifestFile,
            manifestBytes);

        Assert.Contains(result.Issues, issue => issue.Code == issueCode);
    }

    [Fact]
    public void Feasibility_version_filename_refuses_leading_zeroes()
    {
        var recordBytes = CompletedRecordBytes();
        var manifestBytes = Utf8(SyntheticManifest(recordBytes));
        var feasibility = SyntheticFeasibilityVersionTwo(recordBytes, manifestBytes);

        var result = AtlasCouncilRecordValidator.ValidateFeasibilityRecord(
            "atlas-priority-session-2030-01-02-feasibility-v02.md",
            Utf8(feasibility),
            RecordFile,
            recordBytes,
            ManifestFile,
            manifestBytes);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.feasibility.file-name");
    }

    [Fact]
    public void Feasibility_refuses_manifest_mutation_and_incomplete_recommendation_coverage()
    {
        var recordBytes = CompletedRecordBytes();
        var manifestBytes = Utf8(SyntheticManifest(recordBytes));
        var feasibility = SyntheticFeasibility(recordBytes, manifestBytes)
            .Replace(
                "| Synthetic possibility | Existing component | Bounded synthetic slice | None | Machine and later human evidence | Small | None recorded |",
                "| | | | | | | |",
                StringComparison.Ordinal);
        var changedManifestBytes = Utf8(Encoding.UTF8.GetString(manifestBytes) + "\n");

        var result = AtlasCouncilRecordValidator.ValidateFeasibilityRecord(
            FeasibilityFile,
            Utf8(feasibility),
            RecordFile,
            recordBytes,
            ManifestFile,
            changedManifestBytes);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.feasibility.manifest-hash-mismatch");
        Assert.Contains(result.Issues, issue => issue.Code == "atlas.feasibility.coverage-incomplete");
    }

    [Fact]
    public void Feasibility_refuses_product_owner_or_release_sections()
    {
        var recordBytes = CompletedRecordBytes();
        var manifestBytes = Utf8(SyntheticManifest(recordBytes));
        var feasibility = SyntheticFeasibility(recordBytes, manifestBytes)
            + "\n## Product-owner disposition\n\nNot a feasibility finding.\n";

        var result = AtlasCouncilRecordValidator.ValidateFeasibilityRecord(
            FeasibilityFile,
            Utf8(feasibility),
            RecordFile,
            recordBytes,
            ManifestFile,
            manifestBytes);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.feasibility.section-boundary");
    }

    [Fact]
    public void Product_owner_disposition_is_separate_and_binds_feasibility_bytes()
    {
        var recordBytes = CompletedRecordBytes();
        var manifestBytes = Utf8(SyntheticManifest(recordBytes));
        var feasibilityBytes = Utf8(SyntheticFeasibility(recordBytes, manifestBytes));
        var dispositionBytes = Utf8(SyntheticDisposition(recordBytes, manifestBytes, feasibilityBytes));

        var result = AtlasCouncilRecordValidator.ValidateDispositionRecord(
            DispositionFile,
            dispositionBytes,
            RecordFile,
            recordBytes,
            ManifestFile,
            manifestBytes,
            FeasibilityFile,
            feasibilityBytes);

        Assert.True(result.IsValid, Describe(result));
        Assert.True(result.IsRecordedDisposition);
        Assert.False(result.IsHeldDisposition);
    }

    [Theory]
    [InlineData("HELD — conflict-category=financial interest; written-finding=material conflict prevents product-owner action; adoption=NONE")]
    [InlineData("SUBSTITUTED — authority-record=REC-1")]
    [InlineData("SUBSTITUTED — authority-record=none; conflict-category=rights conflict")]
    [InlineData("SUBSTITUTED — authority-path=docs/governance/disposition-authority-v1.md; authority-version=v1; authority-bytes=567; authority-sha256:8888888888888888888888888888888888888888888888888888888888888888; authority-commit=0000000; conflict-category=rights conflict")]
    public void Recorded_product_owner_disposition_refuses_held_or_substitute_conflict_routes_under_v1(
        string conflictDisposition)
    {
        var recordBytes = CompletedRecordBytes();
        var manifestBytes = Utf8(SyntheticManifest(recordBytes));
        var feasibilityBytes = Utf8(SyntheticFeasibility(recordBytes, manifestBytes));
        var disposition = ReplaceRequired(
            SyntheticDisposition(recordBytes, manifestBytes, feasibilityBytes),
            "| Product-owner conflict category and disposition | NONE — no material conflict disclosed |",
            $"| Product-owner conflict category and disposition | {conflictDisposition} |");

        var result = AtlasCouncilRecordValidator.ValidateDispositionRecord(
            DispositionFile,
            Utf8(disposition),
            RecordFile,
            recordBytes,
            ManifestFile,
            manifestBytes,
            FeasibilityFile,
            feasibilityBytes);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "atlas.disposition.product-owner-conflict-invalid");
    }

    [Fact]
    public void Held_product_owner_disposition_is_a_fully_bound_terminal_record_without_actions()
    {
        var recordBytes = CompletedRecordBytes();
        var manifestBytes = Utf8(SyntheticManifest(recordBytes));
        var feasibilityBytes = Utf8(SyntheticFeasibility(recordBytes, manifestBytes));
        var heldDisposition = SyntheticHeldDisposition(
            recordBytes,
            manifestBytes,
            feasibilityBytes);

        var result = AtlasCouncilRecordValidator.ValidateDispositionRecord(
            DispositionFile,
            Utf8(heldDisposition),
            RecordFile,
            recordBytes,
            ManifestFile,
            manifestBytes,
            FeasibilityFile,
            feasibilityBytes);

        Assert.True(result.IsValid, Describe(result));
        Assert.True(result.IsHeldDisposition);
        Assert.False(result.IsRecordedDisposition);
    }

    [Fact]
    public void Held_product_owner_disposition_refuses_any_substantive_action_row()
    {
        var recordBytes = CompletedRecordBytes();
        var manifestBytes = Utf8(SyntheticManifest(recordBytes));
        var feasibilityBytes = Utf8(SyntheticFeasibility(recordBytes, manifestBytes));
        var heldDisposition = ReplaceRequired(
            SyntheticHeldDisposition(recordBytes, manifestBytes, feasibilityBytes),
            "| | | | | | |",
            "| Synthetic possibility | DEFER — 2030-01-04 | No implementation | Await owner evidence | AAC/SLP remains outstanding | Separate protected review |");

        var result = AtlasCouncilRecordValidator.ValidateDispositionRecord(
            DispositionFile,
            Utf8(heldDisposition),
            RecordFile,
            recordBytes,
            ManifestFile,
            manifestBytes,
            FeasibilityFile,
            feasibilityBytes);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "atlas.disposition.held-action-present");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Held_product_owner_disposition_refuses_nonheld_conflict_routes(bool useSubstitute)
    {
        var recordBytes = CompletedRecordBytes();
        var manifestBytes = Utf8(SyntheticManifest(recordBytes));
        var feasibilityBytes = Utf8(SyntheticFeasibility(recordBytes, manifestBytes));
        var conflictDisposition = useSubstitute
            ? $"SUBSTITUTED — authority-path=docs/governance/disposition-authority-v1.md; authority-version=v1; authority-bytes=567; authority-sha256:{new string('8', 64)}; authority-commit=0000000; conflict-category=financial interest"
            : "NONE — no material conflict disclosed";
        var heldDisposition = ReplaceRequired(
            SyntheticHeldDisposition(recordBytes, manifestBytes, feasibilityBytes),
            "| Product-owner conflict category and disposition | HELD — conflict-category=financial interest; written-finding=material conflict prevents product-owner action; adoption=NONE |",
            $"| Product-owner conflict category and disposition | {conflictDisposition} |");

        var result = AtlasCouncilRecordValidator.ValidateDispositionRecord(
            DispositionFile,
            Utf8(heldDisposition),
            RecordFile,
            recordBytes,
            ManifestFile,
            manifestBytes,
            FeasibilityFile,
            feasibilityBytes);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "atlas.disposition.held-conflict-invalid");
    }

    [Theory]
    [InlineData(
        "| Upstream chain-audit repository path, version, byte length, and SHA-256 | BOUND — path=docs/council/atlas-priority-session-2030-01-02-chain-audit-v2.md; version=v2; bytes=4567; sha256:eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee |",
        "| Upstream chain-audit repository path, version, byte length, and SHA-256 | BOUND — path=docs/council/atlas-priority-session-2030-01-02-chain-audit-v2.md; version=v2; bytes=0; sha256:eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee |",
        "atlas.disposition.chain-audit-binding-invalid")]
    [InlineData(
        "| Upstream chain-audit UTC instant | 2030-01-03T14:00:00Z |",
        "| Upstream chain-audit UTC instant | 2030-01-04T12:00:00Z |",
        "atlas.disposition.chain-audit-stale")]
    public void Held_product_owner_disposition_still_requires_valid_fresh_links(
        string validField,
        string invalidField,
        string issueCode)
    {
        var recordBytes = CompletedRecordBytes();
        var manifestBytes = Utf8(SyntheticManifest(recordBytes));
        var feasibilityBytes = Utf8(SyntheticFeasibility(recordBytes, manifestBytes));
        var heldDisposition = ReplaceRequired(
            SyntheticHeldDisposition(recordBytes, manifestBytes, feasibilityBytes),
            validField,
            invalidField);

        var result = AtlasCouncilRecordValidator.ValidateDispositionRecord(
            DispositionFile,
            Utf8(heldDisposition),
            RecordFile,
            recordBytes,
            ManifestFile,
            manifestBytes,
            FeasibilityFile,
            feasibilityBytes);

        Assert.Contains(result.Issues, issue => issue.Code == issueCode);
        Assert.Equal(AtlasCouncilDispositionState.Held, result.DispositionState);
        Assert.False(result.IsHeldDisposition);
        Assert.False(result.IsRecordedDisposition);
    }

    [Fact]
    public void Product_owner_disposition_version_two_binds_its_immediate_predecessor()
    {
        var recordBytes = CompletedRecordBytes();
        var manifestBytes = Utf8(SyntheticManifest(recordBytes));
        var feasibilityBytes = Utf8(SyntheticFeasibility(recordBytes, manifestBytes));
        var disposition = SyntheticDispositionVersionTwo(
            recordBytes,
            manifestBytes,
            feasibilityBytes);

        var result = AtlasCouncilRecordValidator.ValidateDispositionRecord(
            "atlas-priority-session-2030-01-02-disposition-v2.md",
            Utf8(disposition),
            RecordFile,
            recordBytes,
            ManifestFile,
            manifestBytes,
            FeasibilityFile,
            feasibilityBytes);

        Assert.True(result.IsValid, Describe(result));
    }

    [Theory]
    [InlineData(
        "| Product-owner disposition record ID and version | H0-SYNTHETIC-DISPOSITION v2 |",
        "| Product-owner disposition record ID and version | H0-SYNTHETIC-DISPOSITION v3 |",
        "atlas.disposition.version-identity-mismatch")]
    [InlineData(
        "| Predecessor disposition record path, version, byte length, and SHA-256; or NONE | BOUND — path=docs/council/atlas-priority-session-2030-01-02-disposition-v1.md; version=v1; bytes=1234; sha256:9999999999999999999999999999999999999999999999999999999999999999 |",
        "| Predecessor disposition record path, version, byte length, and SHA-256; or NONE | BOUND — path=docs/council/atlas-priority-session-2030-01-02-disposition-v0.md; version=v0; bytes=1234; sha256:9999999999999999999999999999999999999999999999999999999999999999 |",
        "atlas.disposition.predecessor-invalid")]
    public void Product_owner_disposition_version_two_refuses_identity_or_predecessor_drift(
        string validField,
        string invalidField,
        string issueCode)
    {
        var recordBytes = CompletedRecordBytes();
        var manifestBytes = Utf8(SyntheticManifest(recordBytes));
        var feasibilityBytes = Utf8(SyntheticFeasibility(recordBytes, manifestBytes));
        var disposition = ReplaceRequired(
            SyntheticDispositionVersionTwo(recordBytes, manifestBytes, feasibilityBytes),
            validField,
            invalidField);

        var result = AtlasCouncilRecordValidator.ValidateDispositionRecord(
            "atlas-priority-session-2030-01-02-disposition-v2.md",
            Utf8(disposition),
            RecordFile,
            recordBytes,
            ManifestFile,
            manifestBytes,
            FeasibilityFile,
            feasibilityBytes);

        Assert.Contains(result.Issues, issue => issue.Code == issueCode);
    }

    [Fact]
    public void Product_owner_disposition_version_filename_refuses_leading_zeroes()
    {
        var recordBytes = CompletedRecordBytes();
        var manifestBytes = Utf8(SyntheticManifest(recordBytes));
        var feasibilityBytes = Utf8(SyntheticFeasibility(recordBytes, manifestBytes));
        var disposition = SyntheticDispositionVersionTwo(
            recordBytes,
            manifestBytes,
            feasibilityBytes);

        var result = AtlasCouncilRecordValidator.ValidateDispositionRecord(
            "atlas-priority-session-2030-01-02-disposition-v02.md",
            Utf8(disposition),
            RecordFile,
            recordBytes,
            ManifestFile,
            manifestBytes,
            FeasibilityFile,
            feasibilityBytes);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.disposition.file-name");
    }

    [Fact]
    public void Product_owner_disposition_refuses_feasibility_mutation_and_missing_coverage()
    {
        var recordBytes = CompletedRecordBytes();
        var manifestBytes = Utf8(SyntheticManifest(recordBytes));
        var feasibilityBytes = Utf8(SyntheticFeasibility(recordBytes, manifestBytes));
        var disposition = SyntheticDisposition(recordBytes, manifestBytes, feasibilityBytes)
            .Replace(
                "| Synthetic possibility | DEFER — 2030-01-04 | No implementation | Await owner evidence | AAC/SLP remains outstanding | Separate protected review |",
                "| | | | | | |",
                StringComparison.Ordinal);
        var changedFeasibilityBytes = Utf8(Encoding.UTF8.GetString(feasibilityBytes) + "\n");

        var result = AtlasCouncilRecordValidator.ValidateDispositionRecord(
            DispositionFile,
            Utf8(disposition),
            RecordFile,
            recordBytes,
            ManifestFile,
            manifestBytes,
            FeasibilityFile,
            changedFeasibilityBytes);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.disposition.feasibility-hash-mismatch");
        Assert.Contains(result.Issues, issue => issue.Code == "atlas.disposition.coverage-incomplete");
    }

    [Fact]
    public void Product_owner_disposition_refuses_appended_release_sections()
    {
        var recordBytes = CompletedRecordBytes();
        var manifestBytes = Utf8(SyntheticManifest(recordBytes));
        var feasibilityBytes = Utf8(SyntheticFeasibility(recordBytes, manifestBytes));
        var disposition = SyntheticDisposition(recordBytes, manifestBytes, feasibilityBytes)
            + "\n## Release authorization\n\nNot authorized here.\n";

        var result = AtlasCouncilRecordValidator.ValidateDispositionRecord(
            DispositionFile,
            Utf8(disposition),
            RecordFile,
            recordBytes,
            ManifestFile,
            manifestBytes,
            FeasibilityFile,
            feasibilityBytes);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.disposition.section-boundary");
    }

    [Fact]
    public void Product_owner_disposition_refuses_unowned_actions_even_inside_a_complete_row()
    {
        var recordBytes = CompletedRecordBytes();
        var manifestBytes = Utf8(SyntheticManifest(recordBytes));
        var feasibilityBytes = Utf8(SyntheticFeasibility(recordBytes, manifestBytes));
        var disposition = SyntheticDisposition(recordBytes, manifestBytes, feasibilityBytes)
            .Replace(
                "DEFER — 2030-01-04",
                "RELEASE AUTHORIZED — 2030-01-04",
                StringComparison.Ordinal);

        var result = AtlasCouncilRecordValidator.ValidateDispositionRecord(
            DispositionFile,
            Utf8(disposition),
            RecordFile,
            recordBytes,
            ManifestFile,
            manifestBytes,
            FeasibilityFile,
            feasibilityBytes);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.disposition.action-invalid");
        Assert.Contains(result.Issues, issue => issue.Code == "atlas.disposition.authority-overreach");
    }

    [Fact]
    public void Product_owner_disposition_cannot_erase_an_upstream_protected_hold()
    {
        var recordBytes = CompletedRecordBytes();
        var manifestBytes = Utf8(SyntheticManifest(recordBytes));
        var feasibilityBytes = Utf8(SyntheticFeasibility(recordBytes, manifestBytes));
        var disposition = SyntheticDisposition(recordBytes, manifestBytes, feasibilityBytes)
            .Replace(
                "| Synthetic possibility | DEFER — 2030-01-04 | No implementation | Await owner evidence | AAC/SLP remains outstanding | Separate protected review |",
                "| Synthetic possibility | DEFER — 2030-01-04 | No implementation | Await owner evidence | None | Separate protected review |",
                StringComparison.Ordinal);

        var result = AtlasCouncilRecordValidator.ValidateDispositionRecord(
            DispositionFile,
            Utf8(disposition),
            RecordFile,
            recordBytes,
            ManifestFile,
            manifestBytes,
            FeasibilityFile,
            feasibilityBytes);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "atlas.disposition.protected-seat-hold-missing");
    }

    [Fact]
    public void Product_owner_disposition_refuses_approval_and_waiver_synonyms()
    {
        var recordBytes = CompletedRecordBytes();
        var manifestBytes = Utf8(SyntheticManifest(recordBytes));
        var feasibilityBytes = Utf8(SyntheticFeasibility(recordBytes, manifestBytes));
        var disposition = SyntheticDisposition(recordBytes, manifestBytes, feasibilityBytes)
            .Replace(
                "Await owner evidence",
                "Publication approved by this disposition; protected-seat hold waived.",
                StringComparison.Ordinal);

        var result = AtlasCouncilRecordValidator.ValidateDispositionRecord(
            DispositionFile,
            Utf8(disposition),
            RecordFile,
            recordBytes,
            ManifestFile,
            manifestBytes,
            FeasibilityFile,
            feasibilityBytes);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.disposition.authority-overreach");
    }

    [Fact]
    public void Product_owner_disposition_does_not_let_a_later_negation_mask_an_authority_claim()
    {
        var recordBytes = CompletedRecordBytes();
        var manifestBytes = Utf8(SyntheticManifest(recordBytes));
        var feasibilityBytes = Utf8(SyntheticFeasibility(recordBytes, manifestBytes));
        var disposition = SyntheticDisposition(recordBytes, manifestBytes, feasibilityBytes)
            .Replace(
                "Await owner evidence",
                "Publication approved by this disposition; it does not ratify an ADR.",
                StringComparison.Ordinal);

        var result = AtlasCouncilRecordValidator.ValidateDispositionRecord(
            DispositionFile,
            Utf8(disposition),
            RecordFile,
            recordBytes,
            ManifestFile,
            manifestBytes,
            FeasibilityFile,
            feasibilityBytes);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.disposition.authority-overreach");
    }

    [Fact]
    public void Linked_records_require_strict_utc_instants_in_lifecycle_order()
    {
        var recordBytes = CompletedRecordBytes();
        var manifest = SyntheticManifest(recordBytes);
        var malformedManifest = manifest.Replace(
            "2030-01-03T12:00:00Z",
            "TBD",
            StringComparison.Ordinal);
        var manifestResult = AtlasCouncilRecordValidator.ValidateFreezeManifest(
            ManifestFile,
            Utf8(malformedManifest),
            RecordFile,
            recordBytes);
        Assert.Contains(manifestResult.Issues, issue => issue.Code == "atlas.freeze.utc-invalid");

        var manifestBytes = Utf8(manifest);
        var reversedFeasibility = SyntheticFeasibility(recordBytes, manifestBytes).Replace(
            "2030-01-03T13:00:00Z",
            "2030-01-03T11:00:00Z",
            StringComparison.Ordinal);
        var feasibilityResult = AtlasCouncilRecordValidator.ValidateFeasibilityRecord(
            FeasibilityFile,
            Utf8(reversedFeasibility),
            RecordFile,
            recordBytes,
            ManifestFile,
            manifestBytes);
        Assert.Contains(feasibilityResult.Issues, issue => issue.Code == "atlas.feasibility.chronology-invalid");

        var feasibilityBytes = Utf8(SyntheticFeasibility(recordBytes, manifestBytes));
        var reversedDisposition = SyntheticDisposition(recordBytes, manifestBytes, feasibilityBytes).Replace(
            "2030-01-04T12:00:00Z",
            "2030-01-03T12:30:00Z",
            StringComparison.Ordinal);
        var dispositionResult = AtlasCouncilRecordValidator.ValidateDispositionRecord(
            DispositionFile,
            Utf8(reversedDisposition),
            RecordFile,
            recordBytes,
            ManifestFile,
            manifestBytes,
            FeasibilityFile,
            feasibilityBytes);
        Assert.Contains(dispositionResult.Issues, issue => issue.Code == "atlas.disposition.chronology-invalid");
    }

    [Fact]
    public void Linked_chronology_reads_only_the_validated_predecessor_binding_table()
    {
        var recordBytes = CompletedRecordBytes();
        var manifest = SyntheticManifest(recordBytes).Replace(
            "## H0 freeze binding",
            "| Frozen UTC instant | 1900-01-01T00:00:00Z |\n\n## H0 freeze binding",
            StringComparison.Ordinal);
        var manifestBytes = Utf8(manifest);
        var feasibility = SyntheticFeasibility(recordBytes, manifestBytes).Replace(
            "2030-01-03T13:00:00Z",
            "2030-01-03T11:00:00Z",
            StringComparison.Ordinal);

        var result = AtlasCouncilRecordValidator.ValidateFeasibilityRecord(
            FeasibilityFile,
            Utf8(feasibility),
            RecordFile,
            recordBytes,
            ManifestFile,
            manifestBytes);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.feasibility.chronology-invalid");
    }

    [Fact]
    public void Disposition_action_date_must_equal_the_record_utc_date()
    {
        var recordBytes = CompletedRecordBytes();
        var manifestBytes = Utf8(SyntheticManifest(recordBytes));
        var feasibilityBytes = Utf8(SyntheticFeasibility(recordBytes, manifestBytes));
        var disposition = SyntheticDisposition(recordBytes, manifestBytes, feasibilityBytes)
            .Replace("DEFER — 2030-01-04", "DEFER — 1999-01-01", StringComparison.Ordinal);

        var result = AtlasCouncilRecordValidator.ValidateDispositionRecord(
            DispositionFile,
            Utf8(disposition),
            RecordFile,
            recordBytes,
            ManifestFile,
            manifestBytes,
            FeasibilityFile,
            feasibilityBytes);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.disposition.action-date-invalid");
    }

    [Fact]
    public void Session_date_terms_and_separate_choice_coverage_are_consistent()
    {
        var valid = SyntheticRecord(
            AtlasCouncilRecordValidator.SessionRecordStatus,
            sessionComplete: true);
        var futureTerms = valid.Replace("2030-01-01", "2030-01-03", StringComparison.Ordinal);
        var wrongDate = valid.Replace("2030-01-02 · 60 minutes", "2030-01-01 · 60 minutes", StringComparison.Ordinal);
        var partialConsent = valid.Replace(
            "| Participation consent recorded separately | ACCEPTED — general educator: 3 |",
            "| Participation consent recorded separately | ACCEPTED — general educator: 1 |",
            StringComparison.Ordinal);

        Assert.Contains(
            AtlasCouncilRecordValidator.Validate(RecordFile, futureTerms).Issues,
            issue => issue.Code == "atlas.session.operating-terms-retroactive");
        Assert.Contains(
            AtlasCouncilRecordValidator.Validate(RecordFile, wrongDate).Issues,
            issue => issue.Code == "atlas.session.date-invalid");
        Assert.Contains(
            AtlasCouncilRecordValidator.Validate(RecordFile, partialConsent).Issues,
            issue => issue.Code == "atlas.session.participation-coverage-mismatch");
    }

    [Theory]
    [InlineData("1")]
    [InlineData("60")]
    [InlineData("1440")]
    public void Session_duration_accepts_positive_one_UTC_day_boundaries(string minutes)
    {
        var record = SyntheticRecord(
            AtlasCouncilRecordValidator.SessionRecordStatus,
            sessionComplete: true,
            includeRecommendation: true);
        if (minutes != "60")
        {
            record = ReplaceRequired(
                record,
                "| Session date and duration | 2030-01-02 · 60 minutes |",
                $"| Session date and duration | 2030-01-02 · {minutes} minutes |");
        }

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.True(result.IsValid, Describe(result));
        Assert.Equal(AtlasCouncilRecordStatus.SessionRecord, result.Status);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1441")]
    [InlineData("2147483647")]
    [InlineData("2147483648")]
    public void Session_duration_refuses_values_outside_one_UTC_day(string minutes)
    {
        // Synthetic terms cover January 2 and expire at the next UTC midnight.
        var record = ReplaceRequired(
            SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true,
                includeRecommendation: true),
            "| Session date and duration | 2030-01-02 · 60 minutes |",
            $"| Session date and duration | 2030-01-02 · {minutes} minutes |");

        var result = AtlasCouncilRecordValidator.Validate(RecordFile, record);

        Assert.False(
            result.IsValid,
            $"The session validator accepted duration '{minutes}' minutes outside the positive one-UTC-day bound.");
        Assert.Contains(result.Issues, issue => issue.Code == "atlas.session.date-invalid");
    }

    [Fact]
    public void Terminal_boundaries_refuse_unheaded_later_authority_text()
    {
        var recordBytes = CompletedRecordBytes();
        var manifestBytes = Utf8(SyntheticManifest(recordBytes));
        var feasibilityBytes = Utf8(SyntheticFeasibility(recordBytes, manifestBytes));
        var disposition = SyntheticDisposition(recordBytes, manifestBytes, feasibilityBytes)
            + "\nRelease authorized.\n";

        var result = AtlasCouncilRecordValidator.ValidateDispositionRecord(
            DispositionFile,
            Utf8(disposition),
            RecordFile,
            recordBytes,
            ManifestFile,
            manifestBytes,
            FeasibilityFile,
            feasibilityBytes);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.disposition.terminal-boundary");
    }

    [Fact]
    public void Linked_tables_require_complete_rows_and_exact_predecessor_keys()
    {
        var recordBytes = CompletedRecordBytes();
        var manifestBytes = Utf8(SyntheticManifest(recordBytes));
        var feasibility = SyntheticFeasibility(recordBytes, manifestBytes);
        var incomplete = feasibility.Replace(
            "| Synthetic possibility | Existing component | Bounded synthetic slice | None | Machine and later human evidence | Small | None recorded |",
            "| Synthetic possibility | Existing component | | None | Machine and later human evidence | Small | None recorded |",
            StringComparison.Ordinal);
        var mismatch = feasibility.Replace(
            "| Synthetic possibility | Existing component |",
            "| Different possibility | Existing component |",
            StringComparison.Ordinal);

        var incompleteResult = AtlasCouncilRecordValidator.ValidateFeasibilityRecord(
            FeasibilityFile,
            Utf8(incomplete),
            RecordFile,
            recordBytes,
            ManifestFile,
            manifestBytes);
        Assert.Contains(incompleteResult.Issues, issue => issue.Code == "atlas.feasibility.row-incomplete");

        var mismatchResult = AtlasCouncilRecordValidator.ValidateFeasibilityRecord(
            FeasibilityFile,
            Utf8(mismatch),
            RecordFile,
            recordBytes,
            ManifestFile,
            manifestBytes);
        Assert.Contains(mismatchResult.Issues, issue => issue.Code == "atlas.feasibility.recommendation-mismatch");
    }

    [Fact]
    public void Refusals_do_not_echo_untrusted_record_values()
    {
        const string Canary = "PRIVATE SYNTHETIC PARTICIPANT CANARY";
        var result = AtlasCouncilRecordValidator.Validate(RecordFile, SyntheticRecord(Canary));

        Assert.NotEmpty(result.Issues);
        Assert.DoesNotContain(result.Issues, issue => issue.Message.Contains(Canary, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("atlas-priority-session.md", "atlas.file-name")]
    [InlineData("atlas-priority-session-2030-02-30.md", "atlas.file-date")]
    [InlineData("atlas-priority-session-2030-1-2.md", "atlas.file-name")]
    public void Only_real_dated_copy_names_enter_the_H0_record_lifecycle(string fileName, string expectedCode)
    {
        var result = AtlasCouncilRecordValidator.Validate(
            fileName,
            SyntheticRecord(AtlasCouncilRecordValidator.UnrunStatus));

        Assert.Contains(result.Issues, issue => issue.Code == expectedCode);
    }

    private static AtlasCouncilArtifactValidation ValidateSyntheticDownstreamRecordWithChangedField(
        bool productOwnerDisposition,
        string expectedField,
        string replacementField)
    {
        var recordBytes = CompletedRecordBytes();
        var manifestBytes = Utf8(SyntheticManifest(recordBytes));
        var feasibility = SyntheticFeasibility(recordBytes, manifestBytes);
        if (!productOwnerDisposition)
        {
            return AtlasCouncilRecordValidator.ValidateFeasibilityRecord(
                FeasibilityFile,
                Utf8(ReplaceRequired(feasibility, expectedField, replacementField)),
                RecordFile,
                recordBytes,
                ManifestFile,
                manifestBytes);
        }

        var feasibilityBytes = Utf8(feasibility);
        var disposition = ReplaceRequired(
            SyntheticDisposition(recordBytes, manifestBytes, feasibilityBytes),
            expectedField,
            replacementField);
        return AtlasCouncilRecordValidator.ValidateDispositionRecord(
            DispositionFile,
            Utf8(disposition),
            RecordFile,
            recordBytes,
            ManifestFile,
            manifestBytes,
            FeasibilityFile,
            feasibilityBytes);
    }

    private static byte[] CompletedRecordBytes()
        => Utf8(SyntheticRecord(
            AtlasCouncilRecordValidator.SessionRecordStatus,
            sessionComplete: true,
            includeRecommendation: true));

    private static string SyntheticRecord(
        string status,
        bool sessionComplete = false,
        bool includeRecommendation = false)
    {
        var pending = "[not run]";
        var recordIdentity = sessionComplete ? "H0-SYNTHETIC v1" : pending;
        var sessionDate = sessionComplete ? "2030-01-02 · 60 minutes" : pending;
        var repository = sessionComplete ? "0000000 · clean synthetic tree" : pending;
        var buildArtifacts = sessionComplete ? SyntheticBuildArtifact : pending;
        var instrument = sessionComplete ? SyntheticInstrument : pending;
        var facilitator = sessionComplete ? "non-voting facilitator seat" : pending;
        var productOwner = sessionComplete ? "present" : pending;
        var presentSeats = sessionComplete ? "general educator: 3" : pending;
        var rosterBinding = sessionComplete
            ? $"BOUND — path=docs/governance/synthetic-enactment.md; version=v1; bytes=345; sha256:{new string('c', 64)}; commit=0000000"
            : pending;
        var totalSeatedPersons = sessionComplete ? "3" : pending;
        var naturalPersons = sessionComplete ? "3" : pending;
        var practicingEducators = sessionComplete ? "3" : pending;
        var absentSeats = sessionComplete ? "NONE — no absent constituted seat" : pending;
        var materials = sessionComplete ? "synthetic application and staged packet" : pending;
        var operatingTerms = sessionComplete ? "ENACTED — OCF-COUNCIL-TERMS-v1 — 2030-01-01" : "[not enacted / not run]";
        var operatingTermsBinding = sessionComplete
            ? $"BOUND — path=docs/council/draft-first-cohort-operating-terms.md; bytes=1234; sha256:{new string('a', 64)}; commit=0000000"
            : pending;
        var contentLicense = sessionComplete ? "CHOSEN — Synthetic content license — REC-1" : pending;
        var seatAuthority = sessionComplete ? CanonicalSeatAuthority : pending;
        var participation = sessionComplete ? "ACCEPTED — general educator: 3" : pending;
        var withdrawal = sessionComplete ? "ACKNOWLEDGED — general educator: 3" : pending;
        var notes = sessionComplete ? "ACCEPTED — general educator: 3" : pending;
        var publication = sessionComplete ? "ACCEPTED — general educator: 3" : pending;
        var recording = sessionComplete ? "NO RECORDING" : pending;
        var cohortDisclosure = sessionComplete ? "RECORDED AND HONORED — general educator: 3" : pending;
        var credit = sessionComplete ? "RECORDED — general educator: 3" : pending;
        var compensation = sessionComplete ? "RECORDED — OCF-COMPENSATION-v1 — 2030-01-01 — general educator: 3" : pending;
        var compensationAdministration = sessionComplete
            ? "ATTESTED — private-ledger-ref=COMP-REC-1; rate=VERIFIED; utc-quarter-cap-reservation=VERIFIED; district-time-status=VERIFIED"
            : pending;
        var compensationBinding = sessionComplete
            ? $"BOUND — path=docs/council/compensation-policy.md; bytes=2345; sha256:{new string('b', 64)}; commit=0000000"
            : pending;
        var quorum = sessionComplete ? "MET — OCF-COUNCIL-TERMS-v1 — before matter-specific recusals" : pending;
        var conflicts = sessionComplete ? "NONE — no conflict or recusal" : pending;
        var recusalDisputes = sessionComplete ? "NONE — no disputed recusal" : pending;
        var capacities = sessionComplete ? "NONE — one constituted capacity per natural person" : pending;
        var protectedSeatHolds = sessionComplete ? "HELD — AAC/SLP — NOT REVIEWED — HELD" : pending;
        var contentContributionChoice = sessionComplete ? $"NONE — {presentSeats}" : pending;
        var roleAcceptanceChoice = sessionComplete
            ? $"ACCEPTED — {presentSeats} — OCF-COUNCIL-TERMS-v1"
            : pending;
        var maintainerAppointmentChoice = sessionComplete ? $"NONE — {presentSeats}" : pending;
        var copyrightStewardshipChoice = sessionComplete ? $"NONE — {presentSeats}" : pending;
        var withdrawalDisposition = sessionComplete
            ? "RESOLVED — activity-withdrawal=NONE; council-resignation-vacancy=NONE; non-member-role-closure=NOT-APPLICABLE-H0; unresolved=NONE"
            : pending;
        var procedure = sessionComplete
            ? "APPLIED — OCF-COUNCIL-TERMS-v1"
            : "[not enacted / not run]";
        var needId = sessionComplete ? "`N-SYNTHETIC`" : "`N-__`";
        var needValue = sessionComplete ? "synthetic factual paraphrase" : string.Empty;
        var mappingRow = sessionComplete
            ? "| N-SYNTHETIC | Synthetic possibility | Synthetic fit record | G | AAC/SLP hold retained |"
            : "| | | | | |";
        var recommendationRow = includeRecommendation
            ? "| 1 | N-SYNTHETIC · Synthetic possibility | Synthetic rationale | Synthetic proof request | 3 | 3 | 3 | NONE — no affected conflict or recusal | MET — OCF-COUNCIL-TERMS-v1 — after recusals | CONSENSUS — denominator=3 | AAC/SLP hold retained | None recorded |"
            : "| | | | | | | | | | | | |";
        var supplementalValue = sessionComplete ? "None recorded" : string.Empty;
        var outcome = sessionComplete
            ? includeRecommendation ? "RECOMMENDATION RECORDED" : "NO RECOMMENDATION"
            : string.Empty;
        var tally = sessionComplete
            ? includeRecommendation ? "Consensus under synthetic procedure; no vote." : "No ordering; no vote."
            : string.Empty;
        var readBack = sessionComplete ? "general educator: 3" : string.Empty;
        var finalHolds = sessionComplete ? "AAC/SLP hold retained" : string.Empty;

        return $$"""
            # Atlas 2.0 council priority session

            **Status:** {{status}}

            ## Non-negotiable boundaries

            Synthetic fixture boundary.

            ## Prepare without pre-ranking

            Synthetic fixture preparation.

            ### Session header

            | Field | Record |
            |---|---|
            | H0 record ID and version | {{recordIdentity}} |
            | Session date and duration | {{sessionDate}} |
            | Repository commit and dirty-tree disposition | {{repository}} |
            | Build/artifact IDs and SHA-256 values | {{buildArtifacts}} |
            | Instrument name, version, and SHA-256 | {{instrument}} |
            | Facilitator (non-voting) | {{facilitator}} |
            | Product owner present? | {{productOwner}} |
            | Current enacted roster record, version, and SHA-256 | {{rosterBinding}} |
            | Total seated, non-vacant natural persons (count) | {{totalSeatedPersons}} |
            | Seats present (seat + count, no names by default) | {{presentSeats}} |
            | Natural persons present (count) | {{naturalPersons}} |
            | Practicing-educator natural persons present (count) | {{practicingEducators}} |
            | Seats absent | {{absentSeats}} |
            | Exact material actually reviewed | {{materials}} |
            | Enacted operating-terms version and effective date | {{operatingTerms}} |
            | Enacted operating-terms exact file binding | {{operatingTermsBinding}} |
            | Documentation/original-printable content license and accountable decision record | {{contentLicense}} |
            | {{SeatAuthorityField}} | {{seatAuthority}} |
            | Participation consent recorded separately | {{participation}} |
            | Session-opening general quorum result before matter-specific recusals | {{quorum}} |
            | Conflict categories and recusals (de-identified by default) | {{conflicts}} |
            | Disputed-recusal resolution subrecords before affected matters, or NONE | {{recusalDisputes}} |
            | Multi-capacity disclosures | {{capacities}} |
            | Applicable protected seats vacant, absent, or recused; each marked NOT REVIEWED — HELD | {{protectedSeatHolds}} |
            | Withdrawal right and route explained/acknowledged | {{withdrawal}} |
            | Operative compensation-policy version and effective date; election recorded | {{compensation}} |
            | Private compensation-ledger attestation for rate, UTC quarter, cap reservation, and district-time status | {{compensationAdministration}} |
            | Operative compensation-policy exact file binding | {{compensationBinding}} |
            | Private/de-identified note-collection consent recorded | {{notes}} |
            | Public-record publication consent recorded | {{publication}} |
            | Recording consent recorded, or no recording | {{recording}} |
            | Within-cohort identity/affiliation disclosure scope honored; confidentiality/no-contact boundary acknowledged | {{cohortDisclosure}} |
            | Public-credit choice confirmed | {{credit}} |
            | Content-contribution choice and exact license/control identity, or none | {{contentContributionChoice}} |
            | Role-acceptance choice and exact bounded role/control identity, or none | {{roleAcceptanceChoice}} |
            | Maintainer-appointment choice and exact role/control identity, or none | {{maintainerAppointmentChoice}} |
            | Copyright-stewardship choice and exact transfer/control identity, or none | {{copyrightStewardshipChoice}} |
            | Activity withdrawal, council resignation/vacancy, and non-member role-closure dispositions | {{withdrawalDisposition}} |
            | Decision procedure and quorum rule applied (exact governing record) | {{procedure}} |

            ## Sixty-minute needs-first agenda

            Synthetic fixture agenda.

            ### Need card — complete before opening the atlas

            | Prompt | Participant-reviewed de-identified factual paraphrase |
            |---|---|
            | Need ID | {{needId}} |
            | Recurring teacher work or learner-facing barrier | {{needValue}} |
            | Who encounters it (generic role/context only) | {{needValue}} |
            | How often it occurs | {{needValue}} |
            | Current workaround and its time/material cost | {{needValue}} |
            | What a useful paper/offline artifact would make possible | {{needValue}} |
            | What must remain under teacher control | {{needValue}} |
            | Unacceptable failure or harm | {{needValue}} |
            | First classroom proof that would earn trust | {{needValue}} |
            | Seat speaking | {{needValue}} |

            ### Need-to-possibility mapping — complete only after need capture

            | Need ID | Atlas entry / existing capability / new composition / no match | Why it fits or fails to fit | Likely lane (`G`, `A`, `R`, uncertain) | Possibly implicated seats |
            |---|---|---|---|---|
            {{mappingRow}}

            ## Council recommendation record

            | Order, if any | Need ID and mapped possibility | Participant-reviewed de-identified rationale | First proof requested | Eligible total natural persons after matter recusals | Eligible natural persons present | Practicing-educator natural persons present | Affected conflict categories / recusals | Matter-specific quorum | Matter tally / consensus with eligible denominator | Holds / seats still needed | Dissent or alternative |
            |---|---|---|---|---|---|---|---|---|---|---|---|
            {{recommendationRow}}

            - **Council outcome (`RECOMMENDATION RECORDED`, `NO RECOMMENDATION`, or `HOLD`):** {{outcome}}
            - **Needs deliberately not advanced, and why:** {{supplementalValue}}
            - **Useful possibilities with no atlas match:** {{supplementalValue}}
            - **Questions the session could not answer:** {{supplementalValue}}
            - **Corrections members made during read-back:** {{supplementalValue}}
            - **Whether members reached consensus, split, or made no ordering:** {{supplementalValue}}
            - **Vote/tally under the enacted procedure, or consensus/no vote:** {{tally}}
            - **Read-back confirmation (seat + count, no names by default):** {{readBack}}
            - **Applicable seat holds after read-back:** {{finalHolds}}

            ## Close the session record; freeze only through a detached manifest

            Synthetic fixture boundary. Do not add later-authority content here.

            ## Completion check

            A real H0 council record is ready for participant review only when it contains
            the session header, exact enacted roster and control bindings, reconciled
            person/seat authority entries, separate choice and withdrawal dispositions,
            session-opening general quorum, participant-reviewed de-identified factual need
            paraphrases, mapping, every disputed-recusal resolution subrecord, and—for every
            recommendation matter—post-recusal eligible-person counts,
            practicing-educator count, conflict/recusal record, quorum result, and exact tally
            denominator. It must also contain a procedurally valid recommendation,
            no-recommendation, or `HOLD`
            outcome (including dissent and tally), read-back corrections, and seat holds. H0 is
            frozen only when the separate manifest binds those final bytes after review and
            exact-final-byte publication reconfirmation. Feasibility and
            product-owner disposition are separate downstream records, not completion
            fields that can mutate H0. A separately complete unranked consultation note is
            still outside H0–H7 and satisfies no downstream condition. Completeness does
            not waive a protected hold. Until then, the honest Atlas 2.0 status is:

            > **No next atlas priority has been selected. Awaiting real council input and the product owner's recorded disposition.**
            """;
    }

    private static string SyntheticManifest(byte[] recordBytes)
        => $$"""
            # Atlas H0 detached freeze manifest

            **Status:** {{AtlasCouncilRecordValidator.FreezeManifestStatus}}

            ## H0 freeze binding

            | Field | Record |
            |---|---|
            | H0 record ID and version | H0-SYNTHETIC v1 |
            | Final H0 record repository path | docs/council/{{RecordFile}} |
            | Final H0 record SHA-256 | {{Sha256(recordBytes)}} |
            | Final H0 record byte length | {{recordBytes.Length}} |
            | Upstream final-record and detached-manifest bindings (H0: NONE — no predecessor) | NONE — H0 has no predecessor |
            | Repository commit and dirty-tree disposition | 0000000 · clean synthetic tree |
            | Build/artifact IDs and SHA-256 values | {{SyntheticBuildArtifact}} |
            | Instrument name, version, and SHA-256 | {{SyntheticInstrument}} |
            | Current enacted roster record, version, and SHA-256 | BOUND — path=docs/governance/synthetic-enactment.md; version=v1; bytes=345; sha256:cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc; commit=0000000 |
            | Total seated, non-vacant natural persons (count) | 3 |
            | Seats present (seat + count, no names by default) | general educator: 3 |
            | Natural persons present (count) | 3 |
            | Practicing-educator natural persons present (count) | 3 |
            | Seats absent | NONE — no absent constituted seat |
            | Multi-capacity disclosures | NONE — one constituted capacity per natural person |
            | Documentation/original-printable content license and accountable decision record | CHOSEN — Synthetic content license — REC-1 |
            | Enacted operating-terms exact file binding | BOUND — path=docs/council/draft-first-cohort-operating-terms.md; bytes=1234; sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa; commit=0000000 |
            | {{SeatAuthorityField}} | {{CanonicalSeatAuthority}} |
            | Participation consent recorded separately | ACCEPTED — general educator: 3 |
            | Session-opening general quorum result before matter-specific recusals | MET — OCF-COUNCIL-TERMS-v1 — before matter-specific recusals |
            | Conflict categories and recusals (de-identified by default) | NONE — no conflict or recusal |
            | Disputed-recusal resolution subrecords before affected matters, or NONE | NONE — no disputed recusal |
            | Withdrawal right and route explained/acknowledged | ACKNOWLEDGED — general educator: 3 |
            | Operative compensation-policy version and effective date; election recorded | RECORDED — OCF-COMPENSATION-v1 — 2030-01-01 — general educator: 3 |
            | Private compensation-ledger attestation for rate, UTC quarter, cap reservation, and district-time status | ATTESTED — private-ledger-ref=COMP-REC-1; rate=VERIFIED; utc-quarter-cap-reservation=VERIFIED; district-time-status=VERIFIED |
            | Operative compensation-policy exact file binding | BOUND — path=docs/council/compensation-policy.md; bytes=2345; sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb; commit=0000000 |
            | Private/de-identified note-collection consent recorded | ACCEPTED — general educator: 3 |
            | Public-record publication consent recorded | ACCEPTED — general educator: 3 |
            | Recording consent recorded, or no recording | NO RECORDING |
            | Within-cohort identity/affiliation disclosure scope honored; confidentiality/no-contact boundary acknowledged | RECORDED AND HONORED — general educator: 3 |
            | Public-credit choice confirmed | RECORDED — general educator: 3 |
            | Content-contribution choice and exact license/control identity, or none | NONE — general educator: 3 |
            | Role-acceptance choice and exact bounded role/control identity, or none | ACCEPTED — general educator: 3 — OCF-COUNCIL-TERMS-v1 |
            | Maintainer-appointment choice and exact role/control identity, or none | NONE — general educator: 3 |
            | Copyright-stewardship choice and exact transfer/control identity, or none | NONE — general educator: 3 |
            | Activity withdrawal, council resignation/vacancy, and non-member role-closure dispositions | RESOLVED — activity-withdrawal=NONE; council-resignation-vacancy=NONE; non-member-role-closure=NOT-APPLICABLE-H0; unresolved=NONE |
            | Decision procedure and quorum rule applied (exact governing record) | APPLIED — OCF-COUNCIL-TERMS-v1 |
            | Per-recommendation matter counts, conflicts/recusals, quorum, and tally denominators | BOUND — 1 recommendation rows in final H0 record |
            | Exact material actually reviewed | synthetic application and staged packet |
            | Findings, measurements, holds, dissent, and limitations | synthetic H0 finding; AAC/SLP hold retained |
            | Requested corrections and accountable owners | NONE — no correction requested |
            | Participant read-back/review of those exact bytes completed (seat + count, no names by default) | general educator: 3 |
            | Exact-final-byte public-record publication permission reconfirmed after participant review | RECONFIRMED — general educator: 3 |
            | Corrections and dissent incorporated before final hashing | CONFIRMED — all corrections resolved and dissent preserved in final H0 bytes |
            | Pre-freeze withdrawal/removal requests honored; unresolved requests | HONORED — NONE RECEIVED; unresolved=NONE |
            | Applicable protected seats vacant, absent, or recused; each marked NOT REVIEWED — HELD | HELD — AAC/SLP — NOT REVIEWED — HELD |
            | Frozen UTC instant | 2030-01-03T12:00:00Z |
            | H0 freeze-manifest repository path | docs/council/{{ManifestFile}} |
            | Append-only correction, withdrawal, credit-change, or supersession record paths; or none at freeze | none at freeze |

            ## Non-circular and immutable boundary

            Close the final H0 record before computing its byte length and SHA-256. Complete
            this manifest afterward and then close it too. This manifest deliberately has no
            field for its own SHA-256: a self-hash would change the bytes it claims to hash.
            Every downstream record computes the SHA-256 of this completed manifest and
            records that value alongside the final H0 record SHA-256.

            Before hashing, offer the exact proposed public bytes for participant review,
            honor every correction and withdrawal/removal request, and separately
            reconfirm public-record publication permission for those exact bytes as
            `RECONFIRMED — <exact seats present>`. Changed bytes restart review and
            reconfirmation; a refusal leaves the public record open. Record requested
            corrections exactly as `NONE — no correction requested` or `RESOLVED — <opaque
            or de-identified correction references>; unresolved=NONE`, and confirm the
            adjacent incorporation field exactly as `CONFIRMED — all corrections resolved
            and dissent preserved in final H0 bytes`.

            When both H0 withdrawal-disposition values are `NONE`, record exactly `HONORED
            — NONE RECEIVED; unresolved=NONE`. Otherwise record `HONORED —
            activity-withdrawal=<the exact H0 value>; council-resignation-vacancy=<the exact
            H0 value>; unresolved=NONE`; this makes every non-`NONE` H0 reference explicit
            at freeze. Any unresolved correction, withdrawal, removal, resignation, or
            vacancy request prevents freeze. Only a request received after freeze becomes
            an append-only event.

            After completion, never edit either bound file. A correction, withdrawal
            marker, prospective credit change, or supersession is a new append-only linked
            record. It may govern current use without pretending to erase or mutate the
            historical bytes.

            The at-freeze correction-path field is a historical snapshot, not an evergreen
            inventory. Before any downstream review or release consideration, a fresh
            chain audit at the exact candidate repository revision must enumerate and bind
            every later public event, reconcile opaque private-custodian attestations, and
            state the current effective disposition. A missing, ambiguous, conflicting, or
            unresolved event is a HOLD; no downstream record may choose an older convenient
            state.

            The mechanics validator proves only the linkage among the current bytes
            supplied to it. It does not establish the first-complete Git history,
            prevent a coordinated rewrite, or choose which later correction is current.
            Before publication or release consideration, a human history audit must
            verify those claims. Until a versioned correction schema and resolver are
            adopted, append-only correction paths remain a manual governance protocol.
            """;

    private static string SyntheticFeasibility(byte[] recordBytes, byte[] manifestBytes)
        => $$"""
            # Atlas H0 separate feasibility record

            **Status:** {{AtlasCouncilRecordValidator.FeasibilityRecordStatus}}

            ## Frozen H0 binding

            | Field | Record |
            |---|---|
            | Feasibility record ID and version | H0-SYNTHETIC-FEASIBILITY v1 |
            | Predecessor feasibility record path, version, byte length, and SHA-256; or NONE | NONE — first feasibility record |
            | H0 record ID and version | H0-SYNTHETIC v1 |
            | Final H0 record repository path | docs/council/{{RecordFile}} |
            | Final H0 record SHA-256 | {{Sha256(recordBytes)}} |
            | H0 freeze-manifest repository path | docs/council/{{ManifestFile}} |
            | H0 freeze-manifest SHA-256 | {{Sha256(manifestBytes)}} |
            | Enacted operating-terms exact file binding | BOUND — path=docs/council/draft-first-cohort-operating-terms.md; bytes=1234; sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa; commit=0000000 |
            | Operative compensation-policy exact file binding | BOUND — path=docs/council/compensation-policy.md; bytes=2345; sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb; commit=0000000 |
            | Upstream chain-audit ID | CHAIN-H0-SYNTHETIC-v1 |
            | Upstream chain-audit UTC instant | 2030-01-03T12:30:00Z |
            | Upstream chain-audit repository path, version, byte length, and SHA-256 | BOUND — path=docs/council/atlas-priority-session-2030-01-02-chain-audit-v1.md; version=v1; bytes=3456; sha256:dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd |
            | Chain-audit exact candidate repository revision and dirty-tree disposition | 0000000 · clean synthetic tree |
            | Public append-only event bindings, or NONE | NONE — no public event through candidate revision |
            | Private append-only event attestations, or NONE | NONE — no private event attested through audit instant |
            | Current effective upstream dispositions and unresolved chain holds | CURRENT — H0 frozen bytes effective; unresolved-chain-holds=NONE |
            | Feasibility record UTC instant | 2030-01-03T13:00:00Z |

            ## Feasibility assessment

            | Recommended possibility | Reusable engine/capability | Smallest bounded slice | Dependencies and migrations | Required automated and human evidence | Effort/risk range | Conflicts with ADR, plan, or gate |
            |---|---|---|---|---|---|---|
            | Synthetic possibility | Existing component | Bounded synthetic slice | None | Machine and later human evidence | Small | None recorded |

            ## Authority boundary

            - Preserve the council's requested outcome even when the proposed implementation changes.
            - Do not turn ease of implementation into a retroactive council priority.
            - Mark uncertainty. Do not convert rehearsal findings or model judgment into human evidence.
            - If a candidate enters Amber, Restricted, or a protected seat's territory, record the stop; do not design around it.
            - Do not edit the bound H0 record, freeze manifest, or this completed record. A correction is the next `-feasibility-v<n>.md` and exactly binds its immediate predecessor path, version, byte length, and SHA-256. The fresh chain audit, not a self-hash inside this record, determines which linked version is current.
            - A completed record uses a fresh chain audit made after the H0 freeze and before this record. The current-disposition value ends `unresolved-chain-holds=NONE`; any missing, ambiguous, conflicting, withdrawn, restricted, or unresolved chain event is a HOLD and forbids completion.
            """;

    private static string SyntheticDisposition(
        byte[] recordBytes,
        byte[] manifestBytes,
        byte[] feasibilityBytes)
        => $$"""
            # Atlas H0 separate product-owner disposition

            **Status:** {{AtlasCouncilRecordValidator.DispositionRecordStatus}}

            ## Frozen H0 and feasibility binding

            | Field | Record |
            |---|---|
            | Product-owner disposition record ID and version | H0-SYNTHETIC-DISPOSITION v1 |
            | Predecessor disposition record path, version, byte length, and SHA-256; or NONE | NONE — first disposition record |
            | H0 record ID and version | H0-SYNTHETIC v1 |
            | Final H0 record repository path | docs/council/{{RecordFile}} |
            | Final H0 record SHA-256 | {{Sha256(recordBytes)}} |
            | H0 freeze-manifest repository path | docs/council/{{ManifestFile}} |
            | H0 freeze-manifest SHA-256 | {{Sha256(manifestBytes)}} |
            | Feasibility record repository path | docs/council/{{FeasibilityFile}} |
            | Feasibility record SHA-256 | {{Sha256(feasibilityBytes)}} |
            | Enacted operating-terms exact file binding | BOUND — path=docs/council/draft-first-cohort-operating-terms.md; bytes=1234; sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa; commit=0000000 |
            | Operative compensation-policy exact file binding | BOUND — path=docs/council/compensation-policy.md; bytes=2345; sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb; commit=0000000 |
            | Upstream chain-audit ID | CHAIN-H0-FEASIBILITY-SYNTHETIC-v1 |
            | Upstream chain-audit UTC instant | 2030-01-03T14:00:00Z |
            | Upstream chain-audit repository path, version, byte length, and SHA-256 | BOUND — path=docs/council/atlas-priority-session-2030-01-02-chain-audit-v2.md; version=v2; bytes=4567; sha256:eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee |
            | Chain-audit exact candidate repository revision and dirty-tree disposition | 0000000 · clean synthetic tree |
            | Public append-only event bindings, or NONE | NONE — no public event through candidate revision |
            | Private append-only event attestations, or NONE | NONE — no private event attested through audit instant |
            | Current effective upstream dispositions and unresolved chain holds | CURRENT — H0 frozen bytes and feasibility v1 effective; unresolved-chain-holds=NONE |
            | Product-owner conflict category and disposition | NONE — no material conflict disclosed |
            | Product-owner disposition UTC instant | 2030-01-04T12:00:00Z |

            ## Product-owner disposition

            | Recommendation | Disposition and date | Exact bounded scope | Reason | Outstanding seats/gates | Evidence required before completion |
            |---|---|---|---|---|---|
            | Synthetic possibility | DEFER — 2030-01-04 | No implementation | Await owner evidence | AAC/SLP remains outstanding | Separate protected review |

            ## Authority boundary

            This disposition is downstream of, and cannot alter, the final H0 record,
            detached freeze manifest, or feasibility record. A correction or supersession
            is a new linked version. Any architectural change still follows the ADR
            process, and every protected-seat, district, rights, evidence, and typist hold
            remains independently operative.

            A completed disposition uses a fresh chain audit made after the current
            feasibility record and before this disposition; its current-disposition
            value ends `unresolved-chain-holds=NONE`. Any missing, ambiguous,
            conflicting, withdrawn, restricted, or unresolved chain event is a HOLD
            and forbids completion.

            A recorded disposition's conflict field is exactly `NONE — <basis>`. This validator version recognizes no substitute priority authority. A future separately enacted governance/ADR route requires a superseding record schema before use.
            A held record instead uses `HELD — conflict-category=<de-identified category>; written-finding=<substantive finding>; adoption=NONE`, leaves the disposition table without substantive rows, and records no product decision. It is mechanically admissible evidence of a continuing hold, but it is not a completed disposition and cannot satisfy a downstream adoption, implementation, publication, or release dependency. Resolving it requires a new linked `-disposition-v<n>.md` record with fresh chain evidence; silence, self-appointment, or an informal delegation cannot resolve it.
            """;

    private static string SyntheticHeldDisposition(
        byte[] recordBytes,
        byte[] manifestBytes,
        byte[] feasibilityBytes)
    {
        var disposition = ReplaceRequired(
            SyntheticDisposition(recordBytes, manifestBytes, feasibilityBytes),
            $"**Status:** {AtlasCouncilRecordValidator.DispositionRecordStatus}",
            "**Status:** PRODUCT-OWNER DISPOSITION HELD");
        disposition = ReplaceRequired(
            disposition,
            "| Product-owner conflict category and disposition | NONE — no material conflict disclosed |",
            "| Product-owner conflict category and disposition | HELD — conflict-category=financial interest; written-finding=material conflict prevents product-owner action; adoption=NONE |");
        return ReplaceRequired(
            disposition,
            "| Synthetic possibility | DEFER — 2030-01-04 | No implementation | Await owner evidence | AAC/SLP remains outstanding | Separate protected review |",
            "| | | | | | |");
    }

    private static string SyntheticFeasibilityVersionTwo(
        byte[] recordBytes,
        byte[] manifestBytes)
    {
        var feasibility = ReplaceRequired(
            SyntheticFeasibility(recordBytes, manifestBytes),
            "| Feasibility record ID and version | H0-SYNTHETIC-FEASIBILITY v1 |",
            "| Feasibility record ID and version | H0-SYNTHETIC-FEASIBILITY v2 |");
        return ReplaceRequired(
            feasibility,
            "| Predecessor feasibility record path, version, byte length, and SHA-256; or NONE | NONE — first feasibility record |",
            $"| Predecessor feasibility record path, version, byte length, and SHA-256; or NONE | BOUND — path=docs/council/{FeasibilityFile}; version=v1; bytes=1234; sha256:{new string('f', 64)} |");
    }

    private static string SyntheticDispositionVersionTwo(
        byte[] recordBytes,
        byte[] manifestBytes,
        byte[] feasibilityBytes)
    {
        var disposition = ReplaceRequired(
            SyntheticDisposition(recordBytes, manifestBytes, feasibilityBytes),
            "| Product-owner disposition record ID and version | H0-SYNTHETIC-DISPOSITION v1 |",
            "| Product-owner disposition record ID and version | H0-SYNTHETIC-DISPOSITION v2 |");
        return ReplaceRequired(
            disposition,
            "| Predecessor disposition record path, version, byte length, and SHA-256; or NONE | NONE — first disposition record |",
            $"| Predecessor disposition record path, version, byte length, and SHA-256; or NONE | BOUND — path=docs/council/{DispositionFile}; version=v1; bytes=1234; sha256:{new string('9', 64)} |");
    }

    private static (byte[] RecordBytes, string Manifest) SyntheticManifestWithWithdrawalRequests()
    {
        const string NoneDisposition =
            "RESOLVED — activity-withdrawal=NONE; council-resignation-vacancy=NONE; non-member-role-closure=NOT-APPLICABLE-H0; unresolved=NONE";
        const string RequestedDisposition =
            "RESOLVED — activity-withdrawal=ACT-REQ-1; council-resignation-vacancy=VAC-REQ-1; non-member-role-closure=NOT-APPLICABLE-H0; unresolved=NONE";
        var record = ReplaceRequired(
            SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true,
                includeRecommendation: true),
            $"| Activity withdrawal, council resignation/vacancy, and non-member role-closure dispositions | {NoneDisposition} |",
            $"| Activity withdrawal, council resignation/vacancy, and non-member role-closure dispositions | {RequestedDisposition} |");
        var recordBytes = Utf8(record);
        var manifest = ReplaceRequired(
            SyntheticManifest(recordBytes),
            $"| Activity withdrawal, council resignation/vacancy, and non-member role-closure dispositions | {NoneDisposition} |",
            $"| Activity withdrawal, council resignation/vacancy, and non-member role-closure dispositions | {RequestedDisposition} |");
        manifest = ReplaceRequired(
            manifest,
            "| Pre-freeze withdrawal/removal requests honored; unresolved requests | HONORED — NONE RECEIVED; unresolved=NONE |",
            "| Pre-freeze withdrawal/removal requests honored; unresolved requests | HONORED — activity-withdrawal=ACT-REQ-1; council-resignation-vacancy=VAC-REQ-1; unresolved=NONE |");
        return (recordBytes, manifest);
    }

    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    private static string ReplaceRequired(string source, string oldValue, string newValue)
    {
        Assert.Contains(oldValue, source, StringComparison.Ordinal);
        var replaced = source.Replace(oldValue, newValue, StringComparison.Ordinal);
        Assert.NotEqual(source, replaced);
        return replaced;
    }

    private static string WithSessionCounts(string record, int total, int present, int educators)
    {
        if (total != 3 || present != 3 || educators != 3)
        {
            record = WithSeatAuthority(
                record,
                string.Join(
                    MarkdownEntrySeparator,
                    Enumerable.Range(1, total).Select(person =>
                    {
                        var presence = person <= present ? "PRESENT" : "ABSENT";
                        var educator = person <= educators ? "YES" : "NO";
                        return $"CONSTITUTED — seat=general educator; person-ref=P-{person:00}; person-count=1; presence={presence}; practicing-educator={educator}; appointing-authority=synthetic product owner; effective-utc=2029-01-03T00:00:00Z; expiry-exclusive-utc=2030-01-03T00:00:00Z; scope=synthetic general-educator review; acceptance-record=REC-P-{person:00}; qualification-basis=general educator; private-custodian=CUST-P-{person:00}";
                    })));
        }

        if (total != 3)
        {
            record = ReplaceRequired(
                record,
                "| Total seated, non-vacant natural persons (count) | 3 |",
                $"| Total seated, non-vacant natural persons (count) | {total} |");
        }

        if (present != 3)
        {
            record = ReplaceRequired(
                record,
                "| Natural persons present (count) | 3 |",
                $"| Natural persons present (count) | {present} |");
        }

        if (educators != 3)
        {
            record = ReplaceRequired(
                record,
                "| Practicing-educator natural persons present (count) | 3 |",
                $"| Practicing-educator natural persons present (count) | {educators} |");
        }

        if (present != 3)
        {
            record = ReplaceRequired(record, "general educator: 3", $"general educator: {present}");
        }

        var absent = total - present;
        if (absent > 0)
        {
            record = ReplaceRequired(
                record,
                "| Seats absent | NONE — no absent constituted seat |",
                $"| Seats absent | ABSENT — general educator: {absent} |");
        }

        return record;
    }

    private static string WithSeatAuthority(string record, string authority)
        => ReplaceRequired(
            record,
            $"| {SeatAuthorityField} | {CanonicalSeatAuthority} |",
            $"| {SeatAuthorityField} | {authority} |");

    private static string WithRecusalDispute(string record, string dispute)
        => ReplaceRequired(
            record,
            "| Disputed-recusal resolution subrecords before affected matters, or NONE | NONE — no disputed recusal |",
            $"| Disputed-recusal resolution subrecords before affected matters, or NONE | {dispute} |");

    private static string WithHeldRecusalDispute(
        string record,
        string dispute,
        bool replaceNoRecommendationOutcomeWithHold)
    {
        record = WithRecusalDispute(record, dispute);
        record = ReplaceRequired(
            record,
            "| Conflict categories and recusals (de-identified by default) | NONE — no conflict or recusal |",
            "| Conflict categories and recusals (de-identified by default) | RECUSALS — financial recusal dispute remains held |");
        return replaceNoRecommendationOutcomeWithHold
            ? ReplaceRequired(
                record,
                "- **Council outcome (`RECOMMENDATION RECORDED`, `NO RECOMMENDATION`, or `HOLD`):** NO RECOMMENDATION",
                "- **Council outcome (`RECOMMENDATION RECORDED`, `NO RECOMMENDATION`, or `HOLD`):** HOLD")
            : record;
    }

    private static string WithIndependentHeldNeed(string record)
    {
        const string SecondNeedCard = """
            | Prompt | Participant-reviewed de-identified factual paraphrase |
            |---|---|
            | Need ID | `N-HELD` |
            | Recurring teacher work or learner-facing barrier | independent held need |
            | Who encounters it (generic role/context only) | independent held need |
            | How often it occurs | independent held need |
            | Current workaround and its time/material cost | independent held need |
            | What a useful paper/offline artifact would make possible | independent held need |
            | What must remain under teacher control | independent held need |
            | Unacceptable failure or harm | independent held need |
            | First classroom proof that would earn trust | independent held need |
            | Seat speaking | independent held need |
            """;
        record = ReplaceRequired(
            record,
            "### Need-to-possibility mapping — complete only after need capture",
            $"{SecondNeedCard}\n\n### Need-to-possibility mapping — complete only after need capture");
        return ReplaceRequired(
            record,
            "| N-SYNTHETIC | Synthetic possibility | Synthetic fit record | G | AAC/SLP hold retained |",
            "| N-SYNTHETIC | Synthetic possibility | Synthetic fit record | G | AAC/SLP hold retained |\n| N-HELD | Held possibility | Held fit record | G | none implicated |");
    }

    private static string SyntheticMultiCapacityRecord()
    {
        var record = WithSeatAuthority(
            SyntheticRecord(
                AtlasCouncilRecordValidator.SessionRecordStatus,
                sessionComplete: true),
            $"{CanonicalSeatAuthority}{MarkdownEntrySeparator}{CurriculumSeatAuthorityEntryP01}");
        record = ReplaceRequired(
            record,
            "general educator: 3",
            "general educator: 3; curriculum: 1");
        return ReplaceRequired(
            record,
            "| Multi-capacity disclosures | NONE — one constituted capacity per natural person |",
            "| Multi-capacity disclosures | DISCLOSED — person-ref=P-01; seats=curriculum + general educator |");
    }

    private static string WithRecommendation(
        string record,
        int eligibleTotal,
        int eligiblePresent,
        int educatorsPresent,
        string conflicts,
        string tally,
        string holds = "AAC/SLP hold retained")
        => ReplaceRequired(
            record,
            CanonicalRecommendationRow,
            $"| 1 | N-SYNTHETIC · Synthetic possibility | Synthetic rationale | Synthetic proof request | {eligibleTotal} | {eligiblePresent} | {educatorsPresent} | {conflicts} | MET — OCF-COUNCIL-TERMS-v1 — after recusals | {tally} | {holds} | None recorded |");

    private static string Sha256(byte[] bytes)
        => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string Describe(AtlasCouncilRecordValidation result)
        => string.Join(Environment.NewLine, result.Issues.Select(issue => $"{issue.Code}: {issue.Message}"));

    private static string Describe(AtlasCouncilArtifactValidation result)
        => string.Join(Environment.NewLine, result.Issues.Select(issue => $"{issue.Code}: {issue.Message}"));
}
