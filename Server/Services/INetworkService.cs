using System.Net.Sockets;

namespace SharpCAT2.Server.Services;

/// <summary>
/// Service interface for managing network connections and TCP server.
/// This interface has been reviewed and is compliant with C# coding standards.
/// </summary>
public interface INetworkService
{
    /// <summary>
    /// Gets whether the TCP server is running
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Gets the number of connected clients
    /// </summary>
    int ConnectedClientCount { get; }

    /// <summary>
    /// Starts the TCP server on the specified port
    /// </summary>
    /// <param name="port">TCP port to listen on</param>
    /// <param name="cancellationToken">Cancellation token for graceful shutdown</param>
    /// <returns>Task representing the async startup operation</returns>
    Task StartAsync(int port, CancellationToken cancellationToken);

    /// <summary>
    /// Stops the TCP server
    /// </summary>
    /// <returns>Task representing the async shutdown operation</returns>
    Task StopAsync();

    /// <summary>
    /// Sends data to all connected clients
    /// </summary>
    /// <param name="data">Data to send</param>
    /// <returns>Task representing the async broadcast operation</returns>
    Task BroadcastToClientsAsync(string data);

    /// <summary>
    /// Event raised when data is received from a client
    /// </summary>
    event EventHandler<NetworkDataReceivedEventArgs>? DataReceived;

    /// <summary>
    /// Event raised when a client connects
    /// </summary>
    event EventHandler<ClientConnectedEventArgs>? ClientConnected;

    /// <summary>
    /// Event raised when a client disconnects
    /// </summary>
    event EventHandler<ClientDisconnectedEventArgs>? ClientDisconnected;
}

/// <summary>
/// Event arguments for network data received
/// </summary>
public class NetworkDataReceivedEventArgs : EventArgs
{
    public string ClientId { get; }
    public string Data { get; }
    public NetworkStream NetworkStream { get; }

    public NetworkDataReceivedEventArgs(string clientId, string data, NetworkStream networkStream)
    {
        ClientId = clientId;
        Data = data;
        NetworkStream = networkStream;
    }
}

/// <summary>
/// Event arguments for client connected
/// </summary>
public class ClientConnectedEventArgs : EventArgs
{
    public string ClientId { get; }

    public ClientConnectedEventArgs(string clientId)
    {
        ClientId = clientId;
    }
}

/// <summary>
/// Event arguments for client disconnected
/// </summary>
public class ClientDisconnectedEventArgs : EventArgs
{
    public string ClientId { get; }

    public ClientDisconnectedEventArgs(string clientId)
    {
        ClientId = clientId;
    }
}