# Serial Port Abstraction Refactoring

This document describes the serial port abstraction refactoring implemented in SharpCAT2.

## Overview

The codebase has been refactored to separate serial port handling into its own module, providing clean abstraction and improved testability.

## Architecture

### New Module: `SharpCAT2.Radio.Serial`

Located in `SharpCAT2.Radio/Serial/`, this module contains:

#### Core Interface
- **`ISerialPort`** - Abstraction for serial port communication with methods:
  - Connection management: `Open()`, `Close()`, `IsOpen`
  - Data communication: `Write()`, `WriteLine()`, `ReadExisting()`, `Read()`
  - Buffer management: `DiscardInBuffer()`, `DiscardOutBuffer()`
  - Events: `DataReceived` event

#### Implementations
- **`RealSerialPort`** - Wrapper around `System.IO.Ports.SerialPort` for actual hardware
- **`FakeSerialPort`** - Protocol-agnostic simulated serial port for testing (no radio knowledge)

**Important**: `FakeSerialPort` provides only serial I/O simulation and contains no knowledge of radio commands, protocols, or logging. All radio simulation logic is implemented in radio classes like `DummyRadio`.

#### Factory
- **`SerialPortFactory`** - Creates appropriate implementations:
  - `CreateRealSerialPort()` - For hardware communication
  - `CreateFakeSerialPort()` - For testing/simulation (protocol-agnostic transport)
  - `CreateSerialPort()` - Auto-detects based on port name

## Key Changes

### Radio Interface Updates
- `IRadio.ConnectAsync()` now takes `ISerialPort` instead of `SerialPort`
- `BaseRadio` uses `ISerialPort` for all serial communication
- `RadioFactory.AutoDetectRadioAsync()` uses `ISerialPort`

### DummyRadio Refactoring
- **Separated concerns**: Now contains all radio simulation logic and state management
- **Protocol implementation**: Handles CAT command processing, frequency/mode/VFO control
- **Transport usage**: Uses FakeSerialPort purely as a communication transport
- **Comprehensive simulation**: Supports 25+ radio features including RIT/XIT, split operation, power control, S-meter readings, etc.
- **Clean architecture**: Radio logic is separate from transport logic

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
// Create fake port for testing (protocol-agnostic transport)
ISerialPort fakePort = SerialPortFactory.CreateFakeSerialPort();
IRadio radio = RadioFactory.CreateRadio("SharpCAT2", "DummyRadio");
await radio.ConnectAsync(fakePort);

// Radio handles all simulation logic, transport provides communication
var frequency = await radio.GetFrequencyAsync(); // DummyRadio processes this
await radio.SetFrequencyAsync(14074000); // DummyRadio manages state

// Create real port for hardware
ISerialPort realPort = SerialPortFactory.CreateRealSerialPort("COM1", 9600);
IRadio radio = RadioFactory.CreateRadio("Kenwood", "TS-2000");  
await radio.ConnectAsync(realPort);
```

## Benefits

1. **Separation of Concerns**: Serial port logic is isolated from radio control logic
2. **Protocol Agnostic**: FakeSerialPort contains no radio or protocol knowledge
3. **Testability**: Easy to test radio implementations without hardware and test transport independently
4. **Maintainability**: Serial port implementations can be modified independently  
5. **Extensibility**: Easy to add new serial port types (e.g., network, Bluetooth)
6. **Clean Architecture**: Transport layer separate from protocol/radio simulation layer
7. **Backward Compatibility**: Existing code works unchanged

## Architecture Guidelines

### FakeSerialPort Design Principles
- **Protocol Agnostic**: Contains no knowledge of radio commands, CAT protocols, or radio state
- **Transport Only**: Provides serial I/O simulation (open/close, read/write, buffering)
- **No Logging**: Contains no Console.WriteLine or logging statements
- **Test Support**: Provides data injection methods for controlled testing
- **Clean Interface**: Implements ISerialPort without protocol assumptions

### Radio Simulation Design Principles
- **Complete Logic**: Radio classes (like DummyRadio) contain all protocol and simulation logic
- **Transport Usage**: Use serial ports purely for communication transport
- **State Management**: Maintain radio state (frequency, mode, VFO, etc.) at the radio level
- **Feature Implementation**: Implement radio features through command processing, not transport simulation

### Usage Examples

#### Testing with FakeSerialPort (Protocol-Agnostic)
```csharp
// Create a protocol-agnostic fake port for testing
var fakePort = new FakeSerialPort("TEST", 9600);
fakePort.Open();

// Write data (stored for higher layers to process)
fakePort.Write("SOME_COMMAND");

// Inject response data for controlled testing
fakePort.InjectResponseData("EXPECTED_RESPONSE");

// Read injected response
var response = fakePort.ReadExisting(); // Returns "EXPECTED_RESPONSE"

// Verify what was written
var writtenData = fakePort.GetWrittenData(); // Returns "SOME_COMMAND"
```

#### Radio Simulation with DummyRadio
```csharp
// Create radio with simulation logic
var radio = new DummyRadio();
var fakePort = new FakeSerialPort("TEST", 9600);

// Connect radio (radio manages all CAT simulation)
await radio.ConnectAsync(fakePort);

// Send radio commands (processed by radio, not transport)
var command = new RadioCommand("FA;", "Get Frequency");
var response = await radio.SendCommandAsync(command); // Returns "FA00014074000;"

// Use radio features
await radio.SetFrequencyAsync(7074000);
var frequency = await radio.GetFrequencyAsync(); // Returns 7074000

// Radio maintains state, not the transport
var vfo = await radio.GetVfoAsync(); // Returns "A" or "B"
```

## Testing

The refactoring includes comprehensive testing to ensure proper separation of concerns:

### Transport Layer Testing
- **FakeSerialPort Tests**: Verify protocol-agnostic transport functionality
- **Data Injection**: Test controlled response injection for higher layers
- **Buffer Management**: Verify input/output buffer handling
- **No Protocol Knowledge**: Ensure transport contains no radio logic

### Radio Layer Testing  
- **DummyRadio Tests**: Verify comprehensive radio simulation
- **CAT Command Processing**: Test frequency, mode, VFO, split operations
- **State Management**: Verify radio maintains proper internal state
- **Feature Implementation**: Test 25+ radio features (RIT/XIT, power control, etc.)

### Integration Testing
- **Combined Testing**: Radio + Transport integration
- **Real vs Fake**: Same radio code works with both transport types
- **Command Flow**: End-to-end command processing verification

Run the test script:
```bash
./test_serial_abstraction.sh

# Or run specific test categories
dotnet test --filter "FakeSerialPortRefactoredTests"  # Transport tests
dotnet test --filter "DummyRadioRefactoredTests"      # Radio tests
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