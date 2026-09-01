using Foundry.Domain;
using Foundry.Storage;
using System.Text.Json.Nodes;

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

        // Exact current-build identity only. Neither this digest nor the closed
        // root is AAC/SLP, accessibility, or rights-review evidence.
        Assert.Equal(
            "103D117E37090A6CBB9968AE9485D7C1CD9C7D3EEEC65B13DFC1054609D1B2A4",
            catalog.ManifestSha256);
        Assert.Equal(13, catalog.All.Count);
        Assert.Empty(catalog.VerifyIntegrity());
        Assert.Empty(catalog.VerifyClosedDeploymentRoot());
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Closed_shipped_deployment_refuses_top_level_and_nested_unlisted_entries(bool nested)
    {
        var directory = Path.Combine(Path.GetTempPath(), "ocf-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            CopyShippedPack(directory);
            var unlistedName = "unlisted-synthetic-entry";
            var unlistedPath = nested
                ? Path.Combine(directory, unlistedName, "fixture.bin")
                : Path.Combine(directory, unlistedName + ".bin");
            Directory.CreateDirectory(Path.GetDirectoryName(unlistedPath)!);
            File.WriteAllText(unlistedPath, "synthetic unlisted bytes");

            var catalog = new JsonAssetCatalog(directory);

            // Generic pack integrity remains about declared records. Only the
            // shipped-deployment boundary closes the root.
            Assert.Empty(catalog.VerifyIntegrity());
            var issue = Assert.Single(
                catalog.VerifyClosedDeploymentRoot(),
                candidate => candidate.Code == "asset.unexpected-deployment-entry");
            Assert.DoesNotContain(directory, issue.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(unlistedName, issue.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
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
            Assert.False(new JsonAssetCatalog(directory).TryGetContent(
                new AssetId("agency.stop.v1"), out _, out _));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void A_manifest_filename_is_data_not_a_path()
    {
        var root = Path.Combine(Path.GetTempPath(), "ocf-tests", Guid.NewGuid().ToString("N"));
        var directory = Path.Combine(root, "pack");
        Directory.CreateDirectory(directory);
        try
        {
            var source = SymbolPackDirectory();
            foreach (var file in Directory.GetFiles(source))
            {
                File.Copy(file, Path.Combine(directory, Path.GetFileName(file)));
            }

            File.Copy(Path.Combine(source, "stop.svg"), Path.Combine(root, "outside.svg"));
            var manifestPath = Path.Combine(directory, JsonAssetCatalog.ManifestFileName);
            var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsArray();
            manifest[0]!["fileName"] = "../outside.svg";
            File.WriteAllText(manifestPath, manifest.ToJsonString());

            var catalog = new JsonAssetCatalog(directory);

            Assert.Contains(catalog.VerifyIntegrity(), issue => issue.Code == "asset.invalid-file-name");
            Assert.False(catalog.TryGetContent(new AssetId("agency.stop.v1"), out _, out _));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Required_provenance_and_alt_text_fail_closed_at_lookup()
    {
        var root = Path.Combine(Path.GetTempPath(), "ocf-tests", Guid.NewGuid().ToString("N"));
        var directory = Path.Combine(root, "pack");
        Directory.CreateDirectory(directory);
        try
        {
            CopyShippedPack(directory);
            var manifestPath = Path.Combine(directory, JsonAssetCatalog.ManifestFileName);
            var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsArray();
            var stop = manifest[0]!;
            stop["source"] = "";
            stop["creator"] = " ";
            stop["intendedMeaning"] = "";
            stop["altText"] = " ";
            File.WriteAllText(manifestPath, manifest.ToJsonString());

            var catalog = new JsonAssetCatalog(directory);
            var issues = catalog.VerifyIntegrity();

            Assert.Contains(issues, issue => issue.Code == "asset.incomplete-provenance");
            Assert.Contains(issues, issue => issue.Code == "asset.alt-text");
            Assert.False(catalog.TryGetContent(new AssetId("agency.stop.v1"), out var content, out var mimeType));
            Assert.True(content.IsEmpty);
            Assert.Empty(mimeType);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void A_shipped_catalog_refuses_a_consistently_nonredistributable_non_open_asset()
    {
        AssertCatalogRightsAreRefused("LicenseRef-teacher-local", redistributable: false);
    }

    [Theory]
    [InlineData("LicenseRef-teacher-local", true)]
    [InlineData("All Rights Reserved", true)]
    [InlineData("CC0-1.0", false)]
    public void A_manifest_boolean_cannot_forge_or_hide_redistribution_rights(string license, bool redistributable)
    {
        AssertCatalogRightsAreRefused(license, redistributable);
    }

    [Fact]
    public void An_oversized_asset_is_refused_before_whole_file_allocation()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ocf-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            CopyShippedPack(directory);
            using (var stream = new FileStream(
                Path.Combine(directory, "stop.svg"),
                FileMode.Create,
                FileAccess.Write,
                FileShare.None))
            {
                stream.SetLength(AssetFileSafety.MaxAssetBytes + 1L);
            }

            var catalog = new JsonAssetCatalog(directory);

            Assert.Contains(catalog.VerifyIntegrity(), issue => issue.Code == "asset.size-limit");
            Assert.False(catalog.TryGetContent(new AssetId("agency.stop.v1"), out _, out _));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void An_oversized_manifest_is_refused_before_json_binding()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ocf-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllBytes(
                Path.Combine(directory, JsonAssetCatalog.ManifestFileName),
                new byte[AssetManifestReader.MaxManifestBytes + 1]);

            var exception = Assert.Throws<InvalidDataException>(() => new JsonAssetCatalog(directory));

            Assert.Contains(
                AssetManifestReader.MaxManifestBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
                exception.Message,
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("\"source\":", "\"source\":\"shadow\",\"source\":")]
    [InlineData("\"source\":", "\"Source\":\"shadow\",\"source\":")]
    [InlineData("\"source\":", "\"unexpected\":\"shadow\",\"source\":")]
    public void Duplicate_case_confusable_and_unknown_manifest_fields_are_refused(
        string marker,
        string replacement)
    {
        var directory = Path.Combine(Path.GetTempPath(), "ocf-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            CopyShippedPack(directory);
            var path = Path.Combine(directory, JsonAssetCatalog.ManifestFileName);
            var json = File.ReadAllText(path);
            var index = json.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(index >= 0);
            File.WriteAllText(path, string.Concat(json.AsSpan(0, index), replacement, json.AsSpan(index + marker.Length)));

            Assert.Throws<InvalidDataException>(() => new JsonAssetCatalog(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void A_manifest_with_too_many_records_is_refused()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ocf-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var source = JsonNode.Parse(File.ReadAllText(Path.Combine(SymbolPackDirectory(), JsonAssetCatalog.ManifestFileName)))!.AsArray();
            var records = new JsonArray();
            for (var index = 0; index <= AssetManifestReader.MaxRecordCount; index++)
            {
                records.Add(source[0]!.DeepClone());
            }

            File.WriteAllText(Path.Combine(directory, JsonAssetCatalog.ManifestFileName), records.ToJsonString());

            var exception = Assert.Throws<InvalidDataException>(() => new JsonAssetCatalog(directory));
            Assert.Contains("record limit", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void AssertCatalogRightsAreRefused(string license, bool redistributable)
    {
        var root = Path.Combine(Path.GetTempPath(), "ocf-tests", Guid.NewGuid().ToString("N"));
        var directory = Path.Combine(root, "pack");
        Directory.CreateDirectory(directory);
        try
        {
            CopyShippedPack(directory);
            var manifestPath = Path.Combine(directory, JsonAssetCatalog.ManifestFileName);
            var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsArray();
            manifest[0]!["license"] = license;
            manifest[0]!["redistributable"] = redistributable;
            File.WriteAllText(manifestPath, manifest.ToJsonString());

            var catalog = new JsonAssetCatalog(directory);

            Assert.Contains(catalog.VerifyIntegrity(), issue => issue.Code == "asset.redistribution-rights");
            Assert.False(catalog.TryGetContent(new AssetId("agency.stop.v1"), out _, out _));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
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

    private static void CopyShippedPack(string directory)
    {
        foreach (var file in Directory.GetFiles(SymbolPackDirectory()))
        {
            File.Copy(file, Path.Combine(directory, Path.GetFileName(file)));
        }
    }
}
