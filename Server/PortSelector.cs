using System.IO.Ports;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace SharpCAT2.Server;

/// <summary>
/// Handles serial port selection and validation logic.
/// Provides a clean separation of concerns for port-related operations.
/// </summary>
public class PortSelector
{
    private readonly ILogger<PortSelector> _logger;

    public PortSelector(ILogger<PortSelector> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validates or prompts for a port name, providing user-friendly guidance
    /// </summary>
    /// <param name="portName">Initial port name (may be null)</param>
    /// <returns>Valid port name selected by user or validated</returns>
    public string ValidateOrPromptPortName(string? portName)
    {
        if (!string.IsNullOrEmpty(portName))
        {
            return ValidateExistingPortName(portName);
        }
        
        return PromptForPortSelection();
    }

    /// <summary>
    /// Validates an existing port name and provides warnings if needed
    /// </summary>
    /// <param name="portName">Port name to validate</param>
    /// <returns>The original port name (with warnings if invalid)</returns>
    private string ValidateExistingPortName(string portName)
    {
        if (IsValidPortName(portName))
        {
            return portName;
        }
        
        Console.WriteLine($"Warning: Port '{portName}' may not exist or be accessible.");
        Console.WriteLine("Continuing anyway. Use --list to see available ports.");
        return portName;
    }

    /// <summary>
    /// Prompts user for port selection with intelligent port discovery
    /// </summary>
    /// <returns>Selected port name</returns>
    private string PromptForPortSelection()
    {
        Console.WriteLine("No port specified. Scanning for available ports...");
        
        try
        {
            string[] ports = SerialPort.GetPortNames();
            
            return ports.Length switch
            {
                0 => HandleNoPortsFound(),
                1 => HandleSinglePortFound(ports[0]),
                _ => HandleMultiplePortsFound(ports)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning ports");
            return PromptForManualPortEntry();
        }
    }

    /// <summary>
    /// Handles the case when no ports are automatically detected
    /// </summary>
    /// <returns>Manually entered port name</returns>
    private string HandleNoPortsFound()
    {
        Console.WriteLine("No ports found automatically.");
        return PromptForManualPortEntry();
    }

    /// <summary>
    /// Handles the case when exactly one port is detected
    /// </summary>
    /// <param name="portName">The single detected port</param>
    /// <returns>Either the detected port or manually entered port</returns>
    private string HandleSinglePortFound(string portName)
    {
        Console.WriteLine($"Found one port: {portName}");
        Console.Write("Use this port? (y/N): ");
        string? response = Console.ReadLine();
        
        if (IsPositiveResponse(response))
        {
            return portName;
        }
        
        return PromptForManualPortEntry();
    }

    /// <summary>
    /// Handles the case when multiple ports are detected
    /// </summary>
    /// <param name="ports">Array of detected ports</param>
    /// <returns>Selected port name</returns>
    private string HandleMultiplePortsFound(string[] ports)
    {
        DisplayPortOptions(ports);
        
        Console.Write($"Select port number (1-{ports.Length}) or enter custom name: ");
        string? input = Console.ReadLine();
        
        // Try to parse as port selection number
        if (int.TryParse(input, out int selection) && IsValidPortSelection(selection, ports.Length))
        {
            return ports[selection - 1];
        }
        
        // Use as custom port name if provided
        if (!string.IsNullOrEmpty(input))
        {
            return input;
        }
        
        // Fall back to manual entry
        return PromptForManualPortEntry();
    }

    /// <summary>
    /// Displays available port options to the user
    /// </summary>
    /// <param name="ports">Array of available ports</param>
    private static void DisplayPortOptions(string[] ports)
    {
        Console.WriteLine("Multiple ports found:");
        for (int i = 0; i < ports.Length; i++)
        {
            Console.WriteLine($"{i + 1}. {ports[i]}");
        }
    }

    /// <summary>
    /// Prompts user for manual port name entry with platform-specific guidance
    /// </summary>
    /// <returns>Manually entered port name</returns>
    private string PromptForManualPortEntry()
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

    /// <summary>
    /// Gets platform-specific port name example
    /// </summary>
    /// <returns>Example port name for current platform</returns>
    private static string GetPlatformPortExample()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return Constants.WindowsPortExample;
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return Constants.LinuxPortExample;
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return Constants.MacOSPortExample;
        else
            return Constants.WindowsPortExample;
    }

    /// <summary>
    /// Validates if a port name exists in the system
    /// </summary>
    /// <param name="portName">Port name to check</param>
    /// <returns>True if port exists, false otherwise</returns>
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
    /// Checks if user response indicates agreement
    /// </summary>
    /// <param name="response">User input response</param>
    /// <returns>True if response is positive</returns>
    private static bool IsPositiveResponse(string? response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return false;
            
        var normalized = response.Trim().ToLower();
        return normalized == "y" || normalized == "yes";
    }

    /// <summary>
    /// Validates if port selection number is within valid range
    /// </summary>
    /// <param name="selection">Selected port number</param>
    /// <param name="totalPorts">Total number of available ports</param>
    /// <returns>True if selection is valid</returns>
    private static bool IsValidPortSelection(int selection, int totalPorts)
    {
        return selection >= 1 && selection <= totalPorts;
    }
}