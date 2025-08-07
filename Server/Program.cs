using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Concurrent;
using SharpCAT2.Radio;
using SharpCAT2.Radio.Serial;

namespace SharpCAT2.Server;

/// <summary>
/// Main program class for the SharpCAT2 Server application.
/// Provides cross-platform serial port communication with TCP server capabilities
/// and comprehensive amateur radio control features.
/// </summary>
class Program
{
    #region Private Fields

    /// <summary>
    /// Thread-safe collection of connected TCP clients and their network streams
    /// </summary>
    private static readonly ConcurrentDictionary<string, NetworkStream> _tcpClients = new();
    
    /// <summary>
    /// The wrapped serial port for radio communication
    /// </summary>
    private static ISerialPort? _wrappedSerialPort;
    
    /// <summary>
    /// The connected radio instance providing CAT control
    /// </summary>
    private static IRadio? _connectedRadio;
    
    /// <summary>
    /// TCP listener for accepting remote client connections
    /// </summary>
    private static TcpListener? _tcpListener;
    
    /// <summary>
    /// Cancellation token source for graceful shutdown
    /// </summary>
    private static CancellationTokenSource? _cancellationTokenSource;

    /// <summary>
    /// Configuration for the server application
    /// </summary>
    private static ServerConfig? _config;

    /// <summary>
    /// Array of supported baud rates for serial communication.
    /// These rates are validated to ensure compatibility with common radio interfaces.
    /// </summary>
    private static readonly int[] SupportedBaudRates = { 9600, 14400, 19200, 28800, 38400, 57600, 115200, 128000, 256000 };

    #endregion

    /// <summary>
    /// Main entry point for the SharpCAT2 Server application.
    /// Handles command line parsing, serial port setup, radio initialization, 
    /// TCP server startup, and the main communication loop.
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <returns>Task representing the async operation</returns>
    private static async Task Main(string[] args)

