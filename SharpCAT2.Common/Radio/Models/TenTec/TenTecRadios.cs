using System.Text.RegularExpressions;

namespace SharpCAT2.Common.Radio.Models.TenTec;

/// <summary>
/// Base class for Ten-Tec radios
/// Ten-Tec uses a simplified ASCII protocol
/// </summary>
public abstract class BaseTenTecRadio : BaseRadio
{
    public override string Manufacturer => "Ten-Tec";

    protected override bool IsCompleteResponse(string response)
    {
        return response.EndsWith("\r") || response.EndsWith("\n") || response.Length > 0;
    }

    protected override long ParseFrequency(string response)
    {
        // Ten-Tec format: usually in Hz as simple number
        var match = Regex.Match(response, @"(\d{7,11})");
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
            _ => "USB"
        };
    }

    protected override void ParseTransceiverInfo(string response, RadioStatus status)
    {
        if (!string.IsNullOrEmpty(response))
        {
            try
            {
                // Basic Ten-Tec status parsing
                var parts = response.Split(' ');
                if (parts.Length >= 2)
                {
                    if (long.TryParse(parts[0], out long freq))
                    {
                        status.Frequency = freq;
                    }
                    status.Mode = parts[1];
                }
                status.IsPoweredOn = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing Ten-Tec transceiver info: {ex.Message}");
            }
        }
    }

    // Basic operations
    public override async Task<bool> SetPowerOutputAsync(int powerPercent)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.PowerOutput))
            return false;

        var command = new RadioCommand($"*P{powerPercent}\r", $"Set power to {powerPercent}%", false, 2000);
        var response = await SendCommandAsync(command);
        return !string.IsNullOrEmpty(response);
    }

    public override async Task<int> GetPowerOutputAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.PowerOutput))
            return 0;

        var command = new RadioCommand("*P\r", "Get power output", true, 2000);
        var response = await SendCommandAsync(command);
        if (!string.IsNullOrEmpty(response) && int.TryParse(response.Trim(), out int power))
        {
            return power;
        }
        return 0;
    }
}

/// <summary>
/// Ten-Tec OMNI VII radio implementation
/// High-performance HF/6m transceiver with SDR architecture
/// </summary>
public class TenTecOMNIVII : BaseTenTecRadio
{
    public override string ModelName => "OMNI VII";

    /// <summary>
    /// OMNI VII supports advanced HF features with SDR technology
    /// </summary>
    public override SupportedFeatures SupportedFeatures => 
        SupportedFeatures.FrequencyControl | 
        SupportedFeatures.ModeControl |
        SupportedFeatures.DualVFO | 
        SupportedFeatures.VFOSwap |
        SupportedFeatures.SplitOperation |
        SupportedFeatures.RIT | 
        SupportedFeatures.XIT |
        SupportedFeatures.IFBandwidth |
        SupportedFeatures.PowerOutput |
        SupportedFeatures.SMeter |
        SupportedFeatures.SWRMeter |
        SupportedFeatures.MemoryChannels |
        SupportedFeatures.CWKeyer |
        SupportedFeatures.CWSpeed |
        SupportedFeatures.NoiseReduction |
        SupportedFeatures.AGC |
        SupportedFeatures.TransmitStatus |
        SupportedFeatures.ReceiveStatus |
        SupportedFeatures.RadioID |
        SupportedFeatures.PowerOnOff |
        SupportedFeatures.FilterSelection |
        SupportedFeatures.ComputerControl;
}

/// <summary>
/// Ten-Tec Eagle radio implementation
/// High-end HF transceiver with premium build quality
/// </summary>
public class TenTecEagle : BaseTenTecRadio
{
    public override string ModelName => "Eagle";

    /// <summary>
    /// Eagle supports premium HF features
    /// </summary>
    public override SupportedFeatures SupportedFeatures => 
        SupportedFeatures.FrequencyControl | 
        SupportedFeatures.ModeControl |
        SupportedFeatures.DualVFO | 
        SupportedFeatures.VFOSwap |
        SupportedFeatures.SplitOperation |
        SupportedFeatures.RIT | 
        SupportedFeatures.XIT |
        SupportedFeatures.IFBandwidth |
        SupportedFeatures.PowerOutput |
        SupportedFeatures.SMeter |
        SupportedFeatures.SWRMeter |
        SupportedFeatures.ALCMeter |
        SupportedFeatures.AntennaSelection |
        SupportedFeatures.MemoryChannels |
        SupportedFeatures.CWKeyer |
        SupportedFeatures.CWSpeed |
        SupportedFeatures.CWMessage |
        SupportedFeatures.NoiseReduction |
        SupportedFeatures.AGC |
        SupportedFeatures.AudioGain |
        SupportedFeatures.TransmitStatus |
        SupportedFeatures.ReceiveStatus |
        SupportedFeatures.RadioID |
        SupportedFeatures.PowerOnOff |
        SupportedFeatures.FilterSelection |
        SupportedFeatures.Preamp |
        SupportedFeatures.Attenuator |
        SupportedFeatures.ComputerControl;
}

/// <summary>
/// Ten-Tec Argonaut V radio implementation
/// QRP HF transceiver with built-in antenna tuner
/// </summary>
public class TenTecArgonautV : BaseTenTecRadio
{
    public override string ModelName => "Argonaut V";

    /// <summary>
    /// Argonaut V supports QRP HF operation (5W output)
    /// </summary>
    public override SupportedFeatures SupportedFeatures => 
        SupportedFeatures.FrequencyControl | 
        SupportedFeatures.ModeControl |
        SupportedFeatures.DualVFO | 
        SupportedFeatures.RIT | 
        SupportedFeatures.PowerOutput |
        SupportedFeatures.SMeter |
        SupportedFeatures.MemoryChannels |
        SupportedFeatures.CWKeyer |
        SupportedFeatures.AGC |
        SupportedFeatures.TransmitStatus |
        SupportedFeatures.ReceiveStatus |
        SupportedFeatures.RadioID |
        SupportedFeatures.PowerOnOff |
        SupportedFeatures.ComputerControl;
}

/// <summary>
/// Ten-Tec Jupiter radio implementation
/// Popular HF transceiver with DSP technology
/// </summary>
public class TenTecJupiter : BaseTenTecRadio
{
    public override string ModelName => "Jupiter";

    /// <summary>
    /// Jupiter supports standard HF features with DSP
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
        SupportedFeatures.AGC |
        SupportedFeatures.TransmitStatus |
        SupportedFeatures.ReceiveStatus |
        SupportedFeatures.RadioID |
        SupportedFeatures.PowerOnOff |
        SupportedFeatures.ComputerControl;
}