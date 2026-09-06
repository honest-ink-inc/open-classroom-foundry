// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.DeterministicPress;

namespace Foundry.Tests.InstructionalEvals;

/// <summary>
/// Replacement-only synthetic semantic/vector evaluation routes. These are
/// not invented historical press corpora, native-PDF/physical-print evidence,
/// complete typography proof or substitutes for the separate exact-C1 harness.
/// Successful cases exercise compiled selected builders/transforms and Gate B;
/// no renderer, exporter, save or printer is invoked.
/// </summary>
internal static class ReplacementPressReviewCorpus
{
    private static readonly DateTimeOffset Instant = new(2026, 9, 6, 0, 0, 0, TimeSpan.Zero);
    private const string Pledge = "Synthetic blank paper stays with its holder.";

    internal static RecipeEvaluationCorpus Charts() => Corpus("press.charts",
    [
        Case("chart-up-zero-and-exact-proportions", () => ReviewChart(horizontal: false, wide: false)),
        Case("chart-across-int32-axis", () => ReviewChart(horizontal: true, wide: true)),
        Case("chart-refuses-negative-data", () => Refuses("bar-chart", "press.charts",
            values => values["data"] = "Synthetic A|-1\nSynthetic B|2", "negative")),
        Case("chart-refuses-all-zero-data", () => Refuses("bar-chart", "press.charts",
            values => values["data"] = "Synthetic A|0\nSynthetic B|0", "above zero")),
    ]);

    internal static RecipeEvaluationCorpus LearnerHeld() => Corpus("press.learner-held",
    [
        Case("portfolio-four-pages-and-pledges", ReviewPortfolio),
        Case("strategy-verbatim-order-and-pledge", ReviewStrategy),
        Case("goal-six-portrait-one-page", () => ReviewGoal("Letter", 1)),
        Case("goal-six-letter-landscape-two-pages", () => ReviewGoal("Letter landscape", 2)),
        Case("goal-six-a4-landscape-two-pages", () => ReviewGoal("A4 landscape", 2)),
        Case("goal-refuses-seven-prompts", () => Refuses("goal-post", "press.learner-held",
            values => values["prompts"] = string.Join('\n', Enumerable.Range(1, 7).Select(index => $"Synthetic prompt {index}")),
            "Between 2 and 6")),
    ]);

    internal static RecipeEvaluationCorpus Calibration() => Corpus("press.calibration",
    [
        Case("calibration-letter-words-rulers-and-solid-low-ink-endpoint", () => ReviewCalibration("Letter", PageSize.Letter)),
        Case("calibration-a4-words-rulers-and-solid-low-ink-endpoint", () => ReviewCalibration("A4", PageSize.A4)),
        Case("calibration-refuses-letter-landscape", () => Refuses("calibration-proof", "press.calibration",
            values => values[PressRoomCatalog.PageKey] = "Letter landscape", "portrait")),
        Case("calibration-refuses-a4-landscape", () => Refuses("calibration-proof", "press.calibration",
            values => values[PressRoomCatalog.PageKey] = "A4 landscape", "portrait")),
        Case("calibration-refuses-nonfinite-margin", () => Refuses("calibration-proof", "press.calibration",
            values => values["margin"] = "NaN", "finite")),
    ]);

    internal static RecipeEvaluationCorpus Flashcards() => Corpus("press.flashcards",
    [
        Case("flashcard-40-character-no-warning-boundary", () => ReviewFlashcards(40, warningExpected: false)),
        Case("flashcard-41-character-warning-boundary", () => ReviewFlashcards(41, warningExpected: true)),
        Case("flashcard-90-character-warning-survives-edited-review", () => ReviewFlashcards(90, warningExpected: true)),
        Case("flashcard-refuses-blank-answer", () => Refuses("flashcards", "press.flashcards",
            values => values["pairs"] = "Synthetic term|", "blank side")),
    ]);

