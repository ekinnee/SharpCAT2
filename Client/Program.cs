using SharpCAT2.ClientLib;
using SharpCAT2.Common.Radio;

namespace SharpCAT2.Client;

class Program
{
    private static ClientConfig? _config;
    
    private static async Task Main(string[] args)
    {
        Console.WriteLine(ClientConstants.ApplicationTitle);
        Console.WriteLine(ClientConstants.TitleSeparator);
        
        // Load configuration
        _config = await ClientConfig.LoadAsync(ClientConstants.ConfigFileName);
        
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

        // Create command processor for handling client commands
        var commandProcessor = new ClientCommandProcessor(client, serverHost, serverPort);

        // Main command loop
        string? input;
        while (true)
        {
            Console.Write(ClientConstants.CommandPrompt);
            input = Console.ReadLine();
            
            if (string.IsNullOrWhiteSpace(input))
                continue;
                
            // Process command using dedicated processor
            var result = await commandProcessor.ProcessCommandAsync(input);
            
            if (result.ShouldExit)
            {
                await SaveConfigurationAsync();
                return;
            }
            
            if (result.WasHandled)
            {
                continue; // Command was handled locally, don't send to server
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

}