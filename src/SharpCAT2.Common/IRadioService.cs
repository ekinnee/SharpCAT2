using SharpCAT2.Core.Radio;
using SharpCAT2.Core.Serial;
using SharpCAT2.Core.Configuration;

namespace SharpCAT2.Common;

/// <summary>
/// Service interface for managing radio connections and operations
/// </summary>
public interface IRadioService
{
    /// <summary>
    /// Gets the currently connected radio instance
    /// </summary>
    IRadio? ConnectedRadio { get; }

    /// <summary>
    /// Gets whether a radio is currently connected
    /// </summary>
    bool IsRadioConnected { get; }

    /// <summary>
    /// Initializes radio communication based on options
    /// </summary>
    /// <param name="options">Command line options containing radio settings</param>
    /// <param name="serialPort">Serial port to use for radio communication</param>
    /// <returns>Task representing the async initialization operation</returns>
    Task InitializeRadioAsync(CommandLineOptions options, ISerialPort serialPort);

    /// <summary>
    /// Attempts to process user input as a radio command
    /// </summary>
    /// <param name="input">User input to process as a radio command</param>
    /// <returns>True if the input was processed as a radio command, false otherwise</returns>
    Task<bool> TryProcessRadioCommandAsync(string input);

    /// <summary>
    /// Displays comprehensive status information for the connected radio
    /// </summary>
    /// <returns>Radio status information or null if no radio connected</returns>
    Task<string?> GetRadioStatusAsync();

    /// <summary>
    /// Changes the active radio to the specified model
    /// </summary>
    /// <param name="radioName">Name of the radio model to switch to</param>
    /// <param name="serialPort">Serial port to use for the new radio connection</param>
    /// <returns>True if radio was changed successfully, false otherwise</returns>
    Task<bool> ChangeRadioAsync(string radioName, ISerialPort serialPort);

    /// <summary>
    /// Gets a list of available radio models
    /// </summary>
    /// <returns>Dictionary of available radio models</returns>
    Dictionary<string, string> GetAvailableRadios();

    /// <summary>
    /// Gets detailed information about a specific radio model
    /// </summary>
    /// <param name="radioName">Name of the radio model</param>
    /// <returns>Radio information or null if not found</returns>
    string? GetRadioInfo(string radioName);

    /// <summary>
    /// Disconnects the current radio
    /// </summary>
    /// <returns>Task representing the async disconnect operation</returns>
    Task DisconnectRadioAsync();
}