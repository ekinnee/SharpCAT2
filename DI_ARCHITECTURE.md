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
- **Test Coverage**: Expanded from 17 to 44 tests (159% increase)

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