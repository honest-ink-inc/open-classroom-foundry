// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.App.WinForms;

/// <summary>
/// Fail-closed state for Gate B's asynchronous browser preview. A render being
/// assigned is not evidence that it was displayed: readiness exists only after
/// the exact current marker completes for the exact revision and request.
/// </summary>
internal sealed class PreviewReadinessState
{
    private ExpectedLoad? _expected;
    private long _generation;

    internal bool IsReady { get; private set; }

    internal long BeginLoad()
    {
        if (_generation == long.MaxValue)
        {
            throw new InvalidOperationException(
                UiStrings.WithoutMnemonic(UiStrings.PreviewGenerationExhausted));
        }

        _generation++;
        _expected = null;
        IsReady = false;
        return _generation;
    }

    internal void Expect(
        long generation,
        ArtifactRevision revision,
        RenderRequest request,
        string marker)
    {
        ArgumentNullException.ThrowIfNull(revision);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(marker);
        if (generation != _generation)
        {
            return;
        }

        _expected = new ExpectedLoad(generation, revision, request, marker);
        IsReady = false;
    }

    /// <summary>
    /// Navigation always revokes current readiness. The expected load remains
    /// so its subsequent, exact DocumentCompleted event can restore readiness.
    /// </summary>
    internal void NavigationStarted() => IsReady = false;

    internal bool ObserveDocumentCompleted(
        ArtifactRevision currentRevision,
        RenderRequest currentRequest,
        string? observedMarker)
    {
        ArgumentNullException.ThrowIfNull(currentRevision);
        ArgumentNullException.ThrowIfNull(currentRequest);
        var expected = _expected;
        IsReady = expected is not null
            && expected.Generation == _generation
            && ReferenceEquals(expected.Revision, currentRevision)
            && Equals(expected.Request, currentRequest)
            && string.Equals(expected.Marker, observedMarker, StringComparison.Ordinal);
        return IsReady;
    }

    internal void Fail(long generation)
    {
        if (generation == _generation)
        {
            _expected = null;
            IsReady = false;
        }
    }

    internal bool IsReadyFor(
        ArtifactRevision revision,
        RenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(revision);
        ArgumentNullException.ThrowIfNull(request);
        var expected = _expected;
        return IsReady
            && expected is not null
            && expected.Generation == _generation
            && ReferenceEquals(expected.Revision, revision)
            && Equals(expected.Request, request);
    }

    private sealed record ExpectedLoad(
        long Generation,
        ArtifactRevision Revision,
        RenderRequest Request,
        string Marker);
}
