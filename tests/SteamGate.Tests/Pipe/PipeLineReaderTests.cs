using System.Text;
using SteamGate.Core.Pipe;
using Xunit;

namespace SteamGate.Tests.Pipe;

public sealed class PipeLineReaderTests
{
    private static MemoryStream Bytes(string text) => new(Encoding.UTF8.GetBytes(text));

    [Fact]
    public async Task LineCapEnforcedWhileReading()
    {
        using var stream = new MemoryStream(new byte[20000]);

        var line = await PipeLineReader.ReadLineAsync(stream, 16384, TestContext.Current.CancellationToken);

        Assert.Null(line);
        Assert.Equal(16385, stream.Position);
    }

    [Fact]
    public async Task ReadsOneLineAndLeavesTheRest()
    {
        using var stream = Bytes("first\nsecond\n");

        var first = await PipeLineReader.ReadLineAsync(stream, 100, TestContext.Current.CancellationToken);
        var second = await PipeLineReader.ReadLineAsync(stream, 100, TestContext.Current.CancellationToken);
        var third = await PipeLineReader.ReadLineAsync(stream, 100, TestContext.Current.CancellationToken);

        Assert.Equal("first", first);
        Assert.Equal("second", second);
        Assert.Null(third);
    }

    [Fact]
    public async Task ALineExactlyAtTheCapIsAccepted()
    {
        using var stream = Bytes(new string('a', 10) + "\n");

        var line = await PipeLineReader.ReadLineAsync(stream, 10, TestContext.Current.CancellationToken);

        Assert.Equal(new string('a', 10), line);
    }

    [Fact]
    public async Task ALineOneByteOverTheCapIsRejected()
    {
        using var stream = Bytes(new string('a', 11) + "\n");

        Assert.Null(await PipeLineReader.ReadLineAsync(stream, 10, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ClosingCarriageReturnIsDropped()
    {
        using var stream = Bytes("hello\r\n");

        Assert.Equal("hello", await PipeLineReader.ReadLineAsync(stream, 100, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnEmptyLineIsEmptyText()
    {
        using var stream = Bytes("\nnext\n");

        Assert.Equal(string.Empty, await PipeLineReader.ReadLineAsync(stream, 100, TestContext.Current.CancellationToken));
        Assert.Equal("next", await PipeLineReader.ReadLineAsync(stream, 100, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EndOfStreamBeforeNewlineIsNull()
    {
        using var stream = Bytes("no newline");

        Assert.Null(await PipeLineReader.ReadLineAsync(stream, 100, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnEmptyStreamIsNull()
    {
        using var stream = new MemoryStream();

        Assert.Null(await PipeLineReader.ReadLineAsync(stream, 100, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ALoneCarriageReturnBeforeTheNewlineIsDropped()
    {
        using var stream = Bytes("\r\n");

        Assert.Equal(string.Empty, await PipeLineReader.ReadLineAsync(stream, 100, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ACarriageReturnInsideALineStays()
    {
        using var stream = Bytes("a\rb\n");

        Assert.Equal("a\rb", await PipeLineReader.ReadLineAsync(stream, 100, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReadsUtf8Text()
    {
        using var stream = Bytes("café ™\n");

        Assert.Equal("café ™", await PipeLineReader.ReadLineAsync(stream, 100, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void TheDefaultCapIs16Kilobytes()
    {
        Assert.Equal(16384, PipeLineReader.MaxLineBytes);
    }
}
