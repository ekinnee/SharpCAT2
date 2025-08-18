using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SharpCAT2.Core.Services;
using System.Net;
using System.Net.Sockets;
using Xunit;

namespace SharpCAT2.Tests.Integration;

/// <summary>
/// Integration tests for network security features
/// </summary>
public class NetworkSecurityIntegrationTests : IDisposable
{
    private readonly IHost _host;
    private readonly INetworkService _networkService;
    private readonly ISecurityService _securityService;
    private const int TestPort = 18080; // Use different port to avoid conflicts

    public NetworkSecurityIntegrationTests()
    {
        // Create a test host with services
        _host = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Warning); // Reduce test noise
            })
            .ConfigureServices(services =>
            {
                services.AddSingleton<ISecurityService, SecurityService>();
                services.AddSingleton<INetworkService, NetworkService>();
            })
            .Build();

        _networkService = _host.Services.GetRequiredService<INetworkService>();
        _securityService = _host.Services.GetRequiredService<ISecurityService>();
    }

    [Fact]
    public async Task NetworkService_WithSecurityService_ShouldStartAndStopSuccessfully()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        
        // Act
        await _networkService.StartAsync(TestPort, cts.Token);
        
        // Assert
        Assert.True(_networkService.IsRunning);
        Assert.Equal(0, _networkService.ConnectedClientCount);
        
        // Cleanup
        await _networkService.StopAsync();
        Assert.False(_networkService.IsRunning);
    }

    [Fact]
    public async Task NetworkService_ShouldAcceptLocalConnections()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await _networkService.StartAsync(TestPort, cts.Token);
        
        var clientConnectedEvent = new TaskCompletionSource<string>();
        var clientDisconnectedEvent = new TaskCompletionSource<string>();
        
        _networkService.ClientConnected += (sender, args) => clientConnectedEvent.SetResult(args.ClientId);
        _networkService.ClientDisconnected += (sender, args) => clientDisconnectedEvent.SetResult(args.ClientId);
        
        // Act
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, TestPort);
        
        // Assert
        var connectedClientId = await clientConnectedEvent.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(connectedClientId);
        Assert.Contains("127.0.0.1", connectedClientId);
        
        // Cleanup
        client.Close();
        var disconnectedClientId = await clientDisconnectedEvent.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(connectedClientId, disconnectedClientId);
        
        await _networkService.StopAsync();
    }

    [Fact]
    public void SecurityService_ShouldAllowPrivateNetworkConnections()
    {
        // Arrange & Act & Assert
        var localhostEndpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 12345);
        var privateNetworkEndpoint = new IPEndPoint(IPAddress.Parse("192.168.1.100"), 12345);
        var publicEndpoint = new IPEndPoint(IPAddress.Parse("8.8.8.8"), 12345);
        
        Assert.True(_securityService.IsClientAllowed(localhostEndpoint));
        Assert.True(_securityService.IsClientAllowed(privateNetworkEndpoint));
        Assert.False(_securityService.IsClientAllowed(publicEndpoint));
    }

    [Fact]
    public void SecurityService_RateLimiting_ShouldTrackConnectionAttempts()
    {
        // Arrange
        var endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 12345);
        
        // Act & Assert - First connection should not be rate limited
        Assert.False(_securityService.IsClientRateLimited(endpoint));
        
        // Record connection attempt
        _securityService.RecordConnectionAttempt(endpoint);
        
        // Should still not be rate limited after one attempt
        Assert.False(_securityService.IsClientRateLimited(endpoint));
    }

    [Fact]
    public async Task NetworkService_ShouldBroadcastToConnectedClients()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await _networkService.StartAsync(TestPort, cts.Token);
        
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, TestPort);
        
        // Wait a moment for connection to be established
        await Task.Delay(100);
        
        // Act
        await _networkService.BroadcastToClientsAsync("Test message");
        
        // Assert
        var buffer = new byte[1024];
        var stream = client.GetStream();
        stream.ReadTimeout = 5000; // 5 second timeout
        
        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
        Assert.True(bytesRead > 0);
        
        var message = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
        Assert.Equal("Test message", message);
        
        // Cleanup
        client.Close();
        await _networkService.StopAsync();
    }

    public void Dispose()
    {
        _networkService?.StopAsync().Wait(TimeSpan.FromSeconds(5));
        _host?.Dispose();
        GC.SuppressFinalize(this);
    }
}