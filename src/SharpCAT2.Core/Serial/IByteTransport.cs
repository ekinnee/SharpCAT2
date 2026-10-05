namespace SharpCAT2.Core.Serial;

/// <summary>Lossless byte I/O; encoding, framing, transactions and recovery belong to the session/profile.</summary>
/// <remarks>The caller explicitly transfers exclusive lifetime ownership to the future session
/// when constructing it. That owner opens, closes and disposes the transport, including failed
/// startup. Other code must then neither use nor dispose it. A disposed transport cannot be reused.
/// Implementations must support bounded shutdown (cancellation or close-to-unblock); they must
/// not create an unbounded worker per read. There is no competing DataReceived reader.</remarks>
public interface IByteTransport : IAsyncDisposable
{
    bool IsOpen { get; }

    ValueTask OpenAsync(CancellationToken cancellationToken = default);
    ValueTask CloseAsync(CancellationToken cancellationToken = default);

    /// <summary>Reads up to buffer.Length bytes; a zero result for a nonempty buffer means end of transport.</summary>
    /// <remarks>Reject an empty buffer. Preserve incomplete protocol tails in the session.
    /// The caller owns the buffer and must keep it valid until the operation completes.
    /// Cancellation does not authorize a second concurrent reader.</remarks>
    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);

    /// <summary>Writes all supplied bytes exactly, without text encoding or added terminators.</summary>
    /// <remarks>Successful completion means the adapter accepted all bytes, not that the radio
    /// accepted the command. The caller keeps the buffer unchanged until completion. A failure
    /// or cancellation after invocation may have written a prefix or all bytes; absence of
    /// completion is not proof of NotSent or Written; use WriteAttempted until completion is
    /// established. The session must preserve mutation uncertainty,
    /// retain transaction ownership until reply consumption/recovery, and never blindly replay.</remarks>
    ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default);
}
