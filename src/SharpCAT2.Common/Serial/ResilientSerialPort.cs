using System.IO.Ports;
using Microsoft.Extensions.Logging;
using SharpCAT2.Core.Utils;
using SharpCAT2.Core.Serial;

namespace SharpCAT2.Common.Serial;

/// <summary>
/// Resilient serial port wrapper that adds retry logic and automatic recovery capabilities
/// </summary>
public class ResilientSerialPort : ISerialPort
{
    private readonly ISerialPort _innerPort;
    private readonly ILogger? _logger;
    private readonly ConnectionHealthMonitor _healthMonitor;
    private readonly Timer _reconnectionTimer;
    private readonly object _lockObject = new();
    private readonly string _portName;
    private readonly int _baudRate;
    private bool _disposed = false;
    private bool _isReconnecting = false;

    /// <summary>
    /// Retry policy for serial operations
    /// </summary>
    public RetryPolicy RetryPolicy { get; set; } = RetryPolicy.Serial;

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
    public event EventHandler<ConnectionLostEventArgs>? ConnectionLost;

    /// <summary>
    /// Event raised when connection is restored
    /// </summary>
    public event EventHandler<ConnectionRestoredEventArgs>? ConnectionRestored;

    public ResilientSerialPort(ISerialPort innerPort, ILogger? logger = null)
    {
        _innerPort = innerPort ?? throw new ArgumentNullException(nameof(innerPort));
        _logger = logger;
        _portName = innerPort.PortName;
        _baudRate = innerPort.BaudRate;

        _healthMonitor = new ConnectionHealthMonitor(
            healthCheckInterval: TimeSpan.FromSeconds(30),
            maxConsecutiveFailures: 2,
            logger: logger);

        _healthMonitor.StateChanged += OnConnectionStateChanged;

        _reconnectionTimer = new Timer(ReconnectionTimerCallback, null, Timeout.InfiniteTimeSpan, ReconnectionInterval);

        // Forward the data received event
        _innerPort.DataReceived += OnInnerPortDataReceived;
    }

    #region ISerialPort Implementation

    public bool IsOpen 
    { 
        get 
        { 
            try 
            { 
                return _innerPort.IsOpen; 
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error checking if serial port is open");
                return false;
            }
        } 
    }

    public string PortName => _innerPort.PortName;
    public int BaudRate => _innerPort.BaudRate;

    public int BytesToRead 
    { 
        get 
        { 
            try 
            { 
                return _innerPort.BytesToRead; 
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error getting bytes to read from serial port");
                return 0;
            }
        } 
    }

    public int ReadTimeout 
    { 
        get => _innerPort.ReadTimeout; 
        set => _innerPort.ReadTimeout = value; 
    }

    public int WriteTimeout 
    { 
        get => _innerPort.WriteTimeout; 
        set => _innerPort.WriteTimeout = value; 
    }

    public event SerialDataReceivedEventHandler? DataReceived;

