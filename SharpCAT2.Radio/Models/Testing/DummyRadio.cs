using System.IO.Ports;

namespace SharpCAT2.Radio.Models.Testing;

/// <summary>
/// DummyRadio class for demonstration and testing purposes.
/// 
/// This radio implementation provides deterministic, simulated responses without requiring
/// actual radio hardware or serial port communication. It's designed for:
/// - Testing client/server functionality without real hardware
/// - Demonstrating radio features in development environments
/// - Training and educational purposes
/// - UI development and feature testing
/// 
/// LIMITATIONS:
/// - Simulated responses only - no actual radio communication
/// - Does not validate frequency ranges or mode compatibility
/// - Fixed response timing (no real hardware delays)
/// - Memory and settings are reset on each restart
/// </summary>
public class DummyRadio : BaseRadio
{
    #region Private Fields
    
    private bool _isConnected = false;
    private long _currentFrequency = 14074000; // Default to 20m FT8 frequency
    private string _currentMode = "USB";
    private string _currentVfo = "A";
    private bool _splitEnabled = false;
    private bool _powerOn = true;
    private int _powerOutput = 50; // 50%
    private int _ritOffset = 0;
    private int _xitOffset = 0;
    private int _ifBandwidth = 2400;
    private int _cwSpeed = 20; // 20 WPM
    private int _noiseReduction = 0;
    private int _antenna = 1;
    private int _sMeter = 5; // S5 signal
    private double _swr = 1.2; // Good SWR
    
    // Simulated memory channels (channel -> (frequency, mode))
    private readonly Dictionary<int, (long frequency, string mode)> _memoryChannels = new();

    #endregion

    #region Radio Properties

    /// <summary>
    /// Gets the radio model name
    /// </summary>
    public override string ModelName => "DummyRadio";

    /// <summary>
    /// Gets the radio manufacturer
    /// </summary>
    public override string Manufacturer => "SharpCAT2";

    /// <summary>
    /// DummyRadio supports comprehensive features for demonstration purposes
    /// </summary>
    public override SupportedFeatures SupportedFeatures => 
        SupportedFeatures.FrequencyControl | 
        SupportedFeatures.ModeControl |
        SupportedFeatures.DualVFO | 
        SupportedFeatures.VFOSwap |
        SupportedFeatures.SplitOperation |
        SupportedFeatures.RIT | 
        SupportedFeatures.XIT |
        SupportedFeatures.PowerOutput |
        SupportedFeatures.SMeter |
        SupportedFeatures.SWRMeter |
        SupportedFeatures.AntennaSelection |
        SupportedFeatures.MemoryChannels |
        SupportedFeatures.CWKeyer |
        SupportedFeatures.CWSpeed |
        SupportedFeatures.CWMessage |
        SupportedFeatures.NoiseReduction |
        SupportedFeatures.IFBandwidth |
        SupportedFeatures.TransmitStatus |
        SupportedFeatures.ReceiveStatus |
        SupportedFeatures.RadioID |
        SupportedFeatures.PowerOnOff |
        SupportedFeatures.ComputerControl;

    /// <summary>
    /// Gets whether the radio is currently connected (simulated)
    /// </summary>
    public new bool IsConnected => _isConnected;

    /// <summary>
    /// Gets or sets the current frequency
    /// </summary>
    public override long Frequency 
    { 
        get => _currentFrequency; 
        set => _currentFrequency = value; 
    }

    /// <summary>
    /// Gets or sets the current operating mode
    /// </summary>
    public override string Mode 
    { 
        get => _currentMode; 
        set => _currentMode = value; 
    }

    #endregion

    #region Connection Methods

    /// <summary>
    /// Simulates connecting to the radio (always succeeds for DummyRadio)
    /// </summary>
    /// <param name="port">Serial port (ignored for DummyRadio)</param>
    /// <returns>True (always successful for demonstration)</returns>
    public override async Task<bool> ConnectAsync(SerialPort port)
    {
        // Simulate connection delay
        await Task.Delay(100);
        
        _isConnected = true;
        
        Console.WriteLine("DummyRadio: Simulated connection established");
        Console.WriteLine("DummyRadio: This is a testing radio with simulated responses");
        
        return true;
    }

