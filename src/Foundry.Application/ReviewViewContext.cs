// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Contracts;

namespace Foundry.Application;

/// <summary>
/// Exact local source or verified-transcription context displayed beside the
/// current draft at Gate B. The description identifies what the supplied text
/// is; this type does not invent a source or claim that an absent source was
/// verified.
/// </summary>
public sealed class ReviewSourceContext
{
    public ReviewSourceContext(string description, string exactText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(exactText);

        Description = description;
        ExactText = exactText;
    }

    public string Description { get; }

    public string ExactText { get; }
}

/// <summary>
/// Non-authorizing view inputs for one Gate B session. The request is the exact
/// HTML layout/profile shown in the in-process preview. It is deliberately not
/// an export, print, or save request, and it grants no approval capability.
/// </summary>
public sealed class ReviewViewContext
{
    private static readonly RenderRequest DefaultPreviewRequest = new(
        RenderTarget.PrintHtml,
        RenderAudience.Learner,
        TextScalePercent: 100,
        TargetLanguageFirst: false);

    public ReviewViewContext(
        RenderRequest previewRequest,
        ReviewSourceContext? source = null,
        IAssetCatalog? assetCatalog = null)
    {
        ArgumentNullException.ThrowIfNull(previewRequest);
        if (previewRequest.Target is not (RenderTarget.AccessibleHtml or RenderTarget.PrintHtml))
        {
            throw new ArgumentOutOfRangeException(
                nameof(previewRequest),
                "Gate B's embedded visual derivative supports accessible or print HTML only.");
        }

        if (!Enum.IsDefined(previewRequest.Audience))
        {
            throw new ArgumentOutOfRangeException(nameof(previewRequest));
        }

        if (!double.IsFinite(previewRequest.TextScalePercent)
            || previewRequest.TextScalePercent < 100
            || previewRequest.TextScalePercent > 200)
        {
            throw new ArgumentOutOfRangeException(
                nameof(previewRequest),
                "Gate B text scale must be between 100 and 200 percent.");
        }

        PreviewRequest = previewRequest;
        Source = source;
        AssetCatalog = assetCatalog;
    }

    /// <summary>
    /// Honest default for manual paths: learner print layout at 100 percent,
    /// source language first, with no represented source context.
    /// </summary>
    public static ReviewViewContext ManualDefault { get; } = new(DefaultPreviewRequest);

    public RenderRequest PreviewRequest { get; }

    public ReviewSourceContext? Source { get; }

    /// <summary>
    /// Exact local bytes used by the visual derivative when the semantic
    /// document names image assets. This grants no output capability; it lets
    /// Gate B show the same local image bytes that an approved render will use.
    /// </summary>
    public IAssetCatalog? AssetCatalog { get; }
}
