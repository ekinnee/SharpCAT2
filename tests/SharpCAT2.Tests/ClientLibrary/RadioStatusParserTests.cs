using SharpCAT2.ClientLibrary;
using Xunit;

namespace SharpCAT2.Tests.ClientLibrary;

public class RadioStatusParserTests
{
    [Fact]
    public void ParseAndFormat_WithValidKeyValueString_ReturnsFormattedOutput()
    {
        // Arrange
        var statusString = "MODEL=SharpCAT2 DummyRadio;PORT=FAKE;FREQ=14074000;MODE=USB;VFO=A;POWER=True;TX=False;SPLIT=False;RIT=False;XIT=False;POWER_LEVEL=50;S_METER=5;SWR=1.2;ANTENNA=1;TIMESTAMP=2025-08-20T15:30:00Z";
        
        // Act
        var result = RadioStatusParser.ParseAndFormat(statusString);
        
        // Assert
        Assert.NotNull(result);
        Assert.Contains("Radio Status:", result);
        Assert.Contains("SharpCAT2 DummyRadio", result);
        Assert.Contains("14,074,000 Hz", result);
        Assert.Contains("14.07400 MHz", result);
        Assert.Contains("USB", result);
        Assert.Contains("FAKE", result);
        Assert.Contains("S5", result);
        Assert.Contains("1.2:1", result);
    }

    [Fact]
    public void ParseAndFormat_WithErrorResponse_ReturnsErrorMessage()
    {
        // Arrange
        var errorString = "ERROR: No radio connected";
        
        // Act
        var result = RadioStatusParser.ParseAndFormat(errorString);
        
        // Assert
        Assert.NotNull(result);
        Assert.Contains("Radio Status Error:", result);
        Assert.Contains("No radio connected", result);
    }

    [Fact]
    public void ParseAndFormat_WithEmptyString_ReturnsNoStatusMessage()
    {
        // Arrange
        var emptyString = "";
        
        // Act
        var result = RadioStatusParser.ParseAndFormat(emptyString);
        
        // Assert
        Assert.NotNull(result);
        Assert.Contains("No radio status available", result);
    }

    [Fact]
    public void ParseAndFormat_WithPowerOff_ShowsCorrectPowerStatus()
    {
        // Arrange
        var statusString = "MODEL=Test Radio;POWER=False;TX=False";
        
        // Act
        var result = RadioStatusParser.ParseAndFormat(statusString);
        
        // Assert
        Assert.Contains("Power:            OFF", result);
        Assert.Contains("Transmitting:     NO", result);
    }

    [Fact]
    public void ParseAndFormat_WithSMeterValue_FormatsCorrectly()
    {
        // Arrange
        var statusString = "MODEL=Test Radio;S_METER=12";
        
        // Act
        var result = RadioStatusParser.ParseAndFormat(statusString);
        
        // Assert
        Assert.Contains("S-Meter:          S9+3dB", result);
    }
}