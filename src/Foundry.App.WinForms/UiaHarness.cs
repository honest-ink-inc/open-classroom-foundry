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
/// nothing here touches network or a camera, and storage only where the
/// production <see cref="ProjectLibraryRootConfiguration.Switch"/> has already
/// admitted an exact, version-addressed disposable directory.
/// </summary>
public static class UiaHarness
{
    public const string Switch = "--uia-harness";

    /// <summary>
    /// Retained only so old automation receives an explicit refusal. This
    /// shipped command-line switch formerly overwrote the validated production
    /// root and therefore cannot be honored in any build.
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

        return new ReviewForm(new ReviewSession(draft, machine, new DefaultArtifactValidator()));
    }

    public static CaptureForm CreateCaptureForm()
    {
        var store = new InMemorySessionByteStore();
        return new CaptureForm(
            new CaptureSession(new ByteImportCaptureSource(store), new ImageNormalizer(store)),
            DistrictPolicy.Offline);
    }

    internal static Form? FromArgs(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Any(value => value.Equals(LibraryRootSwitch, StringComparison.Ordinal)
            || value.StartsWith(LibraryRootSwitch + "=", StringComparison.Ordinal)))
        {
            throw new ProjectLibraryRootException(
                ProjectLibraryRootFailureCodes.SwitchInvalid,
                UiStrings.ProjectLibraryRootSwitchInvalid);
        }

        var harnessSwitches = args
            .Select((value, position) => (value, position))
            .Where(item => string.Equals(item.value, Switch, StringComparison.Ordinal))
            .Select(item => item.position)
            .ToArray();
        if (harnessSwitches.Length == 0)
        {
            return null;
        }

        if (harnessSwitches.Length != 1
            || harnessSwitches[0] + 1 >= args.Length
            || args[harnessSwitches[0] + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new InvalidDataException(UiStrings.UiaHarnessSwitchInvalid);
        }

        var index = harnessSwitches[0];
        var harnessMode = args[index + 1];
        if (harnessMode is not ("review" or "capture" or "pressroom" or "allaboard" or "modules"))
        {
            throw new InvalidDataException(UiStrings.UiaHarnessSwitchInvalid);
        }

        // Program has already validated and applied this exact switch. Harness
        // storage is narrower still: it must be the empty, disposable rehearsal
        // shape under the OS temp root, never a real teacher library.
        var libraryRoot = args.Contains(ProjectLibraryRootConfiguration.Switch, StringComparer.Ordinal)
            ? ValidateDisposableHarnessRoot(AppServices.LibraryRoot)
            : null;
        if (libraryRoot is null
            && harnessMode is "pressroom" or "allaboard" or "modules")
        {
            throw new ProjectLibraryRootException(
                ProjectLibraryRootFailureCodes.RootInvalid,
                UiStrings.ProjectLibraryRootInvalid);
        }

        // In harness mode a swallowed exception is invisible evidence. Keep the
        // diagnostic content-free: exception messages and stacks may contain a
        // path or authored text from before a public-boundary sanitizer runs.
        System.Windows.Forms.Application.ThreadException += (_, e) =>
            File.WriteAllText(HarnessErrorPath, ContentFreeExceptionDiagnostic(e.Exception));

        var exportTo = ValidateHarnessExport(args, harnessMode, libraryRoot);

        return harnessMode switch
        {
            "review" => CreateReviewForm(),
            "capture" => CreateCaptureForm(),
            // Filter index 2 is the booklet PDF: the imposition leg is the
            // seam-richest export, so it is the one the rehearsal exercises.
            // The library picker resolves at CLICK time to the newest project
            // in the fixture library — the shell Open dialog's UIA exposure
            // proved flaky under load (traceability finding 9), and
            // Microsoft's chrome is not what the rehearsal guards.
            "pressroom" => new PressRoomForm(
                libraryPicker: libraryRoot is null
                    ? null
                    : () => Directory.EnumerateFiles(libraryRoot, "*" + Storage.OcfprojProjectStore.Extension)
                        .OrderByDescending(File.GetLastWriteTimeUtc)
                        .FirstOrDefault(),
                exportPicker: exportTo is null
                    ? null
                    : () => new PressRoomForm.ExportChoice(exportTo, 2)),
            "allaboard" => new AllAboardForm(AppServices.SymbolCatalog()),
            "modules" => new ModuleStudioForm(),
            _ => throw new InvalidOperationException(),
        };
    }

    public static string HarnessErrorPath
        => Path.Combine(Path.GetTempPath(), "ocf-harness-error.txt");

    internal static string ContentFreeExceptionDiagnostic(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return $"Unhandled UI thread exception ({exception.GetType().Name}).";
    }

    private static string ValidateDisposableHarnessRoot(string root)
    {
        var admitted = ProjectLibraryRootConfiguration.ValidateProductionRoot(root);
        var tempRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        var relative = Path.GetRelativePath(tempRoot, admitted);
        var segments = relative.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        const string rehearsalPrefix = "ocf-rehearsal-";
        var rehearsalName = segments.Length == 3 ? segments[0] : string.Empty;
        var suffix = rehearsalName.StartsWith(rehearsalPrefix, StringComparison.Ordinal)
            ? rehearsalName[rehearsalPrefix.Length..]
            : string.Empty;
        bool isEmpty;
        try
        {
            isEmpty = !Directory.EnumerateFileSystemEntries(admitted).Any();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ProjectLibraryRootException(
                ProjectLibraryRootFailureCodes.RootInvalid,
                UiStrings.ProjectLibraryRootInvalid);
        }

        if (segments.Length != 3
            || segments.Any(segment => segment is "." or "..")
            || !Guid.TryParseExact(suffix, "N", out _)
            || !string.Equals(segments[1], EngineIdentity.EngineVersion, StringComparison.Ordinal)
            || !string.Equals(segments[2], "prepared-library", StringComparison.Ordinal)
            || !isEmpty)
        {
            throw new ProjectLibraryRootException(
                ProjectLibraryRootFailureCodes.RootInvalid,
                UiStrings.ProjectLibraryRootInvalid);
        }

        return admitted;
    }

    private static string? ValidateHarnessExport(
        string[] args,
        string harnessMode,
        string? libraryRoot)
    {
        if (args.Any(value => value.StartsWith(ExportToSwitch + "=", StringComparison.Ordinal)))
        {
            throw new InvalidDataException(UiStrings.UiaHarnessExportInvalid);
        }

        var matches = args
            .Select((value, position) => (value, position))
            .Where(item => string.Equals(item.value, ExportToSwitch, StringComparison.Ordinal))
            .Select(item => item.position)
            .ToArray();
        if (matches.Length == 0)
        {
            return null;
        }

        if (matches.Length != 1
            || harnessMode != "pressroom"
            || libraryRoot is null
            || matches[0] + 1 >= args.Length
            || args[matches[0] + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new InvalidDataException(UiStrings.UiaHarnessExportInvalid);
        }

        try
        {
            var requested = args[matches[0] + 1];
            if (string.IsNullOrWhiteSpace(requested) || !Path.IsPathFullyQualified(requested))
            {
                throw new InvalidDataException(UiStrings.UiaHarnessExportInvalid);
            }

            var fullPath = Path.GetFullPath(requested);
            var parent = Path.GetDirectoryName(fullPath);
            var fileName = Path.GetFileName(fullPath);
            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (!string.Equals(parent, libraryRoot, comparison)
                || !Path.GetExtension(fullPath).Equals(".pdf", comparison)
                || string.IsNullOrWhiteSpace(fileName)
                || fileName.Any(Path.GetInvalidFileNameChars().Contains)
                || File.Exists(fullPath)
                || Directory.Exists(fullPath))
            {
                throw new InvalidDataException(UiStrings.UiaHarnessExportInvalid);
            }

            return fullPath;
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception failure) when (failure is IOException
                                             or UnauthorizedAccessException
                                             or ArgumentException
                                             or NotSupportedException)
        {
            throw new InvalidDataException(UiStrings.UiaHarnessExportInvalid);
        }
    }
}
