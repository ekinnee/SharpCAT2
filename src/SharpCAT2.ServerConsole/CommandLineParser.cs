using SharpCAT2.Core.Configuration;

namespace SharpCAT2.ServerConsole;

/// <summary>
/// Dedicated command line argument parser for the SharpCAT2 Server application.
/// Breaks down complex parsing logic into manageable, focused methods.
/// </summary>
public static class CommandLineParser
{
    /// <summary>
    /// Parses command line arguments into a structured options object
    /// </summary>
    /// <param name="args">Command line arguments array</param>
    /// <returns>Parsed command line options</returns>
    public static CommandLineOptions ParseArguments(string[] args)
    {
        var options = new CommandLineOptions();
        
        for (int i = 0; i < args.Length; i++)
        {
            i = ProcessArgument(args, i, options);
        }
        
        return options;
    }

    /// <summary>
    /// Processes a single command line argument and updates options accordingly
    /// </summary>
    /// <param name="args">All command line arguments</param>
    /// <param name="currentIndex">Current argument index</param>
    /// <param name="options">Options object to update</param>
    /// <returns>New index position after processing</returns>
    private static int ProcessArgument(string[] args, int currentIndex, CommandLineOptions options)
    {
        string currentArgument = args[currentIndex].ToLower();
        
        return currentArgument switch
        {
            "-p" or "--port" => ProcessPortArgument(args, currentIndex, options),
            "-b" or "--baud" => ProcessBaudRateArgument(args, currentIndex, options),
            "-t" or "--tcp-port" => ProcessTcpPortArgument(args, currentIndex, options),
            "-r" or "--radio" => ProcessRadioArgument(args, currentIndex, options),
            "--radio-info" => ProcessRadioInfoArgument(args, currentIndex, options),
            "-l" or "--list" => ProcessListPortsFlag(options, currentIndex),
            "-h" or "--help" => ProcessHelpFlag(options, currentIndex),
            "--auto-detect" => ProcessAutoDetectFlag(options, currentIndex),
            "--list-radios" => ProcessListRadiosFlag(options, currentIndex),
            _ => ProcessUnknownArgument(currentArgument, currentIndex)
        };
    }

    #region Argument Processing Methods

    /// <summary>
    /// Processes the port name argument (-p, --port)
    /// </summary>
    private static int ProcessPortArgument(string[] args, int currentIndex, CommandLineOptions options)
    {
        if (currentIndex + 1 >= args.Length)
            throw new ArgumentException("Missing port name argument.");
            
        string portName = args[currentIndex + 1];
        if (string.IsNullOrWhiteSpace(portName))
            throw new ArgumentException("Port name cannot be empty.");
            
        options.PortName = portName.Trim();
        return currentIndex + 1; // Skip next argument as it was consumed
    }

    /// <summary>
    /// Processes the baud rate argument (-b, --baud)
    /// </summary>
    private static int ProcessBaudRateArgument(string[] args, int currentIndex, CommandLineOptions options)
    {
        if (currentIndex + 1 >= args.Length)
            throw new ArgumentException("Missing baud rate argument.");
            
        string baudString = args[currentIndex + 1];
        if (!int.TryParse(baudString, out int baud))
            throw new ArgumentException($"Invalid baud rate '{baudString}'. Must be a valid integer.");
            
        if (!Constants.SupportedBaudRates.Contains(baud))
            throw new ArgumentException($"Unsupported baud rate '{baud}'.");
            
        options.BaudRate = baud;
        return currentIndex + 1;
    }

    /// <summary>
    /// Processes the TCP port argument (-t, --tcp-port)
    /// </summary>
    private static int ProcessTcpPortArgument(string[] args, int currentIndex, CommandLineOptions options)
    {
        if (currentIndex + 1 >= args.Length)
            throw new ArgumentException("Invalid or missing TCP port number.");
            
        string portString = args[currentIndex + 1];
        if (!int.TryParse(portString, out int tcpPort))
            throw new ArgumentException("Invalid or missing TCP port number.");
            
        if (tcpPort < Constants.MinTcpPort || tcpPort > Constants.MaxTcpPort)
            throw new ArgumentException($"TCP port '{tcpPort}' is out of valid range ({Constants.MinTcpPort}-{Constants.MaxTcpPort}).");
            
        options.TcpPort = tcpPort;
        return currentIndex + 1;
    }

    /// <summary>
    /// Processes the radio model argument (-r, --radio)
    /// </summary>
    private static int ProcessRadioArgument(string[] args, int currentIndex, CommandLineOptions options)
    {
        if (currentIndex + 1 >= args.Length)
            throw new ArgumentException("Missing radio model argument.");
            
        string radioModel = args[currentIndex + 1];
        if (string.IsNullOrWhiteSpace(radioModel))
            throw new ArgumentException("Radio model cannot be empty.");
            
        options.RadioModel = radioModel.Trim();
        return currentIndex + 1;
    }

    /// <summary>
    /// Processes the radio info argument (--radio-info)
    /// </summary>
    private static int ProcessRadioInfoArgument(string[] args, int currentIndex, CommandLineOptions options)
    {
        if (currentIndex + 1 >= args.Length)
            throw new ArgumentException("Missing radio info argument.");
            
        string radioInfo = args[currentIndex + 1];
        if (string.IsNullOrWhiteSpace(radioInfo))
            throw new ArgumentException("Radio info parameter cannot be empty.");
            
        options.ShowRadioInfo = radioInfo.Trim();
        return currentIndex + 1;
    }

    /// <summary>
    /// Processes flag-only arguments that don't take parameters
    /// </summary>
    private static int ProcessListPortsFlag(CommandLineOptions options, int currentIndex)
    {
        options.ListPorts = true;
        return currentIndex;
    }

    private static int ProcessHelpFlag(CommandLineOptions options, int currentIndex)
    {
        options.ShowHelp = true;
        return currentIndex;
    }

    private static int ProcessAutoDetectFlag(CommandLineOptions options, int currentIndex)
    {
        options.AutoDetectRadio = true;
        return currentIndex;
    }

    private static int ProcessListRadiosFlag(CommandLineOptions options, int currentIndex)
    {
        options.ListRadios = true;
        return currentIndex;
    }

    /// <summary>
    /// Handles unknown arguments
    /// </summary>
    private static int ProcessUnknownArgument(string argumentText, int currentIndex)
    {
        if (argumentText.StartsWith("-"))
            throw new ArgumentException($"Unknown argument '{argumentText}'.");
            
        return currentIndex; // Ignore non-flag arguments
    }

    #endregion
}