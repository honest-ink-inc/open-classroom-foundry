// SPDX-License-Identifier: GPL-3.0-or-later
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Foundry.Contracts;

/// <summary>
/// Computes a cross-platform fingerprint of every declarative field in one
/// <see cref="RecipeManifest"/>. This is a contract-content fingerprint, not a
/// signature and not proof of the recipe's executable builder or renderer.
/// </summary>
public static class RecipeContractFingerprint
{
    public const string FramingVersion = "recipe-contract-fingerprint.v2";

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static string ComputeSha256(RecipeManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendString(hash, FramingVersion);
        AppendNamedString(hash, "id", manifest.Id);
        AppendNamedString(hash, "version", manifest.Version);
        AppendNamedString(hash, "license", manifest.License);
        AppendNamedString(hash, "minimumEngineVersion", manifest.MinimumEngineVersion);
        AppendNamedString(hash, "instructionalPurpose", manifest.InstructionalPurpose);
        AppendNamedStrings(hash, "prohibitedPurposes", manifest.ProhibitedPurposes);
        AppendNamedStrings(hash, "allowedInputKinds", manifest.AllowedInputKinds);
        AppendNamedInt32(hash, "maximumLane", (int)manifest.MaximumLane);
        AppendNamedStrings(hash, "requiredProviderCapabilities", manifest.RequiredProviderCapabilities);
        AppendNamedString(hash, "outputSchemaId", manifest.OutputSchemaId);
        AppendNamedStrings(hash, "localPreprocessingIds", manifest.LocalPreprocessingIds);
        AppendNamedStrings(hash, "validatorIds", manifest.ValidatorIds);
        AppendNamedString(hash, "editorId", manifest.EditorId);
        AppendNamedString(hash, "rendererId", manifest.RendererId);
        AppendNamedInt32s(hash, "supportedExports", manifest.SupportedExports.Select(value => (int)value));
        AppendNamedStrings(hash, "warnings", manifest.Warnings);
        AppendNamedStrings(hash, "localizationResourceIds", manifest.LocalizationResourceIds);
        AppendNamedStrings(hash, "migrationIds", manifest.MigrationIds);
        AppendNamedString(hash, "evaluationSuiteVersion", manifest.EvaluationSuiteVersion);
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void AppendNamedString(IncrementalHash hash, string name, string value)
    {
        AppendString(hash, name);
        AppendString(hash, value);
    }

    private static void AppendNamedStrings(
        IncrementalHash hash,
        string name,
        IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        AppendString(hash, name);
        AppendInt32(hash, values.Count);
        foreach (var value in values)
        {
            AppendString(hash, value);
        }
    }

    private static void AppendNamedInt32(IncrementalHash hash, string name, int value)
    {
        AppendString(hash, name);
        AppendInt32(hash, value);
    }

    private static void AppendNamedInt32s(
        IncrementalHash hash,
        string name,
        IEnumerable<int> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var materialized = values.ToArray();
        AppendString(hash, name);
        AppendInt32(hash, materialized.Length);
        foreach (var value in materialized)
        {
            AppendInt32(hash, value);
        }
    }

    private static void AppendString(IncrementalHash hash, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var bytes = StrictUtf8.GetBytes(value);
        AppendInt32(hash, bytes.Length);
        hash.AppendData(bytes);
    }

    private static void AppendInt32(IncrementalHash hash, int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        hash.AppendData(bytes);
    }
}
