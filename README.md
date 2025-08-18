# SharpCAT2

A cross-platform .NET serial port communication server application that provides robust error handling and platform-specific guidance.

## Features

- **Modular Architecture**: Four distinct applications sharing a common core library
  - **SharpCAT2.Console**: Command-line application for direct radio control
  - **SharpCAT2.WebApi**: HTTP REST API for web-based radio control and integration  
  - **SharpCAT2.WebClient**: Modern web-based client application with responsive GUI
  - **SharpCAT2.Client**: Remote client for TCP-based communication
- **Cross-Platform Support**: Works on Windows, Linux, and macOS
- **Platform-Specific Guidance**: Automatic detection of operating system with appropriate port naming conventions
- **Robust Error Handling**: Comprehensive error messages and troubleshooting guidance
- **Interactive Port Selection**: Smart port discovery and selection
- **Command-Line Interface**: Flexible command-line options for automation and scripting
- **TCP Server**: Remote client access via TCP connections
- **REST API**: Full HTTP REST API with endpoints for radio status, control, and configuration
- **Web Client**: Modern Blazor web application for browser-based radio control
- **Client Library**: .NET library for programmatic access to remote serial ports
- **Client Console App**: Interactive console application for remote serial communication
- **Radio Support**: Built-in support for 29+ popular amateur radio models with CAT (Computer Aided Transceiver) control
- **Auto-Detection**: Automatic radio type detection and configuration
- **Extensible Architecture**: Plugin-style radio model support through the SharpCAT2.Radio namespace
- **Graceful Error Handling**: Both server and client handle connection and communication errors without crashing

## Requirements

- .NET 8.0 or later
- Appropriate permissions for serial port access (see Platform-Specific Setup below)

## Quick Start

### Server (Basic Usage)

```bash
# Clone and build
git clone https://github.com/ekinnee/SharpCAT2.git
cd SharpCAT2
dotnet build

# Run with simulated port for testing (no hardware required)
cd SharpCAT2.Console
dotnet run -- --port fake

# Connect to real hardware
dotnet run -- --port COM1              # Windows
dotnet run -- --port /dev/ttyUSB0      # Linux  
dotnet run -- --port /dev/cu.usbserial-1410  # macOS

# With radio support
dotnet run -- --port COM1 --radio "Kenwood TS-2000"
dotnet run -- --port fake --radio "SharpCAT2 DummyRadio"  # Simulated radio for testing
dotnet run -- --port COM1 --auto-detect
```

### Client (Connect to Server)

```bash
# In another terminal
cd Client
dotnet run

# Connect to remote server
dotnet run -- --host 192.168.1.100 --port 8080
```

### Web Client (Browser-Based Control)

```bash
# Start the WebApi first
cd SharpCAT2.WebApi  
dotnet run

# In another terminal, start the Web Client
cd SharpCAT2.WebClient
dotnet run

# Open browser to https://localhost:5027 (or URL shown in console)
# Configure WebApi URL in web interface if needed
```

## Building and Running

### Build Entire Solution

```bash
# Clone repository
git clone https://github.com/ekinnee/SharpCAT2.git
cd SharpCAT2

# Build entire solution
dotnet build SharpCAT2.sln

# Or build individual projects
cd SharpCAT2.Console
dotnet restore
dotnet build

# Run tests
dotnet test

# Create release package
dotnet publish -c Release -o ./publish
```

## Usage

### Server Application

The server application opens a serial port and provides both console interface and TCP server for remote clients.

#### Command-Line Options

| Option | Short | Description | Example |
|--------|-------|-------------|---------|
| `--port` | `-p` | Serial port name (hardware ports or 'fake' for simulation) | `--port COM1` or `--port fake` |
| `--baud` | `-b` | Baud rate (default: 9600)<br/>Supported rates: 9600, 14400, 19200, 28800, 38400, 57600, 115200, 128000, 256000 | `--baud 115200` |
| `--tcp-port` | `-t` | TCP server port (default: 8080) | `--tcp-port 9090` |
| `--radio` | `-r` | Radio model name | `--radio "Kenwood TS-2000"` |
| `--auto-detect` | | Auto-detect radio type | `--auto-detect` |
| `--list` | `-l` | List available ports (includes 'fake' for simulation) | `--list` |
| `--list-radios` | | List available radio models | `--list-radios` |
| `--radio-info` | | Show detailed information about a radio model | `--radio-info "Elecraft K3"` |
| `--help` | `-h` | Show help message | `--help` |

