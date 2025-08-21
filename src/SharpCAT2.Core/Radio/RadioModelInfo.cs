using System.Text;

namespace SharpCAT2.Core.Radio;

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

    /// <summary>
    /// Provides a user-friendly, multi-line string representation of the radio model information
    /// </summary>
    /// <returns>Formatted radio information string</returns>
    public override string ToString()
    {
        var sb = new StringBuilder();
        
        // Header
        sb.AppendLine("Radio Model Information:");
        sb.AppendLine("------------------------");
        
        // Basic information
        sb.AppendLine($"Name: {RadioName}");
        sb.AppendLine($"Manufacturer: {Manufacturer}");
        sb.AppendLine($"Model Name: {ModelName}");
        sb.AppendLine($"Feature Count: {FeatureCount}");
        
        // Supported features
        sb.AppendLine("Supported Features:");
        if (SupportedFeatures.Count > 0)
        {
            foreach (var feature in SupportedFeatures)
            {
                sb.AppendLine($"  - {feature}");
            }
        }
        else
        {
            sb.AppendLine("  (None)");
        }
        
        sb.AppendLine($"Is Full Feature Set: {IsFullFeatureSet}");
        
        // Additional properties
        if (AdditionalProperties.Count > 0)
        {
            sb.AppendLine("Additional Properties:");
            foreach (var kvp in AdditionalProperties)
            {
                sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
            }
        }
        
        return sb.ToString().TrimEnd();
    }
}