# SharpCAT2

A cross-platform .NET radio control application that provides CAT (Computer Aided Transceiver) and CI-V protocol support for amateur radio equipment.

## Features

- **Cross-Platform Support**: Works on Windows, Linux, and macOS
- **Multiple Radio Protocols**: Supports CI-V (Icom), CAT (Yaesu/Kenwood), K3/KX (Elecraft), and Smart CAT (FlexRadio)
- **Wide Radio Support**: Compatible with radios from Icom, Yaesu, Kenwood, Elecraft, and FlexRadio
- **Dual Operation Modes**: 
  - Radio abstraction mode for high-level radio control
  - Raw serial mode for direct protocol communication
- **Platform-Specific Guidance**: Automatic detection of operating system with appropriate port naming conventions
- **Robust Error Handling**: Comprehensive error messages and troubleshooting guidance
- **Interactive Operation**: Command-line interfaces for both server and client applications

## Supported Radio Manufacturers

- **Icom**: CI-V protocol support for models like IC-7300, IC-7600, IC-7700, IC-9100, and many others
- **Yaesu**: CAT protocol support for FT-991A, FT-DX series, FT-857D, FT-897D, and more
- **Kenwood**: CAT protocol support for TS-590, TS-2000, TS-480 series, and others
- **Elecraft**: K3/KX protocol support for K2, K3, K3S, KX2, KX3, K4
- **FlexRadio**: Smart CAT protocol support for FLEX-6000 series and others

## Architecture

The application is structured into multiple components:

- **SharpCAT2.Radio**: Core radio abstraction library containing interfaces and protocol implementations
- **Server**: Multi-mode server application supporting both radio abstraction and raw serial communication
- **Client**: Interactive client application for radio control and testing

## Requirements

- .NET 8.0 or later
- Appropriate permissions for serial port access (see Platform-Specific Setup below)

## Building and Running

### Build the Solution

```bash
# Clone the repository
git clone https://github.com/ekinnee/SharpCAT2.git
cd SharpCAT2

# Build the entire solution
dotnet build

# Or build individual components
dotnet build SharpCAT2.Radio/
dotnet build Server/
dotnet build SharpCAT2.Client/
```

### Run Applications

```bash
# Run server in radio mode
dotnet run --project Server -- --manufacturer Icom --model IC-7300 --port COM1

# Run server in raw serial mode
dotnet run --project Server -- --raw --port COM1

# Run interactive client
dotnet run --project SharpCAT2.Client -- --manufacturer Yaesu --model FT-991A --port /dev/ttyUSB0
```

## Usage

### Server Application

The server supports two primary modes:

#### Radio Abstraction Mode
Control radios using high-level commands with automatic protocol handling:

```bash
# List supported radios
dotnet run --project Server -- --list-radios

# Connect to an Icom IC-7300
dotnet run --project Server -- --manufacturer Icom --model IC-7300 --port COM1 --baud 115200

# Connect to a Yaesu FT-991A
dotnet run --project Server -- -m Yaesu -r FT-991A -p /dev/ttyUSB0 -b 4800

# Interactive mode (prompts for radio selection)
dotnet run --project Server
```

#### Raw Serial Mode
Direct serial communication without radio abstraction:

```bash
# Force raw mode
dotnet run --project Server -- --raw --port COM1 --baud 9600

# Default to raw mode when no radio specified
dotnet run --project Server -- --port /dev/ttyUSB0
```

### Client Application

Interactive radio control client:

```bash
# Connect to a radio with prompts
dotnet run --project SharpCAT2.Client

# Direct connection
dotnet run --project SharpCAT2.Client -- -m Icom -r IC-7300 -p COM1 -b 115200

# List supported radios
dotnet run --project SharpCAT2.Client -- --list
```

### Available Commands in Radio Mode

When connected to a radio (both Server and Client), you can use:

- `freq` - Get current frequency
- `freq <Hz>` - Set frequency (e.g., `freq 14230000`)
- `mode` - Get current operating mode
- `mode <MODE>` - Set mode (LSB, USB, CW, FM, AM, Digital)
- `help` - Show available commands
- `quit` - Exit application

### Command-Line Options

#### Server Options
| Option | Description | Example |
|--------|-------------|---------|
| `-m, --manufacturer` | Radio manufacturer | `--manufacturer Icom` |
| `-r, --model` | Radio model | `--model IC-7300` |
| `-p, --port` | Serial port name | `--port COM1` |
| `-b, --baud` | Baud rate | `--baud 115200` |
| `--list-radios` | List supported radios | `--list-radios` |
| `-l, --list` | List available ports | `--list` |
| `--raw` | Force raw serial mode | `--raw` |
| `-h, --help` | Show help | `--help` |

#### Client Options
| Option | Description | Example |
|--------|-------------|---------|
| `-m, --manufacturer` | Radio manufacturer | `--manufacturer Yaesu` |
| `-r, --model` | Radio model | `--model FT-991A` |
| `-p, --port` | Serial port name | `--port /dev/ttyUSB0` |
| `-b, --baud` | Baud rate | `--baud 4800` |
| `-l, --list` | List supported radios | `--list` |
| `-h, --help` | Show help | `--help` |

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
├── SharpCAT2.Radio/              # Core radio abstraction library
│   ├── IRadio.cs                 # Main radio interface
│   ├── RadioBase.cs              # Base implementation
│   ├── RadioCommand.cs           # Command and response structures
│   ├── RadioFactory.cs           # Dynamic radio creation
│   └── Models/                   # Radio implementations
│       ├── IcomRadio.cs          # Icom CI-V protocol
│       ├── YaesuRadio.cs         # Yaesu CAT protocol  
│       ├── KenwoodRadio.cs       # Kenwood CAT protocol
│       ├── ElecraftRadio.cs      # Elecraft K3/KX protocol
│       └── FlexRadio.cs          # FlexRadio Smart CAT protocol
├── Server/                       # Server application
│   ├── Program.cs                # Main server logic
│   └── Server.csproj             # Server project file
├── SharpCAT2.Client/             # Client application
│   ├── Program.cs                # Interactive client
│   └── SharpCAT2.Client.csproj   # Client project file
├── SharpCAT2.sln                 # Solution file
├── README.md                     # This file
└── LICENSE                       # License information
```

### Dependencies

- **System.IO.Ports**: Cross-platform serial port communication
- **.NET 8.0**: Runtime platform
- **SharpCAT2.Radio**: Radio abstraction library (internal dependency)

### Building from Source

```bash
# Clone repository
git clone https://github.com/ekinnee/SharpCAT2.git
cd SharpCAT2

# Restore dependencies
dotnet restore

# Build all projects
dotnet build

# Run tests (if any)
dotnet test

# Create release packages
dotnet publish Server -c Release -o ./publish/Server
dotnet publish SharpCAT2.Client -c Release -o ./publish/Client
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