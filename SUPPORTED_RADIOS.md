# Supported Radio Models

SharpCAT2 supports a wide variety of amateur radio transceivers from major manufacturers. Each radio model implements the `IRadio` interface and provides a `SupportedFeatures` property that enumerates the specific capabilities available.

## Overview

- **Total Models**: 29 radio models across 7 major manufacturers
- **Feature Coverage**: 47 different radio capabilities supported
- **Architecture**: Extensible plugin-style radio support through inheritance
- **Compatibility**: Backward compatible with existing implementations

## Radio Manufacturers

### Kenwood (5 Models)

| Model | Features | Description |
|-------|----------|-------------|
| **TS-2000** | 15 | Multi-band HF/VHF/UHF transceiver with satellite capability |
| **TS-890S** | 30 | High-end HF transceiver with advanced DSP |
| **TS-590SG** | 16 | Popular HF transceiver with excellent performance |
| **TH-D74A** | 16 | VHF/UHF handheld with APRS and D-STAR |
| **TM-D710GA** | 16 | Dual-band mobile with APRS capability |

**Kenwood Features**: Comprehensive CAT command support, dual VFO operation, split mode, RIT/XIT, memory channels, power control.

### Elecraft (5 Models)

| Model | Features | Description |
|-------|----------|-------------|
| **K3** | 20 | High-performance HF transceiver with advanced features |
| **K4** | 35 | Latest generation transceiver with dual receive |
| **KX3** | 18 | Portable QRP transceiver for field operations |
| **K2** | 13 | Classic kit-built HF transceiver |
| **K1** | 8 | Ultra-portable QRP CW-only transceiver |

**Elecraft Features**: Superior weak-signal performance, comprehensive computer control, built-in antenna tuners, advanced DSP noise reduction.

### Yaesu (5 Models)

| Model | Features | Description |
|-------|----------|-------------|
| **FT-991A** | 11 | All-mode HF/VHF/UHF transceiver with digital modes |
| **FT-710** | 18 | Modern HF/6m transceiver with color display |
| **FT-DX101D** | 25 | High-end contest transceiver with advanced features |
| **FT-891** | 16 | Compact HF/6m mobile transceiver |
| **FT-65** | 15 | Affordable VHF/UHF handheld |

**Yaesu Features**: Digital mode support, touchscreen interfaces, built-in antenna tuners, advanced filtering.

### Icom (2 Models)

| Model | Features | Description |
|-------|----------|-------------|
| **IC-7300** | 25 | Direct-sampling SDR HF transceiver with waterfall |
| **IC-9700** | 22 | VHF/UHF/SHF SDR transceiver with D-STAR |

**Icom Features**: Direct-sampling SDR technology, real-time waterfall and spectrum scope, CI-V computer control protocol, D-STAR digital mode support.

### FlexRadio (3 Models)

| Model | Features | Description |
|-------|----------|-------------|
| **FLEX-6400** | 34 | Software Defined Radio with advanced features |
| **FLEX-6600** | 47 | High-end SDR with full feature support |
| **FLEX-6700** | 47 | Top-tier SDR for contest and DX stations |

**FlexRadio Features**: Software Defined Radio technology, multiple slice receivers, VITA-49 protocol, real-time spectrum and waterfall, remote operation capability.

### Alinco (4 Models)

| Model | Features | Description |
|-------|----------|-------------|
| **DX-SR8T** | 14 | HF/6m transceiver with basic features |
| **DJ-MD5TGP** | 16 | VHF/UHF handheld with DMR digital mode |
| **DR-638T** | 16 | Dual-band VHF/UHF mobile transceiver |
| **DX-70T** | 12 | VHF/UHF all-mode base station |

**Alinco Features**: Cost-effective solutions, reliable performance, DMR digital mode support, dual-band operation.

### Ten-Tec (4 Models)

| Model | Features | Description |
|-------|----------|-------------|
| **OMNI VII** | 18 | SDR-based HF transceiver with advanced DSP |
| **Eagle** | 22 | Premium HF transceiver with superior build quality |
| **Argonaut V** | 14 | QRP HF transceiver with built-in tuner |
| **Jupiter** | 16 | Popular HF transceiver with DSP technology |

**Ten-Tec Features**: American-made quality, advanced DSP processing, excellent receiver performance, QRP (low power) operation.

## Feature Categories

### Basic Operation (All Radios)
- Frequency Control
- Mode Control  
- Transmit/Receive Status
- Radio Identification

### HF Operation (Most HF Radios)
- Dual VFO Support
- VFO Swap/Equal
- Split Operation
- RIT/XIT (Incremental Tuning)
- S-Meter Reading
- Power Output Control

