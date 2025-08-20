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
        Assert.Equal("ls", ClientConstants.ListSerialPortsShortCommand);
    }

    [Fact]
    public void RadioConstants_ShouldStillExist()
    {
        // Assert
        Assert.Equal("list-radios", ClientConstants.ListRadiosCommand);
        Assert.Equal("lr", ClientConstants.ListRadiosShortCommand);
    }

    [Fact]
    public void CurrentRadioConstants_ShouldBeCorrect()
    {
        // Assert
        Assert.Equal("current-radio", ClientConstants.CurrentRadioCommand);
        Assert.Equal("cr", ClientConstants.CurrentRadioShortCommand);
    }

    [Fact]
    public void SetRadioConstants_ShouldBeCorrect()
    {
        // Assert
        Assert.Equal("set-radio", ClientConstants.SetRadioCommand);
        Assert.Equal("sr", ClientConstants.SetRadioShortCommand);
    }

    [Fact]
    public void RadioStatusConstants_ShouldBeCorrect()
    {
        // Assert
        Assert.Equal("radio-status", ClientConstants.RadioStatusCommand);
        Assert.Equal("rs", ClientConstants.RadioStatusShortCommand);
    }
}