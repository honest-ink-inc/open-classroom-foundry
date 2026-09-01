using System.Text;
using System.Text.Json;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Storage;

namespace Foundry.Tests.Integration;

/// <summary>A product-owner-adopted rehearsal finding made code: teachers add their own symbols, provenance-first, local by default. This is not council or AAC/SLP approval.</summary>
public class LocalSymbolStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ocf-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        try
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Temp cleanup is best-effort.
        }
    }

    private static SymbolSubmission Submission(
        string id,
        string? license = null,
        byte[]? content = null) => new(
        new AssetId(id),
        IntendedMeaning: "My cup",
        AltText: "A blue cup with two handles",
        Content: content ?? Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>"),
        MimeType: "image/svg+xml",
        TeacherStatedRights: "My own photograph, taken by me",
        AmbiguityNotes: "Specific to one learner's actual cup",
        License: license);

    [Fact]
    public void A_teacher_added_symbol_is_local_by_default_and_never_redistributable()
    {
        var store = new LocalSymbolStore(_directory);

        var provenance = store.Add(Submission("teacher.my-cup.v1"));

        Assert.False(provenance.Redistributable);
        Assert.Equal("LicenseRef-teacher-local", provenance.License);
        Assert.Equal("teacher-created", provenance.Source);
        Assert.True(store.TryGetContent(provenance.Id, out var content, out var mime));
        Assert.Equal("image/svg+xml", mime);
        Assert.NotEqual(0, content.Length);
    }

    [Fact]
    public void An_explicit_open_license_records_posture_without_fabricating_export_dispositions()
    {
        var store = new LocalSymbolStore(_directory);

        var provenance = store.Add(Submission("teacher.shared-star.v1", license: "CC-BY-SA-4.0"));

        Assert.True(provenance.Redistributable);
        Assert.Equal("CC-BY-SA-4.0", provenance.License);
        Assert.Null(provenance.RequiredAttribution);
        Assert.Null(provenance.Modifications);
    }

    [Fact]
    public void The_shelf_persists_across_sessions()
    {
        new LocalSymbolStore(_directory).Add(Submission("teacher.my-cup.v1"));

        var reopened = new LocalSymbolStore(_directory);

        var found = reopened.Find(new AssetId("teacher.my-cup.v1"));
        Assert.NotNull(found);
        Assert.Equal("My cup", found.IntendedMeaning);
    }

    [Fact]
    public void Duplicates_are_refused_version_dont_overwrite()
    {
        var store = new LocalSymbolStore(_directory);
        store.Add(Submission("teacher.my-cup.v1"));

        Assert.Throws<InvalidOperationException>(() => store.Add(Submission("teacher.my-cup.v1")));
    }

    [Fact]
    public void Missing_meaning_alt_text_or_rights_is_refused()
    {
        var store = new LocalSymbolStore(_directory);

        Assert.Throws<ArgumentException>(() => store.Add(Submission("x") with { AltText = " " }));
        Assert.Throws<ArgumentException>(() => store.Add(Submission("x") with { TeacherStatedRights = " " }));
        Assert.Throws<ArgumentException>(() => store.Add(Submission("x") with { MimeType = "application/pdf" }));
    }

    [Fact]
    public void The_shelf_has_the_same_tamper_check_as_the_pack()
    {
        var store = new LocalSymbolStore(_directory);
        var provenance = store.Add(Submission("teacher.my-cup.v1"));

        Assert.Empty(store.VerifyIntegrity());

        File.AppendAllText(Path.Combine(_directory, provenance.FileName), "<!-- drift -->");

        Assert.Contains(store.VerifyIntegrity(), i => i.Code == "asset.hash-mismatch");
    }

    [Fact]
    public void Tampered_content_is_not_returned_even_before_the_caller_runs_an_integrity_report()
    {
        var store = new LocalSymbolStore(_directory);
        var provenance = store.Add(Submission("teacher.my-cup.v1"));
        File.AppendAllText(Path.Combine(_directory, provenance.FileName), "<!-- drift -->");

        var found = store.TryGetContent(provenance.Id, out var content, out var mimeType);

        Assert.False(found);
        Assert.True(content.IsEmpty);
        Assert.Empty(mimeType);
    }

    [Fact]
    public void A_reopened_shelf_fails_closed_when_an_asset_hash_no_longer_matches()
    {
        var store = new LocalSymbolStore(_directory);
        var provenance = store.Add(Submission("teacher.my-cup.v1"));
        File.AppendAllText(Path.Combine(_directory, provenance.FileName), "<!-- drift -->");

        var exception = Assert.Throws<InvalidDataException>(() => new LocalSymbolStore(_directory));

        Assert.Contains("asset.hash-mismatch", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_reopened_shelf_rejects_a_manifest_controlled_path_escape()
    {
        var store = new LocalSymbolStore(_directory);
        var provenance = store.Add(Submission("teacher.my-cup.v1"));
        RewriteManifest([provenance with { FileName = "../outside.svg" }]);

        var exception = Assert.Throws<InvalidDataException>(() => new LocalSymbolStore(_directory));

        Assert.Contains("asset.invalid-file-name", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_reopened_shelf_rejects_portable_filename_collisions()
    {
        var store = new LocalSymbolStore(_directory);
        var provenance = store.Add(Submission("teacher.my-cup.v1"));
        RewriteManifest(
        [
            provenance,
            provenance with
            {
                Id = new AssetId("teacher.other-cup.v1"),
                FileName = provenance.FileName.ToUpperInvariant(),
            },
        ]);

        var exception = Assert.Throws<InvalidDataException>(() => new LocalSymbolStore(_directory));

        Assert.Contains("asset.file-name-collision", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_mutable_manifest_cannot_mark_teacher_local_rights_as_redistributable()
    {
        var store = new LocalSymbolStore(_directory);
        var provenance = store.Add(Submission("teacher.my-cup.v1"));
        Assert.Equal("LicenseRef-teacher-local", provenance.License);
        RewriteManifest([provenance with { Redistributable = true }]);

        var exception = Assert.Throws<InvalidDataException>(() => new LocalSymbolStore(_directory));

        Assert.Contains("asset.redistribution-rights", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Different_ids_that_sanitize_to_the_same_filename_are_refused_without_overwrite()
    {
        var store = new LocalSymbolStore(_directory);
        var first = store.Add(Submission("teacher.a/b.v1"));
        var firstBytes = File.ReadAllBytes(Path.Combine(_directory, first.FileName));
        var colliding = Submission(
            "teacher.ab.v1",
            content: Encoding.UTF8.GetBytes(
                "<svg xmlns=\"http://www.w3.org/2000/svg\"><title>different</title></svg>"));

        var exception = Assert.Throws<InvalidOperationException>(() => store.Add(colliding));

        Assert.Contains("collides", exception.Message, StringComparison.Ordinal);
        Assert.Equal(firstBytes, File.ReadAllBytes(Path.Combine(_directory, first.FileName)));
        Assert.Single(store.All);
    }

    [Fact]
    public void A_stale_store_refuses_instead_of_overwriting_a_newer_manifest()
    {
        var first = new LocalSymbolStore(_directory);
        var stale = new LocalSymbolStore(_directory);
        var firstAsset = first.Add(Submission("teacher.first.v1"));

        var refusal = Assert.Throws<InvalidOperationException>(() =>
            stale.Add(Submission("teacher.stale.v1")));

        Assert.Contains("changed after it was opened", refusal.Message, StringComparison.Ordinal);
        var reopened = new LocalSymbolStore(_directory);
        Assert.NotNull(reopened.Find(firstAsset.Id));
        Assert.Null(reopened.Find(new AssetId("teacher.stale.v1")));
        Assert.False(File.Exists(Path.Combine(_directory, "teacher.stale.v1.svg")));
    }

    [Fact]
    public void A_manifest_that_grows_past_its_bound_is_refused_before_a_stale_store_writes()
    {
        var store = new LocalSymbolStore(_directory);
        Directory.CreateDirectory(_directory);
        File.WriteAllBytes(
            Path.Combine(_directory, LocalSymbolStore.ManifestFileName),
            new byte[AssetManifestReader.MaxManifestBytes + 1]);

        var exception = Assert.Throws<InvalidDataException>(() =>
            store.Add(Submission("teacher.must-not-write.v1")));

        Assert.Contains(
            AssetManifestReader.MaxManifestBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
            exception.Message,
            StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(_directory, "teacher.must-not-write.v1.svg")));
    }

    [Fact]
    public void Control_bearing_optional_provenance_fails_closed_when_a_shelf_reopens()
    {
        var store = new LocalSymbolStore(_directory);
        var provenance = store.Add(Submission("teacher.my-cup.v1"));
        RewriteManifest([provenance with { RequiredAttribution = "credit\nsecond line" }]);

        var exception = Assert.Throws<InvalidDataException>(() => new LocalSymbolStore(_directory));

        Assert.Contains("asset.invalid-optional-provenance", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Active_svg_content_is_refused_before_it_reaches_the_shelf()
    {
        var store = new LocalSymbolStore(_directory);
        var active = Submission(
            "teacher.active.v1",
            content: Encoding.UTF8.GetBytes(
                "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>"));

        Assert.Throws<ArgumentException>(() => store.Add(active));
        Assert.Empty(store.All);
        Assert.False(File.Exists(Path.Combine(_directory, "teacher.active.v1.svg")));
    }

    [Fact]
    public void A_caller_cannot_mutate_the_preflight_capability_through_a_content_copy()
    {
        var submission = Submission("teacher.defensive-copy.v1");
        var firstCopy = submission.CopyContent();
        var originalFirstByte = firstCopy[0];

        firstCopy[0] ^= byte.MaxValue;

        Assert.Equal(originalFirstByte, submission.CopyContent()[0]);
    }

    [Fact]
    public void The_public_shelf_has_no_raw_byte_mutation_route_around_the_capability()
    {
        var publicAdds = typeof(LocalSymbolStore)
            .GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
            .Where(method => method.Name == nameof(LocalSymbolStore.Add))
            .ToArray();

        var add = Assert.Single(publicAdds);
        var parameter = Assert.Single(add.GetParameters());
        Assert.Equal(typeof(SymbolSubmission), parameter.ParameterType);
    }

    [Fact]
    public void The_composite_catalog_resolves_pack_and_shelf_together()
    {
        var repo = new DirectoryInfo(AppContext.BaseDirectory);
        while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "OpenClassroomFoundry.slnx")))
        {
            repo = repo.Parent;
        }

        var pack = new JsonAssetCatalog(Path.Combine(repo!.FullName, "assets", "symbols"));
        var shelf = new LocalSymbolStore(_directory);
        shelf.Add(Submission("teacher.my-cup.v1"));

        var composite = new CompositeAssetCatalog(pack, shelf);

        Assert.NotNull(composite.Find(new AssetId("agency.stop.v1")));
        Assert.NotNull(composite.Find(new AssetId("teacher.my-cup.v1")));
        Assert.True(composite.TryGetContent(new AssetId("teacher.my-cup.v1"), out _, out _));
        Assert.Equal(pack.All.Count + 1, composite.All.Count);
    }

    [Fact]
    public void A_composite_catalog_refuses_an_exact_identity_collision()
    {
        var first = SyntheticProvenance("family-a.shared.v1");
        var second = SyntheticProvenance("family-a.shared.v1") with { Source = "family-b" };

        var exception = Assert.Throws<InvalidDataException>(() => new CompositeAssetCatalog(
            new SyntheticCatalog(first),
            new SyntheticCatalog(second)));

        Assert.Contains("collides", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_composite_catalog_refuses_a_portable_case_confusable_identity_collision()
    {
        var first = SyntheticProvenance("family-a.shared.v1");
        var second = SyntheticProvenance("FAMILY-A.SHARED.V1") with { Source = "family-b" };

        var exception = Assert.Throws<InvalidDataException>(() => new CompositeAssetCatalog(
            new SyntheticCatalog(first),
            new SyntheticCatalog(second)));

        Assert.Contains("collides", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_composite_catalog_snapshots_each_inventory_once_and_preserves_declared_order()
    {
        var first = SyntheticProvenance("family-a.first.v1");
        var second = SyntheticProvenance("family-a.second.v1");
        var changing = new ReorderingSyntheticCatalog(first, second);

        var composite = new CompositeAssetCatalog(changing);
        var firstRead = composite.All.Select(asset => asset.Id).ToArray();
        var secondRead = composite.All.Select(asset => asset.Id).ToArray();

        Assert.Equal([first.Id, second.Id], firstRead);
        Assert.Equal(firstRead, secondRead);
        Assert.Equal(1, changing.AllReads);
        Assert.Equal(second, composite.Find(second.Id));
        Assert.True(composite.TryGetContent(second.Id, out var content, out var mimeType));
        Assert.Equal("image/svg+xml", mimeType);
        Assert.NotEmpty(content.ToArray());
    }

    private void RewriteManifest(IReadOnlyList<AssetProvenance> records)
    {
        File.WriteAllText(
            Path.Combine(_directory, LocalSymbolStore.ManifestFileName),
            JsonSerializer.Serialize(records, StorageJson.Options));
    }

    private static AssetProvenance SyntheticProvenance(string id) => new(
        new AssetId(id),
        ConceptId: "synthetic.concept",
        Version: "1.0.0",
        FileName: $"{id}.svg",
        MimeType: "image/svg+xml",
        Source: "family-a",
        Creator: "Synthetic test fixture",
        License: "CC0-1.0",
        Sha256: new string('A', 64),
        IntendedMeaning: id,
        AltText: $"Synthetic symbol {id}",
        Redistributable: true);

    private sealed class SyntheticCatalog(params AssetProvenance[] assets) : IAssetCatalog
    {
        private static readonly byte[] Svg = Encoding.UTF8.GetBytes(
            "<svg xmlns=\"http://www.w3.org/2000/svg\"/>");

        public IReadOnlyList<AssetProvenance> All { get; } = [.. assets];

        public AssetProvenance? Find(AssetId id)
            => All.SingleOrDefault(asset => asset.Id == id);

        public bool TryGetContent(AssetId id, out ReadOnlyMemory<byte> content, out string mimeType)
        {
            if (Find(id) is not null)
            {
                content = Svg;
                mimeType = "image/svg+xml";
                return true;
            }

            content = default;
            mimeType = string.Empty;
            return false;
        }
    }

    private sealed class ReorderingSyntheticCatalog(params AssetProvenance[] assets) : IAssetCatalog
    {
        private readonly SyntheticCatalog _content = new(assets);
        private readonly AssetProvenance[] _assets = [.. assets];

        public int AllReads { get; private set; }

        public IReadOnlyList<AssetProvenance> All
            => ++AllReads % 2 == 1 ? [.. _assets] : [.. _assets.Reverse()];

        public AssetProvenance? Find(AssetId id) => _content.Find(id);

        public bool TryGetContent(AssetId id, out ReadOnlyMemory<byte> content, out string mimeType)
            => _content.TryGetContent(id, out content, out mimeType);
    }
}
