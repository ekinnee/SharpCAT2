using System.IO.Ports;
using System.Runtime.InteropServices;

namespace SharpCAT2.Server;

class Program
{
    private static void Main(string[] args)
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
            using var serialPort = OpenSerialPort(portName, options.BaudRate);
            
            Console.WriteLine($"Successfully opened serial port: {portName}");
            Console.WriteLine($"Baud rate: {options.BaudRate}");
            Console.WriteLine("Press 'q' to quit, or type messages to send...");
            
            // Start listening for incoming data
            serialPort.DataReceived += (sender, e) =>
            {
                try
                {
                    if (sender is SerialPort port && port.IsOpen)
                    {
                        string data = port.ReadExisting();
                        if (!string.IsNullOrEmpty(data))
                        {
                            Console.Write($"Received: {data}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading data: {ex.Message}");
                }
            };
            
            // Main communication loop
            string? input;
            while ((input = Console.ReadLine()) != "q")
            {
                if (!string.IsNullOrEmpty(input))
                {
                    try
                    {
                        serialPort.WriteLine(input);
                        Console.WriteLine($"Sent: {input}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error sending data: {ex.Message}");
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex.Message}");
            Environment.Exit(1);
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
                        options.BaudRate = baud;
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
        Console.WriteLine("  -l, --list            List available serial ports");
        Console.WriteLine("  -h, --help            Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.WriteLine("  Server --port COM1 --baud 115200");
            Console.WriteLine("  Server -p COM3");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Console.WriteLine("  Server --port /dev/ttyUSB0 --baud 115200");
            Console.WriteLine("  Server -p /dev/ttyACM0");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Console.WriteLine("  Server --port /dev/cu.usbserial-1410 --baud 115200");
            Console.WriteLine("  Server -p /dev/cu.usbmodem1411");
        }
        
        Console.WriteLine();
        Console.WriteLine("  Server --list         # List all available ports");
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
}

public class CommandLineOptions
{
    public string? PortName { get; set; }
    public int BaudRate { get; set; } = 9600;
    public bool ListPorts { get; set; }
    public bool ShowHelp { get; set; }
}
