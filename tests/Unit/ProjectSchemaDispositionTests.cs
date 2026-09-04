// SPDX-License-Identifier: GPL-3.0-or-later

using System.Reflection;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.BuiltIn;
using Foundry.Modules.DeterministicPress;

namespace Foundry.Tests.Unit;

/// <summary>
/// Protects the proposed schema-2 decision boundary. These assertions do not
/// ratify the ADR. They prevent documentation drift from turning an absent
/// product-owner decision into an inferred one and lock the current schema-1
/// implementation boundary while the proposal remains unratified.
/// </summary>
public sealed class ProjectSchemaDispositionTests
{
    [Fact]
    public void Proposed_schema_2_route_preserves_legacy_truth_and_human_authority()
    {
        var root = RepositoryRoot();
        var adr = File.ReadAllText(Path.Combine(
            root,
            "docs",
            "adr",
            "ADR-010-project-schema-2-binds-an-exact-recipe-contract.md"));
        var index = File.ReadAllText(Path.Combine(root, "docs", "adr", "README.md"));
        var traceability = File.ReadAllText(Path.Combine(
            root,
            "docs",
            "release",
            "release-requirement-test-traceability.md"));
        var hardening = File.ReadAllText(Path.Combine(
            root,
            "docs",
            "release",
            "hardening-checklist.md"));

        Assert.Contains(
            "**Status:** Proposed — decision-ready, not ratified; no schema-2 writer or migration is authorized",
            adr,
            StringComparison.Ordinal);
        Assert.Contains("Keep schema 1 immutable, readable, and hash-less", adr, StringComparison.Ordinal);
        Assert.Contains("Make `recipeHash` mandatory in project schema 2", adr, StringComparison.Ordinal);
        Assert.Contains("Do not auto-migrate schema 1", adr, StringComparison.Ordinal);
        Assert.Contains("identity-only and therefore cannot write schema 2", adr, StringComparison.Ordinal);
        Assert.Contains("allowed input kinds", adr, StringComparison.Ordinal);
        Assert.Contains("`sourceLocales`, `outputLocales`", adr, StringComparison.Ordinal);
        Assert.Contains("`provenanceSha256`", adr, StringComparison.Ordinal);
        Assert.Contains("first-admission-frozen module/recipe", adr, StringComparison.Ordinal);
        Assert.Contains("one exact `(moduleId, moduleVersion,", adr, StringComparison.Ordinal);
        Assert.Contains("independent caller strings", adr, StringComparison.Ordinal);
        Assert.Contains("silently writes `moduleVersion`", adr, StringComparison.Ordinal);
        Assert.Contains("ADR-003's schema-1 manifest-field and migration-detail", adr, StringComparison.Ordinal);
        Assert.Contains("require a new Gate B review", adr, StringComparison.Ordinal);
        Assert.Contains("This ADR chooses no engine version and moves no existing tag", adr, StringComparison.Ordinal);
        Assert.Contains(
            "## Ratification decision fields — intentionally blank; fixed deferred/non-effects recorded",
            adr,
            StringComparison.Ordinal);
        Assert.Contains(
            "**Ratified by:** Pending product owner; District IT and records/privacy retain their deployment and retention gates",
            adr,
            StringComparison.Ordinal);
        Assert.Contains("| Product-owner decision | `[not supplied]` |", adr, StringComparison.Ordinal);
        Assert.Contains("| Decision instant | `[not supplied]` |", adr, StringComparison.Ordinal);
        Assert.Contains("| Exact statement accepting or rejecting all ten clauses | `[not supplied]` |", adr, StringComparison.Ordinal);
        Assert.Contains("| ADR-007 disposition | `[not supplied separately]` |", adr, StringComparison.Ordinal);
        Assert.Contains(
            "| Exact first schema-2 engine version | `Deferred — required by a separate exact version act after implementation evidence; no version is authorized here` |",
            adr,
            StringComparison.Ordinal);
        Assert.Contains(
            "| District/records effect | `None; each real deployment and retention plan remains separately held` |",
            adr,
            StringComparison.Ordinal);
        Assert.Contains("| Release effect | `None; all release and publication stops remain open` |", adr, StringComparison.Ordinal);

        Assert.DoesNotContain("**Status:** Accepted", adr, StringComparison.Ordinal);
        Assert.DoesNotContain("**Status:** Ratified", adr, StringComparison.Ordinal);
        Assert.Contains(
            "Proposed — decision-ready; no schema-2 writer, migration, version, or release is authorized",
            index,
            StringComparison.Ordinal);
        Assert.Contains(
            "| [ADR-003](ADR-003-open-ocfproj-package.md) | Open `.ocfproj` project package as the portable source of truth | Accepted |",
            index,
            StringComparison.Ordinal);
        Assert.Equal("0.7.0-alpha", EngineIdentity.EngineVersion);
        Assert.Equal("1", EngineIdentity.ProjectSchemaVersion);
        Assert.Null(typeof(ProjectManifest).GetProperty("RecipeHash"));
        Assert.Equal("1.0.0", PortableProjectIdentity.RecipeVersion);
        AssertPortableSemanticRecipeIsNotCompiled();
        AssertSchema2WriterIsAbsent(root);

        Assert.Contains("makes the previously implicit choices", traceability, StringComparison.Ordinal);
        Assert.Contains("**not** close OP-04, design review, or the release stop", traceability, StringComparison.Ordinal);
        Assert.Contains(
            "Its four authority decision fields are intentionally blank",
            hardening,
            StringComparison.Ordinal);
        Assert.Contains("three", hardening, StringComparison.Ordinal);
    }

