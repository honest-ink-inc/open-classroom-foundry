// SPDX-License-Identifier: GPL-3.0-or-later
namespace Foundry.Inference;

/// <summary>
/// Maps a recipe's OutputSchemaId to its JSON Schema document, so providers that
/// support strict schema binding can make malformed output unrepresentable at
/// generation time. A missing schema is not an error — providers fall back to
/// JSON-object mode and the engine's strict parsers still hold the line.
/// </summary>
public interface IOutputSchemaRegistry
{
    string? FindSchemaJson(string outputSchemaId);
}

public sealed class InMemorySchemaRegistry(IReadOnlyDictionary<string, string> schemasById) : IOutputSchemaRegistry
{
    public string? FindSchemaJson(string outputSchemaId)
        => schemasById.GetValueOrDefault(outputSchemaId);
}
