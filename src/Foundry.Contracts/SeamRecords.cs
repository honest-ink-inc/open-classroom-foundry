using Foundry.Domain;

namespace Foundry.Contracts;

// Small typed payloads for the seams. Deliberately minimal: these mature during
// Days 31–60 as real capture, rendering, and storage land.

/// <summary>
/// For camera-style sources the content arrives from the device; for import-style
/// sources the shell reads the bytes and passes them here. Original filenames and
/// paths are discarded before this point (plan §6.5) — a path is unrepresentable.
/// </summary>
public sealed record CaptureRequest(string SourceKind, string MimeType = "", ReadOnlyMemory<byte> Content = default);

public enum RotationDegrees
{
    None = 0,
    Rotate90 = 90,
    Rotate180 = 180,
    Rotate270 = 270,
}

public sealed record CropRectangle(int X, int Y, int Width, int Height);

/// <summary>
/// Normalization pipeline order: rotate, burn redactions, crop — redaction and crop
/// coordinates are pixels in the rotated image, matching what the teacher sees.
/// Metadata stripping is not optional: normalization always re-encodes onto a fresh
/// canvas, so source metadata (EXIF, GPS, embedded properties) is dropped by
/// construction, and burned regions destroy pixels — never merely cover them.
/// </summary>
public sealed record NormalizationRequest(
    RotationDegrees Rotation = RotationDegrees.None,
    CropRectangle? Crop = null,
    IReadOnlyList<RedactionRegion>? RedactionBurns = null);

public sealed record OcrToken(string Text, double Confidence);

public sealed record OcrResult(IReadOnlyList<OcrToken> Tokens);

public sealed record RedactionRegion(int Page, double X, double Y, double Width, double Height, string Reason);

public sealed record RedactionSuggestions(IReadOnlyList<RedactionRegion> Regions);

public enum RenderTarget
{
    AccessibleHtml,

    /// <summary>Print-ready HTML with a paper stylesheet; the Windows print pipeline turns it into paper or PDF.</summary>
    PrintHtml,

    PrintPdf,
    Svg,
    Png,
}

/// <summary>
/// Teacher-only content (notices, evidence pointers, the approval footer) appears
/// only for <see cref="Teacher"/>; a learner rendering never contains it.
/// </summary>
public enum RenderAudience
{
    Learner,
    Teacher,
}

/// <summary>
/// TextScalePercent is Access Remix's large-print dial (100 = ordinary). Council
/// finding RC-8: TargetLanguageFirst lets a classroom whose room language is the
/// target read its own language first in every bilingual pair and step row.
/// </summary>
public sealed record RenderRequest(
    RenderTarget Target,
    RenderAudience Audience = RenderAudience.Learner,
    double TextScalePercent = 100,
    bool TargetLanguageFirst = false);

public sealed record RenderedOutput(RenderTarget Target, ReadOnlyMemory<byte> Content, string MimeType);

public sealed record PrintRequest(string PrinterName, bool Duplex, int Copies);

public sealed record ExportRequest(RenderTarget Target, string DestinationHint);

/// <summary>
/// DestinationHint is a file-name stem inside the store's teacher-selected root —
/// never a path; the store sanitizes it. SavedAtUtc is supplied by the caller so
/// saving stays deterministic and clock-free in the engine.
/// </summary>
public sealed record ProjectSaveRequest(
    string DestinationHint,
    string ModuleId = "",
    string RecipeId = "",
    string RecipeVersion = "",
    DateTimeOffset SavedAtUtc = default);

public sealed record RecipeRunInputs(IReadOnlyList<SourceEnvelope> Sources, IReadOnlyDictionary<string, string> Parameters);
