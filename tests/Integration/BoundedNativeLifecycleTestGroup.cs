// SPDX-License-Identifier: GPL-3.0-or-later
using System.Reflection;

namespace Foundry.Tests.Integration;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class BoundedNativeLifecycleTestGroup
{
    public const string Name = "Bounded native lifecycle";
}

[Collection(BoundedNativeLifecycleTestGroup.Name)]
public sealed class BoundedNativeLifecycleTestGroupContractTests
{
    [Fact]
    public void Native_lifecycle_classes_share_one_nonparallel_collection()
    {
        var definition = typeof(BoundedNativeLifecycleTestGroup)
            .GetCustomAttribute<CollectionDefinitionAttribute>();
        var definitionData = typeof(BoundedNativeLifecycleTestGroup)
            .GetCustomAttributesData()
            .Single(attribute => attribute.AttributeType == typeof(CollectionDefinitionAttribute));

        Assert.NotNull(definition);
        Assert.True(definition.DisableParallelization);
        var definitionName = Assert.Single(definitionData.ConstructorArguments);
        Assert.Equal(BoundedNativeLifecycleTestGroup.Name, definitionName.Value);

        Assert.Collection(
            [
                typeof(FlashCapCameraSourceTests),
                typeof(ProjectUpgradeOperatorHostTests),
            ],
            AssertUsesBoundedNativeLifecycleCollection,
            AssertUsesBoundedNativeLifecycleCollection);
    }

    private static void AssertUsesBoundedNativeLifecycleCollection(Type testClass)
    {
        var collection = testClass.GetCustomAttributesData().Single(
            attribute => attribute.AttributeType == typeof(CollectionAttribute));

        var argument = Assert.Single(collection.ConstructorArguments);
        Assert.Equal(BoundedNativeLifecycleTestGroup.Name, argument.Value);
    }
}
