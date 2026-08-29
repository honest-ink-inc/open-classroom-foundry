using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.BuiltIn.AllAboard;

namespace Foundry.Tests.Unit;

public class AllAboardBuilderTests
{
    private sealed class FakeCatalog : IAssetCatalog
    {
        private static AssetProvenance Provenance(string id, string meaning, string alt, string? notes = null) => new(
            new AssetId(id), $"concept.{meaning.ToLowerInvariant()}", "1.0.0", $"{meaning.ToLowerInvariant()}.svg",
            "image/svg+xml", "original", "test", "CC0-1.0", "AB", meaning, alt, Redistributable: true, AmbiguityNotes: notes);

        private readonly Dictionary<string, AssetProvenance> _assets = new()
        {
            ["agency.stop.v1"] = Provenance("agency.stop.v1", "Stop", "An octagon outline", "May read as a generic polygon."),
            ["agency.help.v1"] = Provenance("agency.help.v1", "Help", "A life ring"),
        };

        public IReadOnlyList<AssetProvenance> All => [.. _assets.Values];

        public AssetProvenance? Find(AssetId id) => _assets.GetValueOrDefault(id.Value);

        public bool TryGetContent(AssetId id, out ReadOnlyMemory<byte> content, out string mimeType)
        {
            content = new byte[] { 1 };
            mimeType = "image/svg+xml";
            return _assets.ContainsKey(id.Value);
        }
    }

    private static readonly FakeCatalog Catalog = new();

    [Fact]
    public void A_task_strip_is_three_to_eight_steps_and_structurally_sound()
    {
        var document = AllAboardBuilders.TaskStrip(
            "Watering the class plants",
            [new StepSpec("Pick up the can."), new StepSpec("Fill to the line."), new StepSpec("Water each plant once.")],
            Catalog);

        Assert.False(DocumentValidator.HasBlockingIssues(DocumentValidator.Validate(document)));
        Assert.Equal(3, document.Nodes.OfType<StepRow>().Count());

        Assert.Throws<ArgumentException>(() => AllAboardBuilders.TaskStrip(
            "Too short", [new StepSpec("One"), new StepSpec("Two")], Catalog));
        Assert.Throws<ArgumentException>(() => AllAboardBuilders.TaskStrip(
            "Too long", [.. Enumerable.Range(1, 9).Select(i => new StepSpec($"Step {i}"))], Catalog));
    }

    [Fact]
    public void A_bilingual_strip_requires_every_step_translated_and_emits_aligned_pairs()
    {
        var document = AllAboardBuilders.TaskStrip(
            "Watering the class plants",
            [
                new StepSpec("Pick up the can.", TargetText: "Toma la regadera."),
                new StepSpec("Fill to the line.", TargetText: "Llénala hasta la línea."),
                new StepSpec("Water each plant once.", TargetText: "Riega cada planta una vez."),
            ],
            Catalog,
            sourceLocale: "en",
            targetLocale: "es");

        var rows = document.Nodes.OfType<StepRow>().ToList();
        Assert.Equal(3, rows.Count);
        Assert.All(rows, r => Assert.Equal("es", r.TargetLocale));
        Assert.All(rows, r => Assert.False(string.IsNullOrWhiteSpace(r.TargetText)));

        Assert.Throws<ArgumentException>(() => AllAboardBuilders.TaskStrip(
            "Missing translation",
            [new StepSpec("A", TargetText: "A'"), new StepSpec("B"), new StepSpec("C", TargetText: "C'")],
            Catalog, targetLocale: "es"));
    }

    [Fact]
    public void Symbols_sit_in_the_same_row_as_their_step_and_unknown_symbols_block()
    {
        var document = AllAboardBuilders.TaskStrip(
            "With symbols",
            [
                new StepSpec("Stop at the door.", new AssetId("agency.stop.v1")),
                new StepSpec("Ask for help."),
                new StepSpec("Wait for the teacher."),
            ],
            Catalog);

        // RC-3: adjacency is structural — the symbol lives inside its step's row.
        var symbolRow = Assert.Single(document.Nodes.OfType<StepRow>(), r => r.Symbol is not null);
        Assert.Equal("Stop at the door.", symbolRow.Text);
        Assert.Equal("An octagon outline", symbolRow.Symbol!.AltText);

        Assert.Throws<InvalidOperationException>(() => AllAboardBuilders.TaskStrip(
            "Unknown symbol",
            [new StepSpec("A", new AssetId("proprietary.mystery")), new StepSpec("B"), new StepSpec("C")],
            Catalog));
    }

    [Fact]
    public void First_then_and_now_next_done_emit_labeled_cards_in_order()
    {
        var firstThen = AllAboardBuilders.FirstThen(
            new CardSpec("Math journal"), new CardSpec("Blocks"), Catalog);

        Assert.Collection(
            firstThen.Nodes.OfType<Card>(),
            card => Assert.Equal("First: Math journal", card.Title),
            card => Assert.Equal("Then: Blocks", card.Title));

        var nowNextDone = AllAboardBuilders.NowNextDone(
            new CardSpec("Circle time"), new CardSpec("Centers"), new CardSpec("Snack"), Catalog);

        Assert.Equal(3, nowNextDone.Nodes.OfType<Card>().Count());
        Assert.StartsWith("Now:", nowNextDone.Nodes.OfType<Card>().First().Title, StringComparison.Ordinal);
    }

    [Fact]
    public void Agency_cards_keep_ambiguity_notes_off_the_learner_card()
    {
        var deck = AllAboardBuilders.AgencyCards(
            [new AssetId("agency.stop.v1"), new AssetId("agency.help.v1")], Catalog);

        Assert.Equal(2, deck.Nodes.OfType<ImageReference>().Count());
        Assert.Contains(deck.Nodes.OfType<Card>(), c => c.Title == "Stop");
        Assert.Contains(deck.Nodes.OfType<Card>(), c => c.Title == "Help");

        // RC-1: the learner card body is clean; the curator's ambiguity note is teacher craft.
        Assert.All(deck.Nodes.OfType<Card>(), c => Assert.Equal(string.Empty, c.Body));
        var note = Assert.Single(deck.Nodes.OfType<TeacherOnlyNotice>());
        Assert.Contains("generic polygon", note.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Agency_card_labels_are_overridable_for_the_classrooms_language()
    {
        var deck = AllAboardBuilders.AgencyCards(
            [new AssetId("agency.stop.v1"), new AssetId("agency.help.v1")], Catalog,
            language: "es", labels: ["Alto", "Ayuda"]);

        Assert.Contains(deck.Nodes.OfType<Card>(), c => c.Title == "Alto");
        Assert.Contains(deck.Nodes.OfType<Card>(), c => c.Title == "Ayuda");

        Assert.Throws<ArgumentException>(() => AllAboardBuilders.AgencyCards(
            [new AssetId("agency.stop.v1")], Catalog, labels: ["Alto", "extra"]));
    }

    [Fact]
    public void The_recipes_are_green_lane_and_prohibit_the_bright_lines()
    {
        Assert.Equal(4, AllAboardRecipes.All.Count);
        Assert.All(AllAboardRecipes.All, recipe =>
        {
            Assert.Equal(DataLane.Green, recipe.MaximumLane);
            Assert.Empty(recipe.RequiredProviderCapabilities);
            Assert.Contains(recipe.ProhibitedPurposes, p => p.Contains("PECS", StringComparison.Ordinal));
        });
    }
}
