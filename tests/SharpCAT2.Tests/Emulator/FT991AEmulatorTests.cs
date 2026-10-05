using System.Text;
using System.Text.Json;
using SharpCAT2.Core.Serial;
using SharpCAT2.Emulator;
using Xunit;

namespace SharpCAT2.Tests.Emulator;

public sealed class FT991AEmulatorTests
{
    [Fact]
    public async Task IdentityFrequencyModeAndSwapUseHandWrittenFixtureBytes()
    {
        await using var emulator = new FT991AEmulator(initialFrequencyAHz: 14_250_000);
        await emulator.OpenAsync();

        await AssertQueryMatchesFixtureAsync(emulator, "identity-ft991a");
        await AssertQueryMatchesFixtureAsync(emulator, "frequency-a-read-example");

        var lowerBoundary = ReadFixture("frequency-a-lower-bound-set");
        await SendNoReplyAsync(emulator, lowerBoundary.Request);
        Assert.Equal(30_000, emulator.State.FrequencyAHz);
        await AssertQueryAsync(emulator, lowerBoundary.VerificationRequest!, lowerBoundary.VerificationResponse!);

        var upperBoundary = ReadFixture("frequency-b-upper-bound-set");
        await SendNoReplyAsync(emulator, upperBoundary.Request);
        Assert.Equal(470_000_000, emulator.State.FrequencyBHz);
        await AssertQueryAsync(emulator, upperBoundary.VerificationRequest!, upperBoundary.VerificationResponse!);

        await AssertQueryMatchesFixtureAsync(emulator, "mode-query-usb-main-rx");
        var modeSet = ReadFixture("mode-set-c4fm");
        await SendNoReplyAsync(emulator, modeSet.Request);
        Assert.Equal('E', emulator.State.MainRxModeCode);
        await AssertQueryAsync(emulator, modeSet.VerificationRequest!, modeSet.VerificationResponse!);

        emulator.SetExternalFrequencies(7_100_000, 14_250_000);
        var swap = ReadFixture("swap-no-reply-read-back");
        await SendNoReplyAsync(emulator, swap.Request);
        Assert.Equal(14_250_000, emulator.State.FrequencyAHz);
        Assert.Equal(7_100_000, emulator.State.FrequencyBHz);
        for (var index = 0; index < swap.VerificationRequests!.Length; index++)
        {
            await AssertQueryAsync(
                emulator,
                swap.VerificationRequests[index],
                swap.VerificationResponses![index]);
        }
    }

    [Fact]
    public async Task FrequencyBReadAndMalformedFrequencyCommandsAreHandledWithoutFabrication()
    {
        await using var emulator = new FT991AEmulator();
        await emulator.OpenAsync();
        await AssertQueryAsync(emulator, "FB;", "FB007074000;");

        var invalidRange = ReadFixture("frequency-outside-range-nine-digits");
        await SendNoReplyAsync(emulator, invalidRange.Request);
        Assert.Equal(14_074_000, emulator.State.FrequencyAHz);

        foreach (var id in new[] { "frequency-short-width", "frequency-long-width", "frequency-nondigit" })
        {
            var malformed = ReadFixture(id);
            await SendNoReplyAsync(emulator, malformed.Request);
            Assert.Equal(14_074_000, emulator.State.FrequencyAHz);
        }

        var partial = ReadFixture("frequency-incomplete-missing-terminator");
        var before = emulator.WriteCount;
        await emulator.WriteAsync(Encoding.ASCII.GetBytes(partial.Request));
        await emulator.WaitForWriteAsync(before + 1);
        Assert.Equal(0, emulator.PendingReadByteCount);
        Assert.Equal(14_074_000, emulator.State.FrequencyAHz);

        await emulator.WriteAsync(Encoding.ASCII.GetBytes(";"));
        await emulator.WaitForWriteAsync(before + 2);
        Assert.Equal(0, emulator.PendingReadByteCount);
        Assert.Equal(14_250_000, emulator.State.FrequencyAHz);
    }

    [Fact]
    public async Task AutoInformationQueryAndSetterHaveManualReplyBehavior()
    {
        await using var emulator = new FT991AEmulator();
        await emulator.OpenAsync();
        await AssertQueryAsync(emulator, "AI;", "AI0;");

        await SendNoReplyAsync(emulator, "AI1;");
        Assert.True(emulator.State.AutoInformationEnabled);
        await AssertQueryAsync(emulator, "AI;", "AI1;");

        await emulator.WriteAsync(Encoding.ASCII.GetBytes("FA014250000;"));
        await emulator.WaitForWriteAsync(emulator.WriteCount);
        Assert.Equal("FA014250000;", await ReadAsciiAsync(emulator));

        await SendNoReplyAsync(emulator, "AI0;");
        Assert.False(emulator.State.AutoInformationEnabled);
        Assert.Equal(0, emulator.PendingReadByteCount);
        await AssertQueryAsync(emulator, "AI;", "AI0;");
    }

