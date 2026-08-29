namespace Foundry.Domain;

/// <summary>
/// The public-name-neutral internal identity (ADR-006). Storage paths, policy
/// directories, and diagnostics use this — never the ship name.
/// </summary>
public static class EngineIdentity
{
    public const string InternalId = "OpenClassroomFoundry";
}
