using Moq;
using SharpCAT2.ClientLibrary;
using Xunit;

namespace SharpCAT2.Tests.ClientLibrary;

/// <summary>
/// Tests for the ClientLibrary unified resource listing functionality
/// </summary>
public class SharpCAT2ClientResourceTests
{
    [Fact]
    public void GetAvailableSerialPortsAsync_ShouldExist()
    {
        // Arrange
        var client = new SharpCAT2Client("localhost", 8080);

        // Act & Assert
        var method = typeof(SharpCAT2Client).GetMethod("GetAvailableSerialPortsAsync");
        Assert.NotNull(method);
        Assert.Equal(typeof(Task<string?>), method.ReturnType);
    }

    [Fact]
    public void GetAvailableRadiosAsync_ShouldExist()
    {
        // Arrange
        var client = new SharpCAT2Client("localhost", 8080);

        // Act & Assert
        var method = typeof(SharpCAT2Client).GetMethod("GetAvailableRadiosAsync");
        Assert.NotNull(method);
        Assert.Equal(typeof(Task<string?>), method.ReturnType);
    }
}