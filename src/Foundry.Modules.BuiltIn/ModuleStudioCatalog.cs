// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.BuiltIn.AccessRemix;
using Foundry.Modules.BuiltIn.BoardToBrief;
using Foundry.Modules.BuiltIn.DirectionsDuet;
using Foundry.Modules.BuiltIn.FamilyBridge;
using Foundry.Modules.BuiltIn.LessonLoom;
using Foundry.Modules.BuiltIn.ScaffoldSmith;
using Foundry.Modules.BuiltIn.SourceLens;
using Foundry.Modules.BuiltIn.TalkMoves;

namespace Foundry.Modules.BuiltIn;

/// <summary>A stable localization identity paired with neutral English fallback text.</summary>
public sealed record ModuleDisplayText(string LocalizationId, string Fallback);

/// <summary>The small field vocabulary understood by every module-studio surface.</summary>
public enum ModuleFieldKind
{
    Text,
    Multiline,
#pragma warning disable CA1720 // "Integer" is the stable declarative field vocabulary, not a CLR type name.
    Integer,
#pragma warning restore CA1720
    Toggle,
    Choice,
    Lines,
    RecordTable,
    ApprovedArtifact,
    Notice,
}

/// <summary>A submitted value is stable; its visible label is independently localizable.</summary>
public sealed record ModuleChoiceDefinition(string Value, ModuleDisplayText Display);

public sealed record ModuleRecordColumnDefinition(
    string Key,
    ModuleDisplayText Display,
    IReadOnlyList<ModuleChoiceDefinition> Choices);

/// <summary>Shows a field when another field does, or does not, hold one stable submitted value.</summary>
public sealed record ModuleFieldCondition(string FieldKey, string SubmittedValue, bool EqualsSubmittedValue = true);

public sealed record ModuleFieldDefinition(
    string Key,
    ModuleFieldKind Kind,
    ModuleDisplayText Display,
    object? DefaultValue,
    bool IsRequired,
    IReadOnlyList<ModuleChoiceDefinition> Choices,
    IReadOnlyList<ModuleRecordColumnDefinition> Columns,
    int? Minimum = null,
    int? Maximum = null,
    ModuleFieldCondition? Condition = null,
    bool IsSensitive = false);

/// <summary>Whether a mode has a safe synthetic starter or is visibly unavailable.</summary>
public enum ModuleDefaultKind
{
    Synthetic,
    Unavailable,
}

public sealed class ModuleBuildOutcome
{
    private readonly ArtifactPurposeEvidence? _purposeEvidence;

    internal ModuleBuildOutcome(
        ArtifactDocument document,
        RecipeManifest recipe,
        DataLane lane,
        ArtifactPurposeEvidence? purposeEvidence,
        IReadOnlyList<ValidationIssue> issues,
        IArtifactValidator validator,
        IReadOnlyList<string> transformationReport,
        IReadOnlyList<string> notes)
    {
        Document = document;
        Recipe = recipe;
        Lane = lane;
        if (purposeEvidence is not null && !purposeEvidence.AppliesTo(document, lane))
        {
            throw new InvalidOperationException(
                "A module outcome cannot carry purpose evidence for another document or lane.");
        }

        _purposeEvidence = purposeEvidence;
        Issues = issues;
        Validator = validator;
        TransformationReport = transformationReport;
        Notes = notes;
    }

    public ArtifactDocument Document { get; }

    public RecipeManifest Recipe { get; }

    public DataLane Lane { get; }

    public ArtifactPurpose Purpose => _purposeEvidence?.Purpose ?? ArtifactPurpose.Unknown;

    public IReadOnlyList<ValidationIssue> Issues { get; }

    public IArtifactValidator Validator { get; }

    public IReadOnlyList<string> TransformationReport { get; }

    public IReadOnlyList<string> Notes { get; }

    /// <summary>
    /// Creates a draft from this exact typed outcome. Public callers cannot add
    /// purpose here: only evidence already issued to the immutable outcome can
    /// cross into the revision.
    /// </summary>
    public DraftArtifact CreateDraft()
        => _purposeEvidence is null
            ? DraftArtifact.New(Document, Lane)
            : DraftArtifact.NewClassified(Document, Lane, _purposeEvidence);

}

public sealed record ModuleModeDefinition(
    string Key,
    ModuleDisplayText Display,
    RecipeManifest Recipe,
    DataLane Lane,
    IReadOnlyList<ModuleFieldDefinition> Fields,
    ModuleDefaultKind DefaultKind,
    Func<ModuleInputValues, ModuleBuildOutcome>? Build,
    ModuleDisplayText? UnavailableReason)
{
    public bool DefaultsAreSynthetic => DefaultKind == ModuleDefaultKind.Synthetic;

    public bool IsBuildAvailable => Build is not null;
}

public sealed record ModuleDoorDefinition(
    string Id,
    string PublicFileStem,
    ModuleDisplayText Display,
    IReadOnlyList<ModuleModeDefinition> Modes);

/// <summary>
/// Untyped surface values with deliberately loud typed readers. Textual numbers
/// use invariant culture, booleans and choices use stable submitted values, and
/// record rows must carry exactly the declared number of columns.
/// </summary>
public sealed class ModuleInputValues
{
    private readonly Dictionary<string, object?> _values;

    public ModuleInputValues(IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        _values = new Dictionary<string, object?>(values, StringComparer.Ordinal);
    }

    public string Text(string key)
    {
        var value = Required(key);
        return value as string
            ?? throw new ArgumentException($"Field '{key}' must be text.", nameof(key));
    }

#pragma warning disable CA1720 // "Integer" mirrors ModuleFieldKind.Integer for schema consumers.
    public int Integer(string key, int? minimum = null, int? maximum = null)
    {
        var text = Text(key);
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new ArgumentException($"'{text}' is not an invariant-culture whole number for '{key}'.", nameof(key));
        }

        if (minimum is not null && parsed < minimum)
        {
            throw new ArgumentOutOfRangeException(key, parsed, $"Field '{key}' must be at least {minimum.Value.ToString(CultureInfo.InvariantCulture)}.");
        }

        if (maximum is not null && parsed > maximum)
        {
            throw new ArgumentOutOfRangeException(key, parsed, $"Field '{key}' must be at most {maximum.Value.ToString(CultureInfo.InvariantCulture)}.");
        }

        return parsed;
    }
#pragma warning restore CA1720

    public bool Toggle(string key)
    {
        var text = Text(key);
        return text switch
        {
            "true" => true,
            "false" => false,
            _ => throw new ArgumentException($"'{text}' is not the stable boolean value 'true' or 'false' for '{key}'.", nameof(key)),
        };
    }

    public string Choice(string key, IReadOnlyList<string> allowedValues)
    {
        ArgumentNullException.ThrowIfNull(allowedValues);
        var value = Text(key);
        return allowedValues.Contains(value, StringComparer.Ordinal)
            ? value
            : throw new ArgumentException($"'{value}' is not an allowed submitted value for '{key}'.", nameof(key));
    }

    public IReadOnlyList<string> Lines(string key)
        => [.. NormalizedLines(Text(key)).Select(line => line.Trim()).Where(line => line.Length > 0)];

    public IReadOnlyList<IReadOnlyList<string>> Records(string key, int columnCount)
    {
        if (columnCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(columnCount), columnCount, "A record table needs at least one column.");
        }

        var raw = Required(key);
        if (raw is string text)
        {
            var rows = new List<IReadOnlyList<string>>();
            var rowNumber = 0;
            foreach (var line in NormalizedLines(text))
            {
                rowNumber++;
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                rows.Add(CheckedRow(key, rowNumber, [.. line.Split('|').Select(cell => cell.Trim())], columnCount));
            }

            return rows;
        }

        if (raw is IEnumerable<IEnumerable<string>> submittedRows)
        {
            return [.. submittedRows.Select((row, index) => CheckedRow(key, index + 1, [.. row], columnCount))];
        }

        throw new ArgumentException($"Field '{key}' must be pipe-delimited text or a string record table.", nameof(key));
    }

    public ApprovedArtifact ApprovedArtifact(string key)
        => Required(key) as ApprovedArtifact
            ?? throw new ArgumentException($"Field '{key}' must be an ApprovedArtifact; raw documents and drafts are refused.", nameof(key));

    private static string[] NormalizedLines(string text)
        => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

    private static IReadOnlyList<string> CheckedRow(string key, int rowNumber, string[] row, int columnCount)
    {
        if (row.Length != columnCount)
        {
            throw new ArgumentException(
                $"Row {rowNumber.ToString(CultureInfo.InvariantCulture)} of '{key}' has {row.Length.ToString(CultureInfo.InvariantCulture)} columns; exactly {columnCount.ToString(CultureInfo.InvariantCulture)} are required.",
                nameof(key));
        }

        return [.. row];
    }

    private object Required(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _values.TryGetValue(key, out var value) && value is not null
            ? value
            : throw new ArgumentException($"Missing field '{key}'.", nameof(key));
    }
}

