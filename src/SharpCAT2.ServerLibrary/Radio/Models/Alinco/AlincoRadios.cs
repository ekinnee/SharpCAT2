using SharpCAT2.Core.Radio;
using System.Text.RegularExpressions;

namespace SharpCAT2.ServerLibrary.Radio.Models.Alinco;

/// <summary>
/// Base class for Alinco radios
/// Alinco uses simplified CAT commands similar to Kenwood
/// </summary>
public abstract class BaseAlincoRadio : BaseRadio
{
    public override string Manufacturer => "Alinco";

    protected override bool IsCompleteResponse(string response)
    {
        return response.EndsWith(";") || response.EndsWith("\r\n") || response.Length > 0;
    }

    protected override long ParseFrequency(string response)
    {
        // Alinco format similar to Kenwood: FA00014074000;
        var match = Regex.Match(response, @"F[AB](\d{8,11})");
        if (match.Success && long.TryParse(match.Groups[1].Value, out long freq))
        {
            return freq;
        }
        return 0;
    }

    protected override string MapModeNumber(int modeNumber)
    {
        return modeNumber switch
        {
            1 => "LSB",
            2 => "USB",
            3 => "CW",
            4 => "FM",
            5 => "AM",
            6 => "DIG",
            _ => "USB"
        };
    }

    protected override void ParseTransceiverInfo(string response, RadioStatus status)
    {
        if (!string.IsNullOrEmpty(response))
        {
            try
            {
                // Basic Alinco status parsing
                if (response.StartsWith("IF") && response.Length >= 27)
                {
                    // Parse frequency
                    if (response.Length >= 13 && long.TryParse(response.Substring(2, 8), out long freq))
                    {
                        status.Frequency = freq * 10;
                    }

                    // Parse mode
                    if (response.Length > 20)
                    {
                        var modeChar = response.Substring(20, 1);
                        if (int.TryParse(modeChar, out int mode))
                        {
                            status.Mode = MapModeNumber(mode);
                        }
                    }

                    status.IsPoweredOn = true;
                }
            }
            catch (Exception)
            {
                // Parsing errors result in incomplete status
                // Service layer can detect issues through missing status fields
            }
        }
    }

    // Basic VFO operations
    public override async Task<string> GetVfoAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.DualVFO))
            throw new NotSupportedException("Dual VFO not supported");

        var command = new RadioCommand("FN;", "Get VFO", true, 2000);
        var response = await SendCommandAsync(command);
        if (!string.IsNullOrEmpty(response))
        {
            var match = Regex.Match(response, @"FN(\d)");
            if (match.Success)
            {
                return match.Groups[1].Value == "0" ? "A" : "B";
            }
        }
        return "A";
    }

    public override async Task<bool> SetVfoAsync(string vfo)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.DualVFO))
            return false;

        var command = new RadioCommand($"FN{(vfo.ToUpper() == "A" ? "0" : "1")};", $"Set VFO to {vfo}", false, 2000);
        var response = await SendCommandAsync(command);
        return !string.IsNullOrEmpty(response);
    }

    // Power control
    public override async Task<bool> SetPowerOutputAsync(int powerPercent)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.PowerOutput))
            return false;

        var command = new RadioCommand($"PC{powerPercent:D3};", $"Set power to {powerPercent}%", false, 2000);
        var response = await SendCommandAsync(command);
        return !string.IsNullOrEmpty(response);
    }

    public override async Task<int> GetPowerOutputAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.PowerOutput))
            return 0;

        var command = new RadioCommand("PC;", "Get power output", true, 2000);
        var response = await SendCommandAsync(command);
        if (!string.IsNullOrEmpty(response))
        {
            var match = Regex.Match(response, @"PC(\d{3})");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int power))
            {
                return power;
            }
        }
        return 0;
    }
}

/// <summary>
/// Alinco DX-SR8T radio implementation
/// HF/6m transceiver with basic features
/// </summary>
public class AlincoDXSR8T : BaseAlincoRadio
{
    public override string ModelName => "DX-SR8T";

