# SharpCAT2

A cross-platform .NET serial port communication server application that provides robust error handling and platform-specific guidance.

## Features

- **Cross-Platform Support**: Works on Windows, Linux, and macOS
- **Platform-Specific Guidance**: Automatic detection of operating system with appropriate port naming conventions
- **Robust Error Handling**: Comprehensive error messages and troubleshooting guidance
- **Interactive Port Selection**: Smart port discovery and selection
- **Command-Line Interface**: Flexible command-line options for automation and scripting

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

### Basic Usage

```bash
# Show help
dotnet run -- --help

# List available serial ports
dotnet run -- --list

# Connect to a specific port with default settings
dotnet run -- --port COM1              # Windows
dotnet run -- --port /dev/ttyUSB0      # Linux
dotnet run -- --port /dev/cu.usbserial-1410  # macOS

# Connect with custom baud rate
dotnet run -- --port COM1 --baud 115200
```

### Command-Line Options

| Option | Short | Description | Example |
|--------|-------|-------------|---------|
| `--port` | `-p` | Serial port name | `--port COM1` |
| `--baud` | `-b` | Baud rate (default: 9600) | `--baud 115200` |
| `--list` | `-l` | List available ports | `--list` |
| `--help` | `-h` | Show help message | `--help` |

### Interactive Mode

When no port is specified, the application will:
1. Scan for available ports
2. Display found ports for selection
3. Allow manual port name entry if needed

```bash
# Interactive mode - will prompt for port selection
dotnet run
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
│   ├── Program.cs          # Main application logic
│   ├── Server.csproj       # Project file with dependencies
│   └── bin/Debug/          # Build output
├── README.md               # This file
└── LICENSE                 # License information
```

### Dependencies

- **System.IO.Ports**: Cross-platform serial port communication
- **.NET 8.0**: Runtime platform

### Building from Source

```bash
# Clone repository
git clone https://github.com/ekinnee/SharpCAT2.git
cd SharpCAT2/Server

# Restore dependencies
dotnet restore

# Build
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