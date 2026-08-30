// SPDX-License-Identifier: GPL-3.0-or-later
using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Rendering;
using Foundry.Storage;
using Microsoft.Win32.SafeHandles;

namespace Foundry.App.WinForms;

/// <summary>
/// The teacher's three-part Green classification for an otherwise unknown
/// reopened package. Every statement is about content; none is a purpose,
/// recipe, governance, or protected-seat assertion.
/// </summary>
public sealed record LoadedProjectGreenChecklist(
    bool IsGreenQualifyingContent,
    bool HasNoLearnerLinkedContent,
    bool HasNoRestrictedContent);

/// <summary>
/// Ephemeral proof that the teacher classified one exact loaded document as
/// Green. Its constructor is private, and both object identity and semantic
/// digest are rechecked at the Gate B boundary so reuse or substitution fails.
/// </summary>
public sealed class LoadedProjectGreenConfirmation
{
    private readonly LoadedProject _loaded;
    private readonly string _artifactSha256;

    private LoadedProjectGreenConfirmation(LoadedProject loaded)
    {
        _loaded = loaded;
        _artifactSha256 = ArtifactDocumentFingerprint.Compute(loaded.Document);
    }

    internal static LoadedProjectGreenConfirmation Create(LoadedProject loaded) => new(loaded);

    internal bool AppliesTo(LoadedProject loaded)
        => ReferenceEquals(_loaded, loaded)
            && string.Equals(
                _artifactSha256,
                ArtifactDocumentFingerprint.Compute(loaded.Document),
                StringComparison.Ordinal);
}

/// <summary>
/// The small shared machinery the authoring surfaces stand on: the shipped
/// symbol catalog, the post-approval outputs, and the review-session plumbing.
/// Everything here accepts only an ApprovedArtifact where output is concerned —
/// the structural gate is not re-negotiated per surface (ADR-004).
/// </summary>
public static partial class AppServices
{
    private const string PrintViewJobMarker = "-print-view-";
    private const string PrintViewCleanupMarker = "cleanup-";
    private const string PrintViewLeaseFileName = ".active";
    internal const int MaxActivePrintViewLeases = 8;
    private static readonly TimeSpan PrintViewStaleAge = TimeSpan.FromDays(1);
    private static readonly Lock PrintViewCreationLock = new();
    private static readonly Lock PrintViewLeaseLock = new();
    private static readonly Dictionary<string, PrintViewLease> ActivePrintViewLeases =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Engine-owned identity used when a mutable package is re-reviewed or
    /// edited. Package module/recipe selectors are never copied into a new
    /// manifest as though they were authenticated provenance.
    /// </summary>
    public const string PortableProjectModuleId = "portable-semantic-document";

    public const string PortableProjectRecipeId = "portable-semantic-editor";

    public const string PortableProjectRecipeVersion = "1.0.0";

    /// <summary>The shipped CC0 pack beside the executable; an empty catalog when absent — the app still runs, symbol-less.</summary>
    public static IAssetCatalog SymbolCatalog()
    {
        var packaged = Path.Combine(AppContext.BaseDirectory, "assets", "symbols");
        return OpenSymbolCatalog(packaged);
    }

