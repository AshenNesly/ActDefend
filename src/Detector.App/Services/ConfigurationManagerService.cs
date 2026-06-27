using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ActDefend.Core.Configuration;
using ActDefend.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ActDefend.App.Services;

public sealed class ConfigurationManagerService : IConfigurationManager
{
    private readonly ILogger<ConfigurationManagerService> _logger;
    private readonly IOptions<ActDefendOptions> _options;
    
    private readonly string _settingsFilePath;

    public ConfigurationManagerService(ILogger<ConfigurationManagerService> logger, IOptions<ActDefendOptions> options)
    {
        _logger = logger;
        _options = options;
        _settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
    }

    public ActDefendOptions CurrentOptions => _options.Value;

    public async Task SaveAsync(ActDefendOptions options)
    {
        if (!File.Exists(_settingsFilePath))
        {
            _logger.LogError("Settings file not found at {Path}", _settingsFilePath);
            throw new FileNotFoundException($"Cannot save settings: file not found at {_settingsFilePath}");
        }

        try
        {
            _logger.LogInformation("Saving updated ActDefend configuration to {Path}", _settingsFilePath);
            
            // Read as JsonNode to preserve the file structure
            string json = await File.ReadAllTextAsync(_settingsFilePath);
            var rootNode = JsonNode.Parse(json, new JsonNodeOptions { PropertyNameCaseInsensitive = true }, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
            
            if (rootNode is JsonObject rootObj)
            {
                // Serialize the new options
                var optionsJson = JsonSerializer.SerializeToNode(options, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = null // Preserve exact casing
                });

                // Update the ActDefend section
                rootObj[ActDefendOptions.SectionName] = optionsJson;

                // Write back
                var writeOptions = new JsonSerializerOptions { WriteIndented = true };
                string newJson = rootObj.ToJsonString(writeOptions);
                await File.WriteAllTextAsync(_settingsFilePath, newJson);
                
                _logger.LogInformation("Configuration saved successfully.");
            }
            else
            {
                _logger.LogError("Failed to parse appsettings.json root as a JSON object.");
                throw new InvalidOperationException("Invalid appsettings.json format.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save configuration.");
            throw;
        }
    }
}
