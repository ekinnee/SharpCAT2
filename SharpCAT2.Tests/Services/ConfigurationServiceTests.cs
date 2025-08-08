using Microsoft.Extensions.Logging;
using Moq;
using SharpCAT2.Server.Services;
using Xunit;

namespace SharpCAT2.Tests.Services;

/// <summary>
/// Unit tests for the ConfigurationService class
/// </summary>
public class ConfigurationServiceTests
{
    private readonly Mock<ILogger<ConfigurationService>> _mockLogger;
    private readonly ConfigurationService _configurationService;

    public ConfigurationServiceTests()
    {
        _mockLogger = new Mock<ILogger<ConfigurationService>>();
        _configurationService = new ConfigurationService(_mockLogger.Object);
    }

    [Fact]
    public async Task LoadConfigurationAsync_WithNonExistentFile_ShouldReturnDefaultConfig()
    {
        // Arrange
        var nonExistentPath = "/tmp/non-existent-config.json";

        // Act
        var result = await _configurationService.LoadConfigurationAsync(nonExistentPath);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(9600, result.BaudRate);
        Assert.Equal(8080, result.TcpPort);
    }

    [Fact]
    public void ApplyConfigurationToOptions_ShouldUpdateOptions()
    {
        // Arrange
        var config = new SharpCAT2.Server.ServerConfig
        {
            SerialPort = "COM1",
            BaudRate = 115200,
            TcpPort = 9090,
            Radio = "Kenwood TS-2000",
            AutoDetectRadio = true
        };

        var options = new SharpCAT2.Server.CommandLineOptions();

        // Act
        _configurationService.ApplyConfigurationToOptions(config, options);

        // Assert
        Assert.Equal("COM1", options.PortName);
        Assert.Equal(115200, options.BaudRate);
        Assert.Equal(9090, options.TcpPort);
        Assert.Equal("Kenwood TS-2000", options.RadioModel);
        Assert.True(options.AutoDetectRadio);
    }

    [Fact]
    public void UpdateConfigurationFromOptions_ShouldUpdateConfig()
    {
        // Arrange
        var config = new SharpCAT2.Server.ServerConfig();
        var options = new SharpCAT2.Server.CommandLineOptions
        {
            PortName = "COM2",
            BaudRate = 57600,
            TcpPort = 8888,
            RadioModel = "Elecraft K3",
            AutoDetectRadio = false
        };

        // Act
        _configurationService.UpdateConfigurationFromOptions(config, options);

        // Assert
        Assert.Equal("COM2", config.SerialPort);
        Assert.Equal(57600, config.BaudRate);
        Assert.Equal(8888, config.TcpPort);
        Assert.Equal("Elecraft K3", config.Radio);
        Assert.False(config.AutoDetectRadio);
    }
}