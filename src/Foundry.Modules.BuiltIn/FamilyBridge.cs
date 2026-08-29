// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.BuiltIn.DirectionsDuet;

namespace Foundry.Modules.BuiltIn.FamilyBridge;

public sealed record BridgeParagraph(string SourceText, string? TargetText = null);

public sealed record FamilyBridgeResult(ArtifactDocument Document, IReadOnlyList<ValidationIssue> Issues);

/// <summary>
/// Family Bridge, Green general communications only (plan §10.10). The council's
/// readability lint (RC-9) is deterministic law: long sentences block, and a
/// letter asking two things at once is flagged, because one clear ask travels
/// home better than three buried ones. Locked facts survive both languages;
/// translation status is honest; the application holds no recipient and sends
/// nothing — said on the artifact itself.
/// </summary>
public static class FamilyBridgeBuilder
{
    public const int MaxAverageSentenceWords = 20;

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
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(paragraphs);
        ArgumentNullException.ThrowIfNull(glossary);
        ArgumentNullException.ThrowIfNull(lockedFields);

        var issues = new List<ValidationIssue>();

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

        if (targetLocale is not null)
        {
            for (var i = 0; i < paragraphs.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(paragraphs[i].TargetText))
                {
                    issues.Add(ValidationIssue.Blocking("bridge.target-missing",
                        $"Paragraph {i + 1} has no translation; alignment is one-to-one."));
                    continue;
                }

                foreach (var entry in glossary.Entries)
                {
                    if (paragraphs[i].SourceText.Contains(entry.SourceTerm, StringComparison.OrdinalIgnoreCase)
                        && !paragraphs[i].TargetText!.Contains(entry.TargetTerm, StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add(ValidationIssue.Blocking("bridge.glossary",
                            $"Paragraph {i + 1} uses '{entry.SourceTerm}' but its translation lacks the approved '{entry.TargetTerm}' (glossary {glossary.Version})."));
                    }
                }
            }

            foreach (var field in lockedFields)
            {
                var inSource = paragraphs.Any(p => p.SourceText.Contains(field.ExactValue, StringComparison.Ordinal));
                var inTarget = paragraphs.Any(p => p.TargetText?.Contains(field.ExactValue, StringComparison.Ordinal) == true);
                if (!inSource || !inTarget)
                {
                    issues.Add(ValidationIssue.Blocking("bridge.locked",
                        $"Locked {field.Kind} '{field.ExactValue}' must appear verbatim in both languages; it is missing from the {(inSource ? "translation" : "source")}."));
                }
            }
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

        if (targetLocale is not null)
        {
            nodes.Add(new TeacherOnlyNotice(
                $"Glossary {glossary.Version}. Translation status: " +
                (string.IsNullOrWhiteSpace(reviewedBy)
                    ? "drafted - NOT yet language-reviewed by a qualified reviewer."
                    : $"language-reviewed by {reviewedBy}.")));
        }

        nodes.Add(new TeacherOnlyNotice(
            "This application holds no recipient list and sends nothing; addressing and delivery are yours, under your school's rules."));

        var document = new ArtifactDocument(nodes, sourceLocale);
        issues.AddRange(DocumentValidator.Validate(document));
        return new FamilyBridgeResult(document, issues);
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
            "certified-translation claims without a recorded human reviewer",
            "family-deficit assumptions",
        ],
        AllowedInputKinds: ["teacher-entered-text", "approved-glossary", "locked-district-text"],
        MaximumLane: DataLane.Green,
        RequiredProviderCapabilities: [],
        OutputSchemaId: "schema.family-bridge.v1",
        ValidatorIds: ["document.structural", "locked-fields", "bridge.readability"],
        EditorId: "editor.review-session",
        RendererId: "renderer.accessible-html",
        SupportedExports: [RenderTarget.AccessibleHtml, RenderTarget.PrintHtml],
        Warnings: ["One requested action per communication; the interpreter is a person, never this software."],
        EvaluationSuiteVersion: "0.1");
}
