using System.Security.Cryptography;
using Foundry.Storage;

namespace Foundry.Tests.Integration;

/// <summary>
/// The rights-metadata machine row of spec §9 (second forge menu, item 8):
/// "every shipped template and guide face carries complete rights metadata;
/// CI hard-fails otherwise." This suite runs in CI, so failing here IS the
/// hard-fail. Unknown rights block distribution — including ours.
/// </summary>
public class RightsMetadataTests
{
    private static string AssetsRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "OpenClassroomFoundry.slnx")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(
            dir?.FullName ?? throw new InvalidOperationException("Repository root not found."),
            "assets", "symbols");
    }

    [Fact]
    public void Every_shipped_asset_carries_complete_rights_metadata()
    {
        var catalog = new JsonAssetCatalog(AssetsRoot());

        Assert.NotEmpty(catalog.All);
        Assert.All(catalog.All, provenance =>
        {
            Assert.False(string.IsNullOrWhiteSpace(provenance.Id.Value));
            Assert.False(string.IsNullOrWhiteSpace(provenance.License), $"{provenance.Id.Value}: no license");
            Assert.False(string.IsNullOrWhiteSpace(provenance.Source), $"{provenance.Id.Value}: no source");
            Assert.False(string.IsNullOrWhiteSpace(provenance.Creator), $"{provenance.Id.Value}: no creator");
            Assert.False(string.IsNullOrWhiteSpace(provenance.Sha256), $"{provenance.Id.Value}: no content hash");
            Assert.False(string.IsNullOrWhiteSpace(provenance.AltText), $"{provenance.Id.Value}: no alt text");
            Assert.False(string.IsNullOrWhiteSpace(provenance.IntendedMeaning), $"{provenance.Id.Value}: no intended meaning");

            // The pack ships with the GPL program; a non-redistributable entry
            // in the shipped pack is a distribution violation waiting to happen.
            Assert.True(provenance.Redistributable, $"{provenance.Id.Value}: shipped but not redistributable");
        });
    }

    [Fact]
    public void Every_recorded_hash_matches_the_bytes_actually_on_disk()
    {
        var root = AssetsRoot();
        var catalog = new JsonAssetCatalog(root);

        Assert.All(catalog.All, provenance =>
        {
            var path = Path.Combine(root, provenance.FileName);
            Assert.True(File.Exists(path), $"{provenance.Id.Value}: declared file '{provenance.FileName}' is missing");

            var actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
            Assert.Equal(provenance.Sha256, actual, ignoreCase: true);
        });
    }

    [Fact]
    public void No_orphan_file_ships_without_a_provenance_record()
    {
        var root = AssetsRoot();
        var declared = new JsonAssetCatalog(root).All
            .Select(p => p.FileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var orphans = Directory.EnumerateFiles(root)
            .Select(Path.GetFileName)
            .Where(name => name is not null
                && !string.Equals(name, "manifest.json", StringComparison.OrdinalIgnoreCase)
                && !declared.Contains(name))
            .ToList();

        Assert.True(orphans.Count == 0, "Files with no rights record: " + string.Join(", ", orphans));
    }
}
