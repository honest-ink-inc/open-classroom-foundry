// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;

namespace Foundry.Tools.AtlasCouncilRecords;

public enum AtlasCouncilRecordStatus
{
    Unrun,
    SessionHeldReviewPending,
    CouncilRecordFrozen,
}

public sealed record AtlasCouncilRecordIssue(string Code, string Message);

public sealed record AtlasCouncilRecordValidation(
    AtlasCouncilRecordStatus? Status,
    IReadOnlyList<AtlasCouncilRecordIssue> Issues)
{
    public bool IsValid => Issues.Count == 0;
}

/// <summary>
/// Checks the documentary mechanics already fixed by the canonical Atlas 2.0
/// priority-session packet. It does not decide whether a session occurred or
/// whether any participant, reviewer, finding, or disposition is authentic.
/// </summary>
public static class AtlasCouncilRecordValidator
{
    public const string AuthorityBoundary =
        "This validator checks record mechanics only. It does not infer quorum, score needs, rank or recommend possibilities, authenticate participants, select a priority, or perform a protected-seat act. Whether a public-credit identity may appear remains a human confirmation outside this validator.";

    public const string UnrunStatus = "UNRUN";
    public const string SessionHeldStatus = "SESSION HELD — REVIEW PENDING";
    public const string FrozenStatus = "COUNCIL RECORD FROZEN";

    private const string FilePrefix = "atlas-priority-session-";
    private const string FileSuffix = ".md";
    private const string PresentSeatsField = "Seats present (seat + count, no names by default)";
    private const string AbsentSeatsField = "Seats absent";
    private const string SessionOccurredField = "Session occurred; dated copy status changed from `UNRUN`";
    private const string ParticipantReviewField =
        "Participant read-back/review completed (seat + count, no names by default)";
    private const string AbsentSeatHoldsField = "Applicable absent-seat holds rechecked and retained";
    private const string FrozenRecordField =
        "Council record frozen (date, repository path, commit, and record version)";
    private const string RecommendationIdentityColumn = "Need ID and mapped possibility";
    private const string RecommendationHoldsColumn = "Holds / seats still needed";

    private static readonly string[] NeedCardHeader =
    [
        "Prompt",
        "Council member's words",
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
        "Why now, in council members' words",
        "First proof requested",
        RecommendationHoldsColumn,
        "Dissent or alternative",
    ];

    private static readonly string[] RecommendationSupplementalFields =
    [
        "Needs deliberately not advanced, and why",
        "Useful possibilities with no atlas match",
        "Questions the session could not answer",
        "Corrections members made during read-back",
        "Whether members reached consensus, split, or made no ordering",
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
        "## Participant review and council-record freeze",
        "## Separate feasibility appendix — completed after the council record is frozen",
        "## Product-owner disposition — intentionally blank in the template",
        "## Completion check",
    ];

    private static readonly string[] RequiredSessionFields =
    [
        "Session date and duration",
        "Repository commit/build inspected",
        "Facilitator (non-voting)",
        "Product owner present?",
        PresentSeatsField,
        AbsentSeatsField,
        "Materials actually inspected",
        "Withdrawal right confirmed",
        "Compensation terms confirmed",
        "Note-taking choice confirmed",
        "Public-credit choice confirmed",
        "Decision procedure and quorum rule applied (exact governing record)",
    ];

    private static readonly string[] RequiredFreezeFields =
    [
        SessionOccurredField,
        ParticipantReviewField,
        "Corrections and dissent incorporated without facilitator rewriting",
        AbsentSeatHoldsField,
        FrozenRecordField,
    ];

    public static AtlasCouncilRecordValidation Validate(string fileName, string markdown)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(markdown);

        var issues = new List<AtlasCouncilRecordIssue>();
        ValidateDatedFileName(fileName, issues);

        var lines = markdown
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
        var status = ReadStatus(lines, issues);
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
        var freezeFields = ReadFieldTable(
            lines,
            orderedHeadingIndexes[4],
            orderedHeadingIndexes[5],
            issues,
            "record freeze",
            "freeze");

