using System.IO.Ports;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace SharpCAT2.Core.Services;

/// <summary>
/// Implementation of port selection business logic.
/// Handles port validation, discovery, and selection strategies without UI concerns.
/// </summary>
public class PortSelectionService : IPortSelectionService
{
    private readonly ILogger<PortSelectionService> _logger;

    public PortSelectionService(ILogger<PortSelectionService> logger)
    {
        _logger = logger;
    }

    public PortValidationResult ValidatePortName(string portName)
    {
        if (string.IsNullOrWhiteSpace(portName))
        {
            return new PortValidationResult(false, false, true, "Port name cannot be empty");
        }

        // Check if it's a fake port
        if (IsFakePortName(portName))
        {
            return new PortValidationResult(true, true, false, null);
        }

        // Check if real port exists
        try
        {
            var availablePorts = SerialPort.GetPortNames();
            bool exists = availablePorts.Contains(portName, StringComparer.OrdinalIgnoreCase);
            
            if (exists)
            {
                return new PortValidationResult(true, false, false, null);
            }
            else
            {
                return new PortValidationResult(false, false, true, 
                    $"Port '{portName}' may not exist or be accessible. Use --list to see available ports.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error validating port {PortName}", portName);
            return new PortValidationResult(false, false, true, 
                $"Unable to validate port '{portName}' - {ex.Message}");
        }
    }

    public string[] GetAvailablePortNames()
    {
        try
        {
            var realPorts = SerialPort.GetPortNames();
            var fakePorts = new[] { "FAKE", "DUMMY", "TEST", "SIMULATION" };
            return realPorts.Concat(fakePorts).OrderBy(p => p).ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available ports");
            // Return at least fake ports if hardware enumeration fails
            return new[] { "FAKE", "DUMMY", "TEST", "SIMULATION" };
        }
    }

    public PortSelectionStrategy GetRecommendedStrategy()
    {
        try
        {
            string[] realPorts = SerialPort.GetPortNames();
            
            return realPorts.Length switch
            {
                0 => new PortSelectionStrategy(PortSelectionType.NoPortsFound),
                1 => new PortSelectionStrategy(PortSelectionType.SinglePortAvailable, 
                    realPorts[0], realPorts),
                _ => new PortSelectionStrategy(PortSelectionType.MultiplePortsAvailable, 
                    null, realPorts)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error determining port selection strategy");
            return new PortSelectionStrategy(PortSelectionType.ManualEntryRequired);
        }
    }

    public string GetPermissionGuidance()
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

    /// <summary>
    /// Determines if a port name represents a fake/simulation port
    /// </summary>
    /// <param name="portName">Port name to check</param>
    /// <returns>True if it's a fake port</returns>
    private static bool IsFakePortName(string portName)
    {
        var normalizedName = portName.ToUpperInvariant();
        return normalizedName == "FAKE" || normalizedName == "DUMMY" || 
               normalizedName == "TEST" || normalizedName == "SIMULATION";
    }
}