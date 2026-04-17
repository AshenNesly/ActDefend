using ActDefend.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ActDefend.Entropy;

/// <summary>DI registration for Entropy subsystem.</summary>
public static class EntropyServiceExtensions
{
    public static IServiceCollection AddEntropy(this IServiceCollection services)
    {
        services.AddSingleton<IEntropyEngine, EntropySamplingEngine>();
        return services;
    }
}
