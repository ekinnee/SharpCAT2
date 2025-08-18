namespace SharpCAT2.WebClient.Configuration;

/// <summary>
/// Configuration for the web client application
/// </summary>
public class WebClientConfiguration
{
    /// <summary>
    /// SharpCAT2 WebApi base URL
    /// </summary>
    public string WebApiBaseUrl { get; set; } = "https://localhost:5001";

    /// <summary>
    /// Connection timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}