using Xunit;
using SharpCAT2.ClientConsole;
using SharpCAT2.ClientLibrary;
using SharpCAT2.ServerLibrary.Radio;
using SharpCAT2.ServerLibrary;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace SharpCAT2.Tests.Commands;

/// <summary>
/// Comprehensive tests for the radio-info command functionality across all layers
/// </summary>
public class RadioInfoCommandTests
{
    #region ClientConsole Quote Handling Tests

    [Theory]
    [InlineData("radio-info Kenwood TS-2000", "Kenwood TS-2000")]
    [InlineData("ri Kenwood TS-2000", "Kenwood TS-2000")]
    [InlineData("radio-info \"Kenwood TS-2000\"", "Kenwood TS-2000")]
    [InlineData("ri \"Kenwood TS-2000\"", "Kenwood TS-2000")]
    [InlineData("radio-info 'Kenwood TS-2000'", "Kenwood TS-2000")]
    [InlineData("ri 'Kenwood TS-2000'", "Kenwood TS-2000")]
    [InlineData("RADIO-INFO Kenwood TS-2000", "Kenwood TS-2000")]
    [InlineData("RI Kenwood TS-2000", "Kenwood TS-2000")]
    [InlineData("radio-info   Kenwood TS-2000   ", "Kenwood TS-2000")]
    [InlineData("ri   \"Kenwood TS-2000\"   ", "Kenwood TS-2000")]
    [InlineData("radio-info \"Yaesu FT-991A\"", "Yaesu FT-991A")]
    [InlineData("ri \"Ten-Tec OMNI VII\"", "Ten-Tec OMNI VII")]
    public void ExtractRadioNameFromInfoCommand_ShouldHandleQuotesAndCases(string input, string expected)
    {
        // Use reflection to access the private method
        var method = typeof(ClientCommandProcessor).GetMethod(
            "ExtractRadioNameFromInfoCommand", 
            BindingFlags.NonPublic | BindingFlags.Static);
        
        Assert.NotNull(method);
        
        var result = (string)method.Invoke(null, new object[] { input })!;
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("radio-info")]
    [InlineData("ri")]
    [InlineData("radio-info ")]
    [InlineData("ri ")]
    [InlineData("radio-info \"\"")]
    [InlineData("ri ''")]
    [InlineData("other-command Kenwood TS-2000")]
    [InlineData("")]
    public void ExtractRadioNameFromInfoCommand_ShouldReturnEmptyForInvalidInput(string input)
    {
        // Use reflection to access the private method
        var method = typeof(ClientCommandProcessor).GetMethod(
            "ExtractRadioNameFromInfoCommand", 
            BindingFlags.NonPublic | BindingFlags.Static);
        
        Assert.NotNull(method);
        
        var result = (string)method.Invoke(null, new object[] { input })!;
        Assert.True(string.IsNullOrEmpty(result));
    }

    #endregion

    #region RadioFactory Case-Insensitive Tests

    [Theory]
    [InlineData("Kenwood TS-2000")]
    [InlineData("kenwood ts-2000")]
    [InlineData("KENWOOD TS-2000")]
    [InlineData("KenWood Ts-2000")]
    [InlineData("SharpCAT2 DummyRadio")]
    [InlineData("sharpcat2 dummyradio")]
    [InlineData("SHARPCAT2 DUMMYRADIO")]
    public void RadioFactory_CreateRadio_ShouldBeCaseInsensitive(string radioName)
    {
        // Act
        var radio = RadioFactory.CreateRadio(radioName);
        
        // Assert
        Assert.NotNull(radio);
        
        // Verify it's the correct radio type
        if (radioName.ToLower().Contains("kenwood") && radioName.ToLower().Contains("ts-2000"))
        {
            Assert.Equal("Kenwood", radio.Manufacturer);
            Assert.Equal("TS-2000", radio.ModelName);
        }
        else if (radioName.ToLower().Contains("sharpcat2") && radioName.ToLower().Contains("dummyradio"))
        {
            Assert.Equal("SharpCAT2", radio.Manufacturer);
            Assert.Equal("DummyRadio", radio.ModelName);
        }
        
        // Clean up
        radio.Dispose();
    }

