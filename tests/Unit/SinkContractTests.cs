using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;
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
    public void Approved_artifacts_have_exactly_one_private_constructor_and_one_validating_factory()
    {
        Assert.Empty(typeof(ApprovedArtifact).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        var constructor = Assert.Single(
            typeof(ApprovedArtifact).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance));
        Assert.True(constructor.IsPrivate);

        var factory = Assert.Single(
            typeof(ApprovedArtifact).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static),
            method => method.ReturnType == typeof(ApprovedArtifact));
        Assert.Equal("ApproveThroughGate", factory.Name);
        Assert.False(factory.IsPublic);
        Assert.False(typeof(ApprovalGate).IsPublic);
    }

    [Theory]
    [InlineData(typeof(IRenderer))]
    [InlineData(typeof(IExporter))]
    [InlineData(typeof(IPrinter))]
    public void Output_sinks_carry_the_explicit_amber_authorization_capability(Type sinkType)
    {
        var sinkMethod = Assert.Single(sinkType.GetMethods(BindingFlags.Public | BindingFlags.Instance));

        Assert.Contains(
            sinkMethod.GetParameters(),
            parameter => parameter.ParameterType == typeof(AmberSinkAuthorization));
    }

    [Fact]
    public void Domain_production_friend_access_is_minimal_and_explicit()
    {
        var project = XDocument.Load(Path.Combine(RepoRoot(), "src", "Foundry.Domain", "Foundry.Domain.csproj"));
        var productionFriends = project.Descendants("InternalsVisibleTo")
            .Select(element => (string?)element.Attribute("Include") ?? string.Empty)
            .Where(name => !name.StartsWith("Foundry.Tests.", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "Foundry.App.WinForms",
                "Foundry.Application",
                "Foundry.Modules.BuiltIn",
            ],
            productionFriends);
    }

    [Fact]
    public void Compiled_production_friends_expose_only_the_application_review_adapter_as_an_approval_mint_caller()
    {
        var root = RepoRoot();
        var friendProjects = new[]
        {
            (Directory: "Foundry.Domain", Assembly: "Foundry.Domain"),
            (Directory: "Foundry.Application", Assembly: "Foundry.Application"),
            (Directory: "Foundry.App.WinForms", Assembly: "Foundry.App.WinForms"),
            (Directory: "Foundry.Modules.BuiltIn", Assembly: "Foundry.Modules.BuiltIn"),
        };
        var mintTargets = new HashSet<string>(StringComparer.Ordinal)
        {
            "Foundry.Domain.ApprovalGate.Approve",
            "Foundry.Domain.ApprovedArtifact.ApproveThroughGate",
        };
        var calls = friendProjects
            .SelectMany(project => MethodReferences(
                    CurrentReleaseAssemblyPath(root, project.Directory, project.Assembly),
                    mintTargets)
                .Select(call => $"{project.Assembly}:{call}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "Foundry.Application:Foundry.Application.ReviewSession+DomainApprovalGate.Approve -> Foundry.Domain.ApprovalGate.Approve",
                "Foundry.Domain:Foundry.Domain.ApprovalGate.Approve -> Foundry.Domain.ApprovalGate.Approve",
                "Foundry.Domain:Foundry.Domain.ApprovalGate.Approve -> Foundry.Domain.ApprovedArtifact.ApproveThroughGate",
            ],
            calls);
    }

    [Fact]
    public void Portable_snapshot_facade_is_a_read_only_exact_correspondence_verifier()
    {
        var root = RepoRoot();
        var source = File.ReadAllText(
            Path.Combine(root, "src", "Foundry.Rendering", "PortableProjectSnapshot.cs"));
        var project = XDocument.Load(
            Path.Combine(root, "src", "Foundry.Rendering", "Foundry.Rendering.csproj"));
        var productionFriends = project.Descendants("InternalsVisibleTo")
            .Select(element => (string?)element.Attribute("Include") ?? string.Empty)
            .Where(name => !name.StartsWith("Foundry.Tests.", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Contains("public static class PortableProjectSnapshot", source, StringComparison.Ordinal);
        Assert.Equal(["Foundry.ReviewPreview"], productionFriends);
        Assert.Contains("public static bool MatchesExact(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("public static byte[]", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Rewrite", source, StringComparison.Ordinal);

        var callers = new[] { Path.Combine(root, "src"), Path.Combine(root, "tools") }
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Contains(
                    $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal)
                && !path.Contains(
                    $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal)
                && !path.EndsWith("PortableProjectSnapshot.cs", StringComparison.Ordinal)
                && File.ReadAllText(path).Contains("PortableProjectSnapshot.", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "src/Foundry.Storage/OcfprojPackageValidator.cs",
            ],
            callers);
    }

    [Fact]
    public void Amber_authorization_has_no_callable_production_issuer()
    {
        Assert.DoesNotContain(
            typeof(AmberSinkAuthorization).GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static),
            method => method.ReturnType == typeof(AmberSinkAuthorization));
        var constructor = Assert.Single(
            typeof(AmberSinkAuthorization).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance));
        Assert.True(constructor.IsPrivate);

        var delegationMethods = typeof(AmberSinkAuthorization).GetMethods(
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.ReturnType == typeof(AmberSinkAuthorization))
            .Select(method => method.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Empty(delegationMethods);
    }

    [Fact]
    public void Unapproved_preview_capability_is_confined_to_the_review_surface()
    {
        var root = RepoRoot();
        var appRoot = Path.Combine(root, "src", "Foundry.App.WinForms");
        var previewRoot = Path.Combine(root, "src", "Foundry.ReviewPreview");
        var renderingProject = XDocument.Load(
            Path.Combine(root, "src", "Foundry.Rendering", "Foundry.Rendering.csproj"));
        var productionFriends = renderingProject.Descendants("InternalsVisibleTo")
            .Select(element => (string?)element.Attribute("Include") ?? string.Empty)
            .Where(name => !name.StartsWith("Foundry.Tests.", StringComparison.Ordinal))
            .ToArray();
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
        Assert.Equal(["Foundry.ReviewPreview"], productionFriends);
        var previewProject = XDocument.Load(
            Path.Combine(previewRoot, "Foundry.ReviewPreview.csproj"));
        var previewProductionFriends = previewProject.Descendants("InternalsVisibleTo")
            .Select(element => (string?)element.Attribute("Include") ?? string.Empty)
            .Where(name => !name.StartsWith("Foundry.Tests.", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(["Foundry.App.WinForms"], previewProductionFriends);

        var adapterSources = Directory.EnumerateFiles(previewRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal))
            .Select(File.ReadAllText)
            .ToArray();
        Assert.Single(adapterSources);
        Assert.Contains("AccessibleHtmlRenderer.RenderHtmlDocument(", adapterSources[0], StringComparison.Ordinal);
        Assert.DoesNotContain("RenderPortableSnapshot(", adapterSources[0], StringComparison.Ordinal);
        Assert.DoesNotContain("RenderSemanticDerivative(", adapterSources[0], StringComparison.Ordinal);
        Assert.DoesNotContain("LegacyPortableSnapshotRenderer", adapterSources[0], StringComparison.Ordinal);

        var rawRenderTargets = new HashSet<string>(StringComparer.Ordinal)
        {
            "Foundry.Rendering.AccessibleHtmlRenderer.RenderHtmlDocument",
            "Foundry.Rendering.AccessibleHtmlRenderer.RenderPortableSnapshot",
            "Foundry.Rendering.AccessibleHtmlRenderer.RenderSemanticDerivative",
            "Foundry.Rendering.LegacyPortableSnapshotRenderer.RenderV010Dev",
        };
        var adapterReferences = MethodReferences(
            CurrentReleaseAssemblyPath(root, "Foundry.ReviewPreview", "Foundry.ReviewPreview"),
            rawRenderTargets);
        Assert.Equal(
            ["Foundry.Rendering.UnapprovedDraftPreviewFactory.Create -> Foundry.Rendering.AccessibleHtmlRenderer.RenderHtmlDocument"],
            adapterReferences);
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

    [Fact]
    public void Print_internal_export_delegation_has_exactly_one_compiled_caller()
    {
        var assembly = CurrentReleaseAssemblyPath(
            RepoRoot(),
            "Foundry.Infrastructure.Windows",
            "Foundry.Infrastructure.Windows");
        var exportCalls = MethodReferences(
            assembly,
            new HashSet<string>(StringComparer.Ordinal)
            {
                "Foundry.Infrastructure.Windows.EdgePdfExporter.ExportWithinPrintAsync",
            });

        var exportCall = Assert.Single(exportCalls);
        Assert.StartsWith(
            "Foundry.Infrastructure.Windows.WindowsPdfPrinter+<PrintAsync>d__",
            exportCall,
            StringComparison.Ordinal);
        Assert.EndsWith(
            ".MoveNext -> Foundry.Infrastructure.Windows.EdgePdfExporter.ExportWithinPrintAsync",
            exportCall,
            StringComparison.Ordinal);

        var physicalPrintCalls = MethodReferences(
            assembly,
            new HashSet<string>(StringComparer.Ordinal)
            {
                "Foundry.Infrastructure.Windows.WindowsPdfPrinter.PrintPdfAsync",
            });

        var physicalPrintCall = Assert.Single(physicalPrintCalls);
        Assert.StartsWith(
            "Foundry.Infrastructure.Windows.WindowsPdfPrinter+<PrintAsync>d__",
            physicalPrintCall,
            StringComparison.Ordinal);
        Assert.EndsWith(
            ".MoveNext -> Foundry.Infrastructure.Windows.WindowsPdfPrinter.PrintPdfAsync",
            physicalPrintCall,
            StringComparison.Ordinal);
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

    [Theory]
    [InlineData("AllAboardForm.cs", "internal async Task ExportAsync(ApprovedArtifact approved)")]
    [InlineData("PressRoomForm.cs", "internal async Task ExportAsync()")]
    [InlineData("ModuleStudioForm.cs", "internal async Task ExportAsync()")]
    public void Desktop_file_exports_demand_export_before_any_render_or_writer(
        string fileName,
        string methodSignature)
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot(),
            "src",
            "Foundry.App.WinForms",
            fileName));
        var methodStart = source.IndexOf(methodSignature, StringComparison.Ordinal);
        Assert.True(methodStart >= 0, $"{fileName} has no audited export method.");
        var demand = source.IndexOf(
            "ArtifactSinkAuthorizationGate.DemandExport(approved, amberAuthorization: null);",
            methodStart,
            StringComparison.Ordinal);

        Assert.True(demand > methodStart, $"{fileName} does not demand Export in its export method.");
        var beforeDemand = source[methodStart..demand];
        Assert.DoesNotContain("AppServices.Render(", beforeDemand, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteExportBytesAsync(", beforeDemand, StringComparison.Ordinal);
        Assert.DoesNotContain("_exportWriter(", beforeDemand, StringComparison.Ordinal);
        Assert.DoesNotContain("_pdfExporter(", beforeDemand, StringComparison.Ordinal);
        Assert.DoesNotContain("_exportPicker(", beforeDemand, StringComparison.Ordinal);
        Assert.DoesNotContain("ImposeBooklet(", beforeDemand, StringComparison.Ordinal);
        Assert.DoesNotContain("_exportInProgress = true", beforeDemand, StringComparison.Ordinal);
        Assert.DoesNotContain("new CancellationTokenSource", beforeDemand, StringComparison.Ordinal);
        Assert.DoesNotContain("_exportCancellation =", beforeDemand, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("AllAboardForm.cs", "private async Task OpenPrintViewAsync(ApprovedArtifact approved)")]
    [InlineData("PressRoomForm.cs", "private async Task OpenPrintViewAsync()")]
    [InlineData("ModuleStudioForm.cs", "private async Task OpenPrintViewAsync()")]
    public void Desktop_print_view_coordinators_demand_print_before_state_or_delegate(
        string fileName,
        string methodSignature)
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot(),
            "src",
            "Foundry.App.WinForms",
            fileName));
        var methodStart = source.IndexOf(methodSignature, StringComparison.Ordinal);
        Assert.True(methodStart >= 0, $"{fileName} has no audited print-view method.");
        var demand = source.IndexOf(
            "ArtifactSinkAuthorizationGate.DemandPrint(approved, amberAuthorization: null);",
            methodStart,
            StringComparison.Ordinal);

        Assert.True(demand > methodStart, $"{fileName} does not demand Print in its print-view method.");
        var beforeDemand = source[methodStart..demand];
        Assert.DoesNotContain("_printViewInProgress = true", beforeDemand, StringComparison.Ordinal);
        Assert.DoesNotContain("_printViewOpener(", beforeDemand, StringComparison.Ordinal);
    }

    private static string CurrentReleaseAssemblyPath(string root, string projectDirectory, string assemblyName)
    {
        var releaseSegment = $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}";
        var candidates = Directory.EnumerateFiles(
                Path.Combine(root, "src", projectDirectory, "bin", "Release"),
                $"{assemblyName}.dll",
                SearchOption.AllDirectories)
            .Where(path => path.Contains(releaseSegment, StringComparison.OrdinalIgnoreCase)
                && !path.Contains($"{Path.DirectorySeparatorChar}ref{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                && !path.Contains($"{Path.DirectorySeparatorChar}refint{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var assemblyPath = Assert.Single(candidates);
        var latestSourceWrite = Directory.EnumerateFiles(
                Path.Combine(root, "src", projectDirectory),
                "*.cs",
                SearchOption.AllDirectories)
            .Where(path => !path.Contains(
                    $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase)
                && !path.Contains(
                    $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase))
            .Max(File.GetLastWriteTimeUtc);
        Assert.True(
            File.GetLastWriteTimeUtc(assemblyPath) >= latestSourceWrite,
            $"The inspected Release assembly for {assemblyName} is older than its source; rebuild before auditing approval call sites.");
        return assemblyPath;
    }

    private static List<string> MethodReferences(
        string assemblyPath,
        HashSet<string> targets)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var peReader = new PEReader(stream);
        var metadata = peReader.GetMetadataReader();
        var calls = new List<string>();

        foreach (var typeHandle in metadata.TypeDefinitions)
        {
            var type = metadata.GetTypeDefinition(typeHandle);
            var callerType = TypeDefinitionName(metadata, typeHandle);
            foreach (var methodHandle in type.GetMethods())
            {
                var method = metadata.GetMethodDefinition(methodHandle);
                if (method.RelativeVirtualAddress == 0)
                {
                    continue;
                }

                var body = peReader.GetMethodBody(method.RelativeVirtualAddress);
                var il = body.GetILBytes()
                    ?? throw new InvalidOperationException("A concrete method had no IL body.");
                var offset = 0;
                while (offset < il.Length)
                {
                    var opcode = ReadOpcode(il, ref offset);
                    var operandOffset = offset;
                    var operandSize = OperandSize(opcode.OperandType, il, operandOffset);
                    if ((opcode == OpCodes.Call
                            || opcode == OpCodes.Callvirt
                            || opcode == OpCodes.Ldftn
                            || opcode == OpCodes.Ldvirtftn)
                        && operandSize == sizeof(int))
                    {
                        var token = BitConverter.ToInt32(il, operandOffset);
                        var target = CalledMemberName(metadata, MetadataTokens.EntityHandle(token));
                        if (target is not null && targets.Contains(target))
                        {
                            calls.Add(
                                $"{callerType}.{metadata.GetString(method.Name)} -> {target}");
                        }
                    }

                    offset = checked(operandOffset + operandSize);
                }
            }
        }

        return calls;
    }

    private static OpCode ReadOpcode(ReadOnlySpan<byte> il, ref int offset)
    {
        var first = il[offset++];
        var value = first == 0xFE
            ? (ushort)(0xFE00 | il[offset++])
            : first;
        return AllOpCodes.TryGetValue(value, out var opcode)
            ? opcode
            : throw new InvalidOperationException($"Unknown IL opcode 0x{value:X4}.");
    }

    private static int OperandSize(OperandType operandType, ReadOnlySpan<byte> il, int offset)
        => operandType switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineBrTarget
                or OperandType.InlineField
                or OperandType.InlineI
                or OperandType.InlineMethod
                or OperandType.InlineSig
                or OperandType.InlineString
                or OperandType.InlineTok
                or OperandType.InlineType
                or OperandType.ShortInlineR => 4,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch => checked(sizeof(int) + (BitConverter.ToInt32(il[offset..]) * sizeof(int))),
            _ => throw new InvalidOperationException($"Unsupported IL operand type {operandType}."),
        };

    private static string? CalledMemberName(MetadataReader metadata, EntityHandle handle)
    {
        if (handle.Kind == HandleKind.MethodSpecification)
        {
            handle = metadata.GetMethodSpecification((MethodSpecificationHandle)handle).Method;
        }

        if (handle.Kind == HandleKind.MethodDefinition)
        {
            var methodHandle = (MethodDefinitionHandle)handle;
            var method = metadata.GetMethodDefinition(methodHandle);
            foreach (var typeHandle in metadata.TypeDefinitions)
            {
                if (metadata.GetTypeDefinition(typeHandle).GetMethods().Contains(methodHandle))
                {
                    return string.Concat(
                        TypeDefinitionName(metadata, typeHandle),
                        ".",
                        metadata.GetString(method.Name));
                }
            }

            throw new InvalidOperationException("A method definition had no declaring type.");
        }

        if (handle.Kind != HandleKind.MemberReference)
        {
            return null;
        }

        var member = metadata.GetMemberReference((MemberReferenceHandle)handle);
        if (member.Parent.Kind != HandleKind.TypeReference)
        {
            return null;
        }

        var declaringType = metadata.GetTypeReference((TypeReferenceHandle)member.Parent);
        var @namespace = metadata.GetString(declaringType.Namespace);
        var typeName = metadata.GetString(declaringType.Name);
        return string.Concat(
            @namespace,
            @namespace.Length == 0 ? string.Empty : ".",
            typeName,
            ".",
            metadata.GetString(member.Name));
    }

    private static string TypeDefinitionName(MetadataReader metadata, TypeDefinitionHandle handle)
    {
        var type = metadata.GetTypeDefinition(handle);
        var name = metadata.GetString(type.Name);
        var declaring = type.GetDeclaringType();
        if (!declaring.IsNil)
        {
            return string.Concat(TypeDefinitionName(metadata, declaring), "+", name);
        }

        var @namespace = metadata.GetString(type.Namespace);
        return string.Concat(@namespace, @namespace.Length == 0 ? string.Empty : ".", name);
    }

    private static readonly Dictionary<ushort, OpCode> AllOpCodes = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(OpCode))
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(opcode => unchecked((ushort)opcode.Value));
}