    /// <summary>
    /// DX-SR8T supports basic to intermediate HF features
    /// </summary>
    public override SupportedFeatures SupportedFeatures => 
        SupportedFeatures.FrequencyControl | 
        SupportedFeatures.ModeControl |
        SupportedFeatures.DualVFO | 
        SupportedFeatures.VFOSwap |
        SupportedFeatures.SplitOperation |
        SupportedFeatures.RIT | 
        SupportedFeatures.PowerOutput |
        SupportedFeatures.SMeter |
        SupportedFeatures.MemoryChannels |
        SupportedFeatures.CWKeyer |
        SupportedFeatures.TransmitStatus |
        SupportedFeatures.ReceiveStatus |
        SupportedFeatures.RadioID |
        SupportedFeatures.ComputerControl;
}

/// <summary>
/// Alinco DJ-MD5TGP radio implementation
/// VHF/UHF handheld with DMR digital mode
/// </summary>
public class AlincoDJMD5TGP : BaseAlincoRadio
{
    public override string ModelName => "DJ-MD5TGP";

    /// <summary>
    /// DJ-MD5TGP supports VHF/UHF and digital modes
    /// </summary>
    public override SupportedFeatures SupportedFeatures => 
        SupportedFeatures.FrequencyControl | 
        SupportedFeatures.ModeControl |
        SupportedFeatures.MemoryChannels |
        SupportedFeatures.MemoryBanks |
        SupportedFeatures.MemoryScan |
        SupportedFeatures.FrequencyScan |
        SupportedFeatures.SQLControl |
        SupportedFeatures.CTCSSTone |
        SupportedFeatures.DTCSCode |
        SupportedFeatures.RepeaterOffset |
        SupportedFeatures.PowerOutput |
        SupportedFeatures.TransmitStatus |
        SupportedFeatures.ReceiveStatus |
        SupportedFeatures.RadioID |
        SupportedFeatures.PowerOnOff |
        SupportedFeatures.DigitalModes |
        SupportedFeatures.ComputerControl;
}

/// <summary>
/// Alinco DR-638T radio implementation
/// VHF/UHF mobile transceiver with dual-band capability
/// </summary>
public class AlincoDR638T : BaseAlincoRadio
{
    public override string ModelName => "DR-638T";

    /// <summary>
    /// DR-638T supports dual-band mobile features
    /// </summary>
    public override SupportedFeatures SupportedFeatures => 
        SupportedFeatures.FrequencyControl | 
        SupportedFeatures.ModeControl |
        SupportedFeatures.DualVFO |
        SupportedFeatures.MemoryChannels |
        SupportedFeatures.MemoryBanks |
        SupportedFeatures.MemoryScan |
        SupportedFeatures.FrequencyScan |
        SupportedFeatures.SQLControl |
        SupportedFeatures.CTCSSTone |
        SupportedFeatures.DTCSCode |
        SupportedFeatures.RepeaterOffset |
        SupportedFeatures.PowerOutput |
        SupportedFeatures.TransmitStatus |
        SupportedFeatures.ReceiveStatus |
        SupportedFeatures.RadioID |
        SupportedFeatures.PowerOnOff |
        SupportedFeatures.ComputerControl;
}

/// <summary>
/// Alinco DX-70T radio implementation
/// VHF/UHF all-mode base station
/// </summary>
public class AlincoDX70T : BaseAlincoRadio
{
    public override string ModelName => "DX-70T";

    /// <summary>
    /// DX-70T supports all-mode VHF/UHF operation
    /// </summary>
    public override SupportedFeatures SupportedFeatures => 
        SupportedFeatures.FrequencyControl | 
        SupportedFeatures.ModeControl |
        SupportedFeatures.DualVFO |
        SupportedFeatures.MemoryChannels |
        SupportedFeatures.MemoryBanks |
        SupportedFeatures.PowerOutput |
        SupportedFeatures.SMeter |
        SupportedFeatures.SQLControl |
        SupportedFeatures.TransmitStatus |
        SupportedFeatures.ReceiveStatus |
        SupportedFeatures.RadioID |
        SupportedFeatures.PowerOnOff |
        SupportedFeatures.ComputerControl;
}