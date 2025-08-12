namespace SharpCAT2.Client;

/// <summary>
/// Centralized constants for the SharpCAT2 Client application.
/// Provides consistency and maintainability for commonly used values.
/// </summary>
public static class ClientConstants
{
    #region Network Configuration

    /// <summary>
    /// Default server hostname for connections
    /// </summary>
    public const string DefaultServerHost = "localhost";

    /// <summary>
    /// Default server port for connections
    /// </summary>
    public const int DefaultServerPort = 8080;

    #endregion

    #region Configuration Files

    /// <summary>
    /// Default client configuration file name
    /// </summary>
    public const string ConfigFileName = "client_config.json";

    #endregion

    #region User Interface

    /// <summary>
    /// Command prompt character sequence
    /// </summary>
    public const string CommandPrompt = "Serial> ";

    /// <summary>
    /// Application title displayed at startup
    /// </summary>
    public const string ApplicationTitle = "SharpCAT2 Client - Remote Serial Port Communication";

    /// <summary>
    /// Application title separator
    /// </summary>
    public const string TitleSeparator = "===================================================";

    #endregion

    #region Commands

    /// <summary>
    /// Command to exit the application
    /// </summary>
    public const string ExitCommand = "quit";

    /// <summary>
    /// Alternative command to exit the application
    /// </summary>
    public const string AlternativeExitCommand = "exit";

    /// <summary>
    /// Command to show help information
    /// </summary>
    public const string HelpCommand = "help";

    /// <summary>
    /// Command to show connection status
    /// </summary>
    public const string StatusCommand = "status";

    /// <summary>
    /// Command to show radio status
    /// </summary>
    public const string RadioStatusCommand = "radio-status";

    /// <summary>
    /// Short form of radio status command
    /// </summary>
    public const string RadioStatusShortCommand = "rs";

    /// <summary>
    /// Command to list available radios
    /// </summary>
    public const string ListRadiosCommand = "list-radios";

    /// <summary>
    /// Alternative command to list radios
    /// </summary>
    public const string RadiosCommand = "radios";

    /// <summary>
    /// Command to get current radio information
    /// </summary>
    public const string CurrentRadioCommand = "current-radio";

    /// <summary>
    /// Alternative command to get current radio
    /// </summary>
    public const string GetCurrentRadioCommand = "get-current-radio";

    /// <summary>
    /// Command to set radio model
    /// </summary>
    public const string SetRadioCommand = "set-radio";

    #endregion

    #region Connection

    /// <summary>
    /// Connection timeout in milliseconds
    /// </summary>
    public const int ConnectionTimeoutMs = 5000;

    /// <summary>
    /// Reconnection interval in seconds
    /// </summary>
    public const int ReconnectionIntervalSeconds = 5;

    #endregion
}