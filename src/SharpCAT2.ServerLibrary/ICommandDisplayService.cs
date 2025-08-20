using SharpCAT2.ServerLibrary.Radio;

namespace SharpCAT2.ServerLibrary;

/// <summary>
/// Interface for formatting and displaying command results.
/// Separates presentation logic from business logic.
/// </summary>
public interface ICommandDisplayService
{
    /// <summary>
    /// Formats radio status information for display
    /// </summary>
    /// <param name="statusInfo">Radio status information</param>
    /// <returns>Formatted status string</returns>
    string FormatRadioStatus(RadioStatusInfo statusInfo);

    /// <summary>
    /// Formats radio information for display
    /// </summary>
    /// <param name="radioName">Radio name</param>
    /// <param name="manufacturer">Manufacturer name</param>
    /// <param name="modelName">Model name</param>
    /// <param name="featureCount">Number of supported features</param>
    /// <param name="supportedFeatures">List of supported features</param>
    /// <returns>Formatted radio information</returns>
    string FormatRadioInfo(string radioName, string manufacturer, string modelName, 
        int featureCount, IEnumerable<string> supportedFeatures);

    /// <summary>
    /// Formats a list of radio models for display
    /// </summary>
    /// <param name="radios">Dictionary of radio models</param>
    /// <returns>Formatted radio list</returns>
    string FormatRadioList(Dictionary<string, string> radios);

    /// <summary>
    /// Formats current radio information for network response
    /// </summary>
    /// <param name="statusInfo">Radio status info</param>
    /// <param name="portName">Connected port name</param>
    /// <returns>Formatted current radio response</returns>
    string FormatCurrentRadioResponse(RadioStatusInfo? statusInfo, string? portName);

    /// <summary>
    /// Formats network protocol responses for radio commands
    /// </summary>
    /// <param name="command">Command type</param>
    /// <param name="data">Response data</param>
    /// <returns>Formatted network response</returns>
    string FormatNetworkResponse(string command, object data);
}