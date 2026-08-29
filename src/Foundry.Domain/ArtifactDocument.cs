namespace Foundry.Domain;

/// <summary>
/// The semantic document tree of implementation plan §6.5. Model output and teacher
/// authoring both land here — never HTML or Markdown. Renderers escape every string;
/// scripts, arbitrary markup, remote resources, commands, and filesystem paths are
/// unrepresentable by construction (there is no node that could carry them).
/// </summary>
public sealed record ArtifactDocument(IReadOnlyList<DocumentNode> Nodes, string? Language = null)
{
    public static ArtifactDocument Empty { get; } = new([]);
}

public abstract record DocumentNode;

public sealed record Heading(int Level, string Text) : DocumentNode;

public sealed record Paragraph(string Text) : DocumentNode;

/// <summary>A sequence of one-action steps; numbering is derived, never stored.</summary>
public sealed record OrderedSteps(IReadOnlyList<string> Steps) : DocumentNode;

public sealed record UnorderedList(IReadOnlyList<string> Items) : DocumentNode;

public sealed record TableNode(IReadOnlyList<string>? HeaderRow, IReadOnlyList<IReadOnlyList<string>> Rows) : DocumentNode;

public sealed record Card(string Title, string Body) : DocumentNode;

/// <summary>References an asset by catalog identity — never by filesystem path (plan §6.5).</summary>
public sealed record ImageReference(AssetId Asset, string AltText) : DocumentNode;

/// <summary>Aligned bilingual content as a semantic pair, so reading order survives rendering (plan §8).</summary>
public sealed record BilingualPair(string SourceText, string TargetText, string SourceLocale, string TargetLocale) : DocumentNode;

/// <summary>A set of genuinely available options; agency options are content, not chrome.</summary>
public sealed record ChoiceSet(IReadOnlyList<string> Options) : DocumentNode;

/// <summary>Links a claim to the authorized input that supports it (Gate B evidence links).</summary>
public sealed record EvidenceLink(string Claim, string SourcePointer) : DocumentNode;

public sealed record Citation(string Text) : DocumentNode;

/// <summary>Teacher-only content; renderers must never place it in learner output.</summary>
public sealed record TeacherOnlyNotice(string Text) : DocumentNode;

/// <summary>Stable asset identity from the asset catalog; never a path.</summary>
public readonly record struct AssetId(string Value);
