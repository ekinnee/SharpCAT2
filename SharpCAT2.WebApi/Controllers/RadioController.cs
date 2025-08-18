using Microsoft.AspNetCore.Mvc;
using SharpCAT2.Core.Services;
using SharpCAT2.Core.Configuration;
using SharpCAT2.Common.Radio;
using SharpCAT2.Common.Serial;

namespace SharpCAT2.WebApi.Controllers;

/// <summary>
/// API controller for radio operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RadioController : ControllerBase
{
    private readonly IRadioService _radioService;
    private readonly ILogger<RadioController> _logger;

    public RadioController(IRadioService radioService, ILogger<RadioController> logger)
    {
        _radioService = radioService;
        _logger = logger;
    }

    /// <summary>
    /// Get the current radio status
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        try
        {
            if (_radioService.ConnectedRadio == null)
            {
                return Ok(new { connected = false, message = "No radio connected" });
            }

            var status = await _radioService.GetRadioStatusAsync();
            return Ok(new { 
                connected = true, 
                radio = $"{_radioService.ConnectedRadio.Manufacturer} {_radioService.ConnectedRadio.ModelName}",
                status = status 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting radio status");
            return StatusCode(500, new { error = "Failed to get radio status" });
        }
    }

    /// <summary>
    /// Connect to a radio
    /// </summary>
    [HttpPost("connect")]
    public async Task<IActionResult> Connect([FromBody] ConnectRequest request)
    {
        try
        {
            var options = new CommandLineOptions
            {
                PortName = request.SerialPort,
                RadioModel = request.Radio,
                BaudRate = request.BaudRate ?? 9600
            };

            var serialPort = SerialPortFactory.CreateSerialPort(options.PortName, options.BaudRate);
            await _radioService.InitializeRadioAsync(options, serialPort);

            return Ok(new { message = "Successfully connected to radio" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting to radio");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Disconnect from the current radio
    /// </summary>
    [HttpPost("disconnect")]
    public async Task<IActionResult> Disconnect()
    {
        try
        {
            await _radioService.DisconnectRadioAsync();
            return Ok(new { message = "Successfully disconnected from radio" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting from radio");
            return StatusCode(500, new { error = "Failed to disconnect from radio" });
        }
    }

    /// <summary>
    /// Send a command to the radio
    /// </summary>
    [HttpPost("command")]
    public async Task<IActionResult> SendCommand([FromBody] CommandRequest request)
    {
        try
        {
            if (_radioService.ConnectedRadio == null)
            {
                return BadRequest(new { error = "No radio connected" });
            }

            var response = await _radioService.TryProcessRadioCommandAsync(request.Command);
            return Ok(new { processed = response });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending command to radio");
            return StatusCode(500, new { error = "Failed to send command to radio" });
        }
    }

    /// <summary>
    /// Get list of available radios
    /// </summary>
    [HttpGet("available")]
    public IActionResult GetAvailableRadios()
    {
        try
        {
            var radios = RadioFactory.GetAvailableRadios();
            return Ok(radios.Select(r => new { 
                name = r.Key, 
                description = r.Value
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available radios");
            return StatusCode(500, new { error = "Failed to get available radios" });
        }
    }
}

/// <summary>
/// Request model for connecting to a radio
/// </summary>
public class ConnectRequest
{
    public string SerialPort { get; set; } = string.Empty;
    public string? Radio { get; set; }
    public int? BaudRate { get; set; }
}

/// <summary>
/// Request model for sending commands to radio
/// </summary>
public class CommandRequest
{
    public string Command { get; set; } = string.Empty;
}