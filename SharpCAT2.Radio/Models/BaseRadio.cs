using SharpCAT2.Radio.Serial;
using System.Text;
using System.Text.RegularExpressions;

namespace SharpCAT2.Radio.Models;

/// <summary>
/// Base implementation of the IRadio interface providing common functionality
/// </summary>
public abstract class BaseRadio : IRadio
{
    protected ISerialPort? _serialPort;
    protected bool _disposed = false;

    /// <summary>
    /// Gets the radio model name
    /// </summary>
    public abstract string ModelName { get; }

    /// <summary>
    /// Gets the radio manufacturer
    /// </summary>
    public abstract string Manufacturer { get; }

    /// <summary>
    /// Gets the features supported by this radio (default implementation)
    /// </summary>
    public virtual SupportedFeatures SupportedFeatures => SupportedFeatures.BasicOperation;

    /// <summary>
    /// Gets whether the radio is currently connected
    /// </summary>
    public bool IsConnected => _serialPort?.IsOpen == true;

    /// <summary>
    /// Gets or sets the frequency in Hz
    /// </summary>
    public virtual long Frequency { get; set; }

    /// <summary>
    /// Gets or sets the operating mode
    /// </summary>
    public virtual string Mode { get; set; } = "USB";

    /// <summary>
    /// Connects to the radio using the specified serial port
    /// </summary>
    /// <param name="port">Serial port to use for communication</param>
    /// <returns>True if connection successful, false otherwise</returns>
    public virtual async Task<bool> ConnectAsync(ISerialPort port)
    {
        try
        {
            _serialPort = port ?? throw new ArgumentNullException(nameof(port));
            
            if (!_serialPort.IsOpen)
            {
                _serialPort.Open();
            }

            // Test connection by sending a simple command
            var testCommand = new RadioCommand("ID;", "Test connection", true, 2000);
            var response = await SendCommandAsync(testCommand);
            
            return !string.IsNullOrEmpty(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error connecting to radio: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Disconnects from the radio
    /// </summary>
    public virtual void Disconnect()
    {
        try
        {
            _serialPort?.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error disconnecting from radio: {ex.Message}");
        }
    }

    /// <summary>
    /// Sends a command to the radio and returns the response
    /// </summary>
    /// <param name="command">Command to send</param>
    /// <returns>Response from the radio</returns>
    public virtual async Task<string?> SendCommandAsync(RadioCommand command)
    {
        if (!IsConnected || _serialPort == null)
        {
            return null;
        }

        try
        {
            // Clear any existing data in the buffer
            _serialPort.DiscardInBuffer();
            _serialPort.DiscardOutBuffer();

            // Send the command
            await Task.Run(() => _serialPort.Write(command.Command));

            if (!command.ExpectsResponse)
            {
                return string.Empty;
            }

            // Wait for response
            using var cancellationTokenSource = new CancellationTokenSource(command.TimeoutMs);
            return await ReadResponseAsync(cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending command '{command.Command}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Reads a response from the radio
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Response string</returns>
    protected virtual async Task<string> ReadResponseAsync(CancellationToken cancellationToken)
    {
        if (_serialPort == null)
            return string.Empty;

        var response = new StringBuilder();
        var buffer = new byte[1024];

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_serialPort.BytesToRead > 0)
                {
                    int bytesRead = await Task.Run(() => _serialPort.Read(buffer, 0, Math.Min(buffer.Length, _serialPort.BytesToRead)), cancellationToken);
                    if (bytesRead > 0)
                    {
                        string data = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                        response.Append(data);

                        // Check if we have a complete response (ends with semicolon for most radios)
                        if (IsCompleteResponse(response.ToString()))
                        {
                            break;
                        }
                    }
                }
                else
                {
                    await Task.Delay(10, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Timeout occurred
        }

        return response.ToString().Trim();
    }

    /// <summary>
    /// Determines if a response is complete (can be overridden by derived classes)
    /// </summary>
    /// <param name="response">Response received so far</param>
    /// <returns>True if response is complete</returns>
    protected virtual bool IsCompleteResponse(string response)
    {
        return response.EndsWith(";") || response.Length > 0;
    }

    /// <summary>
    /// Gets the current radio status/information
    /// </summary>
    /// <returns>Radio status information</returns>
    public virtual async Task<RadioStatus> GetStatusAsync()
    {
        var status = new RadioStatus();

        try
        {
            // Get frequency
            var freqResponse = await SendCommandAsync(RadioCommand.Common.GetFrequency);
            if (!string.IsNullOrEmpty(freqResponse))
            {
                status.Frequency = ParseFrequency(freqResponse);
                Frequency = status.Frequency;
            }

            // Get mode
            var modeResponse = await SendCommandAsync(RadioCommand.Common.GetMode);
            if (!string.IsNullOrEmpty(modeResponse))
            {
                status.Mode = ParseMode(modeResponse);
                Mode = status.Mode;
            }

            // Get transceiver info
            var txResponse = await SendCommandAsync(RadioCommand.Common.GetTransmitStatus);
            if (!string.IsNullOrEmpty(txResponse))
            {
                ParseTransceiverInfo(txResponse, status);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting radio status: {ex.Message}");
        }

        return status;
    }

    /// <summary>
    /// Sets the radio frequency
    /// </summary>
    /// <param name="frequency">Frequency in Hz</param>
    /// <returns>True if successful</returns>
    public virtual async Task<bool> SetFrequencyAsync(long frequency)
    {
        try
        {
            var command = RadioCommand.Common.SetFrequency(frequency);
            var response = await SendCommandAsync(command);
            
            if (!string.IsNullOrEmpty(response))
            {
                Frequency = frequency;
                return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error setting frequency: {ex.Message}");
        }

        return false;
    }

    /// <summary>
    /// Sets the radio operating mode
    /// </summary>
    /// <param name="mode">Operating mode</param>
    /// <returns>True if successful</returns>
    public virtual async Task<bool> SetModeAsync(string mode)
    {
        try
        {
            var command = RadioCommand.Common.SetMode(mode);
            var response = await SendCommandAsync(command);
            
            if (!string.IsNullOrEmpty(response))
            {
                Mode = mode;
                return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error setting mode: {ex.Message}");
        }

        return false;
    }

    /// <summary>
    /// Parses frequency from response (can be overridden by derived classes)
    /// </summary>
    /// <param name="response">Response string</param>
    /// <returns>Frequency in Hz</returns>
    protected virtual long ParseFrequency(string response)
    {
        // Default implementation for Kenwood/Elecraft style: FA00014074000;
        var match = Regex.Match(response, @"FA(\d{11})");
        if (match.Success && long.TryParse(match.Groups[1].Value, out long freq))
        {
            return freq;
        }
        return 0;
    }

    /// <summary>
    /// Parses mode from response (can be overridden by derived classes)
    /// </summary>
    /// <param name="response">Response string</param>
    /// <returns>Mode string</returns>
    protected virtual string ParseMode(string response)
    {
        // Default implementation for Kenwood style: MD2; (2=USB)
        var match = Regex.Match(response, @"MD(\d+)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int modeNum))
        {
            return MapModeNumber(modeNum);
        }
        return "USB";
    }

    /// <summary>
    /// Maps mode number to mode string (can be overridden by derived classes)
    /// </summary>
    /// <param name="modeNumber">Mode number</param>
    /// <returns>Mode string</returns>
    protected virtual string MapModeNumber(int modeNumber)
    {
        return modeNumber switch
        {
            1 => "LSB",
            2 => "USB",
            3 => "CW",
            4 => "FM",
            5 => "AM",
            6 => "FSK",
            7 => "CW-R",
            8 => "FSK-R",
            _ => "USB"
        };
    }

    /// <summary>
    /// Parses transceiver information (can be overridden by derived classes)
    /// </summary>
    /// <param name="response">Response string</param>
    /// <param name="status">Status object to update</param>
    protected virtual void ParseTransceiverInfo(string response, RadioStatus status)
    {
        // Default implementation for IF command response parsing
        // This is a simplified parser - actual implementation depends on radio protocol
    }

    // Default implementations for extended radio features
    // These should be overridden by derived classes that support these features

    /// <summary>
    /// Gets the current VFO (A or B)
    /// </summary>
    public virtual Task<string> GetVfoAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.DualVFO))
            throw new NotSupportedException("Dual VFO not supported by this radio");
        
        // Default implementation - should be overridden
        return Task.FromResult("A");
    }

    /// <summary>
    /// Sets the active VFO
    /// </summary>
    public virtual Task<bool> SetVfoAsync(string vfo)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.DualVFO))
            return Task.FromResult(false);
        
        // Default implementation - should be overridden
        return Task.FromResult(false);
    }

    /// <summary>
    /// Swaps VFO A and VFO B frequencies
    /// </summary>
    public virtual Task<bool> SwapVfoAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.VFOSwap))
            return Task.FromResult(false);
        
        // Default implementation - should be overridden
        return Task.FromResult(false);
    }

    /// <summary>
    /// Enables or disables split operation
    /// </summary>
    public virtual Task<bool> SetSplitAsync(bool enabled)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.SplitOperation))
            return Task.FromResult(false);
        
        // Default implementation - should be overridden
        return Task.FromResult(false);
    }

    /// <summary>
    /// Gets the current split operation status
    /// </summary>
    public virtual Task<bool> GetSplitAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.SplitOperation))
            return Task.FromResult(false);
        
        // Default implementation - should be overridden
        return Task.FromResult(false);
    }

    /// <summary>
    /// Sets the RIT (Receiver Incremental Tuning) offset
    /// </summary>
    public virtual Task<bool> SetRitAsync(int offsetHz)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.RIT))
            return Task.FromResult(false);
        
        // Default implementation - should be overridden
        return Task.FromResult(false);
    }

    /// <summary>
    /// Gets the current RIT offset
    /// </summary>
    public virtual Task<int> GetRitAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.RIT))
            return Task.FromResult(0);
        
        // Default implementation - should be overridden
        return Task.FromResult(0);
    }

    /// <summary>
    /// Sets the XIT (Transmitter Incremental Tuning) offset
    /// </summary>
    public virtual Task<bool> SetXitAsync(int offsetHz)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.XIT))
            return Task.FromResult(false);
        
        // Default implementation - should be overridden
        return Task.FromResult(false);
    }

    /// <summary>
    /// Gets the current XIT offset
    /// </summary>
    public virtual Task<int> GetXitAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.XIT))
            return Task.FromResult(0);
        
        // Default implementation - should be overridden
        return Task.FromResult(0);
    }

    /// <summary>
    /// Sets the IF bandwidth
    /// </summary>
    public virtual Task<bool> SetIfBandwidthAsync(int bandwidth)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.IFBandwidth))
            return Task.FromResult(false);
        
        // Default implementation - should be overridden
        return Task.FromResult(false);
    }

    /// <summary>
    /// Gets the current IF bandwidth
    /// </summary>
    public virtual Task<int> GetIfBandwidthAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.IFBandwidth))
            return Task.FromResult(0);
        
        // Default implementation - should be overridden
        return Task.FromResult(0);
    }

    /// <summary>
    /// Sets the power output level
    /// </summary>
    public virtual Task<bool> SetPowerOutputAsync(int powerPercent)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.PowerOutput))
            return Task.FromResult(false);
        
        // Default implementation - should be overridden
        return Task.FromResult(false);
    }

    /// <summary>
    /// Gets the current power output level
    /// </summary>
    public virtual Task<int> GetPowerOutputAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.PowerOutput))
            return Task.FromResult(0);
        
        // Default implementation - should be overridden
        return Task.FromResult(0);
    }

    /// <summary>
    /// Gets the S-meter reading
    /// </summary>
    public virtual Task<int> GetSMeterAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.SMeter))
            return Task.FromResult(0);
        
        // Default implementation - should be overridden
        return Task.FromResult(0);
    }

    /// <summary>
    /// Gets the SWR reading
    /// </summary>
    public virtual Task<double> GetSWRAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.SWRMeter))
            return Task.FromResult(1.0);
        
        // Default implementation - should be overridden
        return Task.FromResult(1.0);
    }

    /// <summary>
    /// Sets the active antenna
    /// </summary>
    public virtual Task<bool> SetAntennaAsync(int antenna)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.AntennaSelection))
            return Task.FromResult(false);
        
        // Default implementation - should be overridden
        return Task.FromResult(false);
    }

    /// <summary>
    /// Gets the current active antenna
    /// </summary>
    public virtual Task<int> GetAntennaAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.AntennaSelection))
            return Task.FromResult(1);
        
        // Default implementation - should be overridden
        return Task.FromResult(1);
    }

    /// <summary>
    /// Sets a memory channel
    /// </summary>
    public virtual Task<bool> SetMemoryChannelAsync(int channel, long frequency, string mode)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.MemoryChannels))
            return Task.FromResult(false);
        
        // Default implementation - should be overridden
        return Task.FromResult(false);
    }

    /// <summary>
    /// Recalls a memory channel
    /// </summary>
    public virtual Task<bool> RecallMemoryChannelAsync(int channel)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.MemoryChannels))
            return Task.FromResult(false);
        
        // Default implementation - should be overridden
        return Task.FromResult(false);
    }

    /// <summary>
    /// Sets the CW keyer speed
    /// </summary>
    public virtual Task<bool> SetCwSpeedAsync(int wpm)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.CWKeyer))
            return Task.FromResult(false);
        
        // Default implementation - should be overridden
        return Task.FromResult(false);
    }

    /// <summary>
    /// Gets the current CW keyer speed
    /// </summary>
    public virtual Task<int> GetCwSpeedAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.CWKeyer))
            return Task.FromResult(0);
        
        // Default implementation - should be overridden
        return Task.FromResult(0);
    }

    /// <summary>
    /// Sends a CW message
    /// </summary>
    public virtual Task<bool> SendCwMessageAsync(string message)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.CWMessage))
            return Task.FromResult(false);
        
        // Default implementation - should be overridden
        return Task.FromResult(false);
    }

    /// <summary>
    /// Sets the noise reduction level
    /// </summary>
    public virtual Task<bool> SetNoiseReductionAsync(int level)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.NoiseReduction))
            return Task.FromResult(false);
        
        // Default implementation - should be overridden
        return Task.FromResult(false);
    }

    /// <summary>
    /// Gets the current noise reduction level
    /// </summary>
    public virtual Task<int> GetNoiseReductionAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.NoiseReduction))
            return Task.FromResult(0);
        
        // Default implementation - should be overridden
        return Task.FromResult(0);
    }

    /// <summary>
    /// Powers the radio on or off
    /// </summary>
    public virtual Task<bool> SetPowerAsync(bool powerOn)
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.PowerOnOff))
            return Task.FromResult(false);
        
        // Default implementation - should be overridden
        return Task.FromResult(false);
    }

    /// <summary>
    /// Gets whether the radio is powered on
    /// </summary>
    public virtual Task<bool> GetPowerAsync()
    {
        if (!SupportedFeatures.HasFeature(SupportedFeatures.PowerOnOff))
            return Task.FromResult(true); // Assume powered on if we can't check
        
        // Default implementation - should be overridden
        return Task.FromResult(true);
    }

    /// <summary>
    /// Disposes the radio instance
    /// </summary>
    public virtual void Dispose()
    {
        if (!_disposed)
        {
            Disconnect();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}