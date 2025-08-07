using System.Text;

namespace SharpCAT2.Radio.Models;

/// <summary>
/// Implementation for Elecraft radios using K3/KX protocol
/// </summary>
public class ElecraftRadio : RadioBase
{
    private const char COMMAND_TERMINATOR = ';';
    
    /// <summary>
    /// Initializes a new instance of the ElecraftRadio class
    /// </summary>
    /// <param name="model">Radio model name</param>
    public ElecraftRadio(string model)
    {
        Model = model;
    }
    
    /// <summary>
    /// Gets the radio manufacturer name
    /// </summary>
    public override string Manufacturer => "Elecraft";
    
    /// <summary>
    /// Gets the radio model name
    /// </summary>
    public override string Model { get; }
    
    /// <summary>
    /// Gets the protocol name used by this radio
    /// </summary>
    public override string Protocol => "K3/KX";
    
    /// <summary>
    /// Gets the current frequency
    /// </summary>
    /// <returns>Current frequency in Hz</returns>
    public override async Task<long> GetFrequencyAsync()
    {
        var command = new RadioCommand(RadioCommandType.GetFrequency, "FA;", "Get Frequency A");
        var response = await SendCommandAsync(command);
        
        if (!response.Success)
            throw new InvalidOperationException("Failed to get frequency from radio");
        
        return ParseFrequencyFromElecraft(response.AsString);
    }
    
    /// <summary>
    /// Sets the frequency
    /// </summary>
    /// <param name="frequency">Frequency in Hz</param>
    public override async Task SetFrequencyAsync(long frequency)
    {
        var freqString = $"FA{frequency:D11};";
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
        var command = new RadioCommand(RadioCommandType.GetMode, "MD;", "Get Mode");
        var response = await SendCommandAsync(command);
        
        if (!response.Success)
            throw new InvalidOperationException("Failed to get mode from radio");
        
        return ParseModeFromElecraft(response.AsString);
    }
    
    /// <summary>
    /// Sets the operating mode
    /// </summary>
    /// <param name="mode">Operating mode</param>
    public override async Task SetModeAsync(RadioMode mode)
    {
        var modeCode = EncodeModeForElecraft(mode);
        var command = new RadioCommand(RadioCommandType.SetMode, $"MD{modeCode};", $"Set Mode to {mode}");
        var response = await SendCommandAsync(command);
        
        if (!response.Success)
            throw new InvalidOperationException("Failed to set mode on radio");
    }
    
    /// <summary>
    /// Performs Elecraft-specific initialization
    /// </summary>
    protected override async Task InitializeAsync()
    {
        // Enable extended mode for K3/KX series
        var command = new RadioCommand(RadioCommandType.Custom, "K31;", "Enable Extended Mode");
        await SendCommandAsync(command);
        
        await base.InitializeAsync();
    }
    
    /// <summary>
    /// Parses frequency from Elecraft response
    /// </summary>
    /// <param name="response">Elecraft response string</param>
    /// <returns>Frequency in Hz</returns>
    private long ParseFrequencyFromElecraft(string response)
    {
        // Expected format: FA00014045000; (14.045 MHz)
        if (!response.StartsWith("FA") || response.Length < 13)
            throw new ArgumentException("Invalid Elecraft frequency response");
        
        var freqStr = response.Substring(2, 11);
        if (long.TryParse(freqStr, out long frequency))
            return frequency;
        
        throw new ArgumentException("Unable to parse frequency from Elecraft response");
    }
    
    /// <summary>
    /// Parses mode from Elecraft response
    /// </summary>
    /// <param name="response">Elecraft response string</param>
    /// <returns>Radio mode</returns>
    private RadioMode ParseModeFromElecraft(string response)
    {
        // Expected format: MD1; (LSB)
        if (!response.StartsWith("MD") || response.Length < 4)
            throw new ArgumentException("Invalid Elecraft mode response");
        
        var modeCode = response.Substring(2, 1);
        return modeCode switch
        {
            "1" => RadioMode.LSB,
            "2" => RadioMode.USB,
            "3" => RadioMode.CW,
            "4" => RadioMode.FM,
            "5" => RadioMode.AM,
            "6" => RadioMode.Digital, // DATA-A
            "7" => RadioMode.CW, // CW-REV
            "9" => RadioMode.Digital, // DATA-R
            _ => RadioMode.Unknown
        };
    }
    
    /// <summary>
    /// Encodes mode for Elecraft
    /// </summary>
    /// <param name="mode">Radio mode</param>
    /// <returns>Elecraft mode code</returns>
    private string EncodeModeForElecraft(RadioMode mode)
    {
        return mode switch
        {
            RadioMode.LSB => "1",
            RadioMode.USB => "2",
            RadioMode.CW => "3",
            RadioMode.FM => "4",
            RadioMode.AM => "5",
            RadioMode.Digital => "6",
            _ => throw new ArgumentException($"Unsupported mode for Elecraft: {mode}")
        };
    }
    
    /// <summary>
    /// Checks if the Elecraft response is complete
    /// </summary>
    protected override bool IsCompleteResponse(byte[] response, RadioCommand command)
    {
        return response.Length > 0 && response[^1] == (byte)COMMAND_TERMINATOR;
    }
    
    /// <summary>
    /// Gets the baud rate typically used by Elecraft radios
    /// </summary>
    protected virtual int GetDefaultBaudRate() => 38400;
}