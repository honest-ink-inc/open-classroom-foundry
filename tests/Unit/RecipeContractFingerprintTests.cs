// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.BuiltIn.TalkMoves;

namespace Foundry.Tests.Unit;

public sealed class RecipeContractFingerprintTests
{
    [Fact]
    public void Complete_v2_framing_has_a_cross_runtime_known_answer()
    {
        var recipe = new RecipeManifest(
            Id: "recipe.é",
            Version: "1.2.3",
            License: "GPL-3.0-or-later",
            MinimumEngineVersion: "0.7.0-alpha",
            InstructionalPurpose: "Purpose — exact.",
            ProhibitedPurposes: ["one", "two"],
            AllowedInputKinds: [],
            MaximumLane: DataLane.Restricted,
            RequiredProviderCapabilities: ["cap.a"],
            OutputSchemaId: "schema.demo.v2",
            ValidatorIds: ["validator.z", "validator.a"],
            EditorId: "editor.demo",
            RendererId: "renderer.demo",
            SupportedExports: [RenderTarget.AccessibleHtml, RenderTarget.Svg, RenderTarget.Png],
            Warnings: [string.Empty, "⚠ warning"],
            EvaluationSuiteVersion: "2.0")
        {
            LocalPreprocessingIds = ["pre.local"],
            LocalizationResourceIds = ["locale.demo.en", "locale.demo.es"],
            MigrationIds = ["migration.demo.1"],
        };

        Assert.Equal(
            "02FFBF104347272498E7AC142D78A35059E5C5DEBDC1B3E6FC3A4A9C7D7AF2CD",
            RecipeContractFingerprint.ComputeSha256(recipe));
    }

    [Fact]
    public void Equal_contract_values_have_one_cross_instance_fingerprint()
    {
        var original = TalkMovesBuilder.Recipe;
        var equivalent = original with
        {
            ProhibitedPurposes = [.. original.ProhibitedPurposes],
            AllowedInputKinds = [.. original.AllowedInputKinds],
            RequiredProviderCapabilities = [.. original.RequiredProviderCapabilities],
            LocalPreprocessingIds = [.. original.LocalPreprocessingIds],
            ValidatorIds = [.. original.ValidatorIds],
            SupportedExports = [.. original.SupportedExports],
            Warnings = [.. original.Warnings],
            LocalizationResourceIds = [.. original.LocalizationResourceIds],
            MigrationIds = [.. original.MigrationIds],
        };

        var first = RecipeContractFingerprint.ComputeSha256(original);
        var second = RecipeContractFingerprint.ComputeSha256(equivalent);

        Assert.Matches("^[0-9A-F]{64}$", first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Every_declarative_manifest_field_changes_the_fingerprint()
    {
        var recipe = TalkMovesBuilder.Recipe;
        Assert.Equal(
        [
            nameof(RecipeManifest.AllowedInputKinds),
            nameof(RecipeManifest.EditorId),
            nameof(RecipeManifest.EvaluationSuiteVersion),
            nameof(RecipeManifest.Id),
            nameof(RecipeManifest.InstructionalPurpose),
            nameof(RecipeManifest.License),
            nameof(RecipeManifest.LocalPreprocessingIds),
            nameof(RecipeManifest.LocalizationResourceIds),
            nameof(RecipeManifest.MaximumLane),
            nameof(RecipeManifest.MigrationIds),
            nameof(RecipeManifest.MinimumEngineVersion),
            nameof(RecipeManifest.OutputSchemaId),
            nameof(RecipeManifest.ProhibitedPurposes),
            nameof(RecipeManifest.RendererId),
            nameof(RecipeManifest.RequiredProviderCapabilities),
            nameof(RecipeManifest.SupportedExports),
            nameof(RecipeManifest.ValidatorIds),
            nameof(RecipeManifest.Version),
            nameof(RecipeManifest.Warnings),
        ],
            typeof(RecipeManifest)
                .GetProperties()
                .Select(property => property.Name)
                .Order(StringComparer.Ordinal));
        var baseline = RecipeContractFingerprint.ComputeSha256(recipe);
        var variants = new RecipeManifest[]
        {
            recipe with { Id = recipe.Id + ".changed" },
            recipe with { Version = recipe.Version + ".changed" },
            recipe with { License = recipe.License + ".changed" },
            recipe with { MinimumEngineVersion = recipe.MinimumEngineVersion + ".changed" },
            recipe with { InstructionalPurpose = recipe.InstructionalPurpose + " Changed." },
            recipe with { ProhibitedPurposes = [.. recipe.ProhibitedPurposes, "changed"] },
            recipe with { AllowedInputKinds = [.. recipe.AllowedInputKinds, "changed"] },
            recipe with { MaximumLane = DataLane.Amber },
            recipe with { RequiredProviderCapabilities = [.. recipe.RequiredProviderCapabilities, "changed"] },
            recipe with { OutputSchemaId = recipe.OutputSchemaId + ".changed" },
            recipe with { LocalPreprocessingIds = [.. recipe.LocalPreprocessingIds, "changed"] },
            recipe with { ValidatorIds = [.. recipe.ValidatorIds, "changed"] },
            recipe with { EditorId = recipe.EditorId + ".changed" },
            recipe with { RendererId = recipe.RendererId + ".changed" },
            recipe with { SupportedExports = [.. recipe.SupportedExports, RenderTarget.Svg] },
            recipe with { Warnings = [.. recipe.Warnings, "changed"] },
            recipe with { LocalizationResourceIds = [.. recipe.LocalizationResourceIds, "changed"] },
            recipe with { MigrationIds = [.. recipe.MigrationIds, "changed"] },
            recipe with { EvaluationSuiteVersion = recipe.EvaluationSuiteVersion + ".changed" },
        };

        Assert.All(
            variants,
            variant => Assert.NotEqual(baseline, RecipeContractFingerprint.ComputeSha256(variant)));
        Assert.Equal(variants.Length, variants.Select(RecipeContractFingerprint.ComputeSha256).Distinct().Count());
    }

    [Fact]
    public void Ordered_fields_are_length_framed_and_order_sensitive()
    {
        var recipe = TalkMovesBuilder.Recipe;
        var first = recipe with { Warnings = ["ab", "c"] };
        var regrouped = recipe with { Warnings = ["a", "bc"] };
        var reversed = recipe with { Warnings = ["c", "ab"] };

        var hashes = new[] { first, regrouped, reversed }
            .Select(RecipeContractFingerprint.ComputeSha256)
            .ToArray();

        Assert.Equal(hashes.Length, hashes.Distinct().Count());
    }

    [Fact]
    public void Null_runtime_values_fail_before_a_digest_can_be_claimed()
    {
        var recipe = TalkMovesBuilder.Recipe;

        Assert.Throws<ArgumentNullException>(() =>
            RecipeContractFingerprint.ComputeSha256(recipe with { Warnings = null! }));
        Assert.Throws<ArgumentNullException>(() =>
            RecipeContractFingerprint.ComputeSha256(recipe with { RendererId = null! }));
        Assert.Throws<ArgumentNullException>(() =>
            RecipeContractFingerprint.ComputeSha256(recipe with { LocalPreprocessingIds = null! }));
        Assert.Throws<ArgumentNullException>(() =>
            RecipeContractFingerprint.ComputeSha256(recipe with { LocalizationResourceIds = null! }));
        Assert.Throws<ArgumentNullException>(() =>
            RecipeContractFingerprint.ComputeSha256(recipe with { MigrationIds = null! }));
    }
}
