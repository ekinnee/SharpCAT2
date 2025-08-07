namespace SharpCAT2.Radio;

/// <summary>
/// Represents the current status of a radio
/// </summary>
public class RadioStatus
{
    private VfoInfo _vfoA = new();
    private VfoInfo _vfoB = new();
    private string _currentVfo = "A";

    /// <summary>
    /// Gets or sets VFO A information
    /// </summary>
    public VfoInfo VfoA 
    { 
        get => _vfoA; 
        set => _vfoA = value ?? new VfoInfo(); 
    }

    /// <summary>
    /// Gets or sets VFO B information
    /// </summary>
    public VfoInfo VfoB 
    { 
        get => _vfoB; 
        set => _vfoB = value ?? new VfoInfo(); 
    }

    /// <summary>
    /// Gets or sets the current frequency in Hz (references the active VFO)
    /// </summary>
    public long Frequency 
    { 
        get => ActiveVfo.Frequency; 
        set => ActiveVfo.Frequency = value; 
    }

    /// <summary>
    /// Gets or sets the current operating mode (references the active VFO)
    /// </summary>
    public string Mode 
    { 
        get => ActiveVfo.Mode; 
        set => ActiveVfo.Mode = value ?? string.Empty; 
    }

    /// <summary>
    /// Gets the active VFO based on CurrentVfo setting
    /// </summary>
    private VfoInfo ActiveVfo => _currentVfo == "B" ? _vfoB : _vfoA;

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
    public string CurrentVfo 
    { 
        get => _currentVfo; 
        set => _currentVfo = value ?? "A"; 
    }

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
        return $"VFO {CurrentVfo}: {Frequency:N0} Hz, Mode: {Mode}, TX: {IsTransmitting}, Power: {IsPoweredOn}";
    }

    /// <summary>
    /// Gets a detailed string representation showing both VFOs
    /// </summary>
    /// <returns>Detailed status string</returns>
    public string ToDetailedString()
    {
        return $"VFO A: {VfoA}, VFO B: {VfoB}, Active: {CurrentVfo}, TX: {IsTransmitting}, Power: {IsPoweredOn}";
    }
}