// SPDX-License-Identifier: GPL-3.0-or-later
using System.Buffers;
using System.Globalization;
using System.Text;

namespace Foundry.Domain;

/// <summary>
/// The truth-bearing kinds of constitution requirement 10: values the model may
/// never alter, verified by deterministic comparison — not by judgment.
/// </summary>
public enum LockedFieldKind
{
    Date,
    Number,
    ProperName,
    Negation,
    Quotation,
    Citation,
    Unit,
    Url,
    Condition,
    RightsMetadata,
}

/// <summary>A value that must survive generation verbatim.</summary>
public sealed record LockedField(LockedFieldKind Kind, string ExactValue);

public static class LockedFieldValidator
{
    public static string FormatInventorySummary(IReadOnlyList<LockedField> lockedFields)
    {
        ArgumentNullException.ThrowIfNull(lockedFields);

        var summary = new StringBuilder(
            "Fact-lock summary (source inventory only; not language or specialist review): ");
        summary.Append(lockedFields.Count).Append(lockedFields.Count == 1 ? " declaration" : " declarations");
        for (var index = 0; index < lockedFields.Count; index++)
        {
            var field = lockedFields[index];
            summary.Append("; entry ")
                .Append(index + 1)
                .Append('/')
                .Append(lockedFields.Count)
                .Append(" kind=")
                .Append(field.Kind)
                .Append(" utf16-length=")
                .Append(field.ExactValue.Length)
                .Append(" exact=")
                .Append(QuoteExactValue(field.ExactValue));
        }

        return summary.Append('.').ToString();
    }

    /// <summary>
    /// A reviewed inventory may be empty, but the source-review act must be
    /// explicit before approval. This is not language or specialist review.
    /// </summary>
    public static IReadOnlyList<ValidationIssue> ValidateInventoryReview(bool reviewed) =>
        reviewed
            ? []
            :
            [
                ValidationIssue.Blocking(
                    "locked.inventory-review-required",
                    "Review the source and declare every exact fact that must remain unchanged before approval. This confirms the source inventory only, not language or specialist review."),
            ];

    /// <summary>
    /// Every locked value must appear verbatim (ordinal comparison) somewhere in the
    /// document's text. Absence is a blocking issue: a dropped date or a softened
    /// negation is a factual failure, never a stylistic one.
    /// </summary>
    public static IReadOnlyList<ValidationIssue> Validate(ArtifactDocument document, IReadOnlyList<LockedField> lockedFields)
        => Validate(document, lockedFields, []);

    /// <summary>
    /// Validates locked values while excluding only the exact generated notice
    /// texts the caller identifies as non-source summaries. Other teacher-only
    /// notices remain lock-bearing because some modules use them for verified
    /// source lines.
    /// </summary>
    public static IReadOnlyList<ValidationIssue> Validate(
        ArtifactDocument document,
        IReadOnlyList<LockedField> lockedFields,
        IReadOnlyCollection<string> nonLockBearingTeacherOnlyNotices)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(lockedFields);
        ArgumentNullException.ThrowIfNull(nonLockBearingTeacherOnlyNotices);

        var issues = new List<ValidationIssue>();
        var excludedNotices = new HashSet<string>(nonLockBearingTeacherOnlyNotices, StringComparer.Ordinal);
        var lockBearingDocument = new ArtifactDocument(
            [.. document.Nodes.Where(node => node is not TeacherOnlyNotice notice || !excludedNotices.Contains(notice.Text))],
            document.Language);
        var texts = DocumentText.CollectStrings(lockBearingDocument);

        foreach (var field in lockedFields)
        {
            if (string.IsNullOrWhiteSpace(field.ExactValue))
            {
                issues.Add(ValidationIssue.Blocking("locked.empty", $"A locked {field.Kind} has no value to protect."));
                continue;
            }

            if (!texts.Any(text => ContainsExactOccurrence(text, field)))
            {
                issues.Add(ValidationIssue.Blocking(
                    "locked.missing",
                    $"Locked {field.Kind} '{field.ExactValue}' does not appear verbatim in the document."));
            }
        }

