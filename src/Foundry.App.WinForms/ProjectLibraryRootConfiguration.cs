// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Domain;

namespace Foundry.App.WinForms;

/// <summary>
/// Validates the managed, version-addressed project-library boundary before a
/// production form can save or open a project. The UIA-only root switch remains
/// a separate disposable-test seam in <see cref="UiaHarness"/>.
/// </summary>
public static class ProjectLibraryRootConfiguration
{
    public const string Switch = "--project-library-root";

    public static bool ApplyIfPresent(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Any(value => value.StartsWith(Switch + "=", StringComparison.Ordinal)))
        {
            throw Failure(
                ProjectLibraryRootFailureCodes.SwitchInvalid,
                UiStrings.WithoutMnemonic(UiStrings.ProjectLibraryRootSwitchInvalid));
        }

        var matches = args
            .Select((value, index) => (value, index))
            .Where(pair => string.Equals(pair.value, Switch, StringComparison.Ordinal))
            .Select(pair => pair.index)
            .ToArray();
        if (matches.Length == 0)
        {
            return false;
        }

        if (matches.Length != 1
            || matches[0] + 1 >= args.Length
            || args[matches[0] + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw Failure(
                ProjectLibraryRootFailureCodes.SwitchInvalid,
                UiStrings.WithoutMnemonic(UiStrings.ProjectLibraryRootSwitchInvalid));
        }

        AppServices.LibraryRoot = ValidateProductionRoot(args[matches[0] + 1]);
        return true;
    }

    public static string ValidateProductionRoot(string root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root))
        {
            throw Failure(
                ProjectLibraryRootFailureCodes.RootInvalid,
                UiStrings.WithoutMnemonic(UiStrings.ProjectLibraryRootInvalid));
        }

        try
        {
            var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            if (!Directory.Exists(fullPath)
                || PathsEqual(fullPath, Path.GetPathRoot(fullPath) ?? string.Empty))
            {
                throw Failure(
                    ProjectLibraryRootFailureCodes.RootInvalid,
                    UiStrings.WithoutMnemonic(UiStrings.ProjectLibraryRootInvalid));
            }

            EnsureNoReparseAncestors(new DirectoryInfo(fullPath));
            if (!PathSegments(fullPath).Contains(EngineIdentity.EngineVersion, StringComparer.Ordinal))
            {
                throw Failure(
                    ProjectLibraryRootFailureCodes.VersionSegmentMissing,
                    UiStrings.WithoutMnemonic(UiStrings.ProjectLibraryRootVersionSegmentMissing));
            }

            return fullPath;
        }
        catch (ProjectLibraryRootException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException
                                             or UnauthorizedAccessException
                                             or ArgumentException
                                             or NotSupportedException)
        {
            throw Failure(
                ProjectLibraryRootFailureCodes.RootInvalid,
                UiStrings.WithoutMnemonic(UiStrings.ProjectLibraryRootInvalid));
        }
    }

    internal static string ResolveProjectFileInsideConfiguredRoot(string path)
    {
        try
        {
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppServices.LibraryRoot));
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath)
                || !Path.GetExtension(fullPath).Equals(".ocfproj", StringComparison.OrdinalIgnoreCase)
                || !fullPath.StartsWith(root + Path.DirectorySeparatorChar, PathComparison))
            {
                throw new InvalidOperationException(
                    UiStrings.WithoutMnemonic(UiStrings.ProjectOutsideConfiguredLibrary));
            }

            EnsureNoReparseWithinRoot(new FileInfo(fullPath), root);
            return fullPath;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException
                                             or UnauthorizedAccessException
                                             or ArgumentException
                                             or NotSupportedException)
        {
            throw new InvalidOperationException(
                UiStrings.WithoutMnemonic(UiStrings.ProjectOutsideConfiguredLibrary));
        }
    }

    private static IEnumerable<string> PathSegments(string path)
    {
        var current = new DirectoryInfo(path);
        while (current is not null)
        {
            if (!string.IsNullOrEmpty(current.Name))
            {
                yield return current.Name;
            }

            current = current.Parent;
        }
    }

    private static void EnsureNoReparseAncestors(DirectoryInfo directory)
    {
        DirectoryInfo? current = directory;
        while (current is not null)
        {
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw Failure(
                    ProjectLibraryRootFailureCodes.RootInvalid,
                    UiStrings.WithoutMnemonic(UiStrings.ProjectLibraryRootInvalid));
            }

            current = current.Parent;
        }
    }

    private static void EnsureNoReparseWithinRoot(FileSystemInfo item, string root)
    {
        FileSystemInfo? current = item;
        while (current is not null)
        {
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException(
                    UiStrings.WithoutMnemonic(UiStrings.ProjectOutsideConfiguredLibrary));
            }

            if (PathsEqual(current.FullName, root))
            {
                return;
            }

            current = current switch
            {
                FileInfo file => file.Directory,
                DirectoryInfo directory => directory.Parent,
                _ => null,
            };
        }

        throw new InvalidOperationException(
            UiStrings.WithoutMnemonic(UiStrings.ProjectOutsideConfiguredLibrary));
    }

    private static bool PathsEqual(string left, string right)
        => string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            PathComparison);

    private static StringComparison PathComparison
        => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static ProjectLibraryRootException Failure(string code, string message)
        => new(code, message);
}

public static class ProjectLibraryRootFailureCodes
{
    public const string SwitchInvalid = "project-library-root.switch-invalid";
    public const string RootInvalid = "project-library-root.invalid";
    public const string VersionSegmentMissing = "project-library-root.version-segment-missing";
}

public sealed class ProjectLibraryRootException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
