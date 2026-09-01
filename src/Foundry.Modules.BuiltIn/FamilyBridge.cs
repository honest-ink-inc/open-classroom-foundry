// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.BuiltIn.DirectionsDuet;

namespace Foundry.Modules.BuiltIn.FamilyBridge;

public sealed record BridgeParagraph(string SourceText, string? TargetText = null);

public sealed record FamilyBridgeResult(ArtifactDocument Document, IReadOnlyList<ValidationIssue> Issues);

/// <summary>
/// KinDispatch (stable legacy id: family-bridge), Green general communications only (plan §10.10). The council's
/// readability lint (RC-9) is deterministic law: long sentences block, and a
/// letter asking two things at once is flagged, because one clear ask travels
/// home better than three buried ones. Locked facts survive both languages;
/// translation status is honest; the application holds no recipient and sends
/// nothing — said on the artifact itself.
/// </summary>
public static class FamilyBridgeBuilder
{
    public const int MaxAverageSentenceWords = 20;

    /// <summary>
    /// Compatibility route for callers compiled against the original builder.
    /// It deliberately fails closed on the newly required source-inventory act.
    /// </summary>
    public static FamilyBridgeResult Build(
        string title,
        IReadOnlyList<BridgeParagraph> paragraphs,
        string requestedAction,
        string contact,
        Glossary glossary,
        IReadOnlyList<LockedField> lockedFields,
        string? deadline = null,
        string sourceLocale = "en",
        string? targetLocale = null,
        string? reviewedBy = null)
        => Build(
            title,
            paragraphs,
            requestedAction,
            contact,
            glossary,
            lockedFields,
            lockedFieldInventoryReviewed: false,
            deadline,
            sourceLocale,
            targetLocale,
            reviewedBy);

