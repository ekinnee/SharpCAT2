using Xunit;
using Xunit.Abstractions;
using SharpCAT2.ClientLibrary;

namespace SharpCAT2.Tests.ClientLibrary;

/// <summary>
/// Demonstration test that shows the filtering in action.
/// This test outputs the before and after of protocol filtering.
/// </summary>
public class FilteringDemonstrationTest
{
    private readonly ITestOutputHelper _output;

    public FilteringDemonstrationTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void DemonstrateProtocolFiltering()
    {
        // Arrange - Simulate actual server responses
        var radioServerResponse = @"RADIO_LIST_START
Kenwood TS-2000|25
Kenwood TS-590SG|23
Yaesu FT-991A|22
Yaesu FT-817ND|18
Icom IC-7300|20
Icom IC-9700|24
RADIO_LIST_END";

        var portServerResponse = @"SERIALPORT_LIST_START
COM1
COM3
/dev/ttyUSB0
/dev/ttyACM0
FAKE
SERIALPORT_LIST_END";

        _output.WriteLine("=== PROTOCOL FILTERING DEMONSTRATION ===");
        _output.WriteLine("");

        _output.WriteLine("BEFORE: Raw Server Response (Radios)");
        _output.WriteLine("------------------------------------");
        _output.WriteLine(radioServerResponse);
        _output.WriteLine("");

        // Act - Apply filtering
        var filteredRadios = ProtocolListFilter.FilterRadioList(radioServerResponse);
        var radioEntries = ProtocolListFilter.ParseRadioEntries(filteredRadios);

        _output.WriteLine("AFTER: Filtered Response (Radios)");
        _output.WriteLine("---------------------------------");
        _output.WriteLine(filteredRadios);
        _output.WriteLine("");

        _output.WriteLine("PARSED: Individual Radio Entries");
        _output.WriteLine("--------------------------------");
        foreach (var radio in radioEntries)
        {
            _output.WriteLine($"  • {radio}");
        }
        _output.WriteLine("");

        _output.WriteLine("BEFORE: Raw Server Response (Serial Ports)");
        _output.WriteLine("------------------------------------------");
        _output.WriteLine(portServerResponse);
        _output.WriteLine("");

        // Act - Apply filtering to ports
        var filteredPorts = ProtocolListFilter.FilterSerialPortList(portServerResponse);
        var portEntries = ProtocolListFilter.ParsePortEntries(filteredPorts);

        _output.WriteLine("AFTER: Filtered Response (Serial Ports)");
        _output.WriteLine("---------------------------------------");
        _output.WriteLine(filteredPorts);
        _output.WriteLine("");

        _output.WriteLine("PARSED: Individual Port Entries");
        _output.WriteLine("-------------------------------");
        foreach (var port in portEntries)
        {
            _output.WriteLine($"  • {port}");
        }
        _output.WriteLine("");

        // Assert - Verify filtering worked
        var hasRadioMarkers = filteredRadios?.Contains("RADIO_LIST") == true;
        var hasPortMarkers = filteredPorts?.Contains("SERIALPORT_LIST") == true;

        _output.WriteLine("VERIFICATION:");
        _output.WriteLine($"  ✓ Protocol markers removed from radios: {!hasRadioMarkers}");
        _output.WriteLine($"  ✓ Protocol markers removed from ports: {!hasPortMarkers}");
        _output.WriteLine($"  ✓ All content preserved: {radioEntries.Length == 6 && portEntries.Length == 5}");
        _output.WriteLine("");

        _output.WriteLine("IMPACT ON CLIENT CONSOLE:");
        _output.WriteLine("  • ClientConsole will now receive clean data without protocol markers");
        _output.WriteLine("  • No changes required in ClientConsole code");
        _output.WriteLine("  • Users see only radio names and port names, not internal protocol");
        _output.WriteLine("");

        // Assert
        Assert.False(hasRadioMarkers, "Radio response should not contain protocol markers");
        Assert.False(hasPortMarkers, "Port response should not contain protocol markers");
        Assert.Equal(6, radioEntries.Length);
        Assert.Equal(5, portEntries.Length);
    }
}