using SharpCAT2.ClientLib;

namespace SharpCAT2.Client;

class Program
{
    private static async Task Main(string[] args)
    {
        Console.WriteLine("SharpCAT2 Client - Remote Serial Port Communication");
        Console.WriteLine("===================================================");
        
        // Parse command line arguments for server host/port
        string serverHost = "localhost";
        int serverPort = 8080;
        
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "-h":
                case "--host":
                    if (i + 1 < args.Length)
                        serverHost = args[++i];
                    break;
                case "-p":
                case "--port":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out int port))
                        serverPort = port;
                    break;
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
                    return;
                    
                case "help":
                    ShowCommandHelp();
                    continue;
                    
                case "status":
                    Console.WriteLine($"Connection status: {(client.IsConnected ? "Connected" : "Disconnected")}");
                    Console.WriteLine($"Server: {serverHost}:{serverPort}");
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
        Console.WriteLine("  help     - Show this help message");
        Console.WriteLine("  status   - Show connection status");
        Console.WriteLine("  quit     - Disconnect and exit");
        Console.WriteLine("  exit     - Disconnect and exit");
        Console.WriteLine();
        Console.WriteLine("Any other input will be sent to the remote serial port.");
        Console.WriteLine("The response from the serial device will be displayed.");
    }
}