// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Tools.AtlasCouncilRecords;

namespace Foundry.Tests.Unit;

public class AtlasCouncilRecordValidatorTests
{
    [Fact]
    public void Boundary_expressly_refuses_every_human_or_protected_inference()
    {
        Assert.Equal(
            "This validator checks record mechanics only. It does not infer quorum, score needs, rank or recommend possibilities, authenticate participants, select a priority, or perform a protected-seat act. Whether a public-credit identity may appear remains a human confirmation outside this validator.",
            AtlasCouncilRecordValidator.AuthorityBoundary);
    }

    [Fact]
    public void Unrun_dated_copy_accepts_placeholders_but_not_claimed_session_or_freeze_activity()
    {
        var result = AtlasCouncilRecordValidator.Validate(
            "atlas-priority-session-2030-01-02.md",
            SyntheticRecord(AtlasCouncilRecordValidator.UnrunStatus));

        Assert.True(result.IsValid, Describe(result));
        Assert.Equal(AtlasCouncilRecordStatus.Unrun, result.Status);

        var claimedActivity = SyntheticRecord(AtlasCouncilRecordValidator.UnrunStatus)
            .Replace(
                "| Session occurred; dated copy status changed from `UNRUN` | [not run] |",
                "| Session occurred; dated copy status changed from `UNRUN` | claimed complete |",
                StringComparison.Ordinal);
        var refusal = AtlasCouncilRecordValidator.Validate(
            "atlas-priority-session-2030-01-02.md",
            claimedActivity);

        Assert.Contains(refusal.Issues, issue => issue.Code == "atlas.status.session-mismatch");
    }

    [Fact]
    public void Unrun_refuses_content_from_every_later_record_stage()
    {
        var recommendationOnly = SyntheticRecord(AtlasCouncilRecordValidator.UnrunStatus)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace(
                "|---|---|---|---|---|---|\n| | | | | | |",
                "|---|---|---|---|---|---|\n| 1 | N-SYNTHETIC · Synthetic possibility | Synthetic reason | Synthetic proof | Hold retained | None recorded |",
                StringComparison.Ordinal);
        var records = new (string Stage, string Record)[]
        {
            (
                "session field",
                SyntheticRecord(AtlasCouncilRecordValidator.UnrunStatus).Replace(
                    "| Session date and duration | [not run] |",
                    "| Session date and duration | 2030-01-02 · 60 minutes |",
                    StringComparison.Ordinal)),
            (
                "need card",
                SyntheticRecord(
                    AtlasCouncilRecordValidator.UnrunStatus,
                    needCardComplete: true)),
            (
                "mapping",
                SyntheticRecord(
                    AtlasCouncilRecordValidator.UnrunStatus,
                    includeMapping: true)),
            ("recommendation", recommendationOnly),
            (
                "feasibility",
                SyntheticRecord(
                    AtlasCouncilRecordValidator.UnrunStatus,
                    includeFeasibility: true)),
            (
                "disposition",
                SyntheticRecord(
                    AtlasCouncilRecordValidator.UnrunStatus,
                    includeDisposition: true)),
        };

        foreach (var (stage, record) in records)
        {
            var result = AtlasCouncilRecordValidator.Validate(
                "atlas-priority-session-2030-01-02.md",
                record);

            Assert.True(
                result.Issues.Any(issue => issue.Code == "atlas.status.session-mismatch"),
                $"UNRUN accepted {stage} content.{Environment.NewLine}{Describe(result)}");
        }
    }

    [Fact]
    public void Unrun_refuses_substantive_table_rows_with_noncanonical_outer_boundaries()
    {
        var unrun = SyntheticRecord(AtlasCouncilRecordValidator.UnrunStatus);
        var records = new (string Shape, string Record)[]
        {
            (
                "missing closing boundary",
                unrun.Replace(
                    "| | | | | | | |",
                    "| Synthetic possibility | Existing component | Bounded synthetic slice | None | Machine evidence | Small | None recorded",
                    StringComparison.Ordinal)),
            (
                "missing both outer boundaries",
                unrun.Replace(
                    "| [not run] | [not decided] | | | | |",
                    "Synthetic possibility | Defer · 2030-01-04 | No implementation | Await owner evidence | AAC/SLP | Separate protected review",
                    StringComparison.Ordinal)),
        };

        foreach (var (shape, record) in records)
        {
            var result = AtlasCouncilRecordValidator.Validate(
                "atlas-priority-session-2030-01-02.md",
                record);

            Assert.True(
                result.Issues.Any(issue => issue.Code == "atlas.status.session-mismatch"),
                $"UNRUN accepted a substantive row with {shape}.{Environment.NewLine}{Describe(result)}");
            Assert.Contains(result.Issues, issue => issue.Code == "atlas.table.width");
        }
    }

