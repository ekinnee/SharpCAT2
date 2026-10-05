using System.Text;
using SharpCAT2.Core.Radio;
using SharpCAT2.Core.Radio.Contracts;
using SharpCAT2.Emulator;
using SharpCAT2.ServerLibrary.Radio;
using SharpCAT2.ServerLibrary.Radio.Models.Yaesu;
using Xunit;

namespace SharpCAT2.Tests.Radio;

public class FT991AIntegrationTests
{
    private static string Text(EmulatorWrite write) => Encoding.ASCII.GetString(write.Payload.AsSpan());

    [Fact]
    public async Task ProfileAndIndependentEmulatorComposeThroughPublicLegacyAndTypedApis()
    {
        var transport = new FT991AEmulator();
        using var radio = new YaesuFT991A();
        Assert.True(await radio.ConnectAsync(transport));
        Assert.Equal(new[] { "AI0;", "AI;", "ID;" }, transport.Writes.Select(Text));
        Assert.True(await radio.SetFrequencyAsync(144_000_000));
        Assert.Equal(144_000_000, transport.State.FrequencyAHz);
        Assert.Equal(CompletionEvidence.ReadBackVerified, radio.LastOperationResult!.Evidence);
        var frequency = await radio.ExecuteAsync(new(RadioOperation.GetFrequency, RadioVfo.B));
        Assert.Equal(7_074_000L, frequency.Observation!.Value);
        Assert.True(await radio.SetModeAsync("C4FM"));
        Assert.Equal('E', transport.State.MainRxModeCode);
        Assert.True(await radio.SwapVfoAsync());
        Assert.Equal(7_074_000L, transport.State.FrequencyAHz);
        Assert.Equal(144_000_000L, transport.State.FrequencyBHz);
        Assert.Equal(CompletionEvidence.ReadBackVerified, radio.LastOperationResult!.Evidence);
        Assert.Equal(new[] { "FA;", "FB;", "SV;", "FA;", "FB;" }, transport.Writes.TakeLast(5).Select(Text));
        var status = await radio.GetStatusAsync();
        Assert.Equal(7_074_000L, status.Frequency);
        Assert.Equal("C4FM", status.Mode);
        Assert.Equal("Unknown", status.CurrentVfo);
        var projection = RadioStatusInfo.FromRadioStatus(radio, status);
        var display = new SharpCAT2.ServerLibrary.CommandDisplayService().FormatRadioStatus(projection);
        Assert.Contains("Transmitting: Unknown", display);
        Assert.Contains("Power: Unknown", display);
        Assert.False(projection.IsPowerStateObserved);
        Assert.False(projection.IsTransmitStateObserved);
        Assert.DoesNotContain("TX=", await radio.GetUniversalStatusStringAsync());
        Assert.All(radio.Capabilities, capability => Assert.Equal(CapabilityEvidence.ProtocolTested, capability.Evidence));
    }

    [Fact]
    public async Task InvalidAndUnsupportedOperationsSendNothingAndCannotReturnPlaceholderState()
    {
        var transport = new FT991AEmulator();
        using var radio = new YaesuFT991A();
        Assert.True(await radio.ConnectAsync(transport));
        var count = transport.WriteCount;
        Assert.False(await radio.SetFrequencyAsync(29_999));
        Assert.False(await radio.SetFrequencyAsync(470_000_001));
        Assert.False(await radio.SetModeAsync("NOT-A-MODE"));
        Assert.Null(await radio.SendCommandAsync(new RadioCommand("TX1;", expectsResponse: false)));
        await Assert.ThrowsAsync<NotSupportedException>(() => radio.SetVfoAsync("B"));
        await Assert.ThrowsAsync<NotSupportedException>(() => radio.GetPowerAsync());
        Assert.Equal(count, transport.WriteCount);
        Assert.Equal(RadioOutcome.NotSupported, radio.LastOperationResult!.Outcome);
    }

