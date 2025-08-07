using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Concurrent;

namespace SharpCAT2.Server;

class Program
{

    private static readonly ConcurrentDictionary<string, NetworkStream> _tcpClients = new();
    private static SerialPort? _serialPort;
    private static TcpListener? _tcpListener;
    private static CancellationTokenSource? _cancellationTokenSource;

    // Supported baud rates for serial communication
    private static readonly int[] SupportedBaudRates = { 9600, 14400, 19200, 28800, 38400, 57600, 115200, 128000, 256000 };
    
    private static async Task Main(string[] args)

    {
        Console.WriteLine("SharpCAT2 Server - Cross-Platform Serial Port Communication");
        Console.WriteLine("============================================================");
        
        try
        {
            // Display platform information
            DisplayPlatformInfo();
            
            // Parse command line arguments
            var options = ParseArguments(args);
            
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
            
            // Validate or prompt for port name
            string portName = ValidateOrPromptPortName(options.PortName);
            
            // Open and configure serial port
            _serialPort = OpenSerialPort(portName, options.BaudRate);
            
            Console.WriteLine($"Successfully opened serial port: {portName}");
            Console.WriteLine($"Baud rate: {options.BaudRate}");
            
            // Start TCP server
            _cancellationTokenSource = new CancellationTokenSource();
            await StartTcpServerAsync(options.TcpPort, _cancellationTokenSource.Token);
            
            Console.WriteLine($"TCP server listening on port {options.TcpPort}");
            Console.WriteLine("Press 'q' to quit, or type messages to send to serial port...");
            
            // Set up serial port data received handler
            _serialPort.DataReceived += OnSerialDataReceived;
            
            // Main communication loop
            string? input;
            while ((input = Console.ReadLine()) != "q")
            {
                if (!string.IsNullOrEmpty(input))
                {
                    try
                    {
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
            
            // Cleanup
            _cancellationTokenSource.Cancel();
            _tcpListener?.Stop();
            _serialPort?.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex.Message}");
            Environment.Exit(1);
        }
    }
    
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
        if (_serialPort?.IsOpen == true)
        {
            try
            {
                await Task.Run(() => _serialPort.WriteLine(command));
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
            if (sender is SerialPort port && port.IsOpen)
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

        Console.WriteLine("  -t, --tcp-port <port> TCP server port (default: 8080)");

        Console.WriteLine("                        Supported rates: 9600, 14400, 19200, 28800, 38400, 57600, 115200, 128000, 256000");

        Console.WriteLine("  -l, --list            List available serial ports");
        Console.WriteLine("  -h, --help            Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.WriteLine("  Server --port COM1 --baud 115200");
            Console.WriteLine("  Server -p COM3 --tcp-port 9090");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Console.WriteLine("  Server --port /dev/ttyUSB0 --baud 115200");
            Console.WriteLine("  Server -p /dev/ttyACM0 --tcp-port 9090");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Console.WriteLine("  Server --port /dev/cu.usbserial-1410 --baud 115200");
            Console.WriteLine("  Server -p /dev/cu.usbmodem1411 --tcp-port 9090");
        }
        
        Console.WriteLine();
        Console.WriteLine("  Server --list         # List all available ports");
        Console.WriteLine();
        Console.WriteLine("The server provides both console interface and TCP server for remote clients.");
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
}

public class CommandLineOptions
{
    public string? PortName { get; set; }
    public int BaudRate { get; set; } = 9600;
    public int TcpPort { get; set; } = 8080;
    public bool ListPorts { get; set; }
    public bool ShowHelp { get; set; }
}
