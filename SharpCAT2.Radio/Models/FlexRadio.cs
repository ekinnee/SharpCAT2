using System.Text;

namespace SharpCAT2.Radio.Models;

/// <summary>
/// Implementation for FlexRadio systems using Smart CAT protocol
/// </summary>
public class FlexRadio : RadioBase
{
    private const char COMMAND_TERMINATOR = '\n';
    
    /// <summary>
    /// Initializes a new instance of the FlexRadio class
    /// </summary>
    /// <param name="model">Radio model name</param>
    public FlexRadio(string model)
    {
        Model = model;
    }
    
    /// <summary>
    /// Gets the radio manufacturer name
    /// </summary>
    public override string Manufacturer => "FlexRadio";
    
    /// <summary>
    /// Gets the radio model name
    /// </summary>
    public override string Model { get; }
    
    /// <summary>
    /// Gets the protocol name used by this radio
    /// </summary>
    public override string Protocol => "Smart CAT";
    
    /// <summary>
    /// Gets the current frequency
    /// </summary>
    /// <returns>Current frequency in Hz</returns>
    public override async Task<long> GetFrequencyAsync()
    {
        var command = new RadioCommand(RadioCommandType.GetFrequency, "ZZFA;\n", "Get Frequency A");
        var response = await SendCommandAsync(command);
        
        if (!response.Success)
            throw new InvalidOperationException("Failed to get frequency from radio");
        
        return ParseFrequencyFromFlex(response.AsString);
    }
    
    /// <summary>
    /// Sets the frequency
    /// </summary>
    /// <param name="frequency">Frequency in Hz</param>
    public override async Task SetFrequencyAsync(long frequency)
    {
        var freqString = $"ZZFA{frequency:D11};\n";
        var command = new RadioCommand(RadioCommandType.SetFrequency, freqString, $"Set Frequency to {frequency} Hz");
        var response = await SendCommandAsync(command);
        
        if (!response.Success)
            throw new InvalidOperationException("Failed to set frequency on radio");
    }
    
    /// <summary>
    /// Gets the current operating mode
    /// </summary>
    /// <returns>Current operating mode</returns>
    public override async Task<RadioMode> GetModeAsync()
    {
        var command = new RadioCommand(RadioCommandType.GetMode, "ZZMD;\n", "Get Mode");
        var response = await SendCommandAsync(command);
        
        if (!response.Success)
            throw new InvalidOperationException("Failed to get mode from radio");
        
        return ParseModeFromFlex(response.AsString);
    }
    
    /// <summary>
    /// Sets the operating mode
    /// </summary>
    /// <param name="mode">Operating mode</param>
    public override async Task SetModeAsync(RadioMode mode)
    {
        var modeCode = EncodeModeForFlex(mode);
        var command = new RadioCommand(RadioCommandType.SetMode, $"ZZMD{modeCode};\n", $"Set Mode to {mode}");
        var response = await SendCommandAsync(command);
        
        if (!response.Success)
            throw new InvalidOperationException("Failed to set mode on radio");
    }
    
    /// <summary>
    /// Performs FlexRadio-specific initialization
    /// </summary>
    protected override async Task InitializeAsync()
    {
        // Initialize Smart CAT mode
        var command = new RadioCommand(RadioCommandType.Custom, "ZZAI1;\n", "Enable Auto Information");
        await SendCommandAsync(command);
        
        await base.InitializeAsync();
    }
    
    /// <summary>
    /// Parses frequency from FlexRadio response
    /// </summary>
    /// <param name="response">FlexRadio response string</param>
    /// <returns>Frequency in Hz</returns>
    private long ParseFrequencyFromFlex(string response)
    {
        // Expected format: ZZFA00014045000; (14.045 MHz)
        if (!response.StartsWith("ZZFA") || response.Length < 15)
            throw new ArgumentException("Invalid FlexRadio frequency response");
        
        var freqStr = response.Substring(4, 11);
        if (long.TryParse(freqStr, out long frequency))
            return frequency;
        
        throw new ArgumentException("Unable to parse frequency from FlexRadio response");
    }
    
    /// <summary>
    /// Parses mode from FlexRadio response
    /// </summary>
    /// <param name="response">FlexRadio response string</param>
    /// <returns>Radio mode</returns>
    private RadioMode ParseModeFromFlex(string response)
    {
        // Expected format: ZZMD1; (LSB)
        if (!response.StartsWith("ZZMD") || response.Length < 6)
            throw new ArgumentException("Invalid FlexRadio mode response");
        
        var modeCode = response.Substring(4, 1);
        return modeCode switch
        {
            "0" => RadioMode.LSB,
            "1" => RadioMode.USB,
            "2" => RadioMode.CW,
            "3" => RadioMode.AM,
            "4" => RadioMode.FM,
            "5" => RadioMode.Digital, // DIGL
            "6" => RadioMode.Digital, // DIGU
            _ => RadioMode.Unknown
        };
    }
    
    /// <summary>
    /// Encodes mode for FlexRadio
    /// </summary>
    /// <param name="mode">Radio mode</param>
    /// <returns>FlexRadio mode code</returns>
    private string EncodeModeForFlex(RadioMode mode)
    {
        return mode switch
        {
            RadioMode.LSB => "0",
            RadioMode.USB => "1",
            RadioMode.CW => "2",
            RadioMode.AM => "3",
            RadioMode.FM => "4",
            RadioMode.Digital => "5",
            _ => throw new ArgumentException($"Unsupported mode for FlexRadio: {mode}")
        };
    }
    
    /// <summary>
    /// Checks if the FlexRadio response is complete
    /// </summary>
    protected override bool IsCompleteResponse(byte[] response, RadioCommand command)
    {
        return response.Length > 0 && response[^1] == (byte)COMMAND_TERMINATOR;
    }
    
    /// <summary>
    /// Gets the baud rate typically used by FlexRadio
    /// </summary>
    protected virtual int GetDefaultBaudRate() => 9600;
}