using System.IO.Ports;

namespace SharpCAT2.Common.Serial;

/// <summary>
/// Factory for creating serial port implementations.
/// Provides a centralized way to instantiate the correct ISerialPort implementation
/// based on requirements (real hardware vs. simulation).
/// </summary>
public static class SerialPortFactory
{
    /// <summary>
    /// Creates a real serial port instance from an existing SerialPort
    /// </summary>
    /// <param name="serialPort">The SerialPort to wrap</param>
    /// <returns>ISerialPort implementation wrapping the real port</returns>
    public static ISerialPort CreateRealSerialPort(SerialPort serialPort)
    {
        return new RealSerialPort(serialPort);
    }

    /// <summary>
    /// Creates a real serial port instance with the specified parameters
    /// </summary>
    /// <param name="portName">The port name (e.g., COM1, /dev/ttyUSB0)</param>
    /// <param name="baudRate">The baud rate</param>
    /// <param name="parity">The parity scheme (default: None)</param>
    /// <param name="dataBits">The number of data bits (default: 8)</param>
    /// <param name="stopBits">The stop bits (default: One)</param>
    /// <returns>ISerialPort implementation for real hardware communication</returns>
    public static ISerialPort CreateRealSerialPort(string portName, int baudRate, 
                                                  Parity parity = Parity.None, 
                                                  int dataBits = 8, 
                                                  StopBits stopBits = StopBits.One)
    {
        return new RealSerialPort(portName, baudRate, parity, dataBits, stopBits);
    }

    /// <summary>
    /// Creates a fake serial port instance for testing and simulation
    /// </summary>
    /// <param name="portName">Simulated port name (default: "FAKE")</param>
    /// <param name="baudRate">Simulated baud rate (default: 9600)</param>
    /// <returns>ISerialPort implementation for simulation</returns>
    public static ISerialPort CreateFakeSerialPort(string portName = "FAKE", int baudRate = 9600)
    {
        return new FakeSerialPort(portName, baudRate);
    }

    /// <summary>
    /// Creates the appropriate serial port implementation based on the port name
    /// </summary>
    /// <param name="portName">Port name to determine implementation type</param>
    /// <param name="baudRate">Baud rate for communication</param>
    /// <param name="useFakeForTesting">Force use of fake implementation for testing</param>
    /// <returns>Appropriate ISerialPort implementation</returns>
    public static ISerialPort CreateSerialPort(string portName, int baudRate, bool useFakeForTesting = false)
    {
        // Use fake implementation for testing or if explicitly requested
        if (useFakeForTesting || 
            portName.Equals("FAKE", StringComparison.OrdinalIgnoreCase) ||
            portName.Equals("DUMMY", StringComparison.OrdinalIgnoreCase) ||
            portName.Equals("TEST", StringComparison.OrdinalIgnoreCase) ||
            portName.Equals("SIMULATION", StringComparison.OrdinalIgnoreCase))
        {
            return CreateFakeSerialPort(portName, baudRate);
        }

        // Create real serial port for actual hardware
        return CreateRealSerialPort(portName, baudRate);
    }

    /// <summary>
    /// Determines if a port name indicates a fake/simulated port
    /// </summary>
    /// <param name="portName">Port name to check</param>
    /// <returns>True if the port name indicates a simulated port</returns>
    public static bool IsFakePortName(string portName)
    {
        if (string.IsNullOrWhiteSpace(portName))
            return false;

        var upperName = portName.Trim().ToUpperInvariant();
        return upperName == "FAKE" || 
               upperName == "DUMMY" || 
               upperName == "TEST" || 
               upperName == "SIMULATION";
    }

    /// <summary>
    /// Gets a list of available real serial port names
    /// </summary>
    /// <returns>Array of available port names</returns>
    public static string[] GetAvailablePortNames()
    {
        try
        {
            return SerialPort.GetPortNames();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Checks if a real serial port name is available
    /// </summary>
    /// <param name="portName">Port name to check</param>
    /// <returns>True if the port is available</returns>
    public static bool IsPortAvailable(string portName)
    {
        if (string.IsNullOrWhiteSpace(portName))
            return false;

        if (IsFakePortName(portName))
            return true; // Fake ports are always "available"

        try
        {
            var availablePorts = GetAvailablePortNames();
            return availablePorts.Contains(portName, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}