    [Fact]
    public async Task FragmentationSuppressionOverrideAndHeldReplyAreDeterministic()
    {
        await using var emulator = new FT991AEmulator(initialFrequencyAHz: 14_250_000);
        await emulator.OpenAsync();

        emulator.FragmentNextReply(2, 1, 2);
        await emulator.WriteAsync(Encoding.ASCII.GetBytes("ID;"));
        await emulator.WaitForWriteAsync(1);
        Assert.Equal(new[] { "ID", "0", "67", "0;" }, await ReadFragmentsAsync(emulator, 4));

        emulator.SuppressNextReply();
        await SendNoReplyAsync(emulator, "ID;");
        Assert.Equal(0, emulator.PendingReadByteCount);
        await AssertQueryAsync(emulator, "ID;", "ID0670;");

        var unknownMode = ReadFixture("mode-unknown-caller-setter");
        await SendNoReplyAsync(emulator, unknownMode.Request);
        Assert.Equal('2', emulator.State.MainRxModeCode);

        var unknownDeviceReply = ReadFixture("mode-unknown-device-reply");
        emulator.OverrideNextReply(Encoding.ASCII.GetBytes(unknownDeviceReply.Response!));
        await AssertQueryAsync(emulator, unknownDeviceReply.Request, unknownDeviceReply.Response!);

        emulator.HoldNextReply();
        var expectedWriteCount = emulator.WriteCount + 1;
        await emulator.WriteAsync(Encoding.ASCII.GetBytes("FA;"));
        await emulator.WaitForWriteAsync(expectedWriteCount);
        Assert.Equal(1, emulator.HeldReplyCount);
        Assert.Equal(0, emulator.PendingReadByteCount);
        Assert.Equal(1, await emulator.ReleaseHeldRepliesAsync());
        Assert.Equal("FA014250000;", await ReadAsciiAsync(emulator));

        emulator.SilenceReplies = true;
        await SendNoReplyAsync(emulator, "ID;");
        Assert.Equal(0, emulator.PendingReadByteCount);
    }

    [Fact]
    public async Task QueuedOverridesCanScriptIdentityHandshakeBeforeOpen()
    {
        await using var emulator = new FT991AEmulator();
        emulator.OverrideNextReply(Encoding.ASCII.GetBytes("AI0;"));
        emulator.OverrideNextReply(Encoding.ASCII.GetBytes("ID0650;"));
        await emulator.OpenAsync();

        await AssertQueryAsync(emulator, "AI;", "AI0;");
        await AssertQueryAsync(emulator, "ID;", "ID0650;");
    }

    [Fact]
    public async Task UnsolicitedBytesCanBeInjectedInChosenFragments()
    {
        await using var emulator = new FT991AEmulator();
        await emulator.OpenAsync();

        await emulator.InjectUnsolicitedAsync(Encoding.ASCII.GetBytes("FB007100000;"), new int[] { 1, 3, 2 });

        Assert.Equal(new[] { "F", "B00", "71", "00000;" }, await ReadFragmentsAsync(emulator, 4));
        Assert.Equal(0, emulator.WriteCount);
    }

    [Fact]
    public async Task WritesAreImmutableSnapshotsAndWaitUsesCumulativeCount()
    {
        await using var emulator = new FT991AEmulator();
        await emulator.OpenAsync();
        var wait = emulator.WaitForWriteAsync(1).AsTask();

        await emulator.WriteAsync(Encoding.ASCII.GetBytes("ID;"));
        var snapshot = await wait;
        var payload = snapshot[0].Payload;
        payload[0] = (byte)'X';

        Assert.Equal("ID;", Encoding.ASCII.GetString(emulator.Writes[0].Payload));
        Assert.Equal(1L, emulator.Writes[0].Sequence);
        Assert.Equal(emulator.Generation, emulator.Writes[0].Generation);
    }

