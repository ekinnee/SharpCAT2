using SharpCAT2.Common.Radio.Models.Testing;
using SharpCAT2.Common.Serial;
using Xunit;

namespace SharpCAT2.Tests.Radio;

/// <summary>
/// Tests for radio PortName property functionality
/// </summary>
public class RadioPortNameTests
{
    [Fact]
    public async Task DummyRadio_PortName_ReturnsCorrectPortName()
    {
        // Arrange
        var radio = new DummyRadio();
        var fakePort = new FakeSerialPort("COM7");
        
        // Act
        await radio.ConnectAsync(fakePort);
        var portName = radio.PortName;
        
        // Assert
        Assert.Equal("COM7", portName);
        
        // Cleanup
        radio.Disconnect();
        radio.Dispose();
    }

    [Fact]
    public void DummyRadio_PortName_ReturnsUnknownWhenDisconnected()
    {
        // Arrange
        var radio = new DummyRadio();
        
        // Act - radio not connected
        var portName = radio.PortName;
        
        // Assert
        Assert.Equal("Unknown", portName);
        
        // Cleanup
        radio.Dispose();
    }

    [Theory]
    [InlineData("FAKE")]
    [InlineData("/dev/ttyUSB0")]
    [InlineData("COM1")]
    public async Task DummyRadio_PortName_ReturnsCorrectPortNameForDifferentPortTypes(string portName)
    {
        // Arrange
        var radio = new DummyRadio();
        var fakePort = new FakeSerialPort(portName);
        
        // Act
        await radio.ConnectAsync(fakePort);
        var actualPortName = radio.PortName;
        
        // Assert
        Assert.Equal(portName, actualPortName);
        
        // Cleanup
        radio.Disconnect();
        radio.Dispose();
    }
}