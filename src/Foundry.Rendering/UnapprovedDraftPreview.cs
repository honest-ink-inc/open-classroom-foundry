// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Rendering;

/// <summary>
/// A sealed in-process view model for Gate B's visual derivative. It is not a
/// <see cref="RenderedOutput"/>, has no MIME target or destination, and is
/// internal to the application shell. Consequently it cannot satisfy any
/// renderer, exporter, printer, or project-store sink contract.
/// </summary>
internal sealed class UnapprovedDraftPreview
{
    internal UnapprovedDraftPreview(
        ArtifactRevision revision,
        RenderRequest request,
        string documentText,
        string? loadMarker = null)
    {
        Revision = revision;
        Request = request;
        DocumentText = documentText;
        LoadMarker = loadMarker;
    }

    internal ArtifactRevision Revision { get; }

    internal RenderRequest Request { get; }

    /// <summary>
    /// Self-contained HTML consumed only by the locked embedded browser. The
    /// literal banners and print watermark are part of these bytes, not an
    /// overlay supplied by the Form.
    /// </summary>
    internal string DocumentText { get; }

    /// <summary>
    /// Browser-load identity for the review surface. It is present only on the
    /// embedded, visibly unapproved document and can never satisfy an output
    /// sink. The marker binds the exact revision, request, rendered bytes, and
    /// UI load generation so a stale DocumentCompleted event fails closed.
    /// </summary>
    internal string? LoadMarker { get; }
}

/// <summary>
/// Produces a visibly marked local preview directly from a draft revision.
/// This is intentionally not an <see cref="IRenderer"/>: only approved
/// artifacts can enter that output capability. The semantic derivative is
/// nevertheless produced by the exact same HTML/SVG core as approved output.
/// </summary>
internal static class UnapprovedDraftPreviewFactory
{
    internal const string LoadMarkerElementId = "honest-ink-preview-load-marker";

    internal static UnapprovedDraftPreview Create(
        DraftArtifact draft,
        RenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(request);
        if (request.Target is not (RenderTarget.AccessibleHtml or RenderTarget.PrintHtml))
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "An embedded Gate B preview accepts only accessible or print HTML layout.");
        }

        var documentText = AccessibleHtmlRenderer.RenderHtmlDocument(
            draft.Revision.Document,
            request,
            approval: null,
            isUnapprovedPreview: true);
        return new UnapprovedDraftPreview(draft.Revision, request, documentText);
    }

    /// <summary>
    /// Produces the document assigned to Gate B's embedded browser. The marker
    /// is metadata in the unapproved frame, outside the exact semantic
    /// derivative. Approved renderer bytes remain untouched and deterministic.
    /// </summary>
    internal static UnapprovedDraftPreview CreateForBrowser(
        DraftArtifact draft,
        RenderRequest request,
        long loadGeneration)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(loadGeneration);

        var unmarked = Create(draft, request);
        var marker = ComputeLoadMarker(
            unmarked.Revision,
            unmarked.Request,
            unmarked.DocumentText,
            loadGeneration);
        const string headEnd = "</head>";
        var insertionIndex = unmarked.DocumentText.IndexOf(headEnd, StringComparison.Ordinal);
        if (insertionIndex < 0)
        {
            throw new InvalidOperationException(
                "The unapproved preview did not contain its required HTML head boundary.");
        }

        var markerElement = string.Concat(
            "<meta id=\"",
            LoadMarkerElementId,
            "\" name=\"",
            LoadMarkerElementId,
            "\" content=\"",
            marker,
            "\">\n");
        var markedDocument = unmarked.DocumentText.Insert(insertionIndex, markerElement);
        return new UnapprovedDraftPreview(
            unmarked.Revision,
            unmarked.Request,
            markedDocument,
            marker);
    }

    private static string ComputeLoadMarker(
        ArtifactRevision revision,
        RenderRequest request,
        string exactUnmarkedDocument,
        long loadGeneration)
    {
        var binding = string.Join(
            '\n',
            "honest-ink-unapproved-preview/v1",
            revision.Id.Value.ToString("N"),
            revision.Number.ToString(CultureInfo.InvariantCulture),
            ((int)request.Target).ToString(CultureInfo.InvariantCulture),
            ((int)request.Audience).ToString(CultureInfo.InvariantCulture),
            request.TextScalePercent.ToString("R", CultureInfo.InvariantCulture),
            request.TargetLanguageFirst ? "1" : "0",
            exactUnmarkedDocument);
        var digest = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(binding)));
        return string.Concat(
            "v1-",
            loadGeneration.ToString("x16", CultureInfo.InvariantCulture),
            "-",
            digest);
    }
}
