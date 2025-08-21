using SharpCAT2.ClientLibrary;

namespace SharpCAT2.ClientConsole;

/// <summary>
/// Handles command processing for the SharpCAT2 client application.
/// Provides separation of concerns for command handling logic.
/// </summary>
public class ClientCommandProcessor
{
    private readonly SharpCAT2Client _client;
    private readonly string _serverHost;
    private readonly int _serverPort;

    public ClientCommandProcessor(SharpCAT2Client client, string serverHost, int serverPort)
    {
        _client = client;
        _serverHost = serverHost;
        _serverPort = serverPort;
    }

    /// <summary>
    /// Processes a user command and returns true if the command was handled locally
    /// </summary>
    /// <param name="input">User input command</param>
    /// <returns>True if command was handled locally, false if it should be sent to server</returns>
    public async Task<CommandProcessingResult> ProcessCommandAsync(string input)
    {
        string normalizedInput = input.ToLower().Trim();
        
        return normalizedInput switch
        {
            ClientConstants.ExitCommand or ClientConstants.AlternativeExitCommand => HandleExitCommand(),
            ClientConstants.HelpCommand => HandleHelpCommand(),
            ClientConstants.StatusCommand => HandleStatusCommand(),
            ClientConstants.RadioStatusCommand or ClientConstants.RadioStatusShortCommand => await HandleRadioStatusCommandAsync(),
            ClientConstants.ListRadiosCommand or ClientConstants.ListRadiosShortCommand => await HandleListRadiosCommandAsync(),
            ClientConstants.ListSerialPortsCommand or ClientConstants.ListSerialPortsShortCommand => await HandleListSerialPortsCommandAsync(),
            ClientConstants.CurrentRadioCommand or ClientConstants.CurrentRadioShortCommand => await HandleCurrentRadioCommandAsync(),
            _ when normalizedInput.StartsWith(ClientConstants.SetRadioCommand + " ") => await HandleSetRadioCommandAsync(input),
            _ when normalizedInput.StartsWith(ClientConstants.SetRadioShortCommand + " ") => await HandleSetRadioCommandAsync(input),
            _ when normalizedInput.StartsWith(ClientConstants.RadioInfoCommand + " ") => await HandleRadioInfoCommandAsync(input),
            _ when normalizedInput.StartsWith(ClientConstants.RadioInfoShortCommand + " ") => await HandleRadioInfoCommandAsync(input),
            _ => new CommandProcessingResult(false, false) // Not handled, send to server
        };
    }

    #region Command Handlers

    /// <summary>
    /// Handles exit command
    /// </summary>
    private CommandProcessingResult HandleExitCommand()
    {
        Console.WriteLine("Disconnecting...");
        // Note: Configuration saving will be handled by the main program
        return new CommandProcessingResult(true, true); // Handled and should exit
    }

    /// <summary>
    /// Handles help command
    /// </summary>
    private CommandProcessingResult HandleHelpCommand()
    {
        ShowCommandHelp();
        return new CommandProcessingResult(true, false); // Handled but don't exit
    }

    /// <summary>
    /// Handles status command
    /// </summary>
    private CommandProcessingResult HandleStatusCommand()
    {
        Console.WriteLine($"Connection status: {(_client.IsConnected ? "Connected" : "Disconnected")}");
        Console.WriteLine($"Server: {_serverHost}:{_serverPort}");
        return new CommandProcessingResult(true, false);
    }

    /// <summary>
    /// Handles radio status command
    /// </summary>
    private async Task<CommandProcessingResult> HandleRadioStatusCommandAsync()
    {
        return await HandleClientCommandAsync(
            () => _client.GetRadioStatusAsync(), 
            "Failed to get radio status");
    }

    /// <summary>
    /// Handles list radios command
    /// </summary>
    private async Task<CommandProcessingResult> HandleListRadiosCommandAsync()
    {
        return await HandleClientCommandAsync(
            () => _client.GetAvailableRadiosAsync(), 
            "Failed to get radio list");
    }

    /// <summary>
    /// Handles list serial ports command
    /// </summary>
    private async Task<CommandProcessingResult> HandleListSerialPortsCommandAsync()
    {
        return await HandleClientCommandAsync(
            () => _client.GetAvailableSerialPortsAsync(), 
            "Failed to get serial port list");
    }

    /// <summary>
    /// Handles current radio command
    /// </summary>
    private async Task<CommandProcessingResult> HandleCurrentRadioCommandAsync()
    {
        return await HandleClientCommandAsync(
            () => _client.GetCurrentRadioAsync(), 
            "Failed to get current radio");
    }

