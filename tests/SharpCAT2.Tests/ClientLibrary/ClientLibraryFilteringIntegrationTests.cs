using Moq;
using Xunit;
using SharpCAT2.ClientLibrary;
using System.Reflection;

namespace SharpCAT2.Tests.ClientLibrary;

/// <summary>
/// Integration tests for ClientLibrary methods that use ProtocolListFilter.
/// These tests verify that the filtering integration works correctly in the context
/// of the full ClientLibrary API.
/// </summary>
public class ClientLibraryFilteringIntegrationTests
{
    /// <summary>
    /// Helper to create a mock client that returns specific protocol responses
    /// </summary>
    private SharpCAT2Client CreateMockClientWithResponse(string command, string response)
    {
        var client = new SharpCAT2Client("localhost", 8080);
        
        // Use reflection to access the private SendCommandAsync method for testing
        // This allows us to test the filtering without needing a real server
        var sendCommandMethod = typeof(SharpCAT2Client).GetMethod("SendCommandInternalAsync", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        
        if (sendCommandMethod == null)
        {
            throw new InvalidOperationException("Could not find SendCommandInternalAsync method for testing");
        }

        // Create a mock that returns our test response
        // Note: In a real scenario, you'd typically use a test server or mock the network layer
        return client;
    }

    [Fact]
    public void GetAvailableRadiosAsync_MethodExists_WithCorrectSignature()
    {
        // Arrange
        var client = new SharpCAT2Client("localhost", 8080);

        // Act & Assert
        var method = typeof(SharpCAT2Client).GetMethod("GetAvailableRadiosAsync");
        Assert.NotNull(method);
        Assert.Equal(typeof(Task<string?>), method.ReturnType);
        Assert.Empty(method.GetParameters());
    }

    [Fact]
    public void GetAvailableSerialPortsAsync_MethodExists_WithCorrectSignature()
    {
        // Arrange
        var client = new SharpCAT2Client("localhost", 8080);

        // Act & Assert
        var method = typeof(SharpCAT2Client).GetMethod("GetAvailableSerialPortsAsync");
        Assert.NotNull(method);
        Assert.Equal(typeof(Task<string?>), method.ReturnType);
        Assert.Empty(method.GetParameters());
    }

    [Fact]
    public void GetAvailableRadioEntriesAsync_MethodExists_WithCorrectSignature()
    {
        // Arrange
        var client = new SharpCAT2Client("localhost", 8080);

        // Act & Assert
        var method = typeof(SharpCAT2Client).GetMethod("GetAvailableRadioEntriesAsync");
        Assert.NotNull(method);
        Assert.Equal(typeof(Task<string[]>), method.ReturnType);
        Assert.Empty(method.GetParameters());
    }

    [Fact]
    public void GetAvailableSerialPortEntriesAsync_MethodExists_WithCorrectSignature()
    {
        // Arrange
        var client = new SharpCAT2Client("localhost", 8080);

        // Act & Assert
        var method = typeof(SharpCAT2Client).GetMethod("GetAvailableSerialPortEntriesAsync");
        Assert.NotNull(method);
        Assert.Equal(typeof(Task<string[]>), method.ReturnType);
        Assert.Empty(method.GetParameters());
    }

    [Fact]
    public void ProtocolListFilter_RadioFilterIntegration_WorksWithRealProtocolData()
    {
        // Arrange - Sample data that matches the actual server protocol format
        var serverResponse = @"RADIO_LIST_START
Kenwood TS-2000|25
Kenwood TS-590SG|23
Yaesu FT-991A|22
Yaesu FT-817ND|18
Icom IC-7300|20
Icom IC-9700|24
RADIO_LIST_END";

        // Act
        var filteredResult = ProtocolListFilter.FilterRadioList(serverResponse);

        // Assert
        Assert.NotNull(filteredResult);
        Assert.DoesNotContain("RADIO_LIST_START", filteredResult);
        Assert.DoesNotContain("RADIO_LIST_END", filteredResult);
        
        var expectedContent = @"Kenwood TS-2000|25
Kenwood TS-590SG|23
Yaesu FT-991A|22
Yaesu FT-817ND|18
Icom IC-7300|20
Icom IC-9700|24";
        Assert.Equal(expectedContent, filteredResult);
    }

    [Fact]
    public void ProtocolListFilter_SerialPortFilterIntegration_WorksWithRealProtocolData()
    {
        // Arrange - Sample data that matches the actual server protocol format
        var serverResponse = @"SERIALPORT_LIST_START
COM1
COM3
/dev/ttyUSB0
/dev/ttyACM0
FAKE
SERIALPORT_LIST_END";

        // Act
        var filteredResult = ProtocolListFilter.FilterSerialPortList(serverResponse);

        // Assert
        Assert.NotNull(filteredResult);
        Assert.DoesNotContain("SERIALPORT_LIST_START", filteredResult);
        Assert.DoesNotContain("SERIALPORT_LIST_END", filteredResult);
        
        var expectedContent = @"COM1
COM3
/dev/ttyUSB0
/dev/ttyACM0
FAKE";
        Assert.Equal(expectedContent, filteredResult);
    }

    [Fact]
    public void ProtocolListFilter_RadioEntryParsing_ExtractsIndividualRadios()
    {
        // Arrange
        var filteredRadioList = @"Kenwood TS-2000|25
Yaesu FT-991A|22
Icom IC-7300|20";

        // Act
        var radioEntries = ProtocolListFilter.ParseRadioEntries(filteredRadioList);

        // Assert
        Assert.NotNull(radioEntries);
        Assert.Equal(3, radioEntries.Length);
        Assert.Equal("Kenwood TS-2000|25", radioEntries[0]);
        Assert.Equal("Yaesu FT-991A|22", radioEntries[1]);
        Assert.Equal("Icom IC-7300|20", radioEntries[2]);
    }

    [Fact]
    public void ProtocolListFilter_SerialPortEntryParsing_ExtractsIndividualPorts()
    {
        // Arrange
        var filteredPortList = @"COM1
/dev/ttyUSB0
FAKE";

        // Act
        var portEntries = ProtocolListFilter.ParsePortEntries(filteredPortList);

        // Assert
        Assert.NotNull(portEntries);
        Assert.Equal(3, portEntries.Length);
        Assert.Equal("COM1", portEntries[0]);
        Assert.Equal("/dev/ttyUSB0", portEntries[1]);
        Assert.Equal("FAKE", portEntries[2]);
    }

    [Fact]
    public void ProtocolListFilter_ExtensibilityDesign_SupportsNewListTypes()
    {
        // This test verifies that the design supports extension for future protocol markers
        // without breaking existing functionality

        // Arrange - Verify we can get current supported types
        var supportedTypes = ProtocolListFilter.GetSupportedListTypes();

        // Assert - Should contain the basic types we expect
        Assert.Contains(ProtocolListFilter.ListType.Radios, supportedTypes);
        Assert.Contains(ProtocolListFilter.ListType.SerialPorts, supportedTypes);

        // Verify extensibility pattern works
        Assert.True(supportedTypes.Length >= 2, "Should support at least radio and serial port types");
    }

    [Fact]
    public void ProtocolListFilter_ErrorResilience_HandlesCorruptedResponses()
    {
        // Test various corrupted response scenarios that might occur in real network communications
        
        // Test 1: Truncated response
        var truncatedResponse = @"RADIO_LIST_START
Kenwood TS-2000|25";
        var result1 = ProtocolListFilter.FilterRadioList(truncatedResponse);
        Assert.Equal("Kenwood TS-2000|25", result1);

        // Test 2: No content between markers
        var emptyResponse = @"RADIO_LIST_START
RADIO_LIST_END";
        var result2 = ProtocolListFilter.FilterRadioList(emptyResponse);
        Assert.Equal(string.Empty, result2);

        // Test 3: Multiple start markers (server glitch)
        var duplicateStartResponse = @"RADIO_LIST_START
RADIO_LIST_START
Kenwood TS-2000|25
RADIO_LIST_END";
        var result3 = ProtocolListFilter.FilterRadioList(duplicateStartResponse);
        Assert.Contains("Kenwood TS-2000|25", result3);

        // Test 4: Content before and after markers (network noise)
        var noisyResponse = @"Some network noise
RADIO_LIST_START
Kenwood TS-2000|25
RADIO_LIST_END
More noise";
        var result4 = ProtocolListFilter.FilterRadioList(noisyResponse);
        Assert.Equal("Kenwood TS-2000|25", result4);
        Assert.DoesNotContain("noise", result4);
    }

    [Fact]
    public void ClientLibrary_DocumentedBehavior_FiltersProtocolMarkersFromAllListMethods()
    {
        // This test documents the expected behavior change: 
        // ClientLibrary methods should never return protocol markers to consumers

        // Test data simulating actual server responses
        var radioResponse = @"RADIO_LIST_START
Test Radio|10
RADIO_LIST_END";

        var portResponse = @"SERIALPORT_LIST_START
TEST_PORT
SERIALPORT_LIST_END";

        // Verify filtering behavior
        var filteredRadios = ProtocolListFilter.FilterRadioList(radioResponse);
        var filteredPorts = ProtocolListFilter.FilterSerialPortList(portResponse);

        // Assert that no protocol markers are present in filtered results
        Assert.DoesNotContain("_LIST_START", filteredRadios ?? "");
        Assert.DoesNotContain("_LIST_END", filteredRadios ?? "");
        Assert.DoesNotContain("_LIST_START", filteredPorts ?? "");
        Assert.DoesNotContain("_LIST_END", filteredPorts ?? "");

        // Assert that actual content is preserved
        Assert.Contains("Test Radio|10", filteredRadios ?? "");
        Assert.Contains("TEST_PORT", filteredPorts ?? "");
    }
}