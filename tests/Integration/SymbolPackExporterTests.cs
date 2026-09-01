using System.Text;
using System.Security.Cryptography;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Storage;

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
    public void A_complete_pack_reopens_as_a_verified_catalog_with_a_provenance_report()
    {
        var bytes = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
        var provenance = Provenance("complete.symbol.v1", "complete.svg", bytes);
        var target = Path.Combine(_work, "pack");

        var exported = SymbolPackExporter.ExportPack(
            new MemoryCatalog(provenance, bytes), [provenance.Id], target);

        Assert.Single(exported);

        var reopened = new JsonAssetCatalog(target);
        Assert.Single(reopened.All);
        Assert.Empty(reopened.VerifyIntegrity());
        // ATTRIBUTIONS.md is valid generic-pack metadata. The application does
        // not mistake that broader topology for its exact shipped deployment.
        Assert.Contains(
            reopened.VerifyClosedDeploymentRoot(),
            issue => issue.Code == "asset.unexpected-deployment-entry");
        var report = File.ReadAllText(Path.Combine(target, SymbolPackExporter.AttributionsFileName));
        Assert.Contains("complete.symbol.v1", report, StringComparison.Ordinal);
        Assert.Contains("Source: synthetic-test", report, StringComparison.Ordinal);
        Assert.Contains("Modifications: Original synthetic fixture", report, StringComparison.Ordinal);
        Assert.Contains("Attribution: No attribution required for this synthetic fixture", report, StringComparison.Ordinal);
    }

    [Fact]
    public void Contextual_joiners_in_non_Latin_provenance_remain_exportable()
    {
        var bytes = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
        var provenance = Provenance("persian.symbol.v1", "persian.svg", bytes) with
        {
            Creator = "هنرمند‌مدرس",
            IntendedMeaning = "نشانهٔ کمک",
        };
        var target = Path.Combine(_work, "persian-pack");

        SymbolPackExporter.ExportPack(new MemoryCatalog(provenance, bytes), [provenance.Id], target);

        var report = File.ReadAllText(Path.Combine(target, SymbolPackExporter.AttributionsFileName));
        Assert.Contains("\u200C", report, StringComparison.Ordinal);
        Assert.Empty(new JsonAssetCatalog(target).VerifyIntegrity());
    }

    [Fact]
    public void Script_significant_format_marks_remain_valid_multilingual_provenance()
    {
        var bytes = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
        var provenance = Provenance("syriac.symbol.v1", "syriac.svg", bytes) with
        {
            Creator = "ܐ\u070Fܒ",
            IntendedMeaning = "نهاية\u06DDآية",
        };
        var target = Path.Combine(_work, "syriac-pack");

        SymbolPackExporter.ExportPack(new MemoryCatalog(provenance, bytes), [provenance.Id], target);

        var report = File.ReadAllText(Path.Combine(target, SymbolPackExporter.AttributionsFileName));
        Assert.Contains("\u070F", report, StringComparison.Ordinal);
        Assert.Contains("\u06DD", report, StringComparison.Ordinal);
        Assert.Empty(new JsonAssetCatalog(target).VerifyIntegrity());
    }

    [Fact]
    public void The_shipped_catalog_cannot_be_exported_until_rights_dispositions_are_reviewed()
    {
        var target = Path.Combine(_work, "unreviewed-pack");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            SymbolPackExporter.ExportPack(_pack, [.. _pack.All.Select(asset => asset.Id)], target));

        Assert.Contains("explicit attribution and modification dispositions", exception.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(target));
    }

    [Fact]
    public void A_teacher_local_symbol_is_provably_unexportable()
    {
        var bytes = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
        var open = Provenance("known.open.v1", "known-open.svg", bytes);
        var shelf = new LocalSymbolStore(Path.Combine(_work, "shelf"));
        var local = shelf.Add(new SymbolSubmission(
            new AssetId("teacher.my-cup.v1"), "My cup", "A blue cup",
            Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>"),
            "image/svg+xml", "My own photograph"));

        var composite = new CompositeAssetCatalog(new MemoryCatalog(open, bytes), shelf);
        var target = Path.Combine(_work, "pack");

        var exception = Assert.Throws<InvalidOperationException>(
            () => SymbolPackExporter.ExportPack(composite, [open.Id, local.Id], target));

        // Refused entirely — not silently filtered — and nothing was written.
        Assert.Contains("open export", exception.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(target));
    }

    [Fact]
    public void An_open_license_alone_cannot_substitute_for_export_dispositions()
    {
        var shelf = new LocalSymbolStore(Path.Combine(_work, "shelf"));
        var shared = shelf.Add(new SymbolSubmission(
            new AssetId("teacher.shared-star.v1"), "Star of the day", "A five-pointed star",
            Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>"),
            "image/svg+xml", "Drawn by me, shared freely", License: "CC-BY-4.0"));

        var target = Path.Combine(_work, "pack");
        var exception = Assert.Throws<InvalidOperationException>(() =>
            SymbolPackExporter.ExportPack(shelf, [shared.Id], target));

        Assert.Contains("explicit attribution and modification dispositions", exception.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(target));
    }

    [Theory]
    [InlineData("LicenseRef-teacher-local")]
    [InlineData("All Rights Reserved")]
    public void A_forged_redistributable_boolean_cannot_export_a_non_open_license(string license)
    {
        var bytes = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
        var provenance = Provenance("forged.rights.v1", "forged.svg", bytes) with
        {
            License = license,
            Redistributable = true,
        };
        var target = Path.Combine(_work, "forged-rights-pack");

        var exception = Assert.Throws<InvalidOperationException>(() => SymbolPackExporter.ExportPack(
            new MemoryCatalog(provenance, bytes), [provenance.Id], target));

        Assert.Contains("inconsistent license and redistribution", exception.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(target));
    }

    [Theory]
    [InlineData("../escaped.svg")]
    [InlineData("..\\escaped.svg")]
    public void A_manifest_controlled_traversal_filename_is_refused_before_any_write(string fileName)
    {
        var bytes = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
        var provenance = Provenance("unsafe.traversal.v1", fileName, bytes);
        var target = Path.Combine(_work, "pack");

        var exception = Assert.Throws<InvalidOperationException>(
            () => SymbolPackExporter.ExportPack(new MemoryCatalog(provenance, bytes), [provenance.Id], target));

        Assert.Contains("unsafe asset filename", exception.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(target));
        Assert.False(File.Exists(Path.GetFullPath(Path.Combine(target, fileName))));
    }

    [Fact]
    public void A_manifest_controlled_rooted_filename_is_refused_before_any_write()
    {
        var bytes = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
        var escaped = Path.Combine(_work, "rooted-escape.svg");
        var provenance = Provenance("unsafe.rooted.v1", escaped, bytes);
        var target = Path.Combine(_work, "pack");

        Assert.Throws<InvalidOperationException>(
            () => SymbolPackExporter.ExportPack(new MemoryCatalog(provenance, bytes), [provenance.Id], target));

        Assert.False(Directory.Exists(target));
        Assert.False(File.Exists(escaped));
    }

    [Fact]
    public void Portable_case_insensitive_asset_filename_collisions_are_refused_before_any_write()
    {
        var firstBytes = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><title>first</title></svg>");
        var secondBytes = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><title>second</title></svg>");
        var first = Provenance("collision.first.v1", "shared.svg", firstBytes);
        var second = Provenance("collision.second.v1", "SHARED.svg", secondBytes);
        var target = Path.Combine(_work, "pack");
        var catalog = new MemoryCatalog((first, firstBytes), (second, secondBytes));

        var exception = Assert.Throws<InvalidOperationException>(
            () => SymbolPackExporter.ExportPack(catalog, [first.Id, second.Id], target));

        Assert.Contains("collides", exception.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(target));
    }

    [Theory]
    [InlineData(JsonAssetCatalog.ManifestFileName)]
    [InlineData(SymbolPackExporter.AttributionsFileName)]
    public void Asset_filenames_cannot_collide_with_pack_metadata(string fileName)
    {
        var bytes = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
        var provenance = Provenance("collision.metadata.v1", fileName, bytes);
        var target = Path.Combine(_work, "pack");

        var exception = Assert.Throws<InvalidOperationException>(
            () => SymbolPackExporter.ExportPack(new MemoryCatalog(provenance, bytes), [provenance.Id], target));

        Assert.Contains("collides", exception.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(target));
    }

    [Fact]
    public void Content_must_match_the_manifest_hash_before_any_write()
    {
        var bytes = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
        var provenance = Provenance("hash.mismatch.v1", "mismatch.svg", bytes) with
        {
            Sha256 = new string('0', 64),
        };
        var target = Path.Combine(_work, "pack");

        var exception = Assert.Throws<InvalidOperationException>(
            () => SymbolPackExporter.ExportPack(new MemoryCatalog(provenance, bytes), [provenance.Id], target));

        Assert.Contains("recorded SHA-256", exception.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(target));
    }

    [Fact]
    public void Complete_provenance_is_required_before_any_pack_file_is_written()
    {
        var bytes = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
        var baseline = Provenance("incomplete.provenance.v1", "symbol.svg", bytes);
        var incomplete = new[]
        {
            baseline with { ConceptId = "" },
            baseline with { Version = "" },
            baseline with { Source = "" },
            baseline with { Creator = "" },
            baseline with { IntendedMeaning = "" },
            baseline with { AltText = "" },
            baseline with { RequiredAttribution = "" },
            baseline with { Modifications = "" },
        };

        foreach (var provenance in incomplete)
        {
            var target = Path.Combine(_work, Guid.NewGuid().ToString("N"), "pack");

            Assert.Throws<InvalidOperationException>(() => SymbolPackExporter.ExportPack(
                new MemoryCatalog(provenance, bytes), [provenance.Id], target));
            Assert.False(Directory.Exists(target));
        }
    }

    [Theory]
    [InlineData("\u200B")]
    [InlineData("\uFEFF")]
    [InlineData("—")]
    [InlineData("... !?")]
    [InlineData("\u0301")]
    [InlineData("\u115F")]
    public void Format_or_punctuation_only_dispositions_are_refused_before_any_pack_file_is_written(
        string emptyLookingDisposition)
    {
        var bytes = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
        var baseline = Provenance("empty-looking.provenance.v1", "symbol.svg", bytes);

        foreach (var provenance in new[]
        {
            baseline with { RequiredAttribution = emptyLookingDisposition },
            baseline with { Modifications = emptyLookingDisposition },
        })
        {
            var target = Path.Combine(_work, Guid.NewGuid().ToString("N"), "pack");

            Assert.Throws<InvalidOperationException>(() =>
                SymbolPackExporter.ExportPack(
                    new MemoryCatalog(provenance, bytes),
                    [provenance.Id],
                    target));

            Assert.False(Directory.Exists(target));
        }
    }

    [Fact]
    public void Media_type_and_filename_must_agree_before_any_pack_file_is_written()
    {
        var bytes = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
        var provenance = Provenance("mismatched.media.v1", "symbol.png", bytes);
        var target = Path.Combine(_work, "mismatched-media-pack");

        Assert.Throws<InvalidOperationException>(() => SymbolPackExporter.ExportPack(
            new MemoryCatalog(provenance, bytes), [provenance.Id], target));
        Assert.False(Directory.Exists(target));
    }

    [Fact]
    public void Active_svg_content_is_refused_before_any_pack_file_is_written()
    {
        var bytes = Encoding.UTF8.GetBytes(
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>");
        var provenance = Provenance("active.svg.v1", "active.svg", bytes);
        var target = Path.Combine(_work, "active-svg-pack");

        var exception = Assert.Throws<InvalidOperationException>(() => SymbolPackExporter.ExportPack(
            new MemoryCatalog(provenance, bytes), [provenance.Id], target));

        Assert.Contains("self-contained image", exception.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(target));
    }

    [Fact]
    public void Attribution_values_are_plain_text_and_cannot_activate_markdown_or_remote_fetches()
    {
        var bytes = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
        var provenance = Provenance("safe.markdown.v1", "safe.svg", bytes) with
        {
            IntendedMeaning = "![tracking image](https://evil.invalid/pixel)",
            Creator = "<img src=https://evil.invalid/pixel> www.evil.invalid",
            Source = "https://evil.invalid/source",
            RequiredAttribution = "[remote credit](https://evil.invalid/credit)",
            Modifications = "![remote change](https://evil.invalid/change)",
        };
        var target = Path.Combine(_work, "plain-text-attribution-pack");

        SymbolPackExporter.ExportPack(new MemoryCatalog(provenance, bytes), [provenance.Id], target);

        var markdown = File.ReadAllText(Path.Combine(target, SymbolPackExporter.AttributionsFileName));
        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        Assert.Equal("# Attributions", lines[0]);
        Assert.Equal("```text", lines[2]);
        Assert.Equal("```", lines[^2]);
        Assert.Single(lines[3..^2]);
        Assert.Contains("![tracking image](https://evil.invalid/pixel)", lines[3], StringComparison.Ordinal);
        Assert.Contains("<img src=https://evil.invalid/pixel>", lines[3], StringComparison.Ordinal);
        Assert.Contains("www.evil.invalid", lines[3], StringComparison.Ordinal);
        Assert.Equal(2, lines.Count(line => line.StartsWith("```", StringComparison.Ordinal)));
    }

    [Fact]
    public void Oversized_control_format_or_line_separator_bearing_provenance_is_refused_before_any_write()
    {
        var bytes = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
        var baseline = Provenance("unsafe.optional.v1", "unsafe.svg", bytes);
        var invalid = new[]
        {
            baseline with { AmbiguityNotes = new string('a', 2049) },
            baseline with { RequiredAttribution = "credit\nsecond line" },
            baseline with { Modifications = "change\tchange" },
            baseline with { Creator = "trusted\u202Etxt" },
            baseline with { Source = "source\u2066spoof" },
            baseline with { Creator = "trusted\u200Fspoof" },
            baseline with { AmbiguityNotes = "note\u200Bhidden" },
            baseline with { RequiredAttribution = "credit\u2028second line" },
            baseline with { Modifications = "change\u2029second line" },
            baseline with { Creator = "broken\uD800" },
            baseline with { Creator = "\u200D" },
            baseline with { Source = "\u200C\u200D" },
            baseline with { IntendedMeaning = "\u200D" },
            baseline with { AltText = "\u200C" },
            baseline with { RequiredAttribution = "\u200D" },
            baseline with { Modifications = "\u200C" },
        };

        foreach (var provenance in invalid)
        {
            var target = Path.Combine(_work, Guid.NewGuid().ToString("N"), "pack");
            Assert.Throws<InvalidOperationException>(() => SymbolPackExporter.ExportPack(
                new MemoryCatalog(provenance, bytes), [provenance.Id], target));
            Assert.False(Directory.Exists(target));
        }
    }

    [Fact]
    public void An_existing_destination_is_refused_and_left_exactly_unchanged()
    {
        var bytes = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
        var provenance = Provenance("safe.symbol.v1", "safe.svg", bytes);
        var target = Path.Combine(_work, "pack");
        Directory.CreateDirectory(target);
        var marker = Path.Combine(target, "existing.txt");
        File.WriteAllText(marker, "keep me");

        var exception = Assert.Throws<IOException>(
            () => SymbolPackExporter.ExportPack(new MemoryCatalog(provenance, bytes), [provenance.Id], target));

        Assert.Contains("must not already exist", exception.Message, StringComparison.Ordinal);
        Assert.Equal("keep me", File.ReadAllText(marker));
        Assert.Equal(["existing.txt"], Directory.EnumerateFiles(target).Select(Path.GetFileName));
    }

    [Fact]
    public void A_destination_that_appears_during_preparation_is_preserved_and_the_stage_is_removed()
    {
        var bytes = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
        var provenance = Provenance("safe.raced-symbol.v1", "safe.svg", bytes);
        var target = Path.Combine(_work, "pack");
        var marker = Path.Combine(target, "other-writer.txt");
        var catalog = new MemoryCatalog(
            () =>
            {
                Directory.CreateDirectory(target);
                File.WriteAllText(marker, "other writer");
            },
            provenance,
            bytes);

        var exception = Assert.Throws<IOException>(
            () => SymbolPackExporter.ExportPack(catalog, [provenance.Id], target));

        Assert.Contains("appeared", exception.Message, StringComparison.Ordinal);
        Assert.Equal("other writer", File.ReadAllText(marker));
        Assert.Equal(["other-writer.txt"], Directory.EnumerateFiles(target).Select(Path.GetFileName));
        Assert.DoesNotContain(
            Directory.EnumerateDirectories(_work),
            path => Path.GetFileName(path).EndsWith(".stage", StringComparison.Ordinal));
    }

    private static AssetProvenance Provenance(string id, string fileName, byte[] content)
        => new(
            Id: new AssetId(id),
            ConceptId: id,
            Version: "1.0.0",
            FileName: fileName,
            MimeType: "image/svg+xml",
            Source: "synthetic-test",
            Creator: "test fixture",
            License: "CC0-1.0",
            Sha256: Convert.ToHexString(SHA256.HashData(content)),
            IntendedMeaning: "Synthetic test symbol",
            AltText: "A synthetic geometric test mark",
            Redistributable: true,
            RequiredAttribution: "No attribution required for this synthetic fixture",
            Modifications: "Original synthetic fixture");

    private sealed class MemoryCatalog : IAssetCatalog
    {
        private readonly Dictionary<AssetId, (AssetProvenance Provenance, byte[] Content)> _assets;
        private Action? _beforeRead;

        public MemoryCatalog(AssetProvenance provenance, byte[] content)
            : this((provenance, content))
        {
        }

        public MemoryCatalog(params (AssetProvenance Provenance, byte[] Content)[] assets)
        {
            _assets = assets.ToDictionary(asset => asset.Provenance.Id);
        }

        public MemoryCatalog(Action beforeRead, AssetProvenance provenance, byte[] content)
            : this((provenance, content))
        {
            _beforeRead = beforeRead;
        }

        public IReadOnlyList<AssetProvenance> All => [.. _assets.Values.Select(asset => asset.Provenance)];

        public AssetProvenance? Find(AssetId id)
            => _assets.TryGetValue(id, out var asset) ? asset.Provenance : null;

        public bool TryGetContent(AssetId id, out ReadOnlyMemory<byte> content, out string mimeType)
        {
            _beforeRead?.Invoke();
            _beforeRead = null;

            if (_assets.TryGetValue(id, out var asset))
            {
                content = asset.Content;
                mimeType = asset.Provenance.MimeType;
                return true;
            }

            content = default;
            mimeType = string.Empty;
            return false;
        }
    }
}
