using SharpCAT2.Core.Services;
using Microsoft.Extensions.Logging;

namespace SharpCAT2.ServerConsole;

/// <summary>
/// Refactored port selector using separated concerns.
/// Uses business logic service and UI service for clean separation.
/// </summary>
public class RefactoredPortSelector
{
    private readonly IPortSelectionService _portSelectionService;
    private readonly IUserInterfaceService _userInterfaceService;
    private readonly ILogger<RefactoredPortSelector> _logger;

    public RefactoredPortSelector(
        IPortSelectionService portSelectionService,
        IUserInterfaceService userInterfaceService,
        ILogger<RefactoredPortSelector> logger)
    {
        _portSelectionService = portSelectionService;
        _userInterfaceService = userInterfaceService;
        _logger = logger;
    }

    /// <summary>
    /// Validates or prompts for a port name using separated concerns
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
    /// Validates an existing port name using business logic service
    /// </summary>
    /// <param name="portName">Port name to validate</param>
    /// <returns>The original port name (with warnings if invalid)</returns>
    private string ValidateExistingPortName(string portName)
    {
        var validationResult = _portSelectionService.ValidatePortName(portName);
        
        if (validationResult.IsValid)
        {
            if (validationResult.IsFakePort)
            {
                _userInterfaceService.ShowInfo($"Using simulated port '{portName}' for testing/development.");
            }
            return portName;
        }
        
        if (validationResult.RequiresWarning && validationResult.WarningMessage != null)
        {
            _userInterfaceService.ShowWarning(validationResult.WarningMessage);
            _userInterfaceService.ShowInfo("Continuing anyway. Use --list to see available ports.");
        }
        
        return portName;
    }

    /// <summary>
    /// Prompts user for port selection using business logic and UI services
    /// </summary>
    /// <returns>Selected port name</returns>
    private string PromptForPortSelection()
    {
        _userInterfaceService.ShowInfo("No port specified. Scanning for available ports...");
        
        try
        {
            var strategy = _portSelectionService.GetRecommendedStrategy();
            
            return strategy.Type switch
            {
                PortSelectionType.NoPortsFound => HandleNoPortsFound(),
                PortSelectionType.SinglePortAvailable => HandleSinglePortFound(strategy.RecommendedPort!),
                PortSelectionType.MultiplePortsAvailable => HandleMultiplePortsFound(strategy.AvailablePorts!),
                PortSelectionType.ManualEntryRequired => PromptForManualPortEntry(),
                _ => PromptForManualPortEntry()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during port selection");
            return PromptForManualPortEntry();
        }
    }

    /// <summary>
    /// Handles the case when no ports are automatically detected
    /// </summary>
    /// <returns>Manually entered port name</returns>
    private string HandleNoPortsFound()
    {
        _userInterfaceService.ShowInfo("No ports found automatically.");
        return PromptForManualPortEntry();
    }

    /// <summary>
    /// Handles the case when exactly one port is detected
    /// </summary>
    /// <param name="portName">The single detected port</param>
    /// <returns>Either the detected port or manually entered port</returns>
    private string HandleSinglePortFound(string portName)
    {
        _userInterfaceService.ShowInfo($"Found one port: {portName}");
        _userInterfaceService.Write($"Use this port? (y/n) [y]: ");
        
        try
        {
            string? choice = _userInterfaceService.ReadLine();
            if (string.IsNullOrWhiteSpace(choice) || choice.ToLower().StartsWith("y"))
            {
                return portName;
            }
        }
        catch (InvalidOperationException)
        {
            // Console input redirected, default to yes
            _userInterfaceService.ShowInfo("Using detected port (input redirected).");
            return portName;
        }
        
        return PromptForManualPortEntry();
    }

    /// <summary>
    /// Handles the case when multiple ports are detected
    /// </summary>
    /// <param name="ports">Available ports</param>
    /// <returns>User-selected port name</returns>
    private string HandleMultiplePortsFound(string[] ports)
    {
        _userInterfaceService.ShowAvailablePorts(ports);
        _userInterfaceService.Write("Enter port number or port name: ");
        
        try
        {
            string? input = _userInterfaceService.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                return PromptForManualPortEntry();
            }
            
            // Try to parse as port number
            if (int.TryParse(input, out int portNumber))
            {
                // Account for fake port being #1
                if (portNumber == 1)
                {
                    return "fake";
                }
                else if (portNumber >= 2 && portNumber <= ports.Length + 1)
                {
                    return ports[portNumber - 2]; // Adjust for fake port offset
                }
            }
            
            // Treat as direct port name
            return input.Trim();
        }
        catch (InvalidOperationException)
        {
            // Console input redirected
            _userInterfaceService.ShowInfo("Multiple ports found, but input is redirected. Please specify port with -p argument.");
            return "fake"; // Default to fake port for automated scenarios
        }
    }

    /// <summary>
    /// Prompts for manual port entry with guidance
    /// </summary>
    /// <returns>Manually entered port name</returns>
    private string PromptForManualPortEntry()
    {
        _userInterfaceService.ShowInfo("Please enter a port name manually:");
        _userInterfaceService.ShowInfo("Examples: COM1 (Windows), /dev/ttyUSB0 (Linux), /dev/cu.usbserial-* (macOS), or 'fake' for testing");
        _userInterfaceService.Write("Port name: ");
        
        try
        {
            string? input = _userInterfaceService.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                _userInterfaceService.ShowWarning("No port specified, using 'fake' for testing.");
                return "fake";
            }
            return input.Trim();
        }
        catch (InvalidOperationException)
        {
            // Console input redirected
            _userInterfaceService.ShowWarning("Input redirected and no port specified, using 'fake' for testing.");
            return "fake";
        }
    }
}