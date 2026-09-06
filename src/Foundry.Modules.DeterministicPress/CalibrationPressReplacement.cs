// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Domain;

namespace Foundry.Modules.DeterministicPress;

/// <summary>
/// Candidate press.calibration 0.2.0 layout: wrap the historical instruction
/// words and translate the instrument group, never scale its rulers or patches.
/// The catalog binds its separate meaning-preserving low-ink transform. This
/// producer is not a declaration of schema equivalence or physical print proof.
/// </summary>
public static class CalibrationPressReplacement
{
    private const int InstructionCharactersPerLine = 67;
    private const double InstructionLeadingMm = 5.5;
    private const double HorizontalRulerBaselineMm = 70;

    public static ArtifactDocument ProofPage(PageSize size = PageSize.Letter, double marginMm = BlankformsPress.DefaultMarginMm)
    {
        if (!double.IsFinite(marginMm) || marginMm is < 5 or > 25)
        {
            throw new ArgumentException("A finite margin between 5 and 25 millimeters keeps every instrument on the page.", nameof(marginMm));
        }

        // CalibrationPress remains the exact historical builder. Its first
        // three primitives are the frame/headings, the next five are the
        // numbered instructions, and the remaining primitives are instruments.
        // Keeping the original primitive records makes a translation-only
        // comparison possible without reimplementing ruler or hatch arithmetic.
        var historical = CalibrationPress.ProofPage(size, marginMm);
        var page = (VectorGraphic)historical.Nodes[0];
        var instructions = page.Primitives.Skip(3).Take(5).Cast<TextLabel>()
            .SelectMany(instruction => WrapInstruction(instruction.Text)).ToArray();
        var primitives = page.Primitives.Take(3).ToList();
        for (var index = 0; index < instructions.Length; index++)
        {
            primitives.Add(new TextLabel(marginMm + 10, marginMm + 19 + index * InstructionLeadingMm,
                instructions[index], 3.5, TextAnchor.Start));
        }

        var lastBaseline = marginMm + 19 + (instructions.Length - 1) * InstructionLeadingMm;
        // The tallest ruler tick extends 6 mm above its baseline. Leave a
        // further 6 mm after the last instruction baseline. Across supported
        // portrait pages/margins this translation keeps the complete original
        // instrument group, including patch labels, within the margin frame.
        var translationMm = Math.Max(0, lastBaseline + 12 - HorizontalRulerBaselineMm);
        primitives.AddRange(page.Primitives.Skip(8).Select(primitive => Translate(primitive, translationMm)));
        return new ArtifactDocument([page with { Primitives = primitives }], historical.Language);
    }

    private static IEnumerable<string> WrapInstruction(string instruction)
    {
        var line = string.Empty;
        foreach (var word in instruction.Split(' '))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > InstructionCharactersPerLine)
            {
                yield return line;
                line = word;
            }
            else
            {
                line = line.Length == 0 ? word : line + " " + word;
            }
        }

        if (line.Length > 0)
        {
            yield return line;
        }
    }

    private static VectorPrimitive Translate(VectorPrimitive primitive, double dy) => primitive switch
    {
        LineSeg line => line with { Y1 = line.Y1 + dy, Y2 = line.Y2 + dy },
        RectShape rectangle => rectangle with { Y = rectangle.Y + dy },
        CircleShape circle => circle with { CenterY = circle.CenterY + dy },
        TextLabel label => label with { Y = label.Y + dy },
        _ => throw new NotSupportedException($"The calibration instrument has no translation for '{primitive.GetType().Name}'."),
    };
}
