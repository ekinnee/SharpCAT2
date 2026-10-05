using SharpCAT2.ServerLibrary.Radio;
using System.Text;

namespace SharpCAT2.ServerLibrary;

/// <summary>
/// Implementation of command display service for formatting command results.
/// Handles all presentation formatting logic separately from business logic.
/// </summary>
public class CommandDisplayService : ICommandDisplayService
{
    public string FormatRadioStatus(RadioStatusInfo statusInfo)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Radio Status:");
        sb.AppendLine($"  Model: {statusInfo.Manufacturer} {statusInfo.ModelName}");
        sb.AppendLine($"  Frequency: {statusInfo.Frequency:N0} Hz");
        sb.AppendLine($"  Mode: {statusInfo.Mode}");
        sb.AppendLine($"  VFO: {statusInfo.CurrentVfo}");
        sb.AppendLine($"  Transmitting: {(statusInfo.IsTransmitStateObserved ? statusInfo.IsTransmitting.ToString() : "Unknown")}");
        sb.AppendLine($"  Power: {(statusInfo.IsPowerStateObserved ? statusInfo.IsPoweredOn.ToString() : "Unknown")}");
        sb.AppendLine($"  Timestamp: {statusInfo.Timestamp:HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("Supported Features:");
        sb.AppendLine($"  Feature Count: {statusInfo.FeatureCount}");
        sb.AppendLine($"  Features: {statusInfo.FeaturesDescription}");
        
        return sb.ToString();
    }

    public string FormatRadioInfo(string radioName, string manufacturer, string modelName, 
        int featureCount, IEnumerable<string> supportedFeatures)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Radio Information: {radioName}");
        sb.AppendLine("====================================");
        sb.AppendLine($"Manufacturer: {manufacturer}");
        sb.AppendLine($"Model: {modelName}");
        sb.AppendLine($"Feature Count: {featureCount}");
        sb.AppendLine();
        sb.AppendLine("Supported Features:");
        sb.AppendLine("==================");
        
        var features = supportedFeatures.ToList();
        if (features.Count == 0)
        {
            sb.AppendLine("  Basic operation only");
        }
        else
        {
            foreach (var feature in features.OrderBy(f => f))
            {
                sb.AppendLine($"  ✓ {feature}");
            }
        }

        return sb.ToString();
    }

    public string FormatRadioList(Dictionary<string, string> radios)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Available Radio Models:");
        sb.AppendLine("======================");
        
        if (radios.Count == 0)
        {
            sb.AppendLine("No radio models found.");
        }
        else
        {
            foreach (var radio in radios.OrderBy(r => r.Key))
            {
                sb.AppendLine($"  {radio.Key}");
            }
            
            sb.AppendLine();
            sb.AppendLine($"Found {radios.Count} radio model(s).");
            sb.AppendLine("Use --radio \"Manufacturer Model\" to specify a radio.");
            sb.AppendLine("Use --auto-detect to automatically detect the radio type.");
        }
        
        return sb.ToString();
    }

    public string FormatCurrentRadioResponse(RadioStatusInfo? statusInfo, string? portName)
    {
        if (statusInfo != null)
        {
            var connectionStatus = statusInfo.IsConnected ? "CONNECTED" : "DISCONNECTED";
            return $"CURRENT_RADIO:{statusInfo.Manufacturer} {statusInfo.ModelName}|{portName ?? "Unknown"}|{connectionStatus}";
        }
        else
        {
            return "CURRENT_RADIO:NONE";
        }
    }

    public string FormatNetworkResponse(string command, object data)
    {
        return command.ToLower() switch
        {
            "radio_list_start" => "RADIO_LIST_START",
            "radio_list_end" => "RADIO_LIST_END",
            "serialport_list_start" => "SERIALPORT_LIST_START", 
            "serialport_list_end" => "SERIALPORT_LIST_END",
            _ => data.ToString() ?? ""
        };
    }
}