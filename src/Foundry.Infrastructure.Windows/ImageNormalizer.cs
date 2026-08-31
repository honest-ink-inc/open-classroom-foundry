// SPDX-License-Identifier: GPL-3.0-or-later
using System.Drawing.Imaging;
using System.Security.Cryptography;
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
    public const int MaxEncodedImageBytes = 16 * 1024 * 1024;
    public const int MaxImageDimension = 16_384;
    public const long MaxDecodedPixels = 25_000_000;

    public Task<SourceEnvelope> NormalizeAsync(SourceEnvelope source, NormalizationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        // Decode, redraw, and encode are CPU-bound GDI+ work. Keep them off the
        // WinForms thread, and carry the caller's cancellation through the
        // whole operation rather than returning an already-completed task
        // after blocking the caller.
        return Task.Run(() => NormalizeCore(source, request, cancellationToken), cancellationToken);
    }

    private SourceEnvelope NormalizeCore(
        SourceEnvelope source,
        NormalizationRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!store.TryGet(source.Bytes, out var content))
        {
            throw new InvalidOperationException("The session no longer holds the source bytes; re-capture is required.");
        }

        if (content.IsEmpty || content.Length > MaxEncodedImageBytes)
        {
            throw new InvalidDataException("The captured image exceeds the bounded encoded-image contract.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var inputBytes = content.ToArray();
        try
        {
            return NormalizeOwnedInput(source, request, inputBytes, cancellationToken);
        }
        catch (OutOfMemoryException failure)
        {
            throw new InvalidDataException("The captured image could not be decoded within the bounded image contract.", failure);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(inputBytes);
        }
    }

    private SourceEnvelope NormalizeOwnedInput(
        SourceEnvelope source,
        NormalizationRequest request,
        byte[] inputBytes,
        CancellationToken cancellationToken)
    {
        using var input = new MemoryStream(inputBytes, writable: false);
        using var decodedImage = Image.FromStream(input, useEmbeddedColorManagement: false, validateImageData: false);
        ValidateDimensions(decodedImage.Width, decodedImage.Height);
        using var decoded = new Bitmap(decodedImage);

        cancellationToken.ThrowIfCancellationRequested();

        if (request.Rotation != RotationDegrees.None)
        {
            decoded.RotateFlip(ToRotateFlip(request.Rotation));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Crop coordinates are expressed in the post-rotation space the
        // teacher sees. Validate the whole rectangle before drawing or
        // encoding: silently intersecting an invalid request would normalize
        // a different region than the teacher selected.
        var cropBounds = ValidateCrop(decoded.Width, decoded.Height, request.Crop);

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
                cancellationToken.ThrowIfCancellationRequested();
                var burn = Rectangle.Intersect(
                    new Rectangle(0, 0, working.Width, working.Height),
                    Rectangle.Round(new RectangleF((float)region.X, (float)region.Y, (float)region.Width, (float)region.Height)));

                if (!burn.IsEmpty)
                {
                    graphics.FillRectangle(Brushes.Black, burn);
                }
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        using var final = Crop(working, cropBounds);

        cancellationToken.ThrowIfCancellationRequested();

        using var output = new MemoryStream();
        try
        {
            GdiPlusImageEncoder.Save(final, output, ImageFormat.Png);
            cancellationToken.ThrowIfCancellationRequested();

            var outputBytes = output.ToArray();
            try
            {
                SessionByteReference? reference = null;
                try
                {
                    reference = store.Put(outputBytes);
                    cancellationToken.ThrowIfCancellationRequested();

                    return source with
                    {
                        MimeType = "image/png",
                        MetadataStripped = true,
                        Bytes = reference.Value,
                    };
                }
                catch
                {
                    if (reference is { } tentative)
                    {
                        store.Release(tentative);
                    }

                    throw;
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(outputBytes);
            }
        }
        finally
        {
            if (output.TryGetBuffer(out var buffer))
            {
                CryptographicOperations.ZeroMemory(buffer.AsSpan());
            }
        }
    }

    private static void ValidateDimensions(int width, int height)
    {
        if (width <= 0
            || height <= 0
            || width > MaxImageDimension
            || height > MaxImageDimension
            || checked((long)width * height) > MaxDecodedPixels)
        {
            throw new InvalidDataException("The captured image exceeds the bounded decoded-image contract.");
        }
    }

    private static Rectangle? ValidateCrop(int imageWidth, int imageHeight, CropRectangle? crop)
    {
        if (crop is null)
        {
            return null;
        }

        var right = (long)crop.X + crop.Width;
        var bottom = (long)crop.Y + crop.Height;

        if (crop.X < 0
            || crop.Y < 0
            || crop.Width <= 0
            || crop.Height <= 0
            || right > imageWidth
            || bottom > imageHeight)
        {
            throw new ArgumentException("The crop rectangle must lie wholly within the rotated image.", nameof(crop));
        }

        return new Rectangle(crop.X, crop.Y, crop.Width, crop.Height);
    }

    private static Bitmap Crop(Bitmap working, Rectangle? cropBounds)
    {
        if (cropBounds is not { } bounds)
        {
            return (Bitmap)working.Clone();
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
