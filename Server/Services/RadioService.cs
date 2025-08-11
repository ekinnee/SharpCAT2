using SharpCAT2.Common.Radio;
using SharpCAT2.Common.Serial;
using SharpCAT2.Common.Utils;
using Microsoft.Extensions.Logging;
using System.Text;

namespace SharpCAT2.Server.Services;

/// <summary>
/// Implementation of radio service for managing radio connections and operations
/// </summary>
public class RadioService : IRadioService, IDisposable
{
    private readonly ILogger<RadioService> _logger;
    private IRadio? _connectedRadio;

    public RadioService(ILogger<RadioService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public IRadio? ConnectedRadio => _connectedRadio;

    /// <inheritdoc />
    public bool IsRadioConnected => _connectedRadio?.IsConnected == true;

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
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing radio");
            _connectedRadio?.Dispose();
            _connectedRadio = null;
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
    public async Task<string?> GetRadioStatusAsync()
    {
        if (_connectedRadio == null)
        {
            return "No radio connected.";
        }

        try
        {
            var status = await _connectedRadio.GetStatusAsync();
            var sb = new StringBuilder();
            sb.AppendLine("Radio Status:");
            sb.AppendLine($"  Model: {_connectedRadio.Manufacturer} {_connectedRadio.ModelName}");
            sb.AppendLine($"  Frequency: {status.Frequency:N0} Hz");
            sb.AppendLine($"  Mode: {status.Mode}");
            sb.AppendLine($"  VFO: {status.CurrentVfo}");
            sb.AppendLine($"  Transmitting: {status.IsTransmitting}");
            sb.AppendLine($"  Power: {status.IsPoweredOn}");
            sb.AppendLine($"  Timestamp: {status.Timestamp:HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine("Supported Features:");
            sb.AppendLine($"  Feature Count: {_connectedRadio.SupportedFeatures.GetFeatureCount()}");
            sb.AppendLine($"  Features: {_connectedRadio.SupportedFeatures.GetDescription()}");
            
            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting radio status");
            return $"Error getting radio status: {ex.Message}";
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
            }
            
            // Connect new radio
            if (serialPort?.IsOpen == true)
            {
                bool connected = await newRadio.ConnectAsync(serialPort);
                if (connected)
                {
                    _connectedRadio = newRadio;
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
    public string? GetRadioInfo(string radioName)
    {
        var radio = RadioFactory.CreateRadio(radioName);
        if (radio == null)
        {
            return null;
        }

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Radio Information: {radioName}");
            sb.AppendLine("====================================");
            sb.AppendLine($"Manufacturer: {radio.Manufacturer}");
            sb.AppendLine($"Model: {radio.ModelName}");
            sb.AppendLine($"Feature Count: {radio.SupportedFeatures.GetFeatureCount()}");
            sb.AppendLine();
            sb.AppendLine("Supported Features:");
            sb.AppendLine("==================");
            
            var features = radio.SupportedFeatures;
            
            if (features == SupportedFeatures.FullFeatureSet)
            {
                sb.AppendLine("  All features supported (Full Feature Set)");
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
                    .ToList();

                if (featureNames.Count == 0)
                {
                    sb.AppendLine("  Basic operation only");
                }
                else
                {
                    foreach (var feature in featureNames.OrderBy(f => f.ToString()))
                    {
                        sb.AppendLine($"  ✓ {feature}");
                    }
                }
            }

            return sb.ToString();
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
        }
    }

    public void Dispose()
    {
        _connectedRadio?.Dispose();
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