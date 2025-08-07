using System.IO.Ports;

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
    Task<bool> ConnectAsync(SerialPort port);

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
}