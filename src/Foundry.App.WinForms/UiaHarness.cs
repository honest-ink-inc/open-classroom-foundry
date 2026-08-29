// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Infrastructure.Windows;

namespace Foundry.App.WinForms;

/// <summary>
/// Deterministic fixture surfaces for the UI Automation harness (ADR-002: the
/// harness that must exist before any custom control ever ships). Launched via
/// <c>--uia-harness review|capture</c> so the headed tests drive the real forms
/// over real UIA — the same tree NVDA and Narrator read. Fixture content only;
/// nothing here touches network or a camera, and storage only where
/// <see cref="LibraryRootSwitch"/> explicitly points the library at a
/// disposable directory.
/// </summary>
public static class UiaHarness
{
    public const string Switch = "--uia-harness";

    /// <summary>
    /// Optional companion to <see cref="Switch"/>: redirects the project
    /// library to a disposable directory so the headed dress rehearsal can
    /// save and reopen real projects without ever touching the teacher's
    /// Documents. Honored only in harness mode.
    /// </summary>
    public const string LibraryRootSwitch = "--library-root";

    /// <summary>
    /// Optional companion to <see cref="Switch"/>: injects the Press Room's
    /// export seam with a fixed booklet-PDF destination. The shell Save As
    /// dialog's name field cannot be committed by cross-process automation
    /// (async pre-fill, programmatic text never reaching the dialog's model,
    /// and the foreground lock blocking synthetic keyboard input — all found
    /// 29 Aug 2026 building the pilot dress rehearsal), so the headed test
    /// exercises everything OURS — the gate, the render switch, the
    /// imposition, the bytes, the speaking status — through this seam.
    /// </summary>
    public const string ExportToSwitch = "--export-to";

    public static ReviewForm CreateReviewForm()
    {
        var machine = new JobStateMachine();
        foreach (var state in new[]
        {
            JobState.Imported, JobState.Normalized, JobState.DataLaneConfirmed,
            JobState.DraftGenerated, JobState.SchemaValidated, JobState.InvariantsValidated,
            JobState.AwaitingTeacherReview,
        })
        {
            machine.Transition(state);
        }

        var draft = DraftArtifact.New(new ArtifactDocument(
        [
            new Heading(1, "Watering the class plants"),
            new Paragraph("Pick up the watering can."),
            new Paragraph("Fill it to the line."),
            new Paragraph("Water each plant once."),
            new Paragraph("Put the can back."),
        ]), DataLane.Green);

        return new ReviewForm(new ReviewSession(draft, machine, new DefaultArtifactValidator(), new DomainApprovalGate()));
    }

    public static CaptureForm CreateCaptureForm()
    {
        var store = new InMemorySessionByteStore();
        return new CaptureForm(
            new CaptureSession(new ByteImportCaptureSource(store), new ImageNormalizer(store)),
            DistrictPolicy.Offline);
    }

    public static Form? FromArgs(string[] args)
    {
        var index = Array.IndexOf(args, Switch);
        if (index < 0 || index + 1 >= args.Length)
        {
            return null;
        }

        var libraryIndex = Array.IndexOf(args, LibraryRootSwitch);
        if (libraryIndex >= 0 && libraryIndex + 1 < args.Length)
        {
            AppServices.LibraryRoot = args[libraryIndex + 1];
        }

        // In harness mode a swallowed exception is invisible evidence; write
        // it where the test can read it instead.
        System.Windows.Forms.Application.ThreadException += (_, e) =>
            File.WriteAllText(HarnessErrorPath, e.Exception.ToString());
        AppDomain.CurrentDomain.FirstChanceException += (_, e) =>
            File.AppendAllText(HarnessFirstChancePath, e.Exception.GetType().Name + ": " + e.Exception.Message + Environment.NewLine);

        var exportIndex = Array.IndexOf(args, ExportToSwitch);
        var exportTo = exportIndex >= 0 && exportIndex + 1 < args.Length ? args[exportIndex + 1] : null;

        return args[index + 1] switch
        {
            "review" => CreateReviewForm(),
            "capture" => CreateCaptureForm(),
            // Filter index 2 is the booklet PDF: the imposition leg is the
            // seam-richest export, so it is the one the rehearsal exercises.
            "pressroom" => exportTo is null
                ? new PressRoomForm()
                : new PressRoomForm(exportPicker: () => new PressRoomForm.ExportChoice(exportTo, 2)),
            "allaboard" => new AllAboardForm(AppServices.SymbolCatalog()),
            _ => null,
        };
    }

    public static string HarnessErrorPath
        => Path.Combine(Path.GetTempPath(), "ocf-harness-error.txt");

    public static string HarnessFirstChancePath
        => Path.Combine(Path.GetTempPath(), "ocf-harness-firstchance.txt");
}
