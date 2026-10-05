using System.Globalization;
using System.Text;
using SharpCAT2.Core.Radio;
using SharpCAT2.Core.Radio.Contracts;
using SharpCAT2.ServerLibrary.Radio.Protocols;
using SharpCAT2.ServerLibrary.Radio.Session;

namespace SharpCAT2.ServerLibrary.Radio.Models.Yaesu;

/// <summary>Manufacturer-backed preview profile. Protocol/emulator proof is not hardware verification.</summary>
public class YaesuFT991A : BaseYaesuRadio, IRadioOperations
{
    private readonly FT991AProfile _profile = new();
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(2);
    public override string ModelName => "FT-991A";
    public IReadOnlyList<RadioCapability> Capabilities => _profile.Capabilities;
    public override SupportedFeatures SupportedFeatures => SupportedFeatures.FrequencyControl |
        SupportedFeatures.ModeControl | SupportedFeatures.DualVFO | SupportedFeatures.VFOSwap |
        SupportedFeatures.RadioID | SupportedFeatures.ComputerControl;

    protected override FrameParseResult ParseSessionFrame(ReadOnlyMemory<byte> buffer) => _profile.ParseFrame(buffer.Span);

    protected override Task<bool> SynchronizeSessionAsync(RadioSession session)
    {
        var identify = new RadioOperationRequest(RadioOperation.Identify);
        _profile.TryCreateCommand(identify, out var command, out _, out _);
        return session.ConnectAsync(new SessionTransactionStep[]
        {
            new(_profile.CreateDisableAutomaticInformationCommand(), _ => new(ReplyParseStatus.Unrelated),
                IsMutation: true, QuietPeriodAfter: TimeSpan.FromMilliseconds(100)),
            new(_profile.CreateAutomaticInformationQueryCommand(), frame => _profile.ParseAutomaticInformationReply(frame.Span)),
            new(command!, frame => _profile.ParseReply(identify, "ID", frame.Span))
        }, OperationTimeout);
    }

    public async Task<RadioOperationResult<object>> ExecuteAsync(RadioOperationRequest request,
        CancellationToken cancellationToken = default)
        => await ExecuteCoreAsync(request, OperationTimeout, cancellationToken);

    private async Task<RadioOperationResult<object>> ExecuteCoreAsync(RadioOperationRequest request,
        TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        RadioOperationResult<object> result;
        if (!_profile.TryCreateCommand(request, out var command, out var outcome, out var diagnostic))
            result = new(outcome, CompletionEvidence.NotSent, diagnostic: diagnostic);
        else if (Session is not { } session)
            result = new(RadioOutcome.NotConnected, CompletionEvidence.NotSent);
        else if (request.Operation == RadioOperation.SwapVfo)
            result = await SwapAsync(session, command, timeout, cancellationToken);
        else
        {
            var mutation = request.Operation is RadioOperation.SetFrequency or RadioOperation.SetMode;
            command = new CommandSpecification(command.Payload, command.ResponsePolicy, timeout,
                command.ResponseKey, command.VerificationPayload);
            result = await session.ExecuteAsync(command,
                frame => _profile.ParseReply(request, command.ResponseKey!, frame.Span), mutation,
                mutation ? value => request.Operation == RadioOperation.SetFrequency
                    ? value is long hz && hz == request.FrequencyHz
                    : value is string mode && string.Equals(mode, request.Mode, StringComparison.OrdinalIgnoreCase)
                    : null, cancellationToken);
        }
        LastOperationResult = result;
        // Cache only observed state. A failed/mismatched read-back can still contain a real observation.
        if (result.Observation?.Value is long frequency && request.Vfo == RadioVfo.A) Frequency = frequency;
        if (result.Observation?.Value is string observedMode && request.Operation is RadioOperation.GetMode or RadioOperation.SetMode)
            Mode = observedMode;
        return result;
    }

    private Task<RadioOperationResult<object>> SwapAsync(RadioSession session, CommandSpecification swap,
        TimeSpan timeout, CancellationToken cancellationToken)
    {
        SessionTransactionStep Read(RadioVfo vfo)
        {
            var request = new RadioOperationRequest(RadioOperation.GetFrequency, vfo);
            _profile.TryCreateCommand(request, out var command, out _, out _);
            return new(command!, frame => _profile.ParseReply(request, command!.ResponseKey!, frame.Span));
        }
        return session.ExecuteTransactionAsync(new SessionTransactionStep[]
        {
            Read(RadioVfo.A), Read(RadioVfo.B), new(swap, _ => new(ReplyParseStatus.Unrelated), IsMutation: true),
            Read(RadioVfo.A), Read(RadioVfo.B)
        }, timeout, values =>
        {
            var beforeA = (long)values[0]!;
            var beforeB = (long)values[1]!;
            var observed = new VfoFrequencies((long)values[3]!, (long)values[4]!);
            if (beforeA == beforeB)
                return new(RadioOutcome.OutcomeUnknown, observed, "Equal initial frequencies cannot establish that SV took effect; the command was sent once.");
            return observed.AHz == beforeB && observed.BHz == beforeA
                ? new(RadioOutcome.Succeeded, observed)
                : new(RadioOutcome.ProtocolError, observed, "Observed VFO frequencies do not satisfy the swap postcondition.");
        }, cancellationToken);
    }

