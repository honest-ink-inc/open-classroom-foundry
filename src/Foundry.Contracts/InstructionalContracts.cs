namespace Foundry.Contracts;

// The shared target/evidence/decision vocabulary of Release 0.3 (plan §13): the
// Green planning studio modules speak the same instructional language, so their
// artifacts compose and their evaluations compare.

/// <summary>Backward alignment starts here: what learners will know or do, and what counts as evidence of it.</summary>
public sealed record LearningTarget(string Statement, string EvidenceOfLearning);

/// <summary>One formative "If you see X, then Y" — a check is only real when it has a planned response.</summary>
public sealed record InstructionalDecision(string WhenYouSee, string Then);
