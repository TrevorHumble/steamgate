using System.Text;

namespace SteamGate.Core.Steam;

/// <summary>
/// Reads Valve KeyValues text (libraryfolders.vdf, appmanifest_*.acf) into nested dictionaries whose values
/// are either a string or another such dictionary. The text is untrusted: the reader never recurses.
/// Parse failures (malformed text, nesting past MaxDepth) make TryParse return null; the reader never throws to callers.
/// </summary>
internal static class VdfReader
{
    /// <summary>The deepest block nesting accepted.</summary>
    public const int MaxDepth = 32;

    /// <summary>Returns the parsed text, or null when it is malformed.</summary>
    public static Dictionary<string, object>? TryParse(string text)
    {
        try
        {
            return Parse(text);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static Dictionary<string, object> Parse(string text)
    {
        var root = NewBlock();
        var open = new Stack<Dictionary<string, object>>();
        var current = root;
        string? pendingKey = null;
        var position = 0;

        while (true)
        {
            SkipBlankAndComments(text, ref position);
            if (position >= text.Length)
            {
                break;
            }

            var c = text[position];
            if (c == '}')
            {
                if (pendingKey is not null || open.Count == 0)
                {
                    throw new FormatException("unexpected }");
                }

                current = open.Pop();
                position++;
            }
            else if (c == '{')
            {
                if (pendingKey is null)
                {
                    throw new FormatException("{ without a key");
                }

                if (open.Count >= MaxDepth)
                {
                    throw new FormatException("blocks nested too deeply");
                }

                var child = NewBlock();
                current[pendingKey] = child;
                open.Push(current);
                current = child;
                pendingKey = null;
                position++;
            }
            else if (c == '"')
            {
                var token = ReadQuoted(text, ref position);
                if (pendingKey is null)
                {
                    pendingKey = token;
                }
                else
                {
                    current[pendingKey] = token;
                    pendingKey = null;
                }
            }
            else
            {
                throw new FormatException("unexpected character");
            }
        }

        if (pendingKey is not null || open.Count > 0)
        {
            throw new FormatException("text ends inside a block or after a key");
        }

        return root;
    }

    /// <summary>Returns the child block with this key, or null when absent or not a block.</summary>
    public static Dictionary<string, object>? Block(Dictionary<string, object> parent, string key) =>
        parent.TryGetValue(key, out var value) ? value as Dictionary<string, object> : null;

    /// <summary>Returns every child block of the parent, in file order, leaving out values that are strings.</summary>
    public static IEnumerable<Dictionary<string, object>> Blocks(Dictionary<string, object> parent) =>
        parent.Values.OfType<Dictionary<string, object>>();

    /// <summary>Returns the string value with this key, or null when absent or not a string.</summary>
    public static string? Text(Dictionary<string, object> parent, string key) =>
        parent.TryGetValue(key, out var value) ? value as string : null;

    private static Dictionary<string, object> NewBlock() => new(StringComparer.OrdinalIgnoreCase);

    private static void SkipBlankAndComments(string text, ref int position)
    {
        while (position < text.Length)
        {
            if (char.IsWhiteSpace(text[position]))
            {
                position++;
            }
            else if (text[position] == '/' && position + 1 < text.Length && text[position + 1] == '/')
            {
                while (position < text.Length && text[position] != '\n')
                {
                    position++;
                }
            }
            else
            {
                return;
            }
        }
    }

    // position is on the opening quote; on return it is just past the closing quote.
    private static string ReadQuoted(string text, ref int position)
    {
        var result = new StringBuilder();
        position++;
        while (position < text.Length)
        {
            var c = text[position++];
            if (c == '"')
            {
                return result.ToString();
            }

            if (c != '\\')
            {
                result.Append(c);
                continue;
            }

            if (position >= text.Length)
            {
                break;
            }

            var escaped = text[position++];
            switch (escaped)
            {
                case 'n':
                    result.Append('\n');
                    break;
                case 't':
                    result.Append('\t');
                    break;
                case '\\':
                case '"':
                    result.Append(escaped);
                    break;
                default:
                    // An escape Valve's format does not define stays as written.
                    result.Append('\\').Append(escaped);
                    break;
            }
        }

        throw new FormatException("unterminated string");
    }
}
