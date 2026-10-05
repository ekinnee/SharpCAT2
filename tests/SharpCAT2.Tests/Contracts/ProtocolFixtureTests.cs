using System.Text.Json;
using Xunit;

namespace SharpCAT2.Tests.Contracts;

/// <summary>Checks the independently authored fixture corpus, not a production parser.</summary>
public class ProtocolFixtureTests
{
    [Fact]
    public void FixtureCorpus_IsDeployedWithTraceableUniqueCases()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ft991a.json")));
        var root = document.RootElement;
        Assert.Equal("1711-D (filename suffix)", root.GetProperty("source").GetProperty("revision").GetString());
        Assert.StartsWith("https://www.yaesu.com/", root.GetProperty("source").GetProperty("url").GetString());
        var cases = root.GetProperty("cases").EnumerateArray().ToArray();
        var byId = cases.ToDictionary(c => c.GetProperty("id").GetString()!);
        Assert.Equal(cases.Length, byId.Count);
        foreach (var item in cases)
        {
            Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("request").GetString()));
            var page = item.GetProperty("page");
            Assert.Equal(page.GetProperty("pdfIndex").GetInt32(), page.GetProperty("printed").GetInt32());
            Assert.InRange(page.GetProperty("pdfIndex").GetInt32(), 2, 17);
            Assert.Contains(item.GetProperty("expected").GetProperty("classification").GetString(),
                new[] { "valid", "malformed", "incomplete", "invalidArgument", "identityMismatch" });
        }
        Assert.Equal("ID0670;", byId["identity-ft991a"].GetProperty("response").GetString());
        Assert.Equal("FA000030000;", byId["frequency-a-lower-bound-set"].GetProperty("request").GetString());
        Assert.Equal("FB470000000;", byId["frequency-b-upper-bound-set"].GetProperty("request").GetString());
        Assert.Equal("MD02;", byId["mode-query-usb-main-rx"].GetProperty("response").GetString());
        Assert.Equal(JsonValueKind.Null, byId["swap-no-reply-read-back"].GetProperty("response").ValueKind);
        Assert.Equal("malformed", byId["mode-unknown-device-reply"].GetProperty("expected").GetProperty("classification").GetString());
        Assert.Equal("invalidArgument", byId["mode-unknown-caller-setter"].GetProperty("expected").GetProperty("classification").GetString());
    }
}
