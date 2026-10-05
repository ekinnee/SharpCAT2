using System.Text;
using SharpCAT2.Core.Radio.Contracts;
using SharpCAT2.ServerLibrary.Radio.Protocols;
using SharpCAT2.ServerLibrary.Radio.Session;

namespace SharpCAT2.Tests.Radio.Session;

public class CompositeRadioSessionTests
{
    private static CommandSpecification Command(string text, ResponsePolicy policy = ResponsePolicy.ReplyRequired) =>
        new(Encoding.ASCII.GetBytes(text), policy, TimeSpan.FromSeconds(5), policy == ResponsePolicy.WriteOnly ? null : text[..1]);

    private static FrameParseResult Parse(ReadOnlyMemory<byte> buffer)
    {
        var end = buffer.Span.IndexOf((byte)';');
        return end < 0 ? new(FrameParseStatus.Incomplete, 0) :
            new(FrameParseStatus.Complete, end + 1, buffer[..(end + 1)]);
    }

    private static Func<ReadOnlyMemory<byte>, ReplyParseResult> Match(string opcode) => bytes =>
    {
        var text = Encoding.ASCII.GetString(bytes.Span);
        if (!text.StartsWith(opcode, StringComparison.Ordinal)) return new(ReplyParseStatus.Unrelated);
        return long.TryParse(text[opcode.Length..^1], out var number)
            ? new(ReplyParseStatus.Valid, number) : new(ReplyParseStatus.Invalid);
    };

    private static SessionTransactionStep Step(string text, bool mutation = false) =>
        new(Command(text, mutation ? ResponsePolicy.WriteOnly : ResponsePolicy.ReplyRequired),
            Match(text[..^1]), mutation);

    private static SessionTransactionStep[] Swap() => [Step("FA;"), Step("FB;"), Step("SV;", true), Step("FA;"), Step("FB;")];

    private static SessionTransactionConclusion VerifySwap(IReadOnlyList<object?> values)
    {
        var beforeA = (long)values[0]!;
        var beforeB = (long)values[1]!;
        var after = ((long)values[3]!, (long)values[4]!);
        return new(beforeA == beforeB ? RadioOutcome.OutcomeUnknown :
            after == (beforeB, beforeA) ? RadioOutcome.Succeeded : RadioOutcome.ProtocolError, after);
    }

