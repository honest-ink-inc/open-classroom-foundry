using Foundry.Domain;

namespace Foundry.Contracts;

/// <summary>
/// IT-controlled district policy from ProgramData (plan §6.4). The absence of a
/// policy file is not an open door: the default is <see cref="Offline"/> — no
/// endpoints, no cloud inference, Green lane only. Districts grant capability;
/// its absence removes it.
/// </summary>
public sealed record DistrictPolicy(
    IReadOnlyList<string> AllowedEndpoints,
    string? ProviderId,
    string? DeploymentId,
    DataLane MaximumLane,
    bool CloudInferenceEnabled,
    string? SafeguardingProcedureText = null)
{
    public static DistrictPolicy Offline { get; } = new([], null, null, DataLane.Green, false);
}

/// <summary>Teacher-owned preferences from LocalAppData — conveniences, never policy.</summary>
public sealed record TeacherPreferences(
    string PreferredSourceLocale = "en",
    string? PreferredTargetLocale = null,
    bool DuplexDefault = false,
    int PrintCopiesDefault = 1);

public interface IDistrictPolicyProvider
{
    DistrictPolicy Current { get; }
}

public interface ITeacherPreferencesStore
{
    TeacherPreferences Load();

    void Save(TeacherPreferences preferences);
}
