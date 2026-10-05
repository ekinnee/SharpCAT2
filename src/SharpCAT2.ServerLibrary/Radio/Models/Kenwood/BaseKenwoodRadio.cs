using SharpCAT2.Core.Radio;
using System.Text.RegularExpressions;
using SharpCAT2.ServerLibrary.Radio.Protocols;

namespace SharpCAT2.ServerLibrary.Radio.Models.Kenwood;

/// <summary>
/// Base class for Kenwood radios using CAT protocol
/// </summary>
public abstract class BaseKenwoodRadio : BaseRadio
{
    protected readonly KenwoodCATProtocol _protocol;

    protected BaseKenwoodRadio()
    {
        _protocol = new KenwoodCATProtocol();
    }

    public override string Manufacturer => "Kenwood";

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
        return response is not null;
    }

    public override async Task<bool> SwapVfoAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.VFOSwap))
            return false;

        var response = await SendCommandAsync(_protocol.SwapVfoCommand());
        return response != null;
    }

    // Split Operations
    public override async Task<bool> SetSplitAsync(bool enabled)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.SplitOperation))
            return false;

        var response = await SendCommandAsync(_protocol.SetSplitCommand(enabled));
        return response != null;
    }

    public override async Task<bool> GetSplitAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.SplitOperation))
            return false;

        var response = await SendCommandAsync(_protocol.GetSplitCommand());
        return !string.IsNullOrEmpty(response) && _protocol.ParseSplit(response);
    }

    // RIT/XIT Operations
    public override async Task<bool> SetRitAsync(int offsetHz)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.RIT))
            return false;

        var response = await SendCommandAsync(_protocol.SetRitCommand(offsetHz));
        return response != null;
    }

    public override async Task<int> GetRitAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.RIT))
            return 0;

        var response = await SendCommandAsync(_protocol.GetRitCommand());
        return !string.IsNullOrEmpty(response) ? _protocol.ParseRit(response) : 0;
    }

    public override async Task<bool> SetXitAsync(int offsetHz)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.XIT))
            return false;

        var response = await SendCommandAsync(_protocol.SetXitCommand(offsetHz));
        return response != null;
    }

    public override async Task<int> GetXitAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.XIT))
            return 0;

        var response = await SendCommandAsync(_protocol.GetXitCommand());
        return !string.IsNullOrEmpty(response) ? _protocol.ParseXit(response) : 0;
    }

    // Power Operations
    public override async Task<bool> SetPowerOutputAsync(int powerPercent)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.PowerOutput))
            return false;

        var response = await SendCommandAsync(_protocol.SetPowerOutputCommand(powerPercent));
        return response != null;
    }

    public override async Task<int> GetPowerOutputAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.PowerOutput))
            return 0;

        var response = await SendCommandAsync(_protocol.GetPowerOutputCommand());
        return !string.IsNullOrEmpty(response) ? _protocol.ParsePowerOutput(response) : 0;
    }

    // Metering
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

    // Antenna Operations
    public override async Task<bool> SetAntennaAsync(int antenna)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.AntennaSelection))
            return false;

        var response = await SendCommandAsync(_protocol.SetAntennaCommand(antenna));
        return response != null;
    }

    public override async Task<int> GetAntennaAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.AntennaSelection))
            return 1;

        var response = await SendCommandAsync(_protocol.GetAntennaCommand());
        return !string.IsNullOrEmpty(response) ? _protocol.ParseAntenna(response) : 1;
    }

    // IF Bandwidth Operations
    public override async Task<bool> SetIfBandwidthAsync(int bandwidth)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.IFBandwidth))
            return false;

        var response = await SendCommandAsync(((KenwoodCATProtocol)_protocol).SetIfBandwidthCommand(bandwidth));
        return response != null;
    }

    public override async Task<int> GetIfBandwidthAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.IFBandwidth))
            return 0;

        var response = await SendCommandAsync(((KenwoodCATProtocol)_protocol).GetIfBandwidthCommand());
        if (!string.IsNullOrEmpty(response))
        {
            var match = Regex.Match(response, @"FW(\d{4})");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int bw))
            {
                return bw;
            }
        }
        return 0;
    }

    // Memory Operations
    public override async Task<bool> SetMemoryChannelAsync(int channel, long frequency, string mode)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.MemoryChannels))
            return false;

        try
        {
            var setChannelResponse = await SendCommandAsync(((KenwoodCATProtocol)_protocol).SetMemoryChannelCommand(channel));
            if (setChannelResponse == null) return false;

            var setFreqResponse = await SendCommandAsync(_protocol.SetFrequencyCommand(frequency));
            if (setFreqResponse == null) return false;

            var setModeResponse = await SendCommandAsync(_protocol.SetModeCommand(mode));
            return setModeResponse != null;
        }
        catch
        {
            return false;
        }
    }

    public override async Task<bool> RecallMemoryChannelAsync(int channel)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.MemoryChannels))
            return false;

        var response = await SendCommandAsync(((KenwoodCATProtocol)_protocol).SetMemoryChannelCommand(channel));
        return response != null;
    }

    // CW Operations
    public override async Task<bool> SetCwSpeedAsync(int wpm)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.CWSpeed))
            return false;

        var response = await SendCommandAsync(((KenwoodCATProtocol)_protocol).SetCwSpeedCommand(wpm));
        return response != null;
    }

    public override async Task<int> GetCwSpeedAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.CWSpeed))
            return 0;

        var response = await SendCommandAsync(((KenwoodCATProtocol)_protocol).GetCwSpeedCommand());
        if (!string.IsNullOrEmpty(response))
        {
            var match = Regex.Match(response, @"KS(\d{3})");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int speed))
            {
                return speed;
            }
        }
        return 0;
    }

    public override async Task<bool> SendCwMessageAsync(string message)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.CWKeyer))
            return false;

        var response = await SendCommandAsync(((KenwoodCATProtocol)_protocol).SendCwMessageCommand(message));
        return response != null;
    }

    // Noise Reduction
    public override async Task<bool> SetNoiseReductionAsync(int level)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.NoiseReduction))
            return false;

        var response = await SendCommandAsync(((KenwoodCATProtocol)_protocol).SetNoiseReductionCommand(level));
        return response != null;
    }

    public override async Task<int> GetNoiseReductionAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.NoiseReduction))
            return 0;

        var response = await SendCommandAsync(((KenwoodCATProtocol)_protocol).GetNoiseReductionCommand());
        if (!string.IsNullOrEmpty(response))
        {
            var match = Regex.Match(response, @"NR(\d)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int level))
            {
                return level;
            }
        }
        return 0;
    }

    // Power Control
    public override async Task<bool> SetPowerAsync(bool powerOn)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.PowerOnOff))
            return false;

        var command = powerOn ? ((KenwoodCATProtocol)_protocol).PowerOnCommand() : ((KenwoodCATProtocol)_protocol).PowerOffCommand();
        var response = await SendCommandAsync(command);
        return response != null;
    }

    public override async Task<bool> GetPowerAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.PowerOnOff))
            return true;

        var response = await SendCommandAsync(((KenwoodCATProtocol)_protocol).GetPowerStatusCommand());
        if (!string.IsNullOrEmpty(response))
        {
            var match = Regex.Match(response, @"PS(\d)");
            if (match.Success)
            {
                return match.Groups[1].Value == "1";
            }
        }
        return true;
    }
}