#### Supported Radio Models

SharpCAT2 supports **29 radio models** across **7 major manufacturers**:

- **Kenwood**: TS-2000, TS-890S, TS-590SG, TH-D74A, TM-D710GA
- **Elecraft**: K3, K4, KX3, K2, K1  
- **Yaesu**: FT-991A, FT-710, FT-DX101D, FT-891, FT-65
- **Icom**: IC-7300, IC-9700
- **FlexRadio**: FLEX-6400, FLEX-6600, FLEX-6700
- **Alinco**: DX-SR8T, DJ-MD5TGP, DR-638T, DX-70T
- **Ten-Tec**: OMNI VII, Eagle, Argonaut V, Jupiter
- **SharpCAT2**: DummyRadio (for testing and development)

For detailed information about supported features for each radio model, see [SUPPORTED_RADIOS.md](SUPPORTED_RADIOS.md).

#### Testing and Development

SharpCAT2 provides a special simulated port for testing and development:

- **fake**: Simulated serial port that works without hardware. Perfect for testing, development, and demonstrations.

```bash
# Run with simulated port (no hardware required)
dotnet run -- --port fake

# Test with simulated radio
dotnet run -- --port fake --radio "SharpCAT2 DummyRadio"

# List all ports (including fake)
dotnet run -- --list
```

The fake port provides:
- Pure serial communication simulation without protocol knowledge
- Protocol-agnostic transport layer for any radio simulation
- No hardware dependencies for development and testing
- Compatible with all SharpCAT2 features including radio support

**Architecture**: The `FakeSerialPort` provides only serial I/O simulation (open/close/read/write, buffering, connection state) and contains no knowledge of radio commands or protocols. All radio simulation logic is implemented in radio classes like `DummyRadio`, which use `FakeSerialPort` purely as a transport mechanism. This clean separation allows for proper testing of both transport and protocol layers independently.

### Client Library and Application

The client library (`SharpCAT2.ClientLib`) provides methods for connecting to the SharpCAT2 server over TCP and sending commands to the remote serial port. The client console application provides an interactive interface.

#### Building the Client

```bash
# Navigate to the Client directory
cd Client

# Build the client library
dotnet build SharpCAT2.ClientLib.csproj

# Build the client console application
dotnet build Client.csproj
```

#### Running the Client Application

```bash
# Navigate to the Client directory
cd Client

# Connect to server on localhost:8080
dotnet run --project Client.csproj

# Connect to a remote server
dotnet run --project Client.csproj -- --host 192.168.1.100

# Connect to a custom port
dotnet run --project Client.csproj -- --host localhost --port 9090

# Show help
dotnet run --project Client.csproj -- --help
```

## Configuration

SharpCAT2 supports configuration through both command-line arguments and JSON configuration files. Configuration files provide default values that can be overridden by command-line arguments.

### Server Configuration

The server can be configured using a `server_config.json` file in the Server directory:

```json
{
  "serialPort": "FAKE",
  "radio": "SharpCAT2 DummyRadio",
  "baudRate": 9600,
  "tcpPort": 8080,
  "autoDetectRadio": true
}
```

**Configuration Options:**
- `serialPort`: Default serial port name
- `radio`: Default radio model to use
- `baudRate`: Default baud rate for serial communication
- `tcpPort`: Default TCP port for server connections
- `autoDetectRadio`: Whether to attempt automatic radio detection

### Client Configuration

The client can be configured using a `client_config.json` file in the Client directory:

```json
{
  "serverHost": "localhost",
  "serverPort": 8080
}
```

**Configuration Options:**
- `serverHost`: Default server hostname or IP address
- `serverPort`: Default server TCP port

**Configuration Priority:**
1. Command-line arguments (highest priority)
2. JSON configuration file values
3. Built-in defaults (lowest priority)

### JSON Comment Support

SharpCAT2 configuration files support **JavaScript-style comments** for better documentation and maintainability. You can use both single-line (`//`) and multi-line (`/* */`) comments in your JSON configuration files.

