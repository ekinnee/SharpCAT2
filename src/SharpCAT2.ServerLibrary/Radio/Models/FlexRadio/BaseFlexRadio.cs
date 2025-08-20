using SharpCAT2.Core.Radio;
using System.Text.RegularExpressions;
using SharpCAT2.ServerLibrary.Radio.Protocols;

namespace SharpCAT2.ServerLibrary.Radio.Models.FlexRadio;

/// <summary>
/// Base class for FlexRadio radios using VITA-49 and CAT protocols
/// </summary>
public abstract class BaseFlexRadio : BaseRadio
{
    protected readonly FlexRadioProtocol _protocol;

    protected BaseFlexRadio()
    {
        _protocol = new FlexRadioProtocol();
    }

    public override string Manufacturer => "FlexRadio";

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
            0 => "LSB",
            1 => "USB",
            2 => "DSB",
            3 => "CWL",
            4 => "CWU",
            5 => "FM",
            6 => "AM",
            7 => "DIGU",
            8 => "SPEC",
            9 => "DIGL",
            10 => "SAM",
            11 => "DFM",
            _ => "USB"
        };
    }

    // VFO Operations (FlexRadio calls them "slices")
    public override async Task<string> GetVfoAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.DualVFO))
            throw new NotSupportedException("Multiple slices not supported");

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

    // FlexRadio-specific Slice Management
    public async Task<bool> CreateSliceAsync()
    {
        var response = await SendCommandAsync(_protocol.CreateSliceCommand());
        return !string.IsNullOrEmpty(response);
    }

    public async Task<bool> DeleteSliceAsync(int sliceId)
    {
        var response = await SendCommandAsync(_protocol.DeleteSliceCommand(sliceId));
        return !string.IsNullOrEmpty(response);
    }

    public async Task<string> GetSliceListAsync()
    {
        var response = await SendCommandAsync(_protocol.GetSliceListCommand());
        return response ?? "";
    }

    public async Task<bool> SetSliceFrequencyAsync(int sliceId, long frequency)
    {
        var response = await SendCommandAsync(_protocol.SetSliceFrequencyCommand(sliceId, frequency));
        return !string.IsNullOrEmpty(response);
    }

    public async Task<bool> SetSliceModeAsync(int sliceId, string mode)
    {
        var response = await SendCommandAsync(_protocol.SetSliceModeCommand(sliceId, mode));
        return !string.IsNullOrEmpty(response);
    }

    // SDR Features
    public async Task<bool> SetWaterfallAsync(bool enabled)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.Waterfall))
            return false;

        var response = await SendCommandAsync(_protocol.SetWaterfallCommand(enabled));
        return !string.IsNullOrEmpty(response);
    }

    public async Task<bool> GetWaterfallAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.Waterfall))
            return false;

        var response = await SendCommandAsync(_protocol.GetWaterfallCommand());
        return !string.IsNullOrEmpty(response) && response.Contains("ZZWF1");
    }

    public async Task<bool> SetPanadapterAsync(bool enabled)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.Panadapter))
            return false;

        var response = await SendCommandAsync(_protocol.SetPanadapterCommand(enabled));
        return !string.IsNullOrEmpty(response);
    }

    public async Task<bool> GetPanadapterAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.Panadapter))
            return false;

        var response = await SendCommandAsync(_protocol.GetPanadapterCommand());
        return !string.IsNullOrEmpty(response) && response.Contains("ZZPN1");
    }

    // CW Operations
    public override async Task<int> GetCwSpeedAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.CWSpeed))
            return 20;

        var command = new RadioCommand("ZZKS;", "Get CW speed", true, 2000);
        var response = await SendCommandAsync(command);
        if (!string.IsNullOrEmpty(response))
        {
            var match = Regex.Match(response, @"ZZKS(\d{3})");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int speed))
            {
                return speed;
            }
        }
        return 20;
    }

    public override async Task<bool> SetCwSpeedAsync(int wpm)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.CWSpeed))
            return false;

        var command = new RadioCommand($"ZZKS{Math.Clamp(wpm, 5, 60):D3};", $"Set CW speed to {wpm} WPM", false, 2000);
        var response = await SendCommandAsync(command);
        return !string.IsNullOrEmpty(response);
    }

    public override async Task<bool> SendCwMessageAsync(string message)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.CWMessage))
            return false;

        // FlexRadio CW message sending
        var command = new RadioCommand($"ZZKY {message};", $"Send CW message: {message}", false, 5000);
        var response = await SendCommandAsync(command);
        return !string.IsNullOrEmpty(response);
    }

    // Noise Reduction
    public override async Task<int> GetNoiseReductionAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.NoiseReduction))
            return 0;

        var command = new RadioCommand("ZZNR;", "Get noise reduction", true, 2000);
        var response = await SendCommandAsync(command);
        if (!string.IsNullOrEmpty(response))
        {
            var match = Regex.Match(response, @"ZZNR(\d+)");
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

        var command = new RadioCommand($"ZZNR{Math.Clamp(level, 0, 100):D3};", $"Set noise reduction to level {level}", false, 2000);
        var response = await SendCommandAsync(command);
        return !string.IsNullOrEmpty(response);
    }

    // Power Control
    public override async Task<bool> SetPowerAsync(bool powerOn)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.PowerOnOff))
            return false;

        var command = new RadioCommand($"ZZPS{(powerOn ? "1" : "0")};", $"{(powerOn ? "Power on" : "Power off")}", false, 5000);
        var response = await SendCommandAsync(command);
        return !string.IsNullOrEmpty(response);
    }

    public override async Task<bool> GetPowerAsync()
    {
        var command = new RadioCommand("ZZPS;", "Get power status", true, 2000);
        var response = await SendCommandAsync(command);
        return !string.IsNullOrEmpty(response) && response.Contains("ZZPS1");
    }
}