    {
        Console.WriteLine("SharpCAT2 Server - Cross-Platform Serial Port Communication");
        Console.WriteLine("============================================================");
        
        try
        {
            // Display platform information
            DisplayPlatformInfo();
            
            // Load configuration
            const string configPath = "server_config.json";
            _config = await ServerConfig.LoadAsync(configPath);
            
            // Parse command line arguments
            var options = ParseArguments(args);
            
            // Apply configuration to options if not overridden by command line
            _config.ApplyToCommandLineOptions(options);
            
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
            
            // Open and configure serial port
            var serialPort = OpenSerialPort(portName, options.BaudRate);
            _wrappedSerialPort = SerialPortFactory.CreateRealSerialPort(serialPort);
            
            Console.WriteLine($"Successfully opened serial port: {portName}");
            Console.WriteLine($"Baud rate: {options.BaudRate}");

            // Initialize radio if specified
            await InitializeRadioAsync(options, _wrappedSerialPort);
            
            // Start TCP server
            _cancellationTokenSource = new CancellationTokenSource();
            await StartTcpServerAsync(options.TcpPort, _cancellationTokenSource.Token);
            
            Console.WriteLine($"TCP server listening on port {options.TcpPort}");
            
            if (_connectedRadio != null)
            {
                Console.WriteLine($"Connected to radio: {_connectedRadio.Manufacturer} {_connectedRadio.ModelName}");
                Console.WriteLine("Press 'q' to quit, 's' for radio status, or type radio commands/messages...");
            }
            else
            {
                Console.WriteLine("Press 'q' to quit, or type messages to send to serial port...");
            }
            
            // Set up serial port data received handler
            if (_wrappedSerialPort != null)
            {
                _wrappedSerialPort.DataReceived += OnSerialDataReceived;
            }
            
            // Set up graceful shutdown handler
            Console.CancelKeyPress += async (sender, e) =>
            {
                e.Cancel = true;
                Console.WriteLine("\nShutting down gracefully...");
                await SaveConfigurationAsync(options);
                Environment.Exit(0);
            };
            
            // Main communication loop
            string? input;
            while ((input = Console.ReadLine()) != "q")
            {
                if (!string.IsNullOrEmpty(input))
                {
                    try
                    {
                        // Handle special commands
                        if (input.ToLower() == "s" && _connectedRadio != null)
                        {
                            await ShowRadioStatusAsync();
                            continue;
                        }

                        // Try radio command first if radio is connected
                        if (_connectedRadio != null && await TryRadioCommandAsync(input))
                        {
                            // Radio command was handled
                            continue;
                        }

                        // Fall back to direct serial port communication
                        await SendToSerialPortAsync(input);
                        Console.WriteLine($"Sent: {input}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error sending data: {ex.Message}");
                        break;
                    }
                }
            }
            
            // Cleanup and save configuration
            _cancellationTokenSource.Cancel();
            _tcpListener?.Stop();
            _connectedRadio?.Dispose();
            _wrappedSerialPort?.Close();
            
            // Save configuration on normal shutdown
            await SaveConfigurationAsync(options);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    #region Radio Initialization

    /// <summary>
    /// Initializes radio communication based on command line options.
    /// Supports both automatic radio detection and manual radio model specification.
    /// If radio initialization fails, the application continues with basic serial communication.
    /// </summary>
    /// <param name="options">Parsed command line options containing radio settings</param>
    /// <param name="serialPort">The serial port to use for radio communication</param>
    /// <returns>Task representing the async initialization operation</returns>
    private static async Task InitializeRadioAsync(CommandLineOptions options, ISerialPort serialPort)
    {
        try
        {
            if (options.AutoDetectRadio)
            {
                Console.WriteLine("Auto-detecting radio...");
                _connectedRadio = await RadioFactory.AutoDetectRadioAsync(serialPort);
                
                if (_connectedRadio == null)
                {
                    Console.WriteLine("No radio detected. Continuing with basic serial communication.");
                    return;
                }
            }
            else if (!string.IsNullOrWhiteSpace(options.RadioModel))
            {
                Console.WriteLine($"Connecting to radio: {options.RadioModel}");
                _connectedRadio = RadioFactory.CreateRadio(options.RadioModel);
                
                if (_connectedRadio == null)
                {
                    Console.WriteLine($"Unknown radio model: {options.RadioModel}");
                    Console.WriteLine("Use --list-radios to see available models.");
                    return;
                }
            }
            else
            {
                // No radio specified, continue with basic serial communication
                return;
            }

            // Connect the radio to the serial port
            bool connected = await _connectedRadio.ConnectAsync(serialPort);
            if (!connected)
            {
                Console.WriteLine("Failed to connect to radio. Continuing with basic serial communication.");
                _connectedRadio.Dispose();
                _connectedRadio = null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing radio: {ex.Message}");
            _connectedRadio?.Dispose();
            _connectedRadio = null;
        }
    }

    #endregion

    #region Radio Command Processing

    /// <summary>
    /// Attempts to process user input as a radio command.
    /// Supports both CAT commands (ending with semicolon) and convenient shortcuts
    /// like 'freq' and 'mode' for common operations.
    /// </summary>
    /// <param name="input">User input to process as a radio command</param>
    /// <returns>True if the input was processed as a radio command, false otherwise</returns>
    private static async Task<bool> TryRadioCommandAsync(string input)
    {
        if (_connectedRadio == null)
            return false;

        try
        {
            // Check if it's a well-formed radio command (ends with semicolon)
            if (input.EndsWith(";"))
            {
                var command = new RadioCommand(input, "User command");
                var response = await _connectedRadio.SendCommandAsync(command);
                
                if (response != null)
                {
                    Console.WriteLine($"Radio response: {response}");
                    return true;
                }
            }

            // Try common command shortcuts
            switch (input.ToLower().Trim())
            {
                case "freq":
                case "frequency":
                    var status = await _connectedRadio.GetStatusAsync();
                    Console.WriteLine($"Current frequency: {status.Frequency:N0} Hz");
                    return true;

                case "mode":
                    var modeStatus = await _connectedRadio.GetStatusAsync();
                    Console.WriteLine($"Current mode: {modeStatus.Mode}");
                    return true;

                default:
                    return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing radio command: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Displays comprehensive status information for the connected radio.
    /// Shows current frequency, mode, VFO settings, transmission status,
    /// supported features, and other relevant radio state information.
    /// </summary>
    /// <returns>Task representing the async status retrieval operation</returns>
    private static async Task ShowRadioStatusAsync()
    {
        if (_connectedRadio == null)
        {
            Console.WriteLine("No radio connected.");
            return;
        }

        try
        {
            var status = await _connectedRadio.GetStatusAsync();
            Console.WriteLine("Radio Status:");
            Console.WriteLine($"  Model: {_connectedRadio.Manufacturer} {_connectedRadio.ModelName}");
            Console.WriteLine($"  Frequency: {status.Frequency:N0} Hz");
            Console.WriteLine($"  Mode: {status.Mode}");
            Console.WriteLine($"  VFO: {status.CurrentVfo}");
            Console.WriteLine($"  Transmitting: {status.IsTransmitting}");
            Console.WriteLine($"  Power: {status.IsPoweredOn}");
            Console.WriteLine($"  Timestamp: {status.Timestamp:HH:mm:ss}");
            
            // Display supported features
            Console.WriteLine();
            Console.WriteLine("Supported Features:");
            Console.WriteLine($"  Feature Count: {_connectedRadio.SupportedFeatures.GetFeatureCount()}");
            Console.WriteLine($"  Features: {_connectedRadio.SupportedFeatures.GetDescription()}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting radio status: {ex.Message}");
        }
    }

    private static void ListAvailableRadios()
    {
        Console.WriteLine("Available Radio Models:");
        Console.WriteLine("======================");
        
        var radios = RadioFactory.GetAvailableRadios();
        
        if (radios.Count == 0)
        {
            Console.WriteLine("No radio models found.");
        }
        else
        {
            foreach (var radio in radios.OrderBy(r => r.Key))
            {
                // Create instance to get supported features
                var instance = RadioFactory.CreateRadio(radio.Key);
                if (instance != null)
                {
                    var featureCount = instance.SupportedFeatures.GetFeatureCount();
                    Console.WriteLine($"  {radio.Key} ({featureCount} features)");
                    instance.Dispose();
                }
                else
                {
                    Console.WriteLine($"  {radio.Key}");
                }
            }
            
            Console.WriteLine();
            Console.WriteLine($"Found {radios.Count} radio model(s).");
            Console.WriteLine("Use --radio \"Manufacturer Model\" to specify a radio.");
            Console.WriteLine("Use --auto-detect to automatically detect the radio type.");
        }
    }

    private static void ShowRadioInfo(string radioName)
    {
        Console.WriteLine($"Radio Information: {radioName}");
        Console.WriteLine("====================================");

        var radio = RadioFactory.CreateRadio(radioName);
        if (radio == null)
        {
            Console.WriteLine($"Radio '{radioName}' not found.");
            Console.WriteLine();
            Console.WriteLine("Available radios:");
            ListAvailableRadios();
            return;
        }

        try
        {
            Console.WriteLine($"Manufacturer: {radio.Manufacturer}");
            Console.WriteLine($"Model: {radio.ModelName}");
            Console.WriteLine($"Feature Count: {radio.SupportedFeatures.GetFeatureCount()}");
            Console.WriteLine();
            
            Console.WriteLine("Supported Features:");
            Console.WriteLine("==================");
            
            // Display individual features
            var features = radio.SupportedFeatures;
            
            if (features == SupportedFeatures.FullFeatureSet)
            {
                Console.WriteLine("  All features supported (Full Feature Set)");
            }
            else
            {
                var featureNames = Enum.GetValues<SupportedFeatures>()
                    .Where(f => f != SupportedFeatures.None && 
                               f != SupportedFeatures.FullFeatureSet && 
                               f != SupportedFeatures.BasicOperation &&
                               f != SupportedFeatures.HFOperation &&
                               f != SupportedFeatures.VHFUHFOperation &&
                               f != SupportedFeatures.AdvancedOperation &&
                               features.HasFeature(f))
                    .ToList();

                if (featureNames.Count == 0)
                {
                    Console.WriteLine("  Basic operation only");
                }
                else
                {
                    foreach (var feature in featureNames.OrderBy(f => f.ToString()))
                    {
                        Console.WriteLine($"  ✓ {feature}");
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine("Feature Categories:");
            Console.WriteLine("==================");
            
            // Check feature categories
            if (features.HasFeature(SupportedFeatures.BasicOperation))
                Console.WriteLine("  ✓ Basic Operation (Frequency, Mode, Status)");
            if (features.HasFeature(SupportedFeatures.HFOperation))
                Console.WriteLine("  ✓ HF Operation (VFO, Split, RIT/XIT, S-Meter)");
            if (features.HasFeature(SupportedFeatures.VHFUHFOperation))
                Console.WriteLine("  ✓ VHF/UHF Operation (Squelch, CTCSS, Repeater)");
            if (features.HasFeature(SupportedFeatures.AdvancedOperation))
                Console.WriteLine("  ✓ Advanced Operation (DSP, Memory, CW Keyer)");
            
            if (features == SupportedFeatures.FullFeatureSet)
                Console.WriteLine("  ✓ Full Feature Set (All features supported)");
        }
        finally
        {
            radio.Dispose();
        }
    }

    #endregion

    #region TCP Server Management

    /// <summary>
    /// Starts the TCP server for accepting remote client connections.
    /// The server runs on a background task and accepts multiple concurrent clients.
    /// Each client connection is handled independently with proper error handling.
    /// </summary>
    /// <param name="port">TCP port number to listen on</param>
    /// <param name="cancellationToken">Token for graceful shutdown</param>
    /// <returns>Task representing the async server startup operation</returns>
    private static async Task StartTcpServerAsync(int port, CancellationToken cancellationToken)
    {
        _tcpListener = new TcpListener(IPAddress.Any, port);
        _tcpListener.Start();
        
        // Accept TCP clients in the background
        _ = Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var tcpClient = await _tcpListener.AcceptTcpClientAsync();
                    var clientId = $"{tcpClient.Client.RemoteEndPoint}";
                    var networkStream = tcpClient.GetStream();
                    
                    _tcpClients[clientId] = networkStream;
                    Console.WriteLine($"TCP client connected: {clientId}");
                    
                    // Handle client communication in background
                    _ = Task.Run(async () => await HandleTcpClientAsync(clientId, tcpClient, networkStream, cancellationToken));
                }
                catch (ObjectDisposedException)
                {
                    // TCP listener was stopped
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error accepting TCP client: {ex.Message}");
                }
            }
        }, cancellationToken);
        
        // Wait a moment to ensure the listener is ready
        await Task.Delay(100, cancellationToken);
    }
    
    private static async Task HandleTcpClientAsync(string clientId, TcpClient tcpClient, NetworkStream networkStream, CancellationToken cancellationToken)
    {
        var buffer = new byte[1024];
        
        try
        {
            while (!cancellationToken.IsCancellationRequested && tcpClient.Connected)
            {
                int bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                
                if (bytesRead > 0)
                {
                    string command = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                    Console.WriteLine($"TCP client {clientId} sent: {command}");
                    
                    // Send command to serial port
                    await SendToSerialPortAsync(command);
                }
                else
                {
                    break; // Client disconnected
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling TCP client {clientId}: {ex.Message}");
        }
        finally
        {
            _tcpClients.TryRemove(clientId, out _);
            tcpClient.Close();
            Console.WriteLine($"TCP client disconnected: {clientId}");
        }
    }
    
    private static async Task SendToSerialPortAsync(string command)
    {
        if (_wrappedSerialPort?.IsOpen == true)
        {
            try
            {
                await Task.Run(() => _wrappedSerialPort.WriteLine(command));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending to serial port: {ex.Message}");
            }
        }
    }
    
    private static async void OnSerialDataReceived(object sender, SerialDataReceivedEventArgs e)
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
                    await SendToTcpClientsAsync(data);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading serial data: {ex.Message}");
        }
    }
    
    private static async Task SendToTcpClientsAsync(string data)
    {
        var clientsToRemove = new List<string>();
        var dataBytes = Encoding.UTF8.GetBytes(data);
        
        foreach (var kvp in _tcpClients)
        {
            try
            {
                await kvp.Value.WriteAsync(dataBytes, 0, dataBytes.Length);
                await kvp.Value.FlushAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending to TCP client {kvp.Key}: {ex.Message}");
                clientsToRemove.Add(kvp.Key);
            }
        }
        
        // Remove disconnected clients
        foreach (string clientId in clientsToRemove)
        {
            _tcpClients.TryRemove(clientId, out _);
        }
    }

    #endregion

    #region Platform and Configuration

    /// <summary>
    /// Displays platform-specific information and guidance for serial port usage.
    /// Provides appropriate port naming conventions and setup instructions for 
    /// Windows, Linux, and macOS platforms.
    /// </summary>
    private static void DisplayPlatformInfo()
    {
        string platform = "Unknown";
        string guidance = "";
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            platform = "Windows";
            guidance = "Typical port names: COM1, COM2, COM3, etc.";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            platform = "Linux";
            guidance = "Typical port names: /dev/ttyUSB0, /dev/ttyACM0, /dev/ttyS0, etc.\n" +
                      "Note: You may need to add your user to the 'dialout' group for permissions.";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            platform = "macOS";
            guidance = "Typical port names: /dev/cu.usbserial-*, /dev/cu.usbmodem*, /dev/cu.Bluetooth-*, etc.";
        }
        
        Console.WriteLine($"Platform: {platform}");
        Console.WriteLine($"{guidance}");
        Console.WriteLine();
    }
    
    /// <summary>
    /// Parses command line arguments into a structured options object.
    /// Handles all supported command line switches including port settings,
    /// radio configuration, and information display options.
    /// </summary>
    /// <param name="args">Command line arguments array</param>
    /// <returns>Parsed command line options</returns>
    private static CommandLineOptions ParseArguments(string[] args)
    {
        var options = new CommandLineOptions();
        
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "-p":
                case "--port":
                    if (i + 1 < args.Length)
                        options.PortName = args[++i];
                    break;
                case "-b":
                case "--baud":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out int baud))
                    {
                        if (IsValidBaudRate(baud))
                        {
                            options.BaudRate = baud;
                        }
                        else
                        {
                            Console.WriteLine($"Error: Unsupported baud rate '{baud}'.");
                            ShowSupportedBaudRates();
                            Environment.Exit(1);
                        }
                    }
                    break;
                case "-t":
                case "--tcp-port":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out int tcpPort))
                        options.TcpPort = tcpPort;
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
                        options.RadioModel = args[++i];
                    break;
                case "--auto-detect":
                    options.AutoDetectRadio = true;
                    break;
                case "--list-radios":
                    options.ListRadios = true;
                    break;
                case "--radio-info":
                    if (i + 1 < args.Length)
                        options.ShowRadioInfo = args[++i];
                    break;
            }
        }
        
        return options;
    }
    
    private static void ShowHelp()
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
        Console.WriteLine("Examples:");
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.WriteLine("  Server --port COM1 --baud 115200");
            Console.WriteLine("  Server -p COM3 --tcp-port 9090");
            Console.WriteLine("  Server --port COM1 --radio \"Kenwood TS-2000\"");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Console.WriteLine("  Server --port /dev/ttyUSB0 --baud 115200");
            Console.WriteLine("  Server -p /dev/ttyACM0 --tcp-port 9090");
            Console.WriteLine("  Server --port /dev/ttyUSB0 --auto-detect");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Console.WriteLine("  Server --port /dev/cu.usbserial-1410 --baud 115200");
            Console.WriteLine("  Server -p /dev/cu.usbmodem1411 --tcp-port 9090");
            Console.WriteLine("  Server --port /dev/cu.usbserial-1410 --radio \"Elecraft K3\"");
        }
        
        Console.WriteLine();
        Console.WriteLine("  Server --list         # List all available ports");
        Console.WriteLine("  Server --list-radios  # List all available radio models");
        Console.WriteLine("  Server --radio-info \"Elecraft K3\"  # Show detailed radio information");
        Console.WriteLine();
        Console.WriteLine("The server provides both console interface and TCP server for remote clients.");
        Console.WriteLine("With radio support, you can send CAT commands and get radio status information.");
    }
    
    private static void ListAvailablePorts()
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
            Console.WriteLine($"Error listing ports: {ex.Message}");
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Console.WriteLine();
                Console.WriteLine("This might be a permissions issue. Try:");
                Console.WriteLine("sudo usermod -a -G dialout $USER");
                Console.WriteLine("Then log out and back in.");
            }
        }
    }
    
    private static string ValidateOrPromptPortName(string? portName)
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
                
                Console.Write("Select port number (1-{0}) or enter custom name: ", ports.Length);
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
            Console.WriteLine($"Error scanning ports: {ex.Message}");
            return PromptForPortName();
        }
    }
    
    private static string PromptForPortName()
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
    
    private static string GetPlatformPortExample()
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
    
    private static bool IsValidPortName(string portName)
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
    
    private static SerialPort OpenSerialPort(string portName, int baudRate)
    {
        try
        {
            var serialPort = new SerialPort(portName, baudRate)
            {
                Parity = Parity.None,
                DataBits = 8,
                StopBits = StopBits.One,
                Handshake = Handshake.None,
                ReadTimeout = 500,
                WriteTimeout = 500
            };
            
            serialPort.Open();
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
    
    private static string GetPermissionGuidance()
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
    
    private static bool IsValidBaudRate(int baudRate)
    {
        return SupportedBaudRates.Contains(baudRate);
    }
    
    private static void ShowSupportedBaudRates()
    {
        Console.WriteLine("Supported baud rates:");
        Console.WriteLine(string.Join(", ", SupportedBaudRates));
    }

    #endregion

    #region Configuration Management

    /// <summary>
    /// Saves the current configuration to file
    /// </summary>
    /// <param name="options">Current command line options to save</param>
    private static async Task SaveConfigurationAsync(CommandLineOptions options)
    {
        if (_config != null)
        {
            // Update configuration with current settings
            _config.UpdateFromCommandLineOptions(options);
            
            const string configPath = "server_config.json";
            await _config.SaveAsync(configPath);
        }
    }

    #endregion
}

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
