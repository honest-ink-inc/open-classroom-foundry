using Foundry.Domain;
using Foundry.Modules.DeterministicPress;

namespace Foundry.Tests.Unit;

public class GroupingDeckTests
{
    private static readonly IReadOnlyList<string> Roster =
        [.. Enumerable.Range(1, 22).Select(i => $"Star {i}")];

    [Fact]
    public void The_same_seed_reproduces_the_same_groups_and_seeds_differ()
    {
        var first = System.Text.Json.JsonSerializer.Serialize(GroupingDeck.Cards(Roster, groupSize: 4, seed: 7));
        var second = System.Text.Json.JsonSerializer.Serialize(GroupingDeck.Cards(Roster, groupSize: 4, seed: 7));
        var other = System.Text.Json.JsonSerializer.Serialize(GroupingDeck.Cards(Roster, groupSize: 4, seed: 8));

        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
    }

    [Fact]
    public void Every_label_appears_exactly_once_and_group_sizes_differ_by_at_most_one()
    {
        // 22 labels in groups of 4 -> 6 groups: four of 4 and two of 3.
        var document = GroupingDeck.Cards(Roster, groupSize: 4, seed: 42);
        var labels = document.Nodes.OfType<VectorGraphic>()
            .SelectMany(g => g.Primitives.OfType<TextLabel>())
            .Where(l => l.Text.StartsWith("Star ", StringComparison.Ordinal))
            .Select(l => l.Text)
            .ToList();

        Assert.Equal(22, labels.Count);
        Assert.Equal(Roster.OrderBy(l => l, StringComparer.Ordinal), labels.OrderBy(l => l, StringComparer.Ordinal));

        var cards = document.Nodes.OfType<VectorGraphic>().SelectMany(g => g.Primitives.OfType<RectShape>()).Count();
        Assert.Equal(6, cards);

        // Six cards of two-column three-row layout fit one page.
        Assert.Single(document.Nodes);
        Assert.Contains(
            document.Nodes.OfType<VectorGraphic>().SelectMany(g => g.Primitives.OfType<TextLabel>()),
            l => l.Text.Contains("seed 42", StringComparison.Ordinal));
    }

    [Fact]
    public void Seven_or_more_groups_flow_onto_a_second_page()
    {
        var big = Enumerable.Range(1, 30).Select(i => $"Seat {i}").ToList();

        var document = GroupingDeck.Cards(big, groupSize: 4, seed: 1); // 8 groups
        var pages = document.Nodes.OfType<VectorGraphic>().ToList();

        Assert.Equal(2, pages.Count);
        Assert.Equal(6, pages[0].Primitives.OfType<RectShape>().Count());
        Assert.Equal(2, pages[1].Primitives.OfType<RectShape>().Count());
    }

    [Fact]
    public void Bad_rosters_and_group_sizes_are_refused()
    {
        Assert.Throws<ArgumentException>(() => GroupingDeck.Cards(["only"], 2, 1));
        Assert.Throws<ArgumentException>(() => GroupingDeck.Cards(["a", "a", "b"], 2, 1));
        Assert.Throws<ArgumentException>(() => GroupingDeck.Cards(["a", " ", "b"], 2, 1));
        Assert.Throws<ArgumentException>(() => GroupingDeck.Cards(["a", "b", "c"], 1, 1));
        Assert.Throws<ArgumentException>(() => GroupingDeck.Cards(["a", "b", "c"], 4, 1));
    }

    [Fact]
    public void The_deck_validates_and_the_recipe_names_the_lane_correction()
    {
        Assert.False(DocumentValidator.HasBlockingIssues(
            DocumentValidator.Validate(GroupingDeck.Cards(Roster, 5, 3))));

        var recipe = DeterministicPressRecipes.Grouping;
        Assert.Equal("press.grouping", recipe.Id);
        Assert.Equal(DataLane.Green, recipe.MaximumLane);
        Assert.Empty(recipe.RequiredProviderCapabilities);
        Assert.Contains(recipe.ProhibitedPurposes, p => p.Contains("real learner names", StringComparison.Ordinal));
    }
}
