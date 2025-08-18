using System;

namespace SharpCAT2.Core.Radio;

/// <summary>
/// Enumeration of radio features that can be supported
/// </summary>
[Flags]
public enum SupportedFeatures : long
{
    None = 0,
    
    // Basic frequency and mode operations
    FrequencyControl = 1L << 0,
    ModeControl = 1L << 1,
    
    // VFO operations
    DualVFO = 1L << 2,
    VFOSwap = 1L << 3,
    VFOEqual = 1L << 4,
    
    // Split operations
    SplitOperation = 1L << 5,
    
    // Bandwidth controls
    IFBandwidth = 1L << 6,
    
    // RIT/XIT controls
    RIT = 1L << 7,        // Receiver Incremental Tuning
    XIT = 1L << 8,        // Transmitter Incremental Tuning
    
    // Power and metering
    PowerOutput = 1L << 9,
    SMeter = 1L << 10,
    SWRMeter = 1L << 11,
    ALCMeter = 1L << 12,
    
    // Antenna selection
    AntennaSelection = 1L << 13,
    
    // Memory operations
    MemoryChannels = 1L << 14,
    MemoryBanks = 1L << 15,
    MemoryScan = 1L << 16,
    
    // Scanning operations
    FrequencyScan = 1L << 17,
    
    // CW/Keyer operations
    CWKeyer = 1L << 18,
    CWSpeed = 1L << 19,
    CWMessage = 1L << 20,
    
    // DSP features
    NoiseReduction = 1L << 21,
    NoiseFilter = 1L << 22,
    AGC = 1L << 23,         // Automatic Gain Control
    
    // Audio controls
    AudioGain = 1L << 24,
    SQLControl = 1L << 25,   // Squelch
    
    // Transceiver status
    TransmitStatus = 1L << 26,
    ReceiveStatus = 1L << 27,
    
    // Digital modes
    DigitalModes = 1L << 28,
    PSK31 = 1L << 29,
    RTTY = 1L << 30,
    
    // Band controls
    BandUp = 1L << 31,
    BandDown = 1L << 32,
    
    // Filters
    FilterSelection = 1L << 33,
    
    // Tuning controls
    TuningStep = 1L << 34,
    FastTuning = 1L << 35,
    
    // Power control
    PowerOnOff = 1L << 36,
    
    // Radio identification
    RadioID = 1L << 37,
    
    // Preamp/Attenuator
    Preamp = 1L << 38,
    Attenuator = 1L << 39,
    
    // Tone controls (FM)
    CTCSSTone = 1L << 40,
    DTCSCode = 1L << 41,
    
    // Repeater controls
    RepeaterOffset = 1L << 42,
    
    // Voice operations
    VoiceMemory = 1L << 43,
    
    // Computer control specific
    ComputerControl = 1L << 44,
    
    // Extended features for modern radios
    Waterfall = 1L << 45,
    Panadapter = 1L << 46,
    
    // Additional Hamlib-compatible features
    MonitorLevel = 1L << 47,         // Monitor/sidetone level
    MicGain = 1L << 48,              // Microphone gain
    CompLevel = 1L << 49,            // Speech compression level
    VoxLevel = 1L << 50,             // VOX level control
    VoxDelay = 1L << 51,             // VOX delay control
    VoxGain = 1L << 52,              // VOX gain control
    BreakIn = 1L << 53,              // QSK/Break-in control
    Notch = 1L << 54,                // Notch filter control
    NoiseFilterBank = 1L << 55,      // Multiple noise filter bank
    Equalizer = 1L << 56,            // Audio equalizer
    SpectrumScope = 1L << 57,        // Built-in spectrum scope
    DualReceive = 1L << 58,          // Dual receive capability
    CrossBandRepeat = 1L << 59,      // Cross-band repeat
    EmergencyMode = 1L << 60,        // Emergency/priority channels
    WeatherAlert = 1L << 61,         // Weather alert monitoring
    
