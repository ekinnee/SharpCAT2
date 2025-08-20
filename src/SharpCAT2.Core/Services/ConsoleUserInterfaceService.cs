using System.Runtime.InteropServices;

namespace SharpCAT2.Core.Services;

/// <summary>
/// Console-based implementation of user interface service.
/// Handles all console I/O operations with platform-specific guidance.
/// </summary>
public class ConsoleUserInterfaceService : IUserInterfaceService
{
    public void WriteLine(string message)
    {
        global::System.Console.WriteLine(message);
    }

    public void Write(string message)
    {
        global::System.Console.Write(message);
    }

    public string? ReadLine()
    {
        return global::System.Console.ReadLine();
    }

    public ConsoleKeyInfo ReadKey()
    {
        return global::System.Console.ReadKey();
    }

    public void ShowHelp()
    {
        WriteLine("Usage: Server [options]");
        WriteLine("");
        WriteLine("Options:");
        WriteLine("  -p, --port <name>     Serial port name (e.g., COM1, /dev/ttyUSB0, fake)");
        WriteLine("  -b, --baud <rate>     Baud rate (default: 9600)");
        WriteLine("                        Supported rates: 9600, 14400, 19200, 28800, 38400, 57600, 115200, 128000, 256000");
        WriteLine("  -t, --tcp-port <port> TCP server port (default: 8080)");
        WriteLine("  -r, --radio <model>   Radio model (e.g., \"Kenwood TS-2000\")");
        WriteLine("  --auto-detect         Auto-detect radio type");
        WriteLine("  -l, --list            List available serial ports");
        WriteLine("  --list-radios         List available radio models");
        WriteLine("  --radio-info <model>  Show detailed information about a radio model");
        WriteLine("  -h, --help            Show this help message");
        WriteLine("");
        WriteLine("Special Ports:");
        WriteLine("  fake                  Simulated serial port for testing and development");
        WriteLine("");
        WriteLine("The server provides both console interface and TCP server for remote clients.");
        WriteLine("With radio support, you can send CAT commands and get radio status information.");
    }

    public void ShowAvailablePorts(string[] ports)
    {
        WriteLine("Available Serial Ports:");
        WriteLine("======================");
        
        int portNumber = 1;
        
        // Always show the fake port first as a test/simulation option
        WriteLine($"{portNumber++}. fake (Simulated/Test Port)");
        
        if (ports.Length == 0)
        {
            WriteLine("");
            WriteLine("No hardware serial ports found.");
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                WriteLine("");
                WriteLine("Linux troubleshooting:");
                WriteLine("- Check if devices are connected: ls /dev/tty*");
                WriteLine("- Verify permissions: groups $USER");
                WriteLine("- Add user to dialout group: sudo usermod -a -G dialout $USER");
                WriteLine("- Log out and back in for group changes to take effect");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                WriteLine("");
                WriteLine("macOS troubleshooting:");
                WriteLine("- Check devices manually: ls /dev/cu.*");
                WriteLine("- Verify device drivers are installed");
            }
        }
        else
        {
            for (int i = 0; i < ports.Length; i++)
            {
                WriteLine($"{portNumber++}. {ports[i]}");
            }
            
            WriteLine("");
            WriteLine($"Found {ports.Length} hardware port(s) plus 1 simulated port.");
        }
        
        WriteLine("");
        WriteLine("Note: Use 'fake' for development and testing without hardware.");
    }

    public void ShowAvailableRadios(Dictionary<string, string> radios)
    {
        WriteLine("Available Radio Models:");
        WriteLine("======================");
        
        if (radios.Count == 0)
        {
            WriteLine("No radio models found.");
        }
        else
        {
            foreach (var radio in radios.OrderBy(r => r.Key))
            {
                WriteLine($"  {radio.Key}");
            }
            
            WriteLine("");
            WriteLine($"Found {radios.Count} radio model(s).");
            WriteLine("Use --radio \"Manufacturer Model\" to specify a radio.");
            WriteLine("Use --auto-detect to automatically detect the radio type.");
        }
    }

    public void ShowRadioInfo(string radioInfo)
    {
        WriteLine(radioInfo);
    }

    public void ShowWarning(string message)
    {
        WriteLine($"Warning: {message}");
    }

    public void ShowError(string message)
    {
        WriteLine($"Error: {message}");
    }

    public void ShowInfo(string message)
    {
        WriteLine(message);
    }
}