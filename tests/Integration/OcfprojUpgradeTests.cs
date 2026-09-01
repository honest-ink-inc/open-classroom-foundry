using System.ComponentModel;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Storage;

namespace Foundry.Tests.Integration;

public sealed partial class OcfprojUpgradeTests : IDisposable
{
    private const string PriorEngineVersion = "0.1.0-dev";
    private const string PriorSchemaVersion = "1";
    private const string FrozenFixtureSha256 = "9B592AA00C2CCB31C5678D82C1685DBE99E023FA482AE25F103AE0D50D9A13FD";
    private const string PriorMainFixtureSha256 = "AE0C140FC4FCB1F9A4DF10FBDC32B2FE09384A5481016016B6223EB6DAAF0D5A";
    private const string SourceRelativePath = "prior/assets.ocfproj";
    private static readonly IReadOnlyList<ProjectUpgradeRecipeIdentity> CandidateRecipes =
    [
        new("all-aboard.task-strip", "0.1.0"),
        new(PortableProjectIdentity.RecipeId, PortableProjectIdentity.RecipeVersion),
    ];

    private readonly string _root = Path.Combine(Path.GetTempPath(), "ocf-upgrade-tests", Guid.NewGuid().ToString("N"));
    private readonly string _sourceRoot;
    private readonly string _candidateRoot;
    private readonly string _sourcePath;
    private readonly byte[] _fixtureBytes;
    private readonly byte[] _priorMainFixtureBytes;

