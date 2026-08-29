using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Infrastructure.Simulated;

namespace Foundry.Tests.Unit;

public class SessionByteStoreTests
{
    [Fact]
    public void Bytes_round_trip_through_an_opaque_reference()
    {
        var store = new InMemorySessionByteStore();
        var reference = store.Put(new byte[] { 1, 2, 3 });

        Assert.True(store.TryGet(reference, out var content));
        Assert.Equal(new byte[] { 1, 2, 3 }, content.ToArray());
        Assert.Equal(1, store.Count);
    }

    [Fact]
    public void Empty_content_is_refused()
    {
        Assert.Throws<ArgumentException>(() => new InMemorySessionByteStore().Put(ReadOnlyMemory<byte>.Empty));
    }

    [Fact]
    public void Release_removes_a_single_reference()
    {
        var store = new InMemorySessionByteStore();
        var kept = store.Put(new byte[] { 1 });
        var released = store.Put(new byte[] { 2 });

        store.Release(released);

        Assert.False(store.TryGet(released, out _));
        Assert.True(store.TryGet(kept, out _));
    }

    [Fact]
    public void Purge_empties_the_session()
    {
        var store = new InMemorySessionByteStore();
        store.Put(new byte[] { 1 });
        store.Put(new byte[] { 2 });

        store.PurgeAll();

        Assert.Equal(0, store.Count);
    }
}

public class CaptureSourceTests
{
    [Fact]
    public async Task An_import_lands_in_the_amber_lane_with_no_path_anywhere()
    {
        var store = new InMemorySessionByteStore();
        var source = new ByteImportCaptureSource(store);

        var envelope = await source.CaptureAsync(
            new CaptureRequest(ByteImportCaptureSource.Kind, "image/png", "\t\t\t"u8.ToArray()),
            CancellationToken.None);

        Assert.Equal(DataLane.Amber, envelope.Lane);
        Assert.Equal("file-import", envelope.SourceKind);
        Assert.False(envelope.MetadataStripped);
        Assert.True(store.TryGet(envelope.Bytes, out _));
    }

    [Fact]
    public async Task Unsupported_types_and_empty_imports_are_refused()
    {
        var source = new ByteImportCaptureSource(new InMemorySessionByteStore());

        await Assert.ThrowsAsync<ArgumentException>(
            () => source.CaptureAsync(new CaptureRequest("file-import", "application/pdf", new byte[] { 1 }), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(
            () => source.CaptureAsync(new CaptureRequest("file-import", "image/png"), CancellationToken.None));
    }

    [Fact]
    public async Task The_camera_simulator_serves_its_configured_frame()
    {
        var store = new InMemorySessionByteStore();
        var camera = new SimulatedCameraSource(store, new byte[] { 7, 7 });

        var envelope = await camera.CaptureAsync(new CaptureRequest(SimulatedCameraSource.Kind), CancellationToken.None);

        Assert.Equal("camera-simulator", envelope.SourceKind);
        Assert.Equal(DataLane.Amber, envelope.Lane);
        Assert.True(store.TryGet(envelope.Bytes, out var frame));
        Assert.Equal(new byte[] { 7, 7 }, frame.ToArray());
    }
}

public class SimulatedSinkTests
{
    private static readonly DateTimeOffset SomeInstant = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    private static ApprovedArtifact Approved(DataLane lane)
        => ApprovalGate.Approve(
            DraftArtifact.New(new ArtifactDocument([new Paragraph("Ten-frame practice")]), lane),
            "teacher@example.org",
            [],
            SomeInstant);

    [Fact]
    public async Task The_virtual_printer_records_the_receipt_of_what_it_printed()
    {
        var printer = new VirtualPrintSink();
        var artifact = Approved(DataLane.Green);

        await printer.PrintAsync(artifact, new PrintRequest("virtual", Duplex: false, Copies: 2), CancellationToken.None);

        var job = Assert.Single(printer.Jobs);
        Assert.Equal(artifact.Receipt, job.Receipt);
        Assert.Equal(2, job.Request.Copies);
    }

    [Fact]
    public async Task The_recording_exporter_records_the_receipt()
    {
        var exporter = new RecordingExporter();

        await exporter.ExportAsync(Approved(DataLane.Green), new ExportRequest(RenderTarget.PrintPdf, "exports"), CancellationToken.None);

        Assert.Single(exporter.Exports);
    }

    [Fact]
    public async Task The_project_store_refuses_anything_above_the_green_lane()
    {
        var store = new RecordingProjectStore();

        await store.SaveGreenProjectAsync(Approved(DataLane.Green), new ProjectSaveRequest("library"), CancellationToken.None);
        Assert.Single(store.Saves);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.SaveGreenProjectAsync(Approved(DataLane.Amber), new ProjectSaveRequest("library"), CancellationToken.None));
        Assert.Single(store.Saves);
    }
}
