using SharpCAT2.Core.Radio;
using System.Text.RegularExpressions;

namespace SharpCAT2.Common.Radio.Protocols;

/// <summary>
/// FlexRadio VITA-49 and CAT protocol implementation
/// FlexRadio uses a combination of VITA-49 for streaming data and CAT commands for control
/// </summary>
public class FlexRadioProtocol : IRadioProtocol
{
    public virtual string ProtocolName => "FlexRadio VITA-49/CAT";

    // FlexRadio uses extended CAT commands with ZZ prefix
    public virtual RadioCommand GetFrequencyCommand() => new("ZZFA;", "Get slice A frequency", true, 2000);
    public virtual RadioCommand SetFrequencyCommand(long frequency) => new($"ZZFA{frequency:D11};", $"Set slice A frequency to {frequency} Hz", false, 2000);
    public virtual RadioCommand GetModeCommand() => new("ZZMD;", "Get slice A mode", true, 2000);
    public virtual RadioCommand SetModeCommand(string mode) => new($"ZZMD{MapModeToNumber(mode)};", $"Set slice A mode to {mode}", false, 2000);
    public virtual RadioCommand GetStatusCommand() => new("ZZIF;", "Get slice information", true, 2000);
    public virtual RadioCommand GetVfoCommand() => new("ZZVS;", "Get VFO status", true, 2000);
    public virtual RadioCommand SetVfoCommand(string vfo) => new($"ZZVS{(vfo.ToUpper() == "A" ? "0" : "1")};", $"Set active slice to {vfo}", false, 2000);
    public virtual RadioCommand SwapVfoCommand() => new("ZZSV;", "Swap slices", false, 2000);
    public virtual RadioCommand SetSplitCommand(bool enabled) => new($"ZZSP{(enabled ? "1" : "0")};", $"{(enabled ? "Enable" : "Disable")} split", false, 2000);
    public virtual RadioCommand GetSplitCommand() => new("ZZSP;", "Get split status", true, 2000);
    public virtual RadioCommand SetRitCommand(int offsetHz) => new($"ZZRT{offsetHz:+0000;-0000;+0000};", $"Set RIT offset to {offsetHz} Hz", false, 2000);
    public virtual RadioCommand GetRitCommand() => new("ZZRT;", "Get RIT offset", true, 2000);
    public virtual RadioCommand SetXitCommand(int offsetHz) => new($"ZZXT{offsetHz:+0000;-0000;+0000};", $"Set XIT offset to {offsetHz} Hz", false, 2000);
    public virtual RadioCommand GetXitCommand() => new("ZZXT;", "Get XIT offset", true, 2000);
    public virtual RadioCommand SetPowerOutputCommand(int powerPercent) => new($"ZZPC{powerPercent:D3};", $"Set power output to {powerPercent}%", false, 2000);
    public virtual RadioCommand GetPowerOutputCommand() => new("ZZPC;", "Get power output", true, 2000);
    public virtual RadioCommand GetSMeterCommand() => new("ZZSM;", "Get S-meter reading", true, 2000);
    public virtual RadioCommand GetSWRCommand() => new("ZZSW;", "Get SWR reading", true, 2000);
    public virtual RadioCommand SetAntennaCommand(int antenna) => new($"ZZAN{antenna};", $"Set antenna to {antenna}", false, 2000);
    public virtual RadioCommand GetAntennaCommand() => new("ZZAN;", "Get antenna", true, 2000);

    // FlexRadio-specific commands
    public virtual RadioCommand GetSliceListCommand() => new("ZZSL;", "Get slice list", true, 2000);
    public virtual RadioCommand CreateSliceCommand() => new("ZZCR;", "Create new slice", false, 2000);
    public virtual RadioCommand DeleteSliceCommand(int sliceId) => new($"ZZDL{sliceId};", $"Delete slice {sliceId}", false, 2000);
    public virtual RadioCommand GetWaterfallCommand() => new("ZZWF;", "Get waterfall status", true, 2000);
    public virtual RadioCommand SetWaterfallCommand(bool enabled) => new($"ZZWF{(enabled ? "1" : "0")};", $"{(enabled ? "Enable" : "Disable")} waterfall", false, 2000);
    public virtual RadioCommand GetPanadapterCommand() => new("ZZPN;", "Get panadapter status", true, 2000);
    public virtual RadioCommand SetPanadapterCommand(bool enabled) => new($"ZZPN{(enabled ? "1" : "0")};", $"{(enabled ? "Enable" : "Disable")} panadapter", false, 2000);

    // Response parsing methods
    public virtual bool IsCompleteResponse(string response)
    {
        return response.EndsWith(";") || response.StartsWith("FLEX") || response.Length > 20;
    }

    public virtual long ParseFrequency(string response)
    {
        // FlexRadio format: ZZFA00014074000; (11 digits in Hz)
        var match = Regex.Match(response, @"ZZF[AB](\d{11})");
        if (match.Success && long.TryParse(match.Groups[1].Value, out long freq))
        {
            return freq;
        }
        return 0;
    }

