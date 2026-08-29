// SPDX-License-Identifier: GPL-3.0-or-later
namespace Foundry.App.WinForms;

/// <summary>
/// The single ship-name resource (ADR-006). Nothing else in code carries the
/// public name; internal identifiers remain Foundry.* / OpenClassroomFoundry.
/// </summary>
public static class ProductIdentity
{
    public const string PublicName = "Honest Ink";

    /// <summary>Neutral source only: window titles compose and localize it via UiStrings.</summary>
    public const string Subtitle = "the classroom foundry";

    /// <summary>Internal identifier used for storage paths, diagnostics, and policy — never the public name.</summary>
    public const string InternalId = Domain.EngineIdentity.InternalId;
}
