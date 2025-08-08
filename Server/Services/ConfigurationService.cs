using Microsoft.Extensions.Logging;

namespace SharpCAT2.Server.Services;

/// <summary>
/// Implementation of configuration service for managing application settings
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private readonly ILogger<ConfigurationService> _logger;

    public ConfigurationService(ILogger<ConfigurationService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ServerConfig> LoadConfigurationAsync(string filePath)
    {
        try
        {
            _logger.LogDebug("Loading configuration from {FilePath}", filePath);
            var config = await ServerConfig.LoadAsync(filePath);
            _logger.LogInformation("Configuration loaded successfully from {FilePath}", filePath);
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load configuration from {FilePath}", filePath);
            _logger.LogInformation("Using default configuration");
            return new ServerConfig();
        }
    }

    /// <inheritdoc />
    public async Task SaveConfigurationAsync(ServerConfig config, string filePath)
    {
        try
        {
            _logger.LogDebug("Saving configuration to {FilePath}", filePath);
            await config.SaveAsync(filePath);
            _logger.LogInformation("Configuration saved successfully to {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save configuration to {FilePath}", filePath);
        }
    }

    /// <inheritdoc />
    public void ApplyConfigurationToOptions(ServerConfig config, CommandLineOptions options)
    {
        _logger.LogDebug("Applying configuration to command line options");
        config.ApplyToCommandLineOptions(options);
    }

    /// <inheritdoc />
    public void UpdateConfigurationFromOptions(ServerConfig config, CommandLineOptions options)
    {
        _logger.LogDebug("Updating configuration from command line options");
        config.UpdateFromCommandLineOptions(options);
    }
}