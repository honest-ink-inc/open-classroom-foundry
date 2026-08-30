// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Foundry.Domain;
using Foundry.Storage;

namespace Foundry.Tools.ProjectUpgradeHost;

/// <summary>
/// A deliberately narrow operator host for ADR-007 project preparation. It
/// reviews one exact plan or prepares that exact, SHA-256-confirmed plan. It
/// never discovers a package, build, or destination; and it never installs,
/// launches, versions, signs, distributes, deletes, or publishes software.
/// </summary>
public static class ProjectUpgradeOperatorHost
{
    public const string PlanSchemaVersion = "1";

    public static async Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        try
        {
            var command = ParseCommand(args);
            var heldPlan = await ReadExactPlanAsync(command.PlanPath, cancellationToken).ConfigureAwait(false);
            var request = ParsePlan(heldPlan.Bytes);
            ValidateClosedPlan(request);

            if (command.Kind == OperatorCommandKind.Review)
            {
                WritePlanReview(output, heldPlan.Sha256, request.Projects.Count);
                return 0;
            }

            if (!string.Equals(command.ConfirmedSha256, heldPlan.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw OperatorFailure(
                    OperatorUpgradeFailureCodes.PlanNotConfirmed,
                    "The exact upgrade plan SHA-256 was not confirmed.");
            }

            // Exactly one call crosses the preparation boundary. The request
            // preserves the plan's closed array order; no retry or inferred
            // per-project call exists in this host.
            var receipt = await OcfprojUpgradeService.PrepareCompatibleBatchAsync(
                request,
                cancellationToken).ConfigureAwait(false);
            WriteReceipt(output, heldPlan.Sha256, receipt);
            return 0;
        }
        catch (OperatorUpgradeException refusal)
        {
            WriteFailure(error, refusal.Code, refusal.Message, null, 0);
            return 2;
        }
        catch (ProjectUpgradeException refusal)
        {
            WriteFailure(
                error,
                refusal.Code,
                refusal.Message,
                refusal.PrimaryCode,
                refusal.CleanupResidueCount);
            return 3;
        }
        catch (OperationCanceledException)
        {
            WriteFailure(
                error,
                ProjectUpgradeFailureCodes.Canceled,
                "Upgrade preparation was canceled.",
                null,
                0);
            return 3;
        }
        catch
        {
            // Raw JSON, filesystem, ZIP, and package exceptions can carry a
            // plan path, package name, or authored content. Nothing crosses
            // this boundary except this fixed statement.
            WriteFailure(
                error,
                OperatorUpgradeFailureCodes.HostFailure,
                "The operator host failed at a closed boundary.",
                null,
                0);
            return 3;
        }
    }

    private static OperatorCommand ParseCommand(string[] args)
    {
        if (args.Length == 3
            && string.Equals(args[0], "review", StringComparison.Ordinal)
            && string.Equals(args[1], "--plan", StringComparison.Ordinal))
        {
            return new OperatorCommand(OperatorCommandKind.Review, args[2], null);
        }

        if (args.Length == 5
            && string.Equals(args[0], "prepare", StringComparison.Ordinal)
            && string.Equals(args[1], "--plan", StringComparison.Ordinal)
            && string.Equals(args[3], "--confirm-plan-sha256", StringComparison.Ordinal)
            && IsSha256(args[4]))
        {
            return new OperatorCommand(OperatorCommandKind.Prepare, args[2], args[4]);
        }

        throw OperatorFailure(
            OperatorUpgradeFailureCodes.UsageInvalid,
            "Use 'review --plan <absolute-plan-file>' or 'prepare --plan <absolute-plan-file> --confirm-plan-sha256 <SHA-256>'.");
    }

