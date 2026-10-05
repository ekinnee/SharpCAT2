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

    public Task<bool> ConnectAsync(CommandSpecification synchronization,
        Func<ReadOnlyMemory<byte>, ReplyParseResult> matcher, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(synchronization);
        return ConnectCoreAsync(synchronization, matcher, Budget(synchronization), null, null, token);
    }

    /// <summary>Runs the complete startup transaction before publishing Ready. Quiet drainage
    /// is a bounded profile policy, not proof that an untagged device abandoned every old reply.</summary>
    public Task<bool> ConnectAsync(IReadOnlyList<SessionTransactionStep> steps, TimeSpan timeout,
        Func<IReadOnlyList<object?>, SessionTransactionConclusion>? conclude = null,
        CancellationToken token = default)
    {
        var snapshot = PrepareTransaction(steps, timeout);
        if (snapshot[^1].Command.ResponsePolicy == ResponsePolicy.WriteOnly)
            throw new ArgumentException("Startup must finish with a validated reply.", nameof(steps));
        if (TransactionBytes(snapshot) > _options.MaxQueuedBytes)
            throw new ArgumentException("Startup transaction exceeds the configured byte bound.", nameof(steps));
        return ConnectCoreAsync(snapshot[0].Command, snapshot[0].Matcher, TransactionBudget(timeout),
            snapshot, conclude, token);
    }

    private async Task<bool> ConnectCoreAsync(CommandSpecification synchronization,
        Func<ReadOnlyMemory<byte>, ReplyParseResult> matcher, TimeSpan startupBudget,
        SessionTransactionStep[]? steps, Func<IReadOnlyList<object?>, SessionTransactionConclusion>? conclude,
        CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(synchronization);
        ArgumentNullException.ThrowIfNull(matcher);
        if (steps is null && synchronization.ResponsePolicy != ResponsePolicy.ReplyRequired)
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
            using var deadline = new CancellationTokenSource(startupBudget, _options.TimeProvider);
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

            var remaining = startupBudget - _options.TimeProvider.GetElapsedTime(started);
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
                    remaining, steps, conclude);
                if (steps is not null && steps[0].QuietPeriodAfter > TimeSpan.Zero &&
                    steps[0].Command.ResponsePolicy == ResponsePolicy.WriteOnly)
                {
                    connection.Draining = true;
                    connection.LastReadTimestamp = _options.TimeProvider.GetTimestamp();
                    connection.BufferEpoch++;
                }
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

    /// <summary>Admits all fixed steps as one queue item with one admission deadline. The
    /// conclusion is pure; it must not perform I/O or synchronously call the session.</summary>
    public Task<RadioOperationResult<object>> ExecuteTransactionAsync(IReadOnlyList<SessionTransactionStep> steps,
        TimeSpan timeout, Func<IReadOnlyList<object?>, SessionTransactionConclusion>? conclude = null,
        CancellationToken cancellationToken = default)
    {
        SessionTransactionStep[] snapshot;
        try { snapshot = PrepareTransaction(steps, timeout); }
        catch (ArgumentException exception)
        {
            return Task.FromResult(Result(RadioOutcome.InvalidArgument, CompletionEvidence.NotSent, exception.Message));
        }
        lock (_gate)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromResult(Result(RadioOutcome.Cancelled, CompletionEvidence.NotSent));
            if (_disposeRequested || _state != SessionState.Ready || _connection is null)
                return Task.FromResult(Result(RadioOutcome.NotConnected, CompletionEvidence.NotSent));
            if (_queue.Count >= _options.MaxQueuedOperations || TransactionBytes(snapshot) > _options.MaxQueuedBytes - _queuedBytes)
                return Task.FromResult(Result(RadioOutcome.Busy, CompletionEvidence.NotSent,
                    "Session queue count or byte limit reached."));
            return Enqueue(_connection, snapshot[0].Command, snapshot[0].Matcher, false, null,
                cancellationToken, TransactionBudget(timeout), snapshot, conclude).Completion.Task;
        }
    }

    private SessionTransactionStep[] PrepareTransaction(IReadOnlyList<SessionTransactionStep> steps, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(steps);
        if (steps.Count == 0 || steps.Count > _options.MaxTransactionSteps)
            throw new ArgumentException("Transaction step count is outside the configured bound.", nameof(steps));
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
        var snapshot = steps.ToArray();
        foreach (var step in snapshot)
        {
            ArgumentNullException.ThrowIfNull(step);
            ArgumentNullException.ThrowIfNull(step.Command);
            ArgumentNullException.ThrowIfNull(step.Matcher);
            if (step.QuietPeriodAfter < TimeSpan.Zero || step.QuietPeriodAfter > _options.MaxOperationTimeout)
                throw new ArgumentOutOfRangeException(nameof(steps), "Quiet interval exceeds the operation bound.");
        }
        return snapshot;
    }

    private TimeSpan TransactionBudget(TimeSpan timeout) => timeout < _options.MaxOperationTimeout ? timeout : _options.MaxOperationTimeout;
    private static long TransactionBytes(IEnumerable<SessionTransactionStep> steps) => steps.Sum(step =>
        (long)step.Command.Payload.Length + (step.Command.VerificationPayload?.Length ?? 0));

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
        CancellationToken token, TimeSpan budget, SessionTransactionStep[]? steps = null,
        Func<IReadOnlyList<object?>, SessionTransactionConclusion>? conclude = null)
    {
        var operation = new Operation(command, matcher, mutation, verify, token,
            new CancellationTokenSource(budget, _options.TimeProvider)) { Steps = steps, Conclude = conclude };
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
        if (operation.Steps is not null)
        {
            await RunTransactionAsync(connection, operation).ConfigureAwait(false);
            return;
        }
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
            var reply = await operation.CurrentReply!.Completion.Task.WaitAsync(connection.Cancellation.Token).ConfigureAwait(false);
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

    private async Task RunTransactionAsync(Connection connection, Operation operation)
    {
        var values = new object?[operation.Steps!.Length];
        try
        {
            for (var index = 0; index < operation.Steps.Length; index++)
            {
                var step = operation.Steps[index];
                if (!await WriteStepAsync(connection, operation, step.Command.Payload, step.Matcher,
                    step.Command.ResponsePolicy == ResponsePolicy.ReplyRequired,
                    step.IsMutation || step.Command.ResponsePolicy is ResponsePolicy.WriteOnly or ResponsePolicy.WriteThenReadBack,
                    step.QuietPeriodAfter > TimeSpan.Zero && step.Command.ResponsePolicy == ResponsePolicy.WriteOnly)
                    .ConfigureAwait(false)) return;
                if (step.Command.ResponsePolicy == ResponsePolicy.WriteThenReadBack)
                {
                    if (!await WriteStepAsync(connection, operation, step.Command.VerificationPayload!.Value,
                        step.Matcher, true, false).ConfigureAwait(false)) return;
                }
                if (step.Command.ResponsePolicy != ResponsePolicy.WriteOnly)
                {
                    var reply = await operation.CurrentReply!.Completion.Task.WaitAsync(connection.Cancellation.Token).ConfigureAwait(false);
                    values[index] = reply.Value;
                }
                if (step.QuietPeriodAfter > TimeSpan.Zero)
                    await DrainUntilQuietAsync(connection, operation, step.QuietPeriodAfter).ConfigureAwait(false);
            }
            // A profile can distinguish a failed observed postcondition from an indeterminate
            // equal-frequency swap. It cannot fabricate completion evidence or a generation.
            var conclusion = operation.Conclude?.Invoke(Array.AsReadOnly(values));
            if (conclusion is not null && conclusion.Outcome is not
                (RadioOutcome.Succeeded or RadioOutcome.ProtocolError or RadioOutcome.OutcomeUnknown))
                throw new ArgumentException("Transaction conclusion must be success, protocol failure, or uncertainty.");
            lock (_gate)
            {
                if (connection.Failure is not null || operation.Completion.Task.IsCompleted) return;
                var observation = operation.Observation;
                if (conclusion?.Value is { } value)
                {
                    if (observation is null) throw new ArgumentException("A conclusion value requires an observed reply.");
                    observation = new RadioObservation<object>(value, observation.ObservedAt, connection.Generation);
                }
                var evidence = observation is not null && operation.MutationStarted && conclusion?.Outcome == RadioOutcome.Succeeded
                    ? CompletionEvidence.ReadBackVerified : operation.Evidence;
                operation.Completion.TrySetResult(new RadioOperationResult<object>(
                    conclusion?.Outcome ?? RadioOutcome.Succeeded, evidence, observation, conclusion?.Diagnostic));
            }
        }
        catch (OperationCanceledException) when (connection.Cancellation.IsCancellationRequested) { }
        catch (Exception exception)
        {
            Fault(connection, RadioOutcome.ProtocolError, "Composite transaction failed: " + exception.Message);
        }
    }

    private async Task DrainUntilQuietAsync(Connection connection, Operation operation, TimeSpan quietPeriod)
    {
        await connection.FrameBatch.WaitAsync(connection.Cancellation.Token).ConfigureAwait(false);
        try
        {
            lock (_gate)
            {
                operation.CurrentReply = null;
                connection.Draining = true;
                connection.LastReadTimestamp = _options.TimeProvider.GetTimestamp();
                connection.BufferEpoch++;
            }
        }
        finally { connection.FrameBatch.Release(); }
        while (true)
        {
            TimeSpan remaining;
            lock (_gate) remaining = quietPeriod - _options.TimeProvider.GetElapsedTime(connection.LastReadTimestamp);
            if (remaining > TimeSpan.Zero)
                await Task.Delay(remaining, _options.TimeProvider, connection.Cancellation.Token).ConfigureAwait(false);
            await connection.FrameBatch.WaitAsync(connection.Cancellation.Token).ConfigureAwait(false);
            try
            {
                lock (_gate)
                {
                    if (_options.TimeProvider.GetElapsedTime(connection.LastReadTimestamp) < quietPeriod) continue;
                    connection.Draining = false;
                    connection.BufferEpoch++; // Discard a partial frame before the next expectation.
                    return;
                }
            }
            finally { connection.FrameBatch.Release(); }
        }
    }

    private async Task<bool> WritePayloadAsync(Connection connection, Operation operation, bool verification)
        => await WriteStepAsync(connection, operation,
            verification ? operation.Verification!.Value : operation.Payload, operation.Matcher,
            verification || operation.Command.ResponsePolicy == ResponsePolicy.ReplyRequired,
            !verification && operation.Mutation).ConfigureAwait(false);

    private async Task<bool> WriteStepAsync(Connection connection, Operation operation, ReadOnlyMemory<byte> payload,
        Func<ReadOnlyMemory<byte>, ReplyParseResult> matcher, bool expectsReply, bool mutation, bool drainAfterWrite = false)
    {
        await connection.FrameBatch.WaitAsync(connection.Cancellation.Token).ConfigureAwait(false);
        ValueTask writing;
        try
        {
            RadioOutcome? interrupted = null;
            lock (_gate)
            {
                if (operation.Completion.Task.IsCompleted || connection.Failure is not null) return false;
                if (operation.UserToken.IsCancellationRequested || operation.Deadline.IsCancellationRequested)
                    interrupted = operation.UserToken.IsCancellationRequested ? RadioOutcome.Cancelled : RadioOutcome.TimedOut;
                else
                {
                    if (mutation) operation.MutationStarted = true;
                    operation.Evidence = CompletionEvidence.WriteAttempted;
                    operation.Observation = null;
                    operation.CurrentReply = expectsReply ? new ReplyExpectation(operation, matcher) : null;
                    if (drainAfterWrite)
                    {
                        connection.Draining = true;
                        connection.LastReadTimestamp = _options.TimeProvider.GetTimestamp();
                        connection.BufferEpoch++;
                    }
                }
            }
            if (interrupted is { } outcome) { Interrupt(connection, operation, outcome); return false; }
            writing = _transport.WriteAsync(payload, connection.Cancellation.Token);
        }
        catch (Exception exception)
        {
            Fault(connection, RadioOutcome.TransportError, "Write failed: " + exception.Message);
            return false;
        }
        finally { connection.FrameBatch.Release(); }
        try { await writing.ConfigureAwait(false); }
        catch (Exception exception)
        {
            Fault(connection, RadioOutcome.TransportError, "Write failed: " + exception.Message);
            return false;
        }
        lock (_gate)
            if (operation.Evidence == CompletionEvidence.WriteAttempted) operation.Evidence = CompletionEvidence.Written;
        return true;
    }

    private async Task ReadLoopAsync(Connection connection)
    {
        var buffer = new byte[_options.MaxFrameBytes];
        var buffered = 0;
        ReplyExpectation? tailReply = null;
        long bufferEpoch = 0;
        try
        {
            while (!connection.Cancellation.IsCancellationRequested)
            {
                var readOffset = buffered;
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
                ReplyExpectation? batchReply;
                lock (_gate)
                {
                    if (connection.Draining)
                    {
                        connection.LastReadTimestamp = _options.TimeProvider.GetTimestamp();
                        buffered = 0;
                        tailReply = null;
                        bufferEpoch = connection.BufferEpoch;
                        continue;
                    }
                    if (bufferEpoch != connection.BufferEpoch)
                    {
                        buffer.AsSpan(readOffset, read).CopyTo(buffer);
                        buffered = read;
                        readOffset = 0;
                        tailReply = null;
                        bufferEpoch = connection.BufferEpoch;
                    }
                    batchReply = connection.Active?.CurrentReply is { Accept: true } current ? current : null;
                }
                await connection.FrameBatch.WaitAsync(connection.Cancellation.Token).ConfigureAwait(false);
                try
                {
                    lock (_gate)
                    {
                        if (connection.Draining || bufferEpoch != connection.BufferEpoch)
                        {
                            buffered = 0;
                            tailReply = null;
                            bufferEpoch = connection.BufferEpoch;
                            continue;
                        }
                    }
                    var frameReply = readOffset != 0 ? tailReply : batchReply;
                    while (buffered != 0)
                    {
                        var parsed = _frameParser(buffer.AsMemory(0, buffered));
                        if (parsed.Status == FrameParseStatus.Incomplete)
                        {
                            tailReply = frameReply;
                            if (buffered == buffer.Length)
                                Fault(connection, RadioOutcome.ProtocolError, "Serial frame exceeds the configured limit.");
                            break;
                        }
                        if (parsed.ConsumedBytes > buffered || parsed.Status == FrameParseStatus.Invalid)
                        {
                            Fault(connection, RadioOutcome.ProtocolError, "Invalid serial frame or parser prefix.");
                            return;
                        }
                        Dispatch(connection, parsed.Frame, frameReply);
                        if (connection.Cancellation.IsCancellationRequested) return;
                        buffered -= parsed.ConsumedBytes;
                        buffer.AsSpan(parsed.ConsumedBytes, buffered).CopyTo(buffer);
                        frameReply = batchReply;
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

    private void Dispatch(Connection connection, ReadOnlyMemory<byte> frame, ReplyExpectation? batchReply)
    {
        Operation? operation;
        lock (_gate)
        {
            if (connection.Failure is not null) return;
            operation = batchReply is { Accept: true } && connection.Active == batchReply.Operation &&
                batchReply.Operation.CurrentReply == batchReply ? batchReply.Operation : null;
        }
        if (operation is not null)
        {
            var reply = batchReply!.Matcher(frame);
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
                    if (connection.Failure is not null || connection.Active != operation ||
                        operation.CurrentReply != batchReply || !batchReply.Accept) return;
                    batchReply.Accept = false;
                    operation.Evidence = CompletionEvidence.ReplyReceived;
                    if (reply.Value is not null)
                        operation.Observation = new RadioObservation<object>(reply.Value,
                            _options.TimeProvider.GetUtcNow(), connection.Generation);
                    batchReply.Completion.TrySetResult(reply);
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
        var outcome = operation.MutationStarted
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
        public bool Draining;
        public long LastReadTimestamp;
        public long BufferEpoch;
    }

    private sealed class Operation(CommandSpecification command,
        Func<ReadOnlyMemory<byte>, ReplyParseResult> matcher, bool mutation,
        Func<object?, bool>? verify, CancellationToken userToken, CancellationTokenSource deadline)
    {
        public CommandSpecification Command { get; } = command;
        public ReadOnlyMemory<byte> Payload { get; } = command.Payload;
        public ReadOnlyMemory<byte>? Verification { get; } = command.VerificationPayload;
        public SessionTransactionStep[]? Steps { get; init; }
        public Func<IReadOnlyList<object?>, SessionTransactionConclusion>? Conclude { get; init; }
        public long ByteCount => Steps is null ? (long)Payload.Length + (Verification?.Length ?? 0) : TransactionBytes(Steps);
        public Func<ReadOnlyMemory<byte>, ReplyParseResult> Matcher { get; } = matcher;
        public bool Mutation { get; } = mutation;
        public Func<object?, bool>? Verify { get; } = verify;
        public CancellationToken UserToken { get; } = userToken;
        public CancellationTokenSource Deadline { get; } = deadline;
        public CancellationTokenRegistration UserRegistration { get; set; }
        public CancellationTokenRegistration DeadlineRegistration { get; set; }
        public LinkedListNode<Operation>? Node { get; set; }
        public CompletionEvidence Evidence { get; set; } = CompletionEvidence.NotSent;
        public bool MutationStarted { get; set; }
        public ReplyExpectation? CurrentReply { get; set; }
        public RadioObservation<object>? Observation { get; set; }
        public TaskCompletionSource<RadioOperationResult<object>> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    // Identity belongs to one reply step, rather than the entire composite operation.
    // A captured batch or fragmented tail can never acquire a later step's identity.
    private sealed class ReplyExpectation(Operation operation, Func<ReadOnlyMemory<byte>, ReplyParseResult> matcher)
    {
        public Operation Operation { get; } = operation;
        public Func<ReadOnlyMemory<byte>, ReplyParseResult> Matcher { get; } = matcher;
        public bool Accept { get; set; } = true;
        public TaskCompletionSource<ReplyParseResult> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
