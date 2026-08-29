using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Modules.BuiltIn.ExitLens;

public sealed record ExitLensResult(ArtifactDocument Document, IReadOnlyList<ValidationIssue> Issues);

/// <summary>
/// Exit Lens (plan §10.6), Amber by design, synthetic inputs only until written
/// district authorization. Version 1 is teacher-driven clustering: the teacher
/// defines reasoning clusters, assigns every response, and authors every route —
/// model-proposed clusters arrive later as proposals into this same session.
/// The invariants are already law: every response accounted for (the reserved
/// unreadable/off-target/novel bins exist from birth), no response text ever
/// enters the summary, small clusters report suppressed counts without claiming
/// de-identification, and summarizing purges response-level data in the same act.
/// </summary>
public sealed class ExitLensSession
{
    public const string Unreadable = "Unreadable";
    public const string OffTarget = "Off-target";
    public const string Novel = "Novel / outlier";

    private readonly string _learningTarget;
    private readonly int _suppressionThreshold;
    private readonly List<string> _responses = [];
    private readonly Dictionary<int, string> _assignments = [];
    private readonly Dictionary<string, string?> _clusters;
    private readonly Dictionary<string, string> _routes = [];
    private readonly List<(string Question, string IfSecure, string IfNot)> _hingeQuestions = [];

    public ExitLensSession(string learningTarget, int suppressionThreshold = 4)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(learningTarget);
        if (suppressionThreshold < 1)
        {
            throw new ArgumentException("The suppression threshold is district-defined and at least one.", nameof(suppressionThreshold));
        }

        _learningTarget = learningTarget;
        _suppressionThreshold = suppressionThreshold;
        _clusters = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [Unreadable] = null,
            [OffTarget] = null,
            [Novel] = null,
        };
    }

    public int ResponsesRemaining => _responses.Count;

    public int UnassignedCount => _responses.Count - _assignments.Count;

    public void DefineCluster(string name, string? misconceptionHypothesis = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!_clusters.TryAdd(name, misconceptionHypothesis))
        {
            throw new InvalidOperationException($"Cluster '{name}' already exists.");
        }
    }

    public int AddResponse(string namelessResponseText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(namelessResponseText);
        _responses.Add(namelessResponseText);
        return _responses.Count - 1;
    }

    public string ResponseText(int index) => _responses[index];

    public void Assign(int responseIndex, string clusterName)
    {
        _ = _responses[responseIndex];
        if (!_clusters.ContainsKey(clusterName))
        {
            throw new InvalidOperationException($"Cluster '{clusterName}' is not defined.");
        }

        _assignments[responseIndex] = clusterName;
    }

    public void SetRoute(string clusterName, string teacherAuthoredRoute)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(teacherAuthoredRoute);
        if (!_clusters.ContainsKey(clusterName))
        {
            throw new InvalidOperationException($"Cluster '{clusterName}' is not defined.");
        }

        _routes[clusterName] = teacherAuthoredRoute;
    }

    public void AddHingeQuestion(string question, string ifSecure, string ifNot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);
        if (_hingeQuestions.Count >= 2)
        {
            throw new InvalidOperationException("One or two hinge questions; more is a quiz.");
        }

        _hingeQuestions.Add((question, ifSecure, ifNot));
    }

    /// <summary>
    /// Builds the counts-only summary and purges every response in the same act:
    /// after this returns, no response-level text exists in the session.
    /// </summary>
    public ExitLensResult Summarize()
    {
        var issues = new List<ValidationIssue>();

        if (_responses.Count == 0)
        {
            issues.Add(ValidationIssue.Blocking("exitlens.empty", "There are no responses to summarize."));
        }

        if (UnassignedCount > 0)
        {
            issues.Add(ValidationIssue.Blocking("exitlens.unassigned",
                $"{UnassignedCount} response(s) are unaccounted for; every response lands somewhere, including the unreadable and outlier bins."));
        }

        var counts = _clusters.Keys.ToDictionary(
            name => name,
            name => _assignments.Values.Count(v => string.Equals(v, name, StringComparison.Ordinal)),
            StringComparer.Ordinal);

        foreach (var (name, count) in counts)
        {
            if (count > 0 && name is not (Unreadable or OffTarget or Novel) && !_routes.ContainsKey(name))
            {
                issues.Add(ValidationIssue.Blocking("exitlens.route",
                    $"Cluster '{name}' has responses but no instructional route; a cluster without a next move is a label, not a plan."));
            }
        }

        if (DocumentValidator.HasBlockingIssues(issues))
        {
            return new ExitLensResult(ArtifactDocument.Empty, issues);
        }

        var total = _responses.Count;
        var rows = counts
            .Where(kv => kv.Value > 0)
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(IReadOnlyList<string> (kv) =>
            [
                kv.Key,
                kv.Value < _suppressionThreshold ? $"fewer than {_suppressionThreshold}" : kv.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                kv.Value < _suppressionThreshold ? "—" : $"{100 * kv.Value / total}%",
                _routes.GetValueOrDefault(kv.Key, "—"),
            ])
            .ToList();

        var nodes = new List<DocumentNode>
        {
            new Heading(1, $"Exit check: {_learningTarget}"),
            new Paragraph($"{total} nameless responses, every one accounted for."),
            new TableNode(["Reasoning cluster", "Count", "Share", "Tomorrow's route"], rows),
        };

        foreach (var (question, ifSecure, ifNot) in _hingeQuestions)
        {
            nodes.Add(new Card($"Hinge: {question}", $"If secure: {ifSecure} | If not: {ifNot}"));
        }

        nodes.Add(new TeacherOnlyNotice(
            "Amber summary: counts and teacher-authored routes only. No named groups exist or can; small clusters report "
            + "suppressed counts without any claim of guaranteed de-identification. Store under district-approved rules."));

        // The purge is part of summarizing, not a courtesy afterward.
        _responses.Clear();
        _assignments.Clear();

        return new ExitLensResult(new ArtifactDocument(nodes), issues);
    }

    public static RecipeManifest Recipe { get; } = new(
        Id: "exit-lens",
        Version: "0.1.0",
        License: "GPL-3.0-or-later",
        MinimumEngineVersion: EngineIdentity.EngineVersion,
        InstructionalPurpose: "Turn a nameless response batch into reasoning-cluster counts and teacher-authored next moves - formative visibility without surveillance.",
        ProhibitedPurposes:
        [
            "named groups, rosters, rankings, or student-specific output of any kind",
            "grading, scoring, or longitudinal histories",
            "inference of ability, effort, motivation, personality, or demographics",
            "real student artifacts before written district authorization - synthetic fixtures only",
        ],
        AllowedInputKinds: ["nameless-response-batch-synthetic"],
        MaximumLane: DataLane.Amber,
        RequiredProviderCapabilities: [],
        OutputSchemaId: "schema.exit-lens.v1",
        ValidatorIds: ["document.structural", "exitlens.accounting"],
        EditorId: "editor.review-session",
        RendererId: "renderer.accessible-html",
        SupportedExports: [RenderTarget.AccessibleHtml, RenderTarget.PrintHtml],
        Warnings: ["Summarizing purges response-level data in the same act; there is nothing to keep."],
        EvaluationSuiteVersion: "0.1");
}
