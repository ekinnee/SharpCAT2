using SharpCAT2.ServerLibrary.Radio.Models.Testing;
using SharpCAT2.ServerLibrary.Serial;
using SharpCAT2.ServerLibrary.Radio;
using SharpCAT2.Core.Radio;
using Xunit;
using System.Threading.Tasks;

namespace SharpCAT2.Tests.Radio;

/// <summary>
/// Tests for the enhanced DummyRadio CAT command support
/// Tests the newly added CAT commands and protocol-accurate responses
/// </summary>
public class DummyRadioEnhancedCATTests
{
    [Fact]
    public async Task DummyRadio_VFOBFrequencyCommands_ShouldWorkCorrectly()
    {
        // Arrange
        var radio = new DummyRadio();
        var fakePort = new FakeSerialPort("TEST", 9600);
        await radio.ConnectAsync(fakePort);

        // Act - Get initial VFO B frequency
        var getFreqCommand = new RadioCommand("FB;", "Get VFO B Frequency");
        var freqResponse = await radio.SendCommandAsync(getFreqCommand);

        // Assert initial frequency
        Assert.NotNull(freqResponse);
        Assert.Equal("FB00014074000;", freqResponse); // Default frequency

        // Act - Set VFO B frequency
        var setFreqCommand = new RadioCommand("FB00007074000;", "Set VFO B Frequency");
        var setResponse = await radio.SendCommandAsync(setFreqCommand);

        // Assert set response
        Assert.NotNull(setResponse);
        Assert.Equal("FB00007074000;", setResponse);

        // Act - Verify VFO B frequency was set
        var verifyFreqCommand = new RadioCommand("FB;", "Get VFO B Frequency");
        var verifyResponse = await radio.SendCommandAsync(verifyFreqCommand);

        // Assert frequency changed
        Assert.NotNull(verifyResponse);
        Assert.Equal("FB00007074000;", verifyResponse);

        // Cleanup
        radio.Disconnect();
    }

    [Fact]
    public async Task DummyRadio_RITCommands_ShouldWorkWithRTCommand()
    {
        // Arrange
        var radio = new DummyRadio();
        var fakePort = new FakeSerialPort("TEST", 9600);
        await radio.ConnectAsync(fakePort);

        // Act - Get initial RIT offset
        var getRitCommand = new RadioCommand("RT;", "Get RIT Offset");
        var ritResponse = await radio.SendCommandAsync(getRitCommand);

        // Assert initial RIT
        Assert.NotNull(ritResponse);
        Assert.Equal("RT+0000;", ritResponse); // Default offset

        // Act - Set RIT offset
        var setRitCommand = new RadioCommand("RT+0150;", "Set RIT Offset");
        var setResponse = await radio.SendCommandAsync(setRitCommand);

        // Assert set response
        Assert.NotNull(setResponse);
        Assert.Equal("RT+0150;", setResponse);

        // Act - Verify RIT offset was set
        var verifyRitCommand = new RadioCommand("RT;", "Get RIT Offset");
        var verifyResponse = await radio.SendCommandAsync(verifyRitCommand);

        // Assert offset changed
        Assert.NotNull(verifyResponse);
        Assert.Equal("RT+0150;", verifyResponse);

        // Cleanup
        radio.Disconnect();
    }

    [Fact]
    public async Task DummyRadio_XITCommands_ShouldWorkWithXTCommand()
    {
        // Arrange
        var radio = new DummyRadio();
        var fakePort = new FakeSerialPort("TEST", 9600);
        await radio.ConnectAsync(fakePort);

        // Act - Get initial XIT offset
        var getXitCommand = new RadioCommand("XT;", "Get XIT Offset");
        var xitResponse = await radio.SendCommandAsync(getXitCommand);

        // Assert initial XIT
        Assert.NotNull(xitResponse);
        Assert.Equal("XT+0000;", xitResponse); // Default offset

        // Act - Set XIT offset  
        var setXitCommand = new RadioCommand("XT-0075;", "Set XIT Offset");
        var setResponse = await radio.SendCommandAsync(setXitCommand);

        // Assert set response
        Assert.NotNull(setResponse);
        Assert.Equal("XT-0075;", setResponse);

        // Act - Verify XIT offset was set
        var verifyXitCommand = new RadioCommand("XT;", "Get XIT Offset");
        var verifyResponse = await radio.SendCommandAsync(verifyXitCommand);

        // Assert offset changed
        Assert.NotNull(verifyResponse);
        Assert.Equal("XT-0075;", verifyResponse);

        // Cleanup
        radio.Disconnect();
    }