        return issues;
    }

    /// <summary>
    /// Every declared locked value must occur in the same aligned row indexes on
    /// both sides. This preserves teacher-declared exact facts without making a
    /// linguistic judgment about how many times a translation may repeat them.
    /// </summary>
    public static IReadOnlyList<ValidationIssue> ValidateAlignedPairs(
        IReadOnlyList<(string SourceText, string? TargetText)> pairs,
        IReadOnlyList<LockedField> lockedFields,
        string issueCode,
        string itemLabel)
    {
        ArgumentNullException.ThrowIfNull(pairs);
        ArgumentNullException.ThrowIfNull(lockedFields);
        ArgumentException.ThrowIfNullOrWhiteSpace(issueCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemLabel);

        var issues = new List<ValidationIssue>();

        foreach (var field in lockedFields)
        {
            if (string.IsNullOrWhiteSpace(field.ExactValue))
            {
                issues.Add(ValidationIssue.Blocking(
                    "locked.empty",
                    $"A locked {field.Kind} has no value to protect in the {itemLabel.ToLowerInvariant()}s."));
                continue;
            }

            var found = false;
            var mismatchIndex = -1;

            for (var i = 0; i < pairs.Count; i++)
            {
                var inSource = ContainsExactOccurrence(pairs[i].SourceText, field);
                var inTarget = ContainsExactOccurrence(pairs[i].TargetText, field);
                found |= inSource || inTarget;

                if (inSource != inTarget && mismatchIndex < 0)
                {
                    mismatchIndex = i;
                }
            }

            if (!found)
            {
                issues.Add(ValidationIssue.Blocking(
                    issueCode,
                    $"Locked {field.Kind} '{field.ExactValue}' does not appear in the source or translation of any {itemLabel.ToLowerInvariant()}."));
            }
            else if (mismatchIndex >= 0)
            {
                issues.Add(ValidationIssue.Blocking(
                    issueCode,
                    $"{itemLabel} {mismatchIndex + 1} does not keep locked {field.Kind} '{field.ExactValue}' on both its source and translation sides; locked values may not move between aligned rows."));
            }
        }

        return issues;
    }

    /// <summary>
    /// Every declared locked value must occur at least once on each language side.
    /// Occurrences may repeat or appear in different rows because paragraph-level
    /// translation need not repeat a fact in the same sentence structure.
    /// </summary>
    public static IReadOnlyList<ValidationIssue> ValidateBilingualPairs(
        IReadOnlyList<(string SourceText, string? TargetText)> pairs,
        IReadOnlyList<LockedField> lockedFields,
        string issueCode,
        string itemLabel)
    {
        ArgumentNullException.ThrowIfNull(pairs);
        ArgumentNullException.ThrowIfNull(lockedFields);
        ArgumentException.ThrowIfNullOrWhiteSpace(issueCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemLabel);

        var issues = new List<ValidationIssue>();

        foreach (var field in lockedFields)
        {
            if (string.IsNullOrWhiteSpace(field.ExactValue))
            {
                issues.Add(ValidationIssue.Blocking(
                    "locked.empty",
                    $"A locked {field.Kind} has no value to protect in the bilingual {itemLabel.ToLowerInvariant()}s."));
                continue;
            }

            var inSource = pairs.Any(pair => ContainsExactOccurrence(pair.SourceText, field));
            var inTarget = pairs.Any(pair => ContainsExactOccurrence(pair.TargetText, field));
            if (!inSource || !inTarget)
            {
                var missingSide = inSource ? "translation" : inTarget ? "source" : "source and translation";
                issues.Add(ValidationIssue.Blocking(
                    issueCode,
                    $"Locked {field.Kind} '{field.ExactValue}' must appear exactly in both languages across the bilingual {itemLabel.ToLowerInvariant()}s; it is missing from the {missingSide}."));
            }
        }

        return issues;
    }

    /// <summary>
    /// Message paragraphs may reorder or repeat facts across their target side,
    /// while explicit semantic fields keep facts in their own paired role.
    /// A declaration absent from every supported source region fails closed.
    /// </summary>
    public static IReadOnlyList<ValidationIssue> ValidateBilingualContent(
        IReadOnlyList<(string SourceText, string? TargetText)> flexiblePairs,
        IReadOnlyList<(string RoleLabel, string SourceText, string? TargetText)> structuredPairs,
        IReadOnlyList<LockedField> lockedFields,
        string issueCode,
        string flexibleLabel)
    {
        ArgumentNullException.ThrowIfNull(flexiblePairs);
        ArgumentNullException.ThrowIfNull(structuredPairs);
        ArgumentNullException.ThrowIfNull(lockedFields);
        ArgumentException.ThrowIfNullOrWhiteSpace(issueCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(flexibleLabel);

        var issues = new List<ValidationIssue>();
        foreach (var field in lockedFields)
        {
            if (string.IsNullOrWhiteSpace(field.ExactValue))
            {
                issues.Add(ValidationIssue.Blocking(
                    "locked.empty",
                    $"A locked {field.Kind} has no value to protect in the bilingual content."));
                continue;
            }

            var inFlexibleSource = flexiblePairs.Any(pair => ContainsExactOccurrence(pair.SourceText, field));
            var sourceRoles = structuredPairs
                .Where(pair => ContainsExactOccurrence(pair.SourceText, field))
                .ToList();
            if (!inFlexibleSource && sourceRoles.Count == 0)
            {
                issues.Add(ValidationIssue.Blocking(
                    issueCode,
                    $"Locked {field.Kind} '{field.ExactValue}' does not appear in supported source {flexibleLabel} or an explicit bilingual role."));
                continue;
            }

            if (inFlexibleSource
                && !flexiblePairs.Any(pair => ContainsExactOccurrence(pair.TargetText, field)))
            {
                issues.Add(ValidationIssue.Blocking(
                    issueCode,
                    $"Locked {field.Kind} '{field.ExactValue}' appears in source {flexibleLabel} but not target {flexibleLabel}; it may move only among the aligned {flexibleLabel}."));
            }

            foreach (var (RoleLabel, SourceText, TargetText) in sourceRoles.Where(role =>
                !ContainsExactOccurrence(role.TargetText, field)))
            {
                issues.Add(ValidationIssue.Blocking(
                    issueCode,
                    $"The {RoleLabel} does not keep locked {field.Kind} '{field.ExactValue}' in its paired target-language field."));
            }
        }

        return issues;
    }

    /// <summary>
    /// Finds a deterministic ordinal occurrence only at a safe semantic-token
    /// boundary. Rune-aware classification rejects supplementary letters and
    /// combining marks; punctuation and symbols that can extend numbers, names,
    /// units, dates, or URLs are not accepted as silent boundaries. No case,
    /// Unicode, punctuation, phrase, or URL normalization is performed.
    /// </summary>
    public static bool ContainsExactOccurrence(string? text, LockedField field)
    {
        ArgumentNullException.ThrowIfNull(field);

        if (string.IsNullOrEmpty(text) || field.ExactValue.Length == 0)
        {
            return false;
        }

        var searchStart = 0;
        while (searchStart <= text.Length - field.ExactValue.Length)
        {
            var index = text.IndexOf(field.ExactValue, searchStart, StringComparison.Ordinal);
            if (index < 0)
            {
                return false;
            }

            var endIndex = index + field.ExactValue.Length;
            if (HasLeftBoundary(text, index, field)
                && HasRightBoundary(text, endIndex, field)
                && !IsParentheticalNumber(text, index, endIndex, field)
                && !IsNumericListMarker(text, index, endIndex, field))
            {
                return true;
            }

            searchStart = index + 1;
        }

        return false;
    }

    private static bool HasLeftBoundary(string text, int index, LockedField field)
    {
        if (index == 0)
        {
            return true;
        }

        if (!TryRuneBefore(text, index, out var previous, out var previousStart))
        {
            return false;
        }

        if (Rune.IsWhiteSpace(previous))
        {
            if (!RequiresSpacedContinuationProtection(field)
                || StartsWithSelfDelimitingPunctuation(field.ExactValue)
                || !TryPreviousNonWhitespace(text, previousStart, out var beforeSpace, out var beforeSpaceStart))
            {
                return true;
            }

            if (StartsWithNumber(field.ExactValue) && IsNumber(beforeSpace))
            {
                return false;
            }

            var beforeCategory = Rune.GetUnicodeCategory(beforeSpace);
            if (beforeCategory == UnicodeCategory.DashPunctuation)
            {
                return IsSafeProseDashOnLeft(text, beforeSpaceStart, beforeSpace);
            }

            if (IsNonDashSemanticOperator(beforeSpace))
            {
                return false;
            }

            return !IsNumericSeparatorBeforeWhitespace(text, beforeSpaceStart, beforeSpace);
        }

        if (Rune.GetUnicodeCategory(previous) == UnicodeCategory.OpenPunctuation)
        {
            return true;
        }

        if (field.Kind == LockedFieldKind.Number
            && StartsWithNumber(field.ExactValue)
            && IsCjkLetter(previous))
        {
            return true;
        }

        if (!IsQuote(previous))
        {
            return false;
        }

        return previousStart == 0
            || TryRuneBefore(text, previousStart, out var beforeQuote, out _)
            && (Rune.IsWhiteSpace(beforeQuote)
                || Rune.GetUnicodeCategory(beforeQuote) == UnicodeCategory.OpenPunctuation
                || IsCjkOpeningQuote(previous) && IsCjkLetter(beforeQuote));
    }

    private static bool HasRightBoundary(string text, int endIndex, LockedField field)
    {
        if (endIndex == text.Length)
        {
            return true;
        }

        if (!TryRuneAt(text, endIndex, out var next, out _))
        {
            return false;
        }

        if (Rune.IsWhiteSpace(next))
        {
            if (!RequiresSpacedContinuationProtection(field)
                || EndsWithSelfDelimitingPunctuation(field.ExactValue)
                || !TryNextNonWhitespace(text, endIndex, out var follower, out var followerIndex))
            {
                return true;
            }

            if (EndsWithNumber(field.ExactValue) && IsNumber(follower))
            {
                return false;
            }

            var followerCategory = Rune.GetUnicodeCategory(follower);
            if (followerCategory == UnicodeCategory.DashPunctuation)
            {
                return IsSafeProseDashOnRight(text, followerIndex, follower);
            }

            if (IsNonDashSemanticOperator(follower))
            {
                return false;
            }

            return followerCategory is not (UnicodeCategory.OpenPunctuation
                    or UnicodeCategory.ClosePunctuation
                    or UnicodeCategory.InitialQuotePunctuation
                    or UnicodeCategory.FinalQuotePunctuation
                    or UnicodeCategory.OtherPunctuation)
                || !PunctuationIntroducesSpacedContinuation(text, followerIndex, field);
        }

        var category = Rune.GetUnicodeCategory(next);
        if (field.Kind == LockedFieldKind.Number
            && EndsWithNumber(field.ExactValue)
            && IsCjkLetter(next))
        {
            return true;
        }

        if (field.Kind == LockedFieldKind.Url && IsUriContinuation(next)
            || IsHardContinuation(next)
            || category is UnicodeCategory.MathSymbol
                or UnicodeCategory.CurrencySymbol
                or UnicodeCategory.ModifierSymbol
                or UnicodeCategory.OtherSymbol
                or UnicodeCategory.ConnectorPunctuation)
        {
            return false;
        }

        if (category == UnicodeCategory.DashPunctuation)
        {
            return field.Kind is LockedFieldKind.Number or LockedFieldKind.Date or LockedFieldKind.Unit or LockedFieldKind.Url
                ? IsSafeProseDashOnRight(text, endIndex, next)
                : IsTerminalPunctuationSuffix(text, endIndex, field);
        }

        return category is UnicodeCategory.ClosePunctuation
                or UnicodeCategory.FinalQuotePunctuation
                or UnicodeCategory.OtherPunctuation
            && IsTerminalPunctuationSuffix(text, endIndex, field);
    }

    private static bool IsTerminalPunctuationSuffix(string text, int index, LockedField field)
    {
        while (index < text.Length)
        {
            if (!TryRuneAt(text, index, out var value, out var consumed))
            {
                return false;
            }

            if (Rune.IsWhiteSpace(value))
            {
                return !EndsWithNumber(field.ExactValue)
                    || !PunctuationIntroducesSpacedContinuation(
                        text,
                        index,
                        field,
                        urlLettersAreContinuation: false);
            }

            if (IsCjkSentenceTerminal(value))
            {
                return true;
            }

            if (IsCjkClosingQuote(value)
                && TryRuneAt(text, index + consumed, out var afterQuote, out _)
                && IsCjkLetter(afterQuote))
            {
                return true;
            }

            var category = Rune.GetUnicodeCategory(value);
            if (field.Kind == LockedFieldKind.Url && IsUriContinuation(value)
                || IsHardContinuation(value)
                || category is UnicodeCategory.MathSymbol
                    or UnicodeCategory.CurrencySymbol
                    or UnicodeCategory.ModifierSymbol
                    or UnicodeCategory.OtherSymbol
                    or UnicodeCategory.ConnectorPunctuation
                || category == UnicodeCategory.DashPunctuation
                    && field.Kind is LockedFieldKind.Number or LockedFieldKind.Date or LockedFieldKind.Unit or LockedFieldKind.Url
                || category is not (UnicodeCategory.ClosePunctuation
                    or UnicodeCategory.InitialQuotePunctuation
                    or UnicodeCategory.FinalQuotePunctuation
                    or UnicodeCategory.OtherPunctuation
                    or UnicodeCategory.DashPunctuation))
            {
                return false;
            }

            index += consumed;
        }

        return true;
    }

    private static bool TryRuneAt(string text, int index, out Rune rune, out int consumed)
        => Rune.DecodeFromUtf16(text.AsSpan(index), out rune, out consumed) == OperationStatus.Done;

    private static bool TryRuneBefore(string text, int index, out Rune rune, out int start)
    {
        var status = Rune.DecodeLastFromUtf16(text.AsSpan(0, index), out rune, out var consumed);
        start = index - consumed;
        return status == OperationStatus.Done;
    }

    private static bool IsQuote(Rune value)
        => Rune.GetUnicodeCategory(value) is UnicodeCategory.InitialQuotePunctuation
            or UnicodeCategory.FinalQuotePunctuation
            || value.Value is '\'' or '"';

    private static bool StartsWithNumber(string value)
        => TryRuneAt(value, 0, out var first, out _)
            && IsNumber(first);

    private static bool EndsWithNumber(string value)
        => TryRuneBefore(value, value.Length, out var last, out _)
            && IsNumber(last);

    private static bool StartsWithSelfDelimitingPunctuation(string value)
        => TryRuneAt(value, 0, out var first, out _)
            && (Rune.GetUnicodeCategory(first) is UnicodeCategory.OpenPunctuation
                or UnicodeCategory.InitialQuotePunctuation
                || first.Value is '\'' or '"');

    private static bool EndsWithSelfDelimitingPunctuation(string value)
        => TryRuneBefore(value, value.Length, out var last, out _)
            && (Rune.GetUnicodeCategory(last) is UnicodeCategory.ClosePunctuation
                or UnicodeCategory.FinalQuotePunctuation
                || last.Value is '\'' or '"');

    private static bool IsNumber(Rune value)
        => Rune.GetUnicodeCategory(value) is UnicodeCategory.DecimalDigitNumber
            or UnicodeCategory.LetterNumber
            or UnicodeCategory.OtherNumber;

    private static bool RequiresSpacedContinuationProtection(LockedField field)
        => field.Kind is LockedFieldKind.Number
            or LockedFieldKind.Date
            or LockedFieldKind.Unit
            or LockedFieldKind.Url
            || StartsWithNumber(field.ExactValue)
            || EndsWithNumber(field.ExactValue);

    private static bool IsNonDashSemanticOperator(Rune value)
    {
        var category = Rune.GetUnicodeCategory(value);
        return IsHardContinuation(value)
            || category is UnicodeCategory.MathSymbol
                or UnicodeCategory.CurrencySymbol
                or UnicodeCategory.ModifierSymbol
                or UnicodeCategory.OtherSymbol
                or UnicodeCategory.ConnectorPunctuation;
    }

    private static bool IsNumericSeparatorBeforeWhitespace(string text, int punctuationStart, Rune punctuation)
    {
        if (punctuation.Value is not ('.' or ',' or ':' or 0x066B or 0x066C)
            || punctuationStart == 0)
        {
            return false;
        }

        var cursor = punctuationStart;
        while (cursor > 0 && TryRuneBefore(text, cursor, out var beforeSeparator, out var start))
        {
            cursor = start;
            if (Rune.IsWhiteSpace(beforeSeparator)
                || beforeSeparator.Value is '.' or ',' or ':' or 0x066B or 0x066C)
            {
                continue;
            }

            return IsNumber(beforeSeparator);
        }

        return false;
    }

    private static bool IsSafeProseDashOnLeft(string text, int dashStart, Rune dash)
    {
        if (!IsProseDash(dash)
            || !TryPreviousNonWhitespace(text, dashStart, out var beforeDash, out _))
        {
            return false;
        }

        return !IsNumber(beforeDash) && !IsNonDashSemanticOperator(beforeDash);
    }

    private static bool IsSafeProseDashOnRight(string text, int dashStart, Rune dash)
    {
        if (!IsProseDash(dash))
        {
            return false;
        }

        var afterDash = dashStart + dash.Utf16SequenceLength;
        if (!TryNextNonWhitespace(text, afterDash, out var after, out _))
        {
            return true;
        }

        return !IsNumber(after)
            && !IsNonDashSemanticOperator(after)
            && Rune.GetUnicodeCategory(after) != UnicodeCategory.DashPunctuation;
    }

    private static bool IsProseDash(Rune value)
        => value.Value is 0x2013 or 0x2014 or 0x2015;

    private static bool PunctuationIntroducesSpacedContinuation(
        string text,
        int index,
        LockedField field,
        bool urlLettersAreContinuation = true)
    {
        while (index < text.Length && TryRuneAt(text, index, out var value, out var consumed))
        {
            index += consumed;
            if (Rune.IsWhiteSpace(value))
            {
                continue;
            }

            var category = Rune.GetUnicodeCategory(value);
            if (category is UnicodeCategory.OpenPunctuation
                or UnicodeCategory.ClosePunctuation
                or UnicodeCategory.InitialQuotePunctuation
                or UnicodeCategory.FinalQuotePunctuation
                or UnicodeCategory.OtherPunctuation)
            {
                continue;
            }

            return IsNumber(value)
                || IsNonDashSemanticOperator(value)
                || category == UnicodeCategory.DashPunctuation
                || urlLettersAreContinuation && field.Kind == LockedFieldKind.Url;
        }

        return false;
    }

    private static bool IsParentheticalNumber(
        string text,
        int index,
        int endIndex,
        LockedField field)
    {
        if (field.Kind != LockedFieldKind.Number
            || index == 0
            || endIndex == text.Length
            || !TryPreviousNonWhitespace(text, index, out var before, out _)
            || !TryNextNonWhitespace(text, endIndex, out var after, out _))
        {
            return false;
        }

        return IsParenthesisOrBracketPair(before.Value, after.Value);
    }

    private static bool IsParenthesisOrBracketPair(int before, int after)
    {
        if (IsCjkQuotationPair(before, after))
        {
            return false;
        }

        var beforeCategory = Rune.GetUnicodeCategory(new Rune(before));
        var afterCategory = Rune.GetUnicodeCategory(new Rune(after));
        return beforeCategory == UnicodeCategory.OpenPunctuation
                && afterCategory == UnicodeCategory.ClosePunctuation
            || (before, after) is
            ('(', ')')
            or ('[', ']')
            or ('{', '}')
            or (0x207D, 0x207E)
            or (0x208D, 0x208E)
            or (0x2768, 0x2769)
            or (0x276A, 0x276B)
            or (0x276C, 0x276D)
            or (0x276E, 0x276F)
            or (0x2770, 0x2771)
            or (0x2772, 0x2773)
            or (0x2774, 0x2775)
            or (0x27C5, 0x27C6)
            or (0x27E6, 0x27E7)
            or (0x27E8, 0x27E9)
            or (0x27EA, 0x27EB)
            or (0x27EC, 0x27ED)
            or (0x27EE, 0x27EF)
            or (0x2983, 0x2984)
            or (0xFF08, 0xFF09)
            or (0xFF3B, 0xFF3D)
            or (0xFF5B, 0xFF5D)
            or (0x2985, 0x2986)
            or (0x2987, 0x2988)
            or (0x2989, 0x298A)
            or (0x298B, 0x298C)
            or (0x298D, 0x298E)
            or (0x298F, 0x2990)
            or (0x2991, 0x2992)
            or (0x2993, 0x2994)
            or (0x2995, 0x2996)
            or (0x2997, 0x2998)
            or (0xFE59, 0xFE5A)
            or (0xFE5B, 0xFE5C)
            or (0xFE5D, 0xFE5E);
    }

    private static bool IsCjkQuotationPair(int before, int after)
        => (before, after) is
            (0x300C, 0x300D)
            or (0x300E, 0x300F)
            or (0xFE41, 0xFE42)
            or (0xFE43, 0xFE44)
            or (0xFF62, 0xFF63);

    private static bool IsCjkOpeningQuote(Rune value)
        => value.Value is 0x2018 or 0x201C or 0x300C or 0x300E or 0xFE41 or 0xFE43 or 0xFF62;

    private static bool IsCjkClosingQuote(Rune value)
        => value.Value is 0x2019 or 0x201D or 0x300D or 0x300F or 0xFE42 or 0xFE44 or 0xFF63;

    private static bool IsNumericListMarker(
        string text,
        int index,
        int endIndex,
        LockedField field)
    {
        if (field.Kind != LockedFieldKind.Number
            || !IsPlainNumber(field.ExactValue)
            || !HasOnlyWhitespaceSinceLineStart(text, index)
            || !TryRuneAt(text, endIndex, out var delimiter, out var consumed)
            || !IsListMarkerDelimiter(delimiter))
        {
            return false;
        }

        var cursor = endIndex + consumed;
        while (cursor < text.Length && TryRuneAt(text, cursor, out var value, out consumed))
        {
            if (Rune.IsWhiteSpace(value))
            {
                return true;
            }

            if (Rune.GetUnicodeCategory(value) != UnicodeCategory.ClosePunctuation)
            {
                return false;
            }

            cursor += consumed;
        }

        return true;
    }

    private static bool IsPlainNumber(string value)
    {
        foreach (var rune in value.EnumerateRunes())
        {
            if (!IsNumber(rune))
            {
                return false;
            }
        }

        return value.Length > 0;
    }

    private static bool HasOnlyWhitespaceSinceLineStart(string text, int index)
    {
        while (index > 0 && TryRuneBefore(text, index, out var value, out var start))
        {
            if (value.Value is '\r' or '\n' or 0x2028 or 0x2029)
            {
                return true;
            }

            if (!Rune.IsWhiteSpace(value))
            {
                return false;
            }

            index = start;
        }

        return true;
    }

    private static bool IsListMarkerDelimiter(Rune value)
        => value.Value is '.' or ')' or ']' or ':' or 0x3001 or 0xFF09 or 0xFF0E or 0xFF1A;

    private static bool IsCjkSentenceTerminal(Rune value)
        => value.Value is 0x3002 or 0xFF01 or 0xFF1F;

    private static bool IsCjkLetter(Rune value)
        => value.Value is >= 0x3040 and <= 0x30FF
            or >= 0x3100 and <= 0x312F
            or >= 0x31A0 and <= 0x31BF
            or >= 0x31F0 and <= 0x31FF
            or >= 0x3400 and <= 0x4DBF
            or >= 0x4E00 and <= 0x9FFF
            or >= 0xAC00 and <= 0xD7AF
            or >= 0xF900 and <= 0xFAFF
            or >= 0xFF66 and <= 0xFF9D
            or >= 0x20000 and <= 0x2EE5F
            or >= 0x2F800 and <= 0x2FA1F
            or >= 0x30000 and <= 0x323AF;

    private static bool TryPreviousNonWhitespace(
        string text,
        int index,
        out Rune rune,
        out int start)
    {
        while (index > 0 && TryRuneBefore(text, index, out rune, out start))
        {
            if (!Rune.IsWhiteSpace(rune))
            {
                return true;
            }

            index = start;
        }

        rune = default;
        start = -1;
        return false;
    }

    private static bool TryNextNonWhitespace(
        string text,
        int index,
        out Rune rune,
        out int start)
    {
        while (index < text.Length && TryRuneAt(text, index, out rune, out var consumed))
        {
            start = index;
            if (!Rune.IsWhiteSpace(rune))
            {
                return true;
            }

            index += consumed;
        }

        rune = default;
        start = -1;
        return false;
    }

    private static bool IsHardContinuation(Rune value)
        => value.Value is '%'
            or 0x0609 // Arabic-Indic per mille.
            or 0x060A // Arabic-Indic per ten thousand.
            or 0x066A // Arabic percent sign.
            or 0x2030 // Per mille.
            or 0x2031 // Per ten thousand.
            or 0xFE6A // Small percent sign.
            or 0xFF05 // Fullwidth percent sign.
            or '/'
            or '\\'
            or '#'
            or '&'
            or '@';

    private static bool IsUriContinuation(Rune value)
        => value.Value is '-' or '.' or '_' or '~'
            or ':' or '/' or '?' or '#' or '[' or ']' or '@'
            or '!' or '$' or '&' or '\'' or '(' or ')' or '*' or '+' or ',' or ';' or '=' or '%';

    private static string QuoteExactValue(string value)
    {
        var quoted = new StringBuilder(value.Length + 2).Append('"');
        var index = 0;
        while (index < value.Length)
        {
            var status = Rune.DecodeFromUtf16(value.AsSpan(index), out var rune, out var consumed);
            if (status != OperationStatus.Done)
            {
                AppendUnicodeEscape(quoted, value[index]);
                index++;
                continue;
            }

            switch (rune.Value)
            {
                case '\\':
                    quoted.Append("\\\\");
                    break;
                case '"':
                    quoted.Append("\\\"");
                    break;
                case '\r':
                    quoted.Append("\\r");
                    break;
                case '\n':
                    quoted.Append("\\n");
                    break;
                case '\t':
                    quoted.Append("\\t");
                    break;
                case '\u2028':
                    quoted.Append("\\u2028");
                    break;
                case '\u2029':
                    quoted.Append("\\u2029");
                    break;
                default:
                    if (Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control or UnicodeCategory.Format)
                    {
                        AppendUnicodeEscape(quoted, rune.Value);
                    }
                    else
                    {
                        quoted.Append(rune.ToString());
                    }

                    break;
            }

            index += consumed;
        }

        return quoted.Append('"').ToString();
    }

    private static void AppendUnicodeEscape(StringBuilder builder, int scalarOrCodeUnit)
    {
        if (scalarOrCodeUnit <= 0xFFFF)
        {
            builder.Append("\\u")
                .Append(scalarOrCodeUnit.ToString("X4", CultureInfo.InvariantCulture));
        }
        else
        {
            builder.Append("\\U")
                .Append(scalarOrCodeUnit.ToString("X8", CultureInfo.InvariantCulture));
        }
    }
}

