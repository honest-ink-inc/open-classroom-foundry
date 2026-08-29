using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Modules.BuiltIn.ScaffoldSmith;

/// <summary>One temporary support with its complete rationale — no rationale, no scaffold.</summary>
public sealed record ScaffoldSpec(string Support, string BarrierAddressed, string DemandPreserved, string FadeCriterion);

public sealed record ScaffoldResult(ArtifactDocument Document, IReadOnlyList<ValidationIssue> Issues);

/// <summary>
/// Scaffold Smith (plan §10.3): temporary, removable supports that preserve the
/// learning target. Every scaffold carries barrier, preserved demand, and fade
/// criterion; the removal plan prints on the teacher page so temporariness is
/// visible, not aspirational. Supports are explicitly optional to the learner.
/// Includes the TaskDock task-entry preset (ADR-005).
/// </summary>
public static class ScaffoldSmithBuilder
{
    public static ScaffoldResult BuildPacket(
        string task,
        LearningTarget target,
        IReadOnlyList<string> successCriteria,
        IReadOnlyList<ScaffoldSpec> scaffolds,
        IReadOnlyList<string>? hintLadder = null,
        IReadOnlyList<string>? vocabularyBank = null,
        string? sentenceFrame = null,
        string language = "en")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(task);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(successCriteria);
        ArgumentNullException.ThrowIfNull(scaffolds);

        var issues = new List<ValidationIssue>();

        if (string.IsNullOrWhiteSpace(target.Statement) || string.IsNullOrWhiteSpace(target.EvidenceOfLearning))
        {
            issues.Add(ValidationIssue.Blocking("scaffold.target", "A scaffold packet needs the target and its evidence; supports without a target are busywork."));
        }

        if (successCriteria.Count == 0)
        {
            issues.Add(ValidationIssue.Blocking("scaffold.criteria", "All original success criteria must remain represented."));
        }

        if (scaffolds.Count == 0)
        {
            issues.Add(ValidationIssue.Blocking("scaffold.none", "There is no scaffold to build."));
        }

        for (var i = 0; i < scaffolds.Count; i++)
        {
            var s = scaffolds[i];
            if (string.IsNullOrWhiteSpace(s.Support) || string.IsNullOrWhiteSpace(s.BarrierAddressed)
                || string.IsNullOrWhiteSpace(s.DemandPreserved) || string.IsNullOrWhiteSpace(s.FadeCriterion))
            {
                issues.Add(ValidationIssue.Blocking("scaffold.rationale",
                    $"Scaffold {i + 1} must state its barrier, the demand it preserves, and its fade criterion."));
            }
        }

        if (hintLadder is { Count: 1 })
        {
            issues.Add(ValidationIssue.Blocking("scaffold.ladder", "A hint ladder is progressive; one hint is not a ladder."));
        }

        var nodes = new List<DocumentNode>
        {
            new Heading(1, task),
            new Paragraph(target.Statement),
            new Heading(2, "Success criteria"),
            new UnorderedList(successCriteria),
        };

        if (scaffolds.Count > 0)
        {
            nodes.Add(new Heading(2, "Supports (optional — use what helps, skip what you don't need)"));
            nodes.Add(new UnorderedList([.. scaffolds.Select(s => s.Support)]));
        }

        if (hintLadder is { Count: > 1 })
        {
            nodes.Add(new Heading(2, "Hints — cut apart, take one at a time"));
            nodes.Add(new OrderedSteps(hintLadder));
        }

        if (vocabularyBank is { Count: > 0 })
        {
            nodes.Add(new Heading(2, "Word bank (optional)"));
            nodes.Add(new UnorderedList(vocabularyBank));
        }

        if (!string.IsNullOrWhiteSpace(sentenceFrame))
        {
            nodes.Add(new Card("Sentence frame (optional)", sentenceFrame));
        }

        nodes.Add(new TeacherOnlyNotice($"Evidence of learning: {target.EvidenceOfLearning}"));
        foreach (var s in scaffolds)
        {
            nodes.Add(new TeacherOnlyNotice(
                $"Support: {s.Support} | Barrier addressed: {s.BarrierAddressed} | Demand preserved: {s.DemandPreserved} | Fade when: {s.FadeCriterion}"));
        }

        nodes.Add(new TeacherOnlyNotice(
            "Removal plan: every support above is temporary. Remove each independently the moment its fade criterion is met; the target and criteria never fade."));

