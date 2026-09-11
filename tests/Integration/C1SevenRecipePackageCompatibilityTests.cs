// SPDX-License-Identifier: GPL-3.0-or-later
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.BuiltIn;
using Foundry.Modules.DeterministicPress;
using Foundry.Rendering;
using Foundry.Storage;

namespace Foundry.Tests.Integration;

/// <summary>
/// Consumes genuine, separately captured C1 synthetic packages; it never creates
/// replacement bytes for a missing fixture. Package labels remain mutable data,
/// not construction authority, source authenticity or a fresh typed approval.
/// </summary>
public sealed class C1SevenRecipePackageCompatibilityTests : IDisposable
{
    private const string C1Commit = "5cae09dcb40628265d51912aea98304557abfda6";
    private const string OriginalEngine = "0.7.0-alpha";
    private const string HistoricalVersion = "0.1.0";
    private static readonly DateTimeOffset OriginalInstant = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly string[] ContextlessMembers = ["artifact.json", "manifest.json", "snapshot.html"];
    private static readonly string[] ContextMembers = ["artifact.json", "manifest.json", "render-profile.json", "snapshot.html", "validation.json"];
    private static readonly string[] AddedContextMembers = ["validation.json", "render-profile.json"];

    // Exact earlier C1 default document/manifest observations bound by the
    // reviewed package producer. These are NOT hashes of forthcoming packages.
    private static readonly CaseDefinition[] Definitions =
    [
        new("lesson-default", "lesson-loom", "lesson-loom", "lesson-loom",
            "633f51637a1c90818e91a573e8551bef3f2ff6519ceb7c2a9cc4a19382c00a0f",
            "6644849B3EA513821EF81737DD87047DC8FF8DA816B55B69E69B9CD8C4D00289"),
        new("source-default", "source-lens", "source-lens", "source-lens",
            "0cf24fe90e94ac8eacd971fd1a02521ec0d498fef52e6084b0a6f15df55ddcc3",
            "5A96F3ABA5531C17A0EE4B61525DA640A9DB6B5E0BF542E851EEF8FEA78CB557"),
        new("board-default", "board-to-brief", "board-to-brief", "board-to-brief",
            "652ab50f0b6a32d3490152755fb7de24eafac0126819aba6c0cf2bac447f4d55",
            "A1E6B588E55021D60E1A9FFF1FBBE5CF60F8A8BFC7985B071E7D8B5FC63BB471"),
        new("default-bar-chart", "bar-chart", "deterministic-press", "press.charts",
            "fad40ab0c45688e912b758efbfebda55d9a3b4a7e34f85102d93a2bd6a8a4256",
            "E5937D4D7B8494FF081A1E7657EBFABF5049952BE9DD6AF166FEB3D1A33B3518"),
        new("default-portfolio-passport", "portfolio-passport", "deterministic-press", "press.learner-held",
            "b9732c607f107a64a8e87dfd83603f9383fb54aa3a1c9123d8e4ccead8dc4ec2",
            "363B309D85421A517A8530883A3AC63829EF3CBD9B6D1CE1450DBCA7966EA801"),
        new("default-strategy-shelf", "strategy-shelf", "deterministic-press", "press.learner-held",
            "9b68a14947f1514a5d42d47b96e23a9097fb7d3d5433bd328de78b8f3e36b362",
            "363B309D85421A517A8530883A3AC63829EF3CBD9B6D1CE1450DBCA7966EA801"),
        new("default-goal-post", "goal-post", "deterministic-press", "press.learner-held",
            "bfc0066a344e173cf686b00cca699eda471c1d4efa20c1bba09e28f2ff70fa86",
            "363B309D85421A517A8530883A3AC63829EF3CBD9B6D1CE1450DBCA7966EA801"),
        new("default-calibration-proof", "calibration-proof", "deterministic-press", "press.calibration",
            "ca4f89861601544e74780e8dc62364318c3bf83c6fa3463d55ac8f147e00784a",
            "D4EF35243CC43AB847A3FF7443826299968501795D44F6BC30E1007D53F8773E"),
        new("default-flashcards", "flashcards", "deterministic-press", "press.flashcards",
            "5b1f134e4ab47ba6214eeb43c178b73e8b2405812e39e2f3571e6b30daee62f9",
            "18B4B7167D38FD941B65B1EC19D5E0C24F397974B9AC19AEC070ED4F54F9F5CF"),
    ];

