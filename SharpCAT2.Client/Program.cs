using SharpCAT2.Radio;
using SharpCAT2.Radio.Models;
using System.Runtime.InteropServices;

namespace SharpCAT2.Client;

class Program
{
    private static async Task Main(string[] args)
    {
        Console.WriteLine("SharpCAT2 Client - Radio Control Application");
        Console.WriteLine("=============================================");
        
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
            
            if (options.ListRadios)
            {
                ListSupportedRadios();
                return;
            }
            
            // Validate radio selection
            if (string.IsNullOrEmpty(options.Manufacturer) || string.IsNullOrEmpty(options.Model))
            {
                PromptForRadioSelection(options);
            }
            
            // Create radio instance
            var radio = RadioFactory.CreateRadio(options.Manufacturer!, options.Model!);
            Console.WriteLine($"Created radio: {radio.Manufacturer} {radio.Model} ({radio.Protocol})");
            
            // Validate or prompt for port name
            string portName = ValidateOrPromptPortName(options.PortName);
            
            // Connect to radio
            Console.WriteLine($"Connecting to {radio.Manufacturer} {radio.Model} on {portName}...");
            bool connected = await radio.ConnectAsync(portName, options.BaudRate);
            
            if (!connected)
            {
                Console.WriteLine("Failed to connect to radio.");
                return;
            }
            
            Console.WriteLine("Successfully connected to radio!");
            Console.WriteLine("Commands: freq, mode, quit");
            
            // Interactive command loop
            await RunInteractiveSession(radio);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }
    
    private static async Task RunInteractiveSession(IRadio radio)
    {
        using (radio)
        {
            while (true)
            {
                Console.Write("CAT> ");
                var input = Console.ReadLine()?.Trim().ToLowerInvariant();
                
                if (string.IsNullOrEmpty(input))
                    continue;
                
                try
                {
                    switch (input)
                    {
                        case "quit":
                        case "exit":
                        case "q":
                            return;
                            
                        case "freq":
                        case "frequency":
                            var freq = await radio.GetFrequencyAsync();
                            Console.WriteLine($"Frequency: {freq:N0} Hz ({freq / 1_000_000.0:F3} MHz)");
                            break;
                            
                        case "mode":
                            var mode = await radio.GetModeAsync();
                            Console.WriteLine($"Mode: {mode}");
                            break;
                            
                        case "help":
                            Console.WriteLine("Available commands:");
                            Console.WriteLine("  freq     - Get current frequency");
                            Console.WriteLine("  mode     - Get current mode");
                            Console.WriteLine("  quit     - Exit application");
                            break;
                            
                        default:
                            if (input.StartsWith("freq "))
                            {
                                var freqStr = input.Substring(5);
                                if (long.TryParse(freqStr, out long newFreq))
                                {
                                    await radio.SetFrequencyAsync(newFreq);
                                    Console.WriteLine($"Frequency set to {newFreq:N0} Hz");
                                }
                                else
                                {
                                    Console.WriteLine("Invalid frequency format. Use Hz (e.g., 14230000)");
                                }
                            }
                            else if (input.StartsWith("mode "))
                            {
                                var modeStr = input.Substring(5).ToUpperInvariant();
                                if (Enum.TryParse<RadioMode>(modeStr, out var newMode))
                                {
                                    await radio.SetModeAsync(newMode);
                                    Console.WriteLine($"Mode set to {newMode}");
                                }
                                else
                                {
                                    Console.WriteLine("Invalid mode. Available: LSB, USB, CW, FM, AM, Digital");
                                }
                            }
                            else
                            {
                                Console.WriteLine("Unknown command. Type 'help' for available commands.");
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Command error: {ex.Message}");
                }
            }
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
            guidance = "Typical port names: /dev/ttyUSB0, /dev/ttyACM0, /dev/ttyS0, etc.";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            platform = "macOS";
            guidance = "Typical port names: /dev/cu.usbserial-*, /dev/cu.usbmodem*, etc.";
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
                case "-m":
                case "--manufacturer":
                    if (i + 1 < args.Length)
                        options.Manufacturer = args[++i];
                    break;
                case "-r":
                case "--radio":
                case "--model":
                    if (i + 1 < args.Length)
                        options.Model = args[++i];
                    break;
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
                    options.ListRadios = true;
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
        Console.WriteLine("Usage: Client [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -m, --manufacturer <name>  Radio manufacturer (Icom, Yaesu, Kenwood, Elecraft, FlexRadio)");
        Console.WriteLine("  -r, --model <name>         Radio model");
        Console.WriteLine("  -p, --port <name>          Serial port name");
        Console.WriteLine("  -b, --baud <rate>          Baud rate (default: 9600)");
        Console.WriteLine("  -l, --list                 List supported radios");
        Console.WriteLine("  -h, --help                 Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  Client --manufacturer Icom --model IC-7300 --port COM1");
        Console.WriteLine("  Client -m Yaesu -r FT-991A -p /dev/ttyUSB0 -b 4800");
    }
    
    private static void ListSupportedRadios()
    {
        Console.WriteLine("Supported Radio Manufacturers and Models:");
        Console.WriteLine("========================================");
        
        foreach (var manufacturer in RadioFactory.GetSupportedManufacturers())
        {
            Console.WriteLine($"\n{manufacturer}:");
            var models = RadioFactory.GetSupportedModels(manufacturer);
            foreach (var model in models)
            {
                Console.WriteLine($"  - {model}");
            }
        }
    }
    
    private static void PromptForRadioSelection(CommandLineOptions options)
    {
        if (string.IsNullOrEmpty(options.Manufacturer))
        {
            Console.WriteLine("Available manufacturers:");
            var manufacturers = RadioFactory.GetSupportedManufacturers().ToArray();
            for (int i = 0; i < manufacturers.Length; i++)
            {
                Console.WriteLine($"{i + 1}. {manufacturers[i]}");
            }
            
            Console.Write("Select manufacturer (1-{0}): ", manufacturers.Length);
            var input = Console.ReadLine();
            if (int.TryParse(input, out int selection) && selection >= 1 && selection <= manufacturers.Length)
            {
                options.Manufacturer = manufacturers[selection - 1];
            }
            else
            {
                throw new ArgumentException("Invalid manufacturer selection");
            }
        }
        
        if (string.IsNullOrEmpty(options.Model))
        {
            Console.WriteLine($"\nAvailable {options.Manufacturer} models:");
            var models = RadioFactory.GetSupportedModels(options.Manufacturer!).ToArray();
            for (int i = 0; i < models.Length; i++)
            {
                Console.WriteLine($"{i + 1}. {models[i]}");
            }
            
            Console.Write("Select model (1-{0}): ", models.Length);
            var input = Console.ReadLine();
            if (int.TryParse(input, out int selection) && selection >= 1 && selection <= models.Length)
            {
                options.Model = models[selection - 1];
            }
            else
            {
                throw new ArgumentException("Invalid model selection");
            }
        }
    }
    
    private static string ValidateOrPromptPortName(string? portName)
    {
        if (!string.IsNullOrEmpty(portName))
            return portName;
        
        Console.Write("Enter serial port name: ");
        var input = Console.ReadLine();
        if (string.IsNullOrEmpty(input))
            throw new ArgumentException("Port name cannot be empty");
        
        return input;
    }
}

public class CommandLineOptions
{
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? PortName { get; set; }
    public int BaudRate { get; set; } = 9600;
    public bool ListRadios { get; set; }
    public bool ShowHelp { get; set; }
}