/// <summary>
/// The UI-independent inventory of the ten built-in module doors. Human-held
/// modes remain visible but have no build delegate: authority cannot be
/// represented, much less waived, by a submitted field or keyboard gesture.
/// </summary>
public static class ModuleStudioCatalog
{
    public const string DistrictAuthorizationRequiredId = "modules.authorization.district-required";
    public const string AccessPurposeAuthorityRequiredId = "modules.authorization.access-purpose-required";
    public const string AccessOperationChunk = "chunk";
    public const string AccessOperationOneStepPerPanel = "one-step-per-panel";
    public const string LockedFactsReviewedKey = "locked-facts-reviewed";

    private const string DistrictAuthorizationFallback =
        "Written district authorization is required. Authorization cannot be granted from this application.";
    private const string AccessPurposeAuthorityFallback =
        "Access Remix is held until protected specialist review establishes a non-keyboard purpose authority. Typed content cannot grant it.";

    private static readonly ModuleDisplayText DistrictAuthorizationRequired =
        Display(DistrictAuthorizationRequiredId, DistrictAuthorizationFallback);
    private static readonly ModuleDisplayText AccessPurposeAuthorityRequired =
        Display(AccessPurposeAuthorityRequiredId, AccessPurposeAuthorityFallback);

    public static IReadOnlyList<ModuleDoorDefinition> All { get; } =
    [
        BoardDoor(),
        AccessDoor(),
        DirectionsDoor(),
        ScaffoldDoor(),
        TalkDoor(),
        LessonDoor(),
        ExitDoor(),
        RubricDoor(),
        SourceDoor(),
        FamilyDoor(),
    ];

    public static ModuleDoorDefinition ById(string id)
        => All.FirstOrDefault(door => string.Equals(door.Id, id, StringComparison.Ordinal))
            ?? throw new ArgumentException($"No module door '{id}' exists.", nameof(id));

    public static ModuleModeDefinition ByModeKey(string key)
        => All.SelectMany(door => door.Modes).FirstOrDefault(mode => string.Equals(mode.Key, key, StringComparison.Ordinal))
            ?? throw new ArgumentException($"No module mode '{key}' exists.", nameof(key));

    /// <summary>All submitted defaults except absent approved-artifact inputs and non-input notices.</summary>
    public static Dictionary<string, object?> Defaults(ModuleModeDefinition mode)
    {
        ArgumentNullException.ThrowIfNull(mode);
        return mode.Fields
            .Where(field => field.Kind != ModuleFieldKind.Notice && field.DefaultValue is not null)
            .ToDictionary(field => field.Key, field => field.DefaultValue, StringComparer.Ordinal);
    }

    private static ModuleDoorDefinition BoardDoor()
    {
        var roleChoices = Choices("board-to-brief.role",
            ("title", "Title"), ("step", "Step"), ("material", "Material"),
            ("vocabulary", "Vocabulary"), ("date", "Date"), ("note", "Teacher note"));
        var lockChoices = LockChoices("board-to-brief");

        return Door("board-to-brief", "Board to Brief",
            Mode(
                "board-to-brief",
                "Brief",
                BoardToBriefBuilder.Recipe,
                DataLane.Green,
                ModuleDefaultKind.Synthetic,
                BuildBoard,
                [
                    Table("lines", "Verified lines and roles", "Monday|date\nToday's plan|title\nOpen the course notebook.|step\nCourse notebook|material", "board-to-brief",
                        Column("text", "Verified line", "board-to-brief"),
                        Column("role", "Role", "board-to-brief", roleChoices)),
                    Table("locked-fields", "Locked facts", "date|Monday", "board-to-brief",
                        Column("kind", "Kind", "board-to-brief", lockChoices),
                        Column("value", "Exact value", "board-to-brief")),
                    TextField("language", "Document language", "en", "board-to-brief"),
                    TextField("materials-label", "Materials section label", "Materials", "board-to-brief"),
                    TextField("vocabulary-label", "Vocabulary section label", "Vocabulary", "board-to-brief"),
                ]));
    }

    private static ModuleDoorDefinition AccessDoor()
    {
        var operationChoices = Choices("access-remix.operation",
            (AccessOperationChunk, "Chunk steps"),
            (AccessOperationOneStepPerPanel, "One step per panel"));

        return Door("access-remix", "Access Remix",
            UnavailableMode(
                "access-remix",
                "Layout remix",
                AccessRemixer.Recipe,
                DataLane.Green,
                [
                    ApprovedField("artifact", "Protected source artifact - not available", "access-remix"),
                    ChoiceField("operation", "Layout operation", AccessOperationChunk, "access-remix", operationChoices),
                    IntegerField("chunk-size", "Steps per chunk", 3, 1, 99, "access-remix",
                        new ModuleFieldCondition("operation", AccessOperationChunk)),
                    Notice("layout-only", "Access Remix changes layout only; it never changes artifact text.", "access-remix"),
                ],
                AccessPurposeAuthorityRequired));
    }

    private static ModuleDoorDefinition DirectionsDoor()
    {
        return Door("directions-duet", "Directions Duet",
            Mode(
                "directions-duet",
                "Bilingual directions",
                DirectionsDuetBuilder.Recipe,
                DataLane.Green,
                ModuleDefaultKind.Synthetic,
                BuildDirections,
                [
                    TextField("title", "Title", "Folder routine", "directions-duet"),
                    TextField("source-locale", "Source language", "en", "directions-duet"),
                    TextField("target-locale", "Target language", "es", "directions-duet"),
                    Table("steps", "Aligned steps", "Open folder 3.|Abra la carpeta 3.\nRead page 3.|Lea la página 3.", "directions-duet",
                        Column("source", "Source step", "directions-duet"),
                        Column("target", "Target step", "directions-duet")),
                    TextField("glossary-version", "Working glossary version - not approved", "synthetic-1", "directions-duet"),
                    Table("glossary", "Working glossary - not approved", "folder|carpeta", "directions-duet",
                        Column("source", "Source term", "directions-duet"),
                        Column("target", "Working target term - not approved", "directions-duet")),
                    Table("locked-fields", "Locked facts", "number|3", "directions-duet",
                        Column("kind", "Kind", "directions-duet", LockChoices("directions-duet")),
                        Column("value", "Exact value", "directions-duet")),
                    ToggleField(
                        LockedFactsReviewedKey,
                        "Source content reviewed and exact values declared (not language/specialist review)",
                        false,
                        "directions-duet"),
                    TextField("comprehension-check", "Comprehension check", "Show the correct folder.", "directions-duet", required: false),
                ]));
    }