    [Fact]
    public async Task DummyRadio_AntennaCommands_ShouldWorkCorrectly()
    {
        // Arrange
        var radio = new DummyRadio();
        var fakePort = new FakeSerialPort("TEST", 9600);
        await radio.ConnectAsync(fakePort);

        // Act - Get initial antenna
        var getAntennaCommand = new RadioCommand("AN;", "Get Antenna");
        var antennaResponse = await radio.SendCommandAsync(getAntennaCommand);

        // Assert initial antenna
        Assert.NotNull(antennaResponse);
        Assert.Equal("AN1;", antennaResponse); // Default antenna 1

        // Act - Set antenna to 2
        var setAntennaCommand = new RadioCommand("AN2;", "Set Antenna");
        var setResponse = await radio.SendCommandAsync(setAntennaCommand);

        // Assert set response
        Assert.NotNull(setResponse);
        Assert.Equal("AN2;", setResponse);

        // Act - Verify antenna was set
        var verifyAntennaCommand = new RadioCommand("AN;", "Get Antenna");
        var verifyResponse = await radio.SendCommandAsync(verifyAntennaCommand);

        // Assert antenna changed
        Assert.NotNull(verifyResponse);
        Assert.Equal("AN2;", verifyResponse);

        // Cleanup
        radio.Disconnect();
    }

    [Fact]
    public async Task DummyRadio_RadioStatusCommand_ShouldReturnCorrectFormat()
    {
        // Arrange
        var radio = new DummyRadio();
        var fakePort = new FakeSerialPort("TEST", 9600);
        await radio.ConnectAsync(fakePort);

        // Act - Get radio status
        var statusCommand = new RadioCommand("RS;", "Get Radio Status");
        var statusResponse = await radio.SendCommandAsync(statusCommand);

        // Assert status format
        Assert.NotNull(statusResponse);
        Assert.StartsWith("RS", statusResponse);
        Assert.EndsWith(";", statusResponse);
        Assert.Equal(10, statusResponse.Length); // RS + 7 characters + ;

        // Cleanup
        radio.Disconnect();
    }

    [Fact]
    public async Task DummyRadio_MemoryChannelCommands_ShouldWorkCorrectly()
    {
        // Arrange
        var radio = new DummyRadio();
        var fakePort = new FakeSerialPort("TEST", 9600);
        await radio.ConnectAsync(fakePort);

        // Act - Get initial memory channel
        var getMemoryCommand = new RadioCommand("MC;", "Get Memory Channel");
        var memoryResponse = await radio.SendCommandAsync(getMemoryCommand);

        // Assert initial memory channel
        Assert.NotNull(memoryResponse);
        Assert.Equal("MC000;", memoryResponse); // Default channel 0

        // Act - Set memory channel
        var setMemoryCommand = new RadioCommand("MC042;", "Set Memory Channel");
        var setResponse = await radio.SendCommandAsync(setMemoryCommand);

        // Assert set response
        Assert.NotNull(setResponse);
        Assert.Equal("MC042;", setResponse);

        // Act - Verify memory channel was set
        var verifyMemoryCommand = new RadioCommand("MC;", "Get Memory Channel");
        var verifyResponse = await radio.SendCommandAsync(verifyMemoryCommand);

        // Assert channel changed
        Assert.NotNull(verifyResponse);
        Assert.Equal("MC042;", verifyResponse);

        // Cleanup
        radio.Disconnect();
    }

    [Fact]
    public async Task DummyRadio_CWSpeedCommands_ShouldWorkCorrectly()
    {
        // Arrange
        var radio = new DummyRadio();
        var fakePort = new FakeSerialPort("TEST", 9600);
        await radio.ConnectAsync(fakePort);

        // Act - Get initial CW speed
        var getSpeedCommand = new RadioCommand("KS;", "Get CW Speed");
        var speedResponse = await radio.SendCommandAsync(getSpeedCommand);

        // Assert initial speed
        Assert.NotNull(speedResponse);
        Assert.Equal("KS020;", speedResponse); // Default 20 WPM

        // Act - Set CW speed
        var setSpeedCommand = new RadioCommand("KS035;", "Set CW Speed");
        var setResponse = await radio.SendCommandAsync(setSpeedCommand);

        // Assert set response
        Assert.NotNull(setResponse);
        Assert.Equal("KS035;", setResponse);

        // Act - Verify speed was set
        var verifySpeedCommand = new RadioCommand("KS;", "Get CW Speed");
        var verifyResponse = await radio.SendCommandAsync(verifySpeedCommand);

        // Assert speed changed
        Assert.NotNull(verifyResponse);
        Assert.Equal("KS035;", verifyResponse);

        // Cleanup
        radio.Disconnect();
    }

