// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Modules.BuiltIn.DirectionsDuet;

public sealed record GlossaryEntry(string SourceTerm, string TargetTerm);

public sealed record Glossary(string Version, IReadOnlyList<GlossaryEntry> Entries)
{
    public static Glossary Empty { get; } = new("none", []);
}

/// <summary>One teacher-confirmed one-action microstep and its aligned translation.</summary>
public sealed record DuetStep(string SourceText, string TargetText);

public sealed record DuetResult(ArtifactDocument Document, IReadOnlyList<ValidationIssue> Issues);

/// <summary>
/// Directions Duet (plan §10.5): line-aligned bilingual microsteps with locked
/// facts verified on BOTH sides, working-glossary consistency checked per step,
/// and a translation status that remains unreviewed until an authenticated review
/// capability exists outside this builder.
/// </summary>
public static class DirectionsDuetBuilder
{
    /// <summary>
    /// Compatibility route for callers compiled against the original builder.
    /// It deliberately fails closed on the newly required source-inventory act.
    /// </summary>
    public static DuetResult Build(
        string title,
        IReadOnlyList<DuetStep> steps,
        string sourceLocale,
        string targetLocale,
        Glossary glossary,
        IReadOnlyList<LockedField> lockedFields,
        string? reviewedBy = null,
        string? comprehensionCheck = null)
        => Build(
            title,
            steps,
            sourceLocale,
            targetLocale,
            glossary,
            lockedFields,
            lockedFieldInventoryReviewed: false,
            reviewedBy,
            comprehensionCheck);

    public static DuetResult Build(
        string title,
        IReadOnlyList<DuetStep> steps,
        string sourceLocale,
        string targetLocale,
        Glossary glossary,
        IReadOnlyList<LockedField> lockedFields,
        bool lockedFieldInventoryReviewed,
        string? reviewedBy = null,
        string? comprehensionCheck = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(glossary);
        ArgumentNullException.ThrowIfNull(lockedFields);
        LanguageTag.RequireValid(sourceLocale, nameof(sourceLocale));
        LanguageTag.RequireValid(targetLocale, nameof(targetLocale));

        if (!string.IsNullOrWhiteSpace(reviewedBy))
        {
            throw new ArgumentException(
                "Language-review status cannot be self-attested. This build has no authenticated review capability.",
                nameof(reviewedBy));
        }

        var issues = new List<ValidationIssue>();

        issues.AddRange(LockedFieldValidator.ValidateInventoryReview(lockedFieldInventoryReviewed));

        if (steps.Count == 0)
        {
            issues.Add(ValidationIssue.Blocking("duet.empty", "There are no steps to translate."));
        }

        for (var i = 0; i < steps.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(steps[i].TargetText))
            {
                issues.Add(ValidationIssue.Blocking("duet.target-missing", $"Step {i + 1} has no translation; alignment is one-to-one."));
            }

            foreach (var entry in glossary.Entries)
            {
                // Case-insensitive both sides (RC-7): "Folder" at a sentence start
                // must not escape the folder -> carpeta rule.
                if (steps[i].SourceText.Contains(entry.SourceTerm, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(steps[i].TargetText)
                    && !steps[i].TargetText.Contains(entry.TargetTerm, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(ValidationIssue.Blocking(
                        "duet.glossary",
                        $"Step {i + 1} uses '{entry.SourceTerm}' but its translation lacks working glossary term '{entry.TargetTerm}' (working glossary {glossary.Version}, not approved by this application)."));
                }
            }
        }

        issues.AddRange(LockedFieldValidator.ValidateAlignedPairs(
            [.. steps.Select(step => (step.SourceText, TargetText: (string?)step.TargetText))],
            lockedFields,
            "duet.locked",
            "Step"));

        var nodes = new List<DocumentNode> { new Heading(1, title) };
        nodes.AddRange(steps.Select(s => new BilingualPair(s.SourceText, s.TargetText, sourceLocale, targetLocale)));

        if (!string.IsNullOrWhiteSpace(comprehensionCheck))
        {
            nodes.Add(new Card("Show me", comprehensionCheck));
        }

        nodes.Add(new TeacherOnlyNotice(LockedInventorySummary(lockedFields)));

        // The status speaks only to review, never to origin (RC-6): a teacher-typed
        // translation is not "machine-drafted," and a tool named Honest Ink does not
        // guess where words came from. This builder cannot authenticate a reviewer,
        // so text supplied by a caller can never change the review status.
        nodes.Add(new TeacherOnlyNotice(
            $"Working glossary {glossary.Version} (not approved by this application). " +
            "Translation status: drafted - NOT yet language-reviewed by a qualified reviewer."));

        var document = new ArtifactDocument(nodes, sourceLocale);
        issues.AddRange(DocumentValidator.Validate(document));

        return new DuetResult(document, issues);
    }

    private static string LockedInventorySummary(IReadOnlyList<LockedField> lockedFields)
        => LockedFieldValidator.FormatInventorySummary(lockedFields);

    public static RecipeManifest Recipe { get; } = new(
        Id: "directions-duet",
        Version: "0.1.0",
        License: "GPL-3.0-or-later",
        MinimumEngineVersion: EngineIdentity.EngineVersion,
        InstructionalPurpose: "Turn confirmed classroom directions into line-aligned bilingual microsteps with locked facts intact in both languages.",
        ProhibitedPurposes:
        [
            "certified-translation or language-reviewed claims; this build has no authenticated review capability",
            "safety, legal, emergency, disciplinary, or consequential directions without approved source language and qualified review",
            "altered action count, order, conditions, or deadlines",
        ],
        AllowedInputKinds: ["teacher-entered-text", "teacher-entered-working-glossary"],
        MaximumLane: DataLane.Green,
        RequiredProviderCapabilities: [],
        OutputSchemaId: "schema.directions-duet.v1",
        ValidatorIds: ["document.structural", "locked-fields", "duet.glossary"],
        EditorId: "editor.review-session",
        RendererId: "renderer.accessible-html",
        SupportedExports: [RenderTarget.AccessibleHtml, RenderTarget.PrintHtml],
        Warnings:
        [
            "The source language remains visible and authoritative beside every translation.",
            "Working glossary entries are not approved by this application; translations remain unreviewed.",
        ],
        EvaluationSuiteVersion: "0.1");
}
