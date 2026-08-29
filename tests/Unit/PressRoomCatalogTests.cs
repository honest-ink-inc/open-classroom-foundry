using Foundry.Domain;
using Foundry.Modules.DeterministicPress;

namespace Foundry.Tests.Unit;

public class PressRoomCatalogTests
{
    public static TheoryData<string> EveryPressId()
    {
        var data = new TheoryData<string>();
        foreach (var definition in PressRoomCatalog.All)
        {
            data.Add(definition.Id);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryPressId))]
    public void Every_press_builds_from_its_own_defaults_and_validates_clean(string id)
    {
        var definition = PressRoomCatalog.ById(id);
        var document = definition.Build(new PressInputs(PressRoomCatalog.Defaults(definition)));

        Assert.NotEmpty(document.Nodes);
        Assert.False(DocumentValidator.HasBlockingIssues(DocumentValidator.Validate(document)));

        // The byte-identical claim holds at the catalog boundary too.
        Assert.Equal(
            System.Text.Json.JsonSerializer.Serialize(definition.Build(new PressInputs(PressRoomCatalog.Defaults(definition)))),
            System.Text.Json.JsonSerializer.Serialize(definition.Build(new PressInputs(PressRoomCatalog.Defaults(definition)))));
    }

    [Fact]
    public void The_catalog_is_well_formed_ids_and_keys_unique_labels_present()
    {
        Assert.Equal(PressRoomCatalog.All.Count, PressRoomCatalog.All.Select(d => d.Id).Distinct(StringComparer.Ordinal).Count());

        foreach (var definition in PressRoomCatalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(definition.Title));
            Assert.Contains(definition.Recipe, DeterministicPressRecipes.All);
            Assert.Equal(
                definition.Parameters.Count,
                definition.Parameters.Select(p => p.Key).Distinct(StringComparer.Ordinal).Count());
            Assert.All(definition.Parameters, p => Assert.False(string.IsNullOrWhiteSpace(p.Label)));

            // A default outside its own bounds crashes the surface's spinner:
            // the catalog must be self-consistent before any form is generated.
            foreach (var number in definition.Parameters.OfType<NumberParameter>())
            {
                Assert.InRange(number.Default, number.Minimum, number.Maximum);
            }

            foreach (var choice in definition.Parameters.OfType<ChoiceParameter>())
            {
                Assert.Contains(choice.Default, choice.Options);
            }
        }
    }

    [Fact]
    public void Typed_access_parses_loudly_never_silently()
    {
        var inputs = new PressInputs(new Dictionary<string, string>
        {
            ["count"] = "seven",
            ["list"] = "2, x, 4",
        });

        Assert.Contains("seven", Assert.Throws<ArgumentException>(() => inputs.Whole("count")).Message, StringComparison.Ordinal);
        Assert.Contains("x", Assert.Throws<ArgumentException>(() => inputs.IntList("list")).Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => inputs.Text("absent"));
    }

    [Fact]
    public void Teacher_lines_are_verbatim_trimmed_and_blank_lines_dropped()
    {
        var inputs = new PressInputs(new Dictionary<string, string>
        {
            ["lines"] = "  first \r\n\r\nsecond|two \n",
        });

        Assert.Equal(["first", "second|two"], inputs.Lines("lines"));
        Assert.Equal([("first", null), ("second", "two")], inputs.SplitLines("lines"));
    }

    [Fact]
    public void Bad_teacher_input_reaches_the_surface_as_a_clear_refusal()
    {
        var wordSearch = PressRoomCatalog.ById("word-search");
        var values = PressRoomCatalog.Defaults(wordSearch);
        values["words"] = "two words on one line";

        var exception = Assert.Throws<ArgumentException>(() => wordSearch.Build(new PressInputs(values)));
        Assert.Contains("letters only", exception.Message, StringComparison.Ordinal);
    }
}