    /// <summary>
    /// Handles set radio command
    /// </summary>
    private async Task<CommandProcessingResult> HandleSetRadioCommandAsync(string input)
    {
        string radioName = ExtractRadioNameFromSetCommand(input);
        if (!string.IsNullOrWhiteSpace(radioName))
        {
            return await HandleClientCommandAsync(
                () => _client.SetRadioAsync(radioName), 
                "Failed to set radio");
        }
        else
        {
            Console.WriteLine("Usage: set-radio <manufacturer> <model>");
            Console.WriteLine("Example: set-radio Kenwood TS-2000");
            return new CommandProcessingResult(true, false);
        }
    }

    /// <summary>
    /// Handles radio info command
    /// </summary>
    private async Task<CommandProcessingResult> HandleRadioInfoCommandAsync(string input)
    {
        string radioName = ExtractRadioNameFromInfoCommand(input);
        if (!string.IsNullOrWhiteSpace(radioName))
        {
            try
            {
                var radioInfo = await _client.GetRadioInfoAsync(radioName);
                if (radioInfo != null)
                {
                    Console.WriteLine(radioInfo.ToString());
                }
                else
                {
                    Console.WriteLine($"Radio not found: {radioName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to get radio info: {ex.Message}");
            }
            return new CommandProcessingResult(true, false);
        }
        else
        {
            Console.WriteLine("Usage: radio-info <manufacturer> <model>");
            Console.WriteLine("Example: radio-info Kenwood TS-2000");
            return new CommandProcessingResult(true, false);
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Generic helper for handling client commands that return a string result.
    /// Reduces code duplication for commands that follow the pattern:
    /// call client method -> print result or failure message -> return CommandProcessingResult
    /// </summary>
    /// <param name="clientOperation">The async client operation to execute</param>
    /// <param name="failureMessage">Message to display if the operation returns null</param>
    /// <returns>CommandProcessingResult indicating the command was handled locally</returns>
    private static async Task<CommandProcessingResult> HandleClientCommandAsync(
        Func<Task<string?>> clientOperation, 
        string failureMessage)
    {
        string? result = await clientOperation();
        if (result != null)
        {
            Console.WriteLine(result);
        }
        else
        {
            Console.WriteLine(failureMessage);
        }
        return new CommandProcessingResult(true, false);
    }

    /// <summary>
    /// Extracts radio name from set-radio command (supports both full and short form)
    /// </summary>
    /// <param name="input">Full command input</param>
    /// <returns>Radio name or empty string if invalid</returns>
    private static string ExtractRadioNameFromSetCommand(string input)
    {
        const string fullPrefix = "set-radio ";
        const string shortPrefix = "sr ";
        
        if (input.StartsWith(fullPrefix, StringComparison.OrdinalIgnoreCase) && input.Length > fullPrefix.Length)
        {
            return input[fullPrefix.Length..].Trim();
        }
        
        if (input.StartsWith(shortPrefix, StringComparison.OrdinalIgnoreCase) && input.Length > shortPrefix.Length)
        {
            return input[shortPrefix.Length..].Trim();
        }
        
        return string.Empty;
    }

    /// <summary>
    /// Extracts radio name from radio-info command (supports both full and short form)
    /// </summary>
    /// <param name="input">Full command input</param>
    /// <returns>Radio name or empty string if invalid</returns>
    private static string ExtractRadioNameFromInfoCommand(string input)
    {
        const string fullPrefix = "radio-info ";
        const string shortPrefix = "ri ";
        
        if (input.StartsWith(fullPrefix, StringComparison.OrdinalIgnoreCase) && input.Length > fullPrefix.Length)
        {
            return input[fullPrefix.Length..].Trim();
        }
        
        if (input.StartsWith(shortPrefix, StringComparison.OrdinalIgnoreCase) && input.Length > shortPrefix.Length)
        {
            return input[shortPrefix.Length..].Trim();
        }
        
        return string.Empty;
    }

    /// <summary>
    /// Shows command help information
    /// </summary>
    private static void ShowCommandHelp()
    {
        Console.WriteLine();
        Console.WriteLine("Available commands:");
        Console.WriteLine("  help                        - Show this help message");
        Console.WriteLine("  status                      - Show connection status");
        Console.WriteLine("  radio-status, rs            - Get current radio status");
        Console.WriteLine("  list-radios, lr             - List available radio models");
        Console.WriteLine("  list-serialports, ls        - List available serial ports");
        Console.WriteLine("  current-radio, cr           - Show current active radio");
        Console.WriteLine("  set-radio, sr <name>        - Change active radio (e.g., set-radio Kenwood TS-2000)");
        Console.WriteLine("  radio-info, ri <name>       - Get detailed radio model information (e.g., radio-info Kenwood TS-2000)");
        Console.WriteLine("  quit, exit                  - Disconnect and exit");
        Console.WriteLine();
        Console.WriteLine("All other commands are sent directly to the remote serial port/radio.");
        Console.WriteLine("Note: Only 'FAKE' is supported as a fake/test serial port.");
        Console.WriteLine();
    }

    #endregion
}