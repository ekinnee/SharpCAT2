using SharpCAT2.ServerLibrary.Radio;
using SharpCAT2.Core.Utils;
using SharpCAT2.Core.Radio;
using SharpCAT2.Core.Serial;
using SharpCAT2.Core.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;

namespace SharpCAT2.ServerLibrary;

/// <summary>
/// Implementation of radio service for managing radio connections and operations
/// </summary>
public class RadioService : IRadioService, IDisposable
{
    private readonly ILogger<RadioService> _logger;
    private IRadio? _connectedRadio;
    private string? _connectedPortName;
    private readonly SemaphoreSlim _lifetime = new(1, 1);
    private bool _disposed;

    public RadioService(ILogger<RadioService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public IRadio? ConnectedRadio => _connectedRadio;

    /// <inheritdoc />
    public bool IsRadioConnected => _connectedRadio?.IsConnected == true;

    /// <inheritdoc />
    public string? ConnectedPortName => _connectedPortName;

    /// <inheritdoc />
    public async Task InitializeRadioAsync(CommandLineOptions options, ISerialPort serialPort)
    {
        await _lifetime.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            await InitializeCoreAsync(options, serialPort);
        }
        finally { _lifetime.Release(); }
    }

    private async Task InitializeCoreAsync(CommandLineOptions options, ISerialPort serialPort)
    {
        if (_connectedRadio is not null)
            throw new InvalidOperationException("A session already owns the radio; disconnect before initializing.");
        try
        {
            if (options.AutoDetectRadio)
                throw new NotSupportedException("Select a radio explicitly; auto-detection is unavailable during session migration.");
            if (string.IsNullOrWhiteSpace(options.RadioModel))
                throw new InvalidOperationException("A radio model is required; raw serial fallback is no longer supported.");
            _connectedRadio = RadioFactory.CreateResilientRadio(options.RadioModel, _logger)
                ?? throw new ArgumentException("Unknown radio model.", nameof(options));
            if (!await _connectedRadio.ConnectAsync(serialPort))
                throw new IOException("Radio synchronization failed; no raw serial fallback is allowed.");
            _connectedPortName = serialPort.PortName;
        }
        catch
        {
            var accepted = (_connectedRadio as ResilientRadio)?.InnerRadio is Radio.Models.BaseRadio owner && owner.HasAcceptedTransport;
            _connectedRadio?.Dispose();
            _connectedRadio = null;
            _connectedPortName = null;
            if (!accepted) serialPort.Dispose();
            throw;
        }

        // Set up resilient radio event handlers if available
        if (_connectedRadio is ResilientRadio resilientRadio)
        {
            resilientRadio.ConnectionLost += OnRadioConnectionLost;
            resilientRadio.ConnectionRestored += OnRadioConnectionRestored;
        }
    }