### VHF/UHF Operation (VHF/UHF Radios)
- Squelch Control
- CTCSS/DCS Tone Control
- Repeater Offset
- Memory Scanning

### Advanced Operation (High-End Radios)
- IF Bandwidth Control
- Noise Reduction/Filtering
- AGC Control
- Memory Channel Management
- CW Keyer Operation
- Antenna Selection
- SWR Monitoring

### Software Defined Radio Features
- Waterfall Display
- Panadapter
- Multiple Receivers
- Advanced DSP

## Implementation Status

### ✅ Fully Implemented
- Core interface and base classes
- SupportedFeatures enumeration
- Radio factory and auto-detection
- Server integration with feature reporting

### 🚧 Partial Implementation (TODOs)
- Brand-specific CAT protocol implementations
- Advanced feature method implementations
- CI-V protocol for Icom radios
- VITA-49 protocol for FlexRadio
- DMR support for Alinco

### 📋 Template Available
- Each radio model includes template methods
- TODOs marked for specific implementations
- Inheritance from BaseRadio provides defaults
- Feature flags prevent unsupported operations

## Usage Examples

### List All Available Radios
```bash
dotnet run -- --list-radios
```

### Show Detailed Radio Information
```bash
dotnet run -- --radio-info "Elecraft K3"
dotnet run -- --radio-info "FlexRadio FLEX-6600"
```

### Connect to a Specific Radio
```bash
dotnet run -- --port COM1 --radio "Kenwood TS-2000"
dotnet run -- --port /dev/ttyUSB0 --auto-detect
```

## API Reference

SharpCAT2 provides unified resource listing APIs for clients to access available radios and serial ports through the TCP protocol.

### Protocol Commands

#### LIST_RADIOS
Available via ClientLibrary and ClientConsole for retrieving radio models through TCP protocol.

**ClientLibrary Usage:**
```csharp
var client = new SharpCAT2Client("localhost", 8080);
await client.ConnectAsync();
string radioList = await client.GetAvailableRadiosAsync();
```

**ClientConsole Usage:**
```bash
list-radios
radios          # Alternative command
```

#### LIST_SERIALPORTS
Available via ClientLibrary and ClientConsole for retrieving serial ports through TCP protocol.

**ClientLibrary Usage:**
```csharp
var client = new SharpCAT2Client("localhost", 8080);
await client.ConnectAsync();
string portList = await client.GetAvailableSerialPortsAsync();
```

**ClientConsole Usage:**
```bash
list-serialports
serialports     # Alternative command
```

### Serial Port Support

- **Real Ports**: Automatically detected system serial ports (COM1, /dev/ttyUSB0, etc.)
- **Test Port**: Only 'FAKE' is supported as a fake/simulation port
- **Removed**: DUMMY, TEST, and SIMULATION ports are no longer exposed

## Extension Guide

### Adding New Radio Models

1. **Create Radio Class**
   ```csharp
   public class NewRadio : BaseRadio
   {
       public override string ModelName => "Model-Name";
       public override string Manufacturer => "Manufacturer";
       public override SupportedFeatures SupportedFeatures => /* define features */;
   }
   ```

2. **Register in RadioFactory**
   ```csharp
   RegisterRadio<NewRadio>();
   ```

3. **Add Auto-Detection Pattern**
   ```csharp
   if (response.Contains("MODEL-ID"))
       return CreateRadio("Manufacturer", "Model-Name");
   ```

4. **Override Feature Methods**
   ```csharp
   public override async Task<bool> SetFrequencyAsync(long frequency)
   {
       // Implement radio-specific frequency setting
   }
   ```

### Feature Implementation Patterns

- **Check Feature Support**: Always verify `SupportedFeatures.HasFeature()` before operation
- **Graceful Degradation**: Return sensible defaults for unsupported features  
- **Protocol Compliance**: Follow manufacturer CAT command specifications
- **Error Handling**: Provide meaningful error messages and logging

## Contributing

When adding support for new radio models:

1. Research the CAT command protocol documentation
2. Create a reference implementation following existing patterns
3. Include comprehensive feature definitions
4. Add auto-detection patterns where possible
5. Document any partial implementations with TODOs
6. Test with actual hardware when available

## Hamlib Compatibility

This implementation is designed to be compatible with Hamlib-supported radio models. The feature set covers the most commonly used Hamlib capabilities, making it suitable as a Hamlib alternative for .NET applications.

For the complete list of Hamlib-supported radios, refer to the [Hamlib documentation](https://hamlib.github.io/).