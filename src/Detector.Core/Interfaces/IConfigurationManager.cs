using System.Threading.Tasks;
using ActDefend.Core.Configuration;

namespace ActDefend.Core.Interfaces;

/// <summary>
/// Provides safe application configuration management to persist
/// updated tuning options back to the appsettings.json file.
/// </summary>
public interface IConfigurationManager
{
    /// <summary>
    /// Gets the current running configuration options.
    /// </summary>
    ActDefendOptions CurrentOptions { get; }

    /// <summary>
    /// Saves the provided options to the underlying configuration store.
    /// </summary>
    /// <param name="options">The modified options.</param>
    Task SaveAsync(ActDefendOptions options);
}
