namespace SharpCAT2.Core.Services;

/// <summary>
/// Interface for user interface operations, separating presentation concerns from business logic.
/// Provides a clean abstraction for console I/O and user interaction.
/// </summary>
public interface IUserInterfaceService
{
    /// <summary>
    /// Displays a message to the user
    /// </summary>
    /// <param name="message">Message to display</param>
    void WriteLine(string message);

    /// <summary>
    /// Displays a message without a newline
    /// </summary>
    /// <param name="message">Message to display</param>
    void Write(string message);

    /// <summary>
    /// Reads a line of input from the user
    /// </summary>
    /// <returns>User input or null if cancelled</returns>
    string? ReadLine();

    /// <summary>
    /// Reads a key from the user
    /// </summary>
    /// <returns>Key pressed by user</returns>
    ConsoleKeyInfo ReadKey();

    /// <summary>
    /// Displays help information for the application
    /// </summary>
    void ShowHelp();

    /// <summary>
    /// Displays available serial ports with platform-specific guidance
    /// </summary>
    /// <param name="ports">Available port names</param>
    void ShowAvailablePorts(string[] ports);

    /// <summary>
    /// Displays available radio models
    /// </summary>
    /// <param name="radios">Dictionary of radio models</param>
    void ShowAvailableRadios(Dictionary<string, string> radios);

    /// <summary>
    /// Displays detailed radio information
    /// </summary>
    /// <param name="radioInfo">Formatted radio information</param>
    void ShowRadioInfo(string radioInfo);

    /// <summary>
    /// Shows a warning message
    /// </summary>
    /// <param name="message">Warning message</param>
    void ShowWarning(string message);

    /// <summary>
    /// Shows an error message
    /// </summary>
    /// <param name="message">Error message</param>
    void ShowError(string message);

    /// <summary>
    /// Shows an informational message
    /// </summary>
    /// <param name="message">Information message</param>
    void ShowInfo(string message);
}