    public virtual string ParseMode(string response)
    {
        // FlexRadio format: ZZMDX; where X is mode number
        var match = Regex.Match(response, @"ZZMD(\d+)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int modeNum))
        {
            return MapModeNumber(modeNum);
        }
        return "USB";
    }

    public virtual string ParseVfo(string response)
    {
        // FlexRadio format: ZZVSN; where N is slice number (0=A, 1=B, etc.)
        var match = Regex.Match(response, @"ZZVS(\d)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int sliceNum))
        {
            return sliceNum == 0 ? "A" : "B";
        }
        return "A";
    }

    public virtual bool ParseSplit(string response)
    {
        return response.Contains("ZZSP1");
    }

    public virtual int ParseRit(string response)
    {
        var match = Regex.Match(response, @"ZZRT([+-]?\d+)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int offset))
        {
            return offset;
        }
        return 0;
    }

    public virtual int ParseXit(string response)
    {
        var match = Regex.Match(response, @"ZZXT([+-]?\d+)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int offset))
        {
            return offset;
        }
        return 0;
    }

    public virtual int ParsePowerOutput(string response)
    {
        var match = Regex.Match(response, @"ZZPC(\d{3})");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int power))
        {
            return power;
        }
        return 0;
    }

    public virtual int ParseSMeter(string response)
    {
        var match = Regex.Match(response, @"ZZSM(\d+)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int meter))
        {
            // FlexRadio S-meter is in dBm, convert to S-units
            return Math.Max(0, Math.Min(9 + (meter + 73) / 6, 20));
        }
        return 0;
    }

    public virtual double ParseSWR(string response)
    {
        var match = Regex.Match(response, @"ZZSW(\d+)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int swr))
        {
            // FlexRadio SWR format conversion
            return 1.0 + (swr / 100.0);
        }
        return 1.0;
    }

    public virtual int ParseAntenna(string response)
    {
        var match = Regex.Match(response, @"ZZAN(\d)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int antenna))
        {
            return antenna;
        }
        return 1;
    }

    public virtual void ParseTransceiverInfo(string response, RadioStatus status)
    {
        if (string.IsNullOrEmpty(response))
            return;

        try
        {
            // FlexRadio slice information format
            if (response.StartsWith("ZZIF") && response.Length >= 28)
            {
                // Parse slice frequency
                var freq = response.Substring(4, 11);
                if (long.TryParse(freq, out long frequency))
                {
                    status.Frequency = frequency;
                }

                if (response.Length >= 20)
                {
                    var modeChar = response.Substring(19, 1);
                    if (int.TryParse(modeChar, out int mode))
                    {
                        status.Mode = MapModeNumber(mode);
                    }
                }

                if (response.Length >= 21)
                {
                    var sliceChar = response.Substring(20, 1);
                    status.CurrentVfo = sliceChar == "0" ? "A" : "B";
                }

                status.IsPoweredOn = true;
            }
            else if (response.StartsWith("FLEX"))
            {
                // Parse FlexRadio-specific responses
                var parts = response.Split('|');
                foreach (var part in parts)
                {
                    if (part.StartsWith("FREQ:") && long.TryParse(part.Substring(5), out long freq))
                    {
                        status.Frequency = freq;
                    }
                    else if (part.StartsWith("MODE:"))
                    {
                        status.Mode = part.Substring(5);
                    }
                    else if (part.StartsWith("SLICE:"))
                    {
                        status.CurrentVfo = part.Substring(6);
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

    /// <summary>
    /// FlexRadio slice management helpers
    /// </summary>
    public virtual RadioCommand SetSliceFrequencyCommand(int sliceId, long frequency) => 
        new($"ZZFS{sliceId}{frequency:D11};", $"Set slice {sliceId} frequency to {frequency} Hz", false, 2000);
    
    public virtual RadioCommand SetSliceModeCommand(int sliceId, string mode) => 
        new($"ZZMS{sliceId}{MapModeToNumber(mode)};", $"Set slice {sliceId} mode to {mode}", false, 2000);

    #region Protected Methods

    /// <summary>
    /// FlexRadio mode mapping
    /// </summary>
    protected virtual int MapModeToNumber(string mode)
    {
        return mode.ToUpper() switch
        {
            "LSB" => 0,
            "USB" => 1,
            "DSB" => 2,
            "CWL" => 3,
            "CWU" => 4,
            "FM" => 5,
            "AM" => 6,
            "DIGU" => 7,
            "SPEC" => 8,
            "DIGL" => 9,
            "SAM" => 10,
            "DFM" => 11,
            _ => 1 // Default to USB
        };
    }

    protected virtual string MapModeNumber(int modeNumber)
    {
        return modeNumber switch
        {
            0 => "LSB",
            1 => "USB",
            2 => "DSB",
            3 => "CWL",
            4 => "CWU",
            5 => "FM",
            6 => "AM",
            7 => "DIGU",
            8 => "SPEC",
            9 => "DIGL",
            10 => "SAM",
            11 => "DFM",
            _ => "USB"
        };
    }

    #endregion
}