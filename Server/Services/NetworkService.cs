using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SharpCAT2.Server.Services;

/// <summary>
/// Implementation of network service for managing TCP server and client connections
/// </summary>
public class NetworkService : INetworkService, IDisposable
{
    private readonly ILogger<NetworkService> _logger;
    private readonly ISecurityService _securityService;
    private readonly ConcurrentDictionary<string, NetworkStream> _tcpClients = new();
    private TcpListener? _tcpListener;
    private CancellationTokenSource? _cancellationTokenSource;

    /// <summary>
    /// Default buffer size for TCP network communication
    /// </summary>
    private const int DefaultNetworkBufferSize = 1024;

    public NetworkService(ILogger<NetworkService> logger, ISecurityService securityService)
    {
        _logger = logger;
        _securityService = securityService;
    }

    /// <inheritdoc />
    public bool IsRunning => _tcpListener != null;

    /// <inheritdoc />
    public int ConnectedClientCount => _tcpClients.Count;

    /// <inheritdoc />
    public event EventHandler<NetworkDataReceivedEventArgs>? DataReceived;

    /// <inheritdoc />
    public event EventHandler<ClientConnectedEventArgs>? ClientConnected;

    /// <inheritdoc />
    public event EventHandler<ClientDisconnectedEventArgs>? ClientDisconnected;

    /// <inheritdoc />
    public async Task StartAsync(int port, CancellationToken cancellationToken)
    {
        if (_tcpListener != null)
        {
            _logger.LogWarning("TCP server is already running");
            return;
        }

        _tcpListener = new TcpListener(IPAddress.Any, port);
        _tcpListener.Start();
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _logger.LogInformation("TCP server started on port {Port}", port);

        // Accept TCP clients in the background
        _ = Task.Run(async () => await AcceptClientsAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
        
        // Wait a moment to ensure the listener is ready
        await Task.Delay(100, cancellationToken);
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        if (_tcpListener != null)
        {
            _logger.LogInformation("Stopping TCP server...");
            
            _cancellationTokenSource?.Cancel();
            _tcpListener.Stop();
            _tcpListener = null;

            // Close all client connections
            var clientsToClose = _tcpClients.ToArray();
            foreach (var kvp in clientsToClose)
            {
                try
                {
                    kvp.Value.Close();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error closing client connection {ClientId}", kvp.Key);
                }
            }
            _tcpClients.Clear();

            _logger.LogInformation("TCP server stopped");
        }

        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task BroadcastToClientsAsync(string data)
    {
        var clientsToRemove = new List<string>();
        var dataBytes = Encoding.UTF8.GetBytes(data);
        
        // Take a snapshot of current clients to avoid concurrent modification issues
        var currentClients = _tcpClients.ToArray();
        
        foreach (var kvp in currentClients)
        {
            try
            {
                // Check if client still exists (might have been removed)
                if (_tcpClients.ContainsKey(kvp.Key))
                {
                    await kvp.Value.WriteAsync(dataBytes, 0, dataBytes.Length);
                    await kvp.Value.FlushAsync();
                }
            }
            catch (ObjectDisposedException)
            {
                // Stream was disposed, mark for removal
                clientsToRemove.Add(kvp.Key);
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Network I/O error sending to TCP client {ClientId}", kvp.Key);
                clientsToRemove.Add(kvp.Key);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation sending to TCP client {ClientId}", kvp.Key);
                clientsToRemove.Add(kvp.Key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error sending to TCP client {ClientId}", kvp.Key);
                clientsToRemove.Add(kvp.Key);
            }
        }
        
        // Remove disconnected clients
        foreach (string clientId in clientsToRemove)
        {
            RemoveClient(clientId);
        }
    }

    public void Dispose()
    {
        StopAsync().Wait(TimeSpan.FromSeconds(5));
        _cancellationTokenSource?.Dispose();
        GC.SuppressFinalize(this);
    }

    #region Private Methods

    private async Task AcceptClientsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _tcpListener != null)
        {
            try
            {
                var tcpClient = await _tcpListener.AcceptTcpClientAsync();
                var clientEndPoint = tcpClient.Client.RemoteEndPoint;
                var clientId = $"{clientEndPoint}";

                // Apply security checks
                _securityService.RecordConnectionAttempt(clientEndPoint!);

                if (!_securityService.IsClientAllowed(clientEndPoint!))
                {
                    _logger.LogWarning("Connection denied from {ClientId} - IP not allowed", clientId);
                    tcpClient.Close();
                    continue;
                }

                if (_securityService.IsClientRateLimited(clientEndPoint!))
                {
                    _logger.LogWarning("Connection denied from {ClientId} - rate limited", clientId);
                    tcpClient.Close();
                    continue;
                }

                var networkStream = tcpClient.GetStream();
                _tcpClients[clientId] = networkStream;
                
                _logger.LogInformation("TCP client connected: {ClientId}", clientId);
                ClientConnected?.Invoke(this, new ClientConnectedEventArgs(clientId));
                
                // Handle client communication in background with proper disposal
                _ = Task.Run(async () => await HandleClientAsync(clientId, tcpClient, networkStream, cancellationToken));
            }
            catch (ObjectDisposedException)
            {
                // TCP listener was stopped
                break;
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.OperationAborted)
            {
                // TCP listener was stopped
                break;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not listening"))
            {
                // TCP listener was stopped
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting TCP client");
            }
        }
    }

    private async Task HandleClientAsync(string clientId, TcpClient tcpClient, NetworkStream networkStream, CancellationToken cancellationToken)
    {
        try
        {
            // Ensure proper disposal of client and stream
            using (tcpClient)
            using (networkStream)
            {
                var buffer = new byte[DefaultNetworkBufferSize];
                
                while (!cancellationToken.IsCancellationRequested && tcpClient.Connected)
                {
                    int bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    
                    if (bytesRead > 0)
                    {
                        string command = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                        _logger.LogDebug("TCP client {ClientId} sent: {Command}", clientId, command);
                        
                        // Raise data received event
                        DataReceived?.Invoke(this, new NetworkDataReceivedEventArgs(clientId, command, networkStream));
                    }
                    else
                    {
                        break; // Client disconnected
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Cancellation is expected, don't log as error
        }
        catch (IOException ex)
        {
            _logger.LogInformation("Network I/O error with TCP client {ClientId}: {Message}", clientId, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling TCP client {ClientId}", clientId);
        }
        finally
        {
            RemoveClient(clientId);
        }
    }

    private void RemoveClient(string clientId)
    {
        if (_tcpClients.TryRemove(clientId, out _))
        {
            _logger.LogInformation("TCP client disconnected: {ClientId}", clientId);
            ClientDisconnected?.Invoke(this, new ClientDisconnectedEventArgs(clientId));
        }
    }

    #endregion
}