    private static ModuleDoorDefinition ScaffoldDoor()
    {
        return Door("scaffold-smith", "Scaffold Smith",
            Mode(
                "scaffold-smith.packet",
                "Scaffold packet",
                ScaffoldSmithBuilder.Recipes.Single(recipe => recipe.Id == "scaffold-smith.packet"),
                DataLane.Green,
                ModuleDefaultKind.Synthetic,
                BuildScaffoldPacket,
                [
                    TextField("task", "Task", "Explain how a claim uses evidence.", "scaffold-smith.packet"),
                    TextField("target", "Learning target", "Explain how a claim uses evidence.", "scaffold-smith.packet"),
                    TextField("evidence", "Evidence of learning", "A paragraph that links one claim to two cited details.", "scaffold-smith.packet"),
                    LinesField("success-criteria", "Success criteria", "State one claim.\nCite two details.\nExplain each connection.", "scaffold-smith.packet"),
                    Table("scaffolds", "Temporary supports", "Evidence organizer|Organizing cited details|Choosing and explaining evidence|One complete paragraph without the organizer", "scaffold-smith.packet",
                        Column("support", "Support", "scaffold-smith.packet"),
                        Column("barrier", "Barrier addressed", "scaffold-smith.packet"),
                        Column("demand", "Demand preserved", "scaffold-smith.packet"),
                        Column("fade", "Fade criterion", "scaffold-smith.packet")),
                    LinesField("hint-ladder", "Hint ladder", "Underline the claim.\nPoint to the first detail that supports it.", "scaffold-smith.packet", required: false),
                    LinesField("vocabulary-bank", "Vocabulary bank", "claim\nevidence\nconnection", "scaffold-smith.packet", required: false),
                    TextField("sentence-frame", "Optional sentence frame", "The detail ___ supports the claim because ___.", "scaffold-smith.packet", required: false),
                    TextField("language", "Document language", "en", "scaffold-smith.packet"),
                ]),
            Mode(
                "scaffold-smith.task-entry",
                "Task entry",
                ScaffoldSmithBuilder.Recipes.Single(recipe => recipe.Id == "scaffold-smith.task-entry"),
                DataLane.Green,
                ModuleDefaultKind.Synthetic,
                BuildTaskEntry,
                [
                    TextField("task", "Task", "Prepare the display board.", "scaffold-smith.task-entry"),
                    LinesField("materials", "Materials", "Display board\nPrinted captions\nAdhesive", "scaffold-smith.task-entry", required: false),
                    TextField("first-action", "First action", "Place the title at the top of the board.", "scaffold-smith.task-entry"),
                    LinesField("chunks", "Task chunks", "Arrange the sections.\nAttach the title and images.\nAdd the captions.", "scaffold-smith.task-entry"),
                    LinesField("help-routes", "Help routes", "Check the sample layout.\nAsk a partner to review the order.", "scaffold-smith.task-entry"),
                    TextField("definition-of-done", "Definition of done", "Every section is attached and readable from one meter away.", "scaffold-smith.task-entry"),
                    LinesField("checkpoints", "Checkpoints", "Layout checked before attaching.\nCaptions checked after attaching.", "scaffold-smith.task-entry", required: false),
                    TextField("fade-criterion", "Fade criterion", "the learner begins the task without the entry card", "scaffold-smith.task-entry"),
                    TextField("language", "Document language", "en", "scaffold-smith.task-entry"),
                ]));
    }

    private static ModuleDoorDefinition TalkDoor()
    {
        return Door("talk-moves", ModulePublicIdentity.DiscussionDesign.DisplayName,
            Mode(
                "talk-moves-studio",
                "Discussion plan",
                TalkMovesBuilder.Recipe,
                DataLane.Green,
                ModuleDefaultKind.Synthetic,
                BuildTalk,
                [
                    TextField("topic", "Discussion topic", "Evidence and competing explanations", "talk-moves-studio"),
                    Table("questions", "Questions, purposes, and evidence", "Which explanation is best supported?|Compare the two explanations.|Details from the supplied source", "talk-moves-studio",
                        Column("question", "Question", "talk-moves-studio"),
                        Column("purpose", "Purpose", "talk-moves-studio"),
                        Column("evidence", "Evidence target", "talk-moves-studio")),
                    LinesField("participation-modes", "Participation modes", "Speak aloud\nWrite a response\nPoint to source evidence", "talk-moves-studio"),
                    TextField("invite-move", "Invite move", "What do you think, in any participation mode?", "talk-moves-studio"),
                    TextField("build-move", "Build move", "Who can add to that idea?", "talk-moves-studio"),
                    TextField("evidence-move", "Press for evidence move", "Where in the source do you see that?", "talk-moves-studio"),
                    TextField("repair-move", "Repair move", "Restate what you heard before you disagree.", "talk-moves-studio"),
                    TextField("synthesize-move", "Synthesize move", "Who can connect the two explanations?", "talk-moves-studio"),
                    LinesField("sentence-frames", "Optional sentence starters", "I agree with ___ because ___.\nThe source shows ___.", "talk-moves-studio", required: false),
                    TextField("language", "Document language", "en", "talk-moves-studio"),
                ]));
    }

    private static ModuleDoorDefinition LessonDoor()
    {
        return Door("lesson-loom", ModulePublicIdentity.LessonDesign.DisplayName,
            Mode(
                "lesson-loom",
                "Lesson plan",
                LessonLoomBuilder.Recipe,
                DataLane.Green,
                ModuleDefaultKind.Synthetic,
                BuildLesson,
                [
                    TextField("title", "Lesson title", "Comparing evidence", "lesson-loom"),
                    TextField("target", "Learning target", "Compare two explanations using source evidence.", "lesson-loom"),
                    TextField("evidence", "Evidence of learning", "A written comparison citing one detail for each explanation.", "lesson-loom"),
                    IntegerField("total-minutes", "Total minutes", 45, 1, 240, "lesson-loom"),
                    Table("phases", "Lesson phases", "Launch|10|Read both explanations.|Initial comparison recorded|Clarify what counts as a comparison\nEvidence work|25|Mark one supporting detail for each explanation.|Evidence notes checked|Model one evidence link if needed\nClosure|10|Write the comparison.|Comparisons collected|Use the common gap for the next warm-up", "lesson-loom",
                        Column("name", "Phase", "lesson-loom"),
                        Column("minutes", "Minutes", "lesson-loom"),
                        Column("learner-work", "Learner work", "lesson-loom"),
                        Column("check", "Check", "lesson-loom"),
                        Column("response", "Planned response", "lesson-loom")),
                    LinesField("materials", "Materials", "Two synthetic explanation cards\nEvidence organizer", "lesson-loom"),
                    LinesField("access-routes", "Access routes", "Read-aloud option\nPointing or writing response", "lesson-loom"),
                    LinesField("contingencies", "Contingencies", "If reading takes longer, shorten the evidence share while preserving closure.", "lesson-loom", required: false),
                    TextField("language", "Document language", "en", "lesson-loom"),
                ]));
    }

    private static ModuleDoorDefinition ExitDoor()
    {
        return Door("exit-lens", ModulePublicIdentity.FormativeEvidence.DisplayName,
            UnavailableMode(
                "exit-lens",
                "Response clustering",
                ExitLens.ExitLensSession.Recipe,
                DataLane.Amber,
                [
                    TextField("learning-target", "Learning target", "", "exit-lens"),
                    IntegerField("suppression-threshold", "Suppression threshold", 4, 1, 99, "exit-lens"),
                    Table("clusters", "Teacher-defined clusters and routes", "", "exit-lens",
                        Column("name", "Cluster", "exit-lens"),
                        Column("hypothesis", "Misconception hypothesis", "exit-lens"),
                        Column("route", "Teacher-authored route", "exit-lens")),
                    Table("responses", "Nameless responses and assignments", "", "exit-lens",
                        Column("response", "Nameless response", "exit-lens"),
                        Column("cluster", "Assigned cluster", "exit-lens"), isSensitive: true),
                    Table("hinges", "Hinge questions", "", "exit-lens",
                        Column("question", "Question", "exit-lens"),
                        Column("if-secure", "If secure", "exit-lens"),
                        Column("if-not", "If not secure", "exit-lens")),
                    Notice("reserved-clusters", "Unreadable, Off-target, and Novel / outlier are always reserved clusters.", "exit-lens"),
                ]));
    }

    private static ModuleDoorDefinition RubricDoor()
    {
        var evidenceChoices = Choices("rubric-relay.status",
            ("evidence-found", "Evidence found"),
            ("no-evidence", "No evidence"),
            ("insufficient", "Insufficient evidence"),
            ("unreadable", "Unreadable"));

        return Door("rubric-relay", "Rubric Relay",
            UnavailableMode(
                "rubric-relay",
                "Conference preparation",
                RubricRelay.RubricRelayBuilder.Recipe,
                DataLane.Amber,
                [
                    TextField("assignment", "Assignment", "", "rubric-relay"),
                    MultilineField("artifact", "Deidentified artifact", "", "rubric-relay", isSensitive: true),
                    Table("matrix", "Evidence-to-criterion matrix", "", "rubric-relay",
                        Column("criterion", "Criterion", "rubric-relay"),
                        Column("status", "Evidence status", "rubric-relay", evidenceChoices),
                        Column("quote", "Exact quote", "rubric-relay"),
                        Column("note", "Teacher note", "rubric-relay")),
                    TextField("strength", "One verified strength", "", "rubric-relay"),
                    TextField("revision", "One revision move", "", "rubric-relay"),
                    TextField("question-1", "First conference question", "", "rubric-relay"),
                    TextField("question-2", "Second conference question", "", "rubric-relay"),
                    Notice("no-score", "No score, grade, rank, or numeric rating exists in this mode.", "rubric-relay"),
                ]));
    }

