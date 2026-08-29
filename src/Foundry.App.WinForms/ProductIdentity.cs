namespace Foundry.App.WinForms;

/// <summary>
/// The single ship-name resource (ADR-006). Nothing else in code carries the
/// public name; internal identifiers remain Foundry.* / OpenClassroomFoundry.
/// </summary>
public static class ProductIdentity
{
    public const string PublicName = "Honest Ink";

    public const string Subtitle = "the classroom foundry";

    public const string WindowTitle = $"{PublicName} — {Subtitle}";

    /// <summary>Internal identifier used for storage paths, diagnostics, and policy — never the public name.</summary>
    public const string InternalId = Foundry.Domain.EngineIdentity.InternalId;
}