    public void Open()
    {
        try
        {
            if (!_innerPort.IsOpen)
            {
                _innerPort.Open();
                _healthMonitor.UpdateState(ConnectionState.Connected);
                _healthMonitor.Start();
                _logger?.LogInformation("Serial port {PortName} opened successfully", _portName);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to open serial port {PortName}", _portName);
            _healthMonitor.UpdateState(ConnectionState.Failed);
            throw;
        }
    }

    public void Close()
    {
        try
        {
            _healthMonitor.Stop();
            _healthMonitor.UpdateState(ConnectionState.Disconnected);
            
            if (_innerPort.IsOpen)
            {
                _innerPort.Close();
                _logger?.LogInformation("Serial port {PortName} closed", _portName);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error closing serial port {PortName}", _portName);
        }
    }

    public void Write(string data)
    {
        ExecuteWithRetry(() => _innerPort.Write(data), "Write", data);
    }

    public void WriteLine(string data)
    {
        ExecuteWithRetry(() => _innerPort.WriteLine(data), "WriteLine", data);
    }

    public string ReadExisting()
    {
        return ExecuteWithRetry(() => _innerPort.ReadExisting(), "ReadExisting");
    }

    public string ReadLine()
    {
        return ExecuteWithRetry(() => _innerPort.ReadLine(), "ReadLine");
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        return ExecuteWithRetry(() => _innerPort.Read(buffer, offset, count), "Read");
    }

    public void DiscardInBuffer()
    {
        ExecuteWithRetry(() => _innerPort.DiscardInBuffer(), "DiscardInBuffer");
    }

    public void DiscardOutBuffer()
    {
        ExecuteWithRetry(() => _innerPort.DiscardOutBuffer(), "DiscardOutBuffer");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _healthMonitor?.Stop();
            _reconnectionTimer?.Dispose();
            _healthMonitor?.Dispose();
            _innerPort?.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    #endregion

    #region Resilience Methods

    /// <summary>
    /// Executes an operation with retry logic and connection recovery
    /// </summary>
    private T ExecuteWithRetry<T>(Func<T> operation, string operationName, object? context = null)
    {
        return RetryHelper.ExecuteWithRetryAsync(
            () => Task.FromResult(operation()),
            RetryPolicy,
            _logger,
            $"Serial {operationName}",
            ShouldRetrySerialOperation,
            CancellationToken.None
        ).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Executes a void operation with retry logic and connection recovery
    /// </summary>
    private void ExecuteWithRetry(Action operation, string operationName, object? context = null)
    {
        ExecuteWithRetry(() => { operation(); return true; }, operationName, context);
    }

    /// <summary>
    /// Determines if a serial operation should be retried based on the exception
    /// </summary>
    private bool ShouldRetrySerialOperation(Exception ex)
    {
        var shouldRetry = ex switch
        {
            InvalidOperationException when ex.Message.Contains("port is closed") => true,
            InvalidOperationException when ex.Message.Contains("port is not open") => true,
            System.IO.IOException => true,
            TimeoutException => true,
            UnauthorizedAccessException => false, // Don't retry permission errors
            _ => false
        };

        if (shouldRetry)
        {
            // Attempt reconnection if the port appears to be disconnected
            if (!IsOpen && AutoReconnectEnabled)
            {
                _logger?.LogWarning("Serial port appears disconnected, attempting reconnection");
                TryReconnect();
            }
        }

        return shouldRetry;
    }

    /// <summary>
    /// Attempts to reconnect the serial port
    /// </summary>
    private bool TryReconnect()
    {
        lock (_lockObject)
        {
            if (_isReconnecting || _disposed)
                return false;

            _isReconnecting = true;
        }

        try
        {
            _logger?.LogInformation("Attempting to reconnect serial port {PortName}", _portName);
            _healthMonitor.UpdateState(ConnectionState.Reconnecting);

            // Close existing connection if it's still open
            try
            {
                if (_innerPort.IsOpen)
                {
                    _innerPort.Close();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error closing serial port during reconnection");
            }

            // Wait a moment before attempting to reopen
            Thread.Sleep(1000);

            // Attempt to reopen
            _innerPort.Open();
            
            _healthMonitor.UpdateState(ConnectionState.Connected);
            _healthMonitor.RecordSuccess();
            
            _logger?.LogInformation("Successfully reconnected serial port {PortName}", _portName);
            ConnectionRestored?.Invoke(this, new ConnectionRestoredEventArgs(_portName));
            
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to reconnect serial port {PortName}", _portName);
            _healthMonitor.UpdateState(ConnectionState.Failed);
            _healthMonitor.RecordFailure();
            return false;
        }
        finally
        {
            lock (_lockObject)
            {
                _isReconnecting = false;
            }
        }
    }

    /// <summary>
    /// Timer callback for automatic reconnection attempts
    /// </summary>
    private void ReconnectionTimerCallback(object? state)
    {
        if (_disposed || !AutoReconnectEnabled)
            return;

        if (_healthMonitor.CurrentState == ConnectionState.Failed && !IsOpen)
        {
            TryReconnect();
        }
    }

    /// <summary>
    /// Handles connection state changes from the health monitor
    /// </summary>
    private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        if (e.NewState == ConnectionState.Failed && e.OldState == ConnectionState.Connected)
        {
            ConnectionLost?.Invoke(this, new ConnectionLostEventArgs(_portName, "Connection health check failed"));
            
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

    /// <summary>
    /// Forwards data received events from the inner port
    /// </summary>
    private void OnInnerPortDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            _healthMonitor.RecordSuccess();
            DataReceived?.Invoke(sender, e);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error in data received event handler");
        }
    }

    #endregion
}

/// <summary>
/// Event arguments for connection lost events
/// </summary>
public class ConnectionLostEventArgs : EventArgs
{
    public string PortName { get; }
    public string Reason { get; }
    public DateTime Timestamp { get; }

    public ConnectionLostEventArgs(string portName, string reason)
    {
        PortName = portName;
        Reason = reason;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Event arguments for connection restored events
/// </summary>
public class ConnectionRestoredEventArgs : EventArgs
{
    public string PortName { get; }
    public DateTime Timestamp { get; }

    public ConnectionRestoredEventArgs(string portName)
    {
        PortName = portName;
        Timestamp = DateTime.UtcNow;
    }
}