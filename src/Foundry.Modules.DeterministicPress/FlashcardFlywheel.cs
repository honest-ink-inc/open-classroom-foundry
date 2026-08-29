// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Domain;

namespace Foundry.Modules.DeterministicPress;

public sealed record FlashcardPair(string Term, string Answer);

public sealed record FlywheelResult(ArtifactDocument Document, IReadOnlyList<ValidationIssue> Issues);

/// <summary>
/// The Flashcard Flywheel (spec §5.2): registration-safe double-sided cards. The
/// back sheet mirrors the columns for a long-edge duplex flip, so term i lands
/// exactly behind answer i — the pairing invariant is geometry, proven in tests.
/// Overflow is flagged, never silently truncated.
/// </summary>
public static class FlashcardFlywheel
{
    public const int Columns = 2;
    public const int Rows = 4;
    public const int CardsPerSheet = Columns * Rows;
    // RC-21: at 6 mm type in a ~96x63 mm cell, wrapping fails well before 60
    // characters; the flag fires honestly early. Text is still never truncated.
    public const int OverflowCharacterCount = 40;

    public static FlywheelResult Build(IReadOnlyList<FlashcardPair> pairs, PageSize size = PageSize.Letter, double marginMm = BlankformsPress.DefaultMarginMm)
    {
        ArgumentNullException.ThrowIfNull(pairs);
        if (pairs.Count == 0)
        {
            throw new ArgumentException("A card set needs at least one pair.", nameof(pairs));
        }

        var issues = new List<ValidationIssue>();
        for (var i = 0; i < pairs.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(pairs[i].Term) || string.IsNullOrWhiteSpace(pairs[i].Answer))
            {
                throw new ArgumentException($"Pair {i + 1} has a blank side.", nameof(pairs));
            }

            if (pairs[i].Term.Length > OverflowCharacterCount || pairs[i].Answer.Length > OverflowCharacterCount)
            {
                issues.Add(ValidationIssue.Warning(
                    "flashcard.overflow",
                    $"Pair {i + 1} may overflow its card; the full text is kept — shorten it or accept small type."));
            }
        }

        var (width, height) = BlankformsPress.Dimensions(size);
        var cardWidth = (width - 2 * marginMm) / Columns;
        var cardHeight = (height - 2 * marginMm) / Rows;

        var nodes = new List<DocumentNode>
        {
            new TeacherOnlyNotice(
                "Print double-sided, flip on the LONG edge. Sheet order is front, back, front, back. Cut on the card outlines."),
        };

        var sheetCount = (pairs.Count + CardsPerSheet - 1) / CardsPerSheet;
        for (var sheet = 0; sheet < sheetCount; sheet++)
        {
            nodes.Add(Sheet(pairs, sheet, front: true, width, height, marginMm, cardWidth, cardHeight));
            nodes.Add(Sheet(pairs, sheet, front: false, width, height, marginMm, cardWidth, cardHeight));
        }

        return new FlywheelResult(new ArtifactDocument(nodes), issues);
    }

    /// <summary>Front cell center for pair index i; the back mirrors x about the page center.</summary>
    public static (double X, double Y) FrontCenter(int pairIndex, double width, double height, double marginMm)
    {
        var indexOnSheet = pairIndex % CardsPerSheet;
        var row = indexOnSheet / Columns;
        var column = indexOnSheet % Columns;
        var cardWidth = (width - 2 * marginMm) / Columns;
        var cardHeight = (height - 2 * marginMm) / Rows;
        return (marginMm + column * cardWidth + cardWidth / 2, marginMm + row * cardHeight + cardHeight / 2);
    }

    private static VectorGraphic Sheet(
        IReadOnlyList<FlashcardPair> pairs, int sheet, bool front,
        double width, double height, double marginMm, double cardWidth, double cardHeight)
    {
        var primitives = new List<VectorPrimitive>();

        // Registration corner marks: identical on both sides so a mis-flip is visible.
        foreach (var (x, y) in new[] { (6.0, 6.0), (width - 6, 6.0), (6.0, height - 6), (width - 6, height - 6) })
        {
            primitives.Add(new LineSeg(x - 3, y, x + 3, y, 0.3));
            primitives.Add(new LineSeg(x, y - 3, x, y + 3, 0.3));
        }

        var first = sheet * CardsPerSheet;
        var last = Math.Min(first + CardsPerSheet, pairs.Count);

        for (var i = first; i < last; i++)
        {
            var (frontX, frontY) = FrontCenter(i, width, height, marginMm);
            var x = front ? frontX : width - frontX; // long-edge flip mirrors horizontally
            var cellLeft = x - cardWidth / 2;
            var cellTop = frontY - cardHeight / 2;

            primitives.Add(new RectShape(cellLeft, cellTop, cardWidth, cardHeight, 0.4));
            primitives.Add(new TextLabel(x, frontY + 2, front ? pairs[i].Term : pairs[i].Answer, 6));
        }

        var side = front ? "front (terms)" : "back (answers)";
        return new VectorGraphic(width, height, primitives, $"Flashcard sheet {sheet + 1}, {side}");
    }
}
