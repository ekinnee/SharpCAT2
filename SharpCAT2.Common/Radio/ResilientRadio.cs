using Microsoft.Extensions.Logging;
using SharpCAT2.Common.Serial;
using SharpCAT2.Common.Utils;

namespace SharpCAT2.Common.Radio;

/// <summary>
/// Resilient radio wrapper that adds retry logic and automatic recovery capabilities
/// </summary>
public class ResilientRadio : IRadio
{
    private readonly IRadio _innerRadio;
    private readonly ILogger? _logger;
    private readonly ConnectionHealthMonitor _healthMonitor;
    private readonly Timer _healthCheckTimer;
    private bool _disposed = false;
    private ISerialPort? _currentSerialPort;

    /// <summary>
    /// Retry policy for radio operations
    /// </summary>
    public RetryPolicy RetryPolicy { get; set; } = RetryPolicy.Radio;

    /// <summary>
    /// Whether automatic recovery is enabled
    /// </summary>
    public bool AutoRecoveryEnabled { get; set; } = true;

    /// <summary>
    /// Interval for radio health checks
    /// </summary>
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Event raised when radio connection is lost
    /// </summary>
    public event EventHandler<RadioConnectionLostEventArgs>? ConnectionLost;

    /// <summary>
    /// Event raised when radio connection is restored
    /// </summary>
    public event EventHandler<RadioConnectionRestoredEventArgs>? ConnectionRestored;

    public ResilientRadio(IRadio innerRadio, ILogger? logger = null)
    {
        _innerRadio = innerRadio ?? throw new ArgumentNullException(nameof(innerRadio));
        _logger = logger;

        _healthMonitor = new ConnectionHealthMonitor(
            healthCheckInterval: HealthCheckInterval,
            maxConsecutiveFailures: 2,
            logger: logger);

        _healthMonitor.StateChanged += OnConnectionStateChanged;

        _healthCheckTimer = new Timer(PerformRadioHealthCheck, null, 
            Timeout.InfiniteTimeSpan, HealthCheckInterval);
    }

    #region IRadio Implementation

    public string ModelName => _innerRadio.ModelName;
    public string Manufacturer => _innerRadio.Manufacturer;
    public SupportedFeatures SupportedFeatures => _innerRadio.SupportedFeatures;

    public bool IsConnected => _innerRadio.IsConnected && _healthMonitor.CurrentState == ConnectionState.Connected;

    public long Frequency 
    { 
        get => _innerRadio.Frequency; 
        set => _innerRadio.Frequency = value; 
    }

    public string Mode 
    { 
        get => _innerRadio.Mode; 
        set => _innerRadio.Mode = value; 
    }

    public async Task<bool> ConnectAsync(ISerialPort port)
    {
        try
        {
            _currentSerialPort = port;
            var result = await _innerRadio.ConnectAsync(port);
            
            if (result)
            {
                _healthMonitor.UpdateState(ConnectionState.Connected);
                _healthCheckTimer.Change(HealthCheckInterval, HealthCheckInterval);
                _logger?.LogInformation("Radio {Manufacturer} {ModelName} connected successfully", 
                    Manufacturer, ModelName);
            }
            else
            {
                _healthMonitor.UpdateState(ConnectionState.Failed);
                _logger?.LogWarning("Failed to connect to radio {Manufacturer} {ModelName}", 
                    Manufacturer, ModelName);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error connecting to radio {Manufacturer} {ModelName}", 
                Manufacturer, ModelName);
            _healthMonitor.UpdateState(ConnectionState.Failed);
            throw;
        }
    }

