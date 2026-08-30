// SPDX-License-Identifier: GPL-3.0-or-later
namespace Foundry.Domain;

/// <summary>
/// Structural validation for language tags carried by semantic documents.
/// This deliberately does not ask the operating system whether a culture is
/// installed: BCP-47 tags describe content, and valid tags must remain portable
/// across offline machines. It enforces the bounded ASCII subtag grammar,
/// extension shape, and private-use shape that the renderer can safely emit.
/// </summary>
public static class LanguageTag
{
    public const int MaximumLength = 255;

    public static bool IsStructurallyValid(string? value)
    {
        if (string.IsNullOrEmpty(value)
            || value.Length > MaximumLength
            || value[0] == '-'
            || value[^1] == '-')
        {
            return false;
        }

        var subtags = value.Split('-');
        if (subtags.Any(subtag => subtag.Length is < 1 or > 8 || !IsAsciiAlphanumeric(subtag)))
        {
            return false;
        }

        // Private-use tags start with x and require at least one payload subtag.
        if (string.Equals(subtags[0], "x", StringComparison.OrdinalIgnoreCase))
        {
            return subtags.Length > 1;
        }

        // The ordinary language subtag is two to eight ASCII letters. Obsolete
        // grandfathered i-* forms are intentionally not minted by this engine.
        var languageLength = subtags[0].Length;
        if (languageLength is < 2 or > 8 || !IsAsciiLetters(subtags[0]))
        {
            return false;
        }

        var index = 1;

        // Only a two- or three-letter primary language may carry up to three
        // three-letter extlang subtags.
        if (languageLength is 2 or 3)
        {
            var extlangCount = 0;
            while (index < subtags.Length
                && extlangCount < 3
                && subtags[index].Length == 3
                && IsAsciiLetters(subtags[index]))
            {
                index++;
                extlangCount++;
            }
        }

        // Script precedes region and may appear at most once.
        if (index < subtags.Length
            && subtags[index].Length == 4
            && IsAsciiLetters(subtags[index]))
        {
            index++;
        }

        // Region is either two letters or three digits and may appear once.
        if (index < subtags.Length
            && ((subtags[index].Length == 2 && IsAsciiLetters(subtags[index]))
                || (subtags[index].Length == 3 && IsAsciiDigits(subtags[index]))))
        {
            index++;
        }

        var variants = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (index < subtags.Length && IsVariant(subtags[index]))
        {
            if (!variants.Add(subtags[index]))
            {
                return false;
            }

            index++;
        }

        var extensionSingletons = new HashSet<char>();
        while (index < subtags.Length
            && subtags[index].Length == 1
            && !string.Equals(subtags[index], "x", StringComparison.OrdinalIgnoreCase))
        {
            var singleton = char.ToLowerInvariant(subtags[index][0]);
            if (!extensionSingletons.Add(singleton))
            {
                return false;
            }

            index++;
            var payloadStart = index;
            while (index < subtags.Length && subtags[index].Length is >= 2 and <= 8)
            {
                index++;
            }

            if (index == payloadStart)
            {
                return false;
            }
        }

        if (index < subtags.Length
            && string.Equals(subtags[index], "x", StringComparison.OrdinalIgnoreCase))
        {
            // Private use is terminal and has at least one already-bounded
            // alphanumeric payload subtag.
            return index + 1 < subtags.Length;
        }

        return index == subtags.Length;
    }

    public static void RequireValid(string? value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameterName);
        if (!IsStructurallyValid(value))
        {
            throw new ArgumentException(
                $"'{value}' is not a structurally valid language tag; write it like en, es-MX, or zh-Hant.",
                parameterName);
        }
    }

    private static bool IsVariant(string subtag)
        => (subtag.Length is >= 5 and <= 8)
            || (subtag.Length == 4 && char.IsAsciiDigit(subtag[0]));

    private static bool IsAsciiLetters(string subtag) => subtag.All(char.IsAsciiLetter);

    private static bool IsAsciiDigits(string subtag) => subtag.All(char.IsAsciiDigit);

    private static bool IsAsciiAlphanumeric(string subtag) => subtag.All(char.IsAsciiLetterOrDigit);
}
