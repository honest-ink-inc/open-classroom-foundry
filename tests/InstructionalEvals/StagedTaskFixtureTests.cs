using System.Text;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.BuiltIn.AllAboard;
using Foundry.Rendering;
using Foundry.Storage;

namespace Foundry.Tests.InstructionalEvals;

/// <summary>
/// The thirty staged-task fixtures of SequenceSlate's acceptance proof (stable legacy id: all-aboard; plan §10.1):
/// subjects and environments across the school day, several language pairs, and
/// symbol-bearing strips against the shipped libre pack. Every fixture must build,
/// validate, approve, and render cleanly for both paper and screen.
/// </summary>
public class StagedTaskFixtureTests
{
    public sealed record Fixture(
        string Subject,
        string Title,
        string[] Steps,
        string? TargetLocale = null,
        string[]? TargetSteps = null,
        string[]? Symbols = null)
    {
        public override string ToString() => $"{Subject}: {Title}";
    }

    public static readonly IReadOnlyList<Fixture> Fixtures =
    [
        new("science", "Setting up the microscope", ["Carry it with two hands.", "Plug in the light.", "Place the slide on the stage.", "Turn the coarse knob slowly.", "Look through the eyepiece."]),
        new("science", "Planting our seeds", ["Fill the cup with soil.", "Press one seed down.", "Add three spoons of water.", "Put the cup by the window."], "es", ["Llena el vaso con tierra.", "Presiona una semilla.", "Agrega tres cucharadas de agua.", "Pon el vaso junto a la ventana."]),
        new("science", "Building the circuit", ["Snap the battery holder on.", "Connect the red wire.", "Connect the black wire.", "Press the switch."]),
        new("science", "Reading the weather station", ["Check the thermometer.", "Check the rain gauge.", "Write both numbers on the chart."]),
        new("math", "Ten-frame warm-up", ["Take a ten-frame card.", "Count the dots out loud.", "Build the number with counters.", "Show your partner."]),
        new("math", "Measuring with a protractor", ["Line up the vertex.", "Line up the zero edge.", "Read the inside scale.", "Write the degrees."]),
        new("math", "Math journal time", ["Write today's date.", "Copy the problem.", "Show your thinking with a picture."]),
        new("math", "Fraction-strip cleanup", ["Stack strips by size.", "Wrap the rubber band.", "Put the stack in the bin.", "Push in your chair."]),
        new("literacy", "Book shopping", ["Take your book box.", "Choose two just-right books.", "Choose one look book.", "Sit in your reading spot."]),
        new("literacy", "Partner reading", ["Sit knee to knee.", "Reader one reads a page.", "Reader two retells it.", "Switch jobs.", "Put the book away."]),
        new("literacy", "Word work station", ["Take a word card.", "Build it with tiles.", "Write it on the whiteboard.", "Check it letter by letter."]),
        new("art", "Cleaning paintbrushes", ["Wipe the brush on the rim.", "Swish it in the water jar.", "Blot it on the towel.", "Lay it flat to dry.", "Pour the water in the sink."]),
        new("art", "Clay station start", ["Put on an apron.", "Take one ball of clay.", "Cover the table with the mat.", "Wash hands when finished."]),
        new("art", "Making a collage", ["Choose five papers.", "Tear them into shapes.", "Glue shapes to the board.", "Write your name on the back."]),
        new("pe", "Warm-up circuit", ["Jog one lap.", "Do ten jumping jacks.", "Stretch arms overhead.", "Line up on the blue line."]),
        new("pe", "Jump-rope station", ["Take a rope your height.", "Jump for one song.", "Coil the rope back in the bucket."]),
        new("pe", "Equipment return", ["Balls go in the tall bin.", "Cones stack by the door.", "Pinnies go in the basket."]),
        new("music", "Instrument care", ["Carry the instrument with two hands.", "Play only your part.", "Wipe it with the cloth.", "Set it in the rack."]),
        new("music", "Recorder practice", ["Check your finger holes.", "Play the line slowly.", "Play it again with the beat.", "Put the recorder in its bag."]),
        new("cte", "Making a sandwich", ["Wash your hands.", "Lay out two slices of bread.", "Spread with the plastic knife.", "Add one filling.", "Close and cut in half.", "Wipe the counter."]),
        new("cte", "Washing dishes", ["Scrape the plate.", "Wash in soapy water.", "Rinse in clear water.", "Set it in the rack.", "Hang the towel up."]),
        new("cte", "Threading the needle", ["Cut an arm's length of thread.", "Wet the thread end.", "Push it through the eye.", "Pull half through.", "Tie the ends together."]),
        new("library", "Checking out a book", ["Bring the book to the desk.", "Scan your card.", "Scan the book.", "Put the book in your bag."]),
        new("library", "Shelf return", ["Check the spine label.", "Find the matching shelf.", "Slide the book in straight."]),
        new("routine", "Morning arrival", ["Hang up your backpack.", "Move your lunch clip.", "Turn in your folder.", "Sharpen two pencils.", "Start the warm-up."], "es", ["Cuelga tu mochila.", "Mueve tu clip del almuerzo.", "Entrega tu carpeta.", "Saca punta a dos lápices.", "Empieza el ejercicio."]),
        new("routine", "Lining up", ["Push in your chair.", "Walk to the door.", "Stand behind the line."], "ar", ["ادفع كرسيك إلى الداخل.", "امشِ إلى الباب.", "قف خلف الخط."]),
        new("routine", "Packing your backpack", ["Put your folder in first.", "Add your lunch box.", "Zip the big pocket.", "Check the floor around you.", "Put on your backpack."], "zh", ["先把文件夹放进去。", "放入你的午餐盒。", "拉上大口袋的拉链。", "检查你周围的地面。", "背上你的书包。"]),
        new("routine", "Lunch count", ["Find your name card.", "Move it to your lunch choice.", "Sit at your desk."]),
        new("sel", "Using the calm corner", ["Tell an adult you need a break.", "Set the sand timer.", "Choose one calm tool.", "Return when the sand runs out."], Symbols: ["agency.break.v1", "agency.finished.v1"]),
        new("sel", "Asking for help", ["Raise your hand.", "Wait for the teacher to come.", "Say or point to the problem."], Symbols: ["agency.help.v1", "agency.wait.v1"]),
    ];

