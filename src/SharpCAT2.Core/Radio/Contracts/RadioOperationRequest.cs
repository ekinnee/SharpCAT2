namespace SharpCAT2.Core.Radio.Contracts;

public enum RadioOperation
{
    Identify,
    GetFrequency,
    SetFrequency,
    GetMode,
    SetMode,
    SwapVfo
}

public enum RadioVfo
{
    A,
    B
}

/// <summary>A syntactically valid request. A profile still validates device ranges and modes.</summary>
/// <remarks>A VFO is required only for frequency operations. GetMode and SetMode address
/// MAIN RX and accept no VFO selector. Profiles may return NotSupported for a valid operation
/// or VFO without inventing a manufacturer mapping.</remarks>
public sealed record RadioOperationRequest
{
    public RadioOperationRequest(
        RadioOperation operation,
        RadioVfo? vfo = null,
        long? frequencyHz = null,
        string? mode = null)
    {
        if (!Enum.IsDefined(operation))
            throw new ArgumentOutOfRangeException(nameof(operation));
        if (vfo is not null && !Enum.IsDefined(vfo.Value))
            throw new ArgumentOutOfRangeException(nameof(vfo));

        var needsVfo = operation is RadioOperation.GetFrequency or RadioOperation.SetFrequency;
        if (needsVfo != vfo.HasValue)
            throw new ArgumentException("Only frequency operations require a VFO; other operations reject a selector.", nameof(vfo));
        if (operation == RadioOperation.SetFrequency)
        {
            if (frequencyHz is null or <= 0)
                throw new ArgumentOutOfRangeException(nameof(frequencyHz));
        }
        else if (frequencyHz is not null)
        {
            throw new ArgumentException("Frequency belongs only to SetFrequency.", nameof(frequencyHz));
        }
        if (operation == RadioOperation.SetMode)
        {
            if (string.IsNullOrWhiteSpace(mode))
                throw new ArgumentException("SetMode requires a mode.", nameof(mode));
        }
        else if (mode is not null)
        {
            throw new ArgumentException("Mode belongs only to SetMode.", nameof(mode));
        }

        Operation = operation;
        Vfo = vfo;
        FrequencyHz = frequencyHz;
        Mode = mode;
    }

    public RadioOperation Operation { get; }
    public RadioVfo? Vfo { get; }
    public long? FrequencyHz { get; }
    public string? Mode { get; }
}
