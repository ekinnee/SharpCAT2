using SharpCAT2.Core.Radio;
using System.Text.RegularExpressions;

namespace SharpCAT2.Common.Radio.Protocols;

/// <summary>
/// Yaesu CAT (Computer Aided Transceiver) protocol implementation
/// Used by modern Yaesu radios with CAT interface
/// </summary>
public class YaesuCATProtocol : IRadioProtocol
{
    public virtual string ProtocolName => "Yaesu CAT";

    // Core command creation methods
    public virtual RadioCommand GetFrequencyCommand() => new("FA;", "Get VFO A frequency", true, 2000);
    public virtual RadioCommand SetFrequencyCommand(long frequency) => new($"FA{frequency / 10:D8};", $"Set VFO A frequency to {frequency} Hz", false, 2000);
    public virtual RadioCommand GetModeCommand() => new("MD0;", "Get mode", true, 2000);
    public virtual RadioCommand SetModeCommand(string mode) => new($"MD0{MapModeToNumber(mode)};", $"Set mode to {mode}", false, 2000);
    public virtual RadioCommand GetStatusCommand() => new("IF;", "Get transceiver information", true, 2000);
    public virtual RadioCommand GetVfoCommand() => new("VS;", "Get VFO status", true, 2000);
    public virtual RadioCommand SetVfoCommand(string vfo) => new($"VS{(vfo.ToUpper() == "A" ? "0" : "1")};", $"Set VFO to {vfo}", false, 2000);
    public virtual RadioCommand SwapVfoCommand() => new("SV;", "Swap VFO A and B", false, 2000);
    public virtual RadioCommand SetSplitCommand(bool enabled) => new($"ST{(enabled ? "1" : "0")};", $"{(enabled ? "Enable" : "Disable")} split", false, 2000);
    public virtual RadioCommand GetSplitCommand() => new("ST;", "Get split status", true, 2000);
    public virtual RadioCommand SetRitCommand(int offsetHz) => new($"RC{(offsetHz >= 0 ? "+" : "-")}{Math.Abs(offsetHz):D4};", $"Set RIT offset to {offsetHz} Hz", false, 2000);
    public virtual RadioCommand GetRitCommand() => new("RC;", "Get RIT offset", true, 2000);
    public virtual RadioCommand SetXitCommand(int offsetHz) => new($"RT{(offsetHz >= 0 ? "+" : "-")}{Math.Abs(offsetHz):D4};", $"Set XIT offset to {offsetHz} Hz", false, 2000);
    public virtual RadioCommand GetXitCommand() => new("RT;", "Get XIT offset", true, 2000);
    public virtual RadioCommand SetPowerOutputCommand(int powerPercent) => new($"PC{powerPercent:D3};", $"Set power output to {powerPercent}%", false, 2000);
    public virtual RadioCommand GetPowerOutputCommand() => new("PC;", "Get power output", true, 2000);
    public virtual RadioCommand GetSMeterCommand() => new("SM0;", "Get S-meter reading", true, 2000);
    public virtual RadioCommand GetSWRCommand() => new("RM6;", "Get SWR reading", true, 2000);
    public virtual RadioCommand SetAntennaCommand(int antenna) => new($"AN{antenna};", $"Set antenna to {antenna}", false, 2000);
    public virtual RadioCommand GetAntennaCommand() => new("AN;", "Get antenna", true, 2000);

    // Yaesu-specific commands
    public virtual RadioCommand GetFrequencyBCommand() => new("FB;", "Get VFO B frequency", true, 2000);
    public virtual RadioCommand SetFrequencyBCommand(long frequency) => new($"FB{frequency / 10:D8};", $"Set VFO B frequency to {frequency} Hz", false, 2000);
    public virtual RadioCommand GetIfBandwidthCommand() => new("NA0;", "Get IF bandwidth", true, 2000);
    public virtual RadioCommand SetIfBandwidthCommand(int bandwidth) => new($"NA0{MapBandwidthToNumber(bandwidth)};", $"Set IF bandwidth", false, 2000);
    public virtual RadioCommand GetMemoryChannelCommand() => new("MC;", "Get memory channel", true, 2000);
    public virtual RadioCommand SetMemoryChannelCommand(int channel) => new($"MC{channel:D3};", $"Set memory channel to {channel}", false, 2000);
    public virtual RadioCommand GetCwSpeedCommand() => new("KS;", "Get CW speed", true, 2000);
    public virtual RadioCommand SetCwSpeedCommand(int wpm) => new($"KS{wpm:D3};", $"Set CW speed to {wpm} WPM", false, 2000);
    public virtual RadioCommand GetNoiseReductionCommand() => new("NR0;", "Get noise reduction", true, 2000);
    public virtual RadioCommand SetNoiseReductionCommand(int level) => new($"NR0{level};", $"Set noise reduction to level {level}", false, 2000);
    public virtual RadioCommand GetAGCCommand() => new("GT;", "Get AGC", true, 2000);
    public virtual RadioCommand SetAGCCommand(int level) => new($"GT{level:D3};", $"Set AGC to level {level}", false, 2000);
    public virtual RadioCommand PowerOnCommand() => new("PS1;", "Power on", false, 5000);
    public virtual RadioCommand PowerOffCommand() => new("PS0;", "Power off", false, 5000);
    public virtual RadioCommand GetPowerStatusCommand() => new("PS;", "Get power status", true, 2000);

