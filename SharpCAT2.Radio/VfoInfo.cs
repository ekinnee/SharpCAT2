namespace SharpCAT2.Radio;

/// <summary>
/// Represents information for a single VFO (Variable Frequency Oscillator)
/// </summary>
public class VfoInfo
{
    /// <summary>
    /// Gets or sets the frequency in Hz
    /// </summary>
    public long Frequency { get; set; }

    /// <summary>
    /// Gets or sets the operating mode
    /// </summary>
    public string Mode { get; set; } = string.Empty;

    /// <summary>
    /// Initializes a new instance of VfoInfo
    /// </summary>
    public VfoInfo()
    {
    }

    /// <summary>
    /// Initializes a new instance of VfoInfo with specified frequency and mode
    /// </summary>
    /// <param name="frequency">Frequency in Hz</param>
    /// <param name="mode">Operating mode</param>
    public VfoInfo(long frequency, string mode)
    {
        Frequency = frequency;
        Mode = mode ?? string.Empty;
    }

    /// <summary>
    /// Creates a copy of this VfoInfo
    /// </summary>
    /// <returns>A new VfoInfo instance with the same values</returns>
    public VfoInfo Clone()
    {
        return new VfoInfo(Frequency, Mode);
    }

    public override string ToString()
    {
        return $"Freq: {Frequency:N0} Hz, Mode: {Mode}";
    }
}