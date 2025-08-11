using Microsoft.Extensions.Logging;
using SharpCAT2.Common.Serial;

namespace SharpCAT2.Common.Utils;

/// <summary>
/// Connection state enumeration
/// </summary>
public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Failed
}

/// <summary>
/// Health check result for connection monitoring
/// </summary>
public class HealthCheckResult
{
    public bool IsHealthy { get; init; }
    public string Message { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public TimeSpan ResponseTime { get; init; }
    public Exception? Exception { get; init; }
}

/// <summary>
/// Connection health monitor for tracking connection state and performing health checks
/// </summary>
public class ConnectionHealthMonitor : IDisposable
{
    private readonly ILogger? _logger;
    private readonly Timer _healthCheckTimer;
    private readonly object _lockObject = new();
    private ConnectionState _currentState = ConnectionState.Disconnected;
    private DateTime _lastHealthCheck = DateTime.MinValue;
    private DateTime _lastSuccessfulCheck = DateTime.MinValue;
    private int _consecutiveFailures = 0;
    private bool _disposed = false;

    /// <summary>
    /// Current connection state
    /// </summary>
    public ConnectionState CurrentState 
    { 
        get { lock (_lockObject) { return _currentState; } }
        private set { lock (_lockObject) { _currentState = value; } }
    }

    /// <summary>
    /// Time of last successful health check
    /// </summary>
    public DateTime LastSuccessfulCheck 
    { 
        get { lock (_lockObject) { return _lastSuccessfulCheck; } }
        private set { lock (_lockObject) { _lastSuccessfulCheck = value; } }
    }

    /// <summary>
    /// Number of consecutive health check failures
    /// </summary>
    public int ConsecutiveFailures 
    { 
        get { lock (_lockObject) { return _consecutiveFailures; } }
        private set { lock (_lockObject) { _consecutiveFailures = value; } }
    }

    /// <summary>
    /// Health check interval
    /// </summary>
    public TimeSpan HealthCheckInterval { get; }

    /// <summary>
    /// Maximum consecutive failures before marking connection as failed
    /// </summary>
    public int MaxConsecutiveFailures { get; }

    /// <summary>
    /// Health check timeout
    /// </summary>
    public TimeSpan HealthCheckTimeout { get; }

    /// <summary>
    /// Event raised when connection state changes
    /// </summary>
    public event EventHandler<ConnectionStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Event raised when health check is performed
    /// </summary>
    public event EventHandler<HealthCheckCompletedEventArgs>? HealthCheckCompleted;

    public ConnectionHealthMonitor(
        TimeSpan? healthCheckInterval = null, 
        int maxConsecutiveFailures = 3,
        TimeSpan? healthCheckTimeout = null,
        ILogger? logger = null)
    {
        _logger = logger;
        HealthCheckInterval = healthCheckInterval ?? TimeSpan.FromSeconds(30);
        MaxConsecutiveFailures = maxConsecutiveFailures;
        HealthCheckTimeout = healthCheckTimeout ?? TimeSpan.FromSeconds(5);

        _healthCheckTimer = new Timer(PerformHealthCheck, null, Timeout.InfiniteTimeSpan, HealthCheckInterval);
    }

    /// <summary>
    /// Starts health monitoring
    /// </summary>
    public void Start()
    {
        if (_disposed) return;

        _logger?.LogDebug("Starting connection health monitor");
        _healthCheckTimer.Change(HealthCheckInterval, HealthCheckInterval);
    }

    /// <summary>
    /// Stops health monitoring
    /// </summary>
    public void Stop()
    {
        if (_disposed) return;

        _logger?.LogDebug("Stopping connection health monitor");
        _healthCheckTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>
    /// Updates the connection state
    /// </summary>
    public void UpdateState(ConnectionState newState)
    {
        var oldState = CurrentState;
        if (oldState != newState)
        {
            CurrentState = newState;
            _logger?.LogInformation("Connection state changed from {OldState} to {NewState}", oldState, newState);
            
            // Reset failure count on successful connection
            if (newState == ConnectionState.Connected)
            {
                ConsecutiveFailures = 0;
                LastSuccessfulCheck = DateTime.UtcNow;
            }

            StateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(oldState, newState));
        }
    }

