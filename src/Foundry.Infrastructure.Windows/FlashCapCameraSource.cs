// SPDX-License-Identifier: GPL-3.0-or-later
using FlashCap;
using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Infrastructure.Windows;

/// <summary>
/// The live camera behind the same seam the simulator fills, using FlashCap as
/// the kiosk does. One frame per capture, bytes straight into the session
/// store, envelope born Amber like every unknown. Physical-camera behavior
/// (low light, loss and reconnect, rotation) is hardware-bench work by plan
/// §12; automated tests stop at enumeration, because a test that silently
/// photographs the developer's room is not a test, it is a trespass.
/// </summary>
public sealed class FlashCapCameraSource(ISessionByteStore store) : ICaptureSource
{
    public const string Kind = "camera";

    public static IReadOnlyList<string> EnumerateCameraNames()
        => [.. new CaptureDevices().EnumerateDescriptors().Select(d => d.Name)];

    public async Task<SourceEnvelope> CaptureAsync(CaptureRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var descriptor = new CaptureDevices().EnumerateDescriptors().FirstOrDefault()
            ?? throw new InvalidOperationException("No camera is available on this machine; import an image instead.");

        var characteristics = descriptor.Characteristics.FirstOrDefault()
            ?? throw new InvalidOperationException($"Camera '{descriptor.Name}' reports no capture characteristics.");

        var frame = await descriptor.TakeOneShotAsync(characteristics, cancellationToken).ConfigureAwait(false);
        if (frame.Length == 0)
        {
            throw new InvalidOperationException("The camera returned an empty frame.");
        }

        var reference = store.Put(frame);
        return new SourceEnvelope(
            SourceKind: Kind,
            MimeType: "image/jpeg",
            PageCount: 1,
            Lane: LanePolicy.DefaultForUnknown,
            MetadataStripped: false,
            TeacherStatedRights: string.Empty,
            Bytes: reference);
    }
}