    [Fact]
    public void Unrun_refuses_each_canonical_recommendation_supplemental_field_and_multiline_content()
    {
        var supplementalFields = new[]
        {
            "Needs deliberately not advanced, and why",
            "Useful possibilities with no atlas match",
            "Questions the session could not answer",
            "Corrections members made during read-back",
            "Whether members reached consensus, split, or made no ordering",
        };

        foreach (var field in supplementalFields)
        {
            var marker = $"- **{field}:**";
            var record = SyntheticRecord(AtlasCouncilRecordValidator.UnrunStatus)
                .Replace(marker, $"{marker} Synthetic recorded content", StringComparison.Ordinal);
            var result = AtlasCouncilRecordValidator.Validate(
                "atlas-priority-session-2030-01-02.md",
                record);

            Assert.True(
                result.Issues.Any(issue => issue.Code == "atlas.status.session-mismatch"),
                $"UNRUN accepted content for '{field}'.{Environment.NewLine}{Describe(result)}");
            Assert.Contains(
                result.Issues,
                issue => issue.Code == "atlas.lifecycle.recommendation-before-session");
        }

        var multilineMarker = "- **Questions the session could not answer:**";
        var multiline = SyntheticRecord(AtlasCouncilRecordValidator.UnrunStatus)
            .Replace(
                multilineMarker,
                $"{multilineMarker}{Environment.NewLine}  Synthetic continuation",
                StringComparison.Ordinal);
        var multilineResult = AtlasCouncilRecordValidator.Validate(
            "atlas-priority-session-2030-01-02.md",
            multiline);

        Assert.Contains(
            multilineResult.Issues,
            issue => issue.Code == "atlas.status.session-mismatch");

        var blankSeparated = SyntheticRecord(AtlasCouncilRecordValidator.UnrunStatus)
            .Replace(
                multilineMarker,
                $"{multilineMarker}{Environment.NewLine}{Environment.NewLine}  Synthetic blank-separated continuation",
                StringComparison.Ordinal);
        var blankSeparatedResult = AtlasCouncilRecordValidator.Validate(
            "atlas-priority-session-2030-01-02.md",
            blankSeparated);

        Assert.Contains(
            blankSeparatedResult.Issues,
            issue => issue.Code == "atlas.status.session-mismatch");
        Assert.Contains(
            blankSeparatedResult.Issues,
            issue => issue.Code == "atlas.lifecycle.recommendation-before-session");
    }

    [Fact]
    public void Held_unranked_consultation_records_quorum_field_without_deciding_it()
    {
        var result = AtlasCouncilRecordValidator.Validate(
            "atlas-priority-session-2030-01-02.md",
            SyntheticRecord(
                AtlasCouncilRecordValidator.SessionHeldStatus,
                sessionComplete: true,
                decisionProcedure: "Not enacted; no quorum; unranked consultation only."));

        Assert.True(result.IsValid, Describe(result));
        Assert.Equal(AtlasCouncilRecordStatus.SessionHeldReviewPending, result.Status);

        var missingOccurrence = SyntheticRecord(
                AtlasCouncilRecordValidator.SessionHeldStatus,
                sessionComplete: true)
            .Replace(
                "| Session occurred; dated copy status changed from `UNRUN` | confirmed |",
                "| Session occurred; dated copy status changed from `UNRUN` | [not run] |",
                StringComparison.Ordinal);
        var refusal = AtlasCouncilRecordValidator.Validate(
            "atlas-priority-session-2030-01-02.md",
            missingOccurrence);

        Assert.Contains(refusal.Issues, issue => issue.Code == "atlas.session.occurrence-missing");
    }

    [Fact]
    public void Frozen_record_may_carry_feasibility_then_product_owner_disposition()
    {
        var result = AtlasCouncilRecordValidator.Validate(
            "atlas-priority-session-2030-01-02.md",
            SyntheticRecord(
                AtlasCouncilRecordValidator.FrozenStatus,
                sessionComplete: true,
                freezeComplete: true,
                includeRecommendation: true,
                includeFeasibility: true,
                includeDisposition: true));

        Assert.True(result.IsValid, Describe(result));
        Assert.Equal(AtlasCouncilRecordStatus.CouncilRecordFrozen, result.Status);
    }

    [Theory]
    [InlineData("READY TEMPLATE — UNRUN")]
    [InlineData("SESSION COMPLETE")]
    [InlineData("PRIORITY SELECTED")]
    public void Dated_record_refuses_statuses_outside_the_existing_vocabulary(string status)
    {
        var result = AtlasCouncilRecordValidator.Validate(
            "atlas-priority-session-2030-01-02.md",
            SyntheticRecord(status));

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.status.unknown");
        Assert.Null(result.Status);
    }

    [Fact]
    public void Required_headings_must_appear_once_in_the_authoritative_order()
    {
        var record = SyntheticRecord(AtlasCouncilRecordValidator.UnrunStatus)
            .Replace(
                "### Need card — complete before opening the atlas",
                "### Missing need card",
                StringComparison.Ordinal)
            .Replace(
                "## Completion check",
                "## Product-owner disposition — intentionally blank in the template\n\n## Completion check",
                StringComparison.Ordinal);

        var result = AtlasCouncilRecordValidator.Validate(
            "atlas-priority-session-2030-01-02.md",
            record);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.heading.missing");
        Assert.Contains(result.Issues, issue => issue.Code == "atlas.heading.duplicate");
    }

    [Fact]
    public void Complete_required_headings_in_a_different_order_are_refused()
    {
        const string NeedHeading = "### Need card — complete before opening the atlas";
        const string MappingHeading = "### Need-to-possibility mapping — complete only after need capture";
        const string SwapMarker = "### SYNTHETIC SWAP MARKER";
        var record = SyntheticRecord(AtlasCouncilRecordValidator.UnrunStatus)
            .Replace(NeedHeading, SwapMarker, StringComparison.Ordinal)
            .Replace(MappingHeading, NeedHeading, StringComparison.Ordinal)
            .Replace(SwapMarker, MappingHeading, StringComparison.Ordinal);

        var result = AtlasCouncilRecordValidator.Validate(
            "atlas-priority-session-2030-01-02.md",
            record);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.heading.order");
    }

