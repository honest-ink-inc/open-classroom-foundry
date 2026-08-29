using System.Text;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Storage;

namespace Foundry.Tests.Integration;

/// <summary>The council's directive made code: teachers add their own symbols, provenance-first, local by default.</summary>
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

    private static SymbolSubmission Submission(string id, string? license = null) => new(
        new AssetId(id),
        IntendedMeaning: "My cup",
        AltText: "A blue cup with two handles",
        Content: Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>"),
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
    public void An_explicit_open_license_makes_a_symbol_shareable()
    {
        var store = new LocalSymbolStore(_directory);

        var provenance = store.Add(Submission("teacher.shared-star.v1", license: "CC-BY-SA-4.0"));

        Assert.True(provenance.Redistributable);
        Assert.Equal("CC-BY-SA-4.0", provenance.License);
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
}
