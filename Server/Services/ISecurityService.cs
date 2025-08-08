using System.Net;

namespace SharpCAT2.Server.Services;

/// <summary>
/// Service interface for managing network security features
/// </summary>
public interface ISecurityService
{
    /// <summary>
    /// Validates if a client IP address is allowed to connect
    /// </summary>
    /// <param name="clientEndPoint">Client endpoint to validate</param>
    /// <returns>True if the client is allowed to connect</returns>
    bool IsClientAllowed(EndPoint clientEndPoint);

    /// <summary>
    /// Checks if a client should be rate limited
    /// </summary>
    /// <param name="clientEndPoint">Client endpoint to check</param>
    /// <returns>True if the client should be rate limited</returns>
    bool IsClientRateLimited(EndPoint clientEndPoint);

    /// <summary>
    /// Records a connection attempt from a client
    /// </summary>
    /// <param name="clientEndPoint">Client endpoint that attempted to connect</param>
    void RecordConnectionAttempt(EndPoint clientEndPoint);

    /// <summary>
    /// Validates authentication for a client connection
    /// </summary>
    /// <param name="clientEndPoint">Client endpoint</param>
    /// <param name="authData">Authentication data provided by client</param>
    /// <returns>True if authentication is valid</returns>
    bool ValidateAuthentication(EndPoint clientEndPoint, string? authData);

    /// <summary>
    /// Gets the list of allowed IP addresses/ranges
    /// </summary>
    IReadOnlyList<string> AllowedIpRanges { get; }

    /// <summary>
    /// Gets the rate limit configuration
    /// </summary>
    TimeSpan RateLimitWindow { get; }

    /// <summary>
    /// Gets the maximum connections per rate limit window
    /// </summary>
    int MaxConnectionsPerWindow { get; }

    /// <summary>
    /// Gets whether authentication is required
    /// </summary>
    bool AuthenticationRequired { get; }
}