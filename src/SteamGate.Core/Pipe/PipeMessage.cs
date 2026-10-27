using System.Text.Encodings.Web;
using System.Text.Json;
using SteamGate.Core.Accounting;
using SteamGate.Core.Rules;

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
                ExtendMessage.TypeName => ParseExtend(root),
                ReplyMessage.TypeName => ParseReply(root),
                StateMessage.TypeName => ParseState(root),
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

    private static RequestMessage? ParseRequest(JsonElement root) =>
        TryString(root, "requestId", out var requestId) && TryString(root, "field", out var field)
        && TryValueMembers(root, out var number, out var flag, out var folder, out var days)
            ? new RequestMessage(requestId, field, number, flag, folder, days)
            : null;

    // The four optional value members of a request or a pending entry. A member that is present must be the right kind of
    // value; a JSON null counts as absent. False when one is present and wrong.
    private static bool TryValueMembers(JsonElement root, out int? number, out bool? flag, out string? folder, out List<string>? days)
    {
        number = null;
        flag = null;
        folder = null;
        days = null;
        if (IsPresent(root, "number", out var numberElement))
        {
            if (numberElement.ValueKind != JsonValueKind.Number || !numberElement.TryGetInt32(out var parsedNumber))
            {
                return false;
            }

            number = parsedNumber;
        }

        if (IsPresent(root, "flag", out var flagElement))
        {
            if (flagElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                return false;
            }

            flag = flagElement.GetBoolean();
        }

        if (IsPresent(root, "folder", out var folderElement))
        {
            if (folderElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            folder = folderElement.GetString();
        }

        if (IsPresent(root, "days", out var daysElement))
        {
            if (daysElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            days = [];
            foreach (var day in daysElement.EnumerateArray())
            {
                if (day.ValueKind != JsonValueKind.String)
                {
                    return false;
                }

                days.Add(day.GetString() ?? string.Empty);
            }
        }

        return true;
    }

    private static CancelMessage? ParseCancel(JsonElement root) =>
        TryString(root, "requestId", out var requestId) && TryString(root, "id", out var id) && Guid.TryParse(id, out var guid)
            ? new CancelMessage(requestId, guid)
            : null;

    // The minutes must be a JSON number written as an integer that fits an int; whether it is an allowed length is the service's call.
    private static ExtendMessage? ParseExtend(JsonElement root) =>
        TryString(root, "requestId", out var requestId) && TryInt(root, "minutes", out var minutes)
            ? new ExtendMessage(requestId, minutes)
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

    private static StateMessage? ParseState(JsonElement root)
    {
        if (!TryOptionalNumber(root, "remainingSeconds", out var remaining)
            || !TryOptionalNumber(root, "tonightLimitMinutes", out var limit)
            || !TryOptionalString(root, "counting", out var counting)
            || !TryBool(root, "tonightWeekend", out var weekend)
            || !TryDouble(root, "nightSeconds", out var night)
            || !TryDouble(root, "weekSeconds", out var week)
            || !TryBool(root, "helperConnected", out var helper)
            || !root.TryGetProperty("settings", out var settingsElement)
            || settingsElement.ValueKind != JsonValueKind.Object
            || ParseStateSettings(settingsElement) is not { } settings
            || !TryNamedFolders(root, "addable", out var addable))
        {
            return null;
        }

        // The extension is absent, null, 0, or one of the allowed lengths, or the line is not a state.
        var extensionMinutes = 0;
        if (IsPresent(root, "extensionMinutes", out var extensionElement)
            && (extensionElement.ValueKind != JsonValueKind.Number || !extensionElement.TryGetInt32(out extensionMinutes)
                || (extensionMinutes != 0 && !NightExtension.Lengths.Contains(extensionMinutes))))
        {
            return null;
        }

        // The limit is a whole number of minutes, or the line is not a state.
        if (limit is { } minutes && (minutes != Math.Floor(minutes) || Math.Abs(minutes) > int.MaxValue))
        {
            return null;
        }

        return new StateMessage(remaining, limit is { } whole ? (int)whole : null, weekend, night, week, counting, helper, settings, addable, extensionMinutes);
    }

    private static StateSettings? ParseStateSettings(JsonElement root)
    {
        if (!TryInt(root, "weeknightMinutes", out var weeknight)
            || !TryInt(root, "weekendNightMinutes", out var weekendNight)
            || !TryInt(root, "weeklyCapMinutes", out var weekly)
            || !TryInt(root, "cooldownHours", out var cooldown)
            || !TryBool(root, "enabled", out var enabled)
            || !TryBool(root, "allowUninstall", out var allowUninstall)
            || !TryDayList(root, "weekendNights", out var nights)
            || !TryNamedFolders(root, "notCounted", out var notCounted)
            || !root.TryGetProperty("pending", out var pendingElement)
            || pendingElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var pending = new List<PendingChange>();
        foreach (var item in pendingElement.EnumerateArray())
        {
            if (ParsePending(item) is not { } entry)
            {
                return null;
            }

            pending.Add(entry);
        }

        return new StateSettings(weeknight, weekendNight, weekly, cooldown, enabled, allowUninstall, nights, notCounted, pending);
    }

    private static PendingChange? ParsePending(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !TryString(root, "id", out var idText)
            || !Guid.TryParse(idText, out var id)
            || !TryString(root, "field", out var field)
            || !TryValueMembers(root, out var number, out var flag, out var folder, out var dayNames)
            || !root.TryGetProperty("requestedUtc", out var requestedElement)
            || requestedElement.ValueKind != JsonValueKind.String
            || !requestedElement.TryGetDateTime(out var requested)
            || !TryDouble(root, "remainingWaitSeconds", out var remaining))
        {
            return null;
        }

        IReadOnlyList<DayOfWeek>? days = null;
        if (dayNames is not null)
        {
            if (!DayNames.TryParse(dayNames, out var parsed))
            {
                return null;
            }

            days = parsed;
        }

        return new PendingChange(id, field, number, flag, folder, days, requested, remaining);
    }

    private static bool TryNamedFolders(JsonElement root, string name, out IReadOnlyList<NamedFolder> folders)
    {
        folders = [];
        if (!root.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var parsed = new List<NamedFolder>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || !TryString(item, "folder", out var folder) || !TryString(item, "displayName", out var displayName))
            {
                return false;
            }

            parsed.Add(new NamedFolder(folder, displayName));
        }

        folders = parsed;
        return true;
    }

    private static bool TryDayList(JsonElement root, string name, out IReadOnlyList<DayOfWeek> days)
    {
        days = [];
        if (!root.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var names = new List<string>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            names.Add(item.GetString() ?? string.Empty);
        }

        return DayNames.TryParse(names, out days);
    }

    private static bool TryInt(JsonElement element, string name, out int value)
    {
        value = 0;
        return element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out value);
    }

    private static bool TryDouble(JsonElement element, string name, out double value)
    {
        value = 0;
        return element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out value);
    }

    private static bool TryBool(JsonElement element, string name, out bool value)
    {
        value = false;
        if (!element.TryGetProperty(name, out var property) || property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }

        value = property.GetBoolean();
        return true;
    }

    // An absent member or a JSON null is null; anything present must be a number.
    private static bool TryOptionalNumber(JsonElement element, string name, out double? value)
    {
        value = null;
        if (!IsPresent(element, name, out var property))
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.Number || !property.TryGetDouble(out var parsed))
        {
            return false;
        }

        value = parsed;
        return true;
    }

    private static bool TryOptionalString(JsonElement element, string name, out string? value)
    {
        value = null;
        if (!IsPresent(element, name, out var property))
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString();
        return true;
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

    internal static string Write(Action<Utf8JsonWriter> body) => Write(body, default);

    internal static string Write(Action<Utf8JsonWriter> body, JsonWriterOptions options)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, options))
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

