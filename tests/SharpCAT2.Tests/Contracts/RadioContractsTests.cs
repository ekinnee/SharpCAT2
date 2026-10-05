using System.Runtime.InteropServices;
using SharpCAT2.Core.Radio.Contracts;
using SharpCAT2.ServerLibrary.Radio.Protocols;

namespace SharpCAT2.Tests.Contracts;

public class RadioContractsTests
{
    [Theory]
    [InlineData(RadioOutcome.Succeeded)]
    [InlineData(RadioOutcome.OutcomeUnknown)]
    public void NotSentCannotRepresentSuccessOrUncertainty(RadioOutcome outcome)
    {
        Assert.Throws<ArgumentException>(() => new RadioOperationResult<long>(outcome, CompletionEvidence.NotSent));
    }

    [Fact]
    public void WriteOnlySuccessDoesNotInventObservedState()
    {
        var result = new RadioOperationResult<long>(RadioOutcome.Succeeded, CompletionEvidence.Written);

        Assert.Equal(RadioOutcome.Succeeded, result.Outcome);
        Assert.Equal(CompletionEvidence.Written, result.Evidence);
        Assert.Null(result.Observation);
    }

    [Fact]
    public void UnconfirmedWriteCannotSucceed()
    {
        Assert.Throws<ArgumentException>(() => new RadioOperationResult<long>(
            RadioOutcome.Succeeded, CompletionEvidence.WriteAttempted));
    }

    [Theory]
    [InlineData(CompletionEvidence.NotSent)]
    [InlineData(CompletionEvidence.WriteAttempted)]
    [InlineData(CompletionEvidence.Written)]
    public void ObservationCannotBeClaimedWithoutReplyEvidence(CompletionEvidence evidence)
    {
        var observation = new RadioObservation<long>(14_250_000, DateTimeOffset.UnixEpoch, 1);

        Assert.Throws<ArgumentException>(() => new RadioOperationResult<long>(RadioOutcome.Cancelled, evidence, observation));
    }

    [Fact]
    public void VerifiedReadBackRequiresObservedValue()
    {
        Assert.Throws<ArgumentException>(() => new RadioOperationResult<long>(
            RadioOutcome.Succeeded, CompletionEvidence.ReadBackVerified));
    }