    private static RecipeEvaluationCorpus Corpus(string recipeId, RecipeEvaluationCase[] cases)
        => new(new(recipeId, "0.2.0", "0.2"), $"{nameof(ReplacementPressReviewCorpus)}.{recipeId}", cases);

    private static RecipeEvaluationCase Case(string id, Action evaluate) => new(id, () =>
    {
        evaluate();
        return Task.CompletedTask;
    });

    private static PressDefinition Selected(string definitionId, string recipeId)
    {
        var definition = PressRoomCatalog.ById(definitionId, "0.2.0");
        RecipeEvaluationDispatcher.RequireManifest(new(recipeId, "0.2.0", "0.2"), definition.Recipe);
        return definition;
    }

    private static void Refuses(
        string definitionId,
        string recipeId,
        Action<Dictionary<string, string>> change,
        string messagePart)
    {
        var definition = Selected(definitionId, recipeId);
        var values = PressRoomCatalog.Defaults(definition);
        change(values);
        var refusal = Assert.Throws<ArgumentException>(() => definition.BuildForReview(new PressInputs(values)));
        Assert.Contains(messagePart, refusal.Message, StringComparison.Ordinal);
    }

    private static void ReviewChart(bool horizontal, bool wide)
    {
        BuildAndReview("bar-chart", "press.charts", values =>
        {
            values["title"] = "Synthetic proportion control";
            values["data"] = wide
                ? "Synthetic maximum|2147483647\nSynthetic second|1073741823"
                : "Synthetic zero|0\nSynthetic two|2\nSynthetic four|4";
            values["orientation"] = horizontal ? "Across" : "Up";
        }, (document, issues) =>
        {
            Assert.Empty(issues);
            var page = Assert.Single(document.Nodes.OfType<VectorGraphic>());
            var bars = page.Primitives.OfType<RectShape>().ToArray();
            Assert.Equal(2, bars.Length);
            Assert.All(bars, bar =>
            {
                Assert.True(double.IsFinite(bar.WidthMm) && bar.WidthMm > 0);
                Assert.True(double.IsFinite(bar.HeightMm) && bar.HeightMm > 0);
                Assert.False(bar.Filled);
            });
            if (wide)
            {
                Assert.Contains(page.Primitives.OfType<TextLabel>(), label => label.Text == "2500000000");
                Assert.Equal(2147483647d / 1073741823d, bars[0].WidthMm / bars[1].WidthMm, precision: 10);
            }
            else
            {
                Assert.Equal(2, bars[1].HeightMm / bars[0].HeightMm, precision: 10);
                Assert.Contains(page.Primitives.OfType<TextLabel>(), label => label.Text == "Synthetic zero");
            }
        });
    }

    private static void ReviewGoal(string pageName, int expectedPages)
    {
        var prompts = Enumerable.Range(1, 6).Select(index => $"Synthetic blank prompt {index}").ToArray();
        BuildAndReview("goal-post", "press.learner-held", values =>
        {
            values[PressRoomCatalog.PageKey] = pageName;
            values["prompts"] = string.Join('\n', prompts);
            values["pledge"] = Pledge;
        }, (document, issues) =>
        {
            Assert.Empty(issues);
            var pages = document.Nodes.OfType<VectorGraphic>().ToArray();
            Assert.Equal(expectedPages, pages.Length);
            Assert.Equal(prompts, pages.SelectMany(page => page.Primitives.OfType<TextLabel>())
                .Where(label => label.Text != Pledge).Select(label => label.Text));
            foreach (var page in pages)
            {
                Assert.Single(page.Primitives.OfType<TextLabel>(), label => label.Text == Pledge);
                var labels = page.Primitives.OfType<TextLabel>().Where(label => label.Text != Pledge).ToArray();
                var rules = page.Primitives.OfType<LineSeg>().ToArray();
                Assert.Equal(labels.Length * 3, rules.Length);
                Assert.All(labels, label => Assert.Equal(5, label.FontSizeMm));
                for (var index = 0; index + 1 < labels.Length; index++)
                {
                    Assert.True(rules[index * 3 + 2].Y1 + rules[index * 3 + 2].StrokeWidthMm / 2
                        < labels[index + 1].Y - labels[index + 1].FontSizeMm);
                }
            }
        });
    }

