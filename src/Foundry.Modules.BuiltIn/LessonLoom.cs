using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Modules.BuiltIn.LessonLoom;

/// <summary>One lesson phase. A phase with a Check must plan its Response — a check without a response is theater.</summary>
public sealed record LessonPhase(string Name, int Minutes, string LearnerWork, string? Check = null, string? Response = null);

public sealed record LessonResult(ArtifactDocument Document, IReadOnlyList<ValidationIssue> Issues);

/// <summary>
/// Lesson Loom (plan §10.7): backward alignment from evidence, with the arithmetic
/// the model never does — the engine checks it. Minutes must sum exactly, at least
/// two checks must carry responses, and closure must produce evidence.
/// </summary>
public static class LessonLoomBuilder
{
    public static LessonResult Build(
        string title,
        LearningTarget target,
        int totalMinutes,
        IReadOnlyList<LessonPhase> phases,
        IReadOnlyList<string> materials,
        IReadOnlyList<string> accessRoutes,
        IReadOnlyList<string>? contingencies = null,
        string language = "en")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(phases);
        ArgumentNullException.ThrowIfNull(materials);
        ArgumentNullException.ThrowIfNull(accessRoutes);

        var issues = new List<ValidationIssue>();

        if (phases.Count == 0)
        {
            issues.Add(ValidationIssue.Blocking("loom.phases", "A lesson needs phases."));
        }

        foreach (var phase in phases)
        {
            if (phase.Minutes <= 0)
            {
                issues.Add(ValidationIssue.Blocking("loom.minutes", $"Phase '{phase.Name}' has no time."));
            }

            if (phase.Check is not null && string.IsNullOrWhiteSpace(phase.Response))
            {
                issues.Add(ValidationIssue.Blocking("loom.check-response",
                    $"Phase '{phase.Name}' checks '{phase.Check}' but plans no response; a check without a response is theater."));
            }
        }

        var sum = phases.Sum(p => p.Minutes);
        if (phases.Count > 0 && sum != totalMinutes)
        {
            issues.Add(ValidationIssue.Blocking("loom.timing",
                $"Phase minutes sum to {sum}, not the {totalMinutes} available. Transitions count; wishes don't."));
        }

        if (phases.Count(p => p.Check is not null) < 2)
        {
            issues.Add(ValidationIssue.Blocking("loom.checks", "At least two target-relevant checks need response plans."));
        }

        if (phases.Count > 0 && phases[^1].Check is null)
        {
            issues.Add(ValidationIssue.Blocking("loom.closure", "Closure must produce evidence; the last phase has no check."));
        }

        if (materials.Count == 0)
        {
            issues.Add(ValidationIssue.Warning("loom.materials", "No materials declared - declare them or mark them missing."));
        }

        if (accessRoutes.Count == 0)
        {
            issues.Add(ValidationIssue.Warning("loom.access", "No access routes declared; access preserves the target for every learner."));
        }

        var nodes = new List<DocumentNode>
        {
            new Heading(1, title),
            new Paragraph(target.Statement),
            new TeacherOnlyNotice($"Evidence of learning: {target.EvidenceOfLearning}"),
            new TableNode(
                ["Phase", "Minutes", "Learners are doing"],
                [.. phases.Select(IReadOnlyList<string> (p) =>
                    [p.Name, p.Minutes.ToString(System.Globalization.CultureInfo.InvariantCulture), p.LearnerWork])]),
        };

        var decisions = Decisions(phases);
        if (decisions.Count > 0)
        {
            nodes.Add(new Heading(2, "If you see — then"));
            nodes.Add(new TableNode(
                ["When you see", "Then"],
                [.. decisions.Select(IReadOnlyList<string> (d) => [d.WhenYouSee, d.Then])]));
        }

        if (materials.Count > 0)
        {
            nodes.Add(new Heading(2, "Materials"));
            nodes.Add(new UnorderedList(materials));
        }

        if (accessRoutes.Count > 0)
        {
            nodes.Add(new Heading(2, "Access routes"));
            nodes.Add(new UnorderedList(accessRoutes));
        }

        foreach (var contingency in contingencies ?? [])
        {
            nodes.Add(new TeacherOnlyNotice($"Contingency: {contingency}"));
        }

        var document = new ArtifactDocument(nodes, language);
        issues.AddRange(DocumentValidator.Validate(document));
        return new LessonResult(document, issues);
    }

    /// <summary>The shared instructional-decision contract: every planned check/response pair, in phase order.</summary>
    public static IReadOnlyList<InstructionalDecision> Decisions(IReadOnlyList<LessonPhase> phases)
    {
        ArgumentNullException.ThrowIfNull(phases);
        return [.. phases
            .Where(p => p.Check is not null && !string.IsNullOrWhiteSpace(p.Response))
            .Select(p => new InstructionalDecision(p.Check!, p.Response!))];
    }

    public static RecipeManifest Recipe { get; } = new(
        Id: "lesson-loom",
        Version: "0.1.0",
        License: "GPL-3.0-or-later",
        MinimumEngineVersion: EngineIdentity.EngineVersion,
        InstructionalPurpose: "Weave an objective, time, and materials into a feasible lesson with checks that have planned responses.",
        ProhibitedPurposes:
        [
            "invented standard text, source citations, or safety procedures",
            "unsupported research-based labels",
            "learner profiles or mandated-curriculum replacement",
        ],
        AllowedInputKinds: ["teacher-entered-text"],
        MaximumLane: DataLane.Green,
        RequiredProviderCapabilities: [],
        OutputSchemaId: "schema.lesson-loom.v1",
        ValidatorIds: ["document.structural", "loom.timing"],
        EditorId: "editor.review-session",
        RendererId: "renderer.accessible-html",
        SupportedExports: [RenderTarget.AccessibleHtml, RenderTarget.PrintHtml],
        Warnings: ["Minutes sum exactly and include transitions; the engine checks the arithmetic, not the model."],
        EvaluationSuiteVersion: "0.1");
}
