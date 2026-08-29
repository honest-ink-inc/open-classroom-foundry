using Foundry.Domain;

namespace Foundry.Contracts;

// Small typed payloads for the seams. Deliberately minimal: these mature during
// Days 31–60 as real capture, rendering, and storage land.

public sealed record CaptureRequest(string SourceKind);

public sealed record NormalizationRequest(bool StripMetadata);

public sealed record OcrToken(string Text, double Confidence);

public sealed record OcrResult(IReadOnlyList<OcrToken> Tokens);

public sealed record RedactionRegion(int Page, double X, double Y, double Width, double Height, string Reason);

public sealed record RedactionSuggestions(IReadOnlyList<RedactionRegion> Regions);

public enum RenderTarget
{
    AccessibleHtml,
    PrintPdf,
    Svg,
    Png,
}

public sealed record RenderedOutput(RenderTarget Target, ReadOnlyMemory<byte> Content, string MimeType);

public sealed record PrintRequest(string PrinterName, bool Duplex, int Copies);

public sealed record ExportRequest(RenderTarget Target, string DestinationHint);

public sealed record ProjectSaveRequest(string DestinationHint);

public sealed record AssetRecord(AssetId Id, string Source, string Creator, string License, bool Redistributable);

public sealed record RecipeRunInputs(IReadOnlyList<SourceEnvelope> Sources, IReadOnlyDictionary<string, string> Parameters);