    [Fact]
    public async Task DummyRadio_CWMessageCommand_ShouldEchoMessage()
    {
        // Arrange
        var radio = new DummyRadio();
        var fakePort = new FakeSerialPort("TEST", 9600);
        await radio.ConnectAsync(fakePort);

        // Act - Send CW message
        var messageCommand = new RadioCommand("KY TEST MESSAGE;", "Send CW Message");
        var messageResponse = await radio.SendCommandAsync(messageCommand);

        // Assert message echoed
        Assert.NotNull(messageResponse);
        Assert.Equal("KY TEST MESSAGE;", messageResponse);

        // Cleanup
        radio.Disconnect();
    }

    [Fact]
    public async Task DummyRadio_NoiseReductionCommands_ShouldWorkCorrectly()
    {
        // Arrange
        var radio = new DummyRadio();
        var fakePort = new FakeSerialPort("TEST", 9600);
        await radio.ConnectAsync(fakePort);

        // Act - Get initial noise reduction
        var getNRCommand = new RadioCommand("NR;", "Get Noise Reduction");
        var nrResponse = await radio.SendCommandAsync(getNRCommand);

        // Assert initial NR
        Assert.NotNull(nrResponse);
        Assert.Equal("NR00;", nrResponse); // Default level 0

        // Act - Set noise reduction
        var setNRCommand = new RadioCommand("NR05;", "Set Noise Reduction");
        var setResponse = await radio.SendCommandAsync(setNRCommand);

        // Assert set response
        Assert.NotNull(setResponse);
        Assert.Equal("NR05;", setResponse);

        // Act - Verify NR was set
        var verifyNRCommand = new RadioCommand("NR;", "Get Noise Reduction");
        var verifyResponse = await radio.SendCommandAsync(verifyNRCommand);

        // Assert level changed
        Assert.NotNull(verifyResponse);
        Assert.Equal("NR05;", verifyResponse);

        // Cleanup
        radio.Disconnect();
    }

    [Fact]
    public async Task DummyRadio_IFBandwidthCommands_ShouldWorkCorrectly()
    {
        // Arrange
        var radio = new DummyRadio();
        var fakePort = new FakeSerialPort("TEST", 9600);
        await radio.ConnectAsync(fakePort);

        // Act - Get initial IF bandwidth
        var getBWCommand = new RadioCommand("BW;", "Get IF Bandwidth");
        var bwResponse = await radio.SendCommandAsync(getBWCommand);

        // Assert initial bandwidth
        Assert.NotNull(bwResponse);
        Assert.Equal("BW2400;", bwResponse); // Default 2400 Hz

        // Act - Set IF bandwidth
        var setBWCommand = new RadioCommand("BW1800;", "Set IF Bandwidth");
        var setResponse = await radio.SendCommandAsync(setBWCommand);

        // Assert set response
        Assert.NotNull(setResponse);
        Assert.Equal("BW1800;", setResponse);

        // Act - Verify bandwidth was set
        var verifyBWCommand = new RadioCommand("BW;", "Get IF Bandwidth");
        var verifyResponse = await radio.SendCommandAsync(verifyBWCommand);

        // Assert bandwidth changed
        Assert.NotNull(verifyResponse);
        Assert.Equal("BW1800;", verifyResponse);

        // Cleanup
        radio.Disconnect();
    }

    [Fact]
    public async Task DummyRadio_SWRMeterCommand_ShouldReturnCorrectFormat()
    {
        // Arrange
        var radio = new DummyRadio();
        var fakePort = new FakeSerialPort("TEST", 9600);
        await radio.ConnectAsync(fakePort);

        // Act - Get SWR reading
        var swrCommand = new RadioCommand("RM3;", "Get SWR");
        var swrResponse = await radio.SendCommandAsync(swrCommand);

        // Assert SWR format
        Assert.NotNull(swrResponse);
        Assert.StartsWith("RM3", swrResponse);
        Assert.EndsWith(";", swrResponse);
        Assert.Equal(7, swrResponse.Length); // RM3 + 3 digits + ;

        // Cleanup
        radio.Disconnect();
    }

