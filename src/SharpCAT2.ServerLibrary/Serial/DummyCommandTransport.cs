using System.Text;
using System.Threading.Channels;
using SharpCAT2.Core.Serial;

namespace SharpCAT2.ServerLibrary.Serial;

/// <summary>Existing dummy command engine behind the same session transport seam.
/// This is demo behavior, not the independent FT-991A emulator from Phase 3.</summary>
internal sealed class DummyCommandTransport(ISerialPort port, Func<string, string> process) : IByteTransport
{
    private Channel<byte[]> _responses = NewChannel();
    private byte[]? _pending;
    private int _offset;
    private bool _disposed;
    private static Channel<byte[]> NewChannel() => Channel.CreateBounded<byte[]>(new BoundedChannelOptions(64)
        { SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait });
    public bool IsOpen => port.IsOpen;

    public ValueTask OpenAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);
        _responses = NewChannel();
        _pending = null;
        _offset = 0;
        port.Open();
        return ValueTask.CompletedTask;
    }

    public ValueTask CloseAsync(CancellationToken cancellationToken = default)
    {
        _responses.Writer.TryComplete();
        port.Close();
        return ValueTask.CompletedTask;
    }

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (buffer.IsEmpty) throw new ArgumentException("A read buffer is required.", nameof(buffer));
        if (_pending is null)
        {
            try { _pending = await _responses.Reader.ReadAsync(cancellationToken); }
            catch (ChannelClosedException) { return 0; }
            _offset = 0;
        }
        var count = Math.Min(buffer.Length, _pending.Length - _offset);
        _pending.AsMemory(_offset, count).CopyTo(buffer);
        _offset += count;
        if (_offset == _pending.Length) _pending = null;
        return count;
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsOpen) throw new IOException("Dummy transport is closed.");
        var response = process(Encoding.ASCII.GetString(buffer.Span));
        if (!string.IsNullOrEmpty(response))
            await _responses.Writer.WriteAsync(Encoding.ASCII.GetBytes(response), cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await CloseAsync();
        port.Dispose();
    }
}