/// <summary>Client to server: take the once-a-night extension, for this many minutes. The service decides whether the length is allowed.</summary>
public sealed record ExtendMessage(string RequestId, int Minutes) : PipeMessage
{
    public const string TypeName = "extend";

    public override string Serialize() => Write(writer =>
    {
        writer.WriteString("type", TypeName);
        writer.WriteString("requestId", RequestId);
        writer.WriteNumber("minutes", Minutes);
    });
}

/// <summary>Server to client: the answer to the request, cancel, or extend with this requestId. Pending is the id of the entry the request created, when it did.</summary>
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

/// <summary>A folder under steamapps\common (an installed program or a not-counted entry) and the name to show for it.</summary>
public sealed record NamedFolder(string Folder, string DisplayName);

/// <summary>
/// The rules as the state carries them: GateSettings without its countdown bookkeeping (LockClock), with the not-counted folders
/// named for the screen. Pending entries are the settings' own PendingChange records.
/// </summary>
public sealed record StateSettings(
    int WeeknightMinutes,
    int WeekendNightMinutes,
    int WeeklyCapMinutes,
    int CooldownHours,
    bool Enabled,
    bool AllowUninstall,
    IReadOnlyList<DayOfWeek> WeekendNights,
    IReadOnlyList<NamedFolder> NotCounted,
    IReadOnlyList<PendingChange> Pending)
{
    public bool Equals(StateSettings? other) =>
        other is not null
        && WeeknightMinutes == other.WeeknightMinutes && WeekendNightMinutes == other.WeekendNightMinutes
        && WeeklyCapMinutes == other.WeeklyCapMinutes && CooldownHours == other.CooldownHours
        && Enabled == other.Enabled && AllowUninstall == other.AllowUninstall
        && WeekendNights.SequenceEqual(other.WeekendNights) && NotCounted.SequenceEqual(other.NotCounted)
        && Pending.Count == other.Pending.Count && Pending.Zip(other.Pending).All(pair => StateMessage.SamePending(pair.First, pair.Second));

    public override int GetHashCode() => HashCode.Combine(WeeknightMinutes, WeekendNightMinutes, WeeklyCapMinutes, CooldownHours, Enabled, AllowUninstall, NotCounted.Count, Pending.Count);
}

