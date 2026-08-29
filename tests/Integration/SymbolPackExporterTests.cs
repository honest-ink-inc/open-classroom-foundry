using System.Text;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Storage;
using Xunit;

namespace Foundry.Tests.Integration;

/// <summary>The export-side proof of the Symbol Commons invariant.</summary>
public class SymbolPackExporterTests : IDisposable
{
    private readonly string _work = Path.Combine(Path.GetTempPath(), "ocf-tests", Guid.NewGuid().ToString("N"));
    private readonly JsonAssetCatalog _pack;

    public SymbolPackExporterTests()
    {
        var repo = new DirectoryInfo(AppContext.BaseDirectory);
        while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "OpenClassroomFoundry.slnx")))
        {
            repo = repo.Parent;
        }

        _pack = new JsonAssetCatalog(Path.Combine(repo!.FullName, "assets", "symbols"));
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        try
        {
            if (Directory.Exists(_work))
            {
                Directory.Delete(_work, recursive: true);
            }
        }
        catch (IOException)
        {
            // Temp cleanup is best-effort.
        }
    }

    [Fact]
    public void An_exported_pack_reopens_as_a_verified_catalog_with_attributions()
    {
        var target = Path.Combine(_work, "pack");

        var exported = SymbolPackExporter.ExportPack(_pack, [.. _pack.All.Select(a => a.Id)], target);

        Assert.Equal(13, exported.Count);

        var reopened = new JsonAssetCatalog(target);
        Assert.Equal(13, reopened.All.Count);
        Assert.Empty(reopened.VerifyIntegrity());
        Assert.Contains("agency.stop.v1", File.ReadAllText(Path.Combine(target, SymbolPackExporter.AttributionsFileName)), StringComparison.Ordinal);
    }

    [Fact]
    public void A_teacher_local_symbol_is_provably_unexportable()
    {
        var shelf = new LocalSymbolStore(Path.Combine(_work, "shelf"));
        var local = shelf.Add(new SymbolSubmission(
            new AssetId("teacher.my-cup.v1"), "My cup", "A blue cup",
            Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>"),
            "image/svg+xml", "My own photograph"));

        var composite = new CompositeAssetCatalog(_pack, shelf);
        var target = Path.Combine(_work, "pack");

        var exception = Assert.Throws<InvalidOperationException>(
            () => SymbolPackExporter.ExportPack(composite, [new AssetId("agency.stop.v1"), local.Id], target));

        // Refused entirely — not silently filtered — and nothing was written.
        Assert.Contains("open export", exception.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(target, SymbolPackExporter.AttributionsFileName)));
    }

    [Fact]
    public void An_explicitly_open_teacher_symbol_may_travel()
    {
        var shelf = new LocalSymbolStore(Path.Combine(_work, "shelf"));
        var shared = shelf.Add(new SymbolSubmission(
            new AssetId("teacher.shared-star.v1"), "Star of the day", "A five-pointed star",
            Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>"),
            "image/svg+xml", "Drawn by me, shared freely", License: "CC-BY-4.0"));

        var target = Path.Combine(_work, "pack");
        var exported = SymbolPackExporter.ExportPack(shelf, [shared.Id], target);

        Assert.Single(exported);
        Assert.Contains("CC-BY-4.0", File.ReadAllText(Path.Combine(target, SymbolPackExporter.AttributionsFileName)), StringComparison.Ordinal);
    }
}
