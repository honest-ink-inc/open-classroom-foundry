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
/// facts verified on BOTH sides, glossary consistency checked per step, and the
/// translation status stamped honestly — "language-reviewed" exists only with a
/// recorded human reviewer.
/// </summary>
public static class DirectionsDuetBuilder
{
    public static DuetResult Build(
        string title,
        IReadOnlyList<DuetStep> steps,
        string sourceLocale,
        string targetLocale,
        Glossary glossary,
        IReadOnlyList<LockedField> lockedFields,
        string? reviewedBy = null,
        string? comprehensionCheck = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(glossary);
        ArgumentNullException.ThrowIfNull(lockedFields);

        var issues = new List<ValidationIssue>();

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
                        $"Step {i + 1} uses '{entry.SourceTerm}' but its translation lacks the approved '{entry.TargetTerm}' (glossary {glossary.Version})."));
                }
            }
        }

        foreach (var field in lockedFields)
        {
            var inSource = steps.Any(s => s.SourceText.Contains(field.ExactValue, StringComparison.Ordinal));
            var inTarget = steps.Any(s => s.TargetText?.Contains(field.ExactValue, StringComparison.Ordinal) == true);
            if (!inSource || !inTarget)
            {
                issues.Add(ValidationIssue.Blocking(
                    "duet.locked",
                    $"Locked {field.Kind} '{field.ExactValue}' must appear verbatim in both languages; it is missing from the {(inSource ? "translation" : "source")}."));
            }
        }

        var nodes = new List<DocumentNode> { new Heading(1, title) };
        nodes.AddRange(steps.Select(s => new BilingualPair(s.SourceText, s.TargetText, sourceLocale, targetLocale)));

        if (!string.IsNullOrWhiteSpace(comprehensionCheck))
        {
            nodes.Add(new Card("Show me", comprehensionCheck));
        }

        // The status speaks only to review, never to origin (RC-6): a teacher-typed
        // translation is not "machine-drafted," and a tool named Honest Ink does not
        // guess where words came from.
        nodes.Add(new TeacherOnlyNotice(
            $"Glossary {glossary.Version}. Translation status: " +
            (string.IsNullOrWhiteSpace(reviewedBy)
                ? "drafted - NOT yet language-reviewed by a qualified reviewer."
                : $"language-reviewed by {reviewedBy}.")));

        var document = new ArtifactDocument(nodes, sourceLocale);
        issues.AddRange(DocumentValidator.Validate(document));

        return new DuetResult(document, issues);
    }

    public static RecipeManifest Recipe { get; } = new(
        Id: "directions-duet",
        Version: "0.1.0",
        License: "GPL-3.0-or-later",
        MinimumEngineVersion: EngineIdentity.EngineVersion,
        InstructionalPurpose: "Turn confirmed classroom directions into line-aligned bilingual microsteps with locked facts intact in both languages.",
        ProhibitedPurposes:
        [
            "certified-translation claims without a recorded human reviewer",
            "safety, legal, emergency, disciplinary, or consequential directions without approved source language and qualified review",
            "altered action count, order, conditions, or deadlines",
        ],
        AllowedInputKinds: ["teacher-entered-text", "approved-glossary"],
        MaximumLane: DataLane.Green,
        RequiredProviderCapabilities: [],
        OutputSchemaId: "schema.directions-duet.v1",
        ValidatorIds: ["document.structural", "locked-fields", "duet.glossary"],
        EditorId: "editor.review-session",
        RendererId: "renderer.accessible-html",
        SupportedExports: [RenderTarget.AccessibleHtml, RenderTarget.PrintHtml],
        Warnings: ["The source language remains visible and authoritative beside every translation."],
        EvaluationSuiteVersion: "0.1");
}
