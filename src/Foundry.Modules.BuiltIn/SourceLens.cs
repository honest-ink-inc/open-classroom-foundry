// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Modules.BuiltIn.SourceLens;

/// <summary>
/// Source identity. An empty field is a defect; the literal "unknown" is a
/// scholarly statement — the difference between forgetting to ask and honestly
/// not knowing, and this module never lets the first masquerade as the second.
/// </summary>
public sealed record SourceMetadata(
    string Creator,
    string Title,
    string Date,
    string Type,
    string Rights,
    string Place = "",
    string Audience = "",
    string Provenance = "");

public sealed record InquiryPrompts(
    IReadOnlyList<string> Sourcing,
    IReadOnlyList<string> Contextualization,
    IReadOnlyList<string> CloseReading,
    IReadOnlyList<string> Corroboration,
    IReadOnlyList<string> BoundedInterpretation);

public sealed record SourceLensResult(ArtifactDocument Document, IReadOnlyList<ValidationIssue> Issues);

/// <summary>
/// Source Lens (plan §10.12): disciplined source inquiry. Metadata is stated or
/// explicitly unknown, never guessed; the transcript is human-verified or the
/// artifact does not exist; every inquiry set includes genuine sourcing and
/// corroboration; observation and inference are structurally separate columns.
/// </summary>
public static class SourceLensBuilder
{
    public const string Unknown = "unknown";
    public const string NotRecorded = "not recorded";
    public const string ObservationPrompt = "Record an exact observation.";
    public const string InferencePrompt = "Record an inference and explain what supports it.";

    public static SourceLensResult Build(
        SourceMetadata metadata,
        string verifiedExcerpt,
        bool transcriptVerifiedByTeacher,
        InquiryPrompts prompts,
        int observationRows = 4,
        string language = "en")
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(prompts);
        LanguageTag.RequireValid(language, nameof(language));

        var issues = new List<ValidationIssue>();

