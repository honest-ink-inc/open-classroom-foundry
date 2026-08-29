using Foundry.Domain;
using Foundry.Modules.BuiltIn.ExitLens;
using Foundry.Modules.BuiltIn.RubricRelay;
using Xunit;

namespace Foundry.Tests.Unit;

/// <summary>Release 0.6's Amber kernel — every fixture here is synthetic by constitution.</summary>
public class ExitLensTests
{
    private const string Canary = "CANARY-the-student-wrote-seven-instead-of-nine";

    private static ExitLensSession Session()
    {
        var session = new ExitLensSession("Regroup across a zero when subtracting", suppressionThreshold: 4);
        session.DefineCluster("Secure", null);
        session.DefineCluster("Subtracts smaller from larger", "Treats each column independently");
        return session;
    }

    [Fact]
    public void Every_response_must_land_somewhere_before_a_summary_exists()
    {
        var session = Session();
        session.AddResponse("403 - 138 = 335");
        session.Assign(0, "Subtracts smaller from larger");
        session.AddResponse("(illegible)");

        var blocked = session.Summarize();
        Assert.Contains(blocked.Issues, i => i.Code == "exitlens.unassigned");

        session.Assign(1, ExitLensSession.Unreadable);
        session.SetRoute("Subtracts smaller from larger", "Re-model with base-ten blocks in a pulled group.");
        session.SetRoute("Secure", "Extension: three-digit differences with two zeros.");

        Assert.False(DocumentValidator.HasBlockingIssues(session.Summarize().Issues));
    }

    [Fact]
    public void A_cluster_with_responses_but_no_route_is_a_label_not_a_plan()
    {
        var session = Session();
        session.Assign(session.AddResponse("some synthetic response"), "Secure");

        Assert.Contains(session.Summarize().Issues, i => i.Code == "exitlens.route");
    }

    [Fact]
    public void The_summary_holds_counts_never_response_text_and_purges_in_the_same_act()
    {
        var session = Session();
        for (var i = 0; i < 5; i++)
        {
            session.Assign(session.AddResponse($"{Canary} #{i}"), "Subtracts smaller from larger");
        }

        session.SetRoute("Subtracts smaller from larger", "Re-model with blocks.");
        var result = session.Summarize();

        Assert.False(DocumentValidator.HasBlockingIssues(result.Issues));
        Assert.DoesNotContain(
            DocumentText.CollectStrings(result.Document),
            text => text.Contains(Canary, StringComparison.Ordinal));
        Assert.Equal(0, session.ResponsesRemaining);
    }

    [Fact]
    public void Small_clusters_report_suppressed_counts_without_claiming_deidentification()
    {
        var session = Session();
        for (var i = 0; i < 6; i++)
        {
            session.Assign(session.AddResponse($"synthetic {i}"), "Secure");
        }

        session.Assign(session.AddResponse("synthetic outlier"), ExitLensSession.Novel);
        session.SetRoute("Secure", "Extension set.");

        var text = string.Join('\n', DocumentText.CollectStrings(session.Summarize().Document));

        Assert.Contains("fewer than 4", text, StringComparison.Ordinal);
        Assert.DoesNotContain("de-identif", text.Replace("without any claim of guaranteed de-identification", ""), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("without any claim of guaranteed de-identification", text, StringComparison.Ordinal);
    }

    [Fact]
    public void At_most_two_hinge_questions_and_the_recipe_is_amber_synthetic_only()
    {
        var session = Session();
        session.AddHingeQuestion("Is 507 - 129 more or less than 400?", "Move on", "Re-model");
        session.AddHingeQuestion("Second", "a", "b");
        Assert.Throws<InvalidOperationException>(() => session.AddHingeQuestion("Third", "a", "b"));

        Assert.Equal(DataLane.Amber, ExitLensSession.Recipe.MaximumLane);
        Assert.Contains(ExitLensSession.Recipe.ProhibitedPurposes, p => p.Contains("synthetic fixtures only", StringComparison.Ordinal));
    }
}

public class RubricRelayTests
{
    private const string SyntheticEssay =
        "The author builds suspense by ending the chapter mid-sentence. We never learn who knocked. " +
        "I think the knocking is the neighbor because of the muddy boots on page 12.";

