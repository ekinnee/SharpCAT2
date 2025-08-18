namespace SharpCAT2.WebClient.Models;

/// <summary>
/// Response model for radio status
/// </summary>
public class RadioStatusResponse
{
    public bool Connected { get; set; }
    public string? Message { get; set; }
    public string? Radio { get; set; }
    public string? Status { get; set; }
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
/// Generic API response model
/// </summary>
public class ApiResponse
{
    public string? Message { get; set; }
    public string? Error { get; set; }
    public bool IsSuccess => string.IsNullOrEmpty(Error);
}

/// <summary>
/// Response model for radio commands
/// </summary>
public class CommandResponse
{
    public bool Processed { get; set; }
    public string? Error { get; set; }
    public bool IsSuccess => string.IsNullOrEmpty(Error);
}

/// <summary>
/// Model for available radio
/// </summary>
public class RadioModel
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}