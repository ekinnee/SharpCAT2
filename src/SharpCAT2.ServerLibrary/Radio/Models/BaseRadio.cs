using SharpCAT2.Core.Radio;
using SharpCAT2.Core.Serial;
using System.Text;
using System.Text.RegularExpressions;
using SharpCAT2.Core.Radio.Contracts;
using SharpCAT2.ServerLibrary.Radio.Protocols;
using SharpCAT2.ServerLibrary.Radio.Session;
using SharpCAT2.ServerLibrary.Serial;

namespace SharpCAT2.ServerLibrary.Radio.Models;

/// <summary>
/// Base implementation of the IRadio interface providing common functionality.
/// 
/// LOGGING ARCHITECTURE:
/// Radio classes are designed to be logging-free and platform-agnostic.
/// All logging is handled at the server/service layer (RadioService, etc.) using dependency-injected ILogger.
/// Radio classes communicate errors and status through return values, events, or exceptions only.
/// </summary>
public abstract class BaseRadio : IRadio
{
    private RadioSession? _session;
    private readonly SemaphoreSlim _lifetime = new(1, 1);
    private string? _portName;
    private ISerialPort? _transferredPort;
    public bool HasAcceptedTransport { get; private set; }
    public RadioSession? Session => _session;
    public event Action<ReadOnlyMemory<byte>>? UnsolicitedFrame;
    public RadioOperationResult<object>? LastOperationResult { get; protected set; }
    protected bool _disposed = false;

    /// <summary>
    /// Gets the radio model name
    /// </summary>
    public abstract string ModelName { get; }

    /// <summary>
    /// Gets the radio manufacturer
    /// </summary>
    public abstract string Manufacturer { get; }

    /// <summary>
    /// Gets the features supported by this radio (default implementation)
    /// </summary>
    public virtual SupportedFeatures SupportedFeatures => SupportedFeatures.BasicOperation;

    /// <summary>
    /// Gets whether the radio is currently connected
    /// </summary>
    public bool IsConnected => _session?.State == SessionState.Ready;

    /// <summary>
    /// Gets or sets the frequency in Hz
    /// </summary>
    public virtual long Frequency { get; set; }

    /// <summary>
    /// Gets or sets the operating mode
    /// </summary>
    public virtual string Mode { get; set; } = "USB";

