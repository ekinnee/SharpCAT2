using System.IO.Ports;

namespace SharpCAT2.Core.Serial;

/// <summary>
/// Interface for serial port communication abstraction.
/// Provides a testable abstraction over System.IO.Ports.SerialPort
/// and enables simulation for testing and development.
/// </summary>
public interface ISerialPort : IDisposable
{
    #region Properties

    /// <summary>
    /// Gets whether the serial port is currently open
    /// </summary>
    bool IsOpen { get; }

    /// <summary>
    /// Gets the port name (e.g., COM1, /dev/ttyUSB0)
    /// </summary>
    string PortName { get; }

    /// <summary>
    /// Gets the baud rate for communication
    /// </summary>
    int BaudRate { get; }

    /// <summary>
    /// Gets the number of bytes waiting to be read
    /// </summary>
    int BytesToRead { get; }

    /// <summary>
    /// Gets or sets the read timeout in milliseconds
    /// </summary>
    int ReadTimeout { get; set; }

    /// <summary>
    /// Gets or sets the write timeout in milliseconds
    /// </summary>
    int WriteTimeout { get; set; }

    #endregion

    #region Connection Management

    /// <summary>
    /// Opens the serial port for communication
    /// </summary>
    void Open();

    /// <summary>
    /// Closes the serial port
    /// </summary>
    void Close();

    #endregion

    #region Data Communication

    /// <summary>
    /// Writes data to the serial port
    /// </summary>
    /// <param name="data">Data to write</param>
    void Write(string data);

    /// <summary>
    /// Writes a line of data to the serial port (adds line terminator)
    /// </summary>
    /// <param name="data">Data to write</param>
    void WriteLine(string data);

    /// <summary>
    /// Reads existing data from the serial port
    /// </summary>
    /// <returns>Available data as string</returns>
    string ReadExisting();

    /// <summary>
    /// Reads a line from the serial port
    /// </summary>
    /// <returns>Line read from port</returns>
    string ReadLine();

    /// <summary>
    /// Reads data from the serial port into a buffer
    /// </summary>
    /// <param name="buffer">Buffer to read into</param>
    /// <param name="offset">Offset in buffer to start writing</param>
    /// <param name="count">Maximum number of bytes to read</param>
    /// <returns>Number of bytes actually read</returns>
    int Read(byte[] buffer, int offset, int count);

    /// <summary>
    /// Discards data from the input buffer
    /// </summary>
    void DiscardInBuffer();

    /// <summary>
    /// Discards data from the output buffer
    /// </summary>
    void DiscardOutBuffer();

    #endregion

    #region Events

    /// <summary>
    /// Event raised when data is received on the serial port
    /// </summary>
    event SerialDataReceivedEventHandler? DataReceived;

    #endregion
}