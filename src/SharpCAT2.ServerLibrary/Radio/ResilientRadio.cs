using Microsoft.Extensions.Logging;
using SharpCAT2.ServerLibrary.Serial;
using SharpCAT2.Core.Utils;
using SharpCAT2.Core.Radio;
using SharpCAT2.Core.Serial;

namespace SharpCAT2.ServerLibrary.Radio;

/// <summary>
/// Passive compatibility wrapper. The underlying radio session owns recovery and never replays operations.
/// </summary>
public class ResilientRadio : IRadio
{
    private readonly IRadio _innerRadio;
    public IRadio InnerRadio => _innerRadio;
    private bool _disposed;

    /// <summary>
    /// Legacy retry policy setting; retained but inert
    /// </summary>
    public RetryPolicy RetryPolicy { get; set; } = RetryPolicy.Radio;

    /// <summary>
    /// Legacy recovery setting; retained but inert
    /// </summary>
    public bool AutoRecoveryEnabled { get; set; } = true;

    /// <summary>
    /// Legacy health-check interval; no timer uses this value
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
        _ = logger; // Constructor retained for source compatibility.

    }

    #region IRadio Implementation

    public string ModelName => _innerRadio.ModelName;
    public string Manufacturer => _innerRadio.Manufacturer;
    public SupportedFeatures SupportedFeatures => _innerRadio.SupportedFeatures;

    public bool IsConnected => _innerRadio.IsConnected;

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
        var connected = await _innerRadio.ConnectAsync(port);
        if (connected) ConnectionRestored?.Invoke(this, new(Manufacturer, ModelName));
        return connected;
    }

    public void Disconnect()
    {
        _innerRadio.Disconnect();
        ConnectionLost?.Invoke(this, new(Manufacturer, ModelName, "Explicit disconnect"));
    }

    public async Task<string?> SendCommandAsync(RadioCommand command)
    {
        return await ExecuteOnceAsync(
            () => _innerRadio.SendCommandAsync(command),
            $"SendCommand({command.Command})"
        );
    }

    public async Task<RadioStatus> GetStatusAsync()
    {
        return await ExecuteOnceAsync(
            () => _innerRadio.GetStatusAsync(),
            "GetStatus"
        );
    }

    public async Task<bool> SetFrequencyAsync(long frequency)
    {
        return await ExecuteOnceAsync(
            () => _innerRadio.SetFrequencyAsync(frequency),
            $"SetFrequency({frequency})"
        );
    }

    public async Task<bool> SetModeAsync(string mode)
    {
        return await ExecuteOnceAsync(
            () => _innerRadio.SetModeAsync(mode),
            $"SetMode({mode})"
        );
    }

    // Extended radio control methods forwarded once
    public async Task<string> GetVfoAsync()
    {
        return await ExecuteOnceAsync(() => _innerRadio.GetVfoAsync(), "GetVfo");
    }

    public async Task<bool> SetVfoAsync(string vfo)
    {
        return await ExecuteOnceAsync(() => _innerRadio.SetVfoAsync(vfo), $"SetVfo({vfo})");
    }

    public async Task<bool> SwapVfoAsync()
    {
        return await ExecuteOnceAsync(() => _innerRadio.SwapVfoAsync(), "SwapVfo");
    }

    public async Task<bool> SetSplitAsync(bool enabled)
    {
        return await ExecuteOnceAsync(() => _innerRadio.SetSplitAsync(enabled), $"SetSplit({enabled})");
    }

    public async Task<bool> GetSplitAsync()
    {
        return await ExecuteOnceAsync(() => _innerRadio.GetSplitAsync(), "GetSplit");
    }

    public async Task<bool> SetRitAsync(int offsetHz)
    {
        return await ExecuteOnceAsync(() => _innerRadio.SetRitAsync(offsetHz), $"SetRit({offsetHz})");
    }

    public async Task<int> GetRitAsync()
    {
        return await ExecuteOnceAsync(() => _innerRadio.GetRitAsync(), "GetRit");
    }

    public async Task<bool> SetXitAsync(int offsetHz)
    {
        return await ExecuteOnceAsync(() => _innerRadio.SetXitAsync(offsetHz), $"SetXit({offsetHz})");
    }

    public async Task<int> GetXitAsync()
    {
        return await ExecuteOnceAsync(() => _innerRadio.GetXitAsync(), "GetXit");
    }

    public async Task<bool> SetIfBandwidthAsync(int bandwidth)
    {
        return await ExecuteOnceAsync(() => _innerRadio.SetIfBandwidthAsync(bandwidth), $"SetIfBandwidth({bandwidth})");
    }

    public async Task<int> GetIfBandwidthAsync()
    {
        return await ExecuteOnceAsync(() => _innerRadio.GetIfBandwidthAsync(), "GetIfBandwidth");
    }

    public async Task<bool> SetPowerOutputAsync(int powerPercent)
    {
        return await ExecuteOnceAsync(() => _innerRadio.SetPowerOutputAsync(powerPercent), $"SetPowerOutput({powerPercent})");
    }

    public async Task<int> GetPowerOutputAsync()
    {
        return await ExecuteOnceAsync(() => _innerRadio.GetPowerOutputAsync(), "GetPowerOutput");
    }

    public async Task<int> GetSMeterAsync()
    {
        return await ExecuteOnceAsync(() => _innerRadio.GetSMeterAsync(), "GetSMeter");
    }

    public async Task<double> GetSWRAsync()
    {
        return await ExecuteOnceAsync(() => _innerRadio.GetSWRAsync(), "GetSWR");
    }

    public async Task<bool> SetAntennaAsync(int antenna)
    {
        return await ExecuteOnceAsync(() => _innerRadio.SetAntennaAsync(antenna), $"SetAntenna({antenna})");
    }

    public async Task<int> GetAntennaAsync()
    {
        return await ExecuteOnceAsync(() => _innerRadio.GetAntennaAsync(), "GetAntenna");
    }

    public async Task<bool> SetMemoryChannelAsync(int channel, long frequency, string mode)
    {
        return await ExecuteOnceAsync(() => _innerRadio.SetMemoryChannelAsync(channel, frequency, mode),
            $"SetMemoryChannel({channel}, {frequency}, {mode})");
    }

    public async Task<bool> RecallMemoryChannelAsync(int channel)
    {
        return await ExecuteOnceAsync(() => _innerRadio.RecallMemoryChannelAsync(channel), $"RecallMemoryChannel({channel})");
    }

    public async Task<bool> SetCwSpeedAsync(int wpm)
    {
        return await ExecuteOnceAsync(() => _innerRadio.SetCwSpeedAsync(wpm), $"SetCwSpeed({wpm})");
    }

    public async Task<int> GetCwSpeedAsync()
    {
        return await ExecuteOnceAsync(() => _innerRadio.GetCwSpeedAsync(), "GetCwSpeed");
    }

    public async Task<bool> SendCwMessageAsync(string message)
    {
        return await ExecuteOnceAsync(() => _innerRadio.SendCwMessageAsync(message), $"SendCwMessage({message})");
    }

    public async Task<bool> SetNoiseReductionAsync(int level)
    {
        return await ExecuteOnceAsync(() => _innerRadio.SetNoiseReductionAsync(level), $"SetNoiseReduction({level})");
    }

    public async Task<int> GetNoiseReductionAsync()
    {
        return await ExecuteOnceAsync(() => _innerRadio.GetNoiseReductionAsync(), "GetNoiseReduction");
    }

    public async Task<bool> SetPowerAsync(bool powerOn)
    {
        return await ExecuteOnceAsync(() => _innerRadio.SetPowerAsync(powerOn), $"SetPower({powerOn})");
    }

    public async Task<bool> GetPowerAsync()
    {
        return await ExecuteOnceAsync(() => _innerRadio.GetPowerAsync(), "GetPower");
    }

    // Additional Hamlib-compatible methods with retry logic
    public async Task<bool> SetMonitorLevelAsync(int level)
    {
        return await ExecuteOnceAsync(() => _innerRadio.SetMonitorLevelAsync(level), $"SetMonitorLevel({level})");
    }

    public async Task<int> GetMonitorLevelAsync()
    {
        return await ExecuteOnceAsync(() => _innerRadio.GetMonitorLevelAsync(), "GetMonitorLevel");
    }

    public async Task<bool> SetMicGainAsync(int gain)
    {
        return await ExecuteOnceAsync(() => _innerRadio.SetMicGainAsync(gain), $"SetMicGain({gain})");
    }

    public async Task<int> GetMicGainAsync()
    {
        return await ExecuteOnceAsync(() => _innerRadio.GetMicGainAsync(), "GetMicGain");
    }

    public async Task<bool> SetCompLevelAsync(int level)
    {
        return await ExecuteOnceAsync(() => _innerRadio.SetCompLevelAsync(level), $"SetCompLevel({level})");
    }

    public async Task<int> GetCompLevelAsync()
    {
        return await ExecuteOnceAsync(() => _innerRadio.GetCompLevelAsync(), "GetCompLevel");
    }

    public async Task<bool> SetVoxLevelAsync(int level)
    {
        return await ExecuteOnceAsync(() => _innerRadio.SetVoxLevelAsync(level), $"SetVoxLevel({level})");
    }

    public async Task<int> GetVoxLevelAsync()
    {
        return await ExecuteOnceAsync(() => _innerRadio.GetVoxLevelAsync(), "GetVoxLevel");
    }

    public async Task<bool> SetVoxDelayAsync(int delayMs)
    {
        return await ExecuteOnceAsync(() => _innerRadio.SetVoxDelayAsync(delayMs), $"SetVoxDelay({delayMs})");
    }

    public async Task<int> GetVoxDelayAsync()
    {
        return await ExecuteOnceAsync(() => _innerRadio.GetVoxDelayAsync(), "GetVoxDelay");
    }

    public async Task<bool> SetBreakInAsync(bool enabled)
    {
        return await ExecuteOnceAsync(() => _innerRadio.SetBreakInAsync(enabled), $"SetBreakIn({enabled})");
    }

    public async Task<bool> GetBreakInAsync()
    {
        return await ExecuteOnceAsync(() => _innerRadio.GetBreakInAsync(), "GetBreakIn");
    }

    public async Task<bool> SetNotchAsync(int frequency)
    {
        return await ExecuteOnceAsync(() => _innerRadio.SetNotchAsync(frequency), $"SetNotch({frequency})");
    }

    public async Task<int> GetNotchAsync()
    {
        return await ExecuteOnceAsync(() => _innerRadio.GetNotchAsync(), "GetNotch");
    }

    public async Task<string> GetUniversalStatusStringAsync()
    {
        return await ExecuteOnceAsync(() => _innerRadio.GetUniversalStatusStringAsync(), "GetUniversalStatusString");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _innerRadio?.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    #endregion

    // Compatibility knobs are retained but inert. The inner session owns recovery;
    // failed mutations are never retried by this wrapper.
    private Task<T> ExecuteOnceAsync<T>(Func<Task<T>> operation, string operationName) => operation();
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