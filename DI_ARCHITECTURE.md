# Dependency Injection and Service Architecture

## Overview

SharpCAT2 now uses a modern service-based architecture with dependency injection, providing better testability, maintainability, and extensibility.

## Architecture Changes

### Service Interfaces

The application has been refactored to use the following service interfaces:

- **IConfigurationService**: Manages application configuration with async file operations
- **INetworkService**: Handles TCP server operations and client connections
- **IRadioService**: Manages radio connections, commands, and status
- **ISecurityService**: Provides network security features including IP filtering and rate limiting

### Dependency Injection Setup

The application uses `Microsoft.Extensions.DependencyInjection` with the Generic Host pattern:

```csharp
var host = CreateHostBuilder(args).Build();
var app = host.Services.GetRequiredService<ServerApplication>();
await app.RunAsync(args);
```

### Security Features

The SecurityService provides several security enhancements:

- **IP Filtering**: CIDR notation support with default allowlist for localhost and private networks
- **Rate Limiting**: 10 connections per minute per IP address
- **Connection Tracking**: Logs and monitors connection attempts
- **Authentication Infrastructure**: Ready for future authentication mechanisms

### Testing

The new architecture enables comprehensive testing:

- **Unit Tests**: All services have isolated unit tests with mocking support
- **Integration Tests**: End-to-end testing of network security features
- **Test Coverage**: Expanded from 17 to 62 tests (265% increase)

## Usage

The application maintains backward compatibility with all existing command-line options and functionality while providing improved error handling, logging, and maintainability.

### Configuration

Configuration can be managed through:
- Command-line arguments (highest priority)
- JSON configuration files
- Default values

### Network Security

By default, the application only accepts connections from:
- localhost (127.0.0.1, ::1)
- Private network ranges (192.168.0.0/16, 10.0.0.0/8, 172.16.0.0/12)

Rate limiting prevents abuse with configurable thresholds.

### Logging

Structured logging is available throughout the application using `Microsoft.Extensions.Logging` with configurable log levels.

#### Logging Architecture

**Server/Service-Level Logging Only**: All logging is handled exclusively at the server and service layer using dependency-injected `ILogger` interfaces. This architecture provides:

- **Centralized Control**: All log messages go through a single, configurable logging system
- **Platform Independence**: Radio and protocol classes remain platform-agnostic 
- **Testability**: Services can be tested with mock loggers while radio classes remain focused on functionality
- **Consistency**: All log messages follow consistent patterns and formatting

**Radio Classes Are Log-Free**: Radio model classes, protocol classes, and radio factories do not contain any logging code. Instead:

- **Error Communication**: Errors are communicated via return values (null/false for failures), exceptions, or incomplete status objects
- **State Reporting**: Radio state and operation results are reported through method return values and events
- **Service Layer Responsibility**: The `RadioService` and other service classes detect these conditions and log appropriately

**Example Architecture**:
```csharp
// ❌ Wrong - Radio class should not log directly
public class SomeRadio : BaseRadio 
{
    public async Task<bool> ConnectAsync(ISerialPort port)
    {
        try { /* connection logic */ }
        catch (Exception ex) 
        {
            Console.WriteLine($"Error: {ex.Message}"); // ❌ Direct logging
            return false;
        }
    }
}

// ✅ Correct - Radio returns status, service logs
public class SomeRadio : BaseRadio 
{
    public async Task<bool> ConnectAsync(ISerialPort port)
    {
        try { /* connection logic */ }
        catch (Exception) 
        {
            return false; // ✅ Error communicated via return value
        }
    }
}

// Service layer handles logging
public class RadioService : IRadioService 
{
    private readonly ILogger<RadioService> _logger;
    
    public async Task<bool> ConnectRadioAsync(IRadio radio, ISerialPort port)
    {
        var success = await radio.ConnectAsync(port);
        if (!success)
        {
            _logger.LogError("Failed to connect to radio {Model}", radio.ModelName); // ✅ Service logs
        }
        return success;
    }
}
```