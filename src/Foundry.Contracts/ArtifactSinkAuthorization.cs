// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Domain;

namespace Foundry.Contracts;

[Flags]
internal enum AmberSinkPermission
{
    None = 0,
    Render = 1,
    Export = 2,
    Print = 4,
}

/// <summary>
/// An opaque, in-process capability for one exact approved Amber artifact and
/// an explicit set of output operations. This build deliberately has no
/// production issuer: district authorization remains a held external gate.
/// The type is deliberately inert in this build: exact request/payload binding
/// has not been ratified, so even a reflection-manufactured test instance cannot
/// authorize Amber output. Adding a request-bound issuer and verifier is a
/// separate governed change; nullable sink parameters do not manufacture
/// permission merely because an artifact was approved.
/// </summary>
public sealed class AmberSinkAuthorization
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Style",
        "IDE0051:Remove unused private members",
        Justification = "A future governed district issuer must be added inside this type; no issuer exists in the present build.")]
    private AmberSinkAuthorization(ApprovedArtifact artifact, AmberSinkPermission permissions)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        _ = permissions;
    }

}

/// <summary>
/// The shared fail-closed lane gate at every render, export, print, and project
/// save implementation. Green needs no capability; Restricted is never
/// admitted; Amber needs an exact opaque capability for the requested sink.
/// </summary>
public static class ArtifactSinkAuthorizationGate
{
    public static void DemandRender(
        ApprovedArtifact artifact,
        AmberSinkAuthorization? amberAuthorization)
        => Demand(artifact, amberAuthorization, AmberSinkPermission.Render, "rendered");

    public static void DemandExport(
        ApprovedArtifact artifact,
        AmberSinkAuthorization? amberAuthorization)
        => Demand(artifact, amberAuthorization, AmberSinkPermission.Export, "exported");

    public static void DemandPrint(
        ApprovedArtifact artifact,
        AmberSinkAuthorization? amberAuthorization)
        => Demand(artifact, amberAuthorization, AmberSinkPermission.Print, "printed");

    /// <summary>
    /// Converts one exact top-level export authority into only the render
    /// authority the exporter needs internally. It is not a public issuer.
    /// </summary>
    public static AmberSinkAuthorization? DelegateRenderWithinExport(
        ApprovedArtifact artifact,
        AmberSinkAuthorization? amberAuthorization)
    {
        DemandExport(artifact, amberAuthorization);
        return null;
    }

    /// <summary>
    /// Converts one exact top-level print authority into only the render
    /// authority required by the native print attempt. A renderer therefore
    /// cannot retain a general export capability.
    /// </summary>
    public static AmberSinkAuthorization? DelegateRenderWithinPrint(
        ApprovedArtifact artifact,
        AmberSinkAuthorization? amberAuthorization)
    {
        DemandPrint(artifact, amberAuthorization);
        return null;
    }

    public static void DemandGreenSave(ApprovedArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        if (artifact.Revision.Lane != DataLane.Green)
        {
            throw new InvalidOperationException(
                $"Only Green-lane products may be saved to the project library; this artifact is {artifact.Revision.Lane}.");
        }
    }

    private static void Demand(
        ApprovedArtifact artifact,
        AmberSinkAuthorization? amberAuthorization,
        AmberSinkPermission permission,
        string operation)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        _ = amberAuthorization;
        _ = permission;
        switch (artifact.Revision.Lane)
        {
            case DataLane.Green:
                return;
            case DataLane.Amber:
                throw new InvalidOperationException(
                    $"Amber-lane artifacts cannot be {operation} in this build: no exact request-bound district authorization issuer or verifier is active.");
            case DataLane.Restricted:
                throw new InvalidOperationException(
                    $"Restricted-lane artifacts cannot be {operation} in an early release.");
            default:
                throw new InvalidOperationException(
                    $"An artifact with an undefined data lane cannot be {operation}.");
        }
    }

}
