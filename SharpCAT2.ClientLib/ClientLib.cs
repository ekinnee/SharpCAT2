using System.Net.Sockets;
using System.Text;
using SharpCAT2.Common.Radio;
using SharpCAT2.Common.Utils;

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
    private readonly ConnectionHealthMonitor _healthMonitor;
    private readonly Timer _reconnectionTimer;
    private bool _isReconnecting = false;

    /// <summary>
    /// Retry policy for network operations
    /// </summary>
    public RetryPolicy RetryPolicy { get; set; } = RetryPolicy.Network;

    /// <summary>
    /// Whether automatic reconnection is enabled
    /// </summary>
    public bool AutoReconnectEnabled { get; set; } = true;

    /// <summary>
    /// Interval for automatic reconnection attempts
    /// </summary>
    public TimeSpan ReconnectionInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Event raised when connection is lost
    /// </summary>
    public event EventHandler<ClientConnectionLostEventArgs>? ConnectionLost;

    /// <summary>
    /// Event raised when connection is restored
    /// </summary>
    public event EventHandler<ClientConnectionRestoredEventArgs>? ConnectionRestored;

    /// <summary>
    /// Gets whether the client is currently connected to the server
    /// </summary>
    public bool IsConnected => _tcpClient?.Connected == true && _healthMonitor.CurrentState == ConnectionState.Connected;

    /// <summary>
    /// Initializes a new instance of the SharpCAT2Client
    /// </summary>
    /// <param name="serverHost">Server hostname or IP address</param>
    /// <param name="serverPort">Server TCP port</param>
    public SharpCAT2Client(string serverHost = "localhost", int serverPort = 8080)
    {
        _serverHost = serverHost ?? throw new ArgumentNullException(nameof(serverHost));
        _serverPort = serverPort;

        _healthMonitor = new ConnectionHealthMonitor(
            healthCheckInterval: TimeSpan.FromSeconds(30),
            maxConsecutiveFailures: 2);

        _healthMonitor.StateChanged += OnConnectionStateChanged;

        _reconnectionTimer = new Timer(ReconnectionTimerCallback, null, 
            Timeout.InfiniteTimeSpan, ReconnectionInterval);
    }

    /// <summary>
    /// Connects to the SharpCAT2 server with resilient connection handling
    /// </summary>
    /// <param name="timeoutMs">Connection timeout in milliseconds</param>
    /// <returns>True if connected successfully, false otherwise</returns>
    public async Task<bool> ConnectAsync(int timeoutMs = 5000)
    {
        return await RetryHelper.ExecuteWithRetryAsync(
            () => ConnectInternalAsync(timeoutMs),
            RetryPolicy,
            operationName: "Connect to server",
            shouldRetry: ShouldRetryConnection
        );
    }

    /// <summary>
    /// Internal connection logic
    /// </summary>
    private async Task<bool> ConnectInternalAsync(int timeoutMs)
    {
        try
        {
            _tcpClient = new TcpClient();
            
            using var cancellationTokenSource = new CancellationTokenSource(timeoutMs);
            await _tcpClient.ConnectAsync(_serverHost, _serverPort, cancellationTokenSource.Token);
            
            _networkStream = _tcpClient.GetStream();
            _healthMonitor.UpdateState(ConnectionState.Connected);
            _healthMonitor.Start();
            
            Console.WriteLine($"Connected to server {_serverHost}:{_serverPort}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Connection failed: {ex.Message}");
            _healthMonitor.UpdateState(ConnectionState.Failed);
            Disconnect();
            throw; // Re-throw to let retry logic handle it
        }
    }

    /// <summary>
    /// Sends a command to the remote serial port and returns the response with retry logic
    /// </summary>
    /// <param name="command">Command to send</param>
    /// <param name="timeoutMs">Response timeout in milliseconds</param>
    /// <returns>Response from the serial port, or null if error occurred</returns>
    public async Task<string?> SendCommandAsync(string command, int timeoutMs = 5000)
    {
        return await RetryHelper.ExecuteWithRetryAsync(
            () => SendCommandInternalAsync(command, timeoutMs),
            RetryPolicy,
            operationName: $"Send command: {command}",
            shouldRetry: ShouldRetryCommand
        );
    }

    /// <summary>
    /// Internal command sending logic
    /// </summary>
    private async Task<string?> SendCommandInternalAsync(string command, int timeoutMs)
    {
        if (!IsConnected || _networkStream == null)
        {
            if (AutoReconnectEnabled && !_isReconnecting)
            {
                var reconnected = await TryReconnectAsync();
                if (!reconnected)
                {
                    Console.WriteLine("Error: Not connected to server and reconnection failed");
                    return null;
                }
            }
            else
            {
                Console.WriteLine("Error: Not connected to server");
                return null;
            }
        }

        try
        {
            // Send command to server
            byte[] commandBytes = Encoding.UTF8.GetBytes(command + "\n");
            await _networkStream!.WriteAsync(commandBytes);
            await _networkStream.FlushAsync();

            // Read response from server
            var buffer = new byte[1024];
            using var cancellationTokenSource = new CancellationTokenSource(timeoutMs);
            
            int bytesRead = await _networkStream.ReadAsync(buffer, cancellationTokenSource.Token);
            
            if (bytesRead > 0)
            {
                string response = Encoding.UTF8.GetString(buffer, 0, bytesRead).TrimEnd('\n', '\r');
                _healthMonitor.RecordSuccess();
                return response;
            }
            else
            {
                _healthMonitor.RecordFailure();
                throw new InvalidOperationException("No response received from server");
            }
        }
        catch (Exception ex)
        {
            _healthMonitor.RecordFailure();
            Console.WriteLine($"Error sending command: {ex.Message}");
            throw; // Re-throw to let retry logic handle it
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
            _healthMonitor?.Stop();
            _healthMonitor?.UpdateState(ConnectionState.Disconnected);
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
            _healthMonitor?.Dispose();
            _reconnectionTimer?.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    #region Resilience Methods

    /// <summary>
    /// Determines if a connection operation should be retried
    /// </summary>
    private bool ShouldRetryConnection(Exception ex)
    {
        return ex switch
        {
            SocketException => true,
            TimeoutException => true,
            IOException => true,
            InvalidOperationException => true,
            _ => false
        };
    }

    /// <summary>
    /// Determines if a command operation should be retried
    /// </summary>
    private bool ShouldRetryCommand(Exception ex)
    {
        return ex switch
        {
            SocketException => true,
            TimeoutException => false, // Don't retry timeouts to avoid cascading delays
            IOException => true,
            InvalidOperationException => true,
            _ => false
        };
    }

    /// <summary>
    /// Attempts to reconnect to the server
    /// </summary>
    private async Task<bool> TryReconnectAsync()
    {
        if (_isReconnecting || _disposed)
            return false;

        _isReconnecting = true;
        try
        {
            Console.WriteLine($"Attempting to reconnect to {_serverHost}:{_serverPort}...");
            _healthMonitor.UpdateState(ConnectionState.Reconnecting);

            // Close existing connection
            try
            {
                _networkStream?.Close();
                _tcpClient?.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error closing existing connection: {ex.Message}");
            }

            // Wait a moment before attempting to reconnect
            await Task.Delay(1000);

            // Attempt to reconnect
            var success = await ConnectInternalAsync(5000);
            
            if (success)
            {
                _healthMonitor.UpdateState(ConnectionState.Connected);
                Console.WriteLine($"Successfully reconnected to {_serverHost}:{_serverPort}");
                ConnectionRestored?.Invoke(this, new ClientConnectionRestoredEventArgs(_serverHost, _serverPort));
                return true;
            }
            else
            {
                _healthMonitor.UpdateState(ConnectionState.Failed);
                Console.WriteLine($"Failed to reconnect to {_serverHost}:{_serverPort}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during reconnection: {ex.Message}");
            _healthMonitor.UpdateState(ConnectionState.Failed);
            return false;
        }
        finally
        {
            _isReconnecting = false;
        }
    }

    /// <summary>
    /// Timer callback for automatic reconnection attempts
    /// </summary>
    private void ReconnectionTimerCallback(object? state)
    {
        if (_disposed || !AutoReconnectEnabled)
            return;

        if (_healthMonitor.CurrentState == ConnectionState.Failed && !IsConnected)
        {
            _ = Task.Run(TryReconnectAsync);
        }
    }

    /// <summary>
    /// Handles connection state changes from the health monitor
    /// </summary>
    private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        if (e.NewState == ConnectionState.Failed && e.OldState == ConnectionState.Connected)
        {
            ConnectionLost?.Invoke(this, new ClientConnectionLostEventArgs(_serverHost, _serverPort, "Health check failed"));
            
            if (AutoReconnectEnabled)
            {
                _reconnectionTimer.Change(ReconnectionInterval, ReconnectionInterval);
            }
        }
        else if (e.NewState == ConnectionState.Connected && e.OldState != ConnectionState.Connected)
        {
            _reconnectionTimer.Change(Timeout.InfiniteTimeSpan, ReconnectionInterval);
        }
    }

    #endregion
}

/// <summary>
/// Event arguments for client connection lost events
/// </summary>
public class ClientConnectionLostEventArgs : EventArgs
{
    public string ServerHost { get; }
    public int ServerPort { get; }
    public string Reason { get; }
    public DateTime Timestamp { get; }

    public ClientConnectionLostEventArgs(string serverHost, int serverPort, string reason)
    {
        ServerHost = serverHost;
        ServerPort = serverPort;
        Reason = reason;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Event arguments for client connection restored events
/// </summary>
public class ClientConnectionRestoredEventArgs : EventArgs
{
    public string ServerHost { get; }
    public int ServerPort { get; }
    public DateTime Timestamp { get; }

    public ClientConnectionRestoredEventArgs(string serverHost, int serverPort)
    {
        ServerHost = serverHost;
        ServerPort = serverPort;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Represents the result of processing a client command
/// </summary>
public record CommandProcessingResult(bool WasHandled, bool ShouldExit);