using SharpCAT2.Core.Radio;
using System.Text.RegularExpressions;

namespace SharpCAT2.ServerLibrary.Radio.Models.FlexRadio;

/// <summary>
/// FlexRadio FLEX-6400 radio implementation
/// Software Defined Radio with advanced features
/// </summary>
public class FlexRadio6400 : BaseFlexRadio
{
    public override string ModelName => "FLEX-6400";

    /// <summary>
    /// FLEX-6400 supports advanced SDR features with 2 slices
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
        SupportedFeatures.ALCMeter |
        SupportedFeatures.AntennaSelection |
        SupportedFeatures.MemoryChannels |
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
        SupportedFeatures.TuningStep |
        SupportedFeatures.FastTuning |
        SupportedFeatures.Preamp |
        SupportedFeatures.Attenuator |
        SupportedFeatures.DigitalModes |
        SupportedFeatures.PSK31 |
        SupportedFeatures.RTTY |
        SupportedFeatures.Waterfall |
        SupportedFeatures.Panadapter |
        SupportedFeatures.ComputerControl;

    /// <summary>
    /// FLEX-6400 specific features - 2 slice capability
    /// </summary>
    public async Task<bool> SetDualSliceAsync(bool enabled)
    {
        if (enabled)
        {
            // Create a second slice for dual operation
            return await CreateSliceAsync();
        }
        else
        {
            // Delete slice 1 (keep slice 0)
            return await DeleteSliceAsync(1);
        }
    }

    public Task<int> GetMaxSlicesAsync()
    {
        // FLEX-6400 supports up to 2 slices
        return Task.FromResult(2);
    }
}

/// <summary>
/// FlexRadio FLEX-6600 radio implementation
/// High-end SDR with advanced features and 4 slices
/// </summary>
public class FlexRadio6600 : BaseFlexRadio
{
    public override string ModelName => "FLEX-6600";

    /// <summary>
    /// FLEX-6600 supports full-featured SDR operation with 4 slices
    /// </summary>
    public override SupportedFeatures SupportedFeatures => 
        SupportedFeatures.FullFeatureSet; // All features supported

    /// <summary>
    /// FLEX-6600 specific features - 4 slice capability and SO2R
    /// </summary>
    public Task<int> GetMaxSlicesAsync()
    {
        // FLEX-6600 supports up to 4 slices
        return Task.FromResult(4);
    }

    public async Task<bool> SetSO2RModeAsync(bool enabled)
    {
        // FLEX-6600 supports Single Operator Two Radio mode
        var command = new RadioCommand($"ZZSO2R{(enabled ? "1" : "0")};", $"{(enabled ? "Enable" : "Disable")} SO2R mode", false, 2000);
        var response = await SendCommandAsync(command);
        return !string.IsNullOrEmpty(response);
    }

    public async Task<bool> SetStationControlUnitAsync(int scu)
    {
        // FLEX-6600 can work with multiple SCUs
        var command = new RadioCommand($"ZZSCU{scu};", $"Set SCU to {scu}", false, 2000);
        var response = await SendCommandAsync(command);
        return !string.IsNullOrEmpty(response);
    }
}

/// <summary>
/// FlexRadio FLEX-6700 radio implementation  
/// Top-tier SDR transceiver with 8 slices and contest features
/// </summary>
public class FlexRadio6700 : BaseFlexRadio
{
    public override string ModelName => "FLEX-6700";

    /// <summary>
    /// FLEX-6700 supports all advanced SDR features with maximum slice count
    /// </summary>
    public override SupportedFeatures SupportedFeatures => 
        SupportedFeatures.FullFeatureSet; // All features supported

    /// <summary>
    /// FLEX-6700 specific features - 8 slice capability and contest features
    /// </summary>
    public Task<int> GetMaxSlicesAsync()
    {
        // FLEX-6700 supports up to 8 slices
        return Task.FromResult(8);
    }

    public async Task<bool> SetContestModeAsync(bool enabled)
    {
        // FLEX-6700 has advanced contest features
        var command = new RadioCommand($"ZZCONTEST{(enabled ? "1" : "0")};", $"{(enabled ? "Enable" : "Disable")} contest mode", false, 2000);
        var response = await SendCommandAsync(command);
        return !string.IsNullOrEmpty(response);
    }

    public async Task<bool> SetMultiStationAsync(bool enabled)
    {
        // FLEX-6700 supports multi-station operation
        var command = new RadioCommand($"ZZMULTI{(enabled ? "1" : "0")};", $"{(enabled ? "Enable" : "Disable")} multi-station", false, 2000);
        var response = await SendCommandAsync(command);
        return !string.IsNullOrEmpty(response);
    }

    public async Task<bool> SetDiversityAsync(bool enabled)
    {
        // FLEX-6700 supports diversity reception
        var command = new RadioCommand($"ZZDIV{(enabled ? "1" : "0")};", $"{(enabled ? "Enable" : "Disable")} diversity", false, 2000);
        var response = await SendCommandAsync(command);
        return !string.IsNullOrEmpty(response);
    }
}