    [Fact]
    public void ReplyObservationPreservesValueTimestampAndConnectionGeneration()
    {
        var timestamp = DateTimeOffset.Parse("2026-10-05T12:00:00+00:00");
        var observation = new RadioObservation<long>(14_250_000, timestamp, 7);
        var result = new RadioOperationResult<long>(RadioOutcome.Succeeded, CompletionEvidence.ReplyReceived, observation);

        Assert.Same(observation, result.Observation);
        Assert.Equal(14_250_000, result.Observation!.Value);
        Assert.Equal(timestamp, result.Observation.ObservedAt);
        Assert.Equal(7, result.Observation.ConnectionGeneration);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ObservationRequiresPositiveConnectionGeneration(long generation)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RadioObservation<long>(14_250_000, DateTimeOffset.UnixEpoch, generation));
    }

    [Fact]
    public void CancellationMetadataSeparatesQueuedCancellationFromUnconfirmedWrite()
    {
        var queued = new RadioOperationResult<long>(RadioOutcome.Cancelled, CompletionEvidence.NotSent);
        var written = new RadioOperationResult<long>(
            RadioOutcome.OutcomeUnknown, CompletionEvidence.Written, diagnostic: "Cancelled after completed write; device effect unconfirmed.");

        Assert.Equal(RadioOutcome.Cancelled, queued.Outcome);
        Assert.Null(queued.Observation);
        Assert.Equal(RadioOutcome.OutcomeUnknown, written.Outcome);
        Assert.Equal(CompletionEvidence.Written, written.Evidence);
        Assert.Null(written.Observation);
        Assert.Contains("unconfirmed", written.Diagnostic!);
    }

    [Fact]
    public void PartialWriteUncertaintyDoesNotClaimCompletedWriteOrObservation()
    {
        // NotSent is deliberately unavailable after write invocation; no bytes completed is not established.
        var result = new RadioOperationResult<long>(RadioOutcome.OutcomeUnknown, CompletionEvidence.WriteAttempted,
            diagnostic: "Transport failed during write; a prefix may have reached the device.");

        Assert.Equal(RadioOutcome.OutcomeUnknown, result.Outcome);
        Assert.Equal(CompletionEvidence.WriteAttempted, result.Evidence);
        Assert.Null(result.Observation);
    }

    [Fact]
    public void CommandAndVerificationBytesAreDefensiveSnapshots()
    {
        byte[] payload = [0x00, 0x80, 0xff];
        byte[] verification = [0xfe, 0x00];
        var command = new CommandSpecification(payload, ResponsePolicy.WriteThenReadBack,
            TimeSpan.FromSeconds(5), "frequency/A", verification);
        payload[0] = 0x01;
        verification[0] = 0x01;

        Assert.Equal(new byte[] { 0x00, 0x80, 0xff }, command.Payload.ToArray());
        Assert.Equal(new byte[] { 0xfe, 0x00 }, command.VerificationPayload!.Value.ToArray());

        Assert.True(MemoryMarshal.TryGetArray(command.Payload, out var exposedPayload));
        exposedPayload.Array![exposedPayload.Offset] = 0x02;
        Assert.True(MemoryMarshal.TryGetArray(command.VerificationPayload.Value, out var exposedVerification));
        exposedVerification.Array![exposedVerification.Offset] = 0x02;

        Assert.Equal(0x00, command.Payload.Span[0]);
        Assert.Equal(0xfe, command.VerificationPayload.Value.Span[0]);
    }

    [Theory]
    [InlineData(ResponsePolicy.ReplyRequired)]
    [InlineData(ResponsePolicy.WriteThenReadBack)]
    public void ReplyPolicyRequiresResponseKey(ResponsePolicy policy)
    {
        Assert.Throws<ArgumentException>(() => new CommandSpecification(new byte[] { 1 }, policy, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void VerificationPayloadIsRequiredOnlyForReadBack()
    {
        Assert.Throws<ArgumentException>(() => new CommandSpecification(
            new byte[] { 1 }, ResponsePolicy.WriteThenReadBack, TimeSpan.FromSeconds(1), "readback"));
        Assert.Throws<ArgumentException>(() => new CommandSpecification(
            new byte[] { 1 }, ResponsePolicy.ReplyRequired, TimeSpan.FromSeconds(1), "reply", new byte[] { 2 }));
        Assert.Throws<ArgumentException>(() => new CommandSpecification(
            new byte[] { 1 }, ResponsePolicy.WriteOnly, TimeSpan.FromSeconds(1), verificationPayload: new byte[] { 2 }));
    }

    [Fact]
    public void CommandRequiresBytesAndPositiveTimeout()
    {
        Assert.Throws<ArgumentException>(() => new CommandSpecification(ReadOnlyMemory<byte>.Empty,
            ResponsePolicy.WriteOnly, TimeSpan.FromSeconds(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CommandSpecification(new byte[] { 1 },
            ResponsePolicy.WriteOnly, TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CommandSpecification(new byte[] { 1 },
            ResponsePolicy.WriteOnly, Timeout.InfiniteTimeSpan));
    }

    [Fact]
    public void RequestsCheckShapeWithoutGuessingManufacturerValues()
    {
        var frequency = new RadioOperationRequest(RadioOperation.SetFrequency, RadioVfo.B, long.MaxValue);
        var mode = new RadioOperationRequest(RadioOperation.SetMode, mode: "future-model-mode");

        Assert.Equal(long.MaxValue, frequency.FrequencyHz);
        Assert.Equal("future-model-mode", mode.Mode);
        Assert.Throws<ArgumentOutOfRangeException>(() => new RadioOperationRequest(RadioOperation.SetFrequency, RadioVfo.A, 0));
        Assert.Throws<ArgumentException>(() => new RadioOperationRequest(RadioOperation.SetMode, mode: " "));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RadioOperationRequest(RadioOperation.GetFrequency, (RadioVfo)99));
        Assert.Throws<ArgumentException>(() => new RadioOperationRequest(RadioOperation.GetFrequency));
        Assert.Throws<ArgumentException>(() => new RadioOperationRequest(RadioOperation.SwapVfo, RadioVfo.A));
        Assert.Throws<ArgumentException>(() => new RadioOperationRequest(RadioOperation.GetFrequency, RadioVfo.A, 1));
        Assert.Throws<ArgumentException>(() => new RadioOperationRequest(RadioOperation.Identify, mode: "USB"));
    }

    [Fact]
    public void ModeOperationsAddressMainRxWithoutVfoSelector()
    {
        var getMode = new RadioOperationRequest(RadioOperation.GetMode);
        var setMode = new RadioOperationRequest(RadioOperation.SetMode, mode: "USB");

        Assert.Null(getMode.Vfo);
        Assert.Null(setMode.Vfo);
        Assert.Equal("USB", setMode.Mode);
        Assert.Throws<ArgumentException>(() => new RadioOperationRequest(RadioOperation.GetMode, RadioVfo.A));
        Assert.Throws<ArgumentException>(() => new RadioOperationRequest(RadioOperation.GetMode, RadioVfo.B));
        Assert.Throws<ArgumentException>(() => new RadioOperationRequest(RadioOperation.SetMode, RadioVfo.A, mode: "USB"));
        Assert.Throws<ArgumentException>(() => new RadioOperationRequest(RadioOperation.SetMode, RadioVfo.B, mode: "USB"));
    }

    [Fact]
    public void CapabilityEvidenceIsPerOperationAndDoesNotPromoteHardwareSupport()
    {
        var capability = new RadioCapability(RadioOperation.SetFrequency, CapabilityEvidence.ProtocolTested, "Independent fixtures.");

        Assert.Equal(RadioOperation.SetFrequency, capability.Operation);
        Assert.Equal(CapabilityEvidence.ProtocolTested, capability.Evidence);
        Assert.NotEqual(CapabilityEvidence.HardwareVerified, capability.Evidence);
    }

    [Fact]
    public void UndefinedEnumValuesAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RadioOperationResult<int>((RadioOutcome)99, CompletionEvidence.NotSent));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RadioOperationResult<int>(RadioOutcome.Cancelled, (CompletionEvidence)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CommandSpecification(new byte[] { 1 }, (ResponsePolicy)99, TimeSpan.FromSeconds(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RadioOperationRequest((RadioOperation)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RadioCapability(RadioOperation.Identify, (CapabilityEvidence)99));
    }

    [Theory]
    [InlineData(SessionState.Connecting)]
    [InlineData(SessionState.Recovering)]
    public void ReadinessRequiresOwnerSynchronization(SessionState from)
    {
        Assert.False(SessionTransitions.IsAllowed(from, SessionState.Ready));
        Assert.True(SessionTransitions.IsAllowed(from, SessionState.Ready, synchronizationCompleted: true));
    }

    [Fact]
    public void FaultedRequiresExplicitReconnect()
    {
        Assert.False(SessionTransitions.IsAllowed(SessionState.Faulted, SessionState.Connecting));
        Assert.True(SessionTransitions.IsAllowed(SessionState.Faulted, SessionState.Connecting, explicitReconnect: true));
        Assert.False(SessionTransitions.IsAllowed(SessionState.Faulted, SessionState.Ready,
            synchronizationCompleted: true, explicitReconnect: true));
    }

    [Fact]
    public void TransitionMatrixIncludesShutdownAndMakesDisposedTerminal()
    {
        var allowed = new HashSet<(SessionState, SessionState)>
        {
            (SessionState.Disconnected, SessionState.Connecting),
            (SessionState.Disconnected, SessionState.Stopping),
            (SessionState.Connecting, SessionState.Ready),
            (SessionState.Connecting, SessionState.Faulted),
            (SessionState.Connecting, SessionState.Stopping),
            (SessionState.Ready, SessionState.Recovering),
            (SessionState.Ready, SessionState.Faulted),
            (SessionState.Ready, SessionState.Stopping),
            (SessionState.Recovering, SessionState.Ready),
            (SessionState.Recovering, SessionState.Faulted),
            (SessionState.Recovering, SessionState.Stopping),
            (SessionState.Faulted, SessionState.Connecting),
            (SessionState.Faulted, SessionState.Stopping),
            (SessionState.Stopping, SessionState.Disconnected),
            (SessionState.Stopping, SessionState.Disposed)
        };

        foreach (var from in Enum.GetValues<SessionState>())
        foreach (var to in Enum.GetValues<SessionState>())
            Assert.Equal(allowed.Contains((from, to)), SessionTransitions.IsAllowed(from, to,
                synchronizationCompleted: true, explicitReconnect: true));
        Assert.False(SessionTransitions.IsAllowed((SessionState)99, SessionState.Stopping));
    }

    [Fact]
    public void FrameResultsRetainIncompleteTailsAndAdvanceOnlyCompleteOrInvalidPrefixes()
    {
        Assert.Equal(0, new FrameParseResult(FrameParseStatus.Incomplete, 0).ConsumedBytes);
        Assert.Equal(2, new FrameParseResult(FrameParseStatus.Invalid, 2).ConsumedBytes);
        var input = new byte[] { 0x80, 0x00 };
        var frame = new FrameParseResult(FrameParseStatus.Complete, 3, input);
        input[0] = 0x01;
        Assert.Equal(new byte[] { 0x80, 0x00 }, frame.Frame.ToArray());

        Assert.Throws<ArgumentOutOfRangeException>(() => new FrameParseResult(FrameParseStatus.Incomplete, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrameParseResult(FrameParseStatus.Complete, 0));
        Assert.Throws<ArgumentException>(() => new FrameParseResult(FrameParseStatus.Complete, 1, new byte[] { 1, 2 }));
        Assert.Throws<ArgumentException>(() => new FrameParseResult(FrameParseStatus.Invalid, 1, new byte[] { 1 }));
    }

    [Fact]
    public void MalformedMatchingReplyIsDistinctFromUnrelatedFrame()
    {
        var unrelated = new ReplyParseResult(ReplyParseStatus.Unrelated);
        var malformed = new ReplyParseResult(ReplyParseStatus.Invalid, diagnostic: "Matching frequency reply has invalid digits.");
        var parsed = new ReplyParseResult(ReplyParseStatus.Valid, 14_250_000L);

        Assert.NotEqual(unrelated.Status, malformed.Status);
        Assert.Null(malformed.Value);
        Assert.Equal(14_250_000L, parsed.Value);
        Assert.Throws<ArgumentException>(() => new ReplyParseResult(ReplyParseStatus.Unrelated, 0L));
        Assert.Throws<ArgumentException>(() => new ReplyParseResult(ReplyParseStatus.Invalid, 0L));
    }
}
