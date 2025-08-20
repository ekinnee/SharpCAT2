using SharpCAT2.Common.Radio;
using SharpCAT2.Core.Utils;
using SharpCAT2.Core.Radio;
using SharpCAT2.Core.Serial;
using SharpCAT2.Core.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;

namespace SharpCAT2.Common;

/// <summary>
/// Implementation of radio service for managing radio connections and operations
/// </summary>
public class RadioService : IRadioService, IDisposable
{
    private readonly ILogger<RadioService> _logger;
    private IRadio? _connectedRadio;
    private string? _connectedPortName;

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
        try
        {
            if (options.AutoDetectRadio)
            {
                _logger.LogInformation("Auto-detecting radio...");
                var baseRadio = await RadioFactory.AutoDetectRadioAsync(serialPort);
                
                if (baseRadio == null)
                {
                    _logger.LogWarning("No radio detected. Continuing with basic serial communication.");
                    return;
                }
                
                // Wrap with resilient wrapper
                _connectedRadio = new ResilientRadio(baseRadio, _logger);
                await _connectedRadio.ConnectAsync(serialPort);
                _connectedPortName = serialPort.PortName;
            }
            else if (!string.IsNullOrWhiteSpace(options.RadioModel))
            {
                _logger.LogInformation("Connecting to radio: {RadioModel}", options.RadioModel);
                _connectedRadio = RadioFactory.CreateResilientRadio(options.RadioModel, _logger);
                
                if (_connectedRadio == null)
                {
                    _logger.LogWarning("Unknown radio model: {RadioModel}", options.RadioModel);
                    return;
                }
                
                // Connect the radio to the serial port
                bool connected = await _connectedRadio.ConnectAsync(serialPort);
                if (!connected)
                {
                    _logger.LogWarning("Failed to connect to radio. Continuing with basic serial communication.");
                    _connectedRadio.Dispose();
                    _connectedRadio = null;
                    _connectedPortName = null;
                }
                else
                {
                    _connectedPortName = serialPort.PortName;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing radio");
            _connectedRadio?.Dispose();
            _connectedRadio = null;
            _connectedPortName = null;
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
            return false;
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
            return new RadioStatusInfo
            {
                Manufacturer = _connectedRadio.Manufacturer,
                ModelName = _connectedRadio.ModelName,
                Frequency = status.Frequency,
                Mode = status.Mode,
                CurrentVfo = status.CurrentVfo,
                IsTransmitting = status.IsTransmitting,
                IsPoweredOn = status.IsPoweredOn,
                Timestamp = status.Timestamp,
                FeatureCount = _connectedRadio.SupportedFeatures.GetFeatureCount(),
                FeaturesDescription = _connectedRadio.SupportedFeatures.GetDescription(),
                IsConnected = _connectedRadio.IsConnected
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting radio status");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ChangeRadioAsync(string radioName, ISerialPort serialPort)
    {
        try
        {
            // Check if radio exists
            var newRadio = RadioFactory.CreateResilientRadio(radioName, _logger);
            if (newRadio == null)
            {
                _logger.LogWarning("Unknown radio model: {RadioName}", radioName);
                return false;
            }
            
            // Disconnect current radio if any
            if (_connectedRadio != null)
            {
                _logger.LogInformation("Disconnecting current radio: {Manufacturer} {ModelName}", 
                    _connectedRadio.Manufacturer, _connectedRadio.ModelName);
                _connectedRadio.Disconnect();
                _connectedRadio.Dispose();
                _connectedRadio = null;
                _connectedPortName = null;
            }
            
            // Connect new radio
            if (serialPort?.IsOpen == true)
            {
                bool connected = await newRadio.ConnectAsync(serialPort);
                if (connected)
                {
                    _connectedRadio = newRadio;
                    _connectedPortName = serialPort.PortName;
                    _logger.LogInformation("Successfully changed radio to: {Manufacturer} {ModelName}", 
                        _connectedRadio.Manufacturer, _connectedRadio.ModelName);
                    return true;
                }
                else
                {
                    newRadio.Dispose();
                    _logger.LogWarning("Failed to connect to radio: {RadioName}", radioName);
                    return false;
                }
            }
            else
            {
                newRadio.Dispose();
                _logger.LogWarning("No serial port available for radio connection");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to change radio to {RadioName}", radioName);
            return false;
        }
    }

    /// <inheritdoc />
    public Dictionary<string, string> GetAvailableRadios()
    {
        return RadioFactory.GetAvailableRadios();
    }

    /// <inheritdoc />
    public RadioModelInfo? GetRadioInfo(string radioName)
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

            return new RadioModelInfo
            {
                RadioName = radioName,
                Manufacturer = radio.Manufacturer,
                ModelName = radio.ModelName,
                FeatureCount = radio.SupportedFeatures.GetFeatureCount(),
                SupportedFeatures = supportedFeatureNames,
                IsFullFeatureSet = features == SupportedFeatures.FullFeatureSet
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
        _connectedRadio?.Dispose();
        _connectedRadio = null;
        _connectedPortName = null;
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