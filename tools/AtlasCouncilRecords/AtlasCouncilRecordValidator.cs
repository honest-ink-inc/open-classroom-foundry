// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Foundry.Tools.AtlasCouncilRecords;

public enum AtlasCouncilRecordStatus
{
    Unrun,
    SessionRecord,
}

public sealed record AtlasCouncilRecordIssue(string Code, string Message);

public sealed record AtlasCouncilRecordValidation(
    AtlasCouncilRecordStatus? Status,
    IReadOnlyList<AtlasCouncilRecordIssue> Issues)
{
    public bool IsValid => Issues.Count == 0;
}

public sealed record AtlasCouncilArtifactValidation(
    IReadOnlyList<AtlasCouncilRecordIssue> Issues,
    AtlasCouncilDispositionState? DispositionState = null)
{
    public bool IsValid => Issues.Count == 0;

    public bool IsRecordedDisposition =>
        IsValid && DispositionState is AtlasCouncilDispositionState.Recorded;

    public bool IsHeldDisposition =>
        IsValid && DispositionState is AtlasCouncilDispositionState.Held;
}

public enum AtlasCouncilDispositionState
{
    Recorded,
    Held,
}

/// <summary>
/// Checks the documentary mechanics already fixed by the canonical Atlas 2.0
/// priority-session packet. It does not decide whether a session occurred or
/// whether any participant, reviewer, finding, or disposition is authentic.
/// </summary>
public static class AtlasCouncilRecordValidator
{
    public const string AuthorityBoundary =
        "This validator checks record mechanics, including count arithmetic for the one supported OCF-COUNCIL-TERMS-v1 rule. It does not authenticate supplied control digests against repository bytes, authenticate roster or attendance facts, declare that human quorum occurred, score needs, rank or recommend possibilities, select a priority, or perform a protected-seat act. Whether a public-credit identity may appear remains a human confirmation outside this validator.";

    public const string UnrunStatus = "UNRUN";
    public const string SessionRecordStatus = "SESSION RECORD";
    public const string FreezeManifestStatus = "H0 FREEZE MANIFEST COMPLETE";
    public const string FeasibilityRecordStatus = "FEASIBILITY RECORDED";
    public const string DispositionRecordStatus = "PRODUCT-OWNER DISPOSITION RECORDED";
    public const string DispositionHeldStatus = "PRODUCT-OWNER DISPOSITION HELD";

    private const string SupportedOperatingTermsId = "OCF-COUNCIL-TERMS-v1";
    private const string SupportedCompensationPolicyId = "OCF-COMPENSATION-v1";

    private const string FilePrefix = "atlas-priority-session-";
    private const string FileSuffix = ".md";
    private const string PresentSeatsField = "Seats present (seat + count, no names by default)";
    private const string NaturalPersonsPresentField = "Natural persons present (count)";
    private const string TotalSeatedPersonsField = "Total seated, non-vacant natural persons (count)";
    private const string PracticingEducatorsPresentField = "Practicing-educator natural persons present (count)";
    private const string RosterBindingField = "Current enacted roster record, version, and SHA-256";
    private const string AbsentSeatsField = "Seats absent";
    private const string RecordIdentityField = "H0 record ID and version";
    private const string RepositoryRevisionField = "Repository commit and dirty-tree disposition";
    private const string BuildArtifactsField = "Build/artifact IDs and SHA-256 values";
    private const string InstrumentField = "Instrument name, version, and SHA-256";
    private const string ExactMaterialField = "Exact material actually reviewed";
    private const string SeatAuthorityField = "Constituted-seat authority entries (stable seat/person refs, presence, scope, term, qualification-basis category, and private custodian reference)";
    private const string ParticipationConsentField = "Participation consent recorded separately";
    private const string QuorumResultField = "Session-opening general quorum result before matter-specific recusals";
    private const string WithdrawalField = "Withdrawal right and route explained/acknowledged";
    private const string PublicRecordConsentField = "Public-record publication consent recorded";
    private const string DecisionProcedureField = "Decision procedure and quorum rule applied (exact governing record)";
    private const string NoteCollectionConsentField = "Private/de-identified note-collection consent recorded";
    private const string MultiCapacityField = "Multi-capacity disclosures";
    private const string ConflictRecusalField = "Conflict categories and recusals (de-identified by default)";
    private const string RecusalDisputeRecordsField = "Disputed-recusal resolution subrecords before affected matters, or NONE";
    private const string ContentLicenseField = "Documentation/original-printable content license and accountable decision record";
    private const string CompensationField = "Operative compensation-policy version and effective date; election recorded";
    private const string CompensationAdministrationField = "Private compensation-ledger attestation for rate, UTC quarter, cap reservation, and district-time status";
    private const string RecordingConsentField = "Recording consent recorded, or no recording";
    private const string PublicCreditField = "Public-credit choice confirmed";
    private const string ContentContributionChoiceField = "Content-contribution choice and exact license/control identity, or none";
    private const string RoleAcceptanceChoiceField = "Role-acceptance choice and exact bounded role/control identity, or none";
    private const string MaintainerAppointmentChoiceField = "Maintainer-appointment choice and exact role/control identity, or none";
    private const string CopyrightStewardshipChoiceField = "Copyright-stewardship choice and exact transfer/control identity, or none";
    private const string WithdrawalDispositionField = "Activity withdrawal, council resignation/vacancy, and non-member role-closure dispositions";
    private const string FinalBytePublicationConsentField = "Exact-final-byte public-record publication permission reconfirmed after participant review";
    private const string H0UpstreamField = "Upstream final-record and detached-manifest bindings (H0: NONE — no predecessor)";
    private const string ProtectedSeatHoldsField = "Applicable protected seats vacant, absent, or recused; each marked NOT REVIEWED — HELD";
    private const string FinalRecordPathField = "Final H0 record repository path";
    private const string FinalRecordHashField = "Final H0 record SHA-256";
    private const string FinalRecordLengthField = "Final H0 record byte length";
    private const string OperatingTermsField = "Enacted operating-terms version and effective date";
    private const string OperatingTermsBindingField = "Enacted operating-terms exact file binding";
    private const string CompensationBindingField = "Operative compensation-policy exact file binding";
    private const string CohortDisclosureField = "Within-cohort identity/affiliation disclosure scope honored; confidentiality/no-contact boundary acknowledged";
    private const string FreezeManifestPathField = "H0 freeze-manifest repository path";
    private const string FreezeManifestHashField = "H0 freeze-manifest SHA-256";
    private const string FeasibilityRecordPathField = "Feasibility record repository path";
    private const string FeasibilityRecordHashField = "Feasibility record SHA-256";
    private const string FeasibilityIdentityField = "Feasibility record ID and version";
    private const string FeasibilityPredecessorField = "Predecessor feasibility record path, version, byte length, and SHA-256; or NONE";
    private const string DispositionIdentityField = "Product-owner disposition record ID and version";
    private const string DispositionPredecessorField = "Predecessor disposition record path, version, byte length, and SHA-256; or NONE";
    private const string ProductOwnerConflictField = "Product-owner conflict category and disposition";
    private const string UpstreamChainAuditIdField = "Upstream chain-audit ID";
    private const string UpstreamChainAuditUtcField = "Upstream chain-audit UTC instant";
    private const string UpstreamChainAuditBindingField = "Upstream chain-audit repository path, version, byte length, and SHA-256";
    private const string ChainAuditRevisionField = "Chain-audit exact candidate repository revision and dirty-tree disposition";
    private const string PublicEventBindingsField = "Public append-only event bindings, or NONE";
    private const string PrivateEventAttestationsField = "Private append-only event attestations, or NONE";
    private const string CurrentUpstreamDispositionField = "Current effective upstream dispositions and unresolved chain holds";
    private const string RecommendationIdentityColumn = "Need ID and mapped possibility";
    private const string RecommendationHoldsColumn = "Holds / seats still needed";
    private const string RecommendationEligibleTotalColumn = "Eligible total natural persons after matter recusals";
    private const string RecommendationEligiblePresentColumn = "Eligible natural persons present";
    private const string RecommendationEducatorsPresentColumn = "Practicing-educator natural persons present";
    private const string RecommendationConflictColumn = "Affected conflict categories / recusals";
    private const string RecommendationQuorumColumn = "Matter-specific quorum";
    private const string RecommendationTallyColumn = "Matter tally / consensus with eligible denominator";
    private const string ManifestRecommendationAuditField = "Per-recommendation matter counts, conflicts/recusals, quorum, and tally denominators";
    private const string CouncilOutcomeField = "Council outcome (`RECOMMENDATION RECORDED`, `NO RECOMMENDATION`, or `HOLD`)";
    private const string TallyField = "Vote/tally under the enacted procedure, or consensus/no vote";
    private const string ReadBackField = "Read-back confirmation (seat + count, no names by default)";
    private const string FinalHoldsField = "Applicable seat holds after read-back";
    private const string ManifestParticipantReviewField = "Participant read-back/review of those exact bytes completed (seat + count, no names by default)";
    private const string PreFreezeWithdrawalResolutionField = "Pre-freeze withdrawal/removal requests honored; unresolved requests";
    private const string RequestedCorrectionsField = "Requested corrections and accountable owners";
    private const string CorrectionIncorporationField = "Corrections and dissent incorporated before final hashing";
    private const string H0Title = "# Atlas 2.0 council priority session";
    private const string FreezeManifestTitle = "# Atlas H0 detached freeze manifest";
    private const string FeasibilityTitle = "# Atlas H0 separate feasibility record";
    private const string DispositionTitle = "# Atlas H0 separate product-owner disposition";
    private const string FrozenUtcField = "Frozen UTC instant";
    private const string FeasibilityUtcField = "Feasibility record UTC instant";
    private const string DispositionUtcField = "Product-owner disposition UTC instant";

    private const string FreezeBoundaryText = """
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

    private const string FeasibilityBoundaryText = """
        - Preserve the council's requested outcome even when the proposed implementation changes.
        - Do not turn ease of implementation into a retroactive council priority.
        - Mark uncertainty. Do not convert rehearsal findings or model judgment into human evidence.
        - If a candidate enters Amber, Restricted, or a protected seat's territory, record the stop; do not design around it.
        - Do not edit the bound H0 record, freeze manifest, or this completed record. A correction is the next `-feasibility-v<n>.md` and exactly binds its immediate predecessor path, version, byte length, and SHA-256. The fresh chain audit, not a self-hash inside this record, determines which linked version is current.
        - A completed record uses a fresh chain audit made after the H0 freeze and before this record. The current-disposition value ends `unresolved-chain-holds=NONE`; any missing, ambiguous, conflicting, withdrawn, restricted, or unresolved chain event is a HOLD and forbids completion.
        """;

    private const string DispositionBoundaryText = """
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

