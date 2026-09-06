// SPDX-License-Identifier: GPL-3.0-or-later
using System.IO.Compression;
using System.Text.Json;
using Foundry.Contracts;
using Foundry.Storage;

namespace Foundry.Tests.Integration;

public sealed partial class OcfprojUpgradeTests
{
    [Fact]
    public async Task Exact_C1_sample_opens_and_two_prepared_copies_preserve_every_original_member()
    {
        var firstSource = SourcePath("prior/c1-a.ocfproj");
        var secondSource = SourcePath("prior/c1-b.ocfproj");
        await File.WriteAllBytesAsync(firstSource, _firstAdmissionFixtureBytes);
        await File.WriteAllBytesAsync(secondSource, _firstAdmissionFixtureBytes);
        var original = await OcfprojProjectStore.LoadProjectFileAsync(firstSource, CancellationToken.None);
        Assert.Equal("0.7.0-alpha", original.Manifest.EngineVersion);
        Assert.Equal("1", original.Manifest.SchemaVersion);
        Assert.Equal("all-aboard.task-strip", original.Manifest.RecipeId);
        Assert.Equal("0.1.0", original.Manifest.RecipeVersion);
        Assert.Null(original.Validation);
        Assert.Null(original.RenderProfile);

        var batch = Batch(
            FirstAdmissionItem("prior/c1-a.ocfproj", "prepared/c1-a.ocfproj"),
            FirstAdmissionItem("prior/c1-b.ocfproj", "prepared/c1-b.ocfproj"));
        var result = await OcfprojUpgradeService.PrepareCompatibleBatchAsync(batch, CancellationToken.None);
        Assert.Equal(2, result.Projects.Count);
        Assert.All(result.Projects, receipt =>
        {
            Assert.True(receipt.PackageTransformed);
            Assert.Equal(FirstAdmissionFixtureSha256, receipt.SourceSha256);
            Assert.Equal("0.7.0-alpha", receipt.SourceEngineVersion);
            Assert.Equal("0.8.0-alpha", receipt.TargetEngineVersion);
            Assert.Equal("1", receipt.SourceSchemaVersion);
            Assert.Equal("1", receipt.TargetSchemaVersion);
        });

        var first = await File.ReadAllBytesAsync(CandidatePath("prepared/c1-a.ocfproj"));
        var second = await File.ReadAllBytesAsync(CandidatePath("prepared/c1-b.ocfproj"));
        Assert.Equal(first, second);
        Assert.Equal(Sha256(first), result.Projects[0].OutputSha256);
        OcfprojZipAssertions.HasCanonicalMetadata(first);
        using var originalStream = new MemoryStream(_firstAdmissionFixtureBytes);
        using var copyStream = new MemoryStream(first);
        using var originalArchive = new ZipArchive(originalStream, ZipArchiveMode.Read);
        using var copyArchive = new ZipArchive(copyStream, ZipArchiveMode.Read);
        Assert.Equal(originalArchive.Entries.Select(entry => entry.FullName)
                .Concat(["validation.json", "render-profile.json"]).Order(StringComparer.Ordinal),
            copyArchive.Entries.Select(entry => entry.FullName).Order(StringComparer.Ordinal));
        foreach (var entry in originalArchive.Entries)
        {
            var copied = Assert.Single(copyArchive.Entries, candidate => candidate.FullName == entry.FullName);
            Assert.Equal(entry.LastWriteTime, copied.LastWriteTime);
            Assert.Equal(await ReadFrozenMember(entry), await ReadFrozenMember(copied));
        }

        var prepared = await OcfprojProjectStore.LoadProjectFileAsync(
            CandidatePath("prepared/c1-a.ocfproj"), CancellationToken.None);
        Assert.Equal(JsonSerializer.Serialize(original.Manifest), JsonSerializer.Serialize(prepared.Manifest));
        Assert.Equal(ArtifactDocumentFingerprint.Compute(original.Document),
            ArtifactDocumentFingerprint.Compute(prepared.Document));
        Assert.NotNull(prepared.Validation);
        Assert.NotNull(prepared.RenderProfile);
        Assert.Equal(_firstAdmissionFixtureBytes, await File.ReadAllBytesAsync(firstSource));
        Assert.Equal(_firstAdmissionFixtureBytes, await File.ReadAllBytesAsync(secondSource));
        // The candidate adds context beside the historical source; it does not
        // rewrite the original writer identity or authorize a release/schema 2.
        var reopened = await OcfprojProjectStore.LoadProjectFileAsync(firstSource, CancellationToken.None);
        Assert.Equal(JsonSerializer.Serialize(original.Manifest), JsonSerializer.Serialize(reopened.Manifest));
        Assert.Null(reopened.Validation);
    }