    [Fact]
    public async Task DummyRadio_VFOSwapCommand_ShouldSwapFrequencies()
    {
        // Arrange
        var radio = new DummyRadio();
        var fakePort = new FakeSerialPort("TEST", 9600);
        await radio.ConnectAsync(fakePort);

        // Set different frequencies for VFO A and B
        await radio.SendCommandAsync(new RadioCommand("FA00014074000;", "Set VFO A"));
        await radio.SendCommandAsync(new RadioCommand("FB00007074000;", "Set VFO B"));

        // Act - Swap VFOs
        var swapCommand = new RadioCommand("SV;", "Swap VFO");
        var swapResponse = await radio.SendCommandAsync(swapCommand);

        // Assert swap response
        Assert.NotNull(swapResponse);
        Assert.Equal("SV;", swapResponse);

        // Act - Check frequencies are swapped
        var vfoAResponse = await radio.SendCommandAsync(new RadioCommand("FA;", "Get VFO A"));
        var vfoBResponse = await radio.SendCommandAsync(new RadioCommand("FB;", "Get VFO B"));

        // Assert frequencies swapped
        Assert.Equal("FA00007074000;", vfoAResponse);
        Assert.Equal("FB00014074000;", vfoBResponse);

        // Cleanup
        radio.Disconnect();
    }

    [Fact]
    public async Task DummyRadio_VFOEqualCommand_ShouldCopyVFOA()
    {
        // Arrange
        var radio = new DummyRadio();
        var fakePort = new FakeSerialPort("TEST", 9600);
        await radio.ConnectAsync(fakePort);

        // Set different frequencies for VFO A and B
        await radio.SendCommandAsync(new RadioCommand("FA00014074000;", "Set VFO A"));
        await radio.SendCommandAsync(new RadioCommand("FB00007074000;", "Set VFO B"));

        // Act - VFO equal (copy A to B)
        var equalCommand = new RadioCommand("VV;", "VFO Equal");
        var equalResponse = await radio.SendCommandAsync(equalCommand);

        // Assert equal response
        Assert.NotNull(equalResponse);
        Assert.Equal("VV;", equalResponse);

        // Act - Check VFO B now matches VFO A
        var vfoAResponse = await radio.SendCommandAsync(new RadioCommand("FA;", "Get VFO A"));
        var vfoBResponse = await radio.SendCommandAsync(new RadioCommand("FB;", "Get VFO B"));

        // Assert VFO B copied from VFO A
        Assert.Equal("FA00014074000;", vfoAResponse);
        Assert.Equal("FB00014074000;", vfoBResponse);

        // Cleanup
        radio.Disconnect();
    }

    [Fact]
    public async Task DummyRadio_EnhancedIFCommand_ShouldReturnAccurateFormat()
    {
        // Arrange
        var radio = new DummyRadio();
        var fakePort = new FakeSerialPort("TEST", 9600);
        await radio.ConnectAsync(fakePort);

        // Set some state for testing
        await radio.SendCommandAsync(new RadioCommand("RT+0100;", "Set RIT"));
        await radio.SendCommandAsync(new RadioCommand("FT1;", "Enable Split"));
        await radio.SendCommandAsync(new RadioCommand("MC005;", "Set Memory Channel"));

        // Act - Get transceiver info
        var ifCommand = new RadioCommand("IF;", "Get Transceiver Info");
        var ifResponse = await radio.SendCommandAsync(ifCommand);

        // Assert IF format matches Kenwood specification
        Assert.NotNull(ifResponse);
        Assert.StartsWith("IF", ifResponse);
        Assert.EndsWith(";", ifResponse);
        
        // Should contain frequency, RIT offset, flags, memory channel, etc.
        Assert.Contains("00014074000", ifResponse); // Frequency
        Assert.Contains("+0100", ifResponse); // RIT offset
        Assert.Contains("005", ifResponse); // Memory channel
        
        // Check specific positions for flags
        Assert.True(ifResponse.Length >= 38, "IF response should be at least 38 characters");

        // Cleanup
        radio.Disconnect();
    }

    [Fact]
    public async Task DummyRadio_SMeterVariations_ShouldSupportDifferentCommands()
    {
        // Arrange
        var radio = new DummyRadio();
        var fakePort = new FakeSerialPort("TEST", 9600);
        await radio.ConnectAsync(fakePort);

        // Act - Test different S-meter commands
        var sm0Response = await radio.SendCommandAsync(new RadioCommand("SM0;", "S-meter Main"));
        var sm1Response = await radio.SendCommandAsync(new RadioCommand("SM1;", "S-meter Sub"));
        var smResponse = await radio.SendCommandAsync(new RadioCommand("SM;", "S-meter Generic"));

        // Assert all return valid S-meter readings
        Assert.NotNull(sm0Response);
        Assert.StartsWith("SM0", sm0Response);
        Assert.EndsWith(";", sm0Response);

        Assert.NotNull(sm1Response);
        Assert.StartsWith("SM1", sm1Response);
        Assert.EndsWith(";", sm1Response);

        Assert.NotNull(smResponse);
        Assert.StartsWith("SM0", smResponse); // Generic defaults to SM0
        Assert.EndsWith(";", smResponse);

        // Cleanup
        radio.Disconnect();
    }
}