    private readonly string _root = Directory.CreateTempSubdirectory("ocf-c1-seven-packages-").FullName;

    public static TheoryData<string, bool> DefaultVariants
    {
        get
        {
            var data = new TheoryData<string, bool>();
            foreach (var definition in Definitions)
            {
                data.Add(definition.CaseId, false);
                data.Add(definition.CaseId, true);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(DefaultVariants))]
    public async Task Both_current_loaders_and_prepared_twins_preserve_the_exact_C1_source(
        string caseId, bool includesContext)
    {
        var fixture = ReadFixture(caseId, includesContext);
        var sourceRoot = Directory.CreateDirectory(Path.Combine(_root, "source-library")).FullName;
        var source = Path.Combine(sourceRoot, "original.ocfproj");
        await File.WriteAllBytesAsync(source, fixture.Package);
        var original = await ReadBothAndAssert(sourceRoot, "original", fixture, includesContext);
        Assert.Equal(fixture.Package, await File.ReadAllBytesAsync(source));

        var preparedBytes = new List<byte[]>();
        foreach (var label in new[] { "candidate-a", "candidate-b" })
        {
            var candidateRoot = Directory.CreateDirectory(Path.Combine(_root, label)).FullName;
            var request = Request(sourceRoot, candidateRoot, fixture);
            var receipt = await OcfprojUpgradeService.PrepareCompatibleCopyAsync(request, CancellationToken.None);
            var destination = Path.Combine(candidateRoot, "prepared.ocfproj");
            var bytes = await File.ReadAllBytesAsync(destination);
            preparedBytes.Add(bytes);
            Assert.Equal(!includesContext, receipt.PackageTransformed);
            Assert.Equal(OriginalEngine, receipt.SourceEngineVersion);
            Assert.Equal("1", receipt.SourceSchemaVersion);
            Assert.Equal(fixture.Sha256, receipt.SourceSha256);
            Assert.Equal("0.8.0-alpha", receipt.TargetEngineVersion);
            Assert.Equal("1", receipt.TargetSchemaVersion);
            Assert.Equal(Sha256(bytes), receipt.OutputSha256);
            OcfprojZipAssertions.HasCanonicalMetadata(bytes);
            await AssertOriginalMembersPreserved(fixture.Package, bytes, includesContext);
            if (includesContext)
            {
                Assert.Equal(fixture.Package, bytes);
            }

            var prepared = await ReadBothAndAssert(candidateRoot, "prepared", fixture, hasLoadedContext: true);
            Assert.Equal(JsonSerializer.Serialize(original.Manifest, StorageJson.Options),
                JsonSerializer.Serialize(prepared.Manifest, StorageJson.Options));
            Assert.Equal(bytes, await File.ReadAllBytesAsync(destination));
            Assert.Equal(fixture.Package, await File.ReadAllBytesAsync(source));
            Assert.Equal(["prepared.ocfproj"], Directory.EnumerateFileSystemEntries(candidateRoot)
                .Select(Path.GetFileName).Order(StringComparer.Ordinal));
        }

        Assert.Equal(preparedBytes[0], preparedBytes[1]);
        await ReadBothAndAssert(sourceRoot, "original", fixture, includesContext);
        Assert.Equal(fixture.Package, await File.ReadAllBytesAsync(source));
    }

    [Theory]
    [MemberData(nameof(DefaultVariants))]
    public async Task Exact_outgoing_selection_and_late_failure_never_replace_or_remove_originals(
        string caseId, bool includesContext)
    {
        var fixture = ReadFixture(caseId, includesContext);
        var sourceRoot = Directory.CreateDirectory(Path.Combine(_root, "source-library")).FullName;
        var candidateRoot = Directory.CreateDirectory(Path.Combine(_root, "candidate-library")).FullName;
        var firstSource = Path.Combine(sourceRoot, "original.ocfproj");
        var secondSource = Path.Combine(sourceRoot, "second.ocfproj");
        var sentinel = Path.Combine(sourceRoot, "synthetic-unrelated-source.txt");
        await File.WriteAllBytesAsync(firstSource, fixture.Package);
        await File.WriteAllBytesAsync(secondSource, fixture.Package);
        await File.WriteAllTextAsync(sentinel, "Synthetic source-library sentinel; not an upgrade output.");
        var replacement = SelectRecipe(fixture.Definition, "0.2.0");
        Assert.Equal(fixture.Definition.RecipeId, replacement.Id);
        Assert.Equal("0.2.0", replacement.Version);

        var unavailable = Request(sourceRoot, candidateRoot, fixture) with
        {
            CandidateRecipes = [new ProjectUpgradeRecipeIdentity(replacement.Id, replacement.Version)],
        };
        var refusal = await Assert.ThrowsAsync<ProjectUpgradeException>(() =>
            OcfprojUpgradeService.PrepareCompatibleCopyAsync(unavailable, CancellationToken.None));
        Assert.Equal(ProjectUpgradeFailureCodes.CandidateRecipeUnavailable, refusal.Code);
        Assert.Empty(Directory.EnumerateFileSystemEntries(candidateRoot));
        Assert.Equal(fixture.Package, await File.ReadAllBytesAsync(firstSource));

        var partialsObserved = 0;
        var firstCompletedOutputObserved = false;
        var firstDestination = Path.Combine(candidateRoot, "first.ocfproj");
        var hooks = new ProjectUpgradeTestHooks((_, _) =>
        {
            partialsObserved++;
            if (partialsObserved == 2)
            {
                firstCompletedOutputObserved = File.Exists(firstDestination);
                throw new IOException("Synthetic second-package preparation failure.");
            }

            return Task.CompletedTask;
        });
        var batch = new ProjectUpgradeBatchRequest(
            sourceRoot, candidateRoot, "0.8.0-alpha",
            [
                new ProjectUpgradeItem("original.ocfproj", "first.ocfproj", OriginalEngine, "1", fixture.Sha256),
                new ProjectUpgradeItem("second.ocfproj", "second.ocfproj", OriginalEngine, "1", fixture.Sha256),
            ],
            HistoricalSelection(fixture.Definition));
        var stopped = await Assert.ThrowsAsync<ProjectUpgradeException>(() =>
            OcfprojUpgradeService.PrepareCompatibleBatchAsync(batch, hooks, CancellationToken.None));
        Assert.Equal(ProjectUpgradeFailureCodes.StorageFailure, stopped.Code);
        Assert.Equal(2, partialsObserved);
        Assert.True(firstCompletedOutputObserved, "The late failure must follow an actually completed first candidate copy.");
        Assert.Empty(Directory.EnumerateFileSystemEntries(candidateRoot));
        Assert.Equal("Synthetic source-library sentinel; not an upgrade output.", await File.ReadAllTextAsync(sentinel));
        foreach (var hint in new[] { "original", "second" })
        {
            await ReadBothAndAssert(sourceRoot, hint, fixture, includesContext);
            Assert.Equal(fixture.Package, await File.ReadAllBytesAsync(Path.Combine(sourceRoot, hint + ".ocfproj")));
        }
    }

    private static async Task<LoadedProject> ReadBothAndAssert(
        string root, string hint, Fixture fixture, bool hasLoadedContext)
    {
        var store = new OcfprojProjectStore(root, new AccessibleHtmlRenderer(), new NoAssetCatalog());
        var byStore = await store.LoadProjectAsync(hint, CancellationToken.None);
        var byFile = await OcfprojProjectStore.LoadProjectFileAsync(store.PathFor(hint), CancellationToken.None);
        using var originalStream = new MemoryStream(fixture.Package, writable: false);
        using var originalArchive = new ZipArchive(originalStream, ZipArchiveMode.Read);
        var originalDocument = JsonSerializer.Deserialize<ArtifactDocument>(
            await ReadMember(originalArchive, "artifact.json"), StorageJson.Options);
        var originalManifest = JsonSerializer.Deserialize<ProjectManifest>(
            await ReadMember(originalArchive, "manifest.json"), StorageJson.Options);
        Assert.NotNull(originalDocument);
        Assert.NotNull(originalManifest);
        foreach (var loaded in new[] { byStore, byFile })
        {
            Assert.Equal(JsonSerializer.Serialize(originalManifest, StorageJson.Options),
                JsonSerializer.Serialize(loaded.Manifest, StorageJson.Options));
            Assert.Equal(JsonSerializer.Serialize(originalDocument, StorageJson.Options),
                JsonSerializer.Serialize(loaded.Document, StorageJson.Options));
            Assert.Equal(fixture.Definition.DocumentFingerprint, ArtifactDocumentFingerprint.Compute(loaded.Document));
            Assert.Equal(originalDocument.Nodes.Select(node => node.GetType()), loaded.Document.Nodes.Select(node => node.GetType()));
            Assert.Equal(DocumentText.CollectStrings(originalDocument), DocumentText.CollectStrings(loaded.Document));
            Assert.Equal(OriginalEngine, loaded.Manifest.EngineVersion);
            Assert.Equal("1", loaded.Manifest.SchemaVersion);
            Assert.Equal(fixture.Definition.ModuleId, loaded.Manifest.ModuleId);
            Assert.Equal(fixture.Definition.RecipeId, loaded.Manifest.RecipeId);
            Assert.Equal(HistoricalVersion, loaded.Manifest.ModuleVersion);
            Assert.Equal(HistoricalVersion, loaded.Manifest.RecipeVersion);
            Assert.Equal(DataLane.Green, loaded.Manifest.DataLane);
            Assert.Equal(ArtifactPurpose.Unknown, loaded.Manifest.Purpose);
            Assert.Equal(originalDocument.Language, loaded.Manifest.SourceLocale);
            Assert.Null(loaded.Manifest.OutputLocale);
            Assert.Equal(OriginalInstant, loaded.Manifest.CreatedUtc);
            Assert.Equal(OriginalInstant, loaded.Manifest.ModifiedUtc);
            Assert.NotEqual(Guid.Empty, loaded.Manifest.ProjectId);
            Assert.Equal("artifact.json", loaded.Manifest.ArtifactPath);
            Assert.Equal("teacher-managed", loaded.Manifest.RetentionMode);
            Assert.Empty(loaded.Manifest.AssetIds);
            Assert.NotNull(loaded.Assets);
            Assert.Empty(loaded.Assets.All);
            Assert.False(DocumentValidator.HasBlockingIssues(DocumentValidator.Validate(loaded.Document)));
            Assert.DoesNotContain(loaded.Document.Nodes, node => node is ImageReference or StepRow { Symbol: not null });
            var historical = SelectRecipe(fixture.Definition, loaded.Manifest.RecipeVersion);
            Assert.Equal(fixture.Definition.RecipeFingerprint, RecipeContractFingerprint.ComputeSha256(historical));
            Assert.Equal(OriginalEngine, historical.MinimumEngineVersion);
            Assert.Equal("0.1", historical.EvaluationSuiteVersion);
            if (!hasLoadedContext)
            {
                Assert.Null(loaded.Validation);
                Assert.Null(loaded.RenderProfile);
                continue;
            }

            Assert.NotNull(loaded.Validation);
            Assert.NotNull(loaded.RenderProfile);
            Assert.Equal(1, loaded.Validation.SchemaVersion);
            Assert.Equal(ProjectValidationEnvelope.ExactApprovedDocumentKind, loaded.Validation.Kind);
            Assert.Equal(fixture.Definition.RecipeId, loaded.Validation.RecipeId);
            Assert.Equal(HistoricalVersion, loaded.Validation.RecipeVersion);
            Assert.Equal(DataLane.Green, loaded.Validation.Lane);
            Assert.Equal(ArtifactPurpose.Unknown, loaded.Validation.Purpose);
            Assert.Equal(fixture.Definition.DocumentFingerprint, loaded.Validation.ArtifactSha256);
            Assert.Equal(1, loaded.RenderProfile.SchemaVersion);
            Assert.Equal(fixture.Definition.DocumentFingerprint, loaded.RenderProfile.ArtifactSha256);
            Assert.Equal(fixture.IncludesContext ? RenderAudience.Teacher : RenderAudience.Learner, loaded.RenderProfile.Audience);
            Assert.Equal(fixture.IncludesContext ? 150 : 100, loaded.RenderProfile.TextScalePercent);
            Assert.Equal(fixture.IncludesContext, loaded.RenderProfile.TargetLanguageFirst);
            if (fixture.IncludesContext)
            {
                var validation = JsonSerializer.Deserialize<ProjectValidationEnvelope>(
                    await ReadMember(originalArchive, "validation.json"), StorageJson.Options);
                var profile = JsonSerializer.Deserialize<ProjectRenderProfile>(
                    await ReadMember(originalArchive, "render-profile.json"), StorageJson.Options);
                Assert.Equal(JsonSerializer.Serialize(validation, StorageJson.Options),
                    JsonSerializer.Serialize(loaded.Validation, StorageJson.Options));
                Assert.Equal(profile, loaded.RenderProfile);
            }
            else
            {
                Assert.Empty(loaded.Validation.UntrustedNoticeCodes);
            }
        }

        var snapshot = await ReadMember(originalArchive, "snapshot.html");
        Assert.True(PortableProjectSnapshot.MatchesExact(byFile.Document, OriginalEngine, hasLoadedContext,
            new RenderRequest(RenderTarget.AccessibleHtml, RenderAudience.Learner,
                fixture.IncludesContext ? 150 : 100, fixture.IncludesContext), snapshot));
        var snapshotText = Encoding.UTF8.GetString(snapshot);
        Assert.DoesNotContain("synthetic-package-compatibility@example.invalid", snapshotText, StringComparison.Ordinal);
        foreach (var notice in originalDocument.Nodes.OfType<TeacherOnlyNotice>())
        {
            Assert.DoesNotContain(System.Net.WebUtility.HtmlEncode(notice.Text), snapshotText, StringComparison.Ordinal);
        }

        return byFile;
    }

    private static async Task AssertOriginalMembersPreserved(byte[] original, byte[] candidate, bool includesContext)
    {
        using var sourceStream = new MemoryStream(original, writable: false);
        using var candidateStream = new MemoryStream(candidate, writable: false);
        using var source = new ZipArchive(sourceStream, ZipArchiveMode.Read);
        using var output = new ZipArchive(candidateStream, ZipArchiveMode.Read);
        var originals = source.Entries.Select(entry => entry.FullName).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(includesContext ? ContextMembers : ContextlessMembers, originals);
        Assert.Equal(originals.Concat(includesContext ? [] : AddedContextMembers)
            .Order(StringComparer.Ordinal), output.Entries.Select(entry => entry.FullName).Order(StringComparer.Ordinal));
        foreach (var entry in source.Entries)
        {
            var copy = Assert.Single(output.Entries, item => item.FullName == entry.FullName);
            Assert.Equal(entry.LastWriteTime, copy.LastWriteTime);
            Assert.Equal(entry.Length, copy.Length);
            Assert.Equal(await ReadMember(source, entry.FullName), await ReadMember(output, entry.FullName));
        }

        using var manifest = JsonDocument.Parse(await ReadMember(source, "manifest.json"));
        foreach (var absent in new[] { "recipeHash", "outputSchemaId", "definitionId", "approvedBy", "inputs" })
        {
            Assert.False(manifest.RootElement.TryGetProperty(absent, out _));
        }
    }

    private static Fixture ReadFixture(string caseId, bool includesContext)
    {
        var definition = Assert.Single(Definitions, item => item.CaseId == caseId);
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "HonestInk.slnx")))
        {
            repository = repository.Parent;
        }

        Assert.NotNull(repository);
        var root = Path.Combine(repository.FullName, "tests", "Integration", "Fixtures", "upgrade", "c1-seven-recipes");
        var indexPath = Path.Combine(root, "fixture-index.json");
        Assert.True(File.Exists(indexPath), "Genuine C1 package fixtures are not installed: fixture-index.json is required; no skip or replacement generation is permitted.");
        using var index = JsonDocument.Parse(File.ReadAllBytes(indexPath));
        Assert.Equal(1, index.RootElement.GetProperty("formatVersion").GetInt32());
        Assert.Equal(C1Commit, index.RootElement.GetProperty("originalSourceCommit").GetString());
        Assert.Equal("project-supplied-synthetic", index.RootElement.GetProperty("materialClass").GetString());
        var entries = index.RootElement.GetProperty("packages").EnumerateArray().ToArray();
        Assert.Equal(18, entries.Length);
        var expectedKeys = Definitions.SelectMany(item => new[] { item.CaseId + "/contextless", item.CaseId + "/exact-context" });
        Assert.Equal(expectedKeys.Order(StringComparer.Ordinal), entries.Select(item =>
            item.GetProperty("caseId").GetString() + "/" + item.GetProperty("contextShape").GetString()).Order(StringComparer.Ordinal));
        var context = includesContext ? "exact-context" : "contextless";
        var selected = Assert.Single(entries, item => item.GetProperty("caseId").GetString() == caseId
            && item.GetProperty("contextShape").GetString() == context);
        var fileName = caseId + "-" + context + ".ocfproj.base64";
        Assert.Equal(fileName, selected.GetProperty("fileName").GetString());
        var fixturePath = Path.Combine(root, fileName);
        Assert.True(File.Exists(fixturePath), "The exact genuine C1 package fixture is missing: " + fileName);
        var package = Convert.FromBase64String(File.ReadAllText(fixturePath));
        Assert.Equal(selected.GetProperty("bytes").GetInt64(), package.LongLength);
        var expectedHash = selected.GetProperty("sha256").GetString();
        Assert.Equal(expectedHash, Sha256(package));
        return new Fixture(definition, includesContext, package, Sha256(package));
    }

    private static RecipeManifest SelectRecipe(CaseDefinition definition, string version)
        => definition.ModuleId == "deterministic-press"
            ? PressRoomCatalog.ById(definition.DefinitionId, version).Recipe
            : ModuleStudioCatalog.ByModeKey(definition.DefinitionId, version).Recipe;

    private static ProjectUpgradeRecipeIdentity[] HistoricalSelection(CaseDefinition definition)
    {
        var selected = SelectRecipe(definition, HistoricalVersion);
        Assert.Equal(definition.RecipeId, selected.Id);
        Assert.Equal(HistoricalVersion, selected.Version);
        Assert.Equal(definition.RecipeFingerprint, RecipeContractFingerprint.ComputeSha256(selected));
        return [new ProjectUpgradeRecipeIdentity(selected.Id, selected.Version)];
    }

    private static ProjectUpgradeRequest Request(string sourceRoot, string candidateRoot, Fixture fixture)
        => new(sourceRoot, candidateRoot, "original.ocfproj", "prepared.ocfproj", OriginalEngine,
            "1", fixture.Sha256, "0.8.0-alpha", HistoricalSelection(fixture.Definition));

    private static async Task<byte[]> ReadMember(ZipArchive archive, string name)
    {
        var entry = Assert.Single(archive.Entries, item => item.FullName == name);
        await using var input = entry.Open();
        using var output = new MemoryStream();
        await input.CopyToAsync(output);
        return output.ToArray();
    }

    private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            // Only this unique synthetic test directory is owned; cleanup must
            // not replace the original fixture, package or assertion failure.
        }
    }

    private sealed record CaseDefinition(string CaseId, string DefinitionId, string ModuleId, string RecipeId,
        string DocumentFingerprint, string RecipeFingerprint);
    private sealed record Fixture(CaseDefinition Definition, bool IncludesContext, byte[] Package, string Sha256);

    private sealed class NoAssetCatalog : IAssetCatalog
    {
        public IReadOnlyList<AssetProvenance> All => [];
        public AssetProvenance? Find(AssetId id) => null;
        public bool TryGetContent(AssetId id, out ReadOnlyMemory<byte> content, out string mimeType)
        {
            content = default;
            mimeType = string.Empty;
            return false;
        }
    }
}