    [Fact]
    public async Task FragmentedRepliesAndUnsolicitedFramesDoNotCorruptObservedReads()
    {
        var transport = new FT991AEmulator();
        using var radio = new YaesuFT991A();
        Assert.True(await radio.ConnectAsync(transport));
        transport.FragmentNextReply(1, 2, 3);
        var unsolicited = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        radio.UnsolicitedFrame += frame => unsolicited.TrySetResult(Encoding.ASCII.GetString(frame.Span));
        await transport.InjectUnsolicitedAsync(Encoding.ASCII.GetBytes("ZZunexpected;"));
        Assert.Equal("ZZunexpected;", await unsolicited.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        var result = await radio.ExecuteAsync(new(RadioOperation.GetFrequency, RadioVfo.A));
        Assert.Equal(RadioOutcome.Succeeded, result.Outcome);
        Assert.Equal(14_074_000L, result.Observation!.Value);
    }

    [Fact]
    public async Task SetterMismatchReportsRealObservationWithoutClaimingSuccess()
    {
        var transport = new FT991AEmulator();
        using var radio = new YaesuFT991A();
        Assert.True(await radio.ConnectAsync(transport));
        transport.OverrideNextReply(Encoding.ASCII.GetBytes("FA014074000;"));
        Assert.False(await radio.SetFrequencyAsync(14_250_000));
        Assert.Equal(RadioOutcome.ProtocolError, radio.LastOperationResult!.Outcome);
        Assert.Equal(CompletionEvidence.ReplyReceived, radio.LastOperationResult.Evidence);
        Assert.Equal(14_074_000L, radio.LastOperationResult.Observation!.Value);
    }

    [Fact]
    public async Task CancelledMutationIsUnknownAndRequiresExplicitReconnectWithoutReplay()
    {
        var transport = new FT991AEmulator();
        using var radio = new YaesuFT991A();
        Assert.True(await radio.ConnectAsync(transport));
        transport.HoldNextReply();
        using var cancellation = new CancellationTokenSource();
        var operation = radio.ExecuteAsync(new(RadioOperation.SetFrequency, RadioVfo.A, 14_250_000), cancellation.Token);
        await transport.WaitForWriteAsync(5).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        Assert.Equal(RadioOutcome.OutcomeUnknown, (await operation).Outcome);
        Assert.False(radio.IsConnected);
        Assert.Equal(RadioOutcome.NotConnected, (await radio.ExecuteAsync(new(RadioOperation.GetFrequency, RadioVfo.A))).Outcome);
        Assert.True(await radio.ReconnectAsync());
        Assert.Equal(0, await transport.ReleaseHeldRepliesAsync());
        Assert.Equal(1, transport.Writes.Count(write => Text(write) == "FA014250000;"));
        var fresh = await radio.ExecuteAsync(new(RadioOperation.GetFrequency, RadioVfo.A));
        Assert.Equal(14_250_000L, fresh.Observation!.Value);
        Assert.True(fresh.Observation.ConnectionGeneration > 1);
    }

    [Fact]
    public async Task EqualFrequencySwapIsSentOnceButItsEffectCannotBeProven()
    {
        var transport = new FT991AEmulator(14_074_000, 14_074_000);
        using var radio = new YaesuFT991A();
        Assert.True(await radio.ConnectAsync(transport));
        Assert.False(await radio.SwapVfoAsync());
        Assert.Equal(RadioOutcome.OutcomeUnknown, radio.LastOperationResult!.Outcome);
        Assert.IsType<VfoFrequencies>(radio.LastOperationResult.Observation!.Value);
        Assert.Equal(1, transport.Writes.Count(write => Text(write) == "SV;"));
    }

    [Fact]
    public async Task IdentityMismatchNeverMakesSessionReady()
    {
        var transport = new FT991AEmulator();
        using var radio = new YaesuFT991A();
        transport.OverrideNextReply(Encoding.ASCII.GetBytes("AI0;"));
        transport.OverrideNextReply(Encoding.ASCII.GetBytes("ID0650;"));
        Assert.False(await radio.ConnectAsync(transport));
        Assert.False(radio.IsConnected);
    }

    [Fact]
    public async Task PassiveWrapperForwardsTypedOperationsToTheSameOwner()
    {
        var transport = new FT991AEmulator();
        var owner = new YaesuFT991A();
        using var wrapper = new ResilientRadio(owner);
        Assert.True(await owner.ConnectAsync(transport));
        var result = await wrapper.ExecuteAsync(new(RadioOperation.SetMode, mode: "USB"));
        Assert.Equal(CompletionEvidence.ReadBackVerified, result.Evidence);
        Assert.Equal(new[] { "MD02;", "MD0;" }, transport.Writes.TakeLast(2).Select(Text));
    }

    [Fact]
    public async Task SwapPrerequisitesAndReadBackRemainAtomicAgainstConcurrentCaller()
    {
        var transport = new FT991AEmulator();
        using var radio = new YaesuFT991A();
        Assert.True(await radio.ConnectAsync(transport));
        transport.HoldNextReply();
        var swap = radio.ExecuteAsync(new(RadioOperation.SwapVfo));
        await transport.WaitForWriteAsync(4).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        var mode = radio.ExecuteAsync(new(RadioOperation.SetMode, mode: "AM"));
        Assert.Equal(4, transport.WriteCount);
        await transport.ReleaseHeldRepliesAsync();
        Assert.Equal(RadioOutcome.Succeeded, (await swap).Outcome);
        Assert.Equal(RadioOutcome.Succeeded, (await mode).Outcome);
        Assert.Equal(new[] { "FA;", "FB;", "SV;", "FA;", "FB;", "MD05;", "MD0;" },
            transport.Writes.Skip(3).Select(Text));
    }

    [Fact]
    public async Task MalformedMatchingReadFailsAndCannotBecomeObservedState()
    {
        var transport = new FT991AEmulator();
        using var radio = new YaesuFT991A();
        Assert.True(await radio.ConnectAsync(transport));
        transport.OverrideNextReply(Encoding.ASCII.GetBytes("FA123;"));
        var result = await radio.ExecuteAsync(new(RadioOperation.GetFrequency, RadioVfo.A));
        Assert.Equal(RadioOutcome.ProtocolError, result.Outcome);
        Assert.Null(result.Observation);
        Assert.False(radio.IsConnected);
    }

    [Fact]
    public void CatalogSeparatesProtocolTestedPreviewFromExperimentalAndUnavailableModels()
    {
        using var service = new SharpCAT2.ServerLibrary.RadioService(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SharpCAT2.ServerLibrary.RadioService>.Instance);
        var preview = service.GetRadioInfo("Yaesu FT-991A")!;
        Assert.All(preview.Capabilities, capability => Assert.Equal(CapabilityEvidence.ProtocolTested, capability.Evidence));
        Assert.DoesNotContain("TransmitStatus", preview.SupportedFeatures);
        Assert.All(service.GetRadioInfo("Kenwood TS-2000")!.Capabilities,
            capability => Assert.Equal(CapabilityEvidence.Experimental, capability.Evidence));
        Assert.All(service.GetRadioInfo("Icom IC-7300")!.Capabilities,
            capability => Assert.Equal(CapabilityEvidence.Unavailable, capability.Evidence));
    }

    [Fact]
    public async Task LegacyDeadlineIsValidatedAndLimitsTheWholeAdaptedOperation()
    {
        var transport = new FT991AEmulator();
        using var radio = new YaesuFT991A();
        Assert.True(await radio.ConnectAsync(transport));
        var count = transport.WriteCount;
        var invalid = await radio.ExecuteCommandAsync(new RadioCommand("FA;", timeoutMs: 0));
        Assert.Equal(RadioOutcome.InvalidArgument, invalid.Outcome);
        Assert.Equal(count, transport.WriteCount);
        transport.HoldNextReply();
        var timed = await radio.ExecuteCommandAsync(new RadioCommand("FA;", timeoutMs: 50)).WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(RadioOutcome.TimedOut, timed.Outcome);
        Assert.False(radio.IsConnected);
    }
}
