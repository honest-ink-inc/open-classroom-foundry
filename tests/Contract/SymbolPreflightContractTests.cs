// SPDX-License-Identifier: GPL-3.0-or-later
using System.Reflection;
using Foundry.Application;
using Foundry.Contracts;

namespace Foundry.Tests.Contract;

public class SymbolPreflightContractTests
{
    [Fact]
    public void A_symbol_shelf_capability_cannot_be_minted_or_rewritten_by_a_public_caller()
    {
        var capability = typeof(SymbolSubmission);
        var publicProperties = capability.GetProperties(BindingFlags.Instance | BindingFlags.Public);

        Assert.Empty(capability.GetConstructors(BindingFlags.Instance | BindingFlags.Public));
        Assert.DoesNotContain(publicProperties, property => property.Name == "Content");
        Assert.All(publicProperties, property => Assert.False(property.SetMethod?.IsPublic ?? false));

        var preflight = typeof(CaptureSession).Assembly.GetType(
            "Foundry.Application.SymbolPreflight",
            throwOnError: true)!;
        Assert.False(preflight.IsPublic || preflight.IsNestedPublic);
    }

    [Fact]
    public void The_application_exports_no_alternate_public_symbol_submission_factory()
    {
        var exportedMethods = typeof(CaptureSession).Assembly
            .GetExportedTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance));

        Assert.DoesNotContain(exportedMethods, method =>
            method.ReturnType == typeof(SymbolSubmission)
            || method.ReturnType == typeof(Task<SymbolSubmission>));
    }
}
