using SharpCAT2.Core.Radio.Contracts;
using SharpCAT2.ServerLibrary.Radio.Protocols;
using SharpCAT2.Core.Radio;
using SharpCAT2.Core.Serial;
using SharpCAT2.ServerLibrary.Serial;
using System.Text.RegularExpressions;

namespace SharpCAT2.ServerLibrary.Radio.Models.Testing;

/// <summary>
/// DummyRadio class for demonstration and testing purposes.
/// 
/// This radio implementation provides comprehensive Kenwood-style CAT command simulation 
/// with protocol-accurate responses for development, testing, and demonstration purposes. 
/// Unlike the base implementation, DummyRadio contains full radio state management and 
/// comprehensive CAT command simulation logic.
/// 
/// SUPPORTED CAT COMMANDS:
/// - ID: Radio identification (ID020; - TS-2000 compatible)
/// - FA/FB: VFO A/B frequency get/set (11-digit Hz format)
/// - MD: Mode get/set (1=LSB, 2=USB, 3=CW, 4=FM, 5=AM, 6=FSK, 7=CW-R, 8=FSK-R)
/// - IF: Comprehensive transceiver information (Kenwood format)
/// - PS: Power status get/set (0=off, 1=on)
/// - FR: VFO selection (0=A, 1=B)
/// - FT: Split operation get/set (0=off, 1=on)
/// - RT/XT: RIT/XIT offset get/set (±9999 Hz format)
/// - RC/RC2: RIT/XIT clear commands
/// - PC: Power output level get/set (000-100%)
/// - SM/SM0/SM1: S-meter readings (main/sub receiver)
/// - AN: Antenna selection (1-4)
/// - RS: Radio status (TX/RIT/XIT/Split flags)
/// - MC: Memory channel get/set (000-999)
/// - KS: CW speed get/set (004-060 WPM)
/// - KY: CW message transmission
/// - NR: Noise reduction get/set (00-10)
/// - BW: IF bandwidth get/set (Hz)
/// - RM3: SWR meter reading
/// - SV: VFO swap command
/// - VV: VFO equal (copy A to B)
/// 
/// The radio uses FakeSerialPort purely as a transport mechanism, implementing all radio
/// protocol logic at the radio layer for proper separation of concerns.
/// 
/// HEALTH CHECK ARCHITECTURE:
/// - DummyRadio responds to "ID;" commands with "ID020;" (TS-2000 compatible)
/// - ResilientRadio health checks use the "ID;" command and accept any non-empty response
/// - Health checks occur every 60 seconds by default when radio is connected
/// - Failed health checks trigger automatic reconnection attempts
/// 
/// SIMULATION DESIGN:
/// - FakeSerialPort: Protocol-agnostic transport (buffering, events, I/O simulation)
/// - DummyRadio: Radio protocol logic (CAT commands, state management, responses)
/// - ResilientRadio: Connection management (health checks, retries, error recovery)
/// 
/// REALISTIC BEHAVIOR:
/// - S-meter readings vary dynamically with time-based simulation
/// - SWR readings change based on frequency and band characteristics
/// - All state is maintained consistently across commands
/// - Protocol-accurate response formatting matches real Kenwood radios
/// 
/// TROUBLESHOOTING:
/// - Enable Debug logging to see health check commands and responses
/// - Check that DummyRadio is properly connected (IsConnected = true)
/// - Verify FakeSerialPort is being used as transport (not real serial port)
/// - Health check failures usually indicate command processing issues, not transport issues
/// 
/// Designed for:
/// - Testing client/server functionality without real hardware
/// - Demonstrating radio features in development environments
/// - Training and educational purposes
/// - UI development and feature testing
/// - CAT protocol validation and testing
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
    

    
    // Simulated radio state - moved from FakeSerialPort
    private long _currentFrequency = 14074000; // Default to 20m FT8 frequency
    private long _vfoBFrequency = 14074000; // VFO B frequency
    private string _currentMode = "USB";
    private string _vfoBMode = "USB"; // VFO B mode
    private string _currentVfo = "A";
    private bool _splitEnabled = false;
    private bool _powerOn = true;
    private bool _ritEnabled = false;
    private bool _xitEnabled = false;
    private int _ritOffset = 0;
    private int _xitOffset = 0;
    private int _powerOutput = 100; // Percentage
    private int _sMeter = 9; // S9 signal strength
    private double _swr = 1.2; // SWR reading
    private int _antenna = 1; // Active antenna
    private int _cwSpeed = 20; // CW speed in WPM
    private int _noiseReduction = 0; // Noise reduction level
    private int _ifBandwidth = 2400; // IF bandwidth in Hz
    private int _memoryChannel = 0; // Current memory channel
    private bool _transmitting = false; // TX status

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


    #endregion

    #region Connection Methods

    /// <summary>
    /// Simulates connecting to the radio. Creates a FakeSerialPort if none provided.
    /// </summary>
    /// <param name="port">Serial port (can be fake or real for DummyRadio)</param>
    /// <returns>True (always successful for demonstration)</returns>
    public override Task<bool> ConnectAsync(ISerialPort port)
    {
        var source = port;
        while (source is ResilientSerialPort wrapper) source = wrapper.InnerPort;
        if (source is not null && source is not FakeSerialPort)
            throw new NotSupportedException("DummyRadio requires an explicit simulated port; it never substitutes for physical hardware.");
        return base.ConnectAsync(port ?? SerialPortFactory.CreateFakeSerialPort("DUMMY", 9600));
    }

    public override async Task<RadioOperationResult<object>> ExecuteCommandAsync(RadioCommand command, CancellationToken cancellationToken = default)
    {
        // The legacy dummy engine always echoes. Consume its known reply within the
        // same queue transaction even for a caller requesting write-only presentation.
        var adapted = command.ExpectsResponse ? command : new RadioCommand(command.Command,
            command.Description, expectsResponse: true, timeoutMs: command.TimeoutMs);
        var result = await base.ExecuteCommandAsync(adapted, cancellationToken);
        if (!command.ExpectsResponse && result.Outcome == RadioOutcome.Succeeded)
            result = new(result.Outcome, result.Evidence, diagnostic: "Dummy echo consumed; caller requested no response value.");
        LastOperationResult = result;
        return result;
    }

    protected override IByteTransport CreateTransport(ISerialPort port) => new DummyCommandTransport(port, ProcessCommand);

    #endregion

    #region Command Processing

    /// <summary>
    /// Sends a command to the simulated radio and returns the appropriate response.
    /// This implementation contains all the radio simulation logic that was previously
    /// in FakeSerialPort, maintaining the separation of concerns.
    /// </summary>
    /// <param name="command">Command to process</param>
    /// <returns>Response from the simulated radio</returns>
    public override Task<string?> SendCommandAsync(RadioCommand command)
    {
        // Preserve the dummy's permissive demonstration syntax, through the session.
        if (!command.Command.EndsWith(';'))
            command = new RadioCommand(command.Command + ";", command.Description, command.ExpectsResponse, command.TimeoutMs);
        return base.SendCommandAsync(command);
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
            // Transceiver information - Enhanced to match Kenwood TS-2000 format exactly
            // Format: IF[freq 11][spaces 5][rit offset 5][rit flag][xit flag][ch 3][tx flag][mode][fr][scan][split][tone][tone# 2][shift];
            var ritFlag = _ritEnabled ? "1" : "0";
            var xitFlag = _xitEnabled ? "1" : "0";
            var splitFlag = _splitEnabled ? "1" : "0";
            var txFlag = _transmitting ? "1" : "0";
            var vfoFlag = _currentVfo == "A" ? "0" : "1";
            var scanFlag = "0"; // Not scanning
            var toneFlag = "0"; // No tone
            var toneNumber = "00"; // No tone number
            var shiftFlag = "0"; // No shift
            
            // Build the response according to Kenwood specification
            var response = $"IF{_currentFrequency:D11}     {_ritOffset:+0000;-0000;+0000}{ritFlag}{xitFlag}{_memoryChannel:D3}{txFlag}{GetModeNumber(_currentMode)}{vfoFlag}{scanFlag}{splitFlag}{toneFlag}{toneNumber}{shiftFlag};";
            
            return response;
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
        else if (cmd == "RC;" || cmd == "RD;")
        {
            // RIT/XIT clear - RC clears RIT, RD gets RIT
            if (cmd == "RC;")
            {
                _ritOffset = 0;
                _ritEnabled = false;
                return cmd;
            }
            else // RD;
            {
                return $"RD{_ritOffset:+0000;-0000;+0000};";
            }
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
            // S-meter request - handle various SM commands
            if (cmd == "SM;")
                return $"SM0{_sMeter:D3};"; // Main receiver S-meter
            else if (cmd == "SM0;")
                return $"SM0{_sMeter:D3};"; // Main receiver S-meter explicit
            else if (cmd == "SM1;")
                return $"SM1{_sMeter:D3};"; // Sub receiver S-meter (same as main for dummy)
            else
                return $"SM0{_sMeter:D3};"; // Default to main receiver
        }
        // VFO B frequency commands
        else if (cmd == "FB;")
        {
            return $"FB{_vfoBFrequency:D11};";
        }
        else if (cmd.StartsWith("FB") && cmd.EndsWith(";"))
        {
            // Set VFO B frequency command
            var freqStr = cmd.Substring(2, cmd.Length - 3);
            if (long.TryParse(freqStr, out long freq))
            {
                _vfoBFrequency = freq;
                return cmd; // Echo the command
            }
        }
        // RIT commands (RT)
        else if (cmd == "RT;")
        {
            return $"RT{_ritOffset:+0000;-0000;+0000};";
        }
        else if (cmd.StartsWith("RT") && cmd.EndsWith(";"))
        {
            // Set RIT offset
            var offsetStr = cmd.Substring(2, cmd.Length - 3);
            if (int.TryParse(offsetStr, out int offset))
            {
                _ritOffset = offset;
                _ritEnabled = offset != 0;
                return cmd;
            }
        }
        // XIT commands (XT)
        else if (cmd == "XT;")
        {
            return $"XT{_xitOffset:+0000;-0000;+0000};";
        }
        else if (cmd.StartsWith("XT") && cmd.EndsWith(";"))
        {
            // Set XIT offset
            var offsetStr = cmd.Substring(2, cmd.Length - 3);
            if (int.TryParse(offsetStr, out int offset))
            {
                _xitOffset = offset;
                _xitEnabled = offset != 0;
                return cmd;
            }
        }
        // XIT clear command
        else if (cmd == "RC2;")
        {
            // Clear XIT
            _xitOffset = 0;
            _xitEnabled = false;
            return cmd;
        }
        // Antenna commands (AN)
        else if (cmd == "AN;")
        {
            return $"AN{_antenna};";
        }
        else if (cmd.StartsWith("AN") && cmd.EndsWith(";"))
        {
            // Set antenna
            var antennaStr = cmd.Substring(2, cmd.Length - 3);
            if (int.TryParse(antennaStr, out int antenna) && antenna >= 1 && antenna <= 4)
            {
                _antenna = antenna;
                return cmd;
            }
        }
        // Radio status commands (RS)
        else if (cmd == "RS;")
        {
            // Radio status - return comprehensive status
            var txStatus = _transmitting ? "1" : "0";
            var ritStatus = _ritEnabled ? "1" : "0";
            var xitStatus = _xitEnabled ? "1" : "0";
            var splitStatus = _splitEnabled ? "1" : "0";
            return $"RS{txStatus}{ritStatus}{xitStatus}{splitStatus}000;";
        }
        // Universal radio status commands
        else if (cmd == "RADIO-STATUS;" || cmd == "RS")
        {
            // Return universal key=value status format
            return GetUniversalStatusStringAsync().Result;
        }
        // Memory channel commands (MC)
        else if (cmd == "MC;")
        {
            return $"MC{_memoryChannel:D3};";
        }
        else if (cmd.StartsWith("MC") && cmd.EndsWith(";"))
        {
            // Set memory channel
            var channelStr = cmd.Substring(2, cmd.Length - 3);
            if (int.TryParse(channelStr, out int channel) && channel >= 0 && channel <= 999)
            {
                _memoryChannel = channel;
                return cmd;
            }
        }
        // CW speed commands (KS)
        else if (cmd == "KS;")
        {
            return $"KS{_cwSpeed:D3};";
        }
        else if (cmd.StartsWith("KS") && cmd.EndsWith(";"))
        {
            // Set CW speed
            var speedStr = cmd.Substring(2, cmd.Length - 3);
            if (int.TryParse(speedStr, out int speed) && speed >= 4 && speed <= 60)
            {
                _cwSpeed = speed;
                return cmd;
            }
        }
        // CW message commands (KY)
        else if (cmd.StartsWith("KY ") && cmd.EndsWith(";"))
        {
            // Send CW message - just echo for simulation
            return cmd;
        }
        // Noise reduction commands (NR)
        else if (cmd == "NR;")
        {
            return $"NR{_noiseReduction:D2};";
        }
        else if (cmd.StartsWith("NR") && cmd.EndsWith(";"))
        {
            // Set noise reduction
            var levelStr = cmd.Substring(2, cmd.Length - 3);
            if (int.TryParse(levelStr, out int level) && level >= 0 && level <= 10)
            {
                _noiseReduction = level;
                return cmd;
            }
        }
        // IF bandwidth commands (BW)
        else if (cmd == "BW;")
        {
            return $"BW{_ifBandwidth:D4};";
        }
        else if (cmd.StartsWith("BW") && cmd.EndsWith(";"))
        {
            // Set IF bandwidth
            var bwStr = cmd.Substring(2, cmd.Length - 3);
            if (int.TryParse(bwStr, out int bandwidth))
            {
                _ifBandwidth = bandwidth;
                return cmd;
            }
        }
        // SWR meter commands (RM3)
        else if (cmd == "RM3;")
        {
            // SWR meter reading - convert SWR to Kenwood format
            var swrReading = (int)((_swr - 1.0) * 100);
            return $"RM3{swrReading:D3};";
        }
        // Legacy dummy SW selector mutation executes only inside its session transport.
        else if (cmd == "SW;")
        {
            _currentVfo = _currentVfo == "A" ? "B" : "A";
            return cmd;
        }
        // VFO swap command (SV)
        else if (cmd == "SV;")
        {
            // Swap VFO A and B
            (_currentFrequency, _vfoBFrequency) = (_vfoBFrequency, _currentFrequency);
            (_currentMode, _vfoBMode) = (_vfoBMode, _currentMode);
            return cmd;
        }
        // VFO equal command (VV)
        else if (cmd == "VV;")
        {
            // Copy VFO A to VFO B
            _vfoBFrequency = _currentFrequency;
            _vfoBMode = _currentMode;
            return cmd;
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
        // Simulate varying S-meter readings with more realistic behavior
        var baseTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
        var variation = Math.Sin(baseTime / 1000.0) * 3; // Slow variation
        var noise = (Random.Shared.NextDouble() - 0.5) * 2; // Random noise
        _sMeter = Math.Max(1, Math.Min(15, 7 + (int)(variation + noise))); // S1-S9+60dB range
        return Task.FromResult(_sMeter);
    }

    /// <summary>
    /// Gets the SWR reading
    /// </summary>
    public override Task<double> GetSWRAsync()
    {
        // Simulate SWR reading with realistic variation based on frequency
        var freqMHz = _currentFrequency / 1000000.0;
        var bandCenter = GetBandCenter(freqMHz);
        var bandDeviation = Math.Abs(freqMHz - bandCenter) / bandCenter;
        
        // SWR increases as we move away from band center
        var baseSWR = 1.0 + (bandDeviation * 2.0);
        var variation = Math.Sin(DateTime.Now.Millisecond / 300.0) * 0.2;
        _swr = Math.Max(1.0, Math.Min(3.0, baseSWR + variation));
        
        return Task.FromResult(_swr);
    }
    
    /// <summary>
    /// Gets the approximate band center frequency for SWR simulation
    /// </summary>
    private double GetBandCenter(double freqMHz)
    {
        return freqMHz switch
        {
            >= 1.8 and <= 2.0 => 1.9,     // 160m
            >= 3.5 and <= 4.0 => 3.75,    // 80m
            >= 7.0 and <= 7.3 => 7.15,    // 40m
            >= 14.0 and <= 14.35 => 14.175, // 20m
            >= 21.0 and <= 21.45 => 21.225, // 15m
            >= 28.0 and <= 29.7 => 28.85,   // 10m
            _ => freqMHz // Default to current frequency
        };
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
    public override void Dispose() => base.Dispose();

    #endregion
}