    /// <summary>
    /// Simulates disconnecting from the radio
    /// </summary>
    public override void Disconnect()
    {
        _isConnected = false;
        Console.WriteLine("DummyRadio: Simulated disconnection");
    }

    #endregion

    #region Command Processing

    /// <summary>
    /// Simulates sending a command to the radio and returns a deterministic response
    /// </summary>
    /// <param name="command">Command to process</param>
    /// <returns>Simulated response based on command</returns>
    public override async Task<string?> SendCommandAsync(RadioCommand command)
    {
        if (!_isConnected)
        {
            return null;
        }

        // Simulate command processing delay
        await Task.Delay(50);

        var cmd = command.Command.ToUpper();
        
        // Process common commands with simulated responses
        if (cmd == "ID;")
        {
            return "ID999;"; // Generic ID for DummyRadio
        }
        else if (cmd == "FA;")
        {
            return $"FA{_currentFrequency:D11};";
        }
        else if (cmd.StartsWith("FA") && cmd.EndsWith(";"))
        {
            // Set frequency command
            var freqStr = cmd.Substring(2, cmd.Length - 3);
            if (long.TryParse(freqStr, out long freq))
            {
                _currentFrequency = freq;
                return cmd; // Echo the command
            }
        }
        else if (cmd == "MD;")
        {
            return $"MD{GetModeNumber(_currentMode)};";
        }
        else if (cmd.StartsWith("MD") && cmd.EndsWith(";"))
        {
            // Set mode command
            var modeStr = cmd.Substring(2, cmd.Length - 3);
            if (int.TryParse(modeStr, out int modeNum))
            {
                _currentMode = MapModeNumber(modeNum);
                return cmd; // Echo the command
            }
        }
        else if (cmd == "IF;")
        {
            // Transceiver information
            var ritFlag = _ritOffset != 0 ? "1" : "0";
            var xitFlag = _xitOffset != 0 ? "1" : "0";
            var splitFlag = _splitEnabled ? "1" : "0";
            
            return $"IF{_currentFrequency:D11}     {_ritOffset:+0000;-0000;+0000}{ritFlag}{xitFlag}000{0}{GetModeNumber(_currentMode)}{_currentVfo[0]}{0}{splitFlag}00000;";
        }
        else if (cmd == "PS;")
        {
            return $"PS{(_powerOn ? "1" : "0")};";
        }
        else if (cmd.StartsWith("PS") && cmd.EndsWith(";"))
        {
            var powerStr = cmd.Substring(2, cmd.Length - 3);
            _powerOn = powerStr == "1";
            return cmd;
        }

        // Return a generic success response for unrecognized commands
        return "OK;";
    }

    #endregion

    #region Radio Status

    /// <summary>
    /// Gets the current simulated radio status
    /// </summary>
    /// <returns>Radio status with simulated values</returns>
    public override async Task<RadioStatus> GetStatusAsync()
    {
        await Task.Delay(25); // Simulate status query delay

        var status = new RadioStatus
        {
            Frequency = _currentFrequency,
            Mode = _currentMode,
            CurrentVfo = _currentVfo,
            SignalStrength = _sMeter,
            Antenna = _antenna,
            IsPoweredOn = _powerOn,
            IsTransmitting = false, // Always receiving for demo
            Timestamp = DateTime.UtcNow
        };

        // Add additional DummyRadio-specific info
        status.AdditionalInfo["SplitEnabled"] = _splitEnabled;
        status.AdditionalInfo["RITOffset"] = _ritOffset;
        status.AdditionalInfo["XITOffset"] = _xitOffset;
        status.AdditionalInfo["PowerOutput"] = _powerOutput;
        status.AdditionalInfo["SWR"] = _swr;
        status.AdditionalInfo["IFBandwidth"] = _ifBandwidth;
        status.AdditionalInfo["CWSpeed"] = _cwSpeed;
        status.AdditionalInfo["NoiseReduction"] = _noiseReduction;

        return status;
    }

    #endregion

    #region Frequency and Mode Operations

    /// <summary>
    /// Sets the radio frequency (simulated)
    /// </summary>
    /// <param name="frequency">Frequency in Hz</param>
    /// <returns>True (always successful for DummyRadio)</returns>
    public override async Task<bool> SetFrequencyAsync(long frequency)
    {
        await Task.Delay(25);
        _currentFrequency = frequency;
        Console.WriteLine($"DummyRadio: Frequency set to {frequency:N0} Hz");
        return true;
    }

