using SharpCAT2.Core.Radio;
namespace SharpCAT2.ServerLibrary.Radio.Models.Kenwood;

/// <summary>
/// Kenwood TS-890S radio implementation
/// High-end HF transceiver with advanced DSP and spectrum scope
/// </summary>
public class KenwoodTS890S : BaseKenwoodRadio
{
    public override string ModelName => "TS-890S";

    /// <summary>
    /// TS-890S supports advanced HF features with superior DSP and filtering
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
        SupportedFeatures.Preamp |
        SupportedFeatures.Attenuator |
        SupportedFeatures.DigitalModes |
        SupportedFeatures.SpectrumScope |
        SupportedFeatures.DualReceive |
        SupportedFeatures.ComputerControl;
}

/// <summary>
/// Kenwood TS-590SG radio implementation
/// Popular HF transceiver with excellent performance
/// </summary>
public class KenwoodTS590SG : BaseKenwoodRadio
{
    public override string ModelName => "TS-590SG";

    /// <summary>
    /// TS-590SG supports standard HF features with solid performance
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
        SupportedFeatures.ComputerControl;
}

/// <summary>
/// Kenwood TH-D74A radio implementation
/// VHF/UHF handheld with APRS, D-STAR, and GPS capabilities
/// </summary>
public class KenwoodTHD74A : BaseKenwoodRadio
{
    public override string ModelName => "TH-D74A";

    /// <summary>
    /// TH-D74A supports advanced VHF/UHF handheld features with digital modes
    /// </summary>
    public override SupportedFeatures SupportedFeatures => 
        SupportedFeatures.FrequencyControl | 
        SupportedFeatures.ModeControl |
        SupportedFeatures.DualVFO |
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
        SupportedFeatures.PowerOnOff |
        SupportedFeatures.DigitalModes |
        SupportedFeatures.ComputerControl;
}

/// <summary>
/// Kenwood TM-D710GA radio implementation
/// VHF/UHF mobile with APRS and cross-band repeat capability
/// </summary>
public class KenwoodTMD710GA : BaseKenwoodRadio
{
    public override string ModelName => "TM-D710GA";

    /// <summary>
    /// TM-D710GA supports dual-band mobile features with APRS
    /// </summary>
    public override SupportedFeatures SupportedFeatures => 
        SupportedFeatures.FrequencyControl | 
        SupportedFeatures.ModeControl |
        SupportedFeatures.DualVFO |
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
        SupportedFeatures.PowerOnOff |
        SupportedFeatures.CrossBandRepeat |
        SupportedFeatures.ComputerControl;
}