// SPDX-License-Identifier: GPL-3.0-or-later
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Rendering;

namespace Foundry.Storage;

public static class ProjectUpgradeFailureCodes
{
    public const string AddressInvalid = "upgrade.address-invalid";
    public const string SourceRootInvalid = "upgrade.source-root-invalid";
    public const string CandidateRootInvalid = "upgrade.candidate-root-invalid";
    public const string RootsOverlap = "upgrade.roots-overlap";
    public const string CandidateNotEmpty = "upgrade.candidate-not-empty";
    public const string CandidateBusy = "upgrade.candidate-busy";
    public const string CandidateContaminated = "upgrade.candidate-contaminated";
    public const string SourceMissing = "upgrade.source-missing";
    public const string SourceAddressMismatch = "upgrade.source-address-mismatch";
    public const string SourceVersionMismatch = "upgrade.source-version-mismatch";
    public const string TargetVersionMismatch = "upgrade.target-version-mismatch";
    public const string CandidateRecipeUnavailable = "upgrade.candidate-recipe-unavailable";
    public const string NoMigrationRoute = "upgrade.no-migration-route";
    public const string PackageInvalid = "upgrade.package-invalid";
    public const string DestinationConflict = "upgrade.destination-conflict";
    public const string SourceChanged = "upgrade.source-changed";
    public const string CopyInvalid = "upgrade.copy-invalid";
    public const string Canceled = "upgrade.canceled";
    public const string StorageFailure = "upgrade.storage-failure";
    public const string CleanupResidue = "upgrade.cleanup-residue";
}

/// <summary>A stable, content-free failure suitable for an approved operator inventory.</summary>
public sealed class ProjectUpgradeException : Exception
{
    internal ProjectUpgradeException(
        string code,
        string message,
        string? primaryCode = null,
        int cleanupResidueCount = 0)
        : base(message)
    {
        Code = code;
        PrimaryCode = primaryCode;
        CleanupResidueCount = cleanupResidueCount;
    }

    public string Code { get; }

    /// <summary>The original failure code when cleanup residue is a second failure.</summary>
    public string? PrimaryCode { get; }

    /// <summary>A count only; paths and package-controlled names never enter diagnostics.</summary>
    public int CleanupResidueCount { get; }
}

internal sealed record ProjectUpgradeItem(
    string SourceRelativePath,
    string DestinationRelativePath,
    string SourceEngineVersion,
    string SourceSchemaVersion,
    string SourceSha256);

/// <summary>An exact recipe identity compiled into the candidate build.</summary>
internal sealed record ProjectUpgradeRecipeIdentity(string RecipeId, string RecipeVersion);

internal sealed record ProjectUpgradeBatchRequest(
    string SourceLibraryRoot,
    string CandidateLibraryRoot,
    string TargetEngineVersion,
    IReadOnlyList<ProjectUpgradeItem> Projects,
    IReadOnlyList<ProjectUpgradeRecipeIdentity> CandidateRecipes);

/// <summary>A one-project convenience request retaining the same explicit root boundary as a batch.</summary>
internal sealed record ProjectUpgradeRequest(
    string SourceLibraryRoot,
    string CandidateLibraryRoot,
    string SourceRelativePath,
    string DestinationRelativePath,
    string SourceEngineVersion,
    string SourceSchemaVersion,
    string SourceSha256,
    string TargetEngineVersion,
    IReadOnlyList<ProjectUpgradeRecipeIdentity> CandidateRecipes);

internal sealed record ProjectUpgradeReceipt(
    string SourceEngineVersion,
    string SourceSchemaVersion,
    string SourceSha256,
    string TargetEngineVersion,
    string TargetSchemaVersion,
    string OutputSha256,
    bool PackageTransformed);

internal sealed record ProjectUpgradeBatchReceipt(
    string TargetEngineVersion,
    string TargetSchemaVersion,
    IReadOnlyList<ProjectUpgradeReceipt> Projects);

/// <summary>
/// The in-process compatibility host for a managed engine upgrade. It validates
/// exact relative addresses inside two explicit, canonical, non-overlapping
/// library roots; serializes one initially empty candidate batch; and never
/// downloads, installs, versions, signs, distributes, or publishes software.
/// </summary>
internal static partial class OcfprojUpgradeService
{
    private const int CopyBufferBytes = 128 * 1024;
    private const int MaximumBatchProjects = 512;
    private const int MaximumCandidateRecipes = 2048;
    private const string BatchLockFileName = ".ocf-upgrade-batch.lock";

    internal static async Task<ProjectUpgradeReceipt> PrepareCompatibleCopyAsync(
        ProjectUpgradeRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw Failure(ProjectUpgradeFailureCodes.AddressInvalid, "The project upgrade address is incomplete.");
        }

