namespace SharpCAT2.Radio.Models;

/// <summary>
/// Implementation for Icom radios using CI-V protocol
/// </summary>
public class IcomRadio : RadioBase
{
    private const byte CI_V_PREAMBLE = 0xFE;
    private const byte CI_V_END = 0xFD;
    private const byte CONTROLLER_ADDRESS = 0xE0;
    
    private readonly byte _radioAddress;
    
    /// <summary>
    /// Initializes a new instance of the IcomRadio class
    /// </summary>
    /// <param name="model">Radio model name</param>
    /// <param name="radioAddress">CI-V address of the radio</param>
    public IcomRadio(string model, byte radioAddress = 0x94)
    {
        Model = model;
        _radioAddress = radioAddress;
    }
    
    /// <summary>
    /// Gets the radio manufacturer name
    /// </summary>
    public override string Manufacturer => "Icom";
    
    /// <summary>
    /// Gets the radio model name
    /// </summary>
    public override string Model { get; }
    
    /// <summary>
    /// Gets the protocol name used by this radio
    /// </summary>
    public override string Protocol => "CI-V";
    
    /// <summary>
    /// Gets the current frequency
    /// </summary>
    /// <returns>Current frequency in Hz</returns>
    public override async Task<long> GetFrequencyAsync()
    {
        var command = CreateCIVCommand(0x03); // Get frequency command
        var response = await SendCommandAsync(new RadioCommand(RadioCommandType.GetFrequency, command, "Get Frequency"));
        
        if (!response.Success || response.Data.Length < 11)
            throw new InvalidOperationException("Failed to get frequency from radio");
        
        // Parse CI-V frequency response (BCD format)
        return ParseFrequencyFromCIV(response.Data);
    }
    
    /// <summary>
    /// Sets the frequency
    /// </summary>
    /// <param name="frequency">Frequency in Hz</param>
    public override async Task SetFrequencyAsync(long frequency)
    {
        var freqBytes = EncodeFrequencyToCIV(frequency);
        var command = CreateCIVCommand(0x05, freqBytes); // Set frequency command
        var response = await SendCommandAsync(new RadioCommand(RadioCommandType.SetFrequency, command, $"Set Frequency to {frequency} Hz"));
        
        if (!response.Success)
            throw new InvalidOperationException("Failed to set frequency on radio");
    }
    
    /// <summary>
    /// Gets the current operating mode
    /// </summary>
    /// <returns>Current operating mode</returns>
    public override async Task<RadioMode> GetModeAsync()
    {
        var command = CreateCIVCommand(0x04); // Get mode command
        var response = await SendCommandAsync(new RadioCommand(RadioCommandType.GetMode, command, "Get Mode"));
        
        if (!response.Success || response.Data.Length < 8)
            throw new InvalidOperationException("Failed to get mode from radio");
        
        // Parse CI-V mode response
        return ParseModeFromCIV(response.Data[6]);
    }
    
    /// <summary>
    /// Sets the operating mode
    /// </summary>
    /// <param name="mode">Operating mode</param>
    public override async Task SetModeAsync(RadioMode mode)
    {
        var modeData = new[] { EncodeModeForCIV(mode) };
        var command = CreateCIVCommand(0x06, modeData); // Set mode command
        var response = await SendCommandAsync(new RadioCommand(RadioCommandType.SetMode, command, $"Set Mode to {mode}"));
        
        if (!response.Success)
            throw new InvalidOperationException("Failed to set mode on radio");
    }
    
    /// <summary>
    /// Creates a CI-V command with the specified command code and optional data
    /// </summary>
    /// <param name="commandCode">CI-V command code</param>
    /// <param name="data">Optional data bytes</param>
    /// <returns>Complete CI-V command bytes</returns>
    private byte[] CreateCIVCommand(byte commandCode, byte[]? data = null)
    {
        var command = new List<byte>
        {
            CI_V_PREAMBLE,
            CI_V_PREAMBLE,
            _radioAddress,
            CONTROLLER_ADDRESS,
            commandCode
        };
        
        if (data != null)
        {
            command.AddRange(data);
        }
        
        command.Add(CI_V_END);
        
        return command.ToArray();
    }
    
    /// <summary>
    /// Parses frequency from CI-V response
    /// </summary>
    /// <param name="response">CI-V response bytes</param>
    /// <returns>Frequency in Hz</returns>
    private long ParseFrequencyFromCIV(byte[] response)
    {
        // CI-V frequency is in BCD format, 5 bytes starting at position 6
        // Format: 00 00 14 45 00 for 14.45000 MHz
        if (response.Length < 11)
            throw new ArgumentException("Invalid CI-V frequency response");
        
        long frequency = 0;
        for (int i = 5; i >= 1; i--)
        {
            byte bcd = response[5 + i];
            frequency = frequency * 100 + (bcd >> 4) * 10 + (bcd & 0x0F);
        }
        
        return frequency * 10; // CI-V uses 10 Hz resolution
    }
    
    /// <summary>
    /// Encodes frequency to CI-V BCD format
    /// </summary>
    /// <param name="frequency">Frequency in Hz</param>
    /// <returns>BCD encoded frequency bytes</returns>
    private byte[] EncodeFrequencyToCIV(long frequency)
    {
        frequency /= 10; // CI-V uses 10 Hz resolution
        var result = new byte[5];
        
        for (int i = 0; i < 5; i++)
        {
            int tens = (int)(frequency % 100) / 10;
            int ones = (int)(frequency % 10);
            result[i] = (byte)((tens << 4) | ones);
            frequency /= 100;
        }
        
        return result;
    }
    
    /// <summary>
    /// Parses mode from CI-V response
    /// </summary>
    /// <param name="modeByte">CI-V mode byte</param>
    /// <returns>Radio mode</returns>
    private RadioMode ParseModeFromCIV(byte modeByte)
    {
        return modeByte switch
        {
            0x00 => RadioMode.LSB,
            0x01 => RadioMode.USB,
            0x02 => RadioMode.AM,
            0x03 => RadioMode.CW,
            0x05 => RadioMode.FM,
            0x08 => RadioMode.Digital,
            _ => RadioMode.Unknown
        };
    }
    
    /// <summary>
    /// Encodes mode for CI-V
    /// </summary>
    /// <param name="mode">Radio mode</param>
    /// <returns>CI-V mode byte</returns>
    private byte EncodeModeForCIV(RadioMode mode)
    {
        return mode switch
        {
            RadioMode.LSB => 0x00,
            RadioMode.USB => 0x01,
            RadioMode.AM => 0x02,
            RadioMode.CW => 0x03,
            RadioMode.FM => 0x05,
            RadioMode.Digital => 0x08,
            _ => throw new ArgumentException($"Unsupported mode for Icom CI-V: {mode}")
        };
    }
    
    /// <summary>
    /// Checks if the CI-V response is complete
    /// </summary>
    protected override bool IsCompleteResponse(byte[] response, RadioCommand command)
    {
        return response.Length >= 6 && response[^1] == CI_V_END;
    }
}