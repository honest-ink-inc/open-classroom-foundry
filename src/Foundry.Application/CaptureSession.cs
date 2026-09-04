// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Application;

/// <summary>
/// A point-in-time view of the capture session's authoritative envelope and a
/// detached copy of its bytes. <see cref="Content"/> is owned by the caller;
/// the caller must zero that array when it is no longer needed.
/// </summary>
internal sealed record CaptureEnvelopeByteCopy(SourceEnvelope Envelope, byte[] Content);

/// <summary>
/// The caller's displayed capture generation could not be bound atomically to
/// the requested operation. Capture surfaces must discard that stale preview.
/// </summary>
internal sealed class CaptureGenerationChangedException()
    : InvalidOperationException("The displayed capture generation is no longer authoritative.");

/// <summary>
/// The capture-side presenter (ADR-002): capture → normalize → teacher lane
/// confirmation, with Gate C invokable at any moment and RC-18 lane inheritance
/// at the exit. A teacher may resolve an explicitly provisional unknown input;
/// an established or automatically escalated classification cannot be lowered
/// by that confirmation. Everything downstream inherits the resulting lane, and
/// a flow-computed draft can never fall below its sources.
/// </summary>
public sealed class CaptureSession
{
    private readonly Lock _gate = new();
    private readonly ICaptureSource _source;
    private readonly IDocumentNormalizer _normalizer;
    private readonly ISessionByteStore _store;
    private SourceEnvelope? _envelope;
    private int _operationsInFlight;
    private bool _purgeRequested;

    /// <summary>
    /// Creates a capture session that exclusively owns <paramref name="store"/>.
    /// The source and normalizer must use this same session-scoped store; terminal
    /// cancellation, completion, and Gate C paths purge it as one privacy unit.
    /// </summary>
    public CaptureSession(
        ICaptureSource source,
        IDocumentNormalizer normalizer,
        ISessionByteStore store)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _normalizer = normalizer ?? throw new ArgumentNullException(nameof(normalizer));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public JobStateMachine Machine { get; } = new();

    public SourceEnvelope? Envelope
    {
        get
        {
            lock (_gate)
            {
                return _envelope;
            }
        }
    }

    /// <summary>
    /// Tries to copy the current authoritative envelope without exposing the
    /// session byte store. A copy is refused unless <paramref name="maximumByteCount"/>
    /// is positive, the session is idle and active, and the referenced content
    /// exists with a byte count from 1 through <paramref name="maximumByteCount"/>, inclusive.
    /// On success, the returned content array is detached and caller-owned; the
    /// caller must zero it after use.
    /// </summary>
    internal bool TryCopyAuthoritativeEnvelope(
        int maximumByteCount,
        out CaptureEnvelopeByteCopy? copy)
    {
        copy = null;
        if (maximumByteCount <= 0)
        {
            return false;
        }

        lock (_gate)
        {
            if (_envelope is not { } envelope
                || _operationsInFlight != 0
                || _purgeRequested
                || JobStateMachine.IsTerminal(Machine.State)
                || !_store.TryGet(envelope.Bytes, out var content)
                || content.IsEmpty
                || content.Length > maximumByteCount)
            {
                return false;
            }

            copy = new CaptureEnvelopeByteCopy(envelope, content.ToArray());
            return true;
        }
    }

    /// <summary>
    /// Runs a bounded publication step only while <paramref name="expectedReference"/>
    /// is still the idle, active session generation. The action executes under the
    /// same lock that commits normalization and purge, so a detached preview cannot
    /// be published between a generation check and a competing state change.
    /// </summary>
    internal bool TryPublishForAuthoritativeEnvelope(
        SessionByteReference expectedReference,
        Action publish)
    {
        ArgumentNullException.ThrowIfNull(publish);
        lock (_gate)
        {
            if (_envelope is not { } envelope
                || envelope.Bytes != expectedReference
                || _operationsInFlight != 0
                || _purgeRequested
                || Machine.State != JobState.Normalized)
            {
                return false;
            }

            publish();
            return true;
        }
    }

