// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json;
using Foundry.Domain;

namespace Foundry.Modules.BuiltIn.AllAboard;

/// <summary>The strict structured shape a provider may propose for a task strip — nothing else.</summary>
public sealed record TaskStripSuggestion(string Title, IReadOnlyList<string> Steps);

/// <summary>
/// Deterministic parsing of provider output against schema.all-aboard.v1. Unmapped
/// members are refused, so a payload smuggling extra fields ("tool_call", "admin",
/// instructions of any kind) is malformed output — not a request the engine obeys.
/// Every accepted string is plain text: a proposed step that says "ignore previous
/// instructions" is a badly written step the teacher will delete, never a command.
/// </summary>
public static class TaskStripSuggestionParser
{
    public const int MaximumStepLength = 300;

    /// <summary>
    /// The strict JSON Schema for schema.all-aboard.v1, registered so schema-binding
    /// providers make out-of-shape output unrepresentable at generation time. The
    /// parser enforces the same bounds after the fact for every provider.
    /// </summary>
    public const string SchemaJson = """
        {
          "type": "object",
          "properties": {
            "title": { "type": "string", "minLength": 1 },
            "steps": {
              "type": "array",
              "items": { "type": "string", "minLength": 1, "maxLength": 300 },
              "minItems": 3,
              "maxItems": 8
            }
          },
          "required": ["title", "steps"],
          "additionalProperties": false
        }
        """;

    private static readonly JsonSerializerOptions Strict = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
    };

    public static (TaskStripSuggestion? Suggestion, IReadOnlyList<ValidationIssue> Issues) Parse(string structuredJson)
    {
        ArgumentNullException.ThrowIfNull(structuredJson);

        TaskStripSuggestion? suggestion;
        try
        {
            suggestion = JsonSerializer.Deserialize<TaskStripSuggestion>(structuredJson, Strict);
        }
        catch (JsonException)
        {
            return (null, [ValidationIssue.Blocking("suggestion.malformed", "The provider's output does not match the task-strip schema.")]);
        }

        if (suggestion is null)
        {
            return (null, [ValidationIssue.Blocking("suggestion.malformed", "The provider returned an empty suggestion.")]);
        }

        var issues = new List<ValidationIssue>();

        if (string.IsNullOrWhiteSpace(suggestion.Title))
        {
            issues.Add(ValidationIssue.Blocking("suggestion.title", "The suggested strip has no title."));
        }

        if (suggestion.Steps is null || suggestion.Steps.Count is < AllAboardBuilders.MinimumSteps or > AllAboardBuilders.MaximumSteps)
        {
            issues.Add(ValidationIssue.Blocking(
                "suggestion.step-count",
                $"A task strip has {AllAboardBuilders.MinimumSteps} to {AllAboardBuilders.MaximumSteps} steps."));
        }
        else
        {
            foreach (var step in suggestion.Steps)
            {
                if (string.IsNullOrWhiteSpace(step))
                {
                    issues.Add(ValidationIssue.Blocking("suggestion.blank-step", "A suggested step is blank."));
                }
                else if (step.Length > MaximumStepLength)
                {
                    issues.Add(ValidationIssue.Blocking("suggestion.step-too-long", "A suggested step is too long to be one action."));
                }
            }
        }

        return issues.Count > 0 ? (null, issues) : (suggestion, issues);
    }

    /// <summary>Suggestions arrive symbol-less: symbols are teacher choices, never model choices.</summary>
    public static IReadOnlyList<StepSpec> ToSteps(TaskStripSuggestion suggestion)
    {
        ArgumentNullException.ThrowIfNull(suggestion);
        return [.. suggestion.Steps.Select(s => new StepSpec(s))];
    }
}