    // Response parsing methods
    public virtual bool IsCompleteResponse(string response)
    {
        return response.EndsWith(";") || response.Length > 30; // Yaesu responses end with semicolon
    }

    public virtual long ParseFrequency(string response)
    {
        // Yaesu format: FA12345678; (8 digits in 10Hz units)
        var match = Regex.Match(response, @"F[AB](\d{8})");
        if (match.Success && long.TryParse(match.Groups[1].Value, out long freq))
        {
            return freq * 10; // Convert from 10Hz units to Hz
        }
        return 0;
    }

    public virtual string ParseMode(string response)
    {
        // Yaesu format: MD0X; where X is mode number
        var match = Regex.Match(response, @"MD0(\d)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int modeNum))
        {
            return MapModeNumber(modeNum);
        }
        return "USB";
    }

    public virtual string ParseVfo(string response)
    {
        // Yaesu format: VSX; where X is 0=VFO-A, 1=VFO-B
        var match = Regex.Match(response, @"VS(\d)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int vfoNum))
        {
            return vfoNum == 0 ? "A" : "B";
        }
        return "A";
    }

    public virtual bool ParseSplit(string response)
    {
        return response.Contains("ST1");
    }

    public virtual int ParseRit(string response)
    {
        var match = Regex.Match(response, @"RC([+-])(\d{4})");
        if (match.Success && int.TryParse(match.Groups[2].Value, out int offset))
        {
            return match.Groups[1].Value == "-" ? -offset : offset;
        }
        return 0;
    }

    public virtual int ParseXit(string response)
    {
        var match = Regex.Match(response, @"RT([+-])(\d{4})");
        if (match.Success && int.TryParse(match.Groups[2].Value, out int offset))
        {
            return match.Groups[1].Value == "-" ? -offset : offset;
        }
        return 0;
    }

    public virtual int ParsePowerOutput(string response)
    {
        var match = Regex.Match(response, @"PC(\d{3})");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int power))
        {
            return power;
        }
        return 0;
    }

    public virtual int ParseSMeter(string response)
    {
        var match = Regex.Match(response, @"SM0(\d{3})");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int meter))
        {
            // Convert Yaesu meter reading to S-units (0-9, >9 = dB over S9)
            return meter / 25; // Rough conversion
        }
        return 0;
    }

    public virtual double ParseSWR(string response)
    {
        var match = Regex.Match(response, @"RM6(\d{3})");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int swr))
        {
            // Convert to SWR ratio (e.g., 1.5 for 1.5:1)
            return 1.0 + (swr * 2.0 / 255); // Rough conversion
        }
        return 1.0; // 1.0:1 default
    }

    public virtual int ParseAntenna(string response)
    {
        var match = Regex.Match(response, @"AN(\d)");
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
            // Yaesu IF command returns comprehensive status
            // Format: IF<11 freq><5 spare><+-><4 rit><0><0><0><mode><vfo><0><0><0><0><0>;
            if (response.StartsWith("IF") && response.Length >= 28)
            {
                var freq = response.Substring(2, 8);
                if (long.TryParse(freq, out long frequency))
                {
                    status.Frequency = frequency * 10; // Convert from 10Hz units
                }

                if (response.Length >= 16)
                {
                    var ritSign = response.Substring(13, 1);
                    var ritOffset = response.Substring(14, 4);
                    if (int.TryParse(ritOffset, out int rit))
                    {
                        status.RitOffset = ritSign == "-" ? -rit : rit;
                    }
                }

                if (response.Length >= 22)
                {
                    var modeChar = response.Substring(21, 1);
                    if (int.TryParse(modeChar, out int mode))
                    {
                        status.Mode = MapModeNumber(mode);
                    }
                }

                if (response.Length >= 23)
                {
                    var vfoChar = response.Substring(22, 1);
                    status.CurrentVfo = vfoChar == "0" ? "A" : "B";
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

    // Yaesu mode mapping
    protected virtual int MapModeToNumber(string mode)
    {
        return mode.ToUpper() switch
        {
            "LSB" => 1,
            "USB" => 2,
            "CW" => 3,
            "FM" => 4,
            "AM" => 5,
            "RTTY-LSB" => 6,
            "CW-R" => 7,
            "DATA-LSB" => 8,
            "RTTY-USB" => 9,
            "DATA-FM" => 10,
            "FM-N" => 11,
            "DATA-USB" => 12,
            "AM-N" => 13,
            "C4FM" => 14,
            _ => 2 // Default to USB
        };
    }

    protected virtual string MapModeNumber(int modeNumber)
    {
        return modeNumber switch
        {
            1 => "LSB",
            2 => "USB",
            3 => "CW",
            4 => "FM",
            5 => "AM",
            6 => "RTTY-LSB",
            7 => "CW-R",
            8 => "DATA-LSB",
            9 => "RTTY-USB",
            10 => "DATA-FM",
            11 => "FM-N",
            12 => "DATA-USB",
            13 => "AM-N",
            14 => "C4FM",
            _ => "USB"
        };
    }

    protected virtual int MapBandwidthToNumber(int bandwidth)
    {
        // Yaesu bandwidth mappings vary by model - this is a general mapping
        return bandwidth switch
        {
            >= 3000 => 1, // Wide
            >= 2400 => 2, // Medium-wide
            >= 1800 => 3, // Medium
            >= 1200 => 4, // Medium-narrow
            >= 500 => 5,  // Narrow
            _ => 3        // Default medium
        };
    }
}