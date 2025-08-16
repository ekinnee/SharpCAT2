using SharpCAT2.Common.Serial;
using Xunit;
using System.Threading.Tasks;

namespace SharpCAT2.Tests.Serial;

/// <summary>
/// Tests for the refactored FakeSerialPort to verify it is protocol-agnostic 
/// and only handles transport-level functionality.
/// </summary>
public class FakeSerialPortRefactoredTests
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
    public void FakeSerialPort_WriteData_ShouldStoreInInputBuffer()
    {
        // Arrange
        var fakePort = new FakeSerialPort("TEST", 9600);
        fakePort.Open();

        // Act
        fakePort.Write("TEST_COMMAND");

        // Assert
        var writtenData = fakePort.GetWrittenData();
        Assert.Equal("TEST_COMMAND", writtenData);

        fakePort.Close();
    }

    [Fact]
    public async Task FakeSerialPort_InjectResponseData_ShouldTriggerDataReceived()
    {
        // Arrange
        var fakePort = new FakeSerialPort("TEST", 9600);
        fakePort.Open();
        var dataReceived = false;
        fakePort.DataReceived += (sender, e) => dataReceived = true;

        // Act
        fakePort.InjectResponseData("TEST_RESPONSE");
        fakePort.Write("TRIGGER"); // This will cause the injected data to be processed

        // Wait for async processing
        await Task.Delay(50);

        // Assert
        Assert.True(dataReceived);
        var response = fakePort.ReadExisting();
        Assert.Equal("TEST_RESPONSE", response);

        fakePort.Close();
    }

    [Fact]
    public void FakeSerialPort_ClearWrittenData_ShouldRemoveAllData()
    {
        // Arrange
        var fakePort = new FakeSerialPort("TEST", 9600);
        fakePort.Open();

        // Act
        fakePort.Write("COMMAND1");
        fakePort.Write("COMMAND2");
        fakePort.ClearWrittenData();

        // Assert
        var data = fakePort.GetWrittenData();
        Assert.Null(data);

        fakePort.Close();
    }

    [Fact]
    public void FakeSerialPort_BufferManagement_ShouldWorkCorrectly()
    {
        // Arrange
        var fakePort = new FakeSerialPort("TEST", 9600);
        fakePort.Open();

        // Act
        fakePort.Write("TEST");
        fakePort.InjectResponseData("RESPONSE");
        fakePort.DiscardInBuffer(); // Should clear both input and inject buffers
        fakePort.DiscardOutBuffer();

        // Assert
        var writtenData = fakePort.GetWrittenData();
        Assert.Null(writtenData);
        Assert.Equal(0, fakePort.BytesToRead);

        fakePort.Close();
    }

    [Fact]
    public void FakeSerialPort_NoProtocolKnowledge_ShouldNotProcessCommands()
    {
        // Arrange
        var fakePort = new FakeSerialPort("TEST", 9600);
        fakePort.Open();

        // Act - Write radio commands that the old FakeSerialPort would have processed
        fakePort.Write("ID;");
        fakePort.Write("FA;");
        fakePort.Write("MD;");

        // Assert - Should not generate any automatic responses
        var response = fakePort.ReadExisting();
        Assert.Empty(response); // No automatic responses generated

        // Should have stored the commands for higher layers to process
        Assert.Equal("ID;", fakePort.GetWrittenData());
        Assert.Equal("FA;", fakePort.GetWrittenData());
        Assert.Equal("MD;", fakePort.GetWrittenData());

        fakePort.Close();
    }

    [Fact]
    public void FakeSerialPort_MultipleInjectData_ShouldMaintainOrder()
    {
        // Arrange
        var fakePort = new FakeSerialPort("TEST", 9600);
        fakePort.Open();

        // Act
        fakePort.InjectResponseData("FIRST");
        fakePort.InjectResponseData("SECOND");
        
        // Trigger processing
        fakePort.Write("CMD1");
        fakePort.Write("CMD2");

        // Wait for async processing
        Task.Delay(100).Wait();

        // Assert
        var response = fakePort.ReadExisting();
        Assert.Contains("FIRST", response);
        
        fakePort.Close();
    }
}