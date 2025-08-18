# SharpCAT2 Web Client

## Overview

The SharpCAT2 Web Client is a modern ASP.NET Core Blazor Server application that provides a user-friendly web interface for controlling amateur radios remotely through the SharpCAT2.WebApi. This client offers an intuitive graphical interface as an alternative to the command-line client.

## Features

### Core Functionality
- **Remote Radio Control**: Connect to and control amateur radios via the SharpCAT2.WebApi
- **Real-time Status**: Live status updates showing connection state and radio information  
- **Command Interface**: Send CAT (Computer Aided Transceiver) commands directly to the radio
- **Radio Selection**: Choose from 29+ supported radio models across 7 major manufacturers
- **Connection Management**: Connect/disconnect with configurable serial port and baud rate settings

### User Interface
- **Responsive Design**: Works on desktop, tablet, and mobile devices
- **Bootstrap Styling**: Modern, professional interface with Bootstrap 5 and Bootstrap Icons
- **Real-time Updates**: Automatic status refresh every 5 seconds
- **Command History**: Track sent commands and responses with timestamps
- **Interactive Forms**: Form validation and loading states for better user experience

### Technical Features
- **Service-Oriented Architecture**: Clean separation with dependency injection
- **HTTP Client Abstraction**: Configurable API communication layer
- **Error Handling**: Comprehensive error handling with user-friendly messages
- **Logging**: Structured logging for debugging and monitoring
- **Configuration**: Flexible configuration through appsettings.json

## Architecture

### Project Structure
```
SharpCAT2.WebClient/
├── Components/
│   ├── Layout/
│   │   ├── MainLayout.razor       # Main application layout
│   │   └── NavMenu.razor          # Navigation menu
│   └── Pages/
│       ├── Home.razor             # Main radio control interface
│       └── Error.razor            # Error page
├── Configuration/
│   └── WebClientConfiguration.cs  # Configuration model
├── Models/
│   └── ApiModels.cs               # API request/response models
├── Services/
│   ├── IRadioApiService.cs        # API service interface
│   └── RadioApiService.cs         # API service implementation
├── wwwroot/                       # Static web assets
├── Program.cs                     # Application startup
└── appsettings.json              # Configuration file
```

### Service Layer
- **IRadioApiService**: Interface defining API communication methods
- **RadioApiService**: HTTP client implementation for WebApi communication
- **Dependency Injection**: Registered services with proper lifetime management

### API Communication
The web client communicates with the SharpCAT2.WebApi through the following endpoints:
- `GET /api/radio/status` - Get current radio status
- `POST /api/radio/connect` - Connect to a radio
- `POST /api/radio/disconnect` - Disconnect from radio
- `POST /api/radio/command` - Send command to radio
- `GET /api/radio/available` - Get list of available radios with friendly names
- `GET /api/radio/serial/ports` - Get list of available serial ports (including fake ports)

## Configuration

### Application Settings
Configure the web client through `appsettings.json`:

```json
{
  "SharpCAT2WebClient": {
    "WebApiBaseUrl": "https://localhost:5001",
    "TimeoutSeconds": 30
  }
}
```

#### Configuration Options
- **WebApiBaseUrl**: The base URL of the SharpCAT2.WebApi (required)
- **TimeoutSeconds**: HTTP client timeout in seconds (default: 30)

### Environment-Specific Settings
Use `appsettings.Development.json` for development-specific configuration:

```json
{
  "SharpCAT2WebClient": {
    "WebApiBaseUrl": "http://localhost:5000"
  }
}
```

## Getting Started

### Prerequisites
- .NET 8.0 SDK or later
- SharpCAT2.WebApi running and accessible
- Modern web browser with JavaScript enabled

### Installation & Setup

1. **Clone the Repository**
   ```bash
   git clone https://github.com/ekinnee/SharpCAT2.git
   cd SharpCAT2
   ```

2. **Configure the Web Client**
   Edit `SharpCAT2.WebClient/appsettings.json` to point to your WebApi instance:
   ```json
   {
     "SharpCAT2WebClient": {
       "WebApiBaseUrl": "https://your-webapi-server:port"
     }
   }
   ```

3. **Build the Solution**
   ```bash
   dotnet build
   ```

4. **Start the WebApi Server**
   ```bash
   cd SharpCAT2.WebApi
   dotnet run
   ```

5. **Start the Web Client**
   ```bash
   cd SharpCAT2.WebClient
   dotnet run
   ```

6. **Access the Web Interface**
   Open your browser to `https://localhost:5027` (or the URL shown in the console)

## Usage Guide

### Connecting to a Radio

1. **Select Serial Port**:
   - **Dropdown Selection**: Choose from the list of available serial ports (automatically detected)
   - **Manual Entry**: Select "Enter manually..." if your port is not listed, then type the port name
   - **Common Ports**: `COM3` (Windows), `/dev/ttyUSB0` (Linux), or `FAKE` for testing
   - **Test Ports**: FAKE, DUMMY, SIMULATION, and TEST are available for testing without hardware

2. **Choose Radio Model**:
   - **Auto-detection**: Leave on "Auto-detect" to let the system identify your radio
   - **Manual Selection**: Choose your specific radio model from the friendly dropdown (e.g., "Kenwood TS-2000", "Elecraft K3")
   - **29+ Models Supported**: Covers 7 major manufacturers with descriptive names

