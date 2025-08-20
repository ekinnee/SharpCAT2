using Xunit;
using SharpCAT2.ServerLibrary;
using SharpCAT2.ServerLibrary.Radio;

namespace SharpCAT2.Tests.Services;

/// <summary>
/// Tests for the CommandDisplayService demonstrating separation of concerns
/// </summary>
public class CommandDisplayServiceTests
{
    private readonly CommandDisplayService _commandDisplayService;

    public CommandDisplayServiceTests()
    {
        _commandDisplayService = new CommandDisplayService();
    }

    [Fact]
    public void FormatRadioStatus_WithValidInfo_ShouldReturnFormattedString()
    {
        // Arrange
        var statusInfo = new RadioStatusInfo
        {
            Manufacturer = "TestMfg",
            ModelName = "TestModel",
            Frequency = 14074000,
            Mode = "USB",
            CurrentVfo = "A",
            IsTransmitting = false,
            IsPoweredOn = true,
            FeatureCount = 5,
            FeaturesDescription = "Test features",
            IsConnected = true
        };

        // Act
        var result = _commandDisplayService.FormatRadioStatus(statusInfo);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Radio Status:", result);
        Assert.Contains("TestMfg TestModel", result);
        Assert.Contains("14,074,000 Hz", result);
        Assert.Contains("USB", result);
        Assert.Contains("VFO: A", result);
        Assert.Contains("Transmitting: False", result);
        Assert.Contains("Power: True", result);
        Assert.Contains("Feature Count: 5", result);
        Assert.Contains("Test features", result);
    }

    [Fact]
    public void FormatRadioInfo_WithValidData_ShouldReturnFormattedString()
    {
        // Arrange
        var radioName = "Test Radio";
        var manufacturer = "TestMfg";
        var modelName = "TestModel";
        var featureCount = 3;
        var supportedFeatures = new[] { "FrequencyControl", "ModeControl", "PowerControl" };

        // Act
        var result = _commandDisplayService.FormatRadioInfo(radioName, manufacturer, modelName, featureCount, supportedFeatures);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Radio Information: Test Radio", result);
        Assert.Contains("Manufacturer: TestMfg", result);
        Assert.Contains("Model: TestModel", result);
        Assert.Contains("Feature Count: 3", result);
        Assert.Contains("✓ FrequencyControl", result);
        Assert.Contains("✓ ModeControl", result);
        Assert.Contains("✓ PowerControl", result);
    }

    [Fact]
    public void FormatRadioList_WithValidRadios_ShouldReturnFormattedString()
    {
        // Arrange
        var radios = new Dictionary<string, string>
        {
            ["TestMfg TestModel1"] = "desc1",
            ["TestMfg TestModel2"] = "desc2"
        };

        // Act
        var result = _commandDisplayService.FormatRadioList(radios);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Available Radio Models:", result);
        Assert.Contains("TestMfg TestModel1", result);
        Assert.Contains("TestMfg TestModel2", result);
        Assert.Contains("Found 2 radio model(s)", result);
        Assert.Contains("Use --radio", result);
        Assert.Contains("Use --auto-detect", result);
    }

    [Fact]
    public void FormatCurrentRadioResponse_WithStatusInfo_ShouldReturnFormattedResponse()
    {
        // Arrange
        var statusInfo = new RadioStatusInfo
        {
            Manufacturer = "TestMfg",
            ModelName = "TestModel",
            IsConnected = true
        };
        var portName = "COM1";

        // Act
        var result = _commandDisplayService.FormatCurrentRadioResponse(statusInfo, portName);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("CURRENT_RADIO:TestMfg TestModel|COM1|CONNECTED", result);
    }

    [Fact]
    public void FormatCurrentRadioResponse_WithNullStatus_ShouldReturnNoneResponse()
    {
        // Act
        var result = _commandDisplayService.FormatCurrentRadioResponse(null, null);

        // Assert
        Assert.Equal("CURRENT_RADIO:NONE", result);
    }

    [Fact]
    public void FormatNetworkResponse_WithKnownCommands_ShouldReturnFormattedResponse()
    {
        // Test radio list commands
        var radioListStart = _commandDisplayService.FormatNetworkResponse("radio_list_start", "");
        Assert.Equal("RADIO_LIST_START", radioListStart);

        var radioListEnd = _commandDisplayService.FormatNetworkResponse("radio_list_end", "");
        Assert.Equal("RADIO_LIST_END", radioListEnd);

        // Test serial port list commands
        var portListStart = _commandDisplayService.FormatNetworkResponse("serialport_list_start", "");
        Assert.Equal("SERIALPORT_LIST_START", portListStart);

        var portListEnd = _commandDisplayService.FormatNetworkResponse("serialport_list_end", "");
        Assert.Equal("SERIALPORT_LIST_END", portListEnd);
    }
}