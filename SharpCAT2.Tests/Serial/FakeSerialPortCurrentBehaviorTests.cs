using SharpCAT2.Common.Serial;
using Xunit;
using System.Threading.Tasks;

namespace SharpCAT2.Tests.Serial;

/// <summary>
/// Tests to document current behavior of FakeSerialPort before refactoring.
/// 
/// IMPORTANT: After the protocol-agnostic refactor, these tests have been updated
/// to reflect the new behavior where FakeSerialPort does NOT process radio commands.
/// 
/// The new architecture separates concerns:
/// - FakeSerialPort: Protocol-agnostic transport (buffering, I/O simulation)
/// - DummyRadio: Radio protocol logic (CAT commands, responses)
/// - ResilientRadio: Connection management (health checks, retries)
/// 
/// These tests now verify that FakeSerialPort behaves as a pure transport layer.
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
    public async Task FakeSerialPort_ProtocolAgnosticBehavior_ShouldNotProcessCommands()
    {
        // Arrange
        var fakePort = new FakeSerialPort("TEST", 9600);
        fakePort.Open();

        // Act - FakeSerialPort should NOT process CAT commands in the new architecture
        fakePort.Write("ID;");
        await Task.Delay(50); // Wait for any potential processing
        var response = fakePort.ReadExisting();
        
        // Assert - No automatic response should be generated
        Assert.Equal("", response); // FakeSerialPort is now protocol-agnostic
        
        // But the command should be available for higher layers to process
        var writtenCommand = fakePort.GetWrittenData();
        Assert.Equal("ID;", writtenCommand);

        fakePort.Close();
    }

    [Fact]
    public async Task FakeSerialPort_ProtocolAgnostic_ModeCommands()
    {
        // Arrange
        var fakePort = new FakeSerialPort("TEST", 9600);
        fakePort.Open();

        // Act - FakeSerialPort should be protocol-agnostic
        fakePort.Write("MD;");
        await Task.Delay(50);
        var response = fakePort.ReadExisting();
        
        // Assert - No automatic response
        Assert.Equal("", response);
        
        // Command should be available for processing by higher layers
        var writtenCommand = fakePort.GetWrittenData();
        Assert.Equal("MD;", writtenCommand);

        fakePort.Close();
    }

    [Fact]
    public async Task FakeSerialPort_ProtocolAgnostic_PowerCommands()
    {
        // Arrange
        var fakePort = new FakeSerialPort("TEST", 9600);
        fakePort.Open();

        // Act - FakeSerialPort should be protocol-agnostic
        fakePort.Write("PS;");
        await Task.Delay(50);
        var response = fakePort.ReadExisting();
        
        // Assert - No automatic response
        Assert.Equal("", response);
        
        // Command should be available for processing by higher layers
        var writtenCommand = fakePort.GetWrittenData();
        Assert.Equal("PS;", writtenCommand);

        fakePort.Close();
    }

    [Fact]
    public async Task FakeSerialPort_ProtocolAgnostic_IFCommand()
    {
        // Arrange
        var fakePort = new FakeSerialPort("TEST", 9600);
        fakePort.Open();

        // Act - FakeSerialPort should be protocol-agnostic
        fakePort.Write("IF;");
        await Task.Delay(50);
        var response = fakePort.ReadExisting();
        
        // Assert - No automatic response
        Assert.Equal("", response);
        
        // Command should be available for processing by higher layers
        var writtenCommand = fakePort.GetWrittenData();
        Assert.Equal("IF;", writtenCommand);

        fakePort.Close();
    }

    [Fact]
    public async Task FakeSerialPort_ProtocolAgnostic_UnrecognizedCommands()
    {
        // Arrange
        var fakePort = new FakeSerialPort("TEST", 9600);
        fakePort.Open();

        // Act - FakeSerialPort should be protocol-agnostic
        fakePort.Write("XX;");
        await Task.Delay(50);
        var response = fakePort.ReadExisting();
        
        // Assert - No automatic response (protocol-agnostic)
        Assert.Equal("", response);
        
        // Command should be available for processing by higher layers
        var writtenCommand = fakePort.GetWrittenData();
        Assert.Equal("XX;", writtenCommand);

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