    private static void ReviewPortfolio()
        => BuildAndReview("portfolio-passport", "press.learner-held", values =>
        {
            values["selection"] = "Synthetic selection A\nSynthetic selection B";
            values["reflection"] = "Synthetic blank reflection A\nSynthetic blank reflection B";
            values["pledge"] = Pledge;
        }, (document, issues) =>
        {
            Assert.Empty(issues);
            var pages = document.Nodes.OfType<VectorGraphic>().ToArray();
            Assert.Equal(4, pages.Length);
            Assert.All(pages, page => Assert.Single(page.Primitives.OfType<TextLabel>(), label => label.Text == Pledge));
        });

    private static void ReviewStrategy()
    {
        string[] strategies = ["Synthetic option A", "Synthetic option B", "Synthetic option C", "Synthetic option D"];
        BuildAndReview("strategy-shelf", "press.learner-held", values =>
        {
            values["strategies"] = string.Join('\n', strategies);
            values["pledge"] = Pledge;
        }, (document, issues) =>
        {
            Assert.Empty(issues);
            var page = Assert.Single(document.Nodes.OfType<VectorGraphic>());
            Assert.Equal(strategies, page.Primitives.OfType<TextLabel>()
                .Where(label => label.Text != Pledge).Select(label => label.Text));
            Assert.Single(page.Primitives.OfType<TextLabel>(), label => label.Text == Pledge);
        });
    }

    private static void ReviewCalibration(string pageName, PageSize size)
    {
        var historical = Assert.Single(CalibrationPress.ProofPage(size).Nodes.OfType<VectorGraphic>());
        var originalWords = string.Join(' ', historical.Primitives.Skip(3).Take(5).OfType<TextLabel>().Select(label => label.Text));
        BuildAndReview("calibration-proof", "press.calibration",
            values => values[PressRoomCatalog.PageKey] = pageName, (document, issues) =>
        {
            Assert.Empty(issues);
            var page = Assert.Single(document.Nodes.OfType<VectorGraphic>());
            var instructionLabels = page.Primitives.Skip(3).TakeWhile(primitive => primitive is TextLabel)
                .Cast<TextLabel>().ToArray();
            Assert.Equal(originalWords, string.Join(' ', instructionLabels.Select(label => label.Text)));
            Assert.All(instructionLabels, label => Assert.True(label.Text.Length <= 67));
            Assert.Single(page.Primitives.OfType<LineSeg>(), line => line.Y1 == line.Y2 && line.X2 - line.X1 == 100);
            Assert.Single(page.Primitives.OfType<LineSeg>(), line => line.X1 == line.X2 && line.Y2 - line.Y1 == 100);
            var ramp = page.Primitives.OfType<RectShape>().Where(rectangle => rectangle.WidthMm == 22 && rectangle.HeightMm == 14).ToArray();
            Assert.Equal(6, ramp.Length);
            Assert.All(ramp.Take(5), rectangle => Assert.False(rectangle.Filled));
            Assert.True(ramp[^1].Filled, "The selected low-ink route must keep the calibration density endpoint solid.");
        });
    }

    private static void ReviewFlashcards(int length, bool warningExpected)
    {
        var answer = new string('x', length);
        BuildAndReview("flashcards", "press.flashcards",
            values => values["pairs"] = "Synthetic term|" + answer, (document, issues) =>
        {
            Assert.Equal(2, document.Nodes.OfType<VectorGraphic>().Count());
            Assert.Equal(["Synthetic term", answer], Labels(document).Select(label => label.Text));
            if (warningExpected)
            {
                var warning = Assert.Single(issues);
                Assert.Equal("flashcard.overflow", warning.Code);
                Assert.Equal(ValidationSeverity.Warning, warning.Severity);
                Assert.False(warning.RequiresAcknowledgement);
                Assert.Equal("Pair 1 may overflow its card; the full text is kept — shorten it or accept small type.", warning.Message);
            }
            else
            {
                Assert.Empty(issues);
            }
        }, editTeacherNote: true);
    }

