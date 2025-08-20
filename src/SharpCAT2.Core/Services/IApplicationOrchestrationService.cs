using SharpCAT2.Core.Configuration;
using SharpCAT2.Core.Serial;

namespace SharpCAT2.Core.Services;

/// <summary>
/// Interface for orchestrating the main application flow.
/// Separates application coordination from implementation details.
/// </summary>
public interface IApplicationOrchestrationService
{
    /// <summary>
    /// Runs the main application loop for console input and command processing
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task RunMainLoopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Initializes the application with command line options
    /// </summary>
    /// <param name="options">Parsed command line options</param>
    /// <param name="serialPort">Serial port for radio communication</param>
    /// <returns>Task representing the async operation</returns>
    Task InitializeAsync(CommandLineOptions options, ISerialPort serialPort);

    /// <summary>
    /// Shuts down the application gracefully
    /// </summary>
    /// <param name="options">Command line options for configuration saving</param>
    /// <returns>Task representing the async operation</returns>
    Task ShutdownAsync(CommandLineOptions options);

    /// <summary>
    /// Handles special application commands like help, list ports, etc.
    /// </summary>
    /// <param name="options">Command line options</param>
    /// <returns>True if a special command was handled and app should exit</returns>
    bool HandleSpecialCommands(CommandLineOptions options);
}