**Example server configuration with comments:**
```json
{
  // Serial port configuration
  "serialPort": "COM3",              // Windows serial port
  "radio": "Kenwood TS-2000",        /* Popular radio model */
  "baudRate": 57600,                 // Higher speed for better performance
  "tcpPort": 8888,                   /* Custom TCP port */
  "autoDetectRadio": false           // Manual radio selection
  
  /* Additional settings can be added later:
     - logging configuration
     - security settings
     - performance tuning
  */
}
```

**Example client configuration with comments:**
```json
{
  // Client connection settings
  "serverHost": "192.168.1.100",     // Remote server IP
  "serverPort": 9090                 /* Custom port number */
  // "timeout": 30                   // Optional timeout setting
}
```

**Comment Guidelines:**
- Use `//` for single-line comments
- Use `/* */` for multi-line comments  
- Comments can appear at the end of lines or on separate lines
- Comments are ignored during configuration loading
- Maintain valid JSON structure around comments

## Supported Baud Rates

The server supports the following standard baud rates for serial communication:

- **9600** (default)
- **14400**
- **19200**
- **28800**
- **38400**
- **57600**
- **115200**
- **128000**
- **256000**

If you specify an unsupported baud rate, the application will display an error message with the list of supported rates and exit. This restriction ensures compatibility with common serial devices and prevents configuration errors.

**Example with unsupported baud rate:**
```bash
dotnet run -- --port COM1 --baud 12345
# Output: Error: Unsupported baud rate '12345'.
#         Supported baud rates: 9600, 14400, 19200, 28800, 38400, 57600, 115200, 128000, 256000
```


## Platform-Specific Setup

### Windows

**Port Names**: `COM1`, `COM2`, `COM3`, etc.

**Setup**:
- No additional setup usually required
- Ensure device drivers are installed
- Check Device Manager if ports don't appear

**Example**:
```bash
dotnet run -- --port COM1 --baud 9600
```

### Linux

**Port Names**: 
- USB Serial: `/dev/ttyUSB0`, `/dev/ttyUSB1`, etc.
- USB CDC/ACM: `/dev/ttyACM0`, `/dev/ttyACM1`, etc.
- Built-in Serial: `/dev/ttyS0`, `/dev/ttyS1`, etc.

**Setup**:
```bash
# Add your user to the dialout group for serial port access
sudo usermod -a -G dialout $USER

# Log out and back in for changes to take effect
# Or restart your session

# Verify group membership
groups $USER

# Check available serial devices
ls /dev/tty*
```

**Troubleshooting**:
```bash
# Check if device is recognized
dmesg | grep tty

# Check permissions
ls -la /dev/ttyUSB0

# Test with sudo (temporary fix)
sudo dotnet run -- --port /dev/ttyUSB0
```

**Example**:
```bash
dotnet run -- --port /dev/ttyUSB0 --baud 115200
```

### macOS

**Port Names**: 
- USB Serial: `/dev/cu.usbserial-*`, `/dev/cu.usbmodem*`
- Bluetooth: `/dev/cu.Bluetooth-*`

**Setup**:
- Install appropriate device drivers (e.g., FTDI, Prolific, etc.)
- No group membership changes typically required
- Some devices may require specific drivers from manufacturer

**Finding Devices**:
```bash
# List all cu devices
ls /dev/cu.*

# List USB devices
system_profiler SPUSBDataType
```

**Example**:
```bash
dotnet run -- --port /dev/cu.usbserial-1410 --baud 9600
```

## Common Issues and Solutions

### "Access Denied" Errors

**Linux/macOS**: Add user to dialout group (Linux) or check device drivers (macOS)
```bash
# Linux
sudo usermod -a -G dialout $USER

# Check current groups
groups $USER
```

**Windows**: Check if another application is using the port

### "Port Not Found" Errors

1. **Verify device connection**: Check physical USB connection
2. **List available ports**: Use `dotnet run -- --list`
3. **Check device manager**: (Windows) or `dmesg` (Linux) for device recognition
4. **Driver installation**: Ensure proper drivers are installed

### "Permission Denied" on Linux

```bash
# Temporary fix with sudo
sudo dotnet run -- --port /dev/ttyUSB0

# Permanent fix - add to dialout group
sudo usermod -a -G dialout $USER
# Then logout and login again
```

### No Ports Listed

