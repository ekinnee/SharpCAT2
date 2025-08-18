using SharpCAT2.Common.Radio.Models.Testing;
using SharpCAT2.Common.Serial;
using SharpCAT2.Common.Radio;
using SharpCAT2.Core.Radio;
using Xunit;
using System.Threading.Tasks;

namespace SharpCAT2.Tests.Radio;

/// <summary>
/// Tests to capture current behavior of DummyRadio before refactoring.
/// These tests will be updated after the refactoring to reflect the new radio simulation design.
/// </summary>
public class DummyRadioCurrentBehaviorTests
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
    public async Task DummyRadio_ConnectWithNullPort_ShouldCreateFakePort()
    {
        // Arrange
        var radio = new DummyRadio();

        // Act
        var result = await radio.ConnectAsync(null!);

        // Assert
        Assert.True(result);
        Assert.True(radio.IsConnected);

        // Cleanup
        radio.Disconnect();
    }

    [Fact]
    public async Task DummyRadio_ConnectWithFakePort_ShouldUseProvidedPort()
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
    public async Task DummyRadio_SendCommand_ShouldWorkWithFakePort()
    {
        // Arrange
        var radio = new DummyRadio();
        var fakePort = new FakeSerialPort("TEST", 9600);
        await radio.ConnectAsync(fakePort);

        // Act
        var command = new RadioCommand("ID", "");
        var response = await radio.SendCommandAsync(command);

        // Assert
        Assert.NotNull(response);
        // The response will depend on how the base class processes the command

        // Cleanup
        radio.Disconnect();
    }

    [Fact]
    public async Task DummyRadio_GetStatus_ShouldReturnSimulatedStatus()
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

        // Cleanup
        radio.Disconnect();
    }

    [Fact]
    public void DummyRadio_DisconnectWithoutConnection_ShouldNotThrow()
    {
        // Arrange
        var radio = new DummyRadio();

        // Act & Assert
        var exception = Record.Exception(() => radio.Disconnect());
        Assert.Null(exception);
    }

    [Fact]
    public void DummyRadio_Dispose_ShouldNotThrow()
    {
        // Arrange
        var radio = new DummyRadio();

        // Act & Assert
        var exception = Record.Exception(() => radio.Dispose());
        Assert.Null(exception);
    }
}