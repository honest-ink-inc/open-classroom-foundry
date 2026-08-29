// SPDX-License-Identifier: GPL-3.0-or-later
namespace Foundry.Domain;

/// <summary>
/// The public-name-neutral internal identity (ADR-006). Storage paths, policy
/// directories, and diagnostics use this — never the ship name.
/// </summary>
public static class EngineIdentity
{
    public const string InternalId = "OpenClassroomFoundry";

    public const string EngineVersion = "0.7.0-alpha";

    /// <summary>The .ocfproj schema version this engine writes (ADR-003).</summary>
    public const string ProjectSchemaVersion = "1";
}
