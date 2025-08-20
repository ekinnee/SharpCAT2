namespace SharpCAT2.Common.Radio;

/// <summary>
/// Structured radio model information for separation of concerns.
/// Contains radio model details without presentation formatting.
/// </summary>
public record RadioModelInfo
{
    /// <summary>
    /// Radio model name as provided by user
    /// </summary>
    public string RadioName { get; init; } = string.Empty;

    /// <summary>
    /// Manufacturer name
    /// </summary>
    public string Manufacturer { get; init; } = string.Empty;

    /// <summary>
    /// Model name
    /// </summary>
    public string ModelName { get; init; } = string.Empty;

    /// <summary>
    /// Number of supported features
    /// </summary>
    public int FeatureCount { get; init; }

    /// <summary>
    /// List of supported feature names
    /// </summary>
    public List<string> SupportedFeatures { get; init; } = new();

    /// <summary>
    /// Whether this radio supports all features
    /// </summary>
    public bool IsFullFeatureSet { get; init; }

    /// <summary>
    /// Additional properties for future extension
    /// </summary>
    public Dictionary<string, object> AdditionalProperties { get; init; } = new();
}