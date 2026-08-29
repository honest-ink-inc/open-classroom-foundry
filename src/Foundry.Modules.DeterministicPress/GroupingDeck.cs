// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Domain;

namespace Foundry.Modules.DeterministicPress;

/// <summary>
/// Grouping Deck (atlas #137, lane-corrected): seeded-deterministic grouping
/// cards from a teacher-typed roster of SYNTHETIC or FIRST-NAME-FREE labels —
/// seat numbers, animal names, star colors — never real learner names; that is
/// the lane correction, stated in the recipe's prohibited purposes. Teacher
/// seed in, same groups out every time: a contested draw can be re-printed and
/// verified, never re-rolled in secret.
/// </summary>
public static class GroupingDeck
{
    private const int CardsPerRow = 2;
    private const int CardsPerColumn = 3;

    public static ArtifactDocument Cards(IReadOnlyList<string> roster, int groupSize, int seed, PageSize size = PageSize.Letter, double marginMm = BlankformsPress.DefaultMarginMm)
    {
        ArgumentNullException.ThrowIfNull(roster);
        if (roster.Count is < 2 or > 60)
        {
            throw new ArgumentException("Between two and sixty roster labels.", nameof(roster));
        }

        if (roster.Any(string.IsNullOrWhiteSpace) || roster.Distinct(StringComparer.Ordinal).Count() != roster.Count)
        {
            throw new ArgumentException("Roster labels must be non-blank and distinct.", nameof(roster));
        }

        if (groupSize < 2 || groupSize > roster.Count)
        {
            throw new ArgumentException("Groups of at least two, no larger than the roster.", nameof(groupSize));
        }

        // Shuffle once with the teacher's seed, then deal round-robin so group
        // sizes differ by at most one — remainders are spread, never stacked.
        var order = Enumerable.Range(0, roster.Count).ToList();
        new SeededPrng(seed).Shuffle(order);

        var groupCount = (roster.Count + groupSize - 1) / groupSize;
        var groups = Enumerable.Range(0, groupCount).Select(_ => new List<string>()).ToList();
        for (var i = 0; i < order.Count; i++)
        {
            groups[i % groupCount].Add(roster[order[i]]);
        }

        var (width, height) = BlankformsPress.Dimensions(size);
        var cardWidth = (width - 2 * marginMm) / CardsPerRow;
        var cardHeight = (height - 2 * marginMm - 8) / CardsPerColumn;
        var perPage = CardsPerRow * CardsPerColumn;

        var nodes = new List<DocumentNode>();
        for (var page = 0; page * perPage < groupCount; page++)
        {
            var primitives = new List<VectorPrimitive>();
            var first = page * perPage;
            var last = Math.Min(first + perPage, groupCount);

            for (var g = first; g < last; g++)
            {
                var slot = g - first;
                var x = marginMm + slot % CardsPerRow * cardWidth;
                var y = marginMm + slot / CardsPerRow * cardHeight;

                primitives.Add(new RectShape(x, y, cardWidth, cardHeight, 0.5));
                primitives.Add(new TextLabel(x + cardWidth / 2, y + 10,
                    (g + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), 8));
                primitives.Add(new LineSeg(x + 8, y + 14, x + cardWidth - 8, y + 14, 0.35));

                for (var m = 0; m < groups[g].Count; m++)
                {
                    primitives.Add(new TextLabel(x + cardWidth / 2, y + 24 + m * 8, groups[g][m], 5));
                }
            }

            primitives.Add(new TextLabel(width / 2, height - marginMm,
                $"{groupCount} groups from {roster.Count} labels · seed {seed}", 3.5));

            nodes.Add(new VectorGraphic(width, height, primitives,
                $"Grouping cards, page {page + 1}: groups {first + 1} to {last} of {groupCount}, drawn from a {roster.Count}-label roster with seed {seed}"));
        }

        return new ArtifactDocument(nodes);
    }
}