        var document = new ArtifactDocument(nodes, language);
        issues.AddRange(DocumentValidator.Validate(document));
        return new ScaffoldResult(document, issues);
    }

    /// <summary>The absorbed TaskDock (ADR-005): task entry as a scaffold with a fade criterion of its own.</summary>
    public static ScaffoldResult BuildTaskEntry(
        string task,
        IReadOnlyList<string> materials,
        string firstAction,
        IReadOnlyList<string> chunks,
        IReadOnlyList<string> helpRoutes,
        string definitionOfDone,
        IReadOnlyList<string>? checkpoints = null,
        string fadeCriterion = "the learner starts within 30 seconds without the card",
        string language = "en")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(task);
        ArgumentNullException.ThrowIfNull(materials);
        ArgumentNullException.ThrowIfNull(chunks);
        ArgumentNullException.ThrowIfNull(helpRoutes);

        var issues = new List<ValidationIssue>();

        if (string.IsNullOrWhiteSpace(firstAction))
        {
            issues.Add(ValidationIssue.Blocking("task-entry.first", "Task entry lives or dies on a concrete 30-second first action."));
        }

        if (chunks.Count == 0)
        {
            issues.Add(ValidationIssue.Blocking("task-entry.chunks", "The task has no chunks; nothing to enter."));
        }

        if (helpRoutes.Count == 0)
        {
            issues.Add(ValidationIssue.Blocking("task-entry.help", "A help route is agency; a task card without one is a wall."));
        }

        if (string.IsNullOrWhiteSpace(definitionOfDone))
        {
            issues.Add(ValidationIssue.Blocking("task-entry.done", "Done must be concrete, or the task never ends."));
        }

        var nodes = new List<DocumentNode> { new Heading(1, task) };

        if (materials.Count > 0)
        {
            nodes.Add(new Heading(2, "Materials"));
            nodes.Add(new UnorderedList(materials));
        }

        if (!string.IsNullOrWhiteSpace(firstAction))
        {
            nodes.Add(new Card("First", firstAction));
        }

        if (chunks.Count > 0)
        {
            nodes.Add(new OrderedSteps(chunks));
        }

        if (checkpoints is { Count: > 0 })
        {
            nodes.Add(new Heading(2, "Checkpoints"));
            nodes.Add(new UnorderedList(checkpoints));
        }

        if (helpRoutes.Count > 0)
        {
            nodes.Add(new Heading(2, "If you're stuck"));
            nodes.Add(new OrderedSteps(helpRoutes));
        }

        if (!string.IsNullOrWhiteSpace(definitionOfDone))
        {
            nodes.Add(new Card("Done means", definitionOfDone));
        }

        nodes.Add(new TeacherOnlyNotice(
            $"Task-entry scaffold (TaskDock preset, ADR-005). This card is temporary: fade when {fadeCriterion}."));

        var document = new ArtifactDocument(nodes, language);
        issues.AddRange(DocumentValidator.Validate(document));
        return new ScaffoldResult(document, issues);
    }

    public static IReadOnlyList<RecipeManifest> Recipes { get; } =
    [
        Manifest("scaffold-smith.packet", "Turn an existing task into temporary, removable supports that preserve the learning target, each with barrier, demand, and fade criterion."),
        Manifest("scaffold-smith.task-entry", "Task breakdown for task initiation: materials, first action, chunks, checkpoints, help routes, and a concrete definition of done (the absorbed TaskDock, ADR-005)."),
    ];

    private static RecipeManifest Manifest(string id, string purpose) => new(
        Id: id,
        Version: "0.1.0",
        License: "GPL-3.0-or-later",
        MinimumEngineVersion: EngineIdentity.EngineVersion,
        InstructionalPurpose: purpose,
        ProhibitedPurposes:
        [
            "completed theses, conclusions, proofs, computations, or source interpretations",
            "diagnosis inference or automatic leveling",
            "IEP or accommodation generation",
            "mandatory frames - every support remains optional",
        ],
        AllowedInputKinds: ["teacher-entered-text"],
        MaximumLane: DataLane.Green,
        RequiredProviderCapabilities: [],
        OutputSchemaId: "schema.scaffold-smith.v1",
        ValidatorIds: ["document.structural"],
        EditorId: "editor.review-session",
        RendererId: "renderer.accessible-html",
        SupportedExports: [RenderTarget.AccessibleHtml, RenderTarget.PrintHtml],
        Warnings: ["Supports alter representation, sequence, pacing, or response mode - never the intellectual work."],
        EvaluationSuiteVersion: "0.1");
}
