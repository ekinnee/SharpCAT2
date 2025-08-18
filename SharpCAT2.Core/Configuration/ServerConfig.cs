using Newtonsoft.Json;

namespace SharpCAT2.Core.Configuration;

/// <summary>
/// Configuration for the SharpCAT2 Server application
/// </summary>
public class ServerConfig
{
    /// <summary>
    /// Serial port name (e.g., COM1, /dev/ttyUSB0)
    /// </summary>
    [JsonProperty("serialPort")]
    public string? SerialPort { get; set; }

    /// <summary>
    /// Radio model name for CAT control
    /// </summary>
    [JsonProperty("radio")]
    public string? Radio { get; set; }

    /// <summary>
    /// Baud rate for serial communication
    /// </summary>
    [JsonProperty("baudRate")]
    public int BaudRate { get; set; } = 9600;

    /// <summary>
    /// TCP port for remote client connections
    /// </summary>
    [JsonProperty("tcpPort")]
    public int TcpPort { get; set; } = 8080;

    /// <summary>
    /// Whether to auto-detect the radio type
    /// </summary>
    [JsonProperty("autoDetectRadio")]
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
            var settings = new JsonSerializerSettings
            {
                // Enable comment support by ignoring comments  
                FloatParseHandling = FloatParseHandling.Double,
                DateParseHandling = DateParseHandling.DateTime
            };
            var config = JsonConvert.DeserializeObject<ServerConfig>(jsonContent, settings);
            
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
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented
            };
            
            var jsonContent = JsonConvert.SerializeObject(this, settings);
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
    /// Applies configuration settings to command line options.
    /// Command line arguments take precedence over configuration settings.
    /// </summary>
    /// <param name="options">Command line options to update</param>
    public void ApplyToCommandLineOptions(CommandLineOptions options)
    {
        // Apply config values only if not overridden by command line
        if (!string.IsNullOrEmpty(SerialPort) && string.IsNullOrEmpty(options.PortName))
            options.PortName = SerialPort;
        
        if (!string.IsNullOrEmpty(Radio) && string.IsNullOrEmpty(options.RadioModel))
            options.RadioModel = Radio;
        
        if (options.BaudRate == 9600) // Default value check
            options.BaudRate = BaudRate;
        
        if (options.TcpPort == 8080) // Default value check
            options.TcpPort = TcpPort;
        
        // For auto-detect: only apply config value if command line didn't specify a radio model
        // This ensures command line radio selection disables auto-detect as intended
        if (string.IsNullOrEmpty(options.RadioModel) && !options.AutoDetectRadio)
            options.AutoDetectRadio = AutoDetectRadio;
    }
}