    internal static IAssetCatalog OpenSymbolCatalog(string packaged)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packaged);
        if (!Directory.Exists(packaged))
        {
            return new NoAssetsCatalog();
        }

        var catalog = new JsonAssetCatalog(packaged);
        var blocking = catalog.VerifyIntegrity()
            .Where(issue => issue.Severity == ValidationSeverity.Blocking)
            .ToArray();
        if (blocking.Length > 0)
        {
            throw new InvalidDataException(
                UiStrings.FormatWithoutMnemonic(
                    UiStrings.SymbolCatalogIntegrityFailed,
                    string.Join(Environment.NewLine, blocking.Select(issue => issue.Code))));
        }

        return catalog;
    }

    public static JobStateMachine MachineAtReview()
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

        return machine;
    }

    /// <summary>
    /// Starts Gate B over an explicitly-laned draft with the validator that
    /// owns that recipe's invariants. Callers cannot accidentally turn Amber
    /// into Green or discard a module validator at the UI boundary.
    /// </summary>
    public static ReviewSession SessionOver(
        DraftArtifact draft,
        IArtifactValidator validator,
        ReviewViewContext? viewContext = null)
        => new(
            draft,
            MachineAtReview(),
            validator,
            viewContext ?? ReviewViewContext.ManualDefault);

    /// <summary>The established Green/manual path used by Module Zero and All Aboard.</summary>
    public static ReviewSession SessionOverGreen(
        ArtifactDocument document,
        ReviewViewContext? viewContext = null)
        => SessionOver(
            DraftArtifact.New(document, DataLane.Green),
            new DefaultArtifactValidator(),
            viewContext);

    /// <summary>
    /// Trusted-test compatibility for exercising legacy persisted purpose
    /// declarations. The shipped public host has no raw-purpose entry point.
    /// </summary>
    internal static ReviewSession SessionOverGreen(
        ArtifactDocument document,
        ArtifactPurpose purpose,
        ReviewViewContext? viewContext = null)
        => SessionOver(
            DraftArtifact.New(document, DataLane.Green, purpose),
            new DefaultArtifactValidator(),
            viewContext);

    public static ReviewSession SessionOverRecipe(
        DraftArtifact draft,
        IArtifactValidator validator,
        RecipeManifest recipe,
        IReadOnlyList<string>? transformationReport = null,
        ReviewViewContext? viewContext = null)
        => SessionOver(
            draft,
            new ReviewNoticeValidator(
                validator,
                ReviewNoticeValidator.RequiredRecipeWarnings(recipe, transformationReport)),
            viewContext);

    /// <summary>A review runner may return approval only for the session's exact current revision.</summary>
    public static bool IsExactApproval(ReviewSession session, ApprovedArtifact approved)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(approved);
        var revision = session.Draft.Revision;
        return session.Machine.State == JobState.Approved
            && ReferenceEquals(session.ApprovedResult, approved)
            && ReferenceEquals(approved.Revision, revision)
            && approved.Receipt.ArtifactId == revision.Id
            && approved.Receipt.RevisionNumber == revision.Number;
    }

    public static byte[] Render(
        ApprovedArtifact artifact,
        RenderTarget target,
        IAssetCatalog? assetCatalog = null,
        CancellationToken cancellationToken = default)
        => Render(artifact, new RenderRequest(target), assetCatalog, cancellationToken);

    public static byte[] Render(
        ApprovedArtifact artifact,
        RenderRequest request,
        IAssetCatalog? assetCatalog = null,
        CancellationToken cancellationToken = default)
        => new AccessibleHtmlRenderer(assetCatalog).RenderAsync(
                artifact, request, cancellationToken)
            .GetAwaiter().GetResult().Content.ToArray();

    /// <summary>
    /// Exports an HTML-shaped approved artifact through the bounded local Edge
    /// PDF pipeline. The caller supplies the destination deliberately; this
    /// method neither installs, distributes, nor publishes anything.
    /// </summary>
    public static void ExportPdf(
        ApprovedArtifact artifact,
        string destination,
        IAssetCatalog? assetCatalog = null,
        RenderAudience audience = RenderAudience.Learner,
        double textScalePercent = 100,
        bool targetLanguageFirst = false)
        => ExportPdfAsync(
                artifact,
                destination,
                assetCatalog,
                audience,
                textScalePercent,
                targetLanguageFirst,
                CancellationToken.None)
            .GetAwaiter().GetResult();

    /// <summary>
    /// Responsive PDF route: vector-only presses retain the deterministic native
    /// writer; semantic/image documents take the bounded Edge HTML path.
    /// Both routes commit only a completed file at the teacher's destination.
    /// </summary>
    public static async Task ExportPdfAsync(
        ApprovedArtifact artifact,
        string destination,
        IAssetCatalog? assetCatalog = null,
        RenderAudience audience = RenderAudience.Learner,
        double textScalePercent = 100,
        bool targetLanguageFirst = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);

        cancellationToken.ThrowIfCancellationRequested();
        AccessibleHtmlRenderer.ValidateTextScaleContract(new RenderRequest(
            RenderTarget.PrintPdf,
            audience,
            textScalePercent,
            targetLanguageFirst));
        var nativePdf = await Task.Run(
            () => TryRenderNativePdf(artifact, audience, cancellationToken),
            cancellationToken).ConfigureAwait(false);
        if (nativePdf is not null)
        {
            await WriteExportBytesAsync(destination, nativePdf, cancellationToken).ConfigureAwait(false);
            return;
        }

        await new Infrastructure.Windows.EdgePdfExporter(new AccessibleHtmlRenderer(assetCatalog))
            .ExportAsync(
                artifact,
                new ExportRequest(
                    RenderTarget.PrintPdf,
                    destination,
                    audience,
                    textScalePercent,
                    targetLanguageFirst),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Attempts the deterministic native route. Shape support alone is not
    /// enough: the writer can still refuse a glyph it cannot encode. That
    /// refusal selects the richer local HTML/Edge route; it is not an export
    /// failure in its own right.
    /// </summary>
    internal static byte[]? TryRenderNativePdf(
        ApprovedArtifact artifact,
        RenderAudience audience,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        cancellationToken.ThrowIfCancellationRequested();
        if (!VectorPdfWriter.CanWrite(artifact.Revision.Document))
        {
            return null;
        }

        try
        {
            return VectorPdfWriter.Write(artifact, audience, cancellationToken);
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    internal static async Task WriteExportBytesAsync(
        string destination,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken,
        Func<string, Task>? stageReady = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        cancellationToken.ThrowIfCancellationRequested();
        var resolved = Path.GetFullPath(destination);
        var directory = Path.GetDirectoryName(resolved)
            ?? throw new IOException(UiStrings.WithoutMnemonic(UiStrings.ExportDestinationNoParent));
        var destinationLeaf = Path.GetFileName(resolved);
        var renameInformation = BuildExportRenameInformation(destinationLeaf);

        Directory.CreateDirectory(directory);
        var stage = Path.Combine(directory, $".honest-ink-{Guid.NewGuid():N}.stage");
        // 0 = pending, 1 = cancellation won, 2 = promotion won. The CAS is the
        // linearization point; the cancellation callback never blocks a UI
        // thread behind filesystem latency once promotion has begun.
        var promotionState = 0;
        using var cancellationRegistration = cancellationToken.Register(
            () => Interlocked.CompareExchange(ref promotionState, 1, 0));
        using var destinationDirectoryLease = OpenExportDirectoryLease(directory);
        var physicalDestinationDirectory = GetFinalPath(destinationDirectoryLease.TargetHandle);
        Exception? operationFailure = null;
        Exception? cleanupFailure = null;
        var promotionCompleted = false;
        FileStream? stageStream = null;
        try
        {
            stageStream = OpenExportStage(stage);
            var physicalStageDirectory = Path.GetDirectoryName(GetFinalPath(stageStream.SafeFileHandle));
            if (physicalStageDirectory is null
                || !PathsEqual(physicalDestinationDirectory, physicalStageDirectory))
            {
                throw new IOException("export.stage-parent-mismatch");
            }

            await stageStream.WriteAsync(content, cancellationToken).ConfigureAwait(false);
            await stageStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            stageStream.Flush(flushToDisk: true);
            cancellationToken.ThrowIfCancellationRequested();
            if (stageReady is not null)
            {
                await stageReady(stage).ConfigureAwait(false);
            }

            if (!PathsEqual(
                    physicalDestinationDirectory,
                    GetFinalPath(destinationDirectoryLease.TargetHandle)))
            {
                throw new IOException("export.destination-parent-moved");
            }

            // Cancellation and promotion share one non-blocking linearization
            // point. A cancellation that wins leaves the destination intact;
            // once promotion wins, the completed export remains a success
            // instead of being reported as a false cancellation.
            if (cancellationToken.IsCancellationRequested
                || Interlocked.CompareExchange(ref promotionState, 2, 0) != 0)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            PromoteExportStage(stageStream.SafeFileHandle, renameInformation);
            promotionCompleted = true;
        }
        catch (Exception failure)
        {
            // A failure after the durable, atomic rename cannot turn an
            // already-completed export into a false failure report.
            if (!promotionCompleted)
            {
                operationFailure = failure;
            }
        }
        finally
        {
            if (stageStream is not null)
            {
                // On failure, mark the exact owned file for deletion while its
                // handle is still alive. Never reopen a pathname that junction
                // metadata could have redirected after stage creation.
                if (!promotionCompleted)
                {
                    cleanupFailure = TryMarkExportStageForDeletion(stageStream.SafeFileHandle);
                }

                try
                {
                    await stageStream.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception disposalFailure)
                {
                    // A disposal failure after the durable, atomic rename cannot
                    // turn an already-completed export into a false failure.
                    if (!promotionCompleted)
                    {
                        cleanupFailure = cleanupFailure is null
                            ? disposalFailure
                            : new AggregateException(cleanupFailure, disposalFailure);
                    }
                }
            }
        }

        if (cleanupFailure is not null)
        {
            throw new IOException(
                UiStrings.WithoutMnemonic(UiStrings.ExportStageResidueNotRemoved),
                operationFailure is null
                    ? cleanupFailure
                    : new AggregateException(operationFailure, cleanupFailure));
        }

        if (operationFailure is not null)
        {
            ExceptionDispatchInfo.Capture(operationFailure).Throw();
        }
    }

    private static FileStream OpenExportStage(string stage)
    {
        var handle = CreateFile(
            stage,
            GenericWrite | DeleteAccess,
            // The adversarial-test seam may inspect the completed bytes, but
            // no second handle may write, delete, or rename the stage. The
            // source of the later promotion therefore remains this handle.
            FileShare.Read,
            securityAttributes: 0,
            FileMode.CreateNew,
            FileAttributeNormal | FileFlagOverlapped | FileFlagWriteThrough,
            templateFile: 0);
        if (handle.IsInvalid)
        {
            var error = new Win32Exception(Marshal.GetLastWin32Error());
            handle.Dispose();
            throw new IOException("export.stage-open-failed", error);
        }

        try
        {
            return new FileStream(handle, FileAccess.Write, bufferSize: 4096, isAsync: true);
        }
        catch (Exception streamFailure)
        {
            Exception? cleanupFailure = TryMarkExportStageForDeletion(handle);
            try
            {
                handle.Dispose();
            }
            catch (Exception disposalFailure)
            {
                cleanupFailure = cleanupFailure is null
                    ? disposalFailure
                    : new AggregateException(cleanupFailure, disposalFailure);
            }

            if (cleanupFailure is not null)
            {
                throw new IOException(
                    UiStrings.WithoutMnemonic(UiStrings.ExportStageResidueNotRemoved),
                    new AggregateException(streamFailure, cleanupFailure));
            }

            throw;
        }
    }

    private static ExportDirectoryLease OpenExportDirectoryLease(string directory)
    {
        var resolvedDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        var root = Path.GetPathRoot(resolvedDirectory)
            ?? throw new IOException("export.destination-root-missing");
        var relative = Path.GetRelativePath(root, resolvedDirectory);
        var handles = new List<SafeFileHandle>();
        try
        {
            // Hold every mutable namespace component without delete sharing.
            // OPEN_REPARSE_POINT binds a junction/symlink entry itself while
            // intermediate held components prevent its ancestors moving.
            var component = root;
            foreach (var segment in relative.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries))
            {
                if (segment.Equals(".", StringComparison.Ordinal))
                {
                    continue;
                }

                component = Path.Combine(component, segment);
                handles.Add(OpenExportDirectoryHandle(
                    component,
                    openReparsePoint: true,
                    desiredAccess: 0));
            }

            // Also hold the followed target and use it to bind the physical
            // parent of the newly-created stage.
            var targetHandle = OpenExportDirectoryHandle(
                resolvedDirectory,
                openReparsePoint: false,
                desiredAccess: 0);
            handles.Add(targetHandle);
            return new ExportDirectoryLease(targetHandle, handles);
        }
        catch
        {
            foreach (var handle in handles)
            {
                handle.Dispose();
            }

            throw;
        }
    }

    private static SafeFileHandle OpenExportDirectoryHandle(
        string directory,
        bool openReparsePoint,
        uint desiredAccess)
    {
        var handle = CreateFile(
            directory,
            desiredAccess,
            // Withhold delete sharing so the exact namespace entry cannot be
            // renamed or deleted while it participates in this export lease.
            // Write sharing is required by the filesystem's internal open for
            // a same-directory, handle-owned rename.
            FileShare.ReadWrite,
            securityAttributes: 0,
            FileMode.Open,
            FileFlagBackupSemantics | (openReparsePoint ? FileFlagOpenReparsePoint : 0),
            templateFile: 0);
        if (handle.IsInvalid)
        {
            var error = new Win32Exception(Marshal.GetLastWin32Error());
            handle.Dispose();
            throw new IOException("export.destination-parent-open-failed", error);
        }

        return handle;
    }

    internal static byte[] BuildExportRenameInformation(string destinationLeaf)
    {
        if (destinationLeaf.Length == 0
            || destinationLeaf.Contains(Path.DirectorySeparatorChar)
            || destinationLeaf.Contains(Path.AltDirectorySeparatorChar)
            || destinationLeaf.Contains(Path.VolumeSeparatorChar)
            || destinationLeaf.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new IOException(UiStrings.WithoutMnemonic(UiStrings.ExportDestinationNoParent));
        }

        // FILE_RENAME_INFO is variable length. Its HANDLE field is naturally
        // aligned: offset 8 in a 64-bit process and offset 4 in a 32-bit one.
        // RootDirectory deliberately remains NULL. The documented simple-leaf
        // form renames the already-open stage within its current physical
        // parent. It therefore cannot re-resolve an absolute destination through
        // junction metadata that changes after the stage-parent identity check.
        var destinationBytes = Encoding.Unicode.GetBytes(destinationLeaf);
        var rootDirectoryOffset = IntPtr.Size == 8 ? 8 : 4;
        var fileNameLengthOffset = checked(rootDirectoryOffset + IntPtr.Size);
        var fileNameOffset = checked(fileNameLengthOffset + sizeof(uint));
        var renameStructureSize = IntPtr.Size == 8 ? 24 : 16;
        // Include the platform structure's trailing WCHAR/padding plus a zero
        // terminator. FileNameLength itself excludes that terminator.
        var renameInformation = new byte[checked(
            renameStructureSize + destinationBytes.Length + sizeof(char))];
        renameInformation[0] = 1; // ReplaceIfExists = TRUE.
        if (!BitConverter.TryWriteBytes(
                renameInformation.AsSpan(fileNameLengthOffset, sizeof(uint)),
                checked((uint)destinationBytes.Length)))
        {
            throw new IOException("export.stage-rename-buffer-failed");
        }

        destinationBytes.CopyTo(renameInformation.AsSpan(fileNameOffset));
        return renameInformation;
    }

    private static void PromoteExportStage(
        SafeFileHandle stageHandle,
        byte[] renameInformation)
    {
        var status = NtSetInformationFile(
                stageHandle,
                out _,
                renameInformation,
                checked((uint)renameInformation.Length),
                FileRenameInformation);
        if (status < 0)
        {
            throw new IOException(
                "export.stage-promotion-failed",
                new Win32Exception(unchecked((int)RtlNtStatusToDosError(status))));
        }
    }

    private static IOException? TryMarkExportStageForDeletion(SafeFileHandle stageHandle)
    {
        var disposition = new byte[] { 1 };
        if (!SetFileInformationByHandle(
                stageHandle,
                FileDispositionInfo,
                disposition,
                checked((uint)disposition.Length)))
        {
            return new IOException(
                "export.stage-delete-disposition-failed",
                new Win32Exception(Marshal.GetLastWin32Error()));
        }

        return null;
    }

    /// <summary>
    /// Writes the print view to a temporary file, asks the default browser for
    /// the one-time loopback response, and waits until that response is written.
    /// Browser display and printer state remain human-verification boundaries.
    /// </summary>
    public static void OpenPrintView(
        ApprovedArtifact artifact,
        string name,
        RenderAudience audience = RenderAudience.Learner,
        double textScalePercent = 100,
        bool targetLanguageFirst = false,
        IAssetCatalog? assetCatalog = null)
    {
        var content = Render(
            artifact,
            new RenderRequest(RenderTarget.PrintHtml, audience, textScalePercent, targetLanguageFirst),
            assetCatalog);
        var temporaryRoot = Path.GetTempPath();
        PrintViewLease? lease = null;
        try
        {
            lease = CreatePrintViewLease(name, temporaryRoot, content);

            // The helper copies and hashes the approved bytes while this lease
            // still denies mutation. It writes that owned copy only in response
            // to an exact GET on a random loopback URL, so shell reuse and app
            // shutdown do not reopen a mutable pathname.
            // Browser launch also lives inside the killable helper boundary,
            // so a stuck shell association cannot strand an unbounded worker.
            // Shipped surfaces call this synchronous core through the async
            // wrapper below and therefore keep the WinForms loop responsive.
            using var handoff = StartPrintViewHandoff(
                lease.Path,
                content,
                launchBrowser: true);
            handoff.WaitForResponseWrite();
        }
        finally
        {
            try
            {
                if (lease is not null)
                {
                    try
                    {
                        lease.Dispose();
                    }
                    finally
                    {
                        TryDeletePrintViewJob(lease.Path, temporaryRoot);
                    }
                }
            }
            finally
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(content);
            }
        }
    }

    /// <summary>
    /// Creates and writes one unique print view through one exclusive CreateNew
    /// handle, then reopens and revalidates the exact bytes through a read-only
    /// handle that admits ordinary readers while denying mutation, replacement,
    /// and deletion. That content handle plus a separate exclusive marker handle
    /// form the job's lease. The display
    /// name is data, never a path: separators, drive syntax, and punctuation
    /// collapse to a bounded safe stem before any directory is created. The
    /// optional callback is a deterministic adversarial-test seam after the job
    /// directory exists but before the leaf is opened.
    /// </summary>
    internal static PrintViewLease CreatePrintViewLease(
        string name,
        string temporaryRoot,
        ReadOnlyMemory<byte> approvedBytes,
        Action<string>? jobReady = null,
        Action<string>? contentWritten = null)
    {
        lock (PrintViewCreationLock)
        {
            return CreatePrintViewLeaseCore(
                name,
                temporaryRoot,
                approvedBytes,
                jobReady,
                contentWritten);
        }
    }

    /// <summary>
    /// Runs the bounded print-view handoff away from the WinForms message loop.
    /// The child process owns the independent serve and shell-launch deadlines.
    /// </summary>
    public static Task OpenPrintViewAsync(
        ApprovedArtifact artifact,
        string name,
        RenderAudience audience = RenderAudience.Learner,
        double textScalePercent = 100,
        bool targetLanguageFirst = false,
        IAssetCatalog? assetCatalog = null)
        => Task.Run(() => OpenPrintView(
            artifact,
            name,
            audience,
            textScalePercent,
            targetLanguageFirst,
            assetCatalog));

    private static PrintViewLease CreatePrintViewLeaseCore(
        string name,
        string temporaryRoot,
        ReadOnlyMemory<byte> approvedBytes,
        Action<string>? jobReady,
        Action<string>? contentWritten)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(temporaryRoot);
        if (name.Length > 256)
        {
            throw new ArgumentException("print-view.name-too-long", nameof(name));
        }

        lock (PrintViewLeaseLock)
        {
            // A live lease may still be feeding the copy-owning helper. Never
            // retire it merely to admit another; refuse at the explicit bound.
            if (ActivePrintViewLeases.Count >= MaxActivePrintViewLeases)
            {
                throw new IOException("print-view.active-limit");
            }
        }

        var safeSuffix = new StringBuilder(Math.Min(name.Length, 59));
        var separatorPending = false;
        foreach (var character in name)
        {
            if (char.IsLetterOrDigit(character))
            {
                if (separatorPending && safeSuffix.Length > 0 && safeSuffix.Length < 59)
                {
                    safeSuffix.Append('-');
                }

                separatorPending = false;
                if (safeSuffix.Length < 59)
                {
                    safeSuffix.Append(character);
                }
            }
            else
            {
                separatorPending = true;
            }
        }

        // Prefix every display-derived stem so Windows device basenames such as
        // CON, NUL, COM1, and LPT1 can never become the file's basename.
        var safeStem = safeSuffix.Length == 0 ? "view" : $"view-{safeSuffix}";
        var printRoot = Path.GetFullPath(temporaryRoot);
        CleanupPrintViewJobs(printRoot, PrintViewStaleAge);
        Directory.CreateDirectory(printRoot);
        using var printRootHandle = OpenDirectoryHandle(printRoot);
        var physicalPrintRoot = GetFinalPath(printRootHandle);

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var jobName = $"{EngineIdentity.InternalId}{PrintViewJobMarker}{Guid.NewGuid():N}";
            var jobDirectory = Path.GetFullPath(Path.Combine(printRoot, jobName));
            if (!IsDirectChild(printRoot, jobDirectory)
                || Directory.Exists(jobDirectory)
                || File.Exists(jobDirectory))
            {
                continue;
            }

            Directory.CreateDirectory(jobDirectory);
            if ((File.GetAttributes(jobDirectory) & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("print-view.job-reparse");
            }

            var path = Path.Combine(jobDirectory, $"{safeStem}.print.html");
            var leasePath = Path.Combine(jobDirectory, PrintViewLeaseFileName);
            FileStream? leaseHandle = null;
            FileStream? contentWriter = null;
            FileStream? contentHandle = null;
            var createdLeaf = false;
            try
            {
                jobReady?.Invoke(path);
                leaseHandle = new FileStream(
                    leasePath,
                    new FileStreamOptions
                    {
                        Mode = FileMode.CreateNew,
                        Access = FileAccess.ReadWrite,
                        Share = FileShare.None,
                        BufferSize = 1,
                        Options = FileOptions.DeleteOnClose,
                    });
                var physicalLeasePath = GetFinalPath(leaseHandle.SafeFileHandle);

                contentWriter = new FileStream(
                    path,
                    new FileStreamOptions
                    {
                        Mode = FileMode.CreateNew,
                        Access = FileAccess.ReadWrite,
                        Share = FileShare.None,
                        BufferSize = 4096,
                        Options = FileOptions.WriteThrough,
                    });
                createdLeaf = true;

                var physicalPath = GetFinalPath(contentWriter.SafeFileHandle);
                var physicalLeaseDirectory = Path.GetDirectoryName(physicalLeasePath);
                var physicalContentDirectory = Path.GetDirectoryName(physicalPath);
                var expectedPhysicalJobDirectory = Path.Combine(physicalPrintRoot, jobName);
                if (!IsWithinRoot(physicalPrintRoot, physicalLeasePath)
                    || !IsWithinRoot(physicalPrintRoot, physicalPath)
                    || physicalLeaseDirectory is null
                    || physicalContentDirectory is null
                    || !PathsEqual(physicalLeaseDirectory, physicalContentDirectory)
                    || !IsDirectChild(physicalPrintRoot, physicalContentDirectory)
                    || !PathsEqual(expectedPhysicalJobDirectory, physicalContentDirectory))
                {
                    throw new IOException("print-view.physical-job-mismatch");
                }

                if ((File.GetAttributes(jobDirectory) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException("print-view.job-reparse-race");
                }

                contentWriter.Write(approvedBytes.Span);
                contentWriter.Flush(flushToDisk: true);
                contentWriter.Dispose();
                contentWriter = null;

                // Windows share checks are bidirectional: retaining a writable
                // handle would block ordinary readers that do not share write.
                // Reopen read-only with a restrictive share, then bind and
                // compare the bytes again. Any replacement in the unavoidable
                // close/reopen interval is either refused here or becomes the
                // exact approved file we subsequently hold immutable.
                contentWritten?.Invoke(path);
                contentHandle = new FileStream(
                    path,
                    new FileStreamOptions
                    {
                        Mode = FileMode.Open,
                        Access = FileAccess.Read,
                        Share = FileShare.Read,
                        BufferSize = 4096,
                        Options = FileOptions.SequentialScan,
                    });

                var heldPhysicalPath = GetFinalPath(contentHandle.SafeFileHandle);
                if (!PathsEqual(physicalPath, heldPhysicalPath)
                    || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0
                    || !StreamContentEquals(contentHandle, approvedBytes.Span))
                {
                    throw new IOException("print-view.content-changed-before-lease");
                }

                var lease = new PrintViewLease(path, jobDirectory, contentHandle, leaseHandle);
                RegisterPrintViewLease(lease);
                contentHandle = null;
                leaseHandle = null;
                return lease;
            }
            catch
            {
                contentWriter?.Dispose();
                contentHandle?.Dispose();
                leaseHandle?.Dispose();
                if (createdLeaf)
                {
                    TryDeletePrintViewJob(path, printRoot);
                }
                else
                {
                    TryDeleteEmptyPrintViewJobDirectory(printRoot, jobDirectory);
                }

                throw;
            }
        }

        throw new IOException("print-view.job-name-exhausted");
    }

    internal static void CleanupPrintViewJobs(
        string temporaryRoot,
        TimeSpan minimumAge,
        Action<string>? cleanupReady = null,
        Action<string>? quarantineReady = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(temporaryRoot);
        ArgumentOutOfRangeException.ThrowIfLessThan(minimumAge, TimeSpan.Zero);

        var printRoot = Path.GetFullPath(temporaryRoot);
        if (!Directory.Exists(printRoot))
        {
            return;
        }

        CleanupPrintViewJobsByPattern(
            printRoot,
            $"{EngineIdentity.InternalId}{PrintViewJobMarker}*",
            minimumAge,
            isQuarantine: false,
            cleanupReady,
            quarantineReady);
        CleanupPrintViewJobsByPattern(
            printRoot,
            $".{EngineIdentity.InternalId}{PrintViewJobMarker}{PrintViewCleanupMarker}*",
            minimumAge,
            isQuarantine: true,
            cleanupReady: null,
            quarantineReady: null);
    }

    private static void CleanupPrintViewJobsByPattern(
        string printRoot,
        string pattern,
        TimeSpan minimumAge,
        bool isQuarantine,
        Action<string>? cleanupReady,
        Action<string>? quarantineReady)
    {
        foreach (var jobDirectory in Directory.EnumerateDirectories(
            printRoot,
            pattern,
            SearchOption.TopDirectoryOnly))
        {
            try
            {
                var ownedName = Path.GetFileName(jobDirectory);
                if ((!isQuarantine && !IsOwnedPrintViewJobName(ownedName))
                    || (isQuarantine && !IsOwnedPrintViewQuarantineName(ownedName))
                    || (!isQuarantine && IsActivePrintViewJob(jobDirectory))
                    || (File.GetAttributes(jobDirectory) & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                var age = DateTime.UtcNow - Directory.GetLastWriteTimeUtc(jobDirectory);
                if (age >= minimumAge)
                {
                    cleanupReady?.Invoke(jobDirectory);
                    if (isQuarantine)
                    {
                        TryDeleteOwnedPrintViewQuarantine(printRoot, jobDirectory);
                    }
                    else
                    {
                        TryDeleteOwnedPrintViewDirectory(printRoot, jobDirectory, quarantineReady);
                    }
                }
            }
            catch (Exception failure) when (failure is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException)
            {
                // Another process may still own or be creating this unique job.
                // Leave uncertain state untouched; its own delayed cleanup or a
                // later age-bounded sweep can retry.
            }
        }
    }

    private static bool TryDeletePrintViewJob(string path, string temporaryRoot)
    {
        try
        {
            var printRoot = Path.GetFullPath(temporaryRoot);
            var resolvedPath = Path.GetFullPath(path);
            var jobDirectory = Path.GetDirectoryName(resolvedPath);
            if (jobDirectory is null
                || !IsDirectChild(printRoot, jobDirectory)
                || !IsOwnedPrintViewJobName(Path.GetFileName(jobDirectory)))
            {
                return false;
            }

            return TryDeleteOwnedPrintViewDirectory(printRoot, jobDirectory);
        }
        catch (Exception failure) when (failure is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
            return false;
        }
    }

    private static bool TryDeleteOwnedPrintViewDirectory(
        string printRoot,
        string jobDirectory,
        Action<string>? quarantineReady = null)
    {
        try
        {
            var resolvedDirectory = Path.GetFullPath(jobDirectory);
            if (!IsDirectChild(printRoot, resolvedDirectory)
                || !IsOwnedPrintViewJobName(Path.GetFileName(resolvedDirectory))
                || IsActivePrintViewJob(resolvedDirectory))
            {
                return false;
            }

            using var printRootHandle = OpenDirectoryHandle(printRoot);
            _ = GetFinalPath(printRootHandle);
            var quarantineDirectory = NewPrintViewQuarantinePath(printRoot);
            Directory.Move(resolvedDirectory, quarantineDirectory);
            quarantineReady?.Invoke(quarantineDirectory);

            return TryDeleteOwnedPrintViewQuarantine(printRoot, quarantineDirectory);
        }
        catch (Exception failure) when (failure is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
            return false;
        }
    }

    private static bool TryDeleteOwnedPrintViewQuarantine(
        string printRoot,
        string quarantineDirectory)
    {
        try
        {
            var resolvedDirectory = Path.GetFullPath(quarantineDirectory);
            if (!IsDirectChild(printRoot, resolvedDirectory)
                || !IsOwnedPrintViewQuarantineName(Path.GetFileName(resolvedDirectory))
                || (File.GetAttributes(resolvedDirectory) & FileAttributes.ReparsePoint) != 0)
            {
                return false;
            }

            using var printRootHandle = OpenDirectoryHandle(printRoot);
            var physicalPrintRoot = GetFinalPath(printRootHandle);
            using (var quarantineHandle = OpenDirectoryHandle(resolvedDirectory))
            {
                var physicalQuarantineDirectory = GetFinalPath(quarantineHandle);
                var expectedPhysicalQuarantineDirectory = Path.Combine(
                    physicalPrintRoot,
                    Path.GetFileName(resolvedDirectory));
                if (!IsDirectChild(physicalPrintRoot, physicalQuarantineDirectory)
                    || !PathsEqual(
                        expectedPhysicalQuarantineDirectory,
                        physicalQuarantineDirectory)
                    || (File.GetAttributes(resolvedDirectory) & FileAttributes.ReparsePoint) != 0)
                {
                    return false;
                }

                var childDirectories = Directory.EnumerateDirectories(
                    resolvedDirectory,
                    "*",
                    SearchOption.TopDirectoryOnly).Take(1).Any();
                if (childDirectories)
                {
                    return false;
                }

                var files = Directory.EnumerateFiles(
                    resolvedDirectory,
                    "*",
                    SearchOption.TopDirectoryOnly).ToArray();
                if (files.Any(file => !file.EndsWith(".print.html", StringComparison.OrdinalIgnoreCase)
                    || (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0))
                {
                    return false;
                }

                foreach (var file in files)
                {
                    File.Delete(file);
                }
            }

            Directory.Delete(resolvedDirectory, recursive: false);
            return true;
        }
        catch (Exception failure) when (failure is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
            return false;
        }
    }

    private static string NewPrintViewQuarantinePath(string printRoot)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var candidate = Path.GetFullPath(Path.Combine(
                printRoot,
                $".{EngineIdentity.InternalId}{PrintViewJobMarker}{PrintViewCleanupMarker}{Guid.NewGuid():N}"));
            if (IsDirectChild(printRoot, candidate)
                && !Directory.Exists(candidate)
                && !File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("print-view.quarantine-name-exhausted");
    }

    private static void TryDeleteEmptyPrintViewJobDirectory(
        string printRoot,
        string jobDirectory)
    {
        try
        {
            var resolvedDirectory = Path.GetFullPath(jobDirectory);
            if (!IsDirectChild(printRoot, resolvedDirectory)
                || !IsOwnedPrintViewJobName(Path.GetFileName(resolvedDirectory))
                || (File.GetAttributes(resolvedDirectory) & FileAttributes.ReparsePoint) != 0)
            {
                return;
            }

            Directory.Delete(resolvedDirectory, recursive: false);
        }
        catch (Exception failure) when (failure is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
            // A pre-existing leaf belongs to the contender that created it.
            // Never remove or overwrite it while unwinding our failed CreateNew.
        }
    }

    private static void RegisterPrintViewLease(PrintViewLease lease)
    {
        lock (PrintViewLeaseLock)
        {
            if (ActivePrintViewLeases.Count >= MaxActivePrintViewLeases)
            {
                throw new IOException("print-view.active-limit-race");
            }

            if (!ActivePrintViewLeases.TryAdd(lease.JobDirectory, lease))
            {
                throw new IOException("print-view.job-active-collision");
            }

        }
    }

    private static void ReleasePrintViewLease(PrintViewLease lease)
    {
        lock (PrintViewLeaseLock)
        {
            if (ActivePrintViewLeases.TryGetValue(lease.JobDirectory, out var active)
                && ReferenceEquals(active, lease))
            {
                ActivePrintViewLeases.Remove(lease.JobDirectory);
            }
        }
    }

    internal static void ReleaseAllPrintViewLeases()
    {
        lock (PrintViewCreationLock)
        {
            PrintViewLease[] leases;
            lock (PrintViewLeaseLock)
            {
                leases = [.. ActivePrintViewLeases.Values];
                ActivePrintViewLeases.Clear();
            }

            foreach (var lease in leases)
            {
                try
                {
                    lease.Dispose();
                }
                catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
                {
                    // Process shutdown is best effort, but every remaining
                    // lease is still attempted and the next startup sweep can
                    // retry any job whose browser handle delayed deletion.
                }

                var printRoot = Path.GetDirectoryName(lease.JobDirectory);
                if (printRoot is not null)
                {
                    TryDeletePrintViewJob(lease.Path, printRoot);
                }
            }
        }
    }

    private static bool IsActivePrintViewJob(string jobDirectory)
    {
        var resolvedDirectory = Path.GetFullPath(jobDirectory);
        lock (PrintViewLeaseLock)
        {
            return ActivePrintViewLeases.ContainsKey(resolvedDirectory);
        }
    }

    private static SafeFileHandle OpenDirectoryHandle(string directory)
    {
        var handle = CreateFile(
            directory,
            desiredAccess: 0,
            FileShare.ReadWrite,
            securityAttributes: 0,
            FileMode.Open,
            FileFlagBackupSemantics,
            templateFile: 0);
        if (handle.IsInvalid)
        {
            var error = new Win32Exception(Marshal.GetLastWin32Error());
            handle.Dispose();
            throw new IOException("print-view.root-open-failed", error);
        }

        return handle;
    }

    private static string GetFinalPath(SafeFileHandle handle)
    {
        var capacity = 512u;
        while (capacity <= 32768)
        {
            var path = new char[checked((int)capacity)];
            var length = GetFinalPathNameByHandle(
                handle,
                path,
                capacity,
                FinalPathNameNormalized | VolumeNameDos);
            if (length == 0)
            {
                throw new IOException(
                    "print-view.handle-path-unresolved",
                    new Win32Exception(Marshal.GetLastWin32Error()));
            }

            if (length < capacity)
            {
                return NormalizeFinalPath(new string(path, 0, checked((int)length)));
            }

            capacity = checked(length + 1);
        }

        throw new IOException("print-view.handle-path-too-long");
    }

    private static string NormalizeFinalPath(string path)
    {
        const string extendedUncPrefix = @"\\?\UNC\";
        const string extendedPrefix = @"\\?\";
        if (path.StartsWith(extendedUncPrefix, StringComparison.OrdinalIgnoreCase))
        {
            path = @"\\" + path[extendedUncPrefix.Length..];
        }
        else if (path.StartsWith(extendedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            path = path[extendedPrefix.Length..];
        }

        return Path.GetFullPath(path);
    }

    private static bool IsWithinRoot(string root, string candidate)
    {
        var relative = Path.GetRelativePath(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)),
            Path.GetFullPath(candidate));
        return !Path.IsPathRooted(relative)
            && !relative.Equals(".", StringComparison.Ordinal)
            && !relative.Equals("..", StringComparison.Ordinal)
            && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && !relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private static bool PathsEqual(string left, string right)
        => string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            StringComparison.OrdinalIgnoreCase);

    private static bool StreamContentEquals(FileStream stream, ReadOnlySpan<byte> expected)
    {
        if (stream.Length != expected.Length)
        {
            return false;
        }

        stream.Position = 0;
        Span<byte> buffer = stackalloc byte[4096];
        var offset = 0;
        while (offset < expected.Length)
        {
            var requested = Math.Min(buffer.Length, expected.Length - offset);
            var read = stream.Read(buffer[..requested]);
            if (read == 0 || !buffer[..read].SequenceEqual(expected.Slice(offset, read)))
            {
                return false;
            }

            offset += read;
        }

        stream.Position = 0;
        return true;
    }

    private static bool IsOwnedPrintViewJobName(string name)
    {
        var prefix = $"{EngineIdentity.InternalId}{PrintViewJobMarker}";
        if (!name.StartsWith(prefix, StringComparison.Ordinal)
            || name.Length != prefix.Length + 32)
        {
            return false;
        }

        var suffix = name.AsSpan(prefix.Length);
        return suffix.ToString().All(Uri.IsHexDigit);
    }

    private static bool IsOwnedPrintViewQuarantineName(string name)
    {
        var prefix = $".{EngineIdentity.InternalId}{PrintViewJobMarker}{PrintViewCleanupMarker}";
        if (!name.StartsWith(prefix, StringComparison.Ordinal)
            || name.Length != prefix.Length + 32)
        {
            return false;
        }

        var suffix = name.AsSpan(prefix.Length);
        return suffix.ToString().All(Uri.IsHexDigit);
    }

    private static bool IsDirectChild(string parent, string candidate)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(parent), Path.GetFullPath(candidate));
        return !Path.IsPathRooted(relative)
            && !relative.Contains(Path.DirectorySeparatorChar)
            && !relative.Contains(Path.AltDirectorySeparatorChar)
            && !relative.Equals(".", StringComparison.Ordinal)
            && !relative.Equals("..", StringComparison.Ordinal);
    }

    private const uint GenericWrite = 0x40000000;
    private const uint DeleteAccess = 0x00010000;
    private const uint FileAttributeNormal = 0x00000080;
    private const uint FileFlagWriteThrough = 0x80000000;
    private const uint FileFlagOverlapped = 0x40000000;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private const uint FinalPathNameNormalized = 0x00000000;
    private const uint VolumeNameDos = 0x00000000;
    private const int FileRenameInformation = 10;
    private const int FileDispositionInfo = 4;

    // The app project intentionally does not enable unsafe code. These two
    // SafeHandle/array declarations therefore use runtime marshalling instead
    // of LibraryImport's unsafe generated stubs.
#pragma warning disable SYSLIB1054
    [DllImport(
        "kernel32.dll",
        EntryPoint = "CreateFileW",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        FileShare shareMode,
        nint securityAttributes,
        FileMode creationDisposition,
        uint flagsAndAttributes,
        nint templateFile);

    [DllImport(
        "kernel32.dll",
        EntryPoint = "GetFinalPathNameByHandleW",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(
        SafeFileHandle file,
        [Out] char[] filePath,
        uint filePathCharacterCount,
        uint flags);

    [DllImport("ntdll.dll")]
    private static extern int NtSetInformationFile(
        SafeFileHandle file,
        out IoStatusBlock ioStatusBlock,
        [In] byte[] fileInformation,
        uint bufferSize,
        int fileInformationClass);

    [DllImport("ntdll.dll")]
    private static extern uint RtlNtStatusToDosError(int status);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(
        SafeFileHandle file,
        int fileInformationClass,
        [In] byte[] fileInformation,
        uint bufferSize);
#pragma warning restore SYSLIB1054

    [StructLayout(LayoutKind.Sequential)]
    private struct IoStatusBlock
    {
        internal nint Status;
        internal nuint Information;
    }

    private sealed class ExportDirectoryLease : IDisposable
    {
        private List<SafeFileHandle>? _handles;

        internal ExportDirectoryLease(
            SafeFileHandle targetHandle,
            List<SafeFileHandle> handles)
        {
            TargetHandle = targetHandle;
            _handles = handles;
        }

        internal SafeFileHandle TargetHandle { get; }

        public void Dispose()
        {
            var handles = Interlocked.Exchange(ref _handles, null);
            if (handles is null)
            {
                return;
            }

            for (var index = handles.Count - 1; index >= 0; index--)
            {
                handles[index].Dispose();
            }
        }
    }

    internal sealed class PrintViewLease : IDisposable
    {
        private readonly FileStream _contentHandle;
        private readonly FileStream _markerHandle;
        private int _disposed;

        internal PrintViewLease(
            string path,
            string jobDirectory,
            FileStream contentHandle,
            FileStream markerHandle)
        {
            Path = System.IO.Path.GetFullPath(path);
            JobDirectory = System.IO.Path.GetFullPath(jobDirectory);
            _contentHandle = contentHandle;
            _markerHandle = markerHandle;
        }

        internal string Path { get; }

        internal string JobDirectory { get; }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            try
            {
                _contentHandle.Dispose();
            }
            finally
            {
                try
                {
                    _markerHandle.Dispose();
                }
                finally
                {
                    ReleasePrintViewLease(this);
                }
            }

            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// The teacher's project library root. The ordinary default is itself
    /// version-addressed; a managed deployment may replace it only through the
    /// validated production switch before a form opens. The internal setter is
    /// retained solely for disposable UI test libraries.
    /// </summary>
    internal static string DefaultLibraryRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        EngineIdentity.InternalId,
        EngineIdentity.EngineVersion,
        "projects");

    public static string LibraryRoot { get; internal set; } = DefaultLibraryRoot;

    /// <summary>Saves to the teacher's project library; returns the project name used.</summary>
    public static string SaveToLibrary(
        ApprovedArtifact artifact,
        string hintPrefix,
        string moduleId,
        string recipeId,
        string recipeVersion,
        IAssetCatalog catalog,
        ProjectValidationEnvelope? validation = null,
        ProjectRenderProfile? renderProfile = null)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        validation ??= ProjectValidationEnvelope.Exact(artifact, recipeId, recipeVersion);
        renderProfile ??= ProjectRenderProfile.For(artifact);
        var documentHash = ArtifactDocumentFingerprint.Compute(artifact.Revision.Document);
        if (!string.Equals(validation.RecipeId, recipeId, StringComparison.Ordinal)
            || !string.Equals(validation.RecipeVersion, recipeVersion, StringComparison.Ordinal)
            || validation.Lane != artifact.Revision.Lane
            || validation.Purpose != artifact.Revision.Purpose
            || !string.Equals(validation.ArtifactSha256, documentHash, StringComparison.Ordinal)
            || !string.Equals(renderProfile.ArtifactSha256, documentHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                UiStrings.WithoutMnemonic(UiStrings.SavedArtifactContextMismatch));
        }

        var store = new OcfprojProjectStore(LibraryRoot, new AccessibleHtmlRenderer(), catalog);

        var hint = UiStrings.Format("{0}-{1}", hintPrefix,
            DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture));
        store.SaveGreenProjectAsync(
            artifact,
            new ProjectSaveRequest(
                hint,
                moduleId,
                recipeId,
                recipeVersion,
                DateTimeOffset.UtcNow,
                validation,
                renderProfile),
            CancellationToken.None).GetAwaiter().GetResult();
        return hint;
    }

    /// <summary>Reopens a saved Green project through the hardened reader; reversibility (constitution 11) as one call.</summary>
    public static LoadedProject OpenFromLibrary(string path)
    {
        var resolved = ProjectLibraryRootConfiguration.ResolveProjectFileInsideConfiguredRoot(path);
        return OcfprojProjectStore.LoadProjectFileAsync(resolved, CancellationToken.None)
            .GetAwaiter().GetResult();
    }

    /// <summary>
    /// Refuses the legacy call shape that omitted a teacher lane preflight.
    /// Unknown package content defaults to Amber and cannot enter early-release
    /// Gate B or its Green-only sinks through an implicit overload.
    /// </summary>
    public static ReviewSession SessionOverLoadedProject(LoadedProject loaded)
        => throw new InvalidOperationException(
            UiStrings.WithoutMnemonic(UiStrings.StatusLoadedProjectPreflightNotConfirmed));

    /// <summary>
    /// Records a teacher's explicit content classification for this one loaded
    /// document. Unknown defaults to Amber, so an incomplete checklist cannot
    /// create the Green draft that the early-release review and sinks require.
    /// </summary>
    public static LoadedProjectGreenConfirmation ConfirmLoadedProjectGreen(
        LoadedProject loaded,
        LoadedProjectGreenChecklist checklist)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        ArgumentNullException.ThrowIfNull(checklist);
        if (!checklist.IsGreenQualifyingContent
            || !checklist.HasNoLearnerLinkedContent
            || !checklist.HasNoRestrictedContent)
        {
            throw new InvalidOperationException(
                UiStrings.WithoutMnemonic(UiStrings.LoadedProjectGreenChecklistIncomplete));
        }

        return LoadedProjectGreenConfirmation.Create(loaded);
    }

    /// <summary>
    /// Starts Gate B only after a teacher classified this exact package content
    /// as Green. Mutable package lane and purpose claims remain unauthenticated:
    /// purpose is always Unknown and the generic origin warning remains.
    /// </summary>
    public static ReviewSession SessionOverLoadedProject(
        LoadedProject loaded,
        LoadedProjectGreenConfirmation confirmation)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        ArgumentNullException.ThrowIfNull(confirmation);
        if (!confirmation.AppliesTo(loaded))
        {
            throw new InvalidOperationException(
                UiStrings.WithoutMnemonic(UiStrings.StatusLoadedProjectPreflightNotConfirmed));
        }

        if (loaded.Validation is null || loaded.RenderProfile is null)
        {
            throw new InvalidOperationException(
                UiStrings.WithoutMnemonic(UiStrings.SavedProjectNeedsManagedUpgrade));
        }

        var notices = ResolveTrustedSavedNotices();
        var validator = PersistedProjectValidator.Create(loaded.Validation, notices);
        return SessionOver(
            DraftArtifact.New(loaded.Document, DataLane.Green),
            validator,
            new ReviewViewContext(
                ReviewViewContext.ManualDefault.PreviewRequest,
                assetCatalog: loaded.Assets));
    }

    private static ValidationIssue[] ResolveTrustedSavedNotices()
    {
        // No field inside a mutable ZIP can prove which recipe, seat, or warning
        // produced it. Reopen therefore uses one engine-owned, provenance-free
        // warning independent of every package selector and inventory.
        return
        [
            ValidationIssue.Warning(
                "project.origin-unverified",
                UiStrings.WithoutMnemonic(UiStrings.SavedOriginUnverifiedWarning),
                requiresAcknowledgement: true),
        ];
    }

    /// <summary>Silent print through the same structural gate as every output; native vector PDF first, Edge fallback.</summary>
    public static void Print(
        ApprovedArtifact artifact,
        RenderAudience audience = RenderAudience.Learner,
        double textScalePercent = 100,
        bool targetLanguageFirst = false,
        IAssetCatalog? assetCatalog = null)
        => new Infrastructure.Windows.WindowsPdfPrinter(new AccessibleHtmlRenderer(assetCatalog))
            .PrintAsync(
                artifact,
                new PrintRequest(
                    PrinterName: "",
                    Duplex: false,
                    Copies: 1,
                    Audience: audience,
                    TextScalePercent: textScalePercent,
                    TargetLanguageFirst: targetLanguageFirst),
                CancellationToken.None)
            .GetAwaiter().GetResult();

    /// <summary>For artifacts that reference no assets; the store never consults it.</summary>
    public sealed class NoAssetsCatalog : IAssetCatalog
    {
        public IReadOnlyList<AssetProvenance> All => [];

        public AssetProvenance? Find(AssetId id) => null;

        public bool TryGetContent(AssetId id, out ReadOnlyMemory<byte> content, out string mimeType)
        {
            content = default;
            mimeType = "";
            return false;
        }
    }
}