    public static FamilyBridgeResult Build(
        string title,
        IReadOnlyList<BridgeParagraph> paragraphs,
        string requestedAction,
        string contact,
        Glossary glossary,
        IReadOnlyList<LockedField> lockedFields,
        bool lockedFieldInventoryReviewed,
        string? deadline = null,
        string sourceLocale = "en",
        string? targetLocale = null,
        string? reviewedBy = null,
        string? targetRequestedAction = null,
        string? targetContact = null,
        string? targetDeadline = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(paragraphs);
        ArgumentNullException.ThrowIfNull(glossary);
        ArgumentNullException.ThrowIfNull(lockedFields);
        LanguageTag.RequireValid(sourceLocale, nameof(sourceLocale));
        if (targetLocale is not null)
        {
            LanguageTag.RequireValid(targetLocale, nameof(targetLocale));
        }

        if (!string.IsNullOrWhiteSpace(reviewedBy))
        {
            throw new ArgumentException(
                "Language-review status cannot be self-attested. This build has no authenticated review capability.",
                nameof(reviewedBy));
        }

        var issues = new List<ValidationIssue>();

        issues.AddRange(LockedFieldValidator.ValidateInventoryReview(lockedFieldInventoryReviewed));

        if (targetLocale is null
            && (paragraphs.Any(paragraph => !string.IsNullOrWhiteSpace(paragraph.TargetText))
                || !string.IsNullOrWhiteSpace(targetRequestedAction)
                || !string.IsNullOrWhiteSpace(targetContact)
                || !string.IsNullOrWhiteSpace(targetDeadline)))
        {
            issues.Add(ValidationIssue.Blocking(
                "bridge.target-without-locale",
                "Target-language content cannot be used without an explicit target language."));
        }

        if (paragraphs.Count == 0)
        {
            issues.Add(ValidationIssue.Blocking("bridge.empty", "There is nothing to send home."));
        }

        if (string.IsNullOrWhiteSpace(requestedAction))
        {
            issues.Add(ValidationIssue.Blocking("bridge.action", "Every family communication states its one requested action explicitly."));
        }

        if (string.IsNullOrWhiteSpace(contact))
        {
            issues.Add(ValidationIssue.Blocking("bridge.contact", "Every family communication names where help lives."));
        }

        // The body alone carries the readability bar — a short call-to-action must
        // never launder a winding letter past the plain-language target.
        var average = AverageSentenceWords(paragraphs.Select(p => p.SourceText));
        if (average > MaxAverageSentenceWords)
        {
            issues.Add(ValidationIssue.Blocking("bridge.readability",
                $"Average sentence length is {average:0.#} words; the plain-language target is {MaxAverageSentenceWords}. Shorter sentences travel farther."));
        }

        if (!string.IsNullOrWhiteSpace(requestedAction) && LooksLikeMultipleAsks(requestedAction))
        {
            issues.Add(ValidationIssue.Warning("bridge.actions",
                "This reads like more than one ask; one action per communication travels best."));
        }

        var translatedFields = new List<(string SourceText, string? TargetText, string MissingCode, string MissingMessage)>();
        var bodyFields = new List<(string SourceText, string? TargetText)>();
        var structuredFields = new List<(string SourceText, string? TargetText, string RoleLabel)>();
        if (targetLocale is not null)
        {
            for (var i = 0; i < paragraphs.Count; i++)
            {
                bodyFields.Add((paragraphs[i].SourceText, paragraphs[i].TargetText));
                translatedFields.Add((
                    paragraphs[i].SourceText,
                    paragraphs[i].TargetText,
                    "bridge.target-missing",
                    $"Paragraph {i + 1} has no translation; alignment is one-to-one."));
            }

            if (!string.IsNullOrWhiteSpace(requestedAction))
            {
                structuredFields.Add((requestedAction, targetRequestedAction, "requested action"));
                translatedFields.Add((
                    requestedAction,
                    targetRequestedAction,
                    "bridge.target-action-missing",
                    "The explicit requested action has no target-language version."));
            }

            if (!string.IsNullOrWhiteSpace(deadline))
            {
                structuredFields.Add((deadline, targetDeadline, "deadline"));
                translatedFields.Add((
                    deadline,
                    targetDeadline,
                    "bridge.target-deadline-missing",
                    "The explicit deadline has no target-language version."));
            }
            else if (!string.IsNullOrWhiteSpace(targetDeadline))
            {
                issues.Add(ValidationIssue.Blocking(
                    "bridge.target-deadline-without-source",
                    "A target-language deadline cannot exist without an explicit source deadline."));
            }

            if (!string.IsNullOrWhiteSpace(contact))
            {
                structuredFields.Add((contact, targetContact, "help contact"));
                translatedFields.Add((
                    contact,
                    targetContact,
                    "bridge.target-contact-missing",
                    "The explicit help contact has no target-language version."));
            }

            foreach (var (SourceText, TargetText, MissingCode, MissingMessage) in translatedFields)
            {
                if (string.IsNullOrWhiteSpace(TargetText))
                {
                    issues.Add(ValidationIssue.Blocking(MissingCode, MissingMessage));
                    continue;
                }

                foreach (var entry in glossary.Entries)
                {
                    if (SourceText.Contains(entry.SourceTerm, StringComparison.OrdinalIgnoreCase)
                        && !TargetText.Contains(entry.TargetTerm, StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add(ValidationIssue.Blocking("bridge.glossary",
                            $"Source content uses '{entry.SourceTerm}' but its translation lacks working glossary term '{entry.TargetTerm}' (working glossary {glossary.Version}, not approved by this application)."));
                    }
                }
            }

            issues.AddRange(LockedFieldValidator.ValidateBilingualContent(
                bodyFields,
                [.. structuredFields.Select(field => (field.RoleLabel, field.SourceText, field.TargetText))],
                lockedFields,
                "bridge.locked",
                "message paragraphs"));
        }

        var nodes = new List<DocumentNode> { new Heading(1, title) };

        foreach (var paragraph in paragraphs)
        {
            if (targetLocale is not null && !string.IsNullOrWhiteSpace(paragraph.TargetText))
            {
                nodes.Add(new BilingualPair(paragraph.SourceText, paragraph.TargetText, sourceLocale, targetLocale));
            }
            else
            {
                nodes.Add(new Paragraph(paragraph.SourceText));
            }
        }

        if (targetLocale is null)
        {
            if (!string.IsNullOrWhiteSpace(requestedAction))
            {
                nodes.Add(new Card("What we ask", requestedAction));
            }

            if (!string.IsNullOrWhiteSpace(deadline))
            {
                nodes.Add(new Card("By when", deadline));
            }

            if (!string.IsNullOrWhiteSpace(contact))
            {
                nodes.Add(new Card("Questions? Contact", contact));
            }
        }
        else
        {
            AddBilingualSection(nodes, "What we ask", requestedAction, targetRequestedAction, sourceLocale, targetLocale);
            if (!string.IsNullOrWhiteSpace(deadline))
            {
                AddBilingualSection(nodes, "By when", deadline, targetDeadline, sourceLocale, targetLocale);
            }

            AddBilingualSection(nodes, "Questions? Contact", contact, targetContact, sourceLocale, targetLocale);
        }

        var lockedInventorySummary = LockedInventorySummary(lockedFields);
        nodes.Add(new TeacherOnlyNotice(lockedInventorySummary));

        if (targetLocale is not null)
        {
            var translationStatus =
                $"Working glossary {glossary.Version} (not approved by this application). " +
                "Translation status: drafted - NOT yet language-reviewed by a qualified reviewer.";
            nodes.Add(new TeacherOnlyNotice(translationStatus));
        }

        const string deliveryNotice =
            "This application holds no recipient list and sends nothing; addressing and delivery are yours, under your school's rules.";
        nodes.Add(new TeacherOnlyNotice(deliveryNotice));

        var document = new ArtifactDocument(nodes, sourceLocale);
        if (targetLocale is null)
        {
            issues.AddRange(LockedFieldValidator.Validate(
                SelectTeacherAuthoredSourceContent(document),
                lockedFields));
        }

        issues.AddRange(DocumentValidator.Validate(document));
        return new FamilyBridgeResult(document, issues);
    }

    internal static ArtifactDocument SelectTeacherAuthoredSourceContent(ArtifactDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var nodes = new List<DocumentNode>();
        var inMessageBody = true;

        foreach (var node in document.Nodes)
        {
            switch (node)
            {
                case Heading { Level: 1 } heading:
                    nodes.Add(heading);
                    break;
                case Heading { Level: 2 }:
                    inMessageBody = false;
                    break;
                case Paragraph paragraph when inMessageBody:
                    nodes.Add(paragraph);
                    break;
                case BilingualPair pair when inMessageBody:
                    nodes.Add(new Paragraph(pair.SourceText));
                    break;
                case Card card:
                    nodes.Add(new Paragraph(card.Body));
                    break;
            }
        }

        return new ArtifactDocument(nodes, document.Language);
    }

    private static double AverageSentenceWords(IEnumerable<string> texts)
    {
        var sentences = texts
            .SelectMany(t => t.Split(['.', '!', '?'], StringSplitOptions.RemoveEmptyEntries))
            .Select(s => s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length)
            .Where(words => words > 0)
            .ToList();

        return sentences.Count == 0 ? 0 : sentences.Average();
    }

    private static bool LooksLikeMultipleAsks(string action)
    {
        var sentenceCount = action.Split(['.', '!', '?'], StringSplitOptions.RemoveEmptyEntries)
            .Count(s => !string.IsNullOrWhiteSpace(s));
        return sentenceCount > 1 || action.Contains(" and ", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddBilingualSection(
        List<DocumentNode> nodes,
        string heading,
        string sourceText,
        string? targetText,
        string sourceLocale,
        string targetLocale)
    {
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            return;
        }

        nodes.Add(new Heading(2, heading));
        nodes.Add(new BilingualPair(sourceText, targetText ?? "", sourceLocale, targetLocale));
    }

    private static string LockedInventorySummary(IReadOnlyList<LockedField> lockedFields)
        => LockedFieldValidator.FormatInventorySummary(lockedFields);

    public static RecipeManifest Recipe { get; } = new(
        Id: "family-bridge",
        Version: "0.1.0",
        License: "GPL-3.0-or-later",
        MinimumEngineVersion: EngineIdentity.EngineVersion,
        InstructionalPurpose: "Turn teacher-authored general information into a plain-language, optionally bilingual family communication with one clear ask, a deadline, and a named contact.",
        ProhibitedPurposes:
        [
            "recipient lists, addressing, or automated distribution of any kind",
            "learner-specific progress, grades, attendance, behavior, IEP/504, discipline, health, custody, immigration, or legal content",
            "invented policies, promises, resources, or deadlines",
            "certified-translation or language-reviewed claims; this build has no authenticated review capability",
            "family-deficit assumptions",
        ],
        AllowedInputKinds: ["teacher-entered-text", "teacher-entered-working-glossary", "locked-district-text"],
        MaximumLane: DataLane.Green,
        RequiredProviderCapabilities: [],
        OutputSchemaId: "schema.family-bridge.v1",
        ValidatorIds: ["document.structural", "locked-fields", "bridge.readability"],
        EditorId: "editor.review-session",
        RendererId: "renderer.accessible-html",
        SupportedExports: [RenderTarget.AccessibleHtml, RenderTarget.PrintHtml],
        Warnings:
        [
            "One requested action per communication; the interpreter is a person, never this software.",
            "Working glossary entries are not approved by this application; translations remain unreviewed.",
        ],
        EvaluationSuiteVersion: "0.1");
}