    public override async Task<bool> SetFrequencyAsync(long frequency)
    {
        if (frequency <= 0) { LastOperationResult = new(RadioOutcome.InvalidArgument, CompletionEvidence.NotSent); return false; }
        return (await ExecuteAsync(new(RadioOperation.SetFrequency, RadioVfo.A, frequency))).Outcome == RadioOutcome.Succeeded;
    }
    public override async Task<bool> SetModeAsync(string mode)
    {
        if (string.IsNullOrWhiteSpace(mode)) { LastOperationResult = new(RadioOutcome.InvalidArgument, CompletionEvidence.NotSent); return false; }
        return (await ExecuteAsync(new(RadioOperation.SetMode, mode: mode))).Outcome == RadioOutcome.Succeeded;
    }
    public override async Task<bool> SwapVfoAsync() =>
        (await ExecuteAsync(new(RadioOperation.SwapVfo))).Outcome == RadioOutcome.Succeeded;

    /// <summary>Legacy raw calls are translated only for the implemented preview operations.</summary>
    public override async Task<RadioOperationResult<object>> ExecuteCommandAsync(RadioCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.TimeoutMs <= 0)
        {
            LastOperationResult = new(RadioOutcome.InvalidArgument, CompletionEvidence.NotSent,
                diagnostic: "A positive legacy command deadline is required.");
            return LastOperationResult;
        }
        var text = command.Command?.ToUpperInvariant() ?? string.Empty;
        RadioOperationRequest? request = text switch
        {
            "ID;" => new(RadioOperation.Identify),
            "FA;" => new(RadioOperation.GetFrequency, RadioVfo.A),
            "FB;" => new(RadioOperation.GetFrequency, RadioVfo.B),
            "MD0;" => new(RadioOperation.GetMode),
            "SV;" => new(RadioOperation.SwapVfo),
            _ => null
        };
        if (request is null && text.Length == 12 && text[0] == 'F' && text[1] is 'A' or 'B' && text[^1] == ';' &&
            text.AsSpan(2, 9).IndexOfAnyExceptInRange('0', '9') < 0 &&
            long.TryParse(text.AsSpan(2, 9), NumberStyles.None, CultureInfo.InvariantCulture, out var hz) && hz > 0)
            request = new(RadioOperation.SetFrequency, text[1] == 'A' ? RadioVfo.A : RadioVfo.B, hz);
        if (request is null && text.Length == 5 && text.StartsWith("MD0", StringComparison.Ordinal) && text[^1] == ';')
        {
            var parsed = _profile.ParseReply(new(RadioOperation.GetMode), "MD0", Encoding.ASCII.GetBytes(text));
            if (parsed.Status == ReplyParseStatus.Valid && parsed.Value is string mode)
                request = new(RadioOperation.SetMode, mode: mode);
        }
        if (request is null)
        {
            LastOperationResult = new(RadioOutcome.NotSupported, CompletionEvidence.NotSent,
                diagnostic: "This command has no implemented FT-991A preview mapping.");
            return LastOperationResult;
        }
        return await ExecuteCoreAsync(request, TimeSpan.FromMilliseconds(Math.Min(command.TimeoutMs, 2000)), cancellationToken);
    }

    public override async Task<string?> SendCommandAsync(RadioCommand command)
    {
        var result = await ExecuteCommandAsync(command);
        if (result.Outcome != RadioOutcome.Succeeded) return null;
        if (!command.ExpectsResponse) return string.Empty;
        var text = command.Command.ToUpperInvariant();
        return result.Observation?.Value switch
        {
            long hz => text[..2] + hz.ToString("D9", CultureInfo.InvariantCulture) + ";",
            string identity when text == "ID;" => "ID0670;",
            string mode => ModeReply(mode),
            _ => string.Empty
        };
    }
    private string ModeReply(string mode)
    {
        _profile.TryCreateCommand(new(RadioOperation.SetMode, mode: mode), out var command, out _, out _);
        return Encoding.ASCII.GetString(command!.Payload.Span);
    }

    public override async Task<RadioStatus> GetStatusAsync()
    {
        var frequency = await ExecuteAsync(new(RadioOperation.GetFrequency, RadioVfo.A));
        var mode = await ExecuteAsync(new(RadioOperation.GetMode));
        if (frequency.Outcome != RadioOutcome.Succeeded || mode.Outcome != RadioOutcome.Succeeded)
            throw new IOException("An observed FT-991A frequency and mode are required for status.");
        var status = new RadioStatus { Frequency = (long)frequency.Observation!.Value, Mode = (string)mode.Observation!.Value,
            CurrentVfo = "Unknown" };
        status.AdditionalInfo["FrequencyObservation"] = frequency.Observation;
        status.AdditionalInfo["ModeObservation"] = mode.Observation;
        status.AdditionalInfo["HardwareVerified"] = false;
        return status;
    }

    public override async Task<string> GetUniversalStatusStringAsync()
    {
        var status = await GetStatusAsync();
        return $"MODEL=Yaesu FT-991A;FREQ={status.Frequency.ToString(CultureInfo.InvariantCulture)};MODE={status.Mode};VFO=Unknown;EVIDENCE=ProtocolTested;HARDWARE_VERIFIED=false";
    }

    private static Task<T> Unsupported<T>() => Task.FromException<T>(new NotSupportedException("This operation has no implemented FT-991A preview mapping."));
    public override Task<string> GetVfoAsync() => Unsupported<string>();
    public override Task<bool> SetVfoAsync(string vfo) => Unsupported<bool>();
    public override Task<bool> SetSplitAsync(bool enabled) => Unsupported<bool>();
    public override Task<bool> GetSplitAsync() => Unsupported<bool>();
    public override Task<bool> SetRitAsync(int offsetHz) => Unsupported<bool>();
    public override Task<int> GetRitAsync() => Unsupported<int>();
    public override Task<bool> SetXitAsync(int offsetHz) => Unsupported<bool>();
    public override Task<int> GetXitAsync() => Unsupported<int>();
    public override Task<bool> SetIfBandwidthAsync(int bandwidth) => Unsupported<bool>();
    public override Task<int> GetIfBandwidthAsync() => Unsupported<int>();
    public override Task<bool> SetPowerOutputAsync(int powerPercent) => Unsupported<bool>();
    public override Task<int> GetPowerOutputAsync() => Unsupported<int>();
    public override Task<int> GetSMeterAsync() => Unsupported<int>();
    public override Task<double> GetSWRAsync() => Unsupported<double>();
    public override Task<bool> SetAntennaAsync(int antenna) => Unsupported<bool>();
    public override Task<int> GetAntennaAsync() => Unsupported<int>();
    public override Task<bool> SetMemoryChannelAsync(int channel, long frequency, string mode) => Unsupported<bool>();
    public override Task<bool> RecallMemoryChannelAsync(int channel) => Unsupported<bool>();
    public override Task<bool> SetCwSpeedAsync(int wpm) => Unsupported<bool>();
    public override Task<int> GetCwSpeedAsync() => Unsupported<int>();
    public override Task<bool> SendCwMessageAsync(string message) => Unsupported<bool>();
    public override Task<bool> SetNoiseReductionAsync(int level) => Unsupported<bool>();
    public override Task<int> GetNoiseReductionAsync() => Unsupported<int>();
    public override Task<bool> SetPowerAsync(bool powerOn) => Unsupported<bool>();
    public override Task<bool> GetPowerAsync() => Unsupported<bool>();
    public override Task<bool> SetMonitorLevelAsync(int level) => Unsupported<bool>();
    public override Task<int> GetMonitorLevelAsync() => Unsupported<int>();
    public override Task<bool> SetMicGainAsync(int gain) => Unsupported<bool>();
    public override Task<int> GetMicGainAsync() => Unsupported<int>();
    public override Task<bool> SetCompLevelAsync(int level) => Unsupported<bool>();
    public override Task<int> GetCompLevelAsync() => Unsupported<int>();
    public override Task<bool> SetVoxLevelAsync(int level) => Unsupported<bool>();
    public override Task<int> GetVoxLevelAsync() => Unsupported<int>();
    public override Task<bool> SetVoxDelayAsync(int delayMs) => Unsupported<bool>();
    public override Task<int> GetVoxDelayAsync() => Unsupported<int>();
    public override Task<bool> SetBreakInAsync(bool enabled) => Unsupported<bool>();
    public override Task<bool> GetBreakInAsync() => Unsupported<bool>();
    public override Task<bool> SetNotchAsync(int frequency) => Unsupported<bool>();
    public override Task<int> GetNotchAsync() => Unsupported<int>();
    public async Task<bool> SetBandAsync(string band)
    {
        // FT-991A covers HF/VHF/UHF so implement band switching
        var frequency = band.ToUpper() switch
        {
            "160M" => 1800000L,
            "80M" => 3500000L,
            "60M" => 5330000L,
            "40M" => 7000000L,
            "30M" => 10100000L,
            "20M" => 14000000L,
            "17M" => 18068000L,
            "15M" => 21000000L,
            "12M" => 24890000L,
            "10M" => 28000000L,
            "6M" => 50000000L,
            "2M" => 144000000L,
            "70CM" => 430000000L,
            _ => 0L
        };

        if (frequency > 0)
        {
            return await SetFrequencyAsync(frequency);
        }
        return false;
    }
}
