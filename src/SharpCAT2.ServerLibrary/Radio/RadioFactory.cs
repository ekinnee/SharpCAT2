using System.Reflection;
using Microsoft.Extensions.Logging;
using SharpCAT2.ServerLibrary.Radio.Models;
using SharpCAT2.ServerLibrary.Radio.Models.Yaesu;
using SharpCAT2.ServerLibrary.Radio.Models.Kenwood;
using SharpCAT2.ServerLibrary.Radio.Models.Elecraft;
using SharpCAT2.ServerLibrary.Radio.Models.Icom;
using SharpCAT2.ServerLibrary.Radio.Models.FlexRadio;
using SharpCAT2.ServerLibrary.Radio.Models.Alinco;
using SharpCAT2.ServerLibrary.Radio.Models.TenTec;
using SharpCAT2.ServerLibrary.Radio.Models.Testing;
using SharpCAT2.ServerLibrary.Serial;
using SharpCAT2.Core.Radio;
using SharpCAT2.Core.Serial;

namespace SharpCAT2.ServerLibrary.Radio;

/// <summary>
/// Factory for creating radio instances based on model name.
/// 
/// LOGGING ARCHITECTURE:
/// Radio creation errors are communicated through null return values.
/// The service layer (RadioService) handles logging of factory failures
/// using dependency-injected ILogger. This factory is logging-free.
/// </summary>
public static class RadioFactory
{
    private static readonly Dictionary<string, Type> _radioTypes = new();