        RequireFields(sessionFields, RequiredSessionFields, issues, "session");
        RequireFields(freezeFields, RequiredFreezeFields, issues, "freeze");
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
            issues);
        var recommendationSupplemental = ValidateRecommendationSupplementalFields(
            lines,
            orderedHeadingIndexes[3],
            orderedHeadingIndexes[4],
            issues);
        var feasibility = ValidateDataRows(
            lines,
            orderedHeadingIndexes[5],
            orderedHeadingIndexes[6],
            FeasibilityHeader,
            "feasibility",
            "atlas.feasibility.row-incomplete",
            recommendations.MappedPossibilities,
            "atlas.feasibility.recommendation-mismatch",
            "council-recommended possibility",
            "atlas.feasibility.key-duplicate",
            "atlas.feasibility.coverage-incomplete",
            issues);
        var productOwnerDisposition = ValidateDataRows(
            lines,
            orderedHeadingIndexes[6],
            orderedHeadingIndexes[7],
            DispositionHeader,
            "product-owner disposition",
            "atlas.disposition.row-incomplete",
            feasibility.LinkedKeys,
            "atlas.disposition.feasibility-mismatch",
            "feasibility recommendation",
            "atlas.disposition.key-duplicate",
            "atlas.disposition.coverage-incomplete",
            issues);

        if (status is AtlasCouncilRecordStatus.SessionHeldReviewPending
            or AtlasCouncilRecordStatus.CouncilRecordFrozen)
        {
            RequireCompletedFields(sessionFields, RequiredSessionFields, issues, "session");
            RequireSubstantiveValue(
                sessionFields,
                AbsentSeatsField,
                issues,
                "atlas.holds.absent-seats-unrecorded",
                "The dated record must explicitly record absent seats, including an explicit none when applicable.");
            RequireSubstantiveValue(
                freezeFields,
                SessionOccurredField,
                issues,
                "atlas.session.occurrence-missing",
                "A held or frozen status requires the record to say that the session occurred and left UNRUN.");
        }

        var freezeComplete = RequiredFreezeFields.All(field =>
            freezeFields.TryGetValue(field, out var value) && IsSubstantive(value));

        if (status is AtlasCouncilRecordStatus.Unrun
            && (sessionFields.Values.Any(IsSubstantive)
                || needCards.HasContent
                || needMappings.HasContent
                || recommendations.HasContent
                || recommendationSupplemental.HasContent
                || feasibility.HasContent
                || productOwnerDisposition.HasContent
                || freezeFields.Values.Any(IsSubstantive)))
        {
            issues.Add(new(
                "atlas.status.session-mismatch",
                "An UNRUN record cannot contain completed session, need-card, mapping, recommendation, feasibility, disposition, participant-review, or council-record-freeze content."));
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

        if (status is AtlasCouncilRecordStatus.CouncilRecordFrozen)
        {
            RequireCompletedFields(freezeFields, RequiredFreezeFields, issues, "freeze");
            RequireSubstantiveValue(
                freezeFields,
                AbsentSeatHoldsField,
                issues,
                "atlas.holds.freeze-recheck-missing",
                "A frozen record must explicitly retain or clear every absent-seat hold.");
        }
        else if (freezeFields.TryGetValue(FrozenRecordField, out var frozenValue)
            && IsSubstantive(frozenValue))
        {
            issues.Add(new(
                "atlas.status.freeze-mismatch",
                "The record says it is frozen while its status has not reached COUNCIL RECORD FROZEN."));
        }

        if (feasibility.HasContent
            && (status is not AtlasCouncilRecordStatus.CouncilRecordFrozen || !freezeComplete))
        {
            issues.Add(new(
                "atlas.lifecycle.feasibility-before-freeze",
                "Feasibility content is present before the council record is mechanically frozen."));
        }

        if (feasibility.HasContent && recommendations.MappedPossibilities.Count == 0)
        {
            issues.Add(new(
                "atlas.lifecycle.feasibility-before-recommendation",
                "Feasibility content is present without a preceding recorded council recommendation."));
        }

        if (productOwnerDisposition.HasContent
            && (status is not AtlasCouncilRecordStatus.CouncilRecordFrozen || !freezeComplete))
        {
            issues.Add(new(
                "atlas.lifecycle.disposition-before-freeze",
                "Product-owner disposition content is present before the council record is mechanically frozen."));
        }

        if (productOwnerDisposition.HasContent && feasibility.LinkedKeys.Count == 0)
        {
            issues.Add(new(
                "atlas.lifecycle.disposition-before-feasibility",
                "Product-owner disposition content is present without a preceding feasibility record."));
        }

        return Result(status, issues);
    }

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
        var values = lines
            .Select(line => line.Trim().Replace("**", string.Empty, StringComparison.Ordinal))
            .Where(line => line.StartsWith("Status:", StringComparison.Ordinal))
            .Select(line => line["Status:".Length..].Trim())
            .ToArray();

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
            SessionHeldStatus => AtlasCouncilRecordStatus.SessionHeldReviewPending,
            FrozenStatus => AtlasCouncilRecordStatus.CouncilRecordFrozen,
            _ => UnknownStatus(issues),
        };
    }

    private static AtlasCouncilRecordStatus? UnknownStatus(List<AtlasCouncilRecordIssue> issues)
    {
        issues.Add(new(
            "atlas.status.unknown",
            $"Status must be exactly {UnrunStatus}, {SessionHeldStatus}, or {FrozenStatus}."));
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
                    "Each need card must begin with the canonical Prompt / Council member's words header."));
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
                "The need-card section must retain its canonical Prompt / Council member's words table."));
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
            && !string.Equals(candidate, "N-__", StringComparison.Ordinal);
    }

    private static string NormalizeCell(string value)
        => value.Trim().Trim('`').Trim();

    private static RecommendationValidation ValidateRecommendationHolds(
        string[] lines,
        int startHeading,
        int endHeading,
        NeedMappingValidation needMappings,
        List<AtlasCouncilRecordIssue> issues)
    {
        var rows = ReadTableRows(lines, startHeading, endHeading);
        var mappedPossibilities = new HashSet<string>(StringComparer.Ordinal);
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

        if (firstRow is null || firstRow.Count != RecommendationHeader.Length)
        {
            issues.Add(new(
                "atlas.table.recommendation-width",
                "The council recommendation table must retain its canonical six-column shape."));
        }

        if (!hasCanonicalScaffold || holdsIndex < 0 || recommendationIdentityIndex < 0)
        {
            return new RecommendationValidation(hasContent, mappedPossibilities);
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
                        "Every council recommendation row must retain the canonical six-column shape."));
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
                    "Every started council recommendation row must complete the mapped identity, reason, first proof, holds, and dissent cells; order remains optional."));
                incompleteRowReported = true;
            }

            var exactMapping = false;
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
                    out var recommendationNeedId,
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
                mappedPossibilities.Add(mappedPossibility);
            }
        }

        return new RecommendationValidation(hasContent, mappedPossibilities);
    }

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
                hasContent |= IsSubstantive(line[prefix.Length..]);

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

                    hasContent |= IsSubstantive(continuation);
                }

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

        return new RecommendationSupplementalValidation(hasContent);
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
        var current = new System.Text.StringBuilder();
        for (var index = 0; index < content.Length; index++)
        {
            var character = content[index];
            if (character == '\\' && index + 1 < content.Length)
            {
                current.Append(character);
                current.Append(content[++index]);
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
            && !string.Equals(candidate, "[not run]", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(candidate, "[not enacted / not run]", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(candidate, "[not decided]", StringComparison.OrdinalIgnoreCase);
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
        IReadOnlySet<string> MappedPossibilities);

    private sealed record RecommendationSupplementalValidation(bool HasContent);

    private sealed record DataRowValidation(
        bool HasContent,
        IReadOnlySet<string> LinkedKeys);
}
