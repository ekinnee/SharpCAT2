# SharpCAT2 Copilot Instructions

## Project Overview

SharpCAT2 is a cross-platform .NET 8 serial port communication server application designed for amateur radio CAT (Computer Aided Transceiver) control. The project provides robust error handling, platform-specific guidance, and support for 29+ radio models across 7 major manufacturers.

## Architecture

### Core Components
- **Server Application**: TCP server providing serial port access with dependency injection and service-based architecture
- **Client Library & Application**: .NET library and console app for remote serial communication
- **Common Library**: Shared radio models and serial port abstraction
- **Comprehensive Testing**: Unit and integration tests with simulated hardware support

### Key Services
- `IConfigurationService`: JSON configuration with JavaScript-style comments
- `INetworkService`: TCP server and client connection management
- `IRadioService`: Radio communication and CAT command handling
- `ISecurityService`: IP filtering and rate limiting

### Radio Support
- **29 radio models** across **7 manufacturers** (Kenwood, Elecraft, Yaesu, Icom, FlexRadio, Alinco, Ten-Tec)
- **47+ supported features** including frequency/mode control, VFO operations, digital modes, SDR features
- **Auto-detection capability** with manufacturer-specific communication patterns
- **Extensible architecture** using factory pattern and feature enumeration

## Technology Stack

- **.NET 8.0**: Primary runtime and framework
- **System.IO.Ports**: Cross-platform serial communication
- **Newtonsoft.Json**: Configuration parsing with comment support
- **Microsoft.Extensions.DependencyInjection**: Service container
- **Microsoft.Extensions.Hosting**: Generic Host pattern
- **Microsoft.Extensions.Logging**: Structured logging (server-only)

## Development Guidelines

### Code Organization
- Follow existing namespace structure: `SharpCAT2.{Component}.{Feature}`
- Place radio models in brand-organized folders under `SharpCAT2.Common.Radio.Models`
- Use dependency injection for all services
- Implement proper interfaces for testability

### Adding New Radio Support
1. Create class inheriting from `BaseRadio` in appropriate `Models/{Brand}/` folder
2. Define `SupportedFeatures` property with applicable capabilities
3. Implement radio-specific command parsing and feature methods
4. Register in `RadioFactory` with auto-detection patterns
5. Update `SUPPORTED_RADIOS.md` documentation

### Serial Port Abstraction
- Use `ISerialPort` interface for all serial communication
- `RealSerialPort`: Hardware communication wrapper
- `FakeSerialPort`: Protocol-agnostic simulation (transport only)
- `ResilientSerialPort`: Retry logic and error handling
- `SerialPortFactory`: Intelligent port creation and management

### Testing Strategy
- **Unit Tests**: Service layer, radio models, utilities
- **Integration Tests**: End-to-end serial communication scenarios
- **Simulation Support**: Use `FAKE` port and `DummyRadio` for testing without hardware
- **Test Scripts**: `test_serial_abstraction.sh` for comprehensive validation

### Configuration Management
- Support JSON files with JavaScript-style comments (`//` and `/* */`)
- Hierarchy: Command-line args → JSON config → Built-in defaults
- Separate server and client configuration files
- Validate baud rates against supported list: 9600, 14400, 19200, 28800, 38400, 57600, 115200, 128000, 256000

### Platform Considerations
- **Windows**: COM ports (`COM1`, `COM2`, etc.)
- **Linux**: USB serial (`/dev/ttyUSB0`), CDC/ACM (`/dev/ttyACM0`), built-in (`/dev/ttyS0`)
- **macOS**: USB serial (`/dev/cu.usbserial-*`), USB modem (`/dev/cu.usbmodem*`)
- Handle permissions (Linux dialout group membership)
- Provide platform-specific troubleshooting guidance

### Error Handling & Logging
- **Server-only logging**: Use dependency-injected `ILogger` interfaces
- **Radio classes**: Return errors via exceptions, return values, status objects
- **Service layer**: Interpret radio signals and generate uniform log messages
- **Platform-agnostic radio code**: No direct logging dependencies in radio implementations

### Performance & Security
- Implement connection health monitoring
- Provide retry policies for unreliable operations
- Support IP filtering and rate limiting in security service
- Handle graceful degradation for network and serial failures

## Common Patterns

### Adding a New Radio Model
```csharp
namespace SharpCAT2.Common.Radio.Models.{Brand}
{
    public class {ModelName} : BaseRadio
    {
        public override SupportedFeatures SupportedFeatures => 
            SupportedFeatures.FrequencyControl | 
            SupportedFeatures.ModeControl | 
            /* other features */;

        public override string Identify() => "{Brand} {Model}";
        
        // Implement specific command handling...
    }
}
```

### Service Implementation Pattern
```csharp
public class {Service}Service : I{Service}Service
{
    private readonly ILogger<{Service}Service> _logger;
    
    public {Service}Service(ILogger<{Service}Service> logger)
    {
        _logger = logger;
    }
    
    // Implement interface methods with proper logging and error handling
}
```

## Documentation Standards

- Update `README.md` for user-facing feature changes
- Maintain `SUPPORTED_RADIOS.md` for radio model additions
- Document architecture changes in `DI_ARCHITECTURE.md`
- Keep `CODE_REVIEW.md` and `CS_FILE_STRUCTURE_STANDARDS.md` current
- Provide platform-specific setup instructions and troubleshooting

## Quality Standards

- Maintain cross-platform compatibility (Windows, Linux, macOS)
- Ensure graceful error handling without application crashes
- Support both hardware and simulated testing scenarios
- Follow consistent coding style and naming conventions
- Provide comprehensive test coverage for new features
- Validate configuration changes against existing patterns

## Special Considerations

- **Fake Port**: Always support `FAKE` port for testing and development
- **Auto-detection**: Implement manufacturer-specific detection patterns
- **Backward Compatibility**: Maintain existing API contracts when possible
- **Resource Management**: Properly dispose of serial ports and network connections
- **Thread Safety**: Handle concurrent access to shared radio resources