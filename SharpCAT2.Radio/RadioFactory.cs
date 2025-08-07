using System.Reflection;
using SharpCAT2.Radio.Models;
using SharpCAT2.Radio.Models.Yaesu;
using SharpCAT2.Radio.Models.Kenwood;
using SharpCAT2.Radio.Models.Elecraft;

namespace SharpCAT2.Radio;

/// <summary>
/// Factory for creating radio instances based on model name
/// </summary>
public static class RadioFactory
{
    private static readonly Dictionary<string, Type> _radioTypes = new();

    static RadioFactory()
    {
        // Register built-in radio types
        RegisterRadio<KenwoodTS2000>();
        RegisterRadio<ElecraftK3>();
        RegisterRadio<YaesuFT991A>();
        
        // Discover additional radio types from assemblies
        DiscoverRadioTypes();
    }

    /// <summary>
    /// Registers a radio type
    /// </summary>
    /// <typeparam name="T">Radio type</typeparam>
    public static void RegisterRadio<T>() where T : IRadio, new()
    {
        var instance = new T();
        var key = $"{instance.Manufacturer}_{instance.ModelName}".ToUpper();
        _radioTypes[key] = typeof(T);
        instance.Dispose();
    }

    /// <summary>
    /// Creates a radio instance by manufacturer and model
    /// </summary>
    /// <param name="manufacturer">Radio manufacturer</param>
    /// <param name="model">Radio model</param>
    /// <returns>Radio instance or null if not found</returns>
    public static IRadio? CreateRadio(string manufacturer, string model)
    {
        var key = $"{manufacturer}_{model}".ToUpper();
        
        if (_radioTypes.TryGetValue(key, out Type? radioType))
        {
            try
            {
                return (IRadio?)Activator.CreateInstance(radioType);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating radio instance for {manufacturer} {model}: {ex.Message}");
            }
        }

        return null;
    }

    /// <summary>
    /// Creates a radio instance by combined name (e.g., "Kenwood TS-2000")
    /// </summary>
    /// <param name="radioName">Combined radio name</param>
    /// <returns>Radio instance or null if not found</returns>
    public static IRadio? CreateRadio(string radioName)
    {
        if (string.IsNullOrWhiteSpace(radioName))
            return null;

        var parts = radioName.Trim().Split(' ', 2);
        if (parts.Length >= 2)
        {
            return CreateRadio(parts[0], parts[1]);
        }

        // Try to find by model name only
        foreach (var kvp in _radioTypes)
        {
            try
            {
                var instance = (IRadio?)Activator.CreateInstance(kvp.Value);
                if (instance?.ModelName.Equals(radioName, StringComparison.OrdinalIgnoreCase) == true)
                {
                    return instance;
                }
                instance?.Dispose();
            }
            catch
            {
                // Continue searching
            }
        }

        return null;
    }

    /// <summary>
    /// Gets all available radio types
    /// </summary>
    /// <returns>Dictionary of radio names to types</returns>
    public static Dictionary<string, string> GetAvailableRadios()
    {
        var radios = new Dictionary<string, string>();

        foreach (var kvp in _radioTypes)
        {
            try
            {
                var instance = (IRadio?)Activator.CreateInstance(kvp.Value);
                if (instance != null)
                {
                    radios[$"{instance.Manufacturer} {instance.ModelName}"] = kvp.Key;
                    instance.Dispose();
                }
            }
            catch
            {
                // Skip problematic types
            }
        }

        return radios;
    }

    /// <summary>
    /// Discovers radio types from loaded assemblies using reflection
    /// </summary>
    private static void DiscoverRadioTypes()
    {
        try
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            
            foreach (var assembly in assemblies)
            {
                try
                {
                    var radioTypes = assembly.GetTypes()
                        .Where(t => typeof(IRadio).IsAssignableFrom(t) 
                                   && !t.IsInterface 
                                   && !t.IsAbstract 
                                   && t.GetConstructor(Type.EmptyTypes) != null);

                    foreach (var type in radioTypes)
                    {
                        try
                        {
                            var instance = (IRadio?)Activator.CreateInstance(type);
                            if (instance != null)
                            {
                                var key = $"{instance.Manufacturer}_{instance.ModelName}".ToUpper();
                                if (!_radioTypes.ContainsKey(key))
                                {
                                    _radioTypes[key] = type;
                                }
                                instance.Dispose();
                            }
                        }
                        catch
                        {
                            // Skip types that can't be instantiated
                        }
                    }
                }
                catch
                {
                    // Skip assemblies that can't be reflected
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error discovering radio types: {ex.Message}");
        }
    }

    /// <summary>
    /// Auto-detects radio type by sending identification commands
    /// </summary>
    /// <param name="serialPort">Serial port connected to radio</param>
    /// <returns>Auto-detected radio instance or null</returns>
    public static async Task<IRadio?> AutoDetectRadioAsync(System.IO.Ports.SerialPort serialPort)
    {
        if (serialPort?.IsOpen != true)
            return null;

        // Try common identification commands
        var idCommands = new[]
        {
            "ID;",      // Kenwood/Elecraft
            "RM5;",     // Yaesu
            "*IDN?",    // SCPI standard
            "AI;"       // Auto information
        };

        foreach (var command in idCommands)
        {
            try
            {
                serialPort.DiscardInBuffer();
                serialPort.DiscardOutBuffer();
                serialPort.Write(command);

                await Task.Delay(500); // Wait for response

                if (serialPort.BytesToRead > 0)
                {
                    var buffer = new byte[256];
                    int bytesRead = serialPort.Read(buffer, 0, buffer.Length);
                    string response = System.Text.Encoding.ASCII.GetString(buffer, 0, bytesRead).Trim();

                    var radio = IdentifyRadioFromResponse(response);
                    if (radio != null)
                    {
                        return radio;
                    }
                }
            }
            catch
            {
                // Continue with next command
            }
        }

        return null;
    }

    /// <summary>
    /// Identifies radio type from ID response
    /// </summary>
    /// <param name="response">Response from ID command</param>
    /// <returns>Identified radio instance or null</returns>
    private static IRadio? IdentifyRadioFromResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return null;

        response = response.ToUpper();

        // Kenwood patterns
        if (response.Contains("ID020") || response.Contains("TS-2000"))
            return CreateRadio("Kenwood", "TS-2000");

        // Elecraft patterns  
        if (response.Contains("K3") || response.Contains("ELECRAFT"))
            return CreateRadio("Elecraft", "K3");

        // Yaesu patterns
        if (response.Contains("FT-991") || response.Contains("991"))
            return CreateRadio("Yaesu", "FT-991A");

        return null;
    }
}