    private static ModuleDoorDefinition SourceDoor()
    {
        return Door("source-lens", ModulePublicIdentity.SourceInquiry.DisplayName,
            Mode(
                "source-lens",
                "Source inquiry",
                SourceLensBuilder.Recipe,
                DataLane.Green,
                ModuleDefaultKind.Synthetic,
                BuildSource,
                [
                    TextField("creator", "Creator", "Workshop author", "source-lens"),
                    TextField("title", "Source title", "A synthetic workshop notice", "source-lens"),
                    TextField("date", "Source date", "2026", "source-lens"),
                    TextField("type", "Source type", "Synthetic notice", "source-lens"),
                    TextField("rights", "Rights", "CC0 synthetic fixture", "source-lens"),
                    TextField("place", "Place", "Training room", "source-lens", required: false),
                    TextField("audience", "Audience", "Workshop participants", "source-lens", required: false),
                    TextField("provenance", "Provenance", "Created for local synthetic rehearsal", "source-lens", required: false),
                    MultilineField("excerpt", "Verified excerpt", "The workshop begins at nine. Bring a pencil and the supplied practice sheet.", "source-lens"),
                    ToggleField("transcript-verified", "Transcript verified by teacher", true, "source-lens"),
                    LinesField("sourcing", "Sourcing prompts", "Who created this notice?\nWhat purpose does the notice state?", "source-lens"),
                    LinesField("context", "Context prompts", "What workshop setting would help explain this notice?", "source-lens", required: false),
                    LinesField("close-reading", "Close-reading prompts", "Which details state time and materials?", "source-lens", required: false),
                    LinesField("corroboration", "Corroboration prompts", "What schedule or materials list could confirm these details?", "source-lens"),
                    LinesField("bounded-interpretation", "Bounded-interpretation prompts", "What can this notice support, and what can it not support?", "source-lens", required: false),
                    IntegerField("observation-rows", "Observation rows", 4, 1, 20, "source-lens"),
                    TextField("language", "Document language", "en", "source-lens"),
                ]));
    }

    private static ModuleDoorDefinition FamilyDoor()
    {
        var translated = new ModuleFieldCondition("target-locale", "", EqualsSubmittedValue: false);

        return Door("family-bridge", ModulePublicIdentity.FamilyCommunication.DisplayName,
            Mode(
                "family-bridge",
                "General family communication",
                FamilyBridgeBuilder.Recipe,
                DataLane.Green,
                ModuleDefaultKind.Synthetic,
                BuildFamily,
                [
                    TextField("title", "Title", "Workshop reminder", "family-bridge"),
                    TextField("source-locale", "Source language", "en", "family-bridge"),
                    TextField("target-locale", "Target language", "es", "family-bridge", required: false),
                    Table("paragraphs", "Aligned paragraphs", "Forms are due June 10.|Los formularios vencen June 10.\nThe workshop begins at nine.|El taller comienza a las nueve.", "family-bridge",
                        Column("source", "Source paragraph", "family-bridge"),
                        Column("target", "Target paragraph", "family-bridge")),
                    TextField("requested-action", "One requested action", "Return the form.", "family-bridge"),
                    TextField("contact", "Help contact", "School office", "family-bridge"),
                    TextField("deadline", "Deadline", "June 10", "family-bridge", required: false),
                    TextField("target-requested-action", "Working target action - not approved", "Devuelva el formulario.", "family-bridge", condition: translated),
                    TextField("target-contact", "Working target contact - not approved", "School office", "family-bridge", condition: translated),
                    TextField("target-deadline", "Working target deadline - not approved", "June 10", "family-bridge", required: false, condition: translated),
                    TextField("glossary-version", "Working glossary version - not approved", "synthetic-1", "family-bridge", condition: translated),
                    Table("glossary", "Working glossary - not approved", "forms|formularios", "family-bridge",
                        Column("source", "Source term", "family-bridge"),
                        Column("target", "Working target term - not approved", "family-bridge"), condition: translated),
                    Table("locked-fields", "Locked facts", "date|June 10", "family-bridge",
                        Column("kind", "Kind", "family-bridge", LockChoices("family-bridge")),
                        Column("value", "Exact value", "family-bridge")),
                    ToggleField(
                        LockedFactsReviewedKey,
                        "Source content reviewed and exact values declared (not language/specialist review)",
                        false,
                        "family-bridge"),
                ]));
    }

    private static ModuleBuildOutcome BuildBoard(ModuleInputValues inputs)
    {
        var lines = inputs.Records("lines", 2)
            .Select(row => new BriefLine(row[0], BriefRoleValue(row[1])))
            .ToList();
        var locked = LockedFields(inputs, "locked-fields");
        var result = BoardToBriefBuilder.Build(
            lines,
            locked,
            inputs.Text("language"),
            inputs.Text("materials-label"),
            inputs.Text("vocabulary-label"));

        return Outcome(result.Document, BoardToBriefBuilder.Recipe, DataLane.Green, result.Issues,
            document => ValidateBoard(document, locked));
    }

    private static ModuleBuildOutcome BuildDirections(ModuleInputValues inputs)
    {
        var steps = inputs.Records("steps", 2).Select(row => new DuetStep(row[0], row[1])).ToList();
        var sourceLocale = inputs.Text("source-locale");
        var targetLocale = inputs.Text("target-locale");
        var glossary = GlossaryValue(inputs, "glossary-version", "glossary");
        var locked = LockedFields(inputs, "locked-fields");
        var lockedInventoryReviewed = inputs.Toggle(LockedFactsReviewedKey);
        var check = NullIfWhiteSpace(inputs.Text("comprehension-check"));
        var result = DirectionsDuetBuilder.Build(
            inputs.Text("title"),
            steps,
            sourceLocale,
            targetLocale,
            glossary,
            locked,
            lockedInventoryReviewed,
            comprehensionCheck: check);
        var confirmedSourceProjection = SourceInventoryProjection(result.Document);
        var requiredNotices = result.Document.Nodes.OfType<TeacherOnlyNotice>().Select(notice => notice.Text).ToList();

        return Outcome(result.Document, DirectionsDuetBuilder.Recipe, DataLane.Green, result.Issues,
            document => ValidateBilingual(
                document,
                glossary,
                locked,
                "duet",
                requireTranslation: true,
                validateLocks: true,
                requireAlignedLocks: true,
                lockedInventoryReviewed,
                sourceLocale,
                targetLocale,
                confirmedSourceProjection,
                requiredNotices));
    }

    private static ModuleBuildOutcome BuildScaffoldPacket(ModuleInputValues inputs)
    {
        var target = new LearningTarget(inputs.Text("target"), inputs.Text("evidence"));
        var criteria = inputs.Lines("success-criteria");
        var scaffolds = inputs.Records("scaffolds", 4)
            .Select(row => new ScaffoldSpec(row[0], row[1], row[2], row[3]))
            .ToList();
        var result = ScaffoldSmithBuilder.BuildPacket(
            inputs.Text("task"),
            target,
            criteria,
            scaffolds,
            inputs.Lines("hint-ladder"),
            inputs.Lines("vocabulary-bank"),
            NullIfWhiteSpace(inputs.Text("sentence-frame")),
            inputs.Text("language"));
        var requiredNotices = result.Document.Nodes.OfType<TeacherOnlyNotice>().Select(notice => notice.Text).ToList();
        var requiredFacts = criteria.Concat(scaffolds.Select(scaffold => scaffold.Support)).Append(target.Statement).ToList();

        return Outcome(result.Document, ScaffoldSmithBuilder.Recipes[0], DataLane.Green, result.Issues,
            document => ValidateRequiredStructure(document, "scaffold.structure", requiredFacts, requiredNotices,
                requiredNode: candidate => candidate.Nodes.OfType<UnorderedList>().Count() >= 2));
    }

    private static ModuleBuildOutcome BuildTaskEntry(ModuleInputValues inputs)
    {
        var chunks = inputs.Lines("chunks");
        var routes = inputs.Lines("help-routes");
        var first = inputs.Text("first-action");
        var done = inputs.Text("definition-of-done");
        var fade = inputs.Text("fade-criterion");
        var result = ScaffoldSmithBuilder.BuildTaskEntry(
            inputs.Text("task"),
            inputs.Lines("materials"),
            first,
            chunks,
            routes,
            done,
            inputs.Lines("checkpoints"),
            fade,
            inputs.Text("language"));
        var requiredNotices = result.Document.Nodes.OfType<TeacherOnlyNotice>().Select(notice => notice.Text).ToList();
        var requiredFacts = chunks.Concat(routes).Append(first).Append(done).Append(fade).ToList();

        return Outcome(result.Document, ScaffoldSmithBuilder.Recipes[1], DataLane.Green, result.Issues,
            document => ValidateRequiredStructure(document, "task-entry.structure", requiredFacts, requiredNotices,
                requiredNode: candidate => candidate.Nodes.OfType<OrderedSteps>().Count() >= 2
                    && candidate.Nodes.OfType<Card>().Count() >= 2));
    }

