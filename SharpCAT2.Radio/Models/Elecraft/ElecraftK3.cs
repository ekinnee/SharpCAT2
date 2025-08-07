using System.Text.RegularExpressions;

namespace SharpCAT2.Radio.Models.Elecraft;

/// <summary>
/// Elecraft K3 radio implementation
/// </summary>
public class ElecraftK3 : BaseRadio
{
    public override string ModelName => "K3";
    public override string Manufacturer => "Elecraft";

    protected override long ParseFrequency(string response)
    {
        // Elecraft K3 format: FA00014074000;
        var match = Regex.Match(response, @"FA(\d{11})");
        if (match.Success && long.TryParse(match.Groups[1].Value, out long freq))
        {
            return freq;
        }
        return 0;
    }

    protected override string MapModeNumber(int modeNumber)
    {
        // Elecraft K3 mode mapping
        return modeNumber switch
        {
            1 => "LSB",
            2 => "USB",
            3 => "CW",
            4 => "FM",
            5 => "AM",
            6 => "DATA",
            7 => "CW-REV",
            8 => "DATA-REV",
            9 => "AM-S",
            _ => "USB"
        };
    }

    protected override void ParseTransceiverInfo(string response, RadioStatus status)
    {
        // Elecraft K3 IF response handling
        if (response.StartsWith("IF") && response.Length >= 38)
        {
            try
            {
                // Extract frequency
                if (long.TryParse(response.Substring(2, 11), out long freq))
                {
                    status.Frequency = freq;
                }

                // Extract TX status
                if (response.Length > 28)
                {
                    status.IsTransmitting = response[28] == '1';
                }

                // Extract mode
                if (response.Length > 29 && int.TryParse(response[29].ToString(), out int mode))
                {
                    status.Mode = MapModeNumber(mode);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing K3 transceiver info: {ex.Message}");
            }
        }
    }
}