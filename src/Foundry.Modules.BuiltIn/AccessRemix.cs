// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Modules.BuiltIn.AccessRemix;

public sealed record RemixResult(ArtifactDocument Document, IReadOnlyList<string> TransformationReport);

/// <summary>
/// Access Remix (plan §10.4), first deterministic transforms: chunking and
/// one-item-per-panel over step rows, with numbering preserved because the
/// renderer derives numbers across page breaks. The transformation report lists
/// every authorized change; the remixer verifies its own item-parity invariant
/// and refuses to return a document whose text differs from its source.
/// Large print is a render option (RenderRequest.TextScalePercent), not a
/// content change — reported here so the teacher sees the whole remix.
/// </summary>
public static class AccessRemixer
{
    public static RemixResult Chunk(ArtifactDocument document, int chunkSize)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (chunkSize < 1)
        {
            throw new ArgumentException("Chunks need at least one step.", nameof(chunkSize));
        }

        var nodes = new List<DocumentNode>();
        var runLength = 0;

        foreach (var node in document.Nodes)
        {
            if (node is StepRow)
            {
                if (runLength == chunkSize)
                {
                    nodes.Add(new PageBreak());
                    runLength = 0;
                }

                runLength++;
            }
            else
            {
                runLength = 0;
            }

            nodes.Add(node);
        }

        return Verified(document, new ArtifactDocument(nodes, document.Language),
            [$"Chunked steps into groups of {chunkSize}; numbering preserved across pages."]);
    }

    public static RemixResult OneStepPerPanel(ArtifactDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var nodes = new List<DocumentNode>();
        foreach (var node in document.Nodes)
        {
            nodes.Add(node);
            if (node is StepRow)
            {
                nodes.Add(new PageBreak());
            }
        }

        return Verified(document, new ArtifactDocument(nodes, document.Language),
            ["One step per panel; numbering preserved across panels."]);
    }

    /// <summary>Item parity is the module's first invariant: a remix that changed a word is not a remix.</summary>
    private static RemixResult Verified(ArtifactDocument source, ArtifactDocument remixed, IReadOnlyList<string> report)
    {
        var before = DocumentText.CollectStrings(source);
        var after = DocumentText.CollectStrings(remixed);

        if (!before.SequenceEqual(after, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("The remix altered content; access transforms layout only. Refusing.");
        }

        return new RemixResult(remixed, report);
    }

    public static RecipeManifest Recipe { get; } = new(
        Id: "access-remix",
        Version: "0.1.0",
        License: "GPL-3.0-or-later",
        MinimumEngineVersion: EngineIdentity.EngineVersion,
        InstructionalPurpose: "Convert a teacher-created artifact into large-print, chunked, and one-item-per-panel variants without touching a word of it.",
        ProhibitedPurposes:
        [
            "altered item difficulty or cued answers",
            "content changes of any kind - layout only",
            "claims that a variant fulfills an individual plan or law",
            "formal or high-stakes assessment conversion",
        ],
        AllowedInputKinds: ["approved-artifact"],
        MaximumLane: DataLane.Green,
        RequiredProviderCapabilities: [],
        OutputSchemaId: "schema.access-remix.v1",
        ValidatorIds: ["document.structural", "remix.item-parity"],
        EditorId: "editor.review-session",
        RendererId: "renderer.accessible-html",
        SupportedExports: [RenderTarget.AccessibleHtml, RenderTarget.PrintHtml],
        Warnings: ["Construct-change warnings are non-dismissable; a formal assessment disguised as a worksheet is refused."],
        EvaluationSuiteVersion: "0.1");
}
