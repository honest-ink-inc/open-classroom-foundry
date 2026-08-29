// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Domain;

namespace Foundry.Modules.DeterministicPress;

// The card and protocol trio (atlas #58, #136, #68; fourth forge menu,
// item 5), on the established deck machinery. Card kinds are distinguished by
// SHAPE, never color alone: single border, double border (the control-card
// precedent), and a dashed frame for the deliberately ambiguous. The sorting
// judgment, the discussion, and the feedback all remain the learners' and the
// teacher's — the presses print the teacher's materials, nothing more.

/// <summary>
/// Concept Sort Studio (atlas #58): examples, nonexamples, and deliberately
/// ambiguous cards from three teacher lists. The border language labels the
/// kinds for the teacher's concept-attainment moves; the concept itself
/// prints only on the teacher key.
/// </summary>
public static class ConceptSortStudio
{
    private const int CardsPerRow = 2;
    private const int CardsPerColumn = 4;

    public static ArtifactDocument Cards(
        string concept,
        IReadOnlyList<string> examples,
        IReadOnlyList<string> nonexamples,
        IReadOnlyList<string> ambiguous,
        PageSize size = PageSize.Letter,
        double marginMm = BlankformsPress.DefaultMarginMm)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(concept);
        ArgumentNullException.ThrowIfNull(examples);
        ArgumentNullException.ThrowIfNull(nonexamples);
        ArgumentNullException.ThrowIfNull(ambiguous);

        if (examples.Count is < 2 or > 16 || nonexamples.Count is < 2 or > 16)
        {
            throw new ArgumentException("Between two and sixteen examples, and the same for nonexamples.", nameof(examples));
        }

        if (ambiguous.Count > 8)
        {
            throw new ArgumentException("At most eight deliberately ambiguous cards.", nameof(ambiguous));
        }

        var cards = examples.Select(text => (Text: text, Kind: CardKind.Example))
            .Concat(nonexamples.Select(text => (Text: text, Kind: CardKind.Nonexample)))
            .Concat(ambiguous.Select(text => (Text: text, Kind: CardKind.Ambiguous)))
            .ToList();

        var (width, height) = BlankformsPress.Dimensions(size);
        var cardWidth = (width - 2 * marginMm) / CardsPerRow;
        var cardHeight = (height - 2 * marginMm) / CardsPerColumn;
        var perPage = CardsPerRow * CardsPerColumn;

        var nodes = new List<DocumentNode>();
        for (var page = 0; page * perPage < cards.Count; page++)
        {
            var primitives = new List<VectorPrimitive>();
            var first = page * perPage;
            var last = Math.Min(first + perPage, cards.Count);

            for (var i = first; i < last; i++)
            {
                var slot = i - first;
                var x = marginMm + slot % CardsPerRow * cardWidth;
                var y = marginMm + slot / CardsPerRow * cardHeight;

                primitives.Add(new RectShape(x, y, cardWidth, cardHeight, 0.5));
                switch (cards[i].Kind)
                {
                    case CardKind.Nonexample:
                        // The double border, per the control-card precedent.
                        primitives.Add(new RectShape(x + 2, y + 2, cardWidth - 4, cardHeight - 4, 0.35));
                        break;
                    case CardKind.Ambiguous:
                        // A dashed frame: the card that is MEANT to start an argument.
                        primitives.Add(new LineSeg(x + 2, y + 2, x + cardWidth - 2, y + 2, 0.35, Dashed: true));
                        primitives.Add(new LineSeg(x + cardWidth - 2, y + 2, x + cardWidth - 2, y + cardHeight - 2, 0.35, Dashed: true));
                        primitives.Add(new LineSeg(x + 2, y + cardHeight - 2, x + cardWidth - 2, y + cardHeight - 2, 0.35, Dashed: true));
                        primitives.Add(new LineSeg(x + 2, y + 2, x + 2, y + cardHeight - 2, 0.35, Dashed: true));
                        break;
                    case CardKind.Example:
                    default:
                        break;
                }

                primitives.Add(new TextLabel(x + cardWidth / 2, y + cardHeight / 2 + 2, cards[i].Text, 5));
            }

            nodes.Add(new VectorGraphic(width, height, primitives,
                $"Concept-sort cards, page {page + 1}: teacher-supplied examples (single border), nonexamples (double border), and deliberately ambiguous cards (dashed frame)"));
        }

        nodes.Add(new TeacherOnlyNotice(
            $"Concept (teacher only): {concept}. Single border = example; double border = nonexample; dashed frame = deliberately ambiguous — let the argument happen."));

        return new ArtifactDocument(nodes);
    }

    private enum CardKind
    {
        Example,
        Nonexample,
        Ambiguous,
    }
}

/// <summary>
/// Discussion Role Wheel (atlas #136): role cards of "role | accountable
/// action" pairs, with the teacher's rotation note printed on every page so
/// no learner is permanently passive.
/// </summary>
public static class DiscussionRoleWheel
{
    private const int CardsPerRow = 2;
    private const int CardsPerColumn = 4;

