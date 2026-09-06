// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.App.WinForms;

/// <summary>
/// The existing library-save call as an injectable UI seam. Implementations
/// receive the exact approved artifact and its complete save context; the
/// production operation remains <see cref="AppServices.SaveToLibrary"/>.
/// </summary>
public delegate string ProjectLibrarySaveOperation(
    ApprovedArtifact artifact,
    string hintPrefix,
    string moduleId,
    string recipeId,
    string recipeVersion,
    IAssetCatalog catalog,
    ProjectValidationEnvelope? validation,
    ProjectRenderProfile? renderProfile);
