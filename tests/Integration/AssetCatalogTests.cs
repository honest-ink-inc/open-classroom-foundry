using Foundry.Domain;
using Foundry.Storage;
using Xunit;

namespace Foundry.Tests.Integration;

public class AssetCatalogTests
{
    private static string SymbolPackDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpenClassroomFoundry.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "assets", "symbols");
    }

    [Fact]
    public void The_shipped_pack_has_complete_verified_provenance()
    {
        var catalog = new JsonAssetCatalog(SymbolPackDirectory());

        Assert.Equal(7, catalog.All.Count);
        Assert.Empty(catalog.VerifyIntegrity());
        Assert.All(catalog.All, asset =>
        {
            Assert.Equal("CC0-1.0", asset.License);
            Assert.True(asset.Redistributable);
            Assert.False(string.IsNullOrWhiteSpace(asset.AltText));
            Assert.False(string.IsNullOrWhiteSpace(asset.IntendedMeaning));
        });
    }

    [Fact]
    public void No_symbol_bakes_words_into_the_graphic()
    {
        var catalog = new JsonAssetCatalog(SymbolPackDirectory());

        foreach (var asset in catalog.All)
        {
            Assert.True(catalog.TryGetContent(asset.Id, out var content, out var mimeType));
            Assert.Equal("image/svg+xml", mimeType);

            var svg = System.Text.Encoding.UTF8.GetString(content.Span);
            Assert.DoesNotContain("<text", svg, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("aria-label", svg, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void A_tampered_file_fails_integrity()
    {
        var source = SymbolPackDirectory();
        var directory = Path.Combine(Path.GetTempPath(), "ocf-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            foreach (var file in Directory.GetFiles(source))
            {
                File.Copy(file, Path.Combine(directory, Path.GetFileName(file)));
            }

            File.AppendAllText(Path.Combine(directory, "stop.svg"), "<!-- drift -->");

            var issues = new JsonAssetCatalog(directory).VerifyIntegrity();

            Assert.Contains(issues, i => i.Code == "asset.hash-mismatch");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Lookup_and_content_round_trip()
    {
        var catalog = new JsonAssetCatalog(SymbolPackDirectory());

        var stop = catalog.Find(new AssetId("agency.stop.v1"));

        Assert.NotNull(stop);
        Assert.Equal("Stop", stop.IntendedMeaning);
        Assert.True(catalog.TryGetContent(stop.Id, out var content, out _));
        Assert.Contains("<polygon", System.Text.Encoding.UTF8.GetString(content.Span), StringComparison.Ordinal);
    }
}
