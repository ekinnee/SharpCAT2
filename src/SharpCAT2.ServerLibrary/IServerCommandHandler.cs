using SharpCAT2.ServerLibrary.Radio;
using System.Net.Sockets;

namespace SharpCAT2.ServerLibrary;

/// <summary>
/// Interface for handling server-side commands and network responses.
/// Separates command processing logic from main application logic.
/// </summary>
public interface IServerCommandHandler
{
    /// <summary>
    /// Handles special radio management commands sent over TCP
    /// </summary>
    /// <param name="command">Command to handle</param>
    /// <param name="networkStream">Network stream to send response</param>
    /// <returns>True if command was handled, false if it should be sent to serial port</returns>
    Task<bool> HandleRadioManagementCommandAsync(string command, NetworkStream networkStream);

    /// <summary>
    /// Processes user input as a radio command
    /// </summary>
    /// <param name="input">User input to process</param>
    /// <returns>Result of command processing</returns>
    Task<CommandProcessingResult> ProcessRadioCommandAsync(string input);

    /// <summary>
    /// Checks if input should be handled as a special console command
    /// </summary>
    /// <param name="input">User input</param>
    /// <returns>True if it's a special command like 's' for status</returns>
    bool IsSpecialCommand(string input);
}

/// <summary>
/// Result of command processing operations
/// </summary>
public record CommandProcessingResult(
    bool WasHandled,
    bool ShouldExit = false,
    string? OutputMessage = null,
    object? Data = null
);