// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Modules.BuiltIn.TalkMoves;

/// <summary>A discussion question is only major when it knows its purpose and its evidence target.</summary>
public sealed record DiscussionQuestion(string Question, string Purpose, string EvidenceTarget);

public sealed record TalkMovesResult(ArtifactDocument Document, IReadOnlyList<ValidationIssue> Issues);

/// <summary>
/// Forumwright (stable legacy id: talk-moves; plan §10.9): equitable, intellectually productive discussion.
/// Participation is not airtime: at least three modes plus an always-appended
/// wait/pass option; the five facilitation move families are required; the equity
/// reflection prints as a teacher-only card — used standing up, after the bell.
/// </summary>
public static class TalkMovesBuilder
{
    public const string PassOption = "Wait or pass — always okay";

    public static TalkMovesResult Build(
        string topic,
        IReadOnlyList<DiscussionQuestion> questions,
        IReadOnlyList<string> participationModes,
        string inviteMove,
        string buildMove,
        string pressForEvidenceMove,
        string repairMove,
        string synthesizeMove,
        IReadOnlyList<string>? sentenceFrames = null,
        string language = "en")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(questions);
        ArgumentNullException.ThrowIfNull(participationModes);
        LanguageTag.RequireValid(language, nameof(language));

        var issues = new List<ValidationIssue>();

        if (questions.Count == 0)
        {
            issues.Add(ValidationIssue.Blocking("talk.questions", "A discussion needs at least one planned question."));
        }

        for (var i = 0; i < questions.Count; i++)
        {
            var q = questions[i];
            if (string.IsNullOrWhiteSpace(q.Question) || string.IsNullOrWhiteSpace(q.Purpose) || string.IsNullOrWhiteSpace(q.EvidenceTarget))
            {
                issues.Add(ValidationIssue.Blocking("talk.question-map",
                    $"Question {i + 1} must map to a purpose and an evidence target."));
            }
        }

        if (participationModes.Count < 3)
        {
            issues.Add(ValidationIssue.Blocking("talk.modes",
                "At least three participation modes (speaking, writing, pointing, drawing, AAC, partner-supported...) beyond wait/pass."));
        }

        foreach (var (move, family) in new[]
        {
            (inviteMove, "invite"), (buildMove, "build"), (pressForEvidenceMove, "press for evidence"),
            (repairMove, "repair"), (synthesizeMove, "synthesize"),
        })
        {
            if (string.IsNullOrWhiteSpace(move))
            {
                issues.Add(ValidationIssue.Blocking("talk.moves", $"The '{family}' facilitation move is missing."));
            }
        }

        var nodes = new List<DocumentNode>
        {
            new Heading(1, topic),
            new Heading(2, "Our questions"),
            new UnorderedList([.. questions.Select(q => q.Question)]),
            new Heading(2, "Ways to take part"),
            new UnorderedList([.. participationModes, PassOption]),
        };

        if (sentenceFrames is { Count: > 0 })
        {
            nodes.Add(new Heading(2, "Sentence starters (optional)"));
            nodes.Add(new UnorderedList(sentenceFrames));
        }

        foreach (var q in questions)
        {
            nodes.Add(new TeacherOnlyNotice($"Q: {q.Question} | Purpose: {q.Purpose} | Evidence to press for: {q.EvidenceTarget}"));
        }

        nodes.Add(new TeacherOnlyNotice(
            $"Facilitation moves - Invite: {inviteMove} | Build: {buildMove} | Press for evidence: {pressForEvidenceMove} | Repair: {repairMove} | Synthesize: {synthesizeMove}"));

        nodes.Add(new TeacherOnlyNotice(
            "Equity reflection (90 seconds, after the bell): Who contributed, in any mode? Who was invited and passed - and was the pass honored? Whose idea got built on? Who was pressed for evidence - challenge spread fairly? One change for next time."));

        var document = new ArtifactDocument(nodes, language);
        issues.AddRange(DocumentValidator.Validate(document));
        return new TalkMovesResult(document, issues);
    }

    public static RecipeManifest Recipe { get; } = new(
        Id: "talk-moves-studio",
        Version: "0.1.0",
        License: "GPL-3.0-or-later",
        MinimumEngineVersion: EngineIdentity.EngineVersion,
        InstructionalPurpose: "Design equitable, intellectually productive discussion: purposeful questions, multimodal participation, and disciplined facilitation moves.",
        ProhibitedPurposes:
        [
            "participation ranking or individual analytics",
            "forced personal disclosure",
            "scripted learner opinions or fixed passive roles",
            "recordings, transcripts, or speaker analytics",
        ],
        AllowedInputKinds: ["teacher-entered-text"],
        MaximumLane: DataLane.Green,
        RequiredProviderCapabilities: [],
        OutputSchemaId: "schema.talk-moves.v1",
        ValidatorIds: ["document.structural"],
        EditorId: "editor.review-session",
        RendererId: "renderer.accessible-html",
        SupportedExports: [RenderTarget.AccessibleHtml, RenderTarget.PrintHtml],
        Warnings: ["Challenge claims and evidence, never dignity. Wait and pass are honored contributions."],
        EvaluationSuiteVersion: "0.1");
}