    /// <inheritdoc />
    public async Task<bool> TryProcessRadioCommandAsync(string input)
    {
        if (_connectedRadio == null)
            return false;

        try
        {
            // Check if it's a well-formed radio command (ends with semicolon)
            if (input.EndsWith(";"))
            {
                var command = new RadioCommand(input, "User command");
                var response = await _connectedRadio.SendCommandAsync(command);
                
                if (response != null)
                {
                    _logger.LogDebug("Radio command response: {Response}", response);
                    return true;
                }
                _logger.LogWarning("CAT command failed; it will not be resent: {Input}", input);
                return true; // Recognized but failed, not an invitation to raw fallback/replay.
            }

            // Try common command shortcuts
            switch (input.ToLower().Trim())
            {
                case "freq":
                case "frequency":
                    var status = await _connectedRadio.GetStatusAsync();
                    _logger.LogInformation("Current frequency: {Frequency:N0} Hz", status.Frequency);
                    return true;

                case "mode":
                    var modeStatus = await _connectedRadio.GetStatusAsync();
                    _logger.LogInformation("Current mode: {Mode}", modeStatus.Mode);
                    return true;

                default:
                    return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing radio command: {Input}", input);
            return input.EndsWith(';'); // Recognized failures remain terminal; never replay via fallback.
        }
    }

    /// <inheritdoc />
    public async Task<RadioStatusInfo?> GetRadioStatusAsync()
    {
        if (_connectedRadio == null)
        {
            return null;
        }

        try
        {
            var status = await _connectedRadio.GetStatusAsync();
            return RadioStatusInfo.FromRadioStatus(_connectedRadio, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting radio status");
            return null;
        }
    }

    /// <inheritdoc />
    public Task<bool> ChangeRadioAsync(string radioName, ISerialPort serialPort)
    {
        _logger.LogWarning("Live model switching is unavailable. Restart with the selected profile and a new owned transport.");
        return Task.FromResult(false); // Do not dispose the active owner or reuse its transferred port.
    }

    /// <inheritdoc />
    public Dictionary<string, string> GetAvailableRadios()
    {
        return RadioFactory.GetAvailableRadios();
    }

    /// <inheritdoc />
    public Core.Radio.RadioModelInfo? GetRadioInfo(string radioName)
    {
        var radio = RadioFactory.CreateRadio(radioName);
        if (radio == null)
        {
            return null;
        }

        try
        {
            var features = radio.SupportedFeatures;
            var supportedFeatureNames = new List<string>();
            
            if (features == SupportedFeatures.FullFeatureSet)
            {
                // For full feature set, we can list all available features
                supportedFeatureNames.Add("All features supported (Full Feature Set)");
            }
            else
            {
                var featureNames = Enum.GetValues<SupportedFeatures>()
                    .Where(f => f != SupportedFeatures.None && 
                               f != SupportedFeatures.FullFeatureSet && 
                               f != SupportedFeatures.BasicOperation &&
                               f != SupportedFeatures.HFOperation &&
                               f != SupportedFeatures.VHFUHFOperation &&
                               f != SupportedFeatures.AdvancedOperation &&
                               features.HasFeature(f))
                    .Select(f => f.ToString())
                    .ToList();

                supportedFeatureNames.AddRange(featureNames);
            }

            return new Core.Radio.RadioModelInfo
            {
                RadioName = radioName,
                Manufacturer = radio.Manufacturer,
                ModelName = radio.ModelName,
                FeatureCount = radio.SupportedFeatures.GetFeatureCount(),
                SupportedFeatures = supportedFeatureNames,
                IsFullFeatureSet = features == SupportedFeatures.FullFeatureSet,
                Capabilities = radio is Core.Radio.Contracts.IRadioOperations operations ? operations.Capabilities
                    : Enum.GetValues<Core.Radio.Contracts.RadioOperation>().Select(operation => new Core.Radio.Contracts.RadioCapability(
                        operation, radio is Radio.Models.BaseRadio owner && owner.HasTransportMapping
                            ? Core.Radio.Contracts.CapabilityEvidence.Experimental : Core.Radio.Contracts.CapabilityEvidence.Unavailable, "No manufacturer-backed preview proof for this model.")).ToArray()
            };
        }
        finally
        {
            radio.Dispose();
        }
    }

    /// <inheritdoc />
    public async Task DisconnectRadioAsync()
    {
        await _lifetime.WaitAsync();
        try { await DisconnectCoreAsync(); }
        finally { _lifetime.Release(); }
    }

    private async Task DisconnectCoreAsync()
    {
        if (_connectedRadio != null)
        {
            _logger.LogInformation("Disconnecting radio: {Manufacturer} {ModelName}", 
                _connectedRadio.Manufacturer, _connectedRadio.ModelName);
            
            await Task.Run(() => _connectedRadio.Disconnect());
            _connectedRadio.Dispose();
            _connectedRadio = null;
            _connectedPortName = null;
        }
    }

    public void Dispose()
    {
        _lifetime.Wait();
        try
        {
            if (_disposed) return;
            _disposed = true;
            _connectedRadio?.Dispose();
            _connectedRadio = null;
            _connectedPortName = null;
        }
        finally { _lifetime.Release(); }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Handles radio connection lost events
    /// </summary>
    private void OnRadioConnectionLost(object? sender, RadioConnectionLostEventArgs e)
    {
        _logger.LogWarning("Radio connection lost: {Manufacturer} {ModelName} - {Reason}", 
            e.Manufacturer, e.ModelName, e.Reason);
    }

    /// <summary>
    /// Handles radio connection restored events
    /// </summary>
    private void OnRadioConnectionRestored(object? sender, RadioConnectionRestoredEventArgs e)
    {
        _logger.LogInformation("Radio connection restored: {Manufacturer} {ModelName}", 
            e.Manufacturer, e.ModelName);
    }
}