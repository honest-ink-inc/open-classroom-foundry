using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Modules.BuiltIn.BoardToBrief;

/// <summary>The semantic roles a teacher assigns to verified transcript lines (plan §10.2).</summary>
public enum BriefRole
{
    Title,
    Step,
    Material,
    Vocabulary,
    Date,
    Note,
}

public sealed record BriefLine(string Text, BriefRole Role);

public sealed record BriefResult(ArtifactDocument Document, IReadOnlyList<ValidationIssue> Issues);

/// <summary>
/// Board to Brief's structural builder: verified transcript lines plus teacher
/// role assignments become a clean one-page brief. Traceability is structural —
/// only line texts and the teacher's own section labels enter the document, so
/// nothing can be invented; locked facts are verified deterministically.
/// </summary>
public static class BoardToBriefBuilder
{
    public static BriefResult Build(
        IReadOnlyList<BriefLine> lines,
        IReadOnlyList<LockedField> lockedFields,
        string language = "en",
        string materialsLabel = "Materials",
        string vocabularyLabel = "Vocabulary")
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(lockedFields);

        var issues = new List<ValidationIssue>();

        var titles = lines.Where(l => l.Role == BriefRole.Title).ToList();
        if (titles.Count != 1)
        {
            issues.Add(ValidationIssue.Blocking("brief.title", "A brief has exactly one title line."));
        }

        var nodes = new List<DocumentNode>();
        if (titles.Count == 1)
        {
            nodes.Add(new Heading(1, titles[0].Text));
        }

        foreach (var date in lines.Where(l => l.Role == BriefRole.Date))
        {
            nodes.Add(new Paragraph(date.Text));
        }

        var steps = lines.Where(l => l.Role == BriefRole.Step).Select(l => l.Text).ToList();
        if (steps.Count > 0)
        {
            nodes.Add(new OrderedSteps(steps));
        }

        AddSection(nodes, lines, BriefRole.Material, materialsLabel);
        AddSection(nodes, lines, BriefRole.Vocabulary, vocabularyLabel);

        foreach (var note in lines.Where(l => l.Role == BriefRole.Note))
        {
            nodes.Add(new TeacherOnlyNotice(note.Text));
        }

        var document = new ArtifactDocument(nodes, language);
        issues.AddRange(LockedFieldValidator.Validate(document, lockedFields));
        issues.AddRange(DocumentValidator.Validate(document));

        return new BriefResult(document, issues);
    }

    private static void AddSection(List<DocumentNode> nodes, IReadOnlyList<BriefLine> lines, BriefRole role, string label)
    {
        var items = lines.Where(l => l.Role == role).Select(l => l.Text).ToList();
        if (items.Count > 0)
        {
            nodes.Add(new Heading(2, label));
            nodes.Add(new UnorderedList(items));
        }
    }

    public static RecipeManifest Recipe { get; } = new(
        Id: "board-to-brief",
        Version: "0.1.0",
        License: "GPL-3.0-or-later",
        MinimumEngineVersion: EngineIdentity.EngineVersion,
        InstructionalPurpose: "Turn a verified board transcription into clean, sequenced, accessible directions.",
        ProhibitedPurposes:
        [
            "invented dates, objectives, materials, definitions, or missing steps",
            "silent guessing over uncertain transcription",
            "answer generation of any kind",
        ],
        AllowedInputKinds: ["verified-transcript-line", "teacher-entered-text"],
        MaximumLane: DataLane.Green,
        RequiredProviderCapabilities: [],
        OutputSchemaId: "schema.board-to-brief.v1",
        ValidatorIds: ["document.structural", "locked-fields"],
        EditorId: "editor.review-session",
        RendererId: "renderer.accessible-html",
        SupportedExports: [RenderTarget.AccessibleHtml, RenderTarget.PrintHtml],
        Warnings: ["Transcription and interpretation stay separate: verify every uncertain token before assigning roles."],
        EvaluationSuiteVersion: "0.1");
}
