using SharpCAT2.Common.Serial;
using System.Text.RegularExpressions;

namespace SharpCAT2.Common.Radio.Models.Testing;

/// <summary>
/// DummyRadio class for demonstration and testing purposes.
/// 
/// This radio implementation provides comprehensive radio simulation with deterministic responses
/// for development, testing, and demonstration purposes. Unlike the base implementation,
/// DummyRadio contains full radio state management and CAT command simulation logic.
/// 
/// The radio uses FakeSerialPort purely as a transport mechanism, implementing all radio
/// protocol logic at the radio layer for proper separation of concerns.
/// 
/// Designed for:
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
/// 
/// LOGGING ARCHITECTURE:
/// Like all radio classes, DummyRadio is designed to be logging-free.
/// All logging is handled at the server/service layer using dependency-injected ILogger.
/// This class communicates errors and status through return values and exceptions only.
/// </summary>
public class DummyRadio : BaseRadio
{
    #region Private Fields
    
    private bool _isConnected = false;
    
    // Simulated radio state - moved from FakeSerialPort
    private long _currentFrequency = 14074000; // Default to 20m FT8 frequency
    private string _currentMode = "USB";
    private string _currentVfo = "A";
    private bool _splitEnabled = false;
    private bool _powerOn = true;
    private int _ritOffset = 0;
    private int _xitOffset = 0;
    private int _powerOutput = 100; // Percentage
    private int _sMeter = 9; // S9 signal strength
    private double _swr = 1.2; // SWR reading
    private int _antenna = 1; // Active antenna
    private int _cwSpeed = 20; // CW speed in WPM
    private int _noiseReduction = 0; // Noise reduction level
    private int _ifBandwidth = 2400; // IF bandwidth in Hz

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

    #endregion

    #region Connection Methods

    /// <summary>
    /// Simulates connecting to the radio. Creates a FakeSerialPort if none provided.
    /// </summary>
    /// <param name="port">Serial port (can be fake or real for DummyRadio)</param>
    /// <returns>True (always successful for demonstration)</returns>
    public override async Task<bool> ConnectAsync(ISerialPort port)
    {
        // Simulate connection delay
        await Task.Delay(100);
        
        // If no port is provided or it's a real port, create a fake port for simulation
        if (port == null || port is RealSerialPort)
        {
            _serialPort = SerialPortFactory.CreateFakeSerialPort("DUMMY", 9600);
        }
        else
        {
            _serialPort = port;
        }
        
        if (!_serialPort.IsOpen)
        {
            _serialPort.Open();
        }
        
        _isConnected = true;
        
        return true;
    }

    /// <summary>
    /// Simulates disconnecting from the radio
    /// </summary>
    public override void Disconnect()
    {
        _isConnected = false;
        base.Disconnect();
    }

    #endregion

    #region Command Processing

    /// <summary>
    /// Sends a command to the simulated radio and returns the appropriate response.
    /// This implementation contains all the radio simulation logic that was previously
    /// in FakeSerialPort, maintaining the separation of concerns.
    /// </summary>
    /// <param name="command">Command to process</param>
    /// <returns>Response from the simulated radio</returns>
    public override async Task<string?> SendCommandAsync(RadioCommand command)
    {
        if (!_isConnected || _serialPort == null)
        {
            return null;
        }

        try
        {
            // For FakeSerialPort, we handle the simulation at the radio level
            if (_serialPort is FakeSerialPort fakePort)
            {
                // Process the command and generate response directly
                var response = ProcessCommand(command.Command);
                
                // Simulate command processing delay
                await Task.Delay(25);
                
                return response;
            }
            else
            {
                // For real serial ports, use the base implementation
                return await base.SendCommandAsync(command);
            }
        }
        catch (Exception)
        {
            // Command errors are communicated via null return value
            // Service layer will log these errors based on the null response
            return null;
        }
    }

    #endregion

    #region Command Processing

    /// <summary>
    /// Processes a command and returns the appropriate simulated response.
    /// Contains all the radio simulation logic that was moved from FakeSerialPort.
    /// </summary>
    /// <param name="command">Command to process</param>
    /// <returns>Simulated response</returns>
    private string ProcessCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return string.Empty;

        var cmd = command.Trim().ToUpper();

