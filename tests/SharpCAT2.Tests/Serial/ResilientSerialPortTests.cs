using Microsoft.Extensions.Logging;
using SharpCAT2.ServerLibrary.Serial;
using SharpCAT2.Core.Utils;
using SharpCAT2.Core.Serial;
using Moq;
using Xunit;
using System.IO.Ports;

namespace SharpCAT2.Tests.Serial;

/// <summary>
/// Tests for resilient serial port functionality
/// </summary>
public class ResilientSerialPortTests
{
    [Fact]
    public void ResilientSerialPort_Constructor_SetsUpCorrectly()
    {
        // Arrange
        var fakePort = new FakeSerialPort("TEST", 9600);
        
        // Act
        using var resilientPort = new ResilientSerialPort(fakePort);

        // Assert
        Assert.Equal("TEST", resilientPort.PortName);
        Assert.Equal(9600, resilientPort.BaudRate);
        Assert.True(resilientPort.AutoReconnectEnabled);
        Assert.Equal(TimeSpan.FromSeconds(5), resilientPort.ReconnectionInterval);
    }

    [Fact]
    public void ResilientSerialPort_Open_OpensInnerPort()
    {
        // Arrange
        var fakePort = new FakeSerialPort("TEST", 9600);
        using var resilientPort = new ResilientSerialPort(fakePort);

        // Act
        resilientPort.Open();

        // Assert
        Assert.True(resilientPort.IsOpen);
        Assert.True(fakePort.IsOpen);
    }

    [Fact]
    public void ResilientSerialPort_Close_ClosesInnerPort()
    {
        // Arrange
        var fakePort = new FakeSerialPort("TEST", 9600);
        using var resilientPort = new ResilientSerialPort(fakePort);
        resilientPort.Open();

        // Act
        resilientPort.Close();

        // Assert
        Assert.False(resilientPort.IsOpen);
        Assert.False(fakePort.IsOpen);
    }

    [Fact]
    public void ResilientSerialPort_Write_CallsInnerPortWrite()
    {
        // Arrange
        var innerPort = new Mock<ISerialPort>();
        innerPort.SetupGet(port => port.PortName).Returns("TEST");
        innerPort.SetupGet(port => port.BaudRate).Returns(9600);
        innerPort.SetupGet(port => port.IsOpen).Returns(true);
        using var resilientPort = new ResilientSerialPort(innerPort.Object);

        // Act
        resilientPort.Write("FA;");

        // Assert
        innerPort.Verify(port => port.Write("FA;"), Times.Once);
    }

    [Fact]
    public void ResilientSerialPort_WriteLine_CallsInnerPortWriteLine()
    {
        // Arrange
        var innerPort = new Mock<ISerialPort>();
        innerPort.SetupGet(port => port.PortName).Returns("TEST");
        innerPort.SetupGet(port => port.BaudRate).Returns(9600);
        innerPort.SetupGet(port => port.IsOpen).Returns(true);
        using var resilientPort = new ResilientSerialPort(innerPort.Object);

        // Act
        resilientPort.WriteLine("ID;");

        // Assert
        innerPort.Verify(port => port.WriteLine("ID;"), Times.Once);
    }

    [Fact]
    public void ResilientSerialPort_ReadExisting_ReturnsDataFromInnerPort()
    {
        // Arrange
        const string expectedData = "ID999;\r\n";
        var innerPort = new Mock<ISerialPort>();
        innerPort.SetupGet(port => port.PortName).Returns("TEST");
        innerPort.SetupGet(port => port.BaudRate).Returns(9600);
        innerPort.SetupGet(port => port.IsOpen).Returns(true);
        innerPort.Setup(port => port.ReadExisting()).Returns(expectedData);
        using var resilientPort = new ResilientSerialPort(innerPort.Object);

        // Act
        var data = resilientPort.ReadExisting();

        // Assert
        Assert.Equal(expectedData, data);
        innerPort.Verify(port => port.ReadExisting(), Times.Once);
    }

