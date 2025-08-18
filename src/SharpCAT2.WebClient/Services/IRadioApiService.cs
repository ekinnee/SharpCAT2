using SharpCAT2.WebClient.Models;

namespace SharpCAT2.WebClient.Services;

/// <summary>
/// Interface for communicating with the SharpCAT2 WebApi
/// </summary>
public interface IRadioApiService
{
    /// <summary>
    /// Get the current radio connection status
    /// </summary>
    Task<RadioStatusResponse?> GetStatusAsync();

    /// <summary>
    /// Connect to a radio
    /// </summary>
    /// <param name="request">Connection parameters</param>
    Task<ApiResponse?> ConnectAsync(ConnectRequest request);

    /// <summary>
    /// Disconnect from the current radio
    /// </summary>
    Task<ApiResponse?> DisconnectAsync();

    /// <summary>
    /// Send a command to the radio
    /// </summary>
    /// <param name="command">Command to send</param>
    Task<CommandResponse?> SendCommandAsync(string command);

    /// <summary>
    /// Get list of available radio models
    /// </summary>
    Task<List<RadioModel>?> GetAvailableRadiosAsync();

    /// <summary>
    /// Get list of available serial ports
    /// </summary>
    Task<List<string>?> GetAvailableSerialPortsAsync();
}