    /// <summary>
    /// Sets the radio operating mode (simulated)
    /// </summary>
    /// <param name="mode">Operating mode</param>
    /// <returns>True (always successful for DummyRadio)</returns>
    public override async Task<bool> SetModeAsync(string mode)
    {
        await Task.Delay(25);
        _currentMode = mode;
        Console.WriteLine($"DummyRadio: Mode set to {mode}");
        return true;
    }

    #endregion

    #region VFO Operations

    /// <summary>
    /// Gets the current VFO (simulated)
    /// </summary>
    /// <returns>Current VFO designation</returns>
    public override async Task<string> GetVfoAsync()
    {
        await Task.Delay(25);
        return _currentVfo;
    }

    /// <summary>
    /// Sets the active VFO (simulated)
    /// </summary>
    /// <param name="vfo">VFO to activate</param>
    /// <returns>True if successful</returns>
    public override async Task<bool> SetVfoAsync(string vfo)
    {
        await Task.Delay(25);
        if (vfo == "A" || vfo == "B")
        {
            _currentVfo = vfo;
            Console.WriteLine($"DummyRadio: VFO set to {vfo}");
            return true;
        }
        return false;
    }

    /// <summary>
    /// Swaps VFO A and VFO B frequencies (simulated)
    /// </summary>
    /// <returns>True (always successful for DummyRadio)</returns>
    public override async Task<bool> SwapVfoAsync()
    {
        await Task.Delay(25);
        _currentVfo = _currentVfo == "A" ? "B" : "A";
        Console.WriteLine($"DummyRadio: VFO swapped to {_currentVfo}");
        return true;
    }

    #endregion

    #region Split Operations

    /// <summary>
    /// Enables or disables split operation (simulated)
    /// </summary>
    /// <param name="enabled">True to enable split</param>
    /// <returns>True (always successful for DummyRadio)</returns>
    public override async Task<bool> SetSplitAsync(bool enabled)
    {
        await Task.Delay(25);
        _splitEnabled = enabled;
        Console.WriteLine($"DummyRadio: Split {(enabled ? "enabled" : "disabled")}");
        return true;
    }

    /// <summary>
    /// Gets the current split operation status (simulated)
    /// </summary>
    /// <returns>Current split status</returns>
    public override async Task<bool> GetSplitAsync()
    {
        await Task.Delay(25);
        return _splitEnabled;
    }

    #endregion

    #region RIT/XIT Operations

    /// <summary>
    /// Sets the RIT offset (simulated)
    /// </summary>
    /// <param name="offsetHz">Offset in Hz</param>
    /// <returns>True (always successful for DummyRadio)</returns>
    public override async Task<bool> SetRitAsync(int offsetHz)
    {
        await Task.Delay(25);
        _ritOffset = Math.Max(-9999, Math.Min(9999, offsetHz)); // Clamp to typical range
        Console.WriteLine($"DummyRadio: RIT offset set to {_ritOffset} Hz");
        return true;
    }

    /// <summary>
    /// Gets the current RIT offset (simulated)
    /// </summary>
    /// <returns>RIT offset in Hz</returns>
    public override async Task<int> GetRitAsync()
    {
        await Task.Delay(25);
        return _ritOffset;
    }

    /// <summary>
    /// Sets the XIT offset (simulated)
    /// </summary>
    /// <param name="offsetHz">Offset in Hz</param>
    /// <returns>True (always successful for DummyRadio)</returns>
    public override async Task<bool> SetXitAsync(int offsetHz)
    {
        await Task.Delay(25);
        _xitOffset = Math.Max(-9999, Math.Min(9999, offsetHz)); // Clamp to typical range
        Console.WriteLine($"DummyRadio: XIT offset set to {_xitOffset} Hz");
        return true;
    }

    /// <summary>
    /// Gets the current XIT offset (simulated)
    /// </summary>
    /// <returns>XIT offset in Hz</returns>
    public override async Task<int> GetXitAsync()
    {
        await Task.Delay(25);
        return _xitOffset;
    }

    #endregion

    #region Power and Antenna Operations

