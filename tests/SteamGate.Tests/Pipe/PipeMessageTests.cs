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

    [Fact]
    public void RequestCancelReplyRoundTrip()
    {
        const string numberLine = """{"type":"request","requestId":"r1","field":"weeknightMinutes","number":120}""";
        const string daysLine = """{"type":"request","requestId":"r3","field":"weekendNights","days":["saturday","Sunday"]}""";
        var id = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");
        var cancelLine = """{"type":"cancel","requestId":"r2","id":"0f8fad5b-d9cb-469f-a165-70867728950e"}""";
        var replyLine = """{"type":"reply","requestId":"r1","ok":true,"message":"Waits 48 h 0 min.","pending":"0f8fad5b-d9cb-469f-a165-70867728950e"}""";

        var number = Assert.IsType<RequestMessage>(PipeMessage.Parse(numberLine));
        var days = Assert.IsType<RequestMessage>(PipeMessage.Parse(daysLine));
        var cancel = Assert.IsType<CancelMessage>(PipeMessage.Parse(cancelLine));
        var reply = Assert.IsType<ReplyMessage>(PipeMessage.Parse(replyLine));

        Assert.Equal(new RequestMessage("r1", "weeknightMinutes", 120, null, null, null), number);
        Assert.Equal(new RequestMessage("r3", "weekendNights", null, null, null, ["saturday", "Sunday"]), days);
        Assert.Equal(new CancelMessage("r2", id), cancel);
        Assert.Equal(new ReplyMessage("r1", true, "Waits 48 h 0 min.", id), reply);
        Assert.Equal(numberLine, number.Serialize());
        Assert.Equal(daysLine, days.Serialize());
        Assert.Equal(cancelLine, cancel.Serialize());
        Assert.Equal(replyLine, reply.Serialize());
    }

    [Fact]
    public void RequestWithEveryValueMemberRoundTrips()
    {
        var original = new RequestMessage("r9", "notCountedAdd", 5, false, "OBS Studio", []);

        var parsed = Assert.IsType<RequestMessage>(PipeMessage.Parse(original.Serialize()));

        Assert.Equal(original, parsed);
        Assert.Equal("""{"type":"request","requestId":"r9","field":"notCountedAdd","number":5,"flag":false,"folder":"OBS Studio","days":[]}""", original.Serialize());
        Assert.Equal(5, parsed.Number);
        Assert.False(parsed.Flag);
        Assert.Equal("OBS Studio", parsed.Folder);
        Assert.Empty(parsed.Days!);
    }

    [Fact]
    public void ReplyWithoutPendingOmitsIt()
    {
        var reply = new ReplyMessage("r1", false, "Not allowed.", null);

        Assert.Equal("""{"type":"reply","requestId":"r1","ok":false,"message":"Not allowed."}""", reply.Serialize());
        var parsed = Assert.IsType<ReplyMessage>(PipeMessage.Parse(reply.Serialize()));
        Assert.False(parsed.Ok);
        Assert.Null(parsed.Pending);
        Assert.Equal("Not allowed.", parsed.Message);
    }

    [Fact]
    public void NullMembersCountAsAbsent()
    {
        var request = Assert.IsType<RequestMessage>(PipeMessage.Parse("""{"type":"request","requestId":"r","field":"enabled","flag":true,"number":null,"folder":null,"days":null}"""));
        var reply = Assert.IsType<ReplyMessage>(PipeMessage.Parse("""{"type":"reply","requestId":"r","ok":true,"message":"m","pending":null}"""));

        Assert.Equal(new RequestMessage("r", "enabled", null, true, null, null), request);
        Assert.Null(reply.Pending);
    }

    [Fact]
    public void RequestMessagesCompareByContent()
    {
        Assert.Equal(new RequestMessage("r", "f", 1, true, "x", ["a"]), new RequestMessage("r", "f", 1, true, "x", ["a"]));
        Assert.Equal(new RequestMessage("r", "f", 1, true, "x", ["a"]).GetHashCode(), new RequestMessage("r", "f", 1, true, "x", ["a"]).GetHashCode());
        Assert.NotEqual(new RequestMessage("r", "f", 1, true, "x", ["a"]), new RequestMessage("r", "f", 1, true, "x", ["b"]));
        Assert.NotEqual(new RequestMessage("r", "f", 1, true, "x", ["a"]), new RequestMessage("r", "f", 1, true, "x", null));
        Assert.NotEqual(new RequestMessage("r", "f", 1, true, "x", null), new RequestMessage("r", "f", 1, true, "x", ["a"]));
        Assert.NotEqual(new RequestMessage("r", "f", 1, true, "x", null), new RequestMessage("q", "f", 1, true, "x", null));
        Assert.NotEqual(new RequestMessage("r", "f", 1, true, "x", null), new RequestMessage("r", "g", 1, true, "x", null));
        Assert.NotEqual(new RequestMessage("r", "f", 1, true, "x", null), new RequestMessage("r", "f", 2, true, "x", null));
        Assert.NotEqual(new RequestMessage("r", "f", 1, true, "x", null), new RequestMessage("r", "f", 1, false, "x", null));
        Assert.NotEqual(new RequestMessage("r", "f", 1, true, "x", null), new RequestMessage("r", "f", 1, true, "y", null));
    }

    [Theory]
    [InlineData("""{"type":"request","field":"enabled","flag":true}""")]
    [InlineData("""{"type":"request","requestId":"r","flag":true}""")]
    [InlineData("""{"type":"request","requestId":7,"field":"enabled"}""")]
    [InlineData("""{"type":"request","requestId":"r","field":"x","number":"5"}""")]
    [InlineData("""{"type":"request","requestId":"r","field":"x","number":5.5}""")]
    [InlineData("""{"type":"request","requestId":"r","field":"x","flag":"true"}""")]
    [InlineData("""{"type":"request","requestId":"r","field":"x","flag":1}""")]
    [InlineData("""{"type":"request","requestId":"r","field":"x","folder":5}""")]
    [InlineData("""{"type":"request","requestId":"r","field":"x","days":"Friday"}""")]
    [InlineData("""{"type":"request","requestId":"r","field":"x","days":[5]}""")]
    [InlineData("""{"type":"cancel","id":"0f8fad5b-d9cb-469f-a165-70867728950e"}""")]
    [InlineData("""{"type":"cancel","requestId":"r"}""")]
    [InlineData("""{"type":"cancel","requestId":"r","id":"not a guid"}""")]
    [InlineData("""{"type":"cancel","requestId":"r","id":5}""")]
    [InlineData("""{"type":"reply","message":"m","ok":true}""")]
    [InlineData("""{"type":"reply","requestId":"r","ok":true}""")]
    [InlineData("""{"type":"reply","requestId":"r","message":"m"}""")]
    [InlineData("""{"type":"reply","requestId":"r","message":"m","ok":"yes"}""")]
    [InlineData("""{"type":"reply","requestId":"r","message":"m","ok":true,"pending":"nope"}""")]
    [InlineData("""{"type":"reply","requestId":"r","message":"m","ok":true,"pending":5}""")]
    public void BadRequestCancelReplyReturnsNull(string line)
    {
        Assert.Null(PipeMessage.Parse(line));
    }

    [Fact]
    public void ExtendMessageRoundTrips()
    {
        const string line = """{"type":"extend","requestId":"s7","minutes":20}""";
        var message = new ExtendMessage("s7", 20);

        Assert.Equal(line, message.Serialize());
        Assert.Equal((line + "\n"), System.Text.Encoding.UTF8.GetString(message.ToLine()));
        Assert.Equal(message, PipeMessage.Parse(line));

        // The length is not checked here: the service answers a wrong one with "Not allowed." instead of dropping the connection.
        Assert.Equal(new ExtendMessage("s7", 25), PipeMessage.Parse("""{"type":"extend","requestId":"s7","minutes":25}"""));
    }

    [Theory]
    [InlineData("""{"type":"extend","minutes":20}""")]
    [InlineData("""{"type":"extend","requestId":7,"minutes":20}""")]
    [InlineData("""{"type":"extend","requestId":"s7"}""")]
    [InlineData("""{"type":"extend","requestId":"s7","minutes":"20"}""")]
    [InlineData("""{"type":"extend","requestId":"s7","minutes":20.5}""")]
    [InlineData("""{"type":"extend","requestId":"s7","minutes":20.0}""")]
    [InlineData("""{"type":"extend","requestId":"s7","minutes":null}""")]
    [InlineData("""{"type":"extend","requestId":"s7","minutes":3000000000}""")]
    public void BadExtendIsNull(string line)
    {
        Assert.Null(PipeMessage.Parse(line));
    }
}
