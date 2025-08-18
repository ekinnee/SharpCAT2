namespace SharpCAT2.Core.Radio;

/// <summary>
/// Represents a command to be sent to a radio
/// </summary>
public class RadioCommand
{
    /// <summary>
    /// Gets the command string
    /// </summary>
    public string Command { get; }

    /// <summary>
    /// Gets the command description
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets whether the command expects a response
    /// </summary>
    public bool ExpectsResponse { get; }

    /// <summary>
    /// Gets the timeout for the command in milliseconds
    /// </summary>
    public int TimeoutMs { get; }

    /// <summary>
    /// Gets optional parameters for the command
    /// </summary>
    public Dictionary<string, object> Parameters { get; }

    /// <summary>
    /// Initializes a new RadioCommand
    /// </summary>
    /// <param name="command">Command string</param>
    /// <param name="description">Command description</param>
    /// <param name="expectsResponse">Whether the command expects a response</param>
    /// <param name="timeoutMs">Command timeout in milliseconds</param>
    public RadioCommand(string command, string description = "", bool expectsResponse = true, int timeoutMs = 5000)
    {
        Command = command ?? throw new ArgumentNullException(nameof(command));
        Description = description;
        ExpectsResponse = expectsResponse;
        TimeoutMs = timeoutMs;
        Parameters = new Dictionary<string, object>();
    }

    /// <summary>
    /// Sets a parameter for the command
    /// </summary>
    /// <param name="key">Parameter key</param>
    /// <param name="value">Parameter value</param>
    /// <returns>This RadioCommand instance for chaining</returns>
    public RadioCommand SetParameter(string key, object value)
    {
        Parameters[key] = value;
        return this;
    }

    /// <summary>
    /// Gets a parameter value
    /// </summary>
    /// <typeparam name="T">Type of the parameter</typeparam>
    /// <param name="key">Parameter key</param>
    /// <param name="defaultValue">Default value if parameter not found</param>
    /// <returns>Parameter value</returns>
    public T GetParameter<T>(string key, T defaultValue = default!)
    {
        if (Parameters.TryGetValue(key, out var value) && value is T)
        {
            return (T)value;
        }
        return defaultValue;
    }

    public override string ToString()
    {
        return $"{Command} ({Description})";
    }

    #region Nested Types

    /// <summary>
    /// Common radio commands
    /// </summary>
    public static class Common
    {
        public static RadioCommand GetFrequency => new("FA;", "Get frequency (VFO A)", true);
        public static RadioCommand SetFrequency(long frequency) => new($"FA{frequency:D11};", $"Set frequency to {frequency} Hz", true);
        
        public static RadioCommand GetMode => new("MD;", "Get mode", true);
        public static RadioCommand SetMode(string mode) => new($"MD{mode};", $"Set mode to {mode}", true);
        
        public static RadioCommand GetTransmitStatus => new("IF;", "Get transceiver information", true);
        public static RadioCommand PowerOn => new("PS1;", "Power on", false);
        public static RadioCommand PowerOff => new("PS0;", "Power off", false);
        
        public static RadioCommand GetRigId => new("ID;", "Get rig ID", true);
        public static RadioCommand GetAntenna => new("AN;", "Get antenna", true);
        public static RadioCommand SetAntenna(int antenna) => new($"AN{antenna};", $"Set antenna to {antenna}", true);
    }

    #endregion
}