    private static void BuildAndReview(
        string definitionId,
        string recipeId,
        Action<Dictionary<string, string>> change,
        Action<ArtifactDocument, IReadOnlyList<ValidationIssue>> check,
        bool editTeacherNote = false)
    {
        var definition = Selected(definitionId, recipeId);
        var values = PressRoomCatalog.Defaults(definition);
        change(values);
        var first = definition.BuildForReview(new PressInputs(values));
        var second = definition.BuildForReview(new PressInputs(values));
        Assert.Equal(ArtifactDocumentFingerprint.Compute(first.Document), ArtifactDocumentFingerprint.Compute(second.Document));
        Assert.Equal(first.Issues, second.Issues);
        foreach (var lowInk in new[] { false, true })
        {
            var document = lowInk ? definition.ApplyLowInk(first.Document) : first.Document;
            var twin = lowInk ? definition.ApplyLowInk(first.Document) : second.Document;
            Assert.Equal(ArtifactDocumentFingerprint.Compute(document), ArtifactDocumentFingerprint.Compute(twin));
            Assert.Equal(Labels(first.Document), Labels(document));
            Assert.Equal(first.Document.Language, document.Language);
            Assert.Empty(DocumentValidator.Validate(document));
            check(document, first.Issues);
            Review(definition, document, first.Issues, editTeacherNote);
        }
    }

    private static TextLabel[] Labels(ArtifactDocument document)
        => [.. document.Nodes.OfType<VectorGraphic>().SelectMany(page => page.Primitives).OfType<TextLabel>()];

    private static void Review(
        PressDefinition definition,
        ArtifactDocument document,
        IReadOnlyList<ValidationIssue> builderIssues,
        bool editTeacherNote)
    {
        // Same two production validator layers as PressRoomForm ->
        // AppServices.SessionOverRecipe, without referencing the Windows UI.
        var validator = new ReviewNoticeValidator(
            new ReviewNoticeValidator(new DefaultArtifactValidator(), builderIssues),
            ReviewNoticeValidator.RequiredRecipeWarnings(definition.Recipe));
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

        var session = new ReviewSession(DraftArtifact.New(document, DataLane.Green), machine, validator);
        try
        {
            AssertFreshApproval(session, builderIssues);
            if (editTeacherNote)
            {
                var index = Assert.Single(session.Draft.Revision.Document.Nodes.Select((node, index) => (node, index)),
                    item => item.node is TeacherOnlyNotice).index;
                session.ReplaceNode(index, new TeacherOnlyNotice("Independent synthetic preparation note; review again."));
                AssertFreshApproval(session, builderIssues);
            }
        }
        finally
        {
            if (machine.State == JobState.AwaitingTeacherReview)
            {
                session.Cancel();
            }
        }
    }

    private static void AssertFreshApproval(ReviewSession session, IReadOnlyList<ValidationIssue> builderIssues)
    {
        Assert.False(session.CanApprove);
        Assert.NotEmpty(session.RequiredAcknowledgements);
        Assert.All(builderIssues, issue => Assert.Contains(issue, session.Issues));
        Assert.Throws<InvalidOperationException>(() => session.Approve("synthetic-press-eval@example.invalid", Instant));
        session.SetRequiredIssuesAcknowledged(true);
        Assert.True(session.CanApprove);
        var revision = session.Draft.Revision;
        var approved = session.Approve("synthetic-press-eval@example.invalid", Instant);
        Assert.Same(revision, approved.Revision);
        Assert.Same(approved, session.ApprovedResult);
        Assert.All(builderIssues, issue => Assert.Contains(issue, approved.ValidationIssues));
        Assert.Equal(JobState.Approved, session.Machine.State);
    }
}
