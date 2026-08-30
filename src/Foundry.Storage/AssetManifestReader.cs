// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json;
using System.Text.Json.Serialization;
using Foundry.Contracts;

namespace Foundry.Storage;

/// <summary>
/// The single hostile-input boundary for loose symbol manifests. The complete
/// file is bounded before allocation, JSON shape is inspected before binding,
/// and property names are exact so duplicate or case-confusable fields cannot
/// silently change which provenance record the engine sees.
/// </summary>
internal static class AssetManifestReader
{
    public const int MaxManifestBytes = 2 * 1024 * 1024;
    public const int MaxRecordCount = 512;
    private const int MaxJsonDepth = 16;

    private static readonly JsonSerializerOptions StrictJson = new(JsonSerializerDefaults.Web)
    {
        AllowTrailingCommas = false,
        MaxDepth = MaxJsonDepth,
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() },
    };

    public static (List<AssetProvenance> Records, byte[] Bytes) Read(string path, string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        byte[] bytes;
        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.SequentialScan);

            if (stream.Length is <= 0 or > MaxManifestBytes)
            {
                throw new InvalidDataException(
                    $"The {displayName} must contain between 1 and {MaxManifestBytes} bytes.");
            }

            bytes = GC.AllocateUninitializedArray<byte>(checked((int)stream.Length));
            stream.ReadExactly(bytes);
            if (stream.ReadByte() != -1)
            {
                throw new InvalidDataException(
                    $"The {displayName} changed while it was being read or exceeds {MaxManifestBytes} bytes.");
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException($"The {displayName} could not be read safely.", exception);
        }

        try
        {
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = MaxJsonDepth,
            });

            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException($"The {displayName} must be a JSON array.");
            }

            var count = document.RootElement.GetArrayLength();
            if (count > MaxRecordCount)
            {
                throw new InvalidDataException(
                    $"The {displayName} exceeds the {MaxRecordCount}-record limit.");
            }

            RejectDuplicateOrConfusableProperties(document.RootElement, displayName);

            var records = JsonSerializer.Deserialize<List<AssetProvenance>>(bytes, StrictJson)
                ?? throw new InvalidDataException($"The {displayName} is empty.");
            if (records.Count != count)
            {
                throw new InvalidDataException($"The {displayName} could not be bound exactly.");
            }

            return (records, bytes);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"The {displayName} is not strict, valid provenance JSON.",
                exception);
        }
    }

    private static void RejectDuplicateOrConfusableProperties(JsonElement element, string displayName)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in element.EnumerateObject())
                {
                    if (!names.Add(property.Name))
                    {
                        throw new InvalidDataException(
                            $"The {displayName} contains a duplicate or case-confusable property name.");
                    }

                    RejectDuplicateOrConfusableProperties(property.Value, displayName);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    RejectDuplicateOrConfusableProperties(item, displayName);
                }

                break;
        }
    }
}
