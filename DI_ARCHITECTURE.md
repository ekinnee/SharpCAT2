# Dependency Injection and Service Architecture

## Overview

SharpCAT2 uses a modern service-based architecture with dependency injection, providing better testability, maintainability, and extensibility. The application follows the Generic Host pattern and Microsoft's dependency injection best practices.

## Architecture Changes

### Service-Based Architecture

The application has been completely refactored from a monolithic approach to a service-based architecture with clear separation of concerns:

### Service Interfaces

The application has been refactored to use the following service interfaces:

- **IConfigurationService**: Manages application configuration with async file operations and validation
- **INetworkService**: Handles TCP server operations, client connections, and network communication
- **IRadioService**: Manages radio connections, CAT commands, and radio state management  
- **ISecurityService**: Provides network security features including IP filtering and rate limiting

### Service Implementations

Each service interface has a corresponding implementation that encapsulates specific functionality:

#### ConfigurationService
- **Responsibility**: Configuration management and validation
- **Features**: 
  - Async JSON configuration file loading and saving
  - Command-line argument integration with configuration files
  - Configuration validation and error handling
  - Support for default values and environment-specific settings

#### NetworkService  
- **Responsibility**: TCP server and client management
- **Features**:
  - TCP server lifecycle management
  - Client connection handling and message routing
  - Protocol-agnostic message handling
  - Connection monitoring and cleanup

#### RadioService
- **Responsibility**: Radio communication and control
- **Features**:
  - Radio factory integration for dynamic radio creation
  - CAT command processing and response handling
  - Radio status monitoring and reporting
  - Serial port abstraction integration

#### SecurityService
- **Responsibility**: Network security and access control  
- **Features**:
  - IP address filtering with CIDR notation support
  - Rate limiting per IP address (configurable thresholds)
  - Connection attempt logging and monitoring
  - Authentication infrastructure (ready for future expansion)

### Dependency Injection Setup

The application uses `Microsoft.Extensions.DependencyInjection` with the Generic Host pattern:

```csharp
var host = CreateHostBuilder(args).Build();
var app = host.Services.GetRequiredService<ServerApplication>();
await app.RunAsync(args);
```

**Service Registration:**
```csharp
services.AddSingleton<IConfigurationService, ConfigurationService>();
services.AddSingleton<INetworkService, NetworkService>();
services.AddSingleton<IRadioService, RadioService>();
services.AddSingleton<ISecurityService, SecurityService>();
services.AddTransient<ServerApplication>();
```

### Benefits of the New Architecture

1. **Improved Testability**: All services are easily mockable for comprehensive unit testing
2. **Better Maintainability**: Clear separation of concerns with well-defined interfaces
3. **Enhanced Modularity**: Services can be modified independently without affecting others
4. **Dependency Management**: Proper lifetime management through DI container
5. **Configuration Flexibility**: Clean configuration handling with multiple sources
6. **Security by Design**: Built-in security features with configurable policies

### Security Features

The SecurityService provides comprehensive security enhancements:

- **IP Filtering**: CIDR notation support with default allowlist for localhost and private networks
- **Rate Limiting**: Configurable connection limits per IP address (default: 10 connections per minute)
- **Connection Tracking**: Detailed logging and monitoring of connection attempts
- **Authentication Infrastructure**: Extensible framework ready for future authentication mechanisms
- **Default Security Policy**: Secure by default configuration restricting access to trusted networks

**Default Allowed Networks:**
- localhost: 127.0.0.0/8, ::1/128
- Private networks: 192.168.0.0/16, 10.0.0.0/8, 172.16.0.0/12

### Integration with Radio and Serial Abstraction

The service architecture seamlessly integrates with the radio and serial abstraction layers:

- **RadioService** uses the `ISerialPort` abstraction for hardware-agnostic communication
- **SerialPortFactory** integration enables both real hardware and simulated connections
- **Configuration-driven** radio selection and serial port configuration
- **Graceful error handling** across all service boundaries

### Testing

The new architecture enables comprehensive testing:

- **Unit Tests**: All services have isolated unit tests with mocking support
- **Integration Tests**: End-to-end testing of network security features
- **Test Coverage**: Expanded from 17 to 62 tests (265% increase)
- **Mock-Based Testing**: Services can be tested in isolation using mock dependencies
- **Comprehensive Scenarios**: Tests cover configuration management, network security, radio communication, and error handling

**Test Categories:**
- Service layer unit tests (ConfigurationService, NetworkService, RadioService, SecurityService)
- Serial abstraction tests (ISerialPort implementations and factory)
- Integration tests (end-to-end workflow testing)
- Utility and helper class tests

### Performance and Reliability

The service architecture provides enhanced performance and reliability:

- **Async/Await Pattern**: Non-blocking operations throughout the service layer
- **Resource Management**: Proper disposal patterns for network and serial resources
- **Error Recovery**: Resilient error handling with appropriate fallback strategies
- **Configuration Caching**: Efficient configuration loading and caching
- **Connection Pooling**: Efficient client connection management

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