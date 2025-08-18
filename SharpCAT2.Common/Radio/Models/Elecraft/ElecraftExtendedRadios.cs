using SharpCAT2.Core.Radio;
namespace SharpCAT2.Common.Radio.Models.Elecraft;

/// <summary>
/// Elecraft K4 radio implementation
/// Latest generation high-performance HF transceiver with dual receive and advanced DSP
/// </summary>
public class ElecraftK4 : BaseElecraftRadio
{
    public override string ModelName => "K4";

    /// <summary>
    /// K4 supports the most advanced HF features available
    /// </summary>
    public override SupportedFeatures SupportedFeatures => 
        SupportedFeatures.FrequencyControl | 
        SupportedFeatures.ModeControl |
        SupportedFeatures.DualVFO | 
        SupportedFeatures.VFOSwap |
        SupportedFeatures.VFOEqual |
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
        SupportedFeatures.TuningStep |
        SupportedFeatures.FastTuning |
        SupportedFeatures.Preamp |
        SupportedFeatures.Attenuator |
        SupportedFeatures.DigitalModes |
        SupportedFeatures.PSK31 |
        SupportedFeatures.RTTY |
        SupportedFeatures.DualReceive |
        SupportedFeatures.ComputerControl;
}

/// <summary>
/// Elecraft KX3 radio implementation
/// Portable QRP HF transceiver perfect for field operations
/// </summary>
public class ElecraftKX3 : BaseElecraftRadio
{
    public override string ModelName => "KX3";

    /// <summary>
    /// KX3 supports portable QRP operation features
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
        SupportedFeatures.MemoryChannels |
        SupportedFeatures.CWKeyer |
        SupportedFeatures.CWSpeed |
        SupportedFeatures.AGC |
        SupportedFeatures.TransmitStatus |
        SupportedFeatures.ReceiveStatus |
        SupportedFeatures.RadioID |
        SupportedFeatures.PowerOnOff |
        SupportedFeatures.FilterSelection |
        SupportedFeatures.ComputerControl;
}

/// <summary>
/// Elecraft K2 radio implementation
/// Classic kit-built HF transceiver with proven design
/// </summary>
public class ElecraftK2 : BaseElecraftRadio
{
    public override string ModelName => "K2";

    /// <summary>
    /// K2 supports fundamental HF operation features
    /// </summary>
    public override SupportedFeatures SupportedFeatures => 
        SupportedFeatures.FrequencyControl | 
        SupportedFeatures.ModeControl |
        SupportedFeatures.DualVFO | 
        SupportedFeatures.VFOSwap |
        SupportedFeatures.RIT | 
        SupportedFeatures.XIT |
        SupportedFeatures.PowerOutput |
        SupportedFeatures.SMeter |
        SupportedFeatures.MemoryChannels |
        SupportedFeatures.CWKeyer |
        SupportedFeatures.CWSpeed |
        SupportedFeatures.TransmitStatus |
        SupportedFeatures.ReceiveStatus |
        SupportedFeatures.RadioID |
        SupportedFeatures.PowerOnOff |
        SupportedFeatures.ComputerControl;
}

/// <summary>
/// Elecraft K1 radio implementation
/// Ultra-portable QRP CW-only transceiver for lightweight field operations
/// </summary>
public class ElecraftK1 : BaseElecraftRadio
{
    public override string ModelName => "K1";

    /// <summary>
    /// K1 supports basic CW operation for portable use
    /// </summary>
    public override SupportedFeatures SupportedFeatures => 
        SupportedFeatures.FrequencyControl | 
        SupportedFeatures.ModeControl |
        SupportedFeatures.VFOSwap |
        SupportedFeatures.RIT |
        SupportedFeatures.PowerOutput |
        SupportedFeatures.SMeter |
        SupportedFeatures.MemoryChannels |
        SupportedFeatures.CWKeyer |
        SupportedFeatures.CWSpeed |
        SupportedFeatures.TransmitStatus |
        SupportedFeatures.ReceiveStatus |
        SupportedFeatures.RadioID |
        SupportedFeatures.ComputerControl;
}