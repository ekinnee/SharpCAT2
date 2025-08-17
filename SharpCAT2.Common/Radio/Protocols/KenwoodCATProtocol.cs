using System.Text.RegularExpressions;

namespace SharpCAT2.Common.Radio.Protocols;

/// <summary>
/// Kenwood CAT (Computer Aided Transceiver) protocol implementation
/// Used by Kenwood radios and Elecraft radios (with extensions)
/// </summary>
public class KenwoodCATProtocol : IRadioProtocol
{
    public virtual string ProtocolName => "Kenwood CAT";

    // Command creation methods
    public virtual RadioCommand GetFrequencyCommand() => new("FA;", "Get VFO A frequency", true, 2000);
    public virtual RadioCommand SetFrequencyCommand(long frequency) => new($"FA{frequency:D11};", $"Set VFO A frequency to {frequency} Hz", false, 2000);
    public virtual RadioCommand GetModeCommand() => new("MD;", "Get mode", true, 2000);
    public virtual RadioCommand SetModeCommand(string mode) => new($"MD{MapModeToNumber(mode)};", $"Set mode to {mode}", false, 2000);
    public virtual RadioCommand GetStatusCommand() => new("IF;", "Get transceiver information", true, 2000);
    public virtual RadioCommand GetVfoCommand() => new("FN;", "Get VFO", true, 2000);
    public virtual RadioCommand SetVfoCommand(string vfo) => new($"FN{(vfo.ToUpper() == "A" ? "0" : "1")};", $"Set VFO to {vfo}", false, 2000);
    public virtual RadioCommand SwapVfoCommand() => new("SV;", "Swap VFO A and B", false, 2000);
    public virtual RadioCommand SetSplitCommand(bool enabled) => new($"SP{(enabled ? "1" : "0")};", $"{(enabled ? "Enable" : "Disable")} split", false, 2000);
    public virtual RadioCommand GetSplitCommand() => new("SP;", "Get split status", true, 2000);
    public virtual RadioCommand SetRitCommand(int offsetHz) => new($"RD{offsetHz:+0000;-0000;+0000};", $"Set RIT offset to {offsetHz} Hz", false, 2000);
    public virtual RadioCommand GetRitCommand() => new("RD;", "Get RIT offset", true, 2000);
    public virtual RadioCommand SetXitCommand(int offsetHz) => new($"XT{offsetHz:+0000;-0000;+0000};", $"Set XIT offset to {offsetHz} Hz", false, 2000);
    public virtual RadioCommand GetXitCommand() => new("XT;", "Get XIT offset", true, 2000);
    public virtual RadioCommand SetPowerOutputCommand(int powerPercent) => new($"PC{powerPercent:D3};", $"Set power output to {powerPercent}%", false, 2000);
    public virtual RadioCommand GetPowerOutputCommand() => new("PC;", "Get power output", true, 2000);
    public virtual RadioCommand GetSMeterCommand() => new("SM0;", "Get S-meter reading", true, 2000);
    public virtual RadioCommand GetSWRCommand() => new("RM3;", "Get SWR reading", true, 2000);
    public virtual RadioCommand SetAntennaCommand(int antenna) => new($"AN{antenna};", $"Set antenna to {antenna}", false, 2000);
    public virtual RadioCommand GetAntennaCommand() => new("AN;", "Get antenna", true, 2000);

    // Additional Kenwood commands
    public virtual RadioCommand GetFrequencyBCommand() => new("FB;", "Get VFO B frequency", true, 2000);
    public virtual RadioCommand SetFrequencyBCommand(long frequency) => new($"FB{frequency:D11};", $"Set VFO B frequency to {frequency} Hz", false, 2000);
    public virtual RadioCommand GetIfBandwidthCommand() => new("FW;", "Get IF bandwidth", true, 2000);
    public virtual RadioCommand SetIfBandwidthCommand(int bandwidth) => new($"FW{bandwidth:D4};", $"Set IF bandwidth to {bandwidth} Hz", false, 2000);
    public virtual RadioCommand GetMemoryChannelCommand() => new("MC;", "Get memory channel", true, 2000);
    public virtual RadioCommand SetMemoryChannelCommand(int channel) => new($"MC{channel:D3};", $"Set memory channel to {channel}", false, 2000);
    public virtual RadioCommand GetCwSpeedCommand() => new("KS;", "Get CW speed", true, 2000);
    public virtual RadioCommand SetCwSpeedCommand(int wpm) => new($"KS{wpm:D3};", $"Set CW speed to {wpm} WPM", false, 2000);
    public virtual RadioCommand SendCwMessageCommand(string message) => new($"KY {message};", $"Send CW message: {message}", false, 5000);
    public virtual RadioCommand GetNoiseReductionCommand() => new("NR;", "Get noise reduction", true, 2000);
    public virtual RadioCommand SetNoiseReductionCommand(int level) => new($"NR{level};", $"Set noise reduction to level {level}", false, 2000);
    public virtual RadioCommand PowerOnCommand() => new("PS1;", "Power on", false, 5000);
    public virtual RadioCommand PowerOffCommand() => new("PS0;", "Power off", false, 5000);
    public virtual RadioCommand GetPowerStatusCommand() => new("PS;", "Get power status", true, 2000);