    private static ModuleBuildOutcome BuildTalk(ModuleInputValues inputs)
    {
        var questions = inputs.Records("questions", 3)
            .Select(row => new DiscussionQuestion(row[0], row[1], row[2]))
            .ToList();
        var modes = inputs.Lines("participation-modes");
        var result = TalkMovesBuilder.Build(
            inputs.Text("topic"),
            questions,
            modes,
            inputs.Text("invite-move"),
            inputs.Text("build-move"),
            inputs.Text("evidence-move"),
            inputs.Text("repair-move"),
            inputs.Text("synthesize-move"),
            inputs.Lines("sentence-frames"),
            inputs.Text("language"));
        var requiredNotices = result.Document.Nodes.OfType<TeacherOnlyNotice>().Select(notice => notice.Text).ToList();
        var requiredFacts = questions.Select(question => question.Question).Concat(modes).Append(TalkMovesBuilder.PassOption).ToList();

        return Outcome(result.Document, TalkMovesBuilder.Recipe, DataLane.Green, result.Issues,
            document => ValidateRequiredStructure(document, "talk.structure", requiredFacts, requiredNotices,
                requiredNode: candidate => candidate.Nodes.OfType<UnorderedList>().Count() >= 2));
    }

    private static ModuleBuildOutcome BuildLesson(ModuleInputValues inputs)
    {
        var totalMinutes = inputs.Integer("total-minutes", 1, 240);
        var phases = inputs.Records("phases", 5)
            .Select(row => new LessonPhase(
                row[0],
                ParseIntegerCell("phases", "minutes", row[1]),
                row[2],
                NullIfWhiteSpace(row[3]),
                NullIfWhiteSpace(row[4])))
            .ToList();
        var target = new LearningTarget(inputs.Text("target"), inputs.Text("evidence"));
        var result = LessonLoomBuilder.Build(
            inputs.Text("title"),
            target,
            totalMinutes,
            phases,
            inputs.Lines("materials"),
            inputs.Lines("access-routes"),
            inputs.Lines("contingencies"),
            inputs.Text("language"));
        var decisions = LessonLoomBuilder.Decisions(phases);

        return Outcome(result.Document, LessonLoomBuilder.Recipe, DataLane.Green, result.Issues,
            document => ValidateLesson(document, target, totalMinutes, decisions));
    }

    private static ModuleBuildOutcome BuildSource(ModuleInputValues inputs)
    {
        var metadata = new SourceMetadata(
            inputs.Text("creator"),
            inputs.Text("title"),
            inputs.Text("date"),
            inputs.Text("type"),
            inputs.Text("rights"),
            inputs.Text("place"),
            inputs.Text("audience"),
            inputs.Text("provenance"));
        var excerpt = inputs.Text("excerpt");
        var prompts = new InquiryPrompts(
            inputs.Lines("sourcing"),
            inputs.Lines("context"),
            inputs.Lines("close-reading"),
            inputs.Lines("corroboration"),
            inputs.Lines("bounded-interpretation"));
        var result = SourceLensBuilder.Build(
            metadata,
            excerpt,
            inputs.Toggle("transcript-verified"),
            prompts,
            inputs.Integer("observation-rows", 1, 20),
            inputs.Text("language"));
        var requiredFacts = new[]
        {
            metadata.Creator, metadata.Title, metadata.Date, metadata.Type, metadata.Rights,
            metadata.Place, metadata.Audience, metadata.Provenance, excerpt, SourceLensBuilder.FormatCitation(metadata),
        }.Where(value => !string.IsNullOrWhiteSpace(value))
            .Concat(prompts.Sourcing)
            .Concat(prompts.Corroboration)
            .ToList();

        return Outcome(result.Document, SourceLensBuilder.Recipe, DataLane.Green, result.Issues,
            document => ValidateRequiredStructure(document, "lens.structure", requiredFacts, [],
                requiredNode: candidate => candidate.Nodes.OfType<Citation>().Any()
                    && candidate.Nodes.OfType<TableNode>().Count() >= 2));
    }

    private static ModuleBuildOutcome BuildFamily(ModuleInputValues inputs)
    {
        var targetLocale = NullIfWhiteSpace(inputs.Text("target-locale"));
        var paragraphs = inputs.Records("paragraphs", 2)
            .Select(row => new BridgeParagraph(row[0], NullIfWhiteSpace(row[1])))
            .ToList();
        var glossary = GlossaryValue(inputs, "glossary-version", "glossary");
        var locked = LockedFields(inputs, "locked-fields");
        var lockedInventoryReviewed = inputs.Toggle(LockedFactsReviewedKey);
        var sourceLocale = inputs.Text("source-locale");
        var result = FamilyBridgeBuilder.Build(
            inputs.Text("title"),
            paragraphs,
            inputs.Text("requested-action"),
            inputs.Text("contact"),
            glossary,
            locked,
            lockedInventoryReviewed,
            NullIfWhiteSpace(inputs.Text("deadline")),
            sourceLocale,
            targetLocale,
            targetRequestedAction: targetLocale is null ? null : NullIfWhiteSpace(inputs.Text("target-requested-action")),
            targetContact: targetLocale is null ? null : NullIfWhiteSpace(inputs.Text("target-contact")),
            targetDeadline: targetLocale is null ? null : NullIfWhiteSpace(inputs.Text("target-deadline")));
        var confirmedSourceProjection = SourceInventoryProjection(result.Document);
        var requiredNotices = result.Document.Nodes.OfType<TeacherOnlyNotice>().Select(notice => notice.Text).ToList();

        return Outcome(result.Document, FamilyBridgeBuilder.Recipe, DataLane.Green, result.Issues,
            document => ValidateFamily(
                document,
                glossary,
                locked,
                lockedInventoryReviewed,
                targetLocale is not null,
                sourceLocale,
                targetLocale,
                !string.IsNullOrWhiteSpace(inputs.Text("deadline")),
                confirmedSourceProjection,
                requiredNotices));
    }

    private static List<ValidationIssue> ValidateBoard(ArtifactDocument document, IReadOnlyList<LockedField> locked)
    {
        var issues = new List<ValidationIssue>();
        if (document.Nodes.OfType<Heading>().Count(heading => heading.Level == 1) != 1)
        {
            issues.Add(ValidationIssue.Blocking("brief.title", "A brief keeps exactly one level-one title through review."));
        }

        issues.AddRange(LockedFieldValidator.Validate(document, locked));
        return issues;
    }

