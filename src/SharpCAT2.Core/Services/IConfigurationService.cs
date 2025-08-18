using SharpCAT2.Core.Configuration;

namespace SharpCAT2.Core.Services;

/// <summary>
/// Service interface for managing application configuration
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// Loads configuration from the specified file path
    /// </summary>
    /// <param name="filePath">Path to the configuration file</param>
    /// <returns>Task representing the async load operation</returns>
    Task<ServerConfig> LoadConfigurationAsync(string filePath);

    /// <summary>
    /// Saves configuration to the specified file path
    /// </summary>
    /// <param name="config">Configuration to save</param>
    /// <param name="filePath">Path to save the configuration file</param>
    /// <returns>Task representing the async save operation</returns>
    Task SaveConfigurationAsync(ServerConfig config, string filePath);

    /// <summary>
    /// Applies configuration settings to command line options
    /// </summary>
    /// <param name="config">Configuration to apply</param>
    /// <param name="options">Command line options to update</param>
    void ApplyConfigurationToOptions(ServerConfig config, CommandLineOptions options);

    /// <summary>
    /// Updates configuration with command line options
    /// </summary>
    /// <param name="config">Configuration to update</param>
    /// <param name="options">Command line options to apply</param>
    void UpdateConfigurationFromOptions(ServerConfig config, CommandLineOptions options);
}