    [Fact]
    public void Need_card_and_mapping_tables_retain_canonical_shapes_and_prompt_order()
    {
        var malformed = SyntheticRecord(AtlasCouncilRecordValidator.UnrunStatus)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace(
                "| Prompt | Council member's words |",
                "| Prompt | Reworded column |",
                StringComparison.Ordinal)
            .Replace(
                "| Recurring teacher work or learner-facing barrier |  |\n| Who encounters it (generic role/context only) |  |",
                "| Who encounters it (generic role/context only) |  |\n| Recurring teacher work or learner-facing barrier |  |",
                StringComparison.Ordinal)
            .Replace(
                "| | | | | |",
                "| | | | | | unexpected |",
                StringComparison.Ordinal);

        var result = AtlasCouncilRecordValidator.Validate(
            "atlas-priority-session-2030-01-02.md",
            malformed);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.need-card.header");
        Assert.Contains(result.Issues, issue => issue.Code == "atlas.table.mapping-width");

        var reordered = SyntheticRecord(AtlasCouncilRecordValidator.UnrunStatus)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace(
                "| Recurring teacher work or learner-facing barrier |  |\n| Who encounters it (generic role/context only) |  |",
                "| Who encounters it (generic role/context only) |  |\n| Recurring teacher work or learner-facing barrier |  |",
                StringComparison.Ordinal);
        var reorderedResult = AtlasCouncilRecordValidator.Validate(
            "atlas-priority-session-2030-01-02.md",
            reordered);

        Assert.Contains(reorderedResult.Issues, issue => issue.Code == "atlas.need-card.field-order");

        var truncated = SyntheticRecord(AtlasCouncilRecordValidator.UnrunStatus)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("| Seat speaking |  |\n", string.Empty, StringComparison.Ordinal);
        var truncatedResult = AtlasCouncilRecordValidator.Validate(
            "atlas-priority-session-2030-01-02.md",
            truncated);

        Assert.Contains(truncatedResult.Issues, issue => issue.Code == "atlas.need-card.field-order");
    }

    [Fact]
    public void Substantive_mapping_requires_an_exact_preceding_completed_need_card()
    {
        var valid = SyntheticRecord(
            AtlasCouncilRecordValidator.SessionHeldStatus,
            sessionComplete: true,
            includeMapping: true);
        var validResult = AtlasCouncilRecordValidator.Validate(
            "atlas-priority-session-2030-01-02.md",
            valid);

        Assert.True(validResult.IsValid, Describe(validResult));

        var incompleteNeed = valid.Replace(
            "| What must remain under teacher control | synthetic council words |",
            "| What must remain under teacher control | |",
            StringComparison.Ordinal);
        var incompleteResult = AtlasCouncilRecordValidator.Validate(
            "atlas-priority-session-2030-01-02.md",
            incompleteNeed);

        Assert.Contains(incompleteResult.Issues, issue => issue.Code == "atlas.need-card.incomplete");
        Assert.Contains(incompleteResult.Issues, issue => issue.Code == "atlas.lifecycle.mapping-before-need");

        var mismatchedNeed = valid.Replace(
            "| N-SYNTHETIC | Synthetic possibility |",
            "| N-OTHER | Synthetic possibility |",
            StringComparison.Ordinal);
        var mismatchResult = AtlasCouncilRecordValidator.Validate(
            "atlas-priority-session-2030-01-02.md",
            mismatchedNeed);

        Assert.Contains(mismatchResult.Issues, issue => issue.Code == "atlas.lifecycle.mapping-before-need");
    }