    public static ArtifactDocument Cards(
        IReadOnlyList<(string Left, string? Right)> roles,
        string rotationNote,
        PageSize size = PageSize.Letter,
        double marginMm = BlankformsPress.DefaultMarginMm)
    {
        ArgumentNullException.ThrowIfNull(roles);
        ArgumentException.ThrowIfNullOrWhiteSpace(rotationNote);

        if (roles.Count is < 2 or > 16)
        {
            throw new ArgumentException("Between two and sixteen roles.", nameof(roles));
        }

        foreach (var (left, right) in roles)
        {
            if (string.IsNullOrWhiteSpace(right))
            {
                throw new ArgumentException($"'{left}' has no accountable action; write role | accountable action.", nameof(roles));
            }
        }

        var (width, height) = BlankformsPress.Dimensions(size);
        const double footerMm = 8;
        var cardWidth = (width - 2 * marginMm) / CardsPerRow;
        var cardHeight = (height - 2 * marginMm - footerMm) / CardsPerColumn;
        var perPage = CardsPerRow * CardsPerColumn;

        var nodes = new List<DocumentNode>();
        for (var page = 0; page * perPage < roles.Count; page++)
        {
            var primitives = new List<VectorPrimitive>();
            var first = page * perPage;
            var last = Math.Min(first + perPage, roles.Count);

            for (var i = first; i < last; i++)
            {
                var slot = i - first;
                var x = marginMm + slot % CardsPerRow * cardWidth;
                var y = marginMm + slot / CardsPerRow * cardHeight;

                primitives.Add(new RectShape(x, y, cardWidth, cardHeight, 0.5));
                primitives.Add(new TextLabel(x + cardWidth / 2, y + cardHeight / 2 - 4, roles[i].Left, 5.5));
                primitives.Add(new TextLabel(x + cardWidth / 2, y + cardHeight / 2 + 6, roles[i].Right!, 4));
            }

            // The rotation note rides every page, in ink, in the teacher's words.
            primitives.Add(new TextLabel(width / 2, height - marginMm - 2, rotationNote, 4));

            nodes.Add(new VectorGraphic(width, height, primitives,
                $"Discussion role cards, page {page + 1}: teacher-defined roles with accountable actions and a rotation note so no role is permanent"));
        }

        return new ArtifactDocument(nodes);
    }
}

/// <summary>
/// Peer Feedback Builder (atlas #68): a bounded protocol sheet — the
/// teacher's evidence rule in a box, sentence stems with writing lines, and
/// the author-decision box that keeps the author in charge of their work.
/// </summary>
public static class PeerFeedbackBuilder
{
    public static ArtifactDocument Sheet(
        string title,
        string evidenceRule,
        IReadOnlyList<string> stems,
        string authorHeading,
        PageSize size = PageSize.Letter,
        double marginMm = BlankformsPress.DefaultMarginMm)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceRule);
        ArgumentNullException.ThrowIfNull(stems);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorHeading);

        if (stems.Count is < 2 or > 8)
        {
            throw new ArgumentException("Between two and eight sentence stems.", nameof(stems));
        }

        var (width, height) = BlankformsPress.Dimensions(size);
        const double authorBoxMm = 45;
        var authorTop = height - marginMm - authorBoxMm;
        var stemsTop = marginMm + 28;
        if (stemsTop + stems.Count * 16 > authorTop - 4)
        {
            throw new ArgumentException("The stems must fit above the author-decision box; fewer stems or a taller page.", nameof(stems));
        }

        var primitives = new List<VectorPrimitive>
        {
            new TextLabel(width / 2, marginMm + 5, title, 5.5),
            new RectShape(marginMm, marginMm + 10, width - 2 * marginMm, 12, 0.6),
            new TextLabel(marginMm + 3, marginMm + 17.5, evidenceRule, 4.5, TextAnchor.Start),
        };

        for (var s = 0; s < stems.Count; s++)
        {
            var y = stemsTop + s * 16;
            primitives.Add(new TextLabel(marginMm, y, stems[s], 4.5, TextAnchor.Start));
            primitives.Add(new LineSeg(marginMm, y + 8, width - marginMm, y + 8, 0.3));
        }

        primitives.Add(new RectShape(marginMm, authorTop, width - 2 * marginMm, authorBoxMm, 0.7));
        primitives.Add(new TextLabel(marginMm + 3, authorTop + 6, authorHeading, 4.5, TextAnchor.Start));
        primitives.Add(new LineSeg(marginMm + 3, authorTop + 16, width - marginMm - 3, authorTop + 16, 0.3));
        primitives.Add(new LineSeg(marginMm + 3, authorTop + 26, width - marginMm - 3, authorTop + 26, 0.3));
        primitives.Add(new LineSeg(marginMm + 3, authorTop + 36, width - marginMm - 3, authorTop + 36, 0.3));

        return new ArtifactDocument([new VectorGraphic(width, height, primitives,
            $"A peer-feedback protocol sheet: the teacher's evidence rule, {stems.Count} sentence stems with writing lines, and the author-decision box that keeps the author in charge")]);
    }
}
