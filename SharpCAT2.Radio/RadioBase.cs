using System.IO.Ports;

namespace SharpCAT2.Radio;

/// <summary>
/// Base class providing common radio functionality
/// </summary>
public abstract class RadioBase : IRadio
{
    protected SerialPort? _serialPort;
    protected readonly object _lockObject = new();
    
    /// <summary>
    /// Gets the radio manufacturer name
    /// </summary>
    public abstract string Manufacturer { get; }
    
    /// <summary>
    /// Gets the radio model name
    /// </summary>
    public abstract string Model { get; }
    
    /// <summary>
    /// Gets the protocol name used by this radio
    /// </summary>
    public abstract string Protocol { get; }
    
    /// <summary>
    /// Gets or sets the serial port used for communication
    /// </summary>
    public SerialPort? SerialPort
    {
        get => _serialPort;
        set => _serialPort = value;
    }
    
    /// <summary>
    /// Gets a value indicating whether the radio is connected
    /// </summary>
    public virtual bool IsConnected => _serialPort?.IsOpen == true;
    
    /// <summary>
    /// Opens connection to the radio
    /// </summary>
    /// <param name="portName">Serial port name</param>
    /// <param name="baudRate">Baud rate for communication</param>
    /// <returns>True if connection successful</returns>
    public virtual async Task<bool> ConnectAsync(string portName, int baudRate)
    {
        try
        {
            await DisconnectAsync();
            
            _serialPort = new SerialPort(portName, baudRate)
            {
                Parity = GetParity(),
                DataBits = GetDataBits(),
                StopBits = GetStopBits(),
                Handshake = GetHandshake(),
                ReadTimeout = GetReadTimeout(),
                WriteTimeout = GetWriteTimeout()
            };
            
            _serialPort.Open();
            
            // Perform radio-specific initialization
            await InitializeAsync();
            
            return IsConnected;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to connect to {Manufacturer} {Model}: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Closes connection to the radio
    /// </summary>
    public virtual async Task DisconnectAsync()
    {
        if (_serialPort?.IsOpen == true)
        {
            try
            {
                _serialPort.Close();
            }
            catch (Exception)
            {
                // Ignore errors when closing
            }
        }
        
        _serialPort?.Dispose();
        _serialPort = null;
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Sends a command to the radio
    /// </summary>
    /// <param name="command">Command to send</param>
    /// <returns>Response from radio</returns>
    public virtual async Task<RadioResponse> SendCommandAsync(RadioCommand command)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Radio not connected");
        
        lock (_lockObject)
        {
            try
            {
                // Clear any pending data
                _serialPort!.DiscardInBuffer();
                _serialPort.DiscardOutBuffer();
                
                // Send command
                _serialPort.Write(command.Data, 0, command.Data.Length);
                
                if (!command.ExpectsResponse)
                {
                    return new RadioResponse { Success = true };
                }
                
                // Wait for response
                var response = new List<byte>();
                var startTime = DateTime.Now;
                
                while (DateTime.Now - startTime < command.Timeout)
                {
                    if (_serialPort.BytesToRead > 0)
                    {
                        var buffer = new byte[_serialPort.BytesToRead];
                        _serialPort.Read(buffer, 0, buffer.Length);
                        response.AddRange(buffer);
                        
                        // Check if we have a complete response
                        if (IsCompleteResponse(response.ToArray(), command))
                        {
                            break;
                        }
                    }
                    
                    Thread.Sleep(10);
                }
                
                return new RadioResponse
                {
                    Success = response.Count > 0,
                    Data = response.ToArray(),
                    ErrorMessage = response.Count == 0 ? "No response received" : null
                };
            }
            catch (Exception ex)
            {
                return new RadioResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }
    }
    
    /// <summary>
    /// Gets the current frequency
    /// </summary>
    /// <returns>Current frequency in Hz</returns>
    public abstract Task<long> GetFrequencyAsync();
    
    /// <summary>
    /// Sets the frequency
    /// </summary>
    /// <param name="frequency">Frequency in Hz</param>
    public abstract Task SetFrequencyAsync(long frequency);
    
    /// <summary>
    /// Gets the current operating mode
    /// </summary>
    /// <returns>Current operating mode</returns>
    public abstract Task<RadioMode> GetModeAsync();
    
    /// <summary>
    /// Sets the operating mode
    /// </summary>
    /// <param name="mode">Operating mode</param>
    public abstract Task SetModeAsync(RadioMode mode);
    
    /// <summary>
    /// Performs radio-specific initialization after connection
    /// </summary>
    protected virtual async Task InitializeAsync()
    {
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Checks if the response is complete for the given command
    /// </summary>
    /// <param name="response">Current response data</param>
    /// <param name="command">Original command</param>
    /// <returns>True if response is complete</returns>
    protected virtual bool IsCompleteResponse(byte[] response, RadioCommand command)
    {
        return response.Length > 0;
    }
    
    /// <summary>
    /// Gets the parity setting for this radio
    /// </summary>
    protected virtual Parity GetParity() => Parity.None;
    
    /// <summary>
    /// Gets the data bits setting for this radio
    /// </summary>
    protected virtual int GetDataBits() => 8;
    
    /// <summary>
    /// Gets the stop bits setting for this radio
    /// </summary>
    protected virtual StopBits GetStopBits() => StopBits.One;
    
    /// <summary>
    /// Gets the handshake setting for this radio
    /// </summary>
    protected virtual Handshake GetHandshake() => Handshake.None;
    
    /// <summary>
    /// Gets the read timeout for this radio
    /// </summary>
    protected virtual int GetReadTimeout() => 1000;
    
    /// <summary>
    /// Gets the write timeout for this radio
    /// </summary>
    protected virtual int GetWriteTimeout() => 1000;
    
    /// <summary>
    /// Disposes the radio
    /// </summary>
    public virtual void Dispose()
    {
        DisconnectAsync().Wait();
        GC.SuppressFinalize(this);
    }
}