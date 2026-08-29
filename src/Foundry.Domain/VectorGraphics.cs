using System.Text.Json.Serialization;

namespace Foundry.Domain;

// The geometry node family of the Deterministic Press specification §4: presses
// compose millimeter-exact primitives into the semantic document, so vector forms
// pass the same approval gate and renderers as every other artifact. Coordinates
// and sizes are millimeters in page space; geometry is data, so dimensional
// accuracy is asserted exactly in tests rather than measured after the fact.

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(LineSeg), "line")]
[JsonDerivedType(typeof(CircleShape), "circle")]
[JsonDerivedType(typeof(RectShape), "rect")]
[JsonDerivedType(typeof(TextLabel), "label")]
public abstract record VectorPrimitive;

public sealed record LineSeg(double X1, double Y1, double X2, double Y2, double StrokeWidthMm = 0.35) : VectorPrimitive;

public sealed record CircleShape(double CenterX, double CenterY, double RadiusMm, double StrokeWidthMm = 0.35, bool Filled = false) : VectorPrimitive;

public sealed record RectShape(double X, double Y, double WidthMm, double HeightMm, double StrokeWidthMm = 0.35, bool Filled = false) : VectorPrimitive;

public enum TextAnchor
{
    Start,
    Middle,
    End,
}

/// <summary>Teacher-facing text placed by a press (card terms, number-line numerals). Escaped by renderers like all text.</summary>
public sealed record TextLabel(double X, double Y, string Text, double FontSizeMm = 5, TextAnchor Anchor = TextAnchor.Middle) : VectorPrimitive;

/// <summary>
/// One vector sheet. Description is the accessible account of what the sheet is —
/// required, because a printed form is still a document.
/// </summary>
public sealed record VectorGraphic(double WidthMm, double HeightMm, IReadOnlyList<VectorPrimitive> Primitives, string Description) : DocumentNode;
