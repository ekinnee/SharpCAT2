using System.Collections.Concurrent;
using System.IO.Ports;
using System.Text;

namespace SharpCAT2.Common.Serial;

/// <summary>
/// Fake serial port implementation for testing and simulation.
/// Provides deterministic responses to common radio commands without requiring actual hardware.
/// This is designed for development, testing, and demonstration purposes.
/// </summary>
public class FakeSerialPort : ISerialPort
{
    private readonly ConcurrentQueue<string> _inputBuffer = new();
    private readonly StringBuilder _outputBuffer = new();
    private readonly object _lock = new();
    private bool _isOpen = false;
    private bool _disposed = false;

    // Simulated radio state
    private long _currentFrequency = 14074000; // Default to 20m FT8 frequency
    private string _currentMode = "USB";
    private string _currentVfo = "A";
    private bool _splitEnabled = false;
    private bool _powerOn = true;
    private int _ritOffset = 0;
    private int _xitOffset = 0;

    #region Constructors

    /// <summary>
    /// Initializes a new instance of FakeSerialPort
    /// </summary>
    /// <param name="portName">Simulated port name</param>
    /// <param name="baudRate">Simulated baud rate</param>
    public FakeSerialPort(string portName = "FAKE", int baudRate = 9600)
    {
        PortName = portName;
        BaudRate = baudRate;
        ReadTimeout = 500;
        WriteTimeout = 500;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets whether the fake serial port is "open"
    /// </summary>
    public bool IsOpen => _isOpen;

    /// <summary>
    /// Gets the simulated port name
    /// </summary>
    public string PortName { get; }

    /// <summary>
    /// Gets the simulated baud rate
    /// </summary>
    public int BaudRate { get; }

    /// <summary>
    /// Gets the number of bytes waiting to be read from the simulation buffer
    /// </summary>
    public int BytesToRead
    {
        get
        {
            lock (_lock)
            {
                return _outputBuffer.Length;
            }
        }
    }

    /// <summary>
    /// Gets or sets the simulated read timeout
    /// </summary>
    public int ReadTimeout { get; set; }

    /// <summary>
    /// Gets or sets the simulated write timeout
    /// </summary>
    public int WriteTimeout { get; set; }

    #endregion

    #region Connection Management

    /// <summary>
    /// "Opens" the fake serial port
    /// </summary>
    public void Open()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(FakeSerialPort));

        _isOpen = true;
        Console.WriteLine($"FakeSerialPort: Simulated connection opened on {PortName} at {BaudRate} baud");
    }

    /// <summary>
    /// "Closes" the fake serial port
    /// </summary>
    public void Close()
    {
        _isOpen = false;
        lock (_lock)
        {
            _outputBuffer.Clear();
            while (_inputBuffer.TryDequeue(out _)) { }
        }
        Console.WriteLine($"FakeSerialPort: Simulated connection closed on {PortName}");
    }

    #endregion

    #region Data Communication

    /// <summary>
    /// Simulates writing data to the serial port and generates appropriate responses
    /// </summary>
    /// <param name="data">Data to write</param>
    public void Write(string data)
    {
        if (!_isOpen)
            throw new InvalidOperationException("Serial port is not open");

        // Simulate processing the command and generating a response
        _ = Task.Run(async () =>
        {
            // Simulate command processing delay
            await Task.Delay(25);

            var response = ProcessCommand(data);
            if (!string.IsNullOrEmpty(response))
            {
                // Add response to output buffer
                lock (_lock)
                {
                    _outputBuffer.Append(response);
                }

                // Trigger data received event - SerialDataReceivedEventArgs doesn't have public constructor
                // We'll simulate the event by creating a simple implementation
                try
                {
                    DataReceived?.Invoke(this, null!);
                }
                catch
                {
                    // Ignore errors in event handler simulation
                }
            }
        });
    }

    /// <summary>
    /// Simulates writing a line to the serial port
    /// </summary>
    /// <param name="data">Data to write</param>
    public void WriteLine(string data)
    {
        Write(data + Environment.NewLine);
    }

    /// <summary>
    /// Reads all available data from the simulation buffer
    /// </summary>
    /// <returns>Data from the buffer</returns>
    public string ReadExisting()
    {
        if (!_isOpen)
            throw new InvalidOperationException("Serial port is not open");

        lock (_lock)
        {
            var result = _outputBuffer.ToString();
            _outputBuffer.Clear();
            return result;
        }
    }

    /// <summary>
    /// Reads data from the simulation buffer into a byte array
    /// </summary>
    /// <param name="buffer">Buffer to read into</param>
    /// <param name="offset">Offset in buffer</param>
    /// <param name="count">Maximum bytes to read</param>
    /// <returns>Number of bytes read</returns>
    public int Read(byte[] buffer, int offset, int count)
    {
        if (!_isOpen)
            throw new InvalidOperationException("Serial port is not open");

        var data = ReadExisting();
        var bytes = Encoding.ASCII.GetBytes(data);
        var bytesToCopy = Math.Min(bytes.Length, count);
        
        Array.Copy(bytes, 0, buffer, offset, bytesToCopy);
        
        return bytesToCopy;
    }

    #endregion

    #region Buffer Management

    /// <summary>
    /// Clears the input buffer (simulation)
    /// </summary>
    public void DiscardInBuffer()
    {
        lock (_lock)
        {
            while (_inputBuffer.TryDequeue(out _)) { }
        }
    }

    /// <summary>
    /// Clears the output buffer (simulation)
    /// </summary>
    public void DiscardOutBuffer()
    {
        lock (_lock)
        {
            _outputBuffer.Clear();
        }
    }

    #endregion

    #region Command Processing

    /// <summary>
    /// Processes a command and returns the appropriate simulated response
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
            return "ID999;"; // Generic ID for FakeSerialPort
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
                Console.WriteLine($"FakeSerialPort: Frequency set to {freq:N0} Hz");
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
                Console.WriteLine($"FakeSerialPort: Mode set to {_currentMode}");
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
            Console.WriteLine($"FakeSerialPort: Power {(_powerOn ? "ON" : "OFF")}");
            return cmd;
        }
        else if (cmd.StartsWith("FR") && cmd.EndsWith(";"))
        {
            // Set VFO command
            var vfoStr = cmd.Substring(2, cmd.Length - 3);
            if (vfoStr == "0")
            {
                _currentVfo = "A";
                Console.WriteLine("FakeSerialPort: VFO set to A");
            }
            else if (vfoStr == "1")
            {
                _currentVfo = "B";
                Console.WriteLine("FakeSerialPort: VFO set to B");
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
            Console.WriteLine($"FakeSerialPort: Split {(_splitEnabled ? "enabled" : "disabled")}");
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
            Console.WriteLine("FakeSerialPort: RIT/XIT cleared");
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
    private string MapModeNumber(int modeNumber)
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

    #region Events

    /// <summary>
    /// Event raised when simulated data is received
    /// </summary>
    public event SerialDataReceivedEventHandler? DataReceived;

    #endregion

    #region Disposal

    /// <summary>
    /// Disposes the fake serial port
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            Close();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    #endregion
}