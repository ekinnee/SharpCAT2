using SharpCAT2.ServerLibrary.Radio;
using SharpCAT2.ServerLibrary.Radio.Models.Testing;

namespace SharpCAT2.Tests;

/// <summary>
/// Basic tests for the RadioFactory component
/// </summary>
public class RadioFactoryTests
{
    [Fact]
    public void GetAvailableRadios_ShouldReturnNonEmptyList()
    {
        // Act
        var radios = RadioFactory.GetAvailableRadios();
        
        // Assert
        Assert.NotNull(radios);
        Assert.NotEmpty(radios);
    }

    [Theory]
    [InlineData("DummyRadio")]
    [InlineData("TS-2000")]
    [InlineData("FT-991A")]
    public void CreateRadio_WithValidModel_ShouldReturnRadio(string modelName)
    {
        // Act
        var radio = RadioFactory.CreateRadio(modelName);
        
        // Assert
        Assert.NotNull(radio);
        Assert.Equal(modelName, radio.ModelName);
    }

    [Fact]
    public void CreateRadio_WithInvalidModel_ShouldReturnNull()
    {
        // Act
        var radio = RadioFactory.CreateRadio("NonExistentRadio");
        
        // Assert
        Assert.Null(radio);
    }

    [Fact]
    public void CreateRadio_WithNullModel_ShouldReturnNull()
    {
        // Act
        var radio = RadioFactory.CreateRadio(null!);
        
        // Assert
        Assert.Null(radio);
    }

    [Fact]
    public void CreateRadio_WithEmptyModel_ShouldReturnNull()
    {
        // Act
        var radio = RadioFactory.CreateRadio("");
        
        // Assert
        Assert.Null(radio);
    }
}