using System.Net.Sockets;

namespace SharpCAT2.Core.Services;

/// <summary>
/// Interface for handling network response operations.
/// Separates network communication logic from business logic.
/// </summary>
public interface INetworkResponseService
{
    /// <summary>
    /// Sends a formatted response to a TCP client
    /// </summary>
    /// <param name="networkStream">Network stream to send response</param>
    /// <param name="response">Response text to send</param>
    /// <returns>Task representing the async operation</returns>
    Task SendResponseAsync(NetworkStream networkStream, string response);

    /// <summary>
    /// Handles network data received from clients
    /// </summary>
    /// <param name="clientId">Client identifier</param>
    /// <param name="data">Data received from client</param>
    /// <param name="networkStream">Network stream for responses</param>
    /// <returns>Task representing the async operation</returns>
    Task HandleNetworkDataAsync(string clientId, string data, NetworkStream networkStream);
}