    private static List<ValidationIssue> ValidateBilingual(
        ArtifactDocument document,
        Glossary glossary,
        IReadOnlyList<LockedField> locked,
        string codePrefix,
        bool requireTranslation,
        bool validateLocks,
        bool requireAlignedLocks,
        bool lockedInventoryReviewed,
        string expectedSourceLocale,
        string expectedTargetLocale,
        List<string> confirmedSourceProjection,
        IReadOnlyList<string> requiredNotices)
    {
        var issues = new List<ValidationIssue>();
        var pairs = document.Nodes.OfType<BilingualPair>().ToList();
        if (!HasHeadingOne(document))
        {
            issues.Add(ValidationIssue.Blocking(
                $"{codePrefix}.structure",
                "A non-empty level-one title must survive review."));
        }

        if (pairs.Count == 0)
        {
            issues.Add(ValidationIssue.Blocking($"{codePrefix}.empty", "At least one aligned bilingual item is required."));
        }

        if (pairs.Any(pair =>
            !string.Equals(pair.SourceLocale, expectedSourceLocale, StringComparison.Ordinal)
            || !string.Equals(pair.TargetLocale, expectedTargetLocale, StringComparison.Ordinal)))
        {
            issues.Add(ValidationIssue.Blocking(
                $"{codePrefix}.locale",
                "Bilingual item locale metadata no longer matches the source and target languages selected before review. Return to module inputs to change languages or glossary context."));
        }

        foreach (var pair in pairs)
        {
            if (requireTranslation && string.IsNullOrWhiteSpace(pair.TargetText))
            {
                issues.Add(ValidationIssue.Blocking($"{codePrefix}.target-missing", "An aligned item has no target-language text."));
            }

            foreach (var entry in glossary.Entries)
            {
                if (pair.SourceText.Contains(entry.SourceTerm, StringComparison.OrdinalIgnoreCase)
                    && !pair.TargetText.Contains(entry.TargetTerm, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(ValidationIssue.Blocking($"{codePrefix}.glossary",
                        $"A source item uses '{entry.SourceTerm}' but its translation lacks working glossary term '{entry.TargetTerm}' (not approved by this application)."));
                }
            }
        }

        issues.AddRange(LockedFieldValidator.ValidateInventoryReview(lockedInventoryReviewed));
        RequireSourceInventoryCurrent(
            document,
            lockedInventoryReviewed,
            confirmedSourceProjection,
            issues);
        var alignedPairs = pairs
            .Select(pair => (pair.SourceText, TargetText: (string?)pair.TargetText))
            .ToList();
        if (validateLocks)
        {
            issues.AddRange(requireAlignedLocks
                ? LockedFieldValidator.ValidateAlignedPairs(
                    alignedPairs,
                    locked,
                    $"{codePrefix}.locked",
                    "Aligned item")
                : LockedFieldValidator.ValidateBilingualPairs(
                    alignedPairs,
                    locked,
                    $"{codePrefix}.locked",
                    "Version"));
        }

        RequireNotices(document, requiredNotices, $"{codePrefix}.status", issues);
        return issues;
    }

    private static List<ValidationIssue> ValidateRequiredStructure(
        ArtifactDocument document,
        string code,
        IReadOnlyList<string> requiredFacts,
        IReadOnlyList<string> requiredNotices,
        Func<ArtifactDocument, bool> requiredNode)
    {
        var issues = new List<ValidationIssue>();
        var strings = DocumentText.CollectStrings(document);
        var missingFact = requiredFacts.Any(fact => !strings.Any(value => value.Contains(fact, StringComparison.Ordinal)));
        if (!HasHeadingOne(document) || !requiredNode(document) || missingFact)
        {
            issues.Add(ValidationIssue.Blocking(code, "Review removed mandatory module structure or a load-bearing teacher-authored fact."));
        }

        RequireNotices(document, requiredNotices, code, issues);
        return issues;
    }

    private static List<ValidationIssue> ValidateLesson(
        ArtifactDocument document,
        LearningTarget target,
        int totalMinutes,
        IReadOnlyList<InstructionalDecision> decisions)
    {
        var issues = new List<ValidationIssue>();
        var strings = DocumentText.CollectStrings(document);
        if (!HasHeadingOne(document)
            || !strings.Contains(target.Statement, StringComparer.Ordinal)
            || !strings.Any(value => value.Contains(target.EvidenceOfLearning, StringComparison.Ordinal)))
        {
            issues.Add(ValidationIssue.Blocking("loom.structure", "The lesson target or evidence structure was removed during review."));
        }

        var phaseTable = document.Nodes.OfType<TableNode>().FirstOrDefault(table =>
            table.HeaderRow is not null && table.HeaderRow.SequenceEqual(["Phase", "Minutes", "Learners are doing"], StringComparer.Ordinal));
        if (phaseTable is null || phaseTable.Rows.Count == 0)
        {
            issues.Add(ValidationIssue.Blocking("loom.phases", "The lesson phase table is missing or empty."));
        }
        else
        {
            long sum = 0;
            var validRows = true;
            foreach (var row in phaseTable.Rows)
            {
                if (row.Count != 3
                    || string.IsNullOrWhiteSpace(row[0])
                    || string.IsNullOrWhiteSpace(row[2])
                    || !int.TryParse(row[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes)
                    || minutes <= 0)
                {
                    validRows = false;
                    continue;
                }

                sum += minutes;
            }

            if (!validRows || sum != totalMinutes)
            {
                issues.Add(ValidationIssue.Blocking("loom.timing", "Reviewed phase minutes must remain positive and sum exactly to the available time."));
            }
        }

        var decisionTable = document.Nodes.OfType<TableNode>().FirstOrDefault(table =>
            table.HeaderRow is not null && table.HeaderRow.SequenceEqual(["When you see", "Then"], StringComparer.Ordinal));
        if (decisionTable is null || decisionTable.Rows.Count < 2
            || decisions.Any(decision => !decisionTable.Rows.Any(row => row.Count == 2
                && string.Equals(row[0], decision.WhenYouSee, StringComparison.Ordinal)
                && string.Equals(row[1], decision.Then, StringComparison.Ordinal))))
        {
            issues.Add(ValidationIssue.Blocking("loom.checks", "At least two planned check-response decisions, including closure evidence, must survive review."));
        }

        return issues;
    }

    private static List<ValidationIssue> ValidateFamily(
        ArtifactDocument document,
        Glossary glossary,
        IReadOnlyList<LockedField> locked,
        bool lockedInventoryReviewed,
        bool translated,
        string expectedSourceLocale,
        string? expectedTargetLocale,
        bool requireDeadline,
        List<string> confirmedSourceProjection,
        IReadOnlyList<string> requiredNotices)
    {
        var issues = new List<ValidationIssue>();
        var bodyNodes = document.Nodes
            .TakeWhile(node => node is not Heading { Level: 2 })
            .Where(node => node is Paragraph or BilingualPair)
            .ToList();
        var bodyCount = bodyNodes.Count;
        var hasAction = translated
            ? HasBilingualSection(document, "What we ask")
            : document.Nodes.OfType<Card>().Any(card => card.Title == "What we ask" && !string.IsNullOrWhiteSpace(card.Body));
        var hasContact = translated
            ? HasBilingualSection(document, "Questions? Contact")
            : document.Nodes.OfType<Card>().Any(card => card.Title == "Questions? Contact" && !string.IsNullOrWhiteSpace(card.Body));
        var hasDeadline = !requireDeadline || (translated
            ? HasBilingualSection(document, "By when")
            : document.Nodes.OfType<Card>().Any(card => card.Title == "By when" && !string.IsNullOrWhiteSpace(card.Body)));
        if (!HasHeadingOne(document) || bodyCount == 0 || !hasAction || !hasContact || !hasDeadline)
        {
            issues.Add(ValidationIssue.Blocking("bridge.structure", "The title, message, requested action, and help contact must survive review."));
        }

        var sourceParagraphs = bodyNodes.Select(node => node switch
        {
            Paragraph paragraph => paragraph.Text,
            BilingualPair pair => pair.SourceText,
            _ => null,
        }).Where(text => text is not null).Cast<string>().ToList();
        if (AverageSentenceWords(sourceParagraphs) > FamilyBridgeBuilder.MaxAverageSentenceWords)
        {
            issues.Add(ValidationIssue.Blocking("bridge.readability", "Reviewed body text exceeds the plain-language sentence-length limit."));
        }

        if (translated)
        {
            issues.AddRange(ValidateBilingual(
                document,
                glossary,
                locked,
                "bridge",
                requireTranslation: true,
                validateLocks: false,
                requireAlignedLocks: false,
                lockedInventoryReviewed,
                expectedSourceLocale,
                expectedTargetLocale!,
                confirmedSourceProjection,
                requiredNotices));
            var structuredPairs = new List<(string RoleLabel, string SourceText, string? TargetText)>();
            foreach (var role in new[]
            {
                (Heading: "What we ask", Label: "requested action"),
                (Heading: "By when", Label: "deadline"),
                (Heading: "Questions? Contact", Label: "help contact"),
            })
            {
                if (FindBilingualSection(document, role.Heading) is { } pair)
                {
                    structuredPairs.Add((role.Label, pair.SourceText, pair.TargetText));
                }
            }

            issues.AddRange(LockedFieldValidator.ValidateBilingualContent(
                [.. bodyNodes.OfType<BilingualPair>().Select(pair => (pair.SourceText, TargetText: (string?)pair.TargetText))],
                structuredPairs,
                locked,
                "bridge.locked",
                "message paragraphs"));
        }
        else
        {
            issues.AddRange(LockedFieldValidator.ValidateInventoryReview(lockedInventoryReviewed));
            RequireSourceInventoryCurrent(
                document,
                lockedInventoryReviewed,
                confirmedSourceProjection,
                issues);
            issues.AddRange(LockedFieldValidator.Validate(
                FamilyBridgeBuilder.SelectTeacherAuthoredSourceContent(document),
                locked));
            RequireNotices(document, requiredNotices, "bridge.status", issues);
        }

        return issues;
    }

    private static void RequireSourceInventoryCurrent(
        ArtifactDocument document,
        bool inventoryReviewed,
        List<string> confirmedSourceProjection,
        List<ValidationIssue> issues)
    {
        if (inventoryReviewed
            && !SourceInventoryProjection(document).SequenceEqual(
                confirmedSourceProjection,
                StringComparer.Ordinal))
        {
            issues.Add(ValidationIssue.Blocking(
                "locked.inventory-review-stale",
                "Source content changed after the exact-value inventory was confirmed. Return to module inputs, review Locked facts, and confirm the source inventory again. This is not language or specialist review."));
        }
    }

    private static List<string> SourceInventoryProjection(ArtifactDocument document)
    {
        var projection = new List<string>
        {
            ProjectionEntry("document-language", document.Language),
        };

        var semanticNodeIndex = 0;
        foreach (var node in document.Nodes)
        {
            switch (node)
            {
                case Heading heading:
                    projection.Add(ProjectionEntry(
                        $"{semanticNodeIndex}:heading",
                        heading.Level.ToString(CultureInfo.InvariantCulture),
                        heading.Text));
                    break;
                case Paragraph paragraph:
                    projection.Add(ProjectionEntry($"{semanticNodeIndex}:paragraph", paragraph.Text));
                    break;
                case OrderedSteps ordered:
                    for (var stepIndex = 0; stepIndex < ordered.Steps.Count; stepIndex++)
                    {
                        projection.Add(ProjectionEntry(
                            $"{semanticNodeIndex}:ordered:{stepIndex}",
                            ordered.Steps[stepIndex]));
                    }

                    break;
                case StepRow step:
                    projection.Add(ProjectionEntry(
                        $"{semanticNodeIndex}:step-source",
                        step.SourceLocale,
                        step.Text));
                    if (step.Symbol is not null)
                    {
                        projection.Add(ProjectionEntry(
                            $"{semanticNodeIndex}:step-symbol",
                            step.Symbol.Asset.Value,
                            step.Symbol.AltText));
                    }

                    break;
                case UnorderedList unordered:
                    for (var itemIndex = 0; itemIndex < unordered.Items.Count; itemIndex++)
                    {
                        projection.Add(ProjectionEntry(
                            $"{semanticNodeIndex}:unordered:{itemIndex}",
                            unordered.Items[itemIndex]));
                    }

                    break;
                case TableNode table:
                    if (table.HeaderRow is not null)
                    {
                        for (var columnIndex = 0; columnIndex < table.HeaderRow.Count; columnIndex++)
                        {
                            projection.Add(ProjectionEntry(
                                $"{semanticNodeIndex}:table-header:{columnIndex}",
                                table.HeaderRow[columnIndex]));
                        }
                    }

                    for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
                    {
                        for (var columnIndex = 0; columnIndex < table.Rows[rowIndex].Count; columnIndex++)
                        {
                            projection.Add(ProjectionEntry(
                                $"{semanticNodeIndex}:table:{rowIndex}:{columnIndex}",
                                table.Rows[rowIndex][columnIndex]));
                        }
                    }

                    break;
                case Card card:
                    projection.Add(ProjectionEntry($"{semanticNodeIndex}:card-title", card.Title));
                    projection.Add(ProjectionEntry($"{semanticNodeIndex}:card-body", card.Body));
                    break;
                case ImageReference image:
                    projection.Add(ProjectionEntry(
                        $"{semanticNodeIndex}:image",
                        image.Asset.Value,
                        image.AltText));
                    break;
                case BilingualPair pair:
                    projection.Add(ProjectionEntry(
                        $"{semanticNodeIndex}:bilingual-source",
                        pair.SourceLocale,
                        pair.SourceText));
                    break;
                case ChoiceSet choices:
                    for (var optionIndex = 0; optionIndex < choices.Options.Count; optionIndex++)
                    {
                        projection.Add(ProjectionEntry(
                            $"{semanticNodeIndex}:choice:{optionIndex}",
                            choices.Options[optionIndex]));
                    }

                    break;
                case EvidenceLink evidence:
                    projection.Add(ProjectionEntry(
                        $"{semanticNodeIndex}:evidence",
                        evidence.Claim,
                        evidence.SourcePointer));
                    break;
                case Citation citation:
                    projection.Add(ProjectionEntry($"{semanticNodeIndex}:citation", citation.Text));
                    break;
                case VectorGraphic graphic:
                    projection.Add(ProjectionEntry($"{semanticNodeIndex}:vector-description", graphic.Description));
                    break;
                case TeacherOnlyNotice or PageBreak:
                    continue;
                default:
                    projection.Add(ProjectionEntry($"{semanticNodeIndex}:unknown-node", node.GetType().FullName));
                    break;
            }

            semanticNodeIndex++;
        }

        return projection;
    }

    private static string ProjectionEntry(string kind, params string?[] values)
        => string.Join(
            '|',
            new[] { kind }.Concat(values.Select(value => value is null
                ? "-1:"
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $"{value.Length}:{value}"))));

    private static double AverageSentenceWords(IEnumerable<string> texts)
    {
        var sentenceLengths = texts
            .SelectMany(text => text.Split(['.', '!', '?'], StringSplitOptions.RemoveEmptyEntries))
            .Select(sentence => sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length)
            .Where(length => length > 0)
            .ToList();
        return sentenceLengths.Count == 0 ? 0 : sentenceLengths.Average();
    }

    private static void RequireNotices(
        ArtifactDocument document,
        IReadOnlyList<string> requiredNotices,
        string code,
        List<ValidationIssue> issues)
    {
        var notices = document.Nodes.OfType<TeacherOnlyNotice>().Select(notice => notice.Text).ToList();
        if (requiredNotices.Any(required => !notices.Contains(required, StringComparer.Ordinal)))
        {
            issues.Add(ValidationIssue.Blocking(code, "A load-bearing teacher notice was removed or altered during review."));
        }
    }

    private static bool HasHeadingOne(ArtifactDocument document)
        => document.Nodes.OfType<Heading>().Any(heading => heading.Level == 1 && !string.IsNullOrWhiteSpace(heading.Text));

    private static bool HasBilingualSection(ArtifactDocument document, string heading)
        => FindBilingualSection(document, heading) is not null;

    private static BilingualPair? FindBilingualSection(ArtifactDocument document, string heading)
    {
        for (var index = 0; index + 1 < document.Nodes.Count; index++)
        {
            if (document.Nodes[index] is Heading { Level: 2 } candidate
                && string.Equals(candidate.Text, heading, StringComparison.Ordinal)
                && document.Nodes[index + 1] is BilingualPair pair
                && !string.IsNullOrWhiteSpace(pair.SourceText)
                && !string.IsNullOrWhiteSpace(pair.TargetText))
            {
                return pair;
            }
        }

        return null;
    }

    private static ModuleBuildOutcome Outcome(
        ArtifactDocument document,
        RecipeManifest recipe,
        DataLane lane,
        IReadOnlyList<ValidationIssue> initialIssues,
        Func<ArtifactDocument, IReadOnlyList<ValidationIssue>> moduleValidation,
        IReadOnlyList<string>? transformationReport = null,
        ArtifactPurposeEvidence? purposeEvidence = null)
    {
        // Builder findings remain gate-bearing until the inputs are corrected
        // and rebuilt. Review edits operate on document nodes, not on the
        // typed source fields that produced these findings; dropping a blocker
        // here would let Gate B approve a draft the builder had just refused.
        var reviewNotices = recipe.Warnings
            .Select((message, index) => ValidationIssue.Warning(
                $"recipe.warning.{index + 1}",
                message,
                requiresAcknowledgement: true))
            .Concat((transformationReport ?? []).Select((message, index) => ValidationIssue.Warning(
                $"recipe.transformation.{index + 1}",
                message,
                requiresAcknowledgement: true)));
        var persistentIssues = DistinctIssues(initialIssues.Concat(reviewNotices));
        var validator = new CatalogArtifactValidator(moduleValidation, persistentIssues);
        var issues = DistinctIssues(persistentIssues.Concat(validator.Validate(document)));
        return new ModuleBuildOutcome(
            document,
            recipe,
            lane,
            purposeEvidence,
            issues,
            validator,
            transformationReport ?? [],
            recipe.Warnings);
    }

    private static IReadOnlyList<ValidationIssue> DistinctIssues(IEnumerable<ValidationIssue> issues)
        => [.. issues.Distinct()];

    private static Glossary GlossaryValue(ModuleInputValues inputs, string versionKey, string tableKey)
        => new(inputs.Text(versionKey),
            [.. inputs.Records(tableKey, 2).Select(row => new GlossaryEntry(row[0], row[1]))]);

    private static IReadOnlyList<LockedField> LockedFields(ModuleInputValues inputs, string key)
        => [.. inputs.Records(key, 2).Select(row => new LockedField(LockedKindValue(row[0]), row[1]))];

    private static BriefRole BriefRoleValue(string value) => value switch
    {
        "title" => BriefRole.Title,
        "step" => BriefRole.Step,
        "material" => BriefRole.Material,
        "vocabulary" => BriefRole.Vocabulary,
        "date" => BriefRole.Date,
        "note" => BriefRole.Note,
        _ => throw new ArgumentException($"Unknown brief role submitted value '{value}'.", nameof(value)),
    };

    private static LockedFieldKind LockedKindValue(string value) => value switch
    {
        "date" => LockedFieldKind.Date,
        "number" => LockedFieldKind.Number,
        "proper-name" => LockedFieldKind.ProperName,
        "negation" => LockedFieldKind.Negation,
        "quotation" => LockedFieldKind.Quotation,
        "citation" => LockedFieldKind.Citation,
        "unit" => LockedFieldKind.Unit,
        "url" => LockedFieldKind.Url,
        "condition" => LockedFieldKind.Condition,
        "rights-metadata" => LockedFieldKind.RightsMetadata,
        _ => throw new ArgumentException($"Unknown locked-field kind submitted value '{value}'.", nameof(value)),
    };

    private static int ParseIntegerCell(string tableKey, string columnKey, string value)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new ArgumentException($"'{value}' is not an invariant-culture whole number in '{tableKey}.{columnKey}'.", nameof(value));

    private static string? NullIfWhiteSpace(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private static ModuleDoorDefinition Door(string id, string fallback, params ModuleModeDefinition[] modes)
        => new(
            id,
            ModulePublicIdentity.FindByLegacyId(id)?.FileStem ?? id,
            Display($"modules.{id}.door", fallback),
            modes);

    private static ModuleModeDefinition Mode(
        string key,
        string fallback,
        RecipeManifest recipe,
        DataLane lane,
        ModuleDefaultKind defaultKind,
        Func<ModuleInputValues, ModuleBuildOutcome> build,
        IReadOnlyList<ModuleFieldDefinition> fields)
        => new(key, Display($"modules.{key}.mode", fallback), recipe, lane, fields, defaultKind, build, null);

    private static ModuleModeDefinition UnavailableMode(
        string key,
        string fallback,
        RecipeManifest recipe,
        DataLane lane,
        IReadOnlyList<ModuleFieldDefinition> fields,
        ModuleDisplayText? unavailableReason = null)
        => new(
            key,
            Display($"modules.{key}.mode", fallback),
            recipe,
            lane,
            fields,
            ModuleDefaultKind.Unavailable,
            null,
            unavailableReason ?? DistrictAuthorizationRequired);

    private static ModuleDisplayText Display(string id, string fallback) => new(id, fallback);

    private static ModuleFieldDefinition TextField(
        string key,
        string fallback,
        string defaultValue,
        string scope,
        bool required = true,
        ModuleFieldCondition? condition = null)
        => Field(key, ModuleFieldKind.Text, fallback, defaultValue, scope, required, condition: condition);

    private static ModuleFieldDefinition MultilineField(
        string key,
        string fallback,
        string defaultValue,
        string scope,
        bool required = true,
        bool isSensitive = false)
        => Field(key, ModuleFieldKind.Multiline, fallback, defaultValue, scope, required, isSensitive: isSensitive);

    private static ModuleFieldDefinition LinesField(
        string key,
        string fallback,
        string defaultValue,
        string scope,
        bool required = true)
        => Field(key, ModuleFieldKind.Lines, fallback, defaultValue, scope, required);

    private static ModuleFieldDefinition IntegerField(
        string key,
        string fallback,
        int defaultValue,
        int minimum,
        int maximum,
        string scope,
        ModuleFieldCondition? condition = null)
        => Field(
            key,
            ModuleFieldKind.Integer,
            fallback,
            defaultValue.ToString(CultureInfo.InvariantCulture),
            scope,
            required: true,
            minimum: minimum,
            maximum: maximum,
            condition: condition);

    private static ModuleFieldDefinition ToggleField(string key, string fallback, bool defaultValue, string scope)
        => Field(key, ModuleFieldKind.Toggle, fallback, defaultValue ? "true" : "false", scope, required: true);

    private static ModuleFieldDefinition ChoiceField(
        string key,
        string fallback,
        string defaultValue,
        string scope,
        IReadOnlyList<ModuleChoiceDefinition> choices)
        => Field(key, ModuleFieldKind.Choice, fallback, defaultValue, scope, required: true, choices: choices);

    private static ModuleFieldDefinition ApprovedField(string key, string fallback, string scope)
        => Field(key, ModuleFieldKind.ApprovedArtifact, fallback, null, scope, required: true);

    private static ModuleFieldDefinition Notice(string key, string fallback, string scope)
        => Field(key, ModuleFieldKind.Notice, fallback, null, scope, required: false);

    private static ModuleFieldDefinition Table(
        string key,
        string fallback,
        string defaultValue,
        string scope,
        ModuleRecordColumnDefinition column1,
        ModuleRecordColumnDefinition column2,
        ModuleRecordColumnDefinition? column3 = null,
        ModuleRecordColumnDefinition? column4 = null,
        ModuleRecordColumnDefinition? column5 = null,
        ModuleFieldCondition? condition = null,
        bool isSensitive = false)
    {
        var columns = new[] { column1, column2, column3, column4, column5 }
            .Where(column => column is not null)
            .Cast<ModuleRecordColumnDefinition>()
            .Select(column => column with
            {
                // A column key such as "source" can carry different meanings
                // in two tables in one mode. The field/table key is therefore
                // part of the localization identity; English fallback text is
                // never used as a disambiguator.
                Display = Display(
                    $"modules.{scope}.field.{key}.column.{column.Key}",
                    column.Display.Fallback),
            })
            .ToList();
        return Field(key, ModuleFieldKind.RecordTable, fallback, defaultValue, scope, required: true, columns: columns, condition: condition, isSensitive: isSensitive);
    }

    private static ModuleRecordColumnDefinition Column(
        string key,
        string fallback,
        string scope,
        IReadOnlyList<ModuleChoiceDefinition>? choices = null)
        => new(key, Display($"modules.{scope}.column.{key}", fallback), choices ?? []);

    private static ModuleFieldDefinition Field(
        string key,
        ModuleFieldKind kind,
        string fallback,
        object? defaultValue,
        string scope,
        bool required,
        IReadOnlyList<ModuleChoiceDefinition>? choices = null,
        IReadOnlyList<ModuleRecordColumnDefinition>? columns = null,
        int? minimum = null,
        int? maximum = null,
        ModuleFieldCondition? condition = null,
        bool isSensitive = false)
        => new(
            key,
            kind,
            Display($"modules.{scope}.field.{key}", fallback),
            defaultValue,
            required,
            choices ?? [],
            columns ?? [],
            minimum,
            maximum,
            condition,
            isSensitive);

    private static IReadOnlyList<ModuleChoiceDefinition> Choices(
        string scope,
        params (string Value, string Fallback)[] choices)
        => [.. choices.Select(choice => new ModuleChoiceDefinition(
            choice.Value,
            Display($"modules.{scope}.choice.{choice.Value}", choice.Fallback)))];

    private static IReadOnlyList<ModuleChoiceDefinition> LockChoices(string scope)
        => Choices($"{scope}.locked-kind",
            ("date", "Date"),
            ("number", "Number"),
            ("proper-name", "Proper name"),
            ("negation", "Negation"),
            ("quotation", "Quotation"),
            ("citation", "Citation"),
            ("unit", "Unit"),
            ("url", "URL"),
            ("condition", "Condition"),
            ("rights-metadata", "Rights metadata"));

    private sealed class CatalogArtifactValidator(
        Func<ArtifactDocument, IReadOnlyList<ValidationIssue>> moduleValidation,
        IEnumerable<ValidationIssue> persistentIssues) : IArtifactValidator
    {
        private readonly IReadOnlyList<ValidationIssue> _persistentIssues = [.. persistentIssues];

        public IReadOnlyList<ValidationIssue> Validate(ArtifactDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);
            return DistinctIssues(DocumentValidator.Validate(document)
                .Concat(moduleValidation(document))
                .Concat(_persistentIssues));
        }
    }
}