/// <summary>Enumerates every human-readable string in a document, in document order.</summary>
public static class DocumentText
{
    public static IReadOnlyList<string> CollectStrings(ArtifactDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var strings = new List<string>();

        foreach (var node in document.Nodes)
        {
            switch (node)
            {
                case Heading heading:
                    strings.Add(heading.Text);
                    break;
                case Paragraph paragraph:
                    strings.Add(paragraph.Text);
                    break;
                case OrderedSteps steps:
                    strings.AddRange(steps.Steps);
                    break;
                case UnorderedList list:
                    strings.AddRange(list.Items);
                    break;
                case TableNode table:
                    if (table.HeaderRow is not null)
                    {
                        strings.AddRange(table.HeaderRow);
                    }

                    foreach (var row in table.Rows)
                    {
                        strings.AddRange(row);
                    }

                    break;
                case Card card:
                    strings.Add(card.Title);
                    strings.Add(card.Body);
                    break;
                case ImageReference image:
                    strings.Add(image.AltText);
                    break;
                case BilingualPair pair:
                    strings.Add(pair.SourceText);
                    strings.Add(pair.TargetText);
                    break;
                case ChoiceSet choices:
                    strings.AddRange(choices.Options);
                    break;
                case EvidenceLink evidence:
                    strings.Add(evidence.Claim);
                    strings.Add(evidence.SourcePointer);
                    break;
                case Citation citation:
                    strings.Add(citation.Text);
                    break;
                case TeacherOnlyNotice notice:
                    strings.Add(notice.Text);
                    break;
                case VectorGraphic graphic:
                    strings.Add(graphic.Description);
                    strings.AddRange(graphic.Primitives.OfType<TextLabel>().Select(l => l.Text));
                    break;
                case StepRow step:
                    strings.Add(step.Text);
                    if (step.TargetText is not null)
                    {
                        strings.Add(step.TargetText);
                    }

                    if (step.Symbol is { } stepSymbol)
                    {
                        strings.Add(stepSymbol.AltText);
                    }

                    break;
                default:
                    break;
            }
        }

        return strings;
    }
}
