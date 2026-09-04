// SPDX-License-Identifier: GPL-3.0-or-later
namespace Foundry.Inference;

/// <summary>
/// Maps a recipe's OutputSchemaId to its JSON Schema document, so providers that
/// support strict schema binding can make malformed output unrepresentable at
/// generation time and independently validate the returned object. A missing or
/// unsupported schema is a fail-closed capability refusal, never permission to
/// fall back to a weaker JSON-object mode.
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
