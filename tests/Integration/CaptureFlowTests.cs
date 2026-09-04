using System.Drawing.Imaging;
using System.Text;
using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Infrastructure.Simulated;
using Foundry.Infrastructure.Windows;
using Foundry.Modules.BuiltIn.BoardToBrief;
using Foundry.Storage;

namespace Foundry.Tests.Integration;

/// <summary>
/// The Board to Brief story, end to end and headless: photograph → normalize →
/// lane attestation → OCR → uncertainty resolution → role assignment → brief →
/// inherited-lane draft → review → approval — and at the very end, the Green
/// store honoring or refusing the save on the lane the flow computed.
/// </summary>
public class CaptureFlowTests
{
    private static readonly DateTimeOffset SomeInstant = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    private static byte[] BoardPhoto()
    {
        using var bitmap = new Bitmap(64, 32, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.White);
        }

        using var stream = new MemoryStream();
        GdiPlusImageEncoder.Save(bitmap, stream, ImageFormat.Png);
        return stream.ToArray();
    }

    private static async Task<(ApprovedArtifact Artifact, JobStateMachine Machine)> RunFlowAsync(DataLane teacherAttestedLane)
    {
        var store = new InMemorySessionByteStore();
        var capture = new CaptureSession(
            new SimulatedCameraSource(store, BoardPhoto()),
            new ImageNormalizer(store),
            store);

        await capture.CaptureAsync(new CaptureRequest(SimulatedCameraSource.Kind), CancellationToken.None);
        await capture.NormalizeAsync(new NormalizationRequest(), CancellationToken.None);
        capture.ConfirmLane(teacherAttestedLane);

        var ocr = new FakeOcrService(
            new OcrToken("Finish", 0.98), new OcrToken("chapter", 0.97), new OcrToken("9", 0.41),
            new OcrToken("by", 0.96), new OcrToken("Friday,", 0.95), new OcrToken("October", 0.94), new OcrToken("9", 0.42));
        var transcript = new TranscriptSession(await ocr.RecognizeAsync(capture.Envelope!, CancellationToken.None));
        transcript.Resolve(2, "9");
        transcript.Resolve(6, "9");
        Assert.True(transcript.IsComplete);
        var verifiedStep = Assert.Single(transcript.VerifiedLines());
        Assert.Equal("Finish chapter 9 by Friday, October 9", verifiedStep);

        var brief = BoardToBriefBuilder.Build(
            [
                new BriefLine("Chapter 9 homework", BriefRole.Title),
                new BriefLine(verifiedStep, BriefRole.Step),
                new BriefLine("Bring your annotations", BriefRole.Step),
            ],
            [new LockedField(LockedFieldKind.Date, "Friday, October 9")]);
        Assert.False(DocumentValidator.HasBlockingIssues(brief.Issues));

        // RC-18 in the flow: the draft's lane comes from the confirmed envelope.
        var draft = capture.CreateDraft(brief.Document);

        foreach (var state in new[]
        {
            JobState.DraftGenerated, JobState.SchemaValidated,
            JobState.InvariantsValidated, JobState.AwaitingTeacherReview,
        })
        {
            capture.Machine.Transition(state);
        }

        var review = new ReviewSession(draft, capture.Machine, new DefaultArtifactValidator());
        var approved = review.Approve("teacher@example.org", SomeInstant);

        capture.Machine.Transition(JobState.Rendered);
        capture.Machine.Transition(JobState.Completed);
        Assert.True(capture.PurgeTransientSources());
        return (approved, capture.Machine);
    }

    [Fact]
    public async Task A_staged_board_attested_green_travels_to_the_green_library()
    {
        var (approved, machine) = await RunFlowAsync(DataLane.Green);

        Assert.Equal(DataLane.Green, approved.Revision.Lane);
        Assert.Equal(JobState.TransientSourcesPurged, machine.State);

        var library = new RecordingProjectStore();
        await library.SaveGreenProjectAsync(approved, new ProjectSaveRequest("chapter-9-homework"), CancellationToken.None);
        Assert.Single(library.Saves);
    }

    [Fact]
    public async Task A_board_kept_amber_produces_an_amber_brief_the_green_library_refuses()
    {
        var (approved, _) = await RunFlowAsync(DataLane.Amber);

        // The teacher never typed a lane into the draft; the flow computed it.
        Assert.Equal(DataLane.Amber, approved.Revision.Lane);

        var library = new RecordingProjectStore();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => library.SaveGreenProjectAsync(approved, new ProjectSaveRequest("chapter-9-homework"), CancellationToken.None));
    }

    [Fact]
    public async Task The_safety_pause_purges_the_capture_flow_terminally()
    {
        var store = new InMemorySessionByteStore();
        var capture = new CaptureSession(
            new SimulatedCameraSource(store, BoardPhoto()),
            new ImageNormalizer(store),
            store);
        await capture.CaptureAsync(new CaptureRequest(SimulatedCameraSource.Kind), CancellationToken.None);

        var pause = capture.InvokeSafetyPause(DistrictPolicy.Offline);

        Assert.Equal(JobState.TransientSourcesPurged, capture.Machine.State);
        Assert.Contains("school's", pause.ProcedureText, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => capture.ConfirmLane(DataLane.Green));
        Assert.Null(capture.Envelope);
        Assert.Equal(0, store.Count);
        Assert.True(JobStateMachine.IsTerminal(capture.Machine.State));
    }

    [Fact]
    public async Task Repeated_normalization_zeroes_each_superseded_generation_and_keeps_one_live_reference()
    {
        var store = new InMemorySessionByteStore();
        var capture = new CaptureSession(
            new ByteImportCaptureSource(store),
            new ImageNormalizer(store),
            store);
        var current = await capture.CaptureAsync(
            new CaptureRequest(ByteImportCaptureSource.Kind, "image/png", BoardPhoto()),
            CancellationToken.None);
        Assert.True(store.TryGet(current.Bytes, out var previousBytes));

        for (var index = 0; index < 8; index++)
        {
            current = await capture.NormalizeAsync(
                new NormalizationRequest(Rotation: RotationDegrees.Rotate90),
                CancellationToken.None);

            Assert.Equal(1, store.Count);
            Assert.All(previousBytes.ToArray(), value => Assert.Equal(0, value));
            Assert.True(store.TryGet(current.Bytes, out previousBytes));
        }

        Assert.True(capture.Cancel());
        Assert.Equal(JobState.TransientSourcesPurged, capture.Machine.State);
        Assert.Equal(0, store.Count);
        Assert.All(previousBytes.ToArray(), value => Assert.Equal(0, value));
    }
}

