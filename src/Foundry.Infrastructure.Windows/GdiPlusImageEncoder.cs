// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Frozen;
using System.Drawing.Imaging;

namespace Foundry.Infrastructure.Windows;

/// <summary>
/// Resolves GDI+ encoders once, and publishes the completed table atomically.
/// </summary>
/// <remarks>
/// The .NET 10 format-based <see cref="Image.Save(Stream, ImageFormat)"/> path
/// lazily publishes its encoder table before the table has been populated. Two
/// simultaneous first saves can therefore observe an empty encoder GUID. Passing
/// an explicit <see cref="ImageCodecInfo"/> avoids that racy internal cache.
/// </remarks>
internal static class GdiPlusImageEncoder
{
    private static readonly Lazy<FrozenDictionary<Guid, ImageCodecInfo>> Encoders =
        new(CreateEncoderTable, LazyThreadSafetyMode.ExecutionAndPublication);

    internal static void Save(Image image, Stream output, ImageFormat format)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(format);

        if (!Encoders.Value.TryGetValue(format.Guid, out var encoder))
        {
            throw new InvalidOperationException(
                $"No installed GDI+ encoder supports {format} ({format.Guid:D}).");
        }

        image.Save(output, encoder, encoderParams: null);
    }

    private static FrozenDictionary<Guid, ImageCodecInfo> CreateEncoderTable()
    {
        var encoders = ImageCodecInfo.GetImageEncoders();
        if (encoders.Length == 0)
        {
            throw new InvalidOperationException("GDI+ reported no installed image encoders.");
        }

        return encoders.ToFrozenDictionary(encoder => encoder.FormatID);
    }
}