**Linux**:
```bash
# Check for connected devices
ls /dev/tty*
dmesg | grep -i usb
lsusb
```

**macOS**:
```bash
# Check for devices
ls /dev/cu.*
system_profiler SPUSBDataType
```

**Windows**:
- Check Device Manager under "Ports (COM & LPT)"
- Verify device drivers are installed

## Development

### Project Structure

```
SharpCAT2/
├── src/                          # Source code projects
│   ├── SharpCAT2.Core/           # Core business logic library
│   │   ├── Configuration/        # Configuration classes (ServerConfig, CommandLineOptions)
│   │   ├── Services/             # Service layer implementations
│   │   │   ├── IConfigurationService.cs # Configuration service interface
│   │   │   ├── ConfigurationService.cs  # Configuration service implementation
│   │   │   ├── INetworkService.cs       # Network service interface
│   │   │   ├── NetworkService.cs        # TCP server and client management
│   │   │   ├── IRadioService.cs         # Radio service interface
│   │   │   ├── RadioService.cs          # Radio communication and command handling
│   │   │   ├── ISecurityService.cs      # Security service interface
│   │   │   └── SecurityService.cs       # IP filtering and rate limiting
│   │   └── SharpCAT2.Core.csproj # Core library project file
│   ├── SharpCAT2.Console/        # Console server application (entry point)
│   │   ├── Program.cs            # Main entry point with dependency injection setup
│   │   ├── ServerApplication.cs  # Console application logic using Core services
│   │   ├── CommandLineParser.cs  # Command-line argument parsing
│   │   ├── Constants.cs          # Application constants
│   │   ├── PortSelector.cs       # Serial port selection utilities
│   │   ├── server_config.sample.json # Default server configuration
│   │   └── SharpCAT2.Console.csproj # Console project file
│   ├── SharpCAT2.WebApi/         # Web API application
│   │   ├── Controllers/          # API controllers
│   │   │   └── RadioController.cs # Radio operations REST API
│   │   ├── Program.cs            # Web API startup with Core services DI
│   │   └── SharpCAT2.WebApi.csproj # Web API project file
│   ├── SharpCAT2.Client/         # Client console application
│   │   ├── ClientCommandProcessor.cs # Command processing logic
│   │   ├── ClientConstants.cs    # Client application constants
│   │   ├── Program.cs            # Client console application
│   │   ├── client_config.sample.json # Default client configuration
│   │   └── SharpCAT2.Client.csproj # Client console app project file
│   ├── SharpCAT2.ClientLib/      # Client library for remote communication
│   │   ├── ClientLib.cs          # Client library implementation
│   │   ├── ClientConfig.cs       # Client configuration management
│   │   └── SharpCAT2.ClientLib.csproj # Client library project file
│   ├── SharpCAT2.Common/         # Shared library with radio models and serial abstraction
│   │   ├── Radio/                # Radio support library
│   │   │   ├── RadioFactory.cs   # Dynamic radio creation and discovery
│   │   │   ├── Models/           # Radio model implementations by brand
│   │   │   │   ├── BaseRadio.cs  # Abstract base radio implementation
│   │   │   │   ├── Testing/      # Test radio implementations (DummyRadio)
│   │   │   │   ├── Kenwood/      # Kenwood radio models (TS-2000, TS-890S, etc.)
│   │   │   │   ├── Elecraft/     # Elecraft radio models (K3, K4, KX3, etc.)
│   │   │   │   ├── Yaesu/        # Yaesu radio models (FT-991A, FT-710, etc.)
│   │   │   │   ├── Icom/         # Icom radio models (IC-7300, IC-9700)
│   │   │   │   ├── FlexRadio/    # FlexRadio models (FLEX-6400, 6600, 6700)
│   │   │   │   ├── Alinco/       # Alinco radio models (DX-SR8T, DJ-MD5TGP, etc.)
│   │   │   │   └── TenTec/       # Ten-Tec radio models (OMNI VII, Eagle, etc.)
│   │   │   └── Protocols/        # Protocol implementations
│   │   ├── Serial/               # Serial port abstraction layer
│   │   │   ├── ISerialPort.cs    # Serial port interface abstraction
│   │   │   ├── RealSerialPort.cs # Real hardware serial port wrapper
│   │   │   ├── FakeSerialPort.cs # Protocol-agnostic simulated serial port
│   │   │   ├── SerialPortFactory.cs # Factory for creating serial port instances
│   │   │   └── ResilientSerialPort.cs # Resilient serial port with retry logic
│   │   └── SharpCAT2.Common.csproj # Common library project file
│   └── SharpCAT2.WebClient/      # Blazor Server web client
│       ├── Components/           # Blazor components
│       │   ├── Layout/           # Layout components
│       │   └── Pages/            # Page components
│       ├── Services/             # Web client services
│       └── SharpCAT2.WebClient.csproj # Web client project file
├── tests/                        # Test projects
│   └── SharpCAT2.Tests/          # Comprehensive test suite
│       ├── Services/             # Service layer tests
│       ├── Serial/               # Serial abstraction tests
│       ├── Integration/          # Integration tests
│       ├── Utils/                # Utility tests
│       └── SharpCAT2.Tests.csproj # Test project file
├── .vscode/                      # Visual Studio Code configuration
│   ├── tasks.json                # Build and test tasks
│   ├── launch.json               # Debug configurations
│   ├── settings.json             # Project settings
│   └── extensions.json           # Recommended extensions
├── RadioTemplate.cs              # Template for creating new radio models
├── test_serial_abstraction.sh    # Integration test script
└── *.md                         # Documentation files
```

