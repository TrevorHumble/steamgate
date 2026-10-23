using System.Collections.ObjectModel;
using SteamGate.Core.Rules;

namespace SteamGate.Core;

/// <summary>The limits Trevor sets. Any field missing from settings.json takes the value written here.</summary>
public sealed record GateSettings
{
    // The four approved programs, declared once: Steam install folder name (under steamapps\common) and the name shown on screen.
    // Declared first: the static fields below, and the instance initializers, read it.
    private static readonly (string Folder, string ShownName)[] Approved =
    [
        ("Blender", "Blender"),
        ("OBS Studio", "OBS Studio"),
        ("wallpaper_engine", "Wallpaper Engine"),
        ("Steamworks Shared", "Steamworks Shared"),
    ];

    /// <summary>Steam install folder name (under steamapps\common) to the name shown on screen.</summary>
    public static IReadOnlyDictionary<string, string> ApprovedNames { get; } =
        new ReadOnlyDictionary<string, string>(Approved.ToDictionary(entry => entry.Folder, entry => entry.ShownName));

    public static GateSettings Defaults { get; } = new();

    public int WeeknightMinutes { get; init; } = 90;

    public int WeekendNightMinutes { get; init; } = 180;

    public IReadOnlyList<DayOfWeek> WeekendNights { get; init; } = [DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];

    public int WeeklyCapMinutes { get; init; } = 600;

    public int CooldownHours { get; init; } = 48;

    /// <summary>Steam folder names whose running time is not counted. Matching only; show ApprovedNames to the user.</summary>
    public IReadOnlyList<string> NotCounted { get; init; } = [.. Approved.Select(entry => entry.Folder)];

    public bool Enabled { get; init; } = true;

    /// <summary>Loosening changes still waiting out the cooldown (the lock). Added in 0006; a file without it loads as empty.</summary>
    public IReadOnlyList<PendingChange> Pending { get; init; } = [];

    /// <summary>True once a request to allow removal has waited out the cooldown; the uninstall script refuses until it is true.</summary>
    public bool AllowUninstall { get; init; }

    /// <summary>The countdown's bookkeeping. Null until the first save by a version that has the lock.</summary>
    public LockClock? Clock { get; init; }
}
