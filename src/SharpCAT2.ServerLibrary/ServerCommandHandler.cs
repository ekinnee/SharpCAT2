using SharpCAT2.ServerLibrary.Radio;
using SharpCAT2.ServerLibrary.Serial;
using SharpCAT2.Core.Radio;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using System.Text;

namespace SharpCAT2.ServerLibrary;

/// <summary>
/// Implementation of server command handler.
/// Handles radio management commands and user input processing.
/// </summary>
public class ServerCommandHandler : IServerCommandHandler
{
    private readonly IRadioService _radioService;
    private readonly ICommandDisplayService _commandDisplayService;
    private readonly ILogger<ServerCommandHandler> _logger;

    public ServerCommandHandler(
        IRadioService radioService,
        ICommandDisplayService commandDisplayService,
        ILogger<ServerCommandHandler> logger)
    {
        _radioService = radioService;
        _commandDisplayService = commandDisplayService;
        _logger = logger;
    }

    public async Task<bool> HandleRadioManagementCommandAsync(string command, NetworkStream networkStream)
    {
        try
        {
            string[] parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return false;
            
            string baseCommand = parts[0].ToLower();
            
            switch (baseCommand)
            {
                case "list-radios":
                case "get-radios":
                    await SendRadioListResponseAsync(networkStream);
                    return true;
                    
                case "list-serialports":
                case "get-serialports":
                    await SendSerialPortListResponseAsync(networkStream);
                    return true;
                    
                case "set-radio":
                    if (parts.Length >= 2)
                    {
                        string radioName = string.Join(" ", parts.Skip(1));
                        await HandleSetRadioCommandAsync(radioName, networkStream);
                        return true;
                    }
                    else
                    {
                        await SendTcpResponseAsync(networkStream, "ERROR: set-radio command requires radio name");
                        return true;
                    }
                    
                case "current-radio":
                case "cr":
                    await SendCurrentRadioResponseAsync(networkStream);
                    return true;

                case "s":
                case "radio-status":
                case "rs":
                    await SendRadioStatusResponseAsync(networkStream);
                    return true;

                case "radio-info":
                case "ri":
                    if (parts.Length >= 2)
                    {
                        string radioName = string.Join(" ", parts.Skip(1));
                        await SendRadioInfoResponseAsync(radioName, networkStream);
                        return true;
                    }
                    else
                    {
                        await SendTcpResponseAsync(networkStream, "ERROR: radio-info command requires radio name");
                        return true;
                    }
                    
                default:
                    return false; // Not a radio management command
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling radio management command: {Command}", command);
            await SendTcpResponseAsync(networkStream, $"ERROR: {ex.Message}");
            return true;
        }
    }

    public async Task<CommandProcessingResult> ProcessRadioCommandAsync(string input)
    {
        if (!_radioService.IsRadioConnected || _radioService.ConnectedRadio == null)
        {
            return new CommandProcessingResult(false);
        }

        try
        {
            // Check if it's a well-formed radio command (ends with semicolon)
            if (input.EndsWith(";"))
            {
                var command = new RadioCommand(input, "User command");
                var response = await _radioService.ConnectedRadio.SendCommandAsync(command);
                
                if (response != null)
                {
                    _logger.LogDebug("Radio command response: {Response}", response);
                    return new CommandProcessingResult(true, false, response);
                }
            }

            // Try common command shortcuts
            switch (input.ToLower().Trim())
            {
                case "freq":
                case "frequency":
                    var status = await _radioService.ConnectedRadio.GetStatusAsync();
                    var freqMessage = $"Current frequency: {status.Frequency:N0} Hz";
                    return new CommandProcessingResult(true, false, freqMessage);

                case "mode":
                    var modeStatus = await _radioService.ConnectedRadio.GetStatusAsync();
                    var modeMessage = $"Current mode: {modeStatus.Mode}";
                    return new CommandProcessingResult(true, false, modeMessage);

                default:
                    return new CommandProcessingResult(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing radio command: {Input}", input);
            return new CommandProcessingResult(true, false, $"Error: {ex.Message}");
        }
    }

    public bool IsSpecialCommand(string input)
    {
        string normalizedInput = input.ToLower().Trim();
        return normalizedInput == "s" && _radioService.IsRadioConnected;
    }

    #region Private Helper Methods

    private async Task SendRadioListResponseAsync(NetworkStream networkStream)
    {
        try
        {
            var response = new StringBuilder();
            response.AppendLine("RADIO_LIST_START");
            
            var radios = _radioService.GetAvailableRadios();
            foreach (var radio in radios.OrderBy(r => r.Key))
            {
                response.AppendLine($"{radio.Key}|0"); // Simplified for now
            }
            
            response.AppendLine("RADIO_LIST_END");
            
            await SendTcpResponseAsync(networkStream, response.ToString());
        }
        catch (Exception ex)
        {
            await SendTcpResponseAsync(networkStream, $"ERROR: Failed to get radio list - {ex.Message}");
        }
    }

    private async Task SendSerialPortListResponseAsync(NetworkStream networkStream)
    {
        try
        {
            var response = new StringBuilder();
            response.AppendLine("SERIALPORT_LIST_START");
            
            var realPorts = SerialPortFactory.GetAvailablePortNames();
            var fakePorts = new[] { "FAKE" }; // Only FAKE is supported as fake port
            var allPorts = realPorts.Concat(fakePorts).OrderBy(p => p);
            
            foreach (var port in allPorts)
            {
                response.AppendLine($"{port}");
            }
            
            response.AppendLine("SERIALPORT_LIST_END");
            
            await SendTcpResponseAsync(networkStream, response.ToString());
        }
        catch (Exception ex)
        {
            await SendTcpResponseAsync(networkStream, $"ERROR: Failed to get serial port list - {ex.Message}");
        }
    }

    private async Task HandleSetRadioCommandAsync(string radioName, NetworkStream networkStream)
    {
        try
        {
            // Note: This would need access to the serial port, which should be injected or passed
            // For now, return an error indicating this functionality needs to be implemented differently
            await SendTcpResponseAsync(networkStream, $"ERROR: Set radio functionality requires architecture changes");
        }
        catch (Exception ex)
        {
            await SendTcpResponseAsync(networkStream, $"ERROR: Failed to set radio - {ex.Message}");
        }
    }

    private async Task SendCurrentRadioResponseAsync(NetworkStream networkStream)
    {
        try
        {
            if (_radioService.IsRadioConnected && _radioService.ConnectedRadio != null)
            {
                var statusString = await _radioService.ConnectedRadio.GetUniversalStatusStringAsync();
                await SendTcpResponseAsync(networkStream, statusString);
            }
            else
            {
                await SendTcpResponseAsync(networkStream, "ERROR: No radio connected");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current radio status");
            await SendTcpResponseAsync(networkStream, $"ERROR: Failed to get current radio - {ex.Message}");
        }
    }

    private async Task SendRadioStatusResponseAsync(NetworkStream networkStream)
    {
        try
        {
            if (_radioService.IsRadioConnected && _radioService.ConnectedRadio != null)
            {
                var statusString = await _radioService.ConnectedRadio.GetUniversalStatusStringAsync();
                await SendTcpResponseAsync(networkStream, statusString);
            }
            else
            {
                await SendTcpResponseAsync(networkStream, "ERROR: No radio connected");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting radio status");
            await SendTcpResponseAsync(networkStream, $"ERROR: Failed to get radio status - {ex.Message}");
        }
    }

    private async Task SendRadioInfoResponseAsync(string radioName, NetworkStream networkStream)
    {
        try
        {
            var radioInfo = _radioService.GetRadioInfo(radioName);
            if (radioInfo == null)
            {
                await SendTcpResponseAsync(networkStream, $"ERROR: Unknown radio: {radioName}");
                return;
            }

            var response = new StringBuilder();
            response.AppendLine("RADIO_INFO_START");
            response.AppendLine($"RadioName={radioInfo.RadioName}");
            response.AppendLine($"Manufacturer={radioInfo.Manufacturer}");
            response.AppendLine($"ModelName={radioInfo.ModelName}");
            response.AppendLine($"FeatureCount={radioInfo.FeatureCount}");
            response.AppendLine($"IsFullFeatureSet={radioInfo.IsFullFeatureSet}");
            
            // Add supported features as comma-separated list
            if (radioInfo.SupportedFeatures.Count > 0)
            {
                response.AppendLine($"SupportedFeatures={string.Join(",", radioInfo.SupportedFeatures)}");
            }
            else
            {
                response.AppendLine("SupportedFeatures=");
            }
            
            // Add additional properties if any
            foreach (var kvp in radioInfo.AdditionalProperties)
            {
                response.AppendLine($"{kvp.Key}={kvp.Value}");
            }
            
            response.AppendLine("RADIO_INFO_END");
            
            await SendTcpResponseAsync(networkStream, response.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting radio info for {RadioName}", radioName);
            await SendTcpResponseAsync(networkStream, $"ERROR: Failed to get radio info - {ex.Message}");
        }
    }

    private async Task SendTcpResponseAsync(NetworkStream networkStream, string response)
    {
        try
        {
            byte[] responseBytes = Encoding.UTF8.GetBytes(response + "\n");
            await networkStream.WriteAsync(responseBytes);
            await networkStream.FlushAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending TCP response");
        }
    }

    #endregion
}