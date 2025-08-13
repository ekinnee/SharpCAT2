using System.Text.RegularExpressions;

namespace SharpCAT2.Common.Radio.Protocols;

/// <summary>
/// Icom CI-V (Computer Interface V) protocol implementation
/// Used by Icom radios for computer control
/// CI-V uses binary BCD format rather than ASCII text
/// </summary>
public class IcomCIVProtocol : IRadioProtocol
{
    public virtual string ProtocolName => "Icom CI-V";

    // Default CI-V addresses (can be overridden by radio models)
    protected virtual byte RadioAddress => 0x94; // IC-7300 default
    protected virtual byte ControllerAddress => 0xE0; // PC default

    // CI-V command constants
    protected const byte PREAMBLE = 0xFE;
    protected const byte EOM = 0xFD; // End of Message

    // Core CI-V commands
    protected const byte CMD_FREQUENCY_SET = 0x05;
    protected const byte CMD_FREQUENCY_READ = 0x03;
    protected const byte CMD_MODE_SET = 0x06;
    protected const byte CMD_MODE_READ = 0x04;
    protected const byte CMD_VFO_SELECT = 0x07;
    protected const byte CMD_SPLIT = 0x0F;
    protected const byte CMD_RIT = 0x21;
    protected const byte CMD_XIT = 0x22;
    protected const byte CMD_POWER = 0x14;
    protected const byte CMD_SMETER = 0x15;
    protected const byte CMD_SWR = 0x15;
    protected const byte CMD_ANTENNA = 0x12;

    // Core command creation methods
    public virtual RadioCommand GetFrequencyCommand() => new("CIVFREQGET", "Get frequency", true, 2000);
    public virtual RadioCommand SetFrequencyCommand(long frequency) => new($"CIVFREQSET:{frequency}", $"Set frequency to {frequency} Hz", false, 2000);
    public virtual RadioCommand GetModeCommand() => new("CIVMODEGET", "Get mode", true, 2000);
    public virtual RadioCommand SetModeCommand(string mode) => new($"CIVMODESET:{mode}", $"Set mode to {mode}", false, 2000);
    public virtual RadioCommand GetStatusCommand() => new("CIVSTATUS", "Get status", true, 2000);
    public virtual RadioCommand GetVfoCommand() => new("CIVVFOGET", "Get VFO", true, 2000);
    public virtual RadioCommand SetVfoCommand(string vfo) => new($"CIVVFOSET:{vfo}", $"Set VFO to {vfo}", false, 2000);
    public virtual RadioCommand SwapVfoCommand() => new("CIVVFOSWAP", "Swap VFO", false, 2000);
    public virtual RadioCommand SetSplitCommand(bool enabled) => new($"CIVSPLIT:{enabled}", $"{(enabled ? "Enable" : "Disable")} split", false, 2000);
    public virtual RadioCommand GetSplitCommand() => new("CIVSPLITGET", "Get split status", true, 2000);
    public virtual RadioCommand SetRitCommand(int offsetHz) => new($"CIVRIT:{offsetHz}", $"Set RIT offset to {offsetHz} Hz", false, 2000);
    public virtual RadioCommand GetRitCommand() => new("CIVRITGET", "Get RIT offset", true, 2000);
    public virtual RadioCommand SetXitCommand(int offsetHz) => new($"CIVXIT:{offsetHz}", $"Set XIT offset to {offsetHz} Hz", false, 2000);
    public virtual RadioCommand GetXitCommand() => new("CIVXITGET", "Get XIT offset", true, 2000);
    public virtual RadioCommand SetPowerOutputCommand(int powerPercent) => new($"CIVPOWER:{powerPercent}", $"Set power output to {powerPercent}%", false, 2000);
    public virtual RadioCommand GetPowerOutputCommand() => new("CIVPOWERGET", "Get power output", true, 2000);
    public virtual RadioCommand GetSMeterCommand() => new("CIVSMETER", "Get S-meter reading", true, 2000);
    public virtual RadioCommand GetSWRCommand() => new("CIVSWR", "Get SWR reading", true, 2000);
    public virtual RadioCommand SetAntennaCommand(int antenna) => new($"CIVANT:{antenna}", $"Set antenna to {antenna}", false, 2000);
    public virtual RadioCommand GetAntennaCommand() => new("CIVANTGET", "Get antenna", true, 2000);

    // Response parsing methods
    public virtual bool IsCompleteResponse(string response)
    {
        // For CI-V, we'll simulate responses or convert binary to text representation
        return !string.IsNullOrEmpty(response) && (response.StartsWith("CIV") || response.Length > 0);
    }

    public virtual long ParseFrequency(string response)
    {
        // CI-V frequency is in BCD format, but we'll handle simplified text format for now
        if (response.StartsWith("CIVFREQ:"))
        {
            var freqStr = response.Substring(8);
            if (long.TryParse(freqStr, out long freq))
            {
                return freq;
            }
        }
        return 0;
    }

    public virtual string ParseMode(string response)
    {
        if (response.StartsWith("CIVMODE:"))
        {
            var modeStr = response.Substring(8);
            return MapCIVModeToString(modeStr);
        }
        return "USB";
    }

    public virtual string ParseVfo(string response)
    {
        if (response.StartsWith("CIVVFO:"))
        {
            var vfoStr = response.Substring(7);
            return vfoStr == "0" ? "A" : "B";
        }
        return "A";
    }

