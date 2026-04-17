using ActDefend.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ActDefend.Detection;

/// <summary>DI registration for Detection subsystem.</summary>
public static class DetectionServiceExtensions
{
    public static IServiceCollection AddDetection(this IServiceCollection services)
    {
        services.AddSingleton<IScoringEngine, LightweightScoringEngine>();
        services.AddSingleton<DetectionOrchestrator>();
        return services;
    }
}
