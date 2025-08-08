using System.Text.RegularExpressions;

namespace SharpCAT2.Common.Radio.Models.Yaesu;

/// <summary>
/// Yaesu FT-991A radio implementation
/// </summary>
public class YaesuFT991A : BaseRadio
{
    public override string ModelName => "FT-991A";
    public override string Manufacturer => "Yaesu";

    /// <summary>
    /// FT-991A supports basic to intermediate features
    /// </summary>
    public override SupportedFeatures SupportedFeatures => 
        SupportedFeatures.FrequencyControl | 
        SupportedFeatures.ModeControl |
        SupportedFeatures.DualVFO | 
        SupportedFeatures.SplitOperation |
        SupportedFeatures.PowerOutput |
        SupportedFeatures.SMeter |
        SupportedFeatures.MemoryChannels |
        SupportedFeatures.TransmitStatus |
        SupportedFeatures.ReceiveStatus |
        SupportedFeatures.RadioID |
        SupportedFeatures.DigitalModes;

    protected override long ParseFrequency(string response)
    {
        // Yaesu format may be different, but let's use similar to Kenwood for compatibility
        var match = Regex.Match(response, @"FA(\d{8,11})");
        if (match.Success && long.TryParse(match.Groups[1].Value, out long freq))
        {
            return freq;
        }
        return 0;
    }

    protected override string MapModeNumber(int modeNumber)
    {
        // Yaesu FT-991A mode mapping
        return modeNumber switch
        {
            1 => "LSB",
            2 => "USB",
            3 => "CW",
            4 => "FM",
            5 => "AM",
            6 => "RTTY-L",
            7 => "CW-R",
            8 => "DATA-L",
            9 => "RTTY-U",
            10 => "DATA-U",
            11 => "FM-N",
            _ => "USB"
        };
    }

    protected override void ParseTransceiverInfo(string response, RadioStatus status)
    {
        // Yaesu IF response handling - may have different format than Kenwood
        if (response.StartsWith("IF") && response.Length >= 27)
        {
            try
            {
                // Yaesu might have a different format, this is a basic implementation
                // Extract frequency (assuming similar position)
                if (response.Length >= 13 && long.TryParse(response.Substring(2, 8), out long freq))
                {
                    status.Frequency = freq * 10; // Yaesu might use different scaling
                }

                // Extract other status info - this would need to be adjusted based on actual Yaesu protocol
                if (response.Length > 20)
                {
                    // Extract TX status and mode based on Yaesu specific format
                    // This is simplified and would need actual protocol documentation
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing FT-991A transceiver info: {ex.Message}");
            }
        }
    }

    protected override bool IsCompleteResponse(string response)
    {
        // Yaesu might use different termination
        return response.EndsWith(";") || response.EndsWith("\r\n") || response.Length > 0;
    }
}