    /// <summary>
    /// Sets the power output level (simulated)
    /// </summary>
    /// <param name="powerPercent">Power level as percentage</param>
    /// <returns>True (always successful for DummyRadio)</returns>
    public override async Task<bool> SetPowerOutputAsync(int powerPercent)
    {
        await Task.Delay(25);
        _powerOutput = Math.Max(0, Math.Min(100, powerPercent));
        Console.WriteLine($"DummyRadio: Power output set to {_powerOutput}%");
        return true;
    }

    /// <summary>
    /// Gets the current power output level (simulated)
    /// </summary>
    /// <returns>Power level as percentage</returns>
    public override async Task<int> GetPowerOutputAsync()
    {
        await Task.Delay(25);
        return _powerOutput;
    }

    /// <summary>
    /// Sets the active antenna (simulated)
    /// </summary>
    /// <param name="antenna">Antenna number</param>
    /// <returns>True (always successful for DummyRadio)</returns>
    public override async Task<bool> SetAntennaAsync(int antenna)
    {
        await Task.Delay(25);
        _antenna = Math.Max(1, Math.Min(3, antenna)); // Support 3 antennas
        Console.WriteLine($"DummyRadio: Antenna set to {_antenna}");
        return true;
    }

    /// <summary>
    /// Gets the current active antenna (simulated)
    /// </summary>
    /// <returns>Antenna number</returns>
    public override async Task<int> GetAntennaAsync()
    {
        await Task.Delay(25);
        return _antenna;
    }

    #endregion

    #region Meter Readings

    /// <summary>
    /// Gets the S-meter reading (simulated with slight variation)
    /// </summary>
    /// <returns>S-meter reading</returns>
    public override async Task<int> GetSMeterAsync()
    {
        await Task.Delay(25);
        
        // Add slight random variation to simulate real readings
        var random = new Random();
        var variation = random.Next(-1, 2); // -1, 0, or 1
        return Math.Max(0, Math.Min(15, _sMeter + variation));
    }

    /// <summary>
    /// Gets the SWR reading (simulated with slight variation)
    /// </summary>
    /// <returns>SWR reading</returns>
    public override async Task<double> GetSWRAsync()
    {
        await Task.Delay(25);
        
        // Add slight random variation to simulate real readings
        var random = new Random();
        var variation = (random.NextDouble() - 0.5) * 0.2; // ±0.1
        return Math.Max(1.0, _swr + variation);
    }

    #endregion

    #region Memory Operations

    /// <summary>
    /// Sets a memory channel (simulated)
    /// </summary>
    /// <param name="channel">Memory channel number</param>
    /// <param name="frequency">Frequency in Hz</param>
    /// <param name="mode">Operating mode</param>
    /// <returns>True (always successful for DummyRadio)</returns>
    public override async Task<bool> SetMemoryChannelAsync(int channel, long frequency, string mode)
    {
        await Task.Delay(25);
        
        if (channel >= 0 && channel <= 99) // Support 100 memory channels
        {
            _memoryChannels[channel] = (frequency, mode);
            Console.WriteLine($"DummyRadio: Memory channel {channel} set to {frequency:N0} Hz, {mode}");
            return true;
        }
        
        return false;
    }

    /// <summary>
    /// Recalls a memory channel (simulated)
    /// </summary>
    /// <param name="channel">Memory channel number</param>
    /// <returns>True if successful</returns>
    public override async Task<bool> RecallMemoryChannelAsync(int channel)
    {
        await Task.Delay(25);
        
        if (_memoryChannels.TryGetValue(channel, out var memory))
        {
            _currentFrequency = memory.frequency;
            _currentMode = memory.mode;
            Console.WriteLine($"DummyRadio: Recalled memory channel {channel}: {memory.frequency:N0} Hz, {memory.mode}");
            return true;
        }
        
        Console.WriteLine($"DummyRadio: Memory channel {channel} is empty");
        return false;
    }

    #endregion

    #region CW Operations

    /// <summary>
    /// Sets the CW keyer speed (simulated)
    /// </summary>
    /// <param name="wpm">Words per minute</param>
    /// <returns>True (always successful for DummyRadio)</returns>
    public override async Task<bool> SetCwSpeedAsync(int wpm)
    {
        await Task.Delay(25);
        _cwSpeed = Math.Max(5, Math.Min(60, wpm)); // Typical range
        Console.WriteLine($"DummyRadio: CW speed set to {_cwSpeed} WPM");
        return true;
    }

