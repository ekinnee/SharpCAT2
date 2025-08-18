using Microsoft.AspNetCore.Mvc;
using SharpCAT2.Core.Services;
using SharpCAT2.Core.Configuration;
using SharpCAT2.Common.Radio;
using SharpCAT2.Common.Serial;
using SharpCAT2.Common;

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
                name = r.Value,     // Internal key (e.g., "KENWOOD_TS-2000")
                description = r.Key // Friendly name (e.g., "Kenwood TS-2000")
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available radios");
            return StatusCode(500, new { error = "Failed to get available radios" });
        }
    }

    /// <summary>
    /// Get list of available serial ports
    /// </summary>
    [HttpGet("serial/ports")]
    public IActionResult GetAvailableSerialPorts()
    {
        try
        {
            var fakePorts = new[] { "FAKE", "DUMMY", "TEST", "SIMULATION" };
            var allPorts = new List<string>();
            
            // Always include fake ports first
            allPorts.AddRange(fakePorts);
            
            try
            {
                // Try to get real ports, but don't fail if hardware detection fails
                var realPorts = SerialPortFactory.GetAvailablePortNames();
                allPorts.AddRange(realPorts);
            }
            catch (Exception hardwareEx)
            {
                // Log hardware detection failure but continue with fake ports
                _logger.LogWarning(hardwareEx, "Hardware serial port detection failed, continuing with simulation ports only");
            }
            
            // Remove duplicates and sort
            var uniquePorts = allPorts.Distinct().OrderBy(p => p).ToList();
            
            return Ok(uniquePorts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available serial ports");
            // Even in case of complete failure, return at least the fake ports
            var fallbackPorts = new[] { "FAKE", "DUMMY", "TEST", "SIMULATION" };
            return Ok(fallbackPorts);
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