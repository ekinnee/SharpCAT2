using System.Text.Json;
using SharpCAT2.Core.Network.Contracts;
using SharpCAT2.Core.Radio.Contracts;
using Xunit;

namespace SharpCAT2.Tests.Contracts;

public class WireProtocolTests
{
    private static readonly JsonSerializerOptions Options = WireProtocol.CreateJsonOptions();

    private static WireRequest Request(string id = "2", string operation = "getFrequency", string args = "{}") => new()
    {
        Version = 1, Type = "request", Id = id, Operation = operation,
        Arguments = JsonSerializer.Deserialize<JsonElement>(args)
    };

    [Fact]
    public void Request_RoundTripsPublishedEnvelope()
    {
        const string json = "{\"v\":1,\"type\":\"request\",\"id\":\"42\",\"op\":\"getFrequency\",\"args\":{\"vfo\":\"A\"}}";
        var request = JsonSerializer.Deserialize<WireRequest>(json, Options)!;
        Assert.Equal(42UL, request.Validate(41, negotiated: true));
        Assert.Equal(json, JsonSerializer.Serialize(request, Options));
    }

    [Theory]
    [InlineData("")][InlineData("0")][InlineData("01")][InlineData("+1")]
    [InlineData(" 1")][InlineData("1.0")][InlineData("١")][InlineData("18446744073709551616")]
    public void Id_RejectsNoncanonicalOrOverflowValues(string id) =>
        Assert.Throws<JsonException>(() => WireProtocol.ParseRequestId(id));

    [Fact]
    public void Id_ExhaustionRequiresNewConnection()
    {
        Assert.Equal(ulong.MaxValue, WireProtocol.ParseRequestId("18446744073709551615"));
        Assert.Throws<JsonException>(() => Request("18446744073709551615").Validate(ulong.MaxValue, true));
    }

    [Fact]
    public void Hello_IsRequiredExactlyOnce()
    {
        Assert.Throws<JsonException>(() => Request().Validate(0, false));
        Assert.Equal(1UL, Request("1", "hello").Validate(0, false));
        Assert.Throws<JsonException>(() => Request("2", "hello").Validate(1, true));
    }

    [Theory]
    [InlineData("1")][InlineData("2")]
    public void Request_RejectsReusedAndOutOfOrderIds(string id) =>
        Assert.Throws<JsonException>(() => Request(id).Validate(2, true));

    [Theory]
    [InlineData("null")][InlineData("[]")][InlineData("\"A\"")]
    public void Request_RequiresObjectArguments(string args) =>
        Assert.Throws<JsonException>(() => Request(args: args).Validate(0, true));

    [Fact]
    public void Request_RejectsUnknownVersionTypeAndOperation()
    {
        Assert.Throws<JsonException>(() => (Request() with { Version = 2 }).Validate(0, true));
        Assert.Throws<JsonException>(() => (Request() with { Type = "event" }).Validate(0, true));
        Assert.Throws<JsonException>(() => Request(operation: "transmit").Validate(0, true));
    }

    [Theory]
    [InlineData(0)][InlineData(-1)][InlineData(5001)]
    public void Deadline_MustFitAdmissionBudget(int deadline) =>
        Assert.Throws<JsonException>(() => (Request() with { DeadlineMs = deadline }).Validate(0, true));

    [Fact]
    public void Cancel_TargetsEarlierIdWithoutClaimingCancelledOutcome()
    {
        var request = Request("3", "cancel", "{\"targetId\":\"2\"}");
        Assert.Equal(3UL, request.Validate(2, true));
        Assert.Throws<JsonException>(() => Request("3", "cancel", "{\"targetId\":\"3\"}").Validate(2, true));
        Assert.Throws<JsonException>(() => Request("3", "cancel", "{}").Validate(2, true));
    }

    [Fact]
    public void Json_RejectsMissingMembersAndUnknownEnvelopeFields()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WireRequest>("{\"v\":1}", Options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WireRequest>(
            "{\"v\":1,\"type\":\"request\",\"id\":\"1\",\"op\":\"hello\",\"args\":{},\"unexpected\":true}", Options));
    }

    [Fact]
    public void Response_EnumsAreNamesAndEventsHaveNoRequestId()
    {
        var response = new WireResponse { Version = 1, Type = "response", Id = "42", Outcome = RadioOutcome.Succeeded,
            Evidence = CompletionEvidence.ReplyReceived, Value = JsonSerializer.Deserialize<JsonElement>("{\"hz\":14250000}") };
        var json = JsonSerializer.Serialize(response, Options);
        Assert.Contains("\"outcome\":\"Succeeded\"", json);
        Assert.Contains("\"evidence\":\"ReplyReceived\"", json);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WireResponse>(json.Replace("\"Succeeded\"", "0"), Options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WireResponse>(json.Replace("\"Succeeded\"", "\"Bogus\""), Options));
        var notification = new WireEvent { Version = 1, Type = "event", Name = "connection", Value = JsonSerializer.Deserialize<JsonElement>("{}") };
        using var parsed = JsonDocument.Parse(JsonSerializer.Serialize(notification, Options));
        Assert.Equal("event", parsed.RootElement.GetProperty("type").GetString());
        Assert.False(parsed.RootElement.TryGetProperty("id", out _));
    }
    [Fact]
    public void InboundEnvelopes_RequireVersionTypeAndCanonicalResponseId()
    {
        const string json = """{"v":1,"type":"response","id":"2","outcome":"Succeeded","evidence":"NotSent"}""";
        var response = JsonSerializer.Deserialize<WireResponse>(json, Options)!;
        Assert.Equal(2UL, response.Validate());
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WireResponse>(json.Replace("\"v\":1,", ""), Options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WireResponse>(json.Replace("\"type\":\"response\",", ""), Options));
        Assert.Throws<JsonException>(() => (response with { Version = 2 }).Validate());
        Assert.Throws<JsonException>(() => (response with { Type = "event" }).Validate());
        Assert.Throws<JsonException>(() => (response with { Id = "02" }).Validate());
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WireResponse>(json.Replace("Succeeded", "succeeded"), Options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WireResponse>(json.Replace("NotSent", "notsent"), Options));
        var notification = new WireEvent { Version = 1, Type = "event", Name = "connection", Value = JsonSerializer.Deserialize<JsonElement>("{}") };
        notification.Validate();
        Assert.Throws<JsonException>(() => (notification with { Version = 2 }).Validate());
        Assert.Throws<JsonException>(() => (notification with { Type = "response" }).Validate());
        Assert.Throws<JsonException>(() => (notification with { Name = "" }).Validate());
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WireEvent>("""{"event":"connection","value":{}}""", Options));
    }

    [Fact]
    public void Json_RejectsDuplicateFieldsIncludingArguments()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WireRequest>(
            """{"v":1,"type":"request","id":"1","id":"2","op":"hello","args":{}}""", Options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WireRequest>(
            """{"v":1,"type":"request","id":"1","op":"hello","args":{"clientName":"a","clientName":"b"}}""", Options));
    }
}
