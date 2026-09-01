// SPDX-License-Identifier: GPL-3.0-or-later
using System.Security.Cryptography;
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
    public const int MaximumPolicyFileBytes = 64 * 1024;
    public const int MaximumPolicyJsonDepth = 8;

    private static readonly HashSet<string> RequiredRootProperties = new(StringComparer.Ordinal)
    {
        "allowedEndpoints",
        "providerId",
        "deploymentId",
        "maximumLane",
        "cloudInferenceEnabled",
    };

    private static readonly HashSet<string> AllowedRootProperties = new(RequiredRootProperties, StringComparer.Ordinal)
    {
        "safeguardingProcedureText",
    };

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
        byte[]? policyBytes = null;
        try
        {
            if (!File.Exists(path))
            {
                return DistrictPolicy.Offline;
            }

            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.SequentialScan);

            policyBytes = GC.AllocateUninitializedArray<byte>(MaximumPolicyFileBytes + 1);
            var byteCount = ReadAtMost(stream, policyBytes);
            if (byteCount is <= 0 or > MaximumPolicyFileBytes)
            {
                throw new InvalidDataException("The district policy file is empty or oversized.");
            }

            using var document = JsonDocument.Parse(
                policyBytes.AsMemory(0, byteCount),
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = MaximumPolicyJsonDepth,
                });
            ValidateExactRoot(document.RootElement);

            var policy = JsonSerializer.Deserialize<DistrictPolicy>(
                policyBytes.AsSpan(0, byteCount),
                DistrictPolicyJson.Options)
                ?? throw new InvalidDataException("The district policy file did not contain a policy.");
            ValidatePolicy(policy);
            return policy;
        }
        catch (Exception exception) when (exception is
            JsonException or
            InvalidDataException or
            InvalidOperationException or
            IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException)
        {
            // Fail closed: a corrupt or unreadable policy grants nothing.
            return DistrictPolicy.Offline;
        }
        finally
        {
            if (policyBytes is not null)
            {
                CryptographicOperations.ZeroMemory(policyBytes);
            }
        }
    }

    private static int ReadAtMost(Stream stream, byte[] destination)
    {
        var offset = 0;
        while (offset < destination.Length)
        {
            var read = stream.Read(destination, offset, destination.Length - offset);
            if (read == 0)
            {
                break;
            }

            offset += read;
        }

        return offset;
    }

    private static void ValidateExactRoot(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("The district policy root must be an object.");
        }

        var exactNames = new HashSet<string>(StringComparer.Ordinal);
        var portableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in root.EnumerateObject())
        {
            if (!portableNames.Add(property.Name) || !exactNames.Add(property.Name))
            {
                throw new InvalidDataException(
                    "The district policy contains a duplicate or case-confusable property.");
            }

            if (!AllowedRootProperties.Contains(property.Name))
            {
                throw new InvalidDataException("The district policy contains an unknown property.");
            }
        }

        if (!RequiredRootProperties.IsSubsetOf(exactNames))
        {
            throw new InvalidDataException("The district policy is missing a required property.");
        }

        var maximumLane = root.GetProperty("maximumLane");
        if (maximumLane.ValueKind != JsonValueKind.String
            || maximumLane.GetString() is not ("green" or "amber"))
        {
            throw new InvalidDataException(
                "The district policy maximum lane must be a camel-case Green or Amber string.");
        }
    }

    private static void ValidatePolicy(DistrictPolicy policy)
    {
        if (policy.AllowedEndpoints is null
            || policy.AllowedEndpoints.Any(endpoint => !IsValidInferenceEndpoint(endpoint))
            || policy.MaximumLane is not (DataLane.Green or DataLane.Amber)
            || (policy.CloudInferenceEnabled
                && (policy.AllowedEndpoints.Count == 0
                    || string.IsNullOrWhiteSpace(policy.ProviderId)
                    || string.IsNullOrWhiteSpace(policy.DeploymentId))))
        {
            throw new InvalidDataException("The district policy contains an incomplete grant.");
        }
    }

    private static bool IsValidInferenceEndpoint(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint)
            || !Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        try
        {
            // Keep this platform-policy admission check aligned with the
            // inference boundary without making the Windows adapter depend on
            // a concrete or abstract inference provider project.
            return uri.IsAbsoluteUri
                && uri.Scheme == Uri.UriSchemeHttps
                && !string.IsNullOrWhiteSpace(uri.IdnHost)
                && string.IsNullOrEmpty(uri.UserInfo);
        }
        catch (UriFormatException)
        {
            return false;
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

internal static class DistrictPolicyJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        AllowTrailingCommas = false,
        MaxDepth = JsonDistrictPolicyProvider.MaximumPolicyJsonDepth,
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };
}
