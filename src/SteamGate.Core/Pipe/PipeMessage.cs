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
                RequestMessage.TypeName => ParseRequest(root),
                CancelMessage.TypeName => ParseCancel(root),
                ReplyMessage.TypeName => ParseReply(root),
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

    private static RequestMessage? ParseRequest(JsonElement root)
    {
        if (!TryString(root, "requestId", out var requestId) || !TryString(root, "field", out var field))
        {
            return null;
        }

        // A member that is present must be the right kind of value; a JSON null counts as absent.
        int? number = null;
        if (IsPresent(root, "number", out var numberElement))
        {
            if (numberElement.ValueKind != JsonValueKind.Number || !numberElement.TryGetInt32(out var parsedNumber))
            {
                return null;
            }

            number = parsedNumber;
        }

        bool? flag = null;
        if (IsPresent(root, "flag", out var flagElement))
        {
            if (flagElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                return null;
            }

            flag = flagElement.GetBoolean();
        }

        string? folder = null;
        if (IsPresent(root, "folder", out var folderElement))
        {
            if (folderElement.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            folder = folderElement.GetString();
        }

        List<string>? days = null;
        if (IsPresent(root, "days", out var daysElement))
        {
            if (daysElement.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            days = [];
            foreach (var day in daysElement.EnumerateArray())
            {
                if (day.ValueKind != JsonValueKind.String)
                {
                    return null;
                }

                days.Add(day.GetString() ?? string.Empty);
            }
        }

        return new RequestMessage(requestId, field, number, flag, folder, days);
    }

    private static CancelMessage? ParseCancel(JsonElement root) =>
        TryString(root, "requestId", out var requestId) && TryString(root, "id", out var id) && Guid.TryParse(id, out var guid)
            ? new CancelMessage(requestId, guid)
            : null;

    private static ReplyMessage? ParseReply(JsonElement root)
    {
        if (!TryString(root, "requestId", out var requestId)
            || !TryString(root, "message", out var message)
            || !root.TryGetProperty("ok", out var ok)
            || ok.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return null;
        }

        Guid? pending = null;
        if (IsPresent(root, "pending", out _))
        {
            if (!TryString(root, "pending", out var text) || !Guid.TryParse(text, out var guid))
            {
                return null;
            }

            pending = guid;
        }

        return new ReplyMessage(requestId, ok.GetBoolean(), message, pending);
    }

    // True when the property exists and is not a JSON null.
    private static bool IsPresent(JsonElement element, string name, out JsonElement value) =>
        element.TryGetProperty(name, out value) && value.ValueKind != JsonValueKind.Null;

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

/// <summary>Client to server: change one rule. Exactly one of number, flag, folder, or days is set, the one that matches the field.</summary>
public sealed record RequestMessage(string RequestId, string Field, int? Number, bool? Flag, string? Folder, IReadOnlyList<string>? Days) : PipeMessage
{
    public const string TypeName = "request";

    public bool Equals(RequestMessage? other) =>
        other is not null
        && RequestId == other.RequestId && Field == other.Field && Number == other.Number && Flag == other.Flag && Folder == other.Folder
        && (Days is null ? other.Days is null : other.Days is not null && Days.SequenceEqual(other.Days));

    public override int GetHashCode() => HashCode.Combine(RequestId, Field, Number, Flag, Folder, Days?.Count);

    public override string Serialize() => Write(writer =>
    {
        writer.WriteString("type", TypeName);
        writer.WriteString("requestId", RequestId);
        writer.WriteString("field", Field);
        if (Number is { } number)
        {
            writer.WriteNumber("number", number);
        }

        if (Flag is { } flag)
        {
            writer.WriteBoolean("flag", flag);
        }

        if (Folder is not null)
        {
            writer.WriteString("folder", Folder);
        }

        if (Days is not null)
        {
            writer.WriteStartArray("days");
            foreach (var day in Days)
            {
                writer.WriteStringValue(day);
            }

            writer.WriteEndArray();
        }
    });
}

/// <summary>Client to server: drop the pending change with this id.</summary>
public sealed record CancelMessage(string RequestId, Guid Id) : PipeMessage
{
    public const string TypeName = "cancel";

    public override string Serialize() => Write(writer =>
    {
        writer.WriteString("type", TypeName);
        writer.WriteString("requestId", RequestId);
        writer.WriteString("id", Id);
    });
}

/// <summary>Server to client: the answer to the request or cancel with this requestId. Pending is the id of the entry the request created, when it did.</summary>
public sealed record ReplyMessage(string RequestId, bool Ok, string Message, Guid? Pending) : PipeMessage
{
    public const string TypeName = "reply";

    public override string Serialize() => Write(writer =>
    {
        writer.WriteString("type", TypeName);
        writer.WriteString("requestId", RequestId);
        writer.WriteBoolean("ok", Ok);
        writer.WriteString("message", Message);
        if (Pending is { } pending)
        {
            writer.WriteString("pending", pending);
        }
    });
}