    private static async Task Connect(RadioSession session, ControlledByteTransport transport)
    {
        Assert.True(await session.ConnectAsync(Command("S;"), Match("S")).WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.Equal("S;", await transport.NextWriteAsync());
    }

    [Fact]
    public async Task SwapOwnsQueueUntilBothObservedPostconditionsAreVerified()
    {
        var transport = new ControlledByteTransport();
        await using var session = new RadioSession(transport, Parse);
        await Connect(session, transport);
        var swap = session.ExecuteTransactionAsync(Swap(), TimeSpan.FromSeconds(2), VerifySwap);
        Assert.Equal("FA;", await transport.NextWriteAsync());
        var outside = session.ExecuteAsync(Command("N;", ResponsePolicy.WriteOnly), Match("N"));
        transport.Send("FA10;");
        Assert.Equal("FB;", await transport.NextWriteAsync());
        transport.Send("FB20;");
        Assert.Equal("SV;", await transport.NextWriteAsync());
        Assert.Equal("FA;", await transport.NextWriteAsync());
        transport.Send("FA20;");
        Assert.Equal("FB;", await transport.NextWriteAsync());
        Assert.False(outside.IsCompleted);
        transport.Send("FB10;");
        var result = await swap.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(RadioOutcome.Succeeded, result.Outcome);
        Assert.Equal(CompletionEvidence.ReadBackVerified, result.Evidence);
        Assert.Equal((20L, 10L), result.Observation!.Value);
        Assert.Equal("N;", await transport.NextWriteAsync());
        await outside;
        Assert.Equal(new[] { "S;", "FA;", "FB;", "SV;", "FA;", "FB;", "N;" }, transport.Written);
    }

    [Fact]
    public async Task CancellationDuringPrerequisiteQueryDoesNotClaimMutationUncertainty()
    {
        var transport = new ControlledByteTransport();
        await using var session = new RadioSession(transport, Parse);
        await Connect(session, transport);
        using var cancellation = new CancellationTokenSource();
        var swap = session.ExecuteTransactionAsync(Swap(), TimeSpan.FromSeconds(2), VerifySwap, cancellation.Token);
        await transport.NextWriteAsync();
        transport.Send("FA10;");
        Assert.Equal("FB;", await transport.NextWriteAsync());
        var queued = session.ExecuteAsync(Command("N;", ResponsePolicy.WriteOnly), Match("N"));
        cancellation.Cancel();
        var result = await swap.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(RadioOutcome.Cancelled, result.Outcome);
        Assert.Equal(CompletionEvidence.Written, result.Evidence);
        Assert.Equal(RadioOutcome.NotConnected, (await queued).Outcome);
        Assert.DoesNotContain("SV;", transport.Written);
        Assert.DoesNotContain("N;", transport.Written);
    }

    [Fact]
    public async Task OverallDeadlineAfterSwapPreservesUncertaintyAndDoesNotReplay()
    {
        var clock = new ManualTimeProvider();
        var transport = new ControlledByteTransport();
        await using var session = new RadioSession(transport, Parse, new RadioSessionOptions { TimeProvider = clock });
        await Connect(session, transport);
        var swap = session.ExecuteTransactionAsync(Swap(), TimeSpan.FromSeconds(2), VerifySwap);
        await transport.NextWriteAsync();
        transport.Send("FA10;");
        await transport.NextWriteAsync();
        transport.Send("FB20;");
        Assert.Equal("SV;", await transport.NextWriteAsync());
        Assert.Equal("FA;", await transport.NextWriteAsync());
        clock.Advance(TimeSpan.FromSeconds(2));
        var result = await swap.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(RadioOutcome.OutcomeUnknown, result.Outcome);
        Assert.Equal(SessionState.Faulted, session.State);
        Assert.Equal(1, transport.Written.Count(command => command == "SV;"));
        Assert.Equal(1, transport.Written.Count(command => command == "FB;"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CapturedBatchAndPartialTailCannotCompleteLaterStepInSameOperation(bool fragmented)
    {
        var transport = new ControlledByteTransport();
        await using var session = new RadioSession(transport, Parse);
        await Connect(session, transport);
        var stale = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.UnsolicitedFrame += frame => stale.TrySetResult(Encoding.ASCII.GetString(frame.Span));
        var operation = session.ExecuteTransactionAsync([Step("R;"), Step("R;")], TimeSpan.FromSeconds(2),
            values => new(RadioOutcome.Succeeded, ((long)values[0]!, (long)values[1]!)));
        await transport.NextWriteAsync();
        transport.Send(fragmented ? "R1;R" : "R1;R99;");
        Assert.Equal("R;", await transport.NextWriteAsync());
        if (fragmented) transport.Send("99;");
        Assert.Equal("R99;", await stale.Task.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.False(operation.IsCompleted);
        transport.Send("R2;");
        var result = await operation.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal((1L, 2L), result.Observation!.Value);
        Assert.Equal(CompletionEvidence.ReplyReceived, result.Evidence);
    }

    [Theory]
    [InlineData(10, 10, 10, 10, RadioOutcome.OutcomeUnknown)]
    [InlineData(10, 20, 10, 20, RadioOutcome.ProtocolError)]
    public async Task EqualAndUnchangedObservedFrequenciesDoNotVerifySwap(int beforeA, int beforeB,
        int afterA, int afterB, RadioOutcome expected)
    {
        var transport = new ControlledByteTransport();
        await using var session = new RadioSession(transport, Parse);
        await Connect(session, transport);
        var swap = session.ExecuteTransactionAsync(Swap(), TimeSpan.FromSeconds(2), VerifySwap);
        await transport.NextWriteAsync(); transport.Send($"FA{beforeA};");
        await transport.NextWriteAsync(); transport.Send($"FB{beforeB};");
        Assert.Equal("SV;", await transport.NextWriteAsync());
        await transport.NextWriteAsync(); transport.Send($"FA{afterA};");
        await transport.NextWriteAsync(); transport.Send($"FB{afterB};");
        var result = await swap.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(expected, result.Outcome);
        Assert.Equal(((long)afterA, (long)afterB), result.Observation!.Value);
        Assert.Equal(CompletionEvidence.ReplyReceived, result.Evidence);
        Assert.Equal(1, transport.Written.Count(command => command == "SV;"));
    }

    private static SessionTransactionStep[] Startup() =>
    [
        new(Command("AI0;", ResponsePolicy.WriteOnly), Match("AI"), true, TimeSpan.FromMilliseconds(100)),
        new(Command("AI;"), bytes => Match("AI")(bytes) is { Status: ReplyParseStatus.Valid, Value: 0L }
            ? new(ReplyParseStatus.Valid, false) : new(ReplyParseStatus.Invalid)),
        new(Command("ID;"), bytes => Encoding.ASCII.GetString(bytes.Span) == "ID0670;"
            ? new(ReplyParseStatus.Valid, "ID0670;") : new(ReplyParseStatus.Invalid))
    ];

    [Fact]
    public async Task StartupQuietIntervalStartsAfterAiDisableWriteCompletes()
    {
        var clock = new ManualTimeProvider();
        var writeFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = new ControlledByteTransport { AutoSynchronize = false };
        transport.OnWrite = async (bytes, token) =>
        {
            if (Encoding.ASCII.GetString(bytes) == "AI0;")
            {
                await writeFinished.Task.WaitAsync(token);
            }
        };
        await using var session = new RadioSession(transport, Parse,
            new RadioSessionOptions { TimeProvider = clock, MaxFrameBytes = 16 });
        var connecting = session.ConnectAsync(Startup(), TimeSpan.FromSeconds(2));
        Assert.Equal("AI0;", await transport.NextWriteAsync());
        clock.Advance(TimeSpan.FromMilliseconds(500));
        Assert.Equal(SessionState.Connecting, session.State);
        Assert.False(connecting.IsCompleted);
        writeFinished.TrySetResult();
        await clock.WaitForTimerAsync(TimeSpan.FromMilliseconds(100));
        clock.Advance(TimeSpan.FromMilliseconds(99));
        Assert.Equal(new[] { "AI0;" }, transport.Written);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal("AI;", await transport.NextWriteAsync());
        transport.Send("AI0;");
        Assert.Equal("ID;", await transport.NextWriteAsync());
        Assert.Equal(SessionState.Connecting, session.State);
        transport.Send("ID0670;");
        Assert.True(await connecting.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.Equal(SessionState.Ready, session.State);
        Assert.Equal(1, transport.MaximumReaders);
    }

    [Fact]
    public async Task StartupDrainsOversizedJunkDuringAiDisableWriteWithoutAnotherReader()
    {
        var transport = new ControlledByteTransport { AutoSynchronize = false };
        var writeFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var junk = new string('x', 100) + "AI1;ID9999;AI";
        transport.OnWrite = async (bytes, token) =>
        {
            if (Encoding.ASCII.GetString(bytes) == "AI0;")
            {
                transport.Send(junk);
                await writeFinished.Task.WaitAsync(token);
            }
        };
        await using var session = new RadioSession(transport, Parse, new RadioSessionOptions { MaxFrameBytes = 16 });
        var connecting = session.ConnectAsync(Startup(), TimeSpan.FromSeconds(2));
        Assert.Equal("AI0;", await transport.NextWriteAsync());
        await transport.WaitForReadBytesAsync(junk.Length);
        Assert.Equal(SessionState.Connecting, session.State);
        writeFinished.TrySetResult();
        Assert.Equal("AI;", await transport.NextWriteAsync());
        transport.Send("AI0;");
        Assert.Equal("ID;", await transport.NextWriteAsync());
        transport.Send("ID0670;");
        Assert.True(await connecting.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.Equal(1, transport.MaximumReaders);
    }

    [Theory]
    [InlineData("AI1;", null)]
    [InlineData("AI0;", "ID9999;")]
    public async Task StartupCannotPublishReadyWithoutDisabledAutoInformationAndExactIdentity(string ai, string? id)
    {
        var clock = new ManualTimeProvider();
        var transport = new ControlledByteTransport { AutoSynchronize = false };
        await using var session = new RadioSession(transport, Parse, new RadioSessionOptions { TimeProvider = clock });
        var connecting = session.ConnectAsync(Startup(), TimeSpan.FromSeconds(2));
        Assert.Equal("AI0;", await transport.NextWriteAsync());
        await clock.WaitForTimerAsync(TimeSpan.FromMilliseconds(100));
        clock.Advance(TimeSpan.FromMilliseconds(100));
        Assert.Equal("AI;", await transport.NextWriteAsync());
        transport.Send(ai);
        if (id is not null) { Assert.Equal("ID;", await transport.NextWriteAsync()); transport.Send(id); }
        Assert.False(await connecting.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.Equal(SessionState.Faulted, session.State);
        if (id is null) Assert.DoesNotContain("ID;", transport.Written);
    }

    [Fact]
    public async Task CompositeAdmissionBoundsEntirePayloadAndNumberOfSteps()
    {
        var transport = new ControlledByteTransport();
        await using var session = new RadioSession(transport, Parse,
            new RadioSessionOptions { MaxQueuedBytes = 5, MaxTransactionSteps = 2 });
        await Connect(session, transport);
        Assert.Equal(RadioOutcome.Busy,
            (await session.ExecuteTransactionAsync([Step("FA;"), Step("FB;")], TimeSpan.FromSeconds(2))).Outcome);
        Assert.Equal(RadioOutcome.InvalidArgument,
            (await session.ExecuteTransactionAsync([Step("R;"), Step("R;"), Step("R;")], TimeSpan.FromSeconds(2))).Outcome);
        Assert.Equal(new[] { "S;" }, transport.Written);
    }
}
