using System.IO.Ports;
using Microsoft.Extensions.Logging;
using SharpCAT2.Core.Serial;
using SharpCAT2.Core.Utils;

namespace SharpCAT2.ServerLibrary.Serial;

/// <summary>
/// Source-compatible passive wrapper for the legacy serial-port abstraction.
/// It forwards each operation once; it does not retry, reconnect, or monitor health.
/// </summary>
/// <remarks>
/// The supplied port is owned and disposed by this wrapper. The compatibility
/// settings remain readable and writable but are inert. Connection lifecycle and
/// recovery belong to the single session owner. Connection state events are kept
/// for source compatibility and are not raised by this passive wrapper.
/// </remarks>
public class ResilientSerialPort : ISerialPort
{
    private readonly ISerialPort _innerPort;
    private bool _disposed;
    private RetryPolicy _retryPolicy = RetryPolicy.Serial;
    private bool _autoReconnectEnabled = true;
    private TimeSpan _reconnectionInterval = TimeSpan.FromSeconds(5);
    private EventHandler<ConnectionLostEventArgs>? _connectionLost;
    private EventHandler<ConnectionRestoredEventArgs>? _connectionRestored;

    /// <summary>Legacy compatibility setting. Retries are disabled regardless of this value.</summary>
    public RetryPolicy RetryPolicy
    {
        get => _retryPolicy;
        set => _retryPolicy = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>Legacy compatibility setting. Automatic reconnect is disabled regardless of this value.</summary>
    public bool AutoReconnectEnabled
    {
        get => _autoReconnectEnabled;
        set => _autoReconnectEnabled = value;
    }

    /// <summary>Legacy compatibility setting. No reconnection timer uses this value.</summary>
    public TimeSpan ReconnectionInterval
    {
        get => _reconnectionInterval;
        set => _reconnectionInterval = value;
    }

    /// <summary>Retained for source compatibility; this passive wrapper never raises it.</summary>
    public event EventHandler<ConnectionLostEventArgs>? ConnectionLost
    {
        add => _connectionLost += value;
        remove => _connectionLost -= value;
    }

    /// <summary>Retained for source compatibility; this passive wrapper never raises it.</summary>
    public event EventHandler<ConnectionRestoredEventArgs>? ConnectionRestored
    {
        add => _connectionRestored += value;
        remove => _connectionRestored -= value;
    }

    public ResilientSerialPort(ISerialPort innerPort, ILogger? logger = null)
    {
        _innerPort = innerPort ?? throw new ArgumentNullException(nameof(innerPort));
        _ = logger; // Kept in the signature for source compatibility.
        _innerPort.DataReceived += OnInnerPortDataReceived;
    }

    public bool IsOpen => _innerPort.IsOpen;
    /// <summary>Gets the wrapped legacy port for compatibility inspection.</summary>
    public ISerialPort InnerPort => _innerPort;
    public string PortName => _innerPort.PortName;
    public int BaudRate => _innerPort.BaudRate;
    public int BytesToRead => _innerPort.BytesToRead;

    public int ReadTimeout
    {
        get => _innerPort.ReadTimeout;
        set => _innerPort.ReadTimeout = value;
    }

    public int WriteTimeout
    {
        get => _innerPort.WriteTimeout;
        set => _innerPort.WriteTimeout = value;
    }

    public event SerialDataReceivedEventHandler? DataReceived;

    public void Open() => _innerPort.Open();
    public void Close() => _innerPort.Close();
    public void Write(string data) => _innerPort.Write(data);
    public void WriteLine(string data) => _innerPort.WriteLine(data);
    public string ReadExisting() => _innerPort.ReadExisting();
    public string ReadLine() => _innerPort.ReadLine();
    public int Read(byte[] buffer, int offset, int count) => _innerPort.Read(buffer, offset, count);
    public void DiscardInBuffer() => _innerPort.DiscardInBuffer();
    public void DiscardOutBuffer() => _innerPort.DiscardOutBuffer();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            _innerPort.DataReceived -= OnInnerPortDataReceived;
        }
        finally
        {
            _innerPort.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    private void OnInnerPortDataReceived(object sender, SerialDataReceivedEventArgs e) =>
        DataReceived?.Invoke(sender, e);
}

/// <summary>Arguments retained for legacy connection state event subscribers.</summary>
public class ConnectionLostEventArgs : EventArgs
{
    public string PortName { get; }
    public string Reason { get; }
    public DateTime Timestamp { get; }

    public ConnectionLostEventArgs(string portName, string reason)
    {
        PortName = portName;
        Reason = reason;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>Arguments retained for legacy connection state event subscribers.</summary>
public class ConnectionRestoredEventArgs : EventArgs
{
    public string PortName { get; }
    public DateTime Timestamp { get; }

    public ConnectionRestoredEventArgs(string portName)
    {
        PortName = portName;
        Timestamp = DateTime.UtcNow;
    }
}
