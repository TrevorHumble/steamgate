using SteamGate.Core.Pipe;
using Xunit;

namespace SteamGate.Tests.Pipe;

public sealed class PipeMessageTests
{
    [Fact]
    public void WarnRoundTrips()
    {
        const string line = """{"type":"warn","minutesLeft":5}""";

        var message = Assert.IsType<WarnMessage>(PipeMessage.Parse(line));

        Assert.Equal(5, message.MinutesLeft);
        Assert.Equal(line, message.Serialize());
    }

    [Fact]
    public void HelloRoundTrips()
    {
        const string line = """{"type":"hello","role":"helper"}""";

        var message = Assert.IsType<HelloMessage>(PipeMessage.Parse(line));

        Assert.Equal("helper", message.Role);
        Assert.Equal(line, message.Serialize());
        Assert.Equal("status", Assert.IsType<HelloMessage>(PipeMessage.Parse("""{"type":"hello","role":"status"}""")).Role);
    }

    [Fact]
    public void CloseRoundTrips()
    {
        const string line = """{"type":"close","game":"Viewfinder","displayName":"Viewfinder","processIds":[20,21]}""";

        var message = Assert.IsType<CloseMessage>(PipeMessage.Parse(line));

        Assert.Equal("Viewfinder", message.Game);
        Assert.Equal("Viewfinder", message.DisplayName);
        Assert.Equal([20, 21], message.ProcessIds);
        Assert.Equal(line, message.Serialize());
    }

    [Fact]
    public void CloseWithAnEmptyListAndOddNamesRoundTrips()
    {
        var original = new CloseMessage("Dying Light: The \"Beast\"", "Dying Light ™", []);

        var parsed = Assert.IsType<CloseMessage>(PipeMessage.Parse(original.Serialize()));

        Assert.Equal(original, parsed);
        Assert.Equal(original.DisplayName, parsed.DisplayName);
    }

    [Fact]
    public void CloseMessagesCompareByContent()
    {
        Assert.Equal(new CloseMessage("a", "A", [1, 2]), new CloseMessage("a", "A", [1, 2]));
        Assert.NotEqual(new CloseMessage("a", "A", [1, 2]), new CloseMessage("a", "A", [1, 3]));
        Assert.NotEqual(new CloseMessage("a", "A", [1, 2]), new CloseMessage("b", "A", [1, 2]));
        Assert.NotEqual(new CloseMessage("a", "A", [1, 2]), new CloseMessage("a", "B", [1, 2]));
        Assert.Equal(new CloseMessage("a", "A", [1, 2]).GetHashCode(), new CloseMessage("a", "A", [1, 2]).GetHashCode());
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"type":"nope"}""")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("[]")]
    [InlineData("42")]
    [InlineData("""{"minutesLeft":5}""")]
    [InlineData("""{"type":7}""")]
    [InlineData("""{"type":"hello"}""")]
    [InlineData("""{"type":"hello","role":"admin"}""")]
    [InlineData("""{"type":"hello","role":5}""")]
    [InlineData("""{"type":"warn"}""")]
    [InlineData("""{"type":"warn","minutesLeft":"5"}""")]
    [InlineData("""{"type":"warn","minutesLeft":5.5}""")]
    [InlineData("""{"type":"warn","minutesLeft":99999999999}""")]
    [InlineData("""{"type":"close","displayName":"x","processIds":[]}""")]
    [InlineData("""{"type":"close","game":"x","processIds":[]}""")]
    [InlineData("""{"type":"close","game":"x","displayName":"x"}""")]
    [InlineData("""{"type":"close","game":"x","displayName":"x","processIds":"1"}""")]
    [InlineData("""{"type":"close","game":"x","displayName":"x","processIds":[1,"2"]}""")]
    [InlineData("""{"type":"close","game":"x","displayName":"x","processIds":[1.5]}""")]
    [InlineData("""{"type":"warn","minutesLeft":5""")]
    [InlineData("""{"type":"\ud800"}""")]
    [InlineData("""{"type":"hello","role":"\udc00"}""")]
    [InlineData("""{"type":"close","game":"x","displayName":"\ud800x","processIds":[]}""")]
    public void BadInputReturnsNull(string line)
    {
        Assert.Null(PipeMessage.Parse(line));
    }

    [Fact]
    public void ToLineIsTheJsonAndANewlineInUtf8()
    {
        var line = new CloseMessage("g", "Caf\u00e9", [7]).ToLine();

        Assert.Equal((byte)'\n', line[^1]);
        Assert.Equal("{\"type\":\"close\",\"game\":\"g\",\"displayName\":\"Caf\\u00E9\",\"processIds\":[7]}\n", System.Text.Encoding.UTF8.GetString(line));
        Assert.Equal(new WarnMessage(5).Serialize().Length + 1, new WarnMessage(5).ToLine().Length);
    }

    [Fact]
    public void NullLineReturnsNull()
    {
        Assert.Null(PipeMessage.Parse(null));
    }

    [Fact]
    public void DeeplyNestedInputReturnsNullWithoutThrowing()
    {
        var line = new string('[', 5000);

        Assert.Null(PipeMessage.Parse(line));
    }

    [Fact]
    public void ExtraFieldsAreIgnored()
    {
        var message = Assert.IsType<WarnMessage>(PipeMessage.Parse("""{"extra":true,"type":"warn","minutesLeft":10}"""));

        Assert.Equal(10, message.MinutesLeft);
    }
}
