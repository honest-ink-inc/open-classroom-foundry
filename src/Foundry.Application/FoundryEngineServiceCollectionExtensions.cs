using Foundry.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Foundry.Application;

public static class FoundryEngineServiceCollectionExtensions
{
    /// <summary>
    /// Registers the engine's core services. Infrastructure (capture, OCR, rendering,
    /// printing, storage) and an inference provider are registered by the composition
    /// root; nothing here requires a device, the network, or a model — the engine
    /// resolves and runs fully offline.
    /// </summary>
    public static IServiceCollection AddFoundryEngine(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IApprovalGate, DomainApprovalGate>();
        services.TryAddSingleton<IDataPolicyEvaluator, DefaultDataPolicyEvaluator>();
        services.TryAddSingleton<IArtifactValidator, DefaultArtifactValidator>();
        services.TryAddSingleton<IDiagnosticsSink, InMemoryDiagnosticsSink>();

        return services;
    }
}
