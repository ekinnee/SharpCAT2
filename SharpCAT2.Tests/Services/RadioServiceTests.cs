using Microsoft.Extensions.Logging;
using Moq;
using SharpCAT2.Core.Services;
using SharpCAT2.Common.Radio;
using SharpCAT2.Common.Serial;
using Xunit;

namespace SharpCAT2.Tests.Services;

/// <summary>
/// Unit tests for the RadioService class
/// </summary>
public class RadioServiceTests
{
    private readonly Mock<ILogger<RadioService>> _mockLogger;
    private readonly RadioService _radioService;

    public RadioServiceTests()
    {
        _mockLogger = new Mock<ILogger<RadioService>>();
        _radioService = new RadioService(_mockLogger.Object);
    }

    [Fact]
    public void ConnectedRadio_Initially_ShouldBeNull()
    {
        // Act
        var radio = _radioService.ConnectedRadio;

        // Assert
        Assert.Null(radio);
    }

    [Fact]
    public void IsRadioConnected_Initially_ShouldBeFalse()
    {
        // Act
        var isConnected = _radioService.IsRadioConnected;

        // Assert
        Assert.False(isConnected);
    }

    [Fact]
    public void GetAvailableRadios_ShouldReturnNonEmptyList()
    {
        // Act
        var radios = _radioService.GetAvailableRadios();

        // Assert
        Assert.NotNull(radios);
        Assert.NotEmpty(radios);
    }

    [Fact]
    public void GetRadioInfo_WithValidRadio_ShouldReturnInfo()
    {
        // Arrange
        var radioName = "DummyRadio";

        // Act
        var info = _radioService.GetRadioInfo(radioName);

        // Assert
        Assert.NotNull(info);
        Assert.Contains("DummyRadio", info);
        Assert.Contains("Radio Information", info);
    }

    [Fact]
    public void GetRadioInfo_WithInvalidRadio_ShouldReturnNull()
    {
        // Arrange
        var radioName = "NonExistentRadio";

        // Act
        var info = _radioService.GetRadioInfo(radioName);

        // Assert
        Assert.Null(info);
    }

    [Fact]
    public async Task TryProcessRadioCommandAsync_WithoutConnectedRadio_ShouldReturnFalse()
    {
        // Arrange
        var input = "FA;";

        // Act
        var result = await _radioService.TryProcessRadioCommandAsync(input);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetRadioStatusAsync_WithoutConnectedRadio_ShouldReturnNoRadioMessage()
    {
        // Act
        var status = await _radioService.GetRadioStatusAsync();

        // Assert
        Assert.Equal("No radio connected.", status);
    }

    [Fact]
    public async Task DisconnectRadioAsync_WithoutConnectedRadio_ShouldNotThrow()
    {
        // Act & Assert
        var exception = await Record.ExceptionAsync(() => _radioService.DisconnectRadioAsync());
        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        // Act & Assert
        var exception = Record.Exception(() => _radioService.Dispose());
        Assert.Null(exception);
    }
}