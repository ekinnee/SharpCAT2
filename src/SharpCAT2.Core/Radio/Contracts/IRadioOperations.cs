namespace SharpCAT2.Core.Radio.Contracts;

/// <summary>Observed operations and their evidence, independent of legacy cached properties.</summary>
public interface IRadioOperations
{
    IReadOnlyList<RadioCapability> Capabilities { get; }
    Task<RadioOperationResult<object>> ExecuteAsync(RadioOperationRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Two frequencies observed during one session transaction; no active-VFO claim.</summary>
public sealed record VfoFrequencies(long AHz, long BHz);
