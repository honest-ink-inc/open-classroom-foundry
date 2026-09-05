// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json;
using Foundry.Domain;
using Foundry.Modules.DeterministicPress;

namespace Foundry.Tests.Unit;

public sealed class PressBuildReviewResultTests
{
    [Theory]
    [MemberData(nameof(PressRoomCatalogTests.EveryPressId), MemberType = typeof(PressRoomCatalogTests))]
    public void Every_default_review_build_preserves_the_document_only_output(string id)
    {
        var definition = PressRoomCatalog.ById(id);
        var inputs = new PressInputs(PressRoomCatalog.Defaults(definition));

        var ordinary = definition.Build(inputs);
        var reviewed = definition.BuildForReview(inputs);

        Assert.Equal(JsonSerializer.Serialize(ordinary), JsonSerializer.Serialize(reviewed.Document));
        Assert.Equal(ordinary.Language, reviewed.Document.Language);
        Assert.Empty(reviewed.Issues);
    }

    [Fact]
    public void Flashcard_review_build_preserves_the_exact_existing_warning_and_document()
    {
        var answer = new string('x', 90);
        var direct = FlashcardFlywheel.Build([new FlashcardPair("Synthetic term", answer)]);
        var definition = PressRoomCatalog.ById("flashcards");
        var values = PressRoomCatalog.Defaults(definition);
        values["pairs"] = "Synthetic term | " + answer;

        var reviewed = definition.BuildForReview(new PressInputs(values));

        Assert.Equal(JsonSerializer.Serialize(direct.Document), JsonSerializer.Serialize(reviewed.Document));
        Assert.Equal(direct.Issues, reviewed.Issues);
        var warning = Assert.Single(reviewed.Issues);
        Assert.Equal("flashcard.overflow", warning.Code);
        Assert.Equal(ValidationSeverity.Warning, warning.Severity);
        Assert.False(warning.RequiresAcknowledgement);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Each_catalog_entry_point_invokes_its_builder_only_once(bool includesIssues)
    {
        var calls = 0;
        var document = new ArtifactDocument([new Paragraph("Synthetic one-build proof")]);
        var issue = ValidationIssue.Warning("synthetic.warning", "Synthetic warning");
        var definition = includesIssues
            ? new PressDefinition("synthetic", "Synthetic", DeterministicPressRecipes.Blankforms, [],
                _ =>
                {
                    calls++;
                    return new PressBuildResult(document, [issue]);
                })
            : new PressDefinition("synthetic", "Synthetic", DeterministicPressRecipes.Blankforms, [],
                _ =>
                {
                    calls++;
                    return document;
                });
        var inputs = new PressInputs(new Dictionary<string, string>());

        var review = definition.BuildForReview(inputs);
        Assert.Equal(1, calls);
        Assert.Same(document, review.Document);
        Assert.Equal(includesIssues ? 1 : 0, review.Issues.Count);
        Assert.Same(document, definition.Build(inputs));
        Assert.Equal(2, calls);
    }

    [Fact]
    public void Result_copies_issues_and_exposes_no_mutable_collection_or_replaceable_property()
    {
        var document = new ArtifactDocument([new Paragraph("Synthetic immutable result")]);
        var expected = ValidationIssue.Warning("synthetic.warning", "Synthetic original warning");
        var supplied = new List<ValidationIssue> { expected };

        var result = new PressBuildResult(document, supplied);
        supplied[0] = ValidationIssue.Info("synthetic.changed", "Synthetic replacement");
        supplied.Clear();

        Assert.Same(document, result.Document);
        Assert.Equal(expected, Assert.Single(result.Issues));
        var listView = Assert.IsType<IList<ValidationIssue>>(result.Issues, exactMatch: false);
        Assert.True(listView.IsReadOnly);
        Assert.Throws<NotSupportedException>(listView.Clear);
        Assert.Throws<NotSupportedException>(() => listView[0] = supplied.FirstOrDefault()!);
        Assert.Null(typeof(PressBuildResult).GetProperty(nameof(PressBuildResult.Document))!.SetMethod);
        Assert.Null(typeof(PressBuildResult).GetProperty(nameof(PressBuildResult.Issues))!.SetMethod);
    }

    [Fact]
    public void Result_refuses_missing_document_issue_collection_and_issue_entries()
    {
        var document = new ArtifactDocument([new Paragraph("Synthetic input boundary")]);

        Assert.Throws<ArgumentNullException>(() => new PressBuildResult(null!, []));
        Assert.Throws<ArgumentNullException>(() => new PressBuildResult(document, null!));
        Assert.Throws<ArgumentException>(() => new PressBuildResult(document, [null!]));
    }

    [Fact]
    public void Document_only_builder_retains_its_loud_null_document_refusal()
    {
        var definition = new PressDefinition("synthetic", "Synthetic", DeterministicPressRecipes.Blankforms, [],
            (Func<PressInputs, ArtifactDocument>)(_ => null!));

        var refusal = Assert.Throws<InvalidOperationException>(() =>
            definition.Build(new PressInputs(new Dictionary<string, string>())));

        Assert.Equal("Press 'synthetic' returned no document.", refusal.Message);
    }

    [Fact]
    public void Low_ink_keeps_solid_rectangle_density_and_existing_circle_line_text_behavior()
    {
        var solid = new RectShape(10, 10, 22, 14, 0.4, Filled: true);
        var outline = new RectShape(40, 10, 22, 14, 0.4);
        var circle = new CircleShape(20, 50, 2, 0.3, Filled: true);
        var line = new LineSeg(10, 60, 70, 60, 0.5, Dashed: true);
        var label = new TextLabel(40, 80, "Synthetic density proof", 5);
        var original = new ArtifactDocument([
            new VectorGraphic(100, 100, [solid, outline, circle, line, label], "Synthetic geometry"),
        ], "en");

        var lowInk = LowInkPress.Apply(original);
        var primitives = Assert.Single(lowInk.Nodes.OfType<VectorGraphic>()).Primitives;

        Assert.Equal(solid with { StrokeWidthMm = 0.24 }, primitives[0]);
        Assert.Equal(outline with { StrokeWidthMm = 0.24 }, primitives[1]);
        Assert.Equal(circle with { StrokeWidthMm = 0.2, Filled = false }, primitives[2]);
        Assert.Equal(line with { StrokeWidthMm = 0.3 }, primitives[3]);
        Assert.Equal(label, primitives[4]);
        Assert.Equal(original.Language, lowInk.Language);
        Assert.True(solid.Filled);
        Assert.True(circle.Filled);
    }
}
