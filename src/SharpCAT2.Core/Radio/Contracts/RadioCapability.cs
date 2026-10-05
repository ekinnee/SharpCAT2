namespace SharpCAT2.Core.Radio.Contracts;

/// <summary>Support evidence for one operation; simulation is not hardware verification.</summary>
public enum CapabilityEvidence
{
    Unavailable,
    Experimental,
    ProtocolTested,
    HardwareVerified
}

/// <summary>A per-operation support claim, optionally qualified by its evidence or limitations.</summary>
public sealed record RadioCapability
{
    public RadioCapability(RadioOperation operation, CapabilityEvidence evidence, string? detail = null)
    {
        if (!Enum.IsDefined(operation))
            throw new ArgumentOutOfRangeException(nameof(operation));
        if (!Enum.IsDefined(evidence))
            throw new ArgumentOutOfRangeException(nameof(evidence));

        Operation = operation;
        Evidence = evidence;
        Detail = detail;
    }

    public RadioOperation Operation { get; }
    public CapabilityEvidence Evidence { get; }
    public string? Detail { get; }
}
