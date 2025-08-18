using SharpCAT2.Core.Configuration;

namespace SharpCAT2.ServerConsole;

/// <summary>
/// Centralized constants for the SharpCAT2 Server application.
/// Consolidates magic numbers, timeouts, and configuration values to improve maintainability.
/// </summary>
public static class Constants
{
    #region Serial Communication
    
    /// <summary>
    /// Array of supported baud rates for serial communication.
    /// These rates are validated to ensure compatibility with common radio interfaces.
    /// </summary>
    public static readonly int[] SupportedBaudRates = { 9600, 14400, 19200, 28800, 38400, 57600, 115200, 128000, 256000 };

    /// <summary>
    /// Default baud rate for serial communication
    /// </summary>
    public const int DefaultBaudRate = 9600;

    /// <summary>
    /// Default read timeout for serial port operations in milliseconds
    /// </summary>
    public const int DefaultSerialReadTimeout = 500;

    /// <summary>
    /// Default write timeout for serial port operations in milliseconds
    /// </summary>
    public const int DefaultSerialWriteTimeout = 500;

    #endregion

    #region Network Configuration

    /// <summary>
    /// Default TCP port for server connections
    /// </summary>
    public const int DefaultTcpPort = 8080;

    /// <summary>
    /// Minimum valid TCP port number
    /// </summary>
    public const int MinTcpPort = 1;

    /// <summary>
    /// Maximum valid TCP port number
    /// </summary>
    public const int MaxTcpPort = 65535;

    #endregion

    #region Configuration Files

    /// <summary>
    /// Default server configuration file name
    /// </summary>
    public const string ServerConfigFileName = "server_config.json";

    /// <summary>
    /// Default client configuration file name
    /// </summary>
    public const string ClientConfigFileName = "client_config.json";

    #endregion

    #region Error Recovery

    /// <summary>
    /// Default delay between retry attempts in milliseconds.
    /// This value balances responsiveness with avoiding overwhelming failed hardware.
    /// </summary>
    public const int DefaultRetryDelayMs = 1000;

    /// <summary>
    /// Default maximum number of retry attempts for operations.
    /// After this many failures, the operation is considered permanently failed.
    /// </summary>
    public const int DefaultMaxRetryAttempts = 3;

    #endregion

    #region Platform-Specific Examples

    /// <summary>
    /// Example Windows serial port name
    /// </summary>
    public const string WindowsPortExample = "COM1";

    /// <summary>
    /// Example Linux serial port name
    /// </summary>
    public const string LinuxPortExample = "/dev/ttyUSB0";

    /// <summary>
    /// Example macOS serial port name
    /// </summary>
    public const string MacOSPortExample = "/dev/cu.usbserial-1410";

    #endregion

    #region User Interface

    /// <summary>
    /// Prompt character sequence displayed to users for command input.
    /// Using "Serial>" indicates this is for serial/radio communication.
    /// </summary>
    public const string UserPrompt = "Serial> ";

    /// <summary>
    /// Application title displayed at startup.
    /// Clearly identifies the application and its primary purpose.
    /// </summary>
    public const string ApplicationTitle = "SharpCAT2 Server - Cross-Platform Serial Port Communication";

    /// <summary>
    /// Visual separator line that matches the title length for consistent formatting.
    /// </summary>
    public const string TitleSeparator = "============================================================";

    #endregion
}