using System.Collections.Concurrent;
using System.IO.Ports;
using Microsoft.Extensions.Logging.Abstractions;
using SharpCAT2.Core.Configuration;
using SharpCAT2.Core.Radio;
using SharpCAT2.Core.Serial;
using SharpCAT2.ServerLibrary;
using SharpCAT2.ServerLibrary.Radio;
using SharpCAT2.ServerLibrary.Radio.Models;
using SharpCAT2.ServerLibrary.Radio.Models.Testing;
using SharpCAT2.ServerLibrary.Serial;
using Xunit;

namespace SharpCAT2.Tests.Radio;

public class LegacySessionRoutingTests
{
    [Fact]
    public async Task LegacyCommandsUseOneSessionWithoutDiscardAndWriteOnlyReturnsEmpty()
    {
        var port = new ScriptPort();
        using var radio = new TestRadio();
        Assert.True(await radio.ConnectAsync(port));
        var responses = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => radio.SendCommandAsync(new RadioCommand("FA;"))));
        Assert.All(responses, response => Assert.Equal("FA00014250000;", response));
        Assert.Equal(string.Empty, await radio.SendCommandAsync(new RadioCommand("ZZ;", expectsResponse: false)));
        Assert.Equal(18, port.Writes);
        Assert.Equal(0, port.Discards);
        Assert.NotNull(radio.Session);
        radio.Disconnect();
        Assert.False(radio.IsConnected);
        Assert.True(await radio.ConnectAsync(port));
        radio.Dispose();
        Assert.Equal(1, port.Disposals);
    }

    [Fact]
    public async Task FailureIsHandledOnceAndDoesNotEnableRawFallback()
    {
        var port = new ScriptPort();
        using var radio = new TestRadio();
        var wrapper = new ResilientRadio(radio);
        Assert.True(await wrapper.ConnectAsync(port));
        port.FailWrites = true;
        Assert.Null(await wrapper.SendCommandAsync(new RadioCommand("SV;")));
        Assert.Equal(2, port.Writes);
        Assert.False(radio.IsConnected);
    }

    [Fact]
    public async Task RejectedModelSwitchLeavesCurrentOwnerUsable()
    {
        using var service = new RadioService(NullLogger<RadioService>.Instance);
        var port = new FakeSerialPort("TEST");
        await service.InitializeRadioAsync(new CommandLineOptions { RadioModel = "DummyRadio" }, port);
        var original = service.ConnectedRadio;
        Assert.False(await service.ChangeRadioAsync("Kenwood TS-2000", port));
        Assert.Same(original, service.ConnectedRadio);
        Assert.True(service.IsRadioConnected);
        Assert.True(await service.TryProcessRadioCommandAsync("FA;"));
    }

    [Fact]
    public async Task ConcurrentDummyCommandsUseTheSessionAndDisposeTransferredPort()
    {
        var port = new FakeSerialPort("TEST");
        using var radio = new DummyRadio();
        Assert.True(await radio.ConnectAsync(port));
        var responses = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => radio.SendCommandAsync(new RadioCommand("ID;"))));
        Assert.All(responses, response => Assert.Equal("ID020;", response));
        Assert.NotNull(radio.Session);
        radio.Dispose();
        Assert.Throws<ObjectDisposedException>(() => port.Open());
    }

    [Fact]
    public async Task DummyRejectsWrappedPhysicalPortsBeforeOpening()
    {
        using var port = new ResilientSerialPort(new RealSerialPort("NOT_A_DEVICE", 9600));
        using var radio = new DummyRadio();
        await Assert.ThrowsAsync<NotSupportedException>(() => radio.ConnectAsync(port));
        Assert.False(port.IsOpen);
    }

    [Fact]
    public async Task ReconnectRejectsDifferentPortEvenWhenNameMatches()
    {
        using var radio = new TestRadio();
        var port = new ScriptPort();
        Assert.True(await radio.ConnectAsync(port));
        using var other = new ScriptPort();
        await Assert.ThrowsAsync<InvalidOperationException>(() => radio.ConnectAsync(other));
        Assert.True(radio.IsConnected);
        Assert.False(other.IsOpen);
    }

    [Fact]
    public async Task FailedSynchronizationDisposesTransferredPortExactlyOnce()
    {
        using var radio = new TestRadio();
        var port = new ScriptPort { FailWrites = true };
        Assert.False(await radio.ConnectAsync(port));
        Assert.Equal(1, port.Disposals);
        radio.Dispose();
        Assert.Equal(1, port.Disposals);
    }

    [Fact]
    public async Task RecognizedInvalidCommandDoesNotFallThroughForResend()
    {
        using var service = new RadioService(NullLogger<RadioService>.Instance);
        await service.InitializeRadioAsync(new CommandLineOptions { RadioModel = "DummyRadio" }, new FakeSerialPort("TEST"));
        Assert.True(await service.TryProcessRadioCommandAsync("FA;FA;"));
        Assert.True(service.IsRadioConnected);
    }

    [Fact]
    public async Task UnknownNoArgumentRawCommandPreservesMutationUncertainty()
    {
        using var radio = new TestRadio();
        var port = new ScriptPort();
        Assert.True(await radio.ConnectAsync(port));
        port.FailWrites = true;
        var result = await radio.ExecuteCommandAsync(new RadioCommand("AB;"));
        Assert.Equal(SharpCAT2.Core.Radio.Contracts.RadioOutcome.OutcomeUnknown, result.Outcome);
        Assert.Equal(2, port.Writes);
    }

    [Fact]
    public async Task FailedDummySwapCannotMutateOutsideSession()
    {
        using var radio = new DummyRadio();
        var before = await radio.GetVfoAsync();
        Assert.False(await radio.SwapVfoAsync());
        Assert.Equal(before, await radio.GetVfoAsync());
    }

    [Fact]
    public async Task UnknownYaesuModeCannotBecomeObservedUsb()
    {
        using var radio = new TestYaesuRadio();
        var port = new ScriptPort { ModeReply = "MD99;" };
        Assert.True(await radio.ConnectAsync(port));
        await Assert.ThrowsAsync<FormatException>(() => radio.GetStatusAsync());
    }

    private sealed class TestYaesuRadio : SharpCAT2.ServerLibrary.Radio.Models.Yaesu.BaseYaesuRadio
    {
        public override string ModelName => "Mode parsing test";
        protected override long ParseFrequency(string response) => 14250000;
    }

    [Fact]
    public async Task DummyWriteOnlyEchoCannotCompleteFollowingDifferentOpcodeQuery()
    {
        using var radio = new DummyRadio();
        Assert.True(await radio.ConnectAsync(new FakeSerialPort("TEST")));
        var calls = Enumerable.Range(0, 16).SelectMany(_ => new[]
        {
            radio.SendCommandAsync(new RadioCommand("MD3;", expectsResponse: false)),
            radio.SendCommandAsync(new RadioCommand("FA;"))
        }).ToArray();
        var responses = await Task.WhenAll(calls);
        for (var i = 0; i < responses.Length; i += 2)
        {
            Assert.Equal(string.Empty, responses[i]);
            Assert.Equal("FA00014074000;", responses[i + 1]);
        }
    }

    [Fact]
    public async Task DummyWriteOnlySameOpcodeEchoIsConsumedBeforeNextQuery()
    {
        using var radio = new DummyRadio();
        Assert.True(await radio.ConnectAsync(new FakeSerialPort("TEST")));
        var first = radio.SendCommandAsync(new RadioCommand("FA00014074000;", expectsResponse: false));
        var second = radio.SendCommandAsync(new RadioCommand("FA00014250000;", expectsResponse: false));
        var read = radio.SendCommandAsync(new RadioCommand("FA;"));
        Assert.Equal(string.Empty, await first);
        Assert.Equal(string.Empty, await second);
        Assert.Equal("FA00014250000;", await read);
    }

    [Fact]
    public async Task FailedSerialAssociationCannotClaimLaterByteOwnedSession()
    {
        using var radio = new TestRadio();
        var failedPort = new ScriptPort { FailWrites = true };
        Assert.False(await radio.ConnectAsync(failedPort));
        Assert.Equal(1, failedPort.Disposals);
        Assert.True(await radio.ConnectAsync(new SharpCAT2.Emulator.FT991AEmulator()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => radio.ConnectAsync(failedPort));
        Assert.True(radio.IsConnected);
        Assert.Equal(1, failedPort.Disposals);
    }

    private sealed class TestRadio : BaseRadio
    {
        public override string ModelName => "Session test";
        public override string Manufacturer => "Kenwood";
    }

    private sealed class ScriptPort : ISerialPort
    {
        private readonly ConcurrentQueue<byte> _incoming = new();
        public bool IsOpen { get; private set; }
        public string PortName => "TEST";
        public int BaudRate => 9600;
        public int BytesToRead => _incoming.Count;
        public int ReadTimeout { get; set; } = 100;
        public int WriteTimeout { get; set; } = 100;
        public int Writes { get; private set; }
        public int Discards { get; private set; }
        public int Disposals { get; private set; }
        public bool FailWrites { get; set; }
        public string ModeReply { get; set; } = "MD2;";
        public event SerialDataReceivedEventHandler? DataReceived { add { } remove { } }
        public void Open() => IsOpen = true;
        public void Close() => IsOpen = false;
        public void Write(string data)
        {
            Writes++;
            if (FailWrites) throw new IOException("Injected partial write failure");
            var response = data switch { "ID;" => "ID020;", "FA;" => "FA00014250000;", "MD;" => ModeReply, _ => null };
            if (response is not null) foreach (var b in System.Text.Encoding.ASCII.GetBytes(response)) _incoming.Enqueue(b);
        }
        public int Read(byte[] buffer, int offset, int count)
        {
            var n = 0;
            while (n < count && _incoming.TryDequeue(out var b)) buffer[offset + n++] = b;
            return n;
        }
        public void WriteLine(string data) => throw new InvalidOperationException("Session must preserve exact bytes");
        public string ReadExisting() => throw new InvalidOperationException("Competing reader");
        public string ReadLine() => throw new InvalidOperationException("Competing reader");
        public void DiscardInBuffer() { Discards++; }
        public void DiscardOutBuffer() { Discards++; }
        public void Dispose() { Disposals++; Close(); }
    }
}
