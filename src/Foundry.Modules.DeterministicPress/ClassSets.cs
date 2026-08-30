// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Domain;

namespace Foundry.Modules.DeterministicPress;

/// <summary>
/// Class Sets (fourth forge menu, item 10 — not in the atlas; born of the
/// enriched product): the seeded presses, multiplied. One document, N
/// variants of the same seeded artifact — thirty word searches of the same
/// teacher words with no two grids alike. Variant seeds derive as
/// baseSeed + variantIndex, so any lost sheet is reprintable by the number
/// printed on it: variant v of a set IS the single press at seed
/// base + v − 1, and the tests prove exactly that. A composer over any
/// seeded catalog engine — like Big Print Shop and the Studio Sampler, never
/// a press of its own — and the composed document passes Gate B as itself:
/// the gate is structural, not hereditary.
/// </summary>
public static class ClassSets
{
    /// <summary>The seed bounds the catalog's own seed parameter declares.</summary>
    private const int MaxSeed = 99999999;

    public static ArtifactDocument Compose(
        PressDefinition definition,
        IReadOnlyDictionary<string, string> values,
        int baseSeed,
        int variants)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(values);

        if (!definition.Parameters.Any(p => p.Key == "seed"))
        {
            throw new ArgumentException($"'{definition.Id}' takes no seed; class sets multiply seeded presses only.", nameof(definition));
        }

        if (variants is < 2 or > 40)
        {
            throw new ArgumentException("Between two and forty variants.", nameof(variants));
        }

        if (baseSeed < 1 || baseSeed > MaxSeed - (variants - 1))
        {
            throw new ArgumentException($"The base seed and every variant seed must stay between 1 and {MaxSeed}.", nameof(baseSeed));
        }

        var nodes = new List<DocumentNode>();
        string? documentLanguage = null;
        var capturedLanguage = false;
        for (var variant = 1; variant <= variants; variant++)
        {
            var seed = baseSeed + variant - 1;
            var perVariant = new Dictionary<string, string>(values, StringComparer.Ordinal)
            {
                ["seed"] = seed.ToString(System.Globalization.CultureInfo.InvariantCulture),
            };

            var document = definition.Build(new PressInputs(perVariant));
            if (!capturedLanguage)
            {
                documentLanguage = document.Language;
                capturedLanguage = true;
            }
            else if (!string.Equals(documentLanguage, document.Language, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"'{definition.Id}' changed document language between class-set variants.");
            }

            var stamp = $"Variant {variant} of {variants} · seed {seed}";
            foreach (var node in document.Nodes)
            {
                nodes.Add(node switch
                {
                    // Every sheet carries its number and seed in ink — the
                    // reprint claim, printed where it can be read.
                    VectorGraphic page => page with
                    {
                        Primitives = [.. page.Primitives, new TextLabel(page.WidthMm / 2, page.HeightMm - 6, stamp, 3)],
                        Description = $"{page.Description} (variant {variant} of {variants}, seed {seed})",
                    },
                    TeacherOnlyNotice notice => new TeacherOnlyNotice($"Variant {variant}: {notice.Text}"),
                    _ => throw new NotSupportedException(
                        $"Class sets multiply vector-sheet presses; '{definition.Id}' produced {node.GetType().Name}."),
                });
            }
        }

        return new ArtifactDocument(nodes, documentLanguage);
    }
}
