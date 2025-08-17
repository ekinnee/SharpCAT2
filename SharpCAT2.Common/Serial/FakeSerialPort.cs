using System.Collections.Concurrent;
using RJCP.IO.Ports;
using System.Text;

namespace SharpCAT2.Common.Serial;

/// <summary>
/// Pure fake serial port implementation for testing and simulation.
/// Provides only serial I/O simulation (open/close/read/write, buffers, connection state)
/// without any knowledge of protocols, radio commands, or logging.
/// 
/// This is designed for protocol-agnostic testing and simulation purposes.
/// All protocol-specific logic should be implemented at higher layers.
/// 
/// This file has been reordered to follow C# coding standards:
/// - Fields and constants
/// - Properties  
/// - Constructors
/// - Public methods
/// - Private methods
/// </summary>
public class FakeSerialPort : ISerialPort
{
    private readonly ConcurrentQueue<string> _inputBuffer = new();
    private readonly StringBuilder _outputBuffer = new();
    private readonly ConcurrentQueue<string> _injectDataQueue = new();
    private readonly object _lock = new();
    private bool _isOpen = false;
    private bool _disposed = false;

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

    #region Connection Management

    /// <summary>
    /// "Opens" the fake serial port for simulation
    /// </summary>
    public void Open()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(FakeSerialPort));

        _isOpen = true;
    }

    /// <summary>
    /// "Closes" the fake serial port and clears buffers
    /// </summary>
    public void Close()
    {
        _isOpen = false;
        lock (_lock)
        {
            _outputBuffer.Clear();
            while (_inputBuffer.TryDequeue(out _)) { }
            while (_injectDataQueue.TryDequeue(out _)) { }
        }
    }

    #endregion

    #region Data Communication

    /// <summary>
    /// Simulates writing data to the serial port.
    /// Data is queued for processing by higher layers.
    /// </summary>
    /// <param name="data">Data to write</param>
    public void Write(string data)
    {
        if (!_isOpen)
            throw new InvalidOperationException("Serial port is not open");

        // Store the written data in input buffer for higher layers to process
        lock (_lock)
        {
            _inputBuffer.Enqueue(data);
        }

        // Simulate processing delay and check for injected response data
        _ = Task.Run(async () =>
        {
            // Simulate command processing delay
            await Task.Delay(25);

            // Check if there's any injected response data
            if (_injectDataQueue.TryDequeue(out string? response) && !string.IsNullOrEmpty(response))
            {
                // Add response to output buffer
                lock (_lock)
                {
                    _outputBuffer.Append(response);
                }

                // Trigger data received event
                try
                {
                    DataReceived?.Invoke(this, new SerialDataReceivedEventArgs(SerialData.Chars));
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
            while (_injectDataQueue.TryDequeue(out _)) { }
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

    #region Test Data Injection

    /// <summary>
    /// Injects test data that will be returned as responses to subsequent writes.
    /// This allows higher layers to control the simulation responses.
    /// </summary>
    /// <param name="data">Data to inject as a response</param>
    public void InjectResponseData(string data)
    {
        if (!string.IsNullOrEmpty(data))
        {
            _injectDataQueue.Enqueue(data);
        }
    }

    /// <summary>
    /// Retrieves data that was written to the port (for testing/verification).
    /// </summary>
    /// <returns>The next written command, or null if none available</returns>
    public string? GetWrittenData()
    {
        return _inputBuffer.TryDequeue(out string? data) ? data : null;
    }

    /// <summary>
    /// Clears all written data from the input buffer.
    /// </summary>
    public void ClearWrittenData()
    {
        lock (_lock)
        {
            while (_inputBuffer.TryDequeue(out _)) { }
        }
    }

    #endregion

    #region Events

    /// <summary>
    /// Event raised when simulated data is received
    /// </summary>
    public event EventHandler<SerialDataReceivedEventArgs>? DataReceived;

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