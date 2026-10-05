using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using SharpCAT2.Core.Radio.Contracts;

namespace SharpCAT2.Core.Network.Contracts;

/// <summary>Version 1 envelope contracts; socket framing and dispatch arrive in Phase 4.</summary>
public static class WireProtocol
{
    public const int Version = 1;
    public const int MaximumFrameBytes = 65536;
    public const int MaximumDeadlineMs = 5000;

    public static JsonSerializerOptions CreateJsonOptions() => new()
    {
        PropertyNameCaseInsensitive = false,
        AllowDuplicateProperties = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new ExactEnumConverter<RadioOutcome>(),
            new ExactEnumConverter<CompletionEvidence>() }
    };

    /// <summary>IDs are canonical positive UInt64 decimal strings, never reused within a connection.</summary>
    public static ulong ParseRequestId(string id)
    {
        if (string.IsNullOrEmpty(id) || id[0] == '0' || id.Any(c => c < '0' || c > '9') ||
            !ulong.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            throw new JsonException("Request IDs must be canonical positive UInt64 decimal strings.");
        return value;
    }
}

public sealed record WireRequest
{
    [JsonPropertyName("v")] public required int Version { get; init; }
    [JsonPropertyName("type")] public required string Type { get; init; }
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("op")] public required string Operation { get; init; }
    [JsonPropertyName("args")] public required JsonElement Arguments { get; init; }
    [JsonPropertyName("deadlineMs"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? DeadlineMs { get; init; }

    /// <summary>Pure envelope validation. The connection owner advances its last ID only after admission.</summary>
    public ulong Validate(ulong lastAdmittedId, bool negotiated)
    {
        if (Version != WireProtocol.Version || Type != "request")
            throw new JsonException("Unsupported envelope version or type.");
        var sequence = WireProtocol.ParseRequestId(Id);
        if (sequence <= lastAdmittedId) throw new JsonException("Request IDs must increase within a connection.");
        if (Arguments.ValueKind != JsonValueKind.Object) throw new JsonException("args must be an object.");
        if (DeadlineMs.HasValue && (DeadlineMs <= 0 || DeadlineMs > WireProtocol.MaximumDeadlineMs))
            throw new JsonException("deadlineMs is outside the server maximum.");
        if (Operation is not ("hello" or "listModels" or "listPorts" or "getConnection" or "getStatus" or
            "getFrequency" or "setFrequency" or "getMode" or "setMode" or "swapVfo" or "reconnect" or "cancel"))
            throw new JsonException("Unknown operation.");
        if ((!negotiated && Operation != "hello") || (negotiated && Operation == "hello"))
            throw new JsonException("hello must negotiate the connection before other operations.");
        if (Operation == "cancel")
        {
            if (!Arguments.TryGetProperty("targetId", out var target) || target.ValueKind != JsonValueKind.String ||
                WireProtocol.ParseRequestId(target.GetString()!) >= sequence)
                throw new JsonException("cancel must target an earlier request ID on this connection.");
        }
        return sequence;
    }
}

/// <summary>Responses terminate one request; notifications never use this envelope.</summary>
public sealed record WireResponse
{
    [JsonPropertyName("v")] public required int Version { get; init; }
    [JsonPropertyName("type")] public required string Type { get; init; }
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("outcome")] public required RadioOutcome Outcome { get; init; }
    [JsonPropertyName("evidence")] public required CompletionEvidence Evidence { get; init; }
    [JsonPropertyName("value"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Value { get; init; }
    [JsonPropertyName("diagnostic"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Diagnostic { get; init; }

    /// <summary>Validate before correlating an inbound response with a pending request.</summary>
    public ulong Validate()
    {
        if (Version != WireProtocol.Version || Type != "response" ||
            !Enum.IsDefined(Outcome) || !Enum.IsDefined(Evidence))
            throw new JsonException("Invalid response envelope.");
        return WireProtocol.ParseRequestId(Id);
    }
}

public sealed record WireEvent
{
    [JsonPropertyName("v")] public required int Version { get; init; }
    [JsonPropertyName("type")] public required string Type { get; init; }
    [JsonPropertyName("event")] public required string Name { get; init; }
    [JsonPropertyName("value")] public required JsonElement Value { get; init; }

    /// <summary>Validate before dispatching an inbound notification.</summary>
    public void Validate()
    {
        if (Version != WireProtocol.Version || Type != "event" || string.IsNullOrWhiteSpace(Name))
            throw new JsonException("Invalid event envelope.");
    }
}

internal sealed class ExactEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var name = reader.GetString();
            foreach (var value in Enum.GetValues<T>())
                if (string.Equals(name, value.ToString(), StringComparison.Ordinal)) return value;
        }
        throw new JsonException($"Expected an exact {typeof(T).Name} name.");
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        if (!Enum.IsDefined(value)) throw new JsonException($"Unknown {typeof(T).Name} value.");
        writer.WriteStringValue(value.ToString());
    }
}