public class SymbolPreflightTests
{
    [Fact]
    public async Task A_symbol_submission_walks_the_capture_preflight_and_arrives_metadata_free()
    {
        // A JPEG with a spliced EXIF segment, exactly like the normalizer suite's fixture.
        byte[] plain;
        using (var bitmap = new Bitmap(8, 8, PixelFormat.Format24bppRgb))
        {
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.White);
            }

            using var stream = new MemoryStream();
            GdiPlusImageEncoder.Save(bitmap, stream, ImageFormat.Jpeg);
            plain = stream.ToArray();
        }

        byte[] exif = [(byte)'E', (byte)'x', (byte)'i', (byte)'f', 0, 0, (byte)'I', (byte)'I', 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];
        var spliced = new byte[plain.Length + 4 + exif.Length];
        spliced[0] = plain[0];
        spliced[1] = plain[1];
        spliced[2] = 0xFF;
        spliced[3] = 0xE1;
        spliced[4] = (byte)((exif.Length + 2) >> 8);
        spliced[5] = (byte)((exif.Length + 2) & 0xFF);
        exif.CopyTo(spliced, 6);
        Array.Copy(plain, 2, spliced, 6 + exif.Length, plain.Length - 2);

        var store = new InMemorySessionByteStore();
        var submission = await SymbolPreflight.PrepareAsync(
            spliced, "image/jpeg", new AssetId("teacher.my-cup.v1"), "My cup", "A blue cup",
            "My own photograph", store, new ImageNormalizer(store), new NormalizationRequest(),
            DataLane.Green, CancellationToken.None);

        Assert.Equal("image/png", submission.MimeType);
        Assert.DoesNotContain("Exif", Encoding.ASCII.GetString(submission.CopyContent()), StringComparison.Ordinal);
        Assert.Equal(0, store.Count); // raw and intermediate bytes released on the way out

        // The preflighted bytes are what reach the shelf.
        var shelfDirectory = Path.Combine(Path.GetTempPath(), "ocf-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var shelf = new LocalSymbolStore(shelfDirectory);
            var provenance = shelf.Add(submission);
            Assert.True(shelf.TryGetContent(provenance.Id, out var content, out _));
            Assert.DoesNotContain("Exif", Encoding.ASCII.GetString(content.ToArray()), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(shelfDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task A_real_preflight_failure_purges_and_zeroes_every_transient_buffer()
    {
        var store = new ObservingSessionByteStore();

        await Assert.ThrowsAnyAsync<Exception>(() => SymbolPreflight.PrepareAsync(
            new byte[] { 1, 2, 3, 4 },
            "image/png",
            new AssetId("teacher.invalid.v1"),
            "Invalid fixture",
            "Invalid fixture",
            "Synthetic test fixture",
            store,
            new ImageNormalizer(store),
            new NormalizationRequest(),
            DataLane.Green,
            CancellationToken.None));

        Assert.Equal(0, store.Count);
        Assert.NotEmpty(store.HeldBuffers);
        Assert.All(store.HeldBuffers, AssertZeroed);
    }

    [Fact]
    public async Task Cancellation_after_preflight_normalization_allocates_output_purges_every_buffer()
    {
        var store = new ObservingSessionByteStore();
        var normalizer = new DeferredPreflightNormalizer(store);
        using var cancellation = new CancellationTokenSource();
        var pending = SymbolPreflight.PrepareAsync(
            new byte[] { 1, 2, 3, 4 },
            "image/png",
            new AssetId("teacher.cancelled.v1"),
            "Cancelled fixture",
            "Cancelled fixture",
            "Synthetic test fixture",
            store,
            normalizer,
            new NormalizationRequest(),
            DataLane.Green,
            cancellation.Token);

        await normalizer.OutputStored;
        cancellation.Cancel();
        normalizer.AllowReturn();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(0, store.Count);
        Assert.Equal(2, store.HeldBuffers.Count);
        Assert.All(store.HeldBuffers, AssertZeroed);
    }

    [Fact]
    public async Task Preflight_cancellation_already_requested_still_purges_the_owned_session_store()
    {
        var store = new ObservingSessionByteStore();
        store.Put(new byte[] { 9, 8, 7, 6 });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SymbolPreflight.PrepareAsync(
            new byte[] { 1, 2, 3, 4 },
            "image/png",
            new AssetId("teacher.precancelled.v1"),
            "Pre-cancelled fixture",
            "Pre-cancelled fixture",
            "Synthetic test fixture",
            store,
            new ImageNormalizer(store),
            new NormalizationRequest(),
            DataLane.Green,
            cancellation.Token));

        Assert.Equal(0, store.Count);
        Assert.Single(store.HeldBuffers);
        Assert.All(store.HeldBuffers, AssertZeroed);
    }

    [Theory]
    [InlineData(UntrustedNormalizerMode.ReusesRawReference)]
    [InlineData(UntrustedNormalizerMode.ClaimsUnstrippedOutput)]
    [InlineData(UntrustedNormalizerMode.ReturnsNonPngOutput)]
    [InlineData(UntrustedNormalizerMode.ChangesSourceKind)]
    [InlineData(UntrustedNormalizerMode.ChangesPageCount)]
    [InlineData(UntrustedNormalizerMode.ChangesLane)]
    [InlineData(UntrustedNormalizerMode.ChangesLaneBasis)]
    [InlineData(UntrustedNormalizerMode.ChangesRights)]
    public async Task An_untrusted_normalizer_cannot_mint_a_symbol_shelf_capability(
        UntrustedNormalizerMode mode)
    {
        var store = new ObservingSessionByteStore();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => SymbolPreflight.PrepareAsync(
            new byte[] { 1, 2, 3, 4 },
            "image/png",
            new AssetId("teacher.untrusted.v1"),
            "Untrusted fixture",
            "Untrusted fixture",
            "Synthetic test fixture",
            store,
            new UntrustedPreflightNormalizer(store, mode),
            new NormalizationRequest(),
            DataLane.Green,
            CancellationToken.None));

        Assert.Contains("fresh metadata-stripped PNG", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, store.Count);
        Assert.NotEmpty(store.HeldBuffers);
        Assert.All(store.HeldBuffers, AssertZeroed);
    }

    [Theory]
    [InlineData(DataLane.Amber)]
    [InlineData(DataLane.Restricted)]
    public async Task A_symbol_without_teacher_confirmed_green_cannot_mint_a_shelf_capability(
        DataLane lane)
    {
        var store = new ObservingSessionByteStore();
        store.Put(new byte[] { 9, 8, 7, 6 });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => SymbolPreflight.PrepareAsync(
            new byte[] { 1, 2, 3, 4 },
            "image/png",
            new AssetId("teacher.unconfirmed.v1"),
            "Unconfirmed fixture",
            "Unconfirmed fixture",
            "Synthetic test fixture",
            store,
            new ImageNormalizer(store),
            new NormalizationRequest(),
            lane,
            CancellationToken.None));

        Assert.Contains("confirm that a symbol is Green", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, store.Count);
        Assert.Single(store.HeldBuffers);
        Assert.All(store.HeldBuffers, AssertZeroed);
    }

    private static void AssertZeroed(ReadOnlyMemory<byte> content)
        => Assert.All(content.ToArray(), value => Assert.Equal(0, value));

    private sealed class ObservingSessionByteStore : ISessionByteStore
    {
        private readonly InMemorySessionByteStore _inner = new();

        public List<ReadOnlyMemory<byte>> HeldBuffers { get; } = [];

        public int Count => _inner.Count;

        public SessionByteReference Put(ReadOnlyMemory<byte> content)
        {
            var reference = _inner.Put(content);
            _inner.TryGet(reference, out var held);
            HeldBuffers.Add(held);
            return reference;
        }

        public bool TryGet(SessionByteReference reference, out ReadOnlyMemory<byte> content)
            => _inner.TryGet(reference, out content);

        public void Release(SessionByteReference reference) => _inner.Release(reference);

        public void PurgeAll() => _inner.PurgeAll();
    }

    private sealed class DeferredPreflightNormalizer(ObservingSessionByteStore store) : IDocumentNormalizer
    {
        private readonly TaskCompletionSource _outputStored = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _allowReturn = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task OutputStored => _outputStored.Task;

        public async Task<SourceEnvelope> NormalizeAsync(
            SourceEnvelope source,
            NormalizationRequest request,
            CancellationToken cancellationToken)
        {
            var reference = store.Put(new byte[] { 5, 6, 7, 8 });
            _outputStored.SetResult();
            await _allowReturn.Task;
            return source with { MetadataStripped = true, Bytes = reference };
        }

        public void AllowReturn() => _allowReturn.SetResult();
    }

    public enum UntrustedNormalizerMode
    {
        ReusesRawReference,
        ClaimsUnstrippedOutput,
        ReturnsNonPngOutput,
        ChangesSourceKind,
        ChangesPageCount,
        ChangesLane,
        ChangesLaneBasis,
        ChangesRights,
    }

    private sealed class UntrustedPreflightNormalizer(
        ObservingSessionByteStore store,
        UntrustedNormalizerMode mode) : IDocumentNormalizer
    {
        public Task<SourceEnvelope> NormalizeAsync(
            SourceEnvelope source,
            NormalizationRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var output = mode == UntrustedNormalizerMode.ReusesRawReference
                ? source.Bytes
                : store.Put(new byte[] { 5, 6, 7, 8 });

            return Task.FromResult(source with
            {
                Bytes = output,
                MetadataStripped = mode != UntrustedNormalizerMode.ClaimsUnstrippedOutput,
                MimeType = mode == UntrustedNormalizerMode.ReturnsNonPngOutput ? "image/jpeg" : "image/png",
                SourceKind = mode == UntrustedNormalizerMode.ChangesSourceKind ? "other-source" : source.SourceKind,
                PageCount = mode == UntrustedNormalizerMode.ChangesPageCount ? 2 : source.PageCount,
                Lane = mode == UntrustedNormalizerMode.ChangesLane ? DataLane.Green : source.Lane,
                LaneBasis = mode == UntrustedNormalizerMode.ChangesLaneBasis
                    ? DataLaneBasis.Established
                    : source.LaneBasis,
                TeacherStatedRights = mode == UntrustedNormalizerMode.ChangesRights
                    ? "Rewritten rights"
                    : source.TeacherStatedRights,
            });
        }
    }
}
