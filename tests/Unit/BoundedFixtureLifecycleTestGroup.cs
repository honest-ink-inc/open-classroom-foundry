// SPDX-License-Identifier: GPL-3.0-or-later
using System.Reflection;

namespace Foundry.Tests.Unit;

// This changes intra-Unit scheduling only; other test assemblies can still run.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class BoundedFixtureLifecycleTestGroup
{
    public const string Name = "Bounded fixture lifecycle";
}

[Collection(BoundedFixtureLifecycleTestGroup.Name)]
public sealed class BoundedFixtureLifecycleTestGroupContractTests
{
    [Fact]
    public void Fixture_lifecycle_classes_share_one_exact_nonparallel_collection()
    {
        var definition = typeof(BoundedFixtureLifecycleTestGroup)
            .GetCustomAttribute<CollectionDefinitionAttribute>();
        var definitionData = Assert.Single(
            typeof(BoundedFixtureLifecycleTestGroup).GetCustomAttributesData(),
            attribute => attribute.AttributeType == typeof(CollectionDefinitionAttribute));

        Assert.NotNull(definition);
        Assert.True(definition.DisableParallelization);
        Assert.Equal(BoundedFixtureLifecycleTestGroup.Name,
            Assert.Single(definitionData.ConstructorArguments).Value);

        var assemblyTypes = typeof(BoundedFixtureLifecycleTestGroup).Assembly.GetTypes();
        Assert.Equal(
            [typeof(BoundedFixtureLifecycleTestGroup)],
            assemblyTypes.Where(type => HasNamedAttribute(type, typeof(CollectionDefinitionAttribute))));
        Assert.Equal(
            [
                typeof(BoundedFixtureLifecycleTestGroupContractTests),
                typeof(CiTestRunnerContractTests),
                typeof(FixtureProcessRunnerTests),
            ],
            assemblyTypes.Where(type => HasNamedAttribute(type, typeof(CollectionAttribute)))
                .OrderBy(type => type.FullName, StringComparer.Ordinal));

        foreach (var testClass in new[] { typeof(CiTestRunnerContractTests), typeof(FixtureProcessRunnerTests) })
        {
            var collection = Assert.Single(testClass.GetCustomAttributesData(),
                attribute => attribute.AttributeType == typeof(CollectionAttribute));
            Assert.Equal(BoundedFixtureLifecycleTestGroup.Name,
                Assert.Single(collection.ConstructorArguments).Value);
        }
    }

    private static bool HasNamedAttribute(Type type, Type attributeType) =>
        type.GetCustomAttributesData().Any(attribute =>
            attribute.AttributeType == attributeType
            && attribute.ConstructorArguments.Count == 1
            && string.Equals(attribute.ConstructorArguments[0].Value as string,
                BoundedFixtureLifecycleTestGroup.Name, StringComparison.Ordinal));
}
