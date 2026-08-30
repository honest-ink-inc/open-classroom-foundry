// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Text.Json;
using Foundry.Domain;
using Foundry.Modules.BuiltIn.AllAboard;

namespace Foundry.Tools.SampleGenerator;

/// <summary>One step exactly as the definitions file states it; the symbol is an asset id or nothing.</summary>
public sealed record SeededStep(string Text, string? Symbol = null, string? TargetText = null);

/// <summary>One review packet: its letter, its title, its steps, and — when bilingual — the second column's locale.</summary>
public sealed record SeededPacket(string Letter, string Title, IReadOnlyList<SeededStep> Steps, string? TargetLocale = null);

public sealed record SeededPacketSet(IReadOnlyList<SeededPacket> Packets);

/// <summary>
/// The seeded-error study's packets are an INPUT, never source.
///
/// They used to be a literal array in this tool, under a comment claiming the
/// packet-to-defect mapping "lives only in the facilitator key". That claim was
/// false: the defects are semantic and written in plain language, so anyone
/// reading the source could reconstruct the whole answer key — packet A's
/// unexecutable order and packet C's once/"dos veces" mistranslation are legible
/// on their face. A blind study cannot define its seeds in a repository anyone
/// may read, and this repository is meant to be public.
///
/// So the definitions live outside the repository, with the facilitator, beside
/// the correspondence ledger. Only an obviously-fictional example is committed —
/// enough to document the shape and keep the loader tested, not enough to train
/// anyone. Refusals here are loud and readable because a facilitator hand-edits
/// this file, often shortly before a session.
/// </summary>
public static class SeededPacketFile
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static SeededPacketSet Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"No seeded-packet definitions at '{path}'. The real definitions are the facilitator's and live outside this repository; " +
                "see docs/evidence/pilot-kit/README.md.", path);
        }

        SeededPacketSet? set;
        try
        {
            set = JsonSerializer.Deserialize<SeededPacketSet>(File.ReadAllText(path), Options);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"The definitions file '{path}' is not valid JSON: {exception.Message}", exception);
        }

        return Validate(set?.Packets is null || set.Packets.Count == 0
            ? throw new InvalidOperationException($"The definitions file '{path}' declares no packets.")
            : set);
    }

    private static SeededPacketSet Validate(SeededPacketSet set)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var packet in set.Packets)
        {
            if (string.IsNullOrWhiteSpace(packet.Letter) || packet.Letter.Length != 1 || !char.IsLetter(packet.Letter[0]))
            {
                throw new InvalidOperationException(
                    $"Packet letter '{packet.Letter}' is not a single letter; letters name the packets and carry nothing else.");
            }

            if (!seen.Add(packet.Letter))
            {
                throw new InvalidOperationException($"Two packets claim the letter '{packet.Letter}'; a duplicate letter would confound the matrix.");
            }

            if (string.IsNullOrWhiteSpace(packet.Title))
            {
                throw new InvalidOperationException($"Packet '{packet.Letter}' has no title.");
            }

            if (packet.Steps is null || packet.Steps.Count == 0)
            {
                throw new InvalidOperationException($"Packet '{packet.Letter}' has no steps.");
            }

            // The same bound the builder enforces, stated here so the facilitator
            // learns it from their own file rather than from a stack trace.
            if (packet.Steps.Count is < AllAboardBuilders.MinimumSteps or > AllAboardBuilders.MaximumSteps)
            {
                throw new InvalidOperationException(
                    $"Packet '{packet.Letter}' has {packet.Steps.Count} steps; a task strip has " +
                    $"{AllAboardBuilders.MinimumSteps} to {AllAboardBuilders.MaximumSteps} one-action steps.");
            }

            ValidateBilingualHonesty(packet);
        }

        return set;
    }

    /// <summary>
    /// A step carrying a translation inside a packet that declares no locale would
    /// render a second language with no language tag — the renderer's own rule,
    /// enforced here where the mistake is cheap to state.
    /// </summary>
    private static void ValidateBilingualHonesty(SeededPacket packet)
    {
        var translated = packet.Steps.Any(step => !string.IsNullOrWhiteSpace(step.TargetText));

        if (translated && string.IsNullOrWhiteSpace(packet.TargetLocale))
        {
            throw new InvalidOperationException(
                $"Packet '{packet.Letter}' carries translations but declares no targetLocale; write it like es or es-MX.");
        }

        if (!translated && !string.IsNullOrWhiteSpace(packet.TargetLocale))
        {
            throw new InvalidOperationException(
                $"Packet '{packet.Letter}' declares targetLocale '{packet.TargetLocale}' but carries no translation.");
        }
    }

    public static StepSpec[] ToStepSpecs(SeededPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        return [.. packet.Steps.Select(step => new StepSpec(
            step.Text,
            string.IsNullOrWhiteSpace(step.Symbol) ? null : new AssetId(step.Symbol),
            string.IsNullOrWhiteSpace(step.TargetText) ? null : step.TargetText))];
    }

    /// <summary>The output file name for a packet; lower-cased so the kit is stable across platforms.</summary>
    public static string FileNameFor(SeededPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        return $"packet-{packet.Letter.ToLower(CultureInfo.InvariantCulture)}.print.html";
    }
}
