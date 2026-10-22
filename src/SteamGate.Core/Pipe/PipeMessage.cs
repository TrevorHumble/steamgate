using System.Text.Json;

namespace SteamGate.Core.Pipe;

/// <summary>One line on the SteamGate pipe, as a JSON object with a "type". Parse never throws.</summary>
public abstract record PipeMessage
{
    /// <summary>The reading of a line, or null when it is not a well-formed message of a known type.</summary>
    public static PipeMessage? Parse(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !TryString(root, "type", out var type))
            {
                return null;
            }

            return type switch
            {
                HelloMessage.TypeName => ParseHello(root),
                WarnMessage.TypeName => ParseWarn(root),
                CloseMessage.TypeName => ParseClose(root),
                _ => null,
            };
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            // Malformed JSON, or a string with an escaped lone surrogate, which GetString refuses.
            return null;
        }
    }

    /// <summary>The message as one JSON line, without the newline.</summary>
    public abstract string Serialize();

    /// <summary>The message as it goes on the pipe: the JSON line and its newline, in UTF-8.</summary>
    public byte[] ToLine() => System.Text.Encoding.UTF8.GetBytes(Serialize() + "\n");

    private static HelloMessage? ParseHello(JsonElement root) =>
        TryString(root, "role", out var role) && (role == HelloMessage.HelperRole || role == HelloMessage.StatusRole)
            ? new HelloMessage(role)
            : null;

    private static WarnMessage? ParseWarn(JsonElement root) =>
        root.TryGetProperty("minutesLeft", out var minutes) && minutes.ValueKind == JsonValueKind.Number && minutes.TryGetInt32(out var value)
            ? new WarnMessage(value)
            : null;

    private static CloseMessage? ParseClose(JsonElement root)
    {
        if (!TryString(root, "game", out var game)
            || !TryString(root, "displayName", out var displayName)
            || !root.TryGetProperty("processIds", out var ids)
            || ids.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var processIds = new List<int>();
        foreach (var id in ids.EnumerateArray())
        {
            if (id.ValueKind != JsonValueKind.Number || !id.TryGetInt32(out var pid))
            {
                return null;
            }

            processIds.Add(pid);
        }

        return new CloseMessage(game, displayName, processIds);
    }

    private static bool TryString(JsonElement element, string name, out string value)
    {
        value = string.Empty;
        if (element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString() ?? string.Empty;
            return true;
        }

        return false;
    }

    internal static string Write(Action<Utf8JsonWriter> body)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            body(writer);
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}

/// <summary>The first line a client sends: who it is. The server derives everything else itself.</summary>
public sealed record HelloMessage(string Role) : PipeMessage
{
    public const string TypeName = "hello";
    public const string HelperRole = "helper";
    public const string StatusRole = "status";

    public override string Serialize() => Write(writer =>
    {
        writer.WriteString("type", TypeName);
        writer.WriteString("role", Role);
    });
}

/// <summary>Server to helper: show a warning with this many minutes left.</summary>
public sealed record WarnMessage(int MinutesLeft) : PipeMessage
{
    public const string TypeName = "warn";

    public override string Serialize() => Write(writer =>
    {
        writer.WriteString("type", TypeName);
        writer.WriteNumber("minutesLeft", MinutesLeft);
    });
}

/// <summary>Server to helper: ask this game's windows to close and say so on screen.</summary>
public sealed record CloseMessage(string Game, string DisplayName, IReadOnlyList<int> ProcessIds) : PipeMessage
{
    public const string TypeName = "close";

    public bool Equals(CloseMessage? other) =>
        other is not null && Game == other.Game && DisplayName == other.DisplayName && ProcessIds.SequenceEqual(other.ProcessIds);

    public override int GetHashCode() => HashCode.Combine(Game, DisplayName, ProcessIds.Count);

    public override string Serialize() => Write(writer =>
    {
        writer.WriteString("type", TypeName);
        writer.WriteString("game", Game);
        writer.WriteString("displayName", DisplayName);
        writer.WriteStartArray("processIds");
        foreach (var id in ProcessIds)
        {
            writer.WriteNumberValue(id);
        }

        writer.WriteEndArray();
    });
}