    public virtual bool ParseSplit(string response)
    {
        return response.Contains("CIVSPLIT:1") || response.Contains("SPLIT:ON");
    }

    public virtual int ParseRit(string response)
    {
        if (response.StartsWith("CIVRIT:"))
        {
            var ritStr = response.Substring(7);
            if (int.TryParse(ritStr, out int rit))
            {
                return rit;
            }
        }
        return 0;
    }

    public virtual int ParseXit(string response)
    {
        if (response.StartsWith("CIVXIT:"))
        {
            var xitStr = response.Substring(7);
            if (int.TryParse(xitStr, out int xit))
            {
                return xit;
            }
        }
        return 0;
    }

    public virtual int ParsePowerOutput(string response)
    {
        if (response.StartsWith("CIVPOWER:"))
        {
            var powerStr = response.Substring(9);
            if (int.TryParse(powerStr, out int power))
            {
                return power;
            }
        }
        return 0;
    }

    public virtual int ParseSMeter(string response)
    {
        if (response.StartsWith("CIVSMETER:"))
        {
            var meterStr = response.Substring(10);
            if (int.TryParse(meterStr, out int meter))
            {
                // Convert CI-V meter reading to S-units
                return Math.Max(0, Math.Min(9 + (meter - 120) / 6, 20)); // Rough conversion
            }
        }
        return 0;
    }

    public virtual double ParseSWR(string response)
    {
        if (response.StartsWith("CIVSWR:"))
        {
            var swrStr = response.Substring(7);
            if (double.TryParse(swrStr, out double swr))
            {
                return swr;
            }
        }
        return 1.0;
    }

    public virtual int ParseAntenna(string response)
    {
        if (response.StartsWith("CIVANT:"))
        {
            var antStr = response.Substring(7);
            if (int.TryParse(antStr, out int antenna))
            {
                return antenna;
            }
        }
        return 1;
    }

    public virtual void ParseTransceiverInfo(string response, RadioStatus status)
    {
        if (string.IsNullOrEmpty(response))
            return;

        try
        {
            // CI-V status parsing - simplified text format for now
            if (response.StartsWith("CIVSTATUS:"))
            {
                var parts = response.Substring(10).Split('|');
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
                    else if (part.StartsWith("VFO:"))
                    {
                        status.CurrentVfo = part.Substring(4);
                    }
                    else if (part.StartsWith("SPLIT:"))
                    {
                        status.SplitEnabled = part.Substring(6) == "1";
                    }
                    else if (part.StartsWith("RIT:") && int.TryParse(part.Substring(4), out int rit))
                    {
                        status.RitOffset = rit;
                    }
                }

                status.IsPoweredOn = true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing Icom CI-V transceiver info: {ex.Message}");
        }
    }

    // CI-V specific utility methods
    protected virtual string MapCIVModeToString(string civMode)
    {
        return civMode switch
        {
            "0" => "LSB",
            "1" => "USB",
            "2" => "AM",
            "3" => "CW",
            "4" => "RTTY",
            "5" => "FM",
            "6" => "CW-R",
            "7" => "RTTY-R",
            "8" => "PSK",
            "17" => "PSK-R",
            _ => "USB"
        };
    }

    protected virtual string MapStringToCIVMode(string mode)
    {
        return mode.ToUpper() switch
        {
            "LSB" => "0",
            "USB" => "1",
            "AM" => "2",
            "CW" => "3",
            "RTTY" => "4",
            "FM" => "5",
            "CW-R" => "6",
            "RTTY-R" => "7",
            "PSK" => "8",
            "PSK-R" => "17",
            _ => "1" // Default to USB
        };
    }

    // CI-V binary frame construction helpers
    protected virtual byte[] BuildCIVFrame(byte command, byte[]? data = null)
    {
        var frame = new List<byte>
        {
            PREAMBLE, PREAMBLE,           // Preamble
            RadioAddress,                  // Radio address
            ControllerAddress,             // Controller address
            command                        // Command
        };

        if (data != null)
        {
            frame.AddRange(data);
        }

        frame.Add(EOM); // End of message
        return frame.ToArray();
    }

    protected virtual byte[] FrequencyToBCD(long frequency)
    {
        // Convert frequency to 5-byte BCD format (10 Hz resolution)
        var freqBCD = new byte[5];
        var freq = frequency / 10; // Convert to 10Hz units

        for (int i = 0; i < 5; i++)
        {
            freqBCD[i] = (byte)((freq % 10) | ((freq / 10 % 10) << 4));
            freq /= 100;
        }

        return freqBCD;
    }

    protected virtual long BCDToFrequency(byte[] bcdData)
    {
        if (bcdData.Length < 5) return 0;

        long frequency = 0;
        long multiplier = 10; // 10Hz resolution

        for (int i = 0; i < 5; i++)
        {
            var lowNibble = bcdData[i] & 0x0F;
            var highNibble = (bcdData[i] & 0xF0) >> 4;
            
            frequency += lowNibble * multiplier;
            multiplier *= 10;
            frequency += highNibble * multiplier;
            multiplier *= 10;
        }

        return frequency;
    }

    // Note: In a real implementation, these would send/receive actual CI-V binary frames
    // For now, we're using text-based simulation for compatibility with the existing framework
}