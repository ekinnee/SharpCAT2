using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using SharpCAT2.Core.Radio.Contracts;

namespace SharpCAT2.ServerLibrary.Radio.Protocols;

/// <summary>Pure FT-991A CAT subset from the Yaesu 1711-D reference and independently authored fixtures.</summary>
/// <remarks>Protocol-tested software evidence is not a hardware compatibility claim. Identity is
/// a string, frequencies are long Hz, and modes use the exact canonical names below. This profile
/// owns no port, reader, connection or retry. MD0 addresses MAIN RX, not a selectable VFO.</remarks>
public sealed class FT991AProfile : IRadioProfile
{
    public const long MinimumFrequencyHz = 30_000;
    public const long MaximumFrequencyHz = 470_000_000;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);
    private static readonly (char Code, string Name)[] Modes =
    [
        ('1', "LSB"), ('2', "USB"), ('3', "CW-U"), ('4', "FM"), ('5', "AM"),
        ('6', "RTTY-LSB"), ('7', "CW-L"), ('8', "DATA-LSB"), ('9', "RTTY-USB"),
        ('A', "DATA-FM"), ('B', "FM-N"), ('C', "DATA-USB"), ('D', "AM-N"), ('E', "C4FM")
    ];
    private static readonly IReadOnlyList<RadioCapability> Supported = Array.AsReadOnly(
        new[] { RadioOperation.Identify, RadioOperation.GetFrequency, RadioOperation.SetFrequency,
            RadioOperation.GetMode, RadioOperation.SetMode, RadioOperation.SwapVfo }
        .Select(operation => new RadioCapability(operation,
            CapabilityEvidence.ProtocolTested, "Independent 1711-D fixtures; physical hardware unverified.")).ToArray());

    public string ProfileId => "FT-991A";
    public IReadOnlyList<RadioCapability> Capabilities => Supported;

    public bool TryCreateCommand(RadioOperationRequest request,
        [NotNullWhen(true)] out CommandSpecification? command,
        out RadioOutcome outcome, out string? diagnostic)
    {
        ArgumentNullException.ThrowIfNull(request);
        command = null;
        diagnostic = null;
        outcome = RadioOutcome.Succeeded;
        var key = ResponseKey(request);
        switch (request.Operation)
        {
            case RadioOperation.Identify:
                command = Reply("ID;", "ID");
                break;
            case RadioOperation.GetFrequency:
                command = Reply(key + ";", key!);
                break;
            case RadioOperation.SetFrequency:
                if (request.FrequencyHz is < MinimumFrequencyHz or > MaximumFrequencyHz)
                {
                    outcome = RadioOutcome.InvalidArgument;
                    diagnostic = "FT-991A frequency must be 30000 through 470000000 Hz inclusive.";
                    return false;
                }
                command = ReadBack(key + request.FrequencyHz!.Value.ToString("D9", CultureInfo.InvariantCulture)
                    + ";", key!);
                break;
            case RadioOperation.GetMode:
                command = Reply("MD0;", "MD0");
                break;
            case RadioOperation.SetMode:
                var mode = Array.Find(Modes, item => string.Equals(item.Name, request.Mode, StringComparison.OrdinalIgnoreCase));
                if (mode.Name is null)
                {
                    outcome = RadioOutcome.InvalidArgument;
                    diagnostic = "Mode is not one of the documented FT-991A MAIN RX modes.";
                    return false;
                }
                command = ReadBack("MD0" + mode.Code + ";", "MD0");
                break;
            case RadioOperation.SwapVfo:
                // SV has no reply. The session facade owns the pre/post FA+FB verification transaction.
                command = new CommandSpecification(Ascii("SV;"), ResponsePolicy.WriteOnly, Timeout);
                break;
            default:
                outcome = RadioOutcome.NotSupported;
                diagnostic = "Operation is outside the FT-991A subset.";
                return false;
        }
        return true;
    }

    public FrameParseResult ParseFrame(ReadOnlySpan<byte> buffer)
    {
        if (buffer.IsEmpty) return new FrameParseResult(FrameParseStatus.Incomplete, 0);
        var terminator = buffer.IndexOf((byte)';');
        var consumed = terminator < 0 ? buffer.Length : terminator + 1;
        var prefix = buffer[..consumed];
        if (!IsPrintableAscii(prefix) || !IsLetter(prefix[0]) ||
            (prefix.Length > 1 && !IsLetter(prefix[1])))
            return new FrameParseResult(FrameParseStatus.Invalid, consumed);
        if (terminator < 0) return new FrameParseResult(FrameParseStatus.Incomplete, 0);
        if (!IsFrame(prefix)) return new FrameParseResult(FrameParseStatus.Invalid, consumed);
        return new FrameParseResult(FrameParseStatus.Complete, consumed, prefix.ToArray());
    }

    public ReplyParseResult ParseReply(RadioOperationRequest request, string responseKey, ReadOnlySpan<byte> frame)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(responseKey);
        if (!string.Equals(responseKey, ResponseKey(request), StringComparison.Ordinal))
            return Invalid("Response key does not belong to the requested operation.");
        if (!MatchesOpcode(frame, responseKey)) return new ReplyParseResult(ReplyParseStatus.Unrelated);
        if (!IsFrame(frame)) return Invalid("Matching reply has invalid ASCII framing or terminator.");

        switch (responseKey)
        {
            case "ID":
                return frame.Length == 7 && frame.Slice(2, 4).SequenceEqual("0670"u8)
                    ? new ReplyParseResult(ReplyParseStatus.Valid, "FT-991A")
                    : Invalid("Identity reply does not identify the FT-991A (0670).");
            case "FA":
            case "FB":
                if (frame.Length != 12) return Invalid("Frequency reply requires exactly nine decimal Hz digits.");
                long frequency = 0;
                foreach (var digit in frame.Slice(2, 9))
                {
                    if (digit is < (byte)'0' or > (byte)'9') return Invalid("Frequency reply contains a non-decimal digit.");
                    frequency = frequency * 10 + digit - '0';
                }
                return frequency is >= MinimumFrequencyHz and <= MaximumFrequencyHz
                    ? new ReplyParseResult(ReplyParseStatus.Valid, frequency)
                    : Invalid("Observed frequency is outside the documented FT-991A range.");
            case "MD0":
                if (frame.Length != 5 || frame[2] != (byte)'0')
                    return Invalid("Mode reply requires MAIN RX selector 0 and exactly one mode code.");
                var code = (char)frame[3];
                var mode = Array.Find(Modes, item => item.Code == code);
                return mode.Name is not null ? new ReplyParseResult(ReplyParseStatus.Valid, mode.Name)
                    : Invalid("Device returned an unlisted FT-991A mode code.");
            default:
                return new ReplyParseResult(ReplyParseStatus.Unrelated);
        }
    }

    /// <summary>AI0 disables automatic information (1711-D printed page 4 / PDF index 4); no acknowledgment.</summary>
    public CommandSpecification CreateDisableAutomaticInformationCommand() =>
        new(Ascii("AI0;"), ResponsePolicy.WriteOnly, Timeout);

    /// <summary>The caller/session must verify AI0 before considering startup synchronization complete.</summary>
    public CommandSpecification CreateAutomaticInformationQueryCommand() => Reply("AI;", "AI");

    public ReplyParseResult ParseAutomaticInformationReply(ReadOnlySpan<byte> frame)
    {
        if (!MatchesOpcode(frame, "AI")) return new ReplyParseResult(ReplyParseStatus.Unrelated);
        return IsFrame(frame) && frame.Length == 4 && frame[2] == (byte)'0'
            ? new ReplyParseResult(ReplyParseStatus.Valid, false)
            : Invalid("Automatic information was not confirmed disabled (AI0).");
    }

    private static CommandSpecification Reply(string payload, string key) =>
        new(Ascii(payload), ResponsePolicy.ReplyRequired, Timeout, key);

    private static CommandSpecification ReadBack(string payload, string key) =>
        new(Ascii(payload), ResponsePolicy.WriteThenReadBack, Timeout, key, Ascii(key + ";"));

    private static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);
    private static ReplyParseResult Invalid(string diagnostic) => new(ReplyParseStatus.Invalid, diagnostic: diagnostic);

    private static string? ResponseKey(RadioOperationRequest request) => request.Operation switch
    {
        RadioOperation.Identify => "ID",
        RadioOperation.GetFrequency or RadioOperation.SetFrequency => request.Vfo == RadioVfo.A ? "FA" : "FB",
        RadioOperation.GetMode or RadioOperation.SetMode => "MD0",
        _ => null
    };

    private static bool MatchesOpcode(ReadOnlySpan<byte> frame, string key) =>
        frame.Length >= 2 && Upper(frame[0]) == key[0] && Upper(frame[1]) == key[1];

    private static byte Upper(byte value) => value is >= (byte)'a' and <= (byte)'z' ? (byte)(value - 32) : value;
    private static bool IsLetter(byte value) => Upper(value) is >= (byte)'A' and <= (byte)'Z';
    private static bool IsPrintableAscii(ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes) if (value is < 0x20 or > 0x7e) return false;
        return true;
    }
    private static bool IsFrame(ReadOnlySpan<byte> frame) => frame.Length >= 3 &&
        IsLetter(frame[0]) && IsLetter(frame[1]) && frame[^1] == (byte)';' &&
        frame[..^1].IndexOf((byte)';') < 0 && IsPrintableAscii(frame);
}
