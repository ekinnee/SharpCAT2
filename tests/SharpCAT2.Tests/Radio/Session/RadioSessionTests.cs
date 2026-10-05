using System.Text;
using SharpCAT2.Core.Radio.Contracts;
using SharpCAT2.ServerLibrary.Radio.Protocols;
using SharpCAT2.ServerLibrary.Radio.Session;

namespace SharpCAT2.Tests.Radio.Session;

public class RadioSessionTests
{
    private static CommandSpecification Command(string text, ResponsePolicy policy = ResponsePolicy.ReplyRequired,
        TimeSpan? timeout = null, string? verification = null) => new(Encoding.ASCII.GetBytes(text), policy,
        timeout ?? TimeSpan.FromSeconds(5), policy == ResponsePolicy.WriteOnly ? null : text[..1],
        verification is null ? (ReadOnlyMemory<byte>?)null : new ReadOnlyMemory<byte>(Encoding.ASCII.GetBytes(verification)));

    private static FrameParseResult Parse(ReadOnlyMemory<byte> buffer)
    {
        var index = buffer.Span.IndexOf((byte)';');
        return index < 0 ? new FrameParseResult(FrameParseStatus.Incomplete, 0)
            : new FrameParseResult(FrameParseStatus.Complete, index + 1, buffer[..(index + 1)]);
    }

    private static Func<ReadOnlyMemory<byte>, ReplyParseResult> Match(char opcode) => frame =>
    {
        var text = Encoding.ASCII.GetString(frame.Span);
        if (text[0] != opcode) return new ReplyParseResult(ReplyParseStatus.Unrelated);
        return long.TryParse(text[1..^1], out var value)
            ? new ReplyParseResult(ReplyParseStatus.Valid, value)
            : new ReplyParseResult(ReplyParseStatus.Invalid, diagnostic: "Matching reply has malformed digits.");
    };

