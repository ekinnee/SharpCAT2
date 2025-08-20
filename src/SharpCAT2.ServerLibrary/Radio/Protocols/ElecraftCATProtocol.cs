using SharpCAT2.Core.Radio;
using System.Text.RegularExpressions;

namespace SharpCAT2.ServerLibrary.Radio.Protocols;

/// <summary>
/// Elecraft CAT protocol implementation
/// Based on Kenwood CAT with Elecraft-specific extensions
/// </summary>
public class ElecraftCATProtocol : KenwoodCATProtocol
{
    public override string ProtocolName => "Elecraft CAT";

    // Additional Elecraft-specific commands
    public virtual RadioCommand GetElecraftInfoCommand() => new("K3;", "Get Elecraft radio info", true, 2000);
    public virtual RadioCommand GetElecraftVersionCommand() => new("K31;", "Get firmware version", true, 2000);
    public virtual RadioCommand GetElecraftConfigCommand() => new("KY;", "Get configuration info", true, 2000);
    
    // Enhanced IF bandwidth commands for Elecraft
    public virtual RadioCommand SetElecraftIfBandwidthCommand(int bandwidth) => new($"FW{bandwidth:D5};", $"Set IF bandwidth to {bandwidth} Hz", false, 2000);
    
    // Elecraft-specific DSP commands
    public virtual RadioCommand SetElecraftDSPCommand(int setting) => new($"NB{setting};", $"Set DSP noise blanker to {setting}", false, 2000);
    public virtual RadioCommand GetElecraftDSPCommand() => new("NB;", "Get DSP noise blanker setting", true, 2000);
    
    // Elecraft antenna tuner commands
    public virtual RadioCommand TuneElecraftAntennaCommand() => new("SWT13;", "Start antenna tuning", false, 5000);
    public virtual RadioCommand GetElecraftTunerStatusCommand() => new("AN;", "Get tuner status", true, 2000);
    
    // Elecraft filter selection (more granular than basic Kenwood)
    public virtual RadioCommand SetElecraftFilterCommand(int filter) => new($"FL{filter:D3};", $"Set filter to {filter}", false, 2000);
    public virtual RadioCommand GetElecraftFilterCommand() => new("FL;", "Get current filter", true, 2000);
    
    // Elecraft AGC settings
    public virtual RadioCommand SetElecraftAGCCommand(int speed) => new($"GT{speed:D3};", $"Set AGC speed to {speed}", false, 2000);
    public virtual RadioCommand GetElecraftAGCCommand() => new("GT;", "Get AGC speed", true, 2000);
    
    // Enhanced mode mapping for Elecraft radios
    protected override string MapModeNumber(int modeNumber)
    {
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
            9 => "AM-S",       // AM Synchronous
            10 => "PSK-D",    // PSK-D mode
            11 => "AFSK-A",   // AFSK-A mode
            _ => "USB"
        };
    }
    
    protected override int MapModeToNumber(string mode)
    {
        return mode.ToUpper() switch
        {
            "LSB" => 1,
            "USB" => 2,
            "CW" => 3,
            "FM" => 4,
            "AM" => 5,
            "DATA" or "RTTY" or "PSK31" => 6,
            "CW-R" or "CWR" or "CW-REV" => 7,
            "DATA-R" or "DATA-REV" => 8,
            "AM-S" or "AMS" => 9,
            "PSK-D" => 10,
            "AFSK-A" => 11,
            _ => 2 // Default to USB
        };
    }

    // Enhanced parsing for Elecraft-specific responses
    public virtual int ParseElecraftFilter(string response)
    {
        // Elecraft FL response: FL003; (filter number)
        var match = Regex.Match(response, @"FL(\d{3})");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int filter))
        {
            return filter;
        }
        return 0;
    }

    public virtual int ParseElecraftAGC(string response)
    {
        // Elecraft GT response: GT003; (AGC speed)
        var match = Regex.Match(response, @"GT(\d{3})");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int agc))
        {
            return agc;
        }
        return 0;
    }

    public virtual int ParseElecraftDSP(string response)
    {
        // Elecraft NB response: NB3; (DSP setting)
        var match = Regex.Match(response, @"NB(\d)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int dsp))
        {
            return dsp;
        }
        return 0;
    }

    public virtual string ParseElecraftInfo(string response)
    {
        // Elecraft K3 response format varies, typically contains model info
        if (response.Contains("K3"))
            return "K3";
        if (response.Contains("K4"))
            return "K4";
        if (response.Contains("KX3"))
            return "KX3";
        if (response.Contains("K2"))
            return "K2";
        if (response.Contains("K1"))
            return "K1";
        return "Unknown";
    }

    // Enhanced transceiver info parsing for Elecraft radios
    public override void ParseTransceiverInfo(string response, RadioStatus status)
    {
        // Call base implementation first
        base.ParseTransceiverInfo(response, status);
        
        // Add Elecraft-specific parsing if needed
        if (response.StartsWith("IF") && response.Length >= 38)
        {
            try
            {
                // Elecraft radios may have additional fields in IF response
                // Extract additional information if available
                
                // Some Elecraft radios include filter information
                if (response.Length > 40)
                {
                    // Custom parsing for extended IF response
                    // This would depend on specific radio model
                }
            }
            catch (Exception)
            {
                // Parsing errors result in incomplete status
                // Service layer can detect issues through missing status fields
            }
        }
    }
}