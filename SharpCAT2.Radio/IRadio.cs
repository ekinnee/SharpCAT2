using System.IO.Ports;

namespace SharpCAT2.Radio;

/// <summary>
/// Core interface for radio communication abstraction
/// </summary>
public interface IRadio : IDisposable
{
    /// <summary>
    /// Gets the radio manufacturer name
    /// </summary>
    string Manufacturer { get; }
    
    /// <summary>
    /// Gets the radio model name
    /// </summary>
    string Model { get; }
    
    /// <summary>
    /// Gets the protocol name used by this radio
    /// </summary>
    string Protocol { get; }
    
    /// <summary>
    /// Gets or sets the serial port used for communication
    /// </summary>
    SerialPort? SerialPort { get; set; }
    
    /// <summary>
    /// Gets a value indicating whether the radio is connected
    /// </summary>
    bool IsConnected { get; }
    
    /// <summary>
    /// Opens connection to the radio
    /// </summary>
    /// <param name="portName">Serial port name</param>
    /// <param name="baudRate">Baud rate for communication</param>
    /// <returns>True if connection successful</returns>
    Task<bool> ConnectAsync(string portName, int baudRate);
    
    /// <summary>
    /// Closes connection to the radio
    /// </summary>
    Task DisconnectAsync();
    
    /// <summary>
    /// Sends a command to the radio
    /// </summary>
    /// <param name="command">Command to send</param>
    /// <returns>Response from radio</returns>
    Task<RadioResponse> SendCommandAsync(RadioCommand command);
    
    /// <summary>
    /// Gets the current frequency
    /// </summary>
    /// <returns>Current frequency in Hz</returns>
    Task<long> GetFrequencyAsync();
    
    /// <summary>
    /// Sets the frequency
    /// </summary>
    /// <param name="frequency">Frequency in Hz</param>
    Task SetFrequencyAsync(long frequency);
    
    /// <summary>
    /// Gets the current operating mode
    /// </summary>
    /// <returns>Current operating mode</returns>
    Task<RadioMode> GetModeAsync();
    
    /// <summary>
    /// Sets the operating mode
    /// </summary>
    /// <param name="mode">Operating mode</param>
    Task SetModeAsync(RadioMode mode);
}