// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Domain;

namespace Foundry.Modules.DeterministicPress;

// The Fluency Rehearsal Builder (atlas #77; fourth forge menu, item 4). The
// teacher's passage is preserved EXACTLY — never altered, leveled, or
// re-phrased — printed large, with the teacher's own | phrase marks rendered
// as visible breath-break slashes, repeated-reading tally boxes, and a
// reflection line. The text reconstructs from the printed segments verbatim,
// asserted the same way the Parsons key is.

public static class FluencyRehearsal
{
    /// <summary>What a teacher's | phrase mark becomes in ink: the standard breath-break slash.</summary>
    public const string BreakMark = " / ";

    public static ArtifactDocument Sheet(
        string title,
        IReadOnlyList<string> passageLines,
        int readings,
        string reflectionPrompt,
        PageSize size = PageSize.Letter,
        double marginMm = BlankformsPress.DefaultMarginMm)
    {
        ArgumentNullException.ThrowIfNull(passageLines);

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("The passage needs a title.", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(reflectionPrompt))
        {
            throw new ArgumentException("The reflection line needs a prompt.", nameof(reflectionPrompt));
        }

        if (passageLines.Count is < 1 or > 14)
        {
            throw new ArgumentException("Between one and fourteen passage lines.", nameof(passageLines));
        }

        if (readings is < 1 or > 6)
        {
            throw new ArgumentException("Between one and six readings to tally.", nameof(readings));
        }

        var lineSegments = new List<IReadOnlyList<string>>();
        foreach (var line in passageLines)
        {
            var segments = line.Split('|').Select(s => s.Trim()).ToList();
            if (segments.Any(string.IsNullOrEmpty))
            {
                throw new ArgumentException($"'{line}' has an empty phrase beside a mark; every | needs words on both sides.", nameof(passageLines));
            }

            lineSegments.Add(segments);
        }

        var (width, height) = BlankformsPress.Dimensions(size);
        const double passageFontMm = 7;
        const double lineSpacingMm = 13;

        var primitives = new List<VectorPrimitive>
        {
            new TextLabel(width / 2, marginMm + 5, title, 5.5),
        };

        var passageTop = marginMm + 18;
        for (var i = 0; i < lineSegments.Count; i++)
        {
            primitives.Add(new TextLabel(
                marginMm, passageTop + i * lineSpacingMm, string.Join(BreakMark, lineSegments[i]), passageFontMm, TextAnchor.Start));
        }

        // Repeated-reading tallies: numbered boxes, one per rehearsal.
        const double boxMm = 10;
        var tallyTop = passageTop + lineSegments.Count * lineSpacingMm + 6;
        for (var r = 0; r < readings; r++)
        {
            var x = marginMm + r * (boxMm + 4);
            primitives.Add(new TextLabel(x + boxMm / 2, tallyTop - 2, (r + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), 4));
            primitives.Add(new RectShape(x, tallyTop, boxMm, boxMm, 0.5));
        }

        var reflectionTop = tallyTop + boxMm + 10;
        primitives.Add(new TextLabel(marginMm, reflectionTop, reflectionPrompt, 4.5, TextAnchor.Start));
        primitives.Add(new LineSeg(marginMm, reflectionTop + 9, width - marginMm, reflectionTop + 9, 0.3));
        primitives.Add(new LineSeg(marginMm, reflectionTop + 18, width - marginMm, reflectionTop + 18, 0.3));

        if (reflectionTop + 18 > height - marginMm)
        {
            throw new ArgumentException("The passage must fit one page; fewer lines or a taller page.", nameof(passageLines));
        }

        return new ArtifactDocument([new VectorGraphic(width, height, primitives,
            $"A repeated-reading rehearsal sheet titled {title}: {passageLines.Count} large-print passage lines with the teacher's phrase marks as breath-break slashes, {readings} reading tally boxes, and a reflection line")]);
    }
}
