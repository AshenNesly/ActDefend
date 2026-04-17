using ActDefend.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ActDefend.Features;

/// <summary>
/// DI registration helper for the Features subsystem.
/// </summary>
public static class FeaturesServiceExtensions
{
    public static IServiceCollection AddFeatures(this IServiceCollection services)
    {
        services.AddSingleton<IFeatureExtractor, FeatureExtractor>();
        return services;
    }
}