### Dependencies

- **System.IO.Ports**: Cross-platform serial port communication
- **.NET 8.0**: Runtime platform
- **Newtonsoft.Json**: JSON configuration file parsing with comment support
- **SharpCAT2.Common**: Shared library containing radio control and serial abstraction
- **Microsoft.Extensions.DependencyInjection**: Dependency injection framework
- **Microsoft.Extensions.Hosting**: Generic Host pattern for service lifecycle management
- **Microsoft.Extensions.Logging**: Structured logging framework

### Modern Service Architecture

The application uses a service-based architecture with dependency injection:

**Core Services:**
- **IConfigurationService**: Manages application configuration with async file operations and validation
- **INetworkService**: Handles TCP server operations, client connections, and network communication
- **IRadioService**: Manages radio connections, CAT commands, and radio state management
- **ISecurityService**: Provides network security features including IP filtering and rate limiting

**Architecture Benefits:**
- **Improved Testability**: All services are easily mockable for unit testing
- **Better Maintainability**: Clear separation of concerns with well-defined interfaces
- **Enhanced Security**: Built-in IP filtering, rate limiting, and connection monitoring
- **Flexible Configuration**: Support for JSON configuration files with command-line overrides
- **Structured Logging**: Comprehensive logging throughout all service layers with server-only architecture

**Logging Architecture:**
All logging is handled exclusively at the server/service layer using dependency-injected `ILogger` interfaces. Radio classes, protocol classes, and factories are designed to be logging-free and platform-agnostic. This architectural decision provides:

- **Centralized Control**: All log messages flow through configurable service-layer loggers
- **Platform Independence**: Radio implementations remain focused on communication logic
- **Better Testability**: Radio classes can be tested without logging dependencies
- **Consistent Messaging**: All errors and status updates follow uniform logging patterns

Radio classes communicate errors and status through return values, exceptions, and status objects, while the service layer interprets these signals and generates appropriate log messages.

For detailed information about the architecture, see [DI_ARCHITECTURE.md](DI_ARCHITECTURE.md).

### Radio Architecture

The radio support is implemented through the `SharpCAT2.Common.Radio` namespace which provides:

- **IRadio Interface**: Defines the contract for radio communication with SupportedFeatures property
- **SupportedFeatures Enum**: Comprehensive enumeration of 47 radio capabilities
- **RadioCommand**: Encapsulates radio commands with parameters and metadata
- **RadioStatus**: Represents radio state information
- **RadioFactory**: Dynamic radio discovery and instantiation with auto-detection
- **BaseRadio**: Abstract base class providing common functionality and default implementations
- **Brand-Organized Models**: Radio implementations organized by manufacturer folders

**Supported Features Include:**
- Frequency and mode control
- VFO operations (dual VFO, swap, split)  
- RIT/XIT incremental tuning
- Power output and metering (S-meter, SWR, ALC)
- Antenna selection
- Memory channel management
- CW keyer and message sending
- Noise reduction and DSP features
- Filter selection and bandwidth control
- Digital mode support (PSK31, RTTY, DMR)
- SDR features (waterfall, panadapter)

