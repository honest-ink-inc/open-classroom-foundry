using System.Reflection;
using Foundry.Contracts;
using Foundry.Domain;
using Xunit;

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
}
