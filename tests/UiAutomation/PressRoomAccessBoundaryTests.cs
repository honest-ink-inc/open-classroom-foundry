// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json;
using Foundry.App.WinForms;
using Foundry.Application;
using Foundry.Domain;
using Foundry.Modules.DeterministicPress;
using Xunit.Abstractions;

namespace Foundry.Tests.UiAutomation;

/// <summary>
/// Existing-contract measurements for I20. These synthetic in-process fixtures
/// stop at Gate B: no approval, learner record, export, print, or human review.
/// </summary>
public sealed class PressRoomAccessBoundaryTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(12, false)]
    [InlineData(90, true)]
    public void Flashcard_overflow_warning_survives_the_actual_catalog_review_path(int answerLength, bool expectedOverflow)
        => Sta.Run(() =>
        {
            var answer = new string('x', answerLength);
            var direct = FlashcardFlywheel.Build([new FlashcardPair("Synthetic term", answer)]);
            Assert.Equal(expectedOverflow, direct.Issues.Any(issue => issue.Code == "flashcard.overflow"));
            ReviewSession? captured = null;
            using var form = CreatePressRoom("flashcards", session => captured = session);
            var pairs = ReviewSurfaceContractTests.Flatten(form).OfType<TextBox>().Single();
            pairs.Text = "Synthetic term | " + answer;
            InvokeReview(form);

            var review = Assert.IsType<ReviewSession>(captured);
            var labels = review.Draft.Revision.Document.Nodes.OfType<VectorGraphic>()
                .SelectMany(graphic => graphic.Primitives.OfType<TextLabel>()).ToArray();
            Assert.Contains(labels, label => label.Text == "Synthetic term");
            Assert.Contains(labels, label => label.Text == answer);
            Assert.Null(form.ApprovedResult);
            Assert.Null(review.ApprovedResult);
            var actualOverflow = review.Issues.Any(issue =>
                issue.Code == "flashcard.overflow" && issue.Severity == ValidationSeverity.Warning);
            output.WriteLine(JsonSerializer.Serialize(new
            {
                caseName = $"flashcards-{answerLength}",
                expectedOverflow,
                actualOverflow,
                directIssues = direct.Issues,
                reviewIssues = review.Issues,
                termAndAnswerPreserved = true,
                reviewState = review.Machine.State.ToString(),
            }));

            Assert.True(actualOverflow == expectedOverflow,
                $"The actual flashcards catalog/review path lost its overflow warning: answer length={answerLength}; "
                    + $"builder flashcard.overflow={expectedOverflow}; review flashcard.overflow={actualOverflow}; "
                    + $"review issues=[{IssueSummary(review)}]. The full synthetic answer remains in the reviewed document.");
        });

    [Fact]
    public void Flashcard_build_notice_survives_edit_and_fresh_review_without_changing_acknowledgement()
        => Sta.Run(() =>
        {
            var answer = new string('x', 90);
            var expected = Assert.Single(FlashcardFlywheel.Build([new FlashcardPair("Synthetic term", answer)]).Issues);
            ReviewSession? captured = null;
            using var form = CreatePressRoom("flashcards", session => captured = session);
            ReviewSurfaceContractTests.Flatten(form).OfType<TextBox>().Single().Text = "Synthetic term | " + answer;
            InvokeReview(form);
            var first = Assert.IsType<ReviewSession>(captured);
            Assert.Contains(expected, first.Issues);
            Assert.DoesNotContain(expected, first.RequiredAcknowledgements);
            first.SetRequiredIssuesAcknowledged(acknowledged: true);
            Assert.True(first.CanApprove);
            var revision = first.Draft.Revision;

            first.ReplaceNode(0, new TeacherOnlyNotice("Synthetic edited review notice."));

            Assert.NotSame(revision, first.Draft.Revision);
            Assert.Contains(expected, first.Issues);
            Assert.DoesNotContain(expected, first.RequiredAcknowledgements);
            Assert.False(first.CanApprove);
            Assert.Null(first.ApprovedResult);
            InvokeReview(form);
            var second = Assert.IsType<ReviewSession>(captured);
            Assert.NotSame(first, second);
            Assert.Contains(expected, second.Issues);
            Assert.DoesNotContain(expected, second.RequiredAcknowledgements);
            Assert.False(second.CanApprove);
            Assert.Null(second.ApprovedResult);
            Assert.Null(form.ApprovedResult);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Calibration_review_does_not_silently_empty_the_solid_density_endpoint(bool lowInk)
        => Sta.Run(() =>
        {
            ReviewSession? captured = null;
            using var form = CreatePressRoom("calibration-proof", session => captured = session);
            ReviewSurfaceContractTests.Flatten(form).OfType<CheckBox>().Single().Checked = lowInk;
            InvokeReview(form);

            Assert.Null(form.ApprovedResult);
            if (captured is null)
            {
                // An explicit refusal is an acceptable boundary; this test does
                // not prescribe a replacement density-ramp representation.
                var refusalPrefix = UiStrings.FormatWithoutMnemonic(UiStrings.StatusRefused, "");
                Assert.StartsWith(refusalPrefix, form.StatusText, StringComparison.Ordinal);
                output.WriteLine(JsonSerializer.Serialize(new
                {
                    caseName = $"calibration-low-ink-{lowInk}",
                    refused = true,
                    status = form.StatusText,
                }));
                return;
            }

            var review = captured;
            Assert.Null(review.ApprovedResult);
            var graphic = Assert.Single(review.Draft.Revision.Document.Nodes.OfType<VectorGraphic>());
            var ramp = graphic.Primitives.OfType<RectShape>()
                .Where(rectangle => rectangle.WidthMm == 22 && rectangle.HeightMm == 14)
                .OrderBy(rectangle => rectangle.X).ToArray();
            Assert.Equal(6, ramp.Length);
            var rampInstruction = graphic.Primitives.OfType<TextLabel>().Single(label =>
                label.Text.StartsWith("5. The density ramp", StringComparison.Ordinal)).Text;
            Assert.Equal(
                "5. The density ramp must darken evenly left to right; jumps or banding are driver or toner trouble.",
                rampInstruction);
            var finalPatch = ramp[^1];
            var finalHatchLines = graphic.Primitives.OfType<LineSeg>().Count(line =>
                line.X1 == finalPatch.X && line.X2 == finalPatch.X + finalPatch.WidthMm
                    && line.Y1 > finalPatch.Y && line.Y1 < finalPatch.Y + finalPatch.HeightMm);
            var blocked = review.Issues.Any(issue => issue.Severity == ValidationSeverity.Blocking);
            output.WriteLine(JsonSerializer.Serialize(new
            {
                caseName = $"calibration-low-ink-{lowInk}",
                refused = false,
                blocked,
                rampInstruction,
                ramp,
                finalHatchLines,
                reviewIssues = review.Issues,
                reviewState = review.Machine.State.ToString(),
            }));

            Assert.True(finalPatch.Filled || blocked,
                $"The actual calibration review silently emptied its solid density endpoint: low ink={lowInk}; "
                    + $"final patch Filled={finalPatch.Filled}; final-patch hatch lines={finalHatchLines}; "
                    + $"blocking issue={blocked}; review issues=[{IssueSummary(review)}]. "
                    + $"The unchanged instruction still says: {rampInstruction}");
        });

    private static PressRoomForm CreatePressRoom(string pressId, Action<ReviewSession> capture)
    {
        var form = new PressRoomForm(session =>
        {
            capture(session);
            return null;
        });
        try
        {
            form.Show();
            var presses = (ListBox)ReviewSurfaceContractTests.ByName(
                form, UiStrings.WithoutMnemonic(UiStrings.PressList));
            presses.SelectedIndex = PressRoomCatalog.All.ToList().FindIndex(definition => definition.Id == pressId);
            Assert.Equal(pressId, form.SelectedPress?.Id);
            return form;
        }
        catch
        {
            form.Dispose();
            throw;
        }
    }

    private static void InvokeReview(PressRoomForm form)
    {
        var review = (Button)ReviewSurfaceContractTests.ByName(
            form, UiStrings.WithoutMnemonic(UiStrings.ReviewAndApprove));
        Assert.True(review.Enabled, "The selected synthetic press did not expose its review action.");
        review.PerformClick();
    }

    private static string IssueSummary(ReviewSession review)
        => string.Join(" | ", review.Issues.Select(issue => $"{issue.Code}: {issue.Message}"));
}
