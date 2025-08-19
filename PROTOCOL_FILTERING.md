# Protocol List Filtering in ClientLibrary

## Overview

The SharpCAT2 ClientLibrary now includes an internal protocol marker filtering system that automatically removes server protocol markers from list responses, providing clean, user-friendly data to consuming applications.

## Problem Solved

Previously, methods like `GetAvailableRadiosAsync()` and `GetAvailableSerialPortsAsync()` returned raw server responses that included protocol markers:

```
RADIO_LIST_START
Kenwood TS-2000|25
Yaesu FT-991A|22
RADIO_LIST_END
```

This required every consumer (ClientConsole, custom applications) to manually filter out these markers to get clean data. This refactoring moves that responsibility into the ClientLibrary itself.

## How It Works

### ProtocolListFilter Utility

The `ProtocolListFilter` static class handles all protocol marker filtering:

```csharp
// Filters radio lists
string cleanRadios = ProtocolListFilter.FilterRadioList(serverResponse);

// Filters serial port lists
string cleanPorts = ProtocolListFilter.FilterSerialPortList(serverResponse);

// Generic filtering for extensibility
string cleanList = ProtocolListFilter.FilterList(response, ListType.Radios);
```

### Automatic Integration

ClientLibrary methods now automatically apply filtering:

```csharp
// Before refactoring
public async Task<string?> GetAvailableRadiosAsync()
{
    return await SendCommandAsync("list-radios"); // Raw response with markers
}

// After refactoring
public async Task<string?> GetAvailableRadiosAsync()
{
    var protocolResponse = await SendCommandAsync("list-radios");
    return ProtocolListFilter.FilterRadioList(protocolResponse); // Clean response
}
```

## API Changes

### Modified Methods

- `GetAvailableRadiosAsync()` - Now returns clean radio list without protocol markers
- `GetAvailableSerialPortsAsync()` - Now returns clean port list without protocol markers

### New Methods

- `GetAvailableRadioEntriesAsync()` - Returns parsed radio entries as string array
- `GetAvailableSerialPortEntriesAsync()` - Returns parsed port entries as string array

### Example Usage

```csharp
var client = new SharpCAT2Client("localhost", 8080);
await client.ConnectAsync();

// Get clean string response (no protocol markers)
string radioList = await client.GetAvailableRadiosAsync();
Console.WriteLine(radioList);
// Output:
// Kenwood TS-2000|25
// Yaesu FT-991A|22

// Get parsed array for programmatic use
string[] radios = await client.GetAvailableRadioEntriesAsync();
foreach (var radio in radios)
{
    Console.WriteLine($"Radio: {radio}");
}
```

## Supported Protocol Markers

Currently supported list types and their markers:

| List Type | Start Marker | End Marker |
|-----------|-------------|------------|
| Radios | `RADIO_LIST_START` | `RADIO_LIST_END` |
| Serial Ports | `SERIALPORT_LIST_START` | `SERIALPORT_LIST_END` |

## Extensibility

### Adding New List Types

The system is designed for easy extension. To support new protocol markers:

1. **Add to enum** (if using built-in types):
```csharp
public enum ListType
{
    Radios,
    SerialPorts,
    NewListType  // Add here
}
```

2. **Register markers**:
```csharp
ProtocolListFilter.RegisterListType(
    ListType.NewListType, 
    "NEW_LIST_START", 
    "NEW_LIST_END"
);
```

3. **Use generic filtering**:
```csharp
string cleanList = ProtocolListFilter.FilterList(response, ListType.NewListType);
```

### Custom Marker Registration

For dynamic scenarios, register custom markers at runtime:

```csharp
var customType = (ProtocolListFilter.ListType)100;
ProtocolListFilter.RegisterListType(customType, "CUSTOM_START", "CUSTOM_END");
```

## Error Handling

The filtering utility is designed to be robust:

- **Null input**: Returns null
- **Missing markers**: Returns empty string
- **Malformed responses**: Extracts what it can, ignores noise
- **Multiple start markers**: Treats duplicates as content to filter out
- **No end marker**: Returns all content after start marker

## Testing

Comprehensive test coverage includes:

- **Unit tests**: 25+ tests covering all filtering scenarios
- **Integration tests**: 8+ tests verifying ClientLibrary integration
- **Edge cases**: Corrupted responses, malformed data, network noise
- **Extensibility**: Custom marker registration and validation

Run filtering tests:
```bash
dotnet test --filter "ProtocolListFilter"
```

## Migration Guide

### For ClientConsole

No changes required! The ClientConsole continues to work exactly as before, but now receives clean data without protocol markers.

### For Custom Applications

Existing applications using ClientLibrary methods will automatically receive filtered responses. No code changes are required, but applications can remove any manual filtering code they may have implemented.

### Backward Compatibility

This change is fully backward compatible. The API signatures remain the same, but the returned data is now cleaner and more user-friendly.

## Rationale

### Benefits

1. **Single Responsibility**: Filtering is now handled in one place, not scattered across consumers
2. **Cleaner API**: ClientLibrary methods return only user-facing data
3. **Reduced Duplication**: No need for each consumer to implement filtering logic
4. **Better Error Handling**: Centralized robust parsing with comprehensive edge case handling
5. **Easy Extension**: Simple pattern for supporting new protocol markers
6. **Better Testing**: Isolated filtering logic with comprehensive test coverage

### Design Principles

- **Abstraction**: Hide protocol implementation details from consumers
- **Testability**: Static utility class with pure functions, easy to test
- **Extensibility**: Designed to support future protocol markers without breaking changes
- **Documentation**: Clear patterns and examples for extension

## Performance Considerations

- **Minimal Overhead**: Simple string parsing with StringBuilder for efficiency
- **Memory Friendly**: No unnecessary string allocations
- **Lazy Evaluation**: Filtering only happens when methods are called
- **Caching**: No caching implemented as list data is typically requested infrequently

## Future Enhancements

Potential future improvements:

1. **Async Filtering**: For very large lists (unlikely in amateur radio context)
2. **Typed Responses**: Structured radio/port objects instead of strings
3. **Caching**: Cache filtered results for frequently accessed lists
4. **Validation**: Validate protocol format compliance and provide diagnostics