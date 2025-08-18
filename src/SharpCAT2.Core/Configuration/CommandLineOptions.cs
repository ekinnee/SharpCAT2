namespace SharpCAT2.Core.Configuration;

/// <summary>
/// Represents command line options for the SharpCAT2 Server application.
/// Contains all configurable parameters that can be specified via command line arguments.
/// </summary>
public class CommandLineOptions
{
    #region Serial Port Configuration

    /// <summary>
    /// Gets or sets the serial port name (e.g., COM1, /dev/ttyUSB0).
    /// If null, the application will prompt for port selection.
    /// </summary>
    public string? PortName { get; set; }

    /// <summary>
    /// Gets or sets the baud rate for serial communication.
    /// Default is 9600. Must be one of the supported rates defined in SupportedBaudRates.
    /// </summary>
    public int BaudRate { get; set; } = 9600;

    #endregion

    #region Network Configuration

    /// <summary>
    /// Gets or sets the TCP port for the remote client server.
    /// Default is 8080. Remote clients connect to this port to send commands.
    /// </summary>
    public int TcpPort { get; set; } = 8080;

    #endregion

    #region Radio Configuration

    /// <summary>
    /// Gets or sets the radio model name for CAT control.
    /// Should match one of the available radio models from RadioFactory.
    /// </summary>
    public string? RadioModel { get; set; }

    /// <summary>
    /// Gets or sets whether to automatically detect the connected radio type.
    /// When true, the application will attempt to identify the radio model.
    /// </summary>
    public bool AutoDetectRadio { get; set; }

    #endregion

    #region Information Display Options

    /// <summary>
    /// Gets or sets whether to list available serial ports and exit.
    /// </summary>
    public bool ListPorts { get; set; }

    /// <summary>
    /// Gets or sets whether to show help information and exit.
    /// </summary>
    public bool ShowHelp { get; set; }

    /// <summary>
    /// Gets or sets whether to list available radio models and exit.
    /// </summary>
    public bool ListRadios { get; set; }

    /// <summary>
    /// Gets or sets the radio model name to show detailed information for.
    /// When set, displays comprehensive feature information for the specified radio and exits.
    /// </summary>
    public string? ShowRadioInfo { get; set; }

    #endregion
}