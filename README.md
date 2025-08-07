# SharpCAT2

A cross-platform .NET serial port communication server application that provides robust error handling and platform-specific guidance.

## Features

- **Cross-Platform Support**: Works on Windows, Linux, and macOS
- **Platform-Specific Guidance**: Automatic detection of operating system with appropriate port naming conventions
- **Robust Error Handling**: Comprehensive error messages and troubleshooting guidance
- **Interactive Port Selection**: Smart port discovery and selection
- **Command-Line Interface**: Flexible command-line options for automation and scripting
- **TCP Server**: Remote client access via TCP connections
- **Client Library**: .NET library for programmatic access to remote serial ports
- **Client Console App**: Interactive console application for remote serial communication
- **Radio Support**: Built-in support for popular amateur radio models with CAT (Computer Aided Transceiver) control
- **Auto-Detection**: Automatic radio type detection and configuration
- **Extensible Architecture**: Plugin-style radio model support through the SharpCAT2.Radio namespace
- **Graceful Error Handling**: Both server and client handle connection and communication errors without crashing

## Requirements

- .NET 8.0 or later
- Appropriate permissions for serial port access (see Platform-Specific Setup below)

## Building and Running

### Build the Application

```bash
# Clone the repository
git clone https://github.com/ekinnee/SharpCAT2.git
cd SharpCAT2/Server

# Build the application
dotnet build

# Run the application
dotnet run
```

### Build for Release

```bash
# Build optimized release version
dotnet build --configuration Release

# Run release version
dotnet run --configuration Release
```

## Usage

### Server Application

The server application opens a serial port and provides both console interface and TCP server for remote clients.

#### Basic Usage

```bash
# Show help
dotnet run -- --help

# List available serial ports
dotnet run -- --list

# Connect to a specific port with default settings and default TCP port (8080)
dotnet run -- --port COM1              # Windows
dotnet run -- --port /dev/ttyUSB0      # Linux
dotnet run -- --port /dev/cu.usbserial-1410  # macOS

# Connect with custom baud rate and TCP port
dotnet run -- --port COM1 --baud 115200 --tcp-port 9090

# Connect with radio support
dotnet run -- --port COM1 --radio "Kenwood TS-2000"

# Auto-detect radio type
dotnet run -- --port COM1 --auto-detect
```

#### Command-Line Options

| Option | Short | Description | Example |
|--------|-------|-------------|---------|
| `--port` | `-p` | Serial port name | `--port COM1` |
| `--baud` | `-b` | Baud rate (default: 9600)<br/>Supported rates: 9600, 14400, 19200, 28800, 38400, 57600, 115200, 128000, 256000 | `--baud 115200` |
| `--tcp-port` | `-t` | TCP server port (default: 8080) | `--tcp-port 9090` |
| `--radio` | `-r` | Radio model name | `--radio "Kenwood TS-2000"` |
| `--auto-detect` | | Auto-detect radio type | `--auto-detect` |
| `--list` | `-l` | List available ports | `--list` |
| `--list-radios` | | List available radio models | `--list-radios` |
| `--radio-info` | | Show detailed information about a radio model | `--radio-info "Elecraft K3"` |
| `--help` | `-h` | Show help message | `--help` |

#### Supported Radio Models

SharpCAT2 now supports **28 radio models** across **7 major manufacturers**:

- **Kenwood**: TS-2000, TS-890S, TS-590SG, TH-D74A, TM-D710GA
- **Elecraft**: K3, K4, KX3, K2, K1  
- **Yaesu**: FT-991A, FT-710, FT-DX101D, FT-891, FT-65
- **Icom**: IC-7300, IC-9700
- **FlexRadio**: FLEX-6400, FLEX-6600, FLEX-6700
- **Alinco**: DX-SR8T, DJ-MD5TGP, DR-638T, DX-70T
- **Ten-Tec**: OMNI VII, Eagle, Argonaut V, Jupiter

Each radio model supports a specific set of features from a comprehensive list of **47 radio capabilities** including:
- Frequency and mode control
- VFO operations (dual VFO, swap, split)
- RIT/XIT incremental tuning
- Power output and metering (S-meter, SWR)
- Memory channel management
- CW keyer operation
- Noise reduction and DSP features
- Antenna selection
- Digital mode support

For detailed information about supported features for each radio model, see [SUPPORTED_RADIOS.md](SUPPORTED_RADIOS.md).

To see all available radio models with feature counts:
```bash
dotnet run -- --list-radios
```

To see detailed information about a specific radio:
```bash
dotnet run -- --radio-info "Elecraft K3"
dotnet run -- --radio-info "FlexRadio FLEX-6600"
```

#### Radio Commands

When a radio is connected, you can use these additional commands in the server console:

- `s` - Show current radio status (frequency, mode, supported features, etc.)
- `FA;` - Get frequency (VFO A)
- `FA14074000;` - Set frequency to 14.074 MHz
- `MD;` - Get operating mode
- `MD2;` - Set mode to USB
- `ID;` - Get radio identification

