using SharpCAT2.Core.Radio;
using System.Text.RegularExpressions;

namespace SharpCAT2.Common.Radio.Models.Icom;

/// <summary>
/// Icom IC-7300 radio implementation
/// Direct-sampling SDR HF transceiver with waterfall display
/// </summary>
public class IcomIC7300 : BaseIcomRadio
{
    public override string ModelName => "IC-7300";

    /// <summary>
    /// IC-7300 supports modern HF features including SDR technology
    /// </summary>
    public override SupportedFeatures SupportedFeatures => 
        SupportedFeatures.FrequencyControl | 
        SupportedFeatures.ModeControl |
        SupportedFeatures.DualVFO | 
        SupportedFeatures.VFOSwap |
        SupportedFeatures.VFOEqual |
        SupportedFeatures.SplitOperation |
        SupportedFeatures.IFBandwidth |
        SupportedFeatures.PowerOutput |
        SupportedFeatures.SMeter |
        SupportedFeatures.SWRMeter |
        SupportedFeatures.AntennaSelection |
        SupportedFeatures.MemoryChannels |
        SupportedFeatures.MemoryBanks |
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
        SupportedFeatures.Waterfall |
        SupportedFeatures.Panadapter |
        SupportedFeatures.ComputerControl;

    /// <summary>
    /// IC-7300 specific SDR features
    /// Note: These methods implement radio-specific features
    /// </summary>
    public async Task<bool> SetWaterfallAsync(bool enabled)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.Waterfall))
            return false;

        var command = new RadioCommand($"CIVWFALL:{(enabled ? "1" : "0")}", $"{(enabled ? "Enable" : "Disable")} waterfall", false, 2000);
        var response = await SendCommandAsync(command);
        return !string.IsNullOrEmpty(response);
    }

    public async Task<bool> SetPanadapterAsync(bool enabled)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.Panadapter))
            return false;

        var command = new RadioCommand($"CIVPAN:{(enabled ? "1" : "0")}", $"{(enabled ? "Enable" : "Disable")} panadapter", false, 2000);
        var response = await SendCommandAsync(command);
        return !string.IsNullOrEmpty(response);
    }

    public async Task<bool> SetSpectrumScopeAsync(bool enabled)
    {
        // IC-7300 has built-in spectrum scope
        var command = new RadioCommand($"CIVSPEC:{(enabled ? "1" : "0")}", $"{(enabled ? "Enable" : "Disable")} spectrum scope", false, 2000);
        var response = await SendCommandAsync(command);
        return !string.IsNullOrEmpty(response);
    }

    public async Task<bool> SetDSPNoiseReductionAsync(int level)
    {
        // IC-7300 has advanced DSP noise reduction
        if (!SupportedFeatures.HasFeature(SupportedFeatures.NoiseReduction))
            return false;

        var command = new RadioCommand($"CIVDSPNR:{Math.Clamp(level, 0, 15)}", $"Set DSP noise reduction to level {level}", false, 2000);
        var response = await SendCommandAsync(command);
        return !string.IsNullOrEmpty(response);
    }
}

/// <summary>
/// Icom IC-9700 radio implementation
/// VHF/UHF/SHF transceiver with D-STAR and advanced features
/// </summary>
public class IcomIC9700 : BaseIcomRadio
{
    public override string ModelName => "IC-9700";

    /// <summary>
    /// IC-9700 supports VHF/UHF/SHF operation plus advanced features
    /// </summary>
    public override SupportedFeatures SupportedFeatures => 
        SupportedFeatures.FrequencyControl | 
        SupportedFeatures.ModeControl |
        SupportedFeatures.DualVFO | 
        SupportedFeatures.VFOSwap |
        SupportedFeatures.SplitOperation |
        SupportedFeatures.IFBandwidth |
        SupportedFeatures.PowerOutput |
        SupportedFeatures.SMeter |
        SupportedFeatures.SWRMeter |
        SupportedFeatures.MemoryChannels |
        SupportedFeatures.MemoryBanks |
        SupportedFeatures.SQLControl |
        SupportedFeatures.CTCSSTone |
        SupportedFeatures.DTCSCode |
        SupportedFeatures.RepeaterOffset |
        SupportedFeatures.NoiseReduction |
        SupportedFeatures.AGC |
        SupportedFeatures.TransmitStatus |
        SupportedFeatures.ReceiveStatus |
        SupportedFeatures.RadioID |
        SupportedFeatures.PowerOnOff |
        SupportedFeatures.Waterfall |
        SupportedFeatures.Panadapter |
        SupportedFeatures.ComputerControl;

    /// <summary>
    /// IC-9700 specific multi-band and digital features
    /// Note: These methods implement radio-specific features
    /// </summary>
    public async Task<bool> SetDualBandAsync(bool enabled)
    {
        // IC-9700 can operate on multiple bands simultaneously
        var command = new RadioCommand($"CIVDUAL:{(enabled ? "1" : "0")}", $"{(enabled ? "Enable" : "Disable")} dual band", false, 2000);
        var response = await SendCommandAsync(command);
        return !string.IsNullOrEmpty(response);
    }

    public async Task<bool> SetDStarAsync(bool enabled)
    {
        // IC-9700 has D-STAR digital mode
        var command = new RadioCommand($"CIVDSTAR:{(enabled ? "1" : "0")}", $"{(enabled ? "Enable" : "Disable")} D-STAR", false, 2000);
        var response = await SendCommandAsync(command);
        return !string.IsNullOrEmpty(response);
    }

    public async Task<bool> SetSatelliteModeAsync(bool enabled)
    {
        // IC-9700 supports satellite operation with automatic Doppler correction
        var command = new RadioCommand($"CIVSAT:{(enabled ? "1" : "0")}", $"{(enabled ? "Enable" : "Disable")} satellite mode", false, 2000);
        var response = await SendCommandAsync(command);
        return !string.IsNullOrEmpty(response);
    }

    public async Task<bool> SetCTCSSAsync(double frequency)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.CTCSSTone))
            return false;

        // Convert CTCSS frequency to Icom tone number
        int toneNum = CTCSSToIcomTone(frequency);
        var command = new RadioCommand($"CIVCTCSS:{toneNum}", $"Set CTCSS tone to {frequency} Hz", false, 2000);
        var response = await SendCommandAsync(command);
        return !string.IsNullOrEmpty(response);
    }

    public async Task<bool> SetRepeaterOffsetAsync(long offsetHz)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.RepeaterOffset))
            return false;

        var command = new RadioCommand($"CIVRPTOFS:{offsetHz}", $"Set repeater offset to {offsetHz} Hz", false, 2000);
        var response = await SendCommandAsync(command);
        return !string.IsNullOrEmpty(response);
    }

    private int CTCSSToIcomTone(double frequency)
    {
        // Common CTCSS frequencies mapped to Icom tone numbers
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