    [Fact]
    public void Exact_need_to_possibility_identity_may_not_be_repeated()
    {
        const string Mapping =
            "| N-SYNTHETIC | Synthetic possibility | Synthetic fit record | G | AAC/SLP hold retained |";
        var repeated = SyntheticRecord(
                AtlasCouncilRecordValidator.SessionHeldStatus,
                sessionComplete: true,
                includeMapping: true)
            .Replace(
                Mapping,
                $"{Mapping}{Environment.NewLine}" +
                "| N-SYNTHETIC | Synthetic possibility | Contradictory fit record | R | No seats recorded |",
                StringComparison.Ordinal);

        var result = AtlasCouncilRecordValidator.Validate(
            "atlas-priority-session-2030-01-02.md",
            repeated);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "atlas.mapping.identity-duplicate");
    }

    [Fact]
    public void Repeated_complete_need_cards_are_supported_and_recommendation_still_requires_mapping()
    {
        const string MappingHeading = "### Need-to-possibility mapping — complete only after need capture";
        const string SecondNeedCard = """
            | Prompt | Council member's words |
            |---|---|
            | Need ID | `N-SECOND` |
            | Recurring teacher work or learner-facing barrier | second synthetic words |
            | Who encounters it (generic role/context only) | second synthetic words |
            | How often it occurs | second synthetic words |
            | Current workaround and its time/material cost | second synthetic words |
            | What a useful paper/offline artifact would make possible | second synthetic words |
            | What must remain under teacher control | second synthetic words |
            | Unacceptable failure or harm | second synthetic words |
            | First classroom proof that would earn trust | second synthetic words |
            | Seat speaking | second synthetic words |

            """;
        var repeated = SyntheticRecord(
                AtlasCouncilRecordValidator.SessionHeldStatus,
                sessionComplete: true,
                includeMapping: true)
            .Replace(
                MappingHeading,
                SecondNeedCard + MappingHeading,
                StringComparison.Ordinal)
            .Replace(
                "| N-SYNTHETIC | Synthetic possibility |",
                "| N-SECOND | Synthetic possibility |",
                StringComparison.Ordinal);
        var repeatedResult = AtlasCouncilRecordValidator.Validate(
            "atlas-priority-session-2030-01-02.md",
            repeated);

        Assert.True(repeatedResult.IsValid, Describe(repeatedResult));

        var recommendationWithoutMapping = SyntheticRecord(
                AtlasCouncilRecordValidator.FrozenStatus,
                sessionComplete: true,
                freezeComplete: true,
                includeRecommendation: true)
            .Replace(
                "| N-SYNTHETIC | Synthetic possibility | Synthetic fit record | G | AAC/SLP hold retained |",
                "| | | | | |",
                StringComparison.Ordinal);
        var recommendationResult = AtlasCouncilRecordValidator.Validate(
            "atlas-priority-session-2030-01-02.md",
            recommendationWithoutMapping);

        Assert.Contains(
            recommendationResult.Issues,
            issue => issue.Code == "atlas.lifecycle.recommendation-before-mapping");
    }

    [Fact]
    public void Recommendation_need_id_must_exactly_match_a_completed_mapping()
    {
        var record = SyntheticRecord(
                AtlasCouncilRecordValidator.FrozenStatus,
                sessionComplete: true,
                freezeComplete: true,
                includeRecommendation: true)
            .Replace(
                "N-SYNTHETIC · Synthetic possibility",
                "N-OTHER · Synthetic possibility",
                StringComparison.Ordinal);

        var result = AtlasCouncilRecordValidator.Validate(
            "atlas-priority-session-2030-01-02.md",
            record);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.recommendation.need-unmapped");
    }

    [Fact]
    public void Recommendation_possibility_must_exactly_match_the_possibility_mapped_for_that_need()
    {
        var record = SyntheticRecord(
                AtlasCouncilRecordValidator.FrozenStatus,
                sessionComplete: true,
                freezeComplete: true,
                includeRecommendation: true)
            .Replace(
                "N-SYNTHETIC · Synthetic possibility",
                "N-SYNTHETIC · Different possibility",
                StringComparison.Ordinal);

        var result = AtlasCouncilRecordValidator.Validate(
            "atlas-priority-session-2030-01-02.md",
            record);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "atlas.recommendation.possibility-unmapped");
    }

    [Fact]
    public void Participant_fields_require_canonical_seat_and_count_structure_without_judging_public_credit()
    {
        var record = SyntheticRecord(
                AtlasCouncilRecordValidator.SessionHeldStatus,
                sessionComplete: true)
            .Replace(
                "Seats present (seat + count, no names by default)",
                "Seats present (participant names)",
                StringComparison.Ordinal);

        var result = AtlasCouncilRecordValidator.Validate(
            "atlas-priority-session-2030-01-02.md",
            record);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.session.field-missing");

        var humanConfirmedPublicCredit = SyntheticRecord(
                AtlasCouncilRecordValidator.SessionHeldStatus,
                sessionComplete: true)
            .Replace(
                "| Public-credit choice confirmed | confirmed |",
                "| Public-credit choice confirmed | confirmed |\n| Public name (explicitly credited) | Synthetic public-credit label |",
                StringComparison.Ordinal);
        var publicCreditResult = AtlasCouncilRecordValidator.Validate(
            "atlas-priority-session-2030-01-02.md",
            humanConfirmedPublicCredit);

        Assert.True(publicCreditResult.IsValid, Describe(publicCreditResult));
    }

    [Fact]
    public void Held_or_frozen_record_must_explicitly_record_absent_seats()
    {
        var record = SyntheticRecord(
                AtlasCouncilRecordValidator.SessionHeldStatus,
                sessionComplete: true)
            .Replace(
                "| Seats absent | AAC/SLP: 1 absent; hold retained |",
                "| Seats absent | |",
                StringComparison.Ordinal);

        var result = AtlasCouncilRecordValidator.Validate(
            "atlas-priority-session-2030-01-02.md",
            record);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.holds.absent-seats-unrecorded");
    }

    [Fact]
    public void Recommendation_table_must_keep_the_explicit_holds_column()
    {
        var record = SyntheticRecord(AtlasCouncilRecordValidator.UnrunStatus)
            .Replace(" | Holds / seats still needed", string.Empty, StringComparison.Ordinal);

        var result = AtlasCouncilRecordValidator.Validate(
            "atlas-priority-session-2030-01-02.md",
            record);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.holds.recommendation-column-missing");

        var prematureRecommendation = SyntheticRecord(AtlasCouncilRecordValidator.UnrunStatus)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace(
                "|---|---|---|---|---|---|\n| | | | | | |",
                "|---|---|---|---|---|---|\n| 1 | N-SYNTHETIC · synthetic possibility | Synthetic reason \\| alternative | Synthetic proof | | No dissent recorded |",
                StringComparison.Ordinal);
        var prematureResult = AtlasCouncilRecordValidator.Validate(
            "atlas-priority-session-2030-01-02.md",
            prematureRecommendation);

        Assert.Contains(
            prematureResult.Issues,
            issue => issue.Code == "atlas.holds.recommendation-value-missing");
        Assert.Contains(
            prematureResult.Issues,
            issue => issue.Code == "atlas.lifecycle.recommendation-before-session");

        var shiftedRecommendation = SyntheticRecord(AtlasCouncilRecordValidator.UnrunStatus)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace(
                "|---|---|---|---|---|---|\n| | | | | | |",
                "|---|---|---|---|---|---|\n| 1 | N-SYNTHETIC · synthetic possibility | Synthetic reason | shifted cell | Synthetic proof | hold retained | No dissent recorded |",
                StringComparison.Ordinal);
        var shiftedResult = AtlasCouncilRecordValidator.Validate(
            "atlas-priority-session-2030-01-02.md",
            shiftedRecommendation);

        Assert.Contains(
            shiftedResult.Issues,
            issue => issue.Code == "atlas.table.recommendation-width");
    }

    [Fact]
    public void Feasibility_cannot_precede_record_freeze()
    {
        var result = AtlasCouncilRecordValidator.Validate(
            "atlas-priority-session-2030-01-02.md",
            SyntheticRecord(
                AtlasCouncilRecordValidator.SessionHeldStatus,
                sessionComplete: true,
                includeFeasibility: true));

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.lifecycle.feasibility-before-freeze");
        Assert.Contains(result.Issues, issue => issue.Code == "atlas.lifecycle.feasibility-before-recommendation");

        var frozenWithoutRecommendation = AtlasCouncilRecordValidator.Validate(
            "atlas-priority-session-2030-01-02.md",
            SyntheticRecord(
                AtlasCouncilRecordValidator.FrozenStatus,
                sessionComplete: true,
                freezeComplete: true,
                includeFeasibility: true));

        Assert.DoesNotContain(
            frozenWithoutRecommendation.Issues,
            issue => issue.Code == "atlas.lifecycle.feasibility-before-freeze");
        Assert.Contains(
            frozenWithoutRecommendation.Issues,
            issue => issue.Code == "atlas.lifecycle.feasibility-before-recommendation");

        var frozenWithHoldsOnly = SyntheticRecord(
                AtlasCouncilRecordValidator.FrozenStatus,
                sessionComplete: true,
                freezeComplete: true,
                includeFeasibility: true)
            .Replace(
                "| | | | | | |",
                "| | | | | None recorded | |",
                StringComparison.Ordinal);
        var holdsOnlyResult = AtlasCouncilRecordValidator.Validate(
            "atlas-priority-session-2030-01-02.md",
            frozenWithHoldsOnly);

        Assert.Contains(
            holdsOnlyResult.Issues,
            issue => issue.Code == "atlas.recommendation.identity-value-missing");
        Assert.Contains(
            holdsOnlyResult.Issues,
            issue => issue.Code == "atlas.lifecycle.feasibility-before-recommendation");
    }

    [Fact]
    public void Product_owner_disposition_cannot_precede_feasibility()
    {
        var result = AtlasCouncilRecordValidator.Validate(
            "atlas-priority-session-2030-01-02.md",
            SyntheticRecord(
                AtlasCouncilRecordValidator.FrozenStatus,
                sessionComplete: true,
                freezeComplete: true,
                includeDisposition: true));

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.lifecycle.disposition-before-feasibility");
    }

    [Fact]
    public void Feasibility_and_disposition_require_complete_rows_and_exact_predecessor_keys()
    {
        var valid = SyntheticRecord(
            AtlasCouncilRecordValidator.FrozenStatus,
            sessionComplete: true,
            freezeComplete: true,
            includeRecommendation: true,
            includeFeasibility: true,
            includeDisposition: true);
        var feasibilityCells = new[]
        {
            "Synthetic possibility",
            "Existing component",
            "Bounded synthetic slice",
            "None",
            "Machine and later human evidence",
            "Small",
            "None recorded",
        };
        var dispositionCells = new[]
        {
            "Synthetic possibility",
            "Defer · 2030-01-04",
            "No implementation",
            "Await owner evidence",
            "AAC/SLP",
            "Separate protected review",
        };

        Assert.True(
            AtlasCouncilRecordValidator.Validate(
                "atlas-priority-session-2030-01-02.md",
                valid).IsValid,
            "The complete linked synthetic record must remain valid.");

        AssertEveryCellIsRequired(valid, feasibilityCells, "atlas.feasibility.row-incomplete");
        AssertEveryCellIsRequired(valid, dispositionCells, "atlas.disposition.row-incomplete");

        var mismatchedFeasibility = valid.Replace(
            "| Synthetic possibility | Existing component |",
            "| Different possibility | Existing component |",
            StringComparison.Ordinal);
        var feasibilityResult = AtlasCouncilRecordValidator.Validate(
            "atlas-priority-session-2030-01-02.md",
            mismatchedFeasibility);

        Assert.Contains(
            feasibilityResult.Issues,
            issue => issue.Code == "atlas.feasibility.recommendation-mismatch");

        var mismatchedDisposition = valid.Replace(
            "| Synthetic possibility | Defer · 2030-01-04 |",
            "| Different possibility | Defer · 2030-01-04 |",
            StringComparison.Ordinal);
        var dispositionResult = AtlasCouncilRecordValidator.Validate(
            "atlas-priority-session-2030-01-02.md",
            mismatchedDisposition);

        Assert.Contains(
            dispositionResult.Issues,
            issue => issue.Code == "atlas.disposition.feasibility-mismatch");
    }

    [Fact]
    public void Started_feasibility_and_disposition_sections_require_exact_one_to_one_coverage()
    {
        var valid = TwoRecommendationRecord();
        const string FirstFeasibility =
            "| Synthetic possibility | Existing component | Bounded synthetic slice | None | Machine and later human evidence | Small | None recorded |";
        const string SecondFeasibility =
            "| Second possibility | Second component | Second bounded slice | None | Machine and later human evidence | Small | None recorded |";
        const string FirstDisposition =
            "| Synthetic possibility | Defer · 2030-01-04 | No implementation | Await owner evidence | AAC/SLP | Separate protected review |";
        const string SecondDisposition =
            "| Second possibility | Defer · 2030-01-05 | No implementation | Await owner evidence | Accessibility/AT | Separate protected review |";

        Assert.True(
            AtlasCouncilRecordValidator.Validate(
                "atlas-priority-session-2030-01-02.md",
                valid).IsValid,
            "The complete two-recommendation chain must remain valid.");

        var missingFeasibility = AtlasCouncilRecordValidator.Validate(
            "atlas-priority-session-2030-01-02.md",
            valid.Replace(SecondFeasibility, string.Empty, StringComparison.Ordinal));
        Assert.Contains(
            missingFeasibility.Issues,
            issue => issue.Code == "atlas.feasibility.coverage-incomplete");

        var duplicateFeasibility = AtlasCouncilRecordValidator.Validate(
            "atlas-priority-session-2030-01-02.md",
            valid.Replace(
                FirstFeasibility,
                $"{FirstFeasibility}{Environment.NewLine}{FirstFeasibility}",
                StringComparison.Ordinal));
        Assert.Contains(
            duplicateFeasibility.Issues,
            issue => issue.Code == "atlas.feasibility.key-duplicate");

        var missingDisposition = AtlasCouncilRecordValidator.Validate(
            "atlas-priority-session-2030-01-02.md",
            valid.Replace(SecondDisposition, string.Empty, StringComparison.Ordinal));
        Assert.Contains(
            missingDisposition.Issues,
            issue => issue.Code == "atlas.disposition.coverage-incomplete");

        var duplicateDisposition = AtlasCouncilRecordValidator.Validate(
            "atlas-priority-session-2030-01-02.md",
            valid.Replace(
                FirstDisposition,
                $"{FirstDisposition}{Environment.NewLine}{FirstDisposition}",
                StringComparison.Ordinal));
        Assert.Contains(
            duplicateDisposition.Issues,
            issue => issue.Code == "atlas.disposition.key-duplicate");

        var duplicateRecommendation = AtlasCouncilRecordValidator.Validate(
            "atlas-priority-session-2030-01-02.md",
            valid.Replace("Second possibility", "Synthetic possibility", StringComparison.Ordinal));
        Assert.Contains(
            duplicateRecommendation.Issues,
            issue => issue.Code == "atlas.recommendation.possibility-duplicate");
    }

    [Fact]
    public void Governed_tables_require_exact_single_headers_and_separators()
    {
        var valid = SyntheticRecord(
                AtlasCouncilRecordValidator.FrozenStatus,
                sessionComplete: true,
                freezeComplete: true,
                includeRecommendation: true,
                includeFeasibility: true,
                includeDisposition: true)
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        var tables = new[]
        {
            new
            {
                Header = "| Need ID | Atlas entry / existing capability / new composition / no match | Why it fits or fails to fit | Likely lane (`G`, `A`, `R`, uncertain) | Possibly implicated seats |",
                Separator = "|---|---|---|---|---|",
                HeaderCode = "atlas.mapping.header",
                DuplicateHeaderCode = "atlas.mapping.header-duplicate",
                SeparatorCode = "atlas.mapping.separator",
            },
            new
            {
                Header = "| Order, if any | Need ID and mapped possibility | Why now, in council members' words | First proof requested | Holds / seats still needed | Dissent or alternative |",
                Separator = "|---|---|---|---|---|---|",
                HeaderCode = "atlas.recommendation.header",
                DuplicateHeaderCode = "atlas.recommendation.header-duplicate",
                SeparatorCode = "atlas.recommendation.separator",
            },
            new
            {
                Header = "| Recommended possibility | Reusable engine/capability | Smallest bounded slice | Dependencies and migrations | Required automated and human evidence | Effort/risk range | Conflicts with ADR, plan, or gate |",
                Separator = "|---|---|---|---|---|---|---|",
                HeaderCode = "atlas.feasibility.header",
                DuplicateHeaderCode = "atlas.feasibility.header-duplicate",
                SeparatorCode = "atlas.feasibility.separator",
            },
            new
            {
                Header = "| Recommendation | Disposition and date | Exact bounded scope | Reason | Outstanding seats/gates | Evidence required before completion |",
                Separator = "|---|---|---|---|---|---|",
                HeaderCode = "atlas.disposition.header",
                DuplicateHeaderCode = "atlas.disposition.header-duplicate",
                SeparatorCode = "atlas.disposition.separator",
            },
        };

        foreach (var table in tables)
        {
            var block = $"{table.Header}\n{table.Separator}";
            var malformedHeader = table.Header.Replace("| ", "| Altered ", StringComparison.Ordinal);
            var headerResult = AtlasCouncilRecordValidator.Validate(
                "atlas-priority-session-2030-01-02.md",
                valid.Replace(
                    block,
                    $"{malformedHeader}\n{table.Separator}",
                    StringComparison.Ordinal));
            Assert.Contains(headerResult.Issues, issue => issue.Code == table.HeaderCode);

            var duplicateHeaderResult = AtlasCouncilRecordValidator.Validate(
                "atlas-priority-session-2030-01-02.md",
                valid.Replace(
                    block,
                    $"{table.Header}\n{table.Header}\n{table.Separator}",
                    StringComparison.Ordinal));
            Assert.Contains(
                duplicateHeaderResult.Issues,
                issue => issue.Code == table.DuplicateHeaderCode);

            var malformedSeparators = new[]
            {
                table.Separator.Replace("|---|", "|:---|", StringComparison.Ordinal),
                table.Separator.Replace("---", "--", StringComparison.Ordinal),
            };
            foreach (var malformedSeparator in malformedSeparators)
            {
                var separatorResult = AtlasCouncilRecordValidator.Validate(
                    "atlas-priority-session-2030-01-02.md",
                    valid.Replace(
                        block,
                        $"{table.Header}\n{malformedSeparator}",
                        StringComparison.Ordinal));
                Assert.Contains(separatorResult.Issues, issue => issue.Code == table.SeparatorCode);
            }

            var duplicateSeparatorResult = AtlasCouncilRecordValidator.Validate(
                "atlas-priority-session-2030-01-02.md",
                valid.Replace(
                    block,
                    $"{table.Header}\n{table.Separator}\n{table.Separator}",
                    StringComparison.Ordinal));
            Assert.Contains(
                duplicateSeparatorResult.Issues,
                issue => issue.Code == table.SeparatorCode);
        }
    }

    [Fact]
    public void Frozen_status_requires_rechecked_absent_seat_holds_and_complete_freeze_fields()
    {
        var record = SyntheticRecord(
                AtlasCouncilRecordValidator.FrozenStatus,
                sessionComplete: true,
                freezeComplete: true)
            .Replace(
                "| Applicable absent-seat holds rechecked and retained | AAC/SLP hold retained |",
                "| Applicable absent-seat holds rechecked and retained | [not run] |",
                StringComparison.Ordinal);

        var result = AtlasCouncilRecordValidator.Validate(
            "atlas-priority-session-2030-01-02.md",
            record);

        Assert.Contains(result.Issues, issue => issue.Code == "atlas.freeze.field-pending");
        Assert.Contains(result.Issues, issue => issue.Code == "atlas.holds.freeze-recheck-missing");
    }

    [Fact]
    public void Refusals_do_not_echo_untrusted_record_values()
    {
        const string Canary = "PRIVATE SYNTHETIC PARTICIPANT CANARY";
        var result = AtlasCouncilRecordValidator.Validate(
            "atlas-priority-session-2030-01-02.md",
            SyntheticRecord(Canary));

        Assert.NotEmpty(result.Issues);
        Assert.DoesNotContain(result.Issues, issue => issue.Message.Contains(Canary, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("atlas-priority-session.md", "atlas.file-name")]
    [InlineData("atlas-priority-session-2030-02-30.md", "atlas.file-date")]
    [InlineData("atlas-priority-session-2030-1-2.md", "atlas.file-name")]
    public void Only_real_dated_copy_names_enter_the_lifecycle(string fileName, string expectedCode)
    {
        var result = AtlasCouncilRecordValidator.Validate(
            fileName,
            SyntheticRecord(AtlasCouncilRecordValidator.UnrunStatus));

        Assert.Contains(result.Issues, issue => issue.Code == expectedCode);
    }

    private static string TwoRecommendationRecord()
    {
        const string MappingHeading = "### Need-to-possibility mapping — complete only after need capture";
        const string FirstMapping =
            "| N-SYNTHETIC | Synthetic possibility | Synthetic fit record | G | AAC/SLP hold retained |";
        const string FirstRecommendation =
            "| 1 | N-SYNTHETIC · Synthetic possibility | Synthetic council reason | Synthetic proof request | AAC/SLP hold retained | None recorded |";
        const string FirstFeasibility =
            "| Synthetic possibility | Existing component | Bounded synthetic slice | None | Machine and later human evidence | Small | None recorded |";
        const string FirstDisposition =
            "| Synthetic possibility | Defer · 2030-01-04 | No implementation | Await owner evidence | AAC/SLP | Separate protected review |";
        const string SecondNeedCard = """
            | Prompt | Council member's words |
            |---|---|
            | Need ID | `N-SECOND` |
            | Recurring teacher work or learner-facing barrier | second synthetic words |
            | Who encounters it (generic role/context only) | second synthetic words |
            | How often it occurs | second synthetic words |
            | Current workaround and its time/material cost | second synthetic words |
            | What a useful paper/offline artifact would make possible | second synthetic words |
            | What must remain under teacher control | second synthetic words |
            | Unacceptable failure or harm | second synthetic words |
            | First classroom proof that would earn trust | second synthetic words |
            | Seat speaking | second synthetic words |

            """;

        return SyntheticRecord(
                AtlasCouncilRecordValidator.FrozenStatus,
                sessionComplete: true,
                freezeComplete: true,
                includeRecommendation: true,
                includeFeasibility: true,
                includeDisposition: true)
            .Replace(
                MappingHeading,
                SecondNeedCard + MappingHeading,
                StringComparison.Ordinal)
            .Replace(
                FirstMapping,
                $"{FirstMapping}{Environment.NewLine}| N-SECOND | Second possibility | Second fit record | G | Accessibility/AT hold retained |",
                StringComparison.Ordinal)
            .Replace(
                FirstRecommendation,
                $"{FirstRecommendation}{Environment.NewLine}| 2 | N-SECOND · Second possibility | Second council reason | Second proof request | Accessibility/AT hold retained | None recorded |",
                StringComparison.Ordinal)
            .Replace(
                FirstFeasibility,
                $"{FirstFeasibility}{Environment.NewLine}| Second possibility | Second component | Second bounded slice | None | Machine and later human evidence | Small | None recorded |",
                StringComparison.Ordinal)
            .Replace(
                FirstDisposition,
                $"{FirstDisposition}{Environment.NewLine}| Second possibility | Defer · 2030-01-05 | No implementation | Await owner evidence | Accessibility/AT | Separate protected review |",
                StringComparison.Ordinal);
    }

    private static string SyntheticRecord(
        string status,
        bool sessionComplete = false,
        bool freezeComplete = false,
        bool includeMapping = false,
        bool includeRecommendation = false,
        bool includeFeasibility = false,
        bool includeDisposition = false,
        string decisionProcedure = "Synthetic procedure record; quorum recorded by the cohort.",
        bool needCardComplete = false)
    {
        var pending = "[not run]";
        var sessionDate = sessionComplete ? "2030-01-02 · 60 minutes" : pending;
        var repository = sessionComplete ? "0000000 · synthetic build" : pending;
        var facilitator = sessionComplete ? "non-voting facilitator seat" : pending;
        var productOwner = sessionComplete ? "present" : pending;
        var presentSeats = sessionComplete ? "general educator: 2" : pending;
        var absentSeats = sessionComplete ? "AAC/SLP: 1 absent; hold retained" : pending;
        var materials = sessionComplete ? "synthetic application and staged packet" : pending;
        var confirmation = sessionComplete ? "confirmed" : pending;
        var procedure = sessionComplete ? decisionProcedure : "[not enacted / not run]";
        var sessionOccurred = sessionComplete ? "confirmed" : pending;
        var freezeValue = freezeComplete ? "confirmed" : pending;
        var frozenRecord = freezeComplete
            ? "2030-01-03; docs/council/synthetic.md; 0000000; record 1"
            : pending;
        var hasNeedCardContent = sessionComplete || needCardComplete;
        var needId = hasNeedCardContent ? "`N-SYNTHETIC`" : "`N-__`";
        var needValue = hasNeedCardContent ? "synthetic council words" : string.Empty;
        var mappingRow = includeMapping || includeRecommendation
            ? "| N-SYNTHETIC | Synthetic possibility | Synthetic fit record | G | AAC/SLP hold retained |"
            : "| | | | | |";
        var recommendationRow = includeRecommendation
            ? "| 1 | N-SYNTHETIC · Synthetic possibility | Synthetic council reason | Synthetic proof request | AAC/SLP hold retained | None recorded |"
            : "| | | | | | |";
        var feasibilityRow = includeFeasibility
            ? "| Synthetic possibility | Existing component | Bounded synthetic slice | None | Machine and later human evidence | Small | None recorded |"
            : "| | | | | | | |";
        var dispositionRow = includeDisposition
            ? "| Synthetic possibility | Defer · 2030-01-04 | No implementation | Await owner evidence | AAC/SLP | Separate protected review |"
            : "| [not run] | [not decided] | | | | |";

        return $$"""
            # Atlas 2.0 council priority session — synthetic fixture

            **Status:** {{status}}

            ### Session header

            | Field | Record |
            |---|---|
            | Session date and duration | {{sessionDate}} |
            | Repository commit/build inspected | {{repository}} |
            | Facilitator (non-voting) | {{facilitator}} |
            | Product owner present? | {{productOwner}} |
            | Seats present (seat + count, no names by default) | {{presentSeats}} |
            | Seats absent | {{absentSeats}} |
            | Materials actually inspected | {{materials}} |
            | Withdrawal right confirmed | {{confirmation}} |
            | Compensation terms confirmed | {{confirmation}} |
            | Note-taking choice confirmed | {{confirmation}} |
            | Public-credit choice confirmed | {{confirmation}} |
            | Decision procedure and quorum rule applied (exact governing record) | {{procedure}} |

            ### Need card — complete before opening the atlas

            | Prompt | Council member's words |
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

            | Order, if any | Need ID and mapped possibility | Why now, in council members' words | First proof requested | Holds / seats still needed | Dissent or alternative |
            |---|---|---|---|---|---|
            {{recommendationRow}}

            - **Needs deliberately not advanced, and why:**
            - **Useful possibilities with no atlas match:**
            - **Questions the session could not answer:**
            - **Corrections members made during read-back:**
            - **Whether members reached consensus, split, or made no ordering:**

            ## Participant review and council-record freeze

            | Field | Record |
            |---|---|
            | Session occurred; dated copy status changed from `UNRUN` | {{sessionOccurred}} |
            | Participant read-back/review completed (seat + count, no names by default) | {{freezeComplete switch { true => "general educator: 2", false => pending }}} |
            | Corrections and dissent incorporated without facilitator rewriting | {{freezeValue}} |
            | Applicable absent-seat holds rechecked and retained | {{freezeComplete switch { true => "AAC/SLP hold retained", false => pending }}} |
            | Council record frozen (date, repository path, commit, and record version) | {{frozenRecord}} |

            ## Separate feasibility appendix — completed after the council record is frozen

            | Recommended possibility | Reusable engine/capability | Smallest bounded slice | Dependencies and migrations | Required automated and human evidence | Effort/risk range | Conflicts with ADR, plan, or gate |
            |---|---|---|---|---|---|---|
            {{feasibilityRow}}

            ## Product-owner disposition — intentionally blank in the template

            | Recommendation | Disposition and date | Exact bounded scope | Reason | Outstanding seats/gates | Evidence required before completion |
            |---|---|---|---|---|---|
            {{dispositionRow}}

            ## Completion check

            Synthetic fixture only; this text is not a council finding or priority.
            """;
    }

    private static void AssertEveryCellIsRequired(
        string validRecord,
        string[] completeCells,
        string expectedCode)
    {
        var completeRow = $"| {string.Join(" | ", completeCells)} |";
        for (var missingIndex = 0; missingIndex < completeCells.Length; missingIndex++)
        {
            var incompleteCells = completeCells
                .Select((value, index) => index == missingIndex ? string.Empty : value);
            var incompleteRecord = validRecord.Replace(
                completeRow,
                $"| {string.Join(" | ", incompleteCells)} |",
                StringComparison.Ordinal);
            var result = AtlasCouncilRecordValidator.Validate(
                "atlas-priority-session-2030-01-02.md",
                incompleteRecord);

            Assert.True(
                result.Issues.Any(issue => issue.Code == expectedCode),
                $"Cell {missingIndex + 1} was not required.{Environment.NewLine}{Describe(result)}");
        }
    }

    private static string Describe(AtlasCouncilRecordValidation result)
        => string.Join(Environment.NewLine, result.Issues.Select(issue => $"{issue.Code}: {issue.Message}"));
}