To add support for a new radio model:

1. Create a new class inheriting from `BaseRadio` in the appropriate brand folder
2. Define the `SupportedFeatures` property with applicable capabilities
3. Implement radio-specific command parsing and feature methods
4. Register the radio in `RadioFactory` and add auto-detection patterns
5. The radio will be automatically available in both server and client

### Building from Source

```bash
# Clone repository
git clone https://github.com/ekinnee/SharpCAT2.git
cd SharpCAT2

# Build entire solution
dotnet build SharpCAT2.sln

# Or build individual projects
cd src/SharpCAT2.Console
dotnet restore
dotnet build

# Run tests
dotnet test

# Create release package
dotnet publish -c Release -o ./publish
```

### Running the Applications

**Console Server:**
```bash
# Run the server with default settings
cd src/SharpCAT2.Console
dotnet run

# Run with specific parameters
dotnet run -- --port FAKE --radio "SharpCAT2 DummyRadio"
dotnet run -- --help  # Show all options
```

**Client Application:**
```bash
# Run the client
cd src/SharpCAT2.Client
dotnet run

# Connect to remote server
dotnet run -- --host 192.168.1.100 --port 8080
```

**Web API:**
```bash
# Run the Web API
cd src/SharpCAT2.WebApi
dotnet run
# Access at https://localhost:5001/swagger
```

**Web Client:**
```bash
# Run the Blazor web client
cd src/SharpCAT2.WebClient
dotnet run
# Access at https://localhost:5001
```

### Development Environment

The project includes Visual Studio Code configuration for development:

**Included VS Code Configuration:**
- `.vscode/tasks.json` - Build and test tasks for individual projects and entire solution
- `.vscode/launch.json` - Debug configurations for server and client applications
- `.vscode/settings.json` - Project-specific settings
- `.vscode/extensions.json` - Recommended extensions for .NET development

**Available VS Code Tasks:**
- `build` - Build entire solution (default)
- `build-server` - Build server project only
- `build-client` - Build client project only  
- `test` - Run all tests (default)
- `test-server` - Run server tests only
- `test-client` - Run client tests only

**Usage in VS Code:**
1. Open the repository folder in VS Code
2. Install recommended extensions when prompted
3. Use `Ctrl+Shift+P` (or `Cmd+Shift+P` on Mac) to access tasks:
   - Type "Tasks: Run Task" to see available build/test tasks
   - Type "Debug: Start Debugging" to launch debug configurations

### Testing and Validation

The project includes comprehensive testing infrastructure:

**Unit Tests:**
```bash
# Run all tests
dotnet test

# Run tests with verbose output
dotnet test --verbosity normal

# Run specific test categories
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"
```

**Integration Testing:**
The repository includes a test script for validating serial port abstraction:

```bash
# Run serial abstraction integration tests
./test_serial_abstraction.sh
```

This script validates:
- Radio model enumeration and information
- DummyRadio with FakeSerialPort functionality
- CAT command simulation and responses
- SerialPortFactory functionality

**Manual Testing:**
```bash
# Test with simulated radio (no hardware required)
dotnet run --project SharpCAT2.Console -- --port FAKE --radio "SharpCAT2 DummyRadio"

# Test radio command responses
ID;    # Should return: ID999;
FA;    # Should return: FA00014074000;
s      # Should show detailed radio status
```

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test on multiple platforms if possible
5. Submit a pull request

## Documentation

- **[CLIENT_WEBAPP.md](CLIENT_WEBAPP.md)**: Comprehensive guide for the SharpCAT2 Web Client including setup, configuration, usage, and development
- **[SUPPORTED_RADIOS.md](SUPPORTED_RADIOS.md)**: Complete list of supported radio models and their features
- **[DI_ARCHITECTURE.md](DI_ARCHITECTURE.md)**: Dependency injection and service architecture documentation
- **[SERIAL_ABSTRACTION.md](SERIAL_ABSTRACTION.md)**: Serial port abstraction and testing infrastructure

## License

This project is licensed under the terms specified in the LICENSE file.

## Support

For issues and questions:
1. Check the troubleshooting section above
2. Review existing issues on GitHub
3. Create a new issue with platform details and error messages