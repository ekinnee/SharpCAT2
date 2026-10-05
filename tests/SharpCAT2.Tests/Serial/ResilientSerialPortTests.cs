using System.IO.Ports;
using Microsoft.Extensions.Logging;
using Moq;
using SharpCAT2.Core.Serial;
using SharpCAT2.Core.Utils;
using SharpCAT2.ServerLibrary.Serial;
using Xunit;

namespace SharpCAT2.Tests.Serial;

public class ResilientSerialPortTests
{
    [Fact]
    public void Wrapper_ForwardsPortOperationsAndExposesInnerPort()
    {
        var innerPort = new FakeSerialPort("TEST", 9600);
        using var wrapper = new ResilientSerialPort(innerPort);

        Assert.Equal("TEST", wrapper.PortName);
        Assert.Equal(9600, wrapper.BaudRate);
        Assert.Same(innerPort, wrapper.InnerPort);

        wrapper.Open();
        wrapper.Write("FA;");
        wrapper.WriteLine("ID;");
        Assert.True(wrapper.IsOpen);
        wrapper.Close();
        Assert.False(wrapper.IsOpen);
    }

    [Fact]
    public void CompatibilitySettingsAreInertAndCanStillBeConfigured()
    {
        var innerPort = new FakeSerialPort("TEST", 9600);
        using var wrapper = new ResilientSerialPort(innerPort);
        var policy = new RetryPolicy(maxRetries: 5, retryDelay: TimeSpan.FromMilliseconds(1));

        wrapper.RetryPolicy = policy;
        wrapper.AutoReconnectEnabled = true;
        wrapper.ReconnectionInterval = TimeSpan.FromMilliseconds(1);

        Assert.Same(policy, wrapper.RetryPolicy);
        Assert.True(wrapper.AutoReconnectEnabled);
        Assert.Equal(TimeSpan.FromMilliseconds(1), wrapper.ReconnectionInterval);
    }

    [Fact]
    public void WriteExceptionSurfacesOnceEvenWhenRetryAndReconnectSettingsAreEnabled()
    {
        var innerPort = new Mock<ISerialPort>();
        innerPort.SetupGet(port => port.PortName).Returns("TEST");
        innerPort.SetupGet(port => port.BaudRate).Returns(9600);
        innerPort.SetupGet(port => port.IsOpen).Returns(true);
        innerPort.Setup(port => port.Write("FA;"))
            .Throws(new IOException("write failed after invocation"));
        using var wrapper = new ResilientSerialPort(innerPort.Object)
        {
            AutoReconnectEnabled = true,
            RetryPolicy = new RetryPolicy(maxRetries: 5, retryDelay: TimeSpan.Zero)
        };

        var exception = Assert.Throws<IOException>(() => wrapper.Write("FA;"));

        Assert.Equal("write failed after invocation", exception.Message);
        innerPort.Verify(port => port.Write("FA;"), Times.Once);
        innerPort.Verify(port => port.Open(), Times.Never);
        innerPort.Verify(port => port.Close(), Times.Never);
    }

    [Fact]
    public void DataReceivedEvent_ForwardsOriginalSenderAndArguments()
    {
        var innerPort = new Mock<ISerialPort>();
        innerPort.SetupGet(port => port.PortName).Returns("TEST");
        innerPort.SetupGet(port => port.BaudRate).Returns(9600);
        using var wrapper = new ResilientSerialPort(innerPort.Object);
        var expectedSender = new object();
        object? forwardedSender = null;
        SerialDataReceivedEventArgs? forwardedArgs = null;
        wrapper.DataReceived += (sender, args) =>
        {
            forwardedSender = sender;
            forwardedArgs = args;
        };

        innerPort.Raise(port => port.DataReceived += null!, expectedSender, null!);

        Assert.Same(expectedSender, forwardedSender);
        Assert.Null(forwardedArgs);
    }

    [Fact]
    public void ConnectionEventsRemainSubscribableButAreNotSynthesized()
    {
        var innerPort = new FakeSerialPort("TEST", 9600);
        using var wrapper = new ResilientSerialPort(innerPort);
        var lostCount = 0;
        var restoredCount = 0;
        wrapper.ConnectionLost += (_, _) => lostCount++;
        wrapper.ConnectionRestored += (_, _) => restoredCount++;

        wrapper.Open();
        wrapper.Close();

        Assert.Equal(0, lostCount);
        Assert.Equal(0, restoredCount);
    }

    [Fact]
    public void DisposeOwnsAndDisposesInnerPortOnlyOnce()
    {
        var innerPort = new Mock<ISerialPort>();
        innerPort.SetupGet(port => port.PortName).Returns("TEST");
        innerPort.SetupGet(port => port.BaudRate).Returns(9600);
        var wrapper = new ResilientSerialPort(innerPort.Object);

        wrapper.Dispose();
        wrapper.Dispose();

        innerPort.Verify(port => port.Dispose(), Times.Once);
    }
}