    [Fact]
    public async Task ReopenPersistsRadioStateAndDoesNotLeakOldHeldReplies()
    {
        await using var emulator = new FT991AEmulator();
        await emulator.OpenAsync();
        emulator.SetExternalFrequencies(14_250_000, 7_100_000);
        emulator.HoldNextReply();
        await emulator.WriteAsync(Encoding.ASCII.GetBytes("ID;"));
        await emulator.WaitForWriteAsync(1);
        Assert.Equal(1, emulator.HeldReplyCount);
        var oldGeneration = emulator.Generation;

        emulator.Disconnect();
        Assert.False(emulator.IsOpen);
        Assert.Equal(0, emulator.HeldReplyCount);
        Assert.Equal(0, await emulator.ReadAsync(new byte[8]));

        await emulator.OpenAsync();
        Assert.True(emulator.Generation > oldGeneration);
        Assert.Equal(0, await emulator.ReleaseHeldRepliesAsync());
        await AssertQueryAsync(emulator, "FA;", "FA014250000;");
        await AssertQueryAsync(emulator, "FB;", "FB007100000;");
    }

    [Fact]
    public async Task ClosedTransportReturnsEofAndCannotWriteUntilReopened()
    {
        await using var emulator = new FT991AEmulator();
        await emulator.OpenAsync();
        emulator.Disconnect();

        Assert.Equal(0, await emulator.ReadAsync(new byte[1]));
        await Assert.ThrowsAsync<IOException>(async () =>
            await emulator.WriteAsync(Encoding.ASCII.GetBytes("ID;")));

        await emulator.OpenAsync();
        await AssertQueryAsync(emulator, "ID;", "ID0670;");
    }

    private static async Task AssertQueryMatchesFixtureAsync(FT991AEmulator emulator, string id)
    {
        var fixture = ReadFixture(id);
        Assert.NotNull(fixture.Response);
        await AssertQueryAsync(emulator, fixture.Request, fixture.Response!);
    }

    private static async Task AssertQueryAsync(FT991AEmulator emulator, string request, string expectedResponse)
    {
        var expectedWriteCount = emulator.WriteCount + 1;
        await emulator.WriteAsync(Encoding.ASCII.GetBytes(request));
        var writes = await emulator.WaitForWriteAsync(expectedWriteCount);
        Assert.Equal(Encoding.ASCII.GetBytes(request), writes[^1].Payload);
        Assert.Equal(expectedResponse, await ReadAsciiAsync(emulator));
    }

    private static async Task SendNoReplyAsync(FT991AEmulator emulator, string request)
    {
        var expectedWriteCount = emulator.WriteCount + 1;
        await emulator.WriteAsync(Encoding.ASCII.GetBytes(request));
        var writes = await emulator.WaitForWriteAsync(expectedWriteCount);
        Assert.Equal(Encoding.ASCII.GetBytes(request), writes[^1].Payload);
        Assert.Equal(0, emulator.PendingReadByteCount);
    }

    private static async Task<string> ReadAsciiAsync(FT991AEmulator emulator)
    {
        var bytes = new List<byte>();
        var buffer = new byte[64];
        while (true)
        {
            var read = await emulator.ReadAsync(buffer);
            Assert.True(read > 0, "Expected reply bytes before EOF.");
            bytes.AddRange(buffer.AsSpan(0, read).ToArray());
            if (bytes.Contains((byte)';'))
            {
                return Encoding.ASCII.GetString(bytes.ToArray());
            }
        }
    }

    private static async Task<string[]> ReadFragmentsAsync(FT991AEmulator emulator, int count)
    {
        var fragments = new string[count];
        var buffer = new byte[64];
        for (var index = 0; index < count; index++)
        {
            var read = await emulator.ReadAsync(buffer);
            Assert.True(read > 0, "Expected all deterministic fragments before EOF.");
            fragments[index] = Encoding.ASCII.GetString(buffer, 0, read);
        }

        return fragments;
    }

    private static FixtureSample ReadFixture(string id)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ft991a.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var item = document.RootElement.GetProperty("cases").EnumerateArray()
            .Single(entry => entry.GetProperty("id").GetString() == id);
        var expected = item.GetProperty("expected");

        return new FixtureSample(
            item.GetProperty("request").GetString()!,
            item.GetProperty("response").ValueKind == JsonValueKind.Null
                ? null
                : item.GetProperty("response").GetString(),
            expected.TryGetProperty("verificationRequest", out var verificationRequest)
                ? verificationRequest.GetString()
                : null,
            expected.TryGetProperty("verificationResponse", out var verificationResponse)
                ? verificationResponse.GetString()
                : null,
            expected.TryGetProperty("verificationRequests", out var verificationRequests)
                ? verificationRequests.EnumerateArray().Select(value => value.GetString()!).ToArray()
                : null,
            expected.TryGetProperty("verificationResponses", out var verificationResponses)
                ? verificationResponses.EnumerateArray().Select(value => value.GetString()!).ToArray()
                : null);
    }

    private sealed record FixtureSample(
        string Request,
        string? Response,
        string? VerificationRequest,
        string? VerificationResponse,
        string[]? VerificationRequests,
        string[]? VerificationResponses);
}