    // Parsing methods
    public virtual long ParseFrequency(string response)
    {
        // Kenwood format: FA00014074000; or FB00014074000;
        var match = Regex.Match(response, @"F[AB](\d{11})");
        if (match.Success && long.TryParse(match.Groups[1].Value, out long freq))
        {
            return freq;
        }
        return 0;
    }

    public virtual string ParseMode(string response)
    {
        // Kenwood format: MD2; (2=USB)
        var match = Regex.Match(response, @"MD(\d+)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int modeNum))
        {
            return MapModeNumber(modeNum);
        }
        return "USB";
    }

    public virtual string ParseVfo(string response)
    {
        // Kenwood format: FN0; (0=VFO A, 1=VFO B)
        var match = Regex.Match(response, @"FN(\d)");
        if (match.Success)
        {
            return match.Groups[1].Value == "0" ? "A" : "B";
        }
        return "A";
    }

    public virtual bool ParseSplit(string response)
    {
        // Kenwood format: SP1; (1=enabled, 0=disabled)
        var match = Regex.Match(response, @"SP(\d)");
        if (match.Success)
        {
            return match.Groups[1].Value == "1";
        }
        return false;
    }

    public virtual int ParseRit(string response)
    {
        // Kenwood format: RD+0120; or RD-0050;
        var match = Regex.Match(response, @"RD([+-]\d{4})");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int offset))
        {
            return offset;
        }
        return 0;
    }

    public virtual int ParseXit(string response)
    {
        // Kenwood format: XT+0120; or XT-0050;
        var match = Regex.Match(response, @"XT([+-]\d{4})");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int offset))
        {
            return offset;
        }
        return 0;
    }

    public virtual int ParsePowerOutput(string response)
    {
        // Kenwood format: PC050; (050 = 50%)
        var match = Regex.Match(response, @"PC(\d{3})");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int power))
        {
            return power;
        }
        return 0;
    }

    public virtual int ParseSMeter(string response)
    {
        // Kenwood format: SM0012; (0012 = S-meter reading)
        var match = Regex.Match(response, @"SM0(\d{3})");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int reading))
        {
            // Convert to S-units (0-9 for S-units, >9 for dB over S9)
            return Math.Min(reading / 30, 15); // Approximate conversion
        }
        return 0;
    }

    public virtual double ParseSWR(string response)
    {
        // Kenwood format: RM3010; (010 = SWR reading)
        var match = Regex.Match(response, @"RM3(\d{3})");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int reading))
        {
            // Convert to SWR ratio (1.0 = perfect match)
            return 1.0 + (reading / 100.0);
        }
        return 1.0;
    }

    public virtual int ParseAntenna(string response)
    {
        // Kenwood format: AN1; (1 = antenna 1)
        var match = Regex.Match(response, @"AN(\d)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int antenna))
        {
            return antenna;
        }
        return 1;
    }

    public virtual bool IsCompleteResponse(string response)
    {
        return response.EndsWith(";");
    }

    /// <summary>
    /// Additional parsing for IF command (comprehensive status)
    /// </summary>
    public virtual void ParseTransceiverInfo(string response, RadioStatus status)
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

                // Extract RIT offset (positions 18-22)
                if (response.Length > 22 && int.TryParse(response.Substring(18, 5), out int ritOffset))
                {
                    status.RitOffset = ritOffset;
                }

                // Extract RIT flag (position 23)
                if (response.Length > 23)
                {
                    status.RitEnabled = response[23] == '1';
                }

                // Extract XIT flag (position 24)
                if (response.Length > 24)
                {
                    status.XitEnabled = response[24] == '1';
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

                // Extract split status (position 32)
                if (response.Length > 32)
                {
                    status.SplitEnabled = response[32] == '1';
                }
            }
            catch (Exception)
            {
                // Parsing errors result in incomplete status
                // Service layer can detect issues through missing status fields
            }
        }
    }

    #region Protected Methods

    /// <summary>
    /// Helper methods
    /// </summary>
    protected virtual string MapModeNumber(int modeNumber)
    {
        return modeNumber switch
        {
            1 => "LSB",
            2 => "USB",
            3 => "CW",
            4 => "FM",
            5 => "AM",
            6 => "FSK",
            7 => "CW-R",
            8 => "FSK-R",
            9 => "PSK",
            _ => "USB"
        };
    }

    protected virtual int MapModeToNumber(string mode)
    {
        return mode.ToUpper() switch
        {
            "LSB" => 1,
            "USB" => 2,
            "CW" => 3,
            "FM" => 4,
            "AM" => 5,
            "FSK" or "RTTY" => 6,
            "CW-R" or "CWR" => 7,
            "FSK-R" or "RTTYR" => 8,
            "PSK" or "PSK31" => 9,
            _ => 2 // Default to USB
        };
    }

    #endregion
}