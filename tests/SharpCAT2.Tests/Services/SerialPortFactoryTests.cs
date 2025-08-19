using Xunit;
using SharpCAT2.Common.Serial;

namespace SharpCAT2.Tests.Services;

/// <summary>
/// Unit tests for SerialPortFactory methods
/// </summary>
public class SerialPortFactoryTests
{
    [Fact]
    public void GetAvailablePortNames_ShouldReturnNonNullArray()
    {
        // Act
        var ports = SerialPortFactory.GetAvailablePortNames();

        // Assert
        Assert.NotNull(ports);
        // Don't assert specific ports since this depends on the system
        // Just ensure we get a valid array
    }

    [Theory]
    [InlineData("FAKE")]
    [InlineData("DUMMY")]
    [InlineData("TEST")]
    [InlineData("SIMULATION")]
    public void IsFakePortName_WithFakePortNames_ShouldReturnTrue(string portName)
    {
        // Act
        var result = SerialPortFactory.IsFakePortName(portName);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("COM1")]
    [InlineData("/dev/ttyUSB0")]
    [InlineData("")]
    [InlineData(null)]
    public void IsFakePortName_WithRealPortNames_ShouldReturnFalse(string? portName)
    {
        // Act
        var result = SerialPortFactory.IsFakePortName(portName ?? "");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsPortAvailable_WithFakePortName_ShouldReturnTrue()
    {
        // Act
        var result = SerialPortFactory.IsPortAvailable("FAKE");

        // Assert
        Assert.True(result);
    }
}