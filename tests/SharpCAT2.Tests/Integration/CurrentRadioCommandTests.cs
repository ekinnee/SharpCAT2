using SharpCAT2.Common.Radio.Models.Testing;
using SharpCAT2.Common.Serial;
using SharpCAT2.Core.Radio;
using SharpCAT2.Core.Services;
using SharpCAT2.Core.Configuration;
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
        
        var fakePort = new FakeSerialPort("COM7");
        var options = new CommandLineOptions
        {
            RadioModel = "DummyRadio",
            AutoDetectRadio = false
        };
        
        // Connect the radio through RadioService
        await radioService.InitializeRadioAsync(options, fakePort);
        
        // Act - Get the radio information as ServerApplication would
        var radio = radioService.ConnectedRadio;
        var portName = radioService.ConnectedPortName ?? "Unknown";
        var connectionStatus = radio?.IsConnected == true ? "CONNECTED" : "DISCONNECTED";
        
        var currentRadioResponse = $"CURRENT_RADIO:{radio?.Manufacturer} {radio?.ModelName}|{portName}|{connectionStatus}";
        
        // Assert
        Assert.Equal("CURRENT_RADIO:SharpCAT2 DummyRadio|COM7|CONNECTED", currentRadioResponse);
        Assert.Contains("COM7", currentRadioResponse);
        Assert.DoesNotContain("22", currentRadioResponse); // Should not contain feature count
        
        // Cleanup
        await radioService.DisconnectRadioAsync();
        radioService.Dispose();
    }

    [Theory]
    [InlineData("FAKE", "CURRENT_RADIO:SharpCAT2 DummyRadio|FAKE|CONNECTED")]
    [InlineData("/dev/ttyUSB0", "CURRENT_RADIO:SharpCAT2 DummyRadio|/dev/ttyUSB0|CONNECTED")]
    [InlineData("COM1", "CURRENT_RADIO:SharpCAT2 DummyRadio|COM1|CONNECTED")]
    public async Task RadioService_GetCurrentRadioInfo_ReturnsCorrectFormatForDifferentPorts(string portName, string expectedResponse)
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RadioService>>();
        var radioService = new RadioService(mockLogger.Object);
        
        var fakePort = new FakeSerialPort(portName);
        var options = new CommandLineOptions
        {
            RadioModel = "DummyRadio",
            AutoDetectRadio = false
        };
        
        // Connect the radio through RadioService
        await radioService.InitializeRadioAsync(options, fakePort);
        
        // Act - Get the radio information as ServerApplication would
        var radio = radioService.ConnectedRadio;
        var actualPortName = radioService.ConnectedPortName ?? "Unknown";
        var connectionStatus = radio?.IsConnected == true ? "CONNECTED" : "DISCONNECTED";
        
        var currentRadioResponse = $"CURRENT_RADIO:{radio?.Manufacturer} {radio?.ModelName}|{actualPortName}|{connectionStatus}";
        
        // Assert
        Assert.Equal(expectedResponse, currentRadioResponse);
        
        // Cleanup
        await radioService.DisconnectRadioAsync();
        radioService.Dispose();
    }

    [Fact]
    public void RadioService_GetCurrentRadioInfo_DisconnectedRadio_ReturnsUnknownPort()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<RadioService>>();
        var radioService = new RadioService(mockLogger.Object);
        
        // Act - Get info without connecting any radio
        var radio = radioService.ConnectedRadio;
        var portName = radioService.ConnectedPortName ?? "Unknown";
        var connectionStatus = radio?.IsConnected == true ? "CONNECTED" : "DISCONNECTED";
        
        var currentRadioResponse = $"CURRENT_RADIO:{radio?.Manufacturer ?? "None"} {radio?.ModelName ?? "None"}|{portName}|{connectionStatus}";
        
        // Assert
        Assert.Equal("CURRENT_RADIO:None None|Unknown|DISCONNECTED", currentRadioResponse);
        
        // Cleanup
        radioService.Dispose();
    }
}