3. **Set Baud Rate**: Choose the appropriate baud rate (default: 9600)

4. **Click Connect**: The interface will attempt to connect and show status updates

5. **Monitor Status**: The status panel shows connection state and radio information

### Sending Commands

1. **Ensure Connected**: Radio must be connected before sending commands
2. **Get Help**: Click the "Help" button in the Command Interface section for:
   - CAT command basics and syntax
   - Common command examples with descriptions
   - Error handling information
   - Manufacturer-specific notes
3. **Enter Command**: Type a CAT command in the command input field
4. **Send Command**: Click "Send" or press Enter
5. **View Response**: Results appear in the command history panel

### Command Interface Help

The built-in help system provides:
- **CAT Command Basics**: Explanation of Computer Aided Transceiver commands
- **Common Commands**: Frequently used commands like FA; (frequency), IF; (status), ID; (identification)
- **Command Syntax**: Most commands end with semicolon (;) 
- **Error Handling**: What to expect when commands fail or are not recognized
- **Manufacturer Notes**: Syntax varies by brand - consult your radio manual

### Common CAT Commands
- `FA;` - Get/Set VFO A frequency
- `FB;` - Get/Set VFO B frequency  
- `IF;` - Get transceiver information
- `ID;` - Get radio ID
- `AI0;` - Turn off auto-information mode
- `AI1;` - Turn on auto-information mode
- `TX;` - Start transmit
- `RX;` - Stop transmit (receive)

### Status Monitoring
- Connection status updates automatically every 5 seconds
- Manual refresh available with the "Refresh" button
- Status shows connection state, radio model, and detailed radio information

## Development

### Adding New Features

1. **API Models**: Add new models to `Models/ApiModels.cs`
2. **Service Methods**: Extend `IRadioApiService` and `RadioApiService`
3. **UI Components**: Create new Blazor components in `Components/`
4. **Configuration**: Add new settings to `WebClientConfiguration`

### Service Registration
Register new services in `Program.cs`:

```csharp
builder.Services.AddScoped<INewService, NewService>();
```

### HTTP Client Configuration
The HTTP client is pre-configured with base address and timeout. To customize:

```csharp
builder.Services.AddHttpClient<IRadioApiService, RadioApiService>(client =>
{
    client.BaseAddress = new Uri(webClientConfig.WebApiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(webClientConfig.TimeoutSeconds);
    // Add custom headers, authentication, etc.
});
```

## Testing

### Manual Testing
1. Start the WebApi and WebClient applications
2. Navigate to the web interface
3. Test connection with `FAKE` port for simulation
4. Verify all UI components respond correctly
5. Test error scenarios (invalid ports, disconnected API, etc.)

### Integration Testing
- Ensure compatibility with existing SharpCAT2.WebApi
- Test with multiple radio models if hardware is available
- Verify responsive design on different screen sizes

## Troubleshooting

### Common Issues

**Cannot Connect to WebApi**
- Verify WebApi is running and accessible
- Check `WebApiBaseUrl` configuration
- Ensure firewall allows connections
- Check network connectivity

**Radio Connection Fails**
- Verify serial port name and availability
- Check baud rate settings
- Ensure radio is powered on and properly connected
- Try `FAKE` port for testing without hardware

**UI Not Responsive**
- Check browser console for JavaScript errors
- Verify Blazor SignalR connection
- Refresh the page to reset client state

**Status Not Updating**
- Verify WebApi is responding to status requests
- Check browser network tab for failed requests
- Manual refresh should work even if auto-refresh fails

### Logging
Enable detailed logging in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "SharpCAT2.WebClient": "Debug"
    }
  }
}
```

### Browser Developer Tools
Use browser F12 tools to:
- Check network requests to the WebApi
- View console logs for JavaScript errors  
- Inspect Blazor component state
- Monitor WebSocket connections for real-time updates

## Security Considerations

### Network Security
- Use HTTPS in production environments
- Configure proper CORS policies on the WebApi
- Consider authentication/authorization for multi-user scenarios
- Firewall rules to restrict WebApi access

### Input Validation
- All user inputs are validated before sending to WebApi
- Commands are sanitized to prevent injection attacks
- Error messages don't expose sensitive system information

## Performance

### Optimization
- Status updates use efficient timer-based polling
- HTTP client connections are reused
- Blazor Server reduces client-side JavaScript requirements
- Bootstrap CSS is served from CDN for faster loading

### Scalability
- Blazor Server applications scale well with SignalR
- Consider load balancing for multiple WebApi instances
- Monitor memory usage for long-running sessions

## Contributing

1. Follow existing code structure and naming conventions
2. Add appropriate logging for new features
3. Update documentation for any new functionality
4. Ensure backward compatibility with existing WebApi
5. Test with multiple browsers and devices

## Future Enhancements

### Planned Features
- User authentication and authorization
- Multiple radio connection support
- WebSocket real-time updates from WebApi
- Command templates and shortcuts
- Configuration wizards for radio setup
- Mobile app using Blazor Hybrid

### Extension Points
- Plugin architecture for custom commands
- Themes and UI customization
- Advanced logging and monitoring
- Integration with other amateur radio software