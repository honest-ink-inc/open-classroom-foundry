// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Domain;

namespace Foundry.Modules.DeterministicPress;

// Glossary Garden (atlas #147; fourth forge menu, item 6): a bilingual unit
// glossary from the teacher's own terms, meanings, and translations —
// verbatim, never translated, corrected, or leveled by the press. Bilingual
// terms ride the engine's BilingualPair node, so the lang and direction
// semantics come from the tested renderer, not from this press. Building the
// press is zero-gate; blessing its typography belongs to the multilingual
// seat's week-3 review.

/// <summary>One glossary entry, exactly as the teacher typed it.</summary>
public sealed record GlossaryEntry(string Term, string Meaning, string? Translation);

public static class GlossaryGarden
{
    /// <summary>Parses teacher lines of "term | meaning" or "term | meaning | translation".</summary>
    public static IReadOnlyList<GlossaryEntry> Parse(IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var entries = new List<GlossaryEntry>();
        foreach (var line in lines)
        {
            var parts = line.Split('|');
            if (parts.Length > 3)
            {
                throw new ArgumentException($"'{line}' has too many | marks; write term | meaning | translation.", nameof(lines));
            }

            var term = parts[0].Trim();
            var meaning = parts.Length > 1 ? parts[1].Trim() : "";
            if (term.Length == 0 || meaning.Length == 0)
            {
                throw new ArgumentException($"'{line}' is missing its term or meaning; write term | meaning.", nameof(lines));
            }

            string? translation = null;
            if (parts.Length == 3)
            {
                translation = parts[2].Trim();
                if (translation.Length == 0)
                {
                    throw new ArgumentException($"'{line}' ends in an empty translation; drop the last | or fill it.", nameof(lines));
                }
            }

            entries.Add(new GlossaryEntry(term, meaning, translation));
        }

        return entries;
    }

    public static ArtifactDocument Sheet(
        string title,
        IReadOnlyList<GlossaryEntry> entries,
        string sourceLocale,
        string targetLocale)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("The glossary needs a title.", nameof(title));
        }

        if (entries.Count is < 2 or > 40)
        {
            throw new ArgumentException("Between two and forty entries.", nameof(entries));
        }

        LanguageTag.RequireValid(sourceLocale, nameof(sourceLocale));
        LanguageTag.RequireValid(targetLocale, nameof(targetLocale));

        var nodes = new List<DocumentNode> { new Heading(1, title) };
        foreach (var entry in entries)
        {
            nodes.Add(new Heading(2, entry.Term));
            nodes.Add(new Paragraph(entry.Meaning));
            if (entry.Translation is { } translation)
            {
                // The aligned pair carries the terminology in both languages
                // with correct lang tags — the renderer owns the semantics.
                nodes.Add(new BilingualPair(entry.Term, translation, sourceLocale, targetLocale));
            }
        }

        return new ArtifactDocument(nodes, sourceLocale);
    }
}
