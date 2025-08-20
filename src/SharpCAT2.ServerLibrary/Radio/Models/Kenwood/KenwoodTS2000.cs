using SharpCAT2.Core.Radio;
namespace SharpCAT2.ServerLibrary.Radio.Models.Kenwood;

/// <summary>
/// Kenwood TS-2000 radio implementation
/// Multi-band HF/VHF/UHF transceiver with satellite capability
/// </summary>
public class KenwoodTS2000 : BaseKenwoodRadio
{
    public override string ModelName => "TS-2000";

    /// <summary>
    /// TS-2000 supports comprehensive HF/VHF/UHF features including satellite operation
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
        SupportedFeatures.AntennaSelection |
        SupportedFeatures.MemoryChannels |
        SupportedFeatures.MemoryBanks |
        SupportedFeatures.CWKeyer |
        SupportedFeatures.CWSpeed |
        SupportedFeatures.NoiseReduction |
        SupportedFeatures.SQLControl |
        SupportedFeatures.CTCSSTone |
        SupportedFeatures.RepeaterOffset |
        SupportedFeatures.TransmitStatus |
        SupportedFeatures.ReceiveStatus |
        SupportedFeatures.RadioID |
        SupportedFeatures.PowerOnOff |
        SupportedFeatures.ComputerControl;
}