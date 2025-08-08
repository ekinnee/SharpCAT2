using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net;

namespace SharpCAT2.Server.Services;

/// <summary>
/// Implementation of security service for managing network security features
/// </summary>
public class SecurityService : ISecurityService
{
    private readonly ILogger<SecurityService> _logger;
    private readonly ConcurrentDictionary<string, List<DateTime>> _connectionAttempts = new();
    private readonly List<string> _allowedIpRanges = new();

    public SecurityService(ILogger<SecurityService> logger)
    {
        _logger = logger;
        
        // Default configuration - allow local connections
        _allowedIpRanges.Add("127.0.0.1");     // localhost IPv4
        _allowedIpRanges.Add("::1");           // localhost IPv6
        _allowedIpRanges.Add("192.168.0.0/16"); // Private network range
        _allowedIpRanges.Add("10.0.0.0/8");     // Private network range
        _allowedIpRanges.Add("172.16.0.0/12");  // Private network range
    }

    /// <inheritdoc />
    public IReadOnlyList<string> AllowedIpRanges => _allowedIpRanges.AsReadOnly();

    /// <inheritdoc />
    public TimeSpan RateLimitWindow { get; } = TimeSpan.FromMinutes(1);

    /// <inheritdoc />
    public int MaxConnectionsPerWindow { get; } = 10;

    /// <inheritdoc />
    public bool AuthenticationRequired { get; } = false; // Basic implementation - could be configurable

    /// <inheritdoc />
    public bool IsClientAllowed(EndPoint clientEndPoint)
    {
        if (clientEndPoint is not IPEndPoint ipEndPoint)
        {
            _logger.LogWarning("Unknown endpoint type: {EndPointType}", clientEndPoint.GetType());
            return false;
        }

        var clientIp = ipEndPoint.Address.ToString();
        
        // Check against allowed IP ranges
        foreach (var allowedRange in _allowedIpRanges)
        {
            if (IsIpInRange(clientIp, allowedRange))
            {
                _logger.LogDebug("Client {ClientIp} allowed by range {AllowedRange}", clientIp, allowedRange);
                return true;
            }
        }

        _logger.LogWarning("Client {ClientIp} not in allowed IP ranges", clientIp);
        return false;
    }

    /// <inheritdoc />
    public bool IsClientRateLimited(EndPoint clientEndPoint)
    {
        if (clientEndPoint is not IPEndPoint ipEndPoint)
        {
            return true; // Unknown endpoint types are rate limited
        }

        var clientIp = ipEndPoint.Address.ToString();
        var now = DateTime.UtcNow;
        var windowStart = now - RateLimitWindow;

        // Get or create connection attempts list for this IP
        var attempts = _connectionAttempts.GetOrAdd(clientIp, _ => new List<DateTime>());

        lock (attempts)
        {
            // Remove old attempts outside the window
            attempts.RemoveAll(attempt => attempt < windowStart);

            // Check if client has exceeded the limit
            if (attempts.Count >= MaxConnectionsPerWindow)
            {
                _logger.LogWarning("Client {ClientIp} rate limited: {AttemptCount} connections in {Window}", 
                    clientIp, attempts.Count, RateLimitWindow);
                return true;
            }

            return false;
        }
    }

    /// <inheritdoc />
    public void RecordConnectionAttempt(EndPoint clientEndPoint)
    {
        if (clientEndPoint is not IPEndPoint ipEndPoint)
        {
            return;
        }

        var clientIp = ipEndPoint.Address.ToString();
        var now = DateTime.UtcNow;

        var attempts = _connectionAttempts.GetOrAdd(clientIp, _ => new List<DateTime>());

        lock (attempts)
        {
            attempts.Add(now);
            _logger.LogDebug("Recorded connection attempt from {ClientIp} at {Timestamp}", clientIp, now);
        }

        // Cleanup old entries periodically
        if (now.Ticks % 10000000 == 0) // Roughly every second
        {
            CleanupOldAttempts(now);
        }
    }

    /// <inheritdoc />
    public bool ValidateAuthentication(EndPoint clientEndPoint, string? authData)
    {
        if (!AuthenticationRequired)
        {
            return true; // Authentication is not required in basic implementation
        }

        // Basic implementation - could be extended with proper authentication
        // For now, just log the attempt
        if (clientEndPoint is IPEndPoint ipEndPoint)
        {
            var clientIp = ipEndPoint.Address.ToString();
            _logger.LogDebug("Authentication attempt from {ClientIp} with data: {AuthData}", 
                clientIp, authData ?? "<none>");
        }

        return true; // Allow all connections in basic implementation
    }

    private bool IsIpInRange(string ipAddress, string range)
    {
        try
        {
            // Handle simple IP address comparison
            if (!range.Contains('/'))
            {
                return ipAddress.Equals(range, StringComparison.OrdinalIgnoreCase);
            }

            // Handle CIDR notation
            var parts = range.Split('/');
            if (parts.Length != 2 || !int.TryParse(parts[1], out int prefixLength))
            {
                return false;
            }

            var networkIp = IPAddress.Parse(parts[0]);
            var clientIp = IPAddress.Parse(ipAddress);

            // Convert to bytes for comparison
            var networkBytes = networkIp.GetAddressBytes();
            var clientBytes = clientIp.GetAddressBytes();

            if (networkBytes.Length != clientBytes.Length)
            {
                return false; // Different IP versions
            }

            // Calculate subnet mask
            var bytesToCheck = prefixLength / 8;
            var bitsToCheck = prefixLength % 8;

            // Check full bytes
            for (int i = 0; i < bytesToCheck; i++)
            {
                if (networkBytes[i] != clientBytes[i])
                {
                    return false;
                }
            }

            // Check partial byte if needed
            if (bitsToCheck > 0 && bytesToCheck < networkBytes.Length)
            {
                var mask = (byte)(0xFF << (8 - bitsToCheck));
                if ((networkBytes[bytesToCheck] & mask) != (clientBytes[bytesToCheck] & mask))
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking IP range for {IpAddress} in {Range}", ipAddress, range);
            return false;
        }
    }

    private void CleanupOldAttempts(DateTime now)
    {
        var windowStart = now - RateLimitWindow;
        var keysToRemove = new List<string>();

        foreach (var kvp in _connectionAttempts)
        {
            lock (kvp.Value)
            {
                kvp.Value.RemoveAll(attempt => attempt < windowStart);
                
                // Remove empty lists
                if (kvp.Value.Count == 0)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
        }

        foreach (var key in keysToRemove)
        {
            _connectionAttempts.TryRemove(key, out _);
        }
    }
}