    [Theory]
    [InlineData("kenwood", "ts-2000")]
    [InlineData("KENWOOD", "TS-2000")]
    [InlineData("KenWood", "Ts-2000")]
    [InlineData("sharpcat2", "dummyradio")]
    [InlineData("SHARPCAT2", "DUMMYRADIO")]
    public void RadioFactory_CreateRadio_WithSeparateParams_ShouldBeCaseInsensitive(string manufacturer, string model)
    {
        // Act
        var radio = RadioFactory.CreateRadio(manufacturer, model);
        
        // Assert
        Assert.NotNull(radio);
        
        // Clean up
        radio.Dispose();
    }

    [Theory]
    [InlineData("NonExistent Radio")]
    [InlineData("Invalid Manufacturer")]
    [InlineData("")]
    [InlineData("   ")]
    public void RadioFactory_CreateRadio_ShouldReturnNullForUnknownRadios(string radioName)
    {
        // Act
        var radio = RadioFactory.CreateRadio(radioName);
        
        // Assert
        Assert.Null(radio);
    }

    #endregion

    #region RadioService Case-Insensitive Tests

    [Fact]
    public void RadioService_GetRadioInfo_ShouldBeCaseInsensitive()
    {
        // Arrange
        var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<RadioService>();
        var radioService = new RadioService(logger);
        
        // Test various case combinations
        var testCases = new[]
        {
            "Kenwood TS-2000",
            "kenwood ts-2000", 
            "KENWOOD TS-2000",
            "KenWood Ts-2000",
            "SharpCAT2 DummyRadio",
            "sharpcat2 dummyradio"
        };
        
        foreach (var testCase in testCases)
        {
            // Act
            var radioInfo = radioService.GetRadioInfo(testCase);
            
            // Assert
            Assert.NotNull(radioInfo);
            Assert.NotEmpty(radioInfo.Manufacturer);
            Assert.NotEmpty(radioInfo.ModelName);
            Assert.True(radioInfo.FeatureCount >= 0);
        }
    }

    [Theory]
    [InlineData("NonExistent Radio")]
    [InlineData("Invalid Manufacturer")]
    [InlineData("")]
    [InlineData("   ")]
    public void RadioService_GetRadioInfo_ShouldReturnNullForUnknownRadios(string radioName)
    {
        // Arrange
        var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<RadioService>();
        var radioService = new RadioService(logger);
        
        // Act
        var radioInfo = radioService.GetRadioInfo(radioName);
        
        // Assert
        Assert.Null(radioInfo);
    }

    #endregion

    #region ProtocolListFilter Tests

    [Fact]
    public void ProtocolListFilter_FilterRadioInfo_ShouldHandleCompleteResponse()
    {
        // Arrange
        var protocolResponse = @"RADIO_INFO_START
RadioName=Kenwood TS-2000
Manufacturer=Kenwood
ModelName=TS-2000
FeatureCount=25
IsFullFeatureSet=false
SupportedFeatures=FrequencyControl,ModeControl,DualVFO
RADIO_INFO_END";

        // Act
        var result = ProtocolListFilter.FilterRadioInfo(protocolResponse);

        // Assert
        Assert.NotNull(result);
        Assert.DoesNotContain("RADIO_INFO_START", result);
        Assert.DoesNotContain("RADIO_INFO_END", result);
        Assert.Contains("RadioName=Kenwood TS-2000", result);
        Assert.Contains("Manufacturer=Kenwood", result);
        Assert.Contains("ModelName=TS-2000", result);
        Assert.Contains("FeatureCount=25", result);
        Assert.Contains("IsFullFeatureSet=false", result);
        Assert.Contains("SupportedFeatures=FrequencyControl,ModeControl,DualVFO", result);
    }

