using SharpCAT2.ServerLibrary.Radio.Models.Testing;
using SharpCAT2.ServerLibrary.Serial;
using SharpCAT2.ServerLibrary.Radio;
using SharpCAT2.Core.Radio;
using Xunit;
using System.Threading.Tasks;

namespace SharpCAT2.Tests.Radio;

/// <summary>
/// Tests for the refactored DummyRadio to verify it contains all radio simulation logic
/// and uses FakeSerialPort only as a transport mechanism.
/// </summary>
public class DummyRadioRefactoredTests
{
    [Fact]
    public void DummyRadio_BasicProperties_ShouldBeCorrect()
    {
        // Arrange
        var radio = new DummyRadio();

        // Assert
        Assert.Equal("DummyRadio", radio.ModelName);
        Assert.Equal("SharpCAT2", radio.Manufacturer);
        Assert.False(radio.IsConnected);
    }

    [Fact]
    public void DummyRadio_SupportedFeatures_ShouldIncludeComprehensiveFeatures()
    {
        // Arrange
        var radio = new DummyRadio();

        // Assert
        var features = radio.SupportedFeatures;
        Assert.True(features.HasFlag(SupportedFeatures.FrequencyControl));
        Assert.True(features.HasFlag(SupportedFeatures.ModeControl));
        Assert.True(features.HasFlag(SupportedFeatures.DualVFO));
        Assert.True(features.HasFlag(SupportedFeatures.SplitOperation));
        Assert.True(features.HasFlag(SupportedFeatures.RIT));
        Assert.True(features.HasFlag(SupportedFeatures.XIT));
        Assert.True(features.HasFlag(SupportedFeatures.PowerOnOff));
    }

    [Fact]
    public async Task DummyRadio_ConnectWithFakePort_ShouldWork()
    {
        // Arrange
        var radio = new DummyRadio();
        var fakePort = new FakeSerialPort("TEST", 9600);

        // Act
        var result = await radio.ConnectAsync(fakePort);

        // Assert
        Assert.True(result);
        Assert.True(radio.IsConnected);

        // Cleanup
        radio.Disconnect();
    }

    [Fact]
    public async Task DummyRadio_SendIDCommand_ShouldReturnSimulatedResponse()
    {
        // Arrange
        var radio = new DummyRadio();
        var fakePort = new FakeSerialPort("TEST", 9600);
        await radio.ConnectAsync(fakePort);

        // Act
        var command = new RadioCommand("ID;", "Get ID");
        var response = await radio.SendCommandAsync(command);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("ID020;", response); // Should return TS-2000 compatible ID

        // Cleanup
        radio.Disconnect();
    }

    [Fact]
    public async Task DummyRadio_FrequencyCommands_ShouldWorkCorrectly()
    {
        // Arrange
        var radio = new DummyRadio();
        var fakePort = new FakeSerialPort("TEST", 9600);
        await radio.ConnectAsync(fakePort);

        // Act - Get current frequency
        var getFreqCommand = new RadioCommand("FA;", "Get Frequency");
        var freqResponse = await radio.SendCommandAsync(getFreqCommand);

        // Assert initial frequency
        Assert.NotNull(freqResponse);
        Assert.Equal("FA00014074000;", freqResponse); // Default frequency

        // Act - Set frequency
        var setFreqCommand = new RadioCommand("FA00007074000;", "Set Frequency");
        var setResponse = await radio.SendCommandAsync(setFreqCommand);

        // Assert set response
        Assert.NotNull(setResponse);
        Assert.Equal("FA00007074000;", setResponse);

        // Act - Verify frequency was set
        var verifyFreqCommand = new RadioCommand("FA;", "Get Frequency");
        var verifyResponse = await radio.SendCommandAsync(verifyFreqCommand);

        // Assert frequency changed
        Assert.NotNull(verifyResponse);
        Assert.Equal("FA00007074000;", verifyResponse);

        // Cleanup
        radio.Disconnect();
    }

    [Fact]
    public async Task DummyRadio_ModeCommands_ShouldWorkCorrectly()
    {
        // Arrange
        var radio = new DummyRadio();
        var fakePort = new FakeSerialPort("TEST", 9600);
        await radio.ConnectAsync(fakePort);

        // Act - Get current mode
        var getModeCommand = new RadioCommand("MD;", "Get Mode");
        var modeResponse = await radio.SendCommandAsync(getModeCommand);

        // Assert initial mode
        Assert.NotNull(modeResponse);
        Assert.Equal("MD2;", modeResponse); // USB mode

        // Act - Set mode to LSB
        var setModeCommand = new RadioCommand("MD1;", "Set Mode");
        var setResponse = await radio.SendCommandAsync(setModeCommand);

        // Assert set response
        Assert.NotNull(setResponse);
        Assert.Equal("MD1;", setResponse);

        // Act - Verify mode was set
        var verifyModeCommand = new RadioCommand("MD;", "Get Mode");
        var verifyResponse = await radio.SendCommandAsync(verifyModeCommand);

        // Assert mode changed
        Assert.NotNull(verifyResponse);
        Assert.Equal("MD1;", verifyResponse); // LSB mode

        // Cleanup
        radio.Disconnect();
    }

