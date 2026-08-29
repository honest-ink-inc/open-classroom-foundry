using Foundry.Domain;
using Foundry.Modules.BuiltIn.BoardToBrief;

namespace Foundry.Tests.Unit;

public class BoardToBriefTests
{
    private static IReadOnlyList<BriefLine> BoardLines() =>
    [
        new BriefLine("Chapter 9 vocabulary quiz", BriefRole.Title),
        new BriefLine("Quiz is Friday, October 9", BriefRole.Date),
        new BriefLine("Reread pages 112 to 118", BriefRole.Step),
        new BriefLine("Make ten flashcards", BriefRole.Step),
        new BriefLine("Quiz a partner", BriefRole.Step),
        new BriefLine("Flashcard stock", BriefRole.Material),
        new BriefLine("photosynthesis", BriefRole.Vocabulary),
        new BriefLine("chlorophyll", BriefRole.Vocabulary),
        new BriefLine("Check in with the three absent students", BriefRole.Note),
    ];

    [Fact]
    public void A_brief_assembles_roles_into_clean_structure()
    {
        var result = BoardToBriefBuilder.Build(BoardLines(), [new LockedField(LockedFieldKind.Date, "Friday, October 9")]);

        Assert.False(DocumentValidator.HasBlockingIssues(result.Issues));
        Assert.Equal("Chapter 9 vocabulary quiz", Assert.IsType<Heading>(result.Document.Nodes[0]).Text);
        Assert.Equal(3, Assert.Single(result.Document.Nodes.OfType<OrderedSteps>()).Steps.Count);
        Assert.Equal(2, result.Document.Nodes.OfType<UnorderedList>().Count());
        Assert.Single(result.Document.Nodes.OfType<TeacherOnlyNotice>());
    }

    [Fact]
    public void A_dropped_locked_date_blocks_the_brief()
    {
        var lines = BoardLines().Where(l => l.Role != BriefRole.Date).ToList();

        var result = BoardToBriefBuilder.Build(lines, [new LockedField(LockedFieldKind.Date, "Friday, October 9")]);

        Assert.Contains(result.Issues, i => i.Code == "locked.missing");
    }

    [Fact]
    public void Exactly_one_title_is_required()
    {
        var noTitle = BoardLines().Where(l => l.Role != BriefRole.Title).ToList();

        Assert.Contains(BoardToBriefBuilder.Build(noTitle, []).Issues, i => i.Code == "brief.title");
    }

    [Fact]
    public void Every_string_in_the_brief_traces_to_a_line_or_a_teacher_label()
    {
        var lines = BoardLines();
        var result = BoardToBriefBuilder.Build(lines, []);

        var allowed = lines.Select(l => l.Text).Concat(["Materials", "Vocabulary"]).ToHashSet(StringComparer.Ordinal);

        Assert.All(DocumentText.CollectStrings(result.Document), text => Assert.Contains(text, allowed));
    }

    [Fact]
    public void The_recipe_is_green_and_forbids_invention()
    {
        Assert.Equal(DataLane.Green, BoardToBriefBuilder.Recipe.MaximumLane);
        Assert.Contains(BoardToBriefBuilder.Recipe.ProhibitedPurposes, p => p.Contains("invented", StringComparison.Ordinal));
    }
}
