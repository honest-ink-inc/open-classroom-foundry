// SPDX-License-Identifier: GPL-3.0-or-later

using Foundry.Contracts;
using Foundry.Modules.BuiltIn.AllAboard;

namespace Foundry.Modules.BuiltIn;

/// <summary>
/// One public module identity paired with the legacy key that remains in
/// recipes, schemas, localization ids, and saved projects. Display-name
/// changes must not silently become project-format migrations.
/// </summary>
public sealed record ModulePublicName(
    string LegacyId,
    string Name,
    string Subtitle,
    string FileStem)
{
    public string DisplayName => $"{Name} — {Subtitle}";
}

/// <summary>
/// The single code registry for module names adopted by ADR-008. Internal
/// classes and wire identities deliberately retain their established names.
/// </summary>
public static class ModulePublicIdentity
{
    public static ModulePublicName VisualSupport { get; } = new(
        "all-aboard",
        "SequenceSlate",
        "Visual Support Studio",
        "sequenceslate");

    public static ModulePublicName LessonDesign { get; } = new(
        "lesson-loom",
        "GridLesson",
        "Lesson Design Studio",
        "gridlesson");

    public static ModulePublicName DiscussionDesign { get; } = new(
        "talk-moves",
        "Forumwright",
        "Discussion Design",
        "forumwright");

    public static ModulePublicName FormativeEvidence { get; } = new(
        "exit-lens",
        "ReteachSignal",
        "Formative Evidence",
        "reteachsignal");

    public static ModulePublicName SourceInquiry { get; } = new(
        "source-lens",
        "Inquirywright",
        "Source & Inquiry",
        "inquirywright");

    public static ModulePublicName FamilyCommunication { get; } = new(
        "family-bridge",
        "KinDispatch",
        "Bilingual & Family Press",
        "kindispatch");

    public static IReadOnlyList<ModulePublicName> All { get; } =
    [
        VisualSupport,
        LessonDesign,
        DiscussionDesign,
        FormativeEvidence,
        SourceInquiry,
        FamilyCommunication,
    ];

    public static ModulePublicName? FindByLegacyId(string legacyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(legacyId);
        return All.FirstOrDefault(identity =>
            string.Equals(identity.LegacyId, legacyId, StringComparison.Ordinal));
    }

    public static string FileStemFor(RecipeManifest recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        var prefix = VisualSupport.LegacyId + ".";
        if (!recipe.Id.StartsWith(prefix, StringComparison.Ordinal)
            || !AllAboardRecipes.All.Any(candidate =>
                string.Equals(candidate.Id, recipe.Id, StringComparison.Ordinal)))
        {
            throw new ArgumentException("Unknown SequenceSlate recipe.", nameof(recipe));
        }

        var suffix = recipe.Id[prefix.Length..];
        return $"{VisualSupport.FileStem}-{suffix.Replace('.', '-')}";
    }

    public static string FileStemFor(ModuleDoorDefinition door, ModuleModeDefinition mode)
    {
        ArgumentNullException.ThrowIfNull(door);
        ArgumentNullException.ThrowIfNull(mode);
        if (!door.Modes.Contains(mode))
        {
            throw new ArgumentException("Module and mode do not match.", nameof(mode));
        }

        if (door.Modes.Count == 1)
        {
            return door.PublicFileStem;
        }

        var prefix = door.Id + ".";
        var suffix = mode.Key.StartsWith(prefix, StringComparison.Ordinal)
            ? mode.Key[prefix.Length..]
            : mode.Key;
        return $"{door.PublicFileStem}-{suffix.Replace('.', '-')}";
    }
}
