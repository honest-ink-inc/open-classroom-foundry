// SPDX-License-Identifier: GPL-3.0-or-later
using System.Buffers;

namespace Foundry.Storage;

/// <summary>
/// Conservative file-name and digest rules shared by mutable asset stores and
/// open-pack export. Asset manifests name files, but they never get to name a
/// path: every admitted name is one portable leaf directly beneath its root.
/// </summary>
internal static class AssetFileSafety
{
    public const int MaxAssetBytes = 16 * 1024 * 1024;

    private static readonly SearchValues<char> PortableInvalidFileNameCharacters =
        SearchValues.Create(['<', '>', ':', '"', '/', '\\', '|', '?', '*', '\0']);

    private static readonly HashSet<string> WindowsDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>
    /// Pack and shelf names use this conservative comparer so a pack built on a
    /// case-sensitive host cannot become ambiguous when opened on Windows.
    /// </summary>
    public static StringComparer FileNameComparer { get; } = StringComparer.OrdinalIgnoreCase;

    public static bool TryResolveLeaf(string directory, string? fileName, out string path)
    {
        path = string.Empty;

        if (string.IsNullOrWhiteSpace(fileName)
            || fileName is "." or ".."
            || fileName.EndsWith(' ')
            || fileName.EndsWith('.')
            || fileName.IndexOfAny(PortableInvalidFileNameCharacters) >= 0
            || fileName.Any(char.IsControl)
            || Path.IsPathRooted(fileName)
            || Path.IsPathFullyQualified(fileName))
        {
            return false;
        }

        var firstPeriod = fileName.IndexOf('.');
        var deviceStem = (firstPeriod < 0 ? fileName : fileName[..firstPeriod]).TrimEnd(' ');
        if (WindowsDeviceNames.Contains(deviceStem))
        {
            return false;
        }

        try
        {
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
            var candidate = Path.GetFullPath(Path.Combine(root, fileName));
            var parent = Path.GetDirectoryName(candidate);

            if (parent is null || !FileNameComparer.Equals(parent, root))
            {
                return false;
            }

            path = candidate;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    public static bool IsSha256(string? value)
        => value is { Length: 64 } && value.All(Uri.IsHexDigit);

    public static bool MatchesSha256(ReadOnlySpan<byte> content, string? expected)
        => IsSha256(expected)
            && Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content))
                .Equals(expected, StringComparison.OrdinalIgnoreCase);

    public static byte[] ReadBoundedRegularFile(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.SequentialScan);
        if (stream.Length is <= 0 or > MaxAssetBytes)
        {
            throw new InvalidDataException(
                $"Asset content must contain between 1 and {MaxAssetBytes} bytes.");
        }

        var bytes = GC.AllocateUninitializedArray<byte>(checked((int)stream.Length));
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1)
        {
            throw new InvalidDataException("Asset content changed while it was being read.");
        }

        return bytes;
    }
}
