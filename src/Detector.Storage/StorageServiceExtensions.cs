using ActDefend.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ActDefend.Storage;

/// <summary>DI registration for Storage subsystem.</summary>
public static class StorageServiceExtensions
{
    public static IServiceCollection AddStorage(this IServiceCollection services)
    {
        services.AddSingleton<IAlertRepository, AlertRepository>();
        services.AddSingleton<ITrustedProcessRepository, TrustedProcessRepository>();
        services.AddSingleton<IAlertPublisher, AlertPublisher>();
        return services;
    }
}
