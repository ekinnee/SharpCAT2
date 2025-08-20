using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using SharpCAT2.Core.Services;

namespace SharpCAT2.Tests.Services;

/// <summary>
/// Tests for the PortSelectionService demonstrating separation of concerns
/// </summary>
public class PortSelectionServiceTests
{
    private readonly Mock<ILogger<PortSelectionService>> _mockLogger;
    private readonly PortSelectionService _portSelectionService;

    public PortSelectionServiceTests()
    {
        _mockLogger = new Mock<ILogger<PortSelectionService>>();
        _portSelectionService = new PortSelectionService(_mockLogger.Object);
    }

    [Fact]
    public void ValidatePortName_WithFakePort_ShouldReturnValidResult()
    {
        // Arrange
        var portName = "FAKE";

        // Act
        var result = _portSelectionService.ValidatePortName(portName);

        // Assert
        Assert.True(result.IsValid);
        Assert.True(result.IsFakePort);
        Assert.False(result.RequiresWarning);
        Assert.Null(result.WarningMessage);
    }

    [Fact]
    public void ValidatePortName_WithEmptyPort_ShouldReturnInvalidResult()
    {
        // Arrange
        var portName = "";

        // Act
        var result = _portSelectionService.ValidatePortName(portName);

        // Assert
        Assert.False(result.IsValid);
        Assert.False(result.IsFakePort);
        Assert.True(result.RequiresWarning);
        Assert.NotNull(result.WarningMessage);
    }

    [Fact]
    public void GetAvailablePortNames_ShouldIncludeFakePorts()
    {
        // Act
        var ports = _portSelectionService.GetAvailablePortNames();

        // Assert
        Assert.NotNull(ports);
        Assert.Contains("FAKE", ports);
        Assert.Contains("DUMMY", ports);
        Assert.Contains("TEST", ports);
        Assert.Contains("SIMULATION", ports);
    }

    [Fact]
    public void GetPermissionGuidance_ShouldReturnPlatformSpecificMessage()
    {
        // Act
        var guidance = _portSelectionService.GetPermissionGuidance();

        // Assert
        Assert.NotNull(guidance);
        Assert.NotEmpty(guidance);
        // The message should be platform-specific
        Assert.True(guidance.Contains("group") || guidance.Contains("driver") || guidance.Contains("application"));
    }

    [Fact]
    public void GetRecommendedStrategy_ShouldReturnValidStrategy()
    {
        // Act
        var strategy = _portSelectionService.GetRecommendedStrategy();

        // Assert
        Assert.NotNull(strategy);
        Assert.True(Enum.IsDefined(typeof(PortSelectionType), strategy.Type));
    }
}