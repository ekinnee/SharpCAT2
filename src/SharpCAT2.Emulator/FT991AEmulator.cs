using System.Globalization;
using System.Text;
using System.Threading.Channels;
using SharpCAT2.Core.Serial;

namespace SharpCAT2.Emulator;

/// <summary>
/// Deterministic, in-memory FT-991A CAT device emulator. The model is intentionally
/// implemented independently of the production protocol profile and command parser.
/// </summary>
/// <remarks>
/// This class is one byte transport at a time. Closing and reopening it creates a new
/// connection generation while radio state remains in the emulator. There are no timers;
/// tests explicitly release held replies and inject unsolicited data when needed.
/// </remarks>
public sealed class FT991AEmulator : IByteTransport
{
    public const long MinimumFrequencyHz = 30_000;
    public const long MaximumFrequencyHz = 470_000_000;

    private const int MaximumCommandLength = 32;
    private readonly object _sync = new();
    private readonly List<EmulatorWrite> _writes = [];
    private readonly List<HeldReply> _heldReplies = [];
    private readonly Queue<byte[]> _replyOverrides = new();
    private TaskCompletionSource<bool> _writeChanged = NewSignal();

    private ConnectionGeneration? _connection;
    private int _generation;
    private long _nextWriteSequence;
    private long _frequencyAHz;
    private long _frequencyBHz;
    private char _mainRxModeCode;
    private bool _autoInformationEnabled;
    private bool _silenceReplies;
    private bool _suppressNextReply;
    private bool _holdNextReply;
    private int[]? _nextReplyFragmentLengths;
    private int _disposed;
    private int _readActive;

    public FT991AEmulator(
        long initialFrequencyAHz = 14_074_000,
        long initialFrequencyBHz = 7_074_000,
        char initialModeCode = '2')
    {
        ValidateFrequency(initialFrequencyAHz, nameof(initialFrequencyAHz));
        ValidateFrequency(initialFrequencyBHz, nameof(initialFrequencyBHz));
        if (!IsModeCode(initialModeCode))
        {
            throw new ArgumentOutOfRangeException(nameof(initialModeCode), "Mode must be one of 1-9 or A-E.");
        }

        _frequencyAHz = initialFrequencyAHz;
        _frequencyBHz = initialFrequencyBHz;
        _mainRxModeCode = initialModeCode;
    }

    public bool IsOpen
    {
        get
        {
            lock (_sync)
            {
                return Volatile.Read(ref _disposed) == 0 && _connection?.IsOpen == true;
            }
        }
    }

    /// <summary>The active connection generation, or the most recently opened generation.</summary>
    public int Generation
    {
        get
        {
            lock (_sync)
            {
                return _generation;
            }
        }
    }

    /// <summary>Current persistent radio state, copied atomically for test observations.</summary>
    public EmulatedRadioState State
    {
        get
        {
            lock (_sync)
            {
                return new EmulatedRadioState(
                    _frequencyAHz,
                    _frequencyBHz,
                    _mainRxModeCode,
                    _autoInformationEnabled);
            }
        }
    }

    /// <summary>Number of recorded writes across all connection generations.</summary>
    public int WriteCount
    {
        get
        {
            lock (_sync)
            {
                return _writes.Count;
            }
        }
    }

    /// <summary>Bytes queued for the active transport reader.</summary>
    public int PendingReadByteCount
    {
        get
        {
            lock (_sync)
            {
                return _connection is { IsOpen: true } connection
                    ? Volatile.Read(ref connection.PendingBytes)
                    : 0;
            }
        }
    }

    public int HeldReplyCount
    {
        get
        {
            lock (_sync)
            {
                return _heldReplies.Count;
            }
        }
    }

    /// <summary>Whether all normal and automatic replies are currently suppressed.</summary>
    public bool SilenceReplies
    {
        get
        {
            lock (_sync)
            {
                return _silenceReplies;
            }
        }
        set
        {
            lock (_sync)
            {
                _silenceReplies = value;
            }
        }
    }

    /// <summary>Snapshot of all writes, with independent payload copies.</summary>
    public IReadOnlyList<EmulatorWrite> Writes
    {
        get
        {
            lock (_sync)
            {
                return _writes.Select(write => write.Copy()).ToArray();
            }
        }
    }

    public ValueTask OpenAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            if (_connection?.IsOpen == true)
            {
                return ValueTask.CompletedTask;
            }

            _generation++;
            _connection = new ConnectionGeneration(_generation);
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>Completes the active byte stream; a later open gets a fresh generation.</summary>
    public ValueTask CloseAsync(CancellationToken cancellationToken = default)
    {
        CloseCurrentGeneration();
        return ValueTask.CompletedTask;
    }