    private static async Task<HeldPlan> ReadExactPlanAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path)
            || !Path.IsPathFullyQualified(path)
            || !File.Exists(path))
        {
            throw OperatorFailure(
                OperatorUpgradeFailureCodes.PlanFileInvalid,
                "The exact upgrade plan file is invalid.");
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            EnsureNoReparseAncestors(new FileInfo(fullPath), planFile: true);
            await using var stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (stream.Length is <= 0 or > 1024 * 1024)
            {
                throw OperatorFailure(
                    OperatorUpgradeFailureCodes.PlanFileInvalid,
                    "The exact upgrade plan file is invalid.");
            }

            var bytes = new byte[checked((int)stream.Length)];
            await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
            return new HeldPlan(bytes, Convert.ToHexString(SHA256.HashData(bytes)));
        }
        catch (OperatorUpgradeException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException
                                             or UnauthorizedAccessException
                                             or ArgumentException
                                             or NotSupportedException)
        {
            throw OperatorFailure(
                OperatorUpgradeFailureCodes.PlanFileInvalid,
                "The exact upgrade plan file is invalid.");
        }
    }

    private static ProjectUpgradeBatchRequest ParsePlan(byte[] bytes)
    {
        try
        {
            using var document = JsonDocument.Parse(
                bytes,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 8,
                });
            var root = document.RootElement;
            RequireObject(root, ["schemaVersion", "sourceLibraryRoot", "candidateLibraryRoot", "targetEngineVersion", "projects"]);
            if (!string.Equals(RequiredString(root, "schemaVersion"), PlanSchemaVersion, StringComparison.Ordinal))
            {
                throw new InvalidDataException();
            }

            var projectsElement = RequiredProperty(root, "projects");
            if (projectsElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException();
            }

            var projects = new List<ProjectUpgradeItem>();
            foreach (var item in projectsElement.EnumerateArray())
            {
                RequireObject(
                    item,
                    ["sourceRelativePath", "destinationRelativePath", "sourceEngineVersion", "sourceSchemaVersion", "sourceSha256"]);
                projects.Add(new ProjectUpgradeItem(
                    RequiredString(item, "sourceRelativePath"),
                    RequiredString(item, "destinationRelativePath"),
                    RequiredString(item, "sourceEngineVersion"),
                    RequiredString(item, "sourceSchemaVersion"),
                    RequiredString(item, "sourceSha256")));
            }

            return new ProjectUpgradeBatchRequest(
                RequiredString(root, "sourceLibraryRoot"),
                RequiredString(root, "candidateLibraryRoot"),
                RequiredString(root, "targetEngineVersion"),
                projects);
        }
        catch (Exception exception) when (exception is JsonException
                                             or InvalidDataException
                                             or InvalidOperationException)
        {
            throw OperatorFailure(
                OperatorUpgradeFailureCodes.PlanInvalid,
                "The upgrade plan is invalid.");
        }
    }

    private static void ValidateClosedPlan(ProjectUpgradeBatchRequest request)
    {
        if (!string.Equals(request.TargetEngineVersion, EngineIdentity.EngineVersion, StringComparison.Ordinal))
        {
            throw OperatorFailure(
                OperatorUpgradeFailureCodes.TargetVersionMismatch,
                "The plan target does not match this engine build.");
        }

        if (request.Projects.Count is 0 or > 512)
        {
            throw OperatorFailure(
                OperatorUpgradeFailureCodes.PlanInvalid,
                "The upgrade plan is invalid.");
        }

        var sourceRoot = ValidateExistingRoot(request.SourceLibraryRoot);
        var candidateRoot = ValidateExistingRoot(request.CandidateLibraryRoot);
        if (PathsEqual(sourceRoot, candidateRoot)
            || IsContainedBy(sourceRoot, candidateRoot)
            || IsContainedBy(candidateRoot, sourceRoot))
        {
            throw OperatorFailure(
                OperatorUpgradeFailureCodes.RootInvalid,
                "The plan library roots are invalid.");
        }

        if (Directory.EnumerateFileSystemEntries(candidateRoot).Any())
        {
            throw OperatorFailure(
                OperatorUpgradeFailureCodes.CandidateNotEmpty,
                "The candidate library must be empty before preparation begins.");
        }

        var addressed = new HashSet<string>(PathComparer);
        var destinations = new HashSet<string>(PathComparer);
        foreach (var project in request.Projects)
        {
            if (!RequiredText(project.SourceRelativePath, 512)
                || !RequiredText(project.DestinationRelativePath, 512)
                || !IsVersionToken(project.SourceEngineVersion)
                || !IsSchemaToken(project.SourceSchemaVersion)
                || !IsSha256(project.SourceSha256))
            {
                throw OperatorFailure(
                    OperatorUpgradeFailureCodes.PlanInvalid,
                    "The upgrade plan is invalid.");
            }

            var sourceRelative = CanonicalPackageRelativePath(project.SourceRelativePath);
            var destinationRelative = CanonicalPackageRelativePath(project.DestinationRelativePath);
            if (!addressed.Add(sourceRelative) || !destinations.Add(destinationRelative))
            {
                throw OperatorFailure(
                    OperatorUpgradeFailureCodes.PlanInvalid,
                    "The upgrade plan is invalid.");
            }
        }

        var inventory = EnumeratePackageInventory(sourceRoot);
        if (!addressed.SetEquals(inventory))
        {
            throw OperatorFailure(
                OperatorUpgradeFailureCodes.InventoryNotClosed,
                "The plan does not contain the closed source package inventory.");
        }
    }

    private static HashSet<string> EnumeratePackageInventory(string sourceRoot)
    {
        var result = new HashSet<string>(PathComparer);
        var pending = new Stack<string>();
        pending.Push(sourceRoot);
        try
        {
            while (pending.Count > 0)
            {
                var directory = pending.Pop();
                foreach (var path in Directory.EnumerateFileSystemEntries(directory))
                {
                    var attributes = File.GetAttributes(path);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        throw OperatorFailure(
                            OperatorUpgradeFailureCodes.RootInvalid,
                            "The plan library roots are invalid.");
                    }

                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        pending.Push(path);
                    }
                    else if (Path.GetExtension(path).Equals(".ocfproj", StringComparison.OrdinalIgnoreCase))
                    {
                        result.Add(Path.GetRelativePath(sourceRoot, path).Replace(Path.DirectorySeparatorChar, '/'));
                    }
                }
            }
        }
        catch (OperatorUpgradeException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw OperatorFailure(
                OperatorUpgradeFailureCodes.RootInvalid,
                "The plan library roots are invalid.");
        }

        return result;
    }

    private static string ValidateExistingRoot(string root)
    {
        if (!RequiredText(root, 1024) || !Path.IsPathFullyQualified(root))
        {
            throw OperatorFailure(
                OperatorUpgradeFailureCodes.RootInvalid,
                "The plan library roots are invalid.");
        }

        try
        {
            var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            if (!Directory.Exists(fullPath)
                || PathsEqual(fullPath, Path.GetPathRoot(fullPath) ?? string.Empty))
            {
                throw OperatorFailure(
                    OperatorUpgradeFailureCodes.RootInvalid,
                    "The plan library roots are invalid.");
            }

            EnsureNoReparseAncestors(new DirectoryInfo(fullPath), planFile: false);
            return fullPath;
        }
        catch (OperatorUpgradeException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException
                                             or UnauthorizedAccessException
                                             or ArgumentException
                                             or NotSupportedException)
        {
            throw OperatorFailure(
                OperatorUpgradeFailureCodes.RootInvalid,
                "The plan library roots are invalid.");
        }
    }

    private static string CanonicalPackageRelativePath(string relativePath)
    {
        try
        {
            if (Path.IsPathFullyQualified(relativePath)
                || relativePath.Contains('*')
                || relativePath.Contains('?')
                || relativePath.Contains('\\'))
            {
                throw new InvalidDataException();
            }

            var segments = relativePath.Split('/', StringSplitOptions.None);
            if (segments.Length == 0
                || segments.Any(segment => string.IsNullOrWhiteSpace(segment)
                                           || segment is "." or ".."
                                           || segment != Path.GetFileName(segment)))
            {
                throw new InvalidDataException();
            }

            var canonical = string.Join('/', segments);
            if (!Path.GetExtension(canonical).Equals(".ocfproj", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException();
            }

            return canonical;
        }
        catch (Exception exception) when (exception is ArgumentException
                                             or NotSupportedException
                                             or InvalidDataException)
        {
            throw OperatorFailure(
                OperatorUpgradeFailureCodes.PlanInvalid,
                "The upgrade plan is invalid.");
        }
    }

    private static void EnsureNoReparseAncestors(FileSystemInfo item, bool planFile)
    {
        FileSystemInfo? current = item;
        while (current is not null)
        {
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw OperatorFailure(
                    planFile ? OperatorUpgradeFailureCodes.PlanFileInvalid : OperatorUpgradeFailureCodes.RootInvalid,
                    planFile ? "The exact upgrade plan file is invalid." : "The plan library roots are invalid.");
            }

            current = current switch
            {
                FileInfo file => file.Directory,
                DirectoryInfo directory => directory.Parent,
                _ => null,
            };
        }
    }

    private static void RequireObject(JsonElement element, string[] expectedProperties)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException();
        }

        var actual = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!actual.Add(property.Name))
            {
                throw new InvalidDataException();
            }
        }

        if (actual.Count != expectedProperties.Length
            || expectedProperties.Any(expected => !actual.Contains(expected)))
        {
            throw new InvalidDataException();
        }
    }

    private static JsonElement RequiredProperty(JsonElement element, string name)
        => element.TryGetProperty(name, out var property)
            ? property
            : throw new InvalidDataException();

    private static string RequiredString(JsonElement element, string name)
    {
        var property = RequiredProperty(element, name);
        return property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? throw new InvalidDataException()
            : throw new InvalidDataException();
    }

    private static void WritePlanReview(TextWriter output, string planSha256, int projectCount)
    {
        output.WriteLine("Managed project-upgrade plan review");
        output.WriteLine($"Plan schema: {PlanSchemaVersion}");
        output.WriteLine($"Target engine: {EngineIdentity.EngineVersion}");
        output.WriteLine($"Closed project count: {projectCount.ToString(CultureInfo.InvariantCulture)}");
        output.WriteLine($"Exact plan SHA-256: {planSha256}");
        output.WriteLine("No project path or project content is displayed. Preparation has not run.");
    }

    private static void WriteReceipt(
        TextWriter output,
        string planSha256,
        ProjectUpgradeBatchReceipt receipt)
    {
        output.WriteLine("Managed project-upgrade preparation receipt");
        output.WriteLine($"Exact plan SHA-256: {planSha256}");
        output.WriteLine($"Target engine: {receipt.TargetEngineVersion}");
        output.WriteLine($"Target project schema: {receipt.TargetSchemaVersion}");
        output.WriteLine($"Prepared project count: {receipt.Projects.Count.ToString(CultureInfo.InvariantCulture)}");
        for (var index = 0; index < receipt.Projects.Count; index++)
        {
            var project = receipt.Projects[index];
            output.WriteLine($"Project ordinal: {(index + 1).ToString(CultureInfo.InvariantCulture)}");
            output.WriteLine($"Source engine: {project.SourceEngineVersion}");
            output.WriteLine($"Source project schema: {project.SourceSchemaVersion}");
            output.WriteLine($"Source SHA-256: {project.SourceSha256}");
            output.WriteLine($"Output SHA-256: {project.OutputSha256}");
            output.WriteLine($"Package transformed: {project.PackageTransformed.ToString(CultureInfo.InvariantCulture)}");
        }

        output.WriteLine("No project path or project content is displayed.");
    }

    private static void WriteFailure(
        TextWriter error,
        string code,
        string message,
        string? primaryCode,
        int cleanupResidueCount)
    {
        error.WriteLine("Managed project-upgrade preparation refused");
        error.WriteLine($"Code: {code}");
        error.WriteLine($"Message: {message}");
        if (primaryCode is not null)
        {
            error.WriteLine($"Primary code: {primaryCode}");
        }

        if (cleanupResidueCount > 0)
        {
            error.WriteLine($"Cleanup residue count: {cleanupResidueCount.ToString(CultureInfo.InvariantCulture)}");
        }

        error.WriteLine("No project path or project content is displayed.");
    }

    private static bool RequiredText(string value, int maximumLength)
        => !string.IsNullOrWhiteSpace(value)
            && value.Length <= maximumLength
            && value.IsNormalized(NormalizationForm.FormC)
            && !value.Any(char.IsControl);

    private static bool IsSha256(string? value)
        => value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static bool IsVersionToken(string value)
        => RequiredText(value, 64)
            && value.All(character => character is >= '0' and <= '9'
                or >= 'A' and <= 'Z'
                or >= 'a' and <= 'z'
                or '.' or '-' or '+' or '_');

    private static bool IsSchemaToken(string value)
        => RequiredText(value, 32)
            && value.All(character => character is >= '0' and <= '9');

    private static bool PathsEqual(string left, string right)
        => string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            PathComparison);

    private static bool IsContainedBy(string path, string root)
    {
        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        return fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, PathComparison);
    }

    private static StringComparison PathComparison
        => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static StringComparer PathComparer
        => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static OperatorUpgradeException OperatorFailure(string code, string message)
        => new(code, message);

    private enum OperatorCommandKind
    {
        Review,
        Prepare,
    }

    private sealed record OperatorCommand(
        OperatorCommandKind Kind,
        string PlanPath,
        string? ConfirmedSha256);

    private sealed record HeldPlan(byte[] Bytes, string Sha256);
}

public static class OperatorUpgradeFailureCodes
{
    public const string UsageInvalid = "operator.usage-invalid";
    public const string PlanFileInvalid = "operator.plan-file-invalid";
    public const string PlanInvalid = "operator.plan-invalid";
    public const string PlanNotConfirmed = "operator.plan-not-confirmed";
    public const string TargetVersionMismatch = "operator.target-version-mismatch";
    public const string RootInvalid = "operator.root-invalid";
    public const string CandidateNotEmpty = "operator.candidate-not-empty";
    public const string InventoryNotClosed = "operator.inventory-not-closed";
    public const string HostFailure = "operator.host-failure";
}

public sealed class OperatorUpgradeException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