    /// <summary>
    /// Gets the current CW keyer speed (simulated)
    /// </summary>
    /// <returns>Words per minute</returns>
    public override async Task<int> GetCwSpeedAsync()
    {
        await Task.Delay(25);
        return _cwSpeed;
    }

    /// <summary>
    /// Sends a CW message (simulated)
    /// </summary>
    /// <param name="message">Message to send</param>
    /// <returns>True (always successful for DummyRadio)</returns>
    public override async Task<bool> SendCwMessageAsync(string message)
    {
        await Task.Delay(100); // Simulate message sending delay
        Console.WriteLine($"DummyRadio: Sending CW message: '{message}' at {_cwSpeed} WPM");
        
        // Simulate sending time based on message length and speed
        var sendingTime = message.Length * 60 / (_cwSpeed * 5); // Rough estimation
        await Task.Delay(sendingTime * 100); // Scale down for demo
        
        Console.WriteLine("DummyRadio: CW message sent");
        return true;
    }

    #endregion

    #region Advanced Features

    /// <summary>
    /// Sets the IF bandwidth (simulated)
    /// </summary>
    /// <param name="bandwidth">Bandwidth in Hz</param>
    /// <returns>True (always successful for DummyRadio)</returns>
    public override async Task<bool> SetIfBandwidthAsync(int bandwidth)
    {
        await Task.Delay(25);
        _ifBandwidth = Math.Max(50, Math.Min(6000, bandwidth)); // Typical range
        Console.WriteLine($"DummyRadio: IF bandwidth set to {_ifBandwidth} Hz");
        return true;
    }

    /// <summary>
    /// Gets the current IF bandwidth (simulated)
    /// </summary>
    /// <returns>Bandwidth in Hz</returns>
    public override async Task<int> GetIfBandwidthAsync()
    {
        await Task.Delay(25);
        return _ifBandwidth;
    }

    /// <summary>
    /// Sets the noise reduction level (simulated)
    /// </summary>
    /// <param name="level">Noise reduction level</param>
    /// <returns>True (always successful for DummyRadio)</returns>
    public override async Task<bool> SetNoiseReductionAsync(int level)
    {
        await Task.Delay(25);
        _noiseReduction = Math.Max(0, Math.Min(10, level));
        Console.WriteLine($"DummyRadio: Noise reduction set to {_noiseReduction}");
        return true;
    }

    /// <summary>
    /// Gets the current noise reduction level (simulated)
    /// </summary>
    /// <returns>Noise reduction level</returns>
    public override async Task<int> GetNoiseReductionAsync()
    {
        await Task.Delay(25);
        return _noiseReduction;
    }

    /// <summary>
    /// Powers the radio on or off (simulated)
    /// </summary>
    /// <param name="powerOn">True to power on</param>
    /// <returns>True (always successful for DummyRadio)</returns>
    public override async Task<bool> SetPowerAsync(bool powerOn)
    {
        await Task.Delay(100); // Simulate power on/off delay
        _powerOn = powerOn;
        Console.WriteLine($"DummyRadio: Power {(powerOn ? "ON" : "OFF")}");
        return true;
    }

    /// <summary>
    /// Gets whether the radio is powered on (simulated)
    /// </summary>
    /// <returns>True if powered on</returns>
    public override async Task<bool> GetPowerAsync()
    {
        await Task.Delay(25);
        return _powerOn;
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Maps mode string to mode number for responses
    /// </summary>
    /// <param name="mode">Mode string</param>
    /// <returns>Mode number</returns>
    private int GetModeNumber(string mode)
    {
        return mode.ToUpper() switch
        {
            "LSB" => 1,
            "USB" => 2,
            "CW" => 3,
            "FM" => 4,
            "AM" => 5,
            "FSK" => 6,
            "CW-R" => 7,
            "FSK-R" => 8,
            _ => 2 // Default to USB
        };
    }

    #endregion

    #region Disposal

    /// <summary>
    /// Disposes the DummyRadio instance
    /// </summary>
    public override void Dispose()
    {
        if (!_disposed)
        {
            Disconnect();
            Console.WriteLine("DummyRadio: Disposed");
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    #endregion
}