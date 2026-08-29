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
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    private static async Task<(ApprovedArtifact Artifact, JobStateMachine Machine)> RunFlowAsync(DataLane teacherAttestedLane)
    {
        var store = new InMemorySessionByteStore();
        var capture = new CaptureSession(
            new SimulatedCameraSource(store, BoardPhoto()),
            new ImageNormalizer(store));

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

        var brief = BoardToBriefBuilder.Build(
            [
                new BriefLine("Chapter 9 homework", BriefRole.Title),
                new BriefLine("Finish chapter 9 by Friday, October 9", BriefRole.Step),
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

        var review = new ReviewSession(draft, capture.Machine, new DefaultArtifactValidator(), new DomainApprovalGate());
        var approved = review.Approve("teacher@example.org", SomeInstant);

        store.PurgeAll();
        return (approved, capture.Machine);
    }

    [Fact]
    public async Task A_staged_board_attested_green_travels_to_the_green_library()
    {
        var (approved, machine) = await RunFlowAsync(DataLane.Green);

        Assert.Equal(DataLane.Green, approved.Revision.Lane);
        Assert.Equal(JobState.Approved, machine.State);

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
    public async Task The_safety_pause_ends_a_capture_flow_with_only_purge_ahead()
    {
        var store = new InMemorySessionByteStore();
        var capture = new CaptureSession(new SimulatedCameraSource(store, BoardPhoto()), new ImageNormalizer(store));
        await capture.CaptureAsync(new CaptureRequest(SimulatedCameraSource.Kind), CancellationToken.None);

        var pause = capture.InvokeSafetyPause(DistrictPolicy.Offline);

        Assert.Equal(JobState.Blocked, capture.Machine.State);
        Assert.Contains("school's", pause.ProcedureText, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => capture.ConfirmLane(DataLane.Green));

        store.PurgeAll();
        capture.Machine.Transition(JobState.TransientSourcesPurged);
        Assert.True(JobStateMachine.IsTerminal(capture.Machine.State));
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
            bitmap.Save(stream, ImageFormat.Jpeg);
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
            CancellationToken.None);

        Assert.Equal("image/png", submission.MimeType);
        Assert.DoesNotContain("Exif", Encoding.ASCII.GetString(submission.Content.ToArray()), StringComparison.Ordinal);
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
}
