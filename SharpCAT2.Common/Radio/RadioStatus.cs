namespace SharpCAT2.Common.Radio;

/// <summary>
/// Represents the current status of a radio
/// </summary>
public class RadioStatus
{
    /// <summary>
    /// Gets or sets the current frequency in Hz
    /// </summary>
    public long Frequency { get; set; }

    /// <summary>
    /// Gets or sets the current operating mode
    /// </summary>
    public string Mode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the radio is transmitting
    /// </summary>
    public bool IsTransmitting { get; set; }

    /// <summary>
    /// Gets or sets the current antenna
    /// </summary>
    public int Antenna { get; set; }

    /// <summary>
    /// Gets or sets the signal strength/S-meter reading
    /// </summary>
    public int SignalStrength { get; set; }

    /// <summary>
    /// Gets or sets whether the radio is powered on
    /// </summary>
    public bool IsPoweredOn { get; set; }

    /// <summary>
    /// Gets or sets the VFO (Variable Frequency Oscillator) currently in use
    /// </summary>
    public string CurrentVfo { get; set; } = "A";

    /// <summary>
    /// Gets or sets additional status information
    /// </summary>
    public Dictionary<string, object> AdditionalInfo { get; set; } = new();

    /// <summary>
    /// Gets or sets the timestamp when this status was retrieved
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public override string ToString()
    {
        return $"Freq: {Frequency:N0} Hz, Mode: {Mode}, VFO: {CurrentVfo}, TX: {IsTransmitting}, Power: {IsPoweredOn}";
    }
}