using SharpCAT2.Core.Radio;
using System.Text.RegularExpressions;

namespace SharpCAT2.ServerLibrary.Radio.Models.Yaesu;

/// <summary>
/// Yaesu FT-710 radio implementation
/// Modern HF/6m transceiver with color display and DSP
/// </summary>
public class YaesuFT710 : BaseYaesuRadio
{
    public override string ModelName => "FT-710";
    public override string Manufacturer => "Yaesu";

    /// <summary>
    /// FT-710 supports modern HF features with advanced DSP
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
        SupportedFeatures.AntennaSelection |
        SupportedFeatures.MemoryChannels |
        SupportedFeatures.CWKeyer |
        SupportedFeatures.CWSpeed |
        SupportedFeatures.NoiseReduction |
        SupportedFeatures.AGC |
        SupportedFeatures.TransmitStatus |
        SupportedFeatures.ReceiveStatus |
        SupportedFeatures.RadioID |
        SupportedFeatures.PowerOnOff |
        SupportedFeatures.DigitalModes |
        SupportedFeatures.ComputerControl;

}

/// <summary>
/// Yaesu FT-dx101D radio implementation
/// High-end HF/6m contest transceiver with advanced DSP
/// </summary>
public class YaesuFTDX101D : BaseYaesuRadio
{
    public override string ModelName => "FT-DX101D";
    public override string Manufacturer => "Yaesu";

    /// <summary>
    /// FT-DX101D supports advanced contest features
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
        SupportedFeatures.MemoryBanks |
        SupportedFeatures.CWKeyer |
        SupportedFeatures.CWSpeed |
        SupportedFeatures.CWMessage |
        SupportedFeatures.NoiseReduction |
        SupportedFeatures.NoiseFilter |
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

    /// <summary>
    /// FT-DX101D specific enhancements for contest operation  
    /// Note: These methods implement radio-specific features not in base class
    /// </summary>
    public async Task<bool> SetDualReceiveAsync(bool enabled)
    {
        // FT-DX101D has dual receive capability
        if (!SupportedFeatures.HasFeature(SupportedFeatures.DualVFO))
            return false;

        var command = new RadioCommand($"DV{(enabled ? "1" : "0")};", $"Set dual receive {enabled}", false, 2000);
        var response = await SendCommandAsync(command);
        return !string.IsNullOrEmpty(response);
    }
}

/// <summary>
/// Yaesu FT-891 radio implementation
/// Compact HF/6m mobile transceiver with built-in antenna tuner
/// </summary>
public class YaesuFT891 : BaseYaesuRadio
{
    public override string ModelName => "FT-891";
    public override string Manufacturer => "Yaesu";

    /// <summary>
    /// FT-891 supports basic to intermediate HF features
    /// </summary>
    public override SupportedFeatures SupportedFeatures => 
        SupportedFeatures.FrequencyControl | 
        SupportedFeatures.ModeControl |
        SupportedFeatures.DualVFO | 
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
        SupportedFeatures.ComputerControl;
}

/// <summary>
/// Yaesu FT-65 radio implementation
/// Affordable VHF/UHF handheld transceiver
/// </summary>
public class YaesuFT65 : BaseYaesuRadio
{
    public override string ModelName => "FT-65";
    public override string Manufacturer => "Yaesu";

    /// <summary>
    /// FT-65 supports VHF/UHF handheld features
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
        SupportedFeatures.ComputerControl;

    /// <summary>
    /// FT-65 specific VHF/UHF operations
    /// Note: Some methods implement radio-specific features not in base class
    /// </summary>
    public async Task<bool> SetSquelchAsync(int level)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.SQLControl))
            return false;

        var command = new RadioCommand($"SQ0{level:D3};", $"Set squelch to level {level}", false, 2000);
        var response = await SendCommandAsync(command);
        return !string.IsNullOrEmpty(response);
    }

    public async Task<bool> SetCTCSSAsync(double frequency)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.CTCSSTone))
            return false;

        // Convert CTCSS frequency to Yaesu tone number
        int toneNum = CTCSSToYaesuTone(frequency);
        var command = new RadioCommand($"CT0{toneNum:D2};", $"Set CTCSS tone to {frequency} Hz", false, 2000);
        var response = await SendCommandAsync(command);
        return !string.IsNullOrEmpty(response);
    }

    private int CTCSSToYaesuTone(double frequency)
    {
        // Common CTCSS frequencies mapped to Yaesu tone numbers
        return frequency switch
        {
            67.0 => 1,
            71.9 => 2,
            74.4 => 3,
            77.0 => 4,
            79.7 => 5,
            82.5 => 6,
            85.4 => 7,
            88.5 => 8,
            91.5 => 9,
            94.8 => 10,
            97.4 => 11,
            100.0 => 12,
            103.5 => 13,
            107.2 => 14,
            110.9 => 15,
            114.8 => 16,
            118.8 => 17,
            123.0 => 18,
            127.3 => 19,
            131.8 => 20,
            136.5 => 21,
            141.3 => 22,
            146.2 => 23,
            151.4 => 24,
            156.7 => 25,
            162.2 => 26,
            167.9 => 27,
            173.8 => 28,
            179.9 => 29,
            186.2 => 30,
            192.8 => 31,
            203.5 => 32,
            210.7 => 33,
            218.1 => 34,
            225.7 => 35,
            233.6 => 36,
            241.8 => 37,
            250.3 => 38,
            _ => 0 // Off
        };
    }
}