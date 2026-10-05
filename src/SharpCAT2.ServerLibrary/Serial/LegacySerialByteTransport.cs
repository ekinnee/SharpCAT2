using System.Buffers;
using System.Text;
using SharpCAT2.Core.Serial;

namespace SharpCAT2.ServerLibrary.Serial;

/// <summary>
/// Adapts an owned legacy <see cref="ISerialPort"/> to the byte transport contract.
/// </summary>
/// <remarks>
/// This adapter is intended for the single session owner. RealSerialPort uses native
/// byte writes. Other legacy ports can only write ASCII bytes because their public
/// write surface accepts strings; bytes above 0x7f are rejected without substitution.
/// Reads poll BytesToRead and use one synchronous bounded read at a time. The adapter
/// clamps read/write timeouts to at most 500 ms, but third-party ports that
/// ignore BytesToRead, timeouts, or Close-to-unblock cannot guarantee bounded shutdown.
/// No DataReceived handler or per-read worker is used.
/// </remarks>
public sealed class LegacySerialByteTransport : IByteTransport
{
    private const int DefaultTimeoutMilliseconds = 500;
    private const int MaximumReadChunkBytes = 4096;
    private static readonly TimeSpan IdlePollInterval = TimeSpan.FromMilliseconds(10);

    private readonly ISerialPort _port;
    private int _disposed;
    private int _readActive;

/// <summary>
/// Creates an adapter and transfers ownership of <paramref name="port"/> to it if
/// construction succeeds. If a timeout setter fails during construction, the caller
/// retains responsibility for disposing the port.
/// </summary>
    public LegacySerialByteTransport(ISerialPort port)
    {
        _port = port ?? throw new ArgumentNullException(nameof(port));
        EnsureBoundedTimeout(readTimeout: true);
        EnsureBoundedTimeout(readTimeout: false);
    }

    public bool IsOpen => Volatile.Read(ref _disposed) == 0 && _port.IsOpen;

    public ValueTask OpenAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        _port.Open();
        return ValueTask.CompletedTask;
    }

    /// <summary>Closes synchronously so it can unblock an outstanding legacy read.</summary>
    public ValueTask CloseAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _disposed) == 0 && _port.IsOpen)
        {
            _port.Close();
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (buffer.IsEmpty)
        {
            throw new ArgumentException("Read buffer must not be empty.", nameof(buffer));
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (Interlocked.CompareExchange(ref _readActive, 1, 0) != 0)
        {
            throw new InvalidOperationException("Only one read may be active on a serial transport.");
        }

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Volatile.Read(ref _disposed) != 0 || !_port.IsOpen)
                {
                    return 0;
                }

                int available;
                try
                {
                    available = _port.BytesToRead;
                }
                catch when (Volatile.Read(ref _disposed) != 0 || !_port.IsOpen)
                {
                    return 0;
                }

                if (available <= 0)
                {
                    await Task.Delay(IdlePollInterval, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var readCount = Math.Min(Math.Min(available, buffer.Length), MaximumReadChunkBytes);
                var rented = ArrayPool<byte>.Shared.Rent(readCount);
                try
                {
                    int count;
                    try
                    {
                        count = _port.Read(rented, 0, readCount);
                    }
                    catch (TimeoutException) when (!cancellationToken.IsCancellationRequested)
                    {
                        continue;
                    }
                    catch when (Volatile.Read(ref _disposed) != 0 || !_port.IsOpen)
                    {
                        return 0;
                    }

                    if (count < 0 || count > readCount)
                    {
                        throw new IOException("Legacy serial port returned a read count outside the requested buffer range.");
                    }

                    if (count == 0)
                    {
                        if (Volatile.Read(ref _disposed) != 0 || !_port.IsOpen)
                        {
                            return 0;
                        }

                        await Task.Delay(IdlePollInterval, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    rented.AsMemory(0, count).CopyTo(buffer);
                    return count;
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(rented);
                }
            }
        }
        finally
        {
            Volatile.Write(ref _readActive, 0);
        }
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        if (buffer.IsEmpty)
        {
            return ValueTask.CompletedTask;
        }

        if (GetRealSerialPort(_port) is { } realPort)
        {
            realPort.WriteBytes(buffer);
            return ValueTask.CompletedTask;
        }

        foreach (var value in buffer.Span)
        {
            if (value > 0x7f)
            {
                throw new NotSupportedException(
                    "This legacy serial port accepts strings only; byte values above ASCII 0x7f cannot be written losslessly.");
            }
        }

        _port.Write(Encoding.ASCII.GetString(buffer.Span));
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return ValueTask.CompletedTask;
        }

        try
        {
            if (_port.IsOpen)
            {
                _port.Close();
            }
        }
        finally
        {
            _port.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    private void EnsureBoundedTimeout(bool readTimeout)
    {
        var timeout = readTimeout ? _port.ReadTimeout : _port.WriteTimeout;
        if (timeout <= 0 || timeout > DefaultTimeoutMilliseconds)
        {
            if (readTimeout)
            {
                _port.ReadTimeout = DefaultTimeoutMilliseconds;
            }
            else
            {
                _port.WriteTimeout = DefaultTimeoutMilliseconds;
            }
        }
    }

    private static RealSerialPort? GetRealSerialPort(ISerialPort port)
    {
        while (port is ResilientSerialPort wrapper)
        {
            port = wrapper.InnerPort;
        }

        return port as RealSerialPort;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
    }
}
