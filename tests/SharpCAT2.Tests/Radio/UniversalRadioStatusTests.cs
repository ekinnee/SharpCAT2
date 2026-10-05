using SharpCAT2.ServerLibrary.Radio.Models.Testing;
using SharpCAT2.ServerLibrary.Serial;
using SharpCAT2.Core.Radio;
using Xunit;

namespace SharpCAT2.Tests.Radio;

public class UniversalRadioStatusTests
{
    [Fact]
    public async Task GetUniversalStatusStringAsync_WithDummyRadio_ReturnsFormattedKeyValueString()
    {
        // Arrange
        var dummyRadio = new DummyRadio();
        var fakePort = new FakeSerialPort("FAKE");
        await dummyRadio.ConnectAsync(fakePort);
        
        // Act
        var statusString = await dummyRadio.GetUniversalStatusStringAsync();
        
        // Assert
        Assert.NotNull(statusString);
        Assert.Contains("MODEL=SharpCAT2 DummyRadio", statusString);
        Assert.Contains("PORT=FAKE", statusString);
        Assert.Contains("FREQ=", statusString);
        Assert.Contains("MODE=", statusString);
        Assert.Contains("VFO=", statusString);
        Assert.Contains("POWER=", statusString);
        Assert.Contains("TX=", statusString);
        Assert.Contains("TIMESTAMP=", statusString);
        
        // Verify it's semicolon-delimited
        var parts = statusString.Split(';');
        Assert.True(parts.Length > 5, "Should have multiple key=value pairs");
        
        // Verify each part has key=value format
        foreach (var part in parts.Where(p => !string.IsNullOrEmpty(p)))
        {
            Assert.Contains("=", part);
            var kvp = part.Split('=');
            Assert.Equal(2, kvp.Length);
            Assert.NotEmpty(kvp[0]); // Key should not be empty
            Assert.NotEmpty(kvp[1]); // Value should not be empty
        }
        
        // Clean up
        dummyRadio.Disconnect();
        dummyRadio.Dispose();
    }

    [Fact]
    public async Task GetUniversalStatusStringAsync_WithDisconnectedRadio_ReportsMissingObservation()
    {
        // Arrange
        var dummyRadio = new DummyRadio();
        
        // Act (without connecting)
        var statusString = await dummyRadio.GetUniversalStatusStringAsync();
        
        // Assert
        Assert.NotNull(statusString);
        Assert.Contains("MODEL=SharpCAT2 DummyRadio", statusString);
        Assert.Contains("PORT=Unknown", statusString);
        Assert.Contains("ERROR=Status retrieval failed", statusString);
        Assert.DoesNotContain("FREQ=0", statusString);
        Assert.DoesNotContain("MODE=USB", statusString);
        
        // Clean up
        dummyRadio.Dispose();
    }

    [Fact]
    public async Task GetUniversalStatusStringAsync_VerifyFeatureBasedInclusion()
    {
        // Arrange
        var dummyRadio = new DummyRadio();
        var fakePort = new FakeSerialPort("FAKE");
        await dummyRadio.ConnectAsync(fakePort);
        
        // Act
        var statusString = await dummyRadio.GetUniversalStatusStringAsync();
        
        // Assert - Check that features supported by DummyRadio are included
        var supportedFeatures = dummyRadio.SupportedFeatures;
        
        if (supportedFeatures.HasFeature(SupportedFeatures.SplitOperation))
        {
            Assert.Contains("SPLIT=", statusString);
        }
        
        if (supportedFeatures.HasFeature(SupportedFeatures.RIT))
        {
            Assert.Contains("RIT=", statusString);
            Assert.Contains("RIT_OFFSET=", statusString);
        }
        
        if (supportedFeatures.HasFeature(SupportedFeatures.XIT))
        {
            Assert.Contains("XIT=", statusString);
            Assert.Contains("XIT_OFFSET=", statusString);
        }
        
        if (supportedFeatures.HasFeature(SupportedFeatures.PowerOutput))
        {
            Assert.Contains("POWER_LEVEL=", statusString);
        }
        
        // Clean up
        dummyRadio.Disconnect();
        dummyRadio.Dispose();
    }
}