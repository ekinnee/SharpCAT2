using System.Text;
using System.Threading.Channels;
using SharpCAT2.Core.Serial;

namespace SharpCAT2.Tests.Radio.Session;

internal sealed class ControlledByteTransport : IByteTransport
{
    private Channel<byte[]> _input = Channel.CreateUnbounded<byte[]>();
    private readonly Channel<byte[]> _writes = Channel.CreateUnbounded<byte[]>();
    private readonly Channel<int> _readProgress = Channel.CreateUnbounded<int>();
    private int _readBytes;
    private readonly object _gate = new();
    private readonly List<string> _written = [];
    private byte[]? _tail;
    private int _tailOffset;
    private int _readers;
    public int MaximumReaders { get; private set; }
    public bool IsOpen { get; private set; }
    public int OpenCount { get; private set; }
    public int CloseCount { get; private set; }
    public int DisposeCount { get; private set; }
    public bool FailOpen { get; set; }
    public bool AutoSynchronize { get; set; } = true;
    public bool IgnoreOpenCancellation { get; set; }
    public TaskCompletionSource? OpenGate { get; set; }
    public TaskCompletionSource? CloseGate { get; set; }
    public TaskCompletionSource OpenEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Func<byte[], CancellationToken, ValueTask>? OnWrite { get; set; }
    public IReadOnlyList<string> Written { get { lock (_gate) return _written.ToArray(); } }

    public async ValueTask OpenAsync(CancellationToken cancellationToken = default)
    {
        OpenCount++;
        OpenEntered.TrySetResult();
        if (OpenGate is not null) await OpenGate.Task.WaitAsync(IgnoreOpenCancellation ? CancellationToken.None : cancellationToken);
        if (!IgnoreOpenCancellation) cancellationToken.ThrowIfCancellationRequested();
        if (FailOpen) throw new IOException("Controlled startup failure.");
        _input = Channel.CreateUnbounded<byte[]>();
        _tail = null;
        _tailOffset = 0;
        IsOpen = true;
    }

    public async ValueTask CloseAsync(CancellationToken cancellationToken = default)
    {
        CloseCount++;
        IsOpen = false;
        _input.Writer.TryComplete();
        if (CloseGate is not null) await CloseGate.Task.WaitAsync(cancellationToken);
    }

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (buffer.IsEmpty) throw new ArgumentException("Empty read buffer.");
        var readers = Interlocked.Increment(ref _readers);
        MaximumReaders = Math.Max(MaximumReaders, readers);
        var input = _input;
        try
        {
            if (_tail is null)
            {
                try { _tail = await input.Reader.ReadAsync(cancellationToken); }
                catch (ChannelClosedException) { return 0; }
                _tailOffset = 0;
            }
            var count = Math.Min(buffer.Length, _tail.Length - _tailOffset);
            _tail.AsMemory(_tailOffset, count).CopyTo(buffer);
            _tailOffset += count;
            if (_tailOffset == _tail.Length) _tail = null;
            _readProgress.Writer.TryWrite(Interlocked.Add(ref _readBytes, count));
            return count;
        }
        finally { Interlocked.Decrement(ref _readers); }
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var bytes = buffer.ToArray();
        var text = Encoding.ASCII.GetString(bytes);
        lock (_gate) _written.Add(text);
        _writes.Writer.TryWrite(bytes);
        if (text == "S;" && AutoSynchronize) Send("S1;");
        else if (OnWrite is not null) await OnWrite(bytes, cancellationToken);
    }

    public void Send(string text) => _input.Writer.TryWrite(Encoding.ASCII.GetBytes(text));
    public void End() => _input.Writer.TryComplete();
    public async Task WaitForReadBytesAsync(int minimum)
    {
        while (Volatile.Read(ref _readBytes) < minimum)
            await _readProgress.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
    }
    public async Task<string> NextWriteAsync() => Encoding.ASCII.GetString(
        await _writes.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3)));

    public ValueTask DisposeAsync()
    {
        DisposeCount++;
        IsOpen = false;
        _input.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}
