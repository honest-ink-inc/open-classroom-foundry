// SPDX-License-Identifier: GPL-3.0-or-later
namespace Foundry.Contracts;

/// <summary>
/// Engine-owned identity used when an opened package is deliberately re-saved
/// as a portable semantic edit. Keeping it outside the WinForms assembly lets
/// compatibility tooling prove that the executing candidate still carries the
/// same exact recipe identity without depending on UI code.
/// </summary>
public static class PortableProjectIdentity
{
    public const string ModuleId = "portable-semantic-document";

    public const string RecipeId = "portable-semantic-editor";

    public const string RecipeVersion = "1.0.0";
}
