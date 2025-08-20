using System.Text;

namespace SharpCAT2.ClientLibrary;

/// <summary>
/// Utility class for parsing and formatting radio status responses
/// </summary>
public static class RadioStatusParser
{
    /// <summary>
    /// Parses a key=value semicolon-delimited status string and returns a human-friendly formatted version
    /// </summary>
    /// <param name="statusString">Raw status string (e.g., "MODEL=DummyRadio;PORT=COM7;FREQ=14074000;...")</param>
    /// <returns>Pretty-printed multi-line radio status summary</returns>
    public static string ParseAndFormat(string statusString)
    {
        if (string.IsNullOrWhiteSpace(statusString))
        {
            return "No radio status available.";
        }

        // Handle error responses
        if (statusString.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
        {
            return $"Radio Status Error: {statusString[6..].Trim()}";
        }

        try
        {
            var keyValuePairs = ParseKeyValuePairs(statusString);
            return FormatStatusDisplay(keyValuePairs);
        }
        catch (Exception ex)
        {
            return $"Error parsing radio status: {ex.Message}\nRaw response: {statusString}";
        }
    }

    /// <summary>
    /// Parses the semicolon-delimited key=value pairs into a dictionary
    /// </summary>
    private static Dictionary<string, string> ParseKeyValuePairs(string statusString)
    {
        var pairs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        var parts = statusString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var part in parts)
        {
            var equalIndex = part.IndexOf('=');
            if (equalIndex > 0 && equalIndex < part.Length - 1)
            {
                var key = part[..equalIndex].Trim();
                var value = part[(equalIndex + 1)..].Trim();
                pairs[key] = value;
            }
        }

        return pairs;
    }

    /// <summary>
    /// Formats the key-value pairs into a human-readable status display
    /// </summary>
    private static string FormatStatusDisplay(Dictionary<string, string> status)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("Radio Status:");
        sb.AppendLine("=============");

        // Radio Identity
        if (status.TryGetValue("MODEL", out var model))
        {
            sb.AppendLine($"Radio Model:      {model}");
        }
        if (status.TryGetValue("PORT", out var port))
        {
            sb.AppendLine($"Serial Port:      {port}");
        }

        sb.AppendLine();

        // Operating Status
        sb.AppendLine("Operating Status:");
        sb.AppendLine("-----------------");
        
        if (status.TryGetValue("POWER", out var power))
        {
            sb.AppendLine($"Power:            {(power.ToUpper() == "TRUE" ? "ON" : "OFF")}");
        }
        
        if (status.TryGetValue("TX", out var tx))
        {
            sb.AppendLine($"Transmitting:     {(tx.ToUpper() == "TRUE" ? "YES" : "NO")}");
        }

        // Frequency and Mode
        sb.AppendLine();
        sb.AppendLine("Frequency & Mode:");
        sb.AppendLine("-----------------");
        
        if (status.TryGetValue("FREQ", out var freq) && long.TryParse(freq, out var freqHz))
        {
            sb.AppendLine($"Frequency:        {FormatFrequencyMHz(freqHz)}");
        }
        
        if (status.TryGetValue("MODE", out var mode))
        {
            sb.AppendLine($"Mode:             {mode}");
        }
        
        if (status.TryGetValue("VFO", out var vfo))
        {
            sb.AppendLine($"Active VFO:       {vfo}");
        }

        // Split and Tuning
        sb.AppendLine();
        sb.AppendLine("Split & Tuning:");
        sb.AppendLine("---------------");
        
        if (status.TryGetValue("SPLIT", out var split))
        {
            sb.AppendLine($"Split Operation:  {(split.ToUpper() == "TRUE" ? "ENABLED" : "DISABLED")}");
        }
        
        if (status.TryGetValue("RIT", out var rit))
        {
            var ritStatus = rit.ToUpper() == "TRUE" ? "ENABLED" : "DISABLED";
            if (status.TryGetValue("RIT_OFFSET", out var ritOffset) && ritStatus == "ENABLED")
            {
                sb.AppendLine($"RIT:              {ritStatus} ({ritOffset} Hz)");
            }
            else
            {
                sb.AppendLine($"RIT:              {ritStatus}");
            }
        }
        
        if (status.TryGetValue("XIT", out var xit))
        {
            var xitStatus = xit.ToUpper() == "TRUE" ? "ENABLED" : "DISABLED";
            if (status.TryGetValue("XIT_OFFSET", out var xitOffset) && xitStatus == "ENABLED")
            {
                sb.AppendLine($"XIT:              {xitStatus} ({xitOffset} Hz)");
            }
            else
            {
                sb.AppendLine($"XIT:              {xitStatus}");
            }
        }

        // Power and Meters
        sb.AppendLine();
        sb.AppendLine("Power & Meters:");
        sb.AppendLine("---------------");
        
        if (status.TryGetValue("POWER_LEVEL", out var powerLevel))
        {
            sb.AppendLine($"Power Output:     {powerLevel}%");
        }
        
        if (status.TryGetValue("S_METER", out var sMeter))
        {
            sb.AppendLine($"S-Meter:          {FormatSMeter(sMeter)}");
        }
        
        if (status.TryGetValue("SWR", out var swr))
        {
            sb.AppendLine($"SWR:              {swr}:1");
        }

        // Additional Settings
        var hasAdditional = false;
        if (status.TryGetValue("ANTENNA", out var antenna))
        {
            if (!hasAdditional)
            {
                sb.AppendLine();
                sb.AppendLine("Additional Settings:");
                sb.AppendLine("--------------------");
                hasAdditional = true;
            }
            sb.AppendLine($"Antenna:          {antenna}");
        }
        
        if (status.TryGetValue("MEMORY", out var memory))
        {
            if (!hasAdditional)
            {
                sb.AppendLine();
                sb.AppendLine("Additional Settings:");
                sb.AppendLine("--------------------");
                hasAdditional = true;
            }
            sb.AppendLine($"Memory Channel:   {memory}");
        }
        
        if (status.TryGetValue("IF_BW", out var ifBw))
        {
            if (!hasAdditional)
            {
                sb.AppendLine();
                sb.AppendLine("Additional Settings:");
                sb.AppendLine("--------------------");
                hasAdditional = true;
            }
            sb.AppendLine($"IF Bandwidth:     {ifBw} Hz");
        }
        
        if (status.TryGetValue("NR", out var nr))
        {
            if (!hasAdditional)
            {
                sb.AppendLine();
                sb.AppendLine("Additional Settings:");
                sb.AppendLine("--------------------");
                hasAdditional = true;
            }
            var nrLevel = nr == "0" ? "OFF" : $"Level {nr}";
            sb.AppendLine($"Noise Reduction:  {nrLevel}");
        }

        // Timestamp
        if (status.TryGetValue("TIMESTAMP", out var timestamp))
        {
            sb.AppendLine();
            sb.AppendLine($"Status Retrieved: {timestamp}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formats frequency in Hz to MHz with 5 decimal places
    /// </summary>
    private static string FormatFrequencyMHz(long frequencyHz)
    {
        var frequencyMHz = frequencyHz / 1_000_000.0;
        return $"{frequencyMHz:F5} MHz";
    }

    /// <summary>
    /// Formats S-meter reading into a more human-readable format
    /// </summary>
    private static string FormatSMeter(string sMeterValue)
    {
        if (int.TryParse(sMeterValue, out var value))
        {
            if (value <= 9)
            {
                return $"S{value}";
            }
            else
            {
                var dbOver = value - 9;
                return $"S9+{dbOver}dB";
            }
        }
        
        return sMeterValue;
    }
}