    public OcfprojUpgradeTests()
    {
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "OpenClassroomFoundry.slnx")))
        {
            repository = repository.Parent;
        }

        if (repository is null)
        {
            throw new InvalidOperationException("Could not locate the repository root for the frozen upgrade fixture.");
        }

        var fixturePath = Path.Combine(
            repository.FullName,
            "tests",
            "Integration",
            "Fixtures",
            "upgrade",
            "0.1.0-dev-schema-1-assets.ocfproj.base64");
        _fixtureBytes = Convert.FromBase64String(File.ReadAllText(fixturePath));
        Assert.Equal(2138, _fixtureBytes.Length);
        Assert.Equal(FrozenFixtureSha256, Sha256(_fixtureBytes));
        var priorMainFixturePath = Path.Combine(
            repository.FullName,
            "tests",
            "Integration",
            "Fixtures",
            "upgrade",
            "0.7.0-alpha-prior-main-task-strip.ocfproj.base64");
        _priorMainFixtureBytes = Convert.FromBase64String(File.ReadAllText(priorMainFixturePath));
        Assert.Equal(3323, _priorMainFixtureBytes.Length);
        Assert.Equal(PriorMainFixtureSha256, Sha256(_priorMainFixtureBytes));

        _sourceRoot = Path.Combine(_root, "source-library");
        _candidateRoot = Path.Combine(_root, "candidate-library");
        _sourcePath = Path.Combine(_sourceRoot, SourceRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(_sourcePath)!);
        Directory.CreateDirectory(_candidateRoot);
        File.WriteAllBytes(_sourcePath, _fixtureBytes);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
            // Temp cleanup is best-effort.
        }
    }

    [Fact]
    public async Task An_asset_bearing_frozen_prior_package_becomes_a_validated_context_enriched_copy()
    {
        var request = Request(SourceRelativePath, "prepared/assets.ocfproj");

        var receipt = await OcfprojUpgradeService.PrepareCompatibleCopyAsync(request, CancellationToken.None);
        var destination = CandidatePath(request.DestinationRelativePath);

        Assert.Equal(_fixtureBytes, await File.ReadAllBytesAsync(_sourcePath));
        var destinationBytes = await File.ReadAllBytesAsync(destination);
        Assert.NotEqual(_fixtureBytes, destinationBytes);
        OcfprojZipAssertions.HasCanonicalMetadata(destinationBytes);
        Assert.Equal(PriorEngineVersion, receipt.SourceEngineVersion);
        Assert.Equal(PriorSchemaVersion, receipt.SourceSchemaVersion);
        Assert.Equal(FrozenFixtureSha256, receipt.SourceSha256);
        Assert.Equal(EngineIdentity.EngineVersion, receipt.TargetEngineVersion);
        Assert.Equal(EngineIdentity.ProjectSchemaVersion, receipt.TargetSchemaVersion);
        Assert.Equal(Sha256(destinationBytes), receipt.OutputSha256);
        Assert.True(receipt.PackageTransformed);

        var loaded = await OcfprojProjectStore.LoadProjectFileAsync(destination, CancellationToken.None);
        Assert.Equal(ArtifactPurpose.Unknown, loaded.Manifest.Purpose);
        Assert.Equal(["agency.help.v1"], loaded.Manifest.AssetIds);
        Assert.Equal("Synthetic asset compatibility fixture", loaded.Document.Nodes.OfType<Heading>().Single().Text);
        Assert.Contains(loaded.Document.Nodes, node => node is ImageReference image && image.Asset.Value == "agency.help.v1");
        Assert.NotNull(loaded.Validation);
        Assert.NotNull(loaded.RenderProfile);
        Assert.Equal(ArtifactDocumentFingerprint.Compute(loaded.Document), loaded.Validation.ArtifactSha256);
        Assert.Empty(loaded.Validation.UntrustedNoticeCodes);
        Assert.Equal(RenderAudience.Learner, loaded.RenderProfile.Audience);
        Assert.Equal(100, loaded.RenderProfile.TextScalePercent);
        Assert.False(loaded.RenderProfile.TargetLanguageFirst);
        Assert.Empty(Directory.EnumerateFiles(_candidateRoot, "*.upgrade-partial", SearchOption.AllDirectories));
        Assert.False(File.Exists(Path.Combine(_candidateRoot, ".ocf-upgrade-batch.lock")));
    }

    [Fact]
    public async Task A_project_whose_exact_pinned_recipe_is_absent_from_the_candidate_fails_before_output()
    {
        var request = Request(SourceRelativePath, "prepared/assets.ocfproj") with
        {
            CandidateRecipes = [new ProjectUpgradeRecipeIdentity("different.recipe", "9.9.9")],
        };

        var exception = await Assert.ThrowsAsync<ProjectUpgradeException>(
            () => OcfprojUpgradeService.PrepareCompatibleCopyAsync(request, CancellationToken.None));

        Assert.Equal(ProjectUpgradeFailureCodes.CandidateRecipeUnavailable, exception.Code);
        Assert.Equal(_fixtureBytes, await File.ReadAllBytesAsync(_sourcePath));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_candidateRoot));
    }

    [Theory]
    [InlineData("all-aboard.task-strip", "0.1.1")]
    [InlineData("ALL-ABOARD.TASK-STRIP", "0.1.0")]
    public async Task Candidate_recipe_matching_is_exact_for_version_and_case(
        string candidateRecipeId,
        string candidateRecipeVersion)
    {
        var request = Request(SourceRelativePath, "prepared/assets.ocfproj") with
        {
            CandidateRecipes = [new ProjectUpgradeRecipeIdentity(candidateRecipeId, candidateRecipeVersion)],
        };

        var exception = await Assert.ThrowsAsync<ProjectUpgradeException>(
            () => OcfprojUpgradeService.PrepareCompatibleCopyAsync(request, CancellationToken.None));

        Assert.Equal(ProjectUpgradeFailureCodes.CandidateRecipeUnavailable, exception.Code);
        Assert.Equal(_fixtureBytes, await File.ReadAllBytesAsync(_sourcePath));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_candidateRoot));
    }

    [Fact]
    public async Task A_frozen_prior_main_07_package_still_loads_and_prepares_under_the_unchanged_engine_identity()
    {
        const string sourceRelative = "prior/prior-main-07.ocfproj";
        const string destinationRelative = "prepared/prior-main-07.ocfproj";
        var source = SourcePath(sourceRelative);
        await File.WriteAllBytesAsync(source, _priorMainFixtureBytes);

        var loadedSource = await OcfprojProjectStore.LoadProjectFileAsync(source, CancellationToken.None);
        Assert.Equal(EngineIdentity.EngineVersion, loadedSource.Manifest.EngineVersion);
        Assert.Null(loadedSource.Validation);
        Assert.Equal("Watering the class plants", loadedSource.Document.Nodes.OfType<Heading>().Single().Text);

        var request = new ProjectUpgradeRequest(
            _sourceRoot,
            _candidateRoot,
            sourceRelative,
            destinationRelative,
            EngineIdentity.EngineVersion,
            EngineIdentity.ProjectSchemaVersion,
            PriorMainFixtureSha256,
            EngineIdentity.EngineVersion,
            CandidateRecipes);
        var receipt = await OcfprojUpgradeService.PrepareCompatibleCopyAsync(
            request,
            CancellationToken.None);

        Assert.True(receipt.PackageTransformed);
        Assert.Equal(_priorMainFixtureBytes, await File.ReadAllBytesAsync(source));
        var prepared = await OcfprojProjectStore.LoadProjectFileAsync(
            CandidatePath(destinationRelative),
            CancellationToken.None);
        Assert.NotNull(prepared.Validation);
        Assert.NotNull(prepared.RenderProfile);
        Assert.Equal(
            ArtifactDocumentFingerprint.Compute(loadedSource.Document),
            ArtifactDocumentFingerprint.Compute(prepared.Document));
    }

    [Fact]
    public async Task A_two_project_batch_is_sequential_and_byte_deterministic()
    {
        const string secondRelative = "prior/second.ocfproj";
        File.Copy(_sourcePath, SourcePath(secondRelative));
        var batch = new ProjectUpgradeBatchRequest(
            _sourceRoot,
            _candidateRoot,
            EngineIdentity.EngineVersion,
            [
                Item(SourceRelativePath, "one/first.ocfproj"),
                Item(secondRelative, "two/second.ocfproj"),
            ],
            CandidateRecipes);

        var receipt = await OcfprojUpgradeService.PrepareCompatibleBatchAsync(batch, CancellationToken.None);

        Assert.Equal(2, receipt.Projects.Count);
        Assert.Equal(receipt.Projects[0], receipt.Projects[1]);
        var first = await File.ReadAllBytesAsync(CandidatePath("one/first.ocfproj"));
        var second = await File.ReadAllBytesAsync(CandidatePath("two/second.ocfproj"));
        Assert.NotEqual(_fixtureBytes, first);
        Assert.Equal(first, second);
        Assert.All(receipt.Projects, project => Assert.True(project.PackageTransformed));
        Assert.Equal(_fixtureBytes, await File.ReadAllBytesAsync(_sourcePath));
    }

    [Fact]
    public async Task Hard_link_source_aliases_are_refused_and_prior_outputs_are_removed()
    {
        const string aliasRelative = "prior/alias.ocfproj";
        var aliasPath = SourcePath(aliasRelative);
        if (!CreateHardLink(aliasPath, _sourcePath, 0))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        var batch = new ProjectUpgradeBatchRequest(
            _sourceRoot,
            _candidateRoot,
            EngineIdentity.EngineVersion,
            [
                Item(SourceRelativePath, "one.ocfproj"),
                Item(aliasRelative, "two.ocfproj"),
            ],
            CandidateRecipes);

        var exception = await Assert.ThrowsAsync<ProjectUpgradeException>(
            () => OcfprojUpgradeService.PrepareCompatibleBatchAsync(batch, CancellationToken.None));

        Assert.Equal(ProjectUpgradeFailureCodes.AddressInvalid, exception.Code);
        Assert.Equal(_fixtureBytes, await File.ReadAllBytesAsync(_sourcePath));
        Assert.Equal(_fixtureBytes, await File.ReadAllBytesAsync(aliasPath));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_candidateRoot));
    }

    [Fact]
    public async Task A_later_batch_failure_removes_every_output_prepared_by_that_batch()
    {
        const string secondRelative = "prior/second.ocfproj";
        File.Copy(_sourcePath, SourcePath(secondRelative));
        var batch = new ProjectUpgradeBatchRequest(
            _sourceRoot,
            _candidateRoot,
            EngineIdentity.EngineVersion,
            [
                Item(SourceRelativePath, "one.ocfproj"),
                Item(secondRelative, "two.ocfproj") with { SourceSha256 = new string('0', 64) },
            ],
            CandidateRecipes);

        var exception = await Assert.ThrowsAsync<ProjectUpgradeException>(
            () => OcfprojUpgradeService.PrepareCompatibleBatchAsync(batch, CancellationToken.None));

        Assert.Equal(ProjectUpgradeFailureCodes.SourceAddressMismatch, exception.Code);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_candidateRoot));
        Assert.Equal(_fixtureBytes, await File.ReadAllBytesAsync(_sourcePath));
    }

    [Theory]
    [InlineData("missing-asset")]
    [InlineData("bad-provenance")]
    [InlineData("bad-asset-hash")]
    [InlineData("artifact-assets-disagree")]
    [InlineData("active-snapshot")]
    [InlineData("unrelated-safe-snapshot")]
    [InlineData("artifact-path-disagrees")]
    [InlineData("duplicate-entry")]
    [InlineData("traversal-entry")]
    [InlineData("unknown-entry")]
    [InlineData("unknown-purpose")]
    [InlineData("half-context")]
    [InlineData("forged-warning-message")]
    [InlineData("invalid-render-profile")]
    public async Task Hostile_packages_fail_with_one_content_free_code_and_no_candidate(string mutation)
    {
        var relative = $"hostile/{mutation}.ocfproj";
        var path = MutatedSource(relative, archive => ApplyMutation(archive, mutation));
        var sourceBytes = await File.ReadAllBytesAsync(path);
        var request = Request(relative, "candidate.ocfproj") with { SourceSha256 = Sha256(sourceBytes) };

        var exception = await Assert.ThrowsAsync<ProjectUpgradeException>(
            () => OcfprojUpgradeService.PrepareCompatibleCopyAsync(request, CancellationToken.None));

        Assert.Equal(ProjectUpgradeFailureCodes.PackageInvalid, exception.Code);
        Assert.Null(exception.InnerException);
        Assert.DoesNotContain(_root, exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("evil-student-name", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(sourceBytes, await File.ReadAllBytesAsync(path));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_candidateRoot));
    }

    [Fact]
    public async Task An_unknown_schema_is_a_named_missing_route_not_an_attempted_conversion()
    {
        const string relative = "future/schema-2.ocfproj";
        var path = MutatedSource(relative, archive =>
        {
            ReplaceText(archive, "manifest.json", text => text
                .Replace("\"schemaVersion\": \"1\"", "\"schemaVersion\": \"2\"", StringComparison.Ordinal)
                .Replace("\"engineVersion\": \"0.1.0-dev\"", "\"engineVersion\": \"0.2.0-dev\"", StringComparison.Ordinal));
        });
        var sourceBytes = await File.ReadAllBytesAsync(path);
        var request = Request(relative, "future.ocfproj") with
        {
            SourceEngineVersion = "0.2.0-dev",
            SourceSchemaVersion = "2",
            SourceSha256 = Sha256(sourceBytes),
        };

        var exception = await Assert.ThrowsAsync<ProjectUpgradeException>(
            () => OcfprojUpgradeService.PrepareCompatibleCopyAsync(request, CancellationToken.None));

        Assert.Equal(ProjectUpgradeFailureCodes.NoMigrationRoute, exception.Code);
        Assert.Equal(sourceBytes, await File.ReadAllBytesAsync(path));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_candidateRoot));
    }

    [Fact]
    public async Task Mismatched_addresses_have_stable_distinct_content_free_codes()
    {
        var requests = new[]
        {
            (Request(SourceRelativePath, "bad-hash.ocfproj") with { SourceSha256 = new string('0', 64) }, ProjectUpgradeFailureCodes.SourceAddressMismatch),
            (Request(SourceRelativePath, "bad-engine.ocfproj") with { SourceEngineVersion = "0.1.1-dev" }, ProjectUpgradeFailureCodes.SourceVersionMismatch),
            (Request(SourceRelativePath, "bad-schema.ocfproj") with { SourceSchemaVersion = "2" }, ProjectUpgradeFailureCodes.SourceVersionMismatch),
            (Request(SourceRelativePath, "bad-target.ocfproj") with { TargetEngineVersion = "0.7.1-alpha" }, ProjectUpgradeFailureCodes.TargetVersionMismatch),
        };

        foreach (var (request, expectedCode) in requests)
        {
            var exception = await Assert.ThrowsAsync<ProjectUpgradeException>(
                () => OcfprojUpgradeService.PrepareCompatibleCopyAsync(request, CancellationToken.None));
            Assert.Equal(expectedCode, exception.Code);
            Assert.Null(exception.InnerException);
            Assert.Empty(Directory.EnumerateFileSystemEntries(_candidateRoot));
        }
    }

    [Fact]
    public async Task Candidate_root_must_be_existing_empty_and_separate_from_the_source_root()
    {
        await File.WriteAllTextAsync(Path.Combine(_candidateRoot, "sentinel.txt"), "existing candidate evidence");
        var occupied = await Assert.ThrowsAsync<ProjectUpgradeException>(
            () => OcfprojUpgradeService.PrepareCompatibleCopyAsync(
                Request(SourceRelativePath, "candidate.ocfproj"),
                CancellationToken.None));
        Assert.Equal(ProjectUpgradeFailureCodes.CandidateNotEmpty, occupied.Code);
        Assert.Equal("existing candidate evidence", await File.ReadAllTextAsync(Path.Combine(_candidateRoot, "sentinel.txt")));

        File.Delete(Path.Combine(_candidateRoot, "sentinel.txt"));
        var overlapping = Request(SourceRelativePath, "candidate.ocfproj") with { CandidateLibraryRoot = _sourceRoot };
        var overlap = await Assert.ThrowsAsync<ProjectUpgradeException>(
            () => OcfprojUpgradeService.PrepareCompatibleCopyAsync(overlapping, CancellationToken.None));
        Assert.Equal(ProjectUpgradeFailureCodes.RootsOverlap, overlap.Code);

        var nestedCandidate = Path.Combine(_sourceRoot, "empty-candidate");
        Directory.CreateDirectory(nestedCandidate);
        var nested = await Assert.ThrowsAsync<ProjectUpgradeException>(
            () => OcfprojUpgradeService.PrepareCompatibleCopyAsync(
                Request(SourceRelativePath, "candidate.ocfproj") with { CandidateLibraryRoot = nestedCandidate },
                CancellationToken.None));
        Assert.Equal(ProjectUpgradeFailureCodes.RootsOverlap, nested.Code);
        Assert.Equal(_fixtureBytes, await File.ReadAllBytesAsync(_sourcePath));
    }

    [Fact]
    public async Task Concurrent_batches_cannot_share_one_candidate_destination()
    {
        var partialCreated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstTask = OcfprojUpgradeService.PrepareCompatibleBatchAsync(
            Batch(Item(SourceRelativePath, "same.ocfproj")),
            new ProjectUpgradeTestHooks(async (_, _) =>
            {
                partialCreated.TrySetResult();
                await releaseFirst.Task;
            }),
            CancellationToken.None);
        await partialCreated.Task;

        var second = await Assert.ThrowsAsync<ProjectUpgradeException>(
            () => OcfprojUpgradeService.PrepareCompatibleCopyAsync(
                Request(SourceRelativePath, "same.ocfproj"),
                CancellationToken.None));
        releaseFirst.TrySetResult();
        var first = await firstTask;

        Assert.Equal(ProjectUpgradeFailureCodes.CandidateBusy, second.Code);
        Assert.Single(first.Projects);
        Assert.True(first.Projects[0].PackageTransformed);
        var loaded = await OcfprojProjectStore.LoadProjectFileAsync(
            CandidatePath("same.ocfproj"),
            CancellationToken.None);
        Assert.NotNull(loaded.Validation);
        Assert.NotNull(loaded.RenderProfile);
    }

    [Fact]
    public async Task A_context_bearing_package_stays_byte_identical_and_reports_no_transform()
    {
        var firstReceipt = await OcfprojUpgradeService.PrepareCompatibleCopyAsync(
            Request(SourceRelativePath, "enriched.ocfproj"),
            CancellationToken.None);
        Assert.True(firstReceipt.PackageTransformed);
        var enrichedBytes = await File.ReadAllBytesAsync(CandidatePath("enriched.ocfproj"));

        var secondSourceRoot = Path.Combine(_root, "enriched-source");
        var secondCandidateRoot = Path.Combine(_root, "enriched-candidate");
        Directory.CreateDirectory(secondSourceRoot);
        Directory.CreateDirectory(secondCandidateRoot);
        var secondSource = Path.Combine(secondSourceRoot, "enriched.ocfproj");
        await File.WriteAllBytesAsync(secondSource, enrichedBytes);
        var request = new ProjectUpgradeRequest(
            secondSourceRoot,
            secondCandidateRoot,
            "enriched.ocfproj",
            "prepared.ocfproj",
            PriorEngineVersion,
            PriorSchemaVersion,
            Sha256(enrichedBytes),
            EngineIdentity.EngineVersion,
            CandidateRecipes);

        var secondReceipt = await OcfprojUpgradeService.PrepareCompatibleCopyAsync(
            request,
            CancellationToken.None);
        var prepared = await File.ReadAllBytesAsync(Path.Combine(secondCandidateRoot, "prepared.ocfproj"));

        Assert.False(secondReceipt.PackageTransformed);
        Assert.Equal(enrichedBytes, prepared);
        Assert.Equal(Sha256(enrichedBytes), secondReceipt.OutputSha256);
        Assert.Equal(enrichedBytes, await File.ReadAllBytesAsync(secondSource));
    }

    [Fact]
    public async Task Cancellation_after_partial_creation_removes_partial_lock_and_candidate_directories()
    {
        using var cancellation = new CancellationTokenSource();
        var hooks = new ProjectUpgradeTestHooks((_, _) =>
        {
            cancellation.Cancel();
            return Task.CompletedTask;
        });

        var exception = await Assert.ThrowsAsync<ProjectUpgradeException>(
            () => OcfprojUpgradeService.PrepareCompatibleBatchAsync(
                Batch(Item(SourceRelativePath, "nested/canceled.ocfproj")),
                hooks,
                cancellation.Token));

        Assert.Equal(ProjectUpgradeFailureCodes.Canceled, exception.Code);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_candidateRoot));
        Assert.Equal(_fixtureBytes, await File.ReadAllBytesAsync(_sourcePath));
    }

    [Fact]
    public async Task Cleanup_residue_preserves_the_primary_code_without_exposing_a_path()
    {
        using var cancellation = new CancellationTokenSource();
        var hooks = new ProjectUpgradeTestHooks(
            (_, _) =>
            {
                cancellation.Cancel();
                return Task.CompletedTask;
            },
            path => path.EndsWith(".upgrade-partial", StringComparison.Ordinal)
                ? new IOException("simulated cleanup failure with forbidden raw detail")
                : null);

        var exception = await Assert.ThrowsAsync<ProjectUpgradeException>(
            () => OcfprojUpgradeService.PrepareCompatibleBatchAsync(
                Batch(Item(SourceRelativePath, "residue.ocfproj")),
                hooks,
                cancellation.Token));

        Assert.Equal(ProjectUpgradeFailureCodes.CleanupResidue, exception.Code);
        Assert.Equal(ProjectUpgradeFailureCodes.Canceled, exception.PrimaryCode);
        Assert.Equal(1, exception.CleanupResidueCount);
        Assert.DoesNotContain(_root, exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("forbidden raw detail", exception.Message, StringComparison.Ordinal);
        Assert.Single(Directory.EnumerateFiles(_candidateRoot, "*.upgrade-partial", SearchOption.AllDirectories));
        Assert.False(File.Exists(Path.Combine(_candidateRoot, ".ocf-upgrade-batch.lock")));
    }

    [Fact]
    public async Task Source_is_held_read_only_for_the_entire_address_validate_copy_sequence()
    {
        var writeWasRefused = false;
        var hooks = new ProjectUpgradeTestHooks((_, _) =>
        {
            try
            {
                using var writer = new FileStream(_sourcePath, FileMode.Open, FileAccess.Write, FileShare.None);
            }
            catch (IOException)
            {
                writeWasRefused = true;
            }

            return Task.CompletedTask;
        });

        await OcfprojUpgradeService.PrepareCompatibleBatchAsync(
            Batch(Item(SourceRelativePath, "held.ocfproj")),
            hooks,
            CancellationToken.None);

        Assert.True(writeWasRefused);
        Assert.Equal(_fixtureBytes, await File.ReadAllBytesAsync(_sourcePath));
    }

    private ProjectUpgradeRequest Request(string sourceRelativePath, string destinationRelativePath)
        => new(
            _sourceRoot,
            _candidateRoot,
            sourceRelativePath,
            destinationRelativePath,
            PriorEngineVersion,
            PriorSchemaVersion,
            FrozenFixtureSha256,
            EngineIdentity.EngineVersion,
            CandidateRecipes);

    private static ProjectUpgradeItem Item(string sourceRelativePath, string destinationRelativePath)
        => new(
            sourceRelativePath,
            destinationRelativePath,
            PriorEngineVersion,
            PriorSchemaVersion,
            FrozenFixtureSha256);

    private ProjectUpgradeBatchRequest Batch(params ProjectUpgradeItem[] items)
        => new(_sourceRoot, _candidateRoot, EngineIdentity.EngineVersion, items, CandidateRecipes);

    private string SourcePath(string relativePath)
    {
        var path = Path.Combine(_sourceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return path;
    }

    private string CandidatePath(string relativePath)
        => Path.Combine(_candidateRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private string MutatedSource(string relativePath, Action<ZipArchive> mutate)
    {
        var path = SourcePath(relativePath);
        File.Copy(_sourcePath, path);
        using var archive = ZipFile.Open(path, ZipArchiveMode.Update);
        mutate(archive);
        return path;
    }

    private static void ApplyMutation(ZipArchive archive, string mutation)
    {
        switch (mutation)
        {
            case "missing-asset":
                archive.GetEntry("assets/help.svg")!.Delete();
                break;
            case "bad-provenance":
                ReplaceBytes(archive, "provenance/agency.help.v1.json", "{}"u8.ToArray());
                break;
            case "bad-asset-hash":
                ReplaceBytes(archive, "assets/help.svg", "changed asset bytes"u8.ToArray());
                break;
            case "artifact-assets-disagree":
                ReplaceText(archive, "artifact.json", text => text.Replace("agency.help.v1", "agency.ghost.v1", StringComparison.Ordinal));
                break;
            case "active-snapshot":
                ReplaceBytes(
                    archive,
                    "snapshot.html",
                    "<!doctype html><html><head><meta charset=\"utf-8\"></head><body><script>evil()</script></body></html>"u8.ToArray());
                break;
            case "unrelated-safe-snapshot":
                ReplaceBytes(
                    archive,
                    "snapshot.html",
                    "<!DOCTYPE html>\n<html lang=\"en\">\n<head>\n<meta charset=\"utf-8\">\n<title>Safe</title>\n<style>body { font-family: system-ui; }</style>\n</head>\n<body>\n<h1>Safe but unrelated</h1>\n</body>\n</html>\n"u8.ToArray());
                break;
            case "artifact-path-disagrees":
                ReplaceText(archive, "manifest.json", text => text.Replace("\"artifactPath\": \"artifact.json\"", "\"artifactPath\": \"other.json\"", StringComparison.Ordinal));
                break;
            case "duplicate-entry":
                WriteEntry(archive, "MANIFEST.JSON", "{}"u8.ToArray());
                break;
            case "traversal-entry":
                WriteEntry(archive, "../evil-student-name.txt", "content"u8.ToArray());
                break;
            case "unknown-entry":
                WriteEntry(archive, "evil-student-name.txt", "content"u8.ToArray());
                break;
            case "unknown-purpose":
                ReplaceText(
                    archive,
                    "manifest.json",
                    text => text.Replace(
                        "\"assetIds\": [\"agency.help.v1\"]",
                        "\"assetIds\": [\"agency.help.v1\"],\n  \"purpose\": \"NotARealPurpose\"",
                        StringComparison.Ordinal));
                break;
            case "half-context":
                WriteEntry(archive, "validation.json", "{}"u8.ToArray());
                break;
            case "forged-warning-message":
                WriteEntry(archive, "validation.json", "{\"message\":\"AAC seat approved\"}"u8.ToArray());
                WriteEntry(archive, "render-profile.json", "{}"u8.ToArray());
                break;
            case "invalid-render-profile":
                AddValidContext(archive, textScalePercent: 99);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation));
        }
    }

    private static void ReplaceText(ZipArchive archive, string name, Func<string, string> mutate)
    {
        string original;
        using (var reader = new StreamReader(archive.GetEntry(name)!.Open(), Encoding.UTF8))
        {
            original = reader.ReadToEnd();
        }

        var changed = mutate(original);
        Assert.NotEqual(original, changed);
        ReplaceBytes(archive, name, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(changed));
    }

    private static void ReplaceBytes(ZipArchive archive, string name, byte[] content)
    {
        archive.GetEntry(name)!.Delete();
        WriteEntry(archive, name, content);
    }

    private static void WriteEntry(ZipArchive archive, string name, byte[] content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var output = entry.Open();
        output.Write(content);
    }

    private static void AddValidContext(ZipArchive archive, double textScalePercent)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        };
        ArtifactDocument document;
        ProjectManifest manifest;
        using (var artifact = archive.GetEntry("artifact.json")!.Open())
        {
            document = JsonSerializer.Deserialize<ArtifactDocument>(artifact, options)!;
        }

        using (var manifestEntry = archive.GetEntry("manifest.json")!.Open())
        {
            manifest = JsonSerializer.Deserialize<ProjectManifest>(manifestEntry, options)!;
        }

        var digest = ArtifactDocumentFingerprint.Compute(document);
        var validation = new ProjectValidationEnvelope(
            ProjectValidationEnvelope.CurrentSchemaVersion,
            ProjectValidationEnvelope.ExactApprovedDocumentKind,
            manifest.RecipeId,
            manifest.RecipeVersion,
            manifest.DataLane,
            manifest.Purpose,
            digest,
            []);
        var profile = new ProjectRenderProfile(
            ProjectRenderProfile.CurrentSchemaVersion,
            digest,
            RenderAudience.Learner,
            textScalePercent,
            false);
        WriteEntry(archive, "validation.json", JsonSerializer.SerializeToUtf8Bytes(validation, options));
        WriteEntry(archive, "render-profile.json", JsonSerializer.SerializeToUtf8Bytes(profile, options));
    }

    private static string Sha256(byte[] content)
        => Convert.ToHexString(SHA256.HashData(content));

    [LibraryImport(
        "kernel32.dll",
        EntryPoint = "CreateHardLinkW",
        StringMarshalling = StringMarshalling.Utf16,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CreateHardLink(
        string fileName,
        string existingFileName,
        nint securityAttributes);
}
