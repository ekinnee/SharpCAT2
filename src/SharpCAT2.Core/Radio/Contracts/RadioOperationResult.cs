namespace SharpCAT2.Core.Radio.Contracts;

public enum RadioOutcome
{
    Succeeded,
    InvalidArgument,
    NotSupported,
    NotConnected,
    Busy,
    TimedOut,
    Cancelled,
    ProtocolError,
    TransportError,
    OutcomeUnknown
}

/// <summary>Available completion evidence; Written means a completed local transport write,
/// not proof of device acceptance. WriteAttempted means a write began but completion is unconfirmed.</summary>
public enum CompletionEvidence
{
    NotSent,
    WriteAttempted,
    Written,
    ReplyReceived,
    ReadBackVerified
}

/// <summary>A value parsed from a device reply, rather than a requested value.</summary>
/// <remarks>The value type should itself be immutable. Generations identify a session connection,
/// not a device transaction, and do not disambiguate arbitrarily delayed CAT replies.</remarks>
public sealed record RadioObservation<T>
{
    public RadioObservation(T value, DateTimeOffset observedAt, long connectionGeneration)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (connectionGeneration <= 0)
            throw new ArgumentOutOfRangeException(nameof(connectionGeneration));

        Value = value;
        ObservedAt = observedAt;
        ConnectionGeneration = connectionGeneration;
    }

    public T Value { get; }
    public DateTimeOffset ObservedAt { get; }
    public long ConnectionGeneration { get; }
}

/// <summary>An operation's terminal outcome and independent completion evidence.</summary>
/// <remarks>Cancelled/NotSent means cancellation won before any write began. Once a write
/// starts, cancellation or a transport failure may have left partial bytes or a device effect;
/// an unconfirmed mutation returns OutcomeUnknown with the available evidence. This contract
/// records that fact but does not implement cancellation or authorize command replay.</remarks>
public sealed record RadioOperationResult<T>
{
    public RadioOperationResult(
        RadioOutcome outcome,
        CompletionEvidence evidence,
        RadioObservation<T>? observation = null,
        string? diagnostic = null)
    {
        if (!Enum.IsDefined(outcome))
            throw new ArgumentOutOfRangeException(nameof(outcome));
        if (!Enum.IsDefined(evidence))
            throw new ArgumentOutOfRangeException(nameof(evidence));
        if (evidence == CompletionEvidence.NotSent &&
            outcome is RadioOutcome.Succeeded or RadioOutcome.OutcomeUnknown)
            throw new ArgumentException("Success or uncertainty requires an attempted write.", nameof(evidence));
        if (outcome == RadioOutcome.Succeeded && evidence == CompletionEvidence.WriteAttempted)
            throw new ArgumentException("Success requires a completed write or reply.", nameof(evidence));
        if (observation is not null &&
            evidence is not (CompletionEvidence.ReplyReceived or CompletionEvidence.ReadBackVerified))
            throw new ArgumentException("An observation requires a device reply.", nameof(observation));
        if (evidence == CompletionEvidence.ReadBackVerified && observation is null)
            throw new ArgumentException("Read-back verification requires an observed value.", nameof(observation));

        Outcome = outcome;
        Evidence = evidence;
        Observation = observation;
        Diagnostic = diagnostic;
    }

    public RadioOutcome Outcome { get; }
    public CompletionEvidence Evidence { get; }
    public RadioObservation<T>? Observation { get; }
    public string? Diagnostic { get; }
}
