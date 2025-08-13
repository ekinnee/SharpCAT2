# Serial Port Abstraction Refactoring

This document describes the serial port abstraction refactoring implemented in SharpCAT2.

## Overview

The codebase has been refactored to separate serial port handling into its own module, providing clean abstraction and improved testability.

## Architecture

### New Module: `SharpCAT2.Common.Serial`

Located in `SharpCAT2.Common/Serial/`, this module contains:

#### Core Interface
- **`ISerialPort`** - Abstraction for serial port communication with methods:
  - Connection management: `Open()`, `Close()`, `IsOpen`
  - Data communication: `Write()`, `WriteLine()`, `ReadExisting()`, `Read()`
  - Buffer management: `DiscardInBuffer()`, `DiscardOutBuffer()`
  - Events: `DataReceived` event

#### Implementations
- **`RealSerialPort`** - Wrapper around `System.IO.Ports.SerialPort` for actual hardware
- **`FakeSerialPort`** - Simulated serial port for testing with CAT command responses

#### Factory
- **`SerialPortFactory`** - Creates appropriate implementations:
  - `CreateRealSerialPort()` - For hardware communication
  - `CreateFakeSerialPort()` - For testing/simulation
  - `CreateSerialPort()` - Auto-detects based on port name

## Key Changes

### Radio Interface Updates
- `IRadio.ConnectAsync()` now takes `ISerialPort` instead of `SerialPort`
- `BaseRadio` uses `ISerialPort` for all serial communication
- `RadioFactory.AutoDetectRadioAsync()` uses `ISerialPort`

### DummyRadio Refactoring
- Removed embedded simulation logic from `DummyRadio`
- Now delegates to `FakeSerialPort` for command simulation
- Automatically creates `FakeSerialPort` when needed
- Maintains full feature simulation through serial abstraction

### Server Application Updates
- Uses `SerialPortFactory.CreateSerialPort()` for port creation
- Supports fake ports with names: FAKE, DUMMY, TEST, SIMULATION
- Maintains event handling through `ISerialPort.DataReceived`

## Usage Examples

### Using Fake Serial Port for Testing
```bash
# Run server with simulated radio
dotnet run -- --port FAKE --radio "SharpCAT2 DummyRadio"

# Test CAT commands
ID;     # Returns: ID999;
FA;     # Returns: FA00014074000;
s       # Shows radio status
```

### Using Real Serial Port
```bash
# Run with actual hardware (unchanged from before)
dotnet run -- --port COM1 --radio "Kenwood TS-2000"
dotnet run -- --port /dev/ttyUSB0 --auto-detect
```

### Programmatic Usage
```csharp
// Create fake port for testing
ISerialPort fakePort = SerialPortFactory.CreateFakeSerialPort();
IRadio radio = RadioFactory.CreateRadio("SharpCAT2", "DummyRadio");
await radio.ConnectAsync(fakePort);

// Create real port for hardware
ISerialPort realPort = SerialPortFactory.CreateRealSerialPort("COM1", 9600);
IRadio radio = RadioFactory.CreateRadio("Kenwood", "TS-2000");  
await radio.ConnectAsync(realPort);
```

## Benefits

1. **Separation of Concerns**: Serial port logic is isolated from radio control logic
2. **Testability**: Easy to test radio implementations without hardware
3. **Maintainability**: Serial port implementations can be modified independently  
4. **Extensibility**: Easy to add new serial port types (e.g., network, Bluetooth)
5. **Backward Compatibility**: Existing code works unchanged

## Testing

The refactoring includes comprehensive testing:
- Build verification ensures no regressions
- Functional testing with DummyRadio and FakeSerialPort
- CAT command simulation verification
- Real serial port compatibility testing

Run the test script:
```bash
./test_serial_abstraction.sh
```

## Migration Guide

For developers extending SharpCAT2:

### Before (old approach)
```csharp
public async Task<bool> ConnectAsync(SerialPort port)
{
    _serialPort = port;
    // Direct SerialPort usage
}
```

### After (new approach)  
```csharp
public async Task<bool> ConnectAsync(ISerialPort port)
{
    _serialPort = port;
    // Same usage, now abstracted
}
```

The migration is largely transparent - the `ISerialPort` interface provides the same methods as `SerialPort`.