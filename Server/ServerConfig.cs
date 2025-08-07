using System.Text.Json;
using System.Text.Json.Serialization;

namespace SharpCAT2.Server;

/// <summary>
/// Configuration for the SharpCAT2 Server application
/// </summary>
public class ServerConfig
{
    /// <summary>
    /// Serial port name (e.g., COM1, /dev/ttyUSB0)
    /// </summary>
    [JsonPropertyName("serialPort")]
    public string? SerialPort { get; set; }

    /// <summary>
    /// Radio model name for CAT control
    /// </summary>
    [JsonPropertyName("radio")]
    public string? Radio { get; set; }

    /// <summary>
    /// Baud rate for serial communication
    /// </summary>
    [JsonPropertyName("baudRate")]
    public int BaudRate { get; set; } = 9600;

    /// <summary>
    /// TCP port for remote client connections
    /// </summary>
    [JsonPropertyName("tcpPort")]
    public int TcpPort { get; set; } = 8080;

    /// <summary>
    /// Whether to auto-detect the radio type
    /// </summary>
    [JsonPropertyName("autoDetectRadio")]
    public bool AutoDetectRadio { get; set; } = false;

    /// <summary>
    /// Loads configuration from the specified file path
    /// </summary>
    /// <param name="filePath">Path to the configuration file</param>
    /// <returns>Loaded configuration or default configuration if file doesn't exist</returns>
    public static async Task<ServerConfig> LoadAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Configuration file not found: {filePath}. Using default settings.");
                return new ServerConfig();
            }

            var jsonContent = await File.ReadAllTextAsync(filePath);
            var config = JsonSerializer.Deserialize<ServerConfig>(jsonContent);
            
            if (config == null)
            {
                Console.WriteLine($"Failed to parse configuration file: {filePath}. Using default settings.");
                return new ServerConfig();
            }

            Console.WriteLine($"Configuration loaded from: {filePath}");
            return config;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading configuration from {filePath}: {ex.Message}. Using default settings.");
            return new ServerConfig();
        }
    }

    /// <summary>
    /// Saves configuration to the specified file path
    /// </summary>
    /// <param name="filePath">Path to save the configuration file</param>
    /// <returns>True if saved successfully, false otherwise</returns>
    public async Task<bool> SaveAsync(string filePath)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            
            var jsonContent = JsonSerializer.Serialize(this, options);
            await File.WriteAllTextAsync(filePath, jsonContent);
            
            Console.WriteLine($"Configuration saved to: {filePath}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving configuration to {filePath}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Updates the configuration with command line arguments
    /// </summary>
    /// <param name="options">Parsed command line options</param>
    public void UpdateFromCommandLineOptions(CommandLineOptions options)
    {
        if (!string.IsNullOrEmpty(options.PortName))
            SerialPort = options.PortName;
        
        if (!string.IsNullOrEmpty(options.RadioModel))
            Radio = options.RadioModel;
        
        BaudRate = options.BaudRate;
        TcpPort = options.TcpPort;
        AutoDetectRadio = options.AutoDetectRadio;
    }

    /// <summary>
    /// Applies configuration settings to command line options
    /// </summary>
    /// <param name="options">Command line options to update</param>
    public void ApplyToCommandLineOptions(CommandLineOptions options)
    {
        if (!string.IsNullOrEmpty(SerialPort) && string.IsNullOrEmpty(options.PortName))
            options.PortName = SerialPort;
        
        if (!string.IsNullOrEmpty(Radio) && string.IsNullOrEmpty(options.RadioModel))
            options.RadioModel = Radio;
        
        // Only apply config values if they weren't overridden by command line
        if (options.BaudRate == 9600) // Default value check
            options.BaudRate = BaudRate;
        
        if (options.TcpPort == 8080) // Default value check
            options.TcpPort = TcpPort;
        
        if (!options.AutoDetectRadio)
            options.AutoDetectRadio = AutoDetectRadio;
    }
}