        foreach (var (value, field) in new[]
        {
            (metadata.Creator, "creator"), (metadata.Title, "title"), (metadata.Date, "date"),
            (metadata.Type, "type"), (metadata.Rights, "rights"),
        })
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                issues.Add(ValidationIssue.Blocking("lens.metadata",
                    $"The {field} is blank. State it, or write '{Unknown}' — an explicit unknown is scholarship; a blank is a guess waiting to happen."));
            }
        }

        if (IsUnknown(metadata.Rights))
        {
            issues.Add(ValidationIssue.Warning("lens.rights-unknown",
                "Rights are explicitly unknown: the inquiry may be taught, but redistribution is blocked until rights are known."));
        }

        if (!transcriptVerifiedByTeacher)
        {
            issues.Add(ValidationIssue.Blocking("lens.transcript",
                "Transcript fidelity is human-verified; an unverified excerpt cannot become an artifact."));
        }

        if (string.IsNullOrWhiteSpace(verifiedExcerpt))
        {
            issues.Add(ValidationIssue.Blocking("lens.excerpt", "There is no source excerpt to inquire into."));
        }

        if (prompts.Sourcing.Count == 0)
        {
            issues.Add(ValidationIssue.Blocking("lens.sourcing",
                "Every inquiry set includes genuine sourcing prompts: who made this, when, and why do we think so."));
        }

        if (prompts.Corroboration.Count == 0)
        {
            issues.Add(ValidationIssue.Blocking("lens.corroboration",
                "Every inquiry set includes corroboration: what other evidence agrees, disagrees, or is missing."));
        }

        var nodes = new List<DocumentNode>
        {
            new Heading(1, metadata.Title),
            new TableNode(
                ["Field", "Record"],
                [
                    Row("Creator", metadata.Creator),
                    Row("Date", metadata.Date),
                    Row("Type", metadata.Type),
                    Row("Place", metadata.Place),
                    Row("Audience", metadata.Audience),
                    Row("Provenance", metadata.Provenance),
                    Row("Rights", metadata.Rights),
                ]),
            new Heading(2, "The source, verbatim"),
            new Paragraph(string.IsNullOrWhiteSpace(verifiedExcerpt) ? Unknown : verifiedExcerpt),
            new Citation(FormatCitation(metadata)),
        };

        AddPromptSection(nodes, "Sourcing", prompts.Sourcing);
        AddPromptSection(nodes, "Context", prompts.Contextualization);
        AddPromptSection(nodes, "Close reading", prompts.CloseReading);
        AddPromptSection(nodes, "Corroboration", prompts.Corroboration);
        AddPromptSection(nodes, "Interpretation, within bounds", prompts.BoundedInterpretation);

        nodes.Add(new Heading(2, "Observe, then infer"));
        nodes.Add(new TableNode(
            ["What I observe (I can point to it)", "What I infer (my thinking, and why)"],
            [.. Enumerable.Range(0, Math.Max(1, observationRows)).Select(
                IReadOnlyList<string> (_) => [ObservationPrompt, InferencePrompt])]));

        nodes.Add(new TeacherOnlyNotice(
            "A primary source is evidence, not transparent truth. Separate the source's perspective from its limitations, its context from our present interpretation; harmful language is discussed, never silently sanitized."));

        var document = new ArtifactDocument(nodes, language);
        issues.AddRange(DocumentValidator.Validate(document));
        return new SourceLensResult(document, issues);
    }

    /// <summary>
    /// The provenance/citation editor's formatter: neutral, deterministic, and
    /// honest — an unknown field prints as "unknown", never as an invented "n.d.".
    /// </summary>
    public static string FormatCitation(SourceMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var parts = new List<string>
        {
            Field(metadata.Creator),
            Field(metadata.Title),
            Field(metadata.Date),
            Field(metadata.Type),
        };

        if (!string.IsNullOrWhiteSpace(metadata.Provenance))
        {
            parts.Add(Field(metadata.Provenance));
        }

        return string.Join(". ", parts) + ".";

        static string Field(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? Unknown : value.Trim().TrimEnd('.');
        }
    }

    private static bool IsUnknown(string value)
        => string.Equals(value?.Trim(), Unknown, StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> Row(string field, string value)
        => [field, string.IsNullOrWhiteSpace(value) ? NotRecorded : value];

    private static void AddPromptSection(List<DocumentNode> nodes, string label, IReadOnlyList<string> prompts)
    {
        if (prompts.Count > 0)
        {
            nodes.Add(new Heading(2, label));
            nodes.Add(new OrderedSteps(prompts));
        }
    }

    public static RecipeManifest Recipe { get; } = new(
        Id: "source-lens",
        Version: "0.1.0",
        License: "GPL-3.0-or-later",
        MinimumEngineVersion: EngineIdentity.EngineVersion,
        InstructionalPurpose: "Turn a teacher-authorized source into disciplined sourcing, context, close-reading, corroboration, and bounded-interpretation inquiry.",
        ProhibitedPurposes:
        [
            "fabricated quotations, metadata, context, intent, or corroboration",
            "presentism, teleology, or unsupported symmetry",
            "silent sanitization of harmful language",
            "reenactment of enslavement, genocide, dispossession, or comparable trauma",
        ],
        AllowedInputKinds: ["teacher-authorized-source", "teacher-entered-text"],
        MaximumLane: DataLane.Green,
        RequiredProviderCapabilities: [],
        OutputSchemaId: "schema.source-lens.v1",
        ValidatorIds: ["document.structural", "lens.metadata"],
        EditorId: "editor.review-session",
        RendererId: "renderer.accessible-html",
        SupportedExports: [RenderTarget.AccessibleHtml, RenderTarget.PrintHtml],
        Warnings: ["Missing metadata is stated as unknown, never guessed; rights govern redistribution."],
        EvaluationSuiteVersion: "0.1");
}
