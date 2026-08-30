// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json;
using System.Xml.Linq;

namespace Foundry.Tests.Unit;

public sealed class CiSupplyChainContractTests
{
    private static readonly string Root = FindRepositoryRoot();

    [Fact]
    public void Repository_project_files_do_not_declare_a_weaker_build_contract()
    {
        var properties = XDocument.Load(Path.Combine(Root, "Directory.Build.props"));
        Assert.Equal("enable", Property(properties, "Nullable"));
        Assert.Equal("enable", Property(properties, "ImplicitUsings"));
        Assert.Equal("latest", Property(properties, "LangVersion"));
        Assert.Equal("true", Property(properties, "TreatWarningsAsErrors"));
        Assert.Equal("true", Property(properties, "Deterministic"));
        Assert.Equal("true", Property(properties, "ContinuousIntegrationBuild"));
        Assert.Equal("true", Property(properties, "DeterministicSourcePaths"));
        Assert.Equal("latest", Property(properties, "AnalysisLevel"));

        var allowedFrameworks = new HashSet<string>(StringComparer.Ordinal)
        {
            "net10.0",
            "net10.0-windows10.0.19041.0",
        };
        var weakened = new List<string>();
        var nestedBuildProperties = Directory.EnumerateFiles(Root, "Directory.Build.props", SearchOption.AllDirectories)
            .Where(path => !Path.GetFullPath(path).Equals(
                Path.GetFullPath(Path.Combine(Root, "Directory.Build.props")),
                StringComparison.OrdinalIgnoreCase))
            .Where(path => !PathSegments(path).Any(segment =>
                segment.Equals("bin", StringComparison.OrdinalIgnoreCase)
                || segment.Equals("obj", StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        foreach (var nestedPath in nestedBuildProperties)
        {
            var nested = XDocument.Load(nestedPath);
            if (!nested.Descendants("Import").Any(element =>
                    (element.Attribute("Project")?.Value ?? string.Empty)
                    .Contains("GetPathOfFileAbove('Directory.Build.props'", StringComparison.Ordinal)))
            {
                weakened.Add($"{Path.GetRelativePath(Root, nestedPath)} does not import the repository build contract");
            }
        }

        foreach (var projectPath in Directory.EnumerateFiles(Root, "*.csproj", SearchOption.AllDirectories)
                     .Where(path => !PathSegments(path).Any(segment =>
                         segment.Equals("bin", StringComparison.OrdinalIgnoreCase)
                         || segment.Equals("obj", StringComparison.OrdinalIgnoreCase))))
        {
            var project = XDocument.Load(projectPath);
            var framework = Property(project, "TargetFramework");
            if (!allowedFrameworks.Contains(framework))
            {
                weakened.Add($"{Path.GetRelativePath(Root, projectPath)} target framework '{framework}'");
            }

            foreach (var invariant in new[]
            {
                "TreatWarningsAsErrors",
                "Deterministic",
                "ContinuousIntegrationBuild",
                "DeterministicSourcePaths",
            })
            {
                var localValues = project.Descendants(invariant).Select(element => element.Value.Trim()).ToArray();
                if (localValues.Any(value => !string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)))
                {
                    weakened.Add($"{Path.GetRelativePath(Root, projectPath)} overrides {invariant}");
                }
            }

            var nullableValues = project.Descendants("Nullable").Select(element => element.Value.Trim()).ToArray();
            if (nullableValues.Any(value => !string.Equals(value, "enable", StringComparison.OrdinalIgnoreCase)))
            {
                weakened.Add($"{Path.GetRelativePath(Root, projectPath)} weakens Nullable");
            }

            foreach (var invariant in new[] { "ImplicitUsings", "LangVersion", "AnalysisLevel" })
            {
                var expected = invariant == "ImplicitUsings" ? "enable" : "latest";
                var localValues = project.Descendants(invariant).Select(element => element.Value.Trim()).ToArray();
                if (localValues.Any(value => !string.Equals(value, expected, StringComparison.OrdinalIgnoreCase)))
                {
                    weakened.Add($"{Path.GetRelativePath(Root, projectPath)} overrides {invariant}");
                }
            }
        }

        Assert.True(
            weakened.Count == 0,
            "Project files must not declare a weaker repository build contract:\n" + string.Join('\n', weakened));
    }

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
    public void Human_readable_notice_acknowledges_the_version_locked_dependency_inventory()
    {
        var packageReferences = Directory.EnumerateFiles(Root, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !PathSegments(path).Any(segment =>
                segment.Equals("bin", StringComparison.OrdinalIgnoreCase)
                || segment.Equals("obj", StringComparison.OrdinalIgnoreCase)))
            .SelectMany(path => XDocument.Load(path).Descendants("PackageReference"))
            .ToList();
        Assert.NotEmpty(packageReferences);

        var notice = File.ReadAllText(Path.Combine(Root, "NOTICE.md"));
        Assert.DoesNotContain("None yet", notice, StringComparison.Ordinal);
        Assert.Contains("[CI workflow](.github/workflows/ci.yml)", notice, StringComparison.Ordinal);
        Assert.Contains(
            "[release traceability matrix](docs/release/release-requirement-test-traceability.md#rights-and-openness)",
            notice,
            StringComparison.Ordinal);
        Assert.Contains("commit-scoped", notice, StringComparison.Ordinal);
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
        Assert.Contains("portable-samples:", workflow, StringComparison.Ordinal);
        Assert.Contains("needs: build-and-test", workflow, StringComparison.Ordinal);
        Assert.Contains("Get-ChildItem $rootA -Recurse -File", workflow, StringComparison.Ordinal);
        Assert.Contains("windows-sample-baseline", workflow, StringComparison.Ordinal);
        Assert.Contains(
            "actions/download-artifact@70fc10c6e5e1ce46ad2ea6f2b72d43f7d47b13c3 # v8.0.0",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains(
            "dotnet restore tools/SampleGenerator/Foundry.Tools.SampleGenerator.csproj --locked-mode --configfile NuGet.config",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains("samples-linux-a", workflow, StringComparison.Ordinal);
        Assert.Contains("cmp --silent", workflow, StringComparison.Ordinal);
        Assert.Contains("samples-windows/${file}", workflow, StringComparison.Ordinal);
        Assert.Contains("Windows and Linux sample files matched byte-for-byte", workflow, StringComparison.Ordinal);
        Assert.Contains("name: linux-sample-candidate", workflow, StringComparison.Ordinal);
        Assert.Contains("if: ${{ always() && hashFiles('samples-linux-a/**') != '' }}", workflow, StringComparison.Ordinal);
        Assert.Contains("test -x .githooks/pre-commit", workflow, StringComparison.Ordinal);
        Assert.Contains("GIT_INDEX_FILE=\"${PWD}\"", workflow, StringComparison.Ordinal);
        Assert.Contains("scratch/packet-synthetic.print.html", workflow, StringComparison.Ordinal);
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
        Assert.Contains("security-events: read", codeQl, StringComparison.Ordinal);
        Assert.DoesNotContain("security-events: write", codeQl, StringComparison.Ordinal);
        Assert.Contains("tools: linked", codeQl, StringComparison.Ordinal);
        Assert.Contains("upload: never", codeQl, StringComparison.Ordinal);
        Assert.Contains("upload-database: false", codeQl, StringComparison.Ordinal);

        var site = File.ReadAllText(Path.Combine(Root, ".github", "workflows", "site.yml"));
        Assert.Contains("dotnet restore tools/SiteGenerator/Foundry.Tools.SiteGenerator.csproj --locked-mode --configfile NuGet.config", site, StringComparison.Ordinal);
        Assert.Contains("dotnet run --project tools/SiteGenerator -c Release --no-restore", site, StringComparison.Ordinal);
        Assert.Contains("Actions UI, API, or GitHub CLI", site, StringComparison.Ordinal);
        Assert.Contains("does not prove transactional or atomic visibility", site, StringComparison.Ordinal);
    }

    private static string[] PathSegments(string path)
        => path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static string Property(XDocument document, string name)
        => document.Descendants(name).Select(element => element.Value.Trim()).LastOrDefault() ?? string.Empty;

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
