using SteamGate.Core;
using SteamGate.Core.Pipe;
using SteamGate.Core.Rules;
using SteamGate.Core.Status;

namespace SteamGate.Tests.Status;

/// <summary>Shared builders for the status screen's tests. The default state is the default settings on a weeknight with 47 of 90 minutes used.</summary>
internal static class StateFixtures
{
    public static readonly DateTime Now = new(2026, 10, 20, 21, 14, 0, DateTimeKind.Utc);

    public static StateMessage State(
        GateSettings? settings = null,
        IReadOnlyList<NamedFolder>? addable = null,
        double nightSeconds = 2820,
        double weekSeconds = 22200,
        int? tonightLimit = 90,
        bool weekend = false,
        string? counting = "Cyberpunk 2077",
        double remainingSeconds = 2580) =>
        new(
            RemainingSeconds: remainingSeconds,
            TonightLimitMinutes: tonightLimit,
            TonightWeekend: weekend,
            NightSeconds: nightSeconds,
            WeekSeconds: weekSeconds,
            Counting: counting,
            HelperConnected: true,
            Settings: StatusState.SettingsOf(settings ?? GateSettings.Defaults, []),
            Addable: addable ?? []);

    public static NamedFolder Named(string name) => new(name, name);

    /// <summary>The default settings with the given waiting changes.</summary>
    public static GateSettings WithPending(params PendingChange[] pending) => GateSettings.Defaults with { Pending = pending };
}