    /// <summary>Disconnects the active connection and makes its reader observe EOF.</summary>
    public void Disconnect() => CloseCurrentGeneration();

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
            throw new InvalidOperationException("Only one emulator transport read may be active.");
        }

        try
        {
            ConnectionGeneration? generation;
            lock (_sync)
            {
                generation = _connection is { IsOpen: true } current ? current : null;
            }

            if (generation is null)
            {
                return 0;
            }

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (generation.TryCopyPending(buffer, out var copied))
                {
                    Interlocked.Add(ref generation.PendingBytes, -copied);
                    return copied;
                }

                if (!await generation.Inbound.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return 0;
                }

                if (generation.Inbound.Reader.TryRead(out var next))
                {
                    generation.SetPending(next);
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

        TaskCompletionSource<bool> signal;
        lock (_sync)
        {
            var connection = _connection;
            if (connection?.IsOpen != true)
            {
                throw new IOException("The emulator transport is not open.");
            }

            var write = new EmulatorWrite(++_nextWriteSequence, connection.Generation, buffer.ToArray());
            _writes.Add(write);
            signal = _writeChanged;
            _writeChanged = NewSignal();

            foreach (var value in buffer.Span)
            {
                ProcessIncomingByte(connection, value);
            }
        }

        signal.TrySetResult(true);
        return ValueTask.CompletedTask;
    }

    /// <summary>Waits for the one-based cumulative write count and returns that prefix snapshot.</summary>
    public async ValueTask<IReadOnlyList<EmulatorWrite>> WaitForWriteAsync(
        int count,
        CancellationToken cancellationToken = default)
    {
        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Write count is one-based and must be positive.");
        }

        while (true)
        {
            Task waitTask;
            lock (_sync)
            {
                if (_writes.Count >= count)
                {
                    return _writes.Take(count).Select(write => write.Copy()).ToArray();
                }

                waitTask = _writeChanged.Task;
            }

            await waitTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Sets persistent radio state as if changed outside CAT control.</summary>
    public void SetExternalFrequencies(long frequencyAHz, long frequencyBHz)
    {
        ValidateFrequency(frequencyAHz, nameof(frequencyAHz));
        ValidateFrequency(frequencyBHz, nameof(frequencyBHz));
        lock (_sync)
        {
            _frequencyAHz = frequencyAHz;
            _frequencyBHz = frequencyBHz;
        }
    }

    /// <summary>Holds the next reply until <see cref="ReleaseHeldRepliesAsync"/> is called.</summary>
    public void HoldNextReply()
    {
        lock (_sync)
        {
            _holdNextReply = true;
        }
    }

    /// <summary>Suppresses the next generated reply without affecting later replies.</summary>
    public void SuppressNextReply()
    {
        lock (_sync)
        {
            _suppressNextReply = true;
        }
    }

    /// <summary>Queues a raw reply override for the next response-bearing command.</summary>
    public void OverrideNextReply(ReadOnlyMemory<byte> reply)
    {
        if (reply.IsEmpty)
        {
            throw new ArgumentException("Reply override must not be empty; use suppression for silence.", nameof(reply));
        }

        lock (_sync)
        {
            _replyOverrides.Enqueue(reply.ToArray());
        }
    }

    /// <summary>Fragments the next generated reply according to these byte counts.</summary>
    public void FragmentNextReply(params int[] fragmentLengths)
    {
        ArgumentNullException.ThrowIfNull(fragmentLengths);
        ValidateFragmentLengths(fragmentLengths);
        lock (_sync)
        {
            _nextReplyFragmentLengths = (int[])fragmentLengths.Clone();
        }
    }

    /// <summary>Releases all held replies still belonging to the current connection generation.</summary>
    public ValueTask<int> ReleaseHeldRepliesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            var connection = _connection;
            if (connection?.IsOpen != true)
            {
                _heldReplies.Clear();
                return ValueTask.FromResult(0);
            }

            var released = 0;
            foreach (var held in _heldReplies.ToArray())
            {
                _heldReplies.Remove(held);
                if (held.Generation != connection.Generation)
                {
                    continue;
                }

                EnqueueBytes(connection, held.Payload, held.FragmentLengths);
                released++;
            }

            return ValueTask.FromResult(released);
        }
    }

    /// <summary>Injects bytes as unsolicited device output on the active connection.</summary>
    public ValueTask InjectUnsolicitedAsync(
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        return InjectUnsolicitedAsync(payload, ReadOnlyMemory<int>.Empty, cancellationToken);
    }

    /// <summary>Injects unsolicited output using deterministic byte fragment sizes.</summary>
    public ValueTask InjectUnsolicitedAsync(
        ReadOnlyMemory<byte> payload,
        ReadOnlyMemory<int> fragmentLengths,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        if (payload.IsEmpty)
        {
            throw new ArgumentException("Unsolicited payload must not be empty.", nameof(payload));
        }

        int[] fragments = fragmentLengths.IsEmpty ? Array.Empty<int>() : fragmentLengths.ToArray();
        ValidateFragmentLengths(fragments);
        lock (_sync)
        {
            var connection = _connection;
            if (connection?.IsOpen != true)
            {
                throw new IOException("The emulator transport is not open.");
            }

            EnqueueBytes(connection, payload.ToArray(), fragments);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            CloseCurrentGeneration();
        }

        return ValueTask.CompletedTask;
    }

    private void ProcessIncomingByte(ConnectionGeneration connection, byte value)
    {
        if (connection.DiscardUntilTerminator)
        {
            if (value == (byte)';')
            {
                connection.DiscardUntilTerminator = false;
            }

            return;
        }

        if (value > 0x7f || (value < 0x20 && value != (byte)';'))
        {
            connection.CommandBuffer.Clear();
            connection.DiscardUntilTerminator = true;
            return;
        }

        if (value == (byte)';')
        {
            var command = connection.CommandBuffer.ToString() + ";";
            connection.CommandBuffer.Clear();
            ProcessCommand(connection, command);
            return;
        }

        if (connection.CommandBuffer.Length >= MaximumCommandLength)
        {
            connection.CommandBuffer.Clear();
            connection.DiscardUntilTerminator = true;
            return;
        }

        connection.CommandBuffer.Append((char)value);
    }

    private void ProcessCommand(ConnectionGeneration connection, string command)
    {
        if (!connection.IsOpen || _connection != connection)
        {
            return;
        }

        var upper = command.ToUpperInvariant();
        if (upper == "ID;")
        {
            QueueReply(connection, "ID0670;");
            return;
        }

        if (upper == "AI;")
        {
            QueueReply(connection, _autoInformationEnabled ? "AI1;" : "AI0;");
            return;
        }

        if (upper is "AI0;" or "AI1;")
        {
            _autoInformationEnabled = upper[2] == '1';
            return;
        }

        if (upper == "FA;")
        {
            QueueReply(connection, FormatFrequency("FA", _frequencyAHz));
            return;
        }

        if (upper == "FB;")
        {
            QueueReply(connection, FormatFrequency("FB", _frequencyBHz));
            return;
        }

        if (TryParseFrequencySet(upper, "FA", out var frequencyAHz))
        {
            if (IsFrequencyInRange(frequencyAHz))
            {
                _frequencyAHz = frequencyAHz;
                if (_autoInformationEnabled)
                {
                    QueueReply(connection, FormatFrequency("FA", _frequencyAHz));
                }
            }

            return;
        }

        if (TryParseFrequencySet(upper, "FB", out var frequencyBHz))
        {
            if (IsFrequencyInRange(frequencyBHz))
            {
                _frequencyBHz = frequencyBHz;
                if (_autoInformationEnabled)
                {
                    QueueReply(connection, FormatFrequency("FB", _frequencyBHz));
                }
            }

            return;
        }

        if (upper == "MD0;")
        {
            QueueReply(connection, $"MD0{_mainRxModeCode};");
            return;
        }

        if (upper.Length == 5 && upper.StartsWith("MD0", StringComparison.Ordinal) &&
            upper[4] == ';' && IsModeCode(upper[3]))
        {
            _mainRxModeCode = upper[3];
            if (_autoInformationEnabled)
            {
                QueueReply(connection, $"MD0{_mainRxModeCode};");
            }

            return;
        }

        if (upper == "SV;")
        {
            (_frequencyAHz, _frequencyBHz) = (_frequencyBHz, _frequencyAHz);
        }
    }

    private void QueueReply(ConnectionGeneration connection, string response)
    {
        if (_silenceReplies || _suppressNextReply)
        {
            _suppressNextReply = false;
            return;
        }

        var payload = _replyOverrides.Count > 0
            ? _replyOverrides.Dequeue()
            : Encoding.ASCII.GetBytes(response);
        var fragments = _nextReplyFragmentLengths ?? [];
        _nextReplyFragmentLengths = null;

        if (_holdNextReply)
        {
            _holdNextReply = false;
            _heldReplies.Add(new HeldReply(connection.Generation, payload, fragments));
            return;
        }

        EnqueueBytes(connection, payload, fragments);
    }

    private static void EnqueueBytes(ConnectionGeneration connection, byte[] payload, int[] fragmentLengths)
    {
        if (!connection.IsOpen || payload.Length == 0)
        {
            return;
        }

        var offset = 0;
        foreach (var fragmentLength in fragmentLengths)
        {
            if (offset >= payload.Length)
            {
                break;
            }

            var count = Math.Min(fragmentLength, payload.Length - offset);
            var fragment = new byte[count];
            Buffer.BlockCopy(payload, offset, fragment, 0, count);
            Interlocked.Add(ref connection.PendingBytes, count);
            if (!connection.Inbound.Writer.TryWrite(fragment))
            {
                Interlocked.Add(ref connection.PendingBytes, -count);
            }

            offset += count;
        }

        if (offset < payload.Length)
        {
            var remainder = new byte[payload.Length - offset];
            Buffer.BlockCopy(payload, offset, remainder, 0, remainder.Length);
            Interlocked.Add(ref connection.PendingBytes, remainder.Length);
            if (!connection.Inbound.Writer.TryWrite(remainder))
            {
                Interlocked.Add(ref connection.PendingBytes, -remainder.Length);
            }
        }
    }

    private void CloseCurrentGeneration()
    {
        lock (_sync)
        {
            var connection = _connection;
            if (connection is null || !connection.IsOpen)
            {
                return;
            }

            connection.IsOpen = false;
            connection.CommandBuffer.Clear();
            connection.DiscardUntilTerminator = false;
            connection.Inbound.Writer.TryComplete();
            _heldReplies.RemoveAll(held => held.Generation == connection.Generation);
            _connection = null;
        }
    }

    private static bool TryParseFrequencySet(string command, string opcode, out long frequencyHz)
    {
        frequencyHz = 0;
        if (command.Length != 12 || !command.StartsWith(opcode, StringComparison.Ordinal) || command[^1] != ';')
        {
            return false;
        }

        var parameter = command.AsSpan(2, 9);
        foreach (var digit in parameter)
        {
            if (digit is < '0' or > '9')
                return false;
        }

        return long.TryParse(parameter, NumberStyles.None, CultureInfo.InvariantCulture, out frequencyHz);
    }

    private static string FormatFrequency(string opcode, long frequencyHz) =>
        string.Create(CultureInfo.InvariantCulture, $"{opcode}{frequencyHz:D9};");

    private static void ValidateFrequency(long frequencyHz, string parameterName)
    {
        if (!IsFrequencyInRange(frequencyHz))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                frequencyHz,
                $"Frequency must be between {MinimumFrequencyHz} and {MaximumFrequencyHz} Hz.");
        }
    }

    private static bool IsFrequencyInRange(long frequencyHz) =>
        frequencyHz is >= MinimumFrequencyHz and <= MaximumFrequencyHz;

    private static bool IsModeCode(char value) =>
        value is >= '1' and <= '9' or >= 'A' and <= 'E';

    private static void ValidateFragmentLengths(ReadOnlySpan<int> fragmentLengths)
    {
        foreach (var length in fragmentLengths)
        {
            if (length <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fragmentLengths), "Fragment lengths must be positive.");
            }
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
    }

    private static TaskCompletionSource<bool> NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed record HeldReply(int Generation, byte[] Payload, int[] FragmentLengths);

    private sealed class ConnectionGeneration(int generation)
    {
        public int Generation { get; } = generation;
        public Channel<byte[]> Inbound { get; } = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
        public StringBuilder CommandBuffer { get; } = new();
        public bool DiscardUntilTerminator { get; set; }
        public bool IsOpen { get; set; } = true;
        public int PendingBytes;

        private byte[]? _pending;
        private int _pendingOffset;

        public void SetPending(byte[] bytes)
        {
            _pending = bytes;
            _pendingOffset = 0;
        }

        public bool TryCopyPending(Memory<byte> destination, out int copied)
        {
            copied = 0;
            if (_pending is null)
            {
                return false;
            }

            copied = Math.Min(destination.Length, _pending.Length - _pendingOffset);
            _pending.AsMemory(_pendingOffset, copied).CopyTo(destination);
            _pendingOffset += copied;
            if (_pendingOffset == _pending.Length)
            {
                _pending = null;
                _pendingOffset = 0;
            }

            return copied > 0;
        }
    }
}

/// <summary>Immutable observation of one IByteTransport.WriteAsync invocation.</summary>
public sealed class EmulatorWrite
{
    private readonly byte[] _payload;

    internal EmulatorWrite(long sequence, int generation, byte[] payload)
    {
        Sequence = sequence;
        Generation = generation;
        _payload = (byte[])payload.Clone();
    }

    public long Sequence { get; }
    public int Generation { get; }
    public byte[] Payload => (byte[])_payload.Clone();

    internal EmulatorWrite Copy() => new(Sequence, Generation, _payload);
}

/// <summary>Snapshot of the modeled radio's persistent CAT-visible state.</summary>
public sealed record EmulatedRadioState(
    long FrequencyAHz,
    long FrequencyBHz,
    char MainRxModeCode,
    bool AutoInformationEnabled);
