using System.Text;

namespace SharpCAT2.ClientLibrary;

/// <summary>
/// Static utility for filtering protocol markers from server responses containing lists.
/// Provides clean extraction of user-facing entities from protocol-marked responses,
/// designed for easy extension to support new protocol markers in the future.
/// </summary>
public static class ProtocolListFilter
{
    /// <summary>
    /// Supported protocol list types for filtering
    /// </summary>
    public enum ListType
    {
        /// <summary>
        /// Radio list (RADIO_LIST_START/END)
        /// </summary>
        Radios,
        
        /// <summary>
        /// Serial port list (SERIALPORT_LIST_START/END)
        /// </summary>
        SerialPorts
    }

    /// <summary>
    /// Configuration for a specific list type's protocol markers
    /// </summary>
    private record ListMarkerConfig(string StartMarker, string EndMarker);

    /// <summary>
    /// Mapping of list types to their protocol markers
    /// </summary>
    private static readonly Dictionary<ListType, ListMarkerConfig> MarkerConfigs = new()
    {
        [ListType.Radios] = new("RADIO_LIST_START", "RADIO_LIST_END"),
        [ListType.SerialPorts] = new("SERIALPORT_LIST_START", "SERIALPORT_LIST_END")
    };

    /// <summary>
    /// Filters a protocol response containing a list, removing start/end markers
    /// and returning only the clean list content.
    /// </summary>
    /// <param name="protocolResponse">Raw protocol response from server</param>
    /// <param name="listType">Type of list to filter</param>
    /// <returns>
    /// Clean list content without protocol markers, or empty string if no valid list found.
    /// Returns null if the input response is null.
    /// </returns>
    public static string? FilterList(string? protocolResponse, ListType listType)
    {
        if (protocolResponse == null)
            return null;

        if (!MarkerConfigs.TryGetValue(listType, out var config))
            throw new ArgumentException($"Unsupported list type: {listType}", nameof(listType));

        return ExtractListContent(protocolResponse, config.StartMarker, config.EndMarker);
    }

    /// <summary>
    /// Filters a radio list response, returning only the radio entries without protocol markers.
    /// Each line contains radio information in the format: "Manufacturer Model|FeatureCount"
    /// For clean radio names without the numerical suffix, use ParseRadioNames() on the result.
    /// </summary>
    /// <param name="protocolResponse">Raw protocol response containing radio list</param>
    /// <returns>Clean radio list without markers, or empty string if no valid list found</returns>
    public static string? FilterRadioList(string? protocolResponse)
    {
        return FilterList(protocolResponse, ListType.Radios);
    }

    /// <summary>
    /// Filters a serial port list response, returning only the port entries without protocol markers.
    /// Each line contains a port name (e.g., "COM1", "/dev/ttyUSB0", "FAKE")
    /// </summary>
    /// <param name="protocolResponse">Raw protocol response containing serial port list</param>
    /// <returns>Clean serial port list without markers, or empty string if no valid list found</returns>
    public static string? FilterSerialPortList(string? protocolResponse)
    {
        return FilterList(protocolResponse, ListType.SerialPorts);
    }

    /// <summary>
    /// Parses a filtered radio list into individual radio entries.
    /// Useful for programmatic access to radio information.
    /// </summary>
    /// <param name="filteredRadioList">Filtered radio list (output from FilterRadioList)</param>
    /// <returns>Array of radio entries, each containing "Manufacturer Model|FeatureCount"</returns>
    public static string[] ParseRadioEntries(string? filteredRadioList)
    {
        if (string.IsNullOrWhiteSpace(filteredRadioList))
            return Array.Empty<string>();

        return filteredRadioList
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
    }

