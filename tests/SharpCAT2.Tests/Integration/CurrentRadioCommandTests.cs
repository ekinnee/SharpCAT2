using SharpCAT2.Common.Radio.Models.Testing;
using SharpCAT2.Common.Serial;
using SharpCAT2.Core.Radio;
using SharpCAT2.Core.Services;
using SharpCAT2.Common;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace SharpCAT2.Tests.Integration;

/// <summary>
/// Tests for CURRENT_RADIO command functionality with actual port names
/// </summary>
public class CurrentRadioCommandTests
{
    [Fact]
    public async Task RadioService_GetCurrentRadioInfo_ReturnsFormattedStringWithPortName()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RadioService>>();
        var radioService = new RadioService(mockLogger.Object);
        
        var radio = new DummyRadio();
        var fakePort = new FakeSerialPort("COM7");
        
        // Connect the radio
        await radio.ConnectAsync(fakePort);
        
        // Act - Simulate getting the radio information as ServerApplication would
        var manufacturer = radio.Manufacturer;
        var modelName = radio.ModelName;
        var portName = radio.PortName;
        var connectionStatus = radio.IsConnected ? "CONNECTED" : "DISCONNECTED";
        
        var currentRadioResponse = $"CURRENT_RADIO:{manufacturer} {modelName}|{portName}|{connectionStatus}";
        
        // Assert
        Assert.Equal("CURRENT_RADIO:SharpCAT2 DummyRadio|COM7|CONNECTED", currentRadioResponse);
        Assert.Contains("COM7", currentRadioResponse);
        Assert.DoesNotContain("22", currentRadioResponse); // Should not contain feature count
        
        // Cleanup
        radio.Disconnect();
        radio.Dispose();
    }

    [Theory]
    [InlineData("FAKE", "CURRENT_RADIO:SharpCAT2 DummyRadio|FAKE|CONNECTED")]
    [InlineData("/dev/ttyUSB0", "CURRENT_RADIO:SharpCAT2 DummyRadio|/dev/ttyUSB0|CONNECTED")]
    [InlineData("COM1", "CURRENT_RADIO:SharpCAT2 DummyRadio|COM1|CONNECTED")]
    public async Task RadioService_GetCurrentRadioInfo_ReturnsCorrectFormatForDifferentPorts(string portName, string expectedResponse)
    {
        // Arrange
        var radio = new DummyRadio();
        var fakePort = new FakeSerialPort(portName);
        
        // Connect the radio
        await radio.ConnectAsync(fakePort);
        
        // Act - Simulate the ServerApplication.SendCurrentRadioResponseAsync logic
        var manufacturer = radio.Manufacturer;
        var modelName = radio.ModelName;
        var actualPortName = radio.PortName;
        var connectionStatus = radio.IsConnected ? "CONNECTED" : "DISCONNECTED";
        
        var currentRadioResponse = $"CURRENT_RADIO:{manufacturer} {modelName}|{actualPortName}|{connectionStatus}";
        
        // Assert
        Assert.Equal(expectedResponse, currentRadioResponse);
        
        // Cleanup
        radio.Disconnect();
        radio.Dispose();
    }

    [Fact]
    public void RadioService_GetCurrentRadioInfo_DisconnectedRadio_ReturnsUnknownPort()
    {
        // Arrange
        var radio = new DummyRadio();
        
        // Act - Get info without connecting
        var manufacturer = radio.Manufacturer;
        var modelName = radio.ModelName;
        var portName = radio.PortName;
        var connectionStatus = radio.IsConnected ? "CONNECTED" : "DISCONNECTED";
        
        var currentRadioResponse = $"CURRENT_RADIO:{manufacturer} {modelName}|{portName}|{connectionStatus}";
        
        // Assert
        Assert.Equal("CURRENT_RADIO:SharpCAT2 DummyRadio|Unknown|DISCONNECTED", currentRadioResponse);
        
        // Cleanup
        radio.Dispose();
    }
}