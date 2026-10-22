namespace SteamGate.Core.Steam;

/// <summary>Compares two lists of open game folders.</summary>
public static class GameTransitions
{
    /// <summary>Started: in current only. Stopped: in previous only. Both sorted, folder names compared ignoring case.</summary>
    public static (IReadOnlyList<string> Started, IReadOnlyList<string> Stopped) Diff(IEnumerable<string> previous, IEnumerable<string> current)
    {
        var before = previous.ToList();
        var now = current.ToList();
        return (OnlyIn(now, before), OnlyIn(before, now));
    }

    private static List<string> OnlyIn(List<string> wanted, List<string> other)
    {
        var otherSet = new HashSet<string>(other, StringComparer.OrdinalIgnoreCase);
        return [.. wanted
            .Where(folder => !otherSet.Contains(folder))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)];
    }
}