    private static IReadOnlyList<CriterionEvidence> SoundMatrix() =>
    [
        new CriterionEvidence("Names a craft technique", EvidenceStatus.EvidenceFound, "ending the chapter mid-sentence"),
        new CriterionEvidence("Cites text evidence", EvidenceStatus.EvidenceFound, "the muddy boots on page 12"),
        new CriterionEvidence("Explains effect on the reader", EvidenceStatus.Insufficient, TeacherNote: "Gestures at effect but never states it."),
    ];

    private static RubricRelayResult Build(IReadOnlyList<CriterionEvidence>? matrix = null) => RubricRelayBuilder.Build(
        "Chapter 9 suspense analysis",
        SyntheticEssay,
        matrix ?? SoundMatrix(),
        oneStrength: "You anchored your inference to a page-specific detail.",
        oneRevisionMove: "State the effect on the reader in one sentence, then defend it.",
        conferenceQuestion1: "What do you want the reader to feel at the knock - and where does your draft do that work?",
        conferenceQuestion2: "Which of your two details works harder, and why?");

    [Fact]
    public void Conference_preparation_builds_with_the_matrix_one_strength_one_move_two_questions()
    {
        var result = Build();

        Assert.False(DocumentValidator.HasBlockingIssues(result.Issues));
        Assert.StartsWith("Conference preparation:", ((Heading)result.Document.Nodes[0]).Text, StringComparison.Ordinal);
        Assert.Equal(2, result.Document.Nodes.OfType<Card>().Count());
        Assert.Equal(2, result.Document.Nodes.OfType<OrderedSteps>().Single().Steps.Count);
    }

    [Fact]
    public void A_quote_that_is_not_verbatim_blocks_at_one_hundred_percent_fidelity()
    {
        var matrix = new List<CriterionEvidence>(SoundMatrix())
        {
            [0] = new CriterionEvidence("Names a craft technique", EvidenceStatus.EvidenceFound, "ending the chapter mid-scene"),
        };

        Assert.Contains(Build(matrix).Issues, i => i.Code == "relay.quotation");
    }

    [Fact]
    public void No_evidence_means_no_claim_in_both_directions()
    {
        var claimWithoutQuote = new List<CriterionEvidence>(SoundMatrix())
        {
            [0] = new CriterionEvidence("Names a craft technique", EvidenceStatus.EvidenceFound),
        };
        Assert.Contains(Build(claimWithoutQuote).Issues, i => i.Code == "relay.evidence");

        var quoteWithoutEvidence = new List<CriterionEvidence>(SoundMatrix())
        {
            [2] = new CriterionEvidence("Explains effect", EvidenceStatus.NoEvidence, "the muddy boots on page 12"),
        };
        Assert.Contains(Build(quoteWithoutEvidence).Issues, i => i.Code == "relay.claim");
    }

    [Fact]
    public void No_numeric_field_exists_so_a_score_cannot_be_represented()
    {
        // Structural: CriterionEvidence and the builder expose no numeric type anywhere.
        Assert.DoesNotContain(
            typeof(CriterionEvidence).GetProperties(),
            p => p.PropertyType == typeof(int) || p.PropertyType == typeof(double) || p.PropertyType == typeof(decimal));

        var text = string.Join('\n', DocumentText.CollectStrings(Build().Document));
        Assert.Contains("no score exists here and none can be derived", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_recipe_is_amber_synthetic_only_and_forbids_the_bright_lines()
    {
        Assert.Equal(DataLane.Amber, RubricRelayBuilder.Recipe.MaximumLane);
        Assert.Contains(RubricRelayBuilder.Recipe.ProhibitedPurposes, p => p.Contains("AI-authorship", StringComparison.Ordinal));
        Assert.Contains(RubricRelayBuilder.Recipe.ProhibitedPurposes, p => p.Contains("synthetic fixtures only", StringComparison.Ordinal));
    }
}
