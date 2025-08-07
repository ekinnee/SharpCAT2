using System.Text.RegularExpressions;

namespace SharpCAT2.Radio.Models.Kenwood;

/// <summary>
/// Kenwood TS-2000 radio implementation
/// </summary>
public class KenwoodTS2000 : BaseRadio
{
    public override string ModelName => "TS-2000";
    public override string Manufacturer => "Kenwood";

    /// <summary>
    /// TS-2000 supports most HF features
    /// </summary>
    public override SupportedFeatures SupportedFeatures => 
        SupportedFeatures.FrequencyControl | 
        SupportedFeatures.ModeControl |
        SupportedFeatures.DualVFO | 
        SupportedFeatures.VFOSwap |
        SupportedFeatures.SplitOperation |
        SupportedFeatures.RIT | 
        SupportedFeatures.XIT |
        SupportedFeatures.PowerOutput |
        SupportedFeatures.SMeter |
        SupportedFeatures.AntennaSelection |
        SupportedFeatures.MemoryChannels |
        SupportedFeatures.TransmitStatus |
        SupportedFeatures.ReceiveStatus |
        SupportedFeatures.RadioID |
        SupportedFeatures.PowerOnOff;

    protected override long ParseFrequency(string response)
    {
        // Kenwood TS-2000 format: FA00014074000;
        var match = Regex.Match(response, @"FA(\d{11})");
        if (match.Success && long.TryParse(match.Groups[1].Value, out long freq))
        {
            return freq;
        }
        return 0;
    }

    protected override void ParseTransceiverInfo(string response, RadioStatus status)
    {
        // Kenwood IF response format: IF00014074000     +0000000000030000000;
        // Positions: IF + freq(11) + space(5) + ritoffset(5) + ritflag(1) + xitflag(1) + ch(3) + tx(1) + mode(1) + fr(1) + scan(1) + split(1) + tone(1) + toneno(2) + shift(1)
        if (response.StartsWith("IF") && response.Length >= 38)
        {
            try
            {
                // Extract frequency (positions 2-12)
                if (long.TryParse(response.Substring(2, 11), out long freq))
                {
                    status.Frequency = freq;
                }

                // Extract TX status (position 28)
                if (response.Length > 28)
                {
                    status.IsTransmitting = response[28] == '1';
                }

                // Extract mode (position 29)
                if (response.Length > 29 && int.TryParse(response[29].ToString(), out int mode))
                {
                    status.Mode = MapModeNumber(mode);
                }

                // Extract VFO (position 30)
                if (response.Length > 30)
                {
                    status.CurrentVfo = response[30] == '0' ? "A" : "B";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing transceiver info: {ex.Message}");
            }
        }
    }
}