        // Process common CAT commands with simulated responses
        if (cmd == "ID;")
        {
            return "ID020;"; // TS-2000 compatible ID
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
                Frequency = freq; // Update base class property
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
                Mode = _currentMode; // Update base class property
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
        else if (cmd.StartsWith("FR") && cmd.EndsWith(";"))
        {
            // Set VFO command
            var vfoStr = cmd.Substring(2, cmd.Length - 3);
            if (vfoStr == "0")
            {
                _currentVfo = "A";
            }
            else if (vfoStr == "1")
            {
                _currentVfo = "B";
            }
            return cmd;
        }
        else if (cmd == "FR;")
        {
            return $"FR{(_currentVfo == "A" ? "0" : "1")};";
        }
        else if (cmd.StartsWith("FT") && cmd.EndsWith(";"))
        {
            // Split operation
            var splitStr = cmd.Substring(2, cmd.Length - 3);
            _splitEnabled = splitStr == "1";
            return cmd;
        }
        else if (cmd == "FT;")
        {
            return $"FT{(_splitEnabled ? "1" : "0")};";
        }
        else if (cmd.StartsWith("RC;") || cmd.StartsWith("RT;"))
        {
            // RIT/XIT clear
            _ritOffset = 0;
            _xitOffset = 0;
            return cmd;
        }
        else if (cmd.StartsWith("PC") && cmd.EndsWith(";"))
        {
            // Power control
            var powerStr = cmd.Substring(2, cmd.Length - 3);
            if (int.TryParse(powerStr, out int power))
            {
                _powerOutput = Math.Max(0, Math.Min(100, power));
                return cmd;
            }
        }
        else if (cmd == "PC;")
        {
            return $"PC{_powerOutput:D3};";
        }
        else if (cmd.StartsWith("SM") && cmd.EndsWith(";"))
        {
            // S-meter request
            return $"SM0{_sMeter:D3};"; // Return simulated S-meter reading
        }

        // Return a generic success response for unrecognized commands
        return "OK;";
    }

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

