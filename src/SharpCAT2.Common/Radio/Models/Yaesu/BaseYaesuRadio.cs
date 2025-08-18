using SharpCAT2.Core.Radio;
using System.Text.RegularExpressions;
using SharpCAT2.Common.Radio.Protocols;

namespace SharpCAT2.Common.Radio.Models.Yaesu;

/// <summary>
/// Base class for Yaesu radios using CAT protocol
/// </summary>
public abstract class BaseYaesuRadio : BaseRadio
{
    protected readonly YaesuCATProtocol _protocol;

    protected BaseYaesuRadio()
    {
        _protocol = new YaesuCATProtocol();
    }

    public override string Manufacturer => "Yaesu";

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
        return modeNumber switch
        {
            1 => "LSB",
            2 => "USB",
            3 => "CW",
            4 => "FM",
            5 => "AM",
            6 => "RTTY-LSB",
            7 => "CW-R",
            8 => "DATA-LSB",
            9 => "RTTY-USB",
            10 => "DATA-FM",
            11 => "FM-N",
            12 => "DATA-USB",
            13 => "AM-N",
            14 => "C4FM",
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
        return !string.IsNullOrEmpty(response) && response.Contains("ST1");
    }

    // RIT/XIT Operations
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
        if (!string.IsNullOrEmpty(response))
        {
            var match = Regex.Match(response, @"PC(\d{3})");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int power))
            {
                return power;
            }
        }
        return 0;
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
        if (!string.IsNullOrEmpty(response))
        {
            var match = Regex.Match(response, @"SM0(\d{3})");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int meter))
            {
                // Convert Yaesu meter reading to S-units (0-9, >9 = dB over S9)
                return meter / 25; // Rough conversion
            }
        }
        return 0;
    }

    public override async Task<double> GetSWRAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.SWRMeter))
            return 1.0;

        var response = await SendCommandAsync(_protocol.GetSWRCommand());
        return !string.IsNullOrEmpty(response) ? _protocol.ParseSWR(response) : 1.0;
    }

    // Memory Operations
    public override async Task<bool> RecallMemoryChannelAsync(int channel)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.MemoryChannels))
            return false;

        var response = await SendCommandAsync(_protocol.SetMemoryChannelCommand(channel));
        return !string.IsNullOrEmpty(response);
    }

    // CW Operations
    public override async Task<int> GetCwSpeedAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.CWSpeed))
            return 0;

        var response = await SendCommandAsync(_protocol.GetCwSpeedCommand());
        if (!string.IsNullOrEmpty(response))
        {
            var match = Regex.Match(response, @"KS(\d{3})");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int speed))
            {
                return speed;
            }
        }
        return 20; // Default 20 WPM
    }

    public override async Task<bool> SetCwSpeedAsync(int wpm)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.CWSpeed))
            return false;

        var response = await SendCommandAsync(_protocol.SetCwSpeedCommand(Math.Clamp(wpm, 4, 60)));
        return !string.IsNullOrEmpty(response);
    }

    // Noise Reduction
    public override async Task<int> GetNoiseReductionAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.NoiseReduction))
            return 0;

        var response = await SendCommandAsync(_protocol.GetNoiseReductionCommand());
        if (!string.IsNullOrEmpty(response))
        {
            var match = Regex.Match(response, @"NR0(\d)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int level))
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

        var response = await SendCommandAsync(_protocol.SetNoiseReductionCommand(Math.Clamp(level, 0, 9)));
        return !string.IsNullOrEmpty(response);
    }

    // Power Control
    public override async Task<bool> SetPowerAsync(bool powerOn)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.PowerOnOff))
            return false;

        var command = powerOn ? _protocol.PowerOnCommand() : _protocol.PowerOffCommand();
        var response = await SendCommandAsync(command);
        return !string.IsNullOrEmpty(response);
    }

    public override async Task<bool> GetPowerAsync()
    {
        var response = await SendCommandAsync(_protocol.GetPowerStatusCommand());
        return !string.IsNullOrEmpty(response) && response.Contains("PS1");
    }
}