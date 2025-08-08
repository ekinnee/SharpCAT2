using SharpCAT2.Common.Serial;

namespace SharpCAT2.Radio;

/// <summary>
/// Interface defining the contract for radio communication
/// </summary>
public interface IRadio : IDisposable
{
    /// <summary>
    /// Gets the radio model name
    /// </summary>
    string ModelName { get; }

    /// <summary>
    /// Gets the radio manufacturer
    /// </summary>
    string Manufacturer { get; }

    /// <summary>
    /// Gets the features supported by this radio
    /// </summary>
    SupportedFeatures SupportedFeatures { get; }

    /// <summary>
    /// Gets whether the radio is currently connected
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Gets or sets the frequency in Hz
    /// </summary>
    long Frequency { get; set; }

    /// <summary>
    /// Gets or sets the operating mode (e.g., LSB, USB, CW, AM, FM)
    /// </summary>
    string Mode { get; set; }

    /// <summary>
    /// Connects to the radio using the specified serial port
    /// </summary>
    /// <param name="port">Serial port to use for communication</param>
    /// <returns>True if connection successful, false otherwise</returns>
    Task<bool> ConnectAsync(ISerialPort port);

    /// <summary>
    /// Disconnects from the radio
    /// </summary>
    void Disconnect();

    /// <summary>
    /// Sends a command to the radio and returns the response
    /// </summary>
    /// <param name="command">Command to send</param>
    /// <returns>Response from the radio</returns>
    Task<string?> SendCommandAsync(RadioCommand command);

    /// <summary>
    /// Gets the current radio status/information
    /// </summary>
    /// <returns>Radio status information</returns>
    Task<RadioStatus> GetStatusAsync();

    /// <summary>
    /// Sets the radio frequency
    /// </summary>
    /// <param name="frequency">Frequency in Hz</param>
    /// <returns>True if successful</returns>
    Task<bool> SetFrequencyAsync(long frequency);

    /// <summary>
    /// Sets the radio operating mode
    /// </summary>
    /// <param name="mode">Operating mode</param>
    /// <returns>True if successful</returns>
    Task<bool> SetModeAsync(string mode);

    // Extended radio control methods
    
    /// <summary>
    /// Gets the current VFO (A or B)
    /// </summary>
    /// <returns>Current VFO designation</returns>
    Task<string> GetVfoAsync();

    /// <summary>
    /// Sets the active VFO
    /// </summary>
    /// <param name="vfo">VFO to activate (A or B)</param>
    /// <returns>True if successful</returns>
    Task<bool> SetVfoAsync(string vfo);

    /// <summary>
    /// Swaps VFO A and VFO B frequencies
    /// </summary>
    /// <returns>True if successful</returns>
    Task<bool> SwapVfoAsync();

    /// <summary>
    /// Enables or disables split operation
    /// </summary>
    /// <param name="enabled">True to enable split, false to disable</param>
    /// <returns>True if successful</returns>
    Task<bool> SetSplitAsync(bool enabled);

    /// <summary>
    /// Gets the current split operation status
    /// </summary>
    /// <returns>True if split is enabled</returns>
    Task<bool> GetSplitAsync();

    /// <summary>
    /// Sets the RIT (Receiver Incremental Tuning) offset
    /// </summary>
    /// <param name="offsetHz">Offset in Hz</param>
    /// <returns>True if successful</returns>
    Task<bool> SetRitAsync(int offsetHz);

    /// <summary>
    /// Gets the current RIT offset
    /// </summary>
    /// <returns>RIT offset in Hz</returns>
    Task<int> GetRitAsync();

    /// <summary>
    /// Sets the XIT (Transmitter Incremental Tuning) offset
    /// </summary>
    /// <param name="offsetHz">Offset in Hz</param>
    /// <returns>True if successful</returns>
    Task<bool> SetXitAsync(int offsetHz);

    /// <summary>
    /// Gets the current XIT offset
    /// </summary>
    /// <returns>XIT offset in Hz</returns>
    Task<int> GetXitAsync();

    /// <summary>
    /// Sets the IF bandwidth
    /// </summary>
    /// <param name="bandwidth">Bandwidth in Hz</param>
    /// <returns>True if successful</returns>
    Task<bool> SetIfBandwidthAsync(int bandwidth);

    /// <summary>
    /// Gets the current IF bandwidth
    /// </summary>
    /// <returns>Bandwidth in Hz</returns>
    Task<int> GetIfBandwidthAsync();

    /// <summary>
    /// Sets the power output level
    /// </summary>
    /// <param name="powerPercent">Power level as percentage (0-100)</param>
    /// <returns>True if successful</returns>
    Task<bool> SetPowerOutputAsync(int powerPercent);

    /// <summary>
    /// Gets the current power output level
    /// </summary>
    /// <returns>Power level as percentage (0-100)</returns>
    Task<int> GetPowerOutputAsync();

    /// <summary>
    /// Gets the S-meter reading
    /// </summary>
    /// <returns>S-meter reading (0-9 for S units, >9 for dB over S9)</returns>
    Task<int> GetSMeterAsync();

    /// <summary>
    /// Gets the SWR reading
    /// </summary>
    /// <returns>SWR reading (1.0 = perfect match)</returns>
    Task<double> GetSWRAsync();

    /// <summary>
    /// Sets the active antenna
    /// </summary>
    /// <param name="antenna">Antenna number (1-based)</param>
    /// <returns>True if successful</returns>
    Task<bool> SetAntennaAsync(int antenna);

    /// <summary>
    /// Gets the current active antenna
    /// </summary>
    /// <returns>Antenna number (1-based)</returns>
    Task<int> GetAntennaAsync();

    /// <summary>
    /// Sets a memory channel
    /// </summary>
    /// <param name="channel">Memory channel number</param>
    /// <param name="frequency">Frequency in Hz</param>
    /// <param name="mode">Operating mode</param>
    /// <returns>True if successful</returns>
    Task<bool> SetMemoryChannelAsync(int channel, long frequency, string mode);

    /// <summary>
    /// Recalls a memory channel
    /// </summary>
    /// <param name="channel">Memory channel number</param>
    /// <returns>True if successful</returns>
    Task<bool> RecallMemoryChannelAsync(int channel);

    /// <summary>
    /// Sets the CW keyer speed
    /// </summary>
    /// <param name="wpm">Words per minute</param>
    /// <returns>True if successful</returns>
    Task<bool> SetCwSpeedAsync(int wpm);

    /// <summary>
    /// Gets the current CW keyer speed
    /// </summary>
    /// <returns>Words per minute</returns>
    Task<int> GetCwSpeedAsync();

    /// <summary>
    /// Sends a CW message
    /// </summary>
    /// <param name="message">Message to send</param>
    /// <returns>True if successful</returns>
    Task<bool> SendCwMessageAsync(string message);

    /// <summary>
    /// Sets the noise reduction level
    /// </summary>
    /// <param name="level">Noise reduction level (0=off, 1-10=on with level)</param>
    /// <returns>True if successful</returns>
    Task<bool> SetNoiseReductionAsync(int level);

    /// <summary>
    /// Gets the current noise reduction level
    /// </summary>
    /// <returns>Noise reduction level</returns>
    Task<int> GetNoiseReductionAsync();

    /// <summary>
    /// Powers the radio on or off
    /// </summary>
    /// <param name="powerOn">True to power on, false to power off</param>
    /// <returns>True if successful</returns>
    Task<bool> SetPowerAsync(bool powerOn);

    /// <summary>
    /// Gets whether the radio is powered on
    /// </summary>
    /// <returns>True if powered on</returns>
    Task<bool> GetPowerAsync();
}