    [Fact]
    public void ResilientSerialPort_DiscardBuffers_CallsInnerPortMethods()
    {
        // Arrange
        var fakePort = new FakeSerialPort("TEST", 9600);
        using var resilientPort = new ResilientSerialPort(fakePort);
        resilientPort.Open();

        // Act & Assert (no exceptions should be thrown)
        resilientPort.DiscardInBuffer();
        resilientPort.DiscardOutBuffer();
    }

    [Fact]
    public void ResilientSerialPort_ConfigurableRetryPolicy_CanBeModified()
    {
        // Arrange
        var fakePort = new FakeSerialPort("TEST", 9600);
        using var resilientPort = new ResilientSerialPort(fakePort);
        
        var customPolicy = new RetryPolicy(
            maxRetries: 5, 
            retryDelay: TimeSpan.FromMilliseconds(100)
        );

        // Act
        resilientPort.RetryPolicy = customPolicy;

        // Assert
        Assert.Equal(5, resilientPort.RetryPolicy.MaxRetries);
        Assert.Equal(TimeSpan.FromMilliseconds(100), resilientPort.RetryPolicy.RetryDelay);
    }

    [Fact]
    public void ResilientSerialPort_AutoReconnectSettings_CanBeConfigured()
    {
        // Arrange
        var fakePort = new FakeSerialPort("TEST", 9600);
        using var resilientPort = new ResilientSerialPort(fakePort);

        // Act
        resilientPort.AutoReconnectEnabled = false;
        resilientPort.ReconnectionInterval = TimeSpan.FromSeconds(10);

        // Assert
        Assert.False(resilientPort.AutoReconnectEnabled);
        Assert.Equal(TimeSpan.FromSeconds(10), resilientPort.ReconnectionInterval);
    }

    [Fact]
    public void ResilientSerialPort_DataReceivedEvent_ForwardsFromInnerPort()
    {
        // Verify transport event forwarding only; this does not model a radio response.
        // Arrange
        var innerPort = new Mock<ISerialPort>();
        innerPort.SetupGet(port => port.PortName).Returns("TEST");
        innerPort.SetupGet(port => port.BaudRate).Returns(9600);
        innerPort.SetupGet(port => port.IsOpen).Returns(true);
        using var resilientPort = new ResilientSerialPort(innerPort.Object);
        var expectedSender = new object();
        var eventInvocationCount = 0;
        object? forwardedSender = null;
        SerialDataReceivedEventArgs? forwardedArgs = null;
        resilientPort.DataReceived += (sender, args) =>
        {
            eventInvocationCount++;
            forwardedSender = sender;
            forwardedArgs = args;
        };

        // SerialDataReceivedEventArgs has no public constructor. The mocked transport
        // can raise its event with null args to test that the wrapper forwards the event
        // sender unchanged without depending on platform serial-port internals.
        innerPort.Raise(port => port.DataReceived += null!, expectedSender, null!);

        // Assert
        Assert.Equal(1, eventInvocationCount);
        Assert.Same(expectedSender, forwardedSender);
        Assert.Null(forwardedArgs);
    }

    [Fact]
    public void ResilientSerialPort_EventsSetup_ConnectionEventsExist()
    {
        // Arrange
        var fakePort = new FakeSerialPort("TEST", 9600);
        using var resilientPort = new ResilientSerialPort(fakePort);
        
        bool connectionLostEventSubscribed = false;
        bool connectionRestoredEventSubscribed = false;
        
        // Act - Try to subscribe to events to verify they exist
        try
        {
            resilientPort.ConnectionLost += (sender, e) => { };
            connectionLostEventSubscribed = true;
        }
        catch
        {
            // Event doesn't exist or can't be subscribed to
        }
        
        try
        {
            resilientPort.ConnectionRestored += (sender, e) => { };
            connectionRestoredEventSubscribed = true;
        }
        catch
        {
            // Event doesn't exist or can't be subscribed to
        }

        // Assert
        Assert.True(connectionLostEventSubscribed);
        Assert.True(connectionRestoredEventSubscribed);
    }
}
