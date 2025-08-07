namespace SharpCAT2.Radio;

/// <summary>
/// Represents a command to be sent to a radio
/// </summary>
public class RadioCommand
{
    /// <summary>
    /// Gets or sets the command type
    /// </summary>
    public RadioCommandType Type { get; set; }
    
    /// <summary>
    /// Gets or sets the raw command bytes
    /// </summary>
    public byte[] Data { get; set; } = Array.Empty<byte>();
    
    /// <summary>
    /// Gets or sets the command description
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets whether this command expects a response
    /// </summary>
    public bool ExpectsResponse { get; set; } = true;
    
    /// <summary>
    /// Gets or sets the timeout for waiting for response
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMilliseconds(1000);
    
    /// <summary>
    /// Creates a new radio command
    /// </summary>
    /// <param name="type">Command type</param>
    /// <param name="data">Command data</param>
    /// <param name="description">Command description</param>
    public RadioCommand(RadioCommandType type, byte[] data, string description = "")
    {
        Type = type;
        Data = data;
        Description = description;
    }
    
    /// <summary>
    /// Creates a new radio command from string
    /// </summary>
    /// <param name="type">Command type</param>
    /// <param name="command">Command string</param>
    /// <param name="description">Command description</param>
    public RadioCommand(RadioCommandType type, string command, string description = "")
    {
        Type = type;
        Data = System.Text.Encoding.ASCII.GetBytes(command);
        Description = description;
    }
}

/// <summary>
/// Types of radio commands
/// </summary>
public enum RadioCommandType
{
    /// <summary>
    /// Get current frequency
    /// </summary>
    GetFrequency,
    
    /// <summary>
    /// Set frequency
    /// </summary>
    SetFrequency,
    
    /// <summary>
    /// Get current mode
    /// </summary>
    GetMode,
    
    /// <summary>
    /// Set mode
    /// </summary>
    SetMode,
    
    /// <summary>
    /// Get radio information
    /// </summary>
    GetInfo,
    
    /// <summary>
    /// Custom command
    /// </summary>
    Custom
}

/// <summary>
/// Represents a response from a radio
/// </summary>
public class RadioResponse
{
    /// <summary>
    /// Gets or sets whether the command was successful
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Gets or sets the raw response data
    /// </summary>
    public byte[] Data { get; set; } = Array.Empty<byte>();
    
    /// <summary>
    /// Gets or sets any error message
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Gets or sets the timestamp when response was received
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Gets the response as ASCII string
    /// </summary>
    public string AsString => System.Text.Encoding.ASCII.GetString(Data);
}

/// <summary>
/// Radio operating modes
/// </summary>
public enum RadioMode
{
    /// <summary>
    /// Amplitude Modulation
    /// </summary>
    AM,
    
    /// <summary>
    /// Frequency Modulation
    /// </summary>
    FM,
    
    /// <summary>
    /// Lower Sideband
    /// </summary>
    LSB,
    
    /// <summary>
    /// Upper Sideband
    /// </summary>
    USB,
    
    /// <summary>
    /// Continuous Wave (CW)
    /// </summary>
    CW,
    
    /// <summary>
    /// Digital modes
    /// </summary>
    Digital,
    
    /// <summary>
    /// Unknown mode
    /// </summary>
    Unknown
}