    public void Disconnect()
    {
        try
        {
            _healthCheckTimer.Change(Timeout.InfiniteTimeSpan, HealthCheckInterval);
            _healthMonitor.UpdateState(ConnectionState.Disconnected);
            _innerRadio.Disconnect();
            _logger?.LogInformation("Radio {Manufacturer} {ModelName} disconnected", 
                Manufacturer, ModelName);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error disconnecting radio {Manufacturer} {ModelName}", 
                Manufacturer, ModelName);
        }
    }

    public async Task<string?> SendCommandAsync(RadioCommand command)
    {
        return await ExecuteWithRetryAsync(
            () => _innerRadio.SendCommandAsync(command),
            $"SendCommand({command.Command})"
        );
    }

    public async Task<RadioStatus> GetStatusAsync()
    {
        return await ExecuteWithRetryAsync(
            () => _innerRadio.GetStatusAsync(),
            "GetStatus"
        );
    }

    public async Task<bool> SetFrequencyAsync(long frequency)
    {
        return await ExecuteWithRetryAsync(
            () => _innerRadio.SetFrequencyAsync(frequency),
            $"SetFrequency({frequency})"
        );
    }

    public async Task<bool> SetModeAsync(string mode)
    {
        return await ExecuteWithRetryAsync(
            () => _innerRadio.SetModeAsync(mode),
            $"SetMode({mode})"
        );
    }

    // Extended radio control methods with retry logic
    public async Task<string> GetVfoAsync()
    {
        return await ExecuteWithRetryAsync(() => _innerRadio.GetVfoAsync(), "GetVfo");
    }

    public async Task<bool> SetVfoAsync(string vfo)
    {
        return await ExecuteWithRetryAsync(() => _innerRadio.SetVfoAsync(vfo), $"SetVfo({vfo})");
    }

    public async Task<bool> SwapVfoAsync()
    {
        return await ExecuteWithRetryAsync(() => _innerRadio.SwapVfoAsync(), "SwapVfo");
    }

    public async Task<bool> SetSplitAsync(bool enabled)
    {
        return await ExecuteWithRetryAsync(() => _innerRadio.SetSplitAsync(enabled), $"SetSplit({enabled})");
    }

    public async Task<bool> GetSplitAsync()
    {
        return await ExecuteWithRetryAsync(() => _innerRadio.GetSplitAsync(), "GetSplit");
    }

    public async Task<bool> SetRitAsync(int offsetHz)
    {
        return await ExecuteWithRetryAsync(() => _innerRadio.SetRitAsync(offsetHz), $"SetRit({offsetHz})");
    }

    public async Task<int> GetRitAsync()
    {
        return await ExecuteWithRetryAsync(() => _innerRadio.GetRitAsync(), "GetRit");
    }

    public async Task<bool> SetXitAsync(int offsetHz)
    {
        return await ExecuteWithRetryAsync(() => _innerRadio.SetXitAsync(offsetHz), $"SetXit({offsetHz})");
    }

    public async Task<int> GetXitAsync()
    {
        return await ExecuteWithRetryAsync(() => _innerRadio.GetXitAsync(), "GetXit");
    }

    public async Task<bool> SetIfBandwidthAsync(int bandwidth)
    {
        return await ExecuteWithRetryAsync(() => _innerRadio.SetIfBandwidthAsync(bandwidth), $"SetIfBandwidth({bandwidth})");
    }

    public async Task<int> GetIfBandwidthAsync()
    {
        return await ExecuteWithRetryAsync(() => _innerRadio.GetIfBandwidthAsync(), "GetIfBandwidth");
    }

    public async Task<bool> SetPowerOutputAsync(int powerPercent)
    {
        return await ExecuteWithRetryAsync(() => _innerRadio.SetPowerOutputAsync(powerPercent), $"SetPowerOutput({powerPercent})");
    }

    public async Task<int> GetPowerOutputAsync()
    {
        return await ExecuteWithRetryAsync(() => _innerRadio.GetPowerOutputAsync(), "GetPowerOutput");
    }

    public async Task<int> GetSMeterAsync()
    {
        return await ExecuteWithRetryAsync(() => _innerRadio.GetSMeterAsync(), "GetSMeter");
    }

    public async Task<double> GetSWRAsync()
    {
        return await ExecuteWithRetryAsync(() => _innerRadio.GetSWRAsync(), "GetSWR");
    }

    public async Task<bool> SetAntennaAsync(int antenna)
    {
        return await ExecuteWithRetryAsync(() => _innerRadio.SetAntennaAsync(antenna), $"SetAntenna({antenna})");
    }

    public async Task<int> GetAntennaAsync()
    {
        return await ExecuteWithRetryAsync(() => _innerRadio.GetAntennaAsync(), "GetAntenna");
    }

    public async Task<bool> SetMemoryChannelAsync(int channel, long frequency, string mode)
    {
        return await ExecuteWithRetryAsync(() => _innerRadio.SetMemoryChannelAsync(channel, frequency, mode), 
            $"SetMemoryChannel({channel}, {frequency}, {mode})");
    }

    public async Task<bool> RecallMemoryChannelAsync(int channel)
    {
        return await ExecuteWithRetryAsync(() => _innerRadio.RecallMemoryChannelAsync(channel), $"RecallMemoryChannel({channel})");
    }

    public async Task<bool> SetCwSpeedAsync(int wpm)
    {
        return await ExecuteWithRetryAsync(() => _innerRadio.SetCwSpeedAsync(wpm), $"SetCwSpeed({wpm})");
    }

    public async Task<int> GetCwSpeedAsync()
    {
        return await ExecuteWithRetryAsync(() => _innerRadio.GetCwSpeedAsync(), "GetCwSpeed");
    }

    public async Task<bool> SendCwMessageAsync(string message)
    {
        return await ExecuteWithRetryAsync(() => _innerRadio.SendCwMessageAsync(message), $"SendCwMessage({message})");
    }

    public async Task<bool> SetNoiseReductionAsync(int level)
    {
        return await ExecuteWithRetryAsync(() => _innerRadio.SetNoiseReductionAsync(level), $"SetNoiseReduction({level})");
    }

    public async Task<int> GetNoiseReductionAsync()
    {
        return await ExecuteWithRetryAsync(() => _innerRadio.GetNoiseReductionAsync(), "GetNoiseReduction");
    }

    public async Task<bool> SetPowerAsync(bool powerOn)
    {
        return await ExecuteWithRetryAsync(() => _innerRadio.SetPowerAsync(powerOn), $"SetPower({powerOn})");
    }

    public async Task<bool> GetPowerAsync()
    {
        return await ExecuteWithRetryAsync(() => _innerRadio.GetPowerAsync(), "GetPower");
    }

    // Additional Hamlib-compatible methods with retry logic
    public async Task<bool> SetMonitorLevelAsync(int level)
    {
        return await ExecuteWithRetryAsync(() => _innerRadio.SetMonitorLevelAsync(level), $"SetMonitorLevel({level})");
    }

    public async Task<int> GetMonitorLevelAsync()
    {
        return await ExecuteWithRetryAsync(() => _innerRadio.GetMonitorLevelAsync(), "GetMonitorLevel");
    }

    public async Task<bool> SetMicGainAsync(int gain)
    {
        return await ExecuteWithRetryAsync(() => _innerRadio.SetMicGainAsync(gain), $"SetMicGain({gain})");
    }

    public async Task<int> GetMicGainAsync()
    {
        return await ExecuteWithRetryAsync(() => _innerRadio.GetMicGainAsync(), "GetMicGain");
    }

    public async Task<bool> SetCompLevelAsync(int level)
    {
        return await ExecuteWithRetryAsync(() => _innerRadio.SetCompLevelAsync(level), $"SetCompLevel({level})");
    }

    public async Task<int> GetCompLevelAsync()
    {
        return await ExecuteWithRetryAsync(() => _innerRadio.GetCompLevelAsync(), "GetCompLevel");
    }

    public async Task<bool> SetVoxLevelAsync(int level)
    {
        return await ExecuteWithRetryAsync(() => _innerRadio.SetVoxLevelAsync(level), $"SetVoxLevel({level})");
    }

    public async Task<int> GetVoxLevelAsync()
    {
        return await ExecuteWithRetryAsync(() => _innerRadio.GetVoxLevelAsync(), "GetVoxLevel");
    }

    public async Task<bool> SetVoxDelayAsync(int delayMs)
    {
        return await ExecuteWithRetryAsync(() => _innerRadio.SetVoxDelayAsync(delayMs), $"SetVoxDelay({delayMs})");
    }

    public async Task<int> GetVoxDelayAsync()
    {
        return await ExecuteWithRetryAsync(() => _innerRadio.GetVoxDelayAsync(), "GetVoxDelay");
    }

    public async Task<bool> SetBreakInAsync(bool enabled)
    {
        return await ExecuteWithRetryAsync(() => _innerRadio.SetBreakInAsync(enabled), $"SetBreakIn({enabled})");
    }

    public async Task<bool> GetBreakInAsync()
    {
        return await ExecuteWithRetryAsync(() => _innerRadio.GetBreakInAsync(), "GetBreakIn");
    }

    public async Task<bool> SetNotchAsync(int frequency)
    {
        return await ExecuteWithRetryAsync(() => _innerRadio.SetNotchAsync(frequency), $"SetNotch({frequency})");
    }

    public async Task<int> GetNotchAsync()
    {
        return await ExecuteWithRetryAsync(() => _innerRadio.GetNotchAsync(), "GetNotch");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _healthCheckTimer?.Dispose();
            _innerRadio?.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    #endregion

    #region Resilience Methods

    /// <summary>
    /// Executes an operation with retry logic and connection recovery
    /// </summary>
    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, string operationName)
    {
        return await RetryHelper.ExecuteWithRetryAsync(
            operation,
            RetryPolicy,
            _logger,
            $"Radio {operationName}",
            ShouldRetryRadioOperation
        );
    }

    /// <summary>
    /// Determines if a radio operation should be retried based on the exception
    /// </summary>
    private bool ShouldRetryRadioOperation(Exception ex)
    {
        var shouldRetry = ex switch
        {
            InvalidOperationException => true,
            System.IO.IOException => true,
            TimeoutException => true,
            ArgumentException => false, // Don't retry argument errors
            _ => false
        };

        if (shouldRetry && !IsConnected && AutoRecoveryEnabled)
        {
            // Attempt to recover the radio connection
            _ = Task.Run(TryRecoverConnection);
        }

        return shouldRetry;
    }

    /// <summary>
    /// Attempts to recover the radio connection
    /// </summary>
    private async Task<bool> TryRecoverConnection()
    {
        if (_currentSerialPort == null || _disposed)
            return false;

        try
        {
            _logger?.LogInformation("Attempting to recover radio connection for {Manufacturer} {ModelName}", 
                Manufacturer, ModelName);
            
            _healthMonitor.UpdateState(ConnectionState.Reconnecting);

            // First, disconnect the radio
            try
            {
                _innerRadio.Disconnect();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error disconnecting radio during recovery");
            }

            // Wait a moment before attempting to reconnect
            await Task.Delay(2000);

            // Attempt to reconnect
            var success = await _innerRadio.ConnectAsync(_currentSerialPort);
            
            if (success)
            {
                _healthMonitor.UpdateState(ConnectionState.Connected);
                _healthMonitor.RecordSuccess();
                
                _logger?.LogInformation("Successfully recovered radio connection for {Manufacturer} {ModelName}", 
                    Manufacturer, ModelName);
                
                ConnectionRestored?.Invoke(this, new RadioConnectionRestoredEventArgs(Manufacturer, ModelName));
                return true;
            }
            else
            {
                _healthMonitor.UpdateState(ConnectionState.Failed);
                _healthMonitor.RecordFailure();
                
                _logger?.LogWarning("Failed to recover radio connection for {Manufacturer} {ModelName}", 
                    Manufacturer, ModelName);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during radio connection recovery for {Manufacturer} {ModelName}", 
                Manufacturer, ModelName);
            
            _healthMonitor.UpdateState(ConnectionState.Failed);
            _healthMonitor.RecordFailure();
            return false;
        }
    }

    /// <summary>
    /// Performs a health check on the radio connection
    /// </summary>
    private async void PerformRadioHealthCheck(object? state)
    {
        if (_disposed || _healthMonitor.CurrentState != ConnectionState.Connected)
            return;

        try
        {
            var result = await _healthMonitor.PerformHealthCheckAsync(async (cancellationToken) =>
            {
                // Perform a simple command to test radio responsiveness
                var testCommand = new RadioCommand("ID;", "Health check", true, 2000);
                
                _logger?.LogDebug("Performing health check on radio {Manufacturer} {ModelName} with command: {Command}", 
                    Manufacturer, ModelName, testCommand.Command);
                
                var response = await _innerRadio.SendCommandAsync(testCommand);
                
                _logger?.LogDebug("Health check response from radio {Manufacturer} {ModelName}: '{Response}' (Length: {Length})", 
                    Manufacturer, ModelName, response ?? "null", response?.Length ?? 0);
                
                bool isHealthy = !string.IsNullOrEmpty(response);
                
                if (!isHealthy)
                {
                    _logger?.LogWarning("Health check failed for radio {Manufacturer} {ModelName}: Response was null or empty", 
                        Manufacturer, ModelName);
                }
                
                return isHealthy;
            });

            if (result.IsHealthy)
            {
                _healthMonitor.RecordSuccess();
                _logger?.LogDebug("Health check succeeded for radio {Manufacturer} {ModelName}", 
                    Manufacturer, ModelName);
            }
            else
            {
                _healthMonitor.RecordFailure();
                _logger?.LogWarning("Health check failed for radio {Manufacturer} {ModelName}", 
                    Manufacturer, ModelName);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error during radio health check for {Manufacturer} {ModelName}", 
                Manufacturer, ModelName);
            _healthMonitor.RecordFailure();
        }
    }

    /// <summary>
    /// Handles connection state changes from the health monitor
    /// </summary>
    private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        if (e.NewState == ConnectionState.Failed && e.OldState == ConnectionState.Connected)
        {
            ConnectionLost?.Invoke(this, new RadioConnectionLostEventArgs(Manufacturer, ModelName, "Health check failed"));
            
            if (AutoRecoveryEnabled)
            {
                _ = Task.Run(TryRecoverConnection);
            }
        }
    }

    #endregion
}

/// <summary>
/// Event arguments for radio connection lost events
/// </summary>
public class RadioConnectionLostEventArgs : EventArgs
{
    public string Manufacturer { get; }
    public string ModelName { get; }
    public string Reason { get; }
    public DateTime Timestamp { get; }

    public RadioConnectionLostEventArgs(string manufacturer, string modelName, string reason)
    {
        Manufacturer = manufacturer;
        ModelName = modelName;
        Reason = reason;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Event arguments for radio connection restored events
/// </summary>
public class RadioConnectionRestoredEventArgs : EventArgs
{
    public string Manufacturer { get; }
    public string ModelName { get; }
    public DateTime Timestamp { get; }

    public RadioConnectionRestoredEventArgs(string manufacturer, string modelName)
    {
        Manufacturer = manufacturer;
        ModelName = modelName;
        Timestamp = DateTime.UtcNow;
    }
}