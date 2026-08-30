using Foundry.Application;
using Foundry.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Foundry.Tests.Unit;

public class EngineRegistrationTests
{
    [Fact]
    public void The_engine_resolves_offline_with_no_provider_no_device_and_no_network()
    {
        using var services = new ServiceCollection()
            .AddFoundryEngine()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        Assert.NotNull(services.GetRequiredService<IDataPolicyEvaluator>());
        Assert.NotNull(services.GetRequiredService<IArtifactValidator>());
        Assert.IsType<InMemoryDiagnosticsSink>(services.GetRequiredService<IDiagnosticsSink>());
    }

    [Fact]
    public void Registration_is_idempotent_and_respects_composition_root_overrides()
    {
        var collection = new ServiceCollection();
        collection.AddSingleton<IDiagnosticsSink>(new InMemoryDiagnosticsSink());
        collection.AddFoundryEngine();
        collection.AddFoundryEngine();

        using var services = collection.BuildServiceProvider();

        Assert.Single(collection, d => d.ServiceType == typeof(IDiagnosticsSink));
        Assert.DoesNotContain(collection, d => d.ServiceType == typeof(IApprovalGate));
        Assert.NotNull(services.GetRequiredService<IDiagnosticsSink>());
    }

    [Fact]
    public void The_composition_root_does_not_expose_a_bypass_approval_capability()
    {
        var collection = new ServiceCollection().AddFoundryEngine();

        Assert.DoesNotContain(collection, descriptor => descriptor.ServiceType == typeof(IApprovalGate));
    }

    [Fact]
    public void The_root_container_does_not_create_a_cross_job_session_byte_store()
    {
        var collection = new ServiceCollection().AddFoundryEngine();

        Assert.DoesNotContain(collection, descriptor => descriptor.ServiceType == typeof(ISessionByteStore));
    }
}
