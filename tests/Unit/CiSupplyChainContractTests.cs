// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json;
using System.Xml.Linq;

namespace Foundry.Tests.Unit;

public sealed class CiSupplyChainContractTests
{
    private static readonly string Root = FindRepositoryRoot();

    [Fact]
    public void NuGet_resolution_is_one_source_mapped_and_every_project_has_a_lock()
    {
        var configuration = XDocument.Load(Path.Combine(Root, "NuGet.config"));
        var root = Assert.IsType<XElement>(configuration.Root);
        var packageSources = Assert.Single(root.Elements("packageSources"));
        Assert.Single(packageSources.Elements("clear"));
        var source = Assert.Single(packageSources.Elements("add"));
        Assert.Equal("nuget.org", (string?)source.Attribute("key"));
        Assert.Equal("https://api.nuget.org/v3/index.json", (string?)source.Attribute("value"));

        var sourceMapping = Assert.Single(root.Elements("packageSourceMapping"));
        var mappedSource = Assert.Single(sourceMapping.Elements("packageSource"));
        Assert.Equal("nuget.org", (string?)mappedSource.Attribute("key"));
        var pattern = Assert.Single(mappedSource.Elements("package"));
        Assert.Equal("*", (string?)pattern.Attribute("pattern"));

        var projects = Directory.EnumerateFiles(Root, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !PathSegments(path).Any(segment =>
                segment.Equals("bin", StringComparison.OrdinalIgnoreCase)
                || segment.Equals("obj", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        Assert.NotEmpty(projects);

        var missingOrMalformed = new List<string>();
        foreach (var project in projects)
        {
            var lockPath = Path.Combine(Path.GetDirectoryName(project)!, "packages.lock.json");
            if (!File.Exists(lockPath))
            {
                missingOrMalformed.Add(Path.GetRelativePath(Root, lockPath));
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(lockPath));
                var lockRoot = document.RootElement;
                if (lockRoot.GetProperty("version").GetInt32() != 1
                    || lockRoot.GetProperty("dependencies").ValueKind != JsonValueKind.Object)
                {
                    missingOrMalformed.Add(Path.GetRelativePath(Root, lockPath));
                }
            }
            catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                missingOrMalformed.Add(Path.GetRelativePath(Root, lockPath));
            }
        }

        Assert.True(
            missingOrMalformed.Count == 0,
            "Every project must retain a version-1 packages.lock.json beside the project: "
            + string.Join(", ", missingOrMalformed));

        var appDirectory = Path.Combine(Root, "src", "Foundry.App.WinForms");
        using var portableAppLock = JsonDocument.Parse(File.ReadAllText(Path.Combine(appDirectory, "packages.lock.json")));
        Assert.DoesNotContain(
            portableAppLock.RootElement.GetProperty("dependencies").EnumerateObject(),
            group => group.Name.EndsWith("/win-x64", StringComparison.Ordinal));

        var runtimeLockPath = Path.Combine(appDirectory, "packages.win-x64.lock.json");
        using var runtimeLock = JsonDocument.Parse(File.ReadAllText(runtimeLockPath));
        Assert.Contains(
            runtimeLock.RootElement.GetProperty("dependencies").EnumerateObject(),
            group => group.Name.EndsWith("/win-x64", StringComparison.Ordinal));
    }

    [Fact]
    public void Ci_retains_distinct_repository_and_application_dependency_evidence()
    {
        var workflow = File.ReadAllText(Path.Combine(Root, ".github", "workflows", "ci.yml"));

        Assert.Contains("dotnet restore OpenClassroomFoundry.slnx --locked-mode --configfile NuGet.config", workflow, StringComparison.Ordinal);
        Assert.Contains("--runtime win-x64 --locked-mode", workflow, StringComparison.Ordinal);
        Assert.Contains("-p:NuGetLockFilePath=packages.win-x64.lock.json", workflow, StringComparison.Ordinal);
        Assert.Contains(
            "dotnet format OpenClassroomFoundry.slnx --no-restore --verify-no-changes",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains("repository-build-dependency-inventory.json", workflow, StringComparison.Ordinal);
        Assert.Contains("repository-build-sbom.cdx.json", workflow, StringComparison.Ordinal);
        Assert.Contains("distributable-app-dependency-inventory.json", workflow, StringComparison.Ordinal);
        Assert.Contains("foundry-app-runtime-dependency-sbom.cdx.json", workflow, StringComparison.Ordinal);
        Assert.Contains("restored Foundry.App.WinForms win-x64 NuGet dependency closure", workflow, StringComparison.Ordinal);
        Assert.Contains("not-claimed:", workflow, StringComparison.Ordinal);
        Assert.Contains("this workflow neither configures nor proves GitHub branch protection", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void Gitleaks_archive_uses_a_repository_pinned_digest_not_a_same_origin_checksum()
    {
        var workflow = File.ReadAllText(Path.Combine(Root, ".github", "workflows", "ci.yml"));

        Assert.Contains(
            "EXPECTED_SHA256=\"551f6fc83ea457d62a0d98237cbad105af8d557003051f41f3e7ca7b3f2470eb\"",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains("printf '%s  %s\\n' \"${EXPECTED_SHA256}\" \"${ASSET}\" | sha256sum -c -", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("checksums.txt", workflow, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Auxiliary_workflows_restore_locked_and_state_their_limits()
    {
        var codeQl = File.ReadAllText(Path.Combine(Root, ".github", "workflows", "codeql.yml"));
        Assert.Contains("dotnet restore OpenClassroomFoundry.slnx --locked-mode --configfile NuGet.config", codeQl, StringComparison.Ordinal);
        Assert.Contains("A green run", codeQl, StringComparison.Ordinal);
        Assert.Contains("cannot prove or configure branch protection", codeQl, StringComparison.Ordinal);

        var site = File.ReadAllText(Path.Combine(Root, ".github", "workflows", "site.yml"));
        Assert.Contains("dotnet restore tools/SiteGenerator/Foundry.Tools.SiteGenerator.csproj --locked-mode --configfile NuGet.config", site, StringComparison.Ordinal);
        Assert.Contains("dotnet run --project tools/SiteGenerator -c Release --no-restore", site, StringComparison.Ordinal);
        Assert.Contains("Actions UI, API, or GitHub CLI", site, StringComparison.Ordinal);
        Assert.Contains("does not prove transactional or atomic visibility", site, StringComparison.Ordinal);
    }

    private static string[] PathSegments(string path)
        => path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpenClassroomFoundry.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate the repository root for CI supply-chain contract tests.");
    }
}
