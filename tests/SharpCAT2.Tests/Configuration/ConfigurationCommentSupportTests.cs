using SharpCAT2.Core.Configuration;
using SharpCAT2.ClientLibrary;
using Xunit;

namespace SharpCAT2.Tests.Configuration;

/// <summary>
/// Tests for JSON configuration file comment support using Newtonsoft.Json
/// </summary>
public class ConfigurationCommentSupportTests
{
    [Fact]
    public async Task ServerConfig_LoadAsync_ShouldParseFileWithComments()
    {
        // Arrange
        var testConfigPath = "/tmp/config_tests/server_config_with_comments.json";
        
        // Act
        var config = await ServerConfig.LoadAsync(testConfigPath);
        
        // Assert
        Assert.NotNull(config);
        Assert.Equal("COM3", config.SerialPort);
        Assert.Equal("Kenwood TS-2000", config.Radio);
        Assert.Equal(57600, config.BaudRate);
        Assert.Equal(8888, config.TcpPort);
        Assert.False(config.AutoDetectRadio);
    }
    
    [Fact]
    public async Task ClientConfig_LoadAsync_ShouldParseFileWithComments()
    {
        // Arrange
        var testConfigPath = "/tmp/config_tests/client_config_with_comments.json";
        
        // Act
        var config = await ClientConfig.LoadAsync(testConfigPath);
        
        // Assert
        Assert.NotNull(config);
        Assert.Equal("192.168.1.100", config.ServerHost);
        Assert.Equal(9090, config.ServerPort);
    }
    
    [Fact]
    public async Task ServerConfig_SaveAndLoadRoundTrip_ShouldPreserveData()
    {
        // Arrange
        var testConfigPath = "/tmp/config_tests/server_roundtrip_test.json";
        var originalConfig = new ServerConfig
        {
            SerialPort = "COM5",
            Radio = "Yaesu FT-991A",
            BaudRate = 38400,
            TcpPort = 7777,
            AutoDetectRadio = true
        };
        
        // Act - Save
        var saveResult = await originalConfig.SaveAsync(testConfigPath);
        
        // Act - Load
        var loadedConfig = await ServerConfig.LoadAsync(testConfigPath);
        
        // Assert
        Assert.True(saveResult);
        Assert.NotNull(loadedConfig);
        Assert.Equal(originalConfig.SerialPort, loadedConfig.SerialPort);
        Assert.Equal(originalConfig.Radio, loadedConfig.Radio);
        Assert.Equal(originalConfig.BaudRate, loadedConfig.BaudRate);
        Assert.Equal(originalConfig.TcpPort, loadedConfig.TcpPort);
        Assert.Equal(originalConfig.AutoDetectRadio, loadedConfig.AutoDetectRadio);
        
        // Cleanup
        if (File.Exists(testConfigPath))
            File.Delete(testConfigPath);
    }
    
    [Fact]
    public async Task ClientConfig_SaveAndLoadRoundTrip_ShouldPreserveData()
    {
        // Arrange
        var testConfigPath = "/tmp/config_tests/client_roundtrip_test.json";
        var originalConfig = new ClientConfig
        {
            ServerHost = "test.example.com",
            ServerPort = 12345
        };
        
        // Act - Save
        var saveResult = await originalConfig.SaveAsync(testConfigPath);
        
        // Act - Load
        var loadedConfig = await ClientConfig.LoadAsync(testConfigPath);
        
        // Assert
        Assert.True(saveResult);
        Assert.NotNull(loadedConfig);
        Assert.Equal(originalConfig.ServerHost, loadedConfig.ServerHost);
        Assert.Equal(originalConfig.ServerPort, loadedConfig.ServerPort);
        
        // Cleanup
        if (File.Exists(testConfigPath))
            File.Delete(testConfigPath);
    }
}