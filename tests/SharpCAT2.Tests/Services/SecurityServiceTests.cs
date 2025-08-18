using Microsoft.Extensions.Logging;
using Moq;
using SharpCAT2.Core.Services;
using System.Net;
using Xunit;

namespace SharpCAT2.Tests.Services;

/// <summary>
/// Unit tests for the SecurityService class
/// </summary>
public class SecurityServiceTests
{
    private readonly Mock<ILogger<SecurityService>> _mockLogger;
    private readonly SecurityService _securityService;

    public SecurityServiceTests()
    {
        _mockLogger = new Mock<ILogger<SecurityService>>();
        _securityService = new SecurityService(_mockLogger.Object);
    }

    [Fact]
    public void IsClientAllowed_WithLocalhostIPv4_ShouldReturnTrue()
    {
        // Arrange
        var endPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 12345);

        // Act
        var result = _securityService.IsClientAllowed(endPoint);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsClientAllowed_WithLocalhostIPv6_ShouldReturnTrue()
    {
        // Arrange
        var endPoint = new IPEndPoint(IPAddress.IPv6Loopback, 12345);

        // Act
        var result = _securityService.IsClientAllowed(endPoint);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsClientAllowed_WithPrivateNetworkIP_ShouldReturnTrue()
    {
        // Arrange
        var endPoint = new IPEndPoint(IPAddress.Parse("192.168.1.100"), 12345);

        // Act
        var result = _securityService.IsClientAllowed(endPoint);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsClientAllowed_WithPublicIP_ShouldReturnFalse()
    {
        // Arrange
        var endPoint = new IPEndPoint(IPAddress.Parse("8.8.8.8"), 12345);

        // Act
        var result = _securityService.IsClientAllowed(endPoint);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateAuthentication_WithoutRequiredAuth_ShouldReturnTrue()
    {
        // Arrange
        var endPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 12345);

        // Act
        var result = _securityService.ValidateAuthentication(endPoint, null);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void RecordConnectionAttempt_ShouldNotThrow()
    {
        // Arrange
        var endPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 12345);

        // Act & Assert
        var exception = Record.Exception(() => _securityService.RecordConnectionAttempt(endPoint));
        Assert.Null(exception);
    }

    [Fact]
    public void IsClientRateLimited_WithNewClient_ShouldReturnFalse()
    {
        // Arrange
        var endPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 12345);

        // Act
        var result = _securityService.IsClientRateLimited(endPoint);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void AllowedIpRanges_ShouldContainLocalhost()
    {
        // Act
        var allowedRanges = _securityService.AllowedIpRanges;

        // Assert
        Assert.Contains("127.0.0.1", allowedRanges);
        Assert.Contains("::1", allowedRanges);
    }

    [Fact]
    public void RateLimitWindow_ShouldBeOneMinute()
    {
        // Act
        var window = _securityService.RateLimitWindow;

        // Assert
        Assert.Equal(TimeSpan.FromMinutes(1), window);
    }

    [Fact]
    public void MaxConnectionsPerWindow_ShouldBeTen()
    {
        // Act
        var maxConnections = _securityService.MaxConnectionsPerWindow;

        // Assert
        Assert.Equal(10, maxConnections);
    }
}