    /// <summary>
    /// Maps mode number to mode string
    /// </summary>
    /// <param name="modeNumber">Mode number</param>
    /// <returns>Mode string</returns>
    private new string MapModeNumber(int modeNumber)
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
            _ => "USB"
        };
    }

    #endregion

    #region Enhanced Radio Features Implementation

    /// <summary>
    /// Gets the current VFO (A or B)
    /// </summary>
    public override Task<string> GetVfoAsync()
    {
        return Task.FromResult(_currentVfo);
    }

    /// <summary>
    /// Sets the active VFO
    /// </summary>
    public override async Task<bool> SetVfoAsync(string vfo)
    {
        if (vfo != "A" && vfo != "B")
            return false;

        var command = new RadioCommand($"FR{(vfo == "A" ? "0" : "1")};", "Set VFO");
        var response = await SendCommandAsync(command);
        return !string.IsNullOrEmpty(response);
    }

    /// <summary>
    /// Swaps VFO A and VFO B frequencies
    /// </summary>
    public override async Task<bool> SwapVfoAsync()
    {
        var command = new RadioCommand("SW;", "Swap VFO");
        var response = await SendCommandAsync(command);
        
        // Simulate the swap by toggling VFO
        _currentVfo = _currentVfo == "A" ? "B" : "A";
        
        return !string.IsNullOrEmpty(response);
    }

    /// <summary>
    /// Enables or disables split operation
    /// </summary>
    public override async Task<bool> SetSplitAsync(bool enabled)
    {
        var command = new RadioCommand($"FT{(enabled ? "1" : "0")};", "Set Split");
        var response = await SendCommandAsync(command);
        return !string.IsNullOrEmpty(response);
    }

    /// <summary>
    /// Gets the current split operation status
    /// </summary>
    public override Task<bool> GetSplitAsync()
    {
        return Task.FromResult(_splitEnabled);
    }

    /// <summary>
    /// Sets the RIT (Receiver Incremental Tuning) offset
    /// </summary>
    public override async Task<bool> SetRitAsync(int offsetHz)
    {
        var command = new RadioCommand($"RT{offsetHz:+0000;-0000;+0000};", "Set RIT");
        var response = await SendCommandAsync(command);
        
        if (!string.IsNullOrEmpty(response))
        {
            _ritOffset = offsetHz;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets the current RIT offset
    /// </summary>
    public override Task<int> GetRitAsync()
    {
        return Task.FromResult(_ritOffset);
    }

    /// <summary>
    /// Sets the XIT (Transmitter Incremental Tuning) offset
    /// </summary>
    public override async Task<bool> SetXitAsync(int offsetHz)
    {
        var command = new RadioCommand($"XT{offsetHz:+0000;-0000;+0000};", "Set XIT");
        var response = await SendCommandAsync(command);
        
        if (!string.IsNullOrEmpty(response))
        {
            _xitOffset = offsetHz;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets the current XIT offset
    /// </summary>
    public override Task<int> GetXitAsync()
    {
        return Task.FromResult(_xitOffset);
    }

    /// <summary>
    /// Sets the power output level
    /// </summary>
    public override async Task<bool> SetPowerOutputAsync(int powerPercent)
    {
        var command = new RadioCommand($"PC{powerPercent:D3};", "Set Power Output");
        var response = await SendCommandAsync(command);
        return !string.IsNullOrEmpty(response);
    }

    /// <summary>
    /// Gets the current power output level
    /// </summary>
    public override Task<int> GetPowerOutputAsync()
    {
        return Task.FromResult(_powerOutput);
    }

    /// <summary>
    /// Gets the S-meter reading
    /// </summary>
    public override Task<int> GetSMeterAsync()
    {
        // Simulate varying S-meter readings
        _sMeter = 5 + (int)(Math.Sin(DateTime.Now.Millisecond / 100.0) * 4); // S5-S9
        return Task.FromResult(_sMeter);
    }

    /// <summary>
    /// Gets the SWR reading
    /// </summary>
    public override Task<double> GetSWRAsync()
    {
        // Simulate SWR reading with slight variation
        _swr = 1.0 + (Math.Sin(DateTime.Now.Millisecond / 200.0) * 0.5);
        return Task.FromResult(Math.Max(1.0, _swr));
    }

    /// <summary>
    /// Sets the active antenna
    /// </summary>
    public override async Task<bool> SetAntennaAsync(int antenna)
    {
        if (antenna < 1 || antenna > 4)
            return false;

        var command = new RadioCommand($"AN{antenna};", "Set Antenna");
        var response = await SendCommandAsync(command);
        
        if (!string.IsNullOrEmpty(response))
        {
            _antenna = antenna;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets the current active antenna
    /// </summary>
    public override Task<int> GetAntennaAsync()
    {
        return Task.FromResult(_antenna);
    }

    /// <summary>
    /// Sets the CW keyer speed
    /// </summary>
    public override async Task<bool> SetCwSpeedAsync(int wpm)
    {
        if (wpm < 4 || wpm > 60)
            return false;

        var command = new RadioCommand($"KS{wpm:D3};", "Set CW Speed");
        var response = await SendCommandAsync(command);
        
        if (!string.IsNullOrEmpty(response))
        {
            _cwSpeed = wpm;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets the current CW keyer speed
    /// </summary>
    public override Task<int> GetCwSpeedAsync()
    {
        return Task.FromResult(_cwSpeed);
    }

    /// <summary>
    /// Sends a CW message
    /// </summary>
    public override async Task<bool> SendCwMessageAsync(string message)
    {
        if (string.IsNullOrEmpty(message))
            return false;

        var command = new RadioCommand($"KY {message};", "Send CW Message");
        var response = await SendCommandAsync(command);
        return !string.IsNullOrEmpty(response);
    }

    /// <summary>
    /// Sets the noise reduction level
    /// </summary>
    public override async Task<bool> SetNoiseReductionAsync(int level)
    {
        if (level < 0 || level > 10)
            return false;

        var command = new RadioCommand($"NR{level:D2};", "Set Noise Reduction");
        var response = await SendCommandAsync(command);
        
        if (!string.IsNullOrEmpty(response))
        {
            _noiseReduction = level;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets the current noise reduction level
    /// </summary>
    public override Task<int> GetNoiseReductionAsync()
    {
        return Task.FromResult(_noiseReduction);
    }

    /// <summary>
    /// Sets the IF bandwidth
    /// </summary>
    public override async Task<bool> SetIfBandwidthAsync(int bandwidth)
    {
        var command = new RadioCommand($"BW{bandwidth:D4};", "Set IF Bandwidth");
        var response = await SendCommandAsync(command);
        
        if (!string.IsNullOrEmpty(response))
        {
            _ifBandwidth = bandwidth;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets the current IF bandwidth
    /// </summary>
    public override Task<int> GetIfBandwidthAsync()
    {
        return Task.FromResult(_ifBandwidth);
    }

    /// <summary>
    /// Powers the radio on or off
    /// </summary>
    public override async Task<bool> SetPowerAsync(bool powerOn)
    {
        var command = new RadioCommand($"PS{(powerOn ? "1" : "0")};", "Set Power");
        var response = await SendCommandAsync(command);
        return !string.IsNullOrEmpty(response);
    }

    /// <summary>
    /// Gets whether the radio is powered on
    /// </summary>
    public override Task<bool> GetPowerAsync()
    {
        return Task.FromResult(_powerOn);
    }

    #endregion

    #region Status and Operations

    /// <summary>
    /// Gets the current simulated radio status
    /// </summary>
    /// <returns>Radio status with simulated values</returns>
    public override async Task<RadioStatus> GetStatusAsync()
    {
        // Use base implementation which will query the serial port for status
        var status = await base.GetStatusAsync();
        
        // Add some dummy-specific information
        status.AdditionalInfo["SimulatedRadio"] = true;
        status.AdditionalInfo["DummyRadioVersion"] = "1.0";
        
        return status;
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
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    #endregion
}