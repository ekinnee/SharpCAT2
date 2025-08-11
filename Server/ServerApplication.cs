using Microsoft.Extensions.Logging;
using SharpCAT2.Server.Services;
using SharpCAT2.Common.Serial;
using SharpCAT2.Common.Radio;
using SharpCAT2.Common.Utils;
using System.IO.Ports;
using System.Runtime.InteropServices;

namespace SharpCAT2.Server;

/// <summary>
/// Main server application logic using dependency injection and services
/// </summary>
public class ServerApplication
{
    private readonly ILogger<ServerApplication> _logger;
    private readonly IConfigurationService _configurationService;
    private readonly INetworkService _networkService;
    private readonly IRadioService _radioService;
    private readonly ISecurityService _securityService;

    private ISerialPort? _serialPort;
    private ServerConfig? _config;
    private CancellationTokenSource? _cancellationTokenSource;

    /// <summary>
    /// Array of supported baud rates for serial communication.
    /// </summary>
    private static readonly int[] SupportedBaudRates = { 9600, 14400, 19200, 28800, 38400, 57600, 115200, 128000, 256000 };

    /// <summary>
    /// Default read timeout for serial port operations in milliseconds
    /// </summary>
    private const int DefaultSerialReadTimeout = 500;

    /// <summary>
    /// Default write timeout for serial port operations in milliseconds
    /// </summary>
    private const int DefaultSerialWriteTimeout = 500;

    public ServerApplication(
        ILogger<ServerApplication> logger,
        IConfigurationService configurationService,
        INetworkService networkService,
        IRadioService radioService,
        ISecurityService securityService)
    {
        _logger = logger;
        _configurationService = configurationService;
        _networkService = networkService;
        _radioService = radioService;
        _securityService = securityService;

        // Subscribe to network events
        _networkService.DataReceived += OnNetworkDataReceived;
        _networkService.ClientConnected += OnClientConnected;
        _networkService.ClientDisconnected += OnClientDisconnected;
    }

