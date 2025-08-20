using SharpCAT2.Core.Radio;
namespace SharpCAT2.ServerLibrary.Radio.Protocols;

/// <summary>
/// Interface for radio-specific communication protocols
/// </summary>
public interface IRadioProtocol
{
    /// <summary>
    /// Gets the protocol name
    /// </summary>
    string ProtocolName { get; }

    /// <summary>
    /// Creates a command to get the frequency
    /// </summary>
    RadioCommand GetFrequencyCommand();

    /// <summary>
    /// Creates a command to set the frequency
    /// </summary>
    RadioCommand SetFrequencyCommand(long frequency);

    /// <summary>
    /// Creates a command to get the mode
    /// </summary>
    RadioCommand GetModeCommand();

    /// <summary>
    /// Creates a command to set the mode
    /// </summary>
    RadioCommand SetModeCommand(string mode);

    /// <summary>
    /// Creates a command to get radio status/information
    /// </summary>
    RadioCommand GetStatusCommand();

    /// <summary>
    /// Creates a command to get VFO information
    /// </summary>
    RadioCommand GetVfoCommand();

    /// <summary>
    /// Creates a command to set the active VFO
    /// </summary>
    RadioCommand SetVfoCommand(string vfo);

    /// <summary>
    /// Creates a command to swap VFOs
    /// </summary>
    RadioCommand SwapVfoCommand();

    /// <summary>
    /// Creates a command to set split operation
    /// </summary>
    RadioCommand SetSplitCommand(bool enabled);

    /// <summary>
    /// Creates a command to get split status
    /// </summary>
    RadioCommand GetSplitCommand();

    /// <summary>
    /// Creates a command to set RIT offset
    /// </summary>
    RadioCommand SetRitCommand(int offsetHz);

    /// <summary>
    /// Creates a command to get RIT offset
    /// </summary>
    RadioCommand GetRitCommand();

    /// <summary>
    /// Creates a command to set XIT offset
    /// </summary>
    RadioCommand SetXitCommand(int offsetHz);

    /// <summary>
    /// Creates a command to get XIT offset
    /// </summary>
    RadioCommand GetXitCommand();

    /// <summary>
    /// Creates a command to set power output
    /// </summary>
    RadioCommand SetPowerOutputCommand(int powerPercent);

    /// <summary>
    /// Creates a command to get power output
    /// </summary>
    RadioCommand GetPowerOutputCommand();

    /// <summary>
    /// Creates a command to get S-meter reading
    /// </summary>
    RadioCommand GetSMeterCommand();

    /// <summary>
    /// Creates a command to get SWR reading
    /// </summary>
    RadioCommand GetSWRCommand();

    /// <summary>
    /// Creates a command to set antenna
    /// </summary>
    RadioCommand SetAntennaCommand(int antenna);

    /// <summary>
    /// Creates a command to get antenna
    /// </summary>
    RadioCommand GetAntennaCommand();

    /// <summary>
    /// Parses frequency from response
    /// </summary>
    long ParseFrequency(string response);

    /// <summary>
    /// Parses mode from response
    /// </summary>
    string ParseMode(string response);

    /// <summary>
    /// Parses VFO from response
    /// </summary>
    string ParseVfo(string response);

    /// <summary>
    /// Parses split status from response
    /// </summary>
    bool ParseSplit(string response);

    /// <summary>
    /// Parses RIT offset from response
    /// </summary>
    int ParseRit(string response);

    /// <summary>
    /// Parses XIT offset from response
    /// </summary>
    int ParseXit(string response);

    /// <summary>
    /// Parses power output from response
    /// </summary>
    int ParsePowerOutput(string response);

    /// <summary>
    /// Parses S-meter reading from response
    /// </summary>
    int ParseSMeter(string response);

    /// <summary>
    /// Parses SWR reading from response
    /// </summary>
    double ParseSWR(string response);

    /// <summary>
    /// Parses antenna from response
    /// </summary>
    int ParseAntenna(string response);

    /// <summary>
    /// Determines if response is complete
    /// </summary>
    bool IsCompleteResponse(string response);
}