using System.Globalization;
using System.Text;
using System.Text.Json;
using SharpCAT2.Core.Radio.Contracts;
using SharpCAT2.ServerLibrary.Radio.Protocols;

namespace SharpCAT2.Tests.Radio.Protocols;

public class FT991AProfileTests
{
    private readonly FT991AProfile _profile = new();

    public static IEnumerable<object[]> Fixtures()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "ft991a.json")));
        foreach (var fixture in document.RootElement.GetProperty("cases").EnumerateArray())
            yield return [fixture.GetProperty("id").GetString()!, fixture.GetRawText()];
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void ProductionProfileHonorsEveryIndependentManufacturerFixture(string id, string json)
    {
        Assert.False(string.IsNullOrEmpty(id));
        using var document = JsonDocument.Parse(json);
        var fixture = document.RootElement;
        var expected = fixture.GetProperty("expected");
        var classification = expected.GetProperty("classification").GetString();
        var requestBytes = Bytes(fixture.GetProperty("request").GetString()!);
        var operation = fixture.GetProperty("operation").GetString();
        var setter = fixture.GetProperty("kind").GetString() == "setter";
        var vfo = operation == "frequencyA" ? RadioVfo.A : RadioVfo.B;

        RadioOperationRequest request = operation switch
        {
            "identity" => new(RadioOperation.Identify),
            "frequencyA" or "frequencyB" => setter
                ? new(RadioOperation.SetFrequency, vfo,
                    long.Parse(Encoding.ASCII.GetString(requestBytes)[2..^1], CultureInfo.InvariantCulture))
                : new(RadioOperation.GetFrequency, vfo),
            "mode" => setter ? new(RadioOperation.SetMode,
                mode: expected.TryGetProperty("mode", out var mode) ? mode.GetString() : "F")
                : new(RadioOperation.GetMode),
            "swapVfos" => new(RadioOperation.SwapVfo),
            _ => throw new InvalidOperationException("Fixture operation is outside the frozen subset.")
        };

        if (classification == "incomplete")
        {
            var frame = _profile.ParseFrame(requestBytes);
            Assert.Equal(FrameParseStatus.Incomplete, frame.Status);
            Assert.Equal(0, frame.ConsumedBytes);
            Assert.True(frame.Frame.IsEmpty);
            return;
        }
        if (classification == "invalidArgument")
        {
            Assert.False(_profile.TryCreateCommand(request, out var rejected, out var outcome, out var diagnostic));
            Assert.Null(rejected);
            Assert.Equal(RadioOutcome.InvalidArgument, outcome);
            Assert.False(string.IsNullOrWhiteSpace(diagnostic));
            return;
        }

        Assert.True(_profile.TryCreateCommand(request, out var command, out var creation, out var detail));
        Assert.Equal(RadioOutcome.Succeeded, creation);
        Assert.Null(detail);

        if (classification is "malformed" or "identityMismatch")
        {
            var response = fixture.GetProperty("response");
            var bytes = response.ValueKind == JsonValueKind.Null ? requestBytes : Bytes(response.GetString()!);
            var parsed = _profile.ParseReply(request, command.ResponseKey!, bytes);
            Assert.Equal(ReplyParseStatus.Invalid, parsed.Status);
            Assert.Null(parsed.Value);
            Assert.False(string.IsNullOrWhiteSpace(parsed.Diagnostic));
            return;
        }

        Assert.Equal("valid", classification);
        Assert.Equal(FrameParseStatus.Complete, _profile.ParseFrame(requestBytes).Status);
        // The lowercase-command oracle requires case acceptance, not lowercase builder output.
        Assert.Equal(Encoding.ASCII.GetString(requestBytes).ToUpperInvariant(), Text(command.Payload));
        if (operation == "swapVfos")
        {
            Assert.Equal(ResponsePolicy.WriteOnly, command.ResponsePolicy);
            Assert.Null(command.VerificationPayload);
            Assert.Null(command.ResponseKey);
            var verificationQueries = expected.GetProperty("verificationRequests").EnumerateArray().ToArray();
            var verificationReplies = expected.GetProperty("verificationResponses").EnumerateArray().ToArray();
            Assert.Equal(2, verificationQueries.Length);
            for (var i = 0; i < verificationQueries.Length; i++)
            {
                var query = new RadioOperationRequest(RadioOperation.GetFrequency, i == 0 ? RadioVfo.A : RadioVfo.B);
                Assert.True(_profile.TryCreateCommand(query, out var read, out _, out _));
                Assert.Equal(verificationQueries[i].GetString(), Text(read.Payload));
                Assert.Equal(ReplyParseStatus.Valid, _profile.ParseReply(query, read.ResponseKey!,
                    Bytes(verificationReplies[i].GetString()!)).Status);
            }
            return;
        }

        string replyText;
        if (setter)
        {
            Assert.Equal(JsonValueKind.Null, fixture.GetProperty("response").ValueKind);
            Assert.Equal(ResponsePolicy.WriteThenReadBack, command.ResponsePolicy);
            Assert.Equal(expected.GetProperty("verificationRequest").GetString(), Text(command.VerificationPayload!.Value));
            replyText = expected.GetProperty("verificationResponse").GetString()!;
        }
        else
        {
            Assert.Equal(ResponsePolicy.ReplyRequired, command.ResponsePolicy);
            Assert.Null(command.VerificationPayload);
            replyText = fixture.GetProperty("response").GetString()!;
        }
        var replyBytes = Bytes(replyText);
        var complete = _profile.ParseFrame(replyBytes);
        Assert.Equal(FrameParseStatus.Complete, complete.Status);
        Assert.Equal(replyBytes.Length, complete.ConsumedBytes);
        var result = _profile.ParseReply(request, command.ResponseKey!, complete.Frame.Span);
        Assert.Equal(ReplyParseStatus.Valid, result.Status);
        if (expected.TryGetProperty("frequencyHz", out var frequency)) Assert.Equal(frequency.GetInt64(), result.Value);
        if (expected.TryGetProperty("mode", out var expectedMode)) Assert.Equal(expectedMode.GetString(), result.Value);
        if (expected.TryGetProperty("identity", out var identity)) Assert.Equal(identity.GetString(), result.Value);
    }

    [Theory]
    [InlineData('1', "LSB")]
    [InlineData('2', "USB")]
    [InlineData('3', "CW-U")]
    [InlineData('4', "FM")]
    [InlineData('5', "AM")]
    [InlineData('6', "RTTY-LSB")]
    [InlineData('7', "CW-L")]
    [InlineData('8', "DATA-LSB")]
    [InlineData('9', "RTTY-USB")]
    [InlineData('A', "DATA-FM")]
    [InlineData('B', "FM-N")]
    [InlineData('C', "DATA-USB")]
    [InlineData('D', "AM-N")]
    [InlineData('E', "C4FM")]
    public void EveryDocumentedMainReceiverModeEncodesAndParses(char code, string name)
    {
        // Independent table from the manual's MD page, rather than enumerating production mappings.
        var request = new RadioOperationRequest(RadioOperation.SetMode, mode: name);
        Assert.True(_profile.TryCreateCommand(request, out var command, out _, out _));
        Assert.Equal("MD0" + code + ";", Text(command.Payload));
        Assert.Equal("MD0;", Text(command.VerificationPayload!.Value));
        var reply = _profile.ParseReply(request, command.ResponseKey!, Bytes("MD0" + code + ";"));
        Assert.Equal(ReplyParseStatus.Valid, reply.Status);
        Assert.Equal(name, reply.Value);
    }

    [Theory]
    [InlineData(29999)]
    [InlineData(470000001)]
    [InlineData(999999999)]
    [InlineData(long.MaxValue)]
    public void OutOfManualRangeIsRejectedBeforeCommandBytesExist(long frequency)
    {
        var request = new RadioOperationRequest(RadioOperation.SetFrequency, RadioVfo.A, frequency);
        Assert.False(_profile.TryCreateCommand(request, out var command, out var outcome, out _));
        Assert.Null(command);
        Assert.Equal(RadioOutcome.InvalidArgument, outcome);
    }

    [Theory]
    [InlineData("CW")]
    [InlineData("NFM")]
    [InlineData("2")]
    [InlineData("USB ")]
    [InlineData("UNLISTED")]
    public void UnlistedModeNamesAndAliasesAreRejectedBeforeEncoding(string mode)
    {
        Assert.False(_profile.TryCreateCommand(new(RadioOperation.SetMode, mode: mode),
            out var command, out var outcome, out _));
        Assert.Null(command);
        Assert.Equal(RadioOutcome.InvalidArgument, outcome);
    }

    [Fact]
    public void LowercaseCommandLettersAreAcceptedButModeCodeAndParameterWidthsStayStrict()
    {
        var frequency = new RadioOperationRequest(RadioOperation.GetFrequency, RadioVfo.A);
        Assert.Equal(ReplyParseStatus.Valid, _profile.ParseReply(frequency, "FA", Bytes("fa014250000;")).Status);
        var mode = new RadioOperationRequest(RadioOperation.GetMode);
        Assert.Equal("USB", _profile.ParseReply(mode, "MD0", Bytes("md02;")).Value);
        Assert.Equal(ReplyParseStatus.Invalid, _profile.ParseReply(mode, "MD0", Bytes("MD0a;")).Status);
        var identity = new RadioOperationRequest(RadioOperation.Identify);
        Assert.Equal("FT-991A", _profile.ParseReply(identity, "ID", Bytes("id0670;")).Value);
    }

    [Theory]
    [InlineData("ID670;")]
    [InlineData("ID00670;")]
    [InlineData("ID0671;")]
    [InlineData("ID06A0;")]
    [InlineData("ID0670")]
    [InlineData("ID0670;ID0670;")]
    public void IdentityRequiresExactlyTheKnownModelAnswer(string frame)
    {
        var result = _profile.ParseReply(new(RadioOperation.Identify), "ID", Bytes(frame));
        Assert.Equal(ReplyParseStatus.Invalid, result.Status);
        Assert.Null(result.Value);
    }

    [Theory]
    [InlineData("FA000000000;")]
    [InlineData("FA470000001;")]
    [InlineData("FA+14250000;")]
    [InlineData("FA014250000;extra")]
    [InlineData("FA014250000\n;")]
    public void MalformedMatchingFrequencyReplyIsNeverAnUnrelatedFrame(string frame)
    {
        var result = _profile.ParseReply(new(RadioOperation.GetFrequency, RadioVfo.A), "FA", Bytes(frame));
        Assert.Equal(ReplyParseStatus.Invalid, result.Status);
        Assert.Null(result.Value);
    }

    [Fact]
    public void OtherOpcodeIsUnrelatedAndWrongMainRxSelectorIsMatchingInvalidData()
    {
        var request = new RadioOperationRequest(RadioOperation.GetFrequency, RadioVfo.A);
        Assert.Equal(ReplyParseStatus.Unrelated, _profile.ParseReply(request, "FA", Bytes("FB014250000;")).Status);
        Assert.Equal(ReplyParseStatus.Unrelated, _profile.ParseReply(request, "FA", Bytes("ID0670;")).Status);
        Assert.Equal(ReplyParseStatus.Invalid, _profile.ParseReply(request, "FB", Bytes("FB014250000;")).Status);
        Assert.Equal(ReplyParseStatus.Invalid, _profile.ParseReply(new(RadioOperation.GetMode), "MD0", Bytes("MD12;")).Status);
    }

    [Fact]
    public void FramingRetainsIncompleteTailsAndSplitsCoalescedAsciiFrames()
    {
        var bytes = Bytes("FA014250000;MD02;FB0071");
        var first = _profile.ParseFrame(bytes);
        Assert.Equal("FA014250000;", Text(first.Frame));
        var second = _profile.ParseFrame(bytes.AsSpan(first.ConsumedBytes));
        Assert.Equal("MD02;", Text(second.Frame));
        var tail = _profile.ParseFrame(bytes.AsSpan(first.ConsumedBytes + second.ConsumedBytes));
        Assert.Equal(FrameParseStatus.Incomplete, tail.Status);
        Assert.Equal(0, tail.ConsumedBytes);
        Assert.True(tail.Frame.IsEmpty);
        for (var split = 0; split < 12; split++)
            Assert.Equal(FrameParseStatus.Incomplete, _profile.ParseFrame(Bytes("FA014250000;").AsSpan(0, split)).Status);
    }

    [Theory]
    [InlineData(";", 1)]
    [InlineData("F;", 2)]
    [InlineData("1A;", 3)]
    [InlineData("\r\nFA;", 5)]
    [InlineData("FA\0;", 4)]
    [InlineData("FA\n", 3)]
    public void MalformedAsciiFrameHasAnExplicitInvalidConsumedPrefix(string text, int consumed)
    {
        var frame = _profile.ParseFrame(Bytes(text));
        Assert.Equal(FrameParseStatus.Invalid, frame.Status);
        Assert.Equal(consumed, frame.ConsumedBytes);
        Assert.True(frame.Frame.IsEmpty);
    }

    [Fact]
    public void NonAsciiCannotBeSilentlyDecodedIntoAValidReply()
    {
        byte[] bytes = [(byte)'F', (byte)'A', 0xff, (byte)';'];
        Assert.Equal(FrameParseStatus.Invalid, _profile.ParseFrame(bytes).Status);
        Assert.Equal(ReplyParseStatus.Invalid, _profile.ParseReply(
            new(RadioOperation.GetFrequency, RadioVfo.A), "FA", bytes).Status);
    }

    [Fact]
    public void StartupHelpersDisableAndConfirmAutomaticInformationWithoutInventingAnAck()
    {
        var disable = _profile.CreateDisableAutomaticInformationCommand();
        Assert.Equal("AI0;", Text(disable.Payload));
        Assert.Equal(ResponsePolicy.WriteOnly, disable.ResponsePolicy);
        Assert.Null(disable.ResponseKey);
        var query = _profile.CreateAutomaticInformationQueryCommand();
        Assert.Equal("AI;", Text(query.Payload));
        Assert.Equal(ResponsePolicy.ReplyRequired, query.ResponsePolicy);
        Assert.Equal("AI", query.ResponseKey);
        Assert.Equal(false, _profile.ParseAutomaticInformationReply(Bytes("AI0;")).Value);
        Assert.Equal(ReplyParseStatus.Valid, _profile.ParseAutomaticInformationReply(Bytes("ai0;")).Status);
        Assert.Equal(ReplyParseStatus.Invalid, _profile.ParseAutomaticInformationReply(Bytes("AI1;")).Status);
        Assert.Equal(ReplyParseStatus.Invalid, _profile.ParseAutomaticInformationReply(Bytes("AI00;")).Status);
        Assert.Equal(ReplyParseStatus.Unrelated, _profile.ParseAutomaticInformationReply(Bytes("FA014250000;")).Status);
    }

    [Fact]
    public void CapabilitiesDescribeOnlyTheVerifiedSubsetAndNoHardwareCertification()
    {
        Assert.Equal("FT-991A", _profile.ProfileId);
        Assert.Equal(new[] { RadioOperation.Identify, RadioOperation.GetFrequency, RadioOperation.SetFrequency,
            RadioOperation.GetMode, RadioOperation.SetMode, RadioOperation.SwapVfo },
            _profile.Capabilities.Select(capability => capability.Operation));
        Assert.All(_profile.Capabilities, capability =>
        {
            Assert.Equal(CapabilityEvidence.ProtocolTested, capability.Evidence);
            Assert.Contains("hardware unverified", capability.Detail!);
        });
    }

    private static byte[] Bytes(string text) => Encoding.ASCII.GetBytes(text);
    private static string Text(ReadOnlyMemory<byte> bytes) => Encoding.ASCII.GetString(bytes.Span);
}