    public static TheoryData<int> FixtureIndexes()
    {
        var data = new TheoryData<int>();
        for (var index = 0; index < Fixtures.Count; index++)
        {
            data.Add(index);
        }

        return data;
    }

    private static readonly DateTimeOffset SomeInstant = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    private static JsonAssetCatalog Catalog()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpenClassroomFoundry.slnx")))
        {
            directory = directory.Parent;
        }

        return new JsonAssetCatalog(Path.Combine(directory!.FullName, "assets", "symbols"));
    }

    [Fact]
    public void The_corpus_is_thirty_fixtures_wide_and_deep()
    {
        Assert.Equal(30, Fixtures.Count);
        Assert.True(Fixtures.Select(f => f.Subject).Distinct().Count() >= 8, "At least eight subjects.");
        Assert.True(Fixtures.Count(f => f.TargetLocale is not null) >= 4, "At least four bilingual fixtures.");
        Assert.Equal(3, Fixtures.Where(f => f.TargetLocale is not null).Select(f => f.TargetLocale).Distinct().Count());
        Assert.True(Fixtures.Count(f => f.Symbols is not null) >= 2, "At least two symbol-bearing fixtures.");
    }

    [Theory]
    [MemberData(nameof(FixtureIndexes))]
    public async Task Every_fixture_builds_validates_approves_and_renders_for_screen_and_paper(int fixtureIndex)
    {
        var fixture = Fixtures[fixtureIndex];
        var catalog = Catalog();

        var steps = fixture.Steps.Select((text, index) => new StepSpec(
            text,
            fixture.Symbols is not null && index < fixture.Symbols.Length ? new AssetId(fixture.Symbols[index]) : null,
            fixture.TargetSteps?[index])).ToList();

        var document = AllAboardBuilders.TaskStrip(
            fixture.Title, steps, catalog, targetLocale: fixture.TargetLocale);

        var issues = DocumentValidator.Validate(document);
        Assert.False(DocumentValidator.HasBlockingIssues(issues), fixture.ToString());

        var draft = DraftArtifact.New(document, DataLane.Green);
        var reviewedAssets = ExactAssetCatalogSnapshot.CaptureForReview(document, catalog);
        var approved = ApprovalGate.Approve(
            draft,
            "teacher@example.org",
            issues,
            SomeInstant,
            reviewedAssets.Bindings);

        var renderer = new AccessibleHtmlRenderer(catalog);
        var learner = Encoding.UTF8.GetString(
            (await renderer.RenderAsync(approved, new RenderRequest(RenderTarget.AccessibleHtml), CancellationToken.None)).Content.Span);
        var print = Encoding.UTF8.GetString(
            (await renderer.RenderAsync(approved, new RenderRequest(RenderTarget.PrintHtml), CancellationToken.None)).Content.Span);

        Assert.Contains(fixture.Title, learner, StringComparison.Ordinal);
        Assert.DoesNotContain("<aside class=\"teacher-only\"", learner, StringComparison.Ordinal);
        Assert.DoesNotContain("Approved by", learner, StringComparison.Ordinal);
        Assert.Contains("@page", print, StringComparison.Ordinal);

        if (fixture.TargetLocale is not null)
        {
            Assert.Contains($"lang=\"{fixture.TargetLocale}\"", learner, StringComparison.Ordinal);
            Assert.Contains(fixture.TargetSteps![0], learner, StringComparison.Ordinal);
        }
    }
}
