// SPDX-License-Identifier: GPL-3.0-or-later
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.BuiltIn;
using Foundry.Modules.DeterministicPress;
using Foundry.Rendering;
using Foundry.Storage;

namespace Foundry.Tests.Integration;

/// <summary>
/// Project-supplied synthetic persistence controls for seven exact recipe
/// identities. These are current-writer round trips, not C1 execution,
/// complete schema equivalence, image-bearing coverage or final admission.
/// Blank learner-held prompts contain no learner responses or records.
/// </summary>
public sealed class VersionedRecipePersistenceTests : IDisposable
{
    private const string Reviewer = "synthetic-persistence@example.invalid";
    private static readonly DateTimeOffset Instant = new(2026, 9, 6, 0, 0, 0, TimeSpan.Zero);
    private readonly string _root = Directory.CreateTempSubdirectory("ocf-versioned-persistence-").FullName;

    [Theory]
    [InlineData("lesson-loom", "0.1.0")]
    [InlineData("lesson-loom", "0.2.0")]
    [InlineData("board-to-brief", "0.1.0")]
    [InlineData("board-to-brief", "0.2.0")]
    [InlineData("source-lens", "0.1.0")]
    [InlineData("source-lens", "0.2.0")]
    [InlineData("press.charts", "0.1.0")]
    [InlineData("press.charts", "0.2.0")]
    [InlineData("press.learner-held", "0.1.0")]
    [InlineData("press.learner-held", "0.2.0")]
    [InlineData("press.calibration", "0.1.0")]
    [InlineData("press.calibration", "0.2.0")]
    [InlineData("press.flashcards", "0.1.0")]
    [InlineData("press.flashcards", "0.2.0")]
    public async Task Exact_selected_recipe_survives_current_writer_and_both_readers(string recipeId, string version)
    {
        var selected = BuildRepresentative(recipeId, version);
        Assert.Equal(recipeId, selected.Recipe.Id);
        Assert.Equal(version, selected.Recipe.Version);
        Assert.Equal(version == "0.1.0" ? "0.7.0-alpha" : "0.8.0-alpha", selected.Recipe.MinimumEngineVersion);
        Assert.Equal(version == "0.1.0" ? "0.1" : "0.2", selected.Recipe.EvaluationSuiteVersion);
        AssertRepresentativeShape(recipeId, version, selected.Draft.Revision.Document);

        var session = Session(selected);
        var approved = ApproveFresh(session);
        Assert.Same(selected.Draft.Revision, approved.Revision);
        Assert.Equal(DataLane.Green, approved.Revision.Lane);
        Assert.Equal(ArtifactPurpose.Unknown, approved.Revision.Purpose);
        Assert.Empty(approved.AssetBindings);

        var store = new OcfprojProjectStore(_root, new AccessibleHtmlRenderer(), new EmptyAssetCatalog());
        var twinStore = new OcfprojProjectStore(Path.Combine(_root, "twin"), new AccessibleHtmlRenderer(), new EmptyAssetCatalog());
        foreach (var includeContext in new[] { false, true })
        {
            var hint = $"{recipeId}-{version}-{includeContext}";
            var validation = includeContext ? ProjectValidationEnvelope.Exact(approved, recipeId, version) : null;
            var profile = includeContext
                ? ProjectRenderProfile.For(approved, RenderAudience.Teacher, 150, targetLanguageFirst: true)
                : null;
            var request = new ProjectSaveRequest(
                hint, selected.ModuleId, recipeId, version, Instant, validation, profile);
            await store.SaveGreenProjectAsync(approved, request, CancellationToken.None);
            await twinStore.SaveGreenProjectAsync(approved, request, CancellationToken.None);
            var path = store.PathFor(hint);
            var packageBeforeReads = await File.ReadAllBytesAsync(path);
            Assert.Equal(packageBeforeReads, await File.ReadAllBytesAsync(twinStore.PathFor(hint)));
            var loadedByStore = await store.LoadProjectAsync(hint, CancellationToken.None);
            var loadedByPath = await OcfprojProjectStore.LoadProjectFileAsync(path, CancellationToken.None);
            AssertLoaded(selected, approved, validation, profile, loadedByStore);
            AssertLoaded(selected, approved, validation, profile, loadedByPath);
            Assert.Equal(packageBeforeReads, await File.ReadAllBytesAsync(path));

            using (var archive = ZipFile.OpenRead(path))
            {
                var expectedEntries = includeContext
                    ? new[] { "artifact.json", "manifest.json", "render-profile.json", "snapshot.html", "validation.json" }
                    : ["artifact.json", "manifest.json", "snapshot.html"];
                Assert.Equal(expectedEntries, archive.Entries.Select(entry => entry.FullName).Order(StringComparer.Ordinal));
                Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(approved.Revision.Document, StorageJson.Options),
                    await ReadMember(archive, "artifact.json"));
                using var manifestJson = JsonDocument.Parse(await ReadMember(archive, "manifest.json"));
                Assert.False(manifestJson.RootElement.TryGetProperty("recipeHash", out _));
                Assert.False(manifestJson.RootElement.TryGetProperty("outputSchemaId", out _));
                Assert.False(manifestJson.RootElement.TryGetProperty("approvedBy", out _));

                var snapshot = await ReadMember(archive, "snapshot.html");
                var snapshotRequest = new RenderRequest(
                    RenderTarget.AccessibleHtml, RenderAudience.Learner,
                    profile?.TextScalePercent ?? 100, profile?.TargetLanguageFirst ?? false);
                Assert.True(PortableProjectSnapshot.MatchesExact(
                    loadedByPath.Document, loadedByPath.Manifest.EngineVersion,
                    includeContext, snapshotRequest, snapshot));
                Assert.DoesNotContain(Reviewer, Encoding.UTF8.GetString(snapshot), StringComparison.Ordinal);
                foreach (var notice in approved.Revision.Document.Nodes.OfType<TeacherOnlyNotice>())
                {
                    Assert.DoesNotContain(System.Net.WebUtility.HtmlEncode(notice.Text),
                        Encoding.UTF8.GetString(snapshot), StringComparison.Ordinal);
                }

                var alteredSnapshot = snapshot.ToArray();
                alteredSnapshot[^1] ^= 1;
                Assert.False(PortableProjectSnapshot.MatchesExact(
                    loadedByPath.Document, loadedByPath.Manifest.EngineVersion,
                    includeContext, snapshotRequest, alteredSnapshot));
            }

            if (validation is not null)
            {
                var wrongVersion = version == "0.1.0" ? "0.2.0" : "0.1.0";
                var mismatch = request with { DestinationHint = hint + "-mismatch", RecipeVersion = wrongVersion };
                var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    store.SaveGreenProjectAsync(approved, mismatch, CancellationToken.None));
                Assert.Contains("does not bind to this exact approved artifact", refusal.Message, StringComparison.Ordinal);
                Assert.False(File.Exists(store.PathFor(mismatch.DestinationHint)));
                Assert.Equal(packageBeforeReads, await File.ReadAllBytesAsync(path));
            }
        }
    }

    [Theory]
    [InlineData("lesson-loom")]
    [InlineData("board-to-brief")]
    [InlineData("source-lens")]
    public void Blocking_selected_review_cannot_mint_a_persistable_artifact(string recipeId)
    {
        var selected = BuildRepresentative(recipeId, "0.2.0");
        var session = Session(selected);
        try
        {
            session.SetRequiredIssuesAcknowledged(true);
            Assert.True(session.CanApprove);
            var index = selected.Draft.Revision.Document.Nodes.ToList().FindIndex(node =>
                recipeId == "lesson-loom" ? node is TableNode : node is Paragraph);
            Assert.True(index >= 0);
            session.RemoveNode(index);
            session.SetRequiredIssuesAcknowledged(true);
            Assert.Contains(session.Issues, issue => issue.Severity == ValidationSeverity.Blocking);
            Assert.False(session.CanApprove);
            Assert.Throws<InvalidOperationException>(() => session.Approve(Reviewer, Instant));
            Assert.Null(session.ApprovedResult);
            Assert.Empty(Directory.EnumerateFileSystemEntries(_root));
        }
        finally
        {
            if (session.Machine.State == JobState.AwaitingTeacherReview)
            {
                session.Cancel();
            }
        }
    }

    private static Representative BuildRepresentative(string recipeId, string version)
    {
        if (!recipeId.StartsWith("press.", StringComparison.Ordinal))
        {
            var historical = ModuleStudioCatalog.ByModeKey(recipeId, "0.1.0");
            var selected = ModuleStudioCatalog.ByModeKey(recipeId, version);
            // The same version-independent project-supplied synthetic values are
            // used for either exact version; no loaded selectors supply authority.
            var values = ModuleStudioCatalog.Defaults(historical);
            if (recipeId == "board-to-brief")
            {
                values["lines"] = "Synthetic brief|title\nDATE-10|date\nRead the synthetic card.|step\nSynthetic card|material\nSynthetic independent preparation note.|note";
                values["locked-fields"] = "date|DATE-10";
            }

            var outcome = selected.Build!(new ModuleInputValues(values));
            Assert.Same(selected.Recipe, outcome.Recipe);
            return new Representative(recipeId, outcome.Recipe, outcome.CreateDraft(), outcome.Validator);
        }

        var definitionId = DefinitionId(recipeId);
        var historicalPress = PressRoomCatalog.ById(definitionId, "0.1.0");
        var selectedPress = PressRoomCatalog.ById(definitionId, version);
        var inputs = PressRoomCatalog.Defaults(historicalPress);
        switch (recipeId)
        {
            case "press.charts":
                inputs["title"] = "Synthetic proportional chart";
                inputs["data"] = "Synthetic two|2\nSynthetic four|4";
                break;
            case "press.learner-held":
                inputs[PressRoomCatalog.PageKey] = "Letter landscape";
                inputs["prompts"] = string.Join('\n', Enumerable.Range(1, 6).Select(index => $"Synthetic blank prompt {index}"));
                inputs["pledge"] = "Synthetic blank template; no learner responses are stored.";
                break;
            case "press.flashcards":
                inputs["pairs"] = new string('Q', 90) + "|Synthetic answer\nSynthetic second term|Synthetic second answer";
                break;
        }

        var built = selectedPress.BuildForReview(new PressInputs(inputs));
        Assert.DoesNotContain(built.Issues, issue => issue.Severity == ValidationSeverity.Blocking);
        var document = recipeId == "press.calibration" ? selectedPress.ApplyLowInk(built.Document) : built.Document;
        var validator = new ReviewNoticeValidator(
            new ReviewNoticeValidator(new DefaultArtifactValidator(), built.Issues),
            ReviewNoticeValidator.RequiredRecipeWarnings(selectedPress.Recipe));
        if (recipeId == "press.flashcards")
        {
            Assert.Equal(version == "0.2.0", built.Issues.Any(issue => issue.Code == "flashcard.overflow"));
        }

        return new Representative("deterministic-press", selectedPress.Recipe, DraftArtifact.New(document, DataLane.Green), validator);
    }

    private static void AssertLoaded(
        Representative selected, ApprovedArtifact approved,
        ProjectValidationEnvelope? validation, ProjectRenderProfile? profile, LoadedProject loaded)
    {
        var expected = approved.Revision.Document;
        Assert.Equal(JsonSerializer.Serialize(expected, StorageJson.Options), JsonSerializer.Serialize(loaded.Document, StorageJson.Options));
        Assert.Equal(ArtifactDocumentFingerprint.Compute(expected), ArtifactDocumentFingerprint.Compute(loaded.Document));
        Assert.Equal(expected.Nodes.Select(node => node.GetType()), loaded.Document.Nodes.Select(node => node.GetType()));
        Assert.Equal(DocumentText.CollectStrings(expected), DocumentText.CollectStrings(loaded.Document));
        Assert.Equal(expected.Language, loaded.Document.Language);
        Assert.False(DocumentValidator.HasBlockingIssues(DocumentValidator.Validate(loaded.Document)));
        Assert.Equal("1", loaded.Manifest.SchemaVersion);
        Assert.Equal("0.8.0-alpha", loaded.Manifest.EngineVersion);
        Assert.Equal(selected.ModuleId, loaded.Manifest.ModuleId);
        Assert.Equal(selected.Recipe.Id, loaded.Manifest.RecipeId);
        Assert.Equal(selected.Recipe.Version, loaded.Manifest.ModuleVersion);
        Assert.Equal(selected.Recipe.Version, loaded.Manifest.RecipeVersion);
        Assert.Equal(DataLane.Green, loaded.Manifest.DataLane);
        Assert.Equal(ArtifactPurpose.Unknown, loaded.Manifest.Purpose);
        Assert.Equal("teacher-managed", loaded.Manifest.RetentionMode);
        Assert.Equal("artifact.json", loaded.Manifest.ArtifactPath);
        Assert.Equal(expected.Language, loaded.Manifest.SourceLocale);
        Assert.Null(loaded.Manifest.OutputLocale);
        Assert.Equal(Instant, loaded.Manifest.CreatedUtc);
        Assert.Equal(Instant, loaded.Manifest.ModifiedUtc);
        Assert.NotEqual(Guid.Empty, loaded.Manifest.ProjectId);
        Assert.Empty(loaded.Manifest.AssetIds);
        Assert.NotNull(loaded.Assets);
        Assert.Empty(loaded.Assets.All);
        Assert.DoesNotContain(loaded.Document.Nodes, node => node is ImageReference or StepRow { Symbol: not null });
        Assert.Equal(profile, loaded.RenderProfile);

        // Exact data-only catalog lookup is not authentication of a mutable
        // package, a fresh approval, or permission to execute a replacement.
        var resolved = selected.Recipe.Id.StartsWith("press.", StringComparison.Ordinal)
            ? PressRoomCatalog.ById(DefinitionId(loaded.Manifest.RecipeId), loaded.Manifest.RecipeVersion).Recipe
            : ModuleStudioCatalog.ByModeKey(loaded.Manifest.RecipeId, loaded.Manifest.RecipeVersion).Recipe;
        Assert.Same(selected.Recipe, resolved);
        if (validation is null)
        {
            Assert.Null(loaded.Validation);
            return;
        }

        Assert.NotNull(loaded.Validation);
        Assert.Equal(validation.SchemaVersion, loaded.Validation.SchemaVersion);
        Assert.Equal(validation.Kind, loaded.Validation.Kind);
        Assert.Equal(validation.RecipeId, loaded.Validation.RecipeId);
        Assert.Equal(validation.RecipeVersion, loaded.Validation.RecipeVersion);
        Assert.Equal(validation.Lane, loaded.Validation.Lane);
        Assert.Equal(validation.Purpose, loaded.Validation.Purpose);
        Assert.Equal(validation.ArtifactSha256, loaded.Validation.ArtifactSha256);
        Assert.Equal(validation.UntrustedNoticeCodes, loaded.Validation.UntrustedNoticeCodes);
        Assert.Equal(approved.ValidationIssues.Select(issue => issue.Code).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal),
            loaded.Validation.UntrustedNoticeCodes);
    }

    private static void AssertRepresentativeShape(string recipeId, string version, ArtifactDocument document)
    {
        Assert.NotEmpty(document.Nodes);
        switch (recipeId)
        {
            case "lesson-loom":
                Assert.Contains(document.Nodes, node => node is TeacherOnlyNotice);
                Assert.True(document.Nodes.OfType<TableNode>().Count() >= 2);
                break;
            case "board-to-brief":
                Assert.Contains(document.Nodes, node => node is Paragraph { Text: "DATE-10" });
                Assert.Contains(document.Nodes, node => node is OrderedSteps);
                Assert.Contains(document.Nodes, node => node is TeacherOnlyNotice);
                break;
            case "source-lens":
                Assert.Single(document.Nodes.OfType<Citation>());
                Assert.Equal(7, document.Nodes.OfType<TableNode>().First().Rows.Count);
                Assert.Contains(document.Nodes, node => node is Paragraph);
                break;
            case "press.learner-held":
                Assert.Equal(version == "0.1.0" ? 1 : 2, document.Nodes.OfType<VectorGraphic>().Count());
                break;
            case "press.calibration":
                var calibration = Assert.IsType<VectorGraphic>(Assert.Single(document.Nodes));
                Assert.Equal(version == "0.2.0", calibration.Primitives.OfType<RectShape>().Any(rectangle => rectangle.Filled));
                break;
            case "press.flashcards":
                Assert.Equal(2, document.Nodes.OfType<VectorGraphic>().Count());
                Assert.Contains(document.Nodes.OfType<VectorGraphic>().SelectMany(page => page.Primitives),
                    primitive => primitive is TextLabel label && label.Text == new string('Q', 90));
                break;
            case "press.charts":
                Assert.IsType<VectorGraphic>(Assert.Single(document.Nodes));
                break;
        }
    }

    private static string DefinitionId(string recipeId) => recipeId switch
    {
        "press.charts" => "bar-chart",
        "press.learner-held" => "goal-post",
        "press.calibration" => "calibration-proof",
        "press.flashcards" => "flashcards",
        _ => throw new ArgumentException("The fixture names no selected press definition.", nameof(recipeId)),
    };

    private static ReviewSession Session(Representative selected)
    {
        var machine = new JobStateMachine();
        foreach (var state in new[]
        {
            JobState.Imported, JobState.Normalized, JobState.DataLaneConfirmed,
            JobState.DraftGenerated, JobState.SchemaValidated, JobState.InvariantsValidated,
            JobState.AwaitingTeacherReview,
        })
        {
            machine.Transition(state);
        }

        return new ReviewSession(selected.Draft, machine, selected.Validator);
    }

    private static ApprovedArtifact ApproveFresh(ReviewSession session)
    {
        try
        {
            Assert.DoesNotContain(session.Issues, issue => issue.Severity == ValidationSeverity.Blocking);
            Assert.NotEmpty(session.RequiredAcknowledgements);
            Assert.False(session.CanApprove);
            Assert.Throws<InvalidOperationException>(() => session.Approve(Reviewer, Instant));
            Assert.Null(session.ApprovedResult);
            session.SetRequiredIssuesAcknowledged(true);
            Assert.True(session.CanApprove);
            var approved = session.Approve(Reviewer, Instant);
            Assert.Same(session.Draft.Revision, approved.Revision);
            Assert.Same(approved, session.ApprovedResult);
            Assert.Equal(JobState.Approved, session.Machine.State);
            return approved;
        }
        finally
        {
            if (session.Machine.State == JobState.AwaitingTeacherReview)
            {
                session.Cancel();
            }
        }
    }

    private static async Task<byte[]> ReadMember(ZipArchive archive, string name)
    {
        var entry = Assert.Single(archive.Entries, candidate => candidate.FullName == name);
        await using var input = entry.Open();
        using var output = new MemoryStream();
        await input.CopyToAsync(output);
        return output.ToArray();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        try
        {
            // Only the unique directory created by this test instance is owned.
            Directory.Delete(_root, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Cleanup cannot replace a primary assertion or package failure.
        }
    }

    private sealed record Representative(string ModuleId, RecipeManifest Recipe, DraftArtifact Draft, IArtifactValidator Validator);

    private sealed class EmptyAssetCatalog : IAssetCatalog
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