    // Convenience combinations
    BasicOperation = FrequencyControl | ModeControl | TransmitStatus | ReceiveStatus | RadioID,
    HFOperation = BasicOperation | DualVFO | VFOSwap | SplitOperation | RIT | XIT | SMeter | PowerOutput,
    VHFUHFOperation = BasicOperation | SQLControl | CTCSSTone | RepeaterOffset,
    AdvancedOperation = HFOperation | IFBandwidth | NoiseReduction | AGC | MemoryChannels | CWKeyer,
    SDROperation = AdvancedOperation | Waterfall | Panadapter | SpectrumScope | DualReceive,
    FullFeatureSet = -1L  // All features
}

/// <summary>
/// Helper class for working with SupportedFeatures
/// </summary>
public static class SupportedFeaturesExtensions
{
    /// <summary>
    /// Checks if a specific feature is supported
    /// </summary>
    public static bool HasFeature(this SupportedFeatures features, SupportedFeatures feature)
    {
        return (features & feature) == feature;
    }
    
    /// <summary>
    /// Gets a human-readable description of supported features
    /// </summary>
    public static string GetDescription(this SupportedFeatures features)
    {
        if (features == SupportedFeatures.None)
            return "No advanced features supported";
            
        if (features == SupportedFeatures.FullFeatureSet)
            return "All features supported";
            
        var featureList = new List<string>();
        
        if (features.HasFeature(SupportedFeatures.FrequencyControl))
            featureList.Add("Frequency Control");
        if (features.HasFeature(SupportedFeatures.ModeControl))
            featureList.Add("Mode Control");
        if (features.HasFeature(SupportedFeatures.DualVFO))
            featureList.Add("Dual VFO");
        if (features.HasFeature(SupportedFeatures.SplitOperation))
            featureList.Add("Split Operation");
        if (features.HasFeature(SupportedFeatures.RIT))
            featureList.Add("RIT");
        if (features.HasFeature(SupportedFeatures.XIT))
            featureList.Add("XIT");
        if (features.HasFeature(SupportedFeatures.PowerOutput))
            featureList.Add("Power Output Control");
        if (features.HasFeature(SupportedFeatures.SMeter))
            featureList.Add("S-Meter");
        if (features.HasFeature(SupportedFeatures.SWRMeter))
            featureList.Add("SWR Meter");
        if (features.HasFeature(SupportedFeatures.AntennaSelection))
            featureList.Add("Antenna Selection");
        if (features.HasFeature(SupportedFeatures.MemoryChannels))
            featureList.Add("Memory Channels");
        if (features.HasFeature(SupportedFeatures.CWKeyer))
            featureList.Add("CW Keyer");
        if (features.HasFeature(SupportedFeatures.NoiseReduction))
            featureList.Add("Noise Reduction");
        if (features.HasFeature(SupportedFeatures.IFBandwidth))
            featureList.Add("IF Bandwidth Control");
            
        return string.Join(", ", featureList);
    }
    
    /// <summary>
    /// Gets the count of supported features
    /// </summary>
    public static int GetFeatureCount(this SupportedFeatures features)
    {
        if (features == SupportedFeatures.FullFeatureSet)
        {
            // Return a reasonable count for full feature set instead of trying to count all bits
            return Enum.GetValues<SupportedFeatures>()
                .Where(f => f != SupportedFeatures.None && 
                           f != SupportedFeatures.FullFeatureSet &&
                           f != SupportedFeatures.BasicOperation &&
                           f != SupportedFeatures.HFOperation &&
                           f != SupportedFeatures.VHFUHFOperation &&
                           f != SupportedFeatures.AdvancedOperation &&
                           (long)f > 0 && ((long)f & ((long)f - 1)) == 0) // Power of 2 check
                .Count();
        }

        int count = 0;
        long value = (long)features;
        
        // Handle negative values (shouldn't happen except for FullFeatureSet)
        if (value < 0)
            return 0;
        
        while (value != 0)
        {
            count += (int)(value & 1);
            value >>= 1;
        }
        
        return count;
    }
}