using SharpCAT2.ClientLib;
using SharpCAT2.Radio;

namespace SharpCAT2.Client;

class Program
{
    private static ClientConfig? _config;
    
    private static async Task Main(string[] args)
    {
        Console.WriteLine("SharpCAT2 Client - Remote Serial Port Communication");
        Console.WriteLine("===================================================");
        
        // Load configuration
        const string configPath = "client_config.json";
        _config = await ClientConfig.LoadAsync(configPath);
        
        // Update configuration with command line arguments
        _config.UpdateFromArgs(args);
        
        // Use configuration values
        string serverHost = _config.ServerHost;
        int serverPort = _config.ServerPort;
        
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "--help":
                    ShowHelp();
                    return;
            }
        }

        Console.WriteLine($"Connecting to SharpCAT2 server at {serverHost}:{serverPort}...");
        
        // Create and connect to the client
        using var client = new SharpCAT2Client(serverHost, serverPort);
        
        bool connected = await client.ConnectAsync();
        if (!connected)
        {
            Console.WriteLine("Failed to connect to server. Please ensure:");
            Console.WriteLine("1. The SharpCAT2 server is running");
            Console.WriteLine("2. The server host and port are correct");
            Console.WriteLine("3. No firewall is blocking the connection");
            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            
            try
            {
                Console.ReadKey();
            }
            catch (InvalidOperationException)
            {
                // Handle case where console input is redirected
                Console.WriteLine("Exiting...");
            }
            return;
        }

        Console.WriteLine("Connected successfully!");
        Console.WriteLine("Type commands to send to the remote serial port.");
        Console.WriteLine("Type 'quit' or 'exit' to disconnect and exit.");
        Console.WriteLine("Type 'help' for command help.");
        Console.WriteLine();

        // Set up graceful shutdown handler
        Console.CancelKeyPress += async (sender, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("\nShutting down gracefully...");
            await SaveConfigurationAsync();
            Environment.Exit(0);
        };

        // Main command loop
        string? input;
        while (true)
        {
            Console.Write("Serial> ");
            input = Console.ReadLine();
            
            if (string.IsNullOrWhiteSpace(input))
                continue;
                
            // Handle local commands
            switch (input.ToLower().Trim())
            {
                case "quit":
                case "exit":
                    Console.WriteLine("Disconnecting...");
                    await SaveConfigurationAsync();
                    return;
                    
                case "help":
                    ShowCommandHelp();
                    continue;
                    
                case "status":
                    Console.WriteLine($"Connection status: {(client.IsConnected ? "Connected" : "Disconnected")}");
                    Console.WriteLine($"Server: {serverHost}:{serverPort}");
                    continue;

                case "radio-status":
                case "rs":
                    string? radioStatus = await client.GetRadioStatusAsync();
                    if (radioStatus != null)
                    {
                        Console.WriteLine(radioStatus);
                    }
                    else
                    {
                        Console.WriteLine("Failed to get radio status");
                    }
                    continue;

                case "radios":
                case "list-radios":
                    string? radioList = await client.GetAvailableRadiosAsync();
                    if (radioList != null)
                    {
                        Console.WriteLine(radioList);
                    }
                    else
                    {
                        Console.WriteLine("Failed to get radio list");
                    }
                    continue;

                case "current-radio":
                case "get-current-radio":
                    string? currentRadio = await client.GetCurrentRadioAsync();
                    if (currentRadio != null)
                    {
                        Console.WriteLine(currentRadio);
                    }
                    else
                    {
                        Console.WriteLine("Failed to get current radio");
                    }
                    continue;
            }
            
            // Handle set-radio command (before sending to serial port)
            if (input.ToLower().StartsWith("set-radio "))
            {
                string radioName = input.Substring(10).Trim(); // Remove "set-radio " prefix
                if (!string.IsNullOrWhiteSpace(radioName))
                {
                    string? setRadioResponse = await client.SetRadioAsync(radioName);
                    if (setRadioResponse != null)
                    {
                        Console.WriteLine(setRadioResponse);
                    }
                    else
                    {
                        Console.WriteLine("Failed to set radio");
                    }
                }
                else
                {
                    Console.WriteLine("Please specify a radio name. Example: set-radio Kenwood TS-2000");
                }
                continue;
            }
            
            // Send command to remote serial port
            try
            {
                string? response = await client.SendCommandAsync(input);
                
                if (response != null)
                {
                    if (!string.IsNullOrEmpty(response))
                    {
                        Console.WriteLine($"Response: {response}");
                    }
                    else
                    {
                        Console.WriteLine("(No response)");
                    }
                }
                else
                {
                    Console.WriteLine("Error: Failed to send command or receive response");
                    
                    if (!client.IsConnected)
                    {
                        Console.WriteLine("Connection lost. Attempting to reconnect...");
                        bool reconnected = await client.ConnectAsync();
                        if (reconnected)
                        {
                            Console.WriteLine("Reconnected successfully!");
                        }
                        else
                        {
                            Console.WriteLine("Reconnection failed. Exiting...");
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
                Console.WriteLine("The application will continue. Type 'quit' to exit.");
            }
        }
        
        // Save configuration on normal exit
        await SaveConfigurationAsync();
    }
    
    /// <summary>
    /// Saves the current configuration to file
    /// </summary>
    private static async Task SaveConfigurationAsync()
    {
        if (_config != null)
        {
            const string configPath = "client_config.json";
            await _config.SaveAsync(configPath);
        }
    }
    
    private static void ShowHelp()
    {
        Console.WriteLine("Usage: Client [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -h, --host <hostname>   Server hostname or IP address (default: localhost)");
        Console.WriteLine("  -p, --port <port>       Server TCP port (default: 8080)");
        Console.WriteLine("  --help                  Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  Client                                # Connect to localhost:8080");
        Console.WriteLine("  Client --host 192.168.1.100         # Connect to remote server");
        Console.WriteLine("  Client --host localhost --port 9090  # Connect to custom port");
    }
    
    private static void ShowCommandHelp()
    {
        Console.WriteLine("Available commands:");
        Console.WriteLine("  help           - Show this help message");
        Console.WriteLine("  status         - Show connection status");
        Console.WriteLine("  radio-status   - Show radio status (rs)");
        Console.WriteLine("  list-radios    - List available radio models (radios)");
        Console.WriteLine("  current-radio  - Show current active radio (get-current-radio)");
        Console.WriteLine("  set-radio <name> - Set active radio (e.g., set-radio Kenwood TS-2000)");
        Console.WriteLine("  quit           - Disconnect and exit");
        Console.WriteLine("  exit           - Disconnect and exit");
        Console.WriteLine();
        Console.WriteLine("Radio Management Examples:");
        Console.WriteLine("  list-radios              - List all available radio models");
        Console.WriteLine("  current-radio            - Show current active radio");
        Console.WriteLine("  set-radio Kenwood TS-2000 - Change active radio to Kenwood TS-2000");
        Console.WriteLine("  set-radio Elecraft K3     - Change active radio to Elecraft K3");
        Console.WriteLine();
        Console.WriteLine("Radio Commands:");
        Console.WriteLine("  FA;            - Get frequency (VFO A)");
        Console.WriteLine("  FA14074000;    - Set frequency to 14.074 MHz");
        Console.WriteLine("  MD;            - Get mode");
        Console.WriteLine("  MD2;           - Set mode to USB");
        Console.WriteLine("  ID;            - Get radio ID");
        Console.WriteLine();
        Console.WriteLine("Any other input will be sent to the remote serial port/radio.");
        Console.WriteLine("Radio commands should end with semicolon (;) for most radios.");
    }
}