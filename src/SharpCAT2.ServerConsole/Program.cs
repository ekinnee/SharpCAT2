using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SharpCAT2.Core.Services;
using SharpCAT2.Core.Configuration;
using SharpCAT2.Common.Serial;
using SharpCAT2.Common;
using System.Runtime.InteropServices;

namespace SharpCAT2.ServerConsole;

/// <summary>
/// Main program class for the SharpCAT2 Server application.
/// Uses dependency injection and service-based architecture for improved testability and maintainability.
/// </summary>
class Program
{
    #region Public Methods

    /// <summary>
    /// Validates if the provided baud rate is supported
    /// </summary>
    /// <param name="baudRate">Baud rate to validate</param>
    /// <returns>True if baud rate is supported</returns>
    public static bool IsValidBaudRate(int baudRate)
    {
        return Constants.SupportedBaudRates.Contains(baudRate);
    }

    /// <summary>
    /// Validates if the provided TCP port is in valid range
    /// </summary>
    /// <param name="port">TCP port to validate</param>
    /// <returns>True if port is valid</returns>
    public static bool IsValidTcpPort(int port)
    {
        return port >= Constants.MinTcpPort && port <= Constants.MaxTcpPort;
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Main entry point for the SharpCAT2 Server application.
    /// Sets up dependency injection, parses command line arguments, and starts the application.
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <returns>Task representing the async operation</returns>
    private static async Task Main(string[] args)
    {
        global::System.Console.WriteLine(Constants.ApplicationTitle);
        global::System.Console.WriteLine(Constants.TitleSeparator);
        
        try
        {
            // Display platform information
            DisplayPlatformInfo();
            
            // Create host builder with dependency injection
            var host = CreateHostBuilder(args).Build();
            
            // Get logger for main program
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("SharpCAT2 Server starting up");
            
            // Start the application
            var app = host.Services.GetRequiredService<ServerApplication>();
            await app.RunAsync(args);
        }
        catch (ArgumentException ex)
        {
            global::System.Console.WriteLine($"Invalid argument: {ex.Message}");
            ShowHelp();
            Environment.Exit(1);
        }
        catch (InvalidOperationException ex)
        {
            global::System.Console.WriteLine($"Configuration error: {ex.Message}");
            Environment.Exit(1);
        }
        catch (IOException ex)
        {
            global::System.Console.WriteLine($"I/O error: {ex.Message}");
            Environment.Exit(1);
        }
        catch (UnauthorizedAccessException ex)
        {
            global::System.Console.WriteLine($"Access denied: {ex.Message}");
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            global::System.Console.WriteLine($"Unexpected error: {ex.Message}");
            global::System.Console.WriteLine($"Please report this issue with the following details:");
            global::System.Console.WriteLine($"Exception type: {ex.GetType().Name}");
            global::System.Console.WriteLine($"Stack trace: {ex.StackTrace}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Creates the host builder with dependency injection configuration
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <returns>Configured host builder</returns>
    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .ConfigureServices((context, services) =>
            {
                // Register services
                services.AddSingleton<IConfigurationService, ConfigurationService>();
                services.AddSingleton<ISecurityService, SecurityService>();
                services.AddSingleton<INetworkService, NetworkService>();
                services.AddSingleton<IRadioService, RadioService>();
                
                // Register the main application
                services.AddSingleton<ServerApplication>();
            });

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
        
        global::System.Console.WriteLine($"Platform: {platform}");
        global::System.Console.WriteLine($"{guidance}");
        global::System.Console.WriteLine();
    }

    private static void ShowHelp()
    {
        global::System.Console.WriteLine("Usage: Server [options]");
        global::System.Console.WriteLine();
        global::System.Console.WriteLine("Options:");
        global::System.Console.WriteLine("  -p, --port <name>     Serial port name (e.g., COM1, /dev/ttyUSB0, fake)");
        global::System.Console.WriteLine("  -b, --baud <rate>     Baud rate (default: 9600)");
        global::System.Console.WriteLine("                        Supported rates: 9600, 14400, 19200, 28800, 38400, 57600, 115200, 128000, 256000");
        global::System.Console.WriteLine("  -t, --tcp-port <port> TCP server port (default: 8080)");
        global::System.Console.WriteLine("  -r, --radio <model>   Radio model (e.g., \"Kenwood TS-2000\")");
        global::System.Console.WriteLine("  --auto-detect         Auto-detect radio type");
        global::System.Console.WriteLine("  -l, --list            List available serial ports");
        global::System.Console.WriteLine("  --list-radios         List available radio models");
        global::System.Console.WriteLine("  --radio-info <model>  Show detailed information about a radio model");
        global::System.Console.WriteLine("  -h, --help            Show this help message");
        global::System.Console.WriteLine();
        global::System.Console.WriteLine("Examples:");
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            global::System.Console.WriteLine($"  Server --port {Constants.WindowsPortExample} --baud 115200");
            global::System.Console.WriteLine("  Server -p COM3 --tcp-port 9090");
            global::System.Console.WriteLine($"  Server --port {Constants.WindowsPortExample} --radio \"Kenwood TS-2000\"");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            global::System.Console.WriteLine($"  Server --port {Constants.LinuxPortExample} --baud 115200");
            global::System.Console.WriteLine("  Server -p /dev/ttyACM0 --tcp-port 9090");
            global::System.Console.WriteLine($"  Server --port {Constants.LinuxPortExample} --auto-detect");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            global::System.Console.WriteLine($"  Server --port {Constants.MacOSPortExample} --baud 115200");
            global::System.Console.WriteLine("  Server -p /dev/cu.usbmodem1411 --tcp-port 9090");
            global::System.Console.WriteLine($"  Server --port {Constants.MacOSPortExample} --radio \"Elecraft K3\"");
        }
        
        global::System.Console.WriteLine();
        global::System.Console.WriteLine("  Server --port fake    # Use simulated port for testing/development");
        global::System.Console.WriteLine("  Server --list         # List all available ports");
        global::System.Console.WriteLine("  Server --list-radios  # List all available radio models");
        global::System.Console.WriteLine("  Server --radio-info \"Elecraft K3\"  # Show detailed radio information");
        global::System.Console.WriteLine();
        global::System.Console.WriteLine("Special Ports:");
        global::System.Console.WriteLine("  fake                  Simulated serial port for testing and development");
        global::System.Console.WriteLine();
        global::System.Console.WriteLine("The server provides both console interface and TCP server for remote clients.");
        global::System.Console.WriteLine("With radio support, you can send CAT commands and get radio status information.");
    }

    #endregion
}