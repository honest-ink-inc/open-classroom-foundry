using System.Text.RegularExpressions;

namespace Foundry.Domain;

/// <summary>
/// The content-free diagnostic record of implementation plan §7. Every field is an
/// identifier, a state, a duration, or a count — there is no field that could carry
/// source or output content, and <see cref="DiagnosticPolicy"/> rejects identifier
/// strings shaped like prose so content cannot be smuggled through a code field.
/// </summary>
public sealed record DiagnosticEvent(
    string EventCode,
    string OutcomeCategory,
    string? ModuleId = null,
    string? RecipeId = null,
    string? RecipeVersion = null,
    string? ProviderId = null,
    JobState? FromState = null,
    JobState? ToState = null,
    TimeSpan? Duration = null,
    string? MediaClass = null,
    int? InputTokens = null,
    int? OutputTokens = null);

public static partial class DiagnosticPolicy
{
    private const int MaxIdentifierLength = 64;

    private static readonly HashSet<string> AllowedMediaClasses =
        ["image", "pdf", "document", "text", "none"];

    private static readonly HashSet<string> AllowedOutcomeCategories =
        ["success", "declined", "cancelled", "blocked", "provider-error", "validation-error", "purge-incomplete"];

    // Lowercase identifier segments joined by '.' or '-'. Digits may lead a segment
    // so version identifiers like "1.0.0" pass; prose (spaces, uppercase) never does.
    [GeneratedRegex("^[a-z0-9]+([.-][a-z0-9]+)*$")]
    private static partial Regex IdentifierPattern();

    public static IReadOnlyList<ValidationIssue> Validate(DiagnosticEvent diagnosticEvent)
    {
        ArgumentNullException.ThrowIfNull(diagnosticEvent);

        var issues = new List<ValidationIssue>();

        RequireIdentifier(issues, diagnosticEvent.EventCode, "diag.event-code");
        RequireIdentifier(issues, diagnosticEvent.OutcomeCategory, "diag.outcome");

        if (!AllowedOutcomeCategories.Contains(diagnosticEvent.OutcomeCategory))
        {
            issues.Add(ValidationIssue.Blocking("diag.outcome.unknown", "Outcome category is not in the allowlist."));
        }

        AllowIdentifier(issues, diagnosticEvent.ModuleId, "diag.module-id");
        AllowIdentifier(issues, diagnosticEvent.RecipeId, "diag.recipe-id");
        AllowIdentifier(issues, diagnosticEvent.RecipeVersion, "diag.recipe-version");
        AllowIdentifier(issues, diagnosticEvent.ProviderId, "diag.provider-id");

        if (diagnosticEvent.MediaClass is not null && !AllowedMediaClasses.Contains(diagnosticEvent.MediaClass))
        {
            issues.Add(ValidationIssue.Blocking("diag.media-class", "Media class is not in the broad-class allowlist."));
        }

        return issues;
    }

    public static bool IsContentFree(DiagnosticEvent diagnosticEvent)
        => !DocumentValidator.HasBlockingIssues(Validate(diagnosticEvent));

    private static void RequireIdentifier(List<ValidationIssue> issues, string value, string code)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxIdentifierLength || !IdentifierPattern().IsMatch(value))
        {
            issues.Add(ValidationIssue.Blocking(code, "Value must be a short lowercase identifier, never free text."));
        }
    }

    private static void AllowIdentifier(List<ValidationIssue> issues, string? value, string code)
    {
        if (value is not null)
        {
            RequireIdentifier(issues, value, code);
        }
    }
}
