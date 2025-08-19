using Newtonsoft.Json;

namespace SharpCAT2.ClientLibrary;

/// <summary>
/// Configuration for the SharpCAT2 Client application
/// </summary>
public class ClientConfig
{
    /// <summary>
    /// Server hostname or IP address
    /// </summary>
    [JsonProperty("serverHost")]
    public string ServerHost { get; set; } = "localhost";

    /// <summary>
    /// Server TCP port
    /// </summary>
    [JsonProperty("serverPort")]
    public int ServerPort { get; set; } = 8080;

    /// <summary>
    /// Loads configuration from the specified file path
    /// </summary>
    /// <param name="filePath">Path to the configuration file</param>
    /// <returns>Loaded configuration or default configuration if file doesn't exist</returns>
    public static async Task<ClientConfig> LoadAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Configuration file not found: {filePath}. Using default settings.");
                return new ClientConfig();
            }

            var jsonContent = await File.ReadAllTextAsync(filePath);
            var settings = new JsonSerializerSettings
            {
                // Enable comment support by ignoring comments
                FloatParseHandling = FloatParseHandling.Double,
                DateParseHandling = DateParseHandling.DateTime
            };
            var config = JsonConvert.DeserializeObject<ClientConfig>(jsonContent, settings);
            
            if (config == null)
            {
                Console.WriteLine($"Failed to parse configuration file: {filePath}. Using default settings.");
                return new ClientConfig();
            }

            Console.WriteLine($"Configuration loaded from: {filePath}");
            return config;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading configuration from {filePath}: {ex.Message}. Using default settings.");
            return new ClientConfig();
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
    /// <param name="args">Command line arguments</param>
    public void UpdateFromArgs(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "-h":
                case "--host":
                    if (i + 1 < args.Length)
                        ServerHost = args[++i];
                    break;
                case "-p":
                case "--port":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out int port))
                        ServerPort = port;
                    break;
            }
        }
    }
}