using SharpCAT2.Common.Serial;

namespace SharpCAT2.Common.Radio.Models.Testing;

/// <summary>
/// DummyRadio class for demonstration and testing purposes.
/// 
/// This radio implementation provides deterministic, simulated responses without requiring
/// actual radio hardware or serial port communication. It uses FakeSerialPort to handle
/// the simulation of serial communication, providing a clean separation of concerns.
/// 
/// Designed for:
/// - Testing client/server functionality without real hardware
/// - Demonstrating radio features in development environments
/// - Training and educational purposes
/// - UI development and feature testing
/// 
/// LIMITATIONS:
/// - Simulated responses only - no actual radio communication
/// - Does not validate frequency ranges or mode compatibility
/// - Fixed response timing (no real hardware delays)
/// - Memory and settings are reset on each restart
/// </summary>
public class DummyRadio : BaseRadio
{
    #region Private Fields
    
    private bool _isConnected = false;

    #endregion

    #region Radio Properties

    /// <summary>
    /// Gets the radio model name
    /// </summary>
    public override string ModelName => "DummyRadio";

    /// <summary>
    /// Gets the radio manufacturer
    /// </summary>
    public override string Manufacturer => "SharpCAT2";

    /// <summary>
    /// DummyRadio supports comprehensive features for demonstration purposes
    /// </summary>
    public override SupportedFeatures SupportedFeatures => 
        SupportedFeatures.FrequencyControl | 
        SupportedFeatures.ModeControl |
        SupportedFeatures.DualVFO | 
        SupportedFeatures.VFOSwap |
        SupportedFeatures.SplitOperation |
        SupportedFeatures.RIT | 
        SupportedFeatures.XIT |
        SupportedFeatures.PowerOutput |
        SupportedFeatures.SMeter |
        SupportedFeatures.SWRMeter |
        SupportedFeatures.AntennaSelection |
        SupportedFeatures.MemoryChannels |
        SupportedFeatures.CWKeyer |
        SupportedFeatures.CWSpeed |
        SupportedFeatures.CWMessage |
        SupportedFeatures.NoiseReduction |
        SupportedFeatures.IFBandwidth |
        SupportedFeatures.TransmitStatus |
        SupportedFeatures.ReceiveStatus |
        SupportedFeatures.RadioID |
        SupportedFeatures.PowerOnOff |
        SupportedFeatures.ComputerControl;

    /// <summary>
    /// Gets whether the radio is currently connected (simulated)
    /// </summary>
    public new bool IsConnected => _isConnected;

    #endregion

    #region Connection Methods

    /// <summary>
    /// Simulates connecting to the radio. Creates a FakeSerialPort if none provided.
    /// </summary>
    /// <param name="port">Serial port (can be fake or real for DummyRadio)</param>
    /// <returns>True (always successful for demonstration)</returns>
    public override async Task<bool> ConnectAsync(ISerialPort port)
    {
        // Simulate connection delay
        await Task.Delay(100);
        
        // If no port is provided or it's a real port, create a fake port for simulation
        if (port == null || port is RealSerialPort)
        {
            Console.WriteLine("DummyRadio: Creating FakeSerialPort for simulation");
            _serialPort = SerialPortFactory.CreateFakeSerialPort("DUMMY", 9600);
        }
        else
        {
            _serialPort = port;
        }
        
        if (!_serialPort.IsOpen)
        {
            _serialPort.Open();
        }
        
        _isConnected = true;
        
        Console.WriteLine("DummyRadio: Simulated connection established");
        Console.WriteLine("DummyRadio: This is a testing radio with simulated responses");
        
        return true;
    }

    /// <summary>
    /// Simulates disconnecting from the radio
    /// </summary>
    public override void Disconnect()
    {
        _isConnected = false;
        base.Disconnect();
        Console.WriteLine("DummyRadio: Simulated disconnection");
    }

    #endregion

    #region Command Processing

    /// <summary>
    /// Sends a command to the serial port and returns the response.
    /// If using FakeSerialPort, this will be simulated.
    /// If using RealSerialPort, this will communicate with actual hardware.
    /// </summary>
    /// <param name="command">Command to process</param>
    /// <returns>Response from the serial port</returns>
    public override async Task<string?> SendCommandAsync(RadioCommand command)
    {
        if (!_isConnected || _serialPort == null)
        {
            return null;
        }

        try
        {
            // Use the base class implementation which handles serial communication
            // This will work with both real and fake serial ports
            return await base.SendCommandAsync(command);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DummyRadio: Error sending command '{command.Command}': {ex.Message}");
            return null;
        }
    }

    #endregion

    #region Status and Operations

    /// <summary>
    /// Gets the current simulated radio status
    /// </summary>
    /// <returns>Radio status with simulated values</returns>
    public override async Task<RadioStatus> GetStatusAsync()
    {
        // Use base implementation which will query the serial port for status
        var status = await base.GetStatusAsync();
        
        // Add some dummy-specific information
        status.AdditionalInfo["SimulatedRadio"] = true;
        status.AdditionalInfo["DummyRadioVersion"] = "1.0";
        
        return status;
    }

    #endregion

    #region Disposal

    /// <summary>
    /// Disposes the DummyRadio instance
    /// </summary>
    public override void Dispose()
    {
        if (!_disposed)
        {
            Disconnect();
            Console.WriteLine("DummyRadio: Disposed");
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    #endregion
}