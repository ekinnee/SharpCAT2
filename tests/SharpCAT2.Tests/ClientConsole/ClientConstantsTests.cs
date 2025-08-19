using SharpCAT2.ClientConsole;
using Xunit;

namespace SharpCAT2.Tests.ClientConsole;

/// <summary>
/// Tests for the ClientConsole constants related to unified resource listing
/// </summary>
public class ClientConstantsTests
{
    [Fact]
    public void SerialPortConstants_ShouldBeCorrect()
    {
        // Assert
        Assert.Equal("list-serialports", ClientConstants.ListSerialPortsCommand);
        Assert.Equal("serialports", ClientConstants.SerialPortsCommand);
    }

    [Fact]
    public void RadioConstants_ShouldStillExist()
    {
        // Assert
        Assert.Equal("list-radios", ClientConstants.ListRadiosCommand);
        Assert.Equal("radios", ClientConstants.RadiosCommand);
    }
}