    /// <summary>
    /// Runs the server application with the provided command line arguments
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <returns>Task representing the async operation</returns>
    public async Task RunAsync(string[] args)
    {
        try
        {
            // Load configuration
            const string configPath = "server_config.json";
            _config = await _configurationService.LoadConfigurationAsync(configPath);
            
            // Parse command line arguments
            var options = ParseArguments(args);
            
            // Apply configuration to options if not overridden by command line
            _configurationService.ApplyConfigurationToOptions(_config, options);
            
            if (options.ShowHelp)
            {
                ShowHelp();
                return;
            }
            
            if (options.ListPorts)
            {
                ListAvailablePorts();
                return;
            }

            if (options.ListRadios)
            {
                ListAvailableRadios();
                return;
            }

            if (!string.IsNullOrEmpty(options.ShowRadioInfo))
            {
                ShowRadioInfo(options.ShowRadioInfo);
                return;
            }
            
            // Validate or prompt for port name
            string portName = ValidateOrPromptPortName(options.PortName);
            
            // Open and configure serial port with resilient wrapper
            _serialPort = CreateResilientSerialPort(portName, options.BaudRate);
            
            _logger.LogInformation("Successfully opened serial port: {PortName}", portName);
            _logger.LogInformation("Baud rate: {BaudRate}", options.BaudRate);

            // Initialize radio if specified
            await _radioService.InitializeRadioAsync(options, _serialPort);
            
            // Start TCP server
            _cancellationTokenSource = new CancellationTokenSource();
            await _networkService.StartAsync(options.TcpPort, _cancellationTokenSource.Token);
            
            _logger.LogInformation("TCP server listening on port {TcpPort}", options.TcpPort);
            
            if (_radioService.IsRadioConnected)
            {
                _logger.LogInformation("Connected to radio: {Manufacturer} {ModelName}", 
                    _radioService.ConnectedRadio!.Manufacturer, _radioService.ConnectedRadio.ModelName);
                Console.WriteLine("Press 'q' to quit, 's' for radio status, or type radio commands/messages...");
            }
            else
            {
                Console.WriteLine("Press 'q' to quit, or type messages to send to serial port...");
            }
            
            // Set up serial port event handlers
            if (_serialPort != null)
            {
                _serialPort.DataReceived += OnSerialDataReceived;
                
                // Set up resilient serial port event handlers if available
                if (_serialPort is ResilientSerialPort resilientPort)
                {
                    resilientPort.ConnectionLost += OnSerialConnectionLost;
                    resilientPort.ConnectionRestored += OnSerialConnectionRestored;
                }
            }
            
            // Set up graceful shutdown handler
            Console.CancelKeyPress += async (sender, e) =>
            {
                e.Cancel = true;
                Console.WriteLine("\nShutting down gracefully...");
                await ShutdownAsync(options);
                Environment.Exit(0);
            };
            
            // Main communication loop
            await RunMainLoopAsync();
            
            // Cleanup and save configuration
            await ShutdownAsync(options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in server application");
            throw;
        }
    }

    /// <summary>
    /// Runs the main application loop for console input and command processing
    /// </summary>
    /// <returns>Task representing the async operation</returns>
    private async Task RunMainLoopAsync()
    {
        string? input;
        while ((input = Console.ReadLine()) != "q")
        {
            if (!string.IsNullOrEmpty(input))
            {
                try
                {
                    // Handle special commands
                    if (input.ToLower() == "s" && _radioService.IsRadioConnected)
                    {
                        var status = await _radioService.GetRadioStatusAsync();
                        if (status != null)
                        {
                            Console.WriteLine(status);
                        }
                        continue;
                    }

                    // Try radio command first if radio is connected
                    if (_radioService.IsRadioConnected && await _radioService.TryProcessRadioCommandAsync(input))
                    {
                        // Radio command was handled
                        continue;
                    }

                    // Fall back to direct serial port communication
                    await SendToSerialPortWithRetryAsync(input);
                    Console.WriteLine($"Sent: {input}");
                }
                catch (OperationCanceledException)
                {
                    // Cancellation is expected during shutdown
                    break;
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogWarning(ex, "Operation error (will continue): {Message}", ex.Message);
                    // Don't break - continue processing other inputs
                }
                catch (IOException ex)
                {
                    _logger.LogWarning(ex, "I/O error (will attempt recovery): {Message}", ex.Message);
                    // Don't break immediately - the resilient wrapper will handle recovery
                    await Task.Delay(1000); // Brief pause before continuing
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error (will continue): {Message}", ex.Message);
                    // Continue rather than breaking to maintain server availability
                    await Task.Delay(1000); // Brief pause before continuing
                }
            }
        }
    }

    /// <summary>
    /// Shuts down the application gracefully
    /// </summary>
    /// <param name="options">Command line options for configuration saving</param>
    /// <returns>Task representing the async operation</returns>
    private async Task ShutdownAsync(CommandLineOptions options)
    {
        _logger.LogInformation("Shutting down server application...");
        
        try
        {
            _cancellationTokenSource?.Cancel();
            await _networkService.StopAsync();
            await _radioService.DisconnectRadioAsync();
            _serialPort?.Close();
            
            // Save configuration on normal shutdown
            if (_config != null)
            {
                _configurationService.UpdateConfigurationFromOptions(_config, options);
                await _configurationService.SaveConfigurationAsync(_config, "server_config.json");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during shutdown");
        }
        
        _logger.LogInformation("Server application shutdown complete");
    }

    #region Event Handlers

    /// <summary>
    /// Handles data received from network clients
    /// </summary>
    /// <param name="sender">Event sender</param>
    /// <param name="e">Event arguments</param>
    private async void OnNetworkDataReceived(object? sender, NetworkDataReceivedEventArgs e)
    {
        try
        {
            _logger.LogDebug("Received data from client {ClientId}: {Data}", e.ClientId, e.Data);
            
            // Handle special radio management commands
            if (await HandleRadioManagementCommandAsync(e.Data, e.NetworkStream))
            {
                // Command was handled, no further processing needed
                return;
            }
            
            // Send command to serial port for regular radio/serial commands
            await SendToSerialPortWithRetryAsync(e.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling network data from client {ClientId}", e.ClientId);
        }
    }

    /// <summary>
    /// Handles client connection events
    /// </summary>
    /// <param name="sender">Event sender</param>
    /// <param name="e">Event arguments</param>
    private void OnClientConnected(object? sender, ClientConnectedEventArgs e)
    {
        _logger.LogInformation("Client connected: {ClientId}", e.ClientId);
    }

    /// <summary>
    /// Handles client disconnection events
    /// </summary>
    /// <param name="sender">Event sender</param>
    /// <param name="e">Event arguments</param>
    private void OnClientDisconnected(object? sender, ClientDisconnectedEventArgs e)
    {
        _logger.LogInformation("Client disconnected: {ClientId}", e.ClientId);
    }

    /// <summary>
    /// Handles data received from the serial port
    /// </summary>
    /// <param name="sender">Event sender</param>
    /// <param name="e">Event arguments</param>
    private async void OnSerialDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            if (sender is ISerialPort port && port.IsOpen)
            {
                string data = port.ReadExisting();
                if (!string.IsNullOrEmpty(data))
                {
                    Console.Write($"Received: {data}");
                    
                    // Send data to all connected TCP clients
                    await _networkService.BroadcastToClientsAsync(data);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling serial data");
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Sends a command to the serial port with comprehensive error handling and retry logic
    /// </summary>
    /// <param name="command">Command string to send to the serial port</param>
    /// <returns>Task representing the async send operation</returns>
    private async Task SendToSerialPortWithRetryAsync(string command)
    {
        if (_serialPort?.IsOpen == true)
        {
            try
            {
                await RetryHelper.ExecuteWithRetryAsync(
                    async () => await Task.Run(() => _serialPort.WriteLine(command)),
                    RetryPolicy.Serial,
                    _logger,
                    "SerialPort.WriteLine",
                    ShouldRetrySerialOperation
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send command to serial port after retries: {Command}", command);
            }
        }
        else
        {
            _logger.LogWarning("Serial port is not open, cannot send command: {Command}", command);
        }
    }

    /// <summary>
    /// Determines if a serial operation should be retried
    /// </summary>
    private bool ShouldRetrySerialOperation(Exception ex)
    {
        return ex switch
        {
            InvalidOperationException when ex.Message.Contains("port is closed") => true,
            InvalidOperationException when ex.Message.Contains("port is not open") => true,
            IOException => true,
            TimeoutException => true,
            UnauthorizedAccessException => false, // Don't retry permission errors
            _ => false
        };
    }

    /// <summary>
    /// Handles serial connection lost events
    /// </summary>
    private void OnSerialConnectionLost(object? sender, ConnectionLostEventArgs e)
    {
        _logger.LogWarning("Serial connection lost: {PortName} - {Reason}", e.PortName, e.Reason);
        Console.WriteLine($"Warning: Serial connection lost ({e.Reason}). Attempting automatic recovery...");
    }

    /// <summary>
    /// Handles serial connection restored events
    /// </summary>
    private void OnSerialConnectionRestored(object? sender, ConnectionRestoredEventArgs e)
    {
        _logger.LogInformation("Serial connection restored: {PortName}", e.PortName);
        Console.WriteLine($"Serial connection restored: {e.PortName}");
    }

    /// <summary>
    /// Sends a command to the serial port with comprehensive error handling (legacy method - kept for compatibility)
    /// </summary>
    /// <param name="command">Command string to send to the serial port</param>
    /// <returns>Task representing the async send operation</returns>
    private async Task SendToSerialPortAsync(string command)
    {
        await SendToSerialPortWithRetryAsync(command);
    }

    /// <summary>
    /// Handles special radio management commands sent over TCP
    /// </summary>
    /// <param name="command">Command to handle</param>
    /// <param name="networkStream">Network stream to send response</param>
    /// <returns>True if command was handled, false if it should be sent to serial port</returns>
    private async Task<bool> HandleRadioManagementCommandAsync(string command, System.Net.Sockets.NetworkStream networkStream)
    {
        try
        {
            string[] parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return false;
            
            string baseCommand = parts[0].ToLower();
            
            switch (baseCommand)
            {
                case "list-radios":
                case "get-radios":
                    await SendRadioListResponseAsync(networkStream);
                    return true;
                    
                case "set-radio":
                    if (parts.Length >= 2)
                    {
                        string radioName = string.Join(" ", parts.Skip(1));
                        await HandleSetRadioCommandAsync(radioName, networkStream);
                        return true;
                    }
                    else
                    {
                        await SendTcpResponseAsync(networkStream, "ERROR: set-radio command requires radio name");
                        return true;
                    }
                    
                case "get-current-radio":
                case "current-radio":
                    await SendCurrentRadioResponseAsync(networkStream);
                    return true;
                    
                default:
                    return false; // Not a radio management command
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling radio management command: {Command}", command);
            await SendTcpResponseAsync(networkStream, $"ERROR: {ex.Message}");
            return true;
        }
    }

    /// <summary>
    /// Sends the available radio list as a response
    /// </summary>
    private async Task SendRadioListResponseAsync(System.Net.Sockets.NetworkStream networkStream)
    {
        try
        {
            var response = new System.Text.StringBuilder();
            response.AppendLine("RADIO_LIST_START");
            
            var radios = _radioService.GetAvailableRadios();
            foreach (var radio in radios.OrderBy(r => r.Key))
            {
                response.AppendLine($"{radio.Key}|0"); // Simplified for now
            }
            
            response.AppendLine("RADIO_LIST_END");
            
            await SendTcpResponseAsync(networkStream, response.ToString());
        }
        catch (Exception ex)
        {
            await SendTcpResponseAsync(networkStream, $"ERROR: Failed to get radio list - {ex.Message}");
        }
    }

    /// <summary>
    /// Handles the set-radio command to change the active radio
    /// </summary>
    private async Task HandleSetRadioCommandAsync(string radioName, System.Net.Sockets.NetworkStream networkStream)
    {
        try
        {
            bool success = await _radioService.ChangeRadioAsync(radioName, _serialPort!);
            if (success)
            {
                await SendTcpResponseAsync(networkStream, $"SUCCESS: Radio changed to {radioName}");
            }
            else
            {
                await SendTcpResponseAsync(networkStream, $"ERROR: Failed to change to radio '{radioName}'");
            }
        }
        catch (Exception ex)
        {
            await SendTcpResponseAsync(networkStream, $"ERROR: Failed to set radio - {ex.Message}");
        }
    }

    /// <summary>
    /// Sends the current radio information as a response
    /// </summary>
    private async Task SendCurrentRadioResponseAsync(System.Net.Sockets.NetworkStream networkStream)
    {
        try
        {
            if (_radioService.IsRadioConnected && _radioService.ConnectedRadio != null)
            {
                var radio = _radioService.ConnectedRadio;
                var response = $"CURRENT_RADIO:{radio.Manufacturer} {radio.ModelName}|{radio.SupportedFeatures.GetFeatureCount()}|{(radio.IsConnected ? "CONNECTED" : "DISCONNECTED")}";
                await SendTcpResponseAsync(networkStream, response);
            }
            else
            {
                await SendTcpResponseAsync(networkStream, "CURRENT_RADIO:NONE");
            }
        }
        catch (Exception ex)
        {
            await SendTcpResponseAsync(networkStream, $"ERROR: Failed to get current radio - {ex.Message}");
        }
    }

    /// <summary>
    /// Sends a response to the TCP client
    /// </summary>
    private async Task SendTcpResponseAsync(System.Net.Sockets.NetworkStream networkStream, string response)
    {
        try
        {
            byte[] responseBytes = System.Text.Encoding.UTF8.GetBytes(response + "\n");
            await networkStream.WriteAsync(responseBytes);
            await networkStream.FlushAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending TCP response");
        }
    }

    #endregion

    #region Command Line and Configuration

    /// <summary>
    /// Parses command line arguments into a structured options object
    /// </summary>
    /// <param name="args">Command line arguments array</param>
    /// <returns>Parsed command line options</returns>
    private CommandLineOptions ParseArguments(string[] args)
    {
        var options = new CommandLineOptions();
        
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "-p":
                case "--port":
                    if (i + 1 < args.Length)
                    {
                        var portName = args[++i];
                        if (!string.IsNullOrWhiteSpace(portName))
                        {
                            options.PortName = portName.Trim();
                        }
                        else
                        {
                            throw new ArgumentException("Port name cannot be empty.");
                        }
                    }
                    else
                    {
                        throw new ArgumentException("Missing port name argument.");
                    }
                    break;
                    
                case "-b":
                case "--baud":
                    if (i + 1 < args.Length)
                    {
                        if (int.TryParse(args[++i], out int baud))
                        {
                            if (IsValidBaudRate(baud))
                            {
                                options.BaudRate = baud;
                            }
                            else
                            {
                                throw new ArgumentException($"Unsupported baud rate '{baud}'.");
                            }
                        }
                        else
                        {
                            throw new ArgumentException($"Invalid baud rate '{args[i]}'. Must be a valid integer.");
                        }
                    }
                    else
                    {
                        throw new ArgumentException("Missing baud rate argument.");
                    }
                    break;
                    
                case "-t":
                case "--tcp-port":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out int tcpPort))
                    {
                        if (tcpPort > 0 && tcpPort <= 65535)
                        {
                            options.TcpPort = tcpPort;
                        }
                        else
                        {
                            throw new ArgumentException($"TCP port '{tcpPort}' is out of valid range (1-65535).");
                        }
                    }
                    else
                    {
                        throw new ArgumentException("Invalid or missing TCP port number.");
                    }
                    break;
                    
                case "-l":
                case "--list":
                    options.ListPorts = true;
                    break;
                    
                case "-h":
                case "--help":
                    options.ShowHelp = true;
                    break;
                    
                case "-r":
                case "--radio":
                    if (i + 1 < args.Length)
                    {
                        var radioModel = args[++i];
                        if (!string.IsNullOrWhiteSpace(radioModel))
                        {
                            options.RadioModel = radioModel.Trim();
                        }
                        else
                        {
                            throw new ArgumentException("Radio model cannot be empty.");
                        }
                    }
                    else
                    {
                        throw new ArgumentException("Missing radio model argument.");
                    }
                    break;
                    
                case "--auto-detect":
                    options.AutoDetectRadio = true;
                    break;
                    
                case "--list-radios":
                    options.ListRadios = true;
                    break;
                    
                case "--radio-info":
                    if (i + 1 < args.Length)
                    {
                        var radioInfo = args[++i];
                        if (!string.IsNullOrWhiteSpace(radioInfo))
                        {
                            options.ShowRadioInfo = radioInfo.Trim();
                        }
                        else
                        {
                            throw new ArgumentException("Radio info parameter cannot be empty.");
                        }
                    }
                    else
                    {
                        throw new ArgumentException("Missing radio info argument.");
                    }
                    break;
                    
                default:
                    if (args[i].StartsWith("-"))
                    {
                        throw new ArgumentException($"Unknown argument '{args[i]}'.");
                    }
                    break;
            }
        }
        
        return options;
    }

    private void ShowHelp()
    {
        Console.WriteLine("Usage: Server [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -p, --port <name>     Serial port name (e.g., COM1, /dev/ttyUSB0)");
        Console.WriteLine("  -b, --baud <rate>     Baud rate (default: 9600)");
        Console.WriteLine("                        Supported rates: 9600, 14400, 19200, 28800, 38400, 57600, 115200, 128000, 256000");
        Console.WriteLine("  -t, --tcp-port <port> TCP server port (default: 8080)");
        Console.WriteLine("  -r, --radio <model>   Radio model (e.g., \"Kenwood TS-2000\")");
        Console.WriteLine("  --auto-detect         Auto-detect radio type");
        Console.WriteLine("  -l, --list            List available serial ports");
        Console.WriteLine("  --list-radios         List available radio models");
        Console.WriteLine("  --radio-info <model>  Show detailed information about a radio model");
        Console.WriteLine("  -h, --help            Show this help message");
        Console.WriteLine();
        Console.WriteLine("The server provides both console interface and TCP server for remote clients.");
        Console.WriteLine("With radio support, you can send CAT commands and get radio status information.");
    }

    private void ListAvailablePorts()
    {
        Console.WriteLine("Available Serial Ports:");
        Console.WriteLine("======================");
        
        try
        {
            string[] ports = SerialPort.GetPortNames();
            
            if (ports.Length == 0)
            {
                Console.WriteLine("No serial ports found.");
                
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Console.WriteLine();
                    Console.WriteLine("Linux troubleshooting:");
                    Console.WriteLine("- Check if devices are connected: ls /dev/tty*");
                    Console.WriteLine("- Verify permissions: groups $USER");
                    Console.WriteLine("- Add user to dialout group: sudo usermod -a -G dialout $USER");
                    Console.WriteLine("- Log out and back in for group changes to take effect");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Console.WriteLine();
                    Console.WriteLine("macOS troubleshooting:");
                    Console.WriteLine("- Check devices manually: ls /dev/cu.*");
                    Console.WriteLine("- Verify device drivers are installed");
                }
            }
            else
            {
                for (int i = 0; i < ports.Length; i++)
                {
                    Console.WriteLine($"{i + 1}. {ports[i]}");
                }
                
                Console.WriteLine();
                Console.WriteLine($"Found {ports.Length} port(s).");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing ports");
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Console.WriteLine();
                Console.WriteLine("This might be a permissions issue. Try:");
                Console.WriteLine("sudo usermod -a -G dialout $USER");
                Console.WriteLine("Then log out and back in.");
            }
        }
    }

    private void ListAvailableRadios()
    {
        Console.WriteLine("Available Radio Models:");
        Console.WriteLine("======================");
        
        var radios = _radioService.GetAvailableRadios();
        
        if (radios.Count == 0)
        {
            Console.WriteLine("No radio models found.");
        }
        else
        {
            foreach (var radio in radios.OrderBy(r => r.Key))
            {
                Console.WriteLine($"  {radio.Key}");
            }
            
            Console.WriteLine();
            Console.WriteLine($"Found {radios.Count} radio model(s).");
            Console.WriteLine("Use --radio \"Manufacturer Model\" to specify a radio.");
            Console.WriteLine("Use --auto-detect to automatically detect the radio type.");
        }
    }

    private void ShowRadioInfo(string radioName)
    {
        var info = _radioService.GetRadioInfo(radioName);
        if (info != null)
        {
            Console.WriteLine(info);
        }
        else
        {
            Console.WriteLine($"Radio '{radioName}' not found.");
            Console.WriteLine();
            Console.WriteLine("Available radios:");
            ListAvailableRadios();
        }
    }

    private string ValidateOrPromptPortName(string? portName)
    {
        if (!string.IsNullOrEmpty(portName))
        {
            if (IsValidPortName(portName))
            {
                return portName;
            }
            else
            {
                Console.WriteLine($"Warning: Port '{portName}' may not exist or be accessible.");
                Console.WriteLine("Continuing anyway. Use --list to see available ports.");
                return portName;
            }
        }
        
        // No port specified, try to help user select one
        Console.WriteLine("No port specified. Scanning for available ports...");
        
        try
        {
            string[] ports = SerialPort.GetPortNames();
            
            if (ports.Length == 0)
            {
                Console.WriteLine("No ports found automatically.");
                return PromptForPortName();
            }
            else if (ports.Length == 1)
            {
                Console.WriteLine($"Found one port: {ports[0]}");
                Console.Write("Use this port? (y/N): ");
                string? response = Console.ReadLine();
                
                if (response?.ToLower() == "y" || response?.ToLower() == "yes")
                {
                    return ports[0];
                }
                else
                {
                    return PromptForPortName();
                }
            }
            else
            {
                Console.WriteLine("Multiple ports found:");
                for (int i = 0; i < ports.Length; i++)
                {
                    Console.WriteLine($"{i + 1}. {ports[i]}");
                }
                
                Console.Write($"Select port number (1-{ports.Length}) or enter custom name: ");
                string? input = Console.ReadLine();
                
                if (int.TryParse(input, out int selection) && selection >= 1 && selection <= ports.Length)
                {
                    return ports[selection - 1];
                }
                else if (!string.IsNullOrEmpty(input))
                {
                    return input;
                }
                else
                {
                    return PromptForPortName();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning ports");
            return PromptForPortName();
        }
    }

    private string PromptForPortName()
    {
        string example = GetPlatformPortExample();
        
        while (true)
        {
            Console.Write($"Enter serial port name (e.g., {example}): ");
            string? input = Console.ReadLine();
            
            if (!string.IsNullOrEmpty(input))
            {
                return input;
            }
            
            Console.WriteLine("Port name cannot be empty. Please try again.");
        }
    }

    private string GetPlatformPortExample()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "COM1";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "/dev/ttyUSB0";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "/dev/cu.usbserial-1410";
        else
            return "COM1";
    }

    private bool IsValidPortName(string portName)
    {
        try
        {
            string[] availablePorts = SerialPort.GetPortNames();
            return availablePorts.Contains(portName, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            // If we can't check, assume it might be valid
            return true;
        }
    }

    /// <summary>
    /// Creates the appropriate serial port implementation based on port name with resilient wrapper
    /// </summary>
    /// <param name="portName">Port name to create</param>
    /// <param name="baudRate">Baud rate for communication</param>
    /// <returns>Configured and opened ISerialPort implementation with resilience features</returns>
    private ISerialPort CreateResilientSerialPort(string portName, int baudRate)
    {
        try
        {
            // Use factory to create resilient implementation
            var serialPort = SerialPortFactory.CreateSerialPort(portName, baudRate, 
                useFakeForTesting: false, useResilientWrapper: true, logger: _logger);
            
            // Open the port
            if (!serialPort.IsOpen)
            {
                serialPort.Open();
            }
            
            return serialPort;
        }
        catch (UnauthorizedAccessException)
        {
            string guidance = GetPermissionGuidance();
            throw new InvalidOperationException(
                $"Access denied to port '{portName}'. {guidance}");
        }
        catch (ArgumentException)
        {
            throw new ArgumentException(
                $"Invalid port name '{portName}'. Use --list to see available ports.");
        }
        catch (FileNotFoundException)
        {
            throw new FileNotFoundException(
                $"Port '{portName}' not found. Use --list to see available ports.");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to open port '{portName}': {ex.Message}");
        }
    }

    /// <summary>
    /// Creates the appropriate serial port implementation based on port name (legacy method)
    /// </summary>
    /// <param name="portName">Port name to create</param>
    /// <param name="baudRate">Baud rate for communication</param>
    /// <returns>Configured and opened ISerialPort implementation</returns>
    private ISerialPort CreateSerialPort(string portName, int baudRate)
    {
        return CreateResilientSerialPort(portName, baudRate);
    }

    private string GetPermissionGuidance()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "On Linux, add your user to the 'dialout' group: sudo usermod -a -G dialout $USER (then logout/login)";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "On macOS, check that the device driver is installed and the device is not in use by another application";
        }
        else
        {
            return "Check that the port is not in use by another application";
        }
    }

    private bool IsValidBaudRate(int baudRate)
    {
        return SupportedBaudRates.Contains(baudRate);
    }

    #endregion
}