#### Radio Features and Capabilities

Each radio model exposes a `SupportedFeatures` property that indicates which advanced features are available. The server will display this information when showing radio status or using the `--radio-info` command.

**Feature Categories:**
- **Basic Operation**: Frequency control, mode selection, transmit/receive status
- **HF Operation**: Dual VFO, split operation, RIT/XIT, S-meter, power control
- **VHF/UHF Operation**: Squelch control, CTCSS/DCS tones, repeater operation
- **Advanced Operation**: IF bandwidth, noise reduction, memory channels, CW keyer
- **SDR Features**: Waterfall display, panadapter, multiple receivers

The system gracefully handles unsupported features by returning appropriate defaults or error messages.

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

#### Client Command-Line Options

| Option | Short | Description | Example |
|--------|-------|-------------|---------|
| `--host` | `-h` | Server hostname or IP (default: localhost) | `--host 192.168.1.100` |
| `--port` | `-p` | Server TCP port (default: 8080) | `--port 9090` |
| `--help` | | Show help message | `--help` |

#### Client Library Usage

```csharp
using SharpCAT2.ClientLib;

// Create client instance
using var client = new SharpCAT2Client("localhost", 8080);

// Connect to server
bool connected = await client.ConnectAsync();
if (connected)
{
    // Send command and get response
    string? response = await client.SendCommandAsync("AT");
    if (response != null)
    {
        Console.WriteLine($"Response: {response}");
    }
}
```

### Interactive Mode

#### Server Interactive Mode

When no port is specified, the server application will:
1. Scan for available ports
2. Display found ports for selection
3. Allow manual port name entry if needed
4. Start TCP server and console interface

```bash
# Interactive mode - will prompt for port selection
dotnet run
```


#### Client Interactive Mode

The client application provides an interactive command loop:
- Type commands to send to the remote serial port/radio
- Type `help` for available client commands
- Type `status` to check connection status
- Type `radio-status` or `rs` to get radio status
- Type `list-radios` or `radios` to see available radio models
- Type `quit` or `exit` to disconnect and exit

#### Client Radio Commands

When connected to a server with radio support, you can send these commands:
- `FA;` - Get frequency
- `FA14074000;` - Set frequency to 14.074 MHz
- `MD;` - Get mode
- `ID;` - Get radio ID

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
├── Server/
│   ├── Program.cs          # Server application with serial port and TCP functionality
│   ├── Server.csproj       # Server project file with dependencies
│   └── bin/Debug/          # Build output
├── Client/
│   ├── ClientLib.cs        # Client library implementation
│   ├── Program.cs          # Client console application
│   ├── SharpCAT2.ClientLib.csproj  # Client library project file
│   ├── Client.csproj       # Client console app project file
│   └── bin/Debug/          # Build output
├── SharpCAT2.Radio/        # Radio support library
│   ├── IRadio.cs           # Radio interface definition with SupportedFeatures
│   ├── SupportedFeatures.cs # Comprehensive feature enumeration
│   ├── RadioCommand.cs     # Radio command abstraction
│   ├── RadioStatus.cs      # Radio status information
│   ├── RadioFactory.cs     # Dynamic radio creation and discovery
│   ├── Models/             # Radio model implementations by brand
│   │   ├── BaseRadio.cs    # Abstract base radio implementation
│   │   ├── Kenwood/        # Kenwood radio models (TS-2000, TS-890S, etc.)
│   │   ├── Elecraft/       # Elecraft radio models (K3, K4, KX3, etc.)
│   │   ├── Yaesu/          # Yaesu radio models (FT-991A, FT-710, etc.)
│   │   ├── Icom/           # Icom radio models (IC-7300, IC-9700)
│   │   ├── FlexRadio/      # FlexRadio models (FLEX-6400, 6600, 6700)
│   │   ├── Alinco/         # Alinco radio models (DX-SR8T, DJ-MD5TGP, etc.)
│   │   └── TenTec/         # Ten-Tec radio models (OMNI VII, Eagle, etc.)
│   ├── SharpCAT2.Radio.csproj # Radio library project file
```

### Dependencies

- **System.IO.Ports**: Cross-platform serial port communication
- **.NET 8.0**: Runtime platform
- **SharpCAT2.Radio**: Radio control and CAT interface library

### Radio Architecture

The radio support is implemented through the `SharpCAT2.Radio` namespace which provides:

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
cd Server
dotnet restore
dotnet build

# Run tests (if any)
dotnet test

# Create release package
dotnet publish -c Release -o ./publish
```

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test on multiple platforms if possible
5. Submit a pull request

## License

This project is licensed under the terms specified in the LICENSE file.

## Support

For issues and questions:
1. Check the troubleshooting section above
2. Review existing issues on GitHub
3. Create a new issue with platform details and error messages