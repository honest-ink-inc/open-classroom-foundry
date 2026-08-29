using System.Text.Json;
using System.Text.Json.Serialization;
using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Infrastructure.Windows;

/// <summary>
/// Reads IT-controlled policy from ProgramData (plan §6.4). Missing or unreadable
/// policy fails closed to <see cref="DistrictPolicy.Offline"/>: no endpoints, no
/// cloud inference, Green lane only. Policy is read once per process; changes
/// take effect on restart.
/// </summary>
public sealed class JsonDistrictPolicyProvider : IDistrictPolicyProvider
{
    public const string PolicyFileName = "policy.json";

    public JsonDistrictPolicyProvider(string? policyDirectory = null)
    {
        var directory = policyDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            EngineIdentity.InternalId,
            "policy");

        PolicyPath = Path.Combine(directory, PolicyFileName);
        Current = Load(PolicyPath);
    }

    public string PolicyPath { get; }

    public DistrictPolicy Current { get; }

    private static DistrictPolicy Load(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return DistrictPolicy.Offline;
            }

            return JsonSerializer.Deserialize<DistrictPolicy>(File.ReadAllText(path), PolicyJson.Options)
                ?? DistrictPolicy.Offline;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            // Fail closed: a corrupt or unreadable policy grants nothing.
            return DistrictPolicy.Offline;
        }
    }
}

/// <summary>Teacher preferences in LocalAppData — conveniences that reset harmlessly if unreadable.</summary>
public sealed class JsonTeacherPreferencesStore : ITeacherPreferencesStore
{
    public const string PreferencesFileName = "preferences.json";

    private readonly string _preferencesPath;

    public JsonTeacherPreferencesStore(string? preferencesDirectory = null)
    {
        var directory = preferencesDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            EngineIdentity.InternalId);

        _preferencesPath = Path.Combine(directory, PreferencesFileName);
    }

    public TeacherPreferences Load()
    {
        try
        {
            if (!File.Exists(_preferencesPath))
            {
                return new TeacherPreferences();
            }

            return JsonSerializer.Deserialize<TeacherPreferences>(File.ReadAllText(_preferencesPath), PolicyJson.Options)
                ?? new TeacherPreferences();
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return new TeacherPreferences();
        }
    }

    public void Save(TeacherPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        Directory.CreateDirectory(Path.GetDirectoryName(_preferencesPath)!);
        File.WriteAllText(_preferencesPath, JsonSerializer.Serialize(preferences, PolicyJson.Options));
    }
}

internal static class PolicyJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };
}
