namespace SharpCAT2.Core.Radio.Contracts;

public enum ResponsePolicy
{
    WriteOnly,
    ReplyRequired,
    WriteThenReadBack
}

/// <summary>A profile's byte command and declared completion requirements.</summary>
/// <remarks>The session executes payload and verification payload as one queue transaction;
/// another operation cannot interleave. Timeout is the total budget from admission, including
/// queue wait. ResponseKey is an opaque profile matcher/parser key: for read-back it identifies
/// the verification reply. No policy authorizes automatic replay after failure or cancellation.</remarks>
public sealed class CommandSpecification
{
    private readonly byte[] _payload;
    private readonly byte[]? _verificationPayload;

    public CommandSpecification(
        ReadOnlyMemory<byte> payload,
        ResponsePolicy responsePolicy,
        TimeSpan timeout,
        string? responseKey = null,
        ReadOnlyMemory<byte>? verificationPayload = null)
    {
        if (payload.IsEmpty)
            throw new ArgumentException("A command must contain bytes.", nameof(payload));
        if (!Enum.IsDefined(responsePolicy))
            throw new ArgumentOutOfRangeException(nameof(responsePolicy));
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        if (responseKey is not null && string.IsNullOrWhiteSpace(responseKey))
            throw new ArgumentException("A response key cannot be empty.", nameof(responseKey));
        if (responsePolicy != ResponsePolicy.WriteOnly && responseKey is null)
            throw new ArgumentException("Reply policies require a profile response key.", nameof(responseKey));
        if (responsePolicy == ResponsePolicy.WriteThenReadBack)
        {
            if (verificationPayload is null || verificationPayload.Value.IsEmpty)
                throw new ArgumentException("Read-back requires verification bytes.", nameof(verificationPayload));
        }
        else if (verificationPayload is not null)
        {
            throw new ArgumentException("Only read-back uses verification bytes.", nameof(verificationPayload));
        }

        _payload = payload.ToArray();
        _verificationPayload = verificationPayload?.ToArray();
        ResponsePolicy = responsePolicy;
        Timeout = timeout;
        ResponseKey = responseKey;
    }

    // Each access returns an independent snapshot, including for callers that expose its backing array.
    public ReadOnlyMemory<byte> Payload => _payload.ToArray();
    public ReadOnlyMemory<byte>? VerificationPayload => _verificationPayload is null
        ? (ReadOnlyMemory<byte>?)null
        : new ReadOnlyMemory<byte>(_verificationPayload.ToArray());
    public ResponsePolicy ResponsePolicy { get; }
    public TimeSpan Timeout { get; }
    public string? ResponseKey { get; }
}