    static RadioFactory()
    {
        // Register built-in radio types by brand
        
        // Kenwood radios
        RegisterRadio<KenwoodTS2000>();
        RegisterRadio<KenwoodTS890S>();
        RegisterRadio<KenwoodTS590SG>();
        RegisterRadio<KenwoodTHD74A>();
        RegisterRadio<KenwoodTMD710GA>();
        
        // Elecraft radios
        RegisterRadio<ElecraftK3>();
        RegisterRadio<ElecraftK4>();
        RegisterRadio<ElecraftKX3>();
        RegisterRadio<ElecraftK2>();
        RegisterRadio<ElecraftK1>();
        
        // Yaesu radios
        RegisterRadio<YaesuFT991A>();
        RegisterRadio<YaesuFT710>();
        RegisterRadio<YaesuFTDX101D>();
        RegisterRadio<YaesuFT891>();
        RegisterRadio<YaesuFT65>();
        
        // Icom radios
        RegisterRadio<IcomIC7300>();
        RegisterRadio<IcomIC9700>();
        
        // FlexRadio radios
        RegisterRadio<FlexRadio6400>();
        RegisterRadio<FlexRadio6600>();
        RegisterRadio<FlexRadio6700>();
        
        // Alinco radios
        RegisterRadio<AlincoDXSR8T>();
        RegisterRadio<AlincoDJMD5TGP>();
        RegisterRadio<AlincoDR638T>();
        RegisterRadio<AlincoDX70T>();
        
        // Ten-Tec radios
        RegisterRadio<TenTecOMNIVII>();
        RegisterRadio<TenTecEagle>();
        RegisterRadio<TenTecArgonautV>();
        RegisterRadio<TenTecJupiter>();
        
        // Testing/Demo radios
        RegisterRadio<DummyRadio>();
        
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
    /// Creates a resilient radio instance with retry logic and automatic recovery
    /// </summary>
    /// <param name="manufacturer">Radio manufacturer</param>
    /// <param name="model">Radio model</param>
    /// <param name="logger">Logger for the resilient wrapper</param>
    /// <returns>Resilient radio instance or null if not found</returns>
    public static IRadio? CreateResilientRadio(string manufacturer, string model, ILogger? logger = null)
    {
        var baseRadio = CreateRadio(manufacturer, model);
        return baseRadio != null ? new ResilientRadio(baseRadio, logger) : null;
    }

    /// <summary>
    /// Creates a resilient radio instance by combined name with retry logic and automatic recovery
    /// </summary>
    /// <param name="radioName">Combined radio name</param>
    /// <param name="logger">Logger for the resilient wrapper</param>
    /// <returns>Resilient radio instance or null if not found</returns>
    public static IRadio? CreateResilientRadio(string radioName, ILogger? logger = null)
    {
        var baseRadio = CreateRadio(radioName);
        return baseRadio != null ? new ResilientRadio(baseRadio, logger) : null;
    }

    /// <summary>
    /// Creates a radio instance by manufacturer and model
    /// Supports case-insensitive matching for better user experience.
    /// </summary>
    /// <param name="manufacturer">Radio manufacturer</param>
    /// <param name="model">Radio model</param>
    /// <param name="useResilientWrapper">Use resilient wrapper for error recovery (default: false for compatibility)</param>
    /// <param name="logger">Logger for resilient wrapper</param>
    /// <returns>Radio instance or null if not found</returns>
    public static IRadio? CreateRadio(string manufacturer, string model, bool useResilientWrapper = false, ILogger? logger = null)
    {
        var key = $"{manufacturer}_{model}".ToUpper();
        
        if (_radioTypes.TryGetValue(key, out Type? radioType))
        {
            try
            {
                var baseRadio = (IRadio?)Activator.CreateInstance(radioType);
                if (baseRadio != null && useResilientWrapper)
                {
                    return new ResilientRadio(baseRadio, logger);
                }
                return baseRadio;
            }
            catch (Exception)
            {
                // Radio creation errors result in null return
                // Service layer will handle and log null radio instances
            }
        }

        // If exact match fails, try case-insensitive fuzzy matching
        foreach (var kvp in _radioTypes)
        {
            try
            {
                var instance = (IRadio?)Activator.CreateInstance(kvp.Value);
                if (instance != null)
                {
                    if (instance.Manufacturer.Equals(manufacturer, StringComparison.OrdinalIgnoreCase) &&
                        instance.ModelName.Equals(model, StringComparison.OrdinalIgnoreCase))
                    {
                        if (useResilientWrapper)
                        {
                            return new ResilientRadio(instance, logger);
                        }
                        return instance;
                    }
                    instance.Dispose();
                }
            }
            catch
            {
                // Continue searching
            }
        }

        return null;
    }

    /// <summary>
    /// Creates a radio instance by combined name (e.g., "Kenwood TS-2000")
    /// Supports case-insensitive matching for better user experience.
    /// </summary>
    /// <param name="radioName">Combined radio name</param>
    /// <param name="useResilientWrapper">Use resilient wrapper for error recovery (default: false for compatibility)</param>
    /// <param name="logger">Logger for resilient wrapper</param>
    /// <returns>Radio instance or null if not found</returns>
    public static IRadio? CreateRadio(string radioName, bool useResilientWrapper = false, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(radioName))
            return null;

        var cleanRadioName = radioName.Trim();
        var parts = cleanRadioName.Split(' ', 2);
        if (parts.Length >= 2)
        {
            return CreateRadio(parts[0], parts[1], useResilientWrapper, logger);
        }

        // Try to find by model name only (case-insensitive)
        foreach (var kvp in _radioTypes)
        {
            try
            {
                var instance = (IRadio?)Activator.CreateInstance(kvp.Value);
                if (instance?.ModelName.Equals(cleanRadioName, StringComparison.OrdinalIgnoreCase) == true)
                {
                    if (useResilientWrapper)
                    {
                        return new ResilientRadio(instance, logger);
                    }
                    return instance;
                }
                instance?.Dispose();
            }
            catch
            {
                // Continue searching
            }
        }

        // Try fuzzy matching on combined manufacturer + model name (case-insensitive)
        foreach (var kvp in _radioTypes)
        {
            try
            {
                var instance = (IRadio?)Activator.CreateInstance(kvp.Value);
                if (instance != null)
                {
                    var fullName = $"{instance.Manufacturer} {instance.ModelName}";
                    if (fullName.Equals(cleanRadioName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (useResilientWrapper)
                        {
                            return new ResilientRadio(instance, logger);
                        }
                        return instance;
                    }
                    instance.Dispose();
                }
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
        catch (Exception)
        {
            // Discovery errors are handled silently - built-in radios are already registered
            // Service layer doesn't need to be aware of reflection failures
        }
    }

    /// <summary>
    /// Auto-detects radio type by sending identification commands
    /// </summary>
    /// <param name="serialPort">Serial port connected to radio</param>
    /// <returns>Auto-detected radio instance or null</returns>
    public static Task<IRadio?> AutoDetectRadioAsync(ISerialPort serialPort) =>
        throw new NotSupportedException("Automatic detection is unavailable during session migration. Select a model explicitly; probing cannot own a second serial reader.");

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
        if (response.Contains("ID033") || response.Contains("TS-890"))
            return CreateRadio("Kenwood", "TS-890S");
        if (response.Contains("ID023") || response.Contains("TS-590"))
            return CreateRadio("Kenwood", "TS-590SG");
        if (response.Contains("TH-D74") || response.Contains("THD74"))
            return CreateRadio("Kenwood", "TH-D74A");
        if (response.Contains("TM-D710") || response.Contains("TMD710"))
            return CreateRadio("Kenwood", "TM-D710GA");

        // Elecraft patterns  
        if (response.Contains("K3") || response.Contains("ELECRAFT K3"))
            return CreateRadio("Elecraft", "K3");
        if (response.Contains("K4") || response.Contains("ELECRAFT K4"))
            return CreateRadio("Elecraft", "K4");
        if (response.Contains("KX3") || response.Contains("ELECRAFT KX3"))
            return CreateRadio("Elecraft", "KX3");
        if (response.Contains("K2") || response.Contains("ELECRAFT K2"))
            return CreateRadio("Elecraft", "K2");
        if (response.Contains("K1") || response.Contains("ELECRAFT K1"))
            return CreateRadio("Elecraft", "K1");

        // Yaesu patterns
        if (response.Contains("FT-991") || response.Contains("991"))
            return CreateRadio("Yaesu", "FT-991A");
        if (response.Contains("FT-710") || response.Contains("710"))
            return CreateRadio("Yaesu", "FT-710");
        if (response.Contains("FT-DX101") || response.Contains("FTDX101"))
            return CreateRadio("Yaesu", "FT-DX101D");
        if (response.Contains("FT-891") || response.Contains("891"))
            return CreateRadio("Yaesu", "FT-891");
        if (response.Contains("FT-65") || response.Contains("FT65"))
            return CreateRadio("Yaesu", "FT-65");

        // Icom patterns
        if (response.Contains("IC-7300") || response.Contains("7300"))
            return CreateRadio("Icom", "IC-7300");
        if (response.Contains("IC-9700") || response.Contains("9700"))
            return CreateRadio("Icom", "IC-9700");

        // FlexRadio patterns
        if (response.Contains("FLEX-6400") || response.Contains("6400"))
            return CreateRadio("FlexRadio", "FLEX-6400");
        if (response.Contains("FLEX-6600") || response.Contains("6600"))
            return CreateRadio("FlexRadio", "FLEX-6600");
        if (response.Contains("FLEX-6700") || response.Contains("6700"))
            return CreateRadio("FlexRadio", "FLEX-6700");

        // Alinco patterns
        if (response.Contains("DX-SR8") || response.Contains("DXSR8"))
            return CreateRadio("Alinco", "DX-SR8T");
        if (response.Contains("DJ-MD5") || response.Contains("DJMD5"))
            return CreateRadio("Alinco", "DJ-MD5TGP");
        if (response.Contains("DR-638") || response.Contains("DR638"))
            return CreateRadio("Alinco", "DR-638T");
        if (response.Contains("DX-70") || response.Contains("DX70"))
            return CreateRadio("Alinco", "DX-70T");

        // Ten-Tec patterns
        if (response.Contains("OMNI") && response.Contains("VII"))
            return CreateRadio("Ten-Tec", "OMNI VII");
        if (response.Contains("EAGLE"))
            return CreateRadio("Ten-Tec", "Eagle");
        if (response.Contains("ARGONAUT") && response.Contains("V"))
            return CreateRadio("Ten-Tec", "Argonaut V");
        if (response.Contains("JUPITER"))
            return CreateRadio("Ten-Tec", "Jupiter");

        // Testing radios
        if (response.Contains("DUMMY") || response.Contains("TEST") || response.Contains("ID999"))
            return CreateRadio("SharpCAT2", "DummyRadio");

        return null;
    }
}