using Moq;
using SharpCAT2.ClientLibrary;
using Xunit;

namespace SharpCAT2.Tests.ClientLibrary;

/// <summary>
/// Tests for the ClientLibrary unified resource listing functionality.
/// Updated to reflect protocol marker filtering behavior.
/// </summary>
public class SharpCAT2ClientResourceTests
{
    [Fact]
    public void GetAvailableSerialPortsAsync_ShouldExist()
    {
        // Arrange
        var client = new SharpCAT2Client("localhost", 8080);

        // Act & Assert
        var method = typeof(SharpCAT2Client).GetMethod("GetAvailableSerialPortsAsync");
        Assert.NotNull(method);
        Assert.Equal(typeof(Task<string?>), method.ReturnType);
    }

    [Fact]
    public void GetAvailableRadiosAsync_ShouldExist()
    {
        // Arrange
        var client = new SharpCAT2Client("localhost", 8080);

        // Act & Assert
        var method = typeof(SharpCAT2Client).GetMethod("GetAvailableRadiosAsync");
        Assert.NotNull(method);
        Assert.Equal(typeof(Task<string?>), method.ReturnType);
    }

    [Fact]
    public void GetAvailableRadioEntriesAsync_ShouldExist()
    {
        // Arrange
        var client = new SharpCAT2Client("localhost", 8080);

        // Act & Assert
        var method = typeof(SharpCAT2Client).GetMethod("GetAvailableRadioEntriesAsync");
        Assert.NotNull(method);
        Assert.Equal(typeof(Task<string[]>), method.ReturnType);
    }

    [Fact]
    public void GetAvailableSerialPortEntriesAsync_ShouldExist()
    {
        // Arrange
        var client = new SharpCAT2Client("localhost", 8080);

        // Act & Assert
        var method = typeof(SharpCAT2Client).GetMethod("GetAvailableSerialPortEntriesAsync");
        Assert.NotNull(method);
        Assert.Equal(typeof(Task<string[]>), method.ReturnType);
    }

    [Fact]
    public void ClientLibrary_FilteringBehavior_ShouldReturnCleanData()
    {
        // This test documents the new filtering behavior:
        // ClientLibrary methods should return clean data without protocol markers
        
        // Test the filtering utility behavior directly since we can't easily mock network calls
        var sampleRadioResponse = @"RADIO_LIST_START
Test Radio|10
RADIO_LIST_END";

        var samplePortResponse = @"SERIALPORT_LIST_START
TEST_PORT
SERIALPORT_LIST_END";

        // Act
        var filteredRadios = ProtocolListFilter.FilterRadioList(sampleRadioResponse);
        var filteredPorts = ProtocolListFilter.FilterSerialPortList(samplePortResponse);

        // Assert - Verify no protocol markers are present
        Assert.DoesNotContain("RADIO_LIST_START", filteredRadios ?? "");
        Assert.DoesNotContain("RADIO_LIST_END", filteredRadios ?? "");
        Assert.DoesNotContain("SERIALPORT_LIST_START", filteredPorts ?? "");
        Assert.DoesNotContain("SERIALPORT_LIST_END", filteredPorts ?? "");

        // Assert - Verify content is preserved
        Assert.Contains("Test Radio|10", filteredRadios ?? "");
        Assert.Contains("TEST_PORT", filteredPorts ?? "");
    }
}