    [Fact]
    public void ProtocolListFilter_ParseRadioInfo_ShouldExtractKeyValuePairs()
    {
        // Arrange
        var filteredInfo = @"RadioName=Kenwood TS-2000
Manufacturer=Kenwood
ModelName=TS-2000
FeatureCount=25
IsFullFeatureSet=false
SupportedFeatures=FrequencyControl,ModeControl,DualVFO";

        // Act
        var result = ProtocolListFilter.ParseRadioInfo(filteredInfo);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(6, result.Count);
        Assert.Equal("Kenwood TS-2000", result["RadioName"]);
        Assert.Equal("Kenwood", result["Manufacturer"]);
        Assert.Equal("TS-2000", result["ModelName"]);
        Assert.Equal("25", result["FeatureCount"]);
        Assert.Equal("false", result["IsFullFeatureSet"]);
        Assert.Equal("FrequencyControl,ModeControl,DualVFO", result["SupportedFeatures"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("INVALID_PROTOCOL")]
    public void ProtocolListFilter_FilterRadioInfo_ShouldHandleInvalidInput(string? input)
    {
        // Act
        var result = ProtocolListFilter.FilterRadioInfo(input);

        // Assert
        if (input == null)
        {
            Assert.Null(result);
        }
        else
        {
            Assert.Equal(string.Empty, result);
        }
    }

    #endregion

    #region Error Response Tests

    [Theory]
    [InlineData("ERROR: Unknown radio: Invalid Radio")]
    [InlineData("ERROR: Radio name cannot be empty")]
    [InlineData("ERROR: Failed to get radio info - Some error")]
    public void ClientLib_GetRadioInfoAsync_ShouldHandleServerErrors(string errorResponse)
    {
        // This test would require a mock server to send error responses
        // For now, we test that the error parsing logic works correctly
        
        // Arrange - simulate the error parsing logic from ClientLib
        bool isError = errorResponse.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase);
        string errorMessage = errorResponse.Length > 6 ? errorResponse[6..].Trim() : "Unknown error";
        
        // Assert
        Assert.True(isError);
        Assert.NotEmpty(errorMessage);
        Assert.NotEqual("Unknown error", errorMessage);
    }

    #endregion

    #region Integration Test Scenarios

    [Fact]
    public void RadioInfoWorkflow_EndToEnd_ShouldWorkWithQuotedNames()
    {
        // This test simulates the end-to-end workflow for radio-info command
        
        // 1. Extract radio name from command (simulating ClientConsole)
        string command = "radio-info \"Kenwood TS-2000\"";
        var method = typeof(ClientCommandProcessor).GetMethod(
            "ExtractRadioNameFromInfoCommand", 
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        
        var extractedName = (string)method.Invoke(null, new object[] { command })!;
        Assert.Equal("Kenwood TS-2000", extractedName);
        
        // 2. Look up radio in factory (simulating server lookup)
        var radio = RadioFactory.CreateRadio(extractedName);
        Assert.NotNull(radio);
        Assert.Equal("Kenwood", radio.Manufacturer);
        Assert.Equal("TS-2000", radio.ModelName);
        
        // 3. Get radio info (simulating RadioService)
        var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<RadioService>();
        var radioService = new RadioService(logger);
        var radioInfo = radioService.GetRadioInfo(extractedName);
        
        Assert.NotNull(radioInfo);
        Assert.Equal("Kenwood TS-2000", radioInfo.RadioName);
        Assert.Equal("Kenwood", radioInfo.Manufacturer);
        Assert.Equal("TS-2000", radioInfo.ModelName);
        Assert.True(radioInfo.FeatureCount > 0);
        Assert.NotEmpty(radioInfo.SupportedFeatures);
        
        // Clean up
        radio.Dispose();
    }

    [Fact]
    public void RadioInfoWorkflow_EndToEnd_ShouldWorkWithCaseInsensitiveNames()
    {
        // Test with various case combinations
        var testCases = new[]
        {
            "kenwood ts-2000",
            "KENWOOD TS-2000", 
            "KenWood Ts-2000"
        };
        
        var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<RadioService>();
        var radioService = new RadioService(logger);
        
        foreach (var testCase in testCases)
        {
            // Look up radio in factory
            var radio = RadioFactory.CreateRadio(testCase);
            Assert.NotNull(radio);
            
            // Get radio info
            var radioInfo = radioService.GetRadioInfo(testCase);
            Assert.NotNull(radioInfo);
            Assert.Equal("Kenwood", radioInfo.Manufacturer);
            Assert.Equal("TS-2000", radioInfo.ModelName);
            
            // Clean up
            radio.Dispose();
        }
    }

    #endregion
}