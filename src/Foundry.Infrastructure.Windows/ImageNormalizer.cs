using System.Drawing;
using System.Drawing.Imaging;
using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Infrastructure.Windows;

/// <summary>
/// Rotate → burn redactions → crop, in the coordinate space the teacher sees.
/// Every output is drawn onto a fresh canvas and re-encoded as PNG, so source
/// metadata (EXIF, GPS, embedded properties) is dropped by construction and a
/// burned region destroys pixels — it never merely covers them.
/// </summary>
public sealed class ImageNormalizer(ISessionByteStore store) : IDocumentNormalizer
{
    public Task<SourceEnvelope> NormalizeAsync(SourceEnvelope source, NormalizationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!store.TryGet(source.Bytes, out var content))
        {
            throw new InvalidOperationException("The session no longer holds the source bytes; re-capture is required.");
        }

        using var input = new MemoryStream(content.ToArray());
        using var decoded = new Bitmap(input);

        if (request.Rotation != RotationDegrees.None)
        {
            decoded.RotateFlip(ToRotateFlip(request.Rotation));
        }

        // Fresh canvas: pixel data only, no property items, no embedded profiles.
        using var working = new Bitmap(decoded.Width, decoded.Height, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(working))
        {
            graphics.DrawImage(
                decoded,
                new Rectangle(0, 0, decoded.Width, decoded.Height),
                new Rectangle(0, 0, decoded.Width, decoded.Height),
                GraphicsUnit.Pixel);

            foreach (var region in request.RedactionBurns ?? [])
            {
                var burn = Rectangle.Intersect(
                    new Rectangle(0, 0, working.Width, working.Height),
                    Rectangle.Round(new RectangleF((float)region.X, (float)region.Y, (float)region.Width, (float)region.Height)));

                if (!burn.IsEmpty)
                {
                    graphics.FillRectangle(Brushes.Black, burn);
                }
            }
        }

        using var final = Crop(working, request.Crop);

        using var output = new MemoryStream();
        final.Save(output, ImageFormat.Png);

        var reference = store.Put(output.ToArray());

        return Task.FromResult(source with
        {
            MimeType = "image/png",
            MetadataStripped = true,
            Bytes = reference,
        });
    }

    private static Bitmap Crop(Bitmap working, CropRectangle? crop)
    {
        if (crop is null)
        {
            return (Bitmap)working.Clone();
        }

        var bounds = Rectangle.Intersect(
            new Rectangle(0, 0, working.Width, working.Height),
            new Rectangle(crop.X, crop.Y, crop.Width, crop.Height));

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            throw new ArgumentException("The crop rectangle lies outside the image.", nameof(crop));
        }

        var cropped = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
        using var graphics = Graphics.FromImage(cropped);
        graphics.DrawImage(working, new Rectangle(0, 0, bounds.Width, bounds.Height), bounds, GraphicsUnit.Pixel);
        return cropped;
    }

    private static RotateFlipType ToRotateFlip(RotationDegrees rotation) => rotation switch
    {
        RotationDegrees.Rotate90 => RotateFlipType.Rotate90FlipNone,
        RotationDegrees.Rotate180 => RotateFlipType.Rotate180FlipNone,
        RotationDegrees.Rotate270 => RotateFlipType.Rotate270FlipNone,
        _ => RotateFlipType.RotateNoneFlipNone,
    };
}