    [Fact]
    public async Task Exact_C1_sample_requires_its_outgoing_recipe_even_when_a_replacement_is_available()
    {
        var source = SourcePath("prior/c1.ocfproj");
        await File.WriteAllBytesAsync(source, _firstAdmissionFixtureBytes);
        var batch = Batch(FirstAdmissionItem("prior/c1.ocfproj", "prepared/c1.ocfproj")) with
        {
            CandidateRecipes = [new ProjectUpgradeRecipeIdentity("all-aboard.task-strip", "0.2.0")],
        };
        var refusal = await Assert.ThrowsAsync<ProjectUpgradeException>(() =>
            OcfprojUpgradeService.PrepareCompatibleBatchAsync(batch, CancellationToken.None));
        Assert.Equal(ProjectUpgradeFailureCodes.CandidateRecipeUnavailable, refusal.Code);
        Assert.Equal(_firstAdmissionFixtureBytes, await File.ReadAllBytesAsync(source));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_candidateRoot));
    }

    [Fact]
    public async Task Exact_C1_batch_failure_rolls_back_only_candidate_outputs_and_leaves_sources_reopenable()
    {
        var firstSource = SourcePath("prior/c1-a.ocfproj");
        var secondSource = SourcePath("prior/c1-b.ocfproj");
        await File.WriteAllBytesAsync(firstSource, _firstAdmissionFixtureBytes);
        await File.WriteAllBytesAsync(secondSource, _firstAdmissionFixtureBytes);
        var batch = Batch(
            FirstAdmissionItem("prior/c1-a.ocfproj", "prepared/c1-a.ocfproj"),
            FirstAdmissionItem("prior/c1-b.ocfproj", "prepared/c1-b.ocfproj") with { SourceSha256 = new string('0', 64) });
        var refusal = await Assert.ThrowsAsync<ProjectUpgradeException>(() =>
            OcfprojUpgradeService.PrepareCompatibleBatchAsync(batch, CancellationToken.None));
        Assert.Equal(ProjectUpgradeFailureCodes.SourceAddressMismatch, refusal.Code);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_candidateRoot));
        foreach (var source in new[] { firstSource, secondSource })
        {
            Assert.Equal(_firstAdmissionFixtureBytes, await File.ReadAllBytesAsync(source));
            var reopened = await OcfprojProjectStore.LoadProjectFileAsync(source, CancellationToken.None);
            Assert.Equal("0.7.0-alpha", reopened.Manifest.EngineVersion);
            Assert.Equal("0.1.0", reopened.Manifest.RecipeVersion);
            Assert.Null(reopened.Validation);
        }
    }

    private static ProjectUpgradeItem FirstAdmissionItem(string source, string destination)
        => new(source, destination, "0.7.0-alpha", "1", FirstAdmissionFixtureSha256);

    private static async Task<byte[]> ReadFrozenMember(ZipArchiveEntry entry)
    {
        await using var input = entry.Open();
        using var result = new MemoryStream();
        await input.CopyToAsync(result);
        return result.ToArray();
    }
}