    private static void AssertPortableSemanticRecipeIsNotCompiled()
    {
        var compiledRecipes = new List<RecipeManifest>();
        var recipeAssemblies = new[]
        {
            typeof(ModuleStudioCatalog).Assembly,
            typeof(DeterministicPressRecipes).Assembly,
        };

        foreach (var type in recipeAssemblies.SelectMany(assembly => assembly.GetTypes()))
        {
            foreach (var property in type.GetProperties(
                         BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (CarriesManifest(property.PropertyType))
                {
                    Collect(property.PropertyType, property.GetValue(null));
                }
            }

            foreach (var field in type.GetFields(
                         BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (CarriesManifest(field.FieldType))
                {
                    Collect(field.FieldType, field.GetValue(null));
                }
            }
        }

        Assert.NotEmpty(compiledRecipes);
        Assert.DoesNotContain(
            compiledRecipes,
            recipe => string.Equals(recipe.Id, PortableProjectIdentity.RecipeId, StringComparison.Ordinal));

        var portableMembers = typeof(PortableProjectIdentity)
            .GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Where(member => member switch
            {
                PropertyInfo property => CarriesManifest(property.PropertyType),
                FieldInfo field => CarriesManifest(field.FieldType),
                _ => false,
            });
        Assert.Empty(portableMembers);

        void Collect(Type declaredType, object? value)
        {
            if (declaredType == typeof(RecipeManifest) && value is RecipeManifest manifest)
            {
                compiledRecipes.Add(manifest);
            }
            else if (typeof(IEnumerable<RecipeManifest>).IsAssignableFrom(declaredType)
                     && value is IEnumerable<RecipeManifest> manifests)
            {
                compiledRecipes.AddRange(manifests);
            }
        }

        static bool CarriesManifest(Type declaredType)
        {
            return declaredType == typeof(RecipeManifest)
                        || typeof(IEnumerable<RecipeManifest>).IsAssignableFrom(declaredType);
        }
    }

    private static void AssertSchema2WriterIsAbsent(string root)
    {
        var writer = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Foundry.Storage",
            "OcfprojProjectStore.cs"));
        Assert.Contains("SchemaVersion: EngineIdentity.ProjectSchemaVersion", writer, StringComparison.Ordinal);
        Assert.DoesNotContain("SchemaVersion: \"2\"", writer, StringComparison.Ordinal);

        var productionFiles = Directory
            .EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => segment.Equals("bin", StringComparison.OrdinalIgnoreCase)
                    || segment.Equals("obj", StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        var portableLiteralSources = productionFiles
            .Where(path => File.ReadAllText(path).Contains("portable-semantic-editor", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(root, path))
            .ToArray();
        Assert.Equal([Path.Combine("src", "Foundry.Contracts", "PortableProjectIdentity.cs")], portableLiteralSources);

        var portableIdentityConsumers = productionFiles
            .Where(path => File.ReadAllText(path).Contains("PortableProjectIdentity.RecipeId", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(root, path))
            .ToArray();
        Assert.Equal([Path.Combine("src", "Foundry.App.WinForms", "AppServices.cs")], portableIdentityConsumers);

        Assert.DoesNotContain(
            productionFiles,
            path => Path.GetFileName(path).Contains("Schema2", StringComparison.Ordinal)
                || File.ReadAllText(path).Contains("ProjectManifestV2", StringComparison.Ordinal)
                || File.ReadAllText(path).Contains("ProjectSchemaVersion = \"2\"", StringComparison.Ordinal));
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OpenClassroomFoundry.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not find the repository root by walking up to OpenClassroomFoundry.slnx.");
    }
}
