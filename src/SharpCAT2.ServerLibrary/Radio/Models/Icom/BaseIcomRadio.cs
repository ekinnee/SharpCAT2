using SharpCAT2.Core.Radio;
using System.Text.RegularExpressions;
using SharpCAT2.ServerLibrary.Radio.Protocols;

namespace SharpCAT2.ServerLibrary.Radio.Models.Icom;

/// <summary>
/// Base class for Icom radios using CI-V protocol
/// </summary>
public abstract class BaseIcomRadio : BaseRadio
{
    protected readonly IcomCIVProtocol _protocol;

    protected BaseIcomRadio()
    {
        _protocol = new IcomCIVProtocol();
    }

    public override string Manufacturer => "Icom";

    protected override bool IsCompleteResponse(string response)
    {
        return _protocol.IsCompleteResponse(response);
    }

    protected override long ParseFrequency(string response)
    {
        return _protocol.ParseFrequency(response);
    }

    protected override void ParseTransceiverInfo(string response, RadioStatus status)
    {
        _protocol.ParseTransceiverInfo(response, status);
    }

    protected override string MapModeNumber(int modeNumber)
    {
        // Icom CI-V mode mapping
        return modeNumber switch
        {
            0 => "LSB",
            1 => "USB",
            2 => "AM",
            3 => "CW",
            4 => "RTTY",
            5 => "FM",
            6 => "CW-R",
            7 => "RTTY-R",
            8 => "PSK",
            17 => "PSK-R",
            _ => "USB"
        };
    }

