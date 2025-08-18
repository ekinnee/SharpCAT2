using SharpCAT2.Core.Radio;
using System.Text.RegularExpressions;

namespace SharpCAT2.Common.Radio.Models.Yaesu;

/// <summary>
/// Yaesu FT-991A radio implementation
/// All-mode HF/VHF/UHF transceiver with waterfall display
/// </summary>
public class YaesuFT991A : BaseYaesuRadio
{
    public override string ModelName => "FT-991A";

    /// <summary>
    /// FT-991A supports comprehensive features across HF/VHF/UHF
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
        SupportedFeatures.SWRMeter |
        SupportedFeatures.MemoryChannels |
        SupportedFeatures.MemoryBanks |
        SupportedFeatures.CWKeyer |
        SupportedFeatures.CWSpeed |
        SupportedFeatures.NoiseReduction |
        SupportedFeatures.AGC |
        SupportedFeatures.SQLControl |
        SupportedFeatures.CTCSSTone |
        SupportedFeatures.RepeaterOffset |
        SupportedFeatures.TransmitStatus |
        SupportedFeatures.ReceiveStatus |
        SupportedFeatures.RadioID |
        SupportedFeatures.PowerOnOff |
        SupportedFeatures.DigitalModes |
        SupportedFeatures.ComputerControl;

    /// <summary>
    /// FT-991A specific multi-band operations
    /// Note: These methods implement radio-specific features not in base class
    /// </summary>
    public async Task<bool> SetBandAsync(string band)
    {
        // FT-991A covers HF/VHF/UHF so implement band switching
        var frequency = band.ToUpper() switch
        {
            "160M" => 1800000L,
            "80M" => 3500000L,
            "60M" => 5330000L,
            "40M" => 7000000L,
            "30M" => 10100000L,
            "20M" => 14000000L,
            "17M" => 18068000L,
            "15M" => 21000000L,
            "12M" => 24890000L,
            "10M" => 28000000L,
            "6M" => 50000000L,
            "2M" => 144000000L,
            "70CM" => 430000000L,
            _ => 0L
        };

        if (frequency > 0)
        {
            return await SetFrequencyAsync(frequency);
        }
        return false;
    }
}