    public async Task<SourceEnvelope> CaptureAsync(CaptureRequest request, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Machine.State != JobState.New || _purgeRequested || _operationsInFlight != 0)
            {
                throw new InvalidOperationException($"Cannot capture while the session is {Machine.State}.");
            }

            _operationsInFlight++;
        }

        var committed = false;
        try
        {
            var captured = await _source.CaptureAsync(request, cancellationToken).ConfigureAwait(false);
            lock (_gate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Machine.State != JobState.New || _purgeRequested)
                {
                    throw new InvalidOperationException($"Cannot complete capture while the session is {Machine.State}.");
                }

                if (captured is null || !HasValidLaneMetadata(captured))
                {
                    Machine.Transition(JobState.Blocked);
                    _envelope = captured;
                    committed = true;
                    RequestPurgeLocked();
                    throw new InvalidOperationException(
                        "Capture was blocked because the source returned no valid data-lane classification and basis.");
                }

                if (captured.Lane == DataLane.Restricted)
                {
                    Machine.Transition(JobState.Blocked);
                    _envelope = captured;
                    committed = true;
                    RequestPurgeLocked();
                    throw new InvalidOperationException(
                        "Restricted data is blocked at capture in this early release; transient source purge was requested.");
                }

                Machine.Transition(JobState.Imported);
                _envelope = captured;
                committed = true;
                return _envelope;
            }
        }
        catch
        {
            lock (_gate)
            {
                if (!committed)
                {
                    // A source may allocate a reference before returning or
                    // throwing. New has no earlier capture to preserve, so a
                    // failed attempt can safely clear the entire owned store.
                    PurgeFailedCaptureAttemptLocked();
                }
            }

            throw;
        }
        finally
        {
            lock (_gate)
            {
                FinishOperationLocked();
            }
        }
    }

    public Task<SourceEnvelope> NormalizeAsync(
        NormalizationRequest request,
        CancellationToken cancellationToken)
        => NormalizeCoreAsync(request, expectedPreviewReference: null, cancellationToken);

    /// <summary>
    /// Normalizes only if the displayed preview reference is still the exact
    /// idle normalized generation when operation ownership is acquired.
    /// </summary>
    internal Task<SourceEnvelope> NormalizeAsync(
        NormalizationRequest request,
        SessionByteReference expectedPreviewReference,
        CancellationToken cancellationToken)
        => NormalizeCoreAsync(request, expectedPreviewReference, cancellationToken);

    private async Task<SourceEnvelope> NormalizeCoreAsync(
        NormalizationRequest request,
        SessionByteReference? expectedPreviewReference,
        CancellationToken cancellationToken)
    {
        SourceEnvelope envelope;
        JobState state;
        int storedReferenceCount;
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            envelope = _envelope ?? throw new InvalidOperationException("Nothing has been captured.");
            state = Machine.State;
            if (expectedPreviewReference is { } expected
                && (state != JobState.Normalized
                    || envelope.Bytes != expected
                    || _purgeRequested
                    || _operationsInFlight != 0))
            {
                throw new CaptureGenerationChangedException();
            }

            if (state is not (JobState.Imported or JobState.Normalized)
                || _purgeRequested
                || _operationsInFlight != 0)
            {
                throw new InvalidOperationException($"Cannot normalize a capture while it is {state}.");
            }

            _operationsInFlight++;
            storedReferenceCount = _store.Count;
        }

        SourceEnvelope? normalized = null;
        var committed = false;
        try
        {
            normalized = await _normalizer.NormalizeAsync(envelope, request, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The normalizer returned no source envelope.");
            if (!HasValidLaneMetadata(envelope)
                || !Enum.IsDefined(normalized.Lane)
                || !Enum.IsDefined(normalized.LaneBasis))
            {
                throw new InvalidOperationException("Normalization returned an undefined data lane or basis.");
            }

            // A normalizer may discover a higher lane, but it is not an
            // authority for lowering the source classification. Provisional
            // status survives only a lane-preserving normalization that also
            // explicitly preserves that status. Any drift establishes the
            // inherited classification before pair validation, so an Amber →
            // Restricted escalation is blocked and purged instead of mistaken
            // for a malformed provisional envelope and retained for retry.
            var inheritedLane = LanePolicy.Inherit(envelope.Lane, normalized.Lane);
            var inheritedBasis = envelope.LaneBasis == DataLaneBasis.ProvisionalUnknown
                && normalized.LaneBasis == DataLaneBasis.ProvisionalUnknown
                && normalized.Lane == envelope.Lane
                ? DataLaneBasis.ProvisionalUnknown
                : DataLaneBasis.Established;
            normalized = normalized with
            {
                Lane = inheritedLane,
                LaneBasis = inheritedBasis,
            };
            if (!HasValidLaneMetadata(normalized))
            {
                throw new InvalidOperationException("Normalization returned an invalid data-lane classification and basis.");
            }
            lock (_gate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Machine.State != state || _purgeRequested)
                {
                    throw new InvalidOperationException($"Cannot complete normalization while the session is {Machine.State}.");
                }

                if (normalized.Lane == DataLane.Restricted)
                {
                    Machine.Transition(JobState.Blocked);
                    _envelope = normalized;
                    committed = true;
                    RequestPurgeLocked();
                    throw new InvalidOperationException(
                        "Restricted data is blocked during normalization in this early release; transient source purge was requested.");
                }

                if (normalized.Bytes != envelope.Bytes
                    && !ReleaseSupersededReferenceLocked(envelope.Bytes))
                {
                    throw new InvalidOperationException("The superseded capture bytes could not be released.");
                }

                if (state == JobState.Imported)
                {
                    Machine.Transition(JobState.Normalized);
                }

                _envelope = normalized;
                committed = true;
                return normalized;
            }
        }
        catch
        {
            lock (_gate)
            {
                // Keep the last committed envelope for a recoverable retry, but
                // never retain a distinct tentative output returned after a
                // token cancellation or terminal state change.
                if (!committed && normalized is not null && normalized.Bytes != envelope.Bytes)
                {
                    ReleaseTentativeReferenceLocked(normalized.Bytes);
                }
                else if (!committed
                    && !_purgeRequested
                    && (_store.Count != storedReferenceCount || !_store.TryGet(envelope.Bytes, out _)))
                {
                    // A failed normalizer that never returned an envelope can
                    // still have mutated the store. The reference identity is
                    // unknowable here, so end the session and purge the owned
                    // store instead of silently retaining a possible orphan.
                    _purgeRequested = true;
                    if (JobStateMachine.CanTransition(Machine.State, JobState.Cancelled))
                    {
                        Machine.Transition(JobState.Cancelled);
                    }

                    PurgeOwnedBytesLocked();
                }
            }

            throw;
        }
        finally
        {
            lock (_gate)
            {
                FinishOperationLocked();
            }
        }
    }

    /// <summary>
    /// The teacher resolves an explicitly provisional unknown lane. Established
    /// source or detection classifications may only be retained or escalated.
    /// </summary>
    public SourceEnvelope ConfirmLane(DataLane teacherConfirmedLane)
        => ConfirmLaneCore(teacherConfirmedLane, expectedPreviewReference: null);

    /// <summary>
    /// Confirms only the exact generation the capture surface most recently
    /// published as its authoritative preview.
    /// </summary>
    internal SourceEnvelope ConfirmLane(
        DataLane teacherConfirmedLane,
        SessionByteReference expectedPreviewReference)
        => ConfirmLaneCore(teacherConfirmedLane, expectedPreviewReference);

    private SourceEnvelope ConfirmLaneCore(
        DataLane teacherConfirmedLane,
        SessionByteReference? expectedPreviewReference)
    {
        lock (_gate)
        {
            var envelope = _envelope ?? throw new InvalidOperationException("Nothing has been captured.");
            if (Machine.State != JobState.Normalized || _purgeRequested || _operationsInFlight != 0)
            {
                throw new InvalidOperationException($"Cannot confirm a lane while the session is {Machine.State}.");
            }

            if (expectedPreviewReference is { } expected && envelope.Bytes != expected)
            {
                throw new CaptureGenerationChangedException();
            }

            if (!Enum.IsDefined(teacherConfirmedLane))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(teacherConfirmedLane),
                    teacherConfirmedLane,
                    "The teacher-confirmed data lane is undefined.");
            }

            if (!HasValidLaneMetadata(envelope))
            {
                Machine.Transition(JobState.Blocked);
                RequestPurgeLocked();
                throw new InvalidOperationException(
                    "Lane confirmation was blocked because the authoritative data-lane classification and basis are invalid.");
            }

            var confirmedLane = envelope.LaneBasis == DataLaneBasis.ProvisionalUnknown
                ? teacherConfirmedLane
                : LanePolicy.Inherit(envelope.Lane, teacherConfirmedLane);
            var confirmed = envelope with
            {
                Lane = confirmedLane,
                LaneBasis = DataLaneBasis.Established,
            };
            if (confirmedLane == DataLane.Restricted)
            {
                // Restricted is not an authoring lane in an early release. The
                // refusal, state transition, and purge request are one locked
                // act, so no caller can observe a confirmed Restricted session
                // and continue into draft creation.
                _envelope = confirmed;
                Machine.Transition(JobState.Blocked);
                RequestPurgeLocked();
                throw new InvalidOperationException(
                    "Restricted data is blocked in this early release; transient source purge was requested.");
            }

            Machine.Transition(JobState.DataLaneConfirmed);
            _envelope = confirmed;
            return confirmed;
        }
    }

    /// <summary>
    /// Completes the standalone capture surface and purges its owned bytes.
    /// Downstream orchestrators that need <see cref="CreateDraft"/> instead keep
    /// the session open and purge only after the full artifact job completes.
    /// </summary>
    public bool CompleteCapture()
    {
        lock (_gate)
        {
            if (Machine.State != JobState.DataLaneConfirmed || _purgeRequested || _operationsInFlight != 0)
            {
                throw new InvalidOperationException($"Cannot complete capture while the session is {Machine.State}.");
            }

            Machine.Transition(JobState.Completed);
            return RequestPurgeLocked();
        }
    }

    /// <summary>Cancels and purges without racing a late source or normalizer completion.</summary>
    public bool Cancel()
    {
        lock (_gate)
        {
            if (!Machine.TryTransition(JobState.Cancelled))
            {
                return false;
            }

            RequestPurgeLocked();
            return true;
        }
    }

    /// <summary>
    /// Abandons a capture surface that is being disposed without a completed
    /// owner-retained handoff. The transition and purge request are one locked
    /// act, including a truthful retry from an explicit purge-incomplete state.
    /// </summary>
    internal bool AbandonAndPurge()
    {
        lock (_gate)
        {
            if (Machine.State == JobState.TransientSourcesPurged)
            {
                return true;
            }

            if (JobStateMachine.CanTransition(Machine.State, JobState.Cancelled))
            {
                Machine.Transition(JobState.Cancelled);
            }
            else if (Machine.State is not (JobState.Completed or JobState.Cancelled
                or JobState.Blocked or JobState.Declined or JobState.PurgeIncomplete))
            {
                throw new InvalidOperationException(
                    $"Cannot abandon and purge a capture while the session is {Machine.State}.");
            }

            return RequestPurgeLocked();
        }
    }

    /// <summary>Gate C: the adult's pause, available from any in-flight state.</summary>
    public SafetyPauseResult InvokeSafetyPause(DistrictPolicy policy)
    {
        lock (_gate)
        {
            var result = SafetyGate.Invoke(Machine, policy);
            RequestPurgeLocked();
            return result;
        }
    }

    /// <summary>
    /// Purges after a full artifact job has entered Completed or retries a
    /// previously incomplete purge. Callers never transition to the terminal
    /// state themselves; the byte-store result decides which evidence is true.
    /// </summary>
    public bool PurgeTransientSources()
    {
        lock (_gate)
        {
            if (Machine.State is not (JobState.Completed or JobState.Cancelled or JobState.Blocked
                or JobState.Declined or JobState.PurgeIncomplete))
            {
                throw new InvalidOperationException($"Cannot purge transient sources while the session is {Machine.State}.");
            }

            return RequestPurgeLocked();
        }
    }

    /// <summary>RC-18 at the exit: the draft's lane is computed from the confirmed envelope, never hand-passed.</summary>
    public DraftArtifact CreateDraft(ArtifactDocument document)
    {
        lock (_gate)
        {
            var envelope = _envelope ?? throw new InvalidOperationException("Nothing has been captured.");
            if (Machine.State != JobState.DataLaneConfirmed || _purgeRequested || _operationsInFlight != 0)
            {
                throw new InvalidOperationException("Confirm the lane before creating a draft.");
            }

            return DraftFactory.CreateFromSources(document, [envelope]);
        }
    }

    private void FinishOperationLocked()
    {
        _operationsInFlight--;
        if (_operationsInFlight < 0)
        {
            throw new InvalidOperationException("Capture operation ownership became unbalanced.");
        }

        if (_purgeRequested && _operationsInFlight == 0)
        {
            PurgeOwnedBytesLocked();
        }
    }

    private static bool HasValidLaneMetadata(SourceEnvelope envelope)
        => Enum.IsDefined(envelope.Lane)
            && Enum.IsDefined(envelope.LaneBasis)
            && (envelope.LaneBasis != DataLaneBasis.ProvisionalUnknown
                || envelope.Lane == LanePolicy.DefaultForUnknown);

    private bool RequestPurgeLocked()
    {
        _purgeRequested = true;

        // Clear everything already present immediately. If an operation is
        // still running, remain in its truthful precursor state and repeat the
        // purge when that operation settles; only then is terminal evidence safe.
        return PurgeOwnedBytesLocked();
    }

    private bool PurgeOwnedBytesLocked()
    {
        try
        {
            _store.PurgeAll();
            _envelope = null;
        }
        catch
        {
            _envelope = null;
            if (Machine.State != JobState.PurgeIncomplete
                && JobStateMachine.CanTransition(Machine.State, JobState.PurgeIncomplete))
            {
                Machine.Transition(JobState.PurgeIncomplete);
            }

            return false;
        }

        if (_operationsInFlight == 0
            && Machine.State != JobState.TransientSourcesPurged
            && JobStateMachine.CanTransition(Machine.State, JobState.TransientSourcesPurged))
        {
            Machine.Transition(JobState.TransientSourcesPurged);
        }

        return _operationsInFlight == 0 && Machine.State == JobState.TransientSourcesPurged;
    }

    private void PurgeFailedCaptureAttemptLocked()
    {
        try
        {
            _store.PurgeAll();
        }
        catch
        {
            _purgeRequested = true;
            if (Machine.State == JobState.New)
            {
                Machine.Transition(JobState.Cancelled);
            }

            if (Machine.State != JobState.PurgeIncomplete
                && JobStateMachine.CanTransition(Machine.State, JobState.PurgeIncomplete))
            {
                Machine.Transition(JobState.PurgeIncomplete);
            }
        }
    }

    private void ReleaseTentativeReferenceLocked(SessionByteReference reference)
    {
        try
        {
            _store.Release(reference);
        }
        catch
        {
            // A failed release means the session can no longer prove that the
            // tentative result is gone. Convert the active path into an explicit
            // cancellation/purge path; never leave a silent orphan behind.
            _purgeRequested = true;
            if (JobStateMachine.CanTransition(Machine.State, JobState.Cancelled))
            {
                Machine.Transition(JobState.Cancelled);
            }

            PurgeOwnedBytesLocked();
        }
    }

    private bool ReleaseSupersededReferenceLocked(SessionByteReference reference)
    {
        try
        {
            _store.Release(reference);
            if (!_store.TryGet(reference, out _))
            {
                return true;
            }
        }
        catch
        {
        }

        // The new result cannot become authoritative while an older committed
        // reference may still survive. End and purge the whole owned session so
        // callers never observe two live generations.
        _purgeRequested = true;
        if (JobStateMachine.CanTransition(Machine.State, JobState.Cancelled))
        {
            Machine.Transition(JobState.Cancelled);
        }

        PurgeOwnedBytesLocked();
        return false;
    }
}