    // VFO Operations
    public override async Task<string> GetVfoAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.DualVFO))
            throw new NotSupportedException("Dual VFO not supported");

        var response = await SendCommandAsync(_protocol.GetVfoCommand());
        return !string.IsNullOrEmpty(response) ? _protocol.ParseVfo(response) : "A";
    }

    public override async Task<bool> SetVfoAsync(string vfo)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.DualVFO))
            return false;

        var response = await SendCommandAsync(_protocol.SetVfoCommand(vfo));
        return !string.IsNullOrEmpty(response);
    }

    public override async Task<bool> SwapVfoAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.VFOSwap))
            return false;

        var response = await SendCommandAsync(_protocol.SwapVfoCommand());
        return !string.IsNullOrEmpty(response);
    }

    // Split Operation
    public override async Task<bool> SetSplitAsync(bool enabled)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.SplitOperation))
            return false;

        var response = await SendCommandAsync(_protocol.SetSplitCommand(enabled));
        return !string.IsNullOrEmpty(response);
    }

    public override async Task<bool> GetSplitAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.SplitOperation))
            return false;

        var response = await SendCommandAsync(_protocol.GetSplitCommand());
        return !string.IsNullOrEmpty(response) && _protocol.ParseSplit(response);
    }

    // RIT/XIT Operations (limited on many Icom models)
    public override async Task<int> GetRitAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.RIT))
            return 0;

        var response = await SendCommandAsync(_protocol.GetRitCommand());
        return !string.IsNullOrEmpty(response) ? _protocol.ParseRit(response) : 0;
    }

    public override async Task<bool> SetRitAsync(int offsetHz)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.RIT))
            return false;

        var response = await SendCommandAsync(_protocol.SetRitCommand(offsetHz));
        return !string.IsNullOrEmpty(response);
    }

    public override async Task<int> GetXitAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.XIT))
            return 0;

        var response = await SendCommandAsync(_protocol.GetXitCommand());
        return !string.IsNullOrEmpty(response) ? _protocol.ParseXit(response) : 0;
    }

    public override async Task<bool> SetXitAsync(int offsetHz)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.XIT))
            return false;

        var response = await SendCommandAsync(_protocol.SetXitCommand(offsetHz));
        return !string.IsNullOrEmpty(response);
    }

    // Power Control
    public override async Task<int> GetPowerOutputAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.PowerOutput))
            return 0;

        var response = await SendCommandAsync(_protocol.GetPowerOutputCommand());
        return !string.IsNullOrEmpty(response) ? _protocol.ParsePowerOutput(response) : 0;
    }

    public override async Task<bool> SetPowerOutputAsync(int powerPercent)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.PowerOutput))
            return false;

        var response = await SendCommandAsync(_protocol.SetPowerOutputCommand(Math.Clamp(powerPercent, 0, 100)));
        return !string.IsNullOrEmpty(response);
    }

    // Meter Readings
    public override async Task<int> GetSMeterAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.SMeter))
            return 0;

        var response = await SendCommandAsync(_protocol.GetSMeterCommand());
        return !string.IsNullOrEmpty(response) ? _protocol.ParseSMeter(response) : 0;
    }

    public override async Task<double> GetSWRAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.SWRMeter))
            return 1.0;

        var response = await SendCommandAsync(_protocol.GetSWRCommand());
        return !string.IsNullOrEmpty(response) ? _protocol.ParseSWR(response) : 1.0;
    }

    // Antenna Selection
    public override async Task<int> GetAntennaAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.AntennaSelection))
            return 1;

        var response = await SendCommandAsync(_protocol.GetAntennaCommand());
        return !string.IsNullOrEmpty(response) ? _protocol.ParseAntenna(response) : 1;
    }

    public override async Task<bool> SetAntennaAsync(int antenna)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.AntennaSelection))
            return false;

        var response = await SendCommandAsync(_protocol.SetAntennaCommand(antenna));
        return !string.IsNullOrEmpty(response);
    }

    // Memory Operations (basic implementation)
    public override async Task<bool> RecallMemoryChannelAsync(int channel)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.MemoryChannels))
            return false;

        // Simulate memory recall with CI-V command
        var command = new RadioCommand($"CIVMEM:{channel}", $"Recall memory channel {channel}", false, 2000);
        var response = await SendCommandAsync(command);
        return !string.IsNullOrEmpty(response);
    }

    // Noise Reduction
    public override async Task<int> GetNoiseReductionAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.NoiseReduction))
            return 0;

        var command = new RadioCommand("CIVNRGET", "Get noise reduction", true, 2000);
        var response = await SendCommandAsync(command);
        if (!string.IsNullOrEmpty(response) && response.StartsWith("CIVNR:"))
        {
            var nrStr = response.Substring(6);
            if (int.TryParse(nrStr, out int level))
            {
                return level;
            }
        }
        return 0;
    }

    public override async Task<bool> SetNoiseReductionAsync(int level)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.NoiseReduction))
            return false;

        var command = new RadioCommand($"CIVNR:{Math.Clamp(level, 0, 10)}", $"Set noise reduction to level {level}", false, 2000);
        var response = await SendCommandAsync(command);
        return !string.IsNullOrEmpty(response);
    }

    // Power Control
    public override async Task<bool> SetPowerAsync(bool powerOn)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.PowerOnOff))
            return false;

        var command = new RadioCommand($"CIVPWRON:{(powerOn ? "1" : "0")}", $"{(powerOn ? "Power on" : "Power off")}", false, 5000);
        var response = await SendCommandAsync(command);
        return !string.IsNullOrEmpty(response);
    }

    public override async Task<bool> GetPowerAsync()
    {
        var command = new RadioCommand("CIVPWRSTAT", "Get power status", true, 2000);
        var response = await SendCommandAsync(command);
        return !string.IsNullOrEmpty(response) && (response.Contains("CIVPWR:1") || response.Contains("POWER:ON"));
    }

    // IF Bandwidth Control (common on Icom radios)
    public override async Task<bool> SetIfBandwidthAsync(int bandwidth)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.IFBandwidth))
            return false;

        // Convert bandwidth to Icom bandwidth number (simplified)
        var bwNum = bandwidth switch
        {
            >= 3000 => 1, // Wide
            >= 2400 => 2, // Medium
            >= 1200 => 3, // Narrow
            _ => 2        // Default medium
        };

        var command = new RadioCommand($"CIVBW:{bwNum}", $"Set IF bandwidth to {bandwidth} Hz", false, 2000);
        var response = await SendCommandAsync(command);
        return !string.IsNullOrEmpty(response);
    }

    public override async Task<int> GetIfBandwidthAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.IFBandwidth))
            return 0;

        var command = new RadioCommand("CIVBWGET", "Get IF bandwidth", true, 2000);
        var response = await SendCommandAsync(command);
        if (!string.IsNullOrEmpty(response) && response.StartsWith("CIVBW:"))
        {
            var bwStr = response.Substring(6);
            if (int.TryParse(bwStr, out int bwNum))
            {
                // Convert Icom bandwidth number back to Hz
                return bwNum switch
                {
                    1 => 3000, // Wide
                    2 => 2400, // Medium  
                    3 => 1200, // Narrow
                    _ => 2400  // Default
                };
            }
        }
        return 2400; // Default
    }

    // Filter Selection (Icom radios often have multiple filter options)
    public async Task<bool> SetFilterAsync(int filterNumber)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.FilterSelection))
            return false;

        var command = new RadioCommand($"CIVFILT:{filterNumber}", $"Set filter to {filterNumber}", false, 2000);
        var response = await SendCommandAsync(command);
        return !string.IsNullOrEmpty(response);
    }

    // Preamp/Attenuator Control
    public async Task<bool> SetPreampAsync(bool enabled)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.Preamp))
            return false;

        var command = new RadioCommand($"CIVPREAMP:{(enabled ? "1" : "0")}", $"{(enabled ? "Enable" : "Disable")} preamp", false, 2000);
        var response = await SendCommandAsync(command);
        return !string.IsNullOrEmpty(response);
    }

    public async Task<bool> SetAttenuatorAsync(bool enabled)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.Attenuator))
            return false;

        var command = new RadioCommand($"CIVATT:{(enabled ? "1" : "0")}", $"{(enabled ? "Enable" : "Disable")} attenuator", false, 2000);
        var response = await SendCommandAsync(command);
        return !string.IsNullOrEmpty(response);
    }
}