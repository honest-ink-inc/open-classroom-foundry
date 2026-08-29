using System.Xml.Linq;
using Xunit;

namespace Foundry.Tests.Unit;

/// <summary>
/// Executable form of the boundary rules in src/README.md, ADR-001, and the
/// Deterministic Press specification. These tests read the .csproj files
/// directly, so a forbidden ProjectReference fails the suite even before any
/// code exists to violate it at runtime.
/// </summary>
public class ArchitectureRulesTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "OpenClassroomFoundry.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository root (OpenClassroomFoundry.slnx) not found.");
    }

    private static List<string> ProjectReferences(string relativeCsprojPath)
    {
        var path = Path.Combine(RepoRoot(), relativeCsprojPath);
        var document = XDocument.Load(path);
        return document.Descendants("ProjectReference")
            .Select(r => (string?)r.Attribute("Include") ?? string.Empty)
            .ToList();
    }

    [Theory]
    [InlineData(@"src\Foundry.Modules.DeterministicPress\Foundry.Modules.DeterministicPress.csproj")]
    [InlineData(@"src\Foundry.Modules.BuiltIn\Foundry.Modules.BuiltIn.csproj")]
    public void Modules_may_not_reference_inference_or_infrastructure(string csproj)
    {
        var references = ProjectReferences(csproj);

        Assert.DoesNotContain(references, r => r.Contains("Inference", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, r => r.Contains("Infrastructure", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, r => r.Contains("App.WinForms", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Every_source_file_carries_its_spdx_license_identifier()
    {
        var root = RepoRoot();
        var files = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(root, "tools"), "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

        foreach (var file in files)
        {
            Assert.Contains("SPDX-License-Identifier: GPL-3.0-or-later", File.ReadLines(file).First(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Domain_references_no_projects()
    {
        Assert.Empty(ProjectReferences(@"src\Foundry.Domain\Foundry.Domain.csproj"));
    }

    [Fact]
    public void Contracts_references_only_domain()
    {
        var references = ProjectReferences(@"src\Foundry.Contracts\Foundry.Contracts.csproj");

        var reference = Assert.Single(references);
        Assert.Contains("Foundry.Domain", reference, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(@"src\Foundry.Domain\Foundry.Domain.csproj")]
    [InlineData(@"src\Foundry.Contracts\Foundry.Contracts.csproj")]
    [InlineData(@"src\Foundry.Application\Foundry.Application.csproj")]
    public void Engine_core_may_not_reference_ui_or_windows_infrastructure(string csproj)
    {
        var references = ProjectReferences(csproj);

        Assert.DoesNotContain(references, r => r.Contains("App.WinForms", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, r => r.Contains("Infrastructure.Windows", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Simulated_infrastructure_references_only_domain_and_contracts()
    {
        var references = ProjectReferences(@"src\Foundry.Infrastructure.Simulated\Foundry.Infrastructure.Simulated.csproj");

        Assert.Equal(2, references.Count);
        Assert.Contains(references, r => r.Contains("Foundry.Domain", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(references, r => r.Contains("Foundry.Contracts", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(@"src\Foundry.Inference.AzureOpenAI\Foundry.Inference.AzureOpenAI.csproj")]
    [InlineData(@"src\Foundry.Inference.Local\Foundry.Inference.Local.csproj")]
    [InlineData(@"src\Foundry.Inference.Synthetic\Foundry.Inference.Synthetic.csproj")]
    public void Inference_adapters_reference_only_the_abstractions(string csproj)
    {
        var reference = Assert.Single(ProjectReferences(csproj));
        Assert.Contains("Foundry.Inference.Abstractions", reference, StringComparison.OrdinalIgnoreCase);
    }
}
