// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Modules.BuiltIn.AllAboard;

// The All Aboard thin slice (plan §10.1, Days 61-75): deterministic builders over
// teacher-entered content. No model is involved anywhere in this file — text-first
// cards are the Release 0.1 contract, and suggestions arrive later as proposals
// into the same review surface.

/// <summary>One teacher-confirmed step; optional symbol and optional aligned translation.</summary>
public sealed record StepSpec(string Text, AssetId? Symbol = null, string? TargetText = null);

public sealed record CardSpec(string Label, string Body = "", AssetId? Symbol = null, string? SymbolAltText = null);

/// <summary>
/// A typed All Aboard build result. Teacher-entered content remains purpose
/// Unknown: the engine cannot infer that content is classroom support rather
/// than an assessment from arbitrary prose, in every language, without the
/// protected specialist authority that governs that distinction.
/// </summary>
public sealed class AllAboardBuildOutcome
{
    internal AllAboardBuildOutcome(
        ArtifactDocument document,
        RecipeManifest recipe,
        DataLane lane)
    {
        ArgumentNullException.ThrowIfNull(document);
        Document = document;
        Recipe = recipe;
        Lane = lane;
        Purpose = ArtifactPurpose.Unknown;
    }

    public ArtifactDocument Document { get; }

    public RecipeManifest Recipe { get; }

    public DataLane Lane { get; }

    public ArtifactPurpose Purpose { get; }

    public DraftArtifact CreateDraft() => DraftArtifact.New(Document, Lane);
}

public static class AllAboardBuilders
{
    public const int MinimumSteps = 3;
    public const int MaximumSteps = 8;

    public static AllAboardBuildOutcome BuildTaskStrip(
        string title,
        IReadOnlyList<StepSpec> steps,
        IAssetCatalog assetCatalog,
        string sourceLocale = "en",
        string? targetLocale = null)
        => Outcome(
            TaskStrip(title, steps, assetCatalog, sourceLocale, targetLocale),
            AllAboardRecipes.TaskStrip);

    public static AllAboardBuildOutcome BuildFirstThen(
        CardSpec first,
        CardSpec then,
        IAssetCatalog assetCatalog,
        string language = "en",
        string firstLabel = "First",
        string thenLabel = "Then")
        => Outcome(
            FirstThen(first, then, assetCatalog, language, firstLabel, thenLabel),
            AllAboardRecipes.FirstThen);

    public static AllAboardBuildOutcome BuildNowNextDone(
        CardSpec now,
        CardSpec next,
        CardSpec done,
        IAssetCatalog assetCatalog,
        string language = "en",
        string nowLabel = "Now",
        string nextLabel = "Next",
        string doneLabel = "Done")
        => Outcome(
            NowNextDone(now, next, done, assetCatalog, language, nowLabel, nextLabel, doneLabel),
            AllAboardRecipes.NowNextDone);

    public static AllAboardBuildOutcome BuildAgencyCards(
        IReadOnlyList<AssetId> symbols,
        IAssetCatalog assetCatalog,
        string language = "en",
        IReadOnlyList<string>? labels = null)
        => Outcome(
            AgencyCards(symbols, assetCatalog, language, labels),
            AllAboardRecipes.AgencyCards);

    /// <summary>A three-to-eight-step task strip; bilingual when a target locale is given.</summary>
    public static ArtifactDocument TaskStrip(
        string title,
        IReadOnlyList<StepSpec> steps,
        IAssetCatalog assetCatalog,
        string sourceLocale = "en",
        string? targetLocale = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(assetCatalog);
        LanguageTag.RequireValid(sourceLocale, nameof(sourceLocale));
        if (targetLocale is not null)
        {
            LanguageTag.RequireValid(targetLocale, nameof(targetLocale));
        }

        if (steps.Count is < MinimumSteps or > MaximumSteps)
        {
            throw new ArgumentException(
                $"A task strip has {MinimumSteps} to {MaximumSteps} one-action steps; {steps.Count} were given.", nameof(steps));
        }

        var nodes = new List<DocumentNode> { new Heading(1, title) };

        // RC-3: each step is one row — its symbol and its translation live beside it,
        // and adjacency survives rendering. Numbering derives from document order.
        foreach (var step in steps)
        {
            var symbol = step.Symbol is AssetId id ? ImageFor(id, assetCatalog) : null;

            if (targetLocale is null)
            {
                nodes.Add(new StepRow(step.Text, symbol));
            }
            else
            {
                if (string.IsNullOrWhiteSpace(step.TargetText))
                {
                    throw new ArgumentException(
                        $"A bilingual strip requires a translation for every step; '{step.Text}' has none.", nameof(steps));
                }

                nodes.Add(new StepRow(step.Text, symbol, step.TargetText, sourceLocale, targetLocale));
            }
        }

        return new ArtifactDocument(nodes, sourceLocale);
    }

    public static ArtifactDocument FirstThen(CardSpec first, CardSpec then, IAssetCatalog assetCatalog, string language = "en", string firstLabel = "First", string thenLabel = "Then")
        => Sequence(assetCatalog, language, (firstLabel, first), (thenLabel, then));

    public static ArtifactDocument NowNextDone(CardSpec now, CardSpec next, CardSpec done, IAssetCatalog assetCatalog, string language = "en", string nowLabel = "Now", string nextLabel = "Next", string doneLabel = "Done")
        => Sequence(assetCatalog, language, (nowLabel, now), (nextLabel, next), (doneLabel, done));