        var batch = new ProjectUpgradeBatchRequest(
            request.SourceLibraryRoot,
            request.CandidateLibraryRoot,
            request.TargetEngineVersion,
            [new ProjectUpgradeItem(
                request.SourceRelativePath,
                request.DestinationRelativePath,
                request.SourceEngineVersion,
                request.SourceSchemaVersion,
                request.SourceSha256)],
            request.CandidateRecipes);
        var receipt = await PrepareCompatibleBatchAsync(batch, cancellationToken).ConfigureAwait(false);
        return receipt.Projects[0];
    }

    internal static Task<ProjectUpgradeBatchReceipt> PrepareCompatibleBatchAsync(
        ProjectUpgradeBatchRequest request,
        CancellationToken cancellationToken)
        => PrepareCompatibleBatchAsync(request, hooks: null, cancellationToken);

    internal static async Task<ProjectUpgradeBatchReceipt> PrepareCompatibleBatchAsync(
        ProjectUpgradeBatchRequest request,
        ProjectUpgradeTestHooks? hooks,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw Failure(ProjectUpgradeFailureCodes.AddressInvalid, "The project upgrade batch address is incomplete.");
        }

        BatchContext? context = null;
        FileStream? batchLock = null;
        ProjectUpgradeException? primaryFailure = null;
        ProjectUpgradeBatchReceipt? successfulReceipt = null;

        try
        {
            context = ValidateBatchAddress(request);
            cancellationToken.ThrowIfCancellationRequested();
            batchLock = AcquireBatchLock(context.CandidateRoot);
            context.OwnsBatchLock = true;
            VerifyCandidateContainsOnly(context, [BatchLockFileName]);

            var receipts = new List<ProjectUpgradeReceipt>(request.Projects.Count);
            foreach (var project in request.Projects)
            {
                cancellationToken.ThrowIfCancellationRequested();
                receipts.Add(await PrepareItemAsync(context, project, hooks, cancellationToken).ConfigureAwait(false));
            }

            VerifyCandidateContainsOnly(
                context,
                [BatchLockFileName, .. context.CompletedRelativePaths]);
            successfulReceipt = new ProjectUpgradeBatchReceipt(
                EngineIdentity.EngineVersion,
                EngineIdentity.ProjectSchemaVersion,
                receipts);
        }
        catch (Exception exception)
        {
            primaryFailure = ToContentFreeFailure(exception);
        }
        finally
        {
            batchLock?.Dispose();
        }

        if (context is null)
        {
            throw primaryFailure ?? Failure(ProjectUpgradeFailureCodes.StorageFailure, "Upgrade preparation could not establish its library boundary.");
        }

        if (!context.OwnsBatchLock)
        {
            // A competing process owns whatever appeared after the empty-root
            // check. This batch has created nothing and must not clean another
            // batch's lock or candidate state.
            throw primaryFailure ?? Failure(ProjectUpgradeFailureCodes.CandidateBusy, "The candidate library is already preparing another batch.");
        }

        if (primaryFailure is null && successfulReceipt is not null)
        {
            var lockResidue = CleanupFiles(context, [context.LockPath], hooks);
            if (lockResidue == 0)
            {
                try
                {
                    VerifyCandidateContainsOnly(context, context.CompletedRelativePaths);
                    return successfulReceipt;
                }
                catch (Exception exception)
                {
                    primaryFailure = ToContentFreeFailure(exception);
                }
            }
            else
            {
                primaryFailure = Failure(
                    ProjectUpgradeFailureCodes.CleanupResidue,
                    "Upgrade preparation completed validation but could not remove its batch lock.");
            }
        }

        primaryFailure ??= Failure(ProjectUpgradeFailureCodes.StorageFailure, "Upgrade preparation failed without a completed receipt.");
        var residueCount = CleanupBatch(context, hooks);
        if (residueCount > 0)
        {
            throw new ProjectUpgradeException(
                ProjectUpgradeFailureCodes.CleanupResidue,
                "Upgrade preparation failed and cleanup left bounded candidate residue.",
                primaryFailure.Code,
                residueCount);
        }

        throw primaryFailure;
    }

    private static BatchContext ValidateBatchAddress(ProjectUpgradeBatchRequest request)
    {
        if (!RequiredText(request.TargetEngineVersion, 64)
            || request.Projects is null
            || request.Projects.Count is 0 or > MaximumBatchProjects
            || request.CandidateRecipes is null
            || request.CandidateRecipes.Count is 0 or > MaximumCandidateRecipes)
        {
            throw Failure(ProjectUpgradeFailureCodes.AddressInvalid, "The project upgrade batch address is incomplete.");
        }

        if (!string.Equals(request.TargetEngineVersion, EngineIdentity.EngineVersion, StringComparison.Ordinal))
        {
            throw Failure(ProjectUpgradeFailureCodes.TargetVersionMismatch, "The requested target does not match this engine build.");
        }

        var sourceRoot = ResolveRoot(request.SourceLibraryRoot, source: true);
        var candidateRoot = ResolveRoot(request.CandidateLibraryRoot, source: false);
        EnsureRootsDoNotOverlap(sourceRoot, candidateRoot);

        var candidateRecipes = new HashSet<ProjectUpgradeRecipeIdentity>();
        foreach (var recipe in request.CandidateRecipes)
        {
            if (recipe is null
                || !RequiredText(recipe.RecipeId, 128)
                || !RequiredText(recipe.RecipeVersion, 64)
                || !candidateRecipes.Add(recipe))
            {
                throw Failure(
                    ProjectUpgradeFailureCodes.AddressInvalid,
                    "The candidate recipe inventory is invalid.");
            }
        }

        var items = new List<ResolvedUpgradeItem>(request.Projects.Count);
        var sourceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var destinationNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var project in request.Projects)
        {
            ValidateItemAddress(project);
            var sourceRelative = CanonicalRelativePath(project.SourceRelativePath);
            var source = ResolvePackagePath(sourceRoot.FullPath, project.SourceRelativePath, mustExist: true, source: true);
            var destination = ResolvePackagePath(candidateRoot.FullPath, project.DestinationRelativePath, mustExist: false, source: false);
            var destinationRelative = CanonicalRelativePath(project.DestinationRelativePath);
            if (!sourceNames.Add(sourceRelative) || !destinationNames.Add(destinationRelative))
            {
                throw Failure(ProjectUpgradeFailureCodes.AddressInvalid, "The project upgrade batch repeats an exact package address.");
            }

            items.Add(new ResolvedUpgradeItem(project, source, destination, destinationRelative));
        }

        var initialEntries = Directory.EnumerateFileSystemEntries(candidateRoot.FullPath).ToList();
        if (initialEntries.Count != 0)
        {
            var code = initialEntries.Any(path =>
                string.Equals(Path.GetFileName(path), BatchLockFileName, StringComparison.Ordinal))
                ? ProjectUpgradeFailureCodes.CandidateBusy
                : ProjectUpgradeFailureCodes.CandidateNotEmpty;
            throw Failure(code, code == ProjectUpgradeFailureCodes.CandidateBusy
                ? "The candidate library is already preparing another batch."
                : "The candidate library must be empty before preparation begins.");
        }

        return new BatchContext(sourceRoot, candidateRoot, items, candidateRecipes);
    }

    private static void ValidateItemAddress(ProjectUpgradeItem project)
    {
        if (project is null
            || !RequiredText(project.SourceRelativePath, 512)
            || !RequiredText(project.DestinationRelativePath, 512)
            || !RequiredText(project.SourceEngineVersion, 64)
            || !RequiredText(project.SourceSchemaVersion, 32)
            || !IsSha256(project.SourceSha256))
        {
            throw Failure(ProjectUpgradeFailureCodes.AddressInvalid, "A project upgrade item has an invalid exact address.");
        }
    }

    private static RootDescriptor ResolveRoot(string root, bool source)
    {
        var code = source ? ProjectUpgradeFailureCodes.SourceRootInvalid : ProjectUpgradeFailureCodes.CandidateRootInvalid;
        if (!RequiredText(root, 1024) || !Path.IsPathFullyQualified(root))
        {
            throw Failure(code, source
                ? "The source library root is invalid."
                : "The candidate library root is invalid.");
        }

        try
        {
            var fullPath = TrimDirectorySeparator(Path.GetFullPath(root));
            if (!Directory.Exists(fullPath)
                || string.Equals(fullPath, TrimDirectorySeparator(Path.GetPathRoot(fullPath) ?? string.Empty), PathComparison))
            {
                throw Failure(code, source
                    ? "The source library root is invalid."
                    : "The candidate library root is invalid.");
            }

            EnsureNoReparseAncestors(fullPath, code);
            return new RootDescriptor(fullPath, GetDirectoryIdentity(fullPath));
        }
        catch (ProjectUpgradeException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw Failure(code, source
                ? "The source library root is invalid."
                : "The candidate library root is invalid.");
        }
    }

    private static void EnsureRootsDoNotOverlap(RootDescriptor source, RootDescriptor candidate)
    {
        if (source.Identity == candidate.Identity
            || IsAncestorIdentity(source.Identity, candidate.FullPath)
            || IsAncestorIdentity(candidate.Identity, source.FullPath))
        {
            throw Failure(ProjectUpgradeFailureCodes.RootsOverlap, "Source and candidate libraries must be distinct and non-overlapping.");
        }
    }

    private static bool IsAncestorIdentity(DirectoryIdentity possibleAncestor, string descendantPath)
    {
        var current = new DirectoryInfo(descendantPath).Parent;
        while (current is not null)
        {
            if (GetDirectoryIdentity(current.FullName) == possibleAncestor)
            {
                return true;
            }

            current = current.Parent;
        }

        return false;
    }

    private static string ResolvePackagePath(string root, string relativePath, bool mustExist, bool source)
    {
        var code = source ? ProjectUpgradeFailureCodes.SourceRootInvalid : ProjectUpgradeFailureCodes.CandidateRootInvalid;
        string canonicalRelative;
        try
        {
            canonicalRelative = CanonicalRelativePath(relativePath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            throw Failure(ProjectUpgradeFailureCodes.AddressInvalid, "A project upgrade path is not a safe relative package address.");
        }

        var combined = Path.GetFullPath(Path.Combine(root, canonicalRelative.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsContainedBy(combined, root)
            || !Path.GetExtension(combined).Equals(OcfprojProjectStore.Extension, StringComparison.OrdinalIgnoreCase))
        {
            throw Failure(ProjectUpgradeFailureCodes.AddressInvalid, "A project upgrade path is not a safe relative package address.");
        }

        if (mustExist)
        {
            if (!File.Exists(combined))
            {
                throw Failure(ProjectUpgradeFailureCodes.SourceMissing, "An addressed source project is unavailable.");
            }

            EnsureNoReparseWithinRoot(combined, root, includeFile: true, code);
        }
        else if (File.Exists(combined) || Directory.Exists(combined))
        {
            throw Failure(ProjectUpgradeFailureCodes.DestinationConflict, "A candidate project address already exists.");
        }

        return combined;
    }

    private static string CanonicalRelativePath(string relativePath)
    {
        if (Path.IsPathFullyQualified(relativePath)
            || relativePath.Contains('\\', StringComparison.Ordinal)
            || relativePath.StartsWith('/')
            || relativePath.EndsWith('/')
            || !relativePath.IsNormalized(NormalizationForm.FormC))
        {
            throw new ArgumentException("Relative package address is not canonical.", nameof(relativePath));
        }

        var segments = relativePath.Split('/');
        if (segments.Length == 0 || segments.Any(segment => !IsSafeFileSystemSegment(segment)))
        {
            throw new ArgumentException("Relative package address is not canonical.", nameof(relativePath));
        }

        return string.Join('/', segments);
    }

    private static bool IsSafeFileSystemSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment)
            || segment is "." or ".."
            || segment.Length > 180
            || segment.EndsWith(' ')
            || segment.EndsWith('.')
            || segment.Any(char.IsControl)
            || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || segment.IndexOfAny(['<', '>', ':', '"', '/', '\\', '|', '?', '*']) >= 0
            || IsReservedPortableSegment(segment))
        {
            return false;
        }

        return segment.IsNormalized(NormalizationForm.FormC);
    }

    private static bool IsReservedPortableSegment(string segment)
    {
        var stem = segment.Split('.')[0];
        if (stem.Equals("CON", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("PRN", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("AUX", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("NUL", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return stem.Length == 4
            && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase))
            && stem[3] is >= '1' and <= '9';
    }

    private static async Task<ProjectUpgradeReceipt> PrepareItemAsync(
        BatchContext context,
        ProjectUpgradeItem project,
        ProjectUpgradeTestHooks? hooks,
        CancellationToken cancellationToken)
    {
        var resolved = context.Items.Single(item => ReferenceEquals(item.Request, project));
        EnsureDestinationDirectory(context, resolved.DestinationPath);

        await using var source = new FileStream(
            resolved.SourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            CopyBufferBytes,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        if (!context.SourceIdentities.Add(GetFileIdentity(source, resolved.SourcePath)))
        {
            throw Failure(ProjectUpgradeFailureCodes.AddressInvalid, "The project upgrade batch repeats an aliased source package.");
        }

        var firstSourceHash = await HashAsync(source, cancellationToken).ConfigureAwait(false);
        if (!firstSourceHash.Equals(project.SourceSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw Failure(ProjectUpgradeFailureCodes.SourceAddressMismatch, "An addressed source project does not match its SHA-256.");
        }

        var manifest = await OcfprojPackageValidator.ReadRoutingManifestAsync(source, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(manifest.EngineVersion, project.SourceEngineVersion, StringComparison.Ordinal)
            || !string.Equals(manifest.SchemaVersion, project.SourceSchemaVersion, StringComparison.Ordinal))
        {
            throw Failure(ProjectUpgradeFailureCodes.SourceVersionMismatch, "An addressed source project does not match its recorded versions.");
        }

        if (!string.Equals(manifest.SchemaVersion, EngineIdentity.ProjectSchemaVersion, StringComparison.Ordinal))
        {
            throw Failure(ProjectUpgradeFailureCodes.NoMigrationRoute, "No admitted project migration route exists for the addressed schema.");
        }

        var loadedSource = await OcfprojPackageValidator.ValidateAsync(source, cancellationToken).ConfigureAwait(false);
        if (!context.CandidateRecipes.Contains(new ProjectUpgradeRecipeIdentity(
                loadedSource.Manifest.RecipeId,
                loadedSource.Manifest.RecipeVersion)))
        {
            throw Failure(
                ProjectUpgradeFailureCodes.CandidateRecipeUnavailable,
                "The candidate build does not contain a project's pinned recipe identity.");
        }

        var packageTransformed = loadedSource.Validation is null;

        var destinationDirectory = Path.GetDirectoryName(resolved.DestinationPath)
            ?? throw Failure(ProjectUpgradeFailureCodes.AddressInvalid, "A candidate project has no containing library directory.");
        var temporaryPath = Path.Combine(
            destinationDirectory,
            $".{Path.GetFileName(resolved.DestinationPath)}.{Guid.NewGuid():N}.upgrade-partial");
        context.PartialPaths.Add(temporaryPath);

        string outputHash;
        await using (var output = new FileStream(
            temporaryPath,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            CopyBufferBytes,
            FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            if (hooks?.AfterPartialCreatedAsync is { } afterPartialCreated)
            {
                await afterPartialCreated(temporaryPath, cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            source.Position = 0;
            if (packageTransformed)
            {
                await WriteContextEnrichedCopyAsync(
                    source,
                    output,
                    loadedSource,
                    cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                OcfprojZipCanonicalizer.Canonicalize(output);
            }
            else
            {
                await source.CopyToAsync(output, CopyBufferBytes, cancellationToken).ConfigureAwait(false);
            }

            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            output.Flush(flushToDisk: true);

            outputHash = await HashAsync(output, cancellationToken).ConfigureAwait(false);
            if (!packageTransformed && !outputHash.Equals(firstSourceHash, StringComparison.Ordinal))
            {
                throw Failure(ProjectUpgradeFailureCodes.CopyInvalid, "The candidate project is not byte-identical to its compatible source.");
            }

            var loadedCopy = await OcfprojPackageValidator.ValidateAsync(output, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(loadedCopy.Manifest.EngineVersion, manifest.EngineVersion, StringComparison.Ordinal)
                || !string.Equals(loadedCopy.Manifest.SchemaVersion, manifest.SchemaVersion, StringComparison.Ordinal))
            {
                throw Failure(ProjectUpgradeFailureCodes.CopyInvalid, "The candidate project failed version validation.");
            }

            if (packageTransformed
                && (loadedCopy.Validation is null || loadedCopy.RenderProfile is null))
            {
                throw Failure(ProjectUpgradeFailureCodes.CopyInvalid, "The candidate project lacks its required compatibility context.");
            }

            var secondSourceHash = await HashAsync(source, cancellationToken).ConfigureAwait(false);
            if (!secondSourceHash.Equals(firstSourceHash, StringComparison.Ordinal))
            {
                throw Failure(ProjectUpgradeFailureCodes.SourceChanged, "The source project changed during preparation.");
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            File.Move(temporaryPath, resolved.DestinationPath, overwrite: false);
        }
        catch (IOException)
        {
            throw Failure(ProjectUpgradeFailureCodes.DestinationConflict, "A candidate project address became occupied before commit.");
        }

        context.PartialPaths.Remove(temporaryPath);
        context.CompletedPaths.Add(resolved.DestinationPath);
        context.CompletedRelativePaths.Add(resolved.DestinationRelativePath);

        return new ProjectUpgradeReceipt(
            manifest.EngineVersion,
            manifest.SchemaVersion,
            firstSourceHash,
            EngineIdentity.EngineVersion,
            EngineIdentity.ProjectSchemaVersion,
            outputHash,
            PackageTransformed: packageTransformed);
    }

    private static async Task WriteContextEnrichedCopyAsync(
        Stream source,
        Stream output,
        LoadedProject loaded,
        CancellationToken cancellationToken)
    {
        var digest = ArtifactDocumentFingerprint.Compute(loaded.Document);
        var validation = new ProjectValidationEnvelope(
            ProjectValidationEnvelope.CurrentSchemaVersion,
            ProjectValidationEnvelope.ExactApprovedDocumentKind,
            loaded.Manifest.RecipeId,
            loaded.Manifest.RecipeVersion,
            loaded.Manifest.DataLane,
            loaded.Manifest.Purpose,
            digest,
            []);
        var profile = new ProjectRenderProfile(
            ProjectRenderProfile.CurrentSchemaVersion,
            digest,
            RenderAudience.Learner,
            100,
            TargetLanguageFirst: false);

        source.Position = 0;
        output.Position = 0;
        using var inputArchive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);
        using var outputArchive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        DateTimeOffset? contextStamp = null;
        foreach (var entry in inputArchive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var copy = OcfprojZipCanonicalizer.CreateDataEntry(
                outputArchive,
                entry.FullName,
                CompressionLevel.Optimal);
            copy.LastWriteTime = entry.LastWriteTime;
            contextStamp ??= entry.LastWriteTime;
            await using var input = entry.Open();
            await using var destination = copy.Open();
            if (string.Equals(entry.FullName, "snapshot.html", StringComparison.Ordinal))
            {
                using var heldSnapshot = new MemoryStream();
                await input.CopyToAsync(heldSnapshot, CopyBufferBytes, cancellationToken).ConfigureAwait(false);
                var rewrittenSnapshot = PortableProjectSnapshot.RewriteVerifiedForCurrent(
                    loaded.Document,
                    loaded.Manifest.EngineVersion,
                    hasPersistedContext: loaded.RenderProfile is not null,
                    OcfprojPackageValidator.SnapshotRenderRequest(profile),
                    heldSnapshot.ToArray());
                await destination.WriteAsync(rewrittenSnapshot, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await input.CopyToAsync(destination, CopyBufferBytes, cancellationToken).ConfigureAwait(false);
            }
        }

        var stamp = contextStamp
            ?? throw Failure(ProjectUpgradeFailureCodes.CopyInvalid, "The source project has no deterministic archive stamp.");
        await WriteUpgradeEntryAsync(
            outputArchive,
            "validation.json",
            JsonSerializer.SerializeToUtf8Bytes(validation, StorageJson.Options),
            stamp,
            cancellationToken).ConfigureAwait(false);
        await WriteUpgradeEntryAsync(
            outputArchive,
            "render-profile.json",
            JsonSerializer.SerializeToUtf8Bytes(profile, StorageJson.Options),
            stamp,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteUpgradeEntryAsync(
        ZipArchive archive,
        string name,
        byte[] content,
        DateTimeOffset stamp,
        CancellationToken cancellationToken)
    {
        var entry = OcfprojZipCanonicalizer.CreateDataEntry(archive, name, CompressionLevel.Optimal);
        entry.LastWriteTime = stamp;
        await using var destination = entry.Open();
        await destination.WriteAsync(content, cancellationToken).ConfigureAwait(false);
    }

    private static FileStream AcquireBatchLock(RootDescriptor candidateRoot)
    {
        var lockPath = Path.Combine(candidateRoot.FullPath, BatchLockFileName);
        try
        {
            return new FileStream(
                lockPath,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.WriteThrough);
        }
        catch (IOException)
        {
            throw Failure(ProjectUpgradeFailureCodes.CandidateBusy, "The candidate library is already preparing another batch.");
        }
    }

    private static void EnsureDestinationDirectory(BatchContext context, string destinationPath)
    {
        var directory = Path.GetDirectoryName(destinationPath)
            ?? throw Failure(ProjectUpgradeFailureCodes.AddressInvalid, "A candidate project has no containing library directory.");
        if (string.Equals(directory, context.CandidateRoot.FullPath, PathComparison))
        {
            return;
        }

        var missing = new Stack<string>();
        var current = directory;
        while (!Directory.Exists(current))
        {
            if (!IsContainedBy(current, context.CandidateRoot.FullPath))
            {
                throw Failure(ProjectUpgradeFailureCodes.CandidateRootInvalid, "A candidate project escaped its library root.");
            }

            missing.Push(current);
            current = Path.GetDirectoryName(current)
                ?? throw Failure(ProjectUpgradeFailureCodes.CandidateRootInvalid, "A candidate project escaped its library root.");
        }

        try
        {
            Directory.CreateDirectory(directory);
        }
        finally
        {
            // Directory.CreateDirectory can create ancestors before a later
            // failure. Record every directory this batch found missing that now
            // exists so rollback can still remove a partially created tree.
            while (missing.Count > 0)
            {
                var created = missing.Pop();
                if (Directory.Exists(created))
                {
                    context.CreatedDirectories.Add(created);
                }
            }
        }

        EnsureNoReparseWithinRoot(directory, context.CandidateRoot.FullPath, includeFile: true, ProjectUpgradeFailureCodes.CandidateRootInvalid);
    }

    private static void VerifyCandidateContainsOnly(BatchContext context, IReadOnlyCollection<string> allowedRelativeFiles)
    {
        var allowedFiles = new HashSet<string>(allowedRelativeFiles, StringComparer.OrdinalIgnoreCase);
        var allowedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in allowedRelativeFiles)
        {
            var parent = Path.GetDirectoryName(file.Replace('/', Path.DirectorySeparatorChar));
            while (!string.IsNullOrEmpty(parent))
            {
                allowedDirectories.Add(parent.Replace(Path.DirectorySeparatorChar, '/'));
                parent = Path.GetDirectoryName(parent);
            }
        }

        var directories = new Stack<string>();
        directories.Push(context.CandidateRoot.FullPath);
        while (directories.Count > 0)
        {
            var directory = directories.Pop();
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                if (!IsContainedBy(entry, context.CandidateRoot.FullPath))
                {
                    throw Failure(ProjectUpgradeFailureCodes.CandidateRootInvalid, "A candidate project escaped its library root.");
                }

                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw Failure(ProjectUpgradeFailureCodes.CandidateRootInvalid, "A library address may not cross a reparse point.");
                }

                var relative = Path.GetRelativePath(context.CandidateRoot.FullPath, entry).Replace(Path.DirectorySeparatorChar, '/');
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    if (!allowedDirectories.Contains(relative))
                    {
                        throw Failure(ProjectUpgradeFailureCodes.CandidateContaminated, "The candidate library contains an unaddressed directory.");
                    }

                    directories.Push(entry);
                }
                else if (!allowedFiles.Contains(relative))
                {
                    throw Failure(ProjectUpgradeFailureCodes.CandidateContaminated, "The candidate library contains an unaddressed file.");
                }
            }
        }
    }

    private static int CleanupBatch(BatchContext context, ProjectUpgradeTestHooks? hooks)
    {
        var files = context.PartialPaths
            .Concat(context.CompletedPaths)
            .Append(context.LockPath)
            .Distinct(PathComparer)
            .ToList();
        _ = CleanupFiles(context, files, hooks);

        foreach (var directory in context.CreatedDirectories
                     .Distinct(PathComparer)
                     .OrderByDescending(path => path.Length))
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: false);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // The post-cleanup inventory below is authoritative. An
                // attempted delete can fail transiently and still leave no
                // residue, or fail once for both a file and its parent.
            }
        }

        return CountCandidateResidue(context.CandidateRoot.FullPath);
    }

    private static int CountCandidateResidue(string candidateRoot)
    {
        try
        {
            // Count bounded top-level quarantine units without following an
            // untrusted directory reparse point. Nonzero is the safety fact;
            // package-controlled names never leave this boundary.
            return Directory.EnumerateFileSystemEntries(candidateRoot)
                .Take(MaximumBatchProjects + 1)
                .Count();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return 1;
        }
    }

    private static int CleanupFiles(
        BatchContext context,
        IEnumerable<string> paths,
        ProjectUpgradeTestHooks? hooks)
    {
        var residueCount = 0;
        foreach (var path in paths.Distinct(PathComparer))
        {
            if (!IsContainedBy(path, context.CandidateRoot.FullPath))
            {
                residueCount++;
                continue;
            }

            try
            {
                if (File.Exists(path))
                {
                    var simulated = hooks?.CleanupDeleteFailure?.Invoke(path);
                    if (simulated is not null)
                    {
                        throw simulated;
                    }

                    File.Delete(path);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                residueCount++;
            }
        }

        return residueCount;
    }

    private static void EnsureNoReparseAncestors(string path, string code)
    {
        DirectoryInfo? current = new(path);
        while (current is not null)
        {
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw Failure(code, "A library root may not cross a reparse point.");
            }

            current = current.Parent;
        }
    }

    private static void EnsureNoReparseWithinRoot(string path, string root, bool includeFile, string code)
    {
        FileSystemInfo? current = includeFile
            ? File.Exists(path) ? new FileInfo(path) : new DirectoryInfo(path)
            : new DirectoryInfo(Path.GetDirectoryName(path)!);
        while (current is not null && IsContainedBy(current.FullName, root))
        {
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw Failure(code, "A library address may not cross a reparse point.");
            }

            current = current switch
            {
                FileInfo file => file.Directory,
                DirectoryInfo directory => directory.Parent,
                _ => null,
            };
        }
    }

    private static DirectoryIdentity GetDirectoryIdentity(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new DirectoryIdentity(0, 0, TrimDirectorySeparator(Path.GetFullPath(path)));
        }

        using var handle = CreateFile(
            path,
            desiredAccess: 0,
            FileShare.ReadWrite | FileShare.Delete,
            securityAttributes: 0,
            FileMode.Open,
            FileFlagBackupSemantics,
            templateFile: 0);
        if (handle.IsInvalid || !GetFileInformationByHandle(handle, out var information))
        {
            throw new IOException("Directory identity could not be established.");
        }

        var index = ((ulong)information.FileIndexHigh << 32) | information.FileIndexLow;
        return new DirectoryIdentity(information.VolumeSerialNumber, index, string.Empty);
    }

    private static FileIdentity GetFileIdentity(FileStream stream, string canonicalPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new FileIdentity(0, 0, Path.GetFullPath(canonicalPath));
        }

        if (!GetFileInformationByHandle(stream.SafeFileHandle, out var information))
        {
            throw new IOException("File identity could not be established.");
        }

        var index = ((ulong)information.FileIndexHigh << 32) | information.FileIndexLow;
        return new FileIdentity(information.VolumeSerialNumber, index, string.Empty);
    }

    private static bool IsContainedBy(string path, string root)
    {
        var fullPath = Path.GetFullPath(path);
        var fullRoot = TrimDirectorySeparator(Path.GetFullPath(root));
        return fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, PathComparison);
    }

    private static string TrimDirectorySeparator(string path)
        => Path.TrimEndingDirectorySeparator(path);

    private static async Task<string> HashAsync(Stream stream, CancellationToken cancellationToken)
    {
        stream.Position = 0;
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        stream.Position = 0;
        return Convert.ToHexString(hash);
    }

    private static ProjectUpgradeException ToContentFreeFailure(Exception exception)
        => exception switch
        {
            ProjectUpgradeException failure => failure,
            OperationCanceledException => Failure(ProjectUpgradeFailureCodes.Canceled, "Upgrade preparation was canceled."),
            OcfprojPackageException => Failure(ProjectUpgradeFailureCodes.PackageInvalid, "An addressed project package failed complete validation."),
            IOException or UnauthorizedAccessException => Failure(ProjectUpgradeFailureCodes.StorageFailure, "Upgrade preparation could not complete an approved storage operation."),
            _ => Failure(ProjectUpgradeFailureCodes.StorageFailure, "Upgrade preparation failed at a closed storage boundary."),
        };

    private static ProjectUpgradeException Failure(string code, string message)
        => new(code, message);

    private static bool RequiredText(string? value, int maximumLength)
        => !string.IsNullOrWhiteSpace(value)
            && value.Length <= maximumLength
            && value.IsNormalized(NormalizationForm.FormC)
            && !value.Any(char.IsControl);

    private static bool IsSha256(string value)
        => value.Length == 64 && value.All(Uri.IsHexDigit);

    private static StringComparison PathComparison
        => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static StringComparer PathComparer
        => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private const uint FileFlagBackupSemantics = 0x02000000;

    [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        FileShare shareMode,
        nint securityAttributes,
        FileMode creationDisposition,
        uint flagsAndAttributes,
        nint templateFile);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetFileInformationByHandle(
        SafeFileHandle file,
        out ByHandleFileInformation fileInformation);

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        internal uint FileAttributes;
        internal FileTime CreationTime;
        internal FileTime LastAccessTime;
        internal FileTime LastWriteTime;
        internal uint VolumeSerialNumber;
        internal uint FileSizeHigh;
        internal uint FileSizeLow;
        internal uint NumberOfLinks;
        internal uint FileIndexHigh;
        internal uint FileIndexLow;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        internal uint LowDateTime;
        internal uint HighDateTime;
    }

    private sealed record RootDescriptor(string FullPath, DirectoryIdentity Identity);

    private sealed record ResolvedUpgradeItem(
        ProjectUpgradeItem Request,
        string SourcePath,
        string DestinationPath,
        string DestinationRelativePath);

    private sealed class BatchContext(
        RootDescriptor sourceRoot,
        RootDescriptor candidateRoot,
        IReadOnlyList<ResolvedUpgradeItem> items,
        HashSet<ProjectUpgradeRecipeIdentity> candidateRecipes)
    {
        internal RootDescriptor SourceRoot { get; } = sourceRoot;

        internal RootDescriptor CandidateRoot { get; } = candidateRoot;

        internal IReadOnlyList<ResolvedUpgradeItem> Items { get; } = items;

        internal HashSet<ProjectUpgradeRecipeIdentity> CandidateRecipes { get; } = candidateRecipes;

        internal string LockPath => Path.Combine(CandidateRoot.FullPath, BatchLockFileName);

        internal List<string> PartialPaths { get; } = [];

        internal List<string> CompletedPaths { get; } = [];

        internal List<string> CompletedRelativePaths { get; } = [];

        internal List<string> CreatedDirectories { get; } = [];

        internal HashSet<FileIdentity> SourceIdentities { get; } = [];

        internal bool OwnsBatchLock { get; set; }
    }

    private readonly record struct DirectoryIdentity(uint Volume, ulong Index, string CanonicalPath);

    private readonly record struct FileIdentity(uint Volume, ulong Index, string CanonicalPath);
}

internal sealed record ProjectUpgradeTestHooks(
    Func<string, CancellationToken, Task>? AfterPartialCreatedAsync = null,
    Func<string, Exception?>? CleanupDeleteFailure = null);
