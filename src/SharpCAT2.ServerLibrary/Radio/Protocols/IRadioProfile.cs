using System.Diagnostics.CodeAnalysis;
using SharpCAT2.Core.Radio.Contracts;

namespace SharpCAT2.ServerLibrary.Radio.Protocols;

/// <summary>Pure model protocol ownership: commands, frame boundaries, reply matching and parsing.</summary>
/// <remarks>A profile accepts no port and owns no reads, timers, connection or transport lifetime.
/// Device constraints are checked here; unsupported mappings must not fall back to guessed opcodes.
/// Matching a key alone does not establish transaction identity after a timeout on untagged CAT.</remarks>
public interface IRadioProfile
{
    string ProfileId { get; }
    IReadOnlyList<RadioCapability> Capabilities { get; }

    /// <summary>Returns true with a command and Succeeded, or false with no command and
    /// NotSupported/InvalidArgument plus an explanation. Succeeded here means command creation,
    /// not execution or device acceptance.</summary>
    bool TryCreateCommand(
        RadioOperationRequest request,
        [NotNullWhen(true)] out CommandSpecification? command,
        out RadioOutcome outcome,
        out string? diagnostic);

    /// <summary>Examines the first frame. Incomplete consumes nothing; complete/invalid consume a positive prefix.</summary>
    /// <remarks>The session retains the remainder and retries after receiving more bytes.
    /// Complete frames may be unsolicited; the session routes them using ParseReply.</remarks>
    FrameParseResult ParseFrame(ReadOnlySpan<byte> buffer);

    /// <summary>Uses the command's opaque ResponseKey to distinguish unrelated, valid and malformed matching replies.</summary>
    /// <remarks>A valid value is parsed device data, never an echo of requested state. The session
    /// supplies observation timestamp/generation, verifies read-back against the request, and routes
    /// unrelated frames separately. An invalid matching reply cannot masquerade as unsolicited data.</remarks>
    ReplyParseResult ParseReply(RadioOperationRequest request, string responseKey, ReadOnlySpan<byte> frame);
}

public enum FrameParseStatus
{
    Incomplete,
    Complete,
    Invalid
}

/// <summary>An owned frame snapshot and the exact input prefix the session may remove.</summary>
public sealed class FrameParseResult
{
    private readonly byte[] _frame;

    public FrameParseResult(FrameParseStatus status, int consumedBytes, ReadOnlyMemory<byte> frame = default)
    {
        if (!Enum.IsDefined(status))
            throw new ArgumentOutOfRangeException(nameof(status));
        if (consumedBytes < 0 || (status == FrameParseStatus.Incomplete ? consumedBytes != 0 : consumedBytes == 0))
            throw new ArgumentOutOfRangeException(nameof(consumedBytes));
        if (status == FrameParseStatus.Complete ? frame.IsEmpty || frame.Length > consumedBytes : !frame.IsEmpty)
            throw new ArgumentException("Only a complete result contains a nonempty frame within its consumed prefix.", nameof(frame));

        Status = status;
        ConsumedBytes = consumedBytes;
        _frame = frame.ToArray();
    }

    public FrameParseStatus Status { get; }
    public int ConsumedBytes { get; }
    public ReadOnlyMemory<byte> Frame => _frame.ToArray();
}

public enum ReplyParseStatus
{
    Unrelated,
    Valid,
    Invalid
}

/// <summary>A matched reply's parsed immutable value, or an unrelated/malformed classification.</summary>
/// <remarks>Value may be null for a valid acknowledgment with no state value. Its runtime type
/// follows the requested operation; callers must validate it before constructing a typed result.</remarks>
public sealed record ReplyParseResult
{
    public ReplyParseResult(ReplyParseStatus status, object? value = null, string? diagnostic = null)
    {
        if (!Enum.IsDefined(status))
            throw new ArgumentOutOfRangeException(nameof(status));
        if (status != ReplyParseStatus.Valid && value is not null)
            throw new ArgumentException("Only a valid reply contains a parsed value.", nameof(value));

        Status = status;
        Value = value;
        Diagnostic = diagnostic;
    }

    public ReplyParseStatus Status { get; }
    public object? Value { get; }
    public string? Diagnostic { get; }
}