    private const string H0CompletionText = """
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

    private static readonly string[] NeedCardHeader =
    [
        "Prompt",
        "Participant-reviewed de-identified factual paraphrase",
    ];

    private static readonly string[] FieldHeader =
    [
        "Field",
        "Record",
    ];

    private static readonly string[] RequiredNeedCardPrompts =
    [
        "Need ID",
        "Recurring teacher work or learner-facing barrier",
        "Who encounters it (generic role/context only)",
        "How often it occurs",
        "Current workaround and its time/material cost",
        "What a useful paper/offline artifact would make possible",
        "What must remain under teacher control",
        "Unacceptable failure or harm",
        "First classroom proof that would earn trust",
        "Seat speaking",
    ];

    private static readonly string[] MappingHeader =
    [
        "Need ID",
        "Atlas entry / existing capability / new composition / no match",
        "Why it fits or fails to fit",
        "Likely lane (`G`, `A`, `R`, uncertain)",
        "Possibly implicated seats",
    ];

    private static readonly string[] RecommendationHeader =
    [
        "Order, if any",
        RecommendationIdentityColumn,
        "Participant-reviewed de-identified rationale",
        "First proof requested",
        RecommendationEligibleTotalColumn,
        RecommendationEligiblePresentColumn,
        RecommendationEducatorsPresentColumn,
        RecommendationConflictColumn,
        RecommendationQuorumColumn,
        RecommendationTallyColumn,
        RecommendationHoldsColumn,
        "Dissent or alternative",
    ];

    private static readonly string[] RecommendationSupplementalFields =
    [
        CouncilOutcomeField,
        "Needs deliberately not advanced, and why",
        "Useful possibilities with no atlas match",
        "Questions the session could not answer",
        "Corrections members made during read-back",
        "Whether members reached consensus, split, or made no ordering",
        TallyField,
        ReadBackField,
        FinalHoldsField,
    ];

    private static readonly string[] FeasibilityHeader =
    [
        "Recommended possibility",
        "Reusable engine/capability",
        "Smallest bounded slice",
        "Dependencies and migrations",
        "Required automated and human evidence",
        "Effort/risk range",
        "Conflicts with ADR, plan, or gate",
    ];

    private static readonly string[] DispositionHeader =
    [
        "Recommendation",
        "Disposition and date",
        "Exact bounded scope",
        "Reason",
        "Outstanding seats/gates",
        "Evidence required before completion",
    ];

    private static readonly string[] RequiredHeadings =
    [
        "### Session header",
        "### Need card — complete before opening the atlas",
        "### Need-to-possibility mapping — complete only after need capture",
        "## Council recommendation record",
        "## Close the session record; freeze only through a detached manifest",
        "## Completion check",
    ];

    private static readonly string[] H0SectionHeadings =
    [
        "## Non-negotiable boundaries",
        "## Prepare without pre-ranking",
        "### Session header",
        "## Sixty-minute needs-first agenda",
        "### Need card — complete before opening the atlas",
        "### Need-to-possibility mapping — complete only after need capture",
        "## Council recommendation record",
        "## Close the session record; freeze only through a detached manifest",
        "## Completion check",
    ];

    private static readonly string[] ForbiddenRecordHeadings =
    [
        "## Participant review and council-record freeze",
        "## Separate feasibility appendix — completed after the council record is frozen",
        "## Product-owner disposition — intentionally blank in the template",
        "## H0 freeze binding",
        "## Feasibility assessment",
        "## Product-owner disposition",
    ];

    private static readonly string[] RequiredSessionFields =
    [
        RecordIdentityField,
        "Session date and duration",
        RepositoryRevisionField,
        BuildArtifactsField,
        InstrumentField,
        "Facilitator (non-voting)",
        "Product owner present?",
        RosterBindingField,
        TotalSeatedPersonsField,
        PresentSeatsField,
        NaturalPersonsPresentField,
        PracticingEducatorsPresentField,
        AbsentSeatsField,
        ExactMaterialField,
        OperatingTermsField,
        OperatingTermsBindingField,
        ContentLicenseField,
        SeatAuthorityField,
        ParticipationConsentField,
        QuorumResultField,
        ConflictRecusalField,
        RecusalDisputeRecordsField,
        MultiCapacityField,
        ProtectedSeatHoldsField,
        WithdrawalField,
        CompensationField,
        CompensationAdministrationField,
        CompensationBindingField,
        NoteCollectionConsentField,
        PublicRecordConsentField,
        RecordingConsentField,
        CohortDisclosureField,
        PublicCreditField,
        ContentContributionChoiceField,
        RoleAcceptanceChoiceField,
        MaintainerAppointmentChoiceField,
        CopyrightStewardshipChoiceField,
        WithdrawalDispositionField,
        DecisionProcedureField,
    ];

    private static readonly string[] RequiredFreezeManifestFields =
    [
        RecordIdentityField,
        FinalRecordPathField,
        FinalRecordHashField,
        FinalRecordLengthField,
        H0UpstreamField,
        RepositoryRevisionField,
        BuildArtifactsField,
        InstrumentField,
        RosterBindingField,
        TotalSeatedPersonsField,
        PresentSeatsField,
        NaturalPersonsPresentField,
        PracticingEducatorsPresentField,
        AbsentSeatsField,
        MultiCapacityField,
        ContentLicenseField,
        OperatingTermsBindingField,
        SeatAuthorityField,
        ParticipationConsentField,
        QuorumResultField,
        ConflictRecusalField,
        RecusalDisputeRecordsField,
        WithdrawalField,
        CompensationField,
        CompensationAdministrationField,
        CompensationBindingField,
        NoteCollectionConsentField,
        PublicRecordConsentField,
        RecordingConsentField,
        CohortDisclosureField,
        PublicCreditField,
        ContentContributionChoiceField,
        RoleAcceptanceChoiceField,
        MaintainerAppointmentChoiceField,
        CopyrightStewardshipChoiceField,
        WithdrawalDispositionField,
        DecisionProcedureField,
        ManifestRecommendationAuditField,
        ExactMaterialField,
        "Findings, measurements, holds, dissent, and limitations",
        RequestedCorrectionsField,
        ManifestParticipantReviewField,
        FinalBytePublicationConsentField,
        CorrectionIncorporationField,
        PreFreezeWithdrawalResolutionField,
        ProtectedSeatHoldsField,
        FrozenUtcField,
        FreezeManifestPathField,
        "Append-only correction, withdrawal, credit-change, or supersession record paths; or none at freeze",
    ];

    private static readonly string[] RequiredFeasibilityBindingFields =
    [
        FeasibilityIdentityField,
        FeasibilityPredecessorField,
        RecordIdentityField,
        FinalRecordPathField,
        FinalRecordHashField,
        FreezeManifestPathField,
        FreezeManifestHashField,
        OperatingTermsBindingField,
        CompensationBindingField,
        UpstreamChainAuditIdField,
        UpstreamChainAuditUtcField,
        UpstreamChainAuditBindingField,
        ChainAuditRevisionField,
        PublicEventBindingsField,
        PrivateEventAttestationsField,
        CurrentUpstreamDispositionField,
        FeasibilityUtcField,
    ];

    private static readonly string[] RequiredDispositionBindingFields =
    [
        DispositionIdentityField,
        DispositionPredecessorField,
        RecordIdentityField,
        FinalRecordPathField,
        FinalRecordHashField,
        FreezeManifestPathField,
        FreezeManifestHashField,
        FeasibilityRecordPathField,
        FeasibilityRecordHashField,
        OperatingTermsBindingField,
        CompensationBindingField,
        UpstreamChainAuditIdField,
        UpstreamChainAuditUtcField,
        UpstreamChainAuditBindingField,
        ChainAuditRevisionField,
        PublicEventBindingsField,
        PrivateEventAttestationsField,
        CurrentUpstreamDispositionField,
        ProductOwnerConflictField,
        DispositionUtcField,
    ];

    private static readonly string[] FreezeManifestSectionHeadings =
    [
        "## H0 freeze binding",
        "## Non-circular and immutable boundary",
    ];

    private static readonly string[] FeasibilitySectionHeadings =
    [
        "## Frozen H0 binding",
        "## Feasibility assessment",
        "## Authority boundary",
    ];

    private static readonly string[] DispositionSectionHeadings =
    [
        "## Frozen H0 and feasibility binding",
        "## Product-owner disposition",
        "## Authority boundary",
    ];

    private static readonly (string Canonical, string[] Aliases)[] ProtectedSeatAliases =
    [
        ("AAC/SLP", ["AAC/SLP", "AAC", "SLP", "augmentative and alternative communication", "speech-language"]),
        ("accessibility/AT", ["accessibility/AT", "accessibility", "assistive technology", "AT"]),
        ("multilingual/family communication", ["multilingual/family communication", "multilingual", "family communication"]),
        ("privacy/legal/records", ["privacy/legal/records", "privacy", "legal", "records"]),
        ("safeguarding", ["safeguarding"]),
        ("curriculum", ["curriculum"]),
        ("rights/OER", ["rights/OER", "rights", "OER", "license steward", "licensing"]),
        ("district", ["district"]),
    ];

    public static AtlasCouncilRecordValidation Validate(string fileName, string markdown)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(markdown);

        var issues = new List<AtlasCouncilRecordIssue>();
        ValidateDatedFileName(fileName, issues);

        var lines = SplitLines(markdown);
        RefuseHiddenMarkdownStructure(lines, "atlas.h0.hidden-structure", issues);
        RefuseUnownedAuthorityClaims(lines, "atlas.h0.authority-overreach", issues);
        RefuseProductOwnerDispositionLanguage(lines, "atlas.h0.disposition-overreach", issues);
        RequireExactArtifactHeadingTopology(
            lines,
            H0Title,
            H0SectionHeadings,
            "atlas.lifecycle.section-boundary",
            issues);
        RequireExactTerminalSection(
            lines,
            "## Completion check",
            H0CompletionText,
            "atlas.h0.terminal-boundary",
            issues);
        var status = ReadStatus(lines, issues);
        ValidateNoDetachedSections(lines, issues);
        var headingIndexes = ReadHeadingIndexes(lines, issues);

        if (!TryGetCompleteHeadingOrder(headingIndexes, issues, out var orderedHeadingIndexes))
        {
            return Result(status, issues);
        }

        var sessionFields = ReadFieldTable(
            lines,
            orderedHeadingIndexes[0],
            orderedHeadingIndexes[1],
            issues,
            "session header",
            "session");

        RequireFields(sessionFields, RequiredSessionFields, issues, "session");
        RequireOnlyFields(sessionFields, RequiredSessionFields, "session", issues);
        var needCards = ValidateNeedCards(
            lines,
            orderedHeadingIndexes[1],
            orderedHeadingIndexes[2],
            issues);
        var needMappings = ValidateNeedMappings(
            lines,
            orderedHeadingIndexes[2],
            orderedHeadingIndexes[3],
            needCards.CompletedNeedIds,
            issues);
        var recommendations = ValidateRecommendationHolds(
            lines,
            orderedHeadingIndexes[3],
            orderedHeadingIndexes[4],
            needMappings,
            sessionFields,
            issues);
        var recommendationSupplemental = ValidateRecommendationSupplementalFields(
            lines,
            orderedHeadingIndexes[3],
            orderedHeadingIndexes[4],
            issues);

        if (status is AtlasCouncilRecordStatus.SessionRecord)
        {
            RequireCompletedFields(sessionFields, RequiredSessionFields, issues, "session");
            ValidateSessionAuthorityDispositions(sessionFields, issues);
            ValidateSessionRecordedMechanics(
                fileName,
                sessionFields,
                recommendationSupplemental,
                issues);
            var recusalDisputes = ValidateRecusalDisputeRecords(
                ReadNormalizedField(sessionFields, RecusalDisputeRecordsField),
                needMappings.MappedNeedIds,
                recommendations.RecommendationMatters,
                sessionFields,
                issues);
            ValidateProtectedSeatContinuity(
                sessionFields,
                lines,
                orderedHeadingIndexes,
                recommendationSupplemental,
                recusalDisputes,
                issues);
            RequireSubstantiveValue(
                sessionFields,
                AbsentSeatsField,
                issues,
                "atlas.holds.absent-seats-unrecorded",
                "The dated record must explicitly record absent seats, including an explicit none when applicable.");
            RequireCompleteSessionRecord(
                needCards,
                needMappings,
                recommendations,
                recommendationSupplemental,
                recusalDisputes,
                issues);
        }

        if (status is AtlasCouncilRecordStatus.Unrun
            && (sessionFields.Values.Any(IsSubstantive)
                || needCards.HasContent
                || needMappings.HasContent
                || recommendations.HasContent
                || recommendationSupplemental.HasContent))
        {
            issues.Add(new(
                "atlas.status.session-mismatch",
                "An UNRUN record cannot contain completed session, need-card, mapping, or recommendation content."));
        }

        if (status is AtlasCouncilRecordStatus.Unrun
            && (recommendations.HasContent || recommendationSupplemental.HasContent))
        {
            issues.Add(new(
                "atlas.lifecycle.recommendation-before-session",
                "Council recommendation content cannot appear while the dated record remains UNRUN."));
        }

        if (recommendations.HasContent && !needMappings.HasContent)
        {
            issues.Add(new(
                "atlas.lifecycle.recommendation-before-mapping",
                "Council recommendation content cannot appear without a preceding recorded need-to-possibility mapping."));
        }

        return Result(status, issues);
    }

    public static AtlasCouncilRecordValidation ValidateAgainstCanonicalTemplate(
        string fileName,
        string markdown,
        string canonicalTemplateMarkdown)
    {
        ArgumentNullException.ThrowIfNull(canonicalTemplateMarkdown);

        var validation = Validate(fileName, markdown);
        var issues = validation.Issues.ToList();
        var actualScaffold = BuildH0StaticScaffold(markdown);
        var canonicalScaffold = BuildH0StaticScaffold(canonicalTemplateMarkdown);
        if (!string.Equals(actualScaffold, canonicalScaffold, StringComparison.Ordinal))
        {
            issues.Add(new(
                "atlas.instrument.static-scaffold-mismatch",
                "The dated H0 record changes canonical instructions or other static instrument text outside the declared mutable tables and supplemental values."));
        }

        return new(validation.Status, issues.AsReadOnly());
    }

    private static string BuildH0StaticScaffold(string markdown)
    {
        var scaffold = new StringBuilder();
        var skippingSupplementalContinuation = false;
        var lines = SplitLines(markdown);
        var mutableTableRanges = H0MutableTableRanges(lines);
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var originalLine = lines[lineIndex];
            var line = originalLine.TrimEnd();
            var trimmed = line.Trim();
            if (ReadStatusValues([line]).Length == 1)
            {
                scaffold.AppendLine("**Status:** <STATUS>");
                skippingSupplementalContinuation = false;
                continue;
            }

            if (trimmed.StartsWith('|')
                && trimmed.EndsWith('|')
                && mutableTableRanges.Any(range =>
                    lineIndex > range.Start && lineIndex < range.End))
            {
                continue;
            }

            var supplementalField = RecommendationSupplementalFields.FirstOrDefault(field =>
                trimmed.StartsWith($"- **{field}:**", StringComparison.Ordinal));
            if (supplementalField is not null)
            {
                scaffold.Append("- **").Append(supplementalField).AppendLine(":** <VALUE>");
                skippingSupplementalContinuation = true;
                continue;
            }

            if (skippingSupplementalContinuation
                && (line.Length == 0 || char.IsWhiteSpace(line[0])))
            {
                continue;
            }

            skippingSupplementalContinuation = false;
            scaffold.AppendLine(trimmed);
        }

        return NormalizeWhitespace(scaffold.ToString());
    }

    private static (int Start, int End)[] H0MutableTableRanges(string[] lines)
    {
        (string Start, string End)[] headingPairs =
        [
            ("### Session header", "## Sixty-minute needs-first agenda"),
            ("### Need card — complete before opening the atlas", "### Need-to-possibility mapping — complete only after need capture"),
            ("### Need-to-possibility mapping — complete only after need capture", "## Council recommendation record"),
            ("## Council recommendation record", "## Close the session record; freeze only through a detached manifest"),
        ];
        var ranges = new List<(int Start, int End)>(headingPairs.Length);
        foreach (var (Start, End) in headingPairs)
        {
            var start = Array.FindIndex(
                lines,
                line => string.Equals(line.Trim(), Start, StringComparison.Ordinal));
            var end = Array.FindIndex(
                lines,
                line => string.Equals(line.Trim(), End, StringComparison.Ordinal));
            if (start >= 0 && end > start)
            {
                ranges.Add((start, end));
            }
        }

        return [.. ranges];
    }

    private static void ValidateNoDetachedSections(
        string[] lines,
        List<AtlasCouncilRecordIssue> issues)
    {
        if (lines.Any(line => ForbiddenRecordHeadings.Contains(line.Trim(), StringComparer.Ordinal)))
        {
            issues.Add(new(
                "atlas.lifecycle.detached-content-in-h0",
                "The H0 session record cannot contain freeze-manifest, feasibility, or product-owner-disposition sections; each belongs in its separate linked file."));
        }
    }

    public static AtlasCouncilArtifactValidation ValidateFreezeManifest(
        string manifestFileName,
        ReadOnlyMemory<byte> manifestBytes,
        string recordFileName,
        ReadOnlyMemory<byte> recordBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(recordFileName);

        var issues = new List<AtlasCouncilRecordIssue>();
        ValidateDerivedFileName(
            manifestFileName,
            recordFileName,
            "-freeze-manifest.md",
            "atlas.freeze.file-name",
            issues);

        if (!TryDecodeUtf8(manifestBytes, "atlas.freeze.utf8", issues, out var manifestMarkdown)
            || !TryDecodeUtf8(recordBytes, "atlas.freeze.record-utf8", issues, out var recordMarkdown))
        {
            return ArtifactResult(issues);
        }

        var recordValidation = Validate(recordFileName, recordMarkdown);
        if (!recordValidation.IsValid
            || recordValidation.Status is not AtlasCouncilRecordStatus.SessionRecord)
        {
            issues.Add(new(
                "atlas.freeze.record-invalid",
                "A freeze manifest requires a mechanically valid SESSION RECORD as its final H0 input."));
        }

        if (!TryReadArtifactFieldSection(
                manifestMarkdown,
                FreezeManifestStatus,
                "## H0 freeze binding",
                "## Non-circular and immutable boundary",
                RequiredFreezeManifestFields,
                "H0 freeze binding",
                "freeze",
                issues,
                out var fields,
                out var manifestLines,
                out _))
        {
            return ArtifactResult(issues);
        }

        RefuseHiddenMarkdownStructure(
            SplitLines(manifestMarkdown),
            "atlas.freeze.hidden-structure",
            issues);
        RefuseUnownedAuthorityClaims(
            manifestLines,
            "atlas.freeze.authority-overreach",
            issues);
        RefuseProductOwnerDispositionLanguage(
            manifestLines,
            "atlas.freeze.disposition-overreach",
            issues);
        RefuseManifestSelfHashClaims(manifestLines, fields, issues);

        RequireExactArtifactHeadingTopology(
            manifestLines,
            FreezeManifestTitle,
            FreezeManifestSectionHeadings,
            "atlas.freeze.section-boundary",
            issues);
        RequireExactTerminalSection(
            manifestLines,
            "## Non-circular and immutable boundary",
            FreezeBoundaryText,
            "atlas.freeze.terminal-boundary",
            issues);
        RequireOnlyTableContentBetweenHeadings(
            manifestLines,
            "## H0 freeze binding",
            "## Non-circular and immutable boundary",
            "atlas.freeze.interstitial-content",
            issues);

        if (fields.Keys.Any(field =>
                field.Contains("manifest", StringComparison.OrdinalIgnoreCase)
                && field.Contains("SHA-256", StringComparison.OrdinalIgnoreCase)))
        {
            issues.Add(new(
                "atlas.freeze.self-hash-field",
                "A detached freeze manifest must not contain a field for its own SHA-256."));
        }

        var recordData = ExtractValidatedRecordData(recordMarkdown);
        RequireBinding(
            fields,
            RecordIdentityField,
            recordData.RecordIdentity,
            "atlas.freeze.record-identity-mismatch",
            issues);
        RequireBinding(
            fields,
            FinalRecordPathField,
            RepositoryCouncilPath(recordFileName),
            "atlas.freeze.record-path-mismatch",
            issues);
        RequireBinding(
            fields,
            FinalRecordHashField,
            ComputeSha256(recordBytes.Span),
            "atlas.freeze.record-hash-mismatch",
            issues);
        RequireBinding(
            fields,
            FinalRecordLengthField,
            recordBytes.Length.ToString(CultureInfo.InvariantCulture),
            "atlas.freeze.record-length-mismatch",
            issues);
        RequireBinding(
            fields,
            H0UpstreamField,
            "NONE — H0 has no predecessor",
            "atlas.freeze.upstream-invented",
            issues);
        RequireBinding(
            fields,
            RepositoryRevisionField,
            recordData.RepositoryRevision,
            "atlas.freeze.repository-revision-mismatch",
            issues);
        RequireBinding(
            fields,
            BuildArtifactsField,
            recordData.BuildArtifacts,
            "atlas.freeze.build-artifact-mismatch",
            issues);
        RequireBinding(
            fields,
            InstrumentField,
            recordData.Instrument,
            "atlas.freeze.instrument-mismatch",
            issues);
        RequireBinding(
            fields,
            ExactMaterialField,
            recordData.ExactMaterial,
            "atlas.freeze.material-mismatch",
            issues);
        RequireBinding(
            fields,
            PresentSeatsField,
            recordData.PresentSeats,
            "atlas.freeze.seats-mismatch",
            issues);
        RequireBinding(
            fields,
            AbsentSeatsField,
            recordData.AbsentSeats,
            "atlas.freeze.absent-seats-mismatch",
            issues);
        RequireBinding(
            fields,
            NaturalPersonsPresentField,
            recordData.NaturalPersonsPresent,
            "atlas.freeze.natural-person-count-mismatch",
            issues);
        RequireBinding(
            fields,
            TotalSeatedPersonsField,
            recordData.TotalSeatedPersons,
            "atlas.freeze.total-seated-count-mismatch",
            issues);
        RequireBinding(
            fields,
            PracticingEducatorsPresentField,
            recordData.PracticingEducatorsPresent,
            "atlas.freeze.practicing-educator-count-mismatch",
            issues);
        RequireBinding(
            fields,
            RosterBindingField,
            recordData.RosterBinding,
            "atlas.freeze.roster-binding-mismatch",
            issues);
        RequireBinding(
            fields,
            MultiCapacityField,
            recordData.MultiCapacities,
            "atlas.freeze.capacities-mismatch",
            issues);
        RequireBinding(
            fields,
            ContentLicenseField,
            recordData.ContentLicense,
            "atlas.freeze.content-license-mismatch",
            issues);
        RequireBinding(
            fields,
            OperatingTermsBindingField,
            recordData.OperatingTermsBinding,
            "atlas.freeze.operating-terms-binding-mismatch",
            issues);
        RequireBinding(
            fields,
            SeatAuthorityField,
            recordData.SeatAuthority,
            "atlas.freeze.seat-authority-mismatch",
            issues);
        RequireBinding(
            fields,
            ParticipationConsentField,
            recordData.ParticipationConsent,
            "atlas.freeze.participation-consent-mismatch",
            issues);
        RequireBinding(
            fields,
            QuorumResultField,
            recordData.QuorumResult,
            "atlas.freeze.quorum-mismatch",
            issues);
        RequireBinding(
            fields,
            ConflictRecusalField,
            recordData.ConflictRecusals,
            "atlas.freeze.conflict-recusals-mismatch",
            issues);
        RequireBinding(
            fields,
            RecusalDisputeRecordsField,
            recordData.RecusalDisputeRecords,
            "atlas.freeze.recusal-dispute-records-mismatch",
            issues);
        RequireBinding(
            fields,
            WithdrawalField,
            recordData.WithdrawalAcknowledgement,
            "atlas.freeze.withdrawal-mismatch",
            issues);
        RequireBinding(
            fields,
            CompensationField,
            recordData.CompensationElection,
            "atlas.freeze.compensation-mismatch",
            issues);
        RequireBinding(
            fields,
            CompensationAdministrationField,
            recordData.CompensationAdministration,
            "atlas.freeze.compensation-administration-mismatch",
            issues);
        RequireBinding(
            fields,
            CompensationBindingField,
            recordData.CompensationBinding,
            "atlas.freeze.compensation-binding-mismatch",
            issues);
        RequireBinding(
            fields,
            NoteCollectionConsentField,
            recordData.NoteCollectionConsent,
            "atlas.freeze.note-consent-mismatch",
            issues);
        RequireBinding(
            fields,
            PublicRecordConsentField,
            recordData.PublicRecordConsent,
            "atlas.freeze.publication-consent-mismatch",
            issues);
        RequireBinding(
            fields,
            RecordingConsentField,
            recordData.RecordingConsent,
            "atlas.freeze.recording-consent-mismatch",
            issues);
        RequireBinding(
            fields,
            CohortDisclosureField,
            recordData.CohortDisclosure,
            "atlas.freeze.cohort-disclosure-mismatch",
            issues);
        RequireBinding(
            fields,
            PublicCreditField,
            recordData.PublicCreditChoice,
            "atlas.freeze.public-credit-mismatch",
            issues);
        RequireBinding(
            fields,
            ContentContributionChoiceField,
            recordData.ContentContributionChoice,
            "atlas.freeze.content-contribution-choice-mismatch",
            issues);
        RequireBinding(
            fields,
            RoleAcceptanceChoiceField,
            recordData.RoleAcceptanceChoice,
            "atlas.freeze.role-acceptance-choice-mismatch",
            issues);
        RequireBinding(
            fields,
            MaintainerAppointmentChoiceField,
            recordData.MaintainerAppointmentChoice,
            "atlas.freeze.maintainer-appointment-choice-mismatch",
            issues);
        RequireBinding(
            fields,
            CopyrightStewardshipChoiceField,
            recordData.CopyrightStewardshipChoice,
            "atlas.freeze.copyright-stewardship-choice-mismatch",
            issues);
        RequireBinding(
            fields,
            WithdrawalDispositionField,
            recordData.WithdrawalDisposition,
            "atlas.freeze.withdrawal-disposition-mismatch",
            issues);
        RequireBinding(
            fields,
            DecisionProcedureField,
            recordData.DecisionProcedure,
            "atlas.freeze.decision-procedure-mismatch",
            issues);
        RequireBinding(
            fields,
            ManifestRecommendationAuditField,
            recordData.RecommendedPossibilities.Count == 0
                ? "NONE — no recommendation rows"
                : $"BOUND — {recordData.RecommendedPossibilities.Count} recommendation rows in final H0 record",
            "atlas.freeze.recommendation-audit-mismatch",
            issues);
        RequireBinding(
            fields,
            ManifestParticipantReviewField,
            recordData.PresentSeats,
            "atlas.freeze.review-coverage-mismatch",
            issues);
        RequireExactValue(
            fields,
            FinalBytePublicationConsentField,
            $"RECONFIRMED — {recordData.PresentSeats}",
            "atlas.freeze.final-byte-publication-consent-invalid",
            issues);
        if (!HasResolvedCorrectionRecord(ReadNormalizedField(fields, RequestedCorrectionsField)))
        {
            issues.Add(new(
                "atlas.freeze.correction-resolution-invalid",
                "Requested corrections must be recorded as exact NONE or RESOLVED evidence with unresolved=NONE before final hashing."));
        }
        RequireExactValue(
            fields,
            CorrectionIncorporationField,
            "CONFIRMED — all corrections resolved and dissent preserved in final H0 bytes",
            "atlas.freeze.correction-incorporation-invalid",
            issues);
        if (!HasResolvedPreFreezeWithdrawals(
                ReadNormalizedField(fields, PreFreezeWithdrawalResolutionField),
                recordData.WithdrawalDisposition))
        {
            issues.Add(new(
                "atlas.freeze.withdrawal-resolution-invalid",
                "Before final hashing, every activity-withdrawal and council-resignation/vacancy reference must be exactly covered by the honored-request field, which must end 'unresolved=NONE'."));
        }
        RequireBinding(
            fields,
            ProtectedSeatHoldsField,
            recordData.ProtectedSeatHolds,
            "atlas.freeze.protected-seat-hold-mismatch",
            issues);
        RequireBinding(
            fields,
            FreezeManifestPathField,
            RepositoryCouncilPath(manifestFileName),
            "atlas.freeze.manifest-path-mismatch",
            issues);
        RequireUtcInstant(
            fields,
            FrozenUtcField,
            "atlas.freeze.utc-invalid",
            issues,
            out var frozenInstant);
        if (recordData.SessionDate is not null
            && frozenInstant is not null
            && DateOnly.FromDateTime(frozenInstant.Value.UtcDateTime) < recordData.SessionDate)
        {
            issues.Add(new(
                "atlas.freeze.before-session",
                "The H0 freeze instant cannot precede the dated session it claims to freeze."));
        }

        return ArtifactResult(issues);
    }

    public static AtlasCouncilArtifactValidation ValidateFeasibilityRecord(
        string feasibilityFileName,
        ReadOnlyMemory<byte> feasibilityBytes,
        string recordFileName,
        ReadOnlyMemory<byte> recordBytes,
        string manifestFileName,
        ReadOnlyMemory<byte> manifestBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(feasibilityFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(recordFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestFileName);

        var issues = new List<AtlasCouncilRecordIssue>();
        ValidateVersionedDerivedFileName(
            feasibilityFileName,
            recordFileName,
            "feasibility",
            "atlas.feasibility.file-name",
            issues,
            out var feasibilityVersion);
        var freezeValidation = ValidateFreezeManifest(
            manifestFileName,
            manifestBytes,
            recordFileName,
            recordBytes);
        if (!freezeValidation.IsValid)
        {
            issues.Add(new(
                "atlas.feasibility.freeze-invalid",
                "A feasibility record requires a valid detached H0 freeze manifest."));
        }

        if (!TryDecodeUtf8(feasibilityBytes, "atlas.feasibility.utf8", issues, out var markdown)
            || !TryDecodeUtf8(recordBytes, "atlas.feasibility.record-utf8", issues, out var recordMarkdown)
            || !TryDecodeUtf8(manifestBytes, "atlas.feasibility.manifest-utf8", issues, out var manifestMarkdown))
        {
            return ArtifactResult(issues);
        }

        if (!TryReadArtifactFieldSection(
                markdown,
                FeasibilityRecordStatus,
                "## Frozen H0 binding",
                "## Feasibility assessment",
                RequiredFeasibilityBindingFields,
                "frozen H0 binding",
                "feasibility-binding",
                issues,
                out var fields,
                out var lines,
                out var assessmentHeadingIndex))
        {
            return ArtifactResult(issues);
        }

        RefuseHiddenMarkdownStructure(
            SplitLines(markdown),
            "atlas.feasibility.hidden-structure",
            issues);
        RefuseUnownedAuthorityClaims(
            lines,
            "atlas.feasibility.authority-overreach",
            issues);
        RefuseProductOwnerDispositionLanguage(
            lines,
            "atlas.feasibility.disposition-overreach",
            issues);


        RequireExactArtifactHeadingTopology(
            lines,
            FeasibilityTitle,
            FeasibilitySectionHeadings,
            "atlas.feasibility.section-boundary",
            issues);
        RequireOnlyTableContentBetweenHeadings(
            lines,
            "## Frozen H0 binding",
            "## Feasibility assessment",
            "atlas.feasibility.binding-content",
            issues);

        var authorityHeadingIndex = FindSingleHeading(
            lines,
            "## Authority boundary",
            "atlas.feasibility.authority-heading",
            issues);
        if (authorityHeadingIndex < 0 || authorityHeadingIndex <= assessmentHeadingIndex)
        {
            return ArtifactResult(issues);
        }

        var recordData = ExtractValidatedRecordData(recordMarkdown);
        ValidateLinkedRecordVersion(
            fields,
            recordData.RecordIdentity,
            recordFileName,
            feasibilityVersion,
            "feasibility",
            FeasibilityIdentityField,
            FeasibilityPredecessorField,
            "atlas.feasibility",
            issues);
        RequireH0Bindings(
            fields,
            recordData.RecordIdentity,
            recordFileName,
            recordBytes,
            manifestFileName,
            manifestBytes,
            "atlas.feasibility",
            issues);
        RequireBinding(
            fields,
            OperatingTermsBindingField,
            recordData.OperatingTermsBinding,
            "atlas.feasibility.operating-terms-binding-mismatch",
            issues);
        RequireBinding(
            fields,
            CompensationBindingField,
            recordData.CompensationBinding,
            "atlas.feasibility.compensation-binding-mismatch",
            issues);
        ValidateChainAuditFields(fields, "atlas.feasibility", issues, out var chainAuditInstant);

        var feasibilityRows = ValidateDataRows(
            lines,
            assessmentHeadingIndex,
            authorityHeadingIndex,
            FeasibilityHeader,
            "feasibility",
            "atlas.feasibility.row-incomplete",
            recordData.RecommendedPossibilities,
            "atlas.feasibility.recommendation-mismatch",
            "council-recommended possibility",
            "atlas.feasibility.key-duplicate",
            "atlas.feasibility.coverage-incomplete",
            issues);
        RequireExactCoverage(
            feasibilityRows,
            recordData.RecommendedPossibilities,
            "atlas.feasibility.coverage-incomplete",
            "A completed feasibility record must contain exactly one complete row for every H0 recommendation and no row when H0 made no recommendation.",
            issues);
        RequireExactTerminalSection(
            lines,
            "## Authority boundary",
            FeasibilityBoundaryText,
            "atlas.feasibility.terminal-boundary",
            issues);
        RequireOnlyTableContentBetweenHeadings(
            lines,
            "## Feasibility assessment",
            "## Authority boundary",
            "atlas.feasibility.assessment-content",
            issues);
        RequireUtcInstant(
            fields,
            FeasibilityUtcField,
            "atlas.feasibility.utc-invalid",
            issues,
            out var feasibilityInstant);
        var frozenInstant = ReadLinkedUtcInstant(
            manifestMarkdown,
            "## H0 freeze binding",
            "## Non-circular and immutable boundary",
            FrozenUtcField);
        RequireLaterInstant(
            frozenInstant,
            chainAuditInstant,
            "atlas.feasibility.chain-audit-chronology-invalid",
            "The feasibility chain audit must be strictly later than the completed H0 freeze instant.",
            issues);
        RequireLaterInstant(
            chainAuditInstant,
            feasibilityInstant,
            "atlas.feasibility.chain-audit-stale",
            "The feasibility record UTC instant must be strictly later than its upstream chain audit.",
            issues);
        RequireLaterInstant(
            frozenInstant,
            feasibilityInstant,
            "atlas.feasibility.chronology-invalid",
            "The feasibility record UTC instant must be strictly later than the completed H0 freeze instant.",
            issues);

        return ArtifactResult(issues);
    }

    public static AtlasCouncilArtifactValidation ValidateDispositionRecord(
        string dispositionFileName,
        ReadOnlyMemory<byte> dispositionBytes,
        string recordFileName,
        ReadOnlyMemory<byte> recordBytes,
        string manifestFileName,
        ReadOnlyMemory<byte> manifestBytes,
        string feasibilityFileName,
        ReadOnlyMemory<byte> feasibilityBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dispositionFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(recordFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(feasibilityFileName);

        var issues = new List<AtlasCouncilRecordIssue>();
        ValidateVersionedDerivedFileName(
            dispositionFileName,
            recordFileName,
            "disposition",
            "atlas.disposition.file-name",
            issues,
            out var dispositionVersion);
        var feasibilityValidation = ValidateFeasibilityRecord(
            feasibilityFileName,
            feasibilityBytes,
            recordFileName,
            recordBytes,
            manifestFileName,
            manifestBytes);
        if (!feasibilityValidation.IsValid)
        {
            issues.Add(new(
                "atlas.disposition.feasibility-invalid",
                "A product-owner disposition requires a valid separate feasibility record."));
        }

        if (!TryDecodeUtf8(dispositionBytes, "atlas.disposition.utf8", issues, out var markdown)
            || !TryDecodeUtf8(recordBytes, "atlas.disposition.record-utf8", issues, out var recordMarkdown)
            || !TryDecodeUtf8(feasibilityBytes, "atlas.disposition.feasibility-utf8", issues, out var feasibilityMarkdown))
        {
            return ArtifactResult(issues);
        }

        var statusValues = ReadStatusValues(SplitLines(markdown));
        AtlasCouncilDispositionState? dispositionState = statusValues.Length == 1
            ? statusValues[0] switch
            {
                DispositionRecordStatus => AtlasCouncilDispositionState.Recorded,
                DispositionHeldStatus => AtlasCouncilDispositionState.Held,
                _ => null,
            }
            : null;
        var expectedStatus = dispositionState is AtlasCouncilDispositionState.Held
            ? DispositionHeldStatus
            : DispositionRecordStatus;

        if (!TryReadArtifactFieldSection(
                markdown,
                expectedStatus,
                "## Frozen H0 and feasibility binding",
                "## Product-owner disposition",
                RequiredDispositionBindingFields,
                "frozen H0 and feasibility binding",
                "disposition-binding",
                issues,
                out var fields,
                out var lines,
                out var dispositionHeadingIndex))
        {
            return ArtifactResult(issues);
        }

        RefuseHiddenMarkdownStructure(
            SplitLines(markdown),
            "atlas.disposition.hidden-structure",
            issues);
        RefuseUnownedAuthorityClaims(
            lines,
            "atlas.disposition.authority-overreach",
            issues);


        RequireExactArtifactHeadingTopology(
            lines,
            DispositionTitle,
            DispositionSectionHeadings,
            "atlas.disposition.section-boundary",
            issues);
        RequireOnlyTableContentBetweenHeadings(
            lines,
            "## Frozen H0 and feasibility binding",
            "## Product-owner disposition",
            "atlas.disposition.binding-content",
            issues);

        var authorityHeadingIndex = FindSingleHeading(
            lines,
            "## Authority boundary",
            "atlas.disposition.authority-heading",
            issues);
        if (authorityHeadingIndex < 0 || authorityHeadingIndex <= dispositionHeadingIndex)
        {
            return ArtifactResult(issues);
        }

        RequireExactTerminalSection(
            lines,
            "## Authority boundary",
            DispositionBoundaryText,
            "atlas.disposition.terminal-boundary",
            issues);
        RequireOnlyTableContentBetweenHeadings(
            lines,
            "## Product-owner disposition",
            "## Authority boundary",
            "atlas.disposition.table-content",
            issues);
        RequireUtcInstant(
            fields,
            DispositionUtcField,
            "atlas.disposition.utc-invalid",
            issues,
            out var dispositionInstant);
        var feasibilityInstant = ReadLinkedUtcInstant(
            feasibilityMarkdown,
            "## Frozen H0 binding",
            "## Feasibility assessment",
            FeasibilityUtcField);
        RequireLaterInstant(
            feasibilityInstant,
            dispositionInstant,
            "atlas.disposition.chronology-invalid",
            "The product-owner disposition UTC instant must be strictly later than the feasibility record UTC instant.",
            issues);

        var recordData = ExtractValidatedRecordData(recordMarkdown);
        ValidateLinkedRecordVersion(
            fields,
            recordData.RecordIdentity,
            recordFileName,
            dispositionVersion,
            "disposition",
            DispositionIdentityField,
            DispositionPredecessorField,
            "atlas.disposition",
            issues);
        RequireH0Bindings(
            fields,
            recordData.RecordIdentity,
            recordFileName,
            recordBytes,
            manifestFileName,
            manifestBytes,
            "atlas.disposition",
            issues);
        RequireBinding(
            fields,
            OperatingTermsBindingField,
            recordData.OperatingTermsBinding,
            "atlas.disposition.operating-terms-binding-mismatch",
            issues);
        RequireBinding(
            fields,
            CompensationBindingField,
            recordData.CompensationBinding,
            "atlas.disposition.compensation-binding-mismatch",
            issues);
        ValidateChainAuditFields(fields, "atlas.disposition", issues, out var chainAuditInstant);
        var conflictDisposition = ReadNormalizedField(fields, ProductOwnerConflictField);
        if (dispositionState is AtlasCouncilDispositionState.Held)
        {
            if (!HasHeldProductOwnerConflictDisposition(conflictDisposition))
            {
                issues.Add(new(
                    "atlas.disposition.held-conflict-invalid",
                    "A held product-owner record requires exactly 'HELD — conflict-category=<de-identified category>; written-finding=<substantive finding>; adoption=NONE'."));
            }
        }
        else if (!HasClearProductOwnerConflictDisposition(conflictDisposition))
        {
            issues.Add(new(
                "atlas.disposition.product-owner-conflict-invalid",
                "A recorded product-owner disposition requires exactly 'NONE — <basis>'. This validator version recognizes no substitute priority authority; an unresolved material conflict requires a held record."));
        }
        RequireBinding(
            fields,
            FeasibilityRecordPathField,
            RepositoryCouncilPath(feasibilityFileName),
            "atlas.disposition.feasibility-path-mismatch",
            issues);
        RequireBinding(
            fields,
            FeasibilityRecordHashField,
            ComputeSha256(feasibilityBytes.Span),
            "atlas.disposition.feasibility-hash-mismatch",
            issues);
        RequireLaterInstant(
            feasibilityInstant,
            chainAuditInstant,
            "atlas.disposition.chain-audit-chronology-invalid",
            "The disposition chain audit must be strictly later than the feasibility record it audits.",
            issues);
        RequireLaterInstant(
            chainAuditInstant,
            dispositionInstant,
            "atlas.disposition.chain-audit-stale",
            "The product-owner disposition UTC instant must be strictly later than its upstream chain audit.",
            issues);

        var feasibilityKeys = ExtractDataRowKeys(
            feasibilityMarkdown,
            "## Feasibility assessment",
            "## Authority boundary",
            FeasibilityHeader);
        var dispositionRows = ValidateDataRows(
            lines,
            dispositionHeadingIndex,
            authorityHeadingIndex,
            DispositionHeader,
            "product-owner disposition",
            "atlas.disposition.row-incomplete",
            feasibilityKeys,
            "atlas.disposition.feasibility-mismatch",
            "feasibility recommendation",
            "atlas.disposition.key-duplicate",
            "atlas.disposition.coverage-incomplete",
            issues);
        if (dispositionState is AtlasCouncilDispositionState.Held)
        {
            if (dispositionRows.HasContent)
            {
                issues.Add(new(
                    "atlas.disposition.held-action-present",
                    "A held product-owner record preserves no ADOPT, DEFER, DECLINE, or other substantive disposition row."));
            }
        }
        else
        {
            RequireExactCoverage(
                dispositionRows,
                feasibilityKeys,
                "atlas.disposition.coverage-incomplete",
                "A completed disposition record must contain exactly one complete row for every feasibility recommendation and no row when feasibility contains none.",
                issues);
            ValidateDispositionActions(
                lines,
                dispositionHeadingIndex,
                authorityHeadingIndex,
                dispositionInstant,
                issues);
            ValidateDispositionHoldContinuity(
                lines,
                dispositionHeadingIndex,
                authorityHeadingIndex,
                recordData.ProtectedSeatHolds,
                issues);
        }

        return ArtifactResult(issues, dispositionState);
    }

    private static bool TryReadArtifactFieldSection(
        string markdown,
        string expectedStatus,
        string fieldHeading,
        string nextHeading,
        string[] requiredFields,
        string sectionName,
        string sectionCode,
        List<AtlasCouncilRecordIssue> issues,
        out Dictionary<string, string> fields,
        out string[] lines,
        out int nextHeadingIndex)
    {
        lines = SplitLines(markdown);
        fields = new Dictionary<string, string>(StringComparer.Ordinal);
        ReadExactArtifactStatus(lines, expectedStatus, sectionCode, issues);
        var fieldHeadingIndex = FindSingleHeading(
            lines,
            fieldHeading,
            $"atlas.{sectionCode}.heading",
            issues);
        nextHeadingIndex = FindSingleHeading(
            lines,
            nextHeading,
            $"atlas.{sectionCode}.next-heading",
            issues);
        if (fieldHeadingIndex < 0 || nextHeadingIndex < 0)
        {
            return false;
        }

        if (fieldHeadingIndex >= nextHeadingIndex)
        {
            issues.Add(new(
                $"atlas.{sectionCode}.heading-order",
                $"The {sectionName} and following boundary headings are out of order."));
            return false;
        }

        fields = ReadFieldTable(
            lines,
            fieldHeadingIndex,
            nextHeadingIndex,
            issues,
            sectionName,
            sectionCode);
        RequireFields(fields, requiredFields, issues, sectionCode);
        RequireCompletedFields(fields, requiredFields, issues, sectionCode);
        RequireOnlyFields(fields, requiredFields, sectionCode, issues);
        return true;
    }

    private static void ReadExactArtifactStatus(
        string[] lines,
        string expectedStatus,
        string sectionCode,
        List<AtlasCouncilRecordIssue> issues)
    {
        var values = ReadStatusValues(lines);
        if (values.Length != 1)
        {
            issues.Add(new(
                $"atlas.{sectionCode}.status-count",
                "A linked Atlas record must contain exactly one Status line."));
        }
        else if (!string.Equals(values[0], expectedStatus, StringComparison.Ordinal))
        {
            issues.Add(new(
                $"atlas.{sectionCode}.status",
                "The linked Atlas record does not carry its exact required terminal status."));
        }
    }

    private static int FindSingleHeading(
        string[] lines,
        string heading,
        string issueCode,
        List<AtlasCouncilRecordIssue> issues)
    {
        var indexes = lines
            .Select((line, index) => (Line: line.Trim(), Index: index))
            .Where(candidate => string.Equals(candidate.Line, heading, StringComparison.Ordinal))
            .Select(candidate => candidate.Index)
            .ToArray();
        if (indexes.Length != 1)
        {
            issues.Add(new(
                issueCode,
                "A linked Atlas record must contain each canonical heading exactly once."));
            return -1;
        }

        return indexes[0];
    }

    private static void RequireExactArtifactHeadingTopology(
        string[] lines,
        string expectedTitle,
        string[] expectedSectionHeadings,
        string issueCode,
        List<AtlasCouncilRecordIssue> issues)
    {
        var headings = lines
            .Select(line => line.Trim())
            .Where(IsMarkdownHeading)
            .ToArray();
        var hasOneLeadingTitle = headings.Length > 0
            && string.Equals(headings[0], expectedTitle, StringComparison.Ordinal)
            && headings.Count(heading => heading.StartsWith("# ", StringComparison.Ordinal)) == 1;
        var actualSectionHeadings = headings
            .Where(heading => !heading.StartsWith("# ", StringComparison.Ordinal))
            .ToArray();

        if (!hasOneLeadingTitle
            || headings.Length != expectedSectionHeadings.Length + 1
            || !actualSectionHeadings.SequenceEqual(expectedSectionHeadings, StringComparer.Ordinal))
        {
            issues.Add(new(
                issueCode,
                "A linked Atlas artifact must contain only its one title and exact canonical section headings, in order; later-authority or release sections belong in separate records."));
        }
    }

    private static bool IsMarkdownHeading(string line)
    {
        var hashCount = line.TakeWhile(character => character == '#').Count();
        return hashCount > 0
            && hashCount < line.Length
            && line[hashCount] == ' ';
    }

    private static void RefuseHiddenMarkdownStructure(
        string[] lines,
        string issueCode,
        List<AtlasCouncilRecordIssue> issues)
    {
        var insideInlineCode = false;
        var hiddenStructure = false;
        foreach (var line in lines)
        {
            hiddenStructure |= line.Contains("<!--", StringComparison.Ordinal)
                || line.Contains("-->", StringComparison.Ordinal)
                || line.StartsWith('\t')
                || line.TakeWhile(character => character == ' ').Count() >= 4
                || line.TrimStart().StartsWith("```", StringComparison.Ordinal)
                || line.TrimStart().StartsWith("~~~", StringComparison.Ordinal)
                || IsSetextHeadingUnderline(line)
                || line.Contains("&nbsp;", StringComparison.OrdinalIgnoreCase)
                || ContainsRawHtmlTag(line, ref insideInlineCode);
        }

        if (insideInlineCode)
        {
            hiddenStructure = true;
        }

        if (hiddenStructure)
        {
            issues.Add(new(
                issueCode,
                "Governed Atlas status, headings, fields, and tables must be visible Markdown; raw HTML, HTML comments, indented code, fenced blocks, and unclosed inline-code spans are refused."));
        }
    }

    private static bool ContainsRawHtmlTag(string line, ref bool insideInlineCode)
    {
        for (var index = 0; index < line.Length; index++)
        {
            if (line[index] == '`')
            {
                insideInlineCode = !insideInlineCode;
                continue;
            }

            if (line[index] != '<' || insideInlineCode)
            {
                continue;
            }

            var cursor = index + 1;
            if (cursor < line.Length && line[cursor] == '/')
            {
                cursor++;
            }

            while (cursor < line.Length && char.IsWhiteSpace(line[cursor]))
            {
                cursor++;
            }

            if (cursor >= line.Length || !char.IsLetter(line[cursor]))
            {
                continue;
            }

            while (cursor < line.Length
                   && (char.IsLetterOrDigit(line[cursor]) || line[cursor] is '-' or ':'))
            {
                cursor++;
            }

            if (cursor < line.Length
                && (char.IsWhiteSpace(line[cursor]) || line[cursor] is '/' or '>'))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSetextHeadingUnderline(string line)
    {
        var candidate = line.Trim();
        return candidate.Length >= 3
            && (candidate.All(character => character == '=')
                || candidate.All(character => character == '-'));
    }

    private static void RefuseUnownedAuthorityClaims(
        string[] lines,
        string issueCode,
        List<AtlasCouncilRecordIssue> issues)
    {
        if (lines.Any(ContainsUnownedAuthorityClaim))
        {
            issues.Add(new(
                issueCode,
                "An Atlas record cannot claim release, publication, ADR-ratification, or protected-hold authority anywhere in its bytes."));
        }
    }

    private static bool ContainsUnownedAuthorityClaim(string value)
    {
        foreach (var clause in value.Split([';', '.', '!', '?', '|'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!ContainsAuthorityNegation(clause)
                && (ContainsDirectAuthorityClaim(clause) || ContainsAuthorityObjectAndVerb(clause)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsAuthorityNegation(string value)
    {
        string[] negations =
        [
            "cannot",
            "does not",
            "do not",
            "never",
            "no row may",
            "not authoriz",
            "not approv",
            "not ratif",
            "not waive",
            "not clear",
            "not permit",
            "may not",
        ];
        return negations.Any(negation => value.Contains(negation, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsDirectAuthorityClaim(string value)
    {
        string[] directClaims =
        [
            "may be released",
            "may now be released",
            "may publish",
            "may be published",
            "release may proceed",
            "publication may proceed",
            "adr accepted",
            "accept adr",
            "accept an adr",
        ];
        return directClaims.Any(claim => value.Contains(claim, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsAuthorityObjectAndVerb(string value)
    {
        string[] objects = ["release", "publish", "publication", "adr", "protected-seat hold", "protected hold"];
        string[] verbs =
        [
            "authoriz", "approv", "ratif", "waiv", "clear", "permit", "ship",
        ];
        return objects.Any(@object => value.Contains(@object, StringComparison.OrdinalIgnoreCase))
            && verbs.Any(verb => value.Contains(verb, StringComparison.OrdinalIgnoreCase));
    }

    private static void RefuseProductOwnerDispositionLanguage(
        string[] lines,
        string issueCode,
        List<AtlasCouncilRecordIssue> issues)
    {
        string[] reservedActions =
        [
            "ADOPT FOR PROPOSED FORGE MENU —",
            "DEFER —",
            "DECLINE —",
        ];
        if (lines.Any(line => reservedActions.Any(action =>
                line.Contains(action, StringComparison.OrdinalIgnoreCase))))
        {
            issues.Add(new(
                issueCode,
                "Product-owner disposition language is reserved to the separate downstream disposition record."));
        }
    }

    private static void RefuseManifestSelfHashClaims(
        string[] lines,
        IReadOnlyDictionary<string, string> fields,
        List<AtlasCouncilRecordIssue> issues)
    {
        var bindingHeadingIndex = Array.FindIndex(
            lines,
            line => string.Equals(line.Trim(), "## H0 freeze binding", StringComparison.Ordinal));
        string[] selfDigestTokens = ["sha-256", "sha256", "digest", "checksum", "self-hash"];
        var prebindingClaim = bindingHeadingIndex > 0
            && lines[..bindingHeadingIndex].Any(line =>
                selfDigestTokens.Any(token => line.Contains(token, StringComparison.OrdinalIgnoreCase)));
        var tableValueClaim = fields.Values.Any(value =>
            value.Contains("digest", StringComparison.OrdinalIgnoreCase)
            || value.Contains("checksum", StringComparison.OrdinalIgnoreCase)
            || value.Contains("self-hash", StringComparison.OrdinalIgnoreCase)
            || value.Contains("own SHA", StringComparison.OrdinalIgnoreCase)
            || (value.Contains("manifest", StringComparison.OrdinalIgnoreCase)
                && (value.Contains("SHA-256", StringComparison.OrdinalIgnoreCase)
                    || value.Contains("SHA256", StringComparison.OrdinalIgnoreCase))));
        if (prebindingClaim || tableValueClaim)
        {
            issues.Add(new(
                "atlas.freeze.self-hash-claim",
                "A detached freeze manifest cannot claim a digest for its own bytes in its preamble or binding-table values."));
        }
    }

    private static void RequireExactTerminalSection(
        string[] lines,
        string heading,
        string expectedText,
        string issueCode,
        List<AtlasCouncilRecordIssue> issues)
    {
        var headingIndex = Array.FindIndex(
            lines,
            line => string.Equals(line.Trim(), heading, StringComparison.Ordinal));
        if (headingIndex < 0)
        {
            return;
        }

        var actual = NormalizeWhitespace(string.Join('\n', lines[(headingIndex + 1)..]));
        if (!string.Equals(actual, NormalizeWhitespace(expectedText), StringComparison.Ordinal))
        {
            issues.Add(new(
                issueCode,
                "The terminal authority boundary must remain the exact canonical text and must be the artifact's final content."));
        }
    }

    private static void RequireOnlyTableContentBetweenHeadings(
        string[] lines,
        string startHeading,
        string endHeading,
        string issueCode,
        List<AtlasCouncilRecordIssue> issues)
    {
        var start = Array.FindIndex(
            lines,
            line => string.Equals(line.Trim(), startHeading, StringComparison.Ordinal));
        var end = Array.FindIndex(
            lines,
            line => string.Equals(line.Trim(), endHeading, StringComparison.Ordinal));
        if (start < 0 || end <= start)
        {
            return;
        }

        var hasUnexpectedContent = lines[(start + 1)..end]
            .Select(line => line.Trim())
            .Any(line => line.Length > 0 && !(line.StartsWith('|') && line.EndsWith('|')));
        if (hasUnexpectedContent)
        {
            issues.Add(new(
                issueCode,
                "Only the canonical Markdown table may appear between these linked-record headings; prose or hidden authority claims require a separate record."));
        }
    }

    private static string NormalizeWhitespace(string value)
        => string.Join(
            ' ',
            value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static void RequireUtcInstant(
        Dictionary<string, string> fields,
        string field,
        string issueCode,
        List<AtlasCouncilRecordIssue> issues,
        out DateTimeOffset? instant)
    {
        instant = null;
        if (!fields.TryGetValue(field, out var value)
            || !DateTimeOffset.TryParseExact(
                NormalizeCell(value),
                "yyyy-MM-dd'T'HH:mm:ss'Z'",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            issues.Add(new(
                issueCode,
                $"The '{field}' value must be an exact UTC instant in YYYY-MM-DDTHH:MM:SSZ form."));
            return;
        }

        instant = parsed;
    }

    private static DateTimeOffset? ReadLinkedUtcInstant(
        string markdown,
        string startHeading,
        string endHeading,
        string field)
    {
        var lines = SplitLines(markdown);
        var start = Array.FindIndex(
            lines,
            line => string.Equals(line.Trim(), startHeading, StringComparison.Ordinal));
        var end = Array.FindIndex(
            lines,
            line => string.Equals(line.Trim(), endHeading, StringComparison.Ordinal));
        if (start < 0 || end <= start)
        {
            return null;
        }

        var row = ReadTableRows(lines, start, end)
            .FirstOrDefault(candidate =>
                candidate.Count == 2
                && string.Equals(candidate[0], field, StringComparison.Ordinal));
        return row is not null
            && DateTimeOffset.TryParseExact(
                NormalizeCell(row[1]),
                "yyyy-MM-dd'T'HH:mm:ss'Z'",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed)
            ? parsed
            : null;
    }

    private static void RequireLaterInstant(
        DateTimeOffset? predecessor,
        DateTimeOffset? candidate,
        string issueCode,
        string message,
        List<AtlasCouncilRecordIssue> issues)
    {
        if (predecessor is not null
            && candidate is not null
            && candidate <= predecessor)
        {
            issues.Add(new(issueCode, message));
        }
    }

    private static void ValidateDispositionActions(
        string[] lines,
        int startHeading,
        int endHeading,
        DateTimeOffset? dispositionInstant,
        List<AtlasCouncilRecordIssue> issues)
    {
        string[] allowedActions = ["ADOPT FOR PROPOSED FORGE MENU", "DEFER", "DECLINE"];
        string[] forbiddenAuthorityClaims =
        [
            "RELEASE AUTHORIZED",
            "PUBLICATION AUTHORIZED",
            "PUBLISH AUTHORIZED",
            "ADR RATIFIED",
            "PROTECTED HOLD CLEARED",
        ];

        foreach (var row in ReadTableRows(lines, startHeading, endHeading)
                     .Where(row => row.Count == DispositionHeader.Length)
                     .Where(row => !RowsEqual(row, DispositionHeader) && !IsSeparatorRow(row))
                     .Where(row => row.Any(IsSubstantive)))
        {
            var parts = NormalizeCell(row[1]).Split(" — ", StringSplitOptions.None);
            if (parts.Length != 2
                || !allowedActions.Contains(parts[0], StringComparer.Ordinal)
                || !DateOnly.TryParseExact(
                    parts[1],
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var actionDate))
            {
                issues.Add(new(
                    "atlas.disposition.action-invalid",
                    "Each disposition must be exactly 'ADOPT FOR PROPOSED FORGE MENU — YYYY-MM-DD', 'DEFER — YYYY-MM-DD', or 'DECLINE — YYYY-MM-DD'."));
            }
            else if (dispositionInstant is not null
                && actionDate != DateOnly.FromDateTime(dispositionInstant.Value.UtcDateTime))
            {
                issues.Add(new(
                    "atlas.disposition.action-date-invalid",
                    "A disposition action date must equal the UTC calendar date of the disposition record instant."));
            }

            var joinedRow = string.Join(' ', row);
            if (forbiddenAuthorityClaims.Any(claim =>
                    joinedRow.Contains(claim, StringComparison.OrdinalIgnoreCase))
                || ContainsAuthorityObjectAndVerb(joinedRow))
            {
                issues.Add(new(
                    "atlas.disposition.authority-overreach",
                    "A product-owner disposition cannot authorize release or publication, ratify an ADR, or clear a protected-seat hold."));
            }
        }
    }

    private static void RequireOnlyFields(
        IReadOnlyDictionary<string, string> fields,
        IReadOnlyCollection<string> requiredFields,
        string sectionCode,
        List<AtlasCouncilRecordIssue> issues)
    {
        if (fields.Keys.Any(field => !requiredFields.Contains(field, StringComparer.Ordinal)))
        {
            issues.Add(new(
                $"atlas.{sectionCode}.field-unexpected",
                "The binding table contains a field outside its exact canonical field set."));
        }
    }

    private static void RequireH0Bindings(
        IReadOnlyDictionary<string, string> fields,
        string recordIdentity,
        string recordFileName,
        ReadOnlyMemory<byte> recordBytes,
        string manifestFileName,
        ReadOnlyMemory<byte> manifestBytes,
        string issuePrefix,
        List<AtlasCouncilRecordIssue> issues)
    {
        RequireBinding(
            fields,
            RecordIdentityField,
            recordIdentity,
            $"{issuePrefix}.record-identity-mismatch",
            issues);
        RequireBinding(
            fields,
            FinalRecordPathField,
            RepositoryCouncilPath(recordFileName),
            $"{issuePrefix}.record-path-mismatch",
            issues);
        RequireBinding(
            fields,
            FinalRecordHashField,
            ComputeSha256(recordBytes.Span),
            $"{issuePrefix}.record-hash-mismatch",
            issues);
        RequireBinding(
            fields,
            FreezeManifestPathField,
            RepositoryCouncilPath(manifestFileName),
            $"{issuePrefix}.manifest-path-mismatch",
            issues);
        RequireBinding(
            fields,
            FreezeManifestHashField,
            ComputeSha256(manifestBytes.Span),
            $"{issuePrefix}.manifest-hash-mismatch",
            issues);
    }

    private static void RequireBinding(
        IReadOnlyDictionary<string, string> fields,
        string field,
        string expected,
        string issueCode,
        List<AtlasCouncilRecordIssue> issues)
    {
        if (fields.TryGetValue(field, out var actual)
            && IsSubstantive(actual)
            && !string.Equals(NormalizeCell(actual), expected, StringComparison.Ordinal))
        {
            issues.Add(new(
                issueCode,
                "A linked Atlas record does not exactly bind the supplied predecessor bytes."));
        }
    }

    private static void RequireExactCoverage(
        DataRowValidation rows,
        IReadOnlySet<string> predecessorKeys,
        string issueCode,
        string message,
        List<AtlasCouncilRecordIssue> issues)
    {
        var invalid = predecessorKeys.Count == 0
            ? rows.HasContent
            : rows.LinkedKeys.Count != predecessorKeys.Count
                || predecessorKeys.Any(key => !rows.LinkedKeys.Contains(key));
        if (invalid && !issues.Any(issue => string.Equals(issue.Code, issueCode, StringComparison.Ordinal)))
        {
            issues.Add(new(issueCode, message));
        }
    }

    private static H0RecordData ExtractValidatedRecordData(string markdown)
    {
        var lines = SplitLines(markdown);
        var ignoredIssues = new List<AtlasCouncilRecordIssue>();
        var headingIndexes = ReadHeadingIndexes(lines, ignoredIssues);
        if (!TryGetCompleteHeadingOrder(headingIndexes, ignoredIssues, out var orderedHeadingIndexes))
        {
            return new(
                string.Empty,
                new HashSet<string>(StringComparer.Ordinal),
                new Dictionary<string, string>(StringComparer.Ordinal));
        }

        var sessionFields = ReadFieldTable(
            lines,
            orderedHeadingIndexes[0],
            orderedHeadingIndexes[1],
            ignoredIssues,
            "session header",
            "session");
        var needCards = ValidateNeedCards(
            lines,
            orderedHeadingIndexes[1],
            orderedHeadingIndexes[2],
            ignoredIssues);
        var mappings = ValidateNeedMappings(
            lines,
            orderedHeadingIndexes[2],
            orderedHeadingIndexes[3],
            needCards.CompletedNeedIds,
            ignoredIssues);
        var recommendations = ValidateRecommendationHolds(
            lines,
            orderedHeadingIndexes[3],
            orderedHeadingIndexes[4],
            mappings,
            sessionFields,
            ignoredIssues);
        var recordIdentity = sessionFields.TryGetValue(RecordIdentityField, out var identity)
            ? NormalizeCell(identity)
            : string.Empty;
        return new(
            recordIdentity,
            recommendations.MappedPossibilities,
            sessionFields);
    }

    private static string ReadNormalizedField(
        Dictionary<string, string> fields,
        string field)
        => fields.TryGetValue(field, out var value)
            ? NormalizeCell(value)
            : string.Empty;

    private static HashSet<string> ExtractDataRowKeys(
        string markdown,
        string tableHeading,
        string nextHeading,
        string[] expectedHeader)
    {
        var lines = SplitLines(markdown);
        var start = Array.FindIndex(
            lines,
            line => string.Equals(line.Trim(), tableHeading, StringComparison.Ordinal));
        var end = Array.FindIndex(
            lines,
            line => string.Equals(line.Trim(), nextHeading, StringComparison.Ordinal));
        if (start < 0 || end <= start)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        return ReadTableRows(lines, start, end)
            .Where(row => row.Count == expectedHeader.Length)
            .Where(row => !RowsEqual(row, expectedHeader) && !IsSeparatorRow(row))
            .Where(row => row.All(IsSubstantive))
            .Select(row => NormalizeCell(row[0]))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static void ValidateDerivedFileName(
        string candidateFileName,
        string recordFileName,
        string suffix,
        string issueCode,
        List<AtlasCouncilRecordIssue> issues)
    {
        var expected = recordFileName.EndsWith(FileSuffix, StringComparison.Ordinal)
            ? recordFileName[..^FileSuffix.Length] + suffix
            : string.Empty;
        if (!string.Equals(candidateFileName, expected, StringComparison.Ordinal))
        {
            issues.Add(new(
                issueCode,
                "A linked Atlas record filename must share the exact dated H0 record stem and use its canonical suffix."));
        }
    }

    private static bool TryDecodeUtf8(
        ReadOnlyMemory<byte> bytes,
        string issueCode,
        List<AtlasCouncilRecordIssue> issues,
        out string markdown)
    {
        try
        {
            markdown = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(bytes.Span);
            return true;
        }
        catch (DecoderFallbackException)
        {
            markdown = string.Empty;
            issues.Add(new(issueCode, "The linked Atlas record must be valid UTF-8."));
            return false;
        }
    }

    private static string ComputeSha256(ReadOnlySpan<byte> bytes)
        => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string RepositoryCouncilPath(string fileName)
        => $"docs/council/{fileName}";

    private static string[] SplitLines(string markdown)
        => markdown
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

    private static AtlasCouncilArtifactValidation ArtifactResult(
        List<AtlasCouncilRecordIssue> issues,
        AtlasCouncilDispositionState? dispositionState = null)
        => new(issues.AsReadOnly(), dispositionState);

    private static AtlasCouncilRecordValidation Result(
        AtlasCouncilRecordStatus? status,
        List<AtlasCouncilRecordIssue> issues)
        => new(status, issues.AsReadOnly());

    private static void ValidateDatedFileName(
        string fileName,
        List<AtlasCouncilRecordIssue> issues)
    {
        if (!fileName.StartsWith(FilePrefix, StringComparison.Ordinal)
            || !fileName.EndsWith(FileSuffix, StringComparison.Ordinal)
            || fileName.Length != FilePrefix.Length + "yyyy-MM-dd".Length + FileSuffix.Length)
        {
            issues.Add(new(
                "atlas.file-name",
                "A dated record must use atlas-priority-session-YYYY-MM-DD.md."));
            return;
        }

        var dateText = fileName.Substring(FilePrefix.Length, "yyyy-MM-dd".Length);
        if (!DateOnly.TryParseExact(
            dateText,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _))
        {
            issues.Add(new(
                "atlas.file-date",
                "The dated record filename must contain a real ISO calendar date."));
        }
    }

    private static AtlasCouncilRecordStatus? ReadStatus(
        string[] lines,
        List<AtlasCouncilRecordIssue> issues)
    {
        var values = ReadStatusValues(lines);

        if (values.Length != 1)
        {
            issues.Add(new(
                "atlas.status.count",
                "A dated record must contain exactly one Status line."));
            return null;
        }

        return values[0] switch
        {
            UnrunStatus => AtlasCouncilRecordStatus.Unrun,
            SessionRecordStatus => AtlasCouncilRecordStatus.SessionRecord,
            _ => UnknownStatus(issues),
        };
    }

    private static string[] ReadStatusValues(string[] lines)
        => [.. lines
            .Select(line => line.Trim().Replace("**", string.Empty, StringComparison.Ordinal))
            .Where(line => line.StartsWith("Status:", StringComparison.Ordinal))
            .Select(line => line["Status:".Length..].Trim())];

    private static AtlasCouncilRecordStatus? UnknownStatus(List<AtlasCouncilRecordIssue> issues)
    {
        issues.Add(new(
            "atlas.status.unknown",
            $"Status must be exactly {UnrunStatus} or {SessionRecordStatus}."));
        return null;
    }

    private static Dictionary<string, List<int>> ReadHeadingIndexes(
        string[] lines,
        List<AtlasCouncilRecordIssue> issues)
    {
        var indexes = RequiredHeadings.ToDictionary(
            heading => heading,
            _ => new List<int>(),
            StringComparer.Ordinal);

        for (var index = 0; index < lines.Length; index++)
        {
            var candidate = lines[index].Trim();
            if (indexes.TryGetValue(candidate, out var matches))
            {
                matches.Add(index);
            }
        }

        foreach (var heading in RequiredHeadings)
        {
            var count = indexes[heading].Count;
            if (count == 0)
            {
                issues.Add(new(
                    "atlas.heading.missing",
                    $"The dated record is missing required heading '{heading}'."));
            }
            else if (count > 1)
            {
                issues.Add(new(
                    "atlas.heading.duplicate",
                    $"The dated record repeats required heading '{heading}'."));
            }
        }

        return indexes;
    }

    private static bool TryGetCompleteHeadingOrder(
        Dictionary<string, List<int>> headingIndexes,
        List<AtlasCouncilRecordIssue> issues,
        out int[] orderedIndexes)
    {
        orderedIndexes = new int[RequiredHeadings.Length];
        for (var index = 0; index < RequiredHeadings.Length; index++)
        {
            var matches = headingIndexes[RequiredHeadings[index]];
            if (matches.Count != 1)
            {
                return false;
            }

            orderedIndexes[index] = matches[0];
        }

        for (var index = 1; index < orderedIndexes.Length; index++)
        {
            if (orderedIndexes[index - 1] >= orderedIndexes[index])
            {
                issues.Add(new(
                    "atlas.heading.order",
                    "Required Atlas record headings do not follow the canonical lifecycle order."));
                return false;
            }
        }

        return true;
    }

    private static Dictionary<string, string> ReadFieldTable(
        string[] lines,
        int startHeading,
        int endHeading,
        List<AtlasCouncilRecordIssue> issues,
        string sectionName,
        string sectionCode)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        var rows = ReadTableRows(lines, startHeading, endHeading);
        ValidateCanonicalTableScaffold(
            rows,
            FieldHeader,
            sectionName,
            $"atlas.{sectionCode}.header",
            $"atlas.{sectionCode}.header-duplicate",
            $"atlas.{sectionCode}.separator",
            issues);
        foreach (var row in rows)
        {
            if (row.Count != 2)
            {
                issues.Add(new(
                    "atlas.table.width",
                    $"The {sectionName} table must retain its canonical two-column shape."));
                continue;
            }

            if (IsSeparatorRow(row))
            {
                continue;
            }

            if (RowsEqual(row, FieldHeader))
            {
                continue;
            }

            if (!fields.TryAdd(row[0], row[1]))
            {
                issues.Add(new(
                    "atlas.field.duplicate",
                    $"The {sectionName} repeats a field label."));
            }
        }

        return fields;
    }

    private static void RequireFields(
        Dictionary<string, string> fields,
        IEnumerable<string> required,
        List<AtlasCouncilRecordIssue> issues,
        string sectionName)
    {
        foreach (var field in required)
        {
            if (!fields.ContainsKey(field))
            {
                issues.Add(new(
                    $"atlas.{sectionName}.field-missing",
                    $"The {sectionName} section is missing required field '{field}'."));
            }
        }
    }

    private static void RequireCompletedFields(
        Dictionary<string, string> fields,
        IEnumerable<string> required,
        List<AtlasCouncilRecordIssue> issues,
        string sectionName)
    {
        foreach (var field in required)
        {
            if (fields.TryGetValue(field, out var value) && !IsSubstantive(value))
            {
                issues.Add(new(
                    $"atlas.{sectionName}.field-pending",
                    $"The {sectionName} field '{field}' is still blank or marked not run."));
            }
        }
    }

    private static void RequireSubstantiveValue(
        Dictionary<string, string> fields,
        string field,
        List<AtlasCouncilRecordIssue> issues,
        string code,
        string message)
    {
        if (fields.TryGetValue(field, out var value) && !IsSubstantive(value))
        {
            issues.Add(new(code, message));
        }
    }

    private static void ValidateSessionAuthorityDispositions(
        Dictionary<string, string> fields,
        List<AtlasCouncilRecordIssue> issues)
    {
        if (fields.TryGetValue(OperatingTermsField, out var operatingTerms))
        {
            var parts = NormalizeCell(operatingTerms).Split(" — ", StringSplitOptions.None);
            if (parts.Length != 3
                || !string.Equals(parts[0], "ENACTED", StringComparison.Ordinal)
                || !IsPositiveRecordComponent(parts[1])
                || !DateOnly.TryParseExact(
                    parts[2],
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out _))
            {
                issues.Add(new(
                    "atlas.session.operating-terms-unenacted",
                    "A SESSION RECORD requires 'ENACTED — <exact version> — YYYY-MM-DD'; a draft, pending, or unenacted rule cannot enter or freeze H0."));
            }
        }

        // The structured authority parser below validates its exact grammar.
        // Do not feed the complete value through the generic positive-disposition
        // filter: valid closed-enum components include practicing-educator=NO.
        RequirePrefixedDisposition(fields, ContentLicenseField, "CHOSEN — ", "atlas.session.content-license-unresolved", issues);
        RequirePrefixedDisposition(fields, ParticipationConsentField, "ACCEPTED — ", "atlas.session.participation-consent-invalid", issues);
        RequirePrefixedDisposition(fields, NoteCollectionConsentField, "ACCEPTED — ", "atlas.session.note-consent-invalid", issues);
        RequirePrefixedDisposition(fields, PublicRecordConsentField, "ACCEPTED — ", "atlas.session.publication-consent-invalid", issues);
        RequirePrefixedDisposition(fields, WithdrawalField, "ACKNOWLEDGED — ", "atlas.session.withdrawal-acknowledgement-invalid", issues);
        RequirePrefixedDisposition(fields, QuorumResultField, "MET — ", "atlas.session.quorum-not-met", issues);
        RequirePrefixedDisposition(fields, DecisionProcedureField, "APPLIED — ", "atlas.session.procedure-unapplied", issues);
        RequireOneOfPrefixedDispositions(
            fields,
            AbsentSeatsField,
            ["NONE — ", "ABSENT — "],
            "atlas.session.absent-seats-invalid",
            issues);
        RequireOneOfPrefixedDispositions(
            fields,
            ProtectedSeatHoldsField,
            ["NONE — ", "HELD — "],
            "atlas.session.protected-seat-holds-invalid",
            issues);
        RequireOneOfPrefixedDispositions(
            fields,
            ConflictRecusalField,
            ["NONE — ", "RECUSALS — "],
            "atlas.session.conflict-recusals-invalid",
            issues);
        RequireOneOfPrefixedDispositions(
            fields,
            MultiCapacityField,
            ["NONE — ", "DISCLOSED — "],
            "atlas.session.multi-capacity-invalid",
            issues);
    }

    private static void ValidateSessionRecordedMechanics(
        string fileName,
        Dictionary<string, string> fields,
        RecommendationSupplementalValidation supplemental,
        List<AtlasCouncilRecordIssue> issues)
    {
        var sessionValue = ReadNormalizedField(fields, "Session date and duration");
        var sessionDateValid = TryReadSessionDate(sessionValue, out var sessionDate);
        var fileDateText = fileName.Length >= FilePrefix.Length + "yyyy-MM-dd".Length
            ? fileName.Substring(FilePrefix.Length, "yyyy-MM-dd".Length)
            : string.Empty;
        var fileDateValid = DateOnly.TryParseExact(
            fileDateText,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var fileDate);
        if (!sessionDateValid || !fileDateValid || sessionDate != fileDate)
        {
            issues.Add(new(
                "atlas.session.date-invalid",
                "A SESSION RECORD requires 'YYYY-MM-DD · <duration in minutes>' with 1–1440 minutes, and that date must exactly match its filename."));
        }

        var terms = ReadNormalizedField(fields, OperatingTermsField)
            .Split(" — ", StringSplitOptions.None);
        if (terms.Length == 3
            && !string.Equals(terms[1], SupportedOperatingTermsId, StringComparison.Ordinal))
        {
            issues.Add(new(
                "atlas.session.operating-terms-unsupported",
                $"The validator implements only {SupportedOperatingTermsId}; another enacted rule requires a reviewed validator update before a SESSION RECORD can pass."));
        }
        if (sessionDateValid
            && terms.Length == 3
            && DateOnly.TryParseExact(
                terms[2],
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var termsDate)
            && termsDate > sessionDate)
        {
            issues.Add(new(
                "atlas.session.operating-terms-retroactive",
                "The operating terms must be effective on or before the session date."));
        }

        var recordIdentity = ReadNormalizedField(fields, RecordIdentityField);
        var versionMarker = recordIdentity.LastIndexOf(" v", StringComparison.Ordinal);
        if (!recordIdentity.StartsWith("H0-", StringComparison.Ordinal)
            || versionMarker <= "H0-".Length
            || !IsDottedNumericVersion(recordIdentity[(versionMarker + 2)..]))
        {
            issues.Add(new(
                "atlas.session.record-identity-invalid",
                "The H0 record identity must be an exact 'H0-<record-id> v<numeric-version>' value."));
        }

        var repositoryRevision = ReadNormalizedField(fields, RepositoryRevisionField);
        var revisionToken = repositoryRevision
            .Split([' ', '·'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? string.Empty;
        if (revisionToken.Length is < 7 or > 64
            || !revisionToken.All(char.IsAsciiHexDigit)
            || (!repositoryRevision.Contains("clean", StringComparison.OrdinalIgnoreCase)
                && !repositoryRevision.Contains("dirty", StringComparison.OrdinalIgnoreCase)))
        {
            issues.Add(new(
                "atlas.session.repository-revision-invalid",
                "The repository field must begin with a 7–64 digit hexadecimal revision and explicitly record a clean or dirty-tree disposition."));
        }

        RequireSha256BearingField(fields, BuildArtifactsField, "atlas.session.build-artifacts-invalid", issues);
        RequireSha256BearingField(fields, InstrumentField, "atlas.session.instrument-invalid", issues);

        var presentSeats = ReadNormalizedField(fields, PresentSeatsField);
        var presentSeatCountsValid = TryParseSeatCounts(
            presentSeats,
            out var presentSeatCounts,
            out _);
        if (!presentSeatCountsValid)
        {
            issues.Add(new(
                "atlas.session.seat-counts-invalid",
                "Seats present must be a semicolon-separated list of '<seat>: <positive count>' entries."));
        }

        var naturalPersonsValid = TryReadPositiveCount(fields, NaturalPersonsPresentField, out var naturalPersons);
        if (!naturalPersonsValid)
        {
            issues.Add(new(
                "atlas.session.natural-person-count-invalid",
                "Natural persons present must be one positive decimal integer."));
        }

        var totalSeatedValid = TryReadPositiveCount(fields, TotalSeatedPersonsField, out var totalSeatedPersons);
        if (!totalSeatedValid)
        {
            issues.Add(new(
                "atlas.session.total-seated-count-invalid",
                "Total seated, non-vacant natural persons must be one positive decimal integer."));
        }

        if (naturalPersonsValid
            && totalSeatedValid
            && naturalPersons > totalSeatedPersons)
        {
            issues.Add(new(
                "atlas.session.natural-person-count-inconsistent",
                "Natural persons present cannot exceed total seated, non-vacant natural persons."));
        }

        var practicingEducatorsValid = TryReadPositiveCount(
            fields,
            PracticingEducatorsPresentField,
            out var practicingEducatorsPresent);
        if (!practicingEducatorsValid)
        {
            issues.Add(new(
                "atlas.session.practicing-educator-count-invalid",
                "Practicing-educator natural persons present must be one positive decimal integer."));
        }
        else if (naturalPersonsValid && practicingEducatorsPresent > naturalPersons)
        {
            issues.Add(new(
                "atlas.session.practicing-educator-count-inconsistent",
                "Practicing-educator natural persons present cannot exceed natural persons present."));
        }

        if (naturalPersonsValid
            && totalSeatedValid
            && practicingEducatorsValid
            && (naturalPersons <= totalSeatedPersons / 2
                || naturalPersons < 3
                || practicingEducatorsPresent < 2))
        {
            issues.Add(new(
                "atlas.session.quorum-arithmetic-invalid",
                "Session-opening MET requires a majority of all seated non-vacant natural persons, at least three natural persons present, and at least two practicing educators present."));
        }

        if (!HasStructuredRosterBinding(ReadNormalizedField(fields, RosterBindingField)))
        {
            issues.Add(new(
                "atlas.session.roster-binding-invalid",
                "The current enacted roster requires an exact BOUND repository path, version, positive byte length, SHA-256, and source commit."));
        }

        RequireDispositionCoverage(
            fields,
            ParticipationConsentField,
            "ACCEPTED — ",
            presentSeats,
            "atlas.session.participation-coverage-mismatch",
            issues);
        RequireDispositionCoverage(
            fields,
            WithdrawalField,
            "ACKNOWLEDGED — ",
            presentSeats,
            "atlas.session.withdrawal-coverage-mismatch",
            issues);
        RequireDispositionCoverage(
            fields,
            NoteCollectionConsentField,
            "ACCEPTED — ",
            presentSeats,
            "atlas.session.note-consent-coverage-mismatch",
            issues);
        RequireDispositionCoverage(
            fields,
            PublicRecordConsentField,
            "ACCEPTED — ",
            presentSeats,
            "atlas.session.publication-consent-coverage-mismatch",
            issues);

        var recording = ReadNormalizedField(fields, RecordingConsentField);
        if (!string.Equals(recording, "NO RECORDING", StringComparison.Ordinal)
            && !string.Equals(recording, $"ACCEPTED — {presentSeats}", StringComparison.Ordinal))
        {
            issues.Add(new(
                "atlas.session.recording-consent-invalid",
                "Recording must be exactly 'NO RECORDING' or 'ACCEPTED — <exact seats present>'."));
        }

        RequireExactValue(
            fields,
            PublicCreditField,
            $"RECORDED — {presentSeats}",
            "atlas.session.public-credit-invalid",
            issues);
        RequireExactValue(
            fields,
            ContentContributionChoiceField,
            $"NONE — {presentSeats}",
            "atlas.session.content-contribution-choice-invalid",
            issues);
        RequireExactValue(
            fields,
            RoleAcceptanceChoiceField,
            $"ACCEPTED — {presentSeats} — {SupportedOperatingTermsId}",
            "atlas.session.role-acceptance-choice-invalid",
            issues);
        RequireExactValue(
            fields,
            MaintainerAppointmentChoiceField,
            $"NONE — {presentSeats}",
            "atlas.session.maintainer-appointment-choice-invalid",
            issues);
        RequireExactValue(
            fields,
            CopyrightStewardshipChoiceField,
            $"NONE — {presentSeats}",
            "atlas.session.copyright-stewardship-choice-invalid",
            issues);
        RequireExactValue(
            fields,
            CohortDisclosureField,
            $"RECORDED AND HONORED — {presentSeats}",
            "atlas.session.cohort-disclosure-invalid",
            issues);

        if (!HasResolvedH0WithdrawalDispositions(
                ReadNormalizedField(fields, WithdrawalDispositionField)))
        {
            issues.Add(new(
                "atlas.session.withdrawal-disposition-invalid",
                "H0 must separately record activity withdrawal, council resignation/vacancy, and the non-member H7 role as not applicable, with unresolved=NONE."));
        }

        if (!HasStructuredControlBinding(
                ReadNormalizedField(fields, OperatingTermsBindingField),
                "docs/council/draft-first-cohort-operating-terms.md"))
        {
            issues.Add(new(
                "atlas.session.operating-terms-binding-invalid",
                "The enacted operating terms require an exact BOUND path, positive byte length, SHA-256, and source commit."));
        }

        if (!HasStructuredControlBinding(
                ReadNormalizedField(fields, CompensationBindingField),
                "docs/council/compensation-policy.md"))
        {
            issues.Add(new(
                "atlas.session.compensation-binding-invalid",
                "The operative compensation policy requires an exact BOUND path, positive byte length, SHA-256, and source commit."));
        }

        if (!supplemental.Values.TryGetValue(ReadBackField, out var readBack)
            || !string.Equals(NormalizeCell(readBack), presentSeats, StringComparison.Ordinal))
        {
            issues.Add(new(
                "atlas.session.read-back-coverage-mismatch",
                "The participant read-back seat/count must exactly match the recorded seats present."));
        }

        if (terms.Length == 3)
        {
            var exactTermsVersion = terms[1];
            RequireExactValue(
                fields,
                QuorumResultField,
                $"MET — {exactTermsVersion} — before matter-specific recusals",
                "atlas.session.quorum-rule-mismatch",
                issues);
            RequireExactValue(
                fields,
                DecisionProcedureField,
                $"APPLIED — {exactTermsVersion}",
                "atlas.session.procedure-rule-mismatch",
                issues);
        }

        var authority = ReadNormalizedField(fields, SeatAuthorityField);
        if (!TryParseSeatAuthorityEntries(
                authority,
                sessionDateValid ? sessionDate : null,
                out var authorityEntries))
        {
            issues.Add(new(
                "atlas.session.seat-authority-incomplete",
                "Every constituted assignment must use the exact seat/person/count/presence/educator/authority/effective-UTC/exclusive-expiry-UTC/scope/acceptance/qualification/custodian grammar, with an exact one-calendar-year term covering the whole session UTC date."));
        }
        else
        {
            ValidateSeatAuthorityReconciliation(
                authorityEntries,
                presentSeatCountsValid ? presentSeatCounts : null,
                naturalPersonsValid ? naturalPersons : null,
                totalSeatedValid ? totalSeatedPersons : null,
                practicingEducatorsValid ? practicingEducatorsPresent : null,
                ReadNormalizedField(fields, AbsentSeatsField),
                ReadNormalizedField(fields, MultiCapacityField),
                issues);
        }

        var contentLicense = ReadNormalizedField(fields, ContentLicenseField)
            .Split(" — ", StringSplitOptions.None);
        if (contentLicense.Length != 3
            || !string.Equals(contentLicense[0], "CHOSEN", StringComparison.Ordinal)
            || !IsPositiveRecordComponent(contentLicense[1])
            || !IsPositiveRecordComponent(contentLicense[2]))
        {
            issues.Add(new(
                "atlas.session.content-license-reference-invalid",
                "The content-license field must be 'CHOSEN — <exact license> — <accountable decision record>'."));
        }

        var compensation = ReadNormalizedField(fields, CompensationField)
            .Split(" — ", StringSplitOptions.None);
        if (compensation.Length != 4
            || !string.Equals(compensation[0], "RECORDED", StringComparison.Ordinal)
            || !string.Equals(compensation[1], SupportedCompensationPolicyId, StringComparison.Ordinal)
            || !DateOnly.TryParseExact(
                compensation[2],
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var compensationDate)
            || (sessionDateValid && compensationDate > sessionDate)
            || !string.Equals(compensation[3], presentSeats, StringComparison.Ordinal))
        {
            issues.Add(new(
                "atlas.session.compensation-election-invalid",
                $"The compensation record must be 'RECORDED — {SupportedCompensationPolicyId} — YYYY-MM-DD — <exact seats present>' with an effective date no later than the session."));
        }

        if (!HasStructuredCompensationAdministration(
                ReadNormalizedField(fields, CompensationAdministrationField)))
        {
            issues.Add(new(
                "atlas.session.compensation-administration-invalid",
                "Compensation administration requires an opaque private-ledger reference plus VERIFIED rate, UTC-quarter cap reservation, and district-time status."));
        }

    }

    private static bool TryParseSeatAuthorityEntries(
        string authority,
        DateOnly? sessionDate,
        out IReadOnlyList<SeatAuthorityEntry> entries)
    {
        const string prefix = "CONSTITUTED — ";
        entries = [];
        var parsedEntries = new List<SeatAuthorityEntry>();
        var assignments = new HashSet<string>(StringComparer.Ordinal);
        var personFacts = new Dictionary<string, (bool IsPresent, bool IsPracticingEducator)>(StringComparer.Ordinal);
        foreach (var entry in authority.Split(" || ", StringSplitOptions.None))
        {
            if (!entry.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            string[] requiredPrefixes =
            [
                "seat=",
                "person-ref=",
                "person-count=",
                "presence=",
                "practicing-educator=",
                "appointing-authority=",
                "effective-utc=",
                "expiry-exclusive-utc=",
                "scope=",
                "acceptance-record=",
                "qualification-basis=",
                "private-custodian=",
            ];
            var components = entry[prefix.Length..].Split("; ", StringSplitOptions.None);
            if (components.Length != requiredPrefixes.Length)
            {
                return false;
            }

            var values = new string[requiredPrefixes.Length];
            for (var index = 0; index < requiredPrefixes.Length; index++)
            {
                if (!components[index].StartsWith(requiredPrefixes[index], StringComparison.Ordinal))
                {
                    return false;
                }

                values[index] = components[index][requiredPrefixes[index].Length..].Trim();
                // Presence and educator status are closed enums validated below. In
                // particular, the valid educator value NO must not be interpreted as
                // a generic incomplete-record token.
                if (index is not 3 and not 4 && !IsPositiveRecordComponent(values[index]))
                {
                    return false;
                }
            }

            if (!string.Equals(values[2], "1", StringComparison.Ordinal)
                || values[3] is not ("PRESENT" or "ABSENT")
                || values[4] is not ("YES" or "NO")
                || !TryParseUtcInstant(values[6], out var effective)
                || !TryParseUtcInstant(values[7], out var expiryExclusive)
                || expiryExclusive != effective.AddYears(1))
            {
                return false;
            }

            if (sessionDate is not null)
            {
                var sessionDayStart = new DateTimeOffset(
                    sessionDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
                var sessionDayEnd = sessionDayStart.AddDays(1);
                if (effective > sessionDayStart || expiryExclusive < sessionDayEnd)
                {
                    return false;
                }
            }

            var isPresent = string.Equals(values[3], "PRESENT", StringComparison.Ordinal);
            var isPracticingEducator = string.Equals(values[4], "YES", StringComparison.Ordinal);
            var assignmentKey = $"{values[0]}\u001f{values[1]}";
            if (!assignments.Add(assignmentKey))
            {
                return false;
            }

            if (personFacts.TryGetValue(values[1], out var existingFacts)
                && existingFacts != (isPresent, isPracticingEducator))
            {
                return false;
            }

            personFacts[values[1]] = (isPresent, isPracticingEducator);
            parsedEntries.Add(new(
                values[0],
                values[1],
                isPresent,
                isPracticingEducator));
        }

        if (parsedEntries.Count == 0)
        {
            return false;
        }

        entries = parsedEntries.AsReadOnly();
        return true;
    }

    private static void ValidateSeatAuthorityReconciliation(
        IReadOnlyList<SeatAuthorityEntry> authorityEntries,
        Dictionary<string, int>? expectedPresentSeats,
        int? expectedPresentPersons,
        int? expectedTotalPersons,
        int? expectedPracticingEducators,
        string absentSeats,
        string multiCapacity,
        List<AtlasCouncilRecordIssue> issues)
    {
        var presentSeatCounts = AuthoritySeatCounts(authorityEntries, isPresent: true);
        if (expectedPresentSeats is not null
            && !SeatCountsEqual(presentSeatCounts, expectedPresentSeats))
        {
            issues.Add(new(
                "atlas.session.seat-authority-present-count-mismatch",
                "Constituted PRESENT authority entries must reconcile exactly to the seats-present field."));
        }

        var presentPersonCount = authorityEntries
            .Where(entry => entry.IsPresent)
            .Select(entry => entry.PersonReference)
            .Distinct(StringComparer.Ordinal)
            .Count();
        var totalPersonCount = authorityEntries
            .Select(entry => entry.PersonReference)
            .Distinct(StringComparer.Ordinal)
            .Count();
        var practicingEducatorCount = authorityEntries
            .Where(entry => entry.IsPresent && entry.IsPracticingEducator)
            .Select(entry => entry.PersonReference)
            .Distinct(StringComparer.Ordinal)
            .Count();
        if (expectedPresentPersons is not null && presentPersonCount != expectedPresentPersons)
        {
            issues.Add(new(
                "atlas.session.seat-authority-present-person-mismatch",
                "Distinct PRESENT person references must equal the natural-persons-present count."));
        }

        if (expectedTotalPersons is not null && totalPersonCount != expectedTotalPersons)
        {
            issues.Add(new(
                "atlas.session.seat-authority-total-person-mismatch",
                "Distinct constituted person references must equal the seated, non-vacant natural-person total."));
        }

        if (expectedPracticingEducators is not null
            && practicingEducatorCount != expectedPracticingEducators)
        {
            issues.Add(new(
                "atlas.session.seat-authority-educator-count-mismatch",
                "Distinct PRESENT person references marked practicing-educator=YES must equal the practicing-educator count."));
        }

        var expectedAbsentSeats = AuthoritySeatCounts(authorityEntries, isPresent: false);
        if (!TryParseAbsentSeatCounts(absentSeats, out var recordedAbsentSeats)
            || !SeatCountsEqual(expectedAbsentSeats, recordedAbsentSeats))
        {
            issues.Add(new(
                "atlas.session.seat-authority-absent-count-mismatch",
                "The exact absent-seat disposition must reconcile to all constituted ABSENT authority entries."));
        }

        var expectedMultiCapacity = ExpectedMultiCapacityDisposition(authorityEntries);
        if (!string.Equals(multiCapacity, expectedMultiCapacity, StringComparison.Ordinal))
        {
            issues.Add(new(
                "atlas.session.seat-authority-multi-capacity-mismatch",
                "Multi-capacity disclosures must exactly identify every person reference assigned to more than one seat, with canonical person and seat ordering."));
        }
    }

    private static Dictionary<string, int> AuthoritySeatCounts(
        IReadOnlyList<SeatAuthorityEntry> authorityEntries,
        bool isPresent)
        => authorityEntries
            .Where(entry => entry.IsPresent == isPresent)
            .GroupBy(entry => entry.Seat, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

    private static bool SeatCountsEqual(
        Dictionary<string, int> first,
        Dictionary<string, int> second)
        => first.Count == second.Count
            && first.All(pair => second.TryGetValue(pair.Key, out var count) && count == pair.Value);

    private static bool TryParseAbsentSeatCounts(
        string value,
        out Dictionary<string, int> seatCounts)
    {
        if (string.Equals(value, "NONE — no absent constituted seat", StringComparison.Ordinal))
        {
            seatCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            return true;
        }

        const string prefix = "ABSENT — ";
        if (value.StartsWith(prefix, StringComparison.Ordinal)
            && TryParseSeatCounts(value[prefix.Length..], out var parsed, out _))
        {
            seatCounts = parsed;
            return true;
        }

        seatCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        return false;
    }

    private static string ExpectedMultiCapacityDisposition(
        IReadOnlyList<SeatAuthorityEntry> authorityEntries)
    {
        var disclosures = authorityEntries
            .GroupBy(entry => entry.PersonReference, StringComparer.Ordinal)
            .Select(group => new
            {
                PersonReference = group.Key,
                Seats = group.Select(entry => entry.Seat)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(seat => seat, StringComparer.Ordinal)
                    .ToArray(),
            })
            .Where(group => group.Seats.Length > 1)
            .OrderBy(group => group.PersonReference, StringComparer.Ordinal)
            .Select(group =>
                $"person-ref={group.PersonReference}; seats={string.Join(" + ", group.Seats)}")
            .ToArray();
        return disclosures.Length == 0
            ? "NONE — one constituted capacity per natural person"
            : $"DISCLOSED — {string.Join(" || ", disclosures)}";
    }

    private static bool TryParseUtcInstant(string value, out DateTimeOffset instant)
        => DateTimeOffset.TryParseExact(
            value,
            "yyyy-MM-dd'T'HH:mm:ss'Z'",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out instant);

    private static bool HasStructuredControlBinding(string binding, string expectedPath)
    {
        const string prefix = "BOUND — ";
        if (!binding.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var components = binding[prefix.Length..].Split("; ", StringSplitOptions.None);
        if (components.Length != 4
            || !string.Equals(components[0], $"path={expectedPath}", StringComparison.Ordinal)
            || !components[1].StartsWith("bytes=", StringComparison.Ordinal)
            || !int.TryParse(
                components[1]["bytes=".Length..],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var byteLength)
            || byteLength <= 0
            || !components[2].StartsWith("sha256:", StringComparison.Ordinal)
            || components[2].Length != "sha256:".Length + 64
            || !components[2]["sha256:".Length..].All(char.IsAsciiHexDigit)
            || !components[3].StartsWith("commit=", StringComparison.Ordinal))
        {
            return false;
        }

        var commit = components[3]["commit=".Length..];
        return commit.Length is >= 7 and <= 64 && commit.All(char.IsAsciiHexDigit);
    }

    private static void ValidateChainAuditFields(
        Dictionary<string, string> fields,
        string issuePrefix,
        List<AtlasCouncilRecordIssue> issues,
        out DateTimeOffset? auditInstant)
    {
        var auditId = ReadNormalizedField(fields, UpstreamChainAuditIdField);
        if (!auditId.StartsWith("CHAIN-", StringComparison.Ordinal)
            || !IsPositiveRecordComponent(auditId["CHAIN-".Length..]))
        {
            issues.Add(new(
                $"{issuePrefix}.chain-audit-id-invalid",
                "The upstream chain audit must have a substantive ID beginning 'CHAIN-'."));
        }

        RequireUtcInstant(
            fields,
            UpstreamChainAuditUtcField,
            $"{issuePrefix}.chain-audit-utc-invalid",
            issues,
            out auditInstant);

        if (!HasStructuredVersionedFileBinding(
                ReadNormalizedField(fields, UpstreamChainAuditBindingField)))
        {
            issues.Add(new(
                $"{issuePrefix}.chain-audit-binding-invalid",
                "The chain-audit artifact requires an exact BOUND path, version, positive byte length, and SHA-256."));
        }

        if (!HasStructuredRepositoryRevision(ReadNormalizedField(fields, ChainAuditRevisionField)))
        {
            issues.Add(new(
                $"{issuePrefix}.chain-audit-revision-invalid",
                "The chain audit must bind a 7–64 digit hexadecimal candidate revision and explicitly record its clean or dirty-tree disposition."));
        }

        if (!HasStructuredPublicEventBindings(ReadNormalizedField(fields, PublicEventBindingsField)))
        {
            issues.Add(new(
                $"{issuePrefix}.public-event-bindings-invalid",
                "Public chain events must be NONE with a bounded basis or BOUND as exact path/version/byte-length/SHA-256 entries."));
        }

        if (!HasStructuredPrivateEventAttestations(ReadNormalizedField(fields, PrivateEventAttestationsField)))
        {
            issues.Add(new(
                $"{issuePrefix}.private-event-attestations-invalid",
                "Private chain events must be NONE with a bounded basis or ATTESTED only by substantive opaque custodian references."));
        }

        if (!HasCurrentClearChainDisposition(ReadNormalizedField(fields, CurrentUpstreamDispositionField)))
        {
            issues.Add(new(
                $"{issuePrefix}.chain-disposition-invalid",
                "A completed downstream record must state substantive current upstream dispositions and end exactly with 'unresolved-chain-holds=NONE'; any unresolved chain event remains a HOLD."));
        }
    }

    private static void ValidateVersionedDerivedFileName(
        string candidateFileName,
        string recordFileName,
        string artifactKind,
        string issueCode,
        List<AtlasCouncilRecordIssue> issues,
        out int version)
    {
        version = default;
        var stem = recordFileName.EndsWith(FileSuffix, StringComparison.Ordinal)
            ? recordFileName[..^FileSuffix.Length]
            : string.Empty;
        var prefix = $"{stem}-{artifactKind}-v";
        var versionText = candidateFileName.StartsWith(prefix, StringComparison.Ordinal)
            && candidateFileName.EndsWith(FileSuffix, StringComparison.Ordinal)
                ? candidateFileName[prefix.Length..^FileSuffix.Length]
                : string.Empty;
        if (!int.TryParse(
                versionText,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out version)
            || version <= 0
            || !string.Equals(
                versionText,
                version.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal))
        {
            version = default;
            issues.Add(new(
                issueCode,
                $"A linked Atlas {artifactKind} filename must share the exact dated H0 record stem and end '-{artifactKind}-v<positive integer>.md'."));
        }
    }

    private static void ValidateLinkedRecordVersion(
        Dictionary<string, string> fields,
        string h0RecordIdentity,
        string h0RecordFileName,
        int version,
        string artifactKind,
        string identityField,
        string predecessorField,
        string issuePrefix,
        List<AtlasCouncilRecordIssue> issues)
    {
        var h0VersionMarker = h0RecordIdentity.LastIndexOf(" v", StringComparison.Ordinal);
        var h0IdentityStem = h0VersionMarker > 0
            ? h0RecordIdentity[..h0VersionMarker]
            : h0RecordIdentity;
        var identityKind = artifactKind.Equals("feasibility", StringComparison.Ordinal)
            ? "FEASIBILITY"
            : "DISPOSITION";
        RequireBinding(
            fields,
            identityField,
            $"{h0IdentityStem}-{identityKind} v{version.ToString(CultureInfo.InvariantCulture)}",
            $"{issuePrefix}.version-identity-mismatch",
            issues);

        var predecessor = ReadNormalizedField(fields, predecessorField);
        if (version == 1)
        {
            if (!string.Equals(predecessor, $"NONE — first {artifactKind} record", StringComparison.Ordinal))
            {
                issues.Add(new(
                    $"{issuePrefix}.predecessor-invalid",
                    $"Version 1 must state exactly 'NONE — first {artifactKind} record'."));
            }

            return;
        }

        var stem = h0RecordFileName.EndsWith(FileSuffix, StringComparison.Ordinal)
            ? h0RecordFileName[..^FileSuffix.Length]
            : string.Empty;
        var predecessorVersion = version - 1;
        if (!HasStructuredVersionedFileBinding(
                predecessor,
                $"docs/council/{stem}-{artifactKind}-v{predecessorVersion.ToString(CultureInfo.InvariantCulture)}.md",
                $"v{predecessorVersion.ToString(CultureInfo.InvariantCulture)}"))
        {
            issues.Add(new(
                $"{issuePrefix}.predecessor-invalid",
                $"Version {version.ToString(CultureInfo.InvariantCulture)} must exactly bind version {predecessorVersion.ToString(CultureInfo.InvariantCulture)} by repository path, version, byte length, and SHA-256."));
        }
    }

    private static bool HasStructuredRepositoryRevision(string value)
    {
        var revision = NormalizeCell(value);
        var token = revision
            .Split([' ', '·'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? string.Empty;
        return token.Length is >= 7 and <= 64
            && token.All(char.IsAsciiHexDigit)
            && (revision.Contains("clean", StringComparison.OrdinalIgnoreCase)
                || revision.Contains("dirty", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasStructuredVersionedFileBinding(
        string value,
        string? expectedPath = null,
        string? expectedVersion = null)
    {
        const string prefix = "BOUND — ";
        if (!value.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var components = value[prefix.Length..].Split("; ", StringSplitOptions.None);
        return components.Length == 4
            && components[0].StartsWith("path=", StringComparison.Ordinal)
            && IsPositiveRecordComponent(components[0]["path=".Length..])
            && (expectedPath is null
                || string.Equals(components[0]["path=".Length..], expectedPath, StringComparison.Ordinal))
            && components[1].StartsWith("version=", StringComparison.Ordinal)
            && IsPositiveRecordComponent(components[1]["version=".Length..])
            && (expectedVersion is null
                || string.Equals(components[1]["version=".Length..], expectedVersion, StringComparison.Ordinal))
            && components[2].StartsWith("bytes=", StringComparison.Ordinal)
            && int.TryParse(
                components[2]["bytes=".Length..],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var byteLength)
            && byteLength > 0
            && components[3].StartsWith("sha256:", StringComparison.Ordinal)
            && components[3].Length == "sha256:".Length + 64
            && components[3]["sha256:".Length..].All(char.IsAsciiHexDigit);
    }

    private static bool HasStructuredPublicEventBindings(string value)
    {
        const string nonePrefix = "NONE — ";
        if (value.StartsWith(nonePrefix, StringComparison.Ordinal))
        {
            return IsSubstantive(value[nonePrefix.Length..]);
        }

        const string boundPrefix = "BOUND — ";
        return value.StartsWith(boundPrefix, StringComparison.Ordinal)
            && value[boundPrefix.Length..]
                .Split(" || ", StringSplitOptions.None)
                .All(entry => HasStructuredVersionedFileBinding($"{boundPrefix}{entry}"));
    }

    private static bool HasStructuredPrivateEventAttestations(string value)
    {
        const string nonePrefix = "NONE — ";
        if (value.StartsWith(nonePrefix, StringComparison.Ordinal))
        {
            return IsSubstantive(value[nonePrefix.Length..]);
        }

        const string attestedPrefix = "ATTESTED — ";
        return value.StartsWith(attestedPrefix, StringComparison.Ordinal)
            && value[attestedPrefix.Length..]
                .Split(" || ", StringSplitOptions.None)
                .All(entry => entry.StartsWith("custodian-ref=", StringComparison.Ordinal)
                    && IsPositiveRecordComponent(entry["custodian-ref=".Length..]));
    }

    private static bool HasCurrentClearChainDisposition(string value)
    {
        const string prefix = "CURRENT — ";
        const string suffix = "; unresolved-chain-holds=NONE";
        if (!value.StartsWith(prefix, StringComparison.Ordinal)
            || !value.EndsWith(suffix, StringComparison.Ordinal))
        {
            return false;
        }

        var disposition = value[prefix.Length..^suffix.Length];
        string[] forbiddenStates =
        [
            "ambiguous",
            "conflicting",
            "missing",
            "restricted",
            "revoked",
            "stale",
            "superseded",
            "unresolved",
            "withdrawn",
        ];
        // Current-disposition prose treats hyphens as word boundaries; opaque
        // references elsewhere retain their existing token grammar.
        var dispositionWords = disposition.Replace('-', ' ');
        return disposition.EndsWith(" effective", StringComparison.Ordinal)
            && IsPositiveRecordComponent(disposition)
            && !forbiddenStates.Any(state => ContainsStandaloneToken(dispositionWords, state));
    }

    private static bool HasResolvedCorrectionRecord(string value)
    {
        const string noneValue = "NONE — no correction requested";
        const string resolvedPrefix = "RESOLVED — ";
        const string resolvedSuffix = "; unresolved=NONE";
        return string.Equals(value, noneValue, StringComparison.Ordinal)
            || value.StartsWith(resolvedPrefix, StringComparison.Ordinal)
                && value.EndsWith(resolvedSuffix, StringComparison.Ordinal)
                && IsPositiveRecordComponent(value[resolvedPrefix.Length..^resolvedSuffix.Length]);
    }

    private static bool HasResolvedPreFreezeWithdrawals(
        string value,
        string withdrawalDisposition)
    {
        const string prefix = "HONORED — ";
        const string suffix = "; unresolved=NONE";
        if (!value.StartsWith(prefix, StringComparison.Ordinal)
            || !value.EndsWith(suffix, StringComparison.Ordinal)
            || !TryReadH0WithdrawalDispositions(
                withdrawalDisposition,
                out var activityWithdrawal,
                out var councilResignationVacancy))
        {
            return false;
        }

        var requests = value[prefix.Length..^suffix.Length];
        if (string.Equals(activityWithdrawal, "NONE", StringComparison.Ordinal)
            && string.Equals(councilResignationVacancy, "NONE", StringComparison.Ordinal))
        {
            return string.Equals(requests, "NONE RECEIVED", StringComparison.Ordinal);
        }

        return string.Equals(
            requests,
            $"activity-withdrawal={activityWithdrawal}; council-resignation-vacancy={councilResignationVacancy}",
            StringComparison.Ordinal);
    }

    private static bool HasResolvedH0WithdrawalDispositions(string value)
        => TryReadH0WithdrawalDispositions(value, out _, out _);

    private static bool TryReadH0WithdrawalDispositions(
        string value,
        out string activityWithdrawal,
        out string councilResignationVacancy)
    {
        activityWithdrawal = string.Empty;
        councilResignationVacancy = string.Empty;
        const string prefix = "RESOLVED — ";
        if (!value.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var parts = value[prefix.Length..].Split("; ", StringSplitOptions.None);
        if (parts.Length != 4
            || !HasNoneOrPositiveValue(parts[0], "activity-withdrawal=")
            || !HasNoneOrPositiveValue(parts[1], "council-resignation-vacancy=")
            || !string.Equals(
                parts[2],
                "non-member-role-closure=NOT-APPLICABLE-H0",
                StringComparison.Ordinal)
            || !string.Equals(parts[3], "unresolved=NONE", StringComparison.Ordinal))
        {
            return false;
        }

        activityWithdrawal = parts[0]["activity-withdrawal=".Length..];
        councilResignationVacancy = parts[1]["council-resignation-vacancy=".Length..];
        return true;
    }

    private static bool HasNoneOrPositiveValue(string component, string prefix)
    {
        if (!component.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var value = component[prefix.Length..];
        return string.Equals(value, "NONE", StringComparison.Ordinal)
            || IsPositiveRecordComponent(value);
    }

    private static bool HasClearProductOwnerConflictDisposition(string value)
    {
        const string nonePrefix = "NONE — ";
        return value.StartsWith(nonePrefix, StringComparison.Ordinal)
            && IsSubstantive(value[nonePrefix.Length..]);
    }

    private static bool HasHeldProductOwnerConflictDisposition(string value)
    {
        const string prefix = "HELD — ";
        if (!value.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var parts = value[prefix.Length..].Split("; ", StringSplitOptions.None);
        return parts.Length == 3
            && parts[0].StartsWith("conflict-category=", StringComparison.Ordinal)
            && IsPositiveRecordComponent(parts[0]["conflict-category=".Length..])
            && parts[1].StartsWith("written-finding=", StringComparison.Ordinal)
            && IsPositiveRecordComponent(parts[1]["written-finding=".Length..])
            && string.Equals(parts[2], "adoption=NONE", StringComparison.Ordinal);
    }

    private static bool HasStructuredCompensationAdministration(string value)
    {
        const string prefix = "ATTESTED — ";
        if (!value.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var parts = value[prefix.Length..].Split("; ", StringSplitOptions.None);
        return parts.Length == 4
            && parts[0].StartsWith("private-ledger-ref=", StringComparison.Ordinal)
            && IsPositiveRecordComponent(parts[0]["private-ledger-ref=".Length..])
            && string.Equals(parts[1], "rate=VERIFIED", StringComparison.Ordinal)
            && string.Equals(parts[2], "utc-quarter-cap-reservation=VERIFIED", StringComparison.Ordinal)
            && string.Equals(parts[3], "district-time-status=VERIFIED", StringComparison.Ordinal);
    }

    private static bool HasStructuredRosterBinding(string binding)
    {
        const string prefix = "BOUND — ";
        if (!binding.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var components = binding[prefix.Length..].Split("; ", StringSplitOptions.None);
        if (components.Length != 5
            || !components[0].StartsWith("path=", StringComparison.Ordinal)
            || !IsPositiveRecordComponent(components[0]["path=".Length..])
            || !components[1].StartsWith("version=", StringComparison.Ordinal)
            || !IsPositiveRecordComponent(components[1]["version=".Length..])
            || !components[2].StartsWith("bytes=", StringComparison.Ordinal)
            || !int.TryParse(
                components[2]["bytes=".Length..],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var byteLength)
            || byteLength <= 0
            || !components[3].StartsWith("sha256:", StringComparison.Ordinal)
            || components[3].Length != "sha256:".Length + 64
            || !components[3]["sha256:".Length..].All(char.IsAsciiHexDigit)
            || !components[4].StartsWith("commit=", StringComparison.Ordinal))
        {
            return false;
        }

        var commit = components[4]["commit=".Length..];
        return commit.Length is >= 7 and <= 64 && commit.All(char.IsAsciiHexDigit);
    }

    private static bool IsPositiveRecordComponent(string value)
    {
        var candidate = NormalizeCell(value);
        string[] negativeTokens =
        [
            "no",
            "none",
            "not",
            "missing",
            "unavailable",
            "unresolved",
            "unsupplied",
            "without",
        ];
        return IsSubstantive(candidate)
            && !ContainsNonoperativeDisposition(candidate)
            && !negativeTokens.Any(token => ContainsStandaloneToken(candidate, token));
    }

    private static bool ContainsStandaloneToken(string value, string token)
    {
        var start = 0;
        while (start < value.Length)
        {
            var index = value.IndexOf(token, start, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return false;
            }

            var end = index + token.Length;
            var leftBounded = index == 0
                || !char.IsLetterOrDigit(value[index - 1]) && value[index - 1] != '-';
            var rightBounded = end == value.Length
                || !char.IsLetterOrDigit(value[end]) && value[end] != '-';
            if (leftBounded && rightBounded)
            {
                return true;
            }

            start = index + 1;
        }

        return false;
    }

    private static bool IsDottedNumericVersion(string value)
    {
        var segments = value.Split('.', StringSplitOptions.None);
        return segments.Length > 0
            && segments.All(segment =>
                segment.Length > 0 && segment.All(char.IsAsciiDigit));
    }

    private static bool TryReadSessionDate(string value, out DateOnly date)
    {
        date = default;
        var parts = NormalizeCell(value).Split(" · ", StringSplitOptions.None);
        return parts.Length == 2
            && DateOnly.TryParseExact(
                parts[0],
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out date)
            && parts[1].EndsWith(" minutes", StringComparison.Ordinal)
            && int.TryParse(
                parts[1][..^" minutes".Length],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var minutes)
            && minutes is > 0 and <= 1440;
    }

    private static bool TryParseSeatCounts(
        string value,
        out Dictionary<string, int> seatCounts,
        out int capacityCount)
    {
        seatCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        capacityCount = 0;
        var entries = value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (entries.Length == 0)
        {
            return false;
        }

        foreach (var entry in entries)
        {
            var separator = entry.LastIndexOf(':');
            if (separator <= 0
                || !IsSubstantive(entry[..separator])
                || !int.TryParse(
                    entry[(separator + 1)..].Trim(),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var count)
                || count <= 0)
            {
                return false;
            }

            var seat = entry[..separator].Trim();
            if (!seatCounts.TryAdd(seat, count))
            {
                return false;
            }

            if (capacityCount > int.MaxValue - count)
            {
                return false;
            }

            capacityCount += count;
        }

        return true;
    }

    private static bool TryReadPositiveCount(
        Dictionary<string, string> fields,
        string field,
        out int count)
        => int.TryParse(
                ReadNormalizedField(fields, field),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out count)
            && count > 0;

    private static void RequireSha256BearingField(
        Dictionary<string, string> fields,
        string field,
        string issueCode,
        List<AtlasCouncilRecordIssue> issues)
    {
        var value = ReadNormalizedField(fields, field);
        var marker = value.IndexOf("sha256:", StringComparison.OrdinalIgnoreCase);
        var digestStart = marker + "sha256:".Length;
        var hasDigest = marker >= 0
            && value.Length >= digestStart + 64
            && value.AsSpan(digestStart, 64).ToArray().All(char.IsAsciiHexDigit)
            && (value.Length == digestStart + 64 || !char.IsAsciiHexDigit(value[digestStart + 64]));
        if (!hasDigest)
        {
            issues.Add(new(
                issueCode,
                $"The '{field}' field must contain at least one exact 'sha256:<64 hexadecimal digits>' binding."));
        }
    }

    private static void RequireDispositionCoverage(
        Dictionary<string, string> fields,
        string field,
        string prefix,
        string expectedCoverage,
        string issueCode,
        List<AtlasCouncilRecordIssue> issues)
    {
        var actual = ReadNormalizedField(fields, field);
        if (!actual.StartsWith(prefix, StringComparison.Ordinal)
            || !string.Equals(actual[prefix.Length..], expectedCoverage, StringComparison.Ordinal))
        {
            issues.Add(new(
                issueCode,
                $"The '{field}' disposition must cover the exact recorded seats present."));
        }
    }

    private static void RequireExactValue(
        Dictionary<string, string> fields,
        string field,
        string expected,
        string issueCode,
        List<AtlasCouncilRecordIssue> issues)
    {
        if (!string.Equals(ReadNormalizedField(fields, field), expected, StringComparison.Ordinal))
        {
            issues.Add(new(issueCode, $"The '{field}' value must exactly bind the enacted terms version."));
        }
    }

    private static void ValidateProtectedSeatContinuity(
        Dictionary<string, string> fields,
        string[] lines,
        int[] orderedHeadingIndexes,
        RecommendationSupplementalValidation supplemental,
        RecusalDisputeValidation recusalDisputes,
        List<AtlasCouncilRecordIssue> issues)
    {
        var triggerText = new StringBuilder()
            .Append(ReadNormalizedField(fields, AbsentSeatsField)).Append(' ');
        var sessionConflict = ReadNormalizedField(fields, ConflictRecusalField);
        if (sessionConflict.StartsWith("RECUSALS — ", StringComparison.Ordinal))
        {
            triggerText.Append(sessionConflict).Append(' ');
        }

        var sessionHolds = ReadNormalizedField(fields, ProtectedSeatHoldsField);
        triggerText.Append(sessionHolds);
        foreach (var category in recusalDisputes.ProtectedTriggerCategories)
        {
            triggerText.Append(' ').Append(category);
        }
        var mappingRows = ReadTableRows(
                lines,
                orderedHeadingIndexes[2],
                orderedHeadingIndexes[3])
            .Where(row => row.Count == MappingHeader.Length)
            .Where(row => !RowsEqual(row, MappingHeader) && !IsSeparatorRow(row))
            .Where(row => row.All(IsSubstantive))
            .ToArray();
        foreach (var row in mappingRows)
        {
            triggerText.Append(' ').Append(row[4]);
        }

        var recommendationRows = ReadTableRows(
                lines,
                orderedHeadingIndexes[3],
                orderedHeadingIndexes[4])
            .Where(row => row.Count == RecommendationHeader.Length)
            .Where(row => !RowsEqual(row, RecommendationHeader) && !IsSeparatorRow(row))
            .Where(row => row.Any(IsSubstantive))
            .ToArray();
        var recommendationConflictIndex = Array.IndexOf(RecommendationHeader, RecommendationConflictColumn);
        foreach (var row in recommendationRows)
        {
            var conflict = row[recommendationConflictIndex];
            if (conflict.StartsWith("RECUSALS — ", StringComparison.Ordinal))
            {
                triggerText.Append(' ').Append(conflict);
            }
        }

        var triggers = triggerText.ToString();
        var requiredHolds = ProtectedSeatAliases
            .Where(seat => seat.Aliases.Any(alias => ContainsBoundedAlias(triggers, alias)))
            .Select(seat => seat.Canonical)
            .ToArray();
        if (requiredHolds.Length == 0)
        {
            return;
        }

        var finalHolds = supplemental.Values.TryGetValue(FinalHoldsField, out var finalValue)
            ? NormalizeCell(finalValue)
            : string.Empty;
        if (!sessionHolds.StartsWith("HELD — ", StringComparison.Ordinal)
            || requiredHolds.Any(term => !AffirmativelyCarriesHold(sessionHolds, term))
            || requiredHolds.Any(term => !AffirmativelyCarriesHold(finalHolds, term)))
        {
            issues.Add(new(
                "atlas.session.protected-seat-continuity",
                "Every absent, recused, or implicated protected seat must remain explicit in the session and final read-back holds."));
        }

        var recommendationHoldsIndex = Array.IndexOf(RecommendationHeader, RecommendationHoldsColumn);
        if (recommendationRows.Any(row => requiredHolds.Any(term =>
                !AffirmativelyCarriesHold(row[recommendationHoldsIndex], term))))
        {
            issues.Add(new(
                "atlas.recommendation.protected-seat-hold-missing",
                "Each recommendation must retain every applicable protected-seat hold."));
        }
    }

    private static void ValidateDispositionHoldContinuity(
        string[] lines,
        int startHeading,
        int endHeading,
        string upstreamHolds,
        List<AtlasCouncilRecordIssue> issues)
    {
        var requiredHolds = ProtectedSeatAliases
            .Select(seat => seat.Canonical)
            .Where(term => upstreamHolds.Contains(term, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (!upstreamHolds.StartsWith("HELD — ", StringComparison.Ordinal)
            || requiredHolds.Length == 0)
        {
            return;
        }

        var rows = ReadTableRows(lines, startHeading, endHeading)
            .Where(row => row.Count == DispositionHeader.Length)
            .Where(row => !RowsEqual(row, DispositionHeader) && !IsSeparatorRow(row))
            .Where(row => row.Any(IsSubstantive));
        if (rows.Any(row => requiredHolds.Any(term =>
                !AffirmativelyCarriesHold(row[4], term))))
        {
            issues.Add(new(
                "atlas.disposition.protected-seat-hold-missing",
                "A product-owner disposition must carry every upstream protected-seat hold as outstanding; only a separately bound accountable-seat record may clear it."));
        }
    }

    private static bool ContainsBoundedAlias(string value, string alias)
    {
        var comparison = string.Equals(alias, "AT", StringComparison.Ordinal)
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        var start = 0;
        while (start < value.Length)
        {
            var index = value.IndexOf(alias, start, comparison);
            if (index < 0)
            {
                return false;
            }

            var end = index + alias.Length;
            var leftBounded = index == 0 || !char.IsLetterOrDigit(value[index - 1]);
            var rightBounded = end == value.Length || !char.IsLetterOrDigit(value[end]);
            if (leftBounded && rightBounded)
            {
                return true;
            }

            start = index + 1;
        }

        return false;
    }

    private static bool AffirmativelyCarriesHold(string value, string canonicalSeat)
        => value.Contains(canonicalSeat, StringComparison.OrdinalIgnoreCase)
            && (value.StartsWith("HELD — ", StringComparison.Ordinal)
                || value.Contains("NOT REVIEWED — HELD", StringComparison.Ordinal)
                || value.Contains("hold retained", StringComparison.OrdinalIgnoreCase)
                || value.Contains("remains outstanding", StringComparison.OrdinalIgnoreCase)
                || value.Contains("still needed", StringComparison.OrdinalIgnoreCase));

    private static void RequirePrefixedDisposition(
        Dictionary<string, string> fields,
        string field,
        string requiredPrefix,
        string issueCode,
        List<AtlasCouncilRecordIssue> issues)
    {
        if (fields.TryGetValue(field, out var value))
        {
            var normalized = NormalizeCell(value);
            var suffix = normalized.StartsWith(requiredPrefix, StringComparison.Ordinal)
                ? normalized[requiredPrefix.Length..]
                : string.Empty;
            if (!IsPositiveRecordComponent(suffix))
            {
                issues.Add(new(
                    issueCode,
                    $"The '{field}' field requires the exact leading disposition '{requiredPrefix.TrimEnd()}' followed by an operative, non-pending record reference or basis."));
            }
        }
    }

    private static bool ContainsNonoperativeDisposition(string value)
    {
        string[] refusedTokens =
        [
            "TBD", "TODO", "PENDING", "UNKNOWN", "DRAFT", "NOT ENACTED",
            "NOT MET", "UNRANKED", "NOT ACCEPTED", "NOT CONSTITUTED",
            "NOT APPLIED", "UNCHOSEN", "DECLINED", "REFUSED", "NOT RECORDED",
        ];
        return refusedTokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static void RequireOneOfPrefixedDispositions(
        Dictionary<string, string> fields,
        string field,
        string[] allowedPrefixes,
        string issueCode,
        List<AtlasCouncilRecordIssue> issues)
    {
        if (fields.TryGetValue(field, out var value))
        {
            var normalized = NormalizeCell(value);
            var matchedPrefix = allowedPrefixes.FirstOrDefault(prefix =>
                normalized.StartsWith(prefix, StringComparison.Ordinal));
            var suffix = matchedPrefix is null
                ? string.Empty
                : normalized[matchedPrefix.Length..];
            if (!IsSubstantive(suffix) || ContainsNonoperativeDisposition(suffix))
            {
                issues.Add(new(
                    issueCode,
                    $"The '{field}' field requires one of its exact enacted disposition prefixes followed by a substantive, non-pending basis."));
            }
        }
    }

    private static void RequireCompleteSessionRecord(
        NeedCardValidation needCards,
        NeedMappingValidation needMappings,
        RecommendationValidation recommendations,
        RecommendationSupplementalValidation supplemental,
        RecusalDisputeValidation recusalDisputes,
        List<AtlasCouncilRecordIssue> issues)
    {
        if (needCards.CompletedNeedIds.Count == 0)
        {
            issues.Add(new(
                "atlas.session.need-card-missing",
                "A SESSION RECORD requires at least one complete de-identified need card."));
        }

        if (needCards.CompletedNeedIds.Any(needId => !needMappings.MappedNeedIds.Contains(needId)))
        {
            issues.Add(new(
                "atlas.mapping.coverage-incomplete",
                "A SESSION RECORD requires at least one complete mapping for every completed need-card ID."));
        }

        foreach (var field in RecommendationSupplementalFields)
        {
            if (supplemental.Values.TryGetValue(field, out var value) && !IsSubstantive(value))
            {
                issues.Add(new(
                    "atlas.recommendation.supplemental-field-pending",
                    "Every canonical outcome, read-back, tally, holds, correction, limitation, and no-advance field must be substantive before a SESSION RECORD can be frozen."));
                break;
            }
        }

        if (!supplemental.Values.TryGetValue(CouncilOutcomeField, out var outcome)
            || !IsSubstantive(outcome))
        {
            return;
        }

        var normalizedOutcome = NormalizeCell(outcome);
        var expectedOutcome = recommendations.MappedPossibilities.Count > 0
            ? "RECOMMENDATION RECORDED"
            : recusalDisputes.HeldMatterIds.Count > 0
                ? "HOLD"
                : null;
        var outcomeMatches = expectedOutcome is not null
            ? string.Equals(normalizedOutcome, expectedOutcome, StringComparison.Ordinal)
            : string.Equals(normalizedOutcome, "NO RECOMMENDATION", StringComparison.Ordinal)
                || string.Equals(normalizedOutcome, "HOLD", StringComparison.Ordinal);
        if (!outcomeMatches)
        {
            issues.Add(new(
                "atlas.recommendation.outcome-mismatch",
                "The exact council-outcome field must agree mechanically with complete recommendation rows and any held disputed-recusal matter."));
        }
    }

    private static NeedCardValidation ValidateNeedCards(
        string[] lines,
        int startHeading,
        int endHeading,
        List<AtlasCouncilRecordIssue> issues)
    {
        var rows = ReadTableRows(lines, startHeading, endHeading);
        var completedNeedIds = new HashSet<string>(StringComparer.Ordinal);
        var hasContent = false;
        var tableCount = 0;
        var index = 0;

        while (index < rows.Count)
        {
            var header = rows[index];
            if (!RowsEqual(header, NeedCardHeader))
            {
                issues.Add(new(
                    "atlas.need-card.header",
                    "Each need card must begin with the canonical Prompt / participant-reviewed de-identified factual paraphrase header."));
                break;
            }

            tableCount++;
            index++;
            if (index >= rows.Count
                || !IsCanonicalSeparatorRow(rows[index], NeedCardHeader.Length))
            {
                issues.Add(new(
                    "atlas.need-card.separator",
                    "Each need card must retain its canonical two-column separator row."));
            }
            else
            {
                index++;
            }

            var values = new string[RequiredNeedCardPrompts.Length];
            Array.Fill(values, string.Empty);
            var structurallyComplete = true;
            for (var fieldIndex = 0; fieldIndex < RequiredNeedCardPrompts.Length; fieldIndex++)
            {
                if (index >= rows.Count || RowsEqual(rows[index], NeedCardHeader))
                {
                    issues.Add(new(
                        "atlas.need-card.field-order",
                        "Each need card must retain every canonical prompt exactly once and in order."));
                    structurallyComplete = false;
                    break;
                }

                var row = rows[index++];
                if (row.Count != NeedCardHeader.Length)
                {
                    issues.Add(new(
                        "atlas.table.need-card-width",
                        "Every need-card row must retain the canonical two-column shape."));
                    structurallyComplete = false;
                    continue;
                }

                if (!string.Equals(row[0], RequiredNeedCardPrompts[fieldIndex], StringComparison.Ordinal))
                {
                    issues.Add(new(
                        "atlas.need-card.field-order",
                        "Each need card must retain every canonical prompt exactly once and in order."));
                    structurallyComplete = false;
                }

                values[fieldIndex] = row[1];
            }

            var substantiveFields = values
                .Select((value, fieldIndex) => fieldIndex == 0
                    ? IsSubstantiveNeedId(value)
                    : IsSubstantive(value))
                .ToArray();
            var hasSubstantiveContent = substantiveFields.Any(value => value);
            hasContent |= hasSubstantiveContent;
            var contentComplete = substantiveFields.All(value => value);
            if (hasSubstantiveContent && !contentComplete)
            {
                issues.Add(new(
                    "atlas.need-card.incomplete",
                    "A started need card must record every canonical prompt before mapping begins."));
            }

            if (structurallyComplete && contentComplete)
            {
                var needId = NormalizeCell(values[0]);
                if (!completedNeedIds.Add(needId))
                {
                    issues.Add(new(
                        "atlas.need-card.id-duplicate",
                        "Completed need cards must use distinct exact need IDs."));
                }
            }
        }

        if (tableCount == 0)
        {
            issues.Add(new(
                "atlas.need-card.header",
                "The need-card section must retain its canonical Prompt / participant-reviewed de-identified factual paraphrase table."));
        }

        return new NeedCardValidation(hasContent, completedNeedIds);
    }

    private static NeedMappingValidation ValidateNeedMappings(
        string[] lines,
        int startHeading,
        int endHeading,
        IReadOnlySet<string> completedNeedIds,
        List<AtlasCouncilRecordIssue> issues)
    {
        var rows = ReadTableRows(lines, startHeading, endHeading);
        var mappedNeedIds = new HashSet<string>(StringComparer.Ordinal);
        var completedMappings = new HashSet<NeedPossibilityKey>();
        var hasContent = rows.Any(row =>
            !RowsEqual(row, MappingHeader)
            && !IsSeparatorRow(row)
            && row.Any(IsSubstantive));
        var hasCanonicalScaffold = ValidateCanonicalTableScaffold(
            rows,
            MappingHeader,
            "need-to-possibility mapping",
            "atlas.mapping.header",
            "atlas.mapping.header-duplicate",
            "atlas.mapping.separator",
            issues);
        if (!hasCanonicalScaffold)
        {
            return new NeedMappingValidation(hasContent, mappedNeedIds, completedMappings);
        }

        var index = 2;
        var missingNeedReported = false;
        var incompleteRowReported = false;
        var invalidWidthReported = false;
        var duplicateIdentityReported = false;
        for (; index < rows.Count; index++)
        {
            var row = rows[index];
            if (IsSeparatorRow(row))
            {
                continue;
            }

            var hasRowContent = row.Any(IsSubstantive);
            hasContent |= hasRowContent;
            if (row.Count != MappingHeader.Length)
            {
                if (!invalidWidthReported)
                {
                    issues.Add(new(
                        "atlas.table.mapping-width",
                        "Every need-to-possibility mapping row must retain the canonical five-column shape."));
                    invalidWidthReported = true;
                }

                continue;
            }

            if (!hasRowContent)
            {
                continue;
            }

            var rowComplete = row.All(IsSubstantive);
            if (!incompleteRowReported && !rowComplete)
            {
                issues.Add(new(
                    "atlas.mapping.row-incomplete",
                    "Every started need-to-possibility mapping row must explicitly complete all five canonical cells."));
                incompleteRowReported = true;
            }

            var needId = NormalizeCell(row[0]);
            if (rowComplete
                && !string.Equals(row[3], "G", StringComparison.Ordinal)
                && !string.Equals(row[3], "A", StringComparison.Ordinal)
                && !string.Equals(row[3], "R", StringComparison.Ordinal)
                && !string.Equals(row[3], "uncertain", StringComparison.Ordinal))
            {
                issues.Add(new(
                    "atlas.mapping.lane-invalid",
                    "Every completed mapping row must use exactly G, A, R, or uncertain in the likely-lane column."));
            }

            var hasCompletedNeed = IsSubstantiveNeedId(row[0])
                && completedNeedIds.Contains(needId);
            if (!missingNeedReported && !hasCompletedNeed)
            {
                issues.Add(new(
                    "atlas.lifecycle.mapping-before-need",
                    "Every substantive mapping row must cite an exact, preceding, completed need-card ID."));
                missingNeedReported = true;
            }

            if (rowComplete && hasCompletedNeed)
            {
                mappedNeedIds.Add(needId);
                if (!completedMappings.Add(new(
                    needId,
                    NormalizeCell(row[1])))
                    && !duplicateIdentityReported)
                {
                    issues.Add(new(
                        "atlas.mapping.identity-duplicate",
                        "Each exact need-to-possibility identity must appear in only one completed mapping row."));
                    duplicateIdentityReported = true;
                }
            }
        }

        return new NeedMappingValidation(hasContent, mappedNeedIds, completedMappings);
    }

    private static bool RowsEqual(IReadOnlyList<string> actual, string[] expected)
        => actual.Count == expected.Length
            && actual.Select((value, index) => string.Equals(value, expected[index], StringComparison.Ordinal)).All(equal => equal);

    private static bool IsSubstantiveNeedId(string value)
    {
        var candidate = NormalizeCell(value);
        return IsSubstantive(value)
            && candidate.Length >= 5
            && candidate.StartsWith("N-", StringComparison.Ordinal)
            && candidate[2] != '-'
            && candidate[^1] != '-'
            && candidate[2..].All(character =>
                char.IsAsciiLetterUpper(character)
                || char.IsAsciiDigit(character)
                || character == '-');
    }

    private static string NormalizeCell(string value)
        => value.Trim().Trim('`').Trim();

    private static RecommendationValidation ValidateRecommendationHolds(
        string[] lines,
        int startHeading,
        int endHeading,
        NeedMappingValidation needMappings,
        Dictionary<string, string> sessionFields,
        List<AtlasCouncilRecordIssue> issues)
    {
        var rows = ReadTableRows(lines, startHeading, endHeading);
        var mappedPossibilities = new HashSet<string>(StringComparer.Ordinal);
        var recommendedNeedIds = new HashSet<string>(StringComparer.Ordinal);
        var recommendationMatters = new List<RecommendationMatter>();
        var hasContent = rows.Any(row =>
            !RowsEqual(row, RecommendationHeader)
            && !IsSeparatorRow(row)
            && row.Any(IsSubstantive));
        var hasCanonicalScaffold = ValidateCanonicalTableScaffold(
            rows,
            RecommendationHeader,
            "council recommendation",
            "atlas.recommendation.header",
            "atlas.recommendation.header-duplicate",
            "atlas.recommendation.separator",
            issues);
        var firstRow = rows.FirstOrDefault();
        var holdsIndex = firstRow is null
            ? -1
            : IndexOf(firstRow, RecommendationHoldsColumn);
        if (holdsIndex < 0)
        {
            issues.Add(new(
                "atlas.holds.recommendation-column-missing",
                "The council recommendation table must retain its explicit holds / seats still needed column."));
        }

        var recommendationIdentityIndex = firstRow is null
            ? -1
            : IndexOf(firstRow, RecommendationIdentityColumn);
        if (recommendationIdentityIndex < 0)
        {
            issues.Add(new(
                "atlas.recommendation.identity-column-missing",
                "The council recommendation table must retain its need ID and mapped possibility column."));
        }

        var eligibleTotalIndex = firstRow is null ? -1 : IndexOf(firstRow, RecommendationEligibleTotalColumn);
        var eligiblePresentIndex = firstRow is null ? -1 : IndexOf(firstRow, RecommendationEligiblePresentColumn);
        var educatorsPresentIndex = firstRow is null ? -1 : IndexOf(firstRow, RecommendationEducatorsPresentColumn);
        var conflictIndex = firstRow is null ? -1 : IndexOf(firstRow, RecommendationConflictColumn);
        var quorumIndex = firstRow is null ? -1 : IndexOf(firstRow, RecommendationQuorumColumn);
        var tallyIndex = firstRow is null ? -1 : IndexOf(firstRow, RecommendationTallyColumn);

        if (firstRow is null || firstRow.Count != RecommendationHeader.Length)
        {
            issues.Add(new(
                "atlas.table.recommendation-width",
                "The council recommendation table must retain its canonical twelve-column shape."));
        }

        if (!hasCanonicalScaffold
            || holdsIndex < 0
            || recommendationIdentityIndex < 0
            || eligibleTotalIndex < 0
            || eligiblePresentIndex < 0
            || educatorsPresentIndex < 0
            || conflictIndex < 0
            || quorumIndex < 0
            || tallyIndex < 0)
        {
            return new RecommendationValidation(
                hasContent,
                mappedPossibilities,
                recommendedNeedIds,
                recommendationMatters);
        }

        var missingHoldValueReported = false;
        var missingRecommendationIdentityReported = false;
        var malformedRecommendationIdentityReported = false;
        var unmappedNeedReported = false;
        var unmappedPossibilityReported = false;
        var incompleteRowReported = false;
        var duplicatePossibilityReported = false;
        var invalidWidthReported = false;
        var seenPossibilities = new HashSet<string>(StringComparer.Ordinal);
        for (var rowIndex = 2; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            if (RowsEqual(row, RecommendationHeader)
                || IsCanonicalSeparatorRow(row, RecommendationHeader.Length))
            {
                continue;
            }

            if (row.Count != RecommendationHeader.Length)
            {
                if (!invalidWidthReported)
                {
                    issues.Add(new(
                        "atlas.table.recommendation-width",
                        "Every council recommendation row must retain the canonical twelve-column shape."));
                    invalidWidthReported = true;
                }

                continue;
            }

            if (IsSeparatorRow(row) || !row.Any(IsSubstantive))
            {
                continue;
            }

            var rowComplete = row
                .Skip(1)
                .All(IsSubstantive);
            if (!rowComplete && !incompleteRowReported)
            {
                issues.Add(new(
                    "atlas.recommendation.row-incomplete",
                    "Every started council recommendation row must complete the mapped identity, reason, first proof, matter-bound person counts, conflicts/recusals, quorum, tally denominator, holds, and dissent cells; order remains optional."));
                incompleteRowReported = true;
            }

            if (rowComplete)
            {
                ValidateRecommendationMatterMechanics(
                    row,
                    eligibleTotalIndex,
                    eligiblePresentIndex,
                    educatorsPresentIndex,
                    conflictIndex,
                    quorumIndex,
                    tallyIndex,
                    sessionFields,
                    issues);
            }

            var exactMapping = false;
            var recommendationNeedId = string.Empty;
            var mappedPossibility = string.Empty;
            if (!IsSubstantive(row[recommendationIdentityIndex]))
            {
                if (!missingRecommendationIdentityReported)
                {
                    issues.Add(new(
                        "atlas.recommendation.identity-value-missing",
                        "Every substantive council recommendation row must identify its need ID and mapped possibility."));
                    missingRecommendationIdentityReported = true;
                }
            }
            else if (!TryReadRecommendationIdentity(
                    row[recommendationIdentityIndex],
                    out recommendationNeedId,
                    out mappedPossibility))
            {
                if (!malformedRecommendationIdentityReported)
                {
                    issues.Add(new(
                        "atlas.recommendation.identity-format",
                        "Every council recommendation identity must contain a need ID and mapped possibility."));
                    malformedRecommendationIdentityReported = true;
                }
            }
            else if (!needMappings.MappedNeedIds.Contains(recommendationNeedId))
            {
                if (!unmappedNeedReported)
                {
                    issues.Add(new(
                        "atlas.recommendation.need-unmapped",
                        "Every council recommendation need ID must exactly match a preceding completed mapping row."));
                    unmappedNeedReported = true;
                }
            }
            else if (!needMappings.CompletedMappings.Contains(new(
                recommendationNeedId,
                mappedPossibility)))
            {
                if (!unmappedPossibilityReported)
                {
                    issues.Add(new(
                        "atlas.recommendation.possibility-unmapped",
                        "Every council recommendation possibility must exactly match the possibility recorded for its preceding completed need mapping."));
                    unmappedPossibilityReported = true;
                }
            }
            else
            {
                exactMapping = true;
            }

            if (!missingHoldValueReported
                && (row.Count <= holdsIndex || !IsSubstantive(row[holdsIndex])))
            {
                issues.Add(new(
                    "atlas.holds.recommendation-value-missing",
                    "Every recorded recommendation row must explicitly retain or clear its holds / seats still needed value."));
                missingHoldValueReported = true;
            }

            if (exactMapping && !seenPossibilities.Add(mappedPossibility))
            {
                if (!duplicatePossibilityReported)
                {
                    issues.Add(new(
                        "atlas.recommendation.possibility-duplicate",
                        "Each council-recommended possibility must appear in exactly one complete recommendation row so later records can link one-to-one."));
                    duplicatePossibilityReported = true;
                }

                continue;
            }

            if (exactMapping && rowComplete)
            {
                recommendedNeedIds.Add(recommendationNeedId);
                mappedPossibilities.Add(mappedPossibility);
                recommendationMatters.Add(new(
                    recommendationNeedId,
                    NormalizeCell(row[eligibleTotalIndex]),
                    NormalizeCell(row[eligiblePresentIndex]),
                    NormalizeCell(row[educatorsPresentIndex]),
                    NormalizeCell(row[conflictIndex])));
            }
        }

        return new RecommendationValidation(
            hasContent,
            mappedPossibilities,
            recommendedNeedIds,
            recommendationMatters);
    }

    private static void ValidateRecommendationMatterMechanics(
        IReadOnlyList<string> row,
        int eligibleTotalIndex,
        int eligiblePresentIndex,
        int educatorsPresentIndex,
        int conflictIndex,
        int quorumIndex,
        int tallyIndex,
        Dictionary<string, string> sessionFields,
        List<AtlasCouncilRecordIssue> issues)
    {
        var eligibleTotalValid = TryParseCount(row[eligibleTotalIndex], allowZero: false, out var eligibleTotal);
        var eligiblePresentValid = TryParseCount(row[eligiblePresentIndex], allowZero: false, out var eligiblePresent);
        var educatorsPresentValid = TryParseCount(row[educatorsPresentIndex], allowZero: true, out var educatorsPresent);
        if (!eligibleTotalValid || !eligiblePresentValid || !educatorsPresentValid)
        {
            issues.Add(new(
                "atlas.recommendation.person-count-invalid",
                "Each recommendation matter requires positive eligible-total and eligible-present natural-person counts and a non-negative practicing-educator count."));
        }

        var totalSeatedValid = TryReadPositiveCount(sessionFields, TotalSeatedPersonsField, out var totalSeated);
        var sessionPresentValid = TryReadPositiveCount(sessionFields, NaturalPersonsPresentField, out var sessionPresent);
        var sessionEducatorsValid = TryReadPositiveCount(
            sessionFields,
            PracticingEducatorsPresentField,
            out var sessionEducators);
        if (eligibleTotalValid
            && eligiblePresentValid
            && educatorsPresentValid
            && (!totalSeatedValid || eligibleTotal > totalSeated
                || eligiblePresent > eligibleTotal
                || !sessionPresentValid
                || eligiblePresent > sessionPresent
                || educatorsPresent > eligiblePresent
                || !sessionEducatorsValid
                || educatorsPresent > sessionEducators))
        {
            issues.Add(new(
                "atlas.recommendation.person-count-inconsistent",
                "Matter counts must fit the bound roster and session: eligible present is no greater than eligible total or session present, and practicing educators are no greater than eligible or session educator counts."));
        }

        var conflict = NormalizeCell(row[conflictIndex]);
        var noRecusals = HasAllowedDisposition(conflict, ["NONE — "]);
        var recusalsValid = TryReadMatterRecusals(
            conflict,
            out var recusedTotal,
            out var recusedPresent,
            out var recusedEducators);
        if (!noRecusals && !recusalsValid)
        {
            issues.Add(new(
                "atlas.recommendation.conflict-recusals-invalid",
                "Each recommendation matter requires 'NONE — <basis>' or exact RECUSALS total-persons, present-persons, practicing-educators, and a substantive de-identified basis."));
        }
        else if (eligibleTotalValid
            && eligiblePresentValid
            && educatorsPresentValid
            && totalSeatedValid
            && sessionPresentValid
            && sessionEducatorsValid
            && (noRecusals
                ? eligibleTotal != totalSeated
                    || eligiblePresent != sessionPresent
                    || educatorsPresent != sessionEducators
                : recusedTotal > totalSeated
                    || recusedPresent > sessionPresent
                    || recusedEducators > sessionEducators
                    || recusedPresent > recusedTotal
                    || recusedEducators > recusedPresent
                    || eligibleTotal != totalSeated - recusedTotal
                    || eligiblePresent != sessionPresent - recusedPresent
                    || educatorsPresent != sessionEducators - recusedEducators))
        {
            issues.Add(new(
                "atlas.recommendation.recusal-reconciliation-invalid",
                "NONE must preserve all session counts; RECUSALS counts must reconcile exactly from the bound roster/session counts to the matter counts."));
        }

        var terms = ReadNormalizedField(sessionFields, OperatingTermsField)
            .Split(" — ", StringSplitOptions.None);
        var expectedQuorum = terms.Length == 3
            ? $"MET — {terms[1]} — after recusals"
            : string.Empty;
        if (!string.Equals(NormalizeCell(row[quorumIndex]), expectedQuorum, StringComparison.Ordinal))
        {
            issues.Add(new(
                "atlas.recommendation.quorum-invalid",
                "Each recommendation matter must record MET under the exact enacted terms version after its own recusals."));
        }

        if (!eligiblePresentValid || !TryValidateMatterTally(row[tallyIndex], eligiblePresent))
        {
            issues.Add(new(
                "atlas.recommendation.tally-invalid",
                "Each recommendation matter requires CONSENSUS or a complete VOTE whose non-negative parts sum to the exact eligible-present denominator."));
        }


        if (eligibleTotalValid
            && eligiblePresentValid
            && educatorsPresentValid
            && (eligiblePresent <= eligibleTotal / 2
                || eligiblePresent < 3
                || educatorsPresent < 2))
        {
            issues.Add(new(
                "atlas.recommendation.quorum-arithmetic-invalid",
                "Matter-specific MET requires a majority of eligible natural persons after recusals, at least three eligible natural persons present, and at least two practicing educators present."));
        }
    }

    private static bool HasAllowedDisposition(string value, string[] prefixes)
    {
        var prefix = prefixes.FirstOrDefault(candidate => value.StartsWith(candidate, StringComparison.Ordinal));
        return prefix is not null
            && IsSubstantive(value[prefix.Length..])
            && !ContainsNonoperativeDisposition(value[prefix.Length..]);
    }

    private static bool TryReadMatterRecusals(
        string value,
        out int totalPersons,
        out int presentPersons,
        out int practicingEducators)
    {
        totalPersons = default;
        presentPersons = default;
        practicingEducators = default;
        const string prefix = "RECUSALS — ";
        if (!value.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var parts = value[prefix.Length..].Split("; ", StringSplitOptions.None);
        return parts.Length == 4
            && TryReadNamedCount(parts[0], "total-persons=", out totalPersons)
            && totalPersons > 0
            && TryReadNamedCount(parts[1], "present-persons=", out presentPersons)
            && TryReadNamedCount(parts[2], "practicing-educators=", out practicingEducators)
            && parts[3].StartsWith("basis=", StringComparison.Ordinal)
            && IsPositiveRecordComponent(parts[3]["basis=".Length..]);
    }

    private static bool TryValidateMatterTally(string value, int eligiblePresent)
    {
        var normalized = NormalizeCell(value);
        const string consensusPrefix = "CONSENSUS — denominator=";
        if (normalized.StartsWith(consensusPrefix, StringComparison.Ordinal))
        {
            return TryParseCount(normalized[consensusPrefix.Length..], allowZero: false, out var denominator)
                && denominator == eligiblePresent;
        }

        const string votePrefix = "VOTE — ";
        if (!normalized.StartsWith(votePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var parts = normalized[votePrefix.Length..].Split("; ", StringSplitOptions.None);
        if (parts.Length != 4
            || !TryReadNamedCount(parts[0], "for=", out var forCount)
            || !TryReadNamedCount(parts[1], "against=", out var againstCount)
            || !TryReadNamedCount(parts[2], "abstain=", out var abstainCount)
            || !TryReadNamedCount(parts[3], "denominator=", out var denominatorCount))
        {
            return false;
        }

        return denominatorCount == eligiblePresent
            && (long)forCount + againstCount + abstainCount == denominatorCount
            && forCount > denominatorCount / 2;
    }

    private static RecusalDisputeValidation ValidateRecusalDisputeRecords(
        string value,
        IReadOnlySet<string> mappedNeedIds,
        IReadOnlyList<RecommendationMatter> recommendationMatters,
        Dictionary<string, string> sessionFields,
        List<AtlasCouncilRecordIssue> issues)
    {
        var heldMatterIds = new HashSet<string>(StringComparer.Ordinal);
        var protectedTriggerCategories = new List<string>();
        if (string.Equals(value, "NONE — no disputed recusal", StringComparison.Ordinal))
        {
            return new(heldMatterIds, protectedTriggerCategories);
        }

        var totalSeatedValid = TryReadPositiveCount(
            sessionFields,
            TotalSeatedPersonsField,
            out var totalSeated);
        var sessionPresentValid = TryReadPositiveCount(
            sessionFields,
            NaturalPersonsPresentField,
            out var sessionPresent);
        var sessionEducatorsValid = TryReadPositiveCount(
            sessionFields,
            PracticingEducatorsPresentField,
            out var sessionEducators);
        var seenMatters = new HashSet<string>(StringComparer.Ordinal);
        var heldRecommendedMatters = new HashSet<string>(StringComparer.Ordinal);
        var recommendationReconciliationReported = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in value.Split(" || ", StringSplitOptions.None))
        {
            if (!TryValidateRecusalDisputeRecord(record, out var dispute))
            {
                issues.Add(new(
                    "atlas.session.recusal-dispute-record-invalid",
                    "Each disputed-recusal subrecord must bind one mapped need and one present affected person, record that person's educator status and exact exclusion counts, reconcile supported-rule quorum/count arithmetic and decision, record exact read-back confirmation, preserve a substantive rationale, and resolve as RECUSED/NOT-RECUSED or honestly remain HELD."));
                continue;
            }

            if (!seenMatters.Add(dispute.Matter))
            {
                issues.Add(new(
                    "atlas.session.recusal-dispute-matter-duplicate",
                    "A mapped need may carry at most one canonical disputed-recusal subrecord because the current recommendation cell cannot reconcile multiple affected people without ambiguity."));
            }

            if (!mappedNeedIds.Contains(dispute.Matter))
            {
                issues.Add(new(
                    "atlas.session.recusal-dispute-matter-unmapped",
                    "Every disputed-recusal matter must identify an exact need ID from the completed need-to-possibility mapping."));
            }

            if (totalSeatedValid
                && sessionPresentValid
                && sessionEducatorsValid
                && ((long)dispute.EligibleTotal + dispute.ExcludedTotal != totalSeated
                    || (long)dispute.EligiblePresent + dispute.ExcludedPresent != sessionPresent
                    || (long)dispute.PracticingEducators
                        + dispute.ExcludedPracticingEducators != sessionEducators))
            {
                issues.Add(new(
                    "atlas.session.recusal-dispute-count-reconciliation-invalid",
                    "Each disputed-recusal record's eligible and one-person exclusion counts must reconcile exactly to the bound session total, present, and practicing-educator natural-person counts."));
            }

            if (dispute.Outcome is RecusalDisputeOutcome.Recused or RecusalDisputeOutcome.Held)
            {
                protectedTriggerCategories.Add(dispute.Category);
            }

            var matchingRecommendations = recommendationMatters
                .Where(recommendation => string.Equals(
                    recommendation.NeedId,
                    dispute.Matter,
                    StringComparison.Ordinal))
                .ToArray();
            if (dispute.Outcome == RecusalDisputeOutcome.Held)
            {
                heldMatterIds.Add(dispute.Matter);
                if (matchingRecommendations.Length > 0
                    && heldRecommendedMatters.Add(dispute.Matter))
                {
                    issues.Add(new(
                        "atlas.session.recusal-dispute-held-matter-recommended",
                        "A matter whose disputed recusal remains HELD cannot appear as a completed council recommendation row."));
                }

                continue;
            }

            if (matchingRecommendations.Length == 0)
            {
                continue;
            }

            var expectedEligibleTotal = dispute.Outcome == RecusalDisputeOutcome.Recused
                ? dispute.EligibleTotal
                : dispute.EligibleTotal + dispute.ExcludedTotal;
            var expectedEligiblePresent = dispute.Outcome == RecusalDisputeOutcome.Recused
                ? dispute.EligiblePresent
                : dispute.EligiblePresent + dispute.ExcludedPresent;
            var expectedEducators = dispute.Outcome == RecusalDisputeOutcome.Recused
                ? dispute.PracticingEducators
                : dispute.PracticingEducators + dispute.ExcludedPracticingEducators;
            var expectedConflict = dispute.Outcome == RecusalDisputeOutcome.Recused
                ? $"RECUSALS — total-persons=1; present-persons=1; practicing-educators={dispute.ExcludedPracticingEducators}; basis=DISPUTE — matter={dispute.Matter},category={dispute.Category},outcome=RECUSED"
                : $"NONE — dispute={dispute.Matter}; category={dispute.Category}; outcome=NOT-RECUSED";
            if (matchingRecommendations.Any(recommendation =>
                    !string.Equals(
                        recommendation.EligibleTotal,
                        expectedEligibleTotal.ToString(CultureInfo.InvariantCulture),
                        StringComparison.Ordinal)
                    || !string.Equals(
                        recommendation.EligiblePresent,
                        expectedEligiblePresent.ToString(CultureInfo.InvariantCulture),
                        StringComparison.Ordinal)
                    || !string.Equals(
                        recommendation.PracticingEducators,
                        expectedEducators.ToString(CultureInfo.InvariantCulture),
                        StringComparison.Ordinal)
                    || !string.Equals(
                        recommendation.Conflict,
                        expectedConflict,
                        StringComparison.Ordinal))
                && recommendationReconciliationReported.Add(dispute.Matter))
            {
                issues.Add(new(
                    "atlas.session.recusal-dispute-recommendation-reconciliation-invalid",
                    "A recommendation on a resolved disputed-recusal matter must carry the exact resolved outcome, one-person conflict cell, and corresponding eligible-person counts."));
            }
        }

        return new(heldMatterIds, protectedTriggerCategories);
    }

    private static bool TryValidateRecusalDisputeRecord(
        string value,
        out RecusalDisputeRecord dispute)
    {
        dispute = new(
            string.Empty,
            string.Empty,
            false,
            0,
            0,
            0,
            RecusalDisputeOutcome.Held,
            0,
            0,
            0);
        const string prefix = "DISPUTE — ";
        if (!value.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var parts = value[prefix.Length..].Split("; ", StringSplitOptions.None);
        if (parts.Length != 15
            || !parts[0].StartsWith("matter=", StringComparison.Ordinal)
            || !IsSubstantiveNeedId(parts[0]["matter=".Length..])
            || !parts[1].StartsWith("category=", StringComparison.Ordinal)
            || !IsPositiveRecordComponent(parts[1]["category=".Length..])
            || !string.Equals(parts[2], "affected-person-excluded=YES", StringComparison.Ordinal)
            || parts[3] is not "affected-person-practicing-educator=YES"
                and not "affected-person-practicing-educator=NO"
            || !TryReadNamedCount(parts[4], "excluded-total=", out var excludedTotal)
            || excludedTotal != 1
            || !TryReadNamedCount(parts[5], "excluded-present=", out var excludedPresent)
            || excludedPresent != 1
            || !TryReadNamedCount(
                parts[6],
                "excluded-practicing-educators=",
                out var excludedPracticingEducators)
            || !TryReadNamedCount(parts[8], "eligible-total=", out var eligibleTotal)
            || !TryReadNamedCount(parts[9], "eligible-present=", out var eligiblePresent)
            || !TryReadNamedCount(parts[10], "practicing-educators=", out var practicingEducators)
            || !parts[12].StartsWith("decision=", StringComparison.Ordinal)
            || !string.Equals(parts[13], "read-back=CONFIRMED", StringComparison.Ordinal)
            || !parts[14].StartsWith("rationale=", StringComparison.Ordinal)
            || !IsSubstantive(parts[14]["rationale=".Length..])
            || eligiblePresent > eligibleTotal
            || practicingEducators > eligiblePresent)
        {
            return false;
        }

        var affectedPersonIsPracticingEducator = string.Equals(
            parts[3],
            "affected-person-practicing-educator=YES",
            StringComparison.Ordinal);
        if (excludedPracticingEducators != (affectedPersonIsPracticingEducator ? 1 : 0))
        {
            return false;
        }

        var hasQuorum = HasRecusalDisputeQuorum(
            eligibleTotal,
            eligiblePresent,
            practicingEducators);
        var decision = parts[12]["decision=".Length..];
        var outcome = parts[7] switch
        {
            "outcome=RECUSED" => RecusalDisputeOutcome.Recused,
            "outcome=NOT-RECUSED" => RecusalDisputeOutcome.NotRecused,
            "outcome=HELD" => RecusalDisputeOutcome.Held,
            _ => (RecusalDisputeOutcome?)null,
        };
        var validOutcome = outcome is RecusalDisputeOutcome.Recused or RecusalDisputeOutcome.NotRecused
            ? string.Equals(
                    parts[11],
                    $"quorum=MET — {SupportedOperatingTermsId}",
                    StringComparison.Ordinal)
                && hasQuorum
                && TryValidateResolvedRecusalDisputeDecision(decision, eligiblePresent)
            : outcome == RecusalDisputeOutcome.Held
                && (string.Equals(
                        parts[11],
                        $"quorum=NOT-MET — {SupportedOperatingTermsId}",
                        StringComparison.Ordinal)
                    && !hasQuorum
                    && string.Equals(decision, "NONE — quorum not met", StringComparison.Ordinal)
                    || string.Equals(
                        parts[11],
                        $"quorum=MET — {SupportedOperatingTermsId}",
                        StringComparison.Ordinal)
                    && hasQuorum
                    && TryReadRecusalDisputeVote(
                        decision,
                        eligiblePresent,
                        out var hasStrictMajority)
                    && !hasStrictMajority);
        if (!validOutcome)
        {
            return false;
        }

        dispute = new(
            parts[0]["matter=".Length..],
            parts[1]["category=".Length..],
            affectedPersonIsPracticingEducator,
            excludedTotal,
            excludedPresent,
            excludedPracticingEducators,
            outcome!.Value,
            eligibleTotal,
            eligiblePresent,
            practicingEducators);
        return true;
    }

    private static bool HasRecusalDisputeQuorum(
        int eligibleTotal,
        int eligiblePresent,
        int practicingEducators)
        => eligiblePresent > eligibleTotal / 2
            && eligiblePresent >= 3
            && practicingEducators >= 2;

    private static bool TryValidateResolvedRecusalDisputeDecision(
        string value,
        int eligiblePresent)
    {
        const string consensusPrefix = "CONSENSUS — denominator=";
        if (value.StartsWith(consensusPrefix, StringComparison.Ordinal))
        {
            return TryParseCount(
                    value[consensusPrefix.Length..],
                    allowZero: false,
                    out var consensusDenominator)
                && consensusDenominator == eligiblePresent;
        }

        return TryReadRecusalDisputeVote(
                value,
                eligiblePresent,
                out var hasStrictMajority)
            && hasStrictMajority;
    }

    private static bool TryReadRecusalDisputeVote(
        string value,
        int eligiblePresent,
        out bool hasStrictMajority)
    {
        hasStrictMajority = false;
        const string votePrefix = "VOTE — ";
        if (!value.StartsWith(votePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var voteParts = value[votePrefix.Length..].Split(',', StringSplitOptions.None);
        if (voteParts.Length != 4
            || !TryReadNamedCount(voteParts[0], "for=", out var forCount)
            || !TryReadNamedCount(voteParts[1], "against=", out var againstCount)
            || !TryReadNamedCount(voteParts[2], "abstain=", out var abstainCount)
            || !TryReadNamedCount(voteParts[3], "denominator=", out var denominator))
        {
            return false;
        }

        if (denominator != eligiblePresent
            || (long)forCount + againstCount + abstainCount != denominator)
        {
            return false;
        }

        hasStrictMajority = forCount > denominator / 2;
        return true;
    }

    private static bool TryReadNamedCount(string value, string prefix, out int count)
    {
        count = default;
        return value.StartsWith(prefix, StringComparison.Ordinal)
            && TryParseCount(value[prefix.Length..], allowZero: true, out count);
    }

    private static bool TryParseCount(string value, bool allowZero, out int count)
        => int.TryParse(
                NormalizeCell(value),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out count)
            && (allowZero ? count >= 0 : count > 0);

    private static RecommendationSupplementalValidation ValidateRecommendationSupplementalFields(
        string[] lines,
        int startHeading,
        int endHeading,
        List<AtlasCouncilRecordIssue> issues)
    {
        var occurrences = RecommendationSupplementalFields.ToDictionary(
            field => field,
            _ => new List<int>(),
            StringComparer.Ordinal);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var hasContent = false;

        for (var index = startHeading + 1; index < endHeading; index++)
        {
            var line = lines[index].Trim();
            foreach (var field in RecommendationSupplementalFields)
            {
                var prefix = $"- **{field}:**";
                if (!line.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                occurrences[field].Add(index);
                var value = new StringBuilder(line[prefix.Length..].Trim());
                hasContent |= IsSubstantive(value.ToString());

                for (var continuationIndex = index + 1;
                     continuationIndex < endHeading;
                     continuationIndex++)
                {
                    var continuation = lines[continuationIndex];
                    if (IsRecommendationSupplementalContinuationBoundary(continuation))
                    {
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(continuation))
                    {
                        continue;
                    }

                    if (!char.IsWhiteSpace(continuation[0]))
                    {
                        break;
                    }

                    var continuationValue = continuation.Trim();
                    hasContent |= IsSubstantive(continuationValue);
                    if (continuationValue.Length > 0)
                    {
                        if (value.Length > 0)
                        {
                            value.Append(' ');
                        }

                        value.Append(continuationValue);
                    }
                }

                values.TryAdd(field, value.ToString());
                break;
            }
        }

        foreach (var field in RecommendationSupplementalFields)
        {
            var count = occurrences[field].Count;
            if (count == 0)
            {
                issues.Add(new(
                    "atlas.recommendation.supplemental-field-missing",
                    "The council recommendation section is missing a canonical supplemental record field."));
            }
            else if (count > 1)
            {
                issues.Add(new(
                    "atlas.recommendation.supplemental-field-duplicate",
                    "The council recommendation section repeats a canonical supplemental record field."));
            }
        }

        return new RecommendationSupplementalValidation(hasContent, values);
    }

    private static bool IsRecommendationSupplementalFieldLine(string line)
    {
        var candidate = line.Trim();
        return RecommendationSupplementalFields.Any(field =>
            candidate.StartsWith($"- **{field}:**", StringComparison.Ordinal));
    }

    private static bool IsRecommendationSupplementalContinuationBoundary(string line)
    {
        if (IsRecommendationSupplementalFieldLine(line))
        {
            return true;
        }

        var candidate = line.TrimStart();
        if (candidate.StartsWith('|') && candidate.TrimEnd().EndsWith('|'))
        {
            return true;
        }

        var headingMarkerCount = 0;
        while (headingMarkerCount < candidate.Length
            && candidate[headingMarkerCount] == '#')
        {
            headingMarkerCount++;
        }

        return headingMarkerCount is > 0 and <= 6
            && headingMarkerCount < candidate.Length
            && char.IsWhiteSpace(candidate[headingMarkerCount]);
    }

    private static bool TryReadRecommendationIdentity(
        string value,
        out string needId,
        out string mappedPossibility)
    {
        needId = string.Empty;
        mappedPossibility = string.Empty;
        var candidate = value.Trim();
        var firstWhitespace = candidate.IndexOf(' ');
        if (firstWhitespace <= 0 || firstWhitespace == candidate.Length - 1)
        {
            return false;
        }

        needId = NormalizeCell(candidate[..firstWhitespace]);
        var possibilityCandidate = candidate[(firstWhitespace + 1)..].Trim();
        if (possibilityCandidate.Length > 0 && possibilityCandidate[0] == '·')
        {
            possibilityCandidate = possibilityCandidate[1..].Trim();
        }

        mappedPossibility = NormalizeCell(possibilityCandidate);
        return IsSubstantiveNeedId(needId) && IsSubstantive(mappedPossibility);
    }

    private static int IndexOf(IReadOnlyList<string> values, string expected)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (string.Equals(values[index], expected, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static DataRowValidation ValidateDataRows(
        string[] lines,
        int startHeading,
        int endHeading,
        string[] expectedHeader,
        string sectionName,
        string incompleteRowCode,
        IReadOnlySet<string> predecessorKeys,
        string predecessorMismatchCode,
        string predecessorDescription,
        string duplicateKeyCode,
        string incompleteCoverageCode,
        List<AtlasCouncilRecordIssue> issues)
    {
        var rows = ReadTableRows(lines, startHeading, endHeading);
        var expectedColumnCount = expectedHeader.Length;
        ValidateCanonicalTableScaffold(
            rows,
            expectedHeader,
            sectionName,
            $"atlas.{sectionName.Replace("product-owner ", string.Empty, StringComparison.Ordinal)}.header",
            $"atlas.{sectionName.Replace("product-owner ", string.Empty, StringComparison.Ordinal)}.header-duplicate",
            $"atlas.{sectionName.Replace("product-owner ", string.Empty, StringComparison.Ordinal)}.separator",
            issues);

        var linkedKeys = new HashSet<string>(StringComparer.Ordinal);
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        var hasContent = false;
        var invalidWidthReported = false;
        var incompleteRowReported = false;
        var predecessorMismatchReported = false;
        var duplicateKeyReported = false;
        foreach (var row in rows)
        {
            if (row.Count == 0)
            {
                continue;
            }

            var isHeader = RowsEqual(row, expectedHeader);
            var isSeparator = IsCanonicalSeparatorRow(row, expectedColumnCount);
            var hasRowContent = !isHeader && !isSeparator && row.Any(IsSubstantive);
            hasContent |= hasRowContent;
            if (row.Count != expectedColumnCount && !invalidWidthReported)
            {
                issues.Add(new(
                    "atlas.table.width",
                    $"The {sectionName} table must retain its canonical {expectedColumnCount}-column shape."));
                invalidWidthReported = true;
            }

            if (!hasRowContent || row.Count != expectedColumnCount)
            {
                continue;
            }

            var rowComplete = row.All(IsSubstantive);
            if (!rowComplete && !incompleteRowReported)
            {
                issues.Add(new(
                    incompleteRowCode,
                    $"Every started {sectionName} row must explicitly complete all {expectedColumnCount} canonical cells."));
                incompleteRowReported = true;
            }

            var key = NormalizeCell(row[0]);
            if (IsSubstantive(row[0]) && !seenKeys.Add(key) && !duplicateKeyReported)
            {
                issues.Add(new(
                    duplicateKeyCode,
                    $"Every substantive {sectionName} row must use a unique predecessor key."));
                duplicateKeyReported = true;
            }

            var predecessorLinked = predecessorKeys.Contains(key);
            if (predecessorKeys.Count > 0
                && !predecessorLinked
                && !predecessorMismatchReported)
            {
                issues.Add(new(
                    predecessorMismatchCode,
                    $"Every substantive {sectionName} row must cite an exact preceding {predecessorDescription}."));
                predecessorMismatchReported = true;
            }

            if (rowComplete && predecessorLinked)
            {
                linkedKeys.Add(key);
            }
        }

        if (hasContent
            && predecessorKeys.Count > 0
            && predecessorKeys.Any(key => !linkedKeys.Contains(key)))
        {
            issues.Add(new(
                incompleteCoverageCode,
                $"A started {sectionName} section must contain exactly one complete row for every preceding {predecessorDescription}."));
        }

        return new DataRowValidation(hasContent, linkedKeys);
    }

    private static bool ValidateCanonicalTableScaffold(
        List<IReadOnlyList<string>> rows,
        string[] expectedHeader,
        string sectionName,
        string missingOrMisplacedHeaderCode,
        string duplicateHeaderCode,
        string separatorCode,
        List<AtlasCouncilRecordIssue> issues)
    {
        var headerIndexes = rows
            .Select((row, index) => (row, index))
            .Where(candidate => RowsEqual(candidate.row, expectedHeader))
            .Select(candidate => candidate.index)
            .ToArray();
        var separatorIndexes = rows
            .Select((row, index) => (row, index))
            .Where(candidate => IsSeparatorRow(candidate.row))
            .Select(candidate => candidate.index)
            .ToArray();
        var hasCanonicalSeparator = separatorIndexes.Length == 1
            && separatorIndexes[0] == 1
            && IsCanonicalSeparatorRow(rows[1], expectedHeader.Length);

        if (headerIndexes.Length == 0 || headerIndexes[0] != 0)
        {
            issues.Add(new(
                missingOrMisplacedHeaderCode,
                $"The {sectionName} table must begin with its exact canonical header."));
        }

        if (headerIndexes.Length > 1)
        {
            issues.Add(new(
                duplicateHeaderCode,
                $"The {sectionName} table must contain exactly one canonical header."));
        }

        if (!hasCanonicalSeparator)
        {
            issues.Add(new(
                separatorCode,
                $"The {sectionName} table must contain exactly one exact canonical separator immediately after its header."));
        }

        return headerIndexes.Length == 1
            && headerIndexes[0] == 0
            && hasCanonicalSeparator;
    }

    private static List<IReadOnlyList<string>> ReadTableRows(
        string[] lines,
        int startHeading,
        int endHeading)
    {
        var rows = new List<IReadOnlyList<string>>();
        for (var index = startHeading + 1; index < endHeading; index++)
        {
            var line = lines[index].Trim();
            var startsWithBoundary = line.StartsWith('|');
            var endsWithBoundary = line.EndsWith('|');
            if (!startsWithBoundary || !endsWithBoundary)
            {
                if (startsWithBoundary || endsWithBoundary || ContainsUnescapedPipe(line))
                {
                    rows.Add(["[malformed table row boundary]"]);
                }

                continue;
            }

            rows.Add(SplitMarkdownTableRow(line[1..^1]));
        }

        return rows;
    }

    private static bool ContainsUnescapedPipe(string line)
    {
        var precedingBackslashes = 0;
        foreach (var character in line)
        {
            if (character == '\\')
            {
                precedingBackslashes++;
                continue;
            }

            if (character == '|' && precedingBackslashes % 2 == 0)
            {
                return true;
            }

            precedingBackslashes = 0;
        }

        return false;
    }

    private static List<string> SplitMarkdownTableRow(string content)
    {
        var cells = new List<string>();
        var current = new StringBuilder();
        for (var index = 0; index < content.Length; index++)
        {
            var character = content[index];
            if (character == '\\' && index + 1 < content.Length)
            {
                var escaped = content[++index];
                if (escaped is '|' or '\\')
                {
                    current.Append(escaped);
                }
                else
                {
                    current.Append(character);
                    current.Append(escaped);
                }
            }
            else if (character == '|')
            {
                cells.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }

        cells.Add(current.ToString().Trim());
        return cells;
    }

    private static bool IsSeparatorRow(IReadOnlyList<string> row)
        => row.Count > 0 && row.All(IsSeparatorCell);

    private static bool IsCanonicalSeparatorRow(
        IReadOnlyList<string> row,
        int expectedColumnCount)
        => row.Count == expectedColumnCount
            && row.All(cell => string.Equals(cell, "---", StringComparison.Ordinal));

    private static bool IsSeparatorCell(string cell)
    {
        var candidate = cell.Trim(':');
        return candidate.Length > 0 && candidate.All(character => character == '-');
    }

    private static bool IsSubstantive(string value)
    {
        var candidate = value.Trim().Trim('`').Trim();
        return candidate.Length > 0
            && !(candidate.StartsWith('[')
                && candidate.EndsWith(']'))
            && !string.Equals(candidate, "TBD", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(candidate, "TODO", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(candidate, "PENDING", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(candidate, "UNKNOWN", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(candidate, "N/A", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(candidate, "DRAFT", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(candidate, "X", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(candidate, "LATER", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(candidate, "-", StringComparison.Ordinal)
            && !string.Equals(candidate, "...", StringComparison.Ordinal)
            && !string.Equals(candidate, "[not run]", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(candidate, "[not enacted / not run]", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(candidate, "[not decided]", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(candidate, "[not supplied]", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record NeedCardValidation(
        bool HasContent,
        IReadOnlySet<string> CompletedNeedIds);

    private sealed record NeedMappingValidation(
        bool HasContent,
        IReadOnlySet<string> MappedNeedIds,
        IReadOnlySet<NeedPossibilityKey> CompletedMappings);

    private sealed record NeedPossibilityKey(
        string NeedId,
        string Possibility);

    private sealed record RecommendationValidation(
        bool HasContent,
        IReadOnlySet<string> MappedPossibilities,
        IReadOnlySet<string> RecommendedNeedIds,
        IReadOnlyList<RecommendationMatter> RecommendationMatters);

    private sealed record RecommendationMatter(
        string NeedId,
        string EligibleTotal,
        string EligiblePresent,
        string PracticingEducators,
        string Conflict);

    private sealed record RecusalDisputeValidation(
        IReadOnlySet<string> HeldMatterIds,
        IReadOnlyList<string> ProtectedTriggerCategories);

    private enum RecusalDisputeOutcome
    {
        Recused,
        NotRecused,
        Held,
    }

    private sealed record RecusalDisputeRecord(
        string Matter,
        string Category,
        bool AffectedPersonIsPracticingEducator,
        int ExcludedTotal,
        int ExcludedPresent,
        int ExcludedPracticingEducators,
        RecusalDisputeOutcome Outcome,
        int EligibleTotal,
        int EligiblePresent,
        int PracticingEducators);

    private sealed record RecommendationSupplementalValidation(
        bool HasContent,
        IReadOnlyDictionary<string, string> Values);

    private sealed record DataRowValidation(
        bool HasContent,
        IReadOnlySet<string> LinkedKeys);

    private sealed record SeatAuthorityEntry(
        string Seat,
        string PersonReference,
        bool IsPresent,
        bool IsPracticingEducator);

    private sealed record H0RecordData(
        string RecordIdentity,
        IReadOnlySet<string> RecommendedPossibilities,
        Dictionary<string, string> SessionFields)
    {
        public string RepositoryRevision => Field(RepositoryRevisionField);
        public string BuildArtifacts => Field(BuildArtifactsField);
        public string Instrument => Field(InstrumentField);
        public string ExactMaterial => Field(ExactMaterialField);
        public string PresentSeats => Field(PresentSeatsField);
        public string AbsentSeats => Field(AbsentSeatsField);
        public string NaturalPersonsPresent => Field(NaturalPersonsPresentField);
        public string TotalSeatedPersons => Field(TotalSeatedPersonsField);
        public string PracticingEducatorsPresent => Field(PracticingEducatorsPresentField);
        public string RosterBinding => Field(RosterBindingField);
        public string MultiCapacities => Field(MultiCapacityField);
        public string ContentLicense => Field(ContentLicenseField);
        public string OperatingTermsBinding => Field(OperatingTermsBindingField);
        public string ProtectedSeatHolds => Field(ProtectedSeatHoldsField);
        public string SeatAuthority => Field(SeatAuthorityField);
        public string ParticipationConsent => Field(ParticipationConsentField);
        public string QuorumResult => Field(QuorumResultField);
        public string ConflictRecusals => Field(ConflictRecusalField);
        public string RecusalDisputeRecords => Field(RecusalDisputeRecordsField);
        public string WithdrawalAcknowledgement => Field(WithdrawalField);
        public string CompensationElection => Field(CompensationField);
        public string CompensationAdministration => Field(CompensationAdministrationField);
        public string CompensationBinding => Field(CompensationBindingField);
        public string NoteCollectionConsent => Field(NoteCollectionConsentField);
        public string PublicRecordConsent => Field(PublicRecordConsentField);
        public string RecordingConsent => Field(RecordingConsentField);
        public string CohortDisclosure => Field(CohortDisclosureField);
        public string PublicCreditChoice => Field(PublicCreditField);
        public string ContentContributionChoice => Field(ContentContributionChoiceField);
        public string RoleAcceptanceChoice => Field(RoleAcceptanceChoiceField);
        public string MaintainerAppointmentChoice => Field(MaintainerAppointmentChoiceField);
        public string CopyrightStewardshipChoice => Field(CopyrightStewardshipChoiceField);
        public string WithdrawalDisposition => Field(WithdrawalDispositionField);
        public string DecisionProcedure => Field(DecisionProcedureField);
        public DateOnly? SessionDate => TryReadSessionDate(Field("Session date and duration"), out var date)
            ? date
            : null;

        private string Field(string name) => ReadNormalizedField(SessionFields, name);
    }
}
