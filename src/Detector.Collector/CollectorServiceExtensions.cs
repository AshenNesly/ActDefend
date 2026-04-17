using ActDefend.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ActDefend.Collector;

/// <summary>
/// DI registration helper for the Collector subsystem.
/// Called from Detector.App's DI composition root.
/// </summary>
public static class CollectorServiceExtensions
{
    /// <summary>
    /// Registers the ETW event collector and its dependencies.
    /// </summary>
    public static IServiceCollection AddCollector(this IServiceCollection services)
    {
        services.AddSingleton<IEventCollector, EtwEventCollector>();
        return services;
    }
}
