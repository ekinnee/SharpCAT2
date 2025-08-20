using System.IO.Ports;
using SharpCAT2.Core.Serial;

namespace SharpCAT2.ServerLibrary.Serial;

/// <summary>
/// Real serial port implementation that wraps System.IO.Ports.SerialPort.
/// Provides actual hardware serial communication capabilities.
/// </summary>
public class RealSerialPort : ISerialPort
{
    private readonly SerialPort _serialPort;
    private bool _disposed = false;

    #region Constructors

    /// <summary>
    /// Initializes a new instance of RealSerialPort with an existing SerialPort
    /// </summary>
    /// <param name="serialPort">The SerialPort to wrap</param>
    public RealSerialPort(SerialPort serialPort)
    {
        _serialPort = serialPort ?? throw new ArgumentNullException(nameof(serialPort));
        
        // Forward the data received event
        _serialPort.DataReceived += (sender, e) => DataReceived?.Invoke(sender, e);
    }

    /// <summary>
    /// Initializes a new instance of RealSerialPort with the specified parameters
    /// </summary>
    /// <param name="portName">The port name (e.g., COM1, /dev/ttyUSB0)</param>
    /// <param name="baudRate">The baud rate</param>
    /// <param name="parity">The parity scheme</param>
    /// <param name="dataBits">The number of data bits</param>
    /// <param name="stopBits">The stop bits</param>
    public RealSerialPort(string portName, int baudRate, Parity parity = Parity.None, 
                         int dataBits = 8, StopBits stopBits = StopBits.One)
    {
        _serialPort = new SerialPort(portName, baudRate, parity, dataBits, stopBits)
        {
            Handshake = Handshake.None,
            ReadTimeout = 500,
            WriteTimeout = 500
        };
        
        // Forward the data received event
        _serialPort.DataReceived += (sender, e) => DataReceived?.Invoke(sender, e);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets whether the serial port is currently open
    /// </summary>
    public bool IsOpen => _serialPort.IsOpen;

    /// <summary>
    /// Gets the port name
    /// </summary>
    public string PortName => _serialPort.PortName;

    /// <summary>
    /// Gets the baud rate
    /// </summary>
    public int BaudRate => _serialPort.BaudRate;

    /// <summary>
    /// Gets the number of bytes waiting to be read
    /// </summary>
    public int BytesToRead => _serialPort.BytesToRead;

    /// <summary>
    /// Gets or sets the read timeout in milliseconds
    /// </summary>
    public int ReadTimeout 
    { 
        get => _serialPort.ReadTimeout; 
        set => _serialPort.ReadTimeout = value; 
    }

    /// <summary>
    /// Gets or sets the write timeout in milliseconds
    /// </summary>
    public int WriteTimeout 
    { 
        get => _serialPort.WriteTimeout; 
        set => _serialPort.WriteTimeout = value; 
    }

    #endregion

    #region Connection Management

    /// <summary>
    /// Opens the serial port for communication
    /// </summary>
    public void Open()
    {
        if (!_serialPort.IsOpen)
        {
            _serialPort.Open();
        }
    }

    /// <summary>
    /// Closes the serial port
    /// </summary>
    public void Close()
    {
        if (_serialPort.IsOpen)
        {
            _serialPort.Close();
        }
    }

    #endregion

    #region Data Communication

    /// <summary>
    /// Writes data to the serial port
    /// </summary>
    /// <param name="data">Data to write</param>
    public void Write(string data)
    {
        _serialPort.Write(data);
    }

    /// <summary>
    /// Writes a line of data to the serial port
    /// </summary>
    /// <param name="data">Data to write</param>
    public void WriteLine(string data)
    {
        _serialPort.WriteLine(data);
    }

    /// <summary>
    /// Reads available data from the serial port
    /// </summary>
    /// <returns>Data read from the port</returns>
    public string ReadExisting()
    {
        return _serialPort.ReadExisting();
    }

    /// <summary>
    /// Reads a line from the serial port
    /// </summary>
    /// <returns>Line read from port</returns>
    public string ReadLine()
    {
        return _serialPort.ReadLine();
    }

    /// <summary>
    /// Reads data from the serial port into a buffer
    /// </summary>
    /// <param name="buffer">Buffer to read into</param>
    /// <param name="offset">Offset in buffer to start writing</param>
    /// <param name="count">Maximum number of bytes to read</param>
    /// <returns>Number of bytes actually read</returns>
    public int Read(byte[] buffer, int offset, int count)
    {
        return _serialPort.Read(buffer, offset, count);
    }

    #endregion

    #region Buffer Management

    /// <summary>
    /// Discards data from the input buffer
    /// </summary>
    public void DiscardInBuffer()
    {
        _serialPort.DiscardInBuffer();
    }

    /// <summary>
    /// Discards data from the output buffer
    /// </summary>
    public void DiscardOutBuffer()
    {
        _serialPort.DiscardOutBuffer();
    }

    #endregion

    #region Events

    /// <summary>
    /// Event raised when data is received on the serial port
    /// </summary>
    public event SerialDataReceivedEventHandler? DataReceived;

    #endregion

    #region Disposal

    /// <summary>
    /// Disposes the real serial port
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            Close();
            _serialPort?.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    #endregion
}