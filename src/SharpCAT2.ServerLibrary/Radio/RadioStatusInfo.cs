namespace SharpCAT2.ServerLibrary.Radio;

/// <summary>
/// Structured radio status information for separation of concerns.
/// Contains raw data without presentation formatting.
/// </summary>
public record RadioStatusInfo
{
    public static RadioStatusInfo FromRadioStatus(SharpCAT2.Core.Radio.IRadio radio, SharpCAT2.Core.Radio.RadioStatus status) => new()
    {
        Manufacturer = radio.Manufacturer, ModelName = radio.ModelName,
        Frequency = status.Frequency, Mode = status.Mode, CurrentVfo = status.CurrentVfo,
        IsTransmitting = status.IsTransmitting, IsPoweredOn = status.IsPoweredOn,
        IsPowerStateObserved = status.IsPowerStateObserved, IsTransmitStateObserved = status.IsTransmitStateObserved,
        Timestamp = status.Timestamp, FeatureCount = SharpCAT2.Core.Radio.SupportedFeaturesExtensions.GetFeatureCount(radio.SupportedFeatures),
        FeaturesDescription = SharpCAT2.Core.Radio.SupportedFeaturesExtensions.GetDescription(radio.SupportedFeatures),
        IsConnected = radio.IsConnected, AdditionalProperties = new(status.AdditionalInfo)
    };

    /// <summary>
    /// Radio manufacturer name
    /// </summary>
    public string Manufacturer { get; init; } = string.Empty;

    /// <summary>
    /// Radio model name
    /// </summary>
    public string ModelName { get; init; } = string.Empty;

    /// <summary>
    /// Current frequency in Hz
    /// </summary>
    public long Frequency { get; init; }

    /// <summary>
    /// Current operating mode
    /// </summary>
    public string Mode { get; init; } = string.Empty;

    /// <summary>
    /// Current VFO (A or B)
    /// </summary>
    public string CurrentVfo { get; init; } = string.Empty;

    /// <summary>
    /// Whether radio is currently transmitting
    /// </summary>
    public bool IsTransmitting { get; init; }

    /// <summary>
    /// Whether radio is powered on
    /// </summary>
    public bool IsPoweredOn { get; init; }
    public bool IsPowerStateObserved { get; init; }
    public bool IsTransmitStateObserved { get; init; }

    /// <summary>
    /// Timestamp of status reading
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.Now;

    /// <summary>
    /// Number of supported features
    /// </summary>
    public int FeatureCount { get; init; }

    /// <summary>
    /// Description of supported features
    /// </summary>
    public string FeaturesDescription { get; init; } = string.Empty;

    /// <summary>
    /// Whether radio is connected
    /// </summary>
    public bool IsConnected { get; init; }

    /// <summary>
    /// Additional status properties for future extension
    /// </summary>
    public Dictionary<string, object> AdditionalProperties { get; init; } = new();
}