    private static async Task Connect(RadioSession session, ControlledByteTransport transport)
    {
        Assert.True(await session.ConnectAsync(Command("S;"), Match('S')).WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.Equal("S;", await transport.NextWriteAsync());
        Assert.Equal(SessionState.Ready, session.State);
    }

    [Fact]
    public async Task SynchronizationPrecedesReadinessAndWriteOnlyCompletesWithoutReply()
    {
        var transport = new ControlledByteTransport();
        await using var session = new RadioSession(transport, Parse);
        var disconnected = await session.ExecuteAsync(Command("X;", ResponsePolicy.WriteOnly), Match('X'));
        Assert.Equal(RadioOutcome.NotConnected, disconnected.Outcome);
        await Connect(session, transport);
        var result = await session.ExecuteAsync(Command("X;", ResponsePolicy.WriteOnly), Match('X'));
        Assert.Equal(RadioOutcome.Succeeded, result.Outcome);
        Assert.Equal(CompletionEvidence.Written, result.Evidence);
        Assert.Null(result.Observation);
        Assert.Equal("X;", await transport.NextWriteAsync());
        Assert.Equal(1, transport.MaximumReaders);
    }

    [Fact]
    public async Task ConcurrentRequestsSerializeAndQueuedCancellationWritesNothing()
    {
        var transport = new ControlledByteTransport();
        await using var session = new RadioSession(transport, Parse);
        await Connect(session, transport);
        var active = session.ExecuteAsync(Command("R;"), Match('R'));
        Assert.Equal("R;", await transport.NextWriteAsync());
        using var cancelled = new CancellationTokenSource();
        var queued = session.ExecuteAsync(Command("C;", ResponsePolicy.WriteOnly), Match('C'), cancellationToken: cancelled.Token);
        var next = session.ExecuteAsync(Command("N;", ResponsePolicy.WriteOnly), Match('N'));
        cancelled.Cancel();
        var cancellation = await queued;
        Assert.Equal(RadioOutcome.Cancelled, cancellation.Outcome);
        Assert.Equal(CompletionEvidence.NotSent, cancellation.Evidence);
        Assert.Equal(new[] { "S;", "R;" }, transport.Written);
        transport.Send("R42;");
        Assert.Equal(42L, (await active).Observation!.Value);
        Assert.Equal(RadioOutcome.Succeeded, (await next).Outcome);
        Assert.Equal("N;", await transport.NextWriteAsync());
        Assert.DoesNotContain("C;", transport.Written);
    }

    [Fact]
    public async Task QueueCountAndByteLimitsRejectExplicitlyAndCancellationReleasesCapacity()
    {
        var transport = new ControlledByteTransport();
        await using var session = new RadioSession(transport, Parse,
            new RadioSessionOptions { MaxQueuedOperations = 1, MaxQueuedBytes = 4 });
        await Connect(session, transport);
        var active = session.ExecuteAsync(Command("R;"), Match('R'));
        await transport.NextWriteAsync();
        using var cancellation = new CancellationTokenSource();
        var waiting = session.ExecuteAsync(Command("Q;"), Match('Q'), cancellationToken: cancellation.Token);
        Assert.Equal(RadioOutcome.Busy, (await session.ExecuteAsync(Command("N;"), Match('N'))).Outcome);
        cancellation.Cancel();
        await waiting;
        Assert.Equal(RadioOutcome.Busy, (await session.ExecuteAsync(Command("XXXXX;"), Match('X'))).Outcome);
        var replacement = session.ExecuteAsync(Command("N;", ResponsePolicy.WriteOnly), Match('N'));
        transport.Send("R1;");
        await active;
        Assert.Equal(RadioOutcome.Succeeded, (await replacement).Outcome);
    }

    [Fact]
    public async Task QueuedDeadlineCountsWaitingTimeAndDoesNotFaultCleanActiveTransaction()
    {
        var clock = new ManualTimeProvider();
        var transport = new ControlledByteTransport();
        await using var session = new RadioSession(transport, Parse, new RadioSessionOptions { TimeProvider = clock });
        await Connect(session, transport);
        var active = session.ExecuteAsync(Command("R;"), Match('R'));
        await transport.NextWriteAsync();
        var queued = session.ExecuteAsync(Command("Q;", timeout: TimeSpan.FromSeconds(1)), Match('Q'));
        clock.Advance(TimeSpan.FromSeconds(1));
        var result = await queued;
        Assert.Equal(RadioOutcome.TimedOut, result.Outcome);
        Assert.Equal(CompletionEvidence.NotSent, result.Evidence);
        Assert.Equal(SessionState.Ready, session.State);
        transport.Send("R1;");
        await active;
        Assert.DoesNotContain("Q;", transport.Written);
    }

    [Fact]
    public async Task WriteAndReadBackAreOneTransactionAndRequireActualObservedPostcondition()
    {
        var transport = new ControlledByteTransport();
        await using var session = new RadioSession(transport, Parse);
        await Connect(session, transport);
        var mutation = session.ExecuteAsync(Command("A10;", ResponsePolicy.WriteThenReadBack, verification: "Q;"),
            Match('Q'), verify: value => value is 10L);
        Assert.Equal("A10;", await transport.NextWriteAsync());
        var next = session.ExecuteAsync(Command("B;", ResponsePolicy.WriteOnly), Match('B'));
        Assert.Equal("Q;", await transport.NextWriteAsync());
        Assert.Equal(new[] { "S;", "A10;", "Q;" }, transport.Written);
        transport.Send("Q10;");
        var result = await mutation;
        Assert.Equal(RadioOutcome.Succeeded, result.Outcome);
        Assert.Equal(CompletionEvidence.ReadBackVerified, result.Evidence);
        Assert.Equal(10L, result.Observation!.Value);
        Assert.Equal(1, result.Observation.ConnectionGeneration);
        Assert.Equal("B;", await transport.NextWriteAsync());
        await next;
    }

    [Fact]
    public async Task PartialWriteFailurePreservesUncertaintyFaultsGenerationAndNeverReplays()
    {
        var transport = new ControlledByteTransport
        {
            OnWrite = (_, _) => ValueTask.FromException(new IOException("Failure after accepting a prefix."))
        };
        await using var session = new RadioSession(transport, Parse);
        await Connect(session, transport);
        var result = await session.ExecuteAsync(Command("X;", ResponsePolicy.WriteOnly), Match('X'));
        Assert.Equal(RadioOutcome.OutcomeUnknown, result.Outcome);
        Assert.Equal(CompletionEvidence.WriteAttempted, result.Evidence);
        Assert.Equal(SessionState.Faulted, session.State);
        Assert.Equal(RadioOutcome.NotConnected, (await session.ExecuteAsync(Command("N;"), Match('N'))).Outcome);
        Assert.Equal(new[] { "S;", "X;" }, transport.Written);
    }

    [Fact]
    public async Task PostWriteCancellationFaultsBeforeReleasingTransactionAndFailsQueuedWork()
    {
        var transport = new ControlledByteTransport();
        await using var session = new RadioSession(transport, Parse);
        await Connect(session, transport);
        using var cancellation = new CancellationTokenSource();
        var active = session.ExecuteAsync(Command("X;"), Match('X'), isMutation: true, cancellationToken: cancellation.Token);
        await transport.NextWriteAsync();
        var waiting = session.ExecuteAsync(Command("N;", ResponsePolicy.WriteOnly), Match('N'));
        cancellation.Cancel();
        var result = await active;
        Assert.Equal(RadioOutcome.OutcomeUnknown, result.Outcome);
        Assert.Contains(result.Evidence, new[] { CompletionEvidence.WriteAttempted, CompletionEvidence.Written });
        Assert.Equal(RadioOutcome.NotConnected, (await waiting).Outcome);
        Assert.Equal(SessionState.Faulted, session.State);
        Assert.DoesNotContain("N;", transport.Written);
    }

    [Fact]
    public async Task DeadlineCapFaultsAndLateSameOpcodeCannotCompleteAnotherCallUntilExplicitReconnect()
    {
        var clock = new ManualTimeProvider();
        var transport = new ControlledByteTransport();
        await using var session = new RadioSession(transport, Parse,
            new RadioSessionOptions { TimeProvider = clock, MaxOperationTimeout = TimeSpan.FromSeconds(2) });
        await Connect(session, transport);
        var active = session.ExecuteAsync(Command("R;", timeout: TimeSpan.FromMinutes(1)), Match('R'));
        await transport.NextWriteAsync();
        var queued = session.ExecuteAsync(Command("R;"), Match('R'));
        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(RadioOutcome.TimedOut, (await active).Outcome);
        Assert.Equal(RadioOutcome.NotConnected, (await queued).Outcome);
        transport.Send("R99;");
        Assert.Equal(RadioOutcome.NotConnected, (await session.ExecuteAsync(Command("R;"), Match('R'))).Outcome);
        await Connect(session, transport);
        Assert.Equal(2, session.ConnectionGeneration);
        var fresh = session.ExecuteAsync(Command("R;"), Match('R'));
        await transport.NextWriteAsync();
        Assert.False(fresh.IsCompleted);
        transport.Send("R2;");
        Assert.Equal(2L, (await fresh).Observation!.Value);
        Assert.Equal(2, transport.OpenCount);
    }

    [Fact]
    public async Task CoalescedDuplicateReplyCannotSatisfyNextQueuedRequest()
    {
        var transport = new ControlledByteTransport();
        await using var session = new RadioSession(transport, Parse);
        await Connect(session, transport);
        var unsolicited = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.UnsolicitedFrame += bytes => unsolicited.TrySetResult(Encoding.ASCII.GetString(bytes.Span));
        var first = session.ExecuteAsync(Command("R;"), Match('R'));
        await transport.NextWriteAsync();
        var second = session.ExecuteAsync(Command("R;"), Match('R'));
        transport.Send("R1;R2;");
        Assert.Equal(1L, (await first).Observation!.Value);
        Assert.Equal("R2;", await unsolicited.Task.WaitAsync(TimeSpan.FromSeconds(3)));
        await transport.NextWriteAsync();
        Assert.False(second.IsCompleted);
        transport.Send("R3;");
        Assert.Equal(3L, (await second).Observation!.Value);
    }

    [Fact]
    public async Task FragmentedTailFromOldBatchCannotSatisfyNextQueuedRequest()
    {
        var transport = new ControlledByteTransport();
        await using var session = new RadioSession(transport, Parse);
        await Connect(session, transport);
        var stale = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.UnsolicitedFrame += bytes => stale.TrySetResult(Encoding.ASCII.GetString(bytes.Span));
        var first = session.ExecuteAsync(Command("R;"), Match('R'));
        await transport.NextWriteAsync();
        var second = session.ExecuteAsync(Command("R;"), Match('R'));
        transport.Send("R1;R");
        await first;
        await transport.NextWriteAsync();
        transport.Send("99;R2;");
        Assert.Equal("R99;", await stale.Task.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.Equal(2L, (await second).Observation!.Value);
    }

    [Fact]
    public async Task MatchingMalformedReplyFaultsAndDoesNotDispatchNextCommand()
    {
        var transport = new ControlledByteTransport();
        await using var session = new RadioSession(transport, Parse);
        await Connect(session, transport);
        var active = session.ExecuteAsync(Command("R;"), Match('R'));
        await transport.NextWriteAsync();
        var waiting = session.ExecuteAsync(Command("N;", ResponsePolicy.WriteOnly), Match('N'));
        transport.Send("Rbad;");
        var result = await active;
        Assert.Equal(RadioOutcome.ProtocolError, result.Outcome);
        Assert.Equal(CompletionEvidence.ReplyReceived, result.Evidence);
        Assert.Equal(RadioOutcome.NotConnected, (await waiting).Outcome);
        Assert.DoesNotContain("N;", transport.Written);
    }

    [Fact]
    public async Task EofAndOversizedIncompleteFramesFaultRatherThanSpin()
    {
        var transport = new ControlledByteTransport();
        await using var session = new RadioSession(transport, Parse, new RadioSessionOptions { MaxFrameBytes = 8 });
        await Connect(session, transport);
        var active = session.ExecuteAsync(Command("R;"), Match('R'));
        await transport.NextWriteAsync();
        transport.Send("12345678");
        Assert.Equal(RadioOutcome.ProtocolError, (await active).Outcome);
        await Connect(session, transport);
        var afterReconnect = session.ExecuteAsync(Command("R;"), Match('R'));
        await transport.NextWriteAsync();
        transport.End();
        Assert.Equal(RadioOutcome.TransportError, (await afterReconnect).Outcome);
        Assert.Equal(SessionState.Faulted, session.State);
    }

    [Fact]
    public async Task StopUnblocksInProgressWriteAndDisposeIsIdempotent()
    {
        var transport = new ControlledByteTransport
        {
            OnWrite = async (_, token) => await Task.Delay(Timeout.InfiniteTimeSpan, token)
        };
        var session = new RadioSession(transport, Parse);
        await Connect(session, transport);
        var active = session.ExecuteAsync(Command("X;", ResponsePolicy.WriteOnly), Match('X'));
        await transport.NextWriteAsync();
        await session.StopAsync().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(RadioOutcome.OutcomeUnknown, (await active).Outcome);
        Assert.Equal(CompletionEvidence.WriteAttempted, (await active).Evidence);
        Assert.Equal(SessionState.Disconnected, session.State);
        Assert.False(transport.IsOpen);
        await Task.WhenAll(session.DisposeAsync().AsTask(), session.DisposeAsync().AsTask());
        Assert.Equal(1, transport.DisposeCount);
        Assert.Equal(SessionState.Disposed, session.State);
    }

    [Fact]
    public async Task ConcurrentStopInterruptsStartupAndFailedStartupStillDisposesOwnedTransport()
    {
        var transport = new ControlledByteTransport { OpenGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var session = new RadioSession(transport, Parse);
        var connecting = session.ConnectAsync(Command("S;"), Match('S'));
        await transport.OpenEntered.Task;
        await session.StopAsync().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.False(await connecting);
        Assert.Empty(transport.Written);
        await session.DisposeAsync();
        Assert.Equal(1, transport.DisposeCount);

        var failing = new ControlledByteTransport { FailOpen = true };
        var failedSession = new RadioSession(failing, Parse);
        Assert.False(await failedSession.ConnectAsync(Command("S;"), Match('S')));
        await failedSession.DisposeAsync();
        Assert.Equal(1, failing.DisposeCount);
    }

    [Fact]
    public async Task ShutdownBudgetIsBoundedEvenWhenAdapterCloseStalls()
    {
        var clock = new ManualTimeProvider();
        var transport = new ControlledByteTransport { CloseGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var session = new RadioSession(transport, Parse, new RadioSessionOptions { TimeProvider = clock });
        await Connect(session, transport);
        var stopping = session.StopAsync();
        clock.Advance(TimeSpan.FromSeconds(10));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stopping);
        Assert.Equal(SessionState.Stopping, session.State);
        Assert.Equal(RadioOutcome.NotConnected, (await session.ExecuteAsync(Command("R;"), Match('R'))).Outcome);
        transport.CloseGate.TrySetResult();
        await session.StopAsync().WaitAsync(TimeSpan.FromSeconds(3));
        await session.DisposeAsync();
    }

    [Fact]
    public async Task StartupDeadlineIncludesTimeSpentOpeningTransport()
    {
        var clock = new ManualTimeProvider();
        var transport = new ControlledByteTransport
        {
            OpenGate = new(TaskCreationOptions.RunContinuationsAsynchronously),
            AutoSynchronize = false
        };
        await using var session = new RadioSession(transport, Parse, new RadioSessionOptions { TimeProvider = clock });
        var connecting = session.ConnectAsync(Command("S;"), Match('S'));
        await transport.OpenEntered.Task;
        clock.Advance(TimeSpan.FromSeconds(1));
        transport.OpenGate.TrySetResult();
        Assert.Equal("S;", await transport.NextWriteAsync());
        clock.Advance(TimeSpan.FromSeconds(4));
        Assert.False(await connecting.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.Equal(SessionState.Faulted, session.State);
    }

    [Fact]
    public async Task StopTracksAndClosesLateOpenRatherThanAllowingAnOrphanedTransport()
    {
        var transport = new ControlledByteTransport
        {
            OpenGate = new(TaskCreationOptions.RunContinuationsAsynchronously),
            IgnoreOpenCancellation = true
        };
        await using var session = new RadioSession(transport, Parse);
        var connecting = session.ConnectAsync(Command("S;"), Match('S'));
        await transport.OpenEntered.Task;
        var stopping = session.StopAsync();
        Assert.False(await connecting.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.False(stopping.IsCompleted);
        transport.OpenGate.TrySetResult();
        await stopping.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.False(transport.IsOpen);
        Assert.Empty(transport.Written);
        Assert.True(transport.CloseCount >= 2);
    }
}
