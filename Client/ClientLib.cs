using System.Net.Sockets;
using System.Text;
using SharpCAT2.Common.Radio;

namespace SharpCAT2.ClientLib;

/// <summary>
/// Client library for communicating with SharpCAT2 server over TCP
/// </summary>
public class SharpCAT2Client : IDisposable
{
    private TcpClient? _tcpClient;
    private NetworkStream? _networkStream;
    private readonly string _serverHost;
    private readonly int _serverPort;
    private bool _disposed = false;

    /// <summary>
    /// Gets whether the client is currently connected to the server
    /// </summary>
    public bool IsConnected => _tcpClient?.Connected == true;

    /// <summary>
    /// Initializes a new instance of the SharpCAT2Client
    /// </summary>
    /// <param name="serverHost">Server hostname or IP address</param>
    /// <param name="serverPort">Server TCP port</param>
    public SharpCAT2Client(string serverHost = "localhost", int serverPort = 8080)
    {
        _serverHost = serverHost ?? throw new ArgumentNullException(nameof(serverHost));
        _serverPort = serverPort;
    }

    /// <summary>
    /// Connects to the SharpCAT2 server
    /// </summary>
    /// <param name="timeoutMs">Connection timeout in milliseconds</param>
    /// <returns>True if connected successfully, false otherwise</returns>
    public async Task<bool> ConnectAsync(int timeoutMs = 5000)
    {
        try
        {
            _tcpClient = new TcpClient();
            
            using var cancellationTokenSource = new CancellationTokenSource(timeoutMs);
            await _tcpClient.ConnectAsync(_serverHost, _serverPort, cancellationTokenSource.Token);
            
            _networkStream = _tcpClient.GetStream();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Connection failed: {ex.Message}");
            Disconnect();
            return false;
        }
    }

    /// <summary>
    /// Sends a command to the remote serial port and returns the response
    /// </summary>
    /// <param name="command">Command to send</param>
    /// <param name="timeoutMs">Response timeout in milliseconds</param>
    /// <returns>Response from the serial port, or null if error occurred</returns>
    public async Task<string?> SendCommandAsync(string command, int timeoutMs = 5000)
    {
        if (!IsConnected || _networkStream == null)
        {
            Console.WriteLine("Error: Not connected to server");
            return null;
        }

        try
        {
            // Send command to server
            byte[] commandBytes = Encoding.UTF8.GetBytes(command + "\n");
            await _networkStream.WriteAsync(commandBytes);
            await _networkStream.FlushAsync();

            // Read response from server
            var buffer = new byte[1024];
            using var cancellationTokenSource = new CancellationTokenSource(timeoutMs);
            
            int bytesRead = await _networkStream.ReadAsync(buffer, cancellationTokenSource.Token);
            
            if (bytesRead > 0)
            {
                string response = Encoding.UTF8.GetString(buffer, 0, bytesRead).TrimEnd('\n', '\r');
                return response;
            }
            else
            {
                Console.WriteLine("Error: No response received from server");
                return null;
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Error: Command timed out");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending command: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Sends a command to the remote serial port (synchronous version)
    /// </summary>
    /// <param name="command">Command to send</param>
    /// <param name="timeoutMs">Response timeout in milliseconds</param>
    /// <returns>Response from the serial port, or null if error occurred</returns>
    public string? SendCommand(string command, int timeoutMs = 5000)
    {
        return SendCommandAsync(command, timeoutMs).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Sends a radio command to the remote radio and returns the response
    /// </summary>
    /// <param name="command">Radio command to send</param>
    /// <returns>Response from the radio, or null if error occurred</returns>
    public async Task<string?> SendRadioCommandAsync(RadioCommand command)
    {
        return await SendCommandAsync(command.Command, command.TimeoutMs);
    }

    /// <summary>
    /// Sends a radio command to the remote radio (synchronous version)
    /// </summary>
    /// <param name="command">Radio command to send</param>
    /// <returns>Response from the radio, or null if error occurred</returns>
    public string? SendRadioCommand(RadioCommand command)
    {
        return SendRadioCommandAsync(command).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Gets the radio status from the remote server
    /// </summary>
    /// <returns>Response with radio status information</returns>
    public async Task<string?> GetRadioStatusAsync()
    {
        return await SendCommandAsync("s");
    }

    /// <summary>
    /// Gets available radio models from the server
    /// </summary>
    /// <returns>Response with available radio models</returns>
    public async Task<string?> GetAvailableRadiosAsync()
    {
        return await SendCommandAsync("list-radios");
    }

    /// <summary>
    /// Sets the active radio on the server
    /// </summary>
    /// <param name="radioName">Name of the radio to set (e.g., "Kenwood TS-2000")</param>
    /// <returns>Response indicating success or failure</returns>
    public async Task<string?> SetRadioAsync(string radioName)
    {
        if (string.IsNullOrWhiteSpace(radioName))
        {
            return "ERROR: Radio name cannot be empty";
        }
        
        return await SendCommandAsync($"set-radio {radioName}");
    }

    /// <summary>
    /// Gets the current active radio from the server
    /// </summary>
    /// <returns>Response with current radio information</returns>
    public async Task<string?> GetCurrentRadioAsync()
    {
        return await SendCommandAsync("get-current-radio");
    }

    /// <summary>
    /// Disconnects from the server
    /// </summary>
    public void Disconnect()
    {
        try
        {
            _networkStream?.Close();
            _tcpClient?.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during disconnect: {ex.Message}");
        }
        finally
        {
            _networkStream = null;
            _tcpClient = null;
        }
    }

    /// <summary>
    /// Disposes the client and disconnects if connected
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            Disconnect();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}