    /// <summary>
    /// Connects to the radio using the specified serial port
    /// </summary>
    /// <param name="port">Serial port to use for communication</param>
    /// <returns>True if connection successful, false otherwise</returns>
    public virtual async Task<bool> ConnectAsync(ISerialPort port)
    {
        ArgumentNullException.ThrowIfNull(port);
        await _lifetime.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_session is not null)
            {
                // Existing session retains the transport for explicit reconnect; no second owner.
                if (!ReferenceEquals(_transferredPort, port)) throw new InvalidOperationException("Disconnect and dispose before transferring a different port.");
                if (IsConnected) return true;
                return await _session.ConnectAsync(SynchronizationCommand(), CreateReplyMatcher("ID"));
            }
            if (Manufacturer is not ("Yaesu" or "Kenwood" or "Elecraft" or "SharpCAT2"))
                throw new NotSupportedException("This model has no supported ASCII transport mapping; select an implemented profile.");
            _portName = port.PortName;
            _session = new RadioSession(CreateTransport(port), ParseAsciiFrame);
            _transferredPort = port;
            HasAcceptedTransport = true;
            _session.UnsolicitedFrame += frame => UnsolicitedFrame?.Invoke(frame);
            var connected = await _session.ConnectAsync(SynchronizationCommand(), CreateReplyMatcher("ID"));
            if (!connected)
            {
                await _session.DisposeAsync();
                _session = null;
            }
            return connected;
        }
        finally { _lifetime.Release(); }
    }

    protected virtual IByteTransport CreateTransport(ISerialPort port) => new LegacySerialByteTransport(port);

    private static CommandSpecification SynchronizationCommand() => new(
        Encoding.ASCII.GetBytes("ID;"), ResponsePolicy.ReplyRequired, TimeSpan.FromSeconds(2), "ID");

    /// <summary>Experimental ASCII compatibility framing. Model-specific validation arrives with profiles.</summary>
    public static FrameParseResult ParseAsciiFrame(ReadOnlyMemory<byte> buffer)
    {
        var end = buffer.Span.IndexOf((byte)';');
        if (end < 0) return new(FrameParseStatus.Incomplete, 0);
        var frame = buffer[..(end + 1)];
        foreach (var value in frame.Span)
            if (value > 127 || value < 32) return new(FrameParseStatus.Invalid, end + 1);
        return new(FrameParseStatus.Complete, end + 1, frame);
    }

    protected virtual Func<ReadOnlyMemory<byte>, ReplyParseResult> CreateReplyMatcher(string key) => frame =>
    {
        var text = Encoding.ASCII.GetString(frame.Span);
        if (!text.StartsWith(key, StringComparison.OrdinalIgnoreCase)) return new(ReplyParseStatus.Unrelated);
        return new(ReplyParseStatus.Valid, text);
    };

    public virtual void Disconnect()
    {
        _lifetime.Wait();
        try { _session?.StopAsync().GetAwaiter().GetResult(); }
        finally { _lifetime.Release(); }
    }

    /// <summary>Automatically adapts legacy ASCII calls into the sole session queue.
    /// Null denotes failure; empty string denotes a completed write-only command.</summary>
    public virtual async Task<string?> SendCommandAsync(RadioCommand command)
    {
        var result = await ExecuteCommandAsync(command);
        return result.Outcome == RadioOutcome.Succeeded
            ? result.Observation?.Value as string ?? string.Empty
            : null;
    }

    public virtual async Task<RadioOperationResult<object>> ExecuteCommandAsync(RadioCommand command, CancellationToken cancellationToken = default)
    {
        var result = await ExecuteCommandCoreAsync(command, cancellationToken);
        LastOperationResult = result;
        return result;
    }

    private async Task<RadioOperationResult<object>> ExecuteCommandCoreAsync(RadioCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var text = command.Command;
        if (string.IsNullOrEmpty(text) || text.Length < 3 || !text.EndsWith(';') ||
            text[..^1].Contains(';') || text.Any(c => c < 32 || c > 127) ||
            !char.IsAsciiLetter(text[0]) || !char.IsAsciiLetter(text[1]) || command.TimeoutMs <= 0)
            return new(RadioOutcome.InvalidArgument, CompletionEvidence.NotSent, diagnostic: "One terminated ASCII CAT command is required.");
        var session = _session;
        if (session is null) return new(RadioOutcome.NotConnected, CompletionEvidence.NotSent);
        var key = text[..2].ToUpperInvariant();
        var specification = new CommandSpecification(Encoding.ASCII.GetBytes(text),
            command.ExpectsResponse ? ResponsePolicy.ReplyRequired : ResponsePolicy.WriteOnly,
            TimeSpan.FromMilliseconds(Math.Min(command.TimeoutMs, 5000)), command.ExpectsResponse ? key : null);
        // Legacy caller declares only response policy; conservatively treat non-query commands as mutations.
        var mutation = text.ToUpperInvariant() is not ("ID;" or "FA;" or "FB;" or "MD;" or "MD0;" or "IF;");
        var result = await session.ExecuteAsync(specification, CreateReplyMatcher(key), isMutation: mutation,
            cancellationToken: cancellationToken);
        return result;
    }

    // Kept as a source-compatible pure hook; the session now owns all reads.
    protected virtual bool IsCompleteResponse(string response) => response.EndsWith(';');

    /// <summary>
    /// Gets the current radio status/information
    /// </summary>
    /// <returns>Radio status information</returns>
    public virtual async Task<RadioStatus> GetStatusAsync()
    {
        var frequency = await SendCommandAsync(RadioCommand.Common.GetFrequency);
        if (string.IsNullOrEmpty(frequency)) throw new IOException("No observed frequency is available.");
        var parsedFrequency = ParseFrequency(frequency);
        var fullFrequency = Regex.Match(frequency, @"^FA(\d+);$");
        if (parsedFrequency <= 0 || !fullFrequency.Success ||
            !long.TryParse(fullFrequency.Groups[1].Value, out var observedFrequency) || observedFrequency != parsedFrequency)
            throw new FormatException("The legacy parser did not establish a complete observed frequency.");
        var mode = await SendCommandAsync(RadioCommand.Common.GetMode);
        if (string.IsNullOrEmpty(mode)) throw new IOException("No observed mode is available.");
        var parsedMode = ParseMode(mode);
        Frequency = parsedFrequency;
        Mode = parsedMode;
        var status = new RadioStatus { Frequency = parsedFrequency, Mode = parsedMode };
        status.AdditionalInfo["FrequencyObserved"] = true;
        status.AdditionalInfo["ModeObserved"] = true;
        return status;
    }

    /// <summary>
    /// Sets the radio frequency
    /// </summary>
    /// <param name="frequency">Frequency in Hz</param>
    /// <returns>True if successful</returns>
    public virtual async Task<bool> SetFrequencyAsync(long frequency)
    {
        try
        {
            var command = RadioCommand.Common.SetFrequency(frequency);
            var response = await SendCommandAsync(command);
            
            if (!string.IsNullOrEmpty(response))
            {
                Frequency = frequency;
                return true;
            }
        }
        catch (Exception)
        {
            // Frequency setting errors are communicated via return value
            // Service layer will log these errors based on the false return
        }

        return false;
    }

    /// <summary>
    /// Sets the radio operating mode
    /// </summary>
    /// <param name="mode">Operating mode</param>
    /// <returns>True if successful</returns>
    public virtual async Task<bool> SetModeAsync(string mode)
    {
        try
        {
            var command = RadioCommand.Common.SetMode(mode);
            var response = await SendCommandAsync(command);
            
            if (!string.IsNullOrEmpty(response))
            {
                Mode = mode;
                return true;
            }
        }
        catch (Exception)
        {
            // Mode setting errors are communicated via return value
            // Service layer will log these errors based on the false return
        }

        return false;
    }

    /// <summary>
    /// Parses frequency from response (can be overridden by derived classes)
    /// </summary>
    /// <param name="response">Response string</param>
    /// <returns>Frequency in Hz</returns>
    protected virtual long ParseFrequency(string response)
    {
        // Default implementation for Kenwood/Elecraft style: FA00014074000;
        var match = Regex.Match(response, @"FA(\d{11})");
        if (match.Success && long.TryParse(match.Groups[1].Value, out long freq))
        {
            return freq;
        }
        throw new FormatException("Malformed frequency response.");
    }

    /// <summary>
    /// Parses mode from response (can be overridden by derived classes)
    /// </summary>
    /// <param name="response">Response string</param>
    /// <returns>Mode string</returns>
    protected virtual string ParseMode(string response)
    {
        // Default implementation for Kenwood style: MD2; (2=USB)
        var match = Regex.Match(response, @"^MD(\d+);$");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int modeNum))
        {
            return MapModeNumber(modeNum);
        }
        throw new FormatException("Malformed mode response.");
    }

    /// <summary>
    /// Maps mode number to mode string (can be overridden by derived classes)
    /// </summary>
    /// <param name="modeNumber">Mode number</param>
    /// <returns>Mode string</returns>
    protected virtual string MapModeNumber(int modeNumber)
    {
        return modeNumber switch
        {
            1 => "LSB",
            2 => "USB",
            3 => "CW",
            4 => "FM",
            5 => "AM",
            6 => "FSK",
            7 => "CW-R",
            8 => "FSK-R",
            _ => throw new FormatException("Unknown device mode.")
        };
    }

    /// <summary>
    /// Parses transceiver information (can be overridden by derived classes)
    /// </summary>
    /// <param name="response">Response string</param>
    /// <param name="status">Status object to update</param>
    protected virtual void ParseTransceiverInfo(string response, RadioStatus status)
    {
        // Default implementation for IF command response parsing
        // This is a simplified parser - actual implementation depends on radio protocol
    }

    // Default implementations for extended radio features
    // These should be overridden by derived classes that support these features

    /// <summary>
    /// Gets the current VFO (A or B)
    /// </summary>
    public virtual Task<string> GetVfoAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.DualVFO))
            throw new NotSupportedException("Dual VFO not supported by this radio");
        
        // Default implementation - should be overridden
        return Task.FromResult("A");
    }

    /// <summary>
    /// Sets the active VFO
    /// </summary>
    public virtual Task<bool> SetVfoAsync(string vfo)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.DualVFO))
            return Task.FromResult(false);
        
        // Default implementation - should be overridden
        return Task.FromResult(false);
    }

    /// <summary>
    /// Swaps VFO A and VFO B frequencies
    /// </summary>
    public virtual Task<bool> SwapVfoAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.VFOSwap))
            return Task.FromResult(false);
        
        // Default implementation - should be overridden
        return Task.FromResult(false);
    }

    /// <summary>
    /// Enables or disables split operation
    /// </summary>
    public virtual Task<bool> SetSplitAsync(bool enabled)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.SplitOperation))
            return Task.FromResult(false);
        
        // Default implementation - should be overridden
        return Task.FromResult(false);
    }

    /// <summary>
    /// Gets the current split operation status
    /// </summary>
    public virtual Task<bool> GetSplitAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.SplitOperation))
            return Task.FromResult(false);
        
        // Default implementation - should be overridden
        return Task.FromResult(false);
    }

    /// <summary>
    /// Sets the RIT (Receiver Incremental Tuning) offset
    /// </summary>
    public virtual Task<bool> SetRitAsync(int offsetHz)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.RIT))
            return Task.FromResult(false);
        
        // Default implementation - should be overridden
        return Task.FromResult(false);
    }

    /// <summary>
    /// Gets the current RIT offset
    /// </summary>
    public virtual Task<int> GetRitAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.RIT))
            return Task.FromResult(0);
        
        // Default implementation - should be overridden
        return Task.FromResult(0);
    }

    /// <summary>
    /// Sets the XIT (Transmitter Incremental Tuning) offset
    /// </summary>
    public virtual Task<bool> SetXitAsync(int offsetHz)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.XIT))
            return Task.FromResult(false);
        
        // Default implementation - should be overridden
        return Task.FromResult(false);
    }

    /// <summary>
    /// Gets the current XIT offset
    /// </summary>
    public virtual Task<int> GetXitAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.XIT))
            return Task.FromResult(0);
        
        // Default implementation - should be overridden
        return Task.FromResult(0);
    }

    /// <summary>
    /// Sets the IF bandwidth
    /// </summary>
    public virtual Task<bool> SetIfBandwidthAsync(int bandwidth)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.IFBandwidth))
            return Task.FromResult(false);
        
        // Default implementation - should be overridden
        return Task.FromResult(false);
    }

    /// <summary>
    /// Gets the current IF bandwidth
    /// </summary>
    public virtual Task<int> GetIfBandwidthAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.IFBandwidth))
            return Task.FromResult(0);
        
        // Default implementation - should be overridden
        return Task.FromResult(0);
    }

    /// <summary>
    /// Sets the power output level
    /// </summary>
    public virtual Task<bool> SetPowerOutputAsync(int powerPercent)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.PowerOutput))
            return Task.FromResult(false);
        
        // Default implementation - should be overridden
        return Task.FromResult(false);
    }

    /// <summary>
    /// Gets the current power output level
    /// </summary>
    public virtual Task<int> GetPowerOutputAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.PowerOutput))
            return Task.FromResult(0);
        
        // Default implementation - should be overridden
        return Task.FromResult(0);
    }

    /// <summary>
    /// Gets the S-meter reading
    /// </summary>
    public virtual Task<int> GetSMeterAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.SMeter))
            return Task.FromResult(0);
        
        // Default implementation - should be overridden
        return Task.FromResult(0);
    }

    /// <summary>
    /// Gets the SWR reading
    /// </summary>
    public virtual Task<double> GetSWRAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.SWRMeter))
            return Task.FromResult(1.0);
        
        // Default implementation - should be overridden
        return Task.FromResult(1.0);
    }

    /// <summary>
    /// Sets the active antenna
    /// </summary>
    public virtual Task<bool> SetAntennaAsync(int antenna)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.AntennaSelection))
            return Task.FromResult(false);
        
        // Default implementation - should be overridden
        return Task.FromResult(false);
    }

    /// <summary>
    /// Gets the current active antenna
    /// </summary>
    public virtual Task<int> GetAntennaAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.AntennaSelection))
            return Task.FromResult(1);
        
        // Default implementation - should be overridden
        return Task.FromResult(1);
    }

    /// <summary>
    /// Sets a memory channel
    /// </summary>
    public virtual Task<bool> SetMemoryChannelAsync(int channel, long frequency, string mode)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.MemoryChannels))
            return Task.FromResult(false);
        
        // Default implementation - should be overridden
        return Task.FromResult(false);
    }

    /// <summary>
    /// Recalls a memory channel
    /// </summary>
    public virtual Task<bool> RecallMemoryChannelAsync(int channel)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.MemoryChannels))
            return Task.FromResult(false);
        
        // Default implementation - should be overridden
        return Task.FromResult(false);
    }

    /// <summary>
    /// Sets the CW keyer speed
    /// </summary>
    public virtual Task<bool> SetCwSpeedAsync(int wpm)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.CWKeyer))
            return Task.FromResult(false);
        
        // Default implementation - should be overridden
        return Task.FromResult(false);
    }

    /// <summary>
    /// Gets the current CW keyer speed
    /// </summary>
    public virtual Task<int> GetCwSpeedAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.CWKeyer))
            return Task.FromResult(0);
        
        // Default implementation - should be overridden
        return Task.FromResult(0);
    }

    /// <summary>
    /// Sends a CW message
    /// </summary>
    public virtual Task<bool> SendCwMessageAsync(string message)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.CWMessage))
            return Task.FromResult(false);
        
        // Default implementation - should be overridden
        return Task.FromResult(false);
    }

    /// <summary>
    /// Sets the noise reduction level
    /// </summary>
    public virtual Task<bool> SetNoiseReductionAsync(int level)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.NoiseReduction))
            return Task.FromResult(false);
        
        // Default implementation - should be overridden
        return Task.FromResult(false);
    }

    /// <summary>
    /// Gets the current noise reduction level
    /// </summary>
    public virtual Task<int> GetNoiseReductionAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.NoiseReduction))
            return Task.FromResult(0);
        
        // Default implementation - should be overridden
        return Task.FromResult(0);
    }

    /// <summary>
    /// Powers the radio on or off
    /// </summary>
    public virtual Task<bool> SetPowerAsync(bool powerOn)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.PowerOnOff))
            return Task.FromResult(false);
        
        // Default implementation - should be overridden
        return Task.FromResult(false);
    }

    /// <summary>
    /// Gets whether the radio is powered on
    /// </summary>
    public virtual Task<bool> GetPowerAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.PowerOnOff))
            return Task.FromResult(true); // Assume powered on if we can't check
        
        // Default implementation - should be overridden
        return Task.FromResult(true);
    }

    // Default implementations for additional Hamlib-compatible features

    /// <summary>
    /// Sets the monitor/sidetone level
    /// </summary>
    public virtual Task<bool> SetMonitorLevelAsync(int level)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.MonitorLevel))
            return Task.FromResult(false);
        
        // Default implementation - should be overridden
        return Task.FromResult(false);
    }

    /// <summary>
    /// Gets the current monitor/sidetone level
    /// </summary>
    public virtual Task<int> GetMonitorLevelAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.MonitorLevel))
            return Task.FromResult(0);
        
        // Default implementation - should be overridden
        return Task.FromResult(0);
    }

    /// <summary>
    /// Sets the microphone gain
    /// </summary>
    public virtual Task<bool> SetMicGainAsync(int gain)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.MicGain))
            return Task.FromResult(false);
        
        // Default implementation - should be overridden
        return Task.FromResult(false);
    }

    /// <summary>
    /// Gets the current microphone gain
    /// </summary>
    public virtual Task<int> GetMicGainAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.MicGain))
            return Task.FromResult(0);
        
        // Default implementation - should be overridden
        return Task.FromResult(0);
    }

    /// <summary>
    /// Sets the speech compression level
    /// </summary>
    public virtual Task<bool> SetCompLevelAsync(int level)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.CompLevel))
            return Task.FromResult(false);
        
        // Default implementation - should be overridden
        return Task.FromResult(false);
    }

    /// <summary>
    /// Gets the current speech compression level
    /// </summary>
    public virtual Task<int> GetCompLevelAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.CompLevel))
            return Task.FromResult(0);
        
        // Default implementation - should be overridden
        return Task.FromResult(0);
    }

    /// <summary>
    /// Sets the VOX level
    /// </summary>
    public virtual Task<bool> SetVoxLevelAsync(int level)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.VoxLevel))
            return Task.FromResult(false);
        
        // Default implementation - should be overridden
        return Task.FromResult(false);
    }

    /// <summary>
    /// Gets the current VOX level
    /// </summary>
    public virtual Task<int> GetVoxLevelAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.VoxLevel))
            return Task.FromResult(0);
        
        // Default implementation - should be overridden
        return Task.FromResult(0);
    }

    /// <summary>
    /// Sets the VOX delay
    /// </summary>
    public virtual Task<bool> SetVoxDelayAsync(int delayMs)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.VoxDelay))
            return Task.FromResult(false);
        
        // Default implementation - should be overridden
        return Task.FromResult(false);
    }

    /// <summary>
    /// Gets the current VOX delay
    /// </summary>
    public virtual Task<int> GetVoxDelayAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.VoxDelay))
            return Task.FromResult(0);
        
        // Default implementation - should be overridden
        return Task.FromResult(0);
    }

    /// <summary>
    /// Enables or disables QSK/Break-in mode
    /// </summary>
    public virtual Task<bool> SetBreakInAsync(bool enabled)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.BreakIn))
            return Task.FromResult(false);
        
        // Default implementation - should be overridden
        return Task.FromResult(false);
    }

    /// <summary>
    /// Gets the current break-in mode status
    /// </summary>
    public virtual Task<bool> GetBreakInAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.BreakIn))
            return Task.FromResult(false);
        
        // Default implementation - should be overridden
        return Task.FromResult(false);
    }

    /// <summary>
    /// Sets the notch filter frequency
    /// </summary>
    public virtual Task<bool> SetNotchAsync(int frequency)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.Notch))
            return Task.FromResult(false);
        
        // Default implementation - should be overridden
        return Task.FromResult(false);
    }

    /// <summary>
    /// Gets the current notch filter frequency
    /// </summary>
    public virtual Task<int> GetNotchAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.Notch))
            return Task.FromResult(0);
        
        // Default implementation - should be overridden
        return Task.FromResult(0);
    }

    /// <summary>
    /// Generates a key=value semicolon-delimited status string for universal radio status commands.
    /// This method creates a standardized format that can be parsed by clients for pretty-printing.
    /// </summary>
    /// <returns>Key=value status string (e.g., "MODEL=DummyRadio;PORT=COM7;FREQ=14074000;...")</returns>
    public virtual async Task<string> GetUniversalStatusStringAsync()
    {
        try
        {
            var status = await GetStatusAsync();
            var sb = new StringBuilder();
            
            // Radio identification
            sb.Append($"MODEL={Manufacturer} {ModelName}");
            
            // Serial port info
            var portName = _portName ?? "Unknown";
            sb.Append($";PORT={portName}");
            
            // Core radio status
            sb.Append($";FREQ={status.Frequency}");
            sb.Append($";MODE={status.Mode}");
            sb.Append($";VFO={status.CurrentVfo}");
            sb.Append($";POWER={status.IsPoweredOn}");
            sb.Append($";TX={status.IsTransmitting}");
            
            // VFO and split operations
            if (SupportedFeatures.HasFeature(SupportedFeatures.SplitOperation))
            {
                sb.Append($";SPLIT={status.SplitEnabled}");
            }
            
            // RIT/XIT support
            if (SupportedFeatures.HasFeature(SupportedFeatures.RIT))
            {
                sb.Append($";RIT={status.RitEnabled}");
                sb.Append($";RIT_OFFSET={status.RitOffset}");
            }
            
            if (SupportedFeatures.HasFeature(SupportedFeatures.XIT))
            {
                sb.Append($";XIT={status.XitEnabled}");
                sb.Append($";XIT_OFFSET={status.XitOffset}");
            }
            
            // Power and meters
            if (SupportedFeatures.HasFeature(SupportedFeatures.PowerOutput))
            {
                sb.Append($";POWER_LEVEL={status.PowerOutputPercent}");
            }
            
            if (SupportedFeatures.HasFeature(SupportedFeatures.SMeter))
            {
                sb.Append($";S_METER={status.SignalStrength}");
            }
            
            if (SupportedFeatures.HasFeature(SupportedFeatures.SWRMeter))
            {
                sb.Append($";SWR={status.SWR:F1}");
            }
            
            // Antenna selection
            if (SupportedFeatures.HasFeature(SupportedFeatures.AntennaSelection))
            {
                sb.Append($";ANTENNA={status.Antenna}");
            }
            
            // Memory and bandwidth
            if (SupportedFeatures.HasFeature(SupportedFeatures.MemoryChannels) && status.MemoryChannel > 0)
            {
                sb.Append($";MEMORY={status.MemoryChannel}");
            }
            
            if (SupportedFeatures.HasFeature(SupportedFeatures.IFBandwidth))
            {
                sb.Append($";IF_BW={status.IfBandwidth}");
            }
            
            // Noise reduction
            if (SupportedFeatures.HasFeature(SupportedFeatures.NoiseReduction))
            {
                sb.Append($";NR={status.NoiseReductionLevel}");
            }
            
            // Timestamp
            sb.Append($";TIMESTAMP={status.Timestamp:yyyy-MM-ddTHH:mm:ss}Z");
            
            return sb.ToString();
        }
        catch (Exception)
        {
            // Return minimal status on error
            return $"MODEL={Manufacturer} {ModelName};PORT={_portName ?? "Unknown"};ERROR=Status retrieval failed";
        }
    }

    /// <summary>
    /// Disposes the radio instance
    /// </summary>
    public virtual void Dispose()
    {
        _lifetime.Wait();
        try
        {
            if (_disposed) return;
            _disposed = true;
            _session?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _session = null;
        }
        finally { _lifetime.Release(); }
        GC.SuppressFinalize(this);
    }
}