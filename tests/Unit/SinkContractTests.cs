using System.Reflection;
using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Tests.Unit;

/// <summary>
/// Executable ADR-004: the render, export, print, and save-as-final seams accept
/// ApprovedArtifact and can never grow a DraftArtifact overload unnoticed.
/// </summary>
public class SinkContractTests
{
    [Theory]
    [InlineData(typeof(IRenderer))]
    [InlineData(typeof(IExporter))]
    [InlineData(typeof(IPrinter))]
    [InlineData(typeof(IProjectStore))]
    public void Sinks_accept_only_approved_artifacts(Type sinkType)
    {
        var parameters = sinkType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .SelectMany(m => m.GetParameters())
            .ToList();

        Assert.DoesNotContain(parameters, p => p.ParameterType == typeof(DraftArtifact));
        Assert.Contains(parameters, p => p.ParameterType == typeof(ApprovedArtifact));
    }

    [Fact]
    public void Approved_artifacts_have_no_public_constructor()
    {
        Assert.Empty(typeof(ApprovedArtifact).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void Unapproved_preview_capability_is_confined_to_the_review_surface()
    {
        var appRoot = Path.Combine(RepoRoot(), "src", "Foundry.App.WinForms");
        var callers = Directory.EnumerateFiles(appRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(
                    $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal)
                && !path.Contains(
                    $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal)
                && File.ReadAllText(path).Contains(
                    "UnapprovedDraftPreviewFactory",
                    StringComparison.Ordinal))
            .Select(path => Path.GetFileName(path) ?? string.Empty)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["ReviewForm.cs"], callers);
        Assert.DoesNotContain(
            Directory.EnumerateFiles(appRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains(
                    $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal)
                    && !path.Contains(
                        $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                        StringComparison.Ordinal)),
            path => File.ReadAllText(path).Contains(
                "RenderSemanticDerivative",
                StringComparison.Ordinal));
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "OpenClassroomFoundry.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException();
    }
}
