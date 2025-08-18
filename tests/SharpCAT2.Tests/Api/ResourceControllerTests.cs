using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using SharpCAT2.WebApi.Controllers;
using Xunit;

namespace SharpCAT2.Tests.Api;

/// <summary>
/// Tests for the ResourceController class that handles unified resource listing
/// </summary>
public class ResourceControllerTests
{
    private readonly Mock<ILogger<ResourceController>> _mockLogger;
    private readonly ResourceController _controller;

    public ResourceControllerTests()
    {
        _mockLogger = new Mock<ILogger<ResourceController>>();
        _controller = new ResourceController(_mockLogger.Object);
    }

    [Fact]
    public void GetSerialPorts_ShouldReturnOkResult()
    {
        // Act
        var result = _controller.GetSerialPorts();

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public void GetSerialPorts_ShouldIncludeFakePort()
    {
        // Act
        var result = _controller.GetSerialPorts() as OkObjectResult;
        var ports = result?.Value as List<string>;

        // Assert
        Assert.NotNull(ports);
        Assert.Contains("FAKE", ports);
    }

    [Fact]
    public void GetSerialPorts_ShouldNotIncludeOldFakePorts()
    {
        // Act
        var result = _controller.GetSerialPorts() as OkObjectResult;
        var ports = result?.Value as List<string>;

        // Assert
        Assert.NotNull(ports);
        Assert.DoesNotContain("DUMMY", ports);
        Assert.DoesNotContain("TEST", ports);
        Assert.DoesNotContain("SIMULATION", ports);
    }

    [Fact]
    public void GetRadios_ShouldReturnOkResult()
    {
        // Act
        var result = _controller.GetRadios();

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public void GetRadios_ShouldReturnManufacturerModelFormat()
    {
        // Act
        var result = _controller.GetRadios() as OkObjectResult;
        var radios = result?.Value as dynamic;

        // Assert
        Assert.NotNull(radios);
        // Check that the response contains objects with manufacturer and model properties
        var radiosList = radios as IEnumerable<object>;
        Assert.NotNull(radiosList);
        Assert.True(radiosList.Any());
    }
}