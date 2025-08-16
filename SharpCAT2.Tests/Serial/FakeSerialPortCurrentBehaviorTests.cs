using SharpCAT2.Common.Serial;
using Xunit;
using System.Threading.Tasks;

namespace SharpCAT2.Tests.Serial;

/// <summary>
/// Tests to capture current behavior of FakeSerialPort before refactoring.
/// These tests will be updated after the refactoring to reflect the new protocol-agnostic design.
/// </summary>
public class FakeSerialPortCurrentBehaviorTests
{
    [Fact]
    public void FakeSerialPort_BasicProperties_ShouldWorkCorrectly()
    {
        // Arrange
        var fakePort = new FakeSerialPort("TEST", 9600);

        // Assert
        Assert.Equal("TEST", fakePort.PortName);
        Assert.Equal(9600, fakePort.BaudRate);
        Assert.False(fakePort.IsOpen);
        Assert.Equal(0, fakePort.BytesToRead);
    }

    [Fact]
    public void FakeSerialPort_OpenClose_ShouldUpdateState()
    {
        // Arrange
        var fakePort = new FakeSerialPort("TEST", 9600);

        // Act & Assert - Open
        fakePort.Open();
        Assert.True(fakePort.IsOpen);

        // Act & Assert - Close
        fakePort.Close();
        Assert.False(fakePort.IsOpen);
    }

    [Fact]
    public async Task FakeSerialPort_CurrentCATradeCommands_ShouldReturnExpectedResponses()
    {
        // Arrange
        var fakePort = new FakeSerialPort("TEST", 9600);
        fakePort.Open();

        // Test ID command
        fakePort.Write("ID;");
        await Task.Delay(50); // Wait for async processing
        var response = fakePort.ReadExisting();
        Assert.Equal("ID999;", response);

        // Test FA (frequency) command
        fakePort.Write("FA;");
        await Task.Delay(50);
        response = fakePort.ReadExisting();
        Assert.Equal("FA00014074000;", response); // Default frequency

        // Test setting frequency
        fakePort.Write("FA00007074000;");
        await Task.Delay(50);
        response = fakePort.ReadExisting();
        Assert.Equal("FA00007074000;", response); // Echo command

        // Verify frequency was set
        fakePort.Write("FA;");
        await Task.Delay(50);
        response = fakePort.ReadExisting();
        Assert.Equal("FA00007074000;", response); // New frequency

        fakePort.Close();
    }

    [Fact]
    public async Task FakeSerialPort_CurrentModeCommands_ShouldReturnExpectedResponses()
    {
        // Arrange
        var fakePort = new FakeSerialPort("TEST", 9600);
        fakePort.Open();

        // Test MD (mode) command
        fakePort.Write("MD;");
        await Task.Delay(50);
        var response = fakePort.ReadExisting();
        Assert.Equal("MD2;", response); // USB mode

        // Test setting mode to LSB
        fakePort.Write("MD1;");
        await Task.Delay(50);
        response = fakePort.ReadExisting();
        Assert.Equal("MD1;", response);

        // Verify mode was set
        fakePort.Write("MD;");
        await Task.Delay(50);
        response = fakePort.ReadExisting();
        Assert.Equal("MD1;", response); // LSB mode

        fakePort.Close();
    }

    [Fact]
    public async Task FakeSerialPort_CurrentPowerCommands_ShouldReturnExpectedResponses()
    {
        // Arrange
        var fakePort = new FakeSerialPort("TEST", 9600);
        fakePort.Open();

        // Test PS (power) command
        fakePort.Write("PS;");
        await Task.Delay(50);
        var response = fakePort.ReadExisting();
        Assert.Equal("PS1;", response); // Power on

        fakePort.Close();
    }

    [Fact]
    public async Task FakeSerialPort_CurrentIFCommand_ShouldReturnFormattedResponse()
    {
        // Arrange
        var fakePort = new FakeSerialPort("TEST", 9600);
        fakePort.Open();

        // Test IF (transceiver information) command
        fakePort.Write("IF;");
        await Task.Delay(50);
        var response = fakePort.ReadExisting();
        
        // Should return formatted IF response with current state
        Assert.StartsWith("IF", response);
        Assert.EndsWith(";", response);
        Assert.Contains("00014074000", response); // Default frequency

        fakePort.Close();
    }

    [Fact]
    public async Task FakeSerialPort_UnrecognizedCommands_ShouldReturnOK()
    {
        // Arrange
        var fakePort = new FakeSerialPort("TEST", 9600);
        fakePort.Open();

        // Test unrecognized command
        fakePort.Write("XX;");
        await Task.Delay(50);
        var response = fakePort.ReadExisting();
        Assert.Equal("OK;", response);

        fakePort.Close();
    }

    [Fact]
    public void FakeSerialPort_BufferManagement_ShouldWorkCorrectly()
    {
        // Arrange
        var fakePort = new FakeSerialPort("TEST", 9600);
        fakePort.Open();

        // Test buffer clearing
        fakePort.DiscardInBuffer();
        fakePort.DiscardOutBuffer();

        // Should not throw
        Assert.True(fakePort.IsOpen);

        fakePort.Close();
    }
}