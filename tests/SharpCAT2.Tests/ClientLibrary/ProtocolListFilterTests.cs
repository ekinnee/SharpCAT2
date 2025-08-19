using Xunit;
using SharpCAT2.ClientLibrary;

namespace SharpCAT2.Tests.ClientLibrary;

/// <summary>
/// Comprehensive tests for the ProtocolListFilter utility class.
/// Tests cover filtering logic, edge cases, extensibility, and error handling.
/// </summary>
public class ProtocolListFilterTests
{
    #region Radio List Tests

    [Fact]
    public void FilterRadioList_ValidResponse_ReturnsCleanList()
    {
        // Arrange
        var protocolResponse = @"RADIO_LIST_START
Kenwood TS-2000|25
Yaesu FT-991A|22
Icom IC-7300|20
RADIO_LIST_END";

        // Act
        var result = ProtocolListFilter.FilterRadioList(protocolResponse);

        // Assert
        Assert.NotNull(result);
        var expectedContent = @"Kenwood TS-2000|25
Yaesu FT-991A|22
Icom IC-7300|20";
        Assert.Equal(expectedContent, result);
    }

    [Fact]
    public void FilterRadioList_EmptyList_ReturnsEmptyString()
    {
        // Arrange
        var protocolResponse = @"RADIO_LIST_START
RADIO_LIST_END";

        // Act
        var result = ProtocolListFilter.FilterRadioList(protocolResponse);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void FilterRadioList_MissingStartMarker_ReturnsEmptyString()
    {
        // Arrange
        var protocolResponse = @"Kenwood TS-2000|25
Yaesu FT-991A|22
RADIO_LIST_END";

        // Act
        var result = ProtocolListFilter.FilterRadioList(protocolResponse);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void FilterRadioList_MissingEndMarker_ReturnsContentAfterStart()
    {
        // Arrange
        var protocolResponse = @"RADIO_LIST_START
Kenwood TS-2000|25
Yaesu FT-991A|22";

        // Act
        var result = ProtocolListFilter.FilterRadioList(protocolResponse);

        // Assert
        Assert.NotNull(result);
        var expectedContent = @"Kenwood TS-2000|25
Yaesu FT-991A|22";
        Assert.Equal(expectedContent, result);
    }

    [Fact]
    public void FilterRadioList_NullInput_ReturnsNull()
    {
        // Act
        var result = ProtocolListFilter.FilterRadioList(null);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void FilterRadioList_WhitespaceLines_IgnoresWhitespace()
    {
        // Arrange
        var protocolResponse = @"RADIO_LIST_START

Kenwood TS-2000|25
   
Yaesu FT-991A|22

RADIO_LIST_END";

        // Act
        var result = ProtocolListFilter.FilterRadioList(protocolResponse);

        // Assert
        Assert.NotNull(result);
        var expectedContent = @"Kenwood TS-2000|25
Yaesu FT-991A|22";
        Assert.Equal(expectedContent, result);
    }

    [Fact]
    public void FilterRadioList_CaseInsensitiveMarkers_WorksCorrectly()
    {
        // Arrange
        var protocolResponse = @"radio_list_start
Kenwood TS-2000|25
RADIO_LIST_END";

        // Act
        var result = ProtocolListFilter.FilterRadioList(protocolResponse);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Kenwood TS-2000|25", result);
    }

    #endregion

    #region Serial Port List Tests

    [Fact]
    public void FilterSerialPortList_ValidResponse_ReturnsCleanList()
    {
        // Arrange
        var protocolResponse = @"SERIALPORT_LIST_START
COM1
COM3
/dev/ttyUSB0
FAKE
SERIALPORT_LIST_END";

        // Act
        var result = ProtocolListFilter.FilterSerialPortList(protocolResponse);

        // Assert
        Assert.NotNull(result);
        var expectedContent = @"COM1
COM3
/dev/ttyUSB0
FAKE";
        Assert.Equal(expectedContent, result);
    }

    [Fact]
    public void FilterSerialPortList_EmptyList_ReturnsEmptyString()
    {
        // Arrange
        var protocolResponse = @"SERIALPORT_LIST_START
SERIALPORT_LIST_END";

        // Act
        var result = ProtocolListFilter.FilterSerialPortList(protocolResponse);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void FilterSerialPortList_NullInput_ReturnsNull()
    {
        // Act
        var result = ProtocolListFilter.FilterSerialPortList(null);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Generic Filter Tests

    [Fact]
    public void FilterList_ValidListType_ReturnsFilteredContent()
    {
        // Arrange
        var protocolResponse = @"RADIO_LIST_START
Test Radio|5
RADIO_LIST_END";

        // Act
        var result = ProtocolListFilter.FilterList(protocolResponse, ProtocolListFilter.ListType.Radios);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test Radio|5", result);
    }

    [Fact]
    public void FilterList_InvalidListType_ThrowsArgumentException()
    {
        // Arrange
        var protocolResponse = "Some content";
        var invalidListType = (ProtocolListFilter.ListType)999;

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            ProtocolListFilter.FilterList(protocolResponse, invalidListType));
        Assert.Contains("Unsupported list type", exception.Message);
    }

    #endregion

    #region Parsing Tests

    [Fact]
    public void ParseRadioEntries_ValidInput_ReturnsArray()
    {
        // Arrange
        var filteredList = @"Kenwood TS-2000|25
Yaesu FT-991A|22
Icom IC-7300|20";

        // Act
        var result = ProtocolListFilter.ParseRadioEntries(filteredList);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Length);
        Assert.Equal("Kenwood TS-2000|25", result[0]);
        Assert.Equal("Yaesu FT-991A|22", result[1]);
        Assert.Equal("Icom IC-7300|20", result[2]);
    }

    [Fact]
    public void ParseRadioEntries_EmptyInput_ReturnsEmptyArray()
    {
        // Act
        var result = ProtocolListFilter.ParseRadioEntries("");

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void ParseRadioEntries_NullInput_ReturnsEmptyArray()
    {
        // Act
        var result = ProtocolListFilter.ParseRadioEntries(null);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void ParseRadioEntries_WhitespaceInput_ReturnsEmptyArray()
    {
        // Act
        var result = ProtocolListFilter.ParseRadioEntries("   \n  \t  ");

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void ParsePortEntries_ValidInput_ReturnsArray()
    {
        // Arrange
        var filteredList = @"COM1
/dev/ttyUSB0
FAKE";

        // Act
        var result = ProtocolListFilter.ParsePortEntries(filteredList);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Length);
        Assert.Equal("COM1", result[0]);
        Assert.Equal("/dev/ttyUSB0", result[1]);
        Assert.Equal("FAKE", result[2]);
    }

    [Fact]
    public void ParsePortEntries_EmptyInput_ReturnsEmptyArray()
    {
        // Act
        var result = ProtocolListFilter.ParsePortEntries("");

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void ParsePortEntries_NullInput_ReturnsEmptyArray()
    {
        // Act
        var result = ProtocolListFilter.ParsePortEntries(null);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    #endregion

    #region Extensibility Tests

    [Fact]
    public void GetSupportedListTypes_ReturnsExpectedTypes()
    {
        // Act
        var supportedTypes = ProtocolListFilter.GetSupportedListTypes();

        // Assert
        Assert.NotNull(supportedTypes);
        Assert.Contains(ProtocolListFilter.ListType.Radios, supportedTypes);
        Assert.Contains(ProtocolListFilter.ListType.SerialPorts, supportedTypes);
    }

    [Fact]
    public void RegisterListType_ValidParameters_RegistersSuccessfully()
    {
        // Note: This test is conceptual as RegisterListType modifies static state
        // In a real implementation, you might want to make this more testable
        
        // Arrange
        var customListType = (ProtocolListFilter.ListType)100;
        var startMarker = "CUSTOM_LIST_START";
        var endMarker = "CUSTOM_LIST_END";

        // Act & Assert - Should not throw
        try
        {
            ProtocolListFilter.RegisterListType(customListType, startMarker, endMarker);
            
            // Verify registration by trying to use it
            var testResponse = $"{startMarker}\nTest Item\n{endMarker}";
            var result = ProtocolListFilter.FilterList(testResponse, customListType);
            Assert.Equal("Test Item", result);
        }
        catch (ArgumentException ex) when (ex.Message.Contains("already registered"))
        {
            // This is expected if the test runs multiple times
            // Skip the assertion in this case
        }
    }

    [Fact]
    public void RegisterListType_NullStartMarker_ThrowsArgumentException()
    {
        // Arrange
        var customListType = (ProtocolListFilter.ListType)101;

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            ProtocolListFilter.RegisterListType(customListType, null!, "END"));
        Assert.Contains("Start marker cannot be null", exception.Message);
    }

    [Fact]
    public void RegisterListType_EmptyEndMarker_ThrowsArgumentException()
    {
        // Arrange
        var customListType = (ProtocolListFilter.ListType)102;

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            ProtocolListFilter.RegisterListType(customListType, "START", ""));
        Assert.Contains("End marker cannot be null", exception.Message);
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public void FilterRadioList_MultipleStartMarkers_FiltersOutDuplicateMarkers()
    {
        // Arrange
        var protocolResponse = @"RADIO_LIST_START
First Radio|1
RADIO_LIST_START
Second Radio|2
RADIO_LIST_END";

        // Act
        var result = ProtocolListFilter.FilterRadioList(protocolResponse);

        // Assert
        Assert.NotNull(result);
        // The implementation correctly filters out the duplicate start marker as noise
        var expectedContent = @"First Radio|1
Second Radio|2";
        Assert.Equal(expectedContent, result);
    }

    [Fact]
    public void FilterRadioList_ContentAfterEndMarker_IgnoresContentAfterEnd()
    {
        // Arrange
        var protocolResponse = @"RADIO_LIST_START
Valid Radio|1
RADIO_LIST_END
Ignored Content
More Ignored Content";

        // Act
        var result = ProtocolListFilter.FilterRadioList(protocolResponse);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Valid Radio|1", result);
    }

    [Fact]
    public void FilterRadioList_OnlyMarkers_ReturnsEmptyString()
    {
        // Arrange
        var protocolResponse = @"Some prefix
RADIO_LIST_START
RADIO_LIST_END
Some suffix";

        // Act
        var result = ProtocolListFilter.FilterRadioList(protocolResponse);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void FilterRadioList_ContentBeforeStartMarker_IgnoresContentBeforeStart()
    {
        // Arrange
        var protocolResponse = @"This should be ignored
Some other content
RADIO_LIST_START
Valid Radio|1
RADIO_LIST_END";

        // Act
        var result = ProtocolListFilter.FilterRadioList(protocolResponse);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Valid Radio|1", result);
    }

    #endregion
}