    /// <summary>
    /// Performs a manual health check
    /// </summary>
    public async Task<HealthCheckResult> PerformHealthCheckAsync(Func<CancellationToken, Task<bool>> healthCheckFunc)
    {
        if (_disposed)
            return new HealthCheckResult { IsHealthy = false, Message = "Monitor disposed" };

        var startTime = DateTime.UtcNow;
        
        try
        {
            using var cts = new CancellationTokenSource(HealthCheckTimeout);
            var isHealthy = await healthCheckFunc(cts.Token);
            var responseTime = DateTime.UtcNow - startTime;
            
            var result = new HealthCheckResult
            {
                IsHealthy = isHealthy,
                Message = isHealthy ? "Health check passed" : "Health check failed",
                ResponseTime = responseTime
            };

            ProcessHealthCheckResult(result);
            return result;
        }
        catch (Exception ex)
        {
            var responseTime = DateTime.UtcNow - startTime;
            var result = new HealthCheckResult
            {
                IsHealthy = false,
                Message = $"Health check exception: {ex.Message}",
                ResponseTime = responseTime,
                Exception = ex
            };

            ProcessHealthCheckResult(result);
            return result;
        }
    }

    /// <summary>
    /// Timer callback for automatic health checks
    /// </summary>
    private void PerformHealthCheck(object? state)
    {
        if (_disposed || CurrentState == ConnectionState.Disconnected)
            return;

        try
        {
            // This is a basic health check - derived classes should override
            // For now, just check if enough time has passed since last successful check
            var timeSinceLastSuccess = DateTime.UtcNow - LastSuccessfulCheck;
            var isHealthy = timeSinceLastSuccess < TimeSpan.FromMinutes(5);

            var result = new HealthCheckResult
            {
                IsHealthy = isHealthy,
                Message = isHealthy ? "Basic health check passed" : "Too much time since last successful operation"
            };

            ProcessHealthCheckResult(result);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during automatic health check");
        }
    }

    /// <summary>
    /// Processes health check results and updates state accordingly
    /// </summary>
    private void ProcessHealthCheckResult(HealthCheckResult result)
    {
        lock (_lockObject)
        {
            _lastHealthCheck = result.Timestamp;

            if (result.IsHealthy)
            {
                _consecutiveFailures = 0;
                _lastSuccessfulCheck = result.Timestamp;
                
                if (CurrentState == ConnectionState.Reconnecting)
                {
                    UpdateState(ConnectionState.Connected);
                }
            }
            else
            {
                _consecutiveFailures++;
                
                if (_consecutiveFailures >= MaxConsecutiveFailures && CurrentState == ConnectionState.Connected)
                {
                    UpdateState(ConnectionState.Failed);
                }
            }
        }

        _logger?.LogDebug("Health check result: {IsHealthy}, Failures: {ConsecutiveFailures}, Response: {ResponseTime}ms", 
            result.IsHealthy, ConsecutiveFailures, result.ResponseTime.TotalMilliseconds);

        HealthCheckCompleted?.Invoke(this, new HealthCheckCompletedEventArgs(result));
    }

    /// <summary>
    /// Records a successful operation (resets failure counters)
    /// </summary>
    public void RecordSuccess()
    {
        lock (_lockObject)
        {
            _consecutiveFailures = 0;
            _lastSuccessfulCheck = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Records a failed operation
    /// </summary>
    public void RecordFailure()
    {
        lock (_lockObject)
        {
            _consecutiveFailures++;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _healthCheckTimer?.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Event arguments for connection state changes
/// </summary>
public class ConnectionStateChangedEventArgs : EventArgs
{
    public ConnectionState OldState { get; }
    public ConnectionState NewState { get; }

    public ConnectionStateChangedEventArgs(ConnectionState oldState, ConnectionState newState)
    {
        OldState = oldState;
        NewState = newState;
    }
}

/// <summary>
/// Event arguments for health check completion
/// </summary>
public class HealthCheckCompletedEventArgs : EventArgs
{
    public HealthCheckResult Result { get; }

    public HealthCheckCompletedEventArgs(HealthCheckResult result)
    {
        Result = result;
    }
}