/// <summary>
/// Server to status screen: where the user stands. Sent when something changed, at most once per tick. Lists compare by value.
/// A state line can be longer than the lines a client sends, so it has its own cap.
/// </summary>
public sealed record StateMessage(
    double? RemainingSeconds,
    int? TonightLimitMinutes,
    bool TonightWeekend,
    double NightSeconds,
    double WeekSeconds,
    string? Counting,
    bool HelperConnected,
    StateSettings Settings,
    IReadOnlyList<NamedFolder> Addable,
    int ExtensionMinutes = 0) : PipeMessage
{
    public const string TypeName = "state";

    /// <summary>The longest state line, in bytes, not counting its newline (the same meaning as PipeLineReader.MaxLineBytes). The status screen and the probe read with this cap.</summary>
    public const int MaxLineBytes = 524288;

    // Relaxed escaping writes text as UTF-8; characters outside the Basic Multilingual Plane, control characters, and a lone surrogate stay escaped.
    private static readonly JsonWriterOptions WriterOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public bool Equals(StateMessage? other) =>
        other is not null
        && RemainingSeconds == other.RemainingSeconds && TonightLimitMinutes == other.TonightLimitMinutes && TonightWeekend == other.TonightWeekend
        && NightSeconds == other.NightSeconds && WeekSeconds == other.WeekSeconds && Counting == other.Counting && HelperConnected == other.HelperConnected
        && ExtensionMinutes == other.ExtensionMinutes && Settings.Equals(other.Settings) && Addable.SequenceEqual(other.Addable);

    public override int GetHashCode() => HashCode.Combine(RemainingSeconds, TonightLimitMinutes, NightSeconds, WeekSeconds, Counting, Settings, Addable.Count, ExtensionMinutes);

    public override string Serialize() => Write(writer =>
    {
        writer.WriteString("type", TypeName);
        WriteNumberOrNull(writer, "remainingSeconds", RemainingSeconds);
        WriteNumberOrNull(writer, "tonightLimitMinutes", TonightLimitMinutes);
        writer.WriteBoolean("tonightWeekend", TonightWeekend);
        writer.WriteNumber("extensionMinutes", ExtensionMinutes);
        writer.WriteNumber("nightSeconds", NightSeconds);
        writer.WriteNumber("weekSeconds", WeekSeconds);
        if (Counting is null)
        {
            writer.WriteNull("counting");
        }
        else
        {
            writer.WriteString("counting", Counting);
        }

        writer.WriteBoolean("helperConnected", HelperConnected);
        writer.WriteStartObject("settings");
        writer.WriteNumber("weeknightMinutes", Settings.WeeknightMinutes);
        writer.WriteNumber("weekendNightMinutes", Settings.WeekendNightMinutes);
        writer.WriteNumber("weeklyCapMinutes", Settings.WeeklyCapMinutes);
        writer.WriteNumber("cooldownHours", Settings.CooldownHours);
        writer.WriteBoolean("enabled", Settings.Enabled);
        writer.WriteBoolean("allowUninstall", Settings.AllowUninstall);
        WriteDays(writer, "weekendNights", Settings.WeekendNights);
        WriteNamedFolders(writer, "notCounted", Settings.NotCounted);
        writer.WriteStartArray("pending");
        foreach (var entry in Settings.Pending)
        {
            WritePending(writer, entry);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        WriteNamedFolders(writer, "addable", Addable);
    }, WriterOptions);

    /// <summary>The bytes one addable entry takes in a state line, so the sender can fit a list into the cap without serializing the whole line again for each entry.</summary>
    internal static int EntryBytes(NamedFolder entry)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            WriteNamedFolder(writer, entry);
        }

        return (int)stream.Length;
    }

    internal static bool SamePending(PendingChange left, PendingChange right) =>
        left.Id == right.Id && left.Field == right.Field && left.Number == right.Number && left.Flag == right.Flag && left.Folder == right.Folder
        && (left.Days is null ? right.Days is null : right.Days is not null && left.Days.SequenceEqual(right.Days))
        && left.RequestedUtc == right.RequestedUtc && left.RemainingWaitSeconds == right.RemainingWaitSeconds;

    private static void WriteNumberOrNull(Utf8JsonWriter writer, string name, double? value)
    {
        if (value is { } number)
        {
            writer.WriteNumber(name, number);
        }
        else
        {
            writer.WriteNull(name);
        }
    }

    private static void WriteDays(Utf8JsonWriter writer, string name, IEnumerable<DayOfWeek> days)
    {
        writer.WriteStartArray(name);
        foreach (var day in days)
        {
            writer.WriteStringValue(day.ToString());
        }

        writer.WriteEndArray();
    }

    private static void WriteNamedFolders(Utf8JsonWriter writer, string name, IEnumerable<NamedFolder> folders)
    {
        writer.WriteStartArray(name);
        foreach (var folder in folders)
        {
            WriteNamedFolder(writer, folder);
        }

        writer.WriteEndArray();
    }

    private static void WriteNamedFolder(Utf8JsonWriter writer, NamedFolder entry)
    {
        writer.WriteStartObject();
        writer.WriteString("folder", entry.Folder);
        writer.WriteString("displayName", entry.DisplayName);
        writer.WriteEndObject();
    }

    private static void WritePending(Utf8JsonWriter writer, PendingChange entry)
    {
        writer.WriteStartObject();
        writer.WriteString("id", entry.Id);
        writer.WriteString("field", entry.Field);
        if (entry.Number is { } number)
        {
            writer.WriteNumber("number", number);
        }

        if (entry.Flag is { } flag)
        {
            writer.WriteBoolean("flag", flag);
        }

        if (entry.Folder is not null)
        {
            writer.WriteString("folder", entry.Folder);
        }

        if (entry.Days is not null)
        {
            WriteDays(writer, "days", entry.Days);
        }

        writer.WriteString("requestedUtc", entry.RequestedUtc);
        writer.WriteNumber("remainingWaitSeconds", entry.RemainingWaitSeconds);
        writer.WriteEndObject();
    }
}
