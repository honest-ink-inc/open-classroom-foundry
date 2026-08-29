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
/// nothing here touches storage, network, or a camera.
/// </summary>
public static class UiaHarness
{
    public const string Switch = "--uia-harness";

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

        // In harness mode a swallowed exception is invisible evidence; write
        // it where the test can read it instead.
        System.Windows.Forms.Application.ThreadException += (_, e) =>
            File.WriteAllText(HarnessErrorPath, e.Exception.ToString());
        AppDomain.CurrentDomain.FirstChanceException += (_, e) =>
            File.AppendAllText(HarnessFirstChancePath, e.Exception.GetType().Name + ": " + e.Exception.Message + Environment.NewLine);

        return args[index + 1] switch
        {
            "review" => CreateReviewForm(),
            "capture" => CreateCaptureForm(),
            "pressroom" => new PressRoomForm(),
            "allaboard" => new AllAboardForm(AppServices.SymbolCatalog()),
            _ => null,
        };
    }

    public static string HarnessErrorPath
        => Path.Combine(Path.GetTempPath(), "ocf-harness-error.txt");

    public static string HarnessFirstChancePath
        => Path.Combine(Path.GetTempPath(), "ocf-harness-firstchance.txt");
}
