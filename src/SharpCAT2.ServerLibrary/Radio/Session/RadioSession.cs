using SharpCAT2.Core.Radio.Contracts;
using SharpCAT2.Core.Serial;
using SharpCAT2.ServerLibrary.Radio.Protocols;

namespace SharpCAT2.ServerLibrary.Radio.Session;

/// <summary>Exclusive owner of a transferred transport, one reader and one transaction worker.</summary>
/// <remarks>No operation is replayed. Failed connections require explicit ConnectAsync with a
/// profile synchronization command. Closing and synchronizing cannot universally distinguish
/// arbitrarily delayed, untagged device replies; the profile/caller must establish adequate startup
/// separation. Event handlers must return promptly and must not synchronously wait for session I/O.</remarks>
public sealed class RadioSession : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly IByteTransport _transport;
    private readonly Func<ReadOnlyMemory<byte>, FrameParseResult> _frameParser;
    private readonly RadioSessionOptions _options;
    private readonly LinkedList<Operation> _queue = new();
    private long _queuedBytes;
    private long _generation;
    private SessionState _state = SessionState.Disconnected;
    private Connection? _connection;
    private Task? _stopTask;
    private Task? _disposeTask;
    private bool _disposeRequested;

    public RadioSession(IByteTransport transport,
        Func<ReadOnlyMemory<byte>, FrameParseResult> frameParser, RadioSessionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(frameParser);
        _options = options ?? new RadioSessionOptions();
        _options.Validate();
        _transport = transport;
        _frameParser = frameParser;
    }

    public SessionState State { get { lock (_gate) return _state; } }
    public long ConnectionGeneration { get { lock (_gate) return _generation; } }
    public event Action<ReadOnlyMemory<byte>>? UnsolicitedFrame;

    public async Task<bool> ConnectAsync(CommandSpecification synchronization,
        Func<ReadOnlyMemory<byte>, ReplyParseResult> matcher, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(synchronization);
        ArgumentNullException.ThrowIfNull(matcher);
        if (synchronization.ResponsePolicy != ResponsePolicy.ReplyRequired)
            throw new ArgumentException("Startup synchronization must require a reply.", nameof(synchronization));
        if (synchronization.Payload.Length > _options.MaxQueuedBytes)
            throw new ArgumentException("Startup synchronization exceeds the configured byte bound.", nameof(synchronization));

        await _lifecycle.WaitAsync(token).ConfigureAwait(false);
        Connection? connection = null;
        try
        {
            Connection? previous;
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposeRequested, this);
                if (_state == SessionState.Ready) return true;
                if (_state == SessionState.Stopping) return false;
                previous = _connection;
            }
            if (previous is not null)
            {
                try
                {
                    await AwaitClosedAsync(previous).WaitAsync(_options.ShutdownTimeout,
                        _options.TimeProvider, token).ConfigureAwait(false);
                }
                catch (Exception) when (!token.IsCancellationRequested)
                {
                    return false; // Never reopen while an old reader/write/close remains uncertain.
                }
            }

            var opening = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_gate)
            {
                if (_disposeRequested || _state == SessionState.Stopping) return false;
                connection = new Connection(++_generation);
                connection.OpenTask = opening.Task;
                if (previous is not null) DisposeConnectionResources(previous);
                _connection = connection;
                _stopTask = null;
                _state = SessionState.Connecting;
            }
            var started = _options.TimeProvider.GetTimestamp();
            using var deadline = new CancellationTokenSource(Budget(synchronization), _options.TimeProvider);
            using var openToken = CancellationTokenSource.CreateLinkedTokenSource(token,
                deadline.Token, connection.Cancellation.Token);
            try
            {
                _ = OpenTransportAsync(openToken.Token, opening);
                await connection.OpenTask.WaitAsync(openToken.Token).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                Fault(connection, token.IsCancellationRequested ? RadioOutcome.Cancelled
                    : deadline.IsCancellationRequested ? RadioOutcome.TimedOut : RadioOutcome.TransportError,
                    "Transport startup failed: " + exception.Message);
                return false;
            }

            var remaining = Budget(synchronization) - _options.TimeProvider.GetElapsedTime(started);
            if (remaining <= TimeSpan.Zero)
            {
                Fault(connection, RadioOutcome.TimedOut, "Startup deadline expired before synchronization.");
                return false;
            }
            Operation operation;
            lock (_gate)
            {
                if (connection.Failure is not null || _state != SessionState.Connecting) return false;
                operation = Enqueue(connection, synchronization, matcher, false, null, token,
                    remaining);
                // One worker of each kind per connection, never one Task.Run per read or command.
                connection.Reader = Task.Run(() => ReadLoopAsync(connection));
                connection.Worker = Task.Run(() => WorkLoopAsync(connection));
            }
            var result = await operation.Completion.Task.ConfigureAwait(false);
            lock (_gate)
            {
                if (result.Outcome == RadioOutcome.Succeeded && connection.Failure is null &&
                    _state == SessionState.Connecting && !_disposeRequested)
                {
                    _state = SessionState.Ready;
                    return true;
                }
            }
            Fault(connection, result.Outcome, "Startup synchronization failed: " + result.Diagnostic);
            return false;
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    private async Task OpenTransportAsync(CancellationToken token, TaskCompletionSource completion)
    {
        try
        {
            token.ThrowIfCancellationRequested();
            await _transport.OpenAsync(token).ConfigureAwait(false);
            completion.TrySetResult();
        }
        catch (Exception exception) { completion.TrySetException(exception); }
    }

    public Task<RadioOperationResult<object>> ExecuteAsync(CommandSpecification command,
        Func<ReadOnlyMemory<byte>, ReplyParseResult> matcher, bool isMutation = false,
        Func<object?, bool>? verify = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(matcher);
        if (command.ResponsePolicy == ResponsePolicy.WriteThenReadBack && verify is null)
            return Task.FromResult(Result(RadioOutcome.InvalidArgument, CompletionEvidence.NotSent,
                "Read-back requires a verification predicate."));

        lock (_gate)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromResult(Result(RadioOutcome.Cancelled, CompletionEvidence.NotSent));
            if (_disposeRequested || _state != SessionState.Ready || _connection is null)
                return Task.FromResult(Result(RadioOutcome.NotConnected, CompletionEvidence.NotSent));
            var bytes = (long)command.Payload.Length + (command.VerificationPayload?.Length ?? 0);
            if (_queue.Count >= _options.MaxQueuedOperations || bytes > _options.MaxQueuedBytes - _queuedBytes)
                return Task.FromResult(Result(RadioOutcome.Busy, CompletionEvidence.NotSent,
                    "Session queue count or byte limit reached."));
            return Enqueue(_connection, command, matcher,
                isMutation || command.ResponsePolicy is ResponsePolicy.WriteOnly or ResponsePolicy.WriteThenReadBack,
                verify, cancellationToken,
                Budget(command)).Completion.Task;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task stopping;
        TaskCompletionSource? completion = null;
        Connection? connection;
        lock (_gate)
        {
            if (_state is SessionState.Disconnected or SessionState.Disposed) return Task.CompletedTask;
            _state = SessionState.Stopping;
            connection = _connection;
            if (_stopTask is null || _stopTask.IsFaulted || _stopTask.IsCanceled)
            {
                completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _stopTask = completion.Task;
            }
            stopping = _stopTask;
        }
        if (connection is not null) Fault(connection, RadioOutcome.Cancelled, "Session stopped.");
        if (completion is not null) _ = CompleteLifecycleAsync(() => StopCoreAsync(connection), completion);
        // Cancellation abandons this wait; owned cleanup continues independently.
        return stopping.WaitAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        TaskCompletionSource? completion = null;
        Task disposing;
        lock (_gate)
        {
            _disposeRequested = true;
            if (_disposeTask is null)
            {
                completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _disposeTask = completion.Task;
            }
            disposing = _disposeTask;
        }
        if (completion is not null) _ = CompleteLifecycleAsync(DisposeCoreAsync, completion);
        return new ValueTask(disposing);
    }

    private static async Task CompleteLifecycleAsync(Func<Task> work, TaskCompletionSource completion)
    {
        try { await work().ConfigureAwait(false); completion.TrySetResult(); }
        catch (Exception exception) { completion.TrySetException(exception); }
    }

    private TimeSpan Budget(CommandSpecification command) => command.Timeout < _options.MaxOperationTimeout
        ? command.Timeout : _options.MaxOperationTimeout;

    private Operation Enqueue(Connection connection, CommandSpecification command,
        Func<ReadOnlyMemory<byte>, ReplyParseResult> matcher, bool mutation, Func<object?, bool>? verify,
        CancellationToken token, TimeSpan budget)
    {
        var operation = new Operation(command, matcher, mutation, verify, token,
            new CancellationTokenSource(budget, _options.TimeProvider));
        operation.Node = _queue.AddLast(operation);
        _queuedBytes += operation.ByteCount;
        operation.UserRegistration = token.Register(() => Interrupt(connection, operation, RadioOutcome.Cancelled));
        operation.DeadlineRegistration = operation.Deadline.Token.Register(() => Interrupt(connection, operation, RadioOutcome.TimedOut));
        _ = ReleaseOperationResourcesAsync(operation);
        connection.Signal.Release();
        return operation;
    }

    private static async Task ReleaseOperationResourcesAsync(Operation operation)
    {
        await operation.Completion.Task.ConfigureAwait(false);
        await operation.UserRegistration.DisposeAsync().ConfigureAwait(false);
        await operation.DeadlineRegistration.DisposeAsync().ConfigureAwait(false);
        operation.Deadline.Dispose();
    }

    private void Interrupt(Connection connection, Operation operation, RadioOutcome outcome)
    {
        lock (_gate)
        {
            if (operation.Completion.Task.IsCompleted) return;
            if (operation.Evidence == CompletionEvidence.NotSent)
            {
                RemoveQueued(operation);
                operation.Completion.TrySetResult(Result(outcome, CompletionEvidence.NotSent));
                return;
            }
        }
        // Closing the generation retains ownership; no next command can use the dirty stream.
        Fault(connection, outcome, outcome == RadioOutcome.Cancelled
            ? "Cancellation after write began; device effect may be unknown."
            : "Operation deadline expired after write began.");
    }

    private void RemoveQueued(Operation operation)
    {
        if (operation.Node is null) return;
        _queue.Remove(operation.Node);
        operation.Node = null;
        _queuedBytes -= operation.ByteCount;
    }

    private async Task WorkLoopAsync(Connection connection)
    {
        try
        {
            while (true)
            {
                await connection.Signal.WaitAsync(connection.Cancellation.Token).ConfigureAwait(false);
                Operation? operation;
                lock (_gate)
                {
                    if (connection.Failure is not null) return;
                    operation = _queue.First?.Value;
                    if (operation is null) continue;
                    RemoveQueued(operation);
                    connection.Active = operation;
                }
                await RunOperationAsync(connection, operation).ConfigureAwait(false);
                lock (_gate)
                {
                    connection.Active = null;
                    if (connection.Failure is not null) return;
                }
            }
        }
        catch (OperationCanceledException) when (connection.Cancellation.IsCancellationRequested) { }
        catch (Exception exception)
        {
            Fault(connection, RadioOutcome.TransportError, "Transaction worker failed: " + exception.Message);
        }
    }

    private async Task RunOperationAsync(Connection connection, Operation operation)
    {
        try
        {
            if (!await WritePayloadAsync(connection, operation, false).ConfigureAwait(false)) return;
            lock (_gate)
            {
                if (operation.Evidence == CompletionEvidence.WriteAttempted)
                    operation.Evidence = CompletionEvidence.Written;
                if (connection.Failure is not null) return;
            }
            if (operation.Command.ResponsePolicy == ResponsePolicy.WriteThenReadBack)
            {
                if (!await WritePayloadAsync(connection, operation, true).ConfigureAwait(false)) return;
            }
            if (operation.Command.ResponsePolicy == ResponsePolicy.WriteOnly)
            {
                lock (_gate)
                    if (connection.Failure is null)
                        operation.Completion.TrySetResult(Result(RadioOutcome.Succeeded, CompletionEvidence.Written));
                return;
            }
            var reply = await operation.Reply.Task.WaitAsync(connection.Cancellation.Token).ConfigureAwait(false);
            bool verified = false;
            if (operation.Command.ResponsePolicy == ResponsePolicy.WriteThenReadBack)
            {
                try { verified = reply.Value is not null && operation.Verify!(reply.Value); }
                catch (Exception exception)
                {
                    Fault(connection, RadioOutcome.ProtocolError, "Read-back verification failed: " + exception.Message);
                    return;
                }
            }
            lock (_gate)
            {
                if (connection.Failure is not null) return;
                var evidence = verified ? CompletionEvidence.ReadBackVerified : CompletionEvidence.ReplyReceived;
                operation.Completion.TrySetResult(new RadioOperationResult<object>(
                    operation.Command.ResponsePolicy == ResponsePolicy.WriteThenReadBack && !verified
                        ? RadioOutcome.ProtocolError : RadioOutcome.Succeeded,
                    evidence, operation.Observation,
                    operation.Command.ResponsePolicy == ResponsePolicy.WriteThenReadBack && !verified
                        ? "Observed read-back does not satisfy the requested postcondition." : reply.Diagnostic));
            }
        }
        catch (OperationCanceledException) when (connection.Cancellation.IsCancellationRequested) { }
        catch (Exception exception)
        {
            Fault(connection, RadioOutcome.TransportError, "Write/transaction failed: " + exception.Message);
        }
    }

    private async Task<bool> WritePayloadAsync(Connection connection, Operation operation, bool verification)
    {
        await connection.FrameBatch.WaitAsync(connection.Cancellation.Token).ConfigureAwait(false);
        ValueTask writing;
        try
        {
            lock (_gate)
            {
                if (operation.Completion.Task.IsCompleted || connection.Failure is not null) return false;
                if (!verification && (operation.UserToken.IsCancellationRequested || operation.Deadline.IsCancellationRequested))
                {
                    operation.Completion.TrySetResult(Result(operation.UserToken.IsCancellationRequested
                        ? RadioOutcome.Cancelled : RadioOutcome.TimedOut, CompletionEvidence.NotSent));
                    return false;
                }
                if (!verification) operation.Evidence = CompletionEvidence.WriteAttempted;
                operation.AcceptReply = verification || operation.Command.ResponsePolicy == ResponsePolicy.ReplyRequired;
            }
            writing = _transport.WriteAsync(verification ? operation.Verification!.Value : operation.Payload,
                connection.Cancellation.Token);
        }
        finally { connection.FrameBatch.Release(); }
        await writing.ConfigureAwait(false);
        return true;
    }

    private async Task ReadLoopAsync(Connection connection)
    {
        var buffer = new byte[_options.MaxFrameBytes];
        var buffered = 0;
        Operation? tailOperation = null;
        try
        {
            while (!connection.Cancellation.IsCancellationRequested)
            {
                var hadTail = buffered != 0;
                int read;
                try
                {
                    read = await _transport.ReadAsync(buffer.AsMemory(buffered),
                        connection.Cancellation.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (connection.Cancellation.IsCancellationRequested) { return; }
                catch (Exception exception)
                {
                    Fault(connection, RadioOutcome.TransportError, "Read failed: " + exception.Message);
                    return;
                }
                if (read <= 0 || read > buffer.Length - buffered)
                {
                    Fault(connection, RadioOutcome.TransportError, read == 0 ? "Transport reached EOF." : "Invalid read count.");
                    return;
                }
                buffered += read;
                Operation? batchOperation;
                lock (_gate) batchOperation = connection.Active is { AcceptReply: true } active ? active : null;
                await connection.FrameBatch.WaitAsync(connection.Cancellation.Token).ConfigureAwait(false);
                try
                {
                    var frameOperation = hadTail ? tailOperation : batchOperation;
                    while (buffered != 0)
                    {
                        var parsed = _frameParser(buffer.AsMemory(0, buffered));
                        if (parsed.Status == FrameParseStatus.Incomplete)
                        {
                            tailOperation = frameOperation;
                            if (buffered == buffer.Length)
                                Fault(connection, RadioOutcome.ProtocolError, "Serial frame exceeds the configured limit.");
                            break;
                        }
                        if (parsed.ConsumedBytes > buffered || parsed.Status == FrameParseStatus.Invalid)
                        {
                            Fault(connection, RadioOutcome.ProtocolError, "Invalid serial frame or parser prefix.");
                            return;
                        }
                        Dispatch(connection, parsed.Frame, frameOperation);
                        if (connection.Cancellation.IsCancellationRequested) return;
                        buffered -= parsed.ConsumedBytes;
                        buffer.AsSpan(parsed.ConsumedBytes, buffered).CopyTo(buffer);
                        frameOperation = batchOperation;
                    }
                }
                finally { connection.FrameBatch.Release(); }
            }
        }
        catch (Exception exception)
        {
            Fault(connection, RadioOutcome.ProtocolError, "Frame/reply parser failed: " + exception.Message);
        }
    }

    private void Dispatch(Connection connection, ReadOnlyMemory<byte> frame, Operation? batchOperation)
    {
        Operation? operation;
        lock (_gate)
        {
            if (connection.Failure is not null) return;
            operation = batchOperation is { AcceptReply: true } && connection.Active == batchOperation
                ? batchOperation : null;
        }
        if (operation is not null)
        {
            var reply = operation.Matcher(frame);
            if (reply.Status == ReplyParseStatus.Invalid)
            {
                lock (_gate) operation.Evidence = CompletionEvidence.ReplyReceived;
                Fault(connection, RadioOutcome.ProtocolError, reply.Diagnostic ?? "Malformed matching reply.");
                return;
            }
            if (reply.Status == ReplyParseStatus.Valid)
            {
                lock (_gate)
                {
                    if (connection.Failure is not null || connection.Active != operation || !operation.AcceptReply) return;
                    operation.AcceptReply = false;
                    operation.Evidence = CompletionEvidence.ReplyReceived;
                    if (reply.Value is not null)
                        operation.Observation = new RadioObservation<object>(reply.Value,
                            _options.TimeProvider.GetUtcNow(), connection.Generation);
                    operation.Reply.TrySetResult(reply);
                }
                return;
            }
        }
        var handlers = UnsolicitedFrame;
        if (handlers is null) return;
        foreach (Action<ReadOnlyMemory<byte>> handler in handlers.GetInvocationList())
        {
            try { handler(frame); }
            catch { /* Subscriber failures do not change serial transaction ownership. */ }
        }
    }

    private void Fault(Connection connection, RadioOutcome outcome, string diagnostic)
    {
        TaskCompletionSource close;
        lock (_gate)
        {
            if (connection.Failure is not null) return;
            connection.Failure = new Failure(outcome, diagnostic);
            if (_connection == connection && _state is not (SessionState.Stopping or SessionState.Disposed))
                _state = SessionState.Faulted;
            if (connection.Active is { } active)
                active.Completion.TrySetResult(FailureResult(active, connection.Failure));
            while (_queue.First is { } first)
            {
                RemoveQueued(first.Value);
                first.Value.Completion.TrySetResult(Result(RadioOutcome.NotConnected,
                    CompletionEvidence.NotSent, diagnostic));
            }
            close = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            connection.CloseTask = close.Task;
        }
        try { connection.Cancellation.Cancel(); }
        catch (AggregateException) { /* Adapter cancellation callbacks cannot prevent owned close. */ }
        _ = CloseTransportAsync(connection, close);
    }

    private static RadioOperationResult<object> FailureResult(Operation operation, Failure failure)
    {
        var outcome = operation.Mutation && operation.Evidence != CompletionEvidence.NotSent
            ? RadioOutcome.OutcomeUnknown : failure.Outcome;
        return new RadioOperationResult<object>(outcome, operation.Evidence,
            operation.Observation, failure.Diagnostic);
    }

    private async Task CloseTransportAsync(Connection connection, TaskCompletionSource completion)
    {
        try
        {
            await _transport.CloseAsync().ConfigureAwait(false);
            if (connection.OpenTask is { IsCompleted: false } opening)
            {
                try { await opening.ConfigureAwait(false); } catch { }
                if (_transport.IsOpen) await _transport.CloseAsync().ConfigureAwait(false);
            }
            completion.TrySetResult();
        }
        catch (Exception exception) { completion.TrySetException(exception); }
    }

    private static async Task AwaitClosedAsync(Connection connection)
    {
        await Task.WhenAll(connection.CloseTask, ObserveOpenAsync(connection.OpenTask),
            connection.Reader, connection.Worker).ConfigureAwait(false);
    }

    private static async Task ObserveOpenAsync(Task? opening)
    {
        if (opening is null) return;
        try { await opening.ConfigureAwait(false); } catch { /* ConnectAsync already reported startup failure. */ }
    }

    private async Task StopCoreAsync(Connection? connection)
    {
        using var budget = new CancellationTokenSource(_options.ShutdownTimeout, _options.TimeProvider);
        await _lifecycle.WaitAsync(budget.Token).ConfigureAwait(false);
        try
        {
            if (connection is not null)
            {
                Fault(connection, RadioOutcome.Cancelled, "Session stopped.");
                await AwaitClosedAsync(connection).WaitAsync(budget.Token).ConfigureAwait(false);
            }
            lock (_gate)
            {
                if (_state != SessionState.Disposed) _state = SessionState.Disconnected;
                if (_connection == connection) _connection = null;
            }
            if (connection is not null) DisposeConnectionResources(connection);
        }
        finally { _lifecycle.Release(); }
    }

    private async Task DisposeCoreAsync()
    {
        Exception? failure = null;
        try { await StopAsync().ConfigureAwait(false); }
        catch (Exception exception) { failure = exception; }
        try
        {
            await _transport.DisposeAsync().AsTask().WaitAsync(_options.ShutdownTimeout,
                _options.TimeProvider).ConfigureAwait(false);
        }
        finally { lock (_gate) _state = SessionState.Disposed; }
        if (failure is not null) throw failure;
    }

    private static RadioOperationResult<object> Result(RadioOutcome outcome, CompletionEvidence evidence,
        string? diagnostic = null) => new(outcome, evidence, diagnostic: diagnostic);

    private static void DisposeConnectionResources(Connection connection)
    {
        if (Interlocked.Exchange(ref connection.ResourcesDisposed, 1) != 0) return;
        connection.Cancellation.Dispose();
        connection.Signal.Dispose();
        connection.FrameBatch.Dispose();
    }

    private sealed record Failure(RadioOutcome Outcome, string Diagnostic);

    private sealed class Connection(long generation)
    {
        public long Generation { get; } = generation;
        public CancellationTokenSource Cancellation { get; } = new();
        public SemaphoreSlim Signal { get; } = new(0);
        public SemaphoreSlim FrameBatch { get; } = new(1, 1);
        public Operation? Active { get; set; }
        public Failure? Failure { get; set; }
        public Task? OpenTask { get; set; }
        public Task Reader { get; set; } = Task.CompletedTask;
        public Task Worker { get; set; } = Task.CompletedTask;
        public Task CloseTask { get; set; } = Task.CompletedTask;
        public int ResourcesDisposed;
    }

    private sealed class Operation(CommandSpecification command,
        Func<ReadOnlyMemory<byte>, ReplyParseResult> matcher, bool mutation,
        Func<object?, bool>? verify, CancellationToken userToken, CancellationTokenSource deadline)
    {
        public CommandSpecification Command { get; } = command;
        public ReadOnlyMemory<byte> Payload { get; } = command.Payload;
        public ReadOnlyMemory<byte>? Verification { get; } = command.VerificationPayload;
        public long ByteCount => (long)Payload.Length + (Verification?.Length ?? 0);
        public Func<ReadOnlyMemory<byte>, ReplyParseResult> Matcher { get; } = matcher;
        public bool Mutation { get; } = mutation;
        public Func<object?, bool>? Verify { get; } = verify;
        public CancellationToken UserToken { get; } = userToken;
        public CancellationTokenSource Deadline { get; } = deadline;
        public CancellationTokenRegistration UserRegistration { get; set; }
        public CancellationTokenRegistration DeadlineRegistration { get; set; }
        public LinkedListNode<Operation>? Node { get; set; }
        public CompletionEvidence Evidence { get; set; } = CompletionEvidence.NotSent;
        public bool AcceptReply { get; set; }
        public RadioObservation<object>? Observation { get; set; }
        public TaskCompletionSource<ReplyParseResult> Reply { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<RadioOperationResult<object>> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
