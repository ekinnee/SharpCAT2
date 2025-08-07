using SharpCAT2.Radio.Models;

namespace SharpCAT2.Radio;

/// <summary>
/// Factory for creating radio instances
/// </summary>
public static class RadioFactory
{
    /// <summary>
    /// Creates a radio instance based on manufacturer and model
    /// </summary>
    /// <param name="manufacturer">Radio manufacturer</param>
    /// <param name="model">Radio model</param>
    /// <returns>Radio instance</returns>
    public static IRadio CreateRadio(string manufacturer, string model)
    {
        return manufacturer.ToUpperInvariant() switch
        {
            "ICOM" => CreateIcomRadio(model),
            "YAESU" => new YaesuRadio(model),
            "KENWOOD" => new KenwoodRadio(model),
            "ELECRAFT" => new ElecraftRadio(model),
            "FLEXRADIO" => new FlexRadio(model),
            _ => throw new NotSupportedException($"Unsupported radio manufacturer: {manufacturer}")
        };
    }
    
    /// <summary>
    /// Creates an Icom radio with appropriate CI-V address
    /// </summary>
    /// <param name="model">Icom model</param>
    /// <returns>Icom radio instance</returns>
    private static IcomRadio CreateIcomRadio(string model)
    {
        // Set CI-V addresses based on model
        byte address = model.ToUpperInvariant() switch
        {
            "IC-706" => 0x48,
            "IC-706MKII" => 0x4E,
            "IC-706MKIIG" => 0x58,
            "IC-718" => 0x5E,
            "IC-725" => 0x28,
            "IC-726" => 0x30,
            "IC-728" => 0x38,
            "IC-729" => 0x3A,
            "IC-735" => 0x04,
            "IC-736" => 0x40,
            "IC-737" => 0x3C,
            "IC-738" => 0x44,
            "IC-746" => 0x56,
            "IC-746PRO" => 0x66,
            "IC-751" => 0x1C,
            "IC-756" => 0x50,
            "IC-756PRO" => 0x5C,
            "IC-756PROII" => 0x64,
            "IC-756PROIII" => 0x6E,
            "IC-761" => 0x1E,
            "IC-765" => 0x2C,
            "IC-775" => 0x46,
            "IC-781" => 0x26,
            "IC-910" => 0x60,
            "IC-7000" => 0x70,
            "IC-7100" => 0x88,
            "IC-7200" => 0x76,
            "IC-7300" => 0x94,
            "IC-7400" => 0x6A,
            "IC-7410" => 0x80,
            "IC-7600" => 0x7A,
            "IC-7700" => 0x74,
            "IC-7800" => 0x6C,
            "IC-9100" => 0x7C,
            _ => 0x94 // Default address for IC-7300 and unknown models
        };
        
        return new IcomRadio(model, address);
    }
    
    /// <summary>
    /// Gets all supported manufacturers
    /// </summary>
    /// <returns>List of supported manufacturers</returns>
    public static IEnumerable<string> GetSupportedManufacturers()
    {
        return new[] { "Icom", "Yaesu", "Kenwood", "Elecraft", "FlexRadio" };
    }
    
    /// <summary>
    /// Gets supported models for a manufacturer
    /// </summary>
    /// <param name="manufacturer">Manufacturer name</param>
    /// <returns>List of supported models</returns>
    public static IEnumerable<string> GetSupportedModels(string manufacturer)
    {
        return manufacturer.ToUpperInvariant() switch
        {
            "ICOM" => GetIcomModels(),
            "YAESU" => GetYaesuModels(),
            "KENWOOD" => GetKenwoodModels(),
            "ELECRAFT" => GetElecraftModels(),
            "FLEXRADIO" => GetFlexRadioModels(),
            _ => Enumerable.Empty<string>()
        };
    }
    
    private static IEnumerable<string> GetIcomModels()
    {
        return new[]
        {
            "IC-706", "IC-706MKII", "IC-706MKIIG", "IC-718", "IC-725", "IC-726",
            "IC-728", "IC-729", "IC-735", "IC-736", "IC-737", "IC-738",
            "IC-746", "IC-746PRO", "IC-751", "IC-756", "IC-756PRO",
            "IC-756PROII", "IC-756PROIII", "IC-761", "IC-765", "IC-775",
            "IC-781", "IC-910", "IC-7000", "IC-7100", "IC-7200", "IC-7300",
            "IC-7400", "IC-7410", "IC-7600", "IC-7700", "IC-7800", "IC-9100"
        };
    }
    
    private static IEnumerable<string> GetYaesuModels()
    {
        return new[]
        {
            "FT-100", "FT-450", "FT-450D", "FT-847", "FT-857", "FT-857D",
            "FT-897", "FT-897D", "FT-950", "FT-991", "FT-991A", "FT-1000MP",
            "FT-2000", "FT-DX1200", "FT-DX3000", "FT-DX5000", "FT-DX9000"
        };
    }
    
    private static IEnumerable<string> GetKenwoodModels()
    {
        return new[]
        {
            "TS-50", "TS-450", "TS-450S", "TS-570", "TS-570D", "TS-590",
            "TS-590S", "TS-590SG", "TS-690", "TS-790", "TS-850", "TS-870",
            "TS-940", "TS-950", "TS-2000", "TS-480HX", "TS-480SAT"
        };
    }
    
    private static IEnumerable<string> GetElecraftModels()
    {
        return new[]
        {
            "K2", "K3", "K3S", "KX2", "KX3", "K4"
        };
    }
    
    private static IEnumerable<string> GetFlexRadioModels()
    {
        return new[]
        {
            "FLEX-1500", "FLEX-3000", "FLEX-5000", "FLEX-6300", "FLEX-6400",
            "FLEX-6400M", "FLEX-6500", "FLEX-6600", "FLEX-6600M", "FLEX-6700"
        };
    }
}