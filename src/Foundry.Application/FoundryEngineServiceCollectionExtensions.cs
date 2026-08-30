// SPDX-License-Identifier: GPL-3.0-or-later
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

        services.TryAddSingleton<IDataPolicyEvaluator, DefaultDataPolicyEvaluator>();
        services.TryAddSingleton<IArtifactValidator, DefaultArtifactValidator>();
        services.TryAddSingleton<IDiagnosticsSink, InMemoryDiagnosticsSink>();
        // A byte store is owned by exactly one capture job and is purged as
        // that job's privacy unit. Registering one in the root container would
        // let one job erase another job's bytes. Composition roots must create
        // the store, source, normalizer, and CaptureSession as one object graph.

        return services;
    }
}