    [Fact]
    public async Task DummyRadio_VFOCommands_ShouldWorkCorrectly()
    {
        // Arrange
        var radio = new DummyRadio();
        var fakePort = new FakeSerialPort("TEST", 9600);
        await radio.ConnectAsync(fakePort);

        // Act - Get current VFO
        var currentVfo = await radio.GetVfoAsync();
        Assert.Equal("A", currentVfo);

        // Act - Set VFO to B
        var result = await radio.SetVfoAsync("B");
        Assert.True(result);

        // Act - Verify VFO changed
        var newVfo = await radio.GetVfoAsync();
        Assert.Equal("B", newVfo);

        // Cleanup
        radio.Disconnect();
    }

    [Fact]
    public async Task DummyRadio_SplitCommands_ShouldWorkCorrectly()
    {
        // Arrange
        var radio = new DummyRadio();
        var fakePort = new FakeSerialPort("TEST", 9600);
        await radio.ConnectAsync(fakePort);

        // Act - Check initial split status
        var initialSplit = await radio.GetSplitAsync();
        Assert.False(initialSplit);

        // Act - Enable split
        var result = await radio.SetSplitAsync(true);
        Assert.True(result);

        // Act - Verify split enabled
        var splitEnabled = await radio.GetSplitAsync();
        Assert.True(splitEnabled);

        // Cleanup
        radio.Disconnect();
    }

    [Fact]
    public async Task DummyRadio_PowerCommands_ShouldWorkCorrectly()
    {
        // Arrange
        var radio = new DummyRadio();
        var fakePort = new FakeSerialPort("TEST", 9600);
        await radio.ConnectAsync(fakePort);

        // Act - Check initial power status
        var initialPower = await radio.GetPowerAsync();
        Assert.True(initialPower); // Should be on by default

        // Act - Get power output level
        var powerLevel = await radio.GetPowerOutputAsync();
        Assert.Equal(100, powerLevel); // Default power level

        // Act - Set power output
        var result = await radio.SetPowerOutputAsync(50);
        Assert.True(result);

        // Act - Verify power output changed
        var newPowerLevel = await radio.GetPowerOutputAsync();
        Assert.Equal(50, newPowerLevel);

        // Cleanup
        radio.Disconnect();
    }

    [Fact]
    public async Task DummyRadio_RITCommands_ShouldWorkCorrectly()
    {
        // Arrange
        var radio = new DummyRadio();
        var fakePort = new FakeSerialPort("TEST", 9600);
        await radio.ConnectAsync(fakePort);

        // Act - Check initial RIT offset
        var initialRit = await radio.GetRitAsync();
        Assert.Equal(0, initialRit);

        // Act - Set RIT offset
        var result = await radio.SetRitAsync(100);
        Assert.True(result);

        // Act - Verify RIT offset changed
        var newRit = await radio.GetRitAsync();
        Assert.Equal(100, newRit);

        // Cleanup
        radio.Disconnect();
    }

    [Fact]
    public async Task DummyRadio_GetStatus_ShouldReturnDetailedStatus()
    {
        // Arrange
        var radio = new DummyRadio();
        await radio.ConnectAsync(null!);

        // Act
        var status = await radio.GetStatusAsync();

        // Assert
        Assert.NotNull(status);
        Assert.True(status.AdditionalInfo.ContainsKey("SimulatedRadio"));
        Assert.True(status.AdditionalInfo.ContainsKey("DummyRadioVersion"));
        Assert.True((bool)status.AdditionalInfo["SimulatedRadio"]);
        Assert.Equal("1.0", status.AdditionalInfo["DummyRadioVersion"]);

        // Should have frequency and mode from the simulated state
        Assert.True(status.Frequency > 0);
        Assert.NotNull(status.Mode);

        // Cleanup
        radio.Disconnect();
    }

    [Fact]
    public async Task DummyRadio_TransceiverInfoCommand_ShouldReturnFormattedResponse()
    {
        // Arrange
        var radio = new DummyRadio();
        var fakePort = new FakeSerialPort("TEST", 9600);
        await radio.ConnectAsync(fakePort);

        // Act
        var command = new RadioCommand("IF;", "Get Transceiver Info");
        var response = await radio.SendCommandAsync(command);

        // Assert
        Assert.NotNull(response);
        Assert.StartsWith("IF", response);
        Assert.EndsWith(";", response);
        Assert.Contains("00014074000", response); // Default frequency
        
        // Cleanup
        radio.Disconnect();
    }

    [Fact]
    public async Task DummyRadio_SMeterReading_ShouldReturnValues()
    {
        // Arrange
        var radio = new DummyRadio();
        var fakePort = new FakeSerialPort("TEST", 9600);
        await radio.ConnectAsync(fakePort);

        // Act
        var sMeter1 = await radio.GetSMeterAsync();
        await Task.Delay(50); // Small delay to get different reading
        var sMeter2 = await radio.GetSMeterAsync();

        // Assert
        Assert.True(sMeter1 >= 1 && sMeter1 <= 15); // Valid S-meter range
        Assert.True(sMeter2 >= 1 && sMeter2 <= 15);
        // Readings might be different due to simulation

        // Cleanup
        radio.Disconnect();
    }
}