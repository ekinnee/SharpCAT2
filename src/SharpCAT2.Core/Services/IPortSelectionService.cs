namespace SharpCAT2.Core.Services;

/// <summary>
/// Interface for port selection business logic, separated from UI concerns.
/// Handles port validation, discovery, and selection logic.
/// </summary>
public interface IPortSelectionService
{
    /// <summary>
    /// Validates a port name and determines if it's accessible
    /// </summary>
    /// <param name="portName">Port name to validate</param>
    /// <returns>Validation result with details</returns>
    PortValidationResult ValidatePortName(string portName);

    /// <summary>
    /// Gets all available port names including fake ports
    /// </summary>
    /// <returns>Array of available port names</returns>
    string[] GetAvailablePortNames();

    /// <summary>
    /// Determines the best port selection strategy based on available ports
    /// </summary>
    /// <returns>Port selection recommendation</returns>
    PortSelectionStrategy GetRecommendedStrategy();

    /// <summary>
    /// Gets platform-specific permission guidance for port access issues
    /// </summary>
    /// <returns>Platform-specific guidance text</returns>
    string GetPermissionGuidance();
}

/// <summary>
/// Result of port name validation
/// </summary>
public record PortValidationResult(
    bool IsValid,
    bool IsFakePort,
    bool RequiresWarning,
    string? WarningMessage
);

/// <summary>
/// Port selection strategy recommendation
/// </summary>
public record PortSelectionStrategy(
    PortSelectionType Type,
    string? RecommendedPort = null,
    string[]? AvailablePorts = null
);

/// <summary>
/// Types of port selection strategies
/// </summary>
public enum PortSelectionType
{
    NoPortsFound,
    SinglePortAvailable,
    MultiplePortsAvailable,
    ManualEntryRequired
}