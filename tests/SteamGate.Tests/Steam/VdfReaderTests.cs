using System.Text;
using SteamGate.Core.Steam;
using Xunit;

namespace SteamGate.Tests.Steam;

public sealed class VdfReaderTests
{
    private static string Nested(int depth)
    {
        var text = new StringBuilder();
        for (var i = 0; i < depth; i++)
        {
            text.Append("\"a\" { ");
        }

        text.Append('}', depth);
        return text.ToString();
    }

    // Parsing is private; every test goes through TryParse, which gives null for text it does not accept.
    private static Dictionary<string, object> Parsed(string text)
    {
        var root = VdfReader.TryParse(text);
        Assert.NotNull(root);
        return root;
    }

    [Fact]
    public void EscapedQuote()
    {
        var root = Parsed("\"k\" \"a\\\"b\"");

        Assert.Equal("a\"b", VdfReader.Text(root, "k"));
    }

    [Fact]
    public void Escapes_BackslashNewlineTab()
    {
        var root = Parsed("\"k\" \"C:\\\\x\\ny\\tz\"");

        Assert.Equal("C:\\x\ny\tz", VdfReader.Text(root, "k"));
    }

    [Fact]
    public void UndefinedEscape_StaysAsWritten()
    {
        var root = Parsed("\"k\" \"a\\qb\"");

        Assert.Equal("a\\qb", VdfReader.Text(root, "k"));
    }

    [Fact]
    public void CommentLines_AreSkipped()
    {
        var root = Parsed("// first\n\"a\" \"1\" // trailing\n// \"b\" \"2\"\n\"c\" \"3\"\n// last, no newline");

        Assert.Equal(["a", "c"], root.Keys.ToArray());
        Assert.Equal("3", VdfReader.Text(root, "c"));
    }

    [Fact]
    public void NestedLookup_IgnoresKeyCase()
    {
        var root = Parsed("\"AppState\" { \"InstallDir\" \"Game\" }");

        var block = VdfReader.Block(root, "appstate");

        Assert.NotNull(block);
        Assert.Equal("Game", VdfReader.Text(block, "INSTALLDIR"));
    }

    [Fact]
    public void Lookup_WrongKindOrAbsent_ReturnsNull()
    {
        var root = Parsed("\"s\" \"text\" \"b\" { }");

        Assert.Null(VdfReader.Block(root, "s"));
        Assert.Null(VdfReader.Text(root, "b"));
        Assert.Null(VdfReader.Block(root, "missing"));
        Assert.Null(VdfReader.Text(root, "missing"));
    }

    [Fact]
    public void KeysKeepFileOrder()
    {
        var root = Parsed("\"z\" \"1\" \"a\" \"2\" \"m\" \"3\"");

        Assert.Equal(["z", "a", "m"], root.Keys.ToArray());
    }

    [Fact]
    public void EmptyText_GivesEmptyRoot()
    {
        Assert.Empty(Parsed(" \n\t "));
    }

    [Theory]
    [InlineData("\"a\"")]
    [InlineData("\"a\" {")]
    [InlineData("\"a\" { \"b\" }")]
    [InlineData("}")]
    [InlineData("\"a\" { } }")]
    [InlineData("{ }")]
    [InlineData("\"a\" \"unterminated")]
    [InlineData("\"a\" \"ends with backslash\\")]
    [InlineData("bare \"x\"")]
    [InlineData("/ \"x\" \"y\"")]
    public void Malformed_GivesNull(string text)
    {
        Assert.Null(VdfReader.TryParse(text));
    }

    [Fact]
    public void TryParse_ReturnsNullWhenMalformed_AndTheBlockWhenValid()
    {
        Assert.Null(VdfReader.TryParse("\"a\" {"));
        Assert.Null(VdfReader.TryParse(Nested(33)));
        Assert.Equal("v", VdfReader.Text(VdfReader.TryParse("\"k\" \"v\"")!, "k"));
    }

    [Fact]
    public void DepthLimit_32()
    {
        var atLimit = VdfReader.TryParse(Nested(32));
        var tooDeep = VdfReader.TryParse(Nested(33));
        var hugeDepth = VdfReader.TryParse(Nested(100_000));

        Assert.NotNull(atLimit);
        Assert.Null(tooDeep);
        Assert.Null(hugeDepth);
    }

    [Fact]
    public void Blocks_ListsChildBlocksInFileOrder_AndSkipsStrings()
    {
        var root = Parsed("\"p\" { \"a\" { \"n\" \"1\" } \"s\" \"text\" \"b\" { \"n\" \"2\" } }");

        var blocks = VdfReader.Blocks(VdfReader.Block(root, "p")!);

        Assert.Equal(["1", "2"], blocks.Select(block => VdfReader.Text(block, "n")));
    }
}