    /// <summary>
    /// Parses a filtered radio list into clean radio names, removing the numerical suffix.
    /// This provides user-friendly radio names without feature count information.
    /// </summary>
    /// <param name="filteredRadioList">Filtered radio list (output from FilterRadioList)</param>
    /// <returns>Array of clean radio names (e.g., "Kenwood TS-2000" instead of "Kenwood TS-2000|25")</returns>
    public static string[] ParseRadioNames(string? filteredRadioList)
    {
        if (string.IsNullOrWhiteSpace(filteredRadioList))
            return Array.Empty<string>();

        return filteredRadioList
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(ExtractRadioName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();
    }

    /// <summary>
    /// Extracts the clean radio name from a radio entry, removing any numerical suffix.
    /// Converts "Kenwood TS-2000|25" to "Kenwood TS-2000".
    /// </summary>
    /// <param name="radioEntry">Radio entry that may contain numerical suffix</param>
    /// <returns>Clean radio name without numerical suffix</returns>
    private static string ExtractRadioName(string radioEntry)
    {
        if (string.IsNullOrWhiteSpace(radioEntry))
            return string.Empty;

        var pipeIndex = radioEntry.IndexOf('|');
        return pipeIndex == -1 ? radioEntry.Trim() : radioEntry.Substring(0, pipeIndex).Trim();
    }

    /// <summary>
    /// Parses a filtered serial port list into individual port names.
    /// Useful for programmatic access to port information.
    /// </summary>
    /// <param name="filteredPortList">Filtered port list (output from FilterSerialPortList)</param>
    /// <returns>Array of port names</returns>
    public static string[] ParsePortEntries(string? filteredPortList)
    {
        if (string.IsNullOrWhiteSpace(filteredPortList))
            return Array.Empty<string>();

        return filteredPortList
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
    }

    /// <summary>
    /// Core extraction logic that finds content between start and end markers.
    /// This method handles the actual protocol parsing and can be extended
    /// for new marker types by adding entries to MarkerConfigs.
    /// </summary>
    /// <param name="response">Full protocol response</param>
    /// <param name="startMarker">Start marker to look for</param>
    /// <param name="endMarker">End marker to look for</param>
    /// <returns>Content between markers, or empty string if markers not found properly</returns>
    private static string ExtractListContent(string response, string startMarker, string endMarker)
    {
        if (string.IsNullOrWhiteSpace(response))
            return string.Empty;

        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var result = new StringBuilder();
        bool inList = false;

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            if (trimmedLine.Equals(startMarker, StringComparison.OrdinalIgnoreCase))
            {
                inList = true;
                continue;
            }

            if (trimmedLine.Equals(endMarker, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (inList && !string.IsNullOrWhiteSpace(trimmedLine))
            {
                result.AppendLine(trimmedLine);
            }
        }

        return result.ToString().TrimEnd();
    }

    /// <summary>
    /// Registers a new list type with custom protocol markers.
    /// This method allows for future extension without modifying existing code.
    /// </summary>
    /// <param name="listType">New list type to register</param>
    /// <param name="startMarker">Start marker for this list type</param>
    /// <param name="endMarker">End marker for this list type</param>
    /// <exception cref="ArgumentException">Thrown if the list type is already registered</exception>
    public static void RegisterListType(ListType listType, string startMarker, string endMarker)
    {
        if (MarkerConfigs.ContainsKey(listType))
            throw new ArgumentException($"List type {listType} is already registered", nameof(listType));

        if (string.IsNullOrWhiteSpace(startMarker))
            throw new ArgumentException("Start marker cannot be null or whitespace", nameof(startMarker));

        if (string.IsNullOrWhiteSpace(endMarker))
            throw new ArgumentException("End marker cannot be null or whitespace", nameof(endMarker));

        MarkerConfigs[listType] = new ListMarkerConfig(startMarker, endMarker);
    }

    /// <summary>
    /// Gets the list of currently supported list types.
    /// Useful for debugging and validation.
    /// </summary>
    /// <returns>Array of supported list types</returns>
    public static ListType[] GetSupportedListTypes()
    {
        return MarkerConfigs.Keys.ToArray();
    }
}