    /// <summary>
    /// Agency cards from the libre pack. Agency is content, not chrome. Labels are
    /// overridable per card so a classroom prints "Alto," not the catalog's English
    /// (RC-2); ambiguity notes are curator-to-teacher craft and land as teacher-only
    /// notices, never on the learner card (RC-1).
    /// </summary>
    public static ArtifactDocument AgencyCards(
        IReadOnlyList<AssetId> symbols,
        IAssetCatalog assetCatalog,
        string language = "en",
        IReadOnlyList<string>? labels = null)
    {
        ArgumentNullException.ThrowIfNull(symbols);
        ArgumentNullException.ThrowIfNull(assetCatalog);
        LanguageTag.RequireValid(language, nameof(language));

        if (symbols.Count == 0)
        {
            throw new ArgumentException("An agency deck needs at least one card.", nameof(symbols));
        }

        if (labels is not null && labels.Count != symbols.Count)
        {
            throw new ArgumentException("One label per card, or none.", nameof(labels));
        }

        var nodes = new List<DocumentNode>();
        var teacherNotes = new List<TeacherOnlyNotice>();

        for (var i = 0; i < symbols.Count; i++)
        {
            var provenance = Resolve(symbols[i], assetCatalog);
            var cardLabel = labels?[i] ?? provenance.IntendedMeaning;
            nodes.Add(new ImageReference(symbols[i], provenance.AltText));
            // The label is the learner-facing action, not empty layout filler.
            // Repeating it in the body keeps a Card structurally complete while
            // leaving curator ambiguity notes exclusively in teacher content.
            nodes.Add(new Card(cardLabel, cardLabel));

            if (!string.IsNullOrWhiteSpace(provenance.AmbiguityNotes))
            {
                teacherNotes.Add(new TeacherOnlyNotice($"{provenance.IntendedMeaning}: {provenance.AmbiguityNotes}"));
            }
        }

        nodes.AddRange(teacherNotes);
        return new ArtifactDocument(nodes, language);
    }

    private static ArtifactDocument Sequence(IAssetCatalog assetCatalog, string language, params (string Label, CardSpec Spec)[] cards)
    {
        ArgumentNullException.ThrowIfNull(assetCatalog);
        LanguageTag.RequireValid(language, nameof(language));

        var nodes = new List<DocumentNode>();
        foreach (var (label, spec) in cards)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(spec.Label);
            if (spec.Symbol is AssetId symbol)
            {
                nodes.Add(ImageFor(symbol, assetCatalog));
            }

            var body = string.IsNullOrWhiteSpace(spec.Body) ? spec.Label : spec.Body;
            nodes.Add(new Card($"{label}: {spec.Label}", body));
        }

        return new ArtifactDocument(nodes, language);
    }

    private static ImageReference ImageFor(AssetId id, IAssetCatalog assetCatalog)
    {
        var provenance = Resolve(id, assetCatalog);
        return new ImageReference(id, provenance.AltText);
    }

    private static AssetProvenance Resolve(AssetId id, IAssetCatalog assetCatalog)
        => assetCatalog.Find(id)
            ?? throw new InvalidOperationException($"Symbol '{id.Value}' has no provenance in the catalog; unknown rights block distribution.");

    private static AllAboardBuildOutcome Outcome(
        ArtifactDocument document,
        RecipeManifest recipe)
        => new(document, recipe, DataLane.Green);
}

/// <summary>The thin slice's recipe identities (plan §6.6). Data only; Green lane; no model required.</summary>
public static class AllAboardRecipes
{
    private static readonly IReadOnlyList<string> Prohibited =
    [
        "PECS alignment, equivalence, certification, training, or protocol claims",
        "inference of diagnosis, emotion, preference, intent, capacity, or behavior",
        "silent alteration of an established AAC layout",
        "compliance-only boards that omit agency",
        "model-authored safety directions",
    ];

    public static RecipeManifest TaskStrip { get; } = Manifest("all-aboard.task-strip", "Turn a teacher-confirmed sequence into a printable three-to-eight-step task strip.");

    public static RecipeManifest FirstThen { get; } = Manifest("all-aboard.first-then", "Make a First/Then strip from two teacher-chosen activities.");

    public static RecipeManifest NowNextDone { get; } = Manifest("all-aboard.now-next-done", "Make a Now/Next/Done strip from three teacher-chosen activities.");

    public static RecipeManifest AgencyCards { get; } = Manifest("all-aboard.agency-cards", "Print stop, help, wait, break, different, not-now, and finished cards from the libre symbol pack.");

    public static IReadOnlyList<RecipeManifest> All { get; } = [TaskStrip, FirstThen, NowNextDone, AgencyCards];

    private static RecipeManifest Manifest(string id, string purpose) => new(
        Id: id,
        Version: "0.1.0",
        License: "GPL-3.0-or-later",
        MinimumEngineVersion: EngineIdentity.EngineVersion,
        InstructionalPurpose: purpose,
        ProhibitedPurposes: Prohibited,
        AllowedInputKinds: ["teacher-entered-text", "libre-symbol"],
        MaximumLane: DataLane.Green,
        RequiredProviderCapabilities: [],
        OutputSchemaId: "schema.all-aboard.v1",
        ValidatorIds: ["document.structural"],
        EditorId: "editor.review-session",
        RendererId: "renderer.accessible-html",
        SupportedExports:
        [
            RenderTarget.AccessibleHtml,
            RenderTarget.PrintHtml,
            RenderTarget.PrintPdf,
            RenderTarget.Svg,
        ],
        Warnings: ["Visual supports supplement